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

namespace GAS.Admin.Pages.Organizacion;

[Authorize(Policy = Permisos.Organizacion.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, ContadoresMenu contadores)
    {
        _db = db;
        _contadores = contadores;
    }

    /// <summary>"organigrama" (por defecto) o "personal".</summary>
    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Organizacion.Gestionar);
    /// <summary>El costo por hora solo lo ve quien tiene permiso de Finanzas.</summary>
    public bool VerCosto => User.TienePermiso(Permisos.Finanzas.Ver);
    public bool EditarCosto => PuedeGestionar && User.TienePermiso(Permisos.Finanzas.Gestionar);

    public sealed record Integrante(Persona Persona, string? Puesto, string? Usuario);
    public sealed record Area(Departamento Departamento, string? Responsable, List<Integrante> Personas);

    public List<Area> Areas { get; private set; } = [];
    public List<Integrante> SinDepartamento { get; private set; } = [];
    public List<Integrante> Todas { get; private set; } = [];
    public int Activas { get; private set; }
    public int Socios { get; private set; }
    public int Externos { get; private set; }
    public int HorasSemana { get; private set; }
    public Dictionary<string, decimal> CostoMensual { get; private set; } = [];
    public PersonaModalVm? Modal { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnGetNuevoAsync(int? departamentoId)
    {
        if (!PuedeGestionar) return Forbid();
        return Partial("_PersonaModal", await ModalAsync(null, new PersonaInput { DepartamentoId = departamentoId, FechaIngreso = DateOnly.FromDateTime(DateTime.Today) }));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        var p = await _db.Personas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        return Partial("_PersonaModal", await ModalAsync(p, PersonaInput.Desde(p, VerCosto)));
    }

    public async Task<IActionResult> OnPostCrearAsync([Bind(Prefix = "Input")] PersonaInput input)
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarAsync(input, null);
        if (ModelState.IsValid)
        {
            var p = new Persona();
            input.AplicarA(p, EditarCosto);
            _db.Personas.Add(p);
            await _db.SaveChangesAsync();
            _contadores.Invalidar();
            this.ToastSuccess($"{p.NombreCompleto} agregado al equipo.");
            return Volver();
        }
        await CargarAsync();
        Modal = await ModalAsync(null, input);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id, [Bind(Prefix = "Input")] PersonaInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var p = await _db.Personas.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        await ValidarAsync(input, id);
        if (ModelState.IsValid)
        {
            input.AplicarA(p, EditarCosto);
            var cambios = _db.ChangeTracker.HasChanges();
            await _db.SaveChangesAsync();
            _contadores.Invalidar();
            if (cambios) this.ToastSuccess($"Datos de {p.NombreCompleto} guardados.");
            else this.ToastInfo("No hubo cambios que guardar.");
            return Volver();
        }
        await CargarAsync();
        Modal = await ModalAsync(p, input);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var p = await _db.Personas.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        // Deja sin responsable los departamentos que dirigía.
        var departamentos = await _db.Departamentos.Where(d => d.ResponsableId == id).ToListAsync();
        departamentos.ForEach(d => d.ResponsableId = null);
        if (await _db.RegistrosHoras.AnyAsync(h => h.PersonaId == id))
        {
            this.ToastWarning($"{p.NombreCompleto} tiene horas registradas. Márcala como inactiva para conservar su historial.");
            return Volver();
        }
        (await _db.Tareas.Where(t => t.ResponsableId == id).ToListAsync()).ForEach(t => t.ResponsableId = null);
        (await _db.Trabajos.Where(t => t.ResponsableId == id).ToListAsync()).ForEach(t => t.ResponsableId = null);

        _db.Personas.Remove(p);
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        this.ToastSuccess($"{p.NombreCompleto} eliminado del equipo.");
        return Volver();
    }

    // ---------- Apoyo ----------

    private IActionResult Volver() => RedirectToPage("Index", new { vista = Opciones.Limpio(Vista) });

    private async Task ValidarAsync(PersonaInput input, int? id)
    {
        if (input.DepartamentoId is not null && !await _db.Departamentos.AnyAsync(d => d.Id == input.DepartamentoId))
        {
            input.DepartamentoId = null;
        }
        if (input.PuestoId is not null)
        {
            var puesto = await _db.Puestos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.PuestoId);
            if (puesto is null) input.PuestoId = null;
            else if (input.DepartamentoId is null) input.DepartamentoId = puesto.DepartamentoId;
            else if (puesto.DepartamentoId != input.DepartamentoId)
                ModelState.AddModelError(string.Empty, "El puesto elegido pertenece a otro departamento.");
        }
        var usuario = Opciones.Limpio(input.UsuarioId);
        if (usuario is not null)
        {
            if (!await _db.Users.AnyAsync(u => u.Id == usuario))
                ModelState.AddModelError(string.Empty, "El usuario elegido ya no existe.");
            else if (await _db.Personas.AnyAsync(p => p.UsuarioId == usuario && p.Id != id))
                ModelState.AddModelError(string.Empty, "Ese usuario ya está ligado a otra persona.");
        }
        if (!PersonaInput.Monedas.Contains(input.MonedaCosto))
        {
            input.MonedaCosto = "MXN";
        }
    }

    private async Task<PersonaModalVm> ModalAsync(Persona? p, PersonaInput input)
    {
        var ligados = await _db.Personas.Where(x => x.UsuarioId != null && x.Id != (p == null ? 0 : p.Id)).Select(x => x.UsuarioId!).ToListAsync();
        var responsabilidades = p is null ? [] : await _db.Departamentos.Where(d => d.ResponsableId == p.Id).Select(d => d.Nombre).ToListAsync();

        return new PersonaModalVm
        {
            Id = p?.Id,
            Input = input,
            Departamentos = await _db.Departamentos.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Nombre)
                .Select(d => new OpcionDepartamento(d.Id, d.Nombre, d.Color)).ToListAsync(),
            Puestos = await _db.Puestos.AsNoTracking().OrderBy(x => x.Nombre)
                .Select(x => new OpcionPuesto(x.Id, x.DepartamentoId, x.Nombre)).ToListAsync(),
            Usuarios = await _db.Users.AsNoTracking()
                .Where(u => !ligados.Contains(u.Id))
                .OrderBy(u => u.NombreCompleto == "" ? u.Email : u.NombreCompleto)
                .Select(u => new OpcionUsuarioLibre(u.Id, u.NombreCompleto == "" ? u.Email! : u.NombreCompleto, u.Email!))
                .ToListAsync(),
            PuedeGestionar = PuedeGestionar,
            VerCosto = VerCosto,
            EditarCosto = EditarCosto,
            Responsabilidades = responsabilidades,
            CreadoUtc = p?.CreadoUtc,
            CreadoPor = p?.CreadoPor,
            ModificadoUtc = p?.ModificadoUtc,
            ModificadoPor = p?.ModificadoPor,
            Vista = Vista
        };
    }

    private async Task CargarAsync()
    {
        var personas = await _db.Personas.AsNoTracking()
            .Select(p => new
            {
                Persona = p,
                Puesto = p.Puesto == null ? null : p.Puesto.Nombre,
                Usuario = p.Usuario == null ? null : p.Usuario.Email
            })
            .ToListAsync();

        Todas = personas
            .OrderBy(x => !x.Persona.Activa).ThenBy(x => x.Persona.Tipo).ThenBy(x => x.Persona.NombreCompleto)
            .Select(x => new Integrante(x.Persona, x.Puesto, x.Usuario)).ToList();

        var departamentos = await _db.Departamentos.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Nombre).ToListAsync();
        Areas = departamentos.Select(d => new Area(
                d,
                Todas.FirstOrDefault(i => i.Persona.Id == d.ResponsableId)?.Persona.NombreCompleto,
                Todas.Where(i => i.Persona.DepartamentoId == d.Id && i.Persona.Activa)
                    .OrderBy(i => i.Persona.Id != d.ResponsableId).ThenBy(i => i.Persona.Tipo).ThenBy(i => i.Persona.NombreCompleto)
                    .ToList()))
            .ToList();
        SinDepartamento = Todas.Where(i => i.Persona.DepartamentoId is null && i.Persona.Activa).ToList();

        var activas = Todas.Where(i => i.Persona.Activa).Select(i => i.Persona).ToList();
        Activas = activas.Count;
        Socios = activas.Count(p => p.Tipo == TipoPersona.Socio);
        Externos = activas.Count(p => p.Tipo == TipoPersona.Externo);
        HorasSemana = activas.Sum(p => p.HorasSemana ?? 0);
        if (VerCosto)
        {
            CostoMensual = activas.Where(p => p.CostoMensual is not null).GroupBy(p => p.MonedaCosto)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.CostoMensual!.Value));
        }
    }
}
