using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Data.Seed;

/// <summary>
/// Crea el rol Admin y el primer usuario administrador (solo en Development).
/// Lee las credenciales de los user secrets:
///   SeedAdmin:Email y SeedAdmin:Password
/// Si no están configuradas, o faltan migraciones por aplicar, no hace nada.
/// </summary>
public static class AdminSeeder
{
    public const string AdminRole = RolesBase.Admin;

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var config = provider.GetRequiredService<IConfiguration>();
        var logger = provider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(AdminSeeder));

        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation(
                "Sembrado de administrador omitido: SeedAdmin:Email/Password no están configurados.");
            return;
        }

        try
        {
            var db = provider.GetRequiredService<GasDbContext>();
            var pending = await db.Database.GetPendingMigrationsAsync();
            if (pending.Any())
            {
                logger.LogWarning(
                    "Sembrado de administrador omitido: hay migraciones pendientes ({Migraciones}). Aplícalas con 'dotnet ef database update'.",
                    string.Join(", ", pending));
                return;
            }

            var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync(AdminRole))
            {
                await roleManager.CreateAsync(new IdentityRole(AdminRole));
            }

            var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    NombreCompleto = config["SeedAdmin:Nombre"] ?? string.Empty
                };

                var result = await userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    logger.LogError("No se pudo crear el administrador: {Errores}",
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                    return;
                }

                logger.LogInformation("Administrador inicial creado.");
            }

            if (!await userManager.IsInRoleAsync(user, AdminRole))
            {
                await userManager.AddToRoleAsync(user, AdminRole);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al sembrar el administrador inicial.");
        }
    }
}
