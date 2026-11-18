using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Navegacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Comercial.Oportunidades;

[Authorize(Policy = Permisos.Comercial.Ver)]
public class IndexModel : PageModel
{
    /// <summary>Cuántas oportunidades cerradas (ganadas/perdidas) se muestran por columna.</summary>
    private const int CerradasVisibles = 15;

    private readonly GasDbContext _db;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, ContadoresMenu contadores)
    {
        _db = db;
        _contadores = contadores;
    }

    /// <summary>"mias" para ver solo las del usuario.</summary>
    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }

    [BindProperty]
    public OportunidadInput Input { get; set; } = new();

    public sealed record Tarjeta(Oportunidad Op, string Cliente, string? Responsable, int Cotizaciones);

    public Dictionary<EtapaOportunidad, List<Tarjeta>> Columnas { get; private set; } = [];
    public Dictionary<EtapaOportunidad, int> TotalPorEtapa { get; private set; } = [];
    public int Abiertas { get; private set; }
    public Dictionary<string, decimal> ValorAbierto { get; private set; } = [];
    public Dictionary<string, decimal> ValorPonderado { get; private set; } = [];
    public decimal? TasaCierre { get; private set; }
    public int Vencidas { get; private set; }
    public int Mias { get; private set; }
    public int Todas { get; private set; }
    public bool PuedeGestionar => User.TienePermiso(Permisos.Comercial.Gestionar);
    public OportunidadModalVm? Modal { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnGetNuevoAsync(EtapaOportunidad? etapa, int? clienteId)
    {
        if (!PuedeGestionar) return Forbid();
        var e = etapa ?? EtapaOportunidad.Nueva;
        var input = new OportunidadInput
        {
            Etapa = e,
            Probabilidad = Oportunidad.ProbabilidadSugerida(e),
            ClienteId = clienteId,
            ResponsableId = User.UsuarioId()
        };
        return Partial("_OportunidadModal", await ModalAsync(null, input));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        var o = await _db.Oportunidades.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (o is null) return NotFound();
        return Partial("_OportunidadModal", await ModalAsync(o, OportunidadInput.Desde(o)));
    }

    public async Task<IActionResult> OnPostCrearAsync()
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarAsync();
        if (ModelState.IsValid)
        {
            var o = new Oportunidad { Etapa = Input.Etapa };
            await AsignarClienteAsync(o);
            Input.AplicarA(o);
            AjustarCierre(o, null);
            o.Orden = await _db.Oportunidades.Where(x => x.Etapa == o.Etapa).Select(x => (int?)x.Orden).MaxAsync() + 1 ?? 0;
            _db.Oportunidades.Add(o);
            await _db.SaveChangesAsync();
            _contadores.Invalidar();
            this.ToastSuccess($"Oportunidad \"{o.Titulo}\" creada.");
            return Volver();
        }
        await CargarAsync();
        Modal = await ModalAsync(null, Input);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var o = await _db.Oportunidades.FirstOrDefaultAsync(x => x.Id == id);
        if (o is null) return NotFound();
        await ValidarAsync();
        if (ModelState.IsValid)
        {
            var etapaAnterior = o.Etapa;
            await AsignarClienteAsync(o);
            Input.AplicarA(o);
            if (o.Etapa != Input.Etapa)
            {
                o.Etapa = Input.Etapa;
                o.Orden = await _db.Oportunidades.Where(x => x.Etapa == o.Etapa).Select(x => (int?)x.Orden).MaxAsync() + 1 ?? 0;
            }
            AjustarCierre(o, etapaAnterior);
            var cambios = _db.ChangeTracker.HasChanges();
            await _db.SaveChangesAsync();
            _contadores.Invalidar();
            if (cambios) this.ToastSuccess($"Oportunidad \"{o.Titulo}\" actualizada.");
            else this.ToastInfo("No hubo cambios que guardar.");
            return Volver();
        }
        await CargarAsync();
        Modal = await ModalAsync(o, Input);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var o = await _db.Oportunidades.FirstOrDefaultAsync(x => x.Id == id);
        if (o is null) return NotFound();
        if (await _db.Cotizaciones.AnyAsync(c => c.OportunidadId == id))
        {
            this.ToastWarning("Tiene cotizaciones ligadas. Márcala como Perdida o Ganada en lugar de eliminarla.");
            return Volver();
        }
        _db.Oportunidades.Remove(o);
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        this.ToastSuccess($"Oportunidad \"{o.Titulo}\" eliminada.");
        return Volver();
    }

    /// <summary>Arrastrar en el Kanban: cambia de etapa y reordena la columna destino.</summary>
    public async Task<IActionResult> OnPostMoverAsync([FromBody] MoverRequest? req)
    {
        if (!PuedeGestionar) return Forbid();
        if (req is null || !Enum.TryParse<EtapaOportunidad>(req.Etapa, out var etapa))
        {
            return BadRequest(new { ok = false, error = "Solicitud inválida." });
        }
        var o = await _db.Oportunidades.FirstOrDefaultAsync(x => x.Id == req.Id);
        if (o is null) return NotFound();

        var etapaAnterior = o.Etapa;
        if (o.Etapa != etapa)
        {
            o.Etapa = etapa;
            o.Probabilidad = Oportunidad.ProbabilidadSugerida(etapa);
            AjustarCierre(o, etapaAnterior);
        }

        var ids = (req.Ids ?? []).Distinct().ToList();
        var enColumna = await _db.Oportunidades.Where(x => ids.Contains(x.Id) && (x.Etapa == etapa || x.Id == o.Id)).ToListAsync();
        foreach (var x in enColumna)
        {
            x.Orden = ids.IndexOf(x.Id);
        }

        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        return new JsonResult(new
        {
            ok = true,
            probabilidad = o.Probabilidad,
            requiereMotivo = o.Etapa == EtapaOportunidad.Perdida && string.IsNullOrWhiteSpace(o.MotivoPerdida),
            editarUrl = Url.Page("Index", "Editar", new { id = o.Id })
        });
    }

    // ---------- Apoyo ----------

    private IActionResult Volver() => RedirectToPage("Index", new { vista = Opciones.Limpio(Vista) });

    private static void AjustarCierre(Oportunidad o, EtapaOportunidad? anterior)
    {
        if (o.Abierta) o.FechaCierreUtc = null;
        else if (anterior is null || anterior != o.Etapa) o.FechaCierreUtc = DateTime.UtcNow;
    }

    private async Task AsignarClienteAsync(Oportunidad o)
    {
        var nombre = Opciones.Limpio(Input.NuevoProspecto);
        if (nombre is null) return;

        var existente = await _db.Clientes.FirstOrDefaultAsync(c => c.Nombre == nombre || c.NombreComercial == nombre);
        if (existente is not null)
        {
            Input.ClienteId = existente.Id;
            return;
        }

        var prospecto = new Cliente { Nombre = nombre, Estado = EstadoCliente.Prospecto, ResponsableId = Opciones.Limpio(Input.ResponsableId) };
        _db.Clientes.Add(prospecto);
        o.Cliente = prospecto;
        Input.ClienteId = null;
        this.ToastInfo($"Se registró a {nombre} como prospecto en Clientes.");
    }

    private async Task ValidarAsync()
    {
        if (Input.ClienteId is null && Opciones.Limpio(Input.NuevoProspecto) is null)
        {
            ModelState.AddModelError(string.Empty, "Elige un cliente o escribe el nombre de un prospecto nuevo.");
        }
        else if (Input.ClienteId is not null && Opciones.Limpio(Input.NuevoProspecto) is null
                 && !await _db.Clientes.AnyAsync(c => c.Id == Input.ClienteId))
        {
            ModelState.AddModelError(string.Empty, "El cliente elegido ya no existe.");
        }
        if (Input.Etapa == EtapaOportunidad.Perdida && Opciones.Limpio(Input.MotivoPerdida) is null)
        {
            ModelState.AddModelError(string.Empty, "Indica el motivo por el que se perdió la oportunidad.");
        }
        if (!OportunidadInput.Monedas.Contains(Input.Moneda))
        {
            ModelState.AddModelError(string.Empty, "Elige una moneda válida.");
        }
        if (Input.ProductoId is not null && !await _db.Productos.AnyAsync(p => p.Id == Input.ProductoId))
        {
            Input.ProductoId = null;
        }
    }

    private async Task<OportunidadModalVm> ModalAsync(Oportunidad? o, OportunidadInput input) => new()
    {
        Id = o?.Id,
        Input = input,
        Clientes = await _db.Clientes.AsNoTracking()
            .OrderBy(c => c.NombreComercial ?? c.Nombre)
            .Select(c => new OpcionCliente(c.Id, c.NombreComercial ?? c.Nombre, c.Estado))
            .ToListAsync(),
        Productos = await _db.Productos.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync(),
        Responsables = await Opciones.ResponsablesAsync(_db, o?.ResponsableId),
        Cotizaciones = o is null ? [] : (await _db.Cotizaciones.AsNoTracking()
                .Where(c => c.OportunidadId == o.Id)
                .OrderByDescending(c => c.Folio).ThenByDescending(c => c.Version)
                .ToListAsync())
            .Select(c => new CotizacionResumen(c.Id, c.FolioCompleto, c.Titulo, c.Estado, c.Total, c.Moneda))
            .ToList(),
        PuedeGestionar = PuedeGestionar,
        CreadoUtc = o?.CreadoUtc,
        CreadoPor = o?.CreadoPor,
        ModificadoUtc = o?.ModificadoUtc,
        ModificadoPor = o?.ModificadoPor,
        Vista = Vista
    };

    private async Task CargarAsync()
    {
        var yo = User.UsuarioId();
        var query = _db.Oportunidades.AsNoTracking();
        Todas = await query.CountAsync(o => o.Etapa != EtapaOportunidad.Ganada && o.Etapa != EtapaOportunidad.Perdida);
        Mias = await query.CountAsync(o => o.ResponsableId == yo && o.Etapa != EtapaOportunidad.Ganada && o.Etapa != EtapaOportunidad.Perdida);
        if (Vista == "mias") query = query.Where(o => o.ResponsableId == yo);

        // Conteo por etapa en SQL; completas solo las abiertas y las últimas cerradas visibles.
        var conteo = await query.GroupBy(o => o.Etapa).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        TotalPorEtapa = Enum.GetValues<EtapaOportunidad>().ToDictionary(e => e, e => conteo.FirstOrDefault(c => c.Key == e)?.N ?? 0);

        async Task<List<Tarjeta>> CargarTarjetas(IQueryable<Oportunidad> q) =>
            (await q.Select(o => new
                {
                    Op = o,
                    Cliente = o.Cliente.NombreComercial ?? o.Cliente.Nombre,
                    Responsable = o.Responsable == null ? null : (o.Responsable.NombreCompleto == "" ? o.Responsable.Email : o.Responsable.NombreCompleto),
                    Cotizaciones = o.Cotizaciones.Count
                }).ToListAsync())
            .Select(d => new Tarjeta(d.Op, d.Cliente, d.Responsable, d.Cotizaciones)).ToList();

        var tarjetas = await CargarTarjetas(query.Where(o => o.Etapa != EtapaOportunidad.Ganada && o.Etapa != EtapaOportunidad.Perdida));
        foreach (var cerrada in new[] { EtapaOportunidad.Ganada, EtapaOportunidad.Perdida })
            tarjetas.AddRange(await CargarTarjetas(query.Where(o => o.Etapa == cerrada).OrderByDescending(o => o.FechaCierreUtc).Take(CerradasVisibles)));

        Columnas = Enum.GetValues<EtapaOportunidad>().ToDictionary(e => e, e =>
        {
            var col = tarjetas.Where(t => t.Op.Etapa == e);
            return e is EtapaOportunidad.Ganada or EtapaOportunidad.Perdida
                ? col.OrderByDescending(t => t.Op.FechaCierreUtc).ToList()
                : col.OrderBy(t => t.Op.Orden).ThenBy(t => t.Op.Id).ToList();
        });

        var abiertas = tarjetas.Where(t => t.Op.Abierta).ToList();
        Abiertas = abiertas.Count;
        ValorAbierto = abiertas.Where(t => t.Op.ValorEstimado is not null).GroupBy(t => t.Op.Moneda)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Op.ValorEstimado!.Value));
        ValorPonderado = abiertas.Where(t => t.Op.ValorPonderado is not null).GroupBy(t => t.Op.Moneda)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Op.ValorPonderado!.Value));

        var ganadas = TotalPorEtapa[EtapaOportunidad.Ganada];
        var perdidas = TotalPorEtapa[EtapaOportunidad.Perdida];
        TasaCierre = ganadas + perdidas == 0 ? null : Math.Round((decimal)ganadas / (ganadas + perdidas), 4);

        var hoy = DateOnly.FromDateTime(GAS.Admin.Helpers.Fechas.ALocal(DateTime.UtcNow));
        Vencidas = abiertas.Count(t => t.Op.FechaCierreEstimada < hoy);
    }
}
