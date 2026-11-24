using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace GAS.Admin.Models.Entities;

// Despliegue automático: GAS-Admin se conecta por SSH (con llave) a un servidor, descarga el código
// de GitHub, compila la imagen de Docker en el servidor, aplica migraciones y reemplaza los contenedores.
// Los datos sensibles (dirección del servidor, llave privada, variables de los contenedores) van cifrados
// en LlavesConfiguracion bajo "Despliegue:"; aquí solo queda lo que no es secreto.

public enum EstadoServidor { SinPreparar, Listo, ConError }

public enum EstadoEjecucion { EnCola, EnCurso, Exitoso, Fallido, Revertido, Interrumpido }

[DisplayName("Servidor")]
public class Servidor : EntidadBase
{
    /// <summary>Usuario que GAS-Admin crea en el servidor para desplegar (miembro del grupo docker).</summary>
    public const string UsuarioDespliegue = "gasdeploy";

    [Display(Name = "Nombre")] public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Puerto SSH")] public int Puerto { get; set; } = 22;
    [Display(Name = "Usuario de despliegue")] public string Usuario { get; set; } = UsuarioDespliegue;
    /// <summary>Huella SHA-256 de la llave del servidor, registrada al prepararlo. Si cambia, GAS-Admin no se conecta.</summary>
    [Display(Name = "Huella del servidor")] public string? HuellaHost { get; set; }
    [Display(Name = "Estado")] public EstadoServidor Estado { get; set; } = EstadoServidor.SinPreparar;
    [Display(Name = "Preparado")] public DateTime? PreparadoUtc { get; set; }
    [Display(Name = "Última conexión")] public DateTime? UltimaConexionUtc { get; set; }
    [Display(Name = "Sistema")] public string? Sistema { get; set; }
    [Display(Name = "Último error")] public string? UltimoError { get; set; }
    [Display(Name = "Notas")] public string? Notas { get; set; }

    public override string DescripcionAuditoria => Nombre;

    public static string ClaveHost(int id) => $"Despliegue:Servidores:{id}:Host";
    public static string ClaveLlavePrivada(int id) => $"Despliegue:Servidores:{id}:LlavePrivada";
    public static string PrefijoLlaves(int id) => $"Despliegue:Servidores:{id}:";
}

/// <summary>Cómo se construye y se ejecuta el contenedor de un ambiente.</summary>
[DisplayName("Configuración de despliegue")]
public class ConfiguracionDespliegue : EntidadBase
{
    public int AmbienteId { get; set; }
    public Ambiente Ambiente { get; set; } = null!;
    [Display(Name = "Servidor")] public int ServidorId { get; set; }
    public Servidor Servidor { get; set; } = null!;

    /// <summary>Dockerfile dentro del repositorio, p. ej. deploy/web.Dockerfile. Se compila desde la raíz del repositorio.</summary>
    [Display(Name = "Dockerfile")] public string Dockerfile { get; set; } = string.Empty;
    [Display(Name = "Contenedor")] public string Contenedor { get; set; } = string.Empty;
    [Display(Name = "Imagen")] public string Imagen { get; set; } = string.Empty;
    /// <summary>Uno por línea, formato de docker -p: 127.0.0.1:5002:8080.</summary>
    [Display(Name = "Puertos")] public string? Puertos { get; set; }
    [Display(Name = "Red")] public string? Red { get; set; }
    /// <summary>Uno por línea, formato de docker -v: /root/datos:/app/datos[:ro].</summary>
    [Display(Name = "Volúmenes")] public string? Volumenes { get; set; }
    [Display(Name = "Ruta de salud")] public string RutaSalud { get; set; } = "/";
    [Display(Name = "Agregar el secreto de la API GAS")] public bool IncluirSecretoGas { get; set; } = true;

    [Display(Name = "Aplica migraciones")] public bool Migraciones { get; set; }
    [Display(Name = "Dockerfile de migraciones")] public string? DockerfileMigraciones { get; set; }
    [Display(Name = "Respaldar la base antes de migrar")] public bool RespaldarBd { get; set; } = true;
    [Display(Name = "Contenedor de SQL Server")] public string? ContenedorSql { get; set; }
    [Display(Name = "Base de datos")] public string? BaseDatos { get; set; }

    public override string DescripcionAuditoria => Contenedor;

    public static string ClaveVariables(int ambienteId) => $"Despliegue:Ambientes:{ambienteId}:Variables";
}

/// <summary>
/// Una ejecución de despliegue (uno o varios ambientes del mismo producto y servidor) con su bitácora.
/// No hereda EntidadBase: la bitácora cambia cada segundo y no debe llenar la auditoría. El resultado
/// queda auditado en los registros de Despliegue que se crean al terminar.
/// </summary>
public class EjecucionDespliegue
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    public int ServidorId { get; set; }
    public Servidor Servidor { get; set; } = null!;
    /// <summary>Ids de los ambientes, separados por coma.</summary>
    public string Ambientes { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public string? Commit { get; set; }
    public string? MensajeCommit { get; set; }
    public EstadoEjecucion Estado { get; set; } = EstadoEjecucion.EnCola;
    public string? Resumen { get; set; }
    public string IniciadoPor { get; set; } = string.Empty;
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? InicioUtc { get; set; }
    public DateTime? FinUtc { get; set; }
    public string Bitacora { get; set; } = string.Empty;

    public List<int> IdsAmbientes() =>
        Ambientes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

    public bool Terminada => Estado is not (EstadoEjecucion.EnCola or EstadoEjecucion.EnCurso);
}
