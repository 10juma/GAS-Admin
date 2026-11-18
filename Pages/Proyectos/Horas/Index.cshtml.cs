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

namespace GAS.Admin.Pages.Proyectos.Horas;

public class HorasInput
{
    public int? PersonaId { get; set; }
    public DateOnly Fecha { get; set; }

    [Range(0.25, 24, ErrorMessage = "Las horas deben estar entre 0.25 y 24.")]
    public decimal Horas { get; set; } = 1;

    public int? TrabajoId { get; set; }
    public int? TareaId { get; set; }

    [StringLength(500, ErrorMessage = "La nota no puede exceder 500 caracteres.")]
    public string? Nota { get; set; }
}

public sealed record OpcionTarea(int Id, int? TrabajoId, string Titulo);

public class HorasModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public HorasInput Input { get; init; } = new();
    public List<Opcion> Personas { get; init; } = [];
    public List<GAS.Admin.Pages.Proyectos.Tareas.OpcionTrabajo> Trabajos { get; init; } = [];
    public List<OpcionTarea> Tareas { get; init; } = [];
    /// <summary>Si solo puede registrar sus propias horas.</summary>
    public bool SoloPropias { get; init; }
    public bool VerCosto { get; init; }
    public decimal? CostoHora { get; init; }
    public string? MonedaCosto { get; init; }
    public string? Volver { get; init; }
}

[Authorize(Policy = Permisos.Proyectos.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioTrabajos _trabajos;

    public IndexModel(GasDbContext db, ServicioTrabajos trabajos)
    {
        _db = db;
        _trabajos = trabajos;
    }

    /// <summary>Lunes de la semana a mostrar.</summary>
    [BindProperty(SupportsGet = true)] public DateOnly? Semana { get; set; }
    [BindProperty(SupportsGet = true)] public int? Persona { get; set; }

    public bool Gestiona => User.TienePermiso(Permisos.Proyectos.Gestionar);
    public bool VerCosto => User.TienePermiso(Permisos.Finanzas.Ver);
    public Persona? MiPersona { get; private set; }
    public bool PuedeRegistrar => Gestiona || MiPersona is not null;

    public DateOnly Lunes { get; private set; }
    public List<RegistroHoras> Registros { get; private set; } = [];
    public sealed record Carga(Persona Persona, decimal Horas, decimal?[] PorDia);
    public List<Carga> Cargas { get; private set; } = [];
    public List<(string Trabajo, decimal Horas)> PorTrabajo { get; private set; } = [];
    public decimal TotalSemana { get; private set; }
    public Dictionary<string, decimal> CostoSemana { get; private set; } = [];
    public List<Opcion> Personas { get; private set; } = [];
    public HorasModalVm? Modal { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnGetNuevoAsync(int? trabajoId, int? tareaId, string? volver)
    {
        MiPersona = await _trabajos.PersonaDeUsuarioAsync(User.UsuarioId());
        if (!PuedeRegistrar) return Forbid();
        if (tareaId is not null)
        {
            trabajoId = await _db.Tareas.Where(t => t.Id == tareaId).Select(t => t.TrabajoId).FirstOrDefaultAsync() ?? trabajoId;
        }
        var input = new HorasInput { PersonaId = MiPersona?.Id, Fecha = ServicioTrabajos.Hoy, TrabajoId = trabajoId, TareaId = tareaId };
        return Partial("_HorasModal", await ModalAsync(null, input, volver));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id, string? volver)
    {
        MiPersona = await _trabajos.PersonaDeUsuarioAsync(User.UsuarioId());
        var h = await _db.RegistrosHoras.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (h is null) return NotFound();
        if (!PuedeEditar(h)) return Forbid();
        var input = new HorasInput { PersonaId = h.PersonaId, Fecha = h.Fecha, Horas = h.Horas, TrabajoId = h.TrabajoId, TareaId = h.TareaId, Nota = h.Nota };
        return Partial("_HorasModal", await ModalAsync(h, input, volver));
    }

    public async Task<IActionResult> OnPostCrearAsync([Bind(Prefix = "Input")] HorasInput input, string? volver)
    {
        MiPersona = await _trabajos.PersonaDeUsuarioAsync(User.UsuarioId());
        if (!PuedeRegistrar) return Forbid();
        var persona = await ValidarAsync(input);
        if (ModelState.IsValid && persona is not null)
        {
            var h = new RegistroHoras();
            Aplicar(h, input, persona, congelarCosto: true);
            _db.RegistrosHoras.Add(h);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"{h.Horas:0.##} h registradas para {persona.NombreCompleto}.");
            return this.VolverA(volver, "Index", new { semana = LunesDe(h.Fecha).ToString("yyyy-MM-dd") });
        }
        await CargarAsync();
        Modal = await ModalAsync(null, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id, [Bind(Prefix = "Input")] HorasInput input, string? volver)
    {
        MiPersona = await _trabajos.PersonaDeUsuarioAsync(User.UsuarioId());
        var h = await _db.RegistrosHoras.FirstOrDefaultAsync(x => x.Id == id);
        if (h is null) return NotFound();
        if (!PuedeEditar(h)) return Forbid();
        var persona = await ValidarAsync(input);
        if (ModelState.IsValid && persona is not null)
        {
            // El costo se vuelve a tomar solo si cambió la persona.
            Aplicar(h, input, persona, congelarCosto: h.PersonaId != persona.Id);
            await _db.SaveChangesAsync();
            this.ToastSuccess("Registro de horas actualizado.");
            return this.VolverA(volver, "Index", new { semana = LunesDe(h.Fecha).ToString("yyyy-MM-dd") });
        }
        await CargarAsync();
        Modal = await ModalAsync(h, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, string? volver)
    {
        MiPersona = await _trabajos.PersonaDeUsuarioAsync(User.UsuarioId());
        var h = await _db.RegistrosHoras.FirstOrDefaultAsync(x => x.Id == id);
        if (h is null) return NotFound();
        if (!PuedeEditar(h)) return Forbid();
        _db.RegistrosHoras.Remove(h);
        await _db.SaveChangesAsync();
        this.ToastSuccess("Registro de horas eliminado.");
        return this.VolverA(volver, "Index", new { semana = LunesDe(h.Fecha).ToString("yyyy-MM-dd") });
    }

    // ---------- Apoyo ----------

    public static DateOnly LunesDe(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    private bool PuedeEditar(RegistroHoras h) => Gestiona || (MiPersona is not null && h.PersonaId == MiPersona.Id);

    private static void Aplicar(RegistroHoras h, HorasInput input, Persona persona, bool congelarCosto)
    {
        h.PersonaId = persona.Id;
        h.Fecha = input.Fecha;
        h.Horas = Math.Round(input.Horas, 2);
        h.TrabajoId = input.TrabajoId;
        h.TareaId = input.TareaId;
        h.Nota = Opciones.Limpio(input.Nota);
        if (congelarCosto)
        {
            h.CostoHora = persona.CostoHora;
            h.MonedaCosto = persona.CostoHora is null ? null : persona.MonedaCosto;
        }
    }

    private async Task<Persona?> ValidarAsync(HorasInput input)
    {
        if (!Gestiona) input.PersonaId = MiPersona?.Id;
        var persona = input.PersonaId is null ? null : await _db.Personas.FirstOrDefaultAsync(p => p.Id == input.PersonaId);
        if (persona is null) ModelState.AddModelError(string.Empty, "Elige a la persona que trabajó.");

        if (input.Fecha == default) input.Fecha = ServicioTrabajos.Hoy;
        if (input.Fecha > ServicioTrabajos.Hoy) ModelState.AddModelError(string.Empty, "No se pueden registrar horas en fechas futuras.");

        if (input.TareaId is not null)
        {
            var tarea = await _db.Tareas.AsNoTracking().FirstOrDefaultAsync(t => t.Id == input.TareaId);
            if (tarea is null) input.TareaId = null;
            else if (tarea.TrabajoId is not null) input.TrabajoId = tarea.TrabajoId;
        }
        if (input.TrabajoId is not null && !await _db.Trabajos.AnyAsync(t => t.Id == input.TrabajoId)) input.TrabajoId = null;
        if (input.TrabajoId is null && input.TareaId is null)
        {
            ModelState.AddModelError(string.Empty, "Indica el trabajo o la tarea en que se trabajó.");
        }
        return persona;
    }

    private async Task<HorasModalVm> ModalAsync(RegistroHoras? h, HorasInput input, string? volver)
    {
        var persona = input.PersonaId is null ? null : await _db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.PersonaId);
        return new HorasModalVm
        {
            Id = h?.Id,
            Input = input,
            Personas = await _db.Personas.AsNoTracking().Where(p => p.Activa || p.Id == input.PersonaId)
                .OrderBy(p => p.NombreCompleto).Select(p => new Opcion(p.Id, p.NombreCompleto)).ToListAsync(),
            Trabajos = await _db.Trabajos.AsNoTracking()
                .Where(t => (t.Estado != EstadoTrabajo.Cerrado && t.Estado != EstadoTrabajo.Cancelado) || t.Id == input.TrabajoId)
                .OrderByDescending(t => t.Folio).Select(t => new GAS.Admin.Pages.Proyectos.Tareas.OpcionTrabajo(t.Id, t.Folio, t.Nombre)).ToListAsync(),
            Tareas = await _db.Tareas.AsNoTracking()
                .Where(t => t.Estado != EstadoTarea.Terminado || t.Id == input.TareaId)
                .OrderBy(t => t.Titulo).Select(t => new OpcionTarea(t.Id, t.TrabajoId, t.Titulo)).ToListAsync(),
            SoloPropias = !Gestiona,
            VerCosto = VerCosto,
            CostoHora = h is not null ? h.CostoHora : persona?.CostoHora,
            MonedaCosto = h is not null ? h.MonedaCosto : persona?.MonedaCosto,
            Volver = volver
        };
    }

    private async Task CargarAsync()
    {
        MiPersona ??= await _trabajos.PersonaDeUsuarioAsync(User.UsuarioId());
        Lunes = LunesDe(Semana ?? ServicioTrabajos.Hoy);
        var domingo = Lunes.AddDays(6);

        var q = _db.RegistrosHoras.AsNoTracking().Include(h => h.Persona).Include(h => h.Trabajo).Include(h => h.Tarea)
            .Where(h => h.Fecha >= Lunes && h.Fecha <= domingo);
        if (Persona is not null) q = q.Where(h => h.PersonaId == Persona);
        Registros = await q.OrderByDescending(h => h.Fecha).ThenBy(h => h.Persona.NombreCompleto).ToListAsync();

        TotalSemana = Registros.Sum(h => h.Horas);
        if (VerCosto)
        {
            CostoSemana = Registros.Where(h => h.Costo is not null).GroupBy(h => h.MonedaCosto ?? "MXN")
                .ToDictionary(g => g.Key, g => g.Sum(h => h.Costo!.Value));
        }

        var personas = await _db.Personas.AsNoTracking().Where(p => p.Activa && (Persona == null || p.Id == Persona))
            .OrderBy(p => p.NombreCompleto).ToListAsync();
        Cargas = personas.Select(p =>
        {
            var suyas = Registros.Where(h => h.PersonaId == p.Id).ToList();
            var dias = Enumerable.Range(0, 7).Select(i => (decimal?)suyas.Where(h => h.Fecha == Lunes.AddDays(i)).Sum(h => h.Horas)).Select(v => v == 0 ? null : v).ToArray();
            return new Carga(p, suyas.Sum(h => h.Horas), dias);
        }).ToList();

        PorTrabajo = Registros.GroupBy(h => h.Trabajo is null ? "Sin trabajo" : $"{h.Trabajo.Folio} · {h.Trabajo.Nombre}")
            .Select(g => (g.Key, g.Sum(h => h.Horas))).OrderByDescending(x => x.Item2).ToList();
        Personas = await _db.Personas.AsNoTracking().Where(p => p.Activa).OrderBy(p => p.NombreCompleto)
            .Select(p => new Opcion(p.Id, p.NombreCompleto)).ToListAsync();
    }
}
