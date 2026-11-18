using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Auditoria;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Administracion.Auditoria;

[Authorize(Policy = Permisos.Auditoria.Ver)]
public class IndexModel : PageModel
{
    private const int TamanoPagina = 25;

    private readonly GasDbContext _db;

    public IndexModel(GasDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Accion { get; set; }
    [BindProperty(SupportsGet = true)] public string? Cat { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Desde { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Hasta { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int Pagina { get; set; } = 1;

    public List<AuditLog> Registros { get; private set; } = [];
    public int Total { get; private set; }
    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling(Total / (double)TamanoPagina));

    /// <summary>Total por categoría con los filtros de texto y fechas aplicados.</summary>
    public Dictionary<CategoriaAccion, int> PorCategoria { get; private set; } = [];
    public int TotalSinCategoria { get; private set; }

    public async Task OnGetAsync()
    {
        var query = _db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            query = query.Where(a => a.UsuarioEmail!.Contains(q) || a.Detalle!.Contains(q));
        }

        if (Desde is not null)
        {
            var desdeUtc = Fechas.ALocalUtc(Desde.Value.ToDateTime(TimeOnly.MinValue));
            query = query.Where(a => a.FechaUtc >= desdeUtc);
        }

        if (Hasta is not null)
        {
            var hastaUtc = Fechas.ALocalUtc(Hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(a => a.FechaUtc < hastaUtc);
        }

        // Conteos por categoría (antes de filtrar por categoría o acción).
        var porAccion = await query.GroupBy(a => a.Accion)
            .Select(g => new { Accion = g.Key, Total = g.Count() })
            .ToListAsync();
        TotalSinCategoria = porAccion.Sum(x => x.Total);
        PorCategoria = AccionesAuditoria.Categorias.ToDictionary(
            c => c.Categoria,
            c => porAccion.Where(x => AccionesAuditoria.Info(x.Accion).Categoria == c.Categoria).Sum(x => x.Total));

        if (Enum.TryParse<CategoriaAccion>(Cat, out var categoria))
        {
            var acciones = AccionesAuditoria.DeCategoria(categoria);
            query = query.Where(a => acciones.Contains(a.Accion));
        }

        if (!string.IsNullOrWhiteSpace(Accion))
        {
            query = query.Where(a => a.Accion == Accion);
        }

        Total = await query.CountAsync();
        Pagina = Math.Clamp(Pagina, 1, TotalPaginas);

        Registros = await query
            .OrderByDescending(a => a.FechaUtc)
            .ThenByDescending(a => a.Id)
            .Skip((Pagina - 1) * TamanoPagina)
            .Take(TamanoPagina)
            .ToListAsync();
    }

    public Dictionary<string, string> RutaPagina(int pagina)
    {
        var ruta = new Dictionary<string, string?>
        {
            ["q"] = Q,
            ["accion"] = Accion,
            ["cat"] = Cat,
            ["desde"] = Desde?.ToString("yyyy-MM-dd"),
            ["hasta"] = Hasta?.ToString("yyyy-MM-dd"),
            ["p"] = pagina.ToString()
        };
        return ruta.Where(kv => !string.IsNullOrEmpty(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value!);
    }
}
