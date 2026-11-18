using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Configuracion;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Security;
using GAS.Admin.Services.Configuracion;
using GAS.Admin.Services.Correo;
using GAS.Admin.Services.Proyectos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Organizacion.Configuracion;

public sealed class LlavesModalVm
{
    public GrupoLlaves Grupo { get; init; } = null!;
    public Dictionary<string, EstadoLlave> Estados { get; init; } = [];
    public bool LlaveMaestra { get; init; }
    public string? Volver { get; init; }
}

/// <summary>Una llave de producto o integración para el resumen.</summary>
public sealed record FilaLlave(string Grupo, string Nombre, string Tipo, EstadoLlave Estado, string Donde, string Url);

/// <summary>Configuración de GAS-Admin y edición de todas las llaves cifradas. Solo Admin.</summary>
[Authorize(Roles = RolesBase.Admin)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioLlaves _llaves;
    private readonly PlantillaCorreo _plantilla;
    private readonly IServicioCorreo _correo;

    public IndexModel(GasDbContext db, ServicioLlaves llaves, PlantillaCorreo plantilla, IServicioCorreo correo)
    {
        _db = db;
        _llaves = llaves;
        _plantilla = plantilla;
        _correo = correo;
    }

    public bool CorreoConfigurado => _correo.Configurado;

    // ---------- Vista previa y prueba de los correos ----------

    private static readonly string[] TiposEjemplo = ["cotizacion", "soporte", "contrasena"];

    private ContenidoCorreo Ejemplo(string tipo) => tipo switch
    {
        "soporte" => new("No puedo descargar mi factura", "Ticket TKT-2026-0042",
            MensajeTexto: "Hola Laura,\n\nYa revisamos tu cuenta: la factura de octubre quedó disponible en Configuración › Facturación. Si el botón de descarga sigue sin responder, avísanos y te la enviamos por este medio.",
            Firma: $"{User.NombreVisible()}\nAtención al cliente",
            Nota: "Ticket TKT-2026-0042. Responde a este correo para continuar la conversación."),
        "contrasena" => new("Restablece tu contraseña", "Seguridad",
            MensajeTexto: $"Hola {User.NombreVisible()},\n\nRecibimos una solicitud para restablecer tu contraseña de GAS-Admin. Usa el botón para definir una nueva.",
            Datos: [new("Cuenta", User.Identity?.Name ?? "tu@correo.com"), new("El enlace vence en", "30 minutos")],
            Boton: new("Definir nueva contraseña", $"{Request.Scheme}://{Request.Host}/Account/NuevaPassword?id=ejemplo"),
            Nota: "El enlace solo se puede usar una vez. Si no fuiste tú, ignora este correo: tu contraseña no cambia."),
        _ => new("Cotización COT-2026-0007", "Cotización",
            MensajeTexto: $"Hola Carlos,\n\nTe comparto la cotización COT-2026-0007 para \"Implementación de punto de venta\". La encontrarás adjunta en PDF.\n\nQuedo atento a tus comentarios o dudas.\n\nSaludos,\n{User.NombreVisible()}",
            Datos: [new("Proyecto", "Implementación de punto de venta"), new("Folio", "COT-2026-0007"), new("Vigencia", "18 de diciembre de 2026"), new("Total", "MXN 48,720.00", Destacado: true)],
            Nota: "La cotización completa va adjunta en PDF. Puedes responder directamente a este correo.")
    };

    /// <summary>Muestra un correo de ejemplo tal como lo recibe el destinatario (datos ficticios).</summary>
    public async Task<IActionResult> OnGetVistaCorreoAsync(string? tipo) =>
        Content(await _plantilla.HtmlAsync(Ejemplo(TiposEjemplo.Contains(tipo) ? tipo! : "cotizacion")), "text/html; charset=utf-8");

    /// <summary>Envía al Admin un correo de prueba para confirmar que Resend funciona.</summary>
    public async Task<IActionResult> OnPostCorreoPruebaAsync()
    {
        var para = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(para)) return Forbid();
        if (!_correo.Configurado)
        {
            this.ToastWarning("Primero captura la API key y el remitente de Resend.");
            return RedirectToPage("Index");
        }
        var html = await _plantilla.HtmlAsync(new ContenidoCorreo("Correo de prueba", "Prueba",
            MensajeTexto: $"Hola {User.NombreVisible()},\n\nSi lees esto, GAS-Admin ya puede enviar correos con Resend: cotizaciones, respuestas de soporte y enlaces para recuperar contraseña.",
            Datos: [new("Remitente", _correo.Remitente ?? "—"), new("Enviado", Helpers.Fechas.Formato(DateTime.UtcNow))],
            Nota: "Este correo lo pediste desde Organización › Configuración."));
        var r = await _correo.EnviarAsync(new MensajeCorreo([para], [], "Correo de prueba de GAS-Admin", html,
            "Si lees esto, GAS-Admin ya puede enviar correos con Resend.", [], null));
        if (r.Exito) this.ToastSuccess($"Correo de prueba enviado a {para}. Revisa tu bandeja (y spam).");
        else this.ToastError($"No se pudo enviar: {r.Error}");
        return RedirectToPage("Index");
    }

    public bool LlaveMaestra => _llaves.LlaveMaestraDisponible;
    public string OrigenLlave => _llaves.OrigenLlaveMaestra;
    public IReadOnlyCollection<string> Ilegibles => ProveedorConfiguracionBd.Ilegibles;
    public Dictionary<string, EstadoLlave> Estados { get; private set; } = [];
    public List<FilaLlave> Otras { get; private set; } = [];
    public string UrlWebhook { get; private set; } = "";

    public async Task OnGetAsync()
    {
        Estados = await _llaves.EstadoAsync(CatalogoLlaves.DeGasAdmin.SelectMany(g => g.Campos).Select(c => c.Clave).ToArray());
        var publica = Estados["Seguridad:UrlPublica"].Valor;
        UrlWebhook = $"{(string.IsNullOrWhiteSpace(publica) ? $"{Request.Scheme}://{Request.Host}" : publica.TrimEnd('/'))}/webhooks/resend/correo-entrante";

        var grupos = new List<(GrupoLlaves G, string Tipo, string Donde, string Url)>();
        foreach (var g in CatalogoLlaves.DeIntegraciones)
            grupos.Add((g, "Integración", "Infraestructura › Integraciones", "/Infraestructura/Integraciones"));
        foreach (var c in await _db.CuentasStripe.AsNoTracking().Include(c => c.Producto).OrderBy(c => c.Producto.Nombre).ToListAsync())
            grupos.Add((CatalogoLlaves.Stripe(c.Clave, c.Producto.Nombre), "Stripe", "Finanzas › Configuración", "/Finanzas/Configuracion"));
        foreach (var c in await _db.ConexionesProducto.AsNoTracking().Include(c => c.Producto).OrderBy(c => c.Producto.Nombre).ToListAsync())
            grupos.Add((CatalogoLlaves.Producto(c.Clave, c.Producto.Nombre), "Producto", "Productos › Conexiones", "/Productos/Conexiones"));

        var estados = await _llaves.EstadoAsync(grupos.SelectMany(g => g.G.Campos).Where(c => c.EsSecreto).Select(c => c.Clave).ToArray());
        Otras = grupos.SelectMany(g => g.G.Campos.Where(c => c.EsSecreto)
                .Select(c => new FilaLlave(g.G.Id, g.G.Titulo, g.Tipo, estados[c.Clave], g.Donde, g.Url)))
            .ToList();
    }

    public async Task<IActionResult> OnGetLlavesAsync(string grupo, string? volver)
    {
        var g = await ResolverAsync(grupo);
        if (g is null) return NotFound();
        return Partial("_LlavesModal", new LlavesModalVm
        {
            Grupo = g,
            Estados = await _llaves.EstadoAsync(g.Campos.Select(c => c.Clave).ToArray()),
            LlaveMaestra = LlaveMaestra,
            Volver = volver
        });
    }

    public async Task<IActionResult> OnPostGuardarAsync(string grupo, string?[] valores, string? volver)
    {
        var g = await ResolverAsync(grupo);
        if (g is null) return NotFound();
        if (valores.Length != g.Campos.Count) return BadRequest();

        // Primero se valida todo; si algo falla no se guarda nada.
        var errores = new List<string>();
        for (var i = 0; i < g.Campos.Count; i++)
        {
            var v = valores[i]?.Trim();
            if (string.IsNullOrEmpty(v)) continue;
            var error = v.Length > ServicioLlaves.LargoMaximo ? "es demasiado largo." : g.Campos[i].Validar?.Invoke(v);
            if (error is not null) errores.Add($"{g.Campos[i].Etiqueta}: {error}");
        }
        if (errores.Count > 0)
        {
            this.ToastError(string.Join(" ", errores));
            TempData["GAS.AbrirModal"] = Url.Page("Index", "Llaves", new { grupo, volver });
            return this.VolverA(volver, "Index");
        }

        var cambios = 0;
        for (var i = 0; i < g.Campos.Count; i++)
        {
            var campo = g.Campos[i];
            var v = valores[i]?.Trim();
            if (campo.EsSecreto && string.IsNullOrEmpty(v)) continue; // vacío = no cambiar
            if (await _llaves.GuardarAsync(campo.Clave, v, campo.EsSecreto, $"{g.Titulo} · {campo.Etiqueta}") is { } fallo)
            {
                this.ToastError(fallo);
                return this.VolverA(volver, "Index");
            }
            cambios++;
        }
        this.ToastSuccess(cambios == 0 ? "Sin cambios." : $"{g.Titulo}: configuración guardada. Ya está en uso, sin reiniciar.");
        return this.VolverA(volver, "Index");
    }

    public async Task<IActionResult> OnPostQuitarAsync(string grupo, string clave, string? volver)
    {
        var g = await ResolverAsync(grupo);
        var campo = g?.Campos.FirstOrDefault(c => c.Clave == clave);
        if (g is null || campo is null) return NotFound();
        await _llaves.QuitarAsync(clave, $"{g.Titulo} · {campo.Etiqueta}");
        this.ToastSuccess($"{campo.Etiqueta}: se quitó la llave guardada en GAS-Admin.");
        return this.VolverA(volver, "Index");
    }

    private async Task<GrupoLlaves?> ResolverAsync(string? grupo)
    {
        if (string.IsNullOrWhiteSpace(grupo)) return null;
        if (CatalogoLlaves.Fijo(grupo) is { } fijo) return fijo;
        var partes = grupo.Split(':', 2);
        if (partes.Length != 2) return null;
        var clave = partes[1];
        return partes[0] switch
        {
            "stripe" => await _db.CuentasStripe.AsNoTracking().Where(c => c.Clave == clave)
                .Select(c => CatalogoLlaves.Stripe(c.Clave, c.Producto.Nombre)).FirstOrDefaultAsync(),
            "producto" => await _db.ConexionesProducto.AsNoTracking().Where(c => c.Clave == clave)
                .Select(c => CatalogoLlaves.Producto(c.Clave, c.Producto.Nombre)).FirstOrDefaultAsync(),
            _ => null
        };
    }
}
