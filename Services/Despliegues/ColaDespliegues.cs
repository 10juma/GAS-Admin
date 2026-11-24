using System.Threading.Channels;
using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Services.Despliegues;

/// <summary>
/// Cola de despliegues: se ejecutan de uno en uno, en segundo plano, para que no dependan de que
/// el navegador siga abierto. Si GAS-Admin se reinicia a la mitad, la ejecución queda "Interrumpida".
/// </summary>
public class ColaDespliegues : BackgroundService
{
    private readonly Channel<int> _canal = Channel.CreateUnbounded<int>();
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ColaDespliegues> _logger;

    public ColaDespliegues(IServiceScopeFactory scopes, ILogger<ColaDespliegues> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public void Encolar(int ejecucionId) => _canal.Writer.TryWrite(ejecucionId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MarcarInterrumpidasAsync(stoppingToken);
        await foreach (var id in _canal.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<EjecutorDespliegue>().EjecutarAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado en el despliegue {Id}.", id);
            }
        }
    }

    private async Task MarcarInterrumpidasAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GasDbContext>();
            await db.EjecucionesDespliegue
                .Where(e => e.Estado == EstadoEjecucion.EnCola || e.Estado == EstadoEjecucion.EnCurso)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Estado, EstadoEjecucion.Interrumpido)
                    .SetProperty(e => e.FinUtc, DateTime.UtcNow)
                    .SetProperty(e => e.Resumen, "GAS-Admin se reinició durante el despliegue. Revisa el servidor y vuelve a desplegar."), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "No se pudieron marcar los despliegues interrumpidos.");
        }
    }
}
