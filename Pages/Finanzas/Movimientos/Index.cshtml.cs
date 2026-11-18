using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Finanzas;
using GAS.Admin.Services.Navegacion;
using GAS.Admin.Services.Proyectos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Finanzas.Movimientos;

[Authorize(Policy = Permisos.Finanzas.Ver)]
public class IndexModel : PageModel
{
    private const int Limite = 300;

    private readonly GasDbContext _db;
    private readonly ServicioFinanzas _finanzas;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, ServicioFinanzas finanzas, ContadoresMenu contadores)
    {
        _db = db;
        _finanzas = finanzas;
        _contadores = contadores;
    }

    /// <summary>pendientes (por defecto), porcobrar, porpagar, vencidos, confirmados, cancelados o todos.</summary>
    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }
    /// <summary>Mes yyyy-MM (fecha de pago o, si no hay, la programada).</summary>
    [BindProperty(SupportsGet = true)] public string? Mes { get; set; }
    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }
    [BindProperty(SupportsGet = true)] public int? Trabajo { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Finanzas.Gestionar);
    public DateOnly Hoy { get; } = ServicioFinanzas.Hoy;

    public sealed record Fila(Movimiento M, string? Categoria, string? Relacion, string? TrabajoFolio, string? Cuenta);

    public List<Fila> Movimientos { get; private set; } = [];
    public bool HayMas { get; private set; }
    public string? TrabajoNombre { get; private set; }
    public Dictionary<string, int> Conteos { get; private set; } = [];
    public decimal PorCobrar { get; private set; }
    public decimal PorPagar { get; private set; }
    public decimal VencidoCobrar { get; private set; }
    public decimal VencidoPagar { get; private set; }
    public decimal CobradoMes { get; private set; }
    public decimal PagadoMes { get; private set; }
    public int SinTipoCambio { get; private set; }
    public List<RenovacionPendiente> Renovaciones { get; private set; } = [];
    public MovimientoModalVm? Modal { get; private set; }
    public ConfirmarModalVm? ModalConfirmar { get; private set; }

    public string? VolverActual => Request.Path + Request.QueryString;

    public async Task OnGetAsync() => await CargarAsync();

    // ---------- Crear y editar ----------

    public async Task<IActionResult> OnGetNuevoAsync(TipoMovimiento tipo, int? trabajoId, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var input = new MovimientoInput
        {
            Tipo = tipo,
            FechaProgramada = Hoy,
            TasaIva = tipo == TipoMovimiento.Ingreso ? 16 : 0,
            Linea = tipo == TipoMovimiento.Ingreso ? LineaNegocio.Servicios : LineaNegocio.Interno
        };
        if (trabajoId is not null && await _db.Trabajos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trabajoId) is { } t)
        {
            input.TrabajoId = t.Id;
            input.ClienteId = t.ClienteId;
            input.ProductoId = t.ProductoId;
            input.Moneda = t.Moneda;
            input.Linea = t.EsInterno ? LineaNegocio.Interno : LineaNegocio.Servicios;
        }
        return Partial("_MovimientoModal", await ModalAsync(null, input, volver));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id, string? volver)
    {
        var m = await _db.Movimientos.AsNoTracking().Include(x => x.Cotizacion).FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        return Partial("_MovimientoModal", await ModalAsync(m, MovimientoInput.Desde(m), volver));
    }

    public async Task<IActionResult> OnPostCrearAsync([Bind(Prefix = "Input")] MovimientoInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarAsync(input);
        if (ModelState.IsValid)
        {
            var m = new Movimiento();
            input.AplicarA(m);
            _db.Movimientos.Add(m);
            var aviso = m.Estado == EstadoMovimiento.Confirmado ? await _finanzas.AlConfirmarAsync(m) : null;
            await _db.SaveChangesAsync();
            _contadores.Invalidar();
            this.ToastSuccess($"{(m.EsIngreso ? "Ingreso" : "Egreso")} registrado.");
            if (aviso is not null) this.ToastInfo(aviso);
            return this.VolverA(volver, "Index");
        }
        await CargarAsync();
        Modal = await ModalAsync(null, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id, [Bind(Prefix = "Input")] MovimientoInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var m = await _db.Movimientos.Include(x => x.Cotizacion).FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        input.Tipo = m.Tipo;
        await ValidarAsync(input);
        if (ModelState.IsValid)
        {
            var confirmando = m.Estado != EstadoMovimiento.Confirmado && input.Estado == EstadoMovimiento.Confirmado;
            input.AplicarA(m);
            var aviso = confirmando ? await _finanzas.AlConfirmarAsync(m) : null;
            var cambios = _db.ChangeTracker.HasChanges();
            await _db.SaveChangesAsync();
            _contadores.Invalidar();
            if (cambios) this.ToastSuccess("Movimiento actualizado.");
            else this.ToastInfo("No hubo cambios que guardar.");
            if (aviso is not null) this.ToastInfo(aviso);
            return this.VolverA(volver, "Index");
        }
        await CargarAsync();
        Modal = await ModalAsync(m, input, volver);
        return Page();
    }

    // ---------- Registrar cobro o pago ----------

    public async Task<IActionResult> OnGetConfirmarAsync(int id, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var m = await _db.Movimientos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        var input = new ConfirmarInput
        {
            FechaPago = Hoy,
            CuentaId = m.CuentaId,
            Referencia = m.Referencia,
            TipoCambio = m.Moneda == "MXN" ? null : await _finanzas.TipoCambioAsync(m.Moneda, Hoy) ?? (m.TipoCambio > 0 ? m.TipoCambio : null)
        };
        return Partial("_ConfirmarModal", await ConfirmarVmAsync(m, input, volver));
    }

    public async Task<IActionResult> OnPostConfirmarAsync(int id, [Bind(Prefix = "Input")] ConfirmarInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var m = await _db.Movimientos.FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        if (m.Estado != EstadoMovimiento.Esperado)
        {
            this.ToastWarning("Este movimiento ya no está pendiente.");
            return this.VolverA(volver, "Index");
        }
        if (input.FechaPago > Hoy.AddDays(1)) ModelState.AddModelError(string.Empty, "La fecha de pago no puede ser futura.");
        if (m.Moneda != "MXN" && input.TipoCambio is null or <= 0) ModelState.AddModelError(string.Empty, $"Captura el tipo de cambio {m.Moneda} del día del pago.");
        if (input.CuentaId is not null && !await _db.Cuentas.AnyAsync(c => c.Id == input.CuentaId)) input.CuentaId = null;

        if (ModelState.IsValid)
        {
            m.Estado = EstadoMovimiento.Confirmado;
            m.FechaPago = input.FechaPago;
            m.CuentaId = input.CuentaId;
            m.Referencia = string.IsNullOrWhiteSpace(input.Referencia) ? m.Referencia : input.Referencia.Trim();
            if (m.Moneda != "MXN") m.TipoCambio = input.TipoCambio!.Value;
            m.Recalcular();
            var aviso = await _finanzas.AlConfirmarAsync(m);
            await _db.SaveChangesAsync();
            _contadores.Invalidar();
            this.ToastSuccess(m.EsIngreso ? $"Cobro registrado: {m.Concepto}." : $"Pago registrado: {m.Concepto}.");
            if (aviso is not null) this.ToastInfo(aviso);
            return this.VolverA(volver, "Index");
        }
        await CargarAsync();
        ModalConfirmar = await ConfirmarVmAsync(m, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostEstadoAsync(int id, EstadoMovimiento estado, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var m = await _db.Movimientos.FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        if (estado == EstadoMovimiento.Confirmado) return RedirectToPage("Index");
        var aviso = m.Estado == EstadoMovimiento.Confirmado ? await _finanzas.AlRevertirAsync(m) : null;
        m.Estado = estado;
        m.FechaPago = null;
        await _db.SaveChangesAsync();
        if (aviso is not null) this.ToastInfo(aviso);
        _contadores.Invalidar();
        this.ToastSuccess(estado == EstadoMovimiento.Cancelado ? "Movimiento cancelado." : "El movimiento volvió a pendiente.");
        return this.VolverA(volver, "Index");
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var m = await _db.Movimientos.FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        _db.Movimientos.Remove(m);
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        this.ToastSuccess("Movimiento eliminado.");
        return this.VolverA(volver, "Index");
    }

    public async Task<IActionResult> OnPostRenovacionesAsync(string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var n = await _finanzas.GenerarCargosRenovacionAsync();
        _contadores.Invalidar();
        if (n == 0) this.ToastInfo("No hay renovaciones pendientes de registrar.");
        else this.ToastSuccess(n == 1 ? "Se registró 1 cargo esperado de renovación." : $"Se registraron {n} cargos esperados de renovación.");
        return this.VolverA(volver, "Index");
    }

    // ---------- Apoyo ----------

    private async Task ValidarAsync(MovimientoInput input)
    {
        if (!ServicioFinanzas.Monedas.Contains(input.Moneda)) input.Moneda = "MXN";
        if (input.FechaProgramada == default) input.FechaProgramada = Hoy;
        if (input.Moneda != "MXN" && input.TipoCambio is null or <= 0)
            ModelState.AddModelError(string.Empty, $"Captura el tipo de cambio de {input.Moneda} a pesos.");
        if (input.Estado == EstadoMovimiento.Confirmado)
        {
            input.FechaPago ??= Hoy;
            if (input.FechaPago > Hoy.AddDays(1)) ModelState.AddModelError(string.Empty, "La fecha de pago no puede ser futura.");
        }
        if (input.CategoriaId is not null && !await _db.CategoriasFinancieras.AnyAsync(c => c.Id == input.CategoriaId && c.Tipo == input.Tipo))
            input.CategoriaId = null;
        if (input.ClienteId is not null && !await _db.Clientes.AnyAsync(c => c.Id == input.ClienteId)) input.ClienteId = null;
        if (input.ProductoId is not null && !await _db.Productos.AnyAsync(p => p.Id == input.ProductoId)) input.ProductoId = null;
        if (input.TrabajoId is not null && !await _db.Trabajos.AnyAsync(t => t.Id == input.TrabajoId)) input.TrabajoId = null;
        if (input.ServicioExternoId is not null && !await _db.ServiciosExternos.AnyAsync(s => s.Id == input.ServicioExternoId)) input.ServicioExternoId = null;
        if (input.CuentaId is not null && !await _db.Cuentas.AnyAsync(c => c.Id == input.CuentaId)) input.CuentaId = null;
    }

    private async Task<List<Opcion>> CuentasAsync(int? actual) =>
        (await _db.Cuentas.AsNoTracking().Where(c => c.Activa || c.Id == actual).OrderBy(c => c.Alias).ToListAsync())
        .Select(c => new Opcion(c.Id, c.Nombre, c.Moneda)).ToList();

    private async Task<ConfirmarModalVm> ConfirmarVmAsync(Movimiento m, ConfirmarInput input, string? volver) => new()
    {
        Movimiento = m,
        Input = input,
        Cuentas = await CuentasAsync(input.CuentaId),
        Volver = volver
    };

    private async Task<MovimientoModalVm> ModalAsync(Movimiento? m, MovimientoInput input, string? volver) => new()
    {
        Id = m?.Id,
        Input = input,
        Origen = m?.Origen ?? OrigenMovimiento.Manual,
        Cotizacion = m?.Cotizacion?.FolioCompleto,
        CotizacionId = m?.CotizacionId,
        Categorias = await _db.CategoriasFinancieras.AsNoTracking().Where(c => c.Activa || c.Id == input.CategoriaId)
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre).Select(c => new Opcion(c.Id, c.Nombre, c.Tipo.ToString())).ToListAsync(),
        Clientes = await _db.Clientes.AsNoTracking().OrderBy(c => c.NombreComercial ?? c.Nombre)
            .Select(c => new Opcion(c.Id, c.NombreComercial ?? c.Nombre, null)).ToListAsync(),
        Productos = await _db.Productos.AsNoTracking().OrderBy(p => p.Nombre).Select(p => new Opcion(p.Id, p.Nombre, null)).ToListAsync(),
        Trabajos = await _db.Trabajos.AsNoTracking()
            .Where(t => (t.Estado != EstadoTrabajo.Cerrado && t.Estado != EstadoTrabajo.Cancelado) || t.Id == input.TrabajoId)
            .OrderByDescending(t => t.Folio).Select(t => new Opcion(t.Id, t.Folio + " · " + t.Nombre, null)).ToListAsync(),
        Servicios = await _db.ServiciosExternos.AsNoTracking().OrderBy(s => s.Nombre).Select(s => new Opcion(s.Id, s.Nombre, null)).ToListAsync(),
        Cuentas = await CuentasAsync(input.CuentaId),
        TipoCambioSugerido = await _finanzas.TipoCambioAsync("USD", Hoy),
        PuedeGestionar = PuedeGestionar,
        CreadoUtc = m?.CreadoUtc,
        CreadoPor = m?.CreadoPor,
        ModificadoUtc = m?.ModificadoUtc,
        ModificadoPor = m?.ModificadoPor,
        Volver = volver
    };

    private async Task CargarAsync()
    {
        var hoy = Hoy;
        var inicioMes = ServicioTrabajos.InicioMes(hoy);
        var pendientes = await _db.Movimientos.AsNoTracking().Where(m => m.Estado == EstadoMovimiento.Esperado)
            .Select(m => new { m.Tipo, m.TotalMxn, m.FechaProgramada, m.TipoCambio }).ToListAsync();
        PorCobrar = pendientes.Where(m => m.Tipo == TipoMovimiento.Ingreso).Sum(m => m.TotalMxn);
        PorPagar = pendientes.Where(m => m.Tipo == TipoMovimiento.Egreso).Sum(m => m.TotalMxn);
        VencidoCobrar = pendientes.Where(m => m.Tipo == TipoMovimiento.Ingreso && m.FechaProgramada < hoy).Sum(m => m.TotalMxn);
        VencidoPagar = pendientes.Where(m => m.Tipo == TipoMovimiento.Egreso && m.FechaProgramada < hoy).Sum(m => m.TotalMxn);
        SinTipoCambio = await _db.Movimientos.CountAsync(m => m.Estado != EstadoMovimiento.Cancelado && m.Moneda != "MXN" && m.TipoCambio <= 0);

        var mes = await _db.Movimientos.AsNoTracking()
            .Where(m => m.Estado == EstadoMovimiento.Confirmado && m.FechaPago >= inicioMes && m.FechaPago <= hoy)
            .Select(m => new { m.Tipo, m.SubtotalMxn }).ToListAsync();
        CobradoMes = mes.Where(m => m.Tipo == TipoMovimiento.Ingreso).Sum(m => m.SubtotalMxn);
        PagadoMes = mes.Where(m => m.Tipo == TipoMovimiento.Egreso).Sum(m => m.SubtotalMxn);

        Renovaciones = PuedeGestionar ? await _finanzas.RenovacionesSinCargoAsync() : [];

        var q = _db.Movimientos.AsNoTracking().AsQueryable();
        if (Trabajo is not null)
        {
            q = q.Where(m => m.TrabajoId == Trabajo);
            TrabajoNombre = await _db.Trabajos.Where(t => t.Id == Trabajo).Select(t => t.Folio + " · " + t.Nombre).FirstOrDefaultAsync();
        }
        if (!string.IsNullOrWhiteSpace(Buscar))
        {
            var b = Buscar.Trim();
            q = q.Where(m => m.Concepto.Contains(b) || (m.Proveedor != null && m.Proveedor.Contains(b)) || (m.Referencia != null && m.Referencia.Contains(b))
                             || (m.Cliente != null && (m.Cliente.Nombre.Contains(b) || (m.Cliente.NombreComercial != null && m.Cliente.NombreComercial.Contains(b)))));
        }
        if (Mes is { Length: 7 } && DateOnly.TryParseExact(Mes + "-01", "yyyy-MM-dd", out var desde))
        {
            var hasta = desde.AddMonths(1);
            q = q.Where(m => (m.FechaPago ?? m.FechaProgramada) >= desde && (m.FechaPago ?? m.FechaProgramada) < hasta);
        }

        // Conteos por vista en una sola consulta agregada (sin traer los movimientos a memoria).
        var k = await q.GroupBy(_ => 1).Select(g => new
        {
            Pendientes = g.Count(m => m.Estado == EstadoMovimiento.Esperado),
            PorCobrar = g.Count(m => m.Estado == EstadoMovimiento.Esperado && m.Tipo == TipoMovimiento.Ingreso),
            PorPagar = g.Count(m => m.Estado == EstadoMovimiento.Esperado && m.Tipo == TipoMovimiento.Egreso),
            Vencidos = g.Count(m => m.Estado == EstadoMovimiento.Esperado && m.FechaProgramada < hoy),
            Confirmados = g.Count(m => m.Estado == EstadoMovimiento.Confirmado),
            Cancelados = g.Count(m => m.Estado == EstadoMovimiento.Cancelado),
            Todos = g.Count()
        }).FirstOrDefaultAsync();
        Conteos = new()
        {
            ["pendientes"] = k?.Pendientes ?? 0,
            ["porcobrar"] = k?.PorCobrar ?? 0,
            ["porpagar"] = k?.PorPagar ?? 0,
            ["vencidos"] = k?.Vencidos ?? 0,
            ["confirmados"] = k?.Confirmados ?? 0,
            ["cancelados"] = k?.Cancelados ?? 0,
            ["todos"] = k?.Todos ?? 0
        };

        q = (Vista ?? "pendientes") switch
        {
            "porcobrar" => q.Where(m => m.Estado == EstadoMovimiento.Esperado && m.Tipo == TipoMovimiento.Ingreso),
            "porpagar" => q.Where(m => m.Estado == EstadoMovimiento.Esperado && m.Tipo == TipoMovimiento.Egreso),
            "vencidos" => q.Where(m => m.Estado == EstadoMovimiento.Esperado && m.FechaProgramada < hoy),
            "confirmados" => q.Where(m => m.Estado == EstadoMovimiento.Confirmado),
            "cancelados" => q.Where(m => m.Estado == EstadoMovimiento.Cancelado),
            "todos" => q,
            _ => q.Where(m => m.Estado == EstadoMovimiento.Esperado)
        };
        q = (Vista ?? "pendientes") is "confirmados" or "todos" or "cancelados"
            ? q.OrderByDescending(m => m.FechaPago ?? m.FechaProgramada).ThenByDescending(m => m.Id)
            : q.OrderBy(m => m.FechaProgramada).ThenBy(m => m.Id);

        var filas = await q.Take(Limite + 1).Select(m => new Fila(
                m,
                m.Categoria == null ? null : m.Categoria.Nombre,
                m.Cliente != null ? (m.Cliente.NombreComercial ?? m.Cliente.Nombre)
                    : m.ServicioExterno != null ? m.ServicioExterno.Nombre
                    : m.Proveedor ?? (m.Producto == null ? null : m.Producto.Nombre),
                m.Trabajo == null ? null : m.Trabajo.Folio,
                m.Cuenta == null ? null : (m.Cuenta.UltimosDigitos == null ? m.Cuenta.Alias : m.Cuenta.Alias + " ····" + m.Cuenta.UltimosDigitos)))
            .ToListAsync();
        HayMas = filas.Count > Limite;
        Movimientos = filas.Take(Limite).ToList();
    }
}
