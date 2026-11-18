using GAS.Admin.Models.Entities;

namespace GAS.Admin.Services.Comercial;

public static class TextosCotizacion
{
    public const string CondicionesPorDefecto =
        "Los tiempos de entrega se cuentan a partir de la aprobación y del pago del anticipo.\n" +
        "Cualquier cambio de alcance se cotiza por separado.";

    public static string CondicionPago(Cotizacion c) => c.CondicionPago switch
    {
        Models.Entities.CondicionPago.Contado => "Pago único al aprobar la cotización.",
        Models.Entities.CondicionPago.AnticipoYFinal when c.AnticipoPorcentaje is > 0 and < 100 =>
            $"Anticipo del {c.AnticipoPorcentaje}% al aprobar y {100 - c.AnticipoPorcentaje}% a la entrega.",
        Models.Entities.CondicionPago.AnticipoYFinal => "Anticipo al aprobar y finiquito a la entrega.",
        Models.Entities.CondicionPago.Hitos => "Pagos por hitos según el plan de trabajo acordado.",
        Models.Entities.CondicionPago.Mensual => "Pago mensual por adelantado.",
        _ => ""
    };
}
