using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Data.Seed;

/// <summary>
/// Primeros registros del catálogo: los 5 productos y los 9 servicios externos
/// de Global App Suite. Solo se crean si la tabla está vacía.
/// </summary>
public static class CatalogosSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CatalogosSeeder));

        try
        {
            var db = provider.GetRequiredService<GasDbContext>();
            if ((await db.Database.GetPendingMigrationsAsync()).Any())
            {
                logger.LogWarning("Sembrado de catálogos omitido: hay migraciones pendientes.");
                return;
            }

            if (!await db.Productos.AnyAsync())
            {
                db.Productos.AddRange(
                    new Producto { Nombre = "Revendo", Estado = EstadoProducto.EnOperacion, Modalidad = ModalidadProducto.SaaS, SuperAdmin = EstadoSuperAdmin.Existe },
                    new Producto { Nombre = "Pitazo", Estado = EstadoProducto.EnOperacion, Modalidad = ModalidadProducto.SaaS, SuperAdmin = EstadoSuperAdmin.Existe },
                    new Producto
                    {
                        Nombre = "RentaFest", Estado = EstadoProducto.EnOperacion, Modalidad = ModalidadProducto.PorDefinir,
                        SuperAdmin = EstadoSuperAdmin.PorRevisar, Notas = "No es SaaS; modalidad por documentar."
                    },
                    new Producto { Nombre = "VecindarioDigital", Estado = EstadoProducto.EnDesarrollo, Modalidad = ModalidadProducto.SaaS, SuperAdmin = EstadoSuperAdmin.Existe },
                    new Producto { Nombre = "Togado", Estado = EstadoProducto.EnDesarrollo, Modalidad = ModalidadProducto.SaaS, SuperAdmin = EstadoSuperAdmin.PorRevisar });
                await db.SaveChangesAsync();
                logger.LogInformation("Catálogo de productos inicial creado.");
            }

            if (!await db.ServiciosExternos.AnyAsync())
            {
                db.ServiciosExternos.AddRange(
                    Servicio("Stripe", CategoriaServicio.Pagos, "Suscripciones, clientes, pagos y webhooks", "https://dashboard.stripe.com", PeriodicidadCobro.PorUso),
                    Servicio("GoDaddy", CategoriaServicio.Dominios, "Dominios y renovaciones", "https://dcc.godaddy.com", PeriodicidadCobro.Anual),
                    Servicio("Hostinger", CategoriaServicio.Hosting, "Servidores y alojamiento", "https://hpanel.hostinger.com", PeriodicidadCobro.Mensual),
                    Servicio("GitHub", CategoriaServicio.Desarrollo, "Organizaciones, repositorios y código", "https://github.com", PeriodicidadCobro.Mensual),
                    Servicio("Sentry", CategoriaServicio.Monitoreo, "Errores y rendimiento", "https://sentry.io", PeriodicidadCobro.Mensual),
                    Servicio("UptimeRobot", CategoriaServicio.Disponibilidad, "Monitores y alertas de disponibilidad", "https://dashboard.uptimerobot.com", PeriodicidadCobro.Mensual),
                    Servicio("Firebase", CategoriaServicio.ServiciosMoviles, "Notificaciones y servicios de aplicaciones", "https://console.firebase.google.com", PeriodicidadCobro.PorUso),
                    Servicio("Resend", CategoriaServicio.Correo, "Correos transaccionales", "https://resend.com", PeriodicidadCobro.Mensual),
                    Servicio("Vercel", CategoriaServicio.Despliegue, "Aplicaciones y proyectos alojados", "https://vercel.com", PeriodicidadCobro.Mensual));
                await db.SaveChangesAsync();
                logger.LogInformation("Catálogo de servicios externos inicial creado.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al sembrar los catálogos.");
        }
    }

    private static ServicioExterno Servicio(string nombre, CategoriaServicio categoria, string uso, string panel, PeriodicidadCobro periodicidad) => new()
    {
        Nombre = nombre,
        Categoria = categoria,
        Descripcion = uso,
        UrlPanel = panel,
        Periodicidad = periodicidad,
        Moneda = "USD",
        Estado = EstadoServicio.Activo
    };
}
