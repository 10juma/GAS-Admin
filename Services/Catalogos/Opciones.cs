using GAS.Admin.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Services.Catalogos;

public sealed record OpcionUsuario(string Id, string Nombre);

public static class Opciones
{
    /// <summary>Usuarios activos para elegir responsable (incluye al actual aunque esté inactivo).</summary>
    public static async Task<List<OpcionUsuario>> ResponsablesAsync(GasDbContext db, string? incluirId = null) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Activo || u.Id == incluirId)
            .OrderBy(u => u.NombreCompleto == "" ? u.Email : u.NombreCompleto)
            .Select(u => new OpcionUsuario(u.Id, u.NombreCompleto == "" ? u.Email! : u.NombreCompleto))
            .ToListAsync();

    /// <summary>Normaliza texto opcional: recorta y convierte vacío en null.</summary>
    public static string? Limpio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
