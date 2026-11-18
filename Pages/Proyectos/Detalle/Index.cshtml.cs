using System.ComponentModel.DataAnnotations;
using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Pages.Proyectos.Tareas;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Proyectos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Proyectos.Detalle;

public class EntregableInput
{
    [Required(ErrorMessage = "Ingresa el nombre del entregable.")]
    [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "La descripción no puede exceder 1000 caracteres.")]
    public string? Descripcion { get; set; }

    public DateOnly? FechaCompromiso { get; set; }
    public EstadoEntregable Estado { get; set; } = EstadoEntregable.Pendiente;
    public DateOnly? FechaEntrega { get; set; }
}

public class EntregableModalVm
{
    public int? Id { get; init; }
    public int TrabajoId { get; init; }
    public bool EsNuevo => Id is null;
    public EntregableInput Input { get; init; } = new();
    public List<Adjunto> Archivos { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public string Volver { get; init; } = "";
}

[Authorize(Policy = Permisos.Proyectos.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioTrabajos _servicio;

    public IndexModel(GasDbContext db, ServicioTrabajos servicio)
    {
        _db = db;
        _servicio = servicio;
    }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Proyectos.Gestionar);
    public bool VerFinanzas => User.TienePermiso(Permisos.Finanzas.Ver);
    public bool PuedeRegistrarHoras { get; private set; }

    public Trabajo Trabajo { get; private set; } = null!;
    public string? Para { get; private set; }
    public string? Responsable { get; private set; }
    public string? Departamento { get; private set; }
    public List<Entregable> Entregables { get; private set; } = [];
    public Dictionary<EstadoTarea, List<GAS.Admin.Pages.Proyectos.Tareas.IndexModel.Tarjeta>> Columnas { get; private set; } = [];
    public int Tareas { get; private set; }
    public int TareasTerminadas { get; private set; }
    public decimal HorasEstimadas { get; private set; }
    public decimal HorasRegistradas { get; private set; }
    public List<RegistroHoras> HorasRecientes { get; private set; } = [];
    public List<Adjunto> Archivos { get; private set; } = [];
    public Rentabilidad? Rentabilidad { get; private set; }
    public sealed record CicloFila(CicloTrabajo Ciclo, decimal Horas, Dictionary<string, decimal> Costo);
    public List<CicloFila> Ciclos { get; private set; } = [];
    public bool FaltaCicloActual { get; private set; }
    public EntregableModalVm? ModalEntregable { get; private set; }
    public string Volver => Url.Page("/Proyectos/Detalle/Index", new { id = Trabajo.Id })!;

    public async Task<IActionResult> OnGetAsync(int id) => await CargarAsync(id) ? Page() : NotFound();

    // ---------- Entregables ----------

    public async Task<IActionResult> OnGetNuevoEntregableAsync(int trabajoId)
    {
        if (!PuedeGestionar) return Forbid();
        return Partial("_EntregableModal", new EntregableModalVm
        {
            TrabajoId = trabajoId, PuedeGestionar = true,
            Volver = Url.Page("/Proyectos/Detalle/Index", new { id = trabajoId })!
        });
    }

    public async Task<IActionResult> OnGetEditarEntregableAsync(int id)
    {
        var e = await _db.Entregables.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        return Partial("_EntregableModal", new EntregableModalVm
        {
            Id = e.Id, TrabajoId = e.TrabajoId, PuedeGestionar = PuedeGestionar,
            Input = new EntregableInput { Nombre = e.Nombre, Descripcion = e.Descripcion, FechaCompromiso = e.FechaCompromiso, Estado = e.Estado, FechaEntrega = e.FechaEntrega },
            Archivos = await _db.Adjuntos.AsNoTracking().Where(a => a.EntregableId == e.Id).OrderByDescending(a => a.FechaUtc).ToListAsync(),
            Volver = Url.Page("/Proyectos/Detalle/Index", new { id = e.TrabajoId })!
        });
    }

    public async Task<IActionResult> OnPostCrearEntregableAsync(int trabajoId, [Bind(Prefix = "Input")] EntregableInput input)
    {
        if (!PuedeGestionar) return Forbid();
        if (!await _db.Trabajos.AnyAsync(t => t.Id == trabajoId)) return NotFound();
        if (ModelState.IsValid)
        {
            var orden = await _db.Entregables.Where(e => e.TrabajoId == trabajoId).Select(e => (int?)e.Orden).MaxAsync() + 1 ?? 0;
            var e = new Entregable { TrabajoId = trabajoId, Orden = orden };
            Aplicar(e, input);
            _db.Entregables.Add(e);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"Entregable \"{e.Nombre}\" agregado.");
            return RedirectToPage(new { id = trabajoId });
        }
        await CargarAsync(trabajoId);
        ModalEntregable = new EntregableModalVm { TrabajoId = trabajoId, Input = input, PuedeGestionar = true, Volver = Volver };
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarEntregableAsync(int id, [Bind(Prefix = "Input")] EntregableInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var e = await _db.Entregables.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        if (ModelState.IsValid)
        {
            Aplicar(e, input);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"Entregable \"{e.Nombre}\" actualizado.");
            return RedirectToPage(new { id = e.TrabajoId });
        }
        await CargarAsync(e.TrabajoId);
        ModalEntregable = new EntregableModalVm { Id = id, TrabajoId = e.TrabajoId, Input = input, PuedeGestionar = true, Volver = Volver };
        return Page();
    }

    public async Task<IActionResult> OnPostEstadoEntregableAsync(int id, EstadoEntregable estado)
    {
        if (!PuedeGestionar) return Forbid();
        var e = await _db.Entregables.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        e.Estado = estado;
        e.FechaEntrega = estado == EstadoEntregable.Pendiente ? null : e.FechaEntrega ?? ServicioTrabajos.Hoy;
        await _db.SaveChangesAsync();
        this.ToastSuccess($"\"{e.Nombre}\" marcado como {Etiquetas(estado)}.");
        return RedirectToPage(new { id = e.TrabajoId });
    }

    public async Task<IActionResult> OnPostEliminarEntregableAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var e = await _db.Entregables.FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        (await _db.Tareas.Where(t => t.EntregableId == id).ToListAsync()).ForEach(t => t.EntregableId = null);
        (await _db.Adjuntos.Where(a => a.EntregableId == id).ToListAsync()).ForEach(a => a.EntregableId = null);
        // Los cobros ligados a este entregable (finiquito, hitos) se conservan en Finanzas sin la liga.
        (await _db.Movimientos.Where(m => m.EntregableId == id).ToListAsync()).ForEach(m => m.EntregableId = null);
        _db.Entregables.Remove(e);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Entregable \"{e.Nombre}\" eliminado.");
        return RedirectToPage(new { id = e.TrabajoId });
    }

    // ---------- Ciclos (servicios recurrentes) ----------

    public async Task<IActionResult> OnPostAbrirCicloAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var t = await _db.Trabajos.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null || t.Tipo != TipoTrabajoOperativo.ServicioRecurrente) return NotFound();
        var periodo = ServicioTrabajos.InicioMes(ServicioTrabajos.Hoy);
        if (await _db.Ciclos.AnyAsync(c => c.TrabajoId == id && c.Periodo == periodo))
        {
            this.ToastInfo("El ciclo de este mes ya existe.");
            return RedirectToPage(new { id });
        }
        var ciclo = await _servicio.AgregarCicloAsync(t, periodo);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Ciclo de {periodo.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-MX"))} abierto.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCicloAsync(int cicloId, EstadoCiclo estado)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await _db.Ciclos.FirstOrDefaultAsync(x => x.Id == cicloId);
        if (c is null) return NotFound();
        c.Estado = estado;
        await _db.SaveChangesAsync();
        this.ToastSuccess(estado == EstadoCiclo.Cerrado ? "Ciclo cerrado." : "Ciclo reabierto.");
        return RedirectToPage(new { id = c.TrabajoId });
    }

    // ---------- Apoyo ----------

    private static string Etiquetas(Enum e) => GAS.Admin.Helpers.Etiquetas.Texto(e).ToLowerInvariant();

    private static void Aplicar(Entregable e, EntregableInput input)
    {
        e.Nombre = input.Nombre.Trim();
        e.Descripcion = Opciones.Limpio(input.Descripcion);
        e.FechaCompromiso = input.FechaCompromiso;
        e.Estado = input.Estado;
        e.FechaEntrega = input.Estado == EstadoEntregable.Pendiente ? null : input.FechaEntrega ?? e.FechaEntrega ?? ServicioTrabajos.Hoy;
    }

    private async Task<bool> CargarAsync(int id)
    {
        var t = await _db.Trabajos.AsNoTracking()
            .Include(x => x.Cliente).Include(x => x.Producto).Include(x => x.Cotizacion)
            .Include(x => x.Responsable).Include(x => x.Departamento)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return false;

        Trabajo = t;
        Para = t.EsInterno ? t.Producto?.Nombre : t.Cliente?.NombreVisible;
        Responsable = t.Responsable?.NombreCompleto;
        Departamento = t.Departamento?.Nombre;
        PuedeRegistrarHoras = PuedeGestionar || await _servicio.PersonaDeUsuarioAsync(User.UsuarioId()) is not null;

        Entregables = await _db.Entregables.AsNoTracking().Where(e => e.TrabajoId == id).OrderBy(e => e.Orden).ToListAsync();

        var tareas = await _db.Tareas.AsNoTracking().Where(x => x.TrabajoId == id)
            .Select(x => new
            {
                Tarea = x,
                Responsable = x.Responsable == null ? null : x.Responsable.NombreCompleto,
                Color = x.Departamento == null ? null : x.Departamento.Color,
                Horas = x.Horas.Sum(h => (decimal?)h.Horas) ?? 0,
                Comentarios = x.Comentarios.Count
            }).ToListAsync();
        Tareas = tareas.Count;
        TareasTerminadas = tareas.Count(x => x.Tarea.Estado == EstadoTarea.Terminado);
        HorasEstimadas = tareas.Sum(x => x.Tarea.HorasEstimadas ?? 0);
        Columnas = Enum.GetValues<EstadoTarea>().ToDictionary(e => e, e => tareas.Where(x => x.Tarea.Estado == e)
            .OrderBy(x => x.Tarea.Orden).ThenBy(x => x.Tarea.Id)
            .Select(x => new GAS.Admin.Pages.Proyectos.Tareas.IndexModel.Tarjeta(x.Tarea, null, x.Responsable, x.Color, x.Horas, x.Comentarios)).ToList());

        var horas = await _db.RegistrosHoras.AsNoTracking().Include(h => h.Persona).Include(h => h.Tarea)
            .Where(h => h.TrabajoId == id).OrderByDescending(h => h.Fecha).ThenByDescending(h => h.Id).ToListAsync();
        HorasRegistradas = horas.Sum(h => h.Horas);
        HorasRecientes = horas.Take(8).ToList();

        Archivos = await _db.Adjuntos.AsNoTracking().Where(a => a.TrabajoId == id).OrderByDescending(a => a.FechaUtc).ToListAsync();

        if (VerFinanzas) Rentabilidad = await _servicio.RentabilidadAsync(t);

        if (t.Tipo == TipoTrabajoOperativo.ServicioRecurrente)
        {
            var ciclos = await _db.Ciclos.AsNoTracking().Where(c => c.TrabajoId == id).OrderByDescending(c => c.Periodo).ToListAsync();
            Ciclos = ciclos.Select(c =>
            {
                var delMes = horas.Where(h => h.Fecha.Year == c.Periodo.Year && h.Fecha.Month == c.Periodo.Month).ToList();
                return new CicloFila(c, delMes.Sum(h => h.Horas),
                    delMes.Where(h => h.Costo is not null).GroupBy(h => h.MonedaCosto ?? "MXN").ToDictionary(g => g.Key, g => g.Sum(h => h.Costo!.Value)));
            }).ToList();
            FaltaCicloActual = t.Activo && ciclos.All(c => c.Periodo != ServicioTrabajos.InicioMes(ServicioTrabajos.Hoy));
        }
        return true;
    }
}
