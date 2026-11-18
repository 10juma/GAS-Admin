using System.ComponentModel.DataAnnotations;
using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Infraestructura;
using GAS.Admin.Services.Proyectos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura.Ambientes;

public class AmbienteInput
{
    [Required(ErrorMessage = "Ingresa el nombre del ambiente.")]
    [StringLength(150, ErrorMessage = "El nombre no puede exceder 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;
    public int? ProductoId { get; set; }
    public TipoAmbiente Tipo { get; set; } = TipoAmbiente.Produccion;
    public PlataformaAmbiente Plataforma { get; set; } = PlataformaAmbiente.Web;
    [StringLength(500, ErrorMessage = "La URL no puede exceder 500 caracteres.")]
    public string? Url { get; set; }
    public int? HostingId { get; set; }
    public int? RepositorioId { get; set; }
    [StringLength(100, ErrorMessage = "La rama no puede exceder 100 caracteres.")]
    public string? Rama { get; set; }
    public EstadoAmbiente Estado { get; set; } = EstadoAmbiente.Operativo;
    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }
}

public class DespliegueInput
{
    [Required(ErrorMessage = "Ingresa la versión desplegada.")]
    [StringLength(50, ErrorMessage = "La versión no puede exceder 50 caracteres.")]
    public string Version { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public ResultadoDespliegue Resultado { get; set; } = ResultadoDespliegue.Exitoso;
    [StringLength(150, ErrorMessage = "La referencia no puede exceder 150 caracteres.")]
    public string? Referencia { get; set; }
    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }
}

public class AmbienteModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public AmbienteInput Input { get; init; } = new();
    public string? Version { get; init; }
    public DateTime? UltimoDespliegueUtc { get; init; }
    public List<Despliegue> Despliegues { get; init; } = [];
    public int IncidentesAbiertos { get; init; }
    public List<Opcion> Productos { get; init; } = [];
    public List<Opcion> Servicios { get; init; } = [];
    public List<Opcion> Repositorios { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
    public string? Volver { get; init; }
}

public class DespliegueModalVm
{
    public int AmbienteId { get; init; }
    public string Ambiente { get; init; } = string.Empty;
    public string? VersionActual { get; init; }
    public DespliegueInput Input { get; init; } = new();
    public string? Volver { get; init; }
}

[Authorize(Policy = Permisos.Infraestructura.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioInfraestructura _infra;

    public IndexModel(GasDbContext db, ServicioInfraestructura infra)
    {
        _db = db;
        _infra = infra;
    }

    [BindProperty(SupportsGet = true)] public int? Producto { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Infraestructura.Gestionar);

    public sealed record Fila(Ambiente Ambiente, string? Hosting, string? Repositorio, int Incidentes, MonitorUptime? Monitor = null);
    public sealed record Grupo(int ProductoId, string Producto, List<Fila> Ambientes);

    public List<Grupo> Grupos { get; private set; } = [];
    public List<Opcion> Productos { get; private set; } = [];
    public Dictionary<EstadoAmbiente, int> PorEstado { get; private set; } = [];
    public int Total { get; private set; }
    public AmbienteModalVm? Modal { get; private set; }
    public DespliegueModalVm? ModalDespliegue { get; private set; }

    public string VolverActual => Request.Path + Request.QueryString;

    public async Task OnGetAsync() => await CargarAsync();

    // ---------- Ambientes ----------

    public async Task<IActionResult> OnGetNuevoAsync(int? productoId, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        return Partial("_AmbienteModal", await ModalAsync(null, new AmbienteInput { ProductoId = productoId }, volver));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id, string? volver)
    {
        var a = await _db.Ambientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        return Partial("_AmbienteModal", await ModalAsync(a, Desde(a), volver));
    }

    public async Task<IActionResult> OnPostCrearAsync([Bind(Prefix = "Input")] AmbienteInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarAsync(input, null);
        if (ModelState.IsValid)
        {
            var a = new Ambiente();
            Aplicar(a, input);
            _db.Ambientes.Add(a);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"Ambiente {a.Nombre} registrado.");
            return this.VolverA(volver, "Index");
        }
        await CargarAsync();
        Modal = await ModalAsync(null, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id, [Bind(Prefix = "Input")] AmbienteInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var a = await _db.Ambientes.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        await ValidarAsync(input, id);
        if (ModelState.IsValid)
        {
            Aplicar(a, input);
            var cambios = _db.ChangeTracker.HasChanges();
            await _db.SaveChangesAsync();
            // Si hay incidentes abiertos, el estado lo dictan ellos (salvo Mantenimiento o Inactivo).
            if (await _db.Incidentes.AnyAsync(i => i.AmbienteId == a.Id && i.Estado != EstadoIncidente.Resuelto)
                && await _infra.SincronizarEstadoAsync(a.Id) is { } estado)
            {
                await _db.SaveChangesAsync();
                this.ToastInfo($"Por sus incidentes abiertos, el ambiente quedó como {Etiquetas.Texto(estado).ToLowerInvariant()}.");
            }
            if (cambios) this.ToastSuccess($"Ambiente {a.Nombre} actualizado.");
            else this.ToastInfo("No hubo cambios que guardar.");
            return this.VolverA(volver, "Index");
        }
        await CargarAsync();
        Modal = await ModalAsync(a, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var a = await _db.Ambientes.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        if (await _db.Incidentes.AnyAsync(i => i.AmbienteId == id) || await _db.Credenciales.AnyAsync(c => c.AmbienteId == id))
        {
            this.ToastWarning($"{a.Nombre} tiene incidentes o credenciales ligadas. Márcalo como Inactivo para conservar el historial.");
            return this.VolverA(volver, "Index");
        }
        _db.Ambientes.Remove(a);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Ambiente {a.Nombre} eliminado junto con su bitácora de despliegues.");
        return this.VolverA(volver, "Index");
    }

    // ---------- Despliegues ----------

    public async Task<IActionResult> OnGetDespliegueAsync(int ambienteId, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var a = await _db.Ambientes.AsNoTracking().Include(x => x.Producto).FirstOrDefaultAsync(x => x.Id == ambienteId);
        if (a is null) return NotFound();
        return Partial("_DespliegueModal", DespliegueVm(a, new DespliegueInput
        {
            Fecha = Fechas.ALocal(DateTime.UtcNow).AddSeconds(-DateTime.UtcNow.Second),
            Referencia = a.Rama
        }, volver));
    }

    public async Task<IActionResult> OnPostDesplegarAsync(int ambienteId, [Bind(Prefix = "Input")] DespliegueInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var a = await _db.Ambientes.Include(x => x.Producto).FirstOrDefaultAsync(x => x.Id == ambienteId);
        if (a is null) return NotFound();
        if (input.Fecha == default) input.Fecha = Fechas.ALocal(DateTime.UtcNow);
        var fechaUtc = Fechas.ALocalUtc(input.Fecha);
        if (fechaUtc > DateTime.UtcNow.AddMinutes(5)) ModelState.AddModelError(string.Empty, "La fecha del despliegue no puede ser futura.");
        if (ModelState.IsValid)
        {
            var d = new Despliegue
            {
                AmbienteId = a.Id,
                Version = input.Version.Trim(),
                FechaUtc = fechaUtc,
                Resultado = input.Resultado,
                Referencia = Opciones.Limpio(input.Referencia),
                Notas = Opciones.Limpio(input.Notas),
                Realizo = User.NombreVisible()
            };
            _db.Despliegues.Add(d);
            await _infra.ActualizarVersionAsync(a.Id, d);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"Despliegue {d.Version} registrado en {a.Nombre}.");
            if (d.Resultado != ResultadoDespliegue.Exitoso)
                this.ToastWarning("Si afectó a usuarios, registra el incidente para dar seguimiento.");
            return this.VolverA(volver, "Index");
        }
        await CargarAsync();
        ModalDespliegue = DespliegueVm(a, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarDespliegueAsync(int id, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var d = await _db.Despliegues.FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        _db.Despliegues.Remove(d);
        await _db.SaveChangesAsync();
        await _infra.ActualizarVersionAsync(d.AmbienteId);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Despliegue {d.Version} eliminado de la bitácora.");
        return this.VolverA(volver, "Index");
    }

    // ---------- Apoyo ----------

    private static AmbienteInput Desde(Ambiente a) => new()
    {
        Nombre = a.Nombre, ProductoId = a.ProductoId, Tipo = a.Tipo, Plataforma = a.Plataforma, Url = a.Url,
        HostingId = a.HostingId, RepositorioId = a.RepositorioId, Rama = a.Rama, Estado = a.Estado, Notas = a.Notas
    };

    private static void Aplicar(Ambiente a, AmbienteInput i)
    {
        a.Nombre = i.Nombre.Trim();
        a.ProductoId = i.ProductoId!.Value;
        a.Tipo = i.Tipo;
        a.Plataforma = i.Plataforma;
        a.Url = Listas.Url(i.Url);
        a.HostingId = i.HostingId;
        a.RepositorioId = i.RepositorioId;
        a.Rama = Opciones.Limpio(i.Rama);
        a.Estado = i.Estado;
        a.Notas = Opciones.Limpio(i.Notas);
    }

    private async Task ValidarAsync(AmbienteInput i, int? id)
    {
        if (i.ProductoId is null || !await _db.Productos.AnyAsync(p => p.Id == i.ProductoId))
            ModelState.AddModelError(string.Empty, "Elige el producto del ambiente.");
        else if (await _db.Ambientes.AnyAsync(a => a.Id != id && a.ProductoId == i.ProductoId && a.Nombre == i.Nombre.Trim()))
            ModelState.AddModelError(string.Empty, $"El producto ya tiene un ambiente llamado {i.Nombre.Trim()}.");
        if (!string.IsNullOrWhiteSpace(i.Url) && Listas.Url(i.Url) is null)
            ModelState.AddModelError(string.Empty, "La URL no es válida.");
        if (i.HostingId is not null && !await _db.ServiciosExternos.AnyAsync(s => s.Id == i.HostingId)) i.HostingId = null;
        if (i.RepositorioId is not null && !await _db.Repositorios.AnyAsync(r => r.Id == i.RepositorioId)) i.RepositorioId = null;
    }

    private static DespliegueModalVm DespliegueVm(Ambiente a, DespliegueInput input, string? volver) => new()
    {
        AmbienteId = a.Id,
        Ambiente = $"{a.Producto?.Nombre} · {a.Nombre}",
        VersionActual = a.Version,
        Input = input,
        Volver = volver
    };

    private async Task<AmbienteModalVm> ModalAsync(Ambiente? a, AmbienteInput input, string? volver) => new()
    {
        Id = a?.Id,
        Input = input,
        Version = a?.Version,
        UltimoDespliegueUtc = a?.UltimoDespliegueUtc,
        Despliegues = a is null ? [] : await _db.Despliegues.AsNoTracking().Where(d => d.AmbienteId == a.Id)
            .OrderByDescending(d => d.FechaUtc).Take(15).ToListAsync(),
        IncidentesAbiertos = a is null ? 0 : await _db.Incidentes.CountAsync(i => i.AmbienteId == a.Id && i.Estado != EstadoIncidente.Resuelto),
        Productos = await Listas.ProductosAsync(_db),
        Servicios = await Listas.ServiciosAsync(_db),
        Repositorios = await Listas.RepositoriosAsync(_db, input.RepositorioId),
        PuedeGestionar = PuedeGestionar,
        CreadoUtc = a?.CreadoUtc,
        CreadoPor = a?.CreadoPor,
        ModificadoUtc = a?.ModificadoUtc,
        ModificadoPor = a?.ModificadoPor,
        Volver = volver
    };

    private async Task CargarAsync()
    {
        Productos = await Listas.ProductosAsync(_db);
        var abiertos = await _db.Incidentes.Where(i => i.AmbienteId != null && i.Estado != EstadoIncidente.Resuelto)
            .GroupBy(i => i.AmbienteId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var monitores = (await _db.Monitores.AsNoTracking().Where(m => m.Existe && m.AmbienteId != null).ToListAsync())
            .GroupBy(m => m.AmbienteId!.Value).ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.Estado == EstadoMonitor.Caido).First());
        var q = _db.Ambientes.AsNoTracking().AsQueryable();
        var todos = await q.Select(a => new
            {
                A = a,
                Producto = a.Producto.Nombre,
                Hosting = a.Hosting == null ? null : a.Hosting.Nombre,
                Repositorio = a.Repositorio == null ? null : a.Repositorio.Nombre
            })
            .ToListAsync();
        Total = todos.Count;
        PorEstado = todos.GroupBy(x => x.A.Estado).ToDictionary(g => g.Key, g => g.Count());
        Grupos = todos.Where(x => Producto is null || x.A.ProductoId == Producto)
            .GroupBy(x => new { x.A.ProductoId, x.Producto })
            .OrderBy(g => g.Key.Producto)
            .Select(g => new Grupo(g.Key.ProductoId, g.Key.Producto, g
                .OrderBy(x => x.A.Tipo).ThenBy(x => x.A.Plataforma).ThenBy(x => x.A.Nombre)
                .Select(x => new Fila(x.A, x.Hosting, x.Repositorio, abiertos.GetValueOrDefault(x.A.Id), monitores.GetValueOrDefault(x.A.Id))).ToList()))
            .ToList();
    }
}
