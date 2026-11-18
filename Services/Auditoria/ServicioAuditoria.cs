using GAS.Admin.Data.Context;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;

namespace GAS.Admin.Services.Auditoria;

public interface IAuditoria
{
    /// <summary>
    /// Registra una acción en la bitácora. Si no se indica usuario, se toma el de la sesión actual.
    /// Nunca lanza excepción: un fallo de auditoría no debe romper la operación.
    /// </summary>
    Task RegistrarAsync(
        string accion,
        string? detalle = null,
        string? entidad = null,
        string? entidadId = null,
        ApplicationUser? usuario = null,
        string? email = null);
}

public class ServicioAuditoria : IAuditoria
{
    private readonly GasDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<ServicioAuditoria> _logger;

    public ServicioAuditoria(GasDbContext db, IHttpContextAccessor http, ILogger<ServicioAuditoria> logger)
    {
        _db = db;
        _http = http;
        _logger = logger;
    }

    public async Task RegistrarAsync(
        string accion,
        string? detalle = null,
        string? entidad = null,
        string? entidadId = null,
        ApplicationUser? usuario = null,
        string? email = null)
    {
        var context = _http.HttpContext;
        var principal = context?.User;
        var autenticado = principal?.Identity?.IsAuthenticated == true;

        var log = new AuditLog
        {
            FechaUtc = DateTime.UtcNow,
            Accion = accion,
            UsuarioId = usuario?.Id ?? (autenticado ? principal!.UsuarioId() : null),
            UsuarioEmail = Recortar(usuario?.Email ?? email ?? (autenticado ? principal!.Identity!.Name : null), 256),
            Entidad = Recortar(entidad, 100),
            EntidadId = Recortar(entidadId, 100),
            Detalle = Recortar(detalle, 2000),
            Ip = context?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Recortar(context?.Request.Headers.UserAgent.ToString(), 512)
        };

        try
        {
            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _db.Entry(log).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            _logger.LogError(ex, "No se pudo registrar la acción {Accion} en la bitácora.", accion);
        }
    }

    private static string? Recortar(string? valor, int max) =>
        string.IsNullOrEmpty(valor) || valor.Length <= max ? valor : valor[..max];
}
