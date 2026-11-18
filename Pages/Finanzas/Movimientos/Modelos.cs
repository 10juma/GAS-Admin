using System.ComponentModel.DataAnnotations;
using GAS.Admin.Models.Entities;
using GAS.Admin.Services.Catalogos;

namespace GAS.Admin.Pages.Finanzas.Movimientos;

public sealed record Opcion(int Id, string Nombre, string? Extra = null);

public class MovimientoInput
{
    public TipoMovimiento Tipo { get; set; } = TipoMovimiento.Ingreso;

    [Required(ErrorMessage = "Ingresa el concepto.")]
    [StringLength(200, ErrorMessage = "El concepto no puede exceder 200 caracteres.")]
    public string Concepto { get; set; } = string.Empty;

    public int? CategoriaId { get; set; }
    public LineaNegocio Linea { get; set; } = LineaNegocio.Servicios;
    public DateOnly FechaProgramada { get; set; }
    public string Moneda { get; set; } = "MXN";

    [Range(0.01, 999_999_999, ErrorMessage = "El subtotal debe ser mayor a cero.")]
    public decimal Subtotal { get; set; }

    /// <summary>Porcentaje (16, 8 o 0).</summary>
    [Range(0, 100, ErrorMessage = "La tasa de IVA debe estar entre 0 y 100.")]
    public decimal TasaIva { get; set; } = 16;

    [Range(0, 9999, ErrorMessage = "El tipo de cambio debe ser positivo.")]
    public decimal? TipoCambio { get; set; }

    public int? ClienteId { get; set; }
    public int? ProductoId { get; set; }
    public int? TrabajoId { get; set; }
    public int? ServicioExternoId { get; set; }

    [StringLength(150, ErrorMessage = "El proveedor no puede exceder 150 caracteres.")]
    public string? Proveedor { get; set; }

    public EstadoMovimiento Estado { get; set; } = EstadoMovimiento.Esperado;
    public DateOnly? FechaPago { get; set; }
    public int? CuentaId { get; set; }

    [StringLength(100, ErrorMessage = "La referencia no puede exceder 100 caracteres.")]
    public string? Referencia { get; set; }

    [StringLength(1000, ErrorMessage = "Las notas no pueden exceder 1000 caracteres.")]
    public string? Notas { get; set; }

    public static MovimientoInput Desde(Movimiento m) => new()
    {
        Tipo = m.Tipo, Concepto = m.Concepto, CategoriaId = m.CategoriaId, Linea = m.Linea, FechaProgramada = m.FechaProgramada,
        Moneda = m.Moneda, Subtotal = m.Subtotal, TasaIva = Math.Round(m.TasaIva * 100, 2), TipoCambio = m.Moneda == "MXN" ? null : m.TipoCambio,
        ClienteId = m.ClienteId, ProductoId = m.ProductoId, TrabajoId = m.TrabajoId, ServicioExternoId = m.ServicioExternoId,
        Proveedor = m.Proveedor, Estado = m.Estado, FechaPago = m.FechaPago, CuentaId = m.CuentaId, Referencia = m.Referencia, Notas = m.Notas
    };

    public void AplicarA(Movimiento m)
    {
        m.Tipo = Tipo;
        m.Concepto = Concepto.Trim();
        m.CategoriaId = CategoriaId;
        m.Linea = Linea;
        m.FechaProgramada = FechaProgramada;
        m.Moneda = Moneda;
        m.Subtotal = Subtotal;
        m.TasaIva = Math.Round(TasaIva / 100m, 4);
        m.TipoCambio = Moneda == "MXN" ? 1 : TipoCambio ?? 0;
        m.ClienteId = ClienteId;
        m.ProductoId = ProductoId;
        m.TrabajoId = TrabajoId;
        m.ServicioExternoId = ServicioExternoId;
        m.Proveedor = Opciones.Limpio(Proveedor);
        m.Estado = Estado;
        m.FechaPago = Estado == EstadoMovimiento.Confirmado ? FechaPago : null;
        m.CuentaId = CuentaId;
        m.Referencia = Opciones.Limpio(Referencia);
        m.Notas = Opciones.Limpio(Notas);
        m.Recalcular();
    }
}

public class MovimientoModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public MovimientoInput Input { get; init; } = new();
    public OrigenMovimiento Origen { get; init; } = OrigenMovimiento.Manual;
    public string? Cotizacion { get; init; }
    public int? CotizacionId { get; init; }
    public List<Opcion> Categorias { get; init; } = [];
    public List<Opcion> Clientes { get; init; } = [];
    public List<Opcion> Productos { get; init; } = [];
    public List<Opcion> Trabajos { get; init; } = [];
    public List<Opcion> Servicios { get; init; } = [];
    public List<Opcion> Cuentas { get; init; } = [];
    public decimal? TipoCambioSugerido { get; init; }
    public bool PuedeGestionar { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
    public string? Volver { get; init; }
}

public class ConfirmarInput
{
    public DateOnly FechaPago { get; set; }
    public int? CuentaId { get; set; }

    [StringLength(100, ErrorMessage = "La referencia no puede exceder 100 caracteres.")]
    public string? Referencia { get; set; }

    [Range(0, 9999, ErrorMessage = "El tipo de cambio debe ser positivo.")]
    public decimal? TipoCambio { get; set; }
}

public class ConfirmarModalVm
{
    public Movimiento Movimiento { get; init; } = null!;
    public ConfirmarInput Input { get; init; } = new();
    public List<Opcion> Cuentas { get; init; } = [];
    public string? Volver { get; init; }
}
