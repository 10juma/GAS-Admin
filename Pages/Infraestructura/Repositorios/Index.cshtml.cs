using System.ComponentModel.DataAnnotations;
using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Proyectos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura.Repositorios;

public class RepositorioInput
{
    [Required(ErrorMessage = "Ingresa el nombre del repositorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede exceder 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;
    [StringLength(500, ErrorMessage = "La URL no puede exceder 500 caracteres.")]
    public string? Url { get; set; }
    public int? ProductoId { get; set; }
    public int? ProveedorId { get; set; }
    [Required(ErrorMessage = "Ingresa la rama principal.")]
    [StringLength(100, ErrorMessage = "La rama no puede exceder 100 caracteres.")]
    public string RamaPrincipal { get; set; } = "main";
    public VisibilidadRepositorio Visibilidad { get; set; } = VisibilidadRepositorio.Privado;
    [StringLength(200, ErrorMessage = "La tecnología no puede exceder 200 caracteres.")]
    public string? Tecnologia { get; set; }
    public string? ResponsableId { get; set; }
    public bool Archivado { get; set; }
    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }
}

public class RepositorioModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public RepositorioInput Input { get; init; } = new();
    public List<string> Ambientes { get; init; } = [];
    public List<Opcion> Productos { get; init; } = [];
    public List<Opcion> Servicios { get; init; } = [];
    public List<OpcionUsuario> Responsables { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
}

[Authorize(Policy = Permisos.Infraestructura.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;

    public IndexModel(GasDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public bool Archivados { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Infraestructura.Gestionar);

    public sealed record Fila(Repositorio Repo, string? Producto, string? Proveedor, string? Responsable, int Ambientes, RepoGitHub? GitHub = null);

    public List<Fila> Repositorios { get; private set; } = [];
    public int Activos { get; private set; }
    public int TotalArchivados { get; private set; }
    public RepositorioModalVm? Modal { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnGetNuevoAsync()
    {
        if (!PuedeGestionar) return Forbid();
        var github = await _db.ServiciosExternos.Where(s => s.Nombre == "GitHub").Select(s => (int?)s.Id).FirstOrDefaultAsync();
        return Partial("_RepositorioModal", await ModalAsync(null, new RepositorioInput { ProveedorId = github, ResponsableId = User.UsuarioId() }));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        var r = await _db.Repositorios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound();
        return Partial("_RepositorioModal", await ModalAsync(r, new RepositorioInput
        {
            Nombre = r.Nombre, Url = r.Url, ProductoId = r.ProductoId, ProveedorId = r.ProveedorId, RamaPrincipal = r.RamaPrincipal,
            Visibilidad = r.Visibilidad, Tecnologia = r.Tecnologia, ResponsableId = r.ResponsableId, Archivado = r.Archivado, Notas = r.Notas
        }));
    }

    public async Task<IActionResult> OnPostGuardarAsync(int? id, [Bind(Prefix = "Input")] RepositorioInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var r = id is null ? new Repositorio() : await _db.Repositorios.FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound();
        var nombre = input.Nombre?.Trim() ?? string.Empty;
        if (await _db.Repositorios.AnyAsync(x => x.Id != id && x.Nombre == nombre)) ModelState.AddModelError(string.Empty, $"Ya existe el repositorio {nombre}.");
        if (!string.IsNullOrWhiteSpace(input.Url) && Listas.Url(input.Url) is null) ModelState.AddModelError(string.Empty, "La URL no es válida.");
        if (input.ProductoId is not null && !await _db.Productos.AnyAsync(p => p.Id == input.ProductoId)) input.ProductoId = null;
        if (input.ProveedorId is not null && !await _db.ServiciosExternos.AnyAsync(s => s.Id == input.ProveedorId)) input.ProveedorId = null;
        if (input.ResponsableId is not null && !await _db.Users.AnyAsync(u => u.Id == input.ResponsableId)) input.ResponsableId = null;
        if (!ModelState.IsValid)
        {
            await CargarAsync();
            Modal = await ModalAsync(id is null ? null : r, input);
            return Page();
        }
        r.Nombre = nombre;
        r.Url = Listas.Url(input.Url);
        r.ProductoId = input.ProductoId;
        r.ProveedorId = input.ProveedorId;
        r.RamaPrincipal = input.RamaPrincipal.Trim();
        r.Visibilidad = input.Visibilidad;
        r.Tecnologia = Opciones.Limpio(input.Tecnologia);
        r.ResponsableId = input.ResponsableId;
        r.Archivado = input.Archivado;
        r.Notas = Opciones.Limpio(input.Notas);
        if (id is null) _db.Repositorios.Add(r);
        await _db.SaveChangesAsync();
        this.ToastSuccess(id is null ? $"Repositorio {r.Nombre} registrado." : $"Repositorio {r.Nombre} actualizado.");
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var r = await _db.Repositorios.FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound();
        if (await _db.Ambientes.AnyAsync(a => a.RepositorioId == id))
        {
            this.ToastWarning($"{r.Nombre} está ligado a ambientes. Archívalo en lugar de eliminarlo.");
            return RedirectToPage("Index");
        }
        _db.Repositorios.Remove(r);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Repositorio {r.Nombre} eliminado.");
        return RedirectToPage("Index");
    }

    private async Task<RepositorioModalVm> ModalAsync(Repositorio? r, RepositorioInput input) => new()
    {
        Id = r?.Id,
        Input = input,
        Ambientes = r is null ? [] : await _db.Ambientes.Where(a => a.RepositorioId == r.Id)
            .Select(a => a.Producto.Nombre + " · " + a.Nombre).ToListAsync(),
        Productos = await Listas.ProductosAsync(_db),
        Servicios = await Listas.ServiciosAsync(_db),
        Responsables = await Listas.ResponsablesAsync(_db, input.ResponsableId),
        PuedeGestionar = PuedeGestionar,
        CreadoUtc = r?.CreadoUtc,
        CreadoPor = r?.CreadoPor,
        ModificadoUtc = r?.ModificadoUtc,
        ModificadoPor = r?.ModificadoPor
    };

    private async Task CargarAsync()
    {
        var todos = await _db.Repositorios.AsNoTracking()
            .Select(r => new Fila(r,
                r.Producto == null ? null : r.Producto.Nombre,
                r.Proveedor == null ? null : r.Proveedor.Nombre,
                r.Responsable == null ? null : (r.Responsable.NombreCompleto == "" ? r.Responsable.Email : r.Responsable.NombreCompleto),
                _db.Ambientes.Count(a => a.RepositorioId == r.Id),
                _db.ReposGitHub.FirstOrDefault(g => g.RepositorioId == r.Id)))
            .ToListAsync();
        Activos = todos.Count(f => !f.Repo.Archivado);
        TotalArchivados = todos.Count - Activos;
        Repositorios = todos.Where(f => f.Repo.Archivado == Archivados).OrderBy(f => f.Producto ?? "~").ThenBy(f => f.Repo.Nombre).ToList();
    }
}
