using System.ComponentModel.DataAnnotations;
using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Despliegues;
using GAS.Admin.Services.Infraestructura;
using GAS.Admin.Services.Proyectos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura.Ambientes;

public class AmbienteInput
{
    [Required(ErrorMessage = "Ingresa el nombre del ambiente.")]
    [StringLength(150, ErrorMessage = "El nombre no puede exceder 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;
    public int? ProductoId { get; set; }
    public TipoAmbiente Tipo { get; set; } = TipoAmbiente.Produccion;
    public PlataformaAmbiente Plataforma { get; set; } = PlataformaAmbiente.Web;
    [StringLength(500, ErrorMessage = "La URL no puede exceder 500 caracteres.")]
    public string? Url { get; set; }
    public int? HostingId { get; set; }
    public int? RepositorioId { get; set; }
    [StringLength(100, ErrorMessage = "La rama no puede exceder 100 caracteres.")]
    public string? Rama { get; set; }
    public EstadoAmbiente Estado { get; set; } = EstadoAmbiente.Operativo;
    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }
}

public class DespliegueInput
{
    [Required(ErrorMessage = "Ingresa la versión desplegada.")]
    [StringLength(50, ErrorMessage = "La versión no puede exceder 50 caracteres.")]
    public string Version { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public ResultadoDespliegue Resultado { get; set; } = ResultadoDespliegue.Exitoso;
    [StringLength(150, ErrorMessage = "La referencia no puede exceder 150 caracteres.")]
    public string? Referencia { get; set; }
    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }
}

public class ConfigDespliegueInput
{
    public int? ServidorId { get; set; }
    [StringLength(200)] public string? Dockerfile { get; set; }
    [StringLength(100)] public string? Contenedor { get; set; }
    [StringLength(100)] public string? Imagen { get; set; }
    [StringLength(500)] public string? Puertos { get; set; }
    [StringLength(100)] public string? Red { get; set; }
    [StringLength(2000)] public string? Volumenes { get; set; }
    [StringLength(200)] public string? RutaSalud { get; set; } = "/";
    public bool IncluirSecretoGas { get; set; } = true;
    public bool Migraciones { get; set; }
    [StringLength(200)] public string? DockerfileMigraciones { get; set; }
    public bool RespaldarBd { get; set; } = true;
    [StringLength(100)] public string? ContenedorSql { get; set; }
    [StringLength(128)] public string? BaseDatos { get; set; }
}

public class ConfigDespliegueModalVm
{
    public int AmbienteId { get; init; }
    public string Ambiente { get; init; } = string.Empty;
    public string? Repositorio { get; init; }
    public string? Url { get; init; }
    public bool Existe { get; init; }
    public ConfigDespliegueInput Input { get; init; } = new();
    public List<Opcion> Servidores { get; init; } = [];
    public List<string> VariablesGuardadas { get; init; } = [];
    public string? Variables { get; init; }
    public bool ProductoConectado { get; init; }
    public string? Volver { get; init; }
}

public class DesplegarModalVm
{
    public int AmbienteId { get; init; }
    public string Producto { get; init; } = string.Empty;
    public string Servidor { get; init; } = string.Empty;
    public string? Repositorio { get; init; }
    public string Referencia { get; init; } = string.Empty;
    public List<(int Id, string Nombre, string Detalle, bool Migra)> Ambientes { get; init; } = [];
    public string? Volver { get; init; }
}

public class AmbienteModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public AmbienteInput Input { get; init; } = new();
    public string? Version { get; init; }
    public DateTime? UltimoDespliegueUtc { get; init; }
    public List<Despliegue> Despliegues { get; init; } = [];
    public int IncidentesAbiertos { get; init; }
    public List<Opcion> Productos { get; init; } = [];
    public List<Opcion> Servicios { get; init; } = [];
    public List<Opcion> Repositorios { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
    public string? Volver { get; init; }
}

public class DespliegueModalVm
{
    public int AmbienteId { get; init; }
    public string Ambiente { get; init; } = string.Empty;
    public string? VersionActual { get; init; }
    public DespliegueInput Input { get; init; } = new();
    public string? Volver { get; init; }
}

[Authorize(Policy = Permisos.Infraestructura.Ver)]
public partial class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioInfraestructura _infra;
    private readonly ServicioDespliegues _despliegues;

    public IndexModel(GasDbContext db, ServicioInfraestructura infra, ServicioDespliegues despliegues)
    {
        _db = db;
        _infra = infra;
        _despliegues = despliegues;
    }

    /// <summary>Despliegue automático (servidores y credenciales de producción): solo Admin.</summary>
    public bool EsAdmin => User.IsInRole(RolesBase.Admin);
    public HashSet<int> Automatizados { get; private set; } = [];
    public ConfigDespliegueModalVm? ModalConfig { get; private set; }

    [BindProperty(SupportsGet = true)] public int? Producto { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Infraestructura.Gestionar);

    public sealed record Fila(Ambiente Ambiente, string? Hosting, string? Repositorio, int Incidentes, MonitorUptime? Monitor = null);
    public sealed record Grupo(int ProductoId, string Producto, List<Fila> Ambientes);

    public List<Grupo> Grupos { get; private set; } = [];
    public List<Opcion> Productos { get; private set; } = [];
    public Dictionary<EstadoAmbiente, int> PorEstado { get; private set; } = [];
    public int Total { get; private set; }
    public AmbienteModalVm? Modal { get; private set; }
    public DespliegueModalVm? ModalDespliegue { get; private set; }

    public string VolverActual => Request.Path + Request.QueryString;

    public async Task OnGetAsync() => await CargarAsync();

    // ---------- Ambientes ----------

    public async Task<IActionResult> OnGetNuevoAsync(int? productoId, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        return Partial("_AmbienteModal", await ModalAsync(null, new AmbienteInput { ProductoId = productoId }, volver));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id, string? volver)
    {
        var a = await _db.Ambientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        return Partial("_AmbienteModal", await ModalAsync(a, Desde(a), volver));
    }

    public async Task<IActionResult> OnPostCrearAsync([Bind(Prefix = "Input")] AmbienteInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarAsync(input, null);
        if (ModelState.IsValid)
        {
            var a = new Ambiente();
            Aplicar(a, input);
            _db.Ambientes.Add(a);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"Ambiente {a.Nombre} registrado.");
            return this.VolverA(volver, "Index");
        }
        await CargarAsync();
        Modal = await ModalAsync(null, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id, [Bind(Prefix = "Input")] AmbienteInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var a = await _db.Ambientes.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        await ValidarAsync(input, id);
        if (ModelState.IsValid)
        {
            Aplicar(a, input);
            var cambios = _db.ChangeTracker.HasChanges();
            await _db.SaveChangesAsync();
            // Si hay incidentes abiertos, el estado lo dictan ellos (salvo Mantenimiento o Inactivo).
            if (await _db.Incidentes.AnyAsync(i => i.AmbienteId == a.Id && i.Estado != EstadoIncidente.Resuelto)
                && await _infra.SincronizarEstadoAsync(a.Id) is { } estado)
            {
                await _db.SaveChangesAsync();
                this.ToastInfo($"Por sus incidentes abiertos, el ambiente quedó como {Etiquetas.Texto(estado).ToLowerInvariant()}.");
            }
            if (cambios) this.ToastSuccess($"Ambiente {a.Nombre} actualizado.");
            else this.ToastInfo("No hubo cambios que guardar.");
            return this.VolverA(volver, "Index");
        }
        await CargarAsync();
        Modal = await ModalAsync(a, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var a = await _db.Ambientes.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        if (await _db.Incidentes.AnyAsync(i => i.AmbienteId == id) || await _db.Credenciales.AnyAsync(c => c.AmbienteId == id))
        {
            this.ToastWarning($"{a.Nombre} tiene incidentes o credenciales ligadas. Márcalo como Inactivo para conservar el historial.");
            return this.VolverA(volver, "Index");
        }
        _db.Ambientes.Remove(a);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Ambiente {a.Nombre} eliminado junto con su bitácora de despliegues.");
        return this.VolverA(volver, "Index");
    }

    // ---------- Despliegues ----------

    public async Task<IActionResult> OnGetDespliegueAsync(int ambienteId, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var a = await _db.Ambientes.AsNoTracking().Include(x => x.Producto).FirstOrDefaultAsync(x => x.Id == ambienteId);
        if (a is null) return NotFound();
        return Partial("_DespliegueModal", DespliegueVm(a, new DespliegueInput
        {
            Fecha = Fechas.ALocal(DateTime.UtcNow).AddSeconds(-DateTime.UtcNow.Second),
            Referencia = a.Rama
        }, volver));
    }

    public async Task<IActionResult> OnPostDesplegarAsync(int ambienteId, [Bind(Prefix = "Input")] DespliegueInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var a = await _db.Ambientes.Include(x => x.Producto).FirstOrDefaultAsync(x => x.Id == ambienteId);
        if (a is null) return NotFound();
        if (input.Fecha == default) input.Fecha = Fechas.ALocal(DateTime.UtcNow);
        var fechaUtc = Fechas.ALocalUtc(input.Fecha);
        if (fechaUtc > DateTime.UtcNow.AddMinutes(5)) ModelState.AddModelError(string.Empty, "La fecha del despliegue no puede ser futura.");
        if (ModelState.IsValid)
        {
            var d = new Despliegue
            {
                AmbienteId = a.Id,
                Version = input.Version.Trim(),
                FechaUtc = fechaUtc,
                Resultado = input.Resultado,
                Referencia = Opciones.Limpio(input.Referencia),
                Notas = Opciones.Limpio(input.Notas),
                Realizo = User.NombreVisible()
            };
            _db.Despliegues.Add(d);
            await _infra.ActualizarVersionAsync(a.Id, d);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"Despliegue {d.Version} registrado en {a.Nombre}.");
            if (d.Resultado != ResultadoDespliegue.Exitoso)
                this.ToastWarning("Si afectó a usuarios, registra el incidente para dar seguimiento.");
            return this.VolverA(volver, "Index");
        }
        await CargarAsync();
        ModalDespliegue = DespliegueVm(a, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarDespliegueAsync(int id, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var d = await _db.Despliegues.FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        _db.Despliegues.Remove(d);
        await _db.SaveChangesAsync();
        await _infra.ActualizarVersionAsync(d.AmbienteId);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Despliegue {d.Version} eliminado de la bitácora.");
        return this.VolverA(volver, "Index");
    }

    // ---------- Despliegue automático ----------

    public async Task<IActionResult> OnGetAutomatizarAsync(int ambienteId, string? volver)
    {
        if (!EsAdmin) return Forbid();
        var a = await _db.Ambientes.AsNoTracking().Include(x => x.Producto).Include(x => x.Repositorio).FirstOrDefaultAsync(x => x.Id == ambienteId);
        if (a is null) return NotFound();
        var c = await _db.ConfiguracionesDespliegue.AsNoTracking().FirstOrDefaultAsync(x => x.AmbienteId == ambienteId);
        return Partial("_ConfigDespliegueModal", await ConfigVmAsync(a, c is null ? Sugerida(a) : DesdeConfig(c), c is not null, null, volver));
    }

    public async Task<IActionResult> OnPostAutomatizarAsync(int ambienteId, [Bind(Prefix = "Config")] ConfigDespliegueInput input, string? variables, string? volver)
    {
        if (!EsAdmin) return Forbid();
        var a = await _db.Ambientes.Include(x => x.Producto).Include(x => x.Repositorio).FirstOrDefaultAsync(x => x.Id == ambienteId);
        if (a is null) return NotFound();
        var c = await _db.ConfiguracionesDespliegue.FirstOrDefaultAsync(x => x.AmbienteId == ambienteId);
        await ValidarConfigAsync(input, a);
        if (ModelState.IsValid)
        {
            var nuevo = c is null;
            c ??= _db.ConfiguracionesDespliegue.Add(new ConfiguracionDespliegue { AmbienteId = a.Id }).Entity;
            c.ServidorId = input.ServidorId!.Value;
            c.Dockerfile = input.Dockerfile!.Trim();
            c.Contenedor = input.Contenedor!.Trim();
            c.Imagen = input.Imagen!.Trim();
            c.Puertos = Opciones.Limpio(string.Join('\n', EjecutorDespliegue.Lineas(input.Puertos)));
            c.Red = Opciones.Limpio(input.Red);
            c.Volumenes = Opciones.Limpio(string.Join('\n', EjecutorDespliegue.Lineas(input.Volumenes)));
            c.RutaSalud = "/" + (input.RutaSalud ?? "").Trim().TrimStart('/');
            c.IncluirSecretoGas = input.IncluirSecretoGas;
            c.Migraciones = input.Migraciones;
            c.DockerfileMigraciones = Opciones.Limpio(input.DockerfileMigraciones);
            c.RespaldarBd = input.RespaldarBd;
            c.ContenedorSql = Opciones.Limpio(input.ContenedorSql);
            c.BaseDatos = Opciones.Limpio(input.BaseDatos);
            if (await _despliegues.ActualizarVariablesAsync(a.Id, variables, $"Variables de {a.Producto.Nombre} · {a.Nombre}") is { } errorVars)
            {
                ModelState.AddModelError(string.Empty, errorVars);
            }
            else
            {
                await _db.SaveChangesAsync();
                this.ToastSuccess(nuevo ? $"{a.Nombre} listo para desplegar desde GAS-Admin." : $"Despliegue de {a.Nombre} actualizado.");
                return this.VolverA(volver, "Index");
            }
        }
        await CargarAsync();
        ModalConfig = await ConfigVmAsync(a, input, c is not null, variables, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostQuitarAutomatizacionAsync(int ambienteId, string? volver)
    {
        if (!EsAdmin) return Forbid();
        var c = await _db.ConfiguracionesDespliegue.Include(x => x.Ambiente).FirstOrDefaultAsync(x => x.AmbienteId == ambienteId);
        if (c is null) return NotFound();
        _db.ConfiguracionesDespliegue.Remove(c);
        await _db.SaveChangesAsync();
        await _despliegues.QuitarVariablesAsync(ambienteId, $"Variables de {c.Ambiente.Nombre}");
        this.ToastSuccess($"{c.Ambiente.Nombre} ya no se despliega desde GAS-Admin. Los contenedores siguen corriendo.");
        return this.VolverA(volver, "Index");
    }

    public async Task<IActionResult> OnGetDesplegarAutoAsync(int ambienteId, string? volver)
    {
        if (!EsAdmin) return Forbid();
        var grupo = await _despliegues.GrupoAsync(ambienteId);
        if (grupo.Count == 0) return NotFound();
        var a = grupo.First(g => g.AmbienteId == ambienteId).Ambiente;
        var producto = await _db.Productos.AsNoTracking().Where(p => p.Id == a.ProductoId).Select(p => p.Nombre).FirstAsync();
        var servidor = await _db.Servidores.AsNoTracking().Where(s => s.Id == grupo[0].ServidorId).Select(s => s.Nombre).FirstAsync();
        return Partial("_DesplegarModal", new DesplegarModalVm
        {
            AmbienteId = ambienteId,
            Producto = producto,
            Servidor = servidor,
            Repositorio = a.Repositorio?.Url,
            Referencia = a.Rama ?? a.Repositorio?.RamaPrincipal ?? "main",
            Ambientes = grupo.Select(g => (g.AmbienteId, g.Ambiente.Nombre, $"{g.Contenedor} · {g.Dockerfile}", g.Migraciones)).ToList(),
            Volver = volver
        });
    }

    public async Task<IActionResult> OnPostDesplegarAutoAsync(List<int> ambientes, string? referencia, string? volver)
    {
        if (!EsAdmin) return Forbid();
        var (e, error) = await _despliegues.IniciarAsync(ambientes, referencia ?? "", User.Identity?.Name ?? "Admin");
        if (e is null)
        {
            this.ToastError(error!);
            return this.VolverA(volver, "Index");
        }
        this.ToastInfo($"Despliegue #{e.Id} en marcha. Puedes seguir aquí su avance o cerrar la página.");
        return RedirectToPage("/Infraestructura/Despliegues/Ejecucion", new { id = e.Id });
    }

    private static ConfigDespliegueInput Sugerida(Ambiente a)
    {
        var producto = EjecutorDespliegue.Slug(a.Producto.Nombre);
        var tipo = a.Plataforma == PlataformaAmbiente.Api ? "api" : "web";
        var sufijo = a.Tipo == TipoAmbiente.Produccion ? "" : "-" + EjecutorDespliegue.Slug(a.Tipo.ToString());
        return new ConfigDespliegueInput
        {
            Dockerfile = $"deploy/{tipo}.Dockerfile",
            Contenedor = $"{producto}-{tipo}{sufijo}",
            Imagen = $"{producto}-{tipo}",
            RutaSalud = "/",
            Migraciones = a.Plataforma == PlataformaAmbiente.Api,
            DockerfileMigraciones = a.Plataforma == PlataformaAmbiente.Api ? "deploy/migraciones.Dockerfile" : null
        };
    }

    private static ConfigDespliegueInput DesdeConfig(ConfiguracionDespliegue c) => new()
    {
        ServidorId = c.ServidorId, Dockerfile = c.Dockerfile, Contenedor = c.Contenedor, Imagen = c.Imagen, Puertos = c.Puertos, Red = c.Red,
        Volumenes = c.Volumenes, RutaSalud = c.RutaSalud, IncluirSecretoGas = c.IncluirSecretoGas, Migraciones = c.Migraciones,
        DockerfileMigraciones = c.DockerfileMigraciones, RespaldarBd = c.RespaldarBd, ContenedorSql = c.ContenedorSql, BaseDatos = c.BaseDatos
    };

    private async Task<ConfigDespliegueModalVm> ConfigVmAsync(Ambiente a, ConfigDespliegueInput input, bool existe, string? variables, string? volver) => new()
    {
        AmbienteId = a.Id,
        Ambiente = $"{a.Producto.Nombre} · {a.Nombre}",
        Repositorio = a.Repositorio?.Url,
        Url = a.Url,
        Existe = existe,
        Input = input,
        Servidores = await _db.Servidores.AsNoTracking().OrderBy(s => s.Nombre).Select(s => new Opcion(s.Id, s.Nombre)).ToListAsync(),
        VariablesGuardadas = _despliegues.Variables(a.Id).Keys.OrderBy(k => k).ToList(),
        Variables = variables,
        ProductoConectado = await _db.ConexionesProducto.AnyAsync(c => c.ProductoId == a.ProductoId),
        Volver = volver
    };

    private async Task ValidarConfigAsync(ConfigDespliegueInput i, Ambiente a)
    {
        void Error(string m) => ModelState.AddModelError(string.Empty, m);
        if (i.ServidorId is null || !await _db.Servidores.AnyAsync(s => s.Id == i.ServidorId)) Error("Elige el servidor.");
        if (a.Repositorio is null || Services.Integraciones.ServicioIntegraciones.NombreGitHub(a.Repositorio.Url) is null)
            Error("El ambiente necesita un repositorio de GitHub con su URL (edita el ambiente y el repositorio).");
        if (!RutaRegex().IsMatch(i.Dockerfile ?? "") || i.Dockerfile!.Contains("..")) Error("El Dockerfile debe ser una ruta dentro del repositorio, p. ej. deploy/web.Dockerfile.");
        if (!NombreDockerRegex().IsMatch(i.Contenedor ?? "")) Error("El nombre del contenedor solo admite minúsculas, números, guiones, puntos y guion bajo.");
        if (!NombreDockerRegex().IsMatch(i.Imagen ?? "")) Error("El nombre de la imagen solo admite minúsculas, números, guiones, puntos y guion bajo.");
        foreach (var p in EjecutorDespliegue.Lineas(i.Puertos))
            if (!PuertoRegex().IsMatch(p)) Error($"Puerto «{p}»: usa el formato 127.0.0.1:5002:8080.");
        foreach (var v in EjecutorDespliegue.Lineas(i.Volumenes))
            if (!VolumenRegex().IsMatch(v)) Error($"Volumen «{v}»: usa el formato /ruta/en/servidor:/ruta/en/contenedor[:ro].");
        if (!string.IsNullOrWhiteSpace(i.Red) && !NombreDockerRegex().IsMatch(i.Red.Trim())) Error("El nombre de la red no es válido.");
        if (!string.IsNullOrWhiteSpace(i.RutaSalud) && !RutaSaludRegex().IsMatch(i.RutaSalud.Trim())) Error("La ruta de salud debe ser como /Account/Login.");
        if (i.Migraciones && (!RutaRegex().IsMatch(i.DockerfileMigraciones ?? "") || i.DockerfileMigraciones!.Contains("..")))
            Error("Indica el Dockerfile de migraciones (p. ej. deploy/migraciones.Dockerfile).");
        if (!string.IsNullOrWhiteSpace(i.ContenedorSql) && !NombreDockerRegex().IsMatch(i.ContenedorSql.Trim())) Error("El nombre del contenedor de SQL no es válido.");
        if (i.ServidorId is not null && !string.IsNullOrWhiteSpace(i.Contenedor)
            && await _db.ConfiguracionesDespliegue.AnyAsync(c => c.ServidorId == i.ServidorId && c.AmbienteId != a.Id && c.Contenedor == i.Contenedor.Trim()))
            Error($"Otro ambiente ya usa el contenedor {i.Contenedor.Trim()} en ese servidor.");
        if (i.Migraciones && await _db.ConfiguracionesDespliegue.AnyAsync(c => c.AmbienteId != a.Id && c.Migraciones
                && c.ServidorId == i.ServidorId && c.Ambiente.ProductoId == a.ProductoId && c.Ambiente.RepositorioId == a.RepositorioId))
            Error("Otro ambiente de este producto ya aplica las migraciones; solo uno debe hacerlo.");
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Za-z0-9._/\-]{1,200}$")]
    private static partial System.Text.RegularExpressions.Regex RutaRegex();
    [System.Text.RegularExpressions.GeneratedRegex(@"^[a-z0-9][a-z0-9_.\-]{0,99}$")]
    private static partial System.Text.RegularExpressions.Regex NombreDockerRegex();
    [System.Text.RegularExpressions.GeneratedRegex(@"^((\d{1,3}\.){3}\d{1,3}:)?\d{1,5}:\d{1,5}(/(tcp|udp))?$")]
    private static partial System.Text.RegularExpressions.Regex PuertoRegex();
    [System.Text.RegularExpressions.GeneratedRegex(@"^/[^:\s]*:/[^:\s]*(:(ro|rw))?$")]
    private static partial System.Text.RegularExpressions.Regex VolumenRegex();
    [System.Text.RegularExpressions.GeneratedRegex(@"^/?[A-Za-z0-9._/\-?=&]*$")]
    private static partial System.Text.RegularExpressions.Regex RutaSaludRegex();

    // ---------- Apoyo ----------

    private static AmbienteInput Desde(Ambiente a) => new()
    {
        Nombre = a.Nombre, ProductoId = a.ProductoId, Tipo = a.Tipo, Plataforma = a.Plataforma, Url = a.Url,
        HostingId = a.HostingId, RepositorioId = a.RepositorioId, Rama = a.Rama, Estado = a.Estado, Notas = a.Notas
    };

    private static void Aplicar(Ambiente a, AmbienteInput i)
    {
        a.Nombre = i.Nombre.Trim();
        a.ProductoId = i.ProductoId!.Value;
        a.Tipo = i.Tipo;
        a.Plataforma = i.Plataforma;
        a.Url = Listas.Url(i.Url);
        a.HostingId = i.HostingId;
        a.RepositorioId = i.RepositorioId;
        a.Rama = Opciones.Limpio(i.Rama);
        a.Estado = i.Estado;
        a.Notas = Opciones.Limpio(i.Notas);
    }

    private async Task ValidarAsync(AmbienteInput i, int? id)
    {
        if (i.ProductoId is null || !await _db.Productos.AnyAsync(p => p.Id == i.ProductoId))
            ModelState.AddModelError(string.Empty, "Elige el producto del ambiente.");
        else if (await _db.Ambientes.AnyAsync(a => a.Id != id && a.ProductoId == i.ProductoId && a.Nombre == i.Nombre.Trim()))
            ModelState.AddModelError(string.Empty, $"El producto ya tiene un ambiente llamado {i.Nombre.Trim()}.");
        if (!string.IsNullOrWhiteSpace(i.Url) && Listas.Url(i.Url) is null)
            ModelState.AddModelError(string.Empty, "La URL no es válida.");
        if (i.HostingId is not null && !await _db.ServiciosExternos.AnyAsync(s => s.Id == i.HostingId)) i.HostingId = null;
        if (i.RepositorioId is not null && !await _db.Repositorios.AnyAsync(r => r.Id == i.RepositorioId)) i.RepositorioId = null;
    }

    private static DespliegueModalVm DespliegueVm(Ambiente a, DespliegueInput input, string? volver) => new()
    {
        AmbienteId = a.Id,
        Ambiente = $"{a.Producto?.Nombre} · {a.Nombre}",
        VersionActual = a.Version,
        Input = input,
        Volver = volver
    };

    private async Task<AmbienteModalVm> ModalAsync(Ambiente? a, AmbienteInput input, string? volver) => new()
    {
        Id = a?.Id,
        Input = input,
        Version = a?.Version,
        UltimoDespliegueUtc = a?.UltimoDespliegueUtc,
        Despliegues = a is null ? [] : await _db.Despliegues.AsNoTracking().Where(d => d.AmbienteId == a.Id)
            .OrderByDescending(d => d.FechaUtc).Take(15).ToListAsync(),
        IncidentesAbiertos = a is null ? 0 : await _db.Incidentes.CountAsync(i => i.AmbienteId == a.Id && i.Estado != EstadoIncidente.Resuelto),
        Productos = await Listas.ProductosAsync(_db),
        Servicios = await Listas.ServiciosAsync(_db),
        Repositorios = await Listas.RepositoriosAsync(_db, input.RepositorioId),
        PuedeGestionar = PuedeGestionar,
        CreadoUtc = a?.CreadoUtc,
        CreadoPor = a?.CreadoPor,
        ModificadoUtc = a?.ModificadoUtc,
        ModificadoPor = a?.ModificadoPor,
        Volver = volver
    };

    private async Task CargarAsync()
    {
        Automatizados = (await _db.ConfiguracionesDespliegue.Select(c => c.AmbienteId).ToListAsync()).ToHashSet();
        Productos = await Listas.ProductosAsync(_db);
        var abiertos = await _db.Incidentes.Where(i => i.AmbienteId != null && i.Estado != EstadoIncidente.Resuelto)
            .GroupBy(i => i.AmbienteId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var monitores = (await _db.Monitores.AsNoTracking().Where(m => m.Existe && m.AmbienteId != null).ToListAsync())
            .GroupBy(m => m.AmbienteId!.Value).ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.Estado == EstadoMonitor.Caido).First());
        var q = _db.Ambientes.AsNoTracking().AsQueryable();
        var todos = await q.Select(a => new
            {
                A = a,
                Producto = a.Producto.Nombre,
                Hosting = a.Hosting == null ? null : a.Hosting.Nombre,
                Repositorio = a.Repositorio == null ? null : a.Repositorio.Nombre
            })
            .ToListAsync();
        Total = todos.Count;
        PorEstado = todos.GroupBy(x => x.A.Estado).ToDictionary(g => g.Key, g => g.Count());
        Grupos = todos.Where(x => Producto is null || x.A.ProductoId == Producto)
            .GroupBy(x => new { x.A.ProductoId, x.Producto })
            .OrderBy(g => g.Key.Producto)
            .Select(g => new Grupo(g.Key.ProductoId, g.Key.Producto, g
                .OrderBy(x => x.A.Tipo).ThenBy(x => x.A.Plataforma).ThenBy(x => x.A.Nombre)
                .Select(x => new Fila(x.A, x.Hosting, x.Repositorio, abiertos.GetValueOrDefault(x.A.Id), monitores.GetValueOrDefault(x.A.Id))).ToList()))
            .ToList();
    }
}
