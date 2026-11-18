using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Navegacion;
using GAS.Admin.Services.Proyectos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Proyectos;

[Authorize(Policy = Permisos.Proyectos.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioTrabajos _trabajos;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, ServicioTrabajos trabajos, ContadoresMenu contadores)
    {
        _db = db;
        _trabajos = trabajos;
        _contadores = contadores;
    }

    /// <summary>activos (por defecto), atrasados, cerrados o todos.</summary>
    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }
    [BindProperty(SupportsGet = true)] public string? Tipo { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Proyectos.Gestionar);
    public bool VerFinanzas => User.TienePermiso(Permisos.Finanzas.Ver);
    public bool EditarFinanzas => PuedeGestionar && User.TienePermiso(Permisos.Finanzas.Gestionar);

    public sealed record Fila(Trabajo Trabajo, string? Para, string? Responsable, string? Color, int Tareas, int TareasTerminadas, decimal Horas, bool Atrasado);

    public List<Fila> Trabajos { get; private set; } = [];
    public int Activos { get; private set; }
    public int Atrasados { get; private set; }
    public int Cerrados { get; private set; }
    public int Total { get; private set; }
    public Dictionary<TipoTrabajoOperativo, int> PorTipo { get; private set; } = [];
    public int TareasVencidas { get; private set; }
    public decimal HorasSemana { get; private set; }
    public int Capacidad { get; private set; }
    public TrabajoModalVm? Modal { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnGetNuevoAsync(string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var yo = await _trabajos.PersonaDeUsuarioAsync(User.UsuarioId());
        var input = new TrabajoInput { FechaInicio = ServicioTrabajos.Hoy, ResponsableId = yo?.Id, DepartamentoId = yo?.DepartamentoId };
        return Partial("_TrabajoModal", await ModalAsync(null, input, volver));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id, string? volver)
    {
        var t = await _db.Trabajos.AsNoTracking().Include(x => x.Cotizacion).FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        var input = TrabajoInput.Desde(t);
        if (!VerFinanzas) { input.Presupuesto = null; input.GastosDirectos = 0; }
        return Partial("_TrabajoModal", await ModalAsync(t, input, volver));
    }

    public async Task<IActionResult> OnPostCrearAsync([Bind(Prefix = "Input")] TrabajoInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarAsync(input);
        if (ModelState.IsValid)
        {
            var t = new Trabajo { Folio = await _trabajos.SiguienteFolioAsync(ServicioTrabajos.Hoy.Year) };
            input.AplicarA(t, EditarFinanzas);
            if (t.Tipo == TipoTrabajoOperativo.ServicioRecurrente)
            {
                await _trabajos.AgregarCicloAsync(t, t.FechaInicio ?? ServicioTrabajos.Hoy);
            }
            _db.Trabajos.Add(t);
            await _db.GuardarConFolioAsync(async () => t.Folio = await _trabajos.SiguienteFolioAsync(ServicioTrabajos.Hoy.Year));
            _contadores.Invalidar();
            this.ToastSuccess($"Trabajo {t.Folio} creado.");
            return RedirectToPage("/Proyectos/Detalle/Index", new { id = t.Id });
        }
        await CargarAsync();
        Modal = await ModalAsync(null, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id, [Bind(Prefix = "Input")] TrabajoInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var t = await _db.Trabajos.Include(x => x.Cotizacion).FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        await ValidarAsync(input);
        if (ModelState.IsValid)
        {
            input.AplicarA(t, EditarFinanzas);
            var cambios = _db.ChangeTracker.HasChanges();
            await _db.SaveChangesAsync();
            _contadores.Invalidar();
            if (cambios) this.ToastSuccess($"Trabajo {t.Folio} actualizado.");
            else this.ToastInfo("No hubo cambios que guardar.");
            return this.VolverA(volver, "Index");
        }
        await CargarAsync();
        Modal = await ModalAsync(t, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var t = await _db.Trabajos.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        if (await _db.RegistrosHoras.AnyAsync(h => h.TrabajoId == id) || await _db.Adjuntos.AnyAsync(a => a.TrabajoId == id)
            || await _db.Movimientos.AnyAsync(m => m.TrabajoId == id) || await _db.Tickets.AnyAsync(x => x.Tarea != null && x.Tarea.TrabajoId == id))
        {
            this.ToastWarning($"{t.Folio} tiene horas, archivos o cobros en Finanzas. Cancélalo en lugar de eliminarlo para conservar su historial.");
            return RedirectToPage("/Proyectos/Detalle/Index", new { id });
        }
        _db.Trabajos.Remove(t);
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        this.ToastSuccess($"Trabajo {t.Folio} eliminado.");
        return RedirectToPage("Index");
    }

    // ---------- Apoyo ----------

    private async Task ValidarAsync(TrabajoInput input)
    {
        if (input.Tipo == TipoTrabajoOperativo.ProyectoInterno)
        {
            if (input.ProductoId is null) ModelState.AddModelError(string.Empty, "Un proyecto interno debe ligarse a un producto.");
        }
        else if (input.ClienteId is null || !await _db.Clientes.AnyAsync(c => c.Id == input.ClienteId))
        {
            ModelState.AddModelError(string.Empty, "Elige el cliente del trabajo.");
        }
        if (input.FechaEntrega is not null && input.FechaInicio is not null && input.FechaEntrega < input.FechaInicio)
        {
            ModelState.AddModelError(string.Empty, "La entrega no puede ser antes del inicio.");
        }
        if (!TrabajoInput.Monedas.Contains(input.Moneda)) input.Moneda = "MXN";
    }

    private async Task<TrabajoModalVm> ModalAsync(Trabajo? t, TrabajoInput input, string? volver) => new()
    {
        Id = t?.Id,
        Folio = t?.Folio,
        Cotizacion = t?.Cotizacion?.FolioCompleto,
        CotizacionId = t?.CotizacionId,
        Input = input,
        Clientes = await _db.Clientes.AsNoTracking().OrderBy(c => c.NombreComercial ?? c.Nombre)
            .Select(c => new Opcion(c.Id, c.NombreComercial ?? c.Nombre)).ToListAsync(),
        Productos = await _db.Productos.AsNoTracking().OrderBy(p => p.Nombre).Select(p => new Opcion(p.Id, p.Nombre)).ToListAsync(),
        Departamentos = await _db.Departamentos.AsNoTracking().OrderBy(d => d.Orden).Select(d => new Opcion(d.Id, d.Nombre)).ToListAsync(),
        Personas = await _db.Personas.AsNoTracking().Where(p => p.Activa || p.Id == input.ResponsableId)
            .OrderBy(p => p.NombreCompleto).Select(p => new Opcion(p.Id, p.NombreCompleto)).ToListAsync(),
        PuedeGestionar = PuedeGestionar,
        VerFinanzas = VerFinanzas,
        EditarFinanzas = EditarFinanzas,
        CreadoUtc = t?.CreadoUtc,
        CreadoPor = t?.CreadoPor,
        ModificadoUtc = t?.ModificadoUtc,
        ModificadoPor = t?.ModificadoPor,
        Volver = volver
    };

    private async Task CargarAsync()
    {
        var hoy = ServicioTrabajos.Hoy;
        var datos = await _db.Trabajos.AsNoTracking()
            .Select(t => new
            {
                Trabajo = t,
                Para = t.Tipo == TipoTrabajoOperativo.ProyectoInterno
                    ? (t.Producto == null ? null : t.Producto.Nombre)
                    : (t.Cliente == null ? null : (t.Cliente.NombreComercial ?? t.Cliente.Nombre)),
                Responsable = t.Responsable == null ? null : t.Responsable.NombreCompleto,
                Color = t.Departamento == null ? null : t.Departamento.Color,
                Tareas = t.Tareas.Count,
                Terminadas = t.Tareas.Count(x => x.Estado == EstadoTarea.Terminado),
                Horas = t.Horas.Sum(h => (decimal?)h.Horas) ?? 0
            })
            .ToListAsync();

        var filas = datos.Select(d => new Fila(d.Trabajo, d.Para, d.Responsable, d.Color, d.Tareas, d.Terminadas, d.Horas, d.Trabajo.Atrasado(hoy))).ToList();
        Total = filas.Count;
        Activos = filas.Count(f => f.Trabajo.Activo);
        Atrasados = filas.Count(f => f.Atrasado);
        Cerrados = filas.Count(f => !f.Trabajo.Activo);
        PorTipo = filas.Where(f => f.Trabajo.Activo).GroupBy(f => f.Trabajo.Tipo).ToDictionary(g => g.Key, g => g.Count());

        TareasVencidas = (await _db.Tareas.AsNoTracking().Where(t => t.Estado != EstadoTarea.Terminado && t.FechaVencimiento != null)
            .Select(t => t.FechaVencimiento).ToListAsync()).Count(f => f < hoy);
        var lunes = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));
        HorasSemana = await _db.RegistrosHoras.Where(h => h.Fecha >= lunes && h.Fecha <= hoy).SumAsync(h => (decimal?)h.Horas) ?? 0;
        Capacidad = await _db.Personas.Where(p => p.Activa).SumAsync(p => p.HorasSemana ?? 0);

        IEnumerable<Fila> q = Vista switch
        {
            "atrasados" => filas.Where(f => f.Atrasado),
            "cerrados" => filas.Where(f => !f.Trabajo.Activo),
            "todos" => filas,
            _ => filas.Where(f => f.Trabajo.Activo)
        };
        if (Enum.TryParse<TipoTrabajoOperativo>(Tipo, out var tipo)) q = q.Where(f => f.Trabajo.Tipo == tipo);

        Trabajos = q.OrderByDescending(f => f.Atrasado)
            .ThenByDescending(f => f.Trabajo.Prioridad)
            .ThenBy(f => f.Trabajo.FechaEntrega ?? DateOnly.MaxValue)
            .ThenByDescending(f => f.Trabajo.Folio)
            .ToList();
    }
}
