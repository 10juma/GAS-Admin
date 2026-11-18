using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Data.Seed;

/// <summary>Categorías financieras base (solo si no hay ninguna).</summary>
public static class FinanzasSeeder
{
    /// <summary>Nombres de categorías que usan los movimientos generados automáticamente.</summary>
    public static class Categorias
    {
        public const string Suscripciones = "Suscripciones SaaS";
        public const string Desarrollo = "Desarrollo y proyectos";
        public const string Recurrentes = "Servicios recurrentes";
        public const string Rentas = "Rentas";
        public const string OtrosIngresos = "Otros ingresos";
        public const string Software = "Servicios en la nube y software";
        public const string Dominios = "Dominios y hosting";
        public const string Subcontratos = "Subcontratos";
        public const string Comisiones = "Comisiones bancarias y de pago";
        public const string Marketing = "Publicidad y marketing";
        public const string Oficina = "Oficina y administración";
        public const string Impuestos = "Impuestos y trámites";
        public const string OtrosGastos = "Otros gastos";
    }

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(FinanzasSeeder));

        try
        {
            var db = provider.GetRequiredService<GasDbContext>();
            if ((await db.Database.GetPendingMigrationsAsync()).Any()) return;
            if (await db.CategoriasFinancieras.AnyAsync()) return;

            var orden = 0;
            CategoriaFinanciera C(string nombre, TipoMovimiento tipo) => new() { Nombre = nombre, Tipo = tipo, Orden = orden++ };

            db.CategoriasFinancieras.AddRange(
                C(Categorias.Suscripciones, TipoMovimiento.Ingreso),
                C(Categorias.Desarrollo, TipoMovimiento.Ingreso),
                C(Categorias.Recurrentes, TipoMovimiento.Ingreso),
                C(Categorias.Rentas, TipoMovimiento.Ingreso),
                C(Categorias.OtrosIngresos, TipoMovimiento.Ingreso),
                C(Categorias.Software, TipoMovimiento.Egreso),
                C(Categorias.Dominios, TipoMovimiento.Egreso),
                C(Categorias.Subcontratos, TipoMovimiento.Egreso),
                C(Categorias.Comisiones, TipoMovimiento.Egreso),
                C(Categorias.Marketing, TipoMovimiento.Egreso),
                C(Categorias.Oficina, TipoMovimiento.Egreso),
                C(Categorias.Impuestos, TipoMovimiento.Egreso),
                C(Categorias.OtrosGastos, TipoMovimiento.Egreso));
            await db.SaveChangesAsync();
            logger.LogInformation("Categorías financieras base creadas.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al sembrar las categorías financieras.");
        }
    }
}
