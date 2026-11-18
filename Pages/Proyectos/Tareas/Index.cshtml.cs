using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Pages.Comercial.Oportunidades;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Navegacion;
using GAS.Admin.Services.Proyectos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Proyectos.Tareas;

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

    [BindProperty(SupportsGet = true)] public int? Trabajo { get; set; }
    [BindProperty(SupportsGet = true)] public int? Responsable { get; set; }
    [BindProperty(SupportsGet = true)] public int? Departamento { get; set; }
    /// <summary>"mias", "sueltas" (sin trabajo) o vacío.</summary>
    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Proyectos.Gestionar);

    public sealed record Tarjeta(Tarea Tarea, string? Folio, string? Responsable, string? Color, decimal Horas, int Comentarios);

    public Dictionary<EstadoTarea, List<Tarjeta>> Columnas { get; private set; } = [];
    public List<OpcionTrabajo> TrabajosActivos { get; private set; } = [];
    public List<Opcion> Personas { get; private set; } = [];
    public List<Opcion> Departamentos { get; private set; } = [];
    public int Vencidas { get; private set; }
    public int Total { get; private set; }
    public int? MiPersonaId { get; private set; }
    public TareaModalVm? Modal { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    // ---------- Modal ----------

    public async Task<IActionResult> OnGetNuevoAsync(int? trabajoId, EstadoTarea? estado, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var yo = await _trabajos.PersonaDeUsuarioAsync(User.UsuarioId());
        var input = new TareaInput { TrabajoId = trabajoId, Estado = estado ?? EstadoTarea.Pendiente, ResponsableId = yo?.Id, DepartamentoId = yo?.DepartamentoId };
        return Partial("_TareaModal", await ModalAsync(null, input, volver));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id, string? volver)
    {
        var t = await _db.Tareas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        return Partial("_TareaModal", await ModalAsync(t, TareaInput.Desde(t), volver));
    }

    // ---------- Acciones ----------

    public async Task<IActionResult> OnPostCrearAsync([Bind(Prefix = "Input")] TareaInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarAsync(input);
        if (ModelState.IsValid)
        {
            var t = new Tarea();
            input.AplicarA(t);
            t.Orden = await _db.Tareas.Where(x => x.Estado == t.Estado).Select(x => (int?)x.Orden).MaxAsync() + 1 ?? 0;
            _db.Tareas.Add(t);
            await _db.SaveChangesAsync();
            _contadores.Invalidar();
            this.ToastSuccess($"Tarea \"{t.Titulo}\" creada.");
            return this.VolverA(volver, "Index");
        }
        await CargarAsync();
        Modal = await ModalAsync(null, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id, [Bind(Prefix = "Input")] TareaInput input, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        await ValidarAsync(input);
        if (ModelState.IsValid)
        {
            var estadoAnterior = t.Estado;
            input.AplicarA(t);
            if (t.Estado != estadoAnterior)
                t.Orden = await _db.Tareas.Where(x => x.Estado == t.Estado).Select(x => (int?)x.Orden).MaxAsync() + 1 ?? 0;
            var cambios = _db.ChangeTracker.HasChanges();
            await _db.SaveChangesAsync();
            _contadores.Invalidar();
            if (cambios) this.ToastSuccess($"Tarea \"{t.Titulo}\" actualizada.");
            else this.ToastInfo("No hubo cambios que guardar.");
            return this.VolverA(volver, "Index");
        }
        await CargarAsync();
        Modal = await ModalAsync(t, input, volver);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id, string? volver)
    {
        if (!PuedeGestionar) return Forbid();
        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        if (await _db.RegistrosHoras.AnyAsync(h => h.TareaId == id))
        {
            this.ToastWarning("La tarea tiene horas registradas; márcala como Terminado en lugar de eliminarla.");
            return this.VolverA(volver, "Index");
        }
        var adjuntos = await _db.Adjuntos.Where(a => a.TareaId == id).ToListAsync();
        adjuntos.ForEach(a => a.TareaId = null);
        (await _db.Tickets.Where(x => x.TareaId == id).ToListAsync()).ForEach(x => x.TareaId = null);
        _db.Tareas.Remove(t);
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        this.ToastSuccess($"Tarea \"{t.Titulo}\" eliminada.");
        return this.VolverA(volver, "Index");
    }

    public async Task<IActionResult> OnPostComentarAsync(int id, string? texto, string? volver)
    {
        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        texto = Opciones.Limpio(texto);
        if (texto is null)
        {
            this.ToastWarning("Escribe un comentario.");
            return this.VolverA(volver, "Index");
        }
        _db.TareaComentarios.Add(new TareaComentario
        {
            TareaId = id,
            Autor = User.NombreVisible(),
            Texto = texto.Length > 4000 ? texto[..4000] : texto
        });
        await _db.SaveChangesAsync();
        this.ToastSuccess("Comentario agregado.");
        TempData["GAS.AbrirModal"] = Url.Page("/Proyectos/Tareas/Index", "Editar", new { id, volver });
        return this.VolverA(volver, "Index");
    }

    public async Task<IActionResult> OnPostMoverAsync([FromBody] MoverRequest? req)
    {
        if (!PuedeGestionar) return Forbid();
        if (req is null || !Enum.TryParse<EstadoTarea>(req.Etapa, out var estado)) return BadRequest();
        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == req.Id);
        if (t is null) return NotFound();

        if (t.Estado != estado)
        {
            t.FechaTerminadaUtc = estado == EstadoTarea.Terminado ? DateTime.UtcNow : null;
            t.Estado = estado;
        }
        var ids = (req.Ids ?? []).Distinct().ToList();
        var columna = await _db.Tareas.Where(x => ids.Contains(x.Id)).ToListAsync();
        foreach (var x in columna) x.Orden = ids.IndexOf(x.Id);
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        return new JsonResult(new { ok = true });
    }

    // ---------- Apoyo ----------

    private async Task ValidarAsync(TareaInput input)
    {
        if (input.TrabajoId is not null && !await _db.Trabajos.AnyAsync(t => t.Id == input.TrabajoId)) input.TrabajoId = null;
        if (input.EntregableId is not null)
        {
            var e = await _db.Entregables.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.EntregableId);
            if (e is null || e.TrabajoId != input.TrabajoId) input.EntregableId = null;
        }
        if (input.ResponsableId is not null && !await _db.Personas.AnyAsync(p => p.Id == input.ResponsableId)) input.ResponsableId = null;
        if (input.FechaVencimiento is not null && input.FechaInicio is not null && input.FechaVencimiento < input.FechaInicio)
        {
            ModelState.AddModelError(string.Empty, "El vencimiento no puede ser antes del inicio.");
        }
    }

    private async Task<TareaModalVm> ModalAsync(Tarea? t, TareaInput input, string? volver)
    {
        var yo = await _trabajos.PersonaDeUsuarioAsync(User.UsuarioId());
        return new TareaModalVm
        {
            Id = t?.Id,
            Input = input,
            Trabajos = await _db.Trabajos.AsNoTracking()
                .Where(x => (x.Estado != EstadoTrabajo.Cerrado && x.Estado != EstadoTrabajo.Cancelado) || x.Id == input.TrabajoId)
                .OrderByDescending(x => x.Folio).Select(x => new OpcionTrabajo(x.Id, x.Folio, x.Nombre)).ToListAsync(),
            Entregables = await _db.Entregables.AsNoTracking()
                .Where(e => e.Trabajo.Estado != EstadoTrabajo.Cerrado && e.Trabajo.Estado != EstadoTrabajo.Cancelado || e.Id == input.EntregableId)
                .OrderBy(e => e.Orden).Select(e => new OpcionEntregable(e.Id, e.TrabajoId, e.Nombre)).ToListAsync(),
            Departamentos = await _db.Departamentos.AsNoTracking().OrderBy(d => d.Orden).Select(d => new Opcion(d.Id, d.Nombre)).ToListAsync(),
            Personas = await _db.Personas.AsNoTracking().Where(p => p.Activa || p.Id == input.ResponsableId)
                .OrderBy(p => p.NombreCompleto).Select(p => new Opcion(p.Id, p.NombreCompleto)).ToListAsync(),
            Comentarios = t is null ? [] : await _db.TareaComentarios.AsNoTracking().Where(c => c.TareaId == t.Id).OrderBy(c => c.FechaUtc).ToListAsync(),
            Horas = t is null ? [] : await _db.RegistrosHoras.AsNoTracking().Include(h => h.Persona).Where(h => h.TareaId == t.Id).OrderByDescending(h => h.Fecha).ToListAsync(),
            Archivos = t is null ? [] : await _db.Adjuntos.AsNoTracking().Where(a => a.TareaId == t.Id).OrderByDescending(a => a.FechaUtc).ToListAsync(),
            PuedeGestionar = PuedeGestionar,
            PuedeComentar = true,
            PuedeRegistrarHoras = PuedeGestionar || yo is not null,
            CreadoUtc = t?.CreadoUtc,
            CreadoPor = t?.CreadoPor,
            ModificadoUtc = t?.ModificadoUtc,
            ModificadoPor = t?.ModificadoPor,
            Volver = volver
        };
    }

    private async Task CargarAsync()
    {
        var hoy = ServicioTrabajos.Hoy;
        MiPersonaId = (await _trabajos.PersonaDeUsuarioAsync(User.UsuarioId()))?.Id;

        var q = _db.Tareas.AsNoTracking().Where(t => t.Trabajo == null || (t.Trabajo.Estado != EstadoTrabajo.Cerrado && t.Trabajo.Estado != EstadoTrabajo.Cancelado));
        if (Trabajo is not null) q = q.Where(t => t.TrabajoId == Trabajo);
        if (Responsable is not null) q = q.Where(t => t.ResponsableId == Responsable);
        if (Departamento is not null) q = q.Where(t => t.DepartamentoId == Departamento);
        if (Vista == "mias") q = q.Where(t => t.ResponsableId == MiPersonaId);
        if (Vista == "sueltas") q = q.Where(t => t.TrabajoId == null);
        // Las terminadas hace más de 30 días ya no se muestran: se filtran en la base, no en memoria.
        var limiteTerminadas = DateTime.UtcNow.AddDays(-30);
        q = q.Where(t => t.Estado != EstadoTarea.Terminado || t.FechaTerminadaUtc == null || t.FechaTerminadaUtc >= limiteTerminadas);

        var datos = await q.Select(t => new
        {
            Tarea = t,
            Folio = t.Trabajo == null ? null : t.Trabajo.Folio,
            Responsable = t.Responsable == null ? null : t.Responsable.NombreCompleto,
            Color = t.Departamento == null ? null : t.Departamento.Color,
            Horas = t.Horas.Sum(h => (decimal?)h.Horas) ?? 0,
            Comentarios = t.Comentarios.Count
        }).ToListAsync();

        var tarjetas = datos.Select(d => new Tarjeta(d.Tarea, d.Folio, d.Responsable, d.Color, d.Horas, d.Comentarios)).ToList();
        Total = tarjetas.Count;
        Vencidas = tarjetas.Count(t => t.Tarea.Vencida(hoy));
        var hace30 = DateTime.UtcNow.AddDays(-30);
        Columnas = Enum.GetValues<EstadoTarea>().ToDictionary(e => e, e => tarjetas
            .Where(t => t.Tarea.Estado == e && (e != EstadoTarea.Terminado || t.Tarea.FechaTerminadaUtc >= hace30 || t.Tarea.FechaTerminadaUtc == null))
            .OrderBy(t => t.Tarea.Orden).ThenByDescending(t => t.Tarea.Prioridad).ThenBy(t => t.Tarea.Id).ToList());

        TrabajosActivos = await _db.Trabajos.AsNoTracking()
            .Where(t => t.Estado != EstadoTrabajo.Cerrado && t.Estado != EstadoTrabajo.Cancelado)
            .OrderByDescending(t => t.Folio).Select(t => new OpcionTrabajo(t.Id, t.Folio, t.Nombre)).ToListAsync();
        Personas = await _db.Personas.AsNoTracking().Where(p => p.Activa).OrderBy(p => p.NombreCompleto).Select(p => new Opcion(p.Id, p.NombreCompleto)).ToListAsync();
        Departamentos = await _db.Departamentos.AsNoTracking().OrderBy(d => d.Orden).Select(d => new Opcion(d.Id, d.Nombre)).ToListAsync();
    }
}
