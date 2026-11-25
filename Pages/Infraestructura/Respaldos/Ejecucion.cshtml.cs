using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Auditoria;
using GAS.Admin.Services.Respaldos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura.Respaldos;

/// <summary>Una corrida de respaldo: bitácora en vivo, archivos subidos y descarga ya descifrada.</summary>
[Authorize(Policy = Permisos.Infraestructura.Ver)]
public class EjecucionModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioRespaldos _respaldos;
    private readonly IAuditoria _auditoria;
    private readonly ILogger<EjecucionModel> _logger;

    public EjecucionModel(GasDbContext db, ServicioRespaldos respaldos, IAuditoria auditoria, ILogger<EjecucionModel> logger)
    {
        _db = db;
        _respaldos = respaldos;
        _auditoria = auditoria;
        _logger = logger;
    }

    public EjecucionRespaldo Ejecucion { get; private set; } = null!;
    public bool EsAdmin => User.IsInRole(RolesBase.Admin);

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var e = await _db.EjecucionesRespaldo.AsNoTracking().Include(x => x.Archivos).FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        Ejecucion = e;
        return Page();
    }

    /// <summary>Estado y bitácora para la actualización en vivo.</summary>
    public async Task<IActionResult> OnGetEstadoAsync(int id)
    {
        var e = await _db.EjecucionesRespaldo.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Estado, x.Bitacora, x.Resumen, x.InicioUtc, x.FinUtc }).FirstOrDefaultAsync();
        if (e is null) return NotFound();
        return new JsonResult(new
        {
            texto = Etiquetas.Texto(e.Estado),
            badge = Etiquetas.Badge(e.Estado),
            terminada = e.Estado != EstadoRespaldo.EnCurso,
            bitacora = e.Bitacora,
            resumen = e.Resumen,
            duracion = Despliegues.EjecucionModel.Duracion((e.FinUtc ?? DateTime.UtcNow) - e.InicioUtc)
        });
    }

    /// <summary>Descarga el .bak original: lo baja de B2, verifica la firma y lo descifra al vuelo (solo Admin, queda auditado).</summary>
    public async Task<IActionResult> OnGetDescargarAsync(int id, int archivo, CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();
        var a = await _db.ArchivosRespaldo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == archivo && x.EjecucionId == id, ct);
        if (a is null || a.IdArchivoB2 is null || a.BorradoUtc is not null) return NotFound();

        await _auditoria.RegistrarAsync(AccionesAuditoria.RespaldoDescargado, $"{a.BaseDatos} · {a.Nombre}", "ArchivoRespaldo", a.Id.ToString());
        var nombre = $"{a.BaseDatos}-{Fechas.ALocal(a.CreadoUtc):yyyyMMdd-HHmmss}.bak";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        Response.ContentType = "application/octet-stream";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{nombre}\"";
        Response.Headers.CacheControl = "no-store";
        try
        {
            await _respaldos.DescargarAsync(a, Response.Body, ct);
        }
        catch (Exception ex) when (ex is ErrorB2 or InvalidOperationException or System.Security.Cryptography.CryptographicException or InvalidDataException)
        {
            _logger.LogWarning(ex, "No se pudo descargar el respaldo {Id}.", a.Id);
            if (Response.HasStarted) { HttpContext.Abort(); return new EmptyResult(); }
            Response.Headers.Remove("Content-Disposition");
            this.ToastError($"No se pudo descargar: {ex.Message}");
            return RedirectToPage("Ejecucion", new { id });
        }
        return new EmptyResult();
    }
}
