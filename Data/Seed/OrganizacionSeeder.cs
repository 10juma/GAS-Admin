using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Data.Seed;

/// <summary>Registro de la empresa y departamentos base (solo si no existen).</summary>
public static class OrganizacionSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(OrganizacionSeeder));

        try
        {
            var db = provider.GetRequiredService<GasDbContext>();
            if ((await db.Database.GetPendingMigrationsAsync()).Any()) return;

            if (!await db.Empresas.AnyAsync())
            {
                db.Empresas.Add(new Empresa { NombreComercial = "Global App Suite" });
            }

            if (!await db.Departamentos.AnyAsync())
            {
                db.Departamentos.AddRange(
                    new Departamento { Nombre = "Dirección", Color = "c4", Orden = 0, Descripcion = "Dirección compartida de Global App Suite." },
                    new Departamento { Nombre = "Desarrollo", Color = "c2", Orden = 1, Descripcion = "Construcción y mantenimiento de productos y proyectos." },
                    new Departamento { Nombre = "Comercial", Color = "c3", Orden = 2, Descripcion = "Prospección, propuestas y relación con clientes." },
                    new Departamento { Nombre = "Soporte", Color = "c6", Orden = 3, Descripcion = "Atención a clientes e incidencias." },
                    new Departamento { Nombre = "Administración y finanzas", Color = "c1", Orden = 4, Descripcion = "Finanzas, proveedores y operación administrativa." });
            }

            if (db.ChangeTracker.HasChanges())
            {
                await db.SaveChangesAsync();
                logger.LogInformation("Datos iniciales de organización creados.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al sembrar la organización.");
        }
    }
}
