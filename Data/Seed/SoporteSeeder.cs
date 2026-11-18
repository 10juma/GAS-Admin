using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Data.Seed;

/// <summary>Tiempos de servicio base por prioridad (solo si no existen).</summary>
public static class SoporteSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SoporteSeeder));
        try
        {
            var db = provider.GetRequiredService<GasDbContext>();
            if ((await db.Database.GetPendingMigrationsAsync()).Any()) return;
            var existentes = await db.SlaPrioridades.Select(s => s.Prioridad).ToListAsync();
            var base_ = new[]
            {
                new SlaPrioridad { Prioridad = Prioridad.Urgente, PrimeraRespuestaHoras = 1, ResolucionHoras = 4 },
                new SlaPrioridad { Prioridad = Prioridad.Alta, PrimeraRespuestaHoras = 4, ResolucionHoras = 24 },
                new SlaPrioridad { Prioridad = Prioridad.Media, PrimeraRespuestaHoras = 24, ResolucionHoras = 72 },
                new SlaPrioridad { Prioridad = Prioridad.Baja, PrimeraRespuestaHoras = 48, ResolucionHoras = 168 },
            };
            var faltan = base_.Where(s => !existentes.Contains(s.Prioridad)).ToList();
            if (faltan.Count == 0) return;
            db.SlaPrioridades.AddRange(faltan);
            await db.SaveChangesAsync();
            logger.LogInformation("Tiempos de servicio base creados.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al sembrar los tiempos de servicio.");
        }
    }
}
