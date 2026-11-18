using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Auditoria;
using GAS.Admin.Services.Productos;
using GAS.Admin.Services.Navegacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Productos.Conexiones;

public class ConexionInput
{
    public int? ProductoId { get; set; }

    [Required(ErrorMessage = "Ingresa la clave de configuración.")]
    [RegularExpression(@"^[a-z0-9-]{2,50}$", ErrorMessage = "La clave solo admite minúsculas, números y guiones (ej. revendo).")]
    public string Clave { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa la URL de la API GAS del producto.")]
    [StringLength(500, ErrorMessage = "La URL no puede exceder 500 caracteres.")]
    public string UrlApi { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La URL no puede exceder 500 caracteres.")]
    public string? UrlSuperAdmin { get; set; }

    public bool Activa { get; set; } = true;
}

public class ConexionModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public ConexionInput Input { get; init; } = new();
    public List<(int Id, string Nombre)> Productos { get; init; } = [];
    public bool SecretoConfigurado { get; init; }
    public bool PuedeGestionar { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
}

public sealed record Dependencia(string Nombre, bool Ok, string? Detalle);
public sealed record MetricaAdicional(string Clave, string Nombre, decimal Valor, string? Unidad);

[Authorize(Policy = Permisos.Productos.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioConexiones _conexiones;
    private readonly IAuditoria _auditoria;
    private readonly IWebHostEnvironment _entorno;
    private readonly ContadoresMenu _contadores;
    private readonly GAS.Admin.Services.Configuracion.ServicioLlaves _llaves;

    public IndexModel(GasDbContext db, ServicioConexiones conexiones, IAuditoria auditoria, IWebHostEnvironment entorno, ContadoresMenu contadores,
        GAS.Admin.Services.Configuracion.ServicioLlaves llaves)
    {
        _contadores = contadores;
        _llaves = llaves;
        _db = db;
        _conexiones = conexiones;
        _auditoria = auditoria;
        _entorno = entorno;
    }

    /// <summary>Localhost, IPs privadas o de enlace local (evita que el servidor consulte su red interna).</summary>
    private static bool EsRedPrivada(string? url)
    {
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        if (u.IsLoopback || u.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)) return true;
        if (!System.Net.IPAddress.TryParse(u.Host, out var ip)) return false;
        var b = ip.MapToIPv4().GetAddressBytes();
        return b[0] == 10 || b[0] == 127 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254) || b[0] == 0;
    }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Productos.Gestionar);

    public sealed record Tarjeta(ConexionProducto Conexion, bool Secreto, List<Dependencia> Dependencias, MetricaProducto? Hoy,
        MetricaProducto? Hace30, List<MetricaAdicional> Adicionales, List<(DateOnly Fecha, int Activos)> Tendencia);

    public List<Tarjeta> Tarjetas { get; private set; } = [];
    public List<string> SinConexion { get; private set; } = [];
    public ConexionModalVm? Modal { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    // ---------- Configuración ----------

    private async Task<List<(int, string)>> ProductosLibresAsync(int? actual) =>
        (await _db.Productos.AsNoTracking().Where(p => p.Id == actual || !_db.ConexionesProducto.Any(c => c.ProductoId == p.Id))
            .OrderBy(p => p.Nombre).Select(p => new { p.Id, p.Nombre }).ToListAsync()).Select(p => (p.Id, p.Nombre)).ToList();

    public async Task<IActionResult> OnGetNuevaAsync() =>
        PuedeGestionar ? Partial("_ConexionModal", new ConexionModalVm { Productos = await ProductosLibresAsync(null), PuedeGestionar = true }) : Forbid();

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        var c = await _db.ConexionesProducto.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        return Partial("_ConexionModal", await VmAsync(c, new ConexionInput
        {
            ProductoId = c.ProductoId, Clave = c.Clave, UrlApi = c.UrlApi, UrlSuperAdmin = c.UrlSuperAdmin, Activa = c.Activa
        }));
    }

    private async Task<ConexionModalVm> VmAsync(ConexionProducto? c, ConexionInput input) => new()
    {
        Id = c?.Id,
        Input = input,
        Productos = await ProductosLibresAsync(input.ProductoId),
        SecretoConfigurado = !string.IsNullOrEmpty(input.Clave) && _conexiones.SecretoConfigurado(input.Clave),
        PuedeGestionar = PuedeGestionar,
        CreadoUtc = c?.CreadoUtc,
        CreadoPor = c?.CreadoPor,
        ModificadoUtc = c?.ModificadoUtc,
        ModificadoPor = c?.ModificadoPor
    };

    private static string? UrlValida(string? url) =>
        !string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp)
            ? url.Trim().TrimEnd('/') : null;

    public async Task<IActionResult> OnPostGuardarAsync(int? id, [Bind(Prefix = "Input")] ConexionInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var c = id is null ? new ConexionProducto() : await _db.ConexionesProducto.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        input.Clave = (input.Clave ?? string.Empty).Trim().ToLowerInvariant();
        if (DetectorSecretos.PareceSecreto(input.Clave))
        {
            ModelState.AddModelError(string.Empty, "Aquí va solo el nombre de la clave (ej. revendo), nunca el secreto.");
            input.Clave = string.Empty;
        }
        if (input.ProductoId is null || !await _db.Productos.AnyAsync(p => p.Id == input.ProductoId))
            ModelState.AddModelError(string.Empty, "Elige el producto.");
        else if (await _db.ConexionesProducto.AnyAsync(x => x.Id != id && x.ProductoId == input.ProductoId))
            ModelState.AddModelError(string.Empty, "Ese producto ya está conectado.");
        if (await _db.ConexionesProducto.AnyAsync(x => x.Id != id && x.Clave == input.Clave))
            ModelState.AddModelError(string.Empty, $"La clave {input.Clave} ya está en uso.");
        var api = UrlValida(input.UrlApi);
        if (api is null) ModelState.AddModelError(string.Empty, "La URL de la API no es válida (ej. https://api.revendo.mx/gas/v1).");
        else if (api.StartsWith("http://") && !api.StartsWith("http://localhost") && !api.StartsWith("http://127.0.0.1"))
            ModelState.AddModelError(string.Empty, "Usa HTTPS para la API del producto.");
        var superAdmin = UrlValida(input.UrlSuperAdmin);
        // Cambiar a dónde se envían peticiones firmadas o tokens de acceso es sensible: solo Admin.
        if (!User.IsInRole(RolesBase.Admin) && (id is null || c.UrlApi != api || c.UrlSuperAdmin != superAdmin || c.Clave != input.Clave))
            ModelState.AddModelError(string.Empty, "Solo un administrador puede conectar productos o cambiar sus URLs y clave.");
        if (!_entorno.IsDevelopment() && (EsRedPrivada(api) || EsRedPrivada(superAdmin)))
            ModelState.AddModelError(string.Empty, "Las URLs deben ser públicas (no se permiten direcciones internas).");
        if (!string.IsNullOrWhiteSpace(input.UrlSuperAdmin) && superAdmin is null) ModelState.AddModelError(string.Empty, "La URL del super-admin no es válida.");

        if (!ModelState.IsValid)
        {
            await CargarAsync();
            Modal = await VmAsync(id is null ? null : c, input);
            return Page();
        }
        c.ProductoId = input.ProductoId!.Value;
        var claveAnterior = id is null ? null : c.Clave;
        c.Clave = input.Clave;
        c.UrlApi = api!;
        c.UrlSuperAdmin = superAdmin;
        c.Activa = input.Activa;
        if (id is null) _db.ConexionesProducto.Add(c);
        await _db.SaveChangesAsync();
        this.ToastSuccess(id is null ? "Producto conectado." : "Conexión actualizada.");
        if (claveAnterior is not null && claveAnterior != c.Clave)
            await _llaves.RenombrarAsync($"Productos:Conexiones:{claveAnterior}:Secreto", $"Productos:Conexiones:{c.Clave}:Secreto", $"Producto · {c.Clave}");
        if (!_conexiones.SecretoConfigurado(c.Clave))
            this.ToastWarning("Falta el secreto compartido: un Admin debe generarlo y capturarlo desde la conexión.");
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar || !User.IsInRole(RolesBase.Admin)) return Forbid();
        var c = await _db.ConexionesProducto.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        _db.ConexionesProducto.Remove(c);
        await _db.SaveChangesAsync();
        await _llaves.QuitarAsync($"Productos:Conexiones:{c.Clave}:Secreto", $"Producto · {c.Clave}");
        this.ToastSuccess("Conexión eliminada junto con su copia local de cuentas y métricas. El producto no se modifica.");
        return RedirectToPage("Index");
    }

    // ---------- Sincronizar y acceso ----------

    public async Task<IActionResult> OnPostSincronizarAsync(int? id)
    {
        if (!PuedeGestionar) return Forbid();
        var resultados = id is null ? await _conexiones.SincronizarTodasAsync() : [await _conexiones.SincronizarAsync(id.Value)];
        _contadores.Invalidar();
        if (resultados.Count == 0) this.ToastInfo("No hay productos conectados activos.");
        foreach (var r in resultados)
        {
            if (r.Exito) this.ToastSuccess($"{r.Producto}: {r.Mensaje}");
            else this.ToastError($"{r.Producto}: {r.Mensaje}");
        }
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostSuperAdminAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await _db.ConexionesProducto.Include(x => x.Producto).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        var url = _conexiones.UrlAccesoSuperAdmin(c, User.Identity?.Name ?? "", User.NombreVisible());
        if (url is null)
        {
            this.ToastWarning("Falta la URL del super-admin o el secreto de la conexión.");
            return RedirectToPage("Index");
        }
        await _auditoria.RegistrarAsync(AccionesAuditoria.AccesoSuperAdmin, detalle: $"Acceso firmado al super-admin de {c.Producto.Nombre}",
            entidad: "Conexión de producto", entidadId: c.Id.ToString());
        return Redirect(url);
    }

    private async Task CargarAsync()
    {
        var conexiones = await _db.ConexionesProducto.AsNoTracking().Include(c => c.Producto).OrderBy(c => c.Producto.Nombre).ToListAsync();
        var ids = conexiones.Select(c => c.Id).ToList();
        var desde = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-60);
        var metricas = await _db.MetricasProducto.AsNoTracking().Where(m => ids.Contains(m.ConexionProductoId) && m.Fecha >= desde)
            .OrderBy(m => m.Fecha).ToListAsync();

        foreach (var c in conexiones)
        {
            var propias = metricas.Where(m => m.ConexionProductoId == c.Id).ToList();
            var hoy = propias.LastOrDefault();
            var hace30 = hoy is null ? null : propias.LastOrDefault(m => m.Fecha <= hoy.Fecha.AddDays(-28));
            var deps = new List<Dependencia>();
            if (c.DependenciasJson is not null)
            {
                try
                {
                    foreach (var d in JsonDocument.Parse(c.DependenciasJson).RootElement.EnumerateArray())
                        deps.Add(new(d.GetProperty("nombre").GetString() ?? "?", d.TryGetProperty("ok", out var ok) && ok.GetBoolean(),
                            d.TryGetProperty("detalle", out var det) && det.ValueKind == JsonValueKind.String ? det.GetString() : null));
                }
                catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { }
            }
            var adicionales = new List<MetricaAdicional>();
            if (hoy?.AdicionalesJson is not null)
            {
                try
                {
                    foreach (var a in JsonDocument.Parse(hoy.AdicionalesJson).RootElement.EnumerateArray().Take(10))
                        adicionales.Add(new(a.GetProperty("clave").GetString() ?? "", a.GetProperty("nombre").GetString() ?? "",
                            a.GetProperty("valor").GetDecimal(), a.TryGetProperty("unidad", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null));
                }
                catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { }
            }
            Tarjetas.Add(new(c, _conexiones.SecretoConfigurado(c.Clave), deps, hoy, hace30, adicionales,
                propias.TakeLast(30).Select(m => (m.Fecha, m.UsuariosActivos24h)).ToList()));
        }
        var conectados = conexiones.Select(c => c.ProductoId).ToHashSet();
        SinConexion = await _db.Productos.AsNoTracking().Where(p => !conectados.Contains(p.Id)).OrderBy(p => p.Nombre).Select(p => p.Nombre).ToListAsync();
    }
}
