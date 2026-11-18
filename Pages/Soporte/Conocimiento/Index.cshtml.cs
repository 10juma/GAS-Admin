using System.ComponentModel.DataAnnotations;
using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Soporte.Conocimiento;

public class ArticuloInput
{
    [Required(ErrorMessage = "Escribe el título.")]
    [StringLength(200, ErrorMessage = "El título no puede exceder 200 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe el contenido.")]
    [StringLength(20000, ErrorMessage = "El contenido no puede exceder 20000 caracteres.")]
    public string Contenido { get; set; } = string.Empty;

    public int? ProductoId { get; set; }
    public CategoriaTicket Categoria { get; set; } = CategoriaTicket.Duda;

    [StringLength(300, ErrorMessage = "Las etiquetas no pueden exceder 300 caracteres.")]
    public string? Etiquetas { get; set; }

    public bool Publicable { get; set; }
}

public class ArticuloModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public ArticuloInput Input { get; init; } = new();
    public int Usos { get; init; }
    public string? DesdeTicket { get; init; }
    public List<Opcion> Productos { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
}

[Authorize(Policy = Permisos.Soporte.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;

    public IndexModel(GasDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? Producto { get; set; }
    [BindProperty(SupportsGet = true)] public string? Categoria { get; set; }
    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }
    [BindProperty(SupportsGet = true)] public int? DesdeTicket { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Soporte.Gestionar);

    public List<(ArticuloConocimiento Articulo, string? Producto)> Articulos { get; private set; } = [];
    public List<Opcion> Productos { get; private set; } = [];
    public Dictionary<CategoriaTicket, int> PorCategoria { get; private set; } = [];
    public int Total { get; private set; }
    public int Publicables { get; private set; }
    public ArticuloModalVm? Modal { get; private set; }

    public async Task OnGetAsync()
    {
        if (DesdeTicket is not null && PuedeGestionar)
            TempData["GAS.AbrirModal"] = Url.Page("Index", "Nuevo", new { ticketId = DesdeTicket });
        await CargarAsync();
    }

    public async Task<IActionResult> OnGetNuevoAsync(int? ticketId)
    {
        if (!PuedeGestionar) return Forbid();
        var input = new ArticuloInput();
        string? folio = null;
        if (ticketId is not null && await _db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId) is { } t)
        {
            folio = t.Folio;
            var respuesta = await _db.TicketMensajes.AsNoTracking()
                .Where(m => m.TicketId == t.Id && (m.Tipo == TipoMensajeTicket.Respuesta || m.Tipo == TipoMensajeTicket.Nota))
                .OrderByDescending(m => m.Tipo == TipoMensajeTicket.Respuesta).ThenByDescending(m => m.FechaUtc).Select(m => m.Texto).FirstOrDefaultAsync();
            input = new ArticuloInput { Titulo = t.Asunto, Contenido = respuesta ?? "", ProductoId = t.ProductoId, Categoria = t.Categoria };
        }
        return Partial("_ArticuloModal", await ModalAsync(null, input, folio));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        var a = await _db.Articulos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        return Partial("_ArticuloModal", await ModalAsync(a, new ArticuloInput
        {
            Titulo = a.Titulo, Contenido = a.Contenido, ProductoId = a.ProductoId, Categoria = a.Categoria, Etiquetas = a.Etiquetas, Publicable = a.Publicable
        }, null));
    }

    public async Task<IActionResult> OnPostGuardarAsync(int? id, [Bind(Prefix = "Input")] ArticuloInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var a = id is null ? new ArticuloConocimiento() : await _db.Articulos.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        if (input.ProductoId is not null && !await _db.Productos.AnyAsync(p => p.Id == input.ProductoId)) input.ProductoId = null;
        if (DetectorSecretos.PareceSecreto(input.Contenido))
            ModelState.AddModelError(string.Empty, "El contenido parece incluir una contraseña o llave. Quítala antes de guardar.");
        if (!ModelState.IsValid)
        {
            await CargarAsync();
            Modal = await ModalAsync(id is null ? null : a, input, null);
            return Page();
        }
        a.Titulo = input.Titulo.Trim();
        a.Contenido = input.Contenido.Trim();
        a.ProductoId = input.ProductoId;
        a.Categoria = input.Categoria;
        a.Etiquetas = Opciones.Limpio(input.Etiquetas);
        a.Publicable = input.Publicable;
        if (id is null) _db.Articulos.Add(a);
        await _db.SaveChangesAsync();
        this.ToastSuccess(id is null ? $"Artículo \"{a.Titulo}\" creado." : $"Artículo \"{a.Titulo}\" actualizado.");
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var a = await _db.Articulos.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        _db.Articulos.Remove(a);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Artículo \"{a.Titulo}\" eliminado.");
        return RedirectToPage("Index");
    }

    private async Task<ArticuloModalVm> ModalAsync(ArticuloConocimiento? a, ArticuloInput input, string? folio) => new()
    {
        Id = a?.Id,
        Input = input,
        Usos = a?.Usos ?? 0,
        DesdeTicket = folio,
        Productos = await _db.Productos.AsNoTracking().OrderBy(p => p.Nombre).Select(p => new Opcion(p.Id, p.Nombre, null)).ToListAsync(),
        PuedeGestionar = PuedeGestionar,
        CreadoUtc = a?.CreadoUtc,
        CreadoPor = a?.CreadoPor,
        ModificadoUtc = a?.ModificadoUtc,
        ModificadoPor = a?.ModificadoPor
    };

    private async Task CargarAsync()
    {
        Productos = await _db.Productos.AsNoTracking().OrderBy(p => p.Nombre).Select(p => new Opcion(p.Id, p.Nombre, null)).ToListAsync();
        var q = _db.Articulos.AsNoTracking().AsQueryable();
        if (Producto is not null) q = q.Where(a => a.ProductoId == Producto || a.ProductoId == null);
        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var b = Buscar.Trim();
            q = q.Where(a => a.Titulo.Contains(b) || a.Contenido.Contains(b) || (a.Etiquetas != null && a.Etiquetas.Contains(b)));
        }
        var todos = await q.Select(a => new { A = a, Producto = a.Producto == null ? null : a.Producto.Nombre }).ToListAsync();
        Total = todos.Count;
        Publicables = todos.Count(x => x.A.Publicable);
        PorCategoria = todos.GroupBy(x => x.A.Categoria).ToDictionary(g => g.Key, g => g.Count());
        if (Enum.TryParse<CategoriaTicket>(Categoria, out var cat)) todos = todos.Where(x => x.A.Categoria == cat).ToList();
        Articulos = todos.OrderByDescending(x => x.A.Usos).ThenBy(x => x.A.Titulo).Select(x => (x.A, x.Producto)).ToList();
    }
}
