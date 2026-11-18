using System.Security.Claims;
using GAS.Admin.Data.Context;
using GAS.Admin.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Data.Seed;

/// <summary>
/// Crea los roles base si no existen (en cualquier ambiente).
/// Los permisos por defecto solo se asignan al crear el rol, para no
/// sobrescribir lo que se configure después desde la pantalla de roles.
/// </summary>
public static class SeguridadSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SeguridadSeeder));

        try
        {
            var db = provider.GetRequiredService<GasDbContext>();
            var pendientes = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pendientes.Count > 0)
            {
                logger.LogWarning(
                    "Sembrado de roles omitido: hay migraciones pendientes ({Migraciones}). Aplícalas con 'dotnet ef database update'.",
                    string.Join(", ", pendientes));
                return;
            }

            var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var (nombre, permisos) in RolesBase.PermisosPorDefecto)
            {
                if (await roleManager.RoleExistsAsync(nombre))
                {
                    continue;
                }

                var rol = new IdentityRole(nombre);
                await roleManager.CreateAsync(rol);
                foreach (var permiso in permisos)
                {
                    await roleManager.AddClaimAsync(rol, new Claim(Permisos.ClaimType, permiso));
                }
                logger.LogInformation("Rol {Rol} creado con {Total} permisos.", nombre, permisos.Length);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al sembrar los roles base.");
        }
    }
}
