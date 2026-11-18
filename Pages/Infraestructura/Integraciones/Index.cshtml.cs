using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Integraciones;
using GAS.Admin.Services.Navegacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura.Integraciones;

public class LigaModalVm
{
    /// <summary>monitor o sentry.</summary>
    public string Tipo { get; init; } = "monitor";
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? Detalle { get; init; }
    public int? Seleccionado { get; init; }
    public List<Opcion> Opciones { get; init; } = [];
}

[Authorize(Policy = Permisos.Infraestructura.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioIntegraciones _integraciones;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, ServicioIntegraciones integraciones, ContadoresMenu contadores)
    {
        _db = db;
        _integraciones = integraciones;
        _contadores = contadores;
    }

    /// <summary>monitores (por defecto), sentry o github.</summary>
    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Infraestructura.Gestionar);

    public sealed record Tarjeta(string Nombre, string Descripcion, string Icono, bool Configurada, EstadoIntegracion? Estado, string Grupo, string Ayuda);

    public List<Tarjeta> Tarjetas { get; private set; } = [];
    public List<MonitorUptime> Monitores { get; private set; } = [];
    public List<ProyectoSentry> Proyectos { get; private set; } = [];
    public List<(RepoGitHub Repo, string Producto)> Repos { get; private set; } = [];
    public int ReposSinGitHub { get; private set; }

    public async Task OnGetAsync()
    {
        var estados = await _db.EstadosIntegracion.AsNoTracking().ToDictionaryAsync(e => e.Nombre);
        Tarjetas =
        [
            new(Integracion.UptimeRobot, "Disponibilidad de cada ambiente. Si un monitor cae, se abre un incidente crítico y se resuelve solo al volver.", "activity",
                _integraciones.Configurada(Integracion.UptimeRobot), estados.GetValueOrDefault(Integracion.UptimeRobot),
                Services.Configuracion.CatalogoLlaves.UptimeRobot.Id,
                "En UptimeRobot › Integrations & API crea una Read-Only API Key."),
            new(Integracion.GitHub, "Último commit, pull requests y releases. Cada release publicado se registra como despliegue en producción.", "git",
                _integraciones.Configurada(Integracion.GitHub), estados.GetValueOrDefault(Integracion.GitHub),
                Services.Configuracion.CatalogoLlaves.GitHub.Id,
                "Crea un fine-grained token con acceso de solo lectura a Contents, Metadata y Pull requests de tus repositorios."),
            new(Integracion.Sentry, "Errores sin resolver por producto, con aviso en el menú cuando aparecen nuevos.", "bug",
                _integraciones.Configurada(Integracion.Sentry), estados.GetValueOrDefault(Integracion.Sentry),
                Services.Configuracion.CatalogoLlaves.Sentry.Id,
                "En Sentry › Settings › Auth Tokens crea un token con permisos project:read y event:read.")
        ];

        Monitores = await _db.Monitores.AsNoTracking().Include(m => m.Ambiente).ThenInclude(a => a!.Producto).Include(m => m.Incidente)
            .Where(m => m.Existe).OrderByDescending(m => m.Estado == EstadoMonitor.Caido).ThenBy(m => m.Nombre).ToListAsync();
        Proyectos = await _db.ProyectosSentry.AsNoTracking().Include(p => p.Producto)
            .Where(p => p.Existe).OrderByDescending(p => p.Nuevos24h).ThenByDescending(p => p.SinResolver).ThenBy(p => p.Nombre).ToListAsync();
        Repos = (await _db.ReposGitHub.AsNoTracking().Include(r => r.Repositorio).ThenInclude(r => r.Producto)
                .OrderBy(r => r.NombreCompleto).ToListAsync())
            .Select(r => (r, r.Repositorio.Producto?.Nombre ?? "—")).ToList();
        ReposSinGitHub = (await _db.Repositorios.AsNoTracking().Where(r => !r.Archivado).Select(r => r.Url).ToListAsync())
            .Count(u => ServicioIntegraciones.NombreGitHub(u) is null);
    }

    public async Task<IActionResult> OnPostSincronizarAsync(string? integracion)
    {
        if (!PuedeGestionar) return Forbid();
        var resultados = integracion is null
            ? await _integraciones.SincronizarTodasAsync()
            : [await _integraciones.SincronizarAsync(integracion)];
        if (resultados.Count == 0) this.ToastInfo("No hay integraciones configuradas todavía.");
        foreach (var r in resultados)
        {
            if (r.Exito) this.ToastSuccess($"{r.Integracion}: {r.Mensaje}");
            else this.ToastError($"{r.Integracion}: {r.Mensaje}");
        }
        _contadores.Invalidar();
        return RedirectToPage("Index", new { vista = Vista });
    }

    public async Task<IActionResult> OnGetLigaAsync(string tipo, int id)
    {
        if (tipo == "sentry")
        {
            var p = await _db.ProyectosSentry.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return NotFound();
            return Partial("_LigaModal", new LigaModalVm
            {
                Tipo = "sentry", Id = id, Nombre = p.Nombre, Detalle = p.Plataforma, Seleccionado = p.ProductoId,
                Opciones = await Listas.ProductosAsync(_db)
            });
        }
        var m = await _db.Monitores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        return Partial("_LigaModal", new LigaModalVm
        {
            Tipo = "monitor", Id = id, Nombre = m.Nombre, Detalle = m.Url, Seleccionado = m.AmbienteId,
            Opciones = await Listas.AmbientesAsync(_db)
        });
    }

    public async Task<IActionResult> OnPostLigarAsync(string tipo, int id, int? destino)
    {
        if (!PuedeGestionar) return Forbid();
        if (tipo == "sentry")
        {
            var p = await _db.ProyectosSentry.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return NotFound();
            p.ProductoId = destino is not null && await _db.Productos.AnyAsync(x => x.Id == destino) ? destino : null;
            await _db.SaveChangesAsync();
            this.ToastSuccess(p.ProductoId is null ? $"{p.Nombre} quedó sin producto." : $"{p.Nombre} quedó ligado a su producto.");
            return RedirectToPage("Index", new { vista = "sentry" });
        }
        var m = await _db.Monitores.FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        m.AmbienteId = destino is not null && await _db.Ambientes.AnyAsync(x => x.Id == destino) ? destino : null;
        await _db.SaveChangesAsync();
        this.ToastSuccess(m.AmbienteId is null ? $"{m.Nombre} quedó sin ambiente." : $"{m.Nombre} quedó ligado al ambiente.");
        return RedirectToPage("Index");
    }
}
