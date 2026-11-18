using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GAS.Admin.Models.Entities;

// Finanzas de gestión (no contabilidad fiscal): cobros, pagos, costos y rentabilidad.

public enum TipoMovimiento { Ingreso, Egreso }

/// <summary>Esperado = por cobrar / por pagar; Confirmado = cobrado / pagado.</summary>
public enum EstadoMovimiento { Esperado, Confirmado, Cancelado }

public enum LineaNegocio { ProductosSaaS, Servicios, Interno }

public enum OrigenMovimiento { Manual, Cotizacion, CicloRecurrente, Renovacion, Stripe }

public enum TipoCuenta { Banco, Tarjeta, Efectivo, Plataforma }

[DisplayName("Cuenta")]
public class CuentaFinanciera : EntidadBase
{
    [Display(Name = "Alias")] public string Alias { get; set; } = string.Empty;
    [Display(Name = "Tipo")] public TipoCuenta Tipo { get; set; } = TipoCuenta.Banco;
    [Display(Name = "Institución")] public string? Institucion { get; set; }
    /// <summary>Solo los últimos 4 dígitos; nunca el número completo.</summary>
    [Display(Name = "Últimos 4 dígitos")] public string? UltimosDigitos { get; set; }
    [Display(Name = "Moneda")] public string Moneda { get; set; } = "MXN";
    [Display(Name = "Activa")] public bool Activa { get; set; } = true;
    [Display(Name = "Notas")] public string? Notas { get; set; }

    [NotMapped] public string Nombre => UltimosDigitos is null ? Alias : $"{Alias} ····{UltimosDigitos}";

    public override string DescripcionAuditoria => Nombre;
}

[DisplayName("Categoría financiera")]
public class CategoriaFinanciera : EntidadBase
{
    [Display(Name = "Nombre")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Tipo")] public TipoMovimiento Tipo { get; set; }
    [Display(Name = "Activa")] public bool Activa { get; set; } = true;
    public int Orden { get; set; }

    public override string DescripcionAuditoria => Nombre;
}

[DisplayName("Tipo de cambio")]
public class TipoCambio : EntidadBase
{
    [Display(Name = "Fecha")] public DateOnly Fecha { get; set; }
    [Display(Name = "Moneda")] public string Moneda { get; set; } = "USD";
    /// <summary>Pesos por una unidad de la moneda.</summary>
    [Display(Name = "Valor")] public decimal Valor { get; set; }
    [Display(Name = "Fuente")] public string? Fuente { get; set; }

    public override string DescripcionAuditoria => $"{Moneda} {Valor:0.0000} · {Fecha:dd/MM/yyyy}";
}

[DisplayName("Movimiento")]
public class Movimiento : EntidadBase
{
    [Display(Name = "Tipo")] public TipoMovimiento Tipo { get; set; }
    [Display(Name = "Estado")] public EstadoMovimiento Estado { get; set; } = EstadoMovimiento.Esperado;
    [Display(Name = "Concepto")] public string Concepto { get; set; } = string.Empty;
    [Display(Name = "Categoría")] public int? CategoriaId { get; set; }
    public CategoriaFinanciera? Categoria { get; set; }
    [Display(Name = "Línea de negocio")] public LineaNegocio Linea { get; set; } = LineaNegocio.Servicios;
    [Display(Name = "Origen")] public OrigenMovimiento Origen { get; set; } = OrigenMovimiento.Manual;

    /// <summary>Fecha en que se espera cobrar o pagar (vencimiento).</summary>
    [Display(Name = "Fecha programada")] public DateOnly FechaProgramada { get; set; }
    /// <summary>Fecha real del cobro o pago (al confirmarlo).</summary>
    [Display(Name = "Fecha de pago")] public DateOnly? FechaPago { get; set; }

    [Display(Name = "Moneda")] public string Moneda { get; set; } = "MXN";
    [Display(Name = "Subtotal")] public decimal Subtotal { get; set; }
    [Display(Name = "Tasa de IVA")] public decimal TasaIva { get; set; }
    [Display(Name = "IVA")] public decimal Iva { get; set; }
    [Display(Name = "Total")] public decimal Total { get; set; }
    /// <summary>Pesos por unidad de la moneda (1 si es MXN).</summary>
    [Display(Name = "Tipo de cambio")] public decimal TipoCambio { get; set; } = 1;
    [Display(Name = "Subtotal en MXN")] public decimal SubtotalMxn { get; set; }
    [Display(Name = "Total en MXN")] public decimal TotalMxn { get; set; }

    [Display(Name = "Cliente")] public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    [Display(Name = "Producto")] public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    [Display(Name = "Trabajo")] public int? TrabajoId { get; set; }
    public Trabajo? Trabajo { get; set; }
    [Display(Name = "Ciclo")] public int? CicloTrabajoId { get; set; }
    public CicloTrabajo? Ciclo { get; set; }
    [Display(Name = "Entregable")] public int? EntregableId { get; set; }
    public Entregable? Entregable { get; set; }
    [Display(Name = "Cotización")] public int? CotizacionId { get; set; }
    public Cotizacion? Cotizacion { get; set; }
    [Display(Name = "Servicio externo")] public int? ServicioExternoId { get; set; }
    public ServicioExterno? ServicioExterno { get; set; }
    [Display(Name = "Dominio")] public int? DominioId { get; set; }
    public Dominio? Dominio { get; set; }
    [Display(Name = "Proveedor")] public string? Proveedor { get; set; }
    [Display(Name = "Cuenta")] public int? CuentaId { get; set; }
    public CuentaFinanciera? Cuenta { get; set; }
    [Display(Name = "Referencia")] public string? Referencia { get; set; }
    [Display(Name = "Notas")] public string? Notas { get; set; }

    [NotMapped] public bool EsIngreso => Tipo == TipoMovimiento.Ingreso;
    [NotMapped] public bool Pendiente => Estado == EstadoMovimiento.Esperado;

    public bool Vencido(DateOnly hoy) => Pendiente && FechaProgramada < hoy;

    /// <summary>Calcula IVA, total y equivalentes en pesos.</summary>
    public void Recalcular()
    {
        Subtotal = Math.Round(Subtotal, 2);
        Iva = Math.Round(Subtotal * TasaIva, 2);
        Total = Subtotal + Iva;
        if (Moneda == "MXN") TipoCambio = 1;
        SubtotalMxn = Math.Round(Subtotal * TipoCambio, 2);
        TotalMxn = Math.Round(Total * TipoCambio, 2);
    }

    public override string DescripcionAuditoria => $"{(EsIngreso ? "Cobro" : "Pago")} · {Concepto}";
}
