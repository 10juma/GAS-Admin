using System.ComponentModel.DataAnnotations;
using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Navegacion;
using GAS.Admin.Services.Soporte;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Soporte;

public sealed record Opcion(int Id, string Nombre, string? Extra = null);

public class TicketInput
{
    [Required(ErrorMessage = "Escribe el asunto.")]
    [StringLength(200, ErrorMessage = "El asunto no puede exceder 200 caracteres.")]
    public string Asunto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Describe lo que necesita el cliente.")]
    [StringLength(8000, ErrorMessage = "La descripción no puede exceder 8000 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    public Prioridad Prioridad { get; set; } = Prioridad.Media;
    public CategoriaTicket Categoria { get; set; } = CategoriaTicket.Duda;
    public int? ProductoId { get; set; }
    public int? ClienteId { get; set; }
    public int? CuentaProductoId { get; set; }

    [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres.")]
    public string? ContactoNombre { get; set; }

    [EmailAddress(ErrorMessage = "El correo del contacto no es válido.")]
    [StringLength(256, ErrorMessage = "El correo no puede exceder 256 caracteres.")]
    public string? ContactoEmail { get; set; }

    public string? AsignadoId { get; set; }
}

public class TicketModalVm
{
    public TicketInput Input { get; init; } = new();
    public List<Opcion> Productos { get; init; } = [];
    public List<Opcion> Clientes { get; init; } = [];
    public List<Opcion> Cuentas { get; init; } = [];
    public List<OpcionUsuario> Usuarios { get; init; } = [];
}

[Authorize(Policy = Permisos.Soporte.Ver)]
public class IndexModel : PageModel
{
    private const int Limite = 300;

    private readonly GasDbContext _db;
    private readonly ServicioSoporte _soporte;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, ServicioSoporte soporte, ContadoresMenu contadores)
    {
        _db = db;
        _soporte = soporte;
        _contadores = contadores;
    }

    /// <summary>abiertos (por defecto), mios, sinasignar, vencidos, espera, escalados, resueltos o todos.</summary>
    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }
    [BindProperty(SupportsGet = true)] public int? Producto { get; set; }
    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Soporte.Gestionar);
    public DateTime Ahora { get; } = DateTime.UtcNow;

    public sealed record Fila(Ticket Ticket, string? Producto, string? Para, string? Asignado, int Mensajes);

    public List<Fila> Tickets { get; private set; } = [];
    public Dictionary<string, int> Conteos { get; private set; } = [];
    public List<Opcion> Productos { get; private set; } = [];
    public TimeSpan? PrimeraRespuestaPromedio { get; private set; }
    public decimal? CumplimientoSla { get; private set; }
    public int Resueltos7d { get; private set; }
    public List<SlaPrioridad> Sla { get; private set; } = [];
    public bool HayMas { get; private set; }
    public TicketModalVm? Modal { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    // ---------- Nuevo ticket ----------

    public async Task<IActionResult> OnGetNuevoAsync()
    {
        if (!PuedeGestionar) return Forbid();
        return Partial("_TicketModal", await ModalAsync(new TicketInput { AsignadoId = User.UsuarioId() }));
    }

    public async Task<IActionResult> OnPostCrearAsync([Bind(Prefix = "Input")] TicketInput input)
    {
        if (!PuedeGestionar) return Forbid();
        if (input.ProductoId is not null && !await _db.Productos.AnyAsync(p => p.Id == input.ProductoId)) input.ProductoId = null;
        if (input.ClienteId is not null && !await _db.Clientes.AnyAsync(c => c.Id == input.ClienteId)) input.ClienteId = null;
        if (input.CuentaProductoId is not null)
        {
            var cuenta = await _db.CuentasProducto.AsNoTracking().Include(c => c.Conexion).FirstOrDefaultAsync(c => c.Id == input.CuentaProductoId);
            if (cuenta is null) input.CuentaProductoId = null;
            else
            {
                input.ProductoId ??= cuenta.Conexion.ProductoId;
                input.ClienteId ??= cuenta.ClienteId;
                input.ContactoEmail ??= cuenta.Email;
                input.ContactoNombre ??= cuenta.Nombre;
            }
        }
        if (input.ClienteId is not null && string.IsNullOrWhiteSpace(input.ContactoEmail))
        {
            var cli = await _db.Clientes.AsNoTracking().FirstAsync(c => c.Id == input.ClienteId);
            input.ContactoEmail = cli.ContactoEmail;
            input.ContactoNombre ??= cli.ContactoNombre;
        }
        if (input.AsignadoId is not null && !await _db.Users.AnyAsync(u => u.Id == input.AsignadoId)) input.AsignadoId = null;
        if (!ModelState.IsValid)
        {
            await CargarAsync();
            Modal = await ModalAsync(input);
            return Page();
        }
        var t = await _soporte.CrearAsync(new DatosTicketNuevo(
            input.Asunto, input.Descripcion, CanalTicket.Interno, input.Prioridad, input.Categoria,
            input.ProductoId, input.ClienteId, input.CuentaProductoId, Opciones.Limpio(input.ContactoNombre), Opciones.Limpio(input.ContactoEmail),
            input.AsignadoId, Opciones.Limpio(input.ContactoNombre) ?? User.NombreVisible(), Opciones.Limpio(input.ContactoEmail), null));
        _contadores.Invalidar();
        this.ToastSuccess($"Ticket {t.Folio} registrado.");
        return RedirectToPage("/Soporte/Detalle/Index", new { id = t.Id });
    }

    // ---------- SLA ----------

    /// <summary>Una fila del modal de SLA; vacía = esa prioridad no tiene tiempos.</summary>
    public sealed class SlaFila
    {
        public Prioridad Prioridad { get; set; }
        public int? PrimeraRespuestaHoras { get; set; }
        public int? ResolucionHoras { get; set; }
    }

    public async Task<IActionResult> OnGetSlaAsync()
    {
        var actuales = await _db.SlaPrioridades.AsNoTracking().ToDictionaryAsync(s => s.Prioridad);
        // Siempre las cuatro prioridades, aunque aún no tengan tiempos capturados.
        var filas = Enum.GetValues<Prioridad>().OrderByDescending(p => p)
            .Select(p => actuales.TryGetValue(p, out var s)
                ? new SlaFila { Prioridad = p, PrimeraRespuestaHoras = s.PrimeraRespuestaHoras, ResolucionHoras = s.ResolucionHoras }
                : new SlaFila { Prioridad = p })
            .ToList();
        return Partial("_SlaModal", (filas, PuedeGestionar));
    }

    public async Task<IActionResult> OnPostSlaAsync(List<SlaFila> sla)
    {
        if (!PuedeGestionar) return Forbid();
        var actuales = await _db.SlaPrioridades.ToDictionaryAsync(s => s.Prioridad);
        foreach (var s in sla.Where(x => Enum.IsDefined(x.Prioridad)))
        {
            actuales.TryGetValue(s.Prioridad, out var a);
            if (s.PrimeraRespuestaHoras is null && s.ResolucionHoras is null)
            {
                if (a is not null) _db.SlaPrioridades.Remove(a); // sin tiempos para esa prioridad
                continue;
            }
            if (s.PrimeraRespuestaHoras is null or < 1 or > 720 || s.ResolucionHoras is null or < 1 or > 2160 || s.ResolucionHoras < s.PrimeraRespuestaHoras)
            {
                this.ToastWarning($"Revisa los tiempos de {Etiquetas.Texto(s.Prioridad)}: captura ambos, la resolución debe ser mayor o igual a la primera respuesta (1 a 2160 h). Déjalos vacíos para no tener SLA.");
                return RedirectToPage("Index");
            }
            if (a is null) _db.SlaPrioridades.Add(a = new SlaPrioridad { Prioridad = s.Prioridad });
            a.PrimeraRespuestaHoras = s.PrimeraRespuestaHoras.Value;
            a.ResolucionHoras = s.ResolucionHoras.Value;
        }
        await _db.SaveChangesAsync();
        this.ToastSuccess("Tiempos de servicio actualizados. Aplican a los tickets nuevos y a los que cambien de prioridad.");
        return RedirectToPage("Index");
    }

    private async Task<TicketModalVm> ModalAsync(TicketInput input) => new()
    {
        Input = input,
        Productos = await _db.Productos.AsNoTracking().OrderBy(p => p.Nombre).Select(p => new Opcion(p.Id, p.Nombre, null)).ToListAsync(),
        Clientes = await _db.Clientes.AsNoTracking().OrderBy(c => c.NombreComercial ?? c.Nombre)
            .Select(c => new Opcion(c.Id, c.NombreComercial ?? c.Nombre, c.ContactoEmail)).ToListAsync(),
        Cuentas = await _db.CuentasProducto.AsNoTracking().Where(c => c.Estado != "cancelada").OrderBy(c => c.Nombre).Take(1000)
            .Select(c => new Opcion(c.Id, c.Nombre + " · " + c.Conexion.Producto.Nombre, c.Conexion.ProductoId.ToString())).ToListAsync(),
        Usuarios = await Opciones.ResponsablesAsync(_db, input.AsignadoId)
    };

    private async Task CargarAsync()
    {
        var ahora = Ahora;
        var yo = User.UsuarioId();
        Productos = await _db.Productos.AsNoTracking().OrderBy(p => p.Nombre).Select(p => new Opcion(p.Id, p.Nombre, null)).ToListAsync();
        Sla = await _db.SlaPrioridades.AsNoTracking().ToListAsync();
        Sla = Sla.OrderByDescending(s => s.Prioridad).ToList();

        var q = _db.Tickets.AsNoTracking().AsQueryable();
        if (Producto is not null) q = q.Where(t => t.ProductoId == Producto);
        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var b = Buscar.Trim();
            q = q.Where(t => t.Folio.Contains(b) || t.Asunto.Contains(b) || (t.ContactoEmail != null && t.ContactoEmail.Contains(b))
                             || (t.ContactoNombre != null && t.ContactoNombre.Contains(b)) || t.Mensajes.Any(m => m.Texto.Contains(b)));
        }

        var basica = await q.Select(t => new
        {
            t.Estado, t.AsignadoId, t.PrimeraRespuestaUtc, t.VencePrimeraRespuestaUtc, t.VenceResolucionUtc, t.CreadoUtc, t.ResueltoUtc
        }).ToListAsync();
        bool Abierto(EstadoTicket e) => e is not (EstadoTicket.Resuelto or EstadoTicket.Cerrado);
        Conteos = new()
        {
            ["abiertos"] = basica.Count(x => Abierto(x.Estado)),
            ["mios"] = basica.Count(x => Abierto(x.Estado) && x.AsignadoId == yo),
            ["sinasignar"] = basica.Count(x => Abierto(x.Estado) && x.AsignadoId == null),
            ["vencidos"] = basica.Count(x => Abierto(x.Estado) && ((x.PrimeraRespuestaUtc == null && x.VencePrimeraRespuestaUtc < ahora) || x.VenceResolucionUtc < ahora)),
            ["espera"] = basica.Count(x => x.Estado == EstadoTicket.EnEspera),
            ["escalados"] = basica.Count(x => x.Estado == EstadoTicket.Escalado),
            ["resueltos"] = basica.Count(x => !Abierto(x.Estado)),
            ["todos"] = basica.Count
        };

        // Indicadores de los últimos 30 días.
        var hace30 = ahora.AddDays(-30);
        var recientes = basica.Where(x => x.CreadoUtc >= hace30 && x.PrimeraRespuestaUtc != null).ToList();
        PrimeraRespuestaPromedio = recientes.Count == 0 ? null : TimeSpan.FromMinutes(recientes.Average(x => (x.PrimeraRespuestaUtc!.Value - x.CreadoUtc).TotalMinutes));
        var medibles = basica.Where(x => x.CreadoUtc >= hace30 && x.VencePrimeraRespuestaUtc != null && (x.PrimeraRespuestaUtc != null || x.VencePrimeraRespuestaUtc < ahora)).ToList();
        CumplimientoSla = medibles.Count == 0 ? null : Math.Round((decimal)medibles.Count(x => x.PrimeraRespuestaUtc <= x.VencePrimeraRespuestaUtc) / medibles.Count, 3);
        Resueltos7d = basica.Count(x => x.ResueltoUtc >= ahora.AddDays(-7));

        q = Vista switch
        {
            "mios" => q.Where(t => t.Estado != EstadoTicket.Resuelto && t.Estado != EstadoTicket.Cerrado && t.AsignadoId == yo),
            "sinasignar" => q.Where(t => t.Estado != EstadoTicket.Resuelto && t.Estado != EstadoTicket.Cerrado && t.AsignadoId == null),
            "vencidos" => q.Where(t => t.Estado != EstadoTicket.Resuelto && t.Estado != EstadoTicket.Cerrado
                                       && ((t.PrimeraRespuestaUtc == null && t.VencePrimeraRespuestaUtc < ahora) || t.VenceResolucionUtc < ahora)),
            "espera" => q.Where(t => t.Estado == EstadoTicket.EnEspera),
            "escalados" => q.Where(t => t.Estado == EstadoTicket.Escalado),
            "resueltos" => q.Where(t => t.Estado == EstadoTicket.Resuelto || t.Estado == EstadoTicket.Cerrado),
            "todos" => q,
            _ => q.Where(t => t.Estado != EstadoTicket.Resuelto && t.Estado != EstadoTicket.Cerrado)
        };
        var filas = await q
            .OrderBy(t => t.Estado == EstadoTicket.Resuelto || t.Estado == EstadoTicket.Cerrado)
            .ThenBy(t => t.VenceResolucionUtc ?? DateTime.MaxValue)
            .ThenByDescending(t => t.UltimaActividadUtc)
            .Take(Limite + 1)
            .Select(t => new Fila(t,
                t.Producto == null ? null : t.Producto.Nombre,
                t.Cliente != null ? (t.Cliente.NombreComercial ?? t.Cliente.Nombre) : t.CuentaProducto != null ? t.CuentaProducto.Nombre : t.ContactoNombre ?? t.ContactoEmail,
                t.Asignado == null ? null : (t.Asignado.NombreCompleto == "" ? t.Asignado.Email : t.Asignado.NombreCompleto),
                t.Mensajes.Count))
            .ToListAsync();
        HayMas = filas.Count > Limite;
        Tickets = filas.Take(Limite).ToList();
    }
}
