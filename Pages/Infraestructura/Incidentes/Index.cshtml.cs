using System.ComponentModel.DataAnnotations;
using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Infraestructura;
using GAS.Admin.Services.Navegacion;
using GAS.Admin.Services.Proyectos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura.Incidentes;

public class IncidenteInput
{
    [Required(ErrorMessage = "Describe el incidente en una línea.")]
    [StringLength(200, ErrorMessage = "El título no puede exceder 200 caracteres.")]
    public string Titulo { get; set; } = string.Empty;
    public int? ProductoId { get; set; }
    public int? AmbienteId { get; set; }
    public SeveridadIncidente Severidad { get; set; } = SeveridadIncidente.Media;
    public EstadoIncidente Estado { get; set; } = EstadoIncidente.Abierto;
    public DateTime Inicio { get; set; }
    public DateTime? Fin { get; set; }
    [StringLength(2000, ErrorMessage = "El impacto no puede exceder 2000 caracteres.")]
    public string? Impacto { get; set; }
    [StringLength(2000, ErrorMessage = "La causa no puede exceder 2000 caracteres.")]
    public string? Causa { get; set; }
    [StringLength(4000, ErrorMessage = "Las acciones no pueden exceder 4000 caracteres.")]
    public string? Acciones { get; set; }
    public string? ResponsableId { get; set; }
}

public class IncidenteModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public string? Folio { get; init; }
    public IncidenteInput Input { get; init; } = new();
    public List<Opcion> Productos { get; init; } = [];
    public List<Opcion> Ambientes { get; init; } = [];
    public List<OpcionUsuario> Responsables { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
    public string? Volver { get; init; }
}

[Authorize(Policy = Permisos.Infraestructura.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioInfraestructura _infra;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, ServicioInfraestructura infra, ContadoresMenu contadores)
    {
        _db = db;
        _infra = infra;
        _contadores = contadores;
    }

    /// <summary>abiertos (por defecto), resueltos o todos.</summary>
    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Infraestructura.Gestionar);

    public sealed record Fila(Incidente Incidente, string? Producto, string? Ambiente, string? Responsable);

    public List<Fila> Incidentes { get; private set; } = [];
    public int Abiertos { get; private set; }
    public int Resueltos { get; private set; }
    public int Criticos { get; private set; }
    public TimeSpan? Mttr { get; private set; }
    public IncidenteModalVm? Modal { get; private set; }

    public string VolverActual => Request.Path + Request.QueryString;

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnGetNuevoAsync(int? ambienteId, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var input = new IncidenteInput { Inicio = Fechas.ALocal(DateTime.UtcNow), ResponsableId = User.UsuarioId(), AmbienteId = ambienteId };
        if (ambienteId is not null) input.ProductoId = await _db.Ambientes.Where(a => a.Id == ambienteId).Select(a => (int?)a.ProductoId).FirstOrDefaultAsync();
        return Partial("_IncidenteModal", await ModalAsync(null, input, volver));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id, string? volver)
    {
        var i = await _db.Incidentes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (i is null) return NotFound();
        return Partial("_IncidenteModal", await ModalAsync(i, new IncidenteInput
        {
            Titulo = i.Titulo, ProductoId = i.ProductoId, AmbienteId = i.AmbienteId, Severidad = i.Severidad, Estado = i.Estado,
            Inicio = Fechas.ALocal(i.InicioUtc), Fin = i.FinUtc is null ? null : Fechas.ALocal(i.FinUtc.Value),
            Impacto = i.Impacto, Causa = i.Causa, Acciones = i.Acciones, ResponsableId = i.ResponsableId
        }, volver));
    }

    public async Task<IActionResult> OnPostGuardarAsync(int? id, [Bind(Prefix = "Input")] IncidenteInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var inc = id is null ? new Incidente() : await _db.Incidentes.FirstOrDefaultAsync(x => x.Id == id);
        if (inc is null) return NotFound();

        if (input.AmbienteId is not null)
        {
            var prod = await _db.Ambientes.Where(a => a.Id == input.AmbienteId).Select(a => (int?)a.ProductoId).FirstOrDefaultAsync();
            if (prod is null) input.AmbienteId = null;
            else input.ProductoId = prod;
        }
        if (input.ProductoId is not null && !await _db.Productos.AnyAsync(p => p.Id == input.ProductoId)) input.ProductoId = null;
        if (input.ResponsableId is not null && !await _db.Users.AnyAsync(u => u.Id == input.ResponsableId)) input.ResponsableId = null;
        if (input.Inicio == default) input.Inicio = Fechas.ALocal(DateTime.UtcNow);
        if (input.Estado == EstadoIncidente.Resuelto) input.Fin ??= Fechas.ALocal(DateTime.UtcNow);
        else input.Fin = null;
        if (input.Fin is not null && input.Fin < input.Inicio) ModelState.AddModelError(string.Empty, "La resolución no puede ser antes del inicio.");
        if (Fechas.ALocalUtc(input.Inicio) > DateTime.UtcNow.AddMinutes(5)) ModelState.AddModelError(string.Empty, "El inicio no puede ser futuro.");

        if (!ModelState.IsValid)
        {
            await CargarAsync();
            Modal = await ModalAsync(id is null ? null : inc, input, volver);
            return Page();
        }

        var ambienteAnterior = inc.AmbienteId;
        if (id is null) inc.Folio = await _infra.SiguienteFolioIncidenteAsync(ServicioInfraestructura.Hoy.Year);
        inc.Titulo = input.Titulo.Trim();
        inc.ProductoId = input.ProductoId;
        inc.AmbienteId = input.AmbienteId;
        inc.Severidad = input.Severidad;
        inc.Estado = input.Estado;
        inc.InicioUtc = Fechas.ALocalUtc(input.Inicio);
        inc.FinUtc = input.Fin is null ? null : Fechas.ALocalUtc(input.Fin.Value);
        inc.Impacto = Opciones.Limpio(input.Impacto);
        inc.Causa = Opciones.Limpio(input.Causa);
        inc.Acciones = Opciones.Limpio(input.Acciones);
        inc.ResponsableId = input.ResponsableId;
        if (id is null) _db.Incidentes.Add(inc);

        var estado = await _infra.SincronizarEstadoAsync(inc.AmbienteId, inc);
        if (ambienteAnterior is not null && ambienteAnterior != inc.AmbienteId) await _infra.SincronizarEstadoAsync(ambienteAnterior, inc);
        await _db.GuardarConFolioAsync(async () => inc.Folio = await _infra.SiguienteFolioIncidenteAsync(ServicioInfraestructura.Hoy.Year));
        _contadores.Invalidar();

        this.ToastSuccess(id is null ? $"Incidente {inc.Folio} registrado." : $"Incidente {inc.Folio} actualizado.");
        if (estado is not null) this.ToastInfo($"El ambiente quedó como {Etiquetas.Texto(estado.Value).ToLowerInvariant()}.");
        return this.VolverA(volver, "Index");
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var inc = await _db.Incidentes.FirstOrDefaultAsync(x => x.Id == id);
        if (inc is null) return NotFound();
        (await _db.Monitores.Where(m => m.IncidenteId == id).ToListAsync()).ForEach(m => m.IncidenteId = null);
        _db.Incidentes.Remove(inc);
        await _db.SaveChangesAsync();
        await _infra.SincronizarEstadoAsync(inc.AmbienteId);
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        this.ToastSuccess($"Incidente {inc.Folio} eliminado.");
        return this.VolverA(volver, "Index");
    }

    private async Task<IncidenteModalVm> ModalAsync(Incidente? i, IncidenteInput input, string? volver) => new()
    {
        Id = i?.Id,
        Folio = i?.Folio,
        Input = input,
        Productos = await Listas.ProductosAsync(_db),
        Ambientes = await Listas.AmbientesAsync(_db),
        Responsables = await Listas.ResponsablesAsync(_db, input.ResponsableId),
        PuedeGestionar = PuedeGestionar,
        CreadoUtc = i?.CreadoUtc,
        CreadoPor = i?.CreadoPor,
        ModificadoUtc = i?.ModificadoUtc,
        ModificadoPor = i?.ModificadoPor,
        Volver = volver
    };

    private async Task CargarAsync()
    {
        var todos = await _db.Incidentes.AsNoTracking()
            .Select(i => new Fila(i,
                i.Producto == null ? null : i.Producto.Nombre,
                i.Ambiente == null ? null : i.Ambiente.Nombre,
                i.Responsable == null ? null : (i.Responsable.NombreCompleto == "" ? i.Responsable.Email : i.Responsable.NombreCompleto)))
            .ToListAsync();
        Abiertos = todos.Count(f => f.Incidente.Abierto);
        Resueltos = todos.Count - Abiertos;
        Criticos = todos.Count(f => f.Incidente.Abierto && f.Incidente.Severidad is SeveridadIncidente.Critica or SeveridadIncidente.Alta);
        var desde = DateTime.UtcNow.AddDays(-90);
        var recientes = todos.Where(f => !f.Incidente.Abierto && f.Incidente.FinUtc >= desde).Select(f => f.Incidente.Duracion.TotalMinutes).ToList();
        Mttr = recientes.Count == 0 ? null : TimeSpan.FromMinutes(recientes.Average());

        IEnumerable<Fila> q = Vista switch
        {
            "resueltos" => todos.Where(f => !f.Incidente.Abierto),
            "todos" => todos,
            _ => todos.Where(f => f.Incidente.Abierto)
        };
        Incidentes = q.OrderByDescending(f => f.Incidente.Abierto).ThenByDescending(f => f.Incidente.Severidad).ThenByDescending(f => f.Incidente.InicioUtc).Take(300).ToList();
    }

    public static string Duracion(TimeSpan t) =>
        t.TotalMinutes < 60 ? $"{Math.Max(1, (int)t.TotalMinutes)} min"
        : t.TotalHours < 48 ? $"{t.TotalHours:0.#} h"
        : $"{t.TotalDays:0.#} días";
}
