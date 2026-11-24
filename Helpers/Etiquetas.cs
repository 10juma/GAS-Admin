using GAS.Admin.Models.Entities;

namespace GAS.Admin.Helpers;

/// <summary>Textos en español y clase de chip para los valores de los catálogos.</summary>
public static class Etiquetas
{
    public static string Texto(Enum valor) => valor switch
    {
        EstadoProducto.EnOperacion => "En operación",
        EstadoProducto.EnDesarrollo => "En desarrollo",
        EstadoProducto.Pausado => "Pausado",
        EstadoProducto.Retirado => "Retirado",

        ModalidadProducto.SaaS => "SaaS",
        ModalidadProducto.Renta => "Renta",
        ModalidadProducto.Licencia => "Licencia",
        ModalidadProducto.PorDefinir => "Modalidad por definir",

        EstadoSuperAdmin.Existe => "Con super-admin",
        EstadoSuperAdmin.PorRevisar => "Super-admin por revisar",
        EstadoSuperAdmin.NoTiene => "Sin super-admin",

        EstadoCliente.Prospecto => "Prospecto",
        EstadoCliente.Activo => "Activo",
        EstadoCliente.Inactivo => "Inactivo",

        EstadoClienteProducto.Prueba => "Prueba",
        EstadoClienteProducto.Activo => "Activo",
        EstadoClienteProducto.Cancelado => "Cancelado",

        CategoriaServicio.Pagos => "Pagos",
        CategoriaServicio.Dominios => "Dominios",
        CategoriaServicio.Hosting => "Hosting",
        CategoriaServicio.Desarrollo => "Desarrollo",
        CategoriaServicio.Monitoreo => "Monitoreo",
        CategoriaServicio.Disponibilidad => "Disponibilidad",
        CategoriaServicio.ServiciosMoviles => "Servicios móviles",
        CategoriaServicio.Correo => "Correo",
        CategoriaServicio.Despliegue => "Despliegue",
        CategoriaServicio.Otro => "Otro",

        PeriodicidadCobro.Mensual => "Mensual",
        PeriodicidadCobro.Trimestral => "Trimestral",
        PeriodicidadCobro.Anual => "Anual",
        PeriodicidadCobro.PorUso => "Por uso",
        PeriodicidadCobro.Unico => "Pago único",
        PeriodicidadCobro.Gratuito => "Gratuito",

        EstadoServicio.Activo => "Activo",
        EstadoServicio.EnPrueba => "En prueba",
        EstadoServicio.Cancelado => "Cancelado",

        ModalidadCobro.PrecioFijo => "Precio fijo",
        ModalidadCobro.PorHora => "Por hora",
        ModalidadCobro.Mensual => "Mensual",
        ModalidadCobro.PorEntregable => "Por entregable",
        ModalidadCobro.AMedida => "A medida",

        CategoriaComercial.Desarrollo => "Desarrollo de software",
        CategoriaComercial.AppMovil => "Aplicación móvil",
        CategoriaComercial.Implementacion => "Implementación",
        CategoriaComercial.Personalizacion => "Personalización",
        CategoriaComercial.Consultoria => "Consultoría",
        CategoriaComercial.Analisis => "Análisis",
        CategoriaComercial.Integracion => "Integración",
        CategoriaComercial.Mantenimiento => "Mantenimiento",
        CategoriaComercial.Soporte => "Soporte",
        CategoriaComercial.Suscripcion => "Suscripción SaaS",
        CategoriaComercial.Otro => "Otro",

        EstadoCatalogo.Activo => "Activo",
        EstadoCatalogo.EnDesarrollo => "En desarrollo",
        EstadoCatalogo.Inactivo => "Inactivo",

        EtapaOportunidad.Nueva => "Nueva",
        EtapaOportunidad.Calificada => "Calificada",
        EtapaOportunidad.PropuestaEnviada => "Propuesta enviada",
        EtapaOportunidad.Negociacion => "Negociación",
        EtapaOportunidad.Ganada => "Ganada",
        EtapaOportunidad.Perdida => "Perdida",

        OrigenOportunidad.Referido => "Referido",
        OrigenOportunidad.SitioWeb => "Sitio web",
        OrigenOportunidad.RedesSociales => "Redes sociales",
        OrigenOportunidad.Evento => "Evento",
        OrigenOportunidad.Prospeccion => "Prospección",
        OrigenOportunidad.ClienteExistente => "Cliente existente",
        OrigenOportunidad.Otro => "Otro",

        EstadoCotizacion.Borrador => "Borrador",
        EstadoCotizacion.Enviada => "Enviada",
        EstadoCotizacion.Aprobada => "Aprobada",
        EstadoCotizacion.Rechazada => "Rechazada",
        EstadoCotizacion.Cancelada => "Cancelada",
        EstadoCotizacion.Reemplazada => "Reemplazada",

        CondicionPago.Contado => "Pago único",
        CondicionPago.AnticipoYFinal => "Anticipo y finiquito",
        CondicionPago.Hitos => "Por hitos",
        CondicionPago.Mensual => "Mensualidad",

        TipoTrabajo.Proyecto => "Proyecto",
        TipoTrabajo.ServicioRecurrente => "Servicio recurrente",
        TipoTrabajo.TrabajoPuntual => "Trabajo puntual",

        TipoTrabajoOperativo.Proyecto => "Proyecto",
        TipoTrabajoOperativo.ServicioRecurrente => "Servicio recurrente",
        TipoTrabajoOperativo.TrabajoPuntual => "Trabajo puntual",
        TipoTrabajoOperativo.ProyectoInterno => "Proyecto interno",

        EstadoTrabajo.PorIniciar => "Por iniciar",
        EstadoTrabajo.EnPlaneacion => "En planeación",
        EstadoTrabajo.EnEjecucion => "En ejecución",
        EstadoTrabajo.EnEntrega => "En entrega",
        EstadoTrabajo.Cerrado => "Cerrado",
        EstadoTrabajo.EnPausa => "En pausa",
        EstadoTrabajo.Cancelado => "Cancelado",

        Prioridad.Baja => "Baja",
        Prioridad.Media => "Media",
        Prioridad.Alta => "Alta",
        Prioridad.Urgente => "Urgente",

        EstadoEntregable.Pendiente => "Pendiente",
        EstadoEntregable.Entregado => "Entregado",
        EstadoEntregable.Aceptado => "Aceptado",

        EstadoCiclo.Abierto => "Abierto",
        EstadoCiclo.Cerrado => "Cerrado",

        EstadoTarea.Pendiente => "Pendiente",
        EstadoTarea.PorHacer => "Por hacer",
        EstadoTarea.EnProgreso => "En progreso",
        EstadoTarea.EnRevision => "En revisión",
        EstadoTarea.EnEspera => "En espera",
        EstadoTarea.Terminado => "Terminado",

        TipoPersona.Socio => "Socio",
        TipoPersona.Empleado => "Empleado",
        TipoPersona.Externo => "Externo",

        TipoMovimiento.Ingreso => "Ingreso",
        TipoMovimiento.Egreso => "Egreso",

        EstadoMovimiento.Esperado => "Pendiente",
        EstadoMovimiento.Confirmado => "Confirmado",
        EstadoMovimiento.Cancelado => "Cancelado",

        LineaNegocio.ProductosSaaS => "Productos SaaS",
        LineaNegocio.Servicios => "Servicios",
        LineaNegocio.Interno => "Interno",

        OrigenMovimiento.Manual => "Manual",
        OrigenMovimiento.Cotizacion => "Cotización",
        OrigenMovimiento.CicloRecurrente => "Mensualidad",
        OrigenMovimiento.Renovacion => "Renovación",
        OrigenMovimiento.Stripe => "Stripe",

        TipoCuenta.Banco => "Cuenta bancaria",
        TipoCuenta.Tarjeta => "Tarjeta",
        TipoCuenta.Efectivo => "Efectivo",
        TipoCuenta.Plataforma => "Plataforma de pagos",

        TipoAmbiente.Produccion => "Producción",
        TipoAmbiente.Staging => "Staging",
        TipoAmbiente.Pruebas => "Pruebas",
        TipoAmbiente.Desarrollo => "Desarrollo",

        PlataformaAmbiente.Web => "Web",
        PlataformaAmbiente.Api => "API",
        PlataformaAmbiente.AppMovil => "App móvil",
        PlataformaAmbiente.Backoffice => "Backoffice",
        PlataformaAmbiente.Otro => "Otro",

        EstadoAmbiente.Operativo => "Operativo",
        EstadoAmbiente.Degradado => "Degradado",
        EstadoAmbiente.Caido => "Caído",
        EstadoAmbiente.Mantenimiento => "Mantenimiento",
        EstadoAmbiente.Inactivo => "Inactivo",

        VisibilidadRepositorio.Privado => "Privado",
        VisibilidadRepositorio.Publico => "Público",

        ResultadoDespliegue.Exitoso => "Exitoso",
        ResultadoDespliegue.ConProblemas => "Con problemas",
        ResultadoDespliegue.Revertido => "Revertido",

        EstadoServidor.SinPreparar => "Sin preparar",
        EstadoServidor.Listo => "Listo",
        EstadoServidor.ConError => "Con error",

        EstadoEjecucion.EnCola => "En cola",
        EstadoEjecucion.EnCurso => "En curso",
        EstadoEjecucion.Exitoso => "Exitoso",
        EstadoEjecucion.Fallido => "Falló",
        EstadoEjecucion.Revertido => "Revertido",
        EstadoEjecucion.Interrumpido => "Interrumpido",

        EstadoDominio.Activo => "Activo",
        EstadoDominio.Cancelado => "Cancelado",
        EstadoDominio.Transferido => "Transferido",

        SeveridadIncidente.Baja => "Baja",
        SeveridadIncidente.Media => "Media",
        SeveridadIncidente.Alta => "Alta",
        SeveridadIncidente.Critica => "Crítica",

        EstadoIncidente.Abierto => "Abierto",
        EstadoIncidente.Mitigado => "Mitigado",
        EstadoIncidente.Resuelto => "Resuelto",

        TipoCredencial.ApiKey => "API key",
        TipoCredencial.Token => "Token",
        TipoCredencial.Contrasena => "Contraseña",
        TipoCredencial.ClaveSsh => "Clave SSH",
        TipoCredencial.Certificado => "Certificado",
        TipoCredencial.Webhook => "Secreto de webhook",
        TipoCredencial.Otro => "Otro",

        EstadoCredencial.Activa => "Activa",
        EstadoCredencial.Revocada => "Revocada",

        _ => valor.ToString()
    };

    /// <summary>Modificador de color (success, warning, danger, info, violet o vacío).</summary>
    public static string Tono(Enum valor) => valor switch
    {
        EstadoProducto.EnOperacion or EstadoCliente.Activo or EstadoClienteProducto.Activo
            or EstadoServicio.Activo or EstadoSuperAdmin.Existe or EstadoCatalogo.Activo
            or EtapaOportunidad.Ganada or EstadoCotizacion.Aprobada => "success",
        EstadoProducto.EnDesarrollo or EstadoCliente.Prospecto or EstadoClienteProducto.Prueba
            or EstadoServicio.EnPrueba or EstadoCatalogo.EnDesarrollo or EtapaOportunidad.Nueva
            or EtapaOportunidad.Calificada or EstadoCotizacion.Enviada => "info",
        EstadoProducto.Pausado or EstadoSuperAdmin.PorRevisar or ModalidadProducto.PorDefinir
            or EtapaOportunidad.PropuestaEnviada => "warning",
        EtapaOportunidad.Negociacion or TipoPersona.Socio or TipoTrabajoOperativo.ProyectoInterno
            or EstadoTarea.EnRevision or EstadoTrabajo.EnEntrega => "violet",
        EstadoTrabajo.EnEjecucion or EstadoTarea.EnProgreso or EstadoEntregable.Entregado
            or TipoTrabajoOperativo.Proyecto or Prioridad.Media => "info",
        EstadoTrabajo.Cerrado or EstadoTarea.Terminado or EstadoEntregable.Aceptado or EstadoCiclo.Cerrado => "success",
        EstadoTrabajo.EnPausa or EstadoTarea.EnEspera or Prioridad.Alta or TipoTrabajoOperativo.ServicioRecurrente => "warning",
        EstadoTrabajo.Cancelado or Prioridad.Urgente => "danger",
        TipoPersona.Empleado => "info",
        TipoPersona.Externo => "warning",
        EstadoProducto.Retirado or EstadoClienteProducto.Cancelado or EstadoServicio.Cancelado
            or EtapaOportunidad.Perdida or EstadoCotizacion.Rechazada or EstadoCotizacion.Cancelada => "danger",
        ModalidadProducto.SaaS => "violet",
        TipoMovimiento.Ingreso or EstadoMovimiento.Confirmado => "success",
        TipoMovimiento.Egreso => "danger",
        EstadoMovimiento.Esperado or OrigenMovimiento.CicloRecurrente => "warning",
        LineaNegocio.ProductosSaaS or OrigenMovimiento.Renovacion or OrigenMovimiento.Stripe => "violet",
        LineaNegocio.Servicios or OrigenMovimiento.Cotizacion => "info",
        EstadoAmbiente.Operativo or ResultadoDespliegue.Exitoso or EstadoDominio.Activo or EstadoIncidente.Resuelto
            or EstadoCredencial.Activa or EstadoServidor.Listo or EstadoEjecucion.Exitoso => "success",
        EstadoEjecucion.EnCola or EstadoEjecucion.EnCurso => "info",
        EstadoServidor.SinPreparar or EstadoEjecucion.Revertido or EstadoEjecucion.Interrumpido => "warning",
        EstadoServidor.ConError or EstadoEjecucion.Fallido => "danger",
        EstadoAmbiente.Degradado or ResultadoDespliegue.ConProblemas or SeveridadIncidente.Media
            or EstadoIncidente.Mitigado or TipoAmbiente.Staging => "warning",
        EstadoAmbiente.Caido or ResultadoDespliegue.Revertido or SeveridadIncidente.Critica or SeveridadIncidente.Alta
            or EstadoIncidente.Abierto or EstadoCredencial.Revocada or EstadoDominio.Cancelado => "danger",
        EstadoAmbiente.Mantenimiento or TipoAmbiente.Pruebas => "violet",
        TipoAmbiente.Produccion or SeveridadIncidente.Baja => "info",
        _ => ""
    };

    public static string Badge(Enum valor) => Tono(valor) is { Length: > 0 } t ? $"gas-badge--{t}" : "";
    public static string Chip(Enum valor) => Tono(valor) is { Length: > 0 } t ? $"gas-chip--{t}" : "";

    public static string IconoCategoria(CategoriaServicio c) => c switch
    {
        CategoriaServicio.Pagos => "card",
        CategoriaServicio.Dominios => "globe",
        CategoriaServicio.Hosting => "server",
        CategoriaServicio.Desarrollo => "git",
        CategoriaServicio.Monitoreo => "bug",
        CategoriaServicio.Disponibilidad => "activity",
        CategoriaServicio.ServiciosMoviles => "phone",
        CategoriaServicio.Correo => "mail",
        CategoriaServicio.Despliegue => "rocket",
        _ => "layers"
    };

    public static IEnumerable<T> Valores<T>() where T : struct, Enum => Enum.GetValues<T>();

    public static string Dinero(decimal? monto, string moneda = "MXN") =>
        monto is null ? "—" : $"{moneda} {monto.Value:#,##0.00}";
}
