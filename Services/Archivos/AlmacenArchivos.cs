using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace GAS.Admin.Services.Archivos;

public class OpcionesArchivos
{
    /// <summary>Carpeta donde se guardan. Por defecto: App_Data/archivos (fuera de wwwroot).</summary>
    public string? Ruta { get; set; }
    public long TamanoMaximo { get; set; } = 25 * 1024 * 1024;
}

/// <summary>Almacén de archivos subidos. Hoy en disco; se puede cambiar por nube sin tocar las páginas.</summary>
public interface IAlmacenArchivos
{
    /// <returns>Clave interna con la que se guardó.</returns>
    Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct = default);
    Stream? Abrir(string clave);
    void Eliminar(string clave);
}

public partial class AlmacenDisco : IAlmacenArchivos
{
    private readonly string _raiz;

    public AlmacenDisco(IOptions<OpcionesArchivos> opciones, IWebHostEnvironment env)
    {
        _raiz = Path.GetFullPath(string.IsNullOrWhiteSpace(opciones.Value.Ruta)
            ? Path.Combine(env.ContentRootPath, "App_Data", "archivos")
            : opciones.Value.Ruta);
        Directory.CreateDirectory(_raiz);
    }

    // Clave: aaaa/mm/<guid>.<ext> — nunca contiene datos del usuario.
    [GeneratedRegex(@"^\d{4}/\d{2}/[0-9a-f]{32}\.[a-z0-9]{1,10}$")]
    private static partial Regex ClaveValida();

    public async Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        var clave = $"{ahora:yyyy}/{ahora:MM}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var ruta = Ruta(clave);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        await using var destino = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write);
        await contenido.CopyToAsync(destino, ct);
        return clave;
    }

    public Stream? Abrir(string clave)
    {
        var ruta = Ruta(clave);
        return File.Exists(ruta) ? new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    public void Eliminar(string clave)
    {
        var ruta = Ruta(clave);
        if (File.Exists(ruta)) File.Delete(ruta);
    }

    private string Ruta(string clave)
    {
        if (!ClaveValida().IsMatch(clave)) throw new ArgumentException("Clave de archivo inválida.");
        var ruta = Path.GetFullPath(Path.Combine(_raiz, clave));
        if (!ruta.StartsWith(_raiz, StringComparison.Ordinal)) throw new ArgumentException("Ruta fuera del almacén.");
        return ruta;
    }
}

/// <summary>Tipos de archivo permitidos y su tipo de contenido.</summary>
public static class TiposPermitidos
{
    public static readonly IReadOnlyDictionary<string, string> PorExtension = new Dictionary<string, string>
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".csv"] = "text/csv",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".json"] = "application/json",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".zip"] = "application/zip",
    };

    public static string Lista => string.Join(", ", PorExtension.Keys);
}
