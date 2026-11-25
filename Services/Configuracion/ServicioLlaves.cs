using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Configuracion;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Services.Configuracion;

/// <summary>Estado de una llave para mostrar en pantalla: nunca incluye el valor de un secreto.</summary>
public sealed record EstadoLlave(string Clave, bool Configurada, bool EnBaseDeDatos, bool EsSecreto, string? Pista, string? Valor,
    DateTime? ModificadoUtc, string? ModificadoPor, bool Ilegible);

/// <summary>Captura, cambio y borrado de llaves cifradas (solo Admin).</summary>
public class ServicioLlaves
{
    public const int LargoMaximo = 4000;

    /// <summary>Solo estas rutas se pueden capturar en pantalla (nunca la conexión a la BD ni la llave maestra).</summary>
    private static readonly string[] Permitidas =
        ["Resend:", "Soporte:", "Seguridad:UrlPublica", "Stripe:Claves:", "Integraciones:", "Productos:Conexiones:", "Despliegue:", "Respaldos:"];

    public static bool Permitida(string clave) =>
        Permitidas.Any(p => clave.StartsWith(p, StringComparison.OrdinalIgnoreCase)) && !clave.Contains("..") && clave.Length <= 200;

    private readonly GasDbContext _db;
    private readonly CifradoConfiguracion _cifrado;
    private readonly IConfiguration _config;
    private readonly IAuditoria _auditoria;
    private readonly IHttpContextAccessor _http;

    public ServicioLlaves(GasDbContext db, CifradoConfiguracion cifrado, IConfiguration config, IAuditoria auditoria, IHttpContextAccessor http)
    {
        _db = db;
        _cifrado = cifrado;
        _config = config;
        _auditoria = auditoria;
        _http = http;
    }

    public bool LlaveMaestraDisponible => _cifrado.Disponible;
    public string OrigenLlaveMaestra => _cifrado.Origen;

    /// <summary>Estado de las claves pedidas: si hay valor efectivo y si viene de la BD o de la configuración del servidor.</summary>
    public async Task<Dictionary<string, EstadoLlave>> EstadoAsync(params string[] claves)
    {
        var filas = await _db.LlavesConfiguracion.AsNoTracking().Where(l => claves.Contains(l.Clave)).ToListAsync();
        var ilegibles = ProveedorConfiguracionBd.Ilegibles;
        return claves.ToDictionary(c => c, c =>
        {
            var f = filas.FirstOrDefault(x => string.Equals(x.Clave, c, StringComparison.OrdinalIgnoreCase));
            var efectivo = _config[c];
            var configurada = !string.IsNullOrWhiteSpace(efectivo);
            var esSecreto = f?.EsSecreto ?? true;
            return new EstadoLlave(c, configurada, f is not null, esSecreto,
                f?.Pista ?? (configurada && esSecreto ? Pista(efectivo!) : null),
                esSecreto ? null : efectivo,
                f?.ModificadoUtc, f?.ModificadoPor, ilegibles.Contains(c, StringComparer.OrdinalIgnoreCase));
        }, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Guarda (o reemplaza) una llave. Un valor vacío en un secreto significa "no cambiar".</summary>
    public async Task<string?> GuardarAsync(string clave, string? valor, bool esSecreto, string descripcion)
    {
        if (!Permitida(clave)) throw new ArgumentException($"La llave {clave} no se puede capturar en pantalla.");
        if (!_cifrado.Disponible) return "Falta la llave maestra del servidor (Seguridad:LlaveMaestra); no se puede guardar.";
        valor = valor?.Trim();
        if (string.IsNullOrEmpty(valor)) return esSecreto ? null : await QuitarAsync(clave, descripcion);
        if (valor.Length > LargoMaximo) return "El valor es demasiado largo.";

        var fila = await _db.LlavesConfiguracion.FirstOrDefaultAsync(l => l.Clave == clave);
        var anterior = fila is null ? null : _cifrado.Descifrar(fila.ValorCifrado, fila.Clave);
        if (anterior == valor && fila!.EsSecreto == esSecreto) return null;

        fila ??= _db.LlavesConfiguracion.Add(new LlaveConfiguracion { Clave = clave }).Entity;
        fila.ValorCifrado = _cifrado.Cifrar(valor, clave);
        fila.EsSecreto = esSecreto;
        fila.Pista = esSecreto ? Pista(valor) : null;
        fila.ModificadoUtc = DateTime.UtcNow;
        fila.ModificadoPor = _http.HttpContext?.User.Identity?.Name;
        await _db.SaveChangesAsync();
        ProveedorConfiguracionBd.Recargar();

        await _auditoria.RegistrarAsync(AccionesAuditoria.LlaveActualizada,
            detalle: esSecreto ? $"{descripcion}: llave {(anterior is null ? "capturada" : "reemplazada")} (termina en {fila.Pista})"
                               : $"{descripcion}: {(anterior is null ? "—" : anterior)} → {valor}",
            entidad: "Llave", entidadId: clave);
        return null;
    }

    public async Task<string?> QuitarAsync(string clave, string descripcion)
    {
        var fila = await _db.LlavesConfiguracion.FirstOrDefaultAsync(l => l.Clave == clave);
        if (fila is null) return null;
        _db.LlavesConfiguracion.Remove(fila);
        await _db.SaveChangesAsync();
        ProveedorConfiguracionBd.Recargar();
        await _auditoria.RegistrarAsync(AccionesAuditoria.LlaveActualizada, detalle: $"{descripcion}: llave quitada", entidad: "Llave", entidadId: clave);
        return null;
    }

    /// <summary>Mueve una llave a otra ruta (cuando cambia la clave de una cuenta o conexión). Se vuelve a cifrar con la nueva ruta.</summary>
    public async Task RenombrarAsync(string anterior, string nueva, string descripcion)
    {
        if (string.Equals(anterior, nueva, StringComparison.OrdinalIgnoreCase) || !Permitida(nueva)) return;
        var fila = await _db.LlavesConfiguracion.FirstOrDefaultAsync(l => l.Clave == anterior);
        if (fila is null || _cifrado.Descifrar(fila.ValorCifrado, fila.Clave) is not { } valor) return;
        var ocupada = await _db.LlavesConfiguracion.FirstOrDefaultAsync(l => l.Clave == nueva);
        if (ocupada is not null) _db.LlavesConfiguracion.Remove(ocupada);
        fila.Clave = nueva;
        fila.ValorCifrado = _cifrado.Cifrar(valor, nueva);
        fila.ModificadoUtc = DateTime.UtcNow;
        fila.ModificadoPor = _http.HttpContext?.User.Identity?.Name;
        await _db.SaveChangesAsync();
        ProveedorConfiguracionBd.Recargar();
        await _auditoria.RegistrarAsync(AccionesAuditoria.LlaveActualizada, detalle: $"{descripcion}: llave movida de {anterior} a {nueva}", entidad: "Llave", entidadId: nueva);
    }

    /// <summary>Borra todas las llaves que empiezan con el prefijo (p. ej. al desconectar un producto).</summary>
    public async Task QuitarPrefijoAsync(string prefijo, string descripcion)
    {
        var filas = await _db.LlavesConfiguracion.Where(l => l.Clave.StartsWith(prefijo)).ToListAsync();
        if (filas.Count == 0) return;
        _db.LlavesConfiguracion.RemoveRange(filas);
        await _db.SaveChangesAsync();
        ProveedorConfiguracionBd.Recargar();
        await _auditoria.RegistrarAsync(AccionesAuditoria.LlaveActualizada, detalle: $"{descripcion}: llaves quitadas", entidad: "Llave", entidadId: prefijo);
    }

    private static string Pista(string valor) => valor.Length <= 8 ? "••••" : "••••" + valor[^4..];
}
