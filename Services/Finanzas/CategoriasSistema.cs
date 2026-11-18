namespace GAS.Admin.Services.Finanzas;

/// <summary>
/// Nombres de categoría que usan los movimientos generados automáticamente (cotizaciones aprobadas,
/// renovaciones de servicios y dominios, Stripe). Si existe una categoría con ese nombre y tipo se asigna;
/// si no, el movimiento queda sin categoría.
/// </summary>
public static class CategoriasSistema
{
    public const string Suscripciones = "Suscripciones SaaS";
    public const string Desarrollo = "Desarrollo y proyectos";
    public const string Recurrentes = "Servicios recurrentes";
    public const string Software = "Servicios en la nube y software";
    public const string Dominios = "Dominios y hosting";
    public const string Comisiones = "Comisiones bancarias y de pago";
}
