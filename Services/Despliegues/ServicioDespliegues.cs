using System.Text;
using System.Text.RegularExpressions;
using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Auditoria;
using GAS.Admin.Services.Configuracion;
using GAS.Admin.Services.Integraciones;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Services.Despliegues;

/// <summary>Configuración y arranque de despliegues automáticos (la ejecución la hace <see cref="EjecutorDespliegue"/>).</summary>
public partial class ServicioDespliegues
{
    public const int LargoMaximoVariables = ServicioLlaves.LargoMaximo;

    private readonly GasDbContext _db;
    private readonly ServicioLlaves _llaves;
    private readonly IConfiguration _config;
    private readonly ColaDespliegues _cola;
    private readonly IAuditoria _auditoria;

    public ServicioDespliegues(GasDbContext db, ServicioLlaves llaves, IConfiguration config, ColaDespliegues cola, IAuditoria auditoria)
    {
        _db = db;
        _llaves = llaves;
        _config = config;
        _cola = cola;
        _auditoria = auditoria;
    }

    // ── Variables de los contenedores (cifradas) ─────────────────────────────

    /// <summary>Variables guardadas del ambiente (nombre → valor). Solo para uso interno: nunca se muestran los valores.</summary>
    public Dictionary<string, string> Variables(int ambienteId) => Leer(_config[ConfiguracionDespliegue.ClaveVariables(ambienteId)]);

    /// <summary>
    /// Mezcla las líneas capturadas con las guardadas: NOMBRE=valor agrega o reemplaza, "NOMBRE=" (vacío) la quita,
    /// las que no se mencionan se conservan. Regresa un error de formato o null.
    /// </summary>
    public async Task<string?> ActualizarVariablesAsync(int ambienteId, string? texto, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var actuales = Variables(ambienteId);
        var n = 0;
        foreach (var (linea, i) in texto.Replace("\r", "").Split('\n').Select((l, i) => (l.Trim(), i + 1)))
        {
            if (linea.Length == 0 || linea.StartsWith('#')) continue;
            var igual = linea.IndexOf('=');
            if (igual <= 0) return $"Línea {i}: usa el formato NOMBRE=valor.";
            var nombre = linea[..igual].Trim();
            if (!NombreVariableRegex().IsMatch(nombre)) return $"Línea {i}: «{nombre}» no es un nombre de variable válido (letras, números y _).";
            var valor = linea[(igual + 1)..];
            if (valor.Length == 0) actuales.Remove(nombre);
            else actuales[nombre] = valor;
            n++;
        }
        if (n == 0) return null;
        var serializado = Escribir(actuales);
        if (serializado.Length > LargoMaximoVariables) return "Las variables son demasiado largas para guardarse.";
        return actuales.Count == 0
            ? await _llaves.QuitarAsync(ConfiguracionDespliegue.ClaveVariables(ambienteId), descripcion)
            : await _llaves.GuardarAsync(ConfiguracionDespliegue.ClaveVariables(ambienteId), serializado, esSecreto: true, descripcion);
    }

    public Task<string?> QuitarVariablesAsync(int ambienteId, string descripcion) =>
        _llaves.QuitarAsync(ConfiguracionDespliegue.ClaveVariables(ambienteId), descripcion);

    public static Dictionary<string, string> Leer(string? texto)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(texto)) return d;
        foreach (var linea in texto.Split('\n'))
        {
            var igual = linea.IndexOf('=');
            if (igual > 0) d[linea[..igual]] = linea[(igual + 1)..];
        }
        return d;
    }

    public static string Escribir(IEnumerable<KeyValuePair<string, string>> variables)
    {
        var sb = new StringBuilder();
        foreach (var (k, v) in variables.OrderBy(x => x.Key, StringComparer.Ordinal)) sb.Append(k).Append('=').Append(v).Append('\n');
        return sb.ToString();
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex NombreVariableRegex();

    // ── Arranque ──────────────────────────────────────────────────────────────

    /// <summary>Ambientes que se pueden desplegar junto con el indicado (mismo producto, servidor y repositorio).</summary>
    public async Task<List<ConfiguracionDespliegue>> GrupoAsync(int ambienteId)
    {
        var c = await _db.ConfiguracionesDespliegue.AsNoTracking().Include(x => x.Ambiente).FirstOrDefaultAsync(x => x.AmbienteId == ambienteId);
        if (c is null) return [];
        return await _db.ConfiguracionesDespliegue.AsNoTracking()
            .Include(x => x.Ambiente).ThenInclude(a => a.Repositorio)
            .Where(x => x.ServidorId == c.ServidorId
                     && x.Ambiente.ProductoId == c.Ambiente.ProductoId
                     && x.Ambiente.RepositorioId == c.Ambiente.RepositorioId
                     && x.Ambiente.Estado != EstadoAmbiente.Inactivo)
            .OrderBy(x => x.Ambiente.Plataforma).ThenBy(x => x.Ambiente.Nombre)
            .ToListAsync();
    }

    /// <summary>Valida y pone en cola un despliegue. Regresa la ejecución o el motivo por el que no se puede.</summary>
    public async Task<(EjecucionDespliegue? Ejecucion, string? Error)> IniciarAsync(List<int> ambienteIds, string referencia, string usuario)
    {
        referencia = referencia.Trim();
        if (ambienteIds.Count == 0) return (null, "Elige al menos un ambiente.");
        if (!ReferenciaRegex().IsMatch(referencia)) return (null, "Escribe una rama, etiqueta o commit válido.");

        var configs = await _db.ConfiguracionesDespliegue.AsNoTracking()
            .Include(x => x.Servidor)
            .Include(x => x.Ambiente).ThenInclude(a => a.Repositorio)
            .Where(x => ambienteIds.Contains(x.AmbienteId))
            .ToListAsync();
        if (configs.Count != ambienteIds.Distinct().Count()) return (null, "Algún ambiente no tiene configurado el despliegue automático.");
        if (configs.Select(c => c.ServidorId).Distinct().Count() > 1
            || configs.Select(c => c.Ambiente.ProductoId).Distinct().Count() > 1
            || configs.Select(c => c.Ambiente.RepositorioId).Distinct().Count() > 1)
            return (null, "Solo se pueden desplegar juntos ambientes del mismo producto, servidor y repositorio.");

        var primero = configs[0];
        if (primero.Servidor.Estado == EstadoServidor.SinPreparar) return (null, $"El servidor {primero.Servidor.Nombre} no está preparado.");
        if (ServicioIntegraciones.NombreGitHub(primero.Ambiente.Repositorio?.Url) is null)
            return (null, "El ambiente necesita un repositorio de GitHub (con su URL) para descargar el código.");
        if (string.IsNullOrWhiteSpace(_config["Integraciones:GitHub:Token"]))
            return (null, "Falta el token de GitHub (Infraestructura › Integraciones); con él se descarga el código.");
        if (configs.Count(c => c.Migraciones) > 1) return (null, "Solo un ambiente del grupo debe aplicar las migraciones.");
        if (await _db.EjecucionesDespliegue.AnyAsync(e => e.ServidorId == primero.ServidorId
                && (e.Estado == EstadoEjecucion.EnCola || e.Estado == EstadoEjecucion.EnCurso)))
            return (null, "Ya hay un despliegue en curso en ese servidor; espera a que termine.");

        var e = new EjecucionDespliegue
        {
            ProductoId = primero.Ambiente.ProductoId,
            ServidorId = primero.ServidorId,
            Ambientes = string.Join(',', configs.Select(c => c.AmbienteId).OrderBy(x => x)),
            Referencia = referencia,
            IniciadoPor = usuario,
            Estado = EstadoEjecucion.EnCola
        };
        _db.EjecucionesDespliegue.Add(e);
        await _db.SaveChangesAsync();
        await _auditoria.RegistrarAsync(AccionesAuditoria.DespliegueIniciado,
            detalle: $"{string.Join(", ", configs.Select(c => c.Ambiente.Nombre))} · {referencia} en {primero.Servidor.Nombre}",
            entidad: "EjecucionDespliegue", entidadId: e.Id.ToString());
        _cola.Encolar(e.Id);
        return (e, null);
    }

    [GeneratedRegex(@"^[A-Za-z0-9._/\-]{1,150}$")]
    private static partial Regex ReferenciaRegex();
}
