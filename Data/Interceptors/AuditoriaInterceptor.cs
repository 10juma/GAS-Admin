using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GAS.Admin.Data.Interceptors;

/// <summary>
/// Registra en la auditoría cada alta, cambio y baja de las entidades de negocio
/// (EntidadBase), con el detalle de los campos modificados, y llena los datos de
/// creación/modificación. Los cambios en las relaciones con productos se agregan
/// al registro de su padre (cliente o servicio).
/// </summary>
public class AuditoriaInterceptor : SaveChangesInterceptor
{
    // Campos que no se muestran con su valor.
    private static readonly HashSet<string> Ocultos = ["ReferenciaCredenciales", "CostoHora", "GastosDirectos"];
    // Decimales que no son dinero: se muestran siempre. El resto de decimales se marca como monto.
    private static readonly HashSet<string> DecimalesNoMonetarios = ["TasaIva", "Cantidad", "DescuentoPorcentaje", "Horas", "HorasEstimadas", "Uptime30d"];
    private static readonly HashSet<string> Excluidos = ["CreadoUtc", "CreadoPor", "ModificadoUtc", "ModificadoPor"];
    // Campos técnicos que cambian solos (estado de sincronización) y no deben llenar la bitácora.
    private static readonly HashSet<string> ExcluidosPorEntidad =
        ["CuentaStripe.UltimaSincronizacionUtc", "CuentaStripe.UltimoResultado", "CuentaStripe.UltimoError",
         "ConexionProducto.UltimaSincronizacionUtc", "ConexionProducto.UltimoError", "ConexionProducto.EstadoOk",
         "ConexionProducto.Version", "ConexionProducto.Ambiente", "ConexionProducto.LatenciaMs",
         "ConexionProducto.DependenciasJson", "ConexionProducto.CuentasSincronizadasUtc"];

    private readonly IHttpContextAccessor _http;
    private readonly List<Pendiente> _pendientes = [];
    private bool _guardandoAuditoria;

    private sealed class Pendiente
    {
        public required EntityEntry? Entrada { get; init; }
        public required string Accion { get; init; }
        public required string Entidad { get; init; }
        public string? EntidadId { get; set; }
        public required string Detalle { get; init; }
    }

    public AuditoriaInterceptor(IHttpContextAccessor http) => _http = http;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Preparar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Preparar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        GuardarAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await GuardarAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pendientes.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pendientes.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    // ---------- Antes de guardar: calcular qué cambió ----------

    private void Preparar(DbContext? context)
    {
        if (context is null || _guardandoAuditoria) return;
        _pendientes.Clear();

        var usuario = _http.HttpContext?.User;
        var quien = usuario?.Identity?.IsAuthenticated == true ? usuario.Identity.Name : "Sistema";
        var ahora = DateTime.UtcNow;

        var relaciones = CambiosEnRelaciones(context);

        foreach (var entrada in context.ChangeTracker.Entries<EntidadBase>().ToList())
        {
            var entidad = NombreEntidad(entrada.Entity.GetType());
            relaciones.Remove(entrada.Entity, out var cambiosRelacion);

            switch (entrada.State)
            {
                case EntityState.Added:
                    entrada.Entity.CreadoUtc = ahora;
                    entrada.Entity.CreadoPor = quien;
                    _pendientes.Add(new Pendiente
                    {
                        Entrada = entrada,
                        Accion = AccionesAuditoria.RegistroCreado,
                        Entidad = entidad,
                        Detalle = Unir($"{entidad}: {entrada.Entity.DescripcionAuditoria}", cambiosRelacion)
                    });
                    break;

                case EntityState.Modified:
                    var cambios = entrada.Properties
                        .Where(p => p.IsModified && !Excluidos.Contains(p.Metadata.Name)
                                    && !ExcluidosPorEntidad.Contains($"{entrada.Metadata.ClrType.Name}.{p.Metadata.Name}")
                                    && !Equals(p.OriginalValue, p.CurrentValue))
                        .Select(DescribirCambio)
                        .ToList();
                    if (cambiosRelacion is not null) cambios.Add(cambiosRelacion);
                    if (cambios.Count == 0) break;

                    entrada.Entity.ModificadoUtc = ahora;
                    entrada.Entity.ModificadoPor = quien;
                    _pendientes.Add(new Pendiente
                    {
                        Entrada = entrada,
                        Accion = AccionesAuditoria.RegistroActualizado,
                        Entidad = entidad,
                        Detalle = $"{entidad}: {entrada.Entity.DescripcionAuditoria} · {string.Join("; ", cambios)}"
                    });
                    break;

                case EntityState.Deleted:
                    _pendientes.Add(new Pendiente
                    {
                        Entrada = null,
                        Accion = AccionesAuditoria.RegistroEliminado,
                        Entidad = entidad,
                        EntidadId = entrada.Entity.Id.ToString(CultureInfo.InvariantCulture),
                        Detalle = $"{entidad}: {entrada.Entity.DescripcionAuditoria}"
                    });
                    break;

                case EntityState.Unchanged when cambiosRelacion is not null:
                    // Solo cambiaron sus productos: se registra como cambio del padre.
                    entrada.Entity.ModificadoUtc = ahora;
                    entrada.Entity.ModificadoPor = quien;
                    _pendientes.Add(new Pendiente
                    {
                        Entrada = entrada,
                        Accion = AccionesAuditoria.RegistroActualizado,
                        Entidad = entidad,
                        Detalle = $"{entidad}: {entrada.Entity.DescripcionAuditoria} · {cambiosRelacion}"
                    });
                    break;
            }
        }
    }

    /// <summary>Agrupa altas, bajas y cambios de estado de relaciones con productos por su registro padre.</summary>
    private static Dictionary<EntidadBase, string> CambiosEnRelaciones(DbContext context)
    {
        // Los registros nuevos aún no tienen id definitivo: se agrupa por el objeto padre.
        var nombres = new Dictionary<int, string>();
        foreach (var e in context.ChangeTracker.Entries<Producto>())
        {
            nombres.TryAdd(e.Entity.Id, e.Entity.Nombre);
        }
        string Producto(int id) => nombres.TryGetValue(id, out var n) ? n : $"producto #{id}";

        var porPadre = new Dictionary<EntidadBase, List<string>>(ReferenceEqualityComparer.Instance);
        foreach (var entrada in context.ChangeTracker.Entries<IRelacionProducto>())
        {
            var r = entrada.Entity;
            string? texto = entrada.State switch
            {
                EntityState.Added => "+" + Producto(r.ProductoId) + (r.EstadoRelacion is null ? "" : $" ({Traducir(r.EstadoRelacion)})"),
                EntityState.Deleted => "−" + Producto(r.ProductoId),
                EntityState.Modified => DescribirRelacionModificada(entrada, Producto(r.ProductoId)),
                _ => null
            };
            if (texto is null) continue;

            if (BuscarPadre(context, entrada, r) is not { } padre) continue;
            if (!porPadre.TryGetValue(padre, out var lista)) porPadre[padre] = lista = [];
            lista.Add(texto);
        }

        var resultado = new Dictionary<EntidadBase, string>(ReferenceEqualityComparer.Instance);
        foreach (var (padre, cambios) in porPadre)
        {
            resultado[padre] = "Productos: " + string.Join(", ", cambios);
        }
        return resultado;
    }

    /// <summary>
    /// El padre de la relación. Al quitar la relación, EF suelta la referencia
    /// (Padre queda nulo), así que se busca por el valor original de su llave.
    /// </summary>
    private static EntidadBase? BuscarPadre(DbContext context, EntityEntry entrada, IRelacionProducto relacion)
    {
        if (relacion.Padre is { } padre) return padre;

        foreach (var fk in entrada.Metadata.GetForeignKeys())
        {
            var tipo = fk.PrincipalEntityType.ClrType;
            if (tipo == typeof(Producto) || !typeof(EntidadBase).IsAssignableFrom(tipo)) continue;

            var id = entrada.Property(fk.Properties[0].Name).OriginalValue;
            return context.ChangeTracker.Entries<EntidadBase>()
                .FirstOrDefault(e => e.Entity.GetType() == tipo && Equals(e.Entity.Id, id))?.Entity;
        }
        return null;
    }

    private static string? DescribirRelacionModificada(EntityEntry entrada, string producto)
    {
        var cambios = entrada.Properties
            .Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
            .Select(p => $"{Valor(p.OriginalValue)} → {Valor(p.CurrentValue)}")
            .ToList();
        return cambios.Count == 0 ? null : $"{producto} ({string.Join(", ", cambios)})";
    }

    // ---------- Después de guardar: escribir la bitácora ----------

    private async Task GuardarAsync(DbContext? context, CancellationToken ct)
    {
        if (context is not GasDbContext db || _guardandoAuditoria || _pendientes.Count == 0) return;

        var http = _http.HttpContext;
        var usuario = http?.User;
        var autenticado = usuario?.Identity?.IsAuthenticated == true;

        foreach (var p in _pendientes)
        {
            p.EntidadId ??= (p.Entrada?.Entity as EntidadBase)?.Id.ToString(CultureInfo.InvariantCulture);
            db.AuditLogs.Add(new AuditLog
            {
                FechaUtc = DateTime.UtcNow,
                Accion = p.Accion,
                UsuarioId = autenticado ? usuario!.UsuarioId() : null,
                UsuarioEmail = autenticado ? usuario!.Identity!.Name : null,
                Entidad = p.Entidad,
                EntidadId = p.EntidadId,
                Detalle = p.Detalle.Length > 2000 ? p.Detalle[..2000] : p.Detalle,
                Ip = http?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Recortar(http?.Request.Headers.UserAgent.ToString(), 512)
            });
        }
        _pendientes.Clear();

        _guardandoAuditoria = true;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            _guardandoAuditoria = false;
        }
    }

    // ---------- Formato ----------

    private static string DescribirCambio(PropertyEntry p)
    {
        var nombre = p.Metadata.PropertyInfo?.GetCustomAttribute<DisplayAttribute>()?.Name ?? p.Metadata.Name;
        if (Ocultos.Contains(p.Metadata.Name)) return $"{nombre} (actualizada)";
        if (p.Metadata.IsForeignKey()) return $"{nombre} (cambió)";
        var cambio = $"{Valor(p.OriginalValue)} → {Valor(p.CurrentValue)}";
        // Los montos van marcados; la bitácora los oculta a quien no tiene Finanzas.Ver.
        var tipo = Nullable.GetUnderlyingType(p.Metadata.ClrType) ?? p.Metadata.ClrType;
        if (tipo == typeof(decimal) && !DecimalesNoMonetarios.Contains(p.Metadata.Name))
            cambio = DetalleAuditoria.Marcar(cambio);
        return $"{nombre}: {cambio}";
    }

    private static string Valor(object? valor) => valor switch
    {
        null => "—",
        string s when string.IsNullOrWhiteSpace(s) => "—",
        string s => s.Length > 60 ? s[..60] + "…" : s,
        bool b => b ? "Sí" : "No",
        Enum e => Etiquetas.Texto(e),
        decimal d => d.ToString("#,##0.00", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        _ => Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "—"
    };

    private static string Traducir(string estado) =>
        Enum.TryParse<EstadoClienteProducto>(estado, out var e) ? Etiquetas.Texto(e) : estado;

    private static string NombreEntidad(Type tipo) =>
        tipo.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? tipo.Name;

    private static string Unir(string texto, string? extra) => extra is null ? texto : $"{texto} · {extra}";

    private static string? Recortar(string? valor, int max) =>
        string.IsNullOrEmpty(valor) || valor.Length <= max ? valor : valor[..max];
}
