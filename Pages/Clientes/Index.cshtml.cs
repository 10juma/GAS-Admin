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

namespace GAS.Admin.Pages.Clientes;

[Authorize(Policy = Permisos.Comercial.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, ContadoresMenu contadores)
    {
        _db = db;
        _contadores = contadores;
    }

    [BindProperty(SupportsGet = true)] public string? Estado { get; set; }
    [BindProperty(SupportsGet = true)] public int? Producto { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    [BindProperty]
    public ClienteInput Input { get; set; } = new();

    public sealed record ProductoFila(string Nombre, EstadoClienteProducto Estado);
    public sealed record Fila(int Id, string Nombre, string? RazonSocial, string? Rfc, string? Ciudad, EstadoCliente Estado,
        string? Contacto, string? ContactoEmail, string? Responsable, List<ProductoFila> Productos);

    public List<Fila> Clientes { get; private set; } = [];
    public Dictionary<EstadoCliente, int> PorEstado { get; private set; } = [];
    public int Total { get; private set; }
    public List<(Producto Producto, int Clientes)> PorProducto { get; private set; } = [];
    public bool PuedeGestionar => User.TienePermiso(Permisos.Comercial.Gestionar);
    public ClienteModalVm? Modal { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnGetNuevoAsync()
    {
        if (!PuedeGestionar) return Forbid();
        var productos = await ProductosAsync();
        var input = new ClienteInput
        {
            Productos = productos.Select(p => new ProductoDelCliente { ProductoId = p.Id }).ToList()
        };
        return Partial("_ClienteModal", await ModalAsync(null, input, productos));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        var cliente = await _db.Clientes.AsNoTracking().Include(c => c.Productos).FirstOrDefaultAsync(c => c.Id == id);
        if (cliente is null) return NotFound();
        var productos = await ProductosAsync();
        return Partial("_ClienteModal", await ModalAsync(cliente, ClienteInput.Desde(cliente, productos), productos));
    }

    public async Task<IActionResult> OnPostCrearAsync()
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarAsync(null);

        if (ModelState.IsValid)
        {
            var cliente = new Cliente();
            Input.AplicarA(cliente);
            await _db.Productos.LoadAsync(); // nombres para la auditoría
            SincronizarProductos(cliente);
            _db.Clientes.Add(cliente);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"Cliente {cliente.NombreVisible} creado.");
            return Volver();
        }

        await CargarAsync();
        Modal = await ModalAsync(null, Input, await ProductosAsync());
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var cliente = await _db.Clientes.Include(c => c.Productos).FirstOrDefaultAsync(c => c.Id == id);
        if (cliente is null) return NotFound();
        await ValidarAsync(id);

        if (ModelState.IsValid)
        {
            Input.AplicarA(cliente);
            await _db.Productos.LoadAsync();
            SincronizarProductos(cliente);
            var cambios = _db.ChangeTracker.HasChanges();
            await _db.SaveChangesAsync();
            if (cambios) this.ToastSuccess($"Cambios de {cliente.NombreVisible} guardados.");
            else this.ToastInfo("No hubo cambios que guardar.");
            return Volver();
        }

        await CargarAsync();
        Modal = await ModalAsync(cliente, Input, await ProductosAsync());
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var cliente = await _db.Clientes.Include(c => c.Productos).FirstOrDefaultAsync(c => c.Id == id);
        if (cliente is null) return NotFound();

        if (cliente.Productos.Any(p => p.Estado == EstadoClienteProducto.Activo))
        {
            this.ToastWarning($"{cliente.NombreVisible} tiene productos activos. Cámbialos a Cancelado o marca al cliente como Inactivo.");
            return Volver();
        }

        _db.Clientes.Remove(cliente);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Cliente {cliente.NombreVisible} eliminado.");
        return Volver();
    }

    // ---------- Apoyo ----------

    private IActionResult Volver()
    {
        _contadores.Invalidar();
        return RedirectToPage("Index", new { estado = Opciones.Limpio(Estado), producto = Producto, q = Opciones.Limpio(Q) });
    }

    private void SincronizarProductos(Cliente cliente)
    {
        foreach (var p in Input.Productos)
        {
            var actual = cliente.Productos.FirstOrDefault(x => x.ProductoId == p.ProductoId);
            if (p.Estado is null)
            {
                if (actual is not null) cliente.Productos.Remove(actual);
            }
            else if (actual is null)
            {
                cliente.Productos.Add(new ClienteProducto { ProductoId = p.ProductoId, Estado = p.Estado.Value, Desde = p.Desde });
            }
            else
            {
                actual.Estado = p.Estado.Value;
                actual.Desde = p.Desde;
            }
        }
    }

    private async Task ValidarAsync(int? id)
    {
        var rfc = Opciones.Limpio(Input.Rfc)?.ToUpperInvariant();
        if (rfc is not null && await _db.Clientes.AnyAsync(c => c.Rfc == rfc && c.Id != id))
        {
            ModelState.AddModelError(string.Empty, $"Ya existe un cliente con el RFC {rfc}.");
        }

        var validos = (await _db.Productos.Select(p => p.Id).ToListAsync()).ToHashSet();
        Input.Productos = Input.Productos.Where(p => validos.Contains(p.ProductoId)).ToList();
    }

    private Task<List<Producto>> ProductosAsync() =>
        _db.Productos.AsNoTracking().OrderBy(p => p.Estado).ThenBy(p => p.Nombre).ToListAsync();

    private async Task<ClienteModalVm> ModalAsync(Cliente? cliente, ClienteInput input, List<Producto> productos) => new()
    {
        Id = cliente?.Id,
        Input = input,
        ProductosDisponibles = productos,
        Responsables = await Opciones.ResponsablesAsync(_db, cliente?.ResponsableId),
        PuedeGestionar = PuedeGestionar,
        CreadoUtc = cliente?.CreadoUtc,
        CreadoPor = cliente?.CreadoPor,
        ModificadoUtc = cliente?.ModificadoUtc,
        ModificadoPor = cliente?.ModificadoPor,
        Filtro = Estado,
        FiltroProducto = Producto,
        Q = Q
    };

    private async Task CargarAsync()
    {
        Total = await _db.Clientes.CountAsync();
        var conteos = await _db.Clientes.GroupBy(c => c.Estado).Select(g => new { g.Key, Total = g.Count() }).ToListAsync();
        PorEstado = Enum.GetValues<EstadoCliente>().ToDictionary(e => e, e => conteos.FirstOrDefault(c => c.Key == e)?.Total ?? 0);

        var productos = await _db.Productos.AsNoTracking()
            .Select(p => new { Producto = p, Clientes = p.Clientes.Count(c => c.Estado != EstadoClienteProducto.Cancelado) })
            .ToListAsync();
        PorProducto = productos.OrderByDescending(p => p.Clientes).ThenBy(p => p.Producto.Nombre)
            .Select(p => (p.Producto, p.Clientes)).ToList();

        var query = _db.Clientes.AsNoTracking();
        if (Enum.TryParse<EstadoCliente>(Estado, out var estado)) query = query.Where(c => c.Estado == estado);
        if (Producto is not null) query = query.Where(c => c.Productos.Any(p => p.ProductoId == Producto && p.Estado != EstadoClienteProducto.Cancelado));
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            query = query.Where(c => c.Nombre.Contains(q) || (c.NombreComercial != null && c.NombreComercial.Contains(q))
                                     || (c.Rfc != null && c.Rfc.Contains(q)) || (c.ContactoNombre != null && c.ContactoNombre.Contains(q))
                                     || (c.ContactoEmail != null && c.ContactoEmail.Contains(q)));
        }

        var datos = await query
            .OrderBy(c => c.NombreComercial ?? c.Nombre)
            .Select(c => new
            {
                c.Id, c.Nombre, c.NombreComercial, c.Rfc, c.Ciudad, c.Estado, c.ContactoNombre, c.ContactoEmail,
                Responsable = c.Responsable == null ? null : (c.Responsable.NombreCompleto == "" ? c.Responsable.Email : c.Responsable.NombreCompleto),
                Productos = c.Productos.OrderBy(p => p.Producto.Nombre).Select(p => new { p.Producto.Nombre, p.Estado }).ToList()
            })
            .ToListAsync();

        Clientes = datos.Select(c => new Fila(
                c.Id,
                string.IsNullOrWhiteSpace(c.NombreComercial) ? c.Nombre : c.NombreComercial!,
                string.IsNullOrWhiteSpace(c.NombreComercial) ? null : c.Nombre,
                c.Rfc, c.Ciudad, c.Estado, c.ContactoNombre, c.ContactoEmail, c.Responsable,
                c.Productos.Select(p => new ProductoFila(p.Nombre, p.Estado)).ToList()))
            .ToList();
    }
}
