using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Integraciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura.Errores;

[Authorize(Policy = Permisos.Infraestructura.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioIntegraciones _integraciones;

    public IndexModel(GasDbContext db, ServicioIntegraciones integraciones)
    {
        _db = db;
        _integraciones = integraciones;
    }

    [BindProperty(SupportsGet = true)] public int? Producto { get; set; }
    [BindProperty(SupportsGet = true)] public int? Proyecto { get; set; }
    /// <summary>todos (por defecto) o nuevos (primera vez en las últimas 24 h).</summary>
    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }

    public bool Configurada { get; private set; }
    public EstadoIntegracion? Estado { get; private set; }
    public List<(int? Id, string Nombre, int SinResolver, int Nuevos)> Productos { get; private set; } = [];
    public List<ErrorSentry> Errores { get; private set; } = [];
    public int SinResolver { get; private set; }
    public int Nuevos { get; private set; }
    public string? ProyectoNombre { get; private set; }
    public DateTime Hace24h { get; } = DateTime.UtcNow.AddHours(-24);

    public async Task OnGetAsync()
    {
        Configurada = _integraciones.Configurada(Integracion.Sentry);
        Estado = await _db.EstadosIntegracion.AsNoTracking().FirstOrDefaultAsync(e => e.Nombre == Integracion.Sentry);

        var proyectos = await _db.ProyectosSentry.AsNoTracking().Include(p => p.Producto).Where(p => p.Existe).ToListAsync();
        Productos = proyectos.GroupBy(p => new { p.ProductoId, Nombre = p.Producto?.Nombre ?? "Sin producto" })
            .Select(g => (g.Key.ProductoId, g.Key.Nombre, g.Sum(p => p.SinResolver), g.Sum(p => p.Nuevos24h)))
            .OrderByDescending(x => x.Item4).ThenByDescending(x => x.Item3).ToList();
        SinResolver = proyectos.Sum(p => p.SinResolver);
        Nuevos = proyectos.Sum(p => p.Nuevos24h);
        ProyectoNombre = Proyecto is null ? null : proyectos.FirstOrDefault(p => p.Id == Proyecto)?.Nombre;

        var q = _db.ErroresSentry.AsNoTracking().Include(e => e.Proyecto).ThenInclude(p => p.Producto).Where(e => e.Proyecto.Existe);
        if (Proyecto is not null) q = q.Where(e => e.ProyectoSentryId == Proyecto);
        else if (Producto is not null) q = q.Where(e => e.Proyecto.ProductoId == Producto);
        if (Vista == "nuevos") q = q.Where(e => e.PrimeraVezUtc >= Hace24h);
        Errores = await q.OrderByDescending(e => e.UltimaVezUtc).Take(300).ToListAsync();
    }
}
