using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Infraestructura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura;

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

    public bool PuedeGestionar => User.TienePermiso(Permisos.Infraestructura.Gestionar);
    public DateOnly Hoy { get; } = ServicioInfraestructura.Hoy;

    public sealed record ProductoInfra(int Id, string Nombre, List<Ambiente> Ambientes, int Dominios, int Repositorios, int Incidentes);
    public sealed record DespliegueReciente(Despliegue Despliegue, string Ambiente, string Producto);

    public List<ProductoInfra> Productos { get; private set; } = [];
    public List<Vencimiento> Vencimientos { get; private set; } = [];
    public List<Incidente> IncidentesAbiertos { get; private set; } = [];
    public List<DespliegueReciente> Despliegues { get; private set; } = [];
    public int TotalAmbientes { get; private set; }
    public int Operativos { get; private set; }
    public int Vencen30 { get; private set; }
    public int CredencialesAtender { get; private set; }
    public Dictionary<int, MonitorUptime> MonitorPorAmbiente { get; private set; } = [];
    public Dictionary<int, (int SinResolver, int Nuevos)> ErroresPorProducto { get; private set; } = [];
    public int ErroresNuevos { get; private set; }

    public async Task OnGetAsync()
    {
        var ambientes = await _db.Ambientes.AsNoTracking().OrderBy(a => a.Tipo).ThenBy(a => a.Plataforma).ToListAsync();
        TotalAmbientes = ambientes.Count(a => a.Estado != EstadoAmbiente.Inactivo);
        Operativos = ambientes.Count(a => a.Estado == EstadoAmbiente.Operativo);

        IncidentesAbiertos = await _db.Incidentes.AsNoTracking().Include(i => i.Producto).Include(i => i.Ambiente)
            .Where(i => i.Estado != EstadoIncidente.Resuelto).ToListAsync();
        // La severidad se guarda como texto: se ordena en memoria para respetar Crítica > Alta > Media > Baja.
        IncidentesAbiertos = IncidentesAbiertos.OrderByDescending(i => i.Severidad).ThenBy(i => i.InicioUtc).ToList();

        var dominios = await _db.Dominios.Where(d => d.Estado == EstadoDominio.Activo && d.ProductoId != null)
            .GroupBy(d => d.ProductoId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var repos = await _db.Repositorios.Where(r => !r.Archivado && r.ProductoId != null)
            .GroupBy(r => r.ProductoId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);

        Productos = (await _db.Productos.AsNoTracking().OrderBy(p => p.Nombre).Select(p => new { p.Id, p.Nombre }).ToListAsync())
            .Select(p => new ProductoInfra(p.Id, p.Nombre,
                ambientes.Where(a => a.ProductoId == p.Id && a.Estado != EstadoAmbiente.Inactivo).ToList(),
                dominios.GetValueOrDefault(p.Id), repos.GetValueOrDefault(p.Id),
                IncidentesAbiertos.Count(i => i.ProductoId == p.Id)))
            .ToList();

        Vencimientos = await _infra.VencimientosAsync(60);
        Vencen30 = Vencimientos.Count(v => v.Dias(Hoy) <= 30);
        CredencialesAtender = Vencimientos.Where(v => v.Tipo is TipoVencimiento.Rotacion or TipoVencimiento.Credencial && v.Dias(Hoy) <= 30)
            .Select(v => v.Id).Distinct().Count();

        MonitorPorAmbiente = (await _db.Monitores.AsNoTracking().Where(m => m.Existe && m.AmbienteId != null).ToListAsync())
            .GroupBy(m => m.AmbienteId!.Value).ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.Estado == EstadoMonitor.Caido).First());
        var sentry = await _db.ProyectosSentry.AsNoTracking().Where(p => p.Existe && p.ProductoId != null).ToListAsync();
        ErroresPorProducto = sentry.GroupBy(p => p.ProductoId!.Value).ToDictionary(g => g.Key, g => (g.Sum(p => p.SinResolver), g.Sum(p => p.Nuevos24h)));
        ErroresNuevos = sentry.Sum(p => p.Nuevos24h);

        Despliegues = await _db.Despliegues.AsNoTracking().OrderByDescending(d => d.FechaUtc).Take(8)
            .Select(d => new DespliegueReciente(d, d.Ambiente.Nombre, d.Ambiente.Producto.Nombre)).ToListAsync();
    }
}
