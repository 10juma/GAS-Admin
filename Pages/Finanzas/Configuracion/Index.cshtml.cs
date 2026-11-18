using System.ComponentModel.DataAnnotations;
using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Finanzas;
using GAS.Admin.Services.Stripe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Finanzas.Configuracion;

public class CuentaInput
{
    [Required(ErrorMessage = "Ingresa el alias de la cuenta.")]
    [StringLength(100, ErrorMessage = "El alias no puede exceder 100 caracteres.")]
    public string Alias { get; set; } = string.Empty;
    public TipoCuenta Tipo { get; set; } = TipoCuenta.Banco;
    [StringLength(100, ErrorMessage = "La institución no puede exceder 100 caracteres.")]
    public string? Institucion { get; set; }
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Captura solo los últimos 4 dígitos, nunca el número completo.")]
    public string? UltimosDigitos { get; set; }
    public string Moneda { get; set; } = "MXN";
    public bool Activa { get; set; } = true;
    [StringLength(1000, ErrorMessage = "Las notas no pueden exceder 1000 caracteres.")]
    public string? Notas { get; set; }
}

public class CategoriaInput
{
    [Required(ErrorMessage = "Ingresa el nombre de la categoría.")]
    [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;
    public TipoMovimiento Tipo { get; set; }
    public bool Activa { get; set; } = true;
}

public class TipoCambioInput
{
    public DateOnly Fecha { get; set; }
    public string Moneda { get; set; } = "USD";
    [Range(0.0001, 9999, ErrorMessage = "El tipo de cambio debe ser mayor a cero.")]
    public decimal Valor { get; set; }
    [StringLength(100, ErrorMessage = "La fuente no puede exceder 100 caracteres.")]
    public string? Fuente { get; set; }
}

public class CuentaStripeInput
{
    public int? ProductoId { get; set; }
    [Required(ErrorMessage = "Ingresa la clave de configuración.")]
    [RegularExpression(@"^[a-z0-9-]{2,50}$", ErrorMessage = "La clave solo admite minúsculas, números y guiones (ej. revendo).")]
    public string Clave { get; set; } = string.Empty;
    public bool PreciosIncluyenIva { get; set; } = true;
    public bool Activa { get; set; } = true;
}

/// <summary>Datos de un modal de configuración: qué editar y con qué valores.</summary>
public class ConfigModalVm
{
    public string Tipo { get; init; } = "cuenta";
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public CuentaInput Cuenta { get; init; } = new();
    public CategoriaInput Categoria { get; init; } = new();
    public TipoCambioInput TipoCambio { get; init; } = new();
    public CuentaStripeInput Stripe { get; init; } = new();
    public List<(int Id, string Nombre)> Productos { get; init; } = [];
    public (bool Configurada, bool Restringida, bool Prueba) EstadoClave { get; init; }
    public int Usos { get; init; }
    public bool PuedeGestionar { get; init; }
}

[Authorize(Policy = Permisos.Finanzas.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioStripe _stripe;
    private readonly GAS.Admin.Services.Configuracion.ServicioLlaves _llaves;
    private readonly ServicioFinanzas _finanzas;

    public IndexModel(GasDbContext db, ServicioStripe stripe, ServicioFinanzas finanzas, GAS.Admin.Services.Configuracion.ServicioLlaves llaves)
    {
        _db = db;
        _stripe = stripe;
        _finanzas = finanzas;
        _llaves = llaves;
    }

    /// <summary>cuentas (por defecto), categorias, tipos o stripe.</summary>
    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Finanzas.Gestionar);

    public List<(CuentaFinanciera Cuenta, int Usos)> Cuentas { get; private set; } = [];
    public List<(CategoriaFinanciera Categoria, int Usos)> Categorias { get; private set; } = [];
    public List<TipoCambio> Tipos { get; private set; } = [];
    public int SinTipoCambio { get; private set; }
    public List<(CuentaStripe Cuenta, (bool Configurada, bool Restringida, bool Prueba) Clave)> CuentasStripe { get; private set; } = [];
    public ConfigModalVm? Modal { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    // ---------- Cuentas ----------

    public IActionResult OnGetNuevaCuenta() =>
        PuedeGestionar ? Partial("_ConfigModal", new ConfigModalVm { Tipo = "cuenta", PuedeGestionar = true }) : Forbid();

    public async Task<IActionResult> OnGetEditarCuentaAsync(int id)
    {
        var c = await _db.Cuentas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        return Partial("_ConfigModal", new ConfigModalVm
        {
            Tipo = "cuenta", Id = id, PuedeGestionar = PuedeGestionar, Usos = await _db.Movimientos.CountAsync(m => m.CuentaId == id),
            Cuenta = new() { Alias = c.Alias, Tipo = c.Tipo, Institucion = c.Institucion, UltimosDigitos = c.UltimosDigitos, Moneda = c.Moneda, Activa = c.Activa, Notas = c.Notas }
        });
    }

    public async Task<IActionResult> OnPostGuardarCuentaAsync(int? id, [Bind(Prefix = "Cuenta")] CuentaInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var c = id is null ? new CuentaFinanciera() : await _db.Cuentas.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (!ServicioFinanzas.Monedas.Contains(input.Moneda)) input.Moneda = "MXN";
        input.UltimosDigitos = Opciones.Limpio(input.UltimosDigitos);
        if (!ModelState.IsValid)
        {
            await CargarAsync();
            Vista = "cuentas";
            Modal = new ConfigModalVm { Tipo = "cuenta", Id = id, Cuenta = input, PuedeGestionar = true };
            return Page();
        }
        c.Alias = input.Alias.Trim();
        c.Tipo = input.Tipo;
        c.Institucion = Opciones.Limpio(input.Institucion);
        c.UltimosDigitos = input.UltimosDigitos;
        c.Moneda = input.Moneda;
        c.Activa = input.Activa;
        c.Notas = Opciones.Limpio(input.Notas);
        if (id is null) _db.Cuentas.Add(c);
        await _db.SaveChangesAsync();
        this.ToastSuccess(id is null ? $"Cuenta {c.Nombre} creada." : $"Cuenta {c.Nombre} actualizada.");
        return RedirectToPage("Index", new { vista = "cuentas" });
    }

    public async Task<IActionResult> OnPostEliminarCuentaAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await _db.Cuentas.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (await _db.Movimientos.AnyAsync(m => m.CuentaId == id))
        {
            this.ToastWarning($"{c.Nombre} tiene movimientos. Desactívala en lugar de eliminarla.");
            return RedirectToPage("Index", new { vista = "cuentas" });
        }
        _db.Cuentas.Remove(c);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Cuenta {c.Nombre} eliminada.");
        return RedirectToPage("Index", new { vista = "cuentas" });
    }

    // ---------- Categorías ----------

    public IActionResult OnGetNuevaCategoria(TipoMovimiento tipo) =>
        PuedeGestionar ? Partial("_ConfigModal", new ConfigModalVm { Tipo = "categoria", Categoria = new() { Tipo = tipo }, PuedeGestionar = true }) : Forbid();

    public async Task<IActionResult> OnGetEditarCategoriaAsync(int id)
    {
        var c = await _db.CategoriasFinancieras.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        return Partial("_ConfigModal", new ConfigModalVm
        {
            Tipo = "categoria", Id = id, PuedeGestionar = PuedeGestionar, Usos = await _db.Movimientos.CountAsync(m => m.CategoriaId == id),
            Categoria = new() { Nombre = c.Nombre, Tipo = c.Tipo, Activa = c.Activa }
        });
    }

    public async Task<IActionResult> OnPostGuardarCategoriaAsync(int? id, [Bind(Prefix = "Categoria")] CategoriaInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var c = id is null ? new CategoriaFinanciera() : await _db.CategoriasFinancieras.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (id is not null) input.Tipo = c.Tipo;
        var nombre = input.Nombre?.Trim() ?? string.Empty;
        if (await _db.CategoriasFinancieras.AnyAsync(x => x.Id != id && x.Tipo == input.Tipo && x.Nombre == nombre))
            ModelState.AddModelError(string.Empty, $"Ya existe la categoría \"{nombre}\".");
        if (!ModelState.IsValid)
        {
            await CargarAsync();
            Vista = "categorias";
            Modal = new ConfigModalVm { Tipo = "categoria", Id = id, Categoria = input, PuedeGestionar = true };
            return Page();
        }
        c.Nombre = nombre;
        c.Tipo = input.Tipo;
        c.Activa = input.Activa;
        if (id is null)
        {
            c.Orden = (await _db.CategoriasFinancieras.MaxAsync(x => (int?)x.Orden) ?? 0) + 1;
            _db.CategoriasFinancieras.Add(c);
        }
        await _db.SaveChangesAsync();
        this.ToastSuccess(id is null ? $"Categoría {c.Nombre} creada." : $"Categoría {c.Nombre} actualizada.");
        return RedirectToPage("Index", new { vista = "categorias" });
    }

    public async Task<IActionResult> OnPostEliminarCategoriaAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await _db.CategoriasFinancieras.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (await _db.Movimientos.AnyAsync(m => m.CategoriaId == id))
        {
            this.ToastWarning($"{c.Nombre} tiene movimientos. Desactívala en lugar de eliminarla.");
            return RedirectToPage("Index", new { vista = "categorias" });
        }
        _db.CategoriasFinancieras.Remove(c);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Categoría {c.Nombre} eliminada.");
        return RedirectToPage("Index", new { vista = "categorias" });
    }

    // ---------- Tipos de cambio ----------

    public async Task<IActionResult> OnGetNuevoTipoAsync()
    {
        if (!PuedeGestionar) return Forbid();
        var ultimo = await _db.TiposCambio.AsNoTracking().OrderByDescending(t => t.Fecha).FirstOrDefaultAsync();
        return Partial("_ConfigModal", new ConfigModalVm
        {
            Tipo = "tipo", PuedeGestionar = true,
            TipoCambio = new() { Fecha = ServicioFinanzas.Hoy, Valor = ultimo?.Valor ?? 0, Fuente = ultimo?.Fuente ?? "DOF / Banxico" }
        });
    }

    public async Task<IActionResult> OnGetEditarTipoAsync(int id)
    {
        var t = await _db.TiposCambio.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        return Partial("_ConfigModal", new ConfigModalVm
        {
            Tipo = "tipo", Id = id, PuedeGestionar = PuedeGestionar,
            TipoCambio = new() { Fecha = t.Fecha, Moneda = t.Moneda, Valor = t.Valor, Fuente = t.Fuente }
        });
    }

    public async Task<IActionResult> OnPostGuardarTipoAsync(int? id, [Bind(Prefix = "TipoCambio")] TipoCambioInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var t = id is null ? new TipoCambio() : await _db.TiposCambio.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        input.Moneda = "USD";
        if (input.Fecha == default) input.Fecha = ServicioFinanzas.Hoy;
        if (await _db.TiposCambio.AnyAsync(x => x.Id != id && x.Moneda == input.Moneda && x.Fecha == input.Fecha))
            ModelState.AddModelError(string.Empty, $"Ya hay un tipo de cambio para el {input.Fecha:dd/MM/yyyy}. Edítalo.");
        if (!ModelState.IsValid)
        {
            await CargarAsync();
            Vista = "tipos";
            Modal = new ConfigModalVm { Tipo = "tipo", Id = id, TipoCambio = input, PuedeGestionar = true };
            return Page();
        }
        t.Fecha = input.Fecha;
        t.Moneda = input.Moneda;
        t.Valor = Math.Round(input.Valor, 4);
        t.Fuente = Opciones.Limpio(input.Fuente);
        if (id is null) _db.TiposCambio.Add(t);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Tipo de cambio {t.Moneda} {t.Valor:0.0000} del {t.Fecha:dd/MM/yyyy} guardado.");
        return RedirectToPage("Index", new { vista = "tipos" });
    }

    public async Task<IActionResult> OnPostEliminarTipoAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var t = await _db.TiposCambio.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        _db.TiposCambio.Remove(t);
        await _db.SaveChangesAsync();
        this.ToastSuccess("Tipo de cambio eliminado. Los movimientos conservan el que tenían.");
        return RedirectToPage("Index", new { vista = "tipos" });
    }

    /// <summary>
    /// Aplica el tipo de cambio a los movimientos en dólares que no tienen uno: a los cobrados o pagados,
    /// el de su fecha de pago; a los pendientes, el más reciente.
    /// </summary>
    public async Task<IActionResult> OnPostAplicarTipoAsync()
    {
        if (!PuedeGestionar) return Forbid();
        if (!await _db.TiposCambio.AnyAsync())
        {
            this.ToastWarning("Primero registra un tipo de cambio.");
            return RedirectToPage("Index", new { vista = "tipos" });
        }
        var sinTipo = await _db.Movimientos.Where(m => m.Estado != EstadoMovimiento.Cancelado && m.Moneda != "MXN" && m.TipoCambio <= 0).ToListAsync();
        var aplicados = 0;
        foreach (var m in sinTipo)
        {
            var fecha = m.Estado == EstadoMovimiento.Confirmado ? m.FechaPago ?? m.FechaProgramada : (DateOnly?)null;
            var tc = await _finanzas.TipoCambioAsync(m.Moneda, fecha);
            if (tc is null or <= 0) continue;
            var total = m.Total;
            m.TipoCambio = tc.Value;
            // Se conserva el IVA y total originales (p. ej. los de Stripe); solo se recalculan los equivalentes.
            m.SubtotalMxn = Math.Round(m.Subtotal * m.TipoCambio, 2);
            m.TotalMxn = Math.Round(total * m.TipoCambio, 2);
            aplicados++;
        }
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Se aplicó el tipo de cambio a {aplicados} movimiento(s).");
        return RedirectToPage("Index", new { vista = "tipos" });
    }

    // ---------- Stripe ----------

    private async Task<List<(int Id, string Nombre)>> ProductosSinStripeAsync(int? actual) =>
        (await _db.Productos.AsNoTracking().Where(p => p.Id == actual || !_db.CuentasStripe.Any(c => c.ProductoId == p.Id))
            .OrderBy(p => p.Nombre).Select(p => new { p.Id, p.Nombre }).ToListAsync()).Select(p => (p.Id, p.Nombre)).ToList();

    public async Task<IActionResult> OnGetNuevaStripeAsync() =>
        PuedeGestionar ? Partial("_ConfigModal", new ConfigModalVm { Tipo = "stripe", PuedeGestionar = true, Productos = await ProductosSinStripeAsync(null) }) : Forbid();

    public async Task<IActionResult> OnGetEditarStripeAsync(int id)
    {
        var c = await _db.CuentasStripe.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        return Partial("_ConfigModal", new ConfigModalVm
        {
            Tipo = "stripe", Id = id, PuedeGestionar = PuedeGestionar, Productos = await ProductosSinStripeAsync(c.ProductoId),
            EstadoClave = _stripe.EstadoClave(c.Clave),
            Stripe = new() { ProductoId = c.ProductoId, Clave = c.Clave, PreciosIncluyenIva = c.PreciosIncluyenIva, Activa = c.Activa }
        });
    }

    public async Task<IActionResult> OnPostGuardarStripeAsync(int? id, [Bind(Prefix = "Stripe")] CuentaStripeInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var c = id is null ? new CuentaStripe() : await _db.CuentasStripe.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        input.Clave = (input.Clave ?? string.Empty).Trim().ToLowerInvariant();
        if (input.ProductoId is null || !await _db.Productos.AnyAsync(p => p.Id == input.ProductoId))
            ModelState.AddModelError(string.Empty, "Elige el producto.");
        else if (await _db.CuentasStripe.AnyAsync(x => x.Id != id && x.ProductoId == input.ProductoId))
            ModelState.AddModelError(string.Empty, "Ese producto ya tiene su cuenta de Stripe.");
        if (await _db.CuentasStripe.AnyAsync(x => x.Id != id && x.Clave == input.Clave))
            ModelState.AddModelError(string.Empty, $"La clave {input.Clave} ya está en uso.");
        if (DetectorSecretos.PareceSecreto(input.Clave))
        {
            ModelState.AddModelError(string.Empty, "Aquí va solo el nombre de la clave (ej. revendo), no la API key.");
            input.Clave = string.Empty;
        }
        if (!ModelState.IsValid)
        {
            await CargarAsync();
            Vista = "stripe";
            Modal = new ConfigModalVm { Tipo = "stripe", Id = id, Stripe = input, PuedeGestionar = true, Productos = await ProductosSinStripeAsync(input.ProductoId) };
            return Page();
        }
        var anterior = id is null ? null : c.Clave;
        c.ProductoId = input.ProductoId!.Value;
        c.Clave = input.Clave;
        c.PreciosIncluyenIva = input.PreciosIncluyenIva;
        c.Activa = input.Activa;
        if (id is null) _db.CuentasStripe.Add(c);
        await _db.SaveChangesAsync();
        this.ToastSuccess(id is null ? "Cuenta de Stripe conectada." : "Cuenta de Stripe actualizada.");
        if (anterior is not null && anterior != c.Clave)
            await _llaves.RenombrarAsync($"Stripe:Claves:{anterior}", $"Stripe:Claves:{c.Clave}", $"Stripe · {c.Clave}");
        if (!_stripe.EstadoClave(c.Clave).Configurada)
            this.ToastWarning(User.IsInRole(RolesBase.Admin)
                ? "Falta la API key: captúrala con el botón «Capturar API key» de la cuenta."
                : "Falta la API key: pide a un Admin que la capture.");
        return RedirectToPage("Index", new { vista = "stripe" });
    }

    public async Task<IActionResult> OnPostSincronizarStripeAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var r = await _stripe.SincronizarAsync(id);
        if (r.Exito) this.ToastSuccess($"{r.Cuenta}: {r.Mensaje}");
        else this.ToastError($"{r.Cuenta}: {r.Mensaje}");
        return RedirectToPage("Index", new { vista = "stripe" });
    }

    public async Task<IActionResult> OnPostEliminarStripeAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await _db.CuentasStripe.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        // Se borra la copia local; los movimientos ya registrados en Finanzas se conservan.
        _db.CuentasStripe.Remove(c);
        await _db.SaveChangesAsync();
        await _llaves.QuitarAsync($"Stripe:Claves:{c.Clave}", $"Stripe · {c.Clave}");
        this.ToastSuccess("Cuenta de Stripe desconectada. Los movimientos ya registrados en Finanzas se conservan.");
        return RedirectToPage("Index", new { vista = "stripe" });
    }

    private async Task CargarAsync()
    {
        CuentasStripe = (await _db.CuentasStripe.AsNoTracking().Include(c => c.Producto).OrderBy(c => c.Producto.Nombre).ToListAsync())
            .Select(c => (c, _stripe.EstadoClave(c.Clave))).ToList();

        var usosCuenta = await _db.Movimientos.Where(m => m.CuentaId != null).GroupBy(m => m.CuentaId!.Value)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        Cuentas = (await _db.Cuentas.AsNoTracking().OrderByDescending(c => c.Activa).ThenBy(c => c.Alias).ToListAsync())
            .Select(c => (c, usosCuenta.GetValueOrDefault(c.Id))).ToList();

        var usosCat = await _db.Movimientos.Where(m => m.CategoriaId != null).GroupBy(m => m.CategoriaId!.Value)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        Categorias = (await _db.CategoriasFinancieras.AsNoTracking().OrderBy(c => c.Orden).ThenBy(c => c.Nombre).ToListAsync())
            .Select(c => (c, usosCat.GetValueOrDefault(c.Id))).ToList();

        Tipos = await _db.TiposCambio.AsNoTracking().OrderByDescending(t => t.Fecha).Take(60).ToListAsync();
        SinTipoCambio = await _db.Movimientos.CountAsync(m => m.Estado != EstadoMovimiento.Cancelado && m.Moneda != "MXN" && m.TipoCambio <= 0);
    }
}
