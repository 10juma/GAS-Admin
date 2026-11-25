using System.Threading.Channels;
using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Services.Respaldos;

/// <summary>
/// Corre los respaldos en segundo plano, de uno en uno: los que se piden con "Respaldar ahora" y el diario
/// a la hora configurada (centro de México). El diario solo lo programa GAS-Admin de producción: una instancia
/// local (Development) no respalda sola. El índice único por fecha evita que dos instancias lo dupliquen.
/// </summary>
public class ProgramadorRespaldos : BackgroundService
{
    private static readonly TimeSpan Revision = TimeSpan.FromMinutes(1);
    private readonly Channel<int> _canal = Channel.CreateUnbounded<int>();
    private readonly IServiceScopeFactory _scopes;
    private readonly IHostEnvironment _entorno;
    private readonly IConfiguration _config;
    private readonly ILogger<ProgramadorRespaldos> _logger;

    public ProgramadorRespaldos(IServiceScopeFactory scopes, IHostEnvironment entorno, IConfiguration config, ILogger<ProgramadorRespaldos> logger)
    {
        _scopes = scopes;
        _entorno = entorno;
        _config = config;
        _logger = logger;
    }

    /// <summary>¿Esta instancia lanza el respaldo diario? (Respaldos:Programar=false lo apaga.)</summary>
    public bool Programa => !_entorno.IsDevelopment() && !string.Equals(_config["Respaldos:Programar"], "false", StringComparison.OrdinalIgnoreCase);

    public void Encolar(int ejecucionId) => _canal.Writer.TryWrite(ejecucionId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MarcarInterrumpidosAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var espera = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
            {
                espera.CancelAfter(Revision);
                try
                {
                    if (await _canal.Reader.WaitToReadAsync(espera.Token))
                        while (_canal.Reader.TryRead(out var id))
                            await EjecutarAsync(id, stoppingToken);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // pasó el minuto sin respaldos manuales
                }
            }
            if (Programa) await RevisarProgramadoAsync(stoppingToken);
        }
    }

    private async Task RevisarProgramadoAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var servicio = scope.ServiceProvider.GetRequiredService<ServicioRespaldos>();
            if (!servicio.Configurado) return;
            var local = Fechas.ALocal(DateTime.UtcNow);
            if (TimeOnly.FromDateTime(local) < servicio.Hora) return;
            var e = await servicio.CrearProgramadaAsync(DateOnly.FromDateTime(local), ct);
            if (e is null) return;
            _logger.LogInformation("Respaldo programado #{Id} ({Tipo}).", e.Id, e.Tipo);
            await servicio.EjecutarAsync(e.Id, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al revisar el respaldo programado.");
        }
    }

    private async Task EjecutarAsync(int id, CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ServicioRespaldos>().EjecutarAsync(id, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en el respaldo {Id}.", id);
        }
    }

    /// <summary>Un respaldo dura minutos: si sigue "en curso" después de 3 h, esa instancia se detuvo a la mitad.</summary>
    private async Task MarcarInterrumpidosAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GasDbContext>();
            var limite = DateTime.UtcNow.AddHours(-3);
            await db.EjecucionesRespaldo
                .Where(e => e.Estado == EstadoRespaldo.EnCurso && e.InicioUtc < limite)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Estado, EstadoRespaldo.Interrumpido)
                    .SetProperty(e => e.FinUtc, DateTime.UtcNow)
                    .SetProperty(e => e.Resumen, "GAS-Admin se detuvo durante el respaldo."), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "No se pudieron marcar los respaldos interrumpidos.");
        }
    }
}
