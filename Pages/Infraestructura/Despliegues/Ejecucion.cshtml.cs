using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura.Despliegues;

/// <summary>Una ejecución de despliegue automático con su bitácora en vivo.</summary>
[Authorize(Policy = Permisos.Infraestructura.Ver)]
public class EjecucionModel : PageModel
{
    private readonly GasDbContext _db;

    public EjecucionModel(GasDbContext db) => _db = db;

    public EjecucionDespliegue Ejecucion { get; private set; } = null!;
    public List<string> Ambientes { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var e = await _db.EjecucionesDespliegue.AsNoTracking().Include(x => x.Producto).Include(x => x.Servidor).FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        Ejecucion = e;
        var ids = e.IdsAmbientes();
        Ambientes = await _db.Ambientes.AsNoTracking().Where(a => ids.Contains(a.Id)).OrderBy(a => a.Nombre).Select(a => a.Nombre).ToListAsync();
        return Page();
    }

    /// <summary>Estado y bitácora para la actualización en vivo (cada 2 s mientras no termine).</summary>
    public async Task<IActionResult> OnGetEstadoAsync(int id)
    {
        var e = await _db.EjecucionesDespliegue.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Estado, x.Bitacora, x.Resumen, x.Commit, x.MensajeCommit, x.InicioUtc, x.FinUtc })
            .FirstOrDefaultAsync();
        if (e is null) return NotFound();
        return new JsonResult(new
        {
            estado = e.Estado.ToString(),
            texto = Etiquetas.Texto(e.Estado),
            badge = Etiquetas.Badge(e.Estado),
            terminada = e.Estado is not (EstadoEjecucion.EnCola or EstadoEjecucion.EnCurso),
            bitacora = e.Bitacora,
            resumen = e.Resumen,
            commit = e.Commit is null ? null : $"{e.Commit[..7]} · {e.MensajeCommit}",
            duracion = e.InicioUtc is null ? null : Duracion((e.FinUtc ?? DateTime.UtcNow) - e.InicioUtc.Value)
        });
    }

    public static string Duracion(TimeSpan t) => t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes} min {t.Seconds:00} s" : $"{t.Seconds} s";
}
