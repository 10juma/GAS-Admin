using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Navegacion;
using GAS.Admin.Services.Soporte;
using GAS.Admin.Pages.Soporte;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Soporte.Detalle;

[Authorize(Policy = Permisos.Soporte.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioSoporte _soporte;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, ServicioSoporte soporte, ContadoresMenu contadores)
    {
        _db = db;
        _soporte = soporte;
        _contadores = contadores;
    }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Soporte.Gestionar);
    public bool PuedeEscalar => PuedeGestionar && User.TienePermiso(Permisos.Proyectos.Gestionar);
    public DateTime Ahora { get; } = DateTime.UtcNow;

    public Ticket T { get; private set; } = null!;
    public List<TicketMensaje> Mensajes { get; private set; } = [];
    public List<OpcionUsuario> Usuarios { get; private set; } = [];
    public List<ArticuloConocimiento> Articulos { get; private set; } = [];
    public List<Opcion> Trabajos { get; private set; } = [];
    public List<Opcion> Personas { get; private set; } = [];
    public List<(string Folio, int Id, string Asunto, EstadoTicket Estado)> Anteriores { get; private set; } = [];
    public bool CorreoConfigurado => _soporte.CorreoConfigurado;

    public async Task<IActionResult> OnGetAsync(int id) => await CargarAsync(id) ? Page() : NotFound();

    private async Task<Ticket?> TicketAsync(int id) => await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);

    public async Task<IActionResult> OnPostResponderAsync(int id, string? texto, bool resolver, int? articuloId)
    {
        if (!PuedeGestionar) return Forbid();
        var t = await TicketAsync(id);
        if (t is null) return NotFound();
        if (string.IsNullOrWhiteSpace(texto))
        {
            this.ToastWarning("Escribe la respuesta.");
            return RedirectToPage(new { id });
        }
        if (t.Estado == EstadoTicket.Cerrado)
        {
            this.ToastWarning("El ticket está cerrado. Reábrelo para responder.");
            return RedirectToPage(new { id });
        }
        var (correo, error) = await _soporte.ResponderAsync(t, texto.Length > 8000 ? texto[..8000] : texto, User.NombreVisible(), User.Identity?.Name, resolver);
        if (articuloId is not null) await _db.Articulos.Where(a => a.Id == articuloId).ExecuteUpdateAsync(s => s.SetProperty(a => a.Usos, a => a.Usos + 1));
        _contadores.Invalidar();
        if (correo) this.ToastSuccess(resolver ? $"Respuesta enviada a {t.ContactoEmail} y ticket resuelto." : $"Respuesta enviada a {t.ContactoEmail}.");
        else
        {
            this.ToastInfo(resolver ? "Respuesta registrada y ticket resuelto." : "Respuesta registrada.");
            if (error is not null) this.ToastWarning(error);
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostNotaAsync(int id, string? texto)
    {
        if (!PuedeGestionar) return Forbid();
        var t = await TicketAsync(id);
        if (t is null) return NotFound();
        if (string.IsNullOrWhiteSpace(texto))
        {
            this.ToastWarning("Escribe la nota.");
            return RedirectToPage(new { id });
        }
        await _soporte.NotaAsync(t, texto.Length > 8000 ? texto[..8000] : texto, User.NombreVisible());
        this.ToastSuccess("Nota interna agregada. El cliente no la ve.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDatosAsync(int id, EstadoTicket estado, Prioridad prioridad, CategoriaTicket categoria, string? asignadoId)
    {
        if (!PuedeGestionar) return Forbid();
        var t = await TicketAsync(id);
        if (t is null) return NotFound();
        if (asignadoId is not null && !await _db.Users.AnyAsync(u => u.Id == asignadoId)) asignadoId = null;

        var cambios = new List<string>();
        var cambioEstado = t.Estado != estado;
        if (t.Estado != estado)
        {
            cambios.Add($"estado: {ServicioSoporte.TextoEstado(t.Estado)} → {ServicioSoporte.TextoEstado(estado)}");
            t.ResueltoUtc = estado is EstadoTicket.Resuelto or EstadoTicket.Cerrado ? t.ResueltoUtc ?? DateTime.UtcNow : null;
            t.Estado = estado;
        }
        if (t.Prioridad != prioridad)
        {
            cambios.Add($"prioridad: {Etiquetas.Texto(t.Prioridad)} → {Etiquetas.Texto(prioridad)}");
            t.Prioridad = prioridad;
            await _soporte.AplicarSlaAsync(t);
        }
        if (t.Categoria != categoria)
        {
            cambios.Add($"categoría: {ServicioSoporte.TextoCategoria(categoria)}");
            t.Categoria = categoria;
        }
        if (t.AsignadoId != asignadoId)
        {
            var nombre = asignadoId is null ? "nadie" : await _db.Users.Where(u => u.Id == asignadoId)
                .Select(u => u.NombreCompleto == "" ? u.Email : u.NombreCompleto).FirstAsync();
            cambios.Add($"asignado a {nombre}");
            t.AsignadoId = asignadoId;
            if (t.Estado == EstadoTicket.Nuevo && asignadoId is not null) t.Estado = EstadoTicket.Abierto;
        }
        if (cambios.Count == 0)
        {
            this.ToastInfo("No hubo cambios que guardar.");
            return RedirectToPage(new { id });
        }
        await _soporte.NotaAsync(t, $"{User.NombreVisible()} cambió {string.Join("; ", cambios)}.", "Sistema", TipoMensajeTicket.Sistema);
        if (cambioEstado) await _soporte.AvisarProductoAsync(t);
        _contadores.Invalidar();
        this.ToastSuccess($"Ticket {t.Folio} actualizado.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEscalarAsync(int id, int? trabajoId, int? responsableId)
    {
        if (!PuedeEscalar) return Forbid();
        var t = await TicketAsync(id);
        if (t is null) return NotFound();
        if (t.TareaId is not null)
        {
            this.ToastInfo("Este ticket ya está escalado.");
            return RedirectToPage(new { id });
        }
        if (trabajoId is not null && !await _db.Trabajos.AnyAsync(x => x.Id == trabajoId)) trabajoId = null;
        if (responsableId is not null && !await _db.Personas.AnyAsync(x => x.Id == responsableId)) responsableId = null;
        var tarea = await _soporte.EscalarAsync(t, trabajoId, responsableId, User.NombreVisible());
        _contadores.Invalidar();
        this.ToastSuccess($"Se creó la tarea \"{tarea.Titulo}\" en el tablero de Proyectos.");
        return RedirectToPage(new { id });
    }

    private async Task<bool> CargarAsync(int id)
    {
        var t = await _db.Tickets.AsNoTracking()
            .Include(x => x.Producto).Include(x => x.Cliente).Include(x => x.CuentaProducto).Include(x => x.Asignado).Include(x => x.Tarea)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return false;
        T = t;
        Mensajes = await _db.TicketMensajes.AsNoTracking().Where(m => m.TicketId == id).OrderBy(m => m.FechaUtc).ThenBy(m => m.Id).ToListAsync();
        Usuarios = await Opciones.ResponsablesAsync(_db, t.AsignadoId);
        Articulos = await _db.Articulos.AsNoTracking().Where(a => a.ProductoId == null || a.ProductoId == t.ProductoId)
            .OrderByDescending(a => a.ProductoId == t.ProductoId && a.Categoria == t.Categoria).ThenByDescending(a => a.Usos).ThenBy(a => a.Titulo)
            .Take(100).ToListAsync();
        if (PuedeEscalar && t.TareaId is null)
        {
            Trabajos = await _db.Trabajos.AsNoTracking().Where(x => x.Estado != EstadoTrabajo.Cerrado && x.Estado != EstadoTrabajo.Cancelado)
                .OrderByDescending(x => x.ProductoId == t.ProductoId).ThenByDescending(x => x.Folio)
                .Select(x => new Opcion(x.Id, x.Folio + " · " + x.Nombre, null)).ToListAsync();
            Personas = await _db.Personas.AsNoTracking().Where(p => p.Activa).OrderBy(p => p.NombreCompleto)
                .Select(p => new Opcion(p.Id, p.NombreCompleto, null)).ToListAsync();
        }
        if (t.ContactoEmail is not null)
        {
            Anteriores = (await _db.Tickets.AsNoTracking().Where(x => x.Id != id && x.ContactoEmail == t.ContactoEmail)
                    .OrderByDescending(x => x.CreadoUtc).Take(5).Select(x => new { x.Folio, x.Id, x.Asunto, x.Estado }).ToListAsync())
                .Select(x => (x.Folio, x.Id, x.Asunto, x.Estado)).ToList();
        }
        return true;
    }
}
