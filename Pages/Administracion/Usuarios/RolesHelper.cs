using GAS.Admin.Data.Context;
using GAS.Admin.Security;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Administracion.Usuarios;

public sealed record RolOpcion(string Nombre, string Descripcion);

public static class RolesHelper
{
    public static string Describir(string rol, int permisos) => rol switch
    {
        RolesBase.Admin => "Acceso total a GAS-Admin",
        RolesBase.Desarrollador => "Productos, proyectos e infraestructura",
        RolesBase.Finanzas => "Finanzas, comercial y dirección",
        RolesBase.Soporte => "Atención al cliente y consulta",
        _ => permisos == 1 ? "1 permiso" : $"{permisos} permisos"
    };

    public static async Task<List<RolOpcion>> ObtenerAsync(GasDbContext db)
    {
        var roles = await db.Roles.AsNoTracking()
            .Select(r => new
            {
                r.Name,
                Permisos = db.RoleClaims.Count(c => c.RoleId == r.Id && c.ClaimType == Permisos.ClaimType)
            })
            .ToListAsync();

        return roles
            .Where(r => r.Name is not null)
            .OrderBy(r => r.Name == RolesBase.Admin ? 0 : 1).ThenBy(r => r.Name)
            .Select(r => new RolOpcion(r.Name!, Describir(r.Name!, r.Permisos)))
            .ToList();
    }
}
