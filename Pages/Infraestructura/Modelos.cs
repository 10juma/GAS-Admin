using GAS.Admin.Data.Context;
using GAS.Admin.Services.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura;

public sealed record Opcion(int Id, string Nombre, string? Extra = null);

/// <summary>Listas para los selects de los modales de infraestructura.</summary>
public static class Listas
{
    public static Task<List<Opcion>> ProductosAsync(GasDbContext db) =>
        db.Productos.AsNoTracking().OrderBy(p => p.Nombre).Select(p => new Opcion(p.Id, p.Nombre, null)).ToListAsync();

    public static Task<List<Opcion>> ServiciosAsync(GasDbContext db) =>
        db.ServiciosExternos.AsNoTracking().OrderBy(s => s.Nombre).Select(s => new Opcion(s.Id, s.Nombre, s.Categoria.ToString())).ToListAsync();

    public static Task<List<Opcion>> RepositoriosAsync(GasDbContext db, int? actual = null) =>
        db.Repositorios.AsNoTracking().Where(r => !r.Archivado || r.Id == actual).OrderBy(r => r.Nombre)
            .Select(r => new Opcion(r.Id, r.Nombre, r.ProductoId.ToString())).ToListAsync();

    public static Task<List<Opcion>> AmbientesAsync(GasDbContext db) =>
        db.Ambientes.AsNoTracking().OrderBy(a => a.Producto.Nombre).ThenBy(a => a.Tipo).ThenBy(a => a.Nombre)
            .Select(a => new Opcion(a.Id, a.Producto.Nombre + " · " + a.Nombre, a.ProductoId.ToString())).ToListAsync();

    public static Task<List<OpcionUsuario>> ResponsablesAsync(GasDbContext db, string? actual) => Opciones.ResponsablesAsync(db, actual);

    /// <summary>Acepta solo URLs http(s) bien formadas; si no, null.</summary>
    public static string? Url(string? valor)
    {
        var v = Opciones.Limpio(valor);
        if (v is null) return null;
        if (!v.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !v.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) v = "https://" + v;
        return Uri.TryCreate(v, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps) ? v : null;
    }
}
