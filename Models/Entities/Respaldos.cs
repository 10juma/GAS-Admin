namespace GAS.Admin.Models.Entities;

// Respaldos automáticos de las bases de datos de los productos (y de GAS-Admin) fuera del VPS.
// GAS-Admin entra por SSH al servidor, respalda cada base con BACKUP DATABASE, descarga el .bak,
// lo comprime y cifra, y lo sube a Backblaze B2 con bloqueo (Object Lock) para que no se pueda borrar
// antes de tiempo. Las credenciales de B2 y la llave de cifrado van cifradas en LlavesConfiguracion
// bajo "Respaldos:"; aquí solo queda el historial.

public enum TipoRespaldo { Diario, Semanal, Mensual, Manual }

public enum EstadoRespaldo { EnCurso, Exitoso, ConErrores, Fallido, Interrumpido }

/// <summary>
/// Una corrida de respaldo (todas las bases). No hereda EntidadBase: la bitácora cambia durante la
/// corrida y no debe llenar la auditoría.
/// </summary>
public class EjecucionRespaldo
{
    public int Id { get; set; }
    public TipoRespaldo Tipo { get; set; }
    public EstadoRespaldo Estado { get; set; } = EstadoRespaldo.EnCurso;
    /// <summary>Fecha local (yyyy-MM-dd) de la corrida programada; única para que dos instancias no respalden el mismo día.</summary>
    public string? ClaveProgramada { get; set; }
    public string IniciadoPor { get; set; } = string.Empty;
    public DateTime InicioUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinUtc { get; set; }
    public string? Resumen { get; set; }
    public string Bitacora { get; set; } = string.Empty;
    public List<ArchivoRespaldo> Archivos { get; set; } = [];

    public bool Terminada => Estado != EstadoRespaldo.EnCurso;
}

/// <summary>Un archivo de respaldo (una base) en Backblaze B2.</summary>
public class ArchivoRespaldo
{
    public int Id { get; set; }
    public int EjecucionId { get; set; }
    public EjecucionRespaldo Ejecucion { get; set; } = null!;
    public string Producto { get; set; } = string.Empty;
    public string BaseDatos { get; set; } = string.Empty;
    /// <summary>Nombre del archivo en el bucket (carpeta/archivo).</summary>
    public string Nombre { get; set; } = string.Empty;
    public string? IdArchivoB2 { get; set; }
    /// <summary>Tamaño del .bak original y del archivo comprimido y cifrado que se subió.</summary>
    public long BytesOriginal { get; set; }
    public long Bytes { get; set; }
    /// <summary>SHA-256 del .bak original, para comprobar que lo descifrado es idéntico.</summary>
    public string? Sha256 { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Hasta cuándo se conserva; antes de eso B2 no deja borrarlo.</summary>
    public DateTime RetenerHastaUtc { get; set; }
    public DateTime? BorradoUtc { get; set; }
    public string? Error { get; set; }
}
