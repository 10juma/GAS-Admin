using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Archivos;
using GAS.Admin.Services.Auditoria;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GAS.Admin.Pages.Proyectos.Archivos;

/// <summary>Subir, descargar y eliminar archivos de trabajos, entregables y tareas.</summary>
[Authorize(Policy = Permisos.Proyectos.Ver)]
[RequestSizeLimit(26 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 26 * 1024 * 1024)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly IAlmacenArchivos _almacen;
    private readonly OpcionesArchivos _opciones;
    private readonly IAuditoria _auditoria;

    public IndexModel(GasDbContext db, IAlmacenArchivos almacen, IOptions<OpcionesArchivos> opciones, IAuditoria auditoria)
    {
        _db = db;
        _almacen = almacen;
        _opciones = opciones.Value;
        _auditoria = auditoria;
    }

    public IActionResult OnGet() => RedirectToPage("/Proyectos/Index");

    public async Task<IActionResult> OnPostSubirAsync(int? trabajoId, int? entregableId, int? tareaId, IFormFile? archivo, string? volver)
    {
        if (!User.TienePermiso(Permisos.Proyectos.Gestionar)) return Forbid();

        if (archivo is null || archivo.Length == 0)
        {
            this.ToastWarning("Elige un archivo para subir.");
            return Volver(volver);
        }
        if (archivo.Length > _opciones.TamanoMaximo)
        {
            this.ToastError($"El archivo pesa más de {_opciones.TamanoMaximo / 1024 / 1024} MB.");
            return Volver(volver);
        }
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!TiposPermitidos.PorExtension.TryGetValue(extension, out var tipo))
        {
            this.ToastError($"Tipo de archivo no permitido. Se aceptan: {TiposPermitidos.Lista}.");
            return Volver(volver);
        }

        // El archivo debe pertenecer a algo que exista.
        if (entregableId is not null)
            trabajoId = await _db.Entregables.Where(e => e.Id == entregableId).Select(e => (int?)e.TrabajoId).FirstOrDefaultAsync() ?? trabajoId;
        if (tareaId is not null && !await _db.Tareas.AnyAsync(t => t.Id == tareaId)) tareaId = null;
        if (trabajoId is not null && !await _db.Trabajos.AnyAsync(t => t.Id == trabajoId)) trabajoId = null;
        if (trabajoId is null && tareaId is null)
        {
            this.ToastError("No se encontró a qué pertenece el archivo.");
            return Volver(volver);
        }

        await using var flujo = archivo.OpenReadStream();
        var clave = await _almacen.GuardarAsync(flujo, extension);
        var nombre = Path.GetFileName(archivo.FileName);
        if (nombre.Length > 255) nombre = nombre[^255..];

        var adjunto = new Adjunto
        {
            TrabajoId = trabajoId, EntregableId = entregableId, TareaId = tareaId,
            NombreOriginal = nombre, Clave = clave, TipoContenido = tipo, Tamano = archivo.Length,
            SubidoPor = User.Identity?.Name
        };
        _db.Adjuntos.Add(adjunto);
        await _db.SaveChangesAsync();
        await _auditoria.RegistrarAsync(AccionesAuditoria.ArchivoSubido, detalle: nombre, entidad: "Archivo", entidadId: adjunto.Id.ToString());

        this.ToastSuccess($"{nombre} subido.");
        return Volver(volver);
    }

    public async Task<IActionResult> OnGetDescargarAsync(int id)
    {
        var a = await _db.Adjuntos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        var flujo = _almacen.Abrir(a.Clave);
        if (flujo is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(flujo, a.TipoContenido, a.NombreOriginal);
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, string? volver)
    {
        if (!User.TienePermiso(Permisos.Proyectos.Gestionar)) return Forbid();
        var a = await _db.Adjuntos.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        _db.Adjuntos.Remove(a);
        await _db.SaveChangesAsync();
        _almacen.Eliminar(a.Clave);
        await _auditoria.RegistrarAsync(AccionesAuditoria.ArchivoEliminado, detalle: a.NombreOriginal, entidad: "Archivo", entidadId: id.ToString());
        this.ToastSuccess($"{a.NombreOriginal} eliminado.");
        return Volver(volver);
    }

    private IActionResult Volver(string? volver) =>
        !string.IsNullOrEmpty(volver) && Url.IsLocalUrl(volver) ? LocalRedirect(volver) : RedirectToPage("/Proyectos/Index");
}
