using System.Threading.Channels;
using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Services.Despliegues;

/// <summary>
/// Cola de despliegues: se ejecutan de uno en uno, en segundo plano, para que no dependan de que
/// el navegador siga abierto. Si GAS-Admin se reinicia a la mitad y nadie más la continúa, la ejecución
/// queda "Interrumpida" (ver <see cref="MarcarInterrumpidasAsync"/>).
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
        // En paralelo: la revisión tarda hasta 5 min y la cola no debe esperarla.
        _ = MarcarInterrumpidasAsync(stoppingToken);
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

    /// <summary>
    /// Un despliegue "en curso" al arrancar no siempre es huérfano: cuando otra instancia de GAS-Admin (la local)
    /// despliega este mismo GAS-Admin, el contenedor nuevo arranca mientras aquella sigue escribiendo la bitácora.
    /// Por eso solo se marca Interrumpido lo que no avanzó (ni estado ni bitácora) durante toda la ventana de espera.
    /// </summary>
    private async Task MarcarInterrumpidasAsync(CancellationToken ct)
    {
        try
        {
            var pendientes = await LeerPendientesAsync(ct);
            for (var i = 0; i < VentanaInterrupcion / RevisionInterrupcion && pendientes.Count > 0; i++)
            {
                await Task.Delay(RevisionInterrupcion, ct);
                var ahora = await LeerPendientesAsync(ct);
                // Sigue sin cambios solo si continúa pendiente con el mismo estado y la misma bitácora.
                pendientes = pendientes.Where(p => ahora.Contains(p)).ToList();
            }
            if (pendientes.Count == 0) return;

            var ids = pendientes.Select(p => p.Id).ToList();
            _logger.LogWarning("Despliegues {Ids} sin avance desde que arrancó GAS-Admin: se marcan como interrumpidos.", ids);
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GasDbContext>();
            await db.EjecucionesDespliegue
                .Where(e => ids.Contains(e.Id) && (e.Estado == EstadoEjecucion.EnCola || e.Estado == EstadoEjecucion.EnCurso))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Estado, EstadoEjecucion.Interrumpido)
                    .SetProperty(e => e.FinUtc, DateTime.UtcNow)
                    .SetProperty(e => e.Resumen, "GAS-Admin se reinició durante el despliegue. Revisa el servidor y vuelve a desplegar."), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron marcar los despliegues interrumpidos.");
        }
    }

    private static readonly TimeSpan VentanaInterrupcion = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RevisionInterrupcion = TimeSpan.FromSeconds(30);

    private sealed record Pendiente(int Id, EstadoEjecucion Estado, int LargoBitacora);

    private async Task<List<Pendiente>> LeerPendientesAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GasDbContext>();
        return await db.EjecucionesDespliegue.AsNoTracking()
            .Where(e => e.Estado == EstadoEjecucion.EnCola || e.Estado == EstadoEjecucion.EnCurso)
            .Select(e => new Pendiente(e.Id, e.Estado, e.Bitacora.Length))
            .ToListAsync(ct);
    }
}
