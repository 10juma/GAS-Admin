using GAS.Admin.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Data.Context;

public class GasDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public GasDbContext(DbContextOptions<GasDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<ClienteProducto> ClientesProductos => Set<ClienteProducto>();
    public DbSet<ServicioExterno> ServiciosExternos => Set<ServicioExterno>();
    public DbSet<ServicioProducto> ServiciosProductos => Set<ServicioProducto>();
    public DbSet<ServicioComercial> ServiciosComerciales => Set<ServicioComercial>();
    public DbSet<Oportunidad> Oportunidades => Set<Oportunidad>();
    public DbSet<Cotizacion> Cotizaciones => Set<Cotizacion>();
    public DbSet<CotizacionLinea> CotizacionLineas => Set<CotizacionLinea>();
    public DbSet<CotizacionEnvio> CotizacionEnvios => Set<CotizacionEnvio>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Puesto> Puestos => Set<Puesto>();
    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<Trabajo> Trabajos => Set<Trabajo>();
    public DbSet<Entregable> Entregables => Set<Entregable>();
    public DbSet<CicloTrabajo> Ciclos => Set<CicloTrabajo>();
    public DbSet<Tarea> Tareas => Set<Tarea>();
    public DbSet<TareaComentario> TareaComentarios => Set<TareaComentario>();
    public DbSet<RegistroHoras> RegistrosHoras => Set<RegistroHoras>();
    public DbSet<Adjunto> Adjuntos => Set<Adjunto>();
    public DbSet<CuentaFinanciera> Cuentas => Set<CuentaFinanciera>();
    public DbSet<CategoriaFinanciera> CategoriasFinancieras => Set<CategoriaFinanciera>();
    public DbSet<TipoCambio> TiposCambio => Set<TipoCambio>();
    public DbSet<Movimiento> Movimientos => Set<Movimiento>();
    public DbSet<Repositorio> Repositorios => Set<Repositorio>();
    public DbSet<Ambiente> Ambientes => Set<Ambiente>();
    public DbSet<Despliegue> Despliegues => Set<Despliegue>();
    public DbSet<Servidor> Servidores => Set<Servidor>();
    public DbSet<ConfiguracionDespliegue> ConfiguracionesDespliegue => Set<ConfiguracionDespliegue>();
    public DbSet<EjecucionDespliegue> EjecucionesDespliegue => Set<EjecucionDespliegue>();
    public DbSet<EjecucionRespaldo> EjecucionesRespaldo => Set<EjecucionRespaldo>();
    public DbSet<ArchivoRespaldo> ArchivosRespaldo => Set<ArchivoRespaldo>();
    public DbSet<Dominio> Dominios => Set<Dominio>();
    public DbSet<Incidente> Incidentes => Set<Incidente>();
    public DbSet<Credencial> Credenciales => Set<Credencial>();
    public DbSet<CuentaStripe> CuentasStripe => Set<CuentaStripe>();
    public DbSet<SuscriptorStripe> SuscriptoresStripe => Set<SuscriptorStripe>();
    public DbSet<SuscripcionStripe> SuscripcionesStripe => Set<SuscripcionStripe>();
    public DbSet<FacturaStripe> FacturasStripe => Set<FacturaStripe>();
    public DbSet<MetricaStripe> MetricasStripe => Set<MetricaStripe>();
    public DbSet<EstadoIntegracion> EstadosIntegracion => Set<EstadoIntegracion>();
    public DbSet<LlaveConfiguracion> LlavesConfiguracion => Set<LlaveConfiguracion>();
    public DbSet<MonitorUptime> Monitores => Set<MonitorUptime>();
    public DbSet<RepoGitHub> ReposGitHub => Set<RepoGitHub>();
    public DbSet<ProyectoSentry> ProyectosSentry => Set<ProyectoSentry>();
    public DbSet<ErrorSentry> ErroresSentry => Set<ErrorSentry>();
    public DbSet<ConexionProducto> ConexionesProducto => Set<ConexionProducto>();
    public DbSet<MetricaProducto> MetricasProducto => Set<MetricaProducto>();
    public DbSet<CuentaProducto> CuentasProducto => Set<CuentaProducto>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMensaje> TicketMensajes => Set<TicketMensaje>();
    public DbSet<SlaPrioridad> SlaPrioridades => Set<SlaPrioridad>();
    public DbSet<ArticuloConocimiento> Articulos => Set<ArticuloConocimiento>();
    public DbSet<CierreMensual> CierresMensuales => Set<CierreMensual>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.NombreCompleto).HasMaxLength(150).IsRequired();
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.Property(a => a.Accion).HasMaxLength(100).IsRequired();
            entity.Property(a => a.UsuarioId).HasMaxLength(450);
            entity.Property(a => a.UsuarioEmail).HasMaxLength(256);
            entity.Property(a => a.Entidad).HasMaxLength(100);
            entity.Property(a => a.EntidadId).HasMaxLength(100);
            entity.Property(a => a.Detalle).HasMaxLength(2000);
            entity.Property(a => a.Ip).HasMaxLength(45);
            entity.Property(a => a.UserAgent).HasMaxLength(512);

            entity.HasIndex(a => a.FechaUtc);
            entity.HasIndex(a => a.UsuarioId);
            entity.HasIndex(a => a.Accion);
        });

        // ---------- Catálogos ----------
        builder.Entity<Producto>(entity =>
        {
            entity.ToTable("Productos");
            entity.Property(p => p.Nombre).HasMaxLength(100).IsRequired();
            entity.HasIndex(p => p.Nombre).IsUnique();
            entity.Property(p => p.Descripcion).HasMaxLength(500);
            entity.Property(p => p.Estado).HasConversion<string>().HasMaxLength(30);
            entity.Property(p => p.Modalidad).HasConversion<string>().HasMaxLength(30);
            entity.Property(p => p.SuperAdmin).HasConversion<string>().HasMaxLength(30);
            entity.Property(p => p.SitioWeb).HasMaxLength(300);
            entity.Property(p => p.SuperAdminUrl).HasMaxLength(300);
            entity.Property(p => p.Notas).HasMaxLength(2000);
            entity.HasOne(p => p.Responsable).WithMany().HasForeignKey(p => p.ResponsableId).OnDelete(DeleteBehavior.SetNull);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Cliente>(entity =>
        {
            entity.ToTable("Clientes");
            entity.Property(c => c.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(c => c.NombreComercial).HasMaxLength(200);
            entity.Property(c => c.Rfc).HasMaxLength(13);
            entity.HasIndex(c => c.Rfc);
            entity.HasIndex(c => c.Nombre);
            entity.Property(c => c.Estado).HasConversion<string>().HasMaxLength(30);
            entity.Property(c => c.Ciudad).HasMaxLength(100);
            entity.Property(c => c.SitioWeb).HasMaxLength(300);
            entity.Property(c => c.ContactoNombre).HasMaxLength(150);
            entity.Property(c => c.ContactoEmail).HasMaxLength(256);
            entity.Property(c => c.ContactoTelefono).HasMaxLength(30);
            entity.Property(c => c.Notas).HasMaxLength(2000);
            entity.HasOne(c => c.Responsable).WithMany().HasForeignKey(c => c.ResponsableId).OnDelete(DeleteBehavior.SetNull);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<ClienteProducto>(entity =>
        {
            entity.ToTable("ClientesProductos");
            entity.HasKey(cp => new { cp.ClienteId, cp.ProductoId });
            entity.Property(cp => cp.Estado).HasConversion<string>().HasMaxLength(30);
            entity.HasOne(cp => cp.Cliente).WithMany(c => c.Productos).HasForeignKey(cp => cp.ClienteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(cp => cp.Producto).WithMany(p => p.Clientes).HasForeignKey(cp => cp.ProductoId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ServicioExterno>(entity =>
        {
            entity.ToTable("ServiciosExternos");
            entity.Property(s => s.Nombre).HasMaxLength(100).IsRequired();
            entity.HasIndex(s => s.Nombre).IsUnique();
            entity.Property(s => s.Categoria).HasConversion<string>().HasMaxLength(30);
            entity.Property(s => s.Descripcion).HasMaxLength(500);
            entity.Property(s => s.Estado).HasConversion<string>().HasMaxLength(30);
            entity.Property(s => s.UrlPanel).HasMaxLength(300);
            entity.Property(s => s.CuentaAsociada).HasMaxLength(200);
            entity.Property(s => s.Plan).HasMaxLength(100);
            entity.Property(s => s.Costo).HasPrecision(18, 2);
            entity.Property(s => s.Moneda).HasMaxLength(3).IsRequired();
            entity.Property(s => s.Periodicidad).HasConversion<string>().HasMaxLength(30);
            entity.Property(s => s.ReferenciaCredenciales).HasMaxLength(300);
            entity.Property(s => s.DocumentacionUrl).HasMaxLength(300);
            entity.Property(s => s.Notas).HasMaxLength(2000);
            entity.HasOne(s => s.Responsable).WithMany().HasForeignKey(s => s.ResponsableId).OnDelete(DeleteBehavior.SetNull);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<ServicioProducto>(entity =>
        {
            entity.ToTable("ServiciosProductos");
            entity.HasKey(sp => new { sp.ServicioExternoId, sp.ProductoId });
            entity.HasOne(sp => sp.ServicioExterno).WithMany(s => s.Productos).HasForeignKey(sp => sp.ServicioExternoId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(sp => sp.Producto).WithMany(p => p.Servicios).HasForeignKey(sp => sp.ProductoId).OnDelete(DeleteBehavior.Restrict);
        });

        ConfigurarComercial(builder);
        ConfigurarOrganizacion(builder);
        ConfigurarProyectos(builder);
        ConfigurarFinanzas(builder);
        ConfigurarInfraestructura(builder);
        ConfigurarStripe(builder);
        ConfigurarIntegraciones(builder);
        ConfigurarConexiones(builder);
        ConfigurarSoporte(builder);
        builder.Entity<CierreMensual>(entity =>
        {
            entity.ToTable("CierresMensuales");
            entity.HasKey(c => c.Mes);
            entity.Property(c => c.IndicadoresJson).IsRequired();
        });
    }

    private static void ConfigurarSoporte(ModelBuilder builder)
    {
        builder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Tickets");
            entity.Property(t => t.Folio).HasMaxLength(20).IsRequired();
            entity.HasIndex(t => t.Folio).IsUnique();
            entity.Property(t => t.Asunto).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.Prioridad).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.Categoria).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.Canal).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.ContactoNombre).HasMaxLength(200);
            entity.Property(t => t.ContactoEmail).HasMaxLength(256);
            entity.Property(t => t.ReferenciaExterna).HasMaxLength(300);
            entity.HasIndex(t => new { t.Estado, t.UltimaActividadUtc });
            entity.HasOne(t => t.Producto).WithMany().HasForeignKey(t => t.ProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(t => t.Cliente).WithMany().HasForeignKey(t => t.ClienteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(t => t.CuentaProducto).WithMany().HasForeignKey(t => t.CuentaProductoId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(t => t.Asignado).WithMany().HasForeignKey(t => t.AsignadoId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(t => t.Tarea).WithMany().HasForeignKey(t => t.TareaId).OnDelete(DeleteBehavior.ClientSetNull);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<TicketMensaje>(entity =>
        {
            entity.ToTable("TicketMensajes");
            entity.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20);
            entity.Property(m => m.Autor).HasMaxLength(256).IsRequired();
            entity.Property(m => m.AutorEmail).HasMaxLength(256);
            entity.Property(m => m.Texto).IsRequired();
            entity.Property(m => m.CorreoId).HasMaxLength(200);
            entity.Property(m => m.CorreoError).HasMaxLength(500);
            entity.Property(m => m.ReferenciaExterna).HasMaxLength(150);
            entity.HasIndex(m => m.ReferenciaExterna).IsUnique().HasFilter("[ReferenciaExterna] IS NOT NULL");
            entity.HasIndex(m => new { m.TicketId, m.FechaUtc });
            entity.HasOne(m => m.Ticket).WithMany(t => t.Mensajes).HasForeignKey(m => m.TicketId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SlaPrioridad>(entity =>
        {
            entity.ToTable("SlaPrioridades");
            entity.HasKey(s => s.Prioridad);
            entity.Property(s => s.Prioridad).HasConversion<string>().HasMaxLength(20);
        });

        builder.Entity<ArticuloConocimiento>(entity =>
        {
            entity.ToTable("ArticulosConocimiento");
            entity.Property(a => a.Titulo).HasMaxLength(200).IsRequired();
            entity.Property(a => a.Contenido).IsRequired();
            entity.Property(a => a.Categoria).HasConversion<string>().HasMaxLength(20);
            entity.Property(a => a.Etiquetas).HasMaxLength(300);
            entity.HasOne(a => a.Producto).WithMany().HasForeignKey(a => a.ProductoId).OnDelete(DeleteBehavior.SetNull);
            ConfigurarAuditoria(entity);
        });
    }

    private static void ConfigurarConexiones(ModelBuilder builder)
    {
        builder.Entity<ConexionProducto>(entity =>
        {
            entity.ToTable("ConexionesProducto");
            entity.HasIndex(c => c.ProductoId).IsUnique();
            entity.Property(c => c.Clave).HasMaxLength(50).IsRequired();
            entity.HasIndex(c => c.Clave).IsUnique();
            entity.Property(c => c.UrlApi).HasMaxLength(500).IsRequired();
            entity.Property(c => c.UrlSuperAdmin).HasMaxLength(500);
            entity.Property(c => c.UltimoError).HasMaxLength(1000);
            entity.Property(c => c.Version).HasMaxLength(50);
            entity.Property(c => c.Ambiente).HasMaxLength(50);
            entity.Property(c => c.DependenciasJson).HasMaxLength(4000);
            entity.HasOne(c => c.Producto).WithMany().HasForeignKey(c => c.ProductoId).OnDelete(DeleteBehavior.Restrict);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<MetricaProducto>(entity =>
        {
            entity.ToTable("MetricasProducto");
            entity.HasIndex(m => new { m.ConexionProductoId, m.Fecha }).IsUnique();
            entity.Property(m => m.AdicionalesJson).HasMaxLength(4000);
            entity.HasOne(m => m.Conexion).WithMany().HasForeignKey(m => m.ConexionProductoId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CuentaProducto>(entity =>
        {
            entity.ToTable("CuentasProducto");
            entity.Property(c => c.ExternoId).HasMaxLength(100).IsRequired();
            entity.HasIndex(c => new { c.ConexionProductoId, c.ExternoId }).IsUnique();
            entity.Property(c => c.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(256);
            entity.Property(c => c.Plan).HasMaxLength(100);
            entity.Property(c => c.Estado).HasMaxLength(20).IsRequired();
            entity.HasIndex(c => c.Estado);
            entity.Property(c => c.StripeCustomerId).HasMaxLength(100);
            entity.HasOne(c => c.Conexion).WithMany().HasForeignKey(c => c.ConexionProductoId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(c => c.Cliente).WithMany().HasForeignKey(c => c.ClienteId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigurarIntegraciones(ModelBuilder builder)
    {
        builder.Entity<EstadoIntegracion>(entity =>
        {
            entity.ToTable("EstadosIntegracion");
            entity.HasKey(e => e.Nombre);
            entity.Property(e => e.Nombre).HasMaxLength(30);
            entity.Property(e => e.UltimoResultado).HasMaxLength(500);
            entity.Property(e => e.UltimoError).HasMaxLength(1000);
        });

        builder.Entity<LlaveConfiguracion>(entity =>
        {
            entity.ToTable("LlavesConfiguracion");
            entity.Property(e => e.Clave).HasMaxLength(200);
            entity.HasIndex(e => e.Clave).IsUnique();
            entity.Property(e => e.Pista).HasMaxLength(20);
            entity.Property(e => e.ModificadoPor).HasMaxLength(256);
        });

        builder.Entity<MonitorUptime>(entity =>
        {
            entity.ToTable("MonitoresUptime");
            entity.Property(m => m.ExternoId).HasMaxLength(50).IsRequired();
            entity.HasIndex(m => m.ExternoId).IsUnique();
            entity.Property(m => m.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(m => m.Url).HasMaxLength(500);
            entity.Property(m => m.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(m => m.Uptime30d).HasPrecision(6, 3);
            entity.HasOne(m => m.Ambiente).WithMany().HasForeignKey(m => m.AmbienteId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(m => m.Incidente).WithMany().HasForeignKey(m => m.IncidenteId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<RepoGitHub>(entity =>
        {
            entity.ToTable("ReposGitHub");
            entity.HasKey(r => r.RepositorioId);
            entity.Property(r => r.NombreCompleto).HasMaxLength(200).IsRequired();
            entity.Property(r => r.RamaPrincipal).HasMaxLength(100);
            entity.Property(r => r.UltimoCommitSha).HasMaxLength(64);
            entity.Property(r => r.UltimoCommitMensaje).HasMaxLength(300);
            entity.Property(r => r.UltimoCommitAutor).HasMaxLength(150);
            entity.Property(r => r.UltimoRelease).HasMaxLength(100);
            entity.Property(r => r.Error).HasMaxLength(500);
            entity.HasOne(r => r.Repositorio).WithOne().HasForeignKey<RepoGitHub>(r => r.RepositorioId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProyectoSentry>(entity =>
        {
            entity.ToTable("ProyectosSentry");
            entity.Property(p => p.ExternoId).HasMaxLength(50).IsRequired();
            entity.HasIndex(p => p.ExternoId).IsUnique();
            entity.Property(p => p.Slug).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Plataforma).HasMaxLength(50);
            entity.HasOne(p => p.Producto).WithMany().HasForeignKey(p => p.ProductoId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ErrorSentry>(entity =>
        {
            entity.ToTable("ErroresSentry");
            entity.Property(e => e.ExternoId).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => new { e.ProyectoSentryId, e.ExternoId }).IsUnique();
            entity.Property(e => e.Titulo).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Culpable).HasMaxLength(500);
            entity.Property(e => e.Nivel).HasMaxLength(20);
            entity.Property(e => e.Url).HasMaxLength(500);
            entity.HasOne(e => e.Proyecto).WithMany(p => p.Errores).HasForeignKey(e => e.ProyectoSentryId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurarStripe(ModelBuilder builder)
    {
        builder.Entity<CuentaStripe>(entity =>
        {
            entity.ToTable("CuentasStripe");
            entity.HasIndex(c => c.ProductoId).IsUnique();
            entity.Property(c => c.Clave).HasMaxLength(50).IsRequired();
            entity.HasIndex(c => c.Clave).IsUnique();
            entity.Property(c => c.CuentaId).HasMaxLength(100);
            entity.Property(c => c.NombreStripe).HasMaxLength(200);
            entity.Property(c => c.UltimoResultado).HasMaxLength(500);
            entity.Property(c => c.UltimoError).HasMaxLength(1000);
            entity.HasOne(c => c.Producto).WithMany().HasForeignKey(c => c.ProductoId).OnDelete(DeleteBehavior.Restrict);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<SuscriptorStripe>(entity =>
        {
            entity.ToTable("SuscriptoresStripe");
            entity.Property(s => s.StripeId).HasMaxLength(100).IsRequired();
            entity.HasIndex(s => new { s.CuentaStripeId, s.StripeId }).IsUnique();
            entity.Property(s => s.Nombre).HasMaxLength(200);
            entity.Property(s => s.Email).HasMaxLength(256);
            entity.HasOne(s => s.Cuenta).WithMany().HasForeignKey(s => s.CuentaStripeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.Cliente).WithMany().HasForeignKey(s => s.ClienteId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SuscripcionStripe>(entity =>
        {
            entity.ToTable("SuscripcionesStripe");
            entity.Property(s => s.StripeId).HasMaxLength(100).IsRequired();
            entity.HasIndex(s => new { s.CuentaStripeId, s.StripeId }).IsUnique();
            entity.Property(s => s.Estado).HasMaxLength(30).IsRequired();
            entity.HasIndex(s => s.Estado);
            entity.Property(s => s.Plan).HasMaxLength(200);
            entity.Property(s => s.Moneda).HasMaxLength(3).IsRequired();
            entity.Property(s => s.MontoMensual).HasPrecision(18, 2);
            entity.Property(s => s.Intervalo).HasMaxLength(10);
            entity.HasOne(s => s.Cuenta).WithMany().HasForeignKey(s => s.CuentaStripeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.Suscriptor).WithMany(x => x.Suscripciones).HasForeignKey(s => s.SuscriptorId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<FacturaStripe>(entity =>
        {
            entity.ToTable("FacturasStripe");
            entity.Property(f => f.StripeId).HasMaxLength(100).IsRequired();
            entity.HasIndex(f => new { f.CuentaStripeId, f.StripeId }).IsUnique();
            entity.Property(f => f.Numero).HasMaxLength(100);
            entity.Property(f => f.Estado).HasMaxLength(20).IsRequired();
            entity.Property(f => f.Moneda).HasMaxLength(3).IsRequired();
            entity.Property(f => f.MonedaComision).HasMaxLength(3);
            entity.Property(f => f.Total).HasPrecision(18, 2);
            entity.Property(f => f.Impuestos).HasPrecision(18, 2);
            entity.Property(f => f.Pagado).HasPrecision(18, 2);
            entity.Property(f => f.Comision).HasPrecision(18, 2);
            entity.Property(f => f.Url).HasMaxLength(500);
            entity.HasIndex(f => new { f.Estado, f.FechaUtc });
            entity.HasOne(f => f.Cuenta).WithMany().HasForeignKey(f => f.CuentaStripeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(f => f.Suscriptor).WithMany().HasForeignKey(f => f.SuscriptorId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(f => f.Suscripcion).WithMany().HasForeignKey(f => f.SuscripcionId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<MetricaStripe>(entity =>
        {
            entity.ToTable("MetricasStripe");
            entity.HasIndex(m => new { m.CuentaStripeId, m.Fecha }).IsUnique();
            entity.Property(m => m.Moneda).HasMaxLength(3).IsRequired();
            entity.Property(m => m.Mrr).HasPrecision(18, 2);
            entity.HasOne(m => m.Cuenta).WithMany().HasForeignKey(m => m.CuentaStripeId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurarInfraestructura(ModelBuilder builder)
    {
        builder.Entity<Repositorio>(entity =>
        {
            entity.ToTable("Repositorios");
            entity.Property(r => r.Nombre).HasMaxLength(150).IsRequired();
            entity.HasIndex(r => r.Nombre).IsUnique();
            entity.Property(r => r.Url).HasMaxLength(500);
            entity.Property(r => r.RamaPrincipal).HasMaxLength(100).IsRequired();
            entity.Property(r => r.Visibilidad).HasConversion<string>().HasMaxLength(20);
            entity.Property(r => r.Tecnologia).HasMaxLength(200);
            entity.Property(r => r.Notas).HasMaxLength(2000);
            entity.HasOne(r => r.Producto).WithMany().HasForeignKey(r => r.ProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.Proveedor).WithMany().HasForeignKey(r => r.ProveedorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.Responsable).WithMany().HasForeignKey(r => r.ResponsableId).OnDelete(DeleteBehavior.SetNull);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Ambiente>(entity =>
        {
            entity.ToTable("Ambientes");
            entity.Property(a => a.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(a => a.Tipo).HasConversion<string>().HasMaxLength(20);
            entity.Property(a => a.Plataforma).HasConversion<string>().HasMaxLength(20);
            entity.Property(a => a.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(a => a.Url).HasMaxLength(500);
            entity.Property(a => a.Rama).HasMaxLength(100);
            entity.Property(a => a.Version).HasMaxLength(50);
            entity.Property(a => a.Notas).HasMaxLength(2000);
            entity.HasOne(a => a.Producto).WithMany().HasForeignKey(a => a.ProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.Hosting).WithMany().HasForeignKey(a => a.HostingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.Repositorio).WithMany().HasForeignKey(a => a.RepositorioId).OnDelete(DeleteBehavior.Restrict);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Despliegue>(entity =>
        {
            entity.ToTable("Despliegues");
            entity.Property(d => d.Version).HasMaxLength(50).IsRequired();
            entity.Property(d => d.Resultado).HasConversion<string>().HasMaxLength(20);
            entity.Property(d => d.Referencia).HasMaxLength(150);
            entity.Property(d => d.Realizo).HasMaxLength(256);
            entity.Property(d => d.ExternoId).HasMaxLength(100);
            entity.HasIndex(d => new { d.AmbienteId, d.ExternoId }).IsUnique().HasFilter("[ExternoId] IS NOT NULL");
            entity.Property(d => d.Notas).HasMaxLength(2000);
            entity.HasIndex(d => new { d.AmbienteId, d.FechaUtc });
            entity.HasOne(d => d.Ambiente).WithMany(a => a.Despliegues).HasForeignKey(d => d.AmbienteId).OnDelete(DeleteBehavior.Cascade);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Servidor>(entity =>
        {
            entity.ToTable("Servidores");
            entity.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Nombre).IsUnique();
            entity.Property(x => x.Usuario).HasMaxLength(50).IsRequired();
            entity.Property(x => x.HuellaHost).HasMaxLength(100);
            entity.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Sistema).HasMaxLength(300);
            entity.Property(x => x.UltimoError).HasMaxLength(1000);
            entity.Property(x => x.Notas).HasMaxLength(2000);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<ConfiguracionDespliegue>(entity =>
        {
            entity.ToTable("ConfiguracionesDespliegue");
            entity.HasIndex(x => x.AmbienteId).IsUnique();
            entity.HasOne(x => x.Ambiente).WithMany().HasForeignKey(x => x.AmbienteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Servidor).WithMany().HasForeignKey(x => x.ServidorId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Dockerfile).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Contenedor).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Imagen).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Puertos).HasMaxLength(500);
            entity.Property(x => x.Red).HasMaxLength(100);
            entity.Property(x => x.Volumenes).HasMaxLength(2000);
            entity.Property(x => x.RutaSalud).HasMaxLength(200);
            entity.Property(x => x.DockerfileMigraciones).HasMaxLength(200);
            entity.Property(x => x.ContenedorSql).HasMaxLength(100);
            entity.Property(x => x.BaseDatos).HasMaxLength(128);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<EjecucionDespliegue>(entity =>
        {
            entity.ToTable("EjecucionesDespliegue");
            entity.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Servidor).WithMany().HasForeignKey(x => x.ServidorId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Ambientes).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Referencia).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Commit).HasMaxLength(40);
            entity.Property(x => x.MensajeCommit).HasMaxLength(500);
            entity.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Resumen).HasMaxLength(1000);
            entity.Property(x => x.IniciadoPor).HasMaxLength(256);
            entity.HasIndex(x => x.CreadoUtc);
        });

        builder.Entity<EjecucionRespaldo>(entity =>
        {
            entity.ToTable("EjecucionesRespaldo");
            entity.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.ClaveProgramada).HasMaxLength(10);
            entity.Property(x => x.IniciadoPor).HasMaxLength(256);
            entity.Property(x => x.Resumen).HasMaxLength(1000);
            entity.HasIndex(x => x.ClaveProgramada).IsUnique().HasFilter("[ClaveProgramada] IS NOT NULL");
            entity.HasIndex(x => x.InicioUtc);
        });

        builder.Entity<ArchivoRespaldo>(entity =>
        {
            entity.ToTable("ArchivosRespaldo");
            entity.HasOne(x => x.Ejecucion).WithMany(e => e.Archivos).HasForeignKey(x => x.EjecucionId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.Producto).HasMaxLength(150);
            entity.Property(x => x.BaseDatos).HasMaxLength(128);
            entity.Property(x => x.Nombre).HasMaxLength(500);
            entity.Property(x => x.IdArchivoB2).HasMaxLength(200);
            entity.Property(x => x.Sha256).HasMaxLength(64);
            entity.Property(x => x.Error).HasMaxLength(1000);
            entity.HasIndex(x => new { x.BorradoUtc, x.RetenerHastaUtc });
        });

        builder.Entity<Dominio>(entity =>
        {
            entity.ToTable("Dominios");
            entity.Property(d => d.Nombre).HasMaxLength(253).IsRequired();
            entity.HasIndex(d => d.Nombre).IsUnique();
            entity.Property(d => d.Dns).HasMaxLength(150);
            entity.Property(d => d.Costo).HasPrecision(18, 2);
            entity.Property(d => d.Moneda).HasMaxLength(3).IsRequired();
            entity.Property(d => d.SslEmisor).HasMaxLength(100);
            entity.Property(d => d.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(d => d.Notas).HasMaxLength(2000);
            entity.HasOne(d => d.Producto).WithMany().HasForeignKey(d => d.ProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(d => d.Registrador).WithMany().HasForeignKey(d => d.RegistradorId).OnDelete(DeleteBehavior.Restrict);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Incidente>(entity =>
        {
            entity.ToTable("Incidentes");
            entity.Property(i => i.Folio).HasMaxLength(20).IsRequired();
            entity.HasIndex(i => i.Folio).IsUnique();
            entity.Property(i => i.Titulo).HasMaxLength(200).IsRequired();
            entity.Property(i => i.Severidad).HasConversion<string>().HasMaxLength(20);
            entity.Property(i => i.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(i => i.Impacto).HasMaxLength(2000);
            entity.Property(i => i.Causa).HasMaxLength(2000);
            entity.Property(i => i.Acciones).HasMaxLength(4000);
            entity.HasIndex(i => new { i.Estado, i.InicioUtc });
            entity.HasOne(i => i.Producto).WithMany().HasForeignKey(i => i.ProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.Ambiente).WithMany().HasForeignKey(i => i.AmbienteId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(i => i.Responsable).WithMany().HasForeignKey(i => i.ResponsableId).OnDelete(DeleteBehavior.SetNull);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Credencial>(entity =>
        {
            entity.ToTable("Credenciales");
            entity.Property(c => c.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(20);
            entity.Property(c => c.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(c => c.Referencia).HasMaxLength(300).IsRequired();
            entity.Property(c => c.Notas).HasMaxLength(2000);
            entity.HasOne(c => c.Producto).WithMany().HasForeignKey(c => c.ProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.Ambiente).WithMany().HasForeignKey(c => c.AmbienteId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(c => c.ServicioExterno).WithMany().HasForeignKey(c => c.ServicioExternoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.Responsable).WithMany().HasForeignKey(c => c.ResponsableId).OnDelete(DeleteBehavior.SetNull);
            ConfigurarAuditoria(entity);
        });
    }

    private static void ConfigurarFinanzas(ModelBuilder builder)
    {
        builder.Entity<CuentaFinanciera>(entity =>
        {
            entity.ToTable("CuentasFinancieras");
            entity.Property(c => c.Alias).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(20);
            entity.Property(c => c.Institucion).HasMaxLength(100);
            entity.Property(c => c.UltimosDigitos).HasMaxLength(4);
            entity.Property(c => c.Moneda).HasMaxLength(3).IsRequired();
            entity.Property(c => c.Notas).HasMaxLength(1000);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<CategoriaFinanciera>(entity =>
        {
            entity.ToTable("CategoriasFinancieras");
            entity.Property(c => c.Nombre).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(c => new { c.Tipo, c.Nombre }).IsUnique();
            ConfigurarAuditoria(entity);
        });

        builder.Entity<TipoCambio>(entity =>
        {
            entity.ToTable("TiposCambio");
            entity.Property(t => t.Moneda).HasMaxLength(3).IsRequired();
            entity.Property(t => t.Valor).HasPrecision(18, 4);
            entity.Property(t => t.Fuente).HasMaxLength(100);
            entity.HasIndex(t => new { t.Moneda, t.Fecha }).IsUnique();
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Movimiento>(entity =>
        {
            entity.ToTable("Movimientos");
            entity.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20);
            entity.Property(m => m.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(m => m.Linea).HasConversion<string>().HasMaxLength(20);
            entity.Property(m => m.Origen).HasConversion<string>().HasMaxLength(20);
            entity.Property(m => m.Concepto).HasMaxLength(200).IsRequired();
            entity.Property(m => m.Moneda).HasMaxLength(3).IsRequired();
            entity.Property(m => m.Subtotal).HasPrecision(18, 2);
            entity.Property(m => m.TasaIva).HasPrecision(5, 4);
            entity.Property(m => m.Iva).HasPrecision(18, 2);
            entity.Property(m => m.Total).HasPrecision(18, 2);
            entity.Property(m => m.TipoCambio).HasPrecision(18, 4);
            entity.Property(m => m.SubtotalMxn).HasPrecision(18, 2);
            entity.Property(m => m.TotalMxn).HasPrecision(18, 2);
            entity.Property(m => m.Proveedor).HasMaxLength(150);
            entity.Property(m => m.Referencia).HasMaxLength(100);
            entity.Property(m => m.Notas).HasMaxLength(1000);
            entity.HasIndex(m => new { m.Estado, m.FechaProgramada });
            entity.HasIndex(m => m.FechaPago);
            entity.HasOne(m => m.Categoria).WithMany().HasForeignKey(m => m.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.Cliente).WithMany().HasForeignKey(m => m.ClienteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.Producto).WithMany().HasForeignKey(m => m.ProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.Trabajo).WithMany().HasForeignKey(m => m.TrabajoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.Ciclo).WithMany().HasForeignKey(m => m.CicloTrabajoId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(m => m.Entregable).WithMany().HasForeignKey(m => m.EntregableId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(m => m.Cotizacion).WithMany().HasForeignKey(m => m.CotizacionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.ServicioExterno).WithMany().HasForeignKey(m => m.ServicioExternoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.Cuenta).WithMany().HasForeignKey(m => m.CuentaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.Dominio).WithMany().HasForeignKey(m => m.DominioId).OnDelete(DeleteBehavior.Restrict);
            ConfigurarAuditoria(entity);
        });
    }

    private static void ConfigurarProyectos(ModelBuilder builder)
    {
        builder.Entity<Trabajo>(entity =>
        {
            entity.ToTable("Trabajos");
            entity.Property(t => t.Folio).HasMaxLength(20).IsRequired();
            entity.HasIndex(t => t.Folio).IsUnique();
            entity.Property(t => t.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Tipo).HasConversion<string>().HasMaxLength(30);
            entity.Property(t => t.Estado).HasConversion<string>().HasMaxLength(30);
            entity.Property(t => t.Prioridad).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.Descripcion).HasMaxLength(2000);
            entity.Property(t => t.Presupuesto).HasPrecision(18, 2);
            entity.Property(t => t.CostoEstimado).HasPrecision(18, 2);
            entity.Property(t => t.GastosDirectos).HasPrecision(18, 2);
            entity.Property(t => t.Moneda).HasMaxLength(3).IsRequired();
            entity.Property(t => t.Notas).HasMaxLength(2000);
            entity.HasIndex(t => t.CotizacionId).IsUnique().HasFilter("[CotizacionId] IS NOT NULL");
            entity.HasOne(t => t.Cliente).WithMany().HasForeignKey(t => t.ClienteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(t => t.Producto).WithMany().HasForeignKey(t => t.ProductoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(t => t.Cotizacion).WithMany().HasForeignKey(t => t.CotizacionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(t => t.Departamento).WithMany().HasForeignKey(t => t.DepartamentoId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(t => t.Responsable).WithMany().HasForeignKey(t => t.ResponsableId).OnDelete(DeleteBehavior.ClientSetNull);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Entregable>(entity =>
        {
            entity.ToTable("Entregables");
            entity.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Descripcion).HasMaxLength(1000);
            entity.Property(e => e.Estado).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(e => e.Trabajo).WithMany(t => t.Entregables).HasForeignKey(e => e.TrabajoId).OnDelete(DeleteBehavior.Cascade);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<CicloTrabajo>(entity =>
        {
            entity.ToTable("CiclosTrabajo");
            entity.HasIndex(c => new { c.TrabajoId, c.Periodo }).IsUnique();
            entity.Property(c => c.Monto).HasPrecision(18, 2);
            entity.Property(c => c.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(c => c.Notas).HasMaxLength(1000);
            entity.HasOne(c => c.Trabajo).WithMany(t => t.Ciclos).HasForeignKey(c => c.TrabajoId).OnDelete(DeleteBehavior.Cascade);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Tarea>(entity =>
        {
            entity.ToTable("Tareas");
            entity.Property(t => t.Titulo).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Descripcion).HasMaxLength(4000);
            entity.Property(t => t.Prioridad).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.HorasEstimadas).HasPrecision(7, 2);
            entity.HasIndex(t => new { t.Estado, t.Orden });
            entity.HasOne(t => t.Trabajo).WithMany(x => x.Tareas).HasForeignKey(t => t.TrabajoId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(t => t.Entregable).WithMany().HasForeignKey(t => t.EntregableId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(t => t.Departamento).WithMany().HasForeignKey(t => t.DepartamentoId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(t => t.Responsable).WithMany().HasForeignKey(t => t.ResponsableId).OnDelete(DeleteBehavior.ClientSetNull);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<TareaComentario>(entity =>
        {
            entity.ToTable("TareaComentarios");
            entity.Property(c => c.Autor).HasMaxLength(256).IsRequired();
            entity.Property(c => c.Texto).HasMaxLength(4000).IsRequired();
            entity.HasOne(c => c.Tarea).WithMany(t => t.Comentarios).HasForeignKey(c => c.TareaId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RegistroHoras>(entity =>
        {
            entity.ToTable("RegistrosHoras");
            entity.Property(h => h.Horas).HasPrecision(5, 2);
            entity.Property(h => h.CostoHora).HasPrecision(18, 2);
            entity.Property(h => h.MonedaCosto).HasMaxLength(3);
            entity.Property(h => h.Nota).HasMaxLength(500);
            entity.HasIndex(h => new { h.PersonaId, h.Fecha });
            entity.HasOne(h => h.Persona).WithMany().HasForeignKey(h => h.PersonaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(h => h.Trabajo).WithMany(t => t.Horas).HasForeignKey(h => h.TrabajoId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(h => h.Tarea).WithMany(t => t.Horas).HasForeignKey(h => h.TareaId).OnDelete(DeleteBehavior.ClientSetNull);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Adjunto>(entity =>
        {
            entity.ToTable("Adjuntos");
            entity.Property(a => a.NombreOriginal).HasMaxLength(255).IsRequired();
            entity.Property(a => a.Clave).HasMaxLength(100).IsRequired();
            entity.HasIndex(a => a.Clave).IsUnique();
            entity.Property(a => a.TipoContenido).HasMaxLength(150).IsRequired();
            entity.Property(a => a.SubidoPor).HasMaxLength(256);
            entity.HasOne(a => a.Trabajo).WithMany().HasForeignKey(a => a.TrabajoId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(a => a.Entregable).WithMany().HasForeignKey(a => a.EntregableId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(a => a.Tarea).WithMany().HasForeignKey(a => a.TareaId).OnDelete(DeleteBehavior.ClientSetNull);
        });
    }

    private static void ConfigurarOrganizacion(ModelBuilder builder)
    {
        builder.Entity<Empresa>(entity =>
        {
            entity.ToTable("Empresa");
            entity.Property(e => e.RazonSocial).HasMaxLength(200);
            entity.Property(e => e.NombreComercial).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Rfc).HasMaxLength(13);
            entity.Property(e => e.RegimenFiscal).HasMaxLength(150);
            entity.Property(e => e.Domicilio).HasMaxLength(500);
            entity.Property(e => e.CodigoPostal).HasMaxLength(10);
            entity.Property(e => e.Telefono).HasMaxLength(30);
            entity.Property(e => e.Correo).HasMaxLength(256);
            entity.Property(e => e.SitioWeb).HasMaxLength(300);
            entity.Property(e => e.Lema).HasMaxLength(200);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Departamento>(entity =>
        {
            entity.ToTable("Departamentos");
            entity.Property(d => d.Nombre).HasMaxLength(100).IsRequired();
            entity.HasIndex(d => d.Nombre).IsUnique();
            entity.Property(d => d.Descripcion).HasMaxLength(500);
            entity.Property(d => d.Color).HasMaxLength(10).IsRequired();
            // Persona ↔ Departamento se referencian entre sí: sin cascadas en la base.
            entity.HasOne(d => d.Responsable).WithMany().HasForeignKey(d => d.ResponsableId).OnDelete(DeleteBehavior.ClientSetNull);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Puesto>(entity =>
        {
            entity.ToTable("Puestos");
            entity.Property(p => p.Nombre).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Descripcion).HasMaxLength(1000);
            entity.HasIndex(p => new { p.DepartamentoId, p.Nombre }).IsUnique();
            entity.HasOne(p => p.Departamento).WithMany(d => d.Puestos).HasForeignKey(p => p.DepartamentoId).OnDelete(DeleteBehavior.Restrict);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Persona>(entity =>
        {
            entity.ToTable("Personas");
            entity.Property(p => p.NombreCompleto).HasMaxLength(150).IsRequired();
            entity.Property(p => p.Correo).HasMaxLength(256);
            entity.Property(p => p.Telefono).HasMaxLength(30);
            entity.Property(p => p.Tipo).HasConversion<string>().HasMaxLength(20);
            entity.Property(p => p.CostoHora).HasPrecision(18, 2);
            entity.Property(p => p.MonedaCosto).HasMaxLength(3).IsRequired();
            entity.Property(p => p.Notas).HasMaxLength(2000);
            entity.HasIndex(p => p.UsuarioId).IsUnique().HasFilter("[UsuarioId] IS NOT NULL");
            entity.HasOne(p => p.Departamento).WithMany(d => d.Personas).HasForeignKey(p => p.DepartamentoId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(p => p.Puesto).WithMany().HasForeignKey(p => p.PuestoId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(p => p.Usuario).WithMany().HasForeignKey(p => p.UsuarioId).OnDelete(DeleteBehavior.SetNull);
            ConfigurarAuditoria(entity);
        });
    }

    private static void ConfigurarComercial(ModelBuilder builder)
    {
        builder.Entity<ServicioComercial>(entity =>
        {
            entity.ToTable("ServiciosComerciales");
            entity.Property(s => s.Nombre).HasMaxLength(150).IsRequired();
            entity.HasIndex(s => s.Nombre).IsUnique();
            entity.Property(s => s.Categoria).HasConversion<string>().HasMaxLength(30);
            entity.Property(s => s.Descripcion).HasMaxLength(1000);
            entity.Property(s => s.Modalidad).HasConversion<string>().HasMaxLength(30);
            entity.Property(s => s.PrecioBase).HasPrecision(18, 2);
            entity.Property(s => s.CostoEstimado).HasPrecision(18, 2);
            entity.Property(s => s.Moneda).HasMaxLength(3).IsRequired();
            entity.Property(s => s.Unidad).HasMaxLength(50);
            entity.Property(s => s.Entregables).HasMaxLength(1000);
            entity.Property(s => s.TiempoEstimado).HasMaxLength(100);
            entity.Property(s => s.Estado).HasConversion<string>().HasMaxLength(30);
            entity.Property(s => s.Notas).HasMaxLength(2000);
            entity.HasOne(s => s.Producto).WithMany().HasForeignKey(s => s.ProductoId).OnDelete(DeleteBehavior.SetNull);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Oportunidad>(entity =>
        {
            entity.ToTable("Oportunidades");
            entity.Property(o => o.Titulo).HasMaxLength(200).IsRequired();
            entity.Property(o => o.Etapa).HasConversion<string>().HasMaxLength(30);
            entity.Property(o => o.Origen).HasConversion<string>().HasMaxLength(30);
            entity.Property(o => o.ValorEstimado).HasPrecision(18, 2);
            entity.Property(o => o.Moneda).HasMaxLength(3).IsRequired();
            entity.Property(o => o.MotivoPerdida).HasMaxLength(500);
            entity.Property(o => o.Notas).HasMaxLength(2000);
            entity.HasIndex(o => new { o.Etapa, o.Orden });
            entity.HasOne(o => o.Cliente).WithMany().HasForeignKey(o => o.ClienteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(o => o.Producto).WithMany().HasForeignKey(o => o.ProductoId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(o => o.Responsable).WithMany().HasForeignKey(o => o.ResponsableId).OnDelete(DeleteBehavior.SetNull);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<Cotizacion>(entity =>
        {
            entity.ToTable("Cotizaciones");
            entity.Property(c => c.Folio).HasMaxLength(20).IsRequired();
            entity.HasIndex(c => new { c.Folio, c.Version }).IsUnique();
            entity.Property(c => c.Titulo).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Estado).HasConversion<string>().HasMaxLength(30);
            entity.Property(c => c.Moneda).HasMaxLength(3).IsRequired();
            entity.Property(c => c.TasaIva).HasPrecision(5, 4);
            entity.Property(c => c.Subtotal).HasPrecision(18, 2);
            entity.Property(c => c.Descuento).HasPrecision(18, 2);
            entity.Property(c => c.Iva).HasPrecision(18, 2);
            entity.Property(c => c.Total).HasPrecision(18, 2);
            entity.Property(c => c.CostoEstimado).HasPrecision(18, 2);
            entity.Property(c => c.CondicionPago).HasConversion<string>().HasMaxLength(30);
            entity.Property(c => c.TipoTrabajo).HasConversion<string>().HasMaxLength(30);
            entity.Property(c => c.Condiciones).HasMaxLength(4000);
            entity.Property(c => c.NotasInternas).HasMaxLength(2000);
            entity.HasOne(c => c.Cliente).WithMany().HasForeignKey(c => c.ClienteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.Oportunidad).WithMany(o => o.Cotizaciones).HasForeignKey(c => c.OportunidadId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(c => c.Responsable).WithMany().HasForeignKey(c => c.ResponsableId).OnDelete(DeleteBehavior.SetNull);
            ConfigurarAuditoria(entity);
        });

        builder.Entity<CotizacionLinea>(entity =>
        {
            entity.ToTable("CotizacionLineas");
            entity.Property(l => l.Concepto).HasMaxLength(200).IsRequired();
            entity.Property(l => l.Descripcion).HasMaxLength(1000);
            entity.Property(l => l.Unidad).HasMaxLength(50);
            entity.Property(l => l.Cantidad).HasPrecision(18, 2);
            entity.Property(l => l.PrecioUnitario).HasPrecision(18, 2);
            entity.Property(l => l.DescuentoPorcentaje).HasPrecision(5, 2);
            entity.Property(l => l.CostoUnitario).HasPrecision(18, 2);
            entity.Property(l => l.Importe).HasPrecision(18, 2);
            entity.HasOne(l => l.Cotizacion).WithMany(c => c.Lineas).HasForeignKey(l => l.CotizacionId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CotizacionEnvio>(entity =>
        {
            entity.ToTable("CotizacionEnvios");
            entity.Property(e => e.Para).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Copia).HasMaxLength(500);
            entity.Property(e => e.Asunto).HasMaxLength(300).IsRequired();
            entity.Property(e => e.EnviadoPor).HasMaxLength(256);
            entity.Property(e => e.IdProveedor).HasMaxLength(100);
            entity.Property(e => e.Error).HasMaxLength(1000);
            entity.HasOne(e => e.Cotizacion).WithMany(c => c.Envios).HasForeignKey(e => e.CotizacionId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurarAuditoria<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity)
        where T : EntidadBase
    {
        entity.Property(e => e.CreadoPor).HasMaxLength(256);
        entity.Property(e => e.ModificadoPor).HasMaxLength(256);
    }
}
