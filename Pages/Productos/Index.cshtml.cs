using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Navegacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Productos;

[Authorize(Policy = Permisos.Productos.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, ContadoresMenu contadores)
    {
        _db = db;
        _contadores = contadores;
    }

    [BindProperty(SupportsGet = true)]
    public string? Estado { get; set; }

    [BindProperty]
    public ProductoInput Input { get; set; } = new();

    public sealed record Tarjeta(Producto Producto, int ClientesActivos, int ClientesTotal, int Servicios, string? Responsable);

    public List<Tarjeta> Productos { get; private set; } = [];
    /// <summary>Conexión con el super-admin (API GAS) y cuentas vigentes, por producto.</summary>
    public Dictionary<int, (ConexionProducto Conexion, int Cuentas)> Conexiones { get; private set; } = [];
    public Dictionary<EstadoProducto, int> PorEstado { get; private set; } = [];
    public int Total { get; private set; }
    public bool PuedeGestionar => User.TienePermiso(Permisos.Productos.Gestionar);
    public ProductoModalVm? Modal { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnGetNuevoAsync()
    {
        if (!PuedeGestionar) return Forbid();
        return Partial("_ProductoModal", await ModalAsync(null, new ProductoInput()));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        var producto = await _db.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (producto is null) return NotFound();
        return Partial("_ProductoModal", await ModalAsync(producto, null));
    }

    public async Task<IActionResult> OnPostCrearAsync()
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarNombreAsync(null);

        if (ModelState.IsValid)
        {
            var producto = new Producto();
            Input.AplicarA(producto);
            _db.Productos.Add(producto);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"Producto {producto.Nombre} creado.");
            return Volver();
        }

        await CargarAsync();
        Modal = await ModalAsync(null, Input);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == id);
        if (producto is null) return NotFound();
        await ValidarNombreAsync(id);

        if (ModelState.IsValid)
        {
            Input.AplicarA(producto);
            var cambios = _db.ChangeTracker.HasChanges();
            await _db.SaveChangesAsync();
            if (cambios) this.ToastSuccess($"Cambios de {producto.Nombre} guardados.");
            else this.ToastInfo("No hubo cambios que guardar.");
            return Volver();
        }

        await CargarAsync();
        Modal = await ModalAsync(producto, Input);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == id);
        if (producto is null) return NotFound();

        var enUso = await _db.ClientesProductos.AnyAsync(c => c.ProductoId == id)
                    || await _db.ServiciosProductos.AnyAsync(s => s.ProductoId == id);
        if (enUso)
        {
            this.ToastWarning($"{producto.Nombre} tiene clientes o servicios ligados. Márcalo como \"Retirado\" en lugar de eliminarlo.");
            return Volver();
        }

        _db.Productos.Remove(producto);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Producto {producto.Nombre} eliminado.");
        return Volver();
    }

    // ---------- Apoyo ----------

    private IActionResult Volver()
    {
        _contadores.Invalidar();
        return RedirectToPage("Index", new { estado = Opciones.Limpio(Estado) });
    }

    private async Task ValidarNombreAsync(int? id)
    {
        var nombre = Input.Nombre?.Trim() ?? "";
        if (nombre.Length > 0 && await _db.Productos.AnyAsync(p => p.Nombre == nombre && p.Id != id))
        {
            ModelState.AddModelError(string.Empty, $"Ya existe un producto llamado {nombre}.");
        }
    }

    private async Task<ProductoModalVm> ModalAsync(Producto? producto, ProductoInput? input) => new()
    {
        Id = producto?.Id,
        Input = input ?? ProductoInput.Desde(producto!),
        Responsables = await Opciones.ResponsablesAsync(_db, producto?.ResponsableId),
        PuedeGestionar = PuedeGestionar,
        Clientes = producto is null ? 0 : await _db.ClientesProductos.CountAsync(c => c.ProductoId == producto.Id),
        Servicios = producto is null ? 0 : await _db.ServiciosProductos.CountAsync(s => s.ProductoId == producto.Id),
        CreadoUtc = producto?.CreadoUtc,
        CreadoPor = producto?.CreadoPor,
        ModificadoUtc = producto?.ModificadoUtc,
        ModificadoPor = producto?.ModificadoPor,
        Filtro = Estado
    };

    private async Task CargarAsync()
    {
        var conexiones = await _db.ConexionesProducto.AsNoTracking().ToListAsync();
        var vigentes = await _db.CuentasProducto.Where(c => c.Estado == "activa" || c.Estado == "prueba")
            .GroupBy(c => c.ConexionProductoId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        Conexiones = conexiones.ToDictionary(c => c.ProductoId, c => (c, vigentes.GetValueOrDefault(c.Id)));
        var todos = await _db.Productos.AsNoTracking()
            .Select(p => new
            {
                Producto = p,
                ClientesActivos = p.Clientes.Count(c => c.Estado == EstadoClienteProducto.Activo),
                ClientesTotal = p.Clientes.Count,
                Servicios = p.Servicios.Count,
                Responsable = p.Responsable == null ? null : (p.Responsable.NombreCompleto == "" ? p.Responsable.Email : p.Responsable.NombreCompleto)
            })
            .ToListAsync();

        Total = todos.Count;
        PorEstado = Enum.GetValues<EstadoProducto>().ToDictionary(e => e, e => todos.Count(t => t.Producto.Estado == e));

        var filtrados = Enum.TryParse<EstadoProducto>(Estado, out var estado)
            ? todos.Where(t => t.Producto.Estado == estado)
            : todos;

        Productos = filtrados
            .OrderBy(t => t.Producto.Estado).ThenBy(t => t.Producto.Nombre)
            .Select(t => new Tarjeta(t.Producto, t.ClientesActivos, t.ClientesTotal, t.Servicios, t.Responsable))
            .ToList();
    }
}
