using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Pages.Organizacion;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Organizacion.Departamentos;

[Authorize(Policy = Permisos.Organizacion.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;

    public IndexModel(GasDbContext db) => _db = db;

    public bool PuedeGestionar => User.TienePermiso(Permisos.Organizacion.Gestionar);

    public sealed record PuestoFila(int Id, string Nombre, int Ocupantes);
    public sealed record Fila(Departamento Departamento, string? Responsable, int Personas, int Horas, List<PuestoFila> Puestos);

    public List<Fila> Departamentos { get; private set; } = [];
    public DepartamentoModalVm? ModalDepartamento { get; private set; }
    public PuestoModalVm? ModalPuesto { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    // ---------- Departamentos ----------

    public async Task<IActionResult> OnGetNuevoAsync()
    {
        if (!PuedeGestionar) return Forbid();
        var orden = await _db.Departamentos.Select(d => (int?)d.Orden).MaxAsync() + 1 ?? 0;
        return Partial("_DepartamentoModal", await ModalDepartamentoAsync(null, new DepartamentoInput { Orden = orden }));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        var d = await _db.Departamentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        return Partial("_DepartamentoModal", await ModalDepartamentoAsync(d, DepartamentoInput.Desde(d)));
    }

    public async Task<IActionResult> OnPostCrearAsync([Bind(Prefix = "Input")] DepartamentoInput input)
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarDepartamentoAsync(input, null);
        if (ModelState.IsValid)
        {
            var d = new Departamento();
            input.AplicarA(d);
            _db.Departamentos.Add(d);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"Departamento {d.Nombre} creado.");
            return RedirectToPage("Index");
        }
        await CargarAsync();
        ModalDepartamento = await ModalDepartamentoAsync(null, input);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id, [Bind(Prefix = "Input")] DepartamentoInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var d = await _db.Departamentos.FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        await ValidarDepartamentoAsync(input, id);
        if (ModelState.IsValid)
        {
            input.AplicarA(d);
            var cambios = _db.ChangeTracker.HasChanges();
            await _db.SaveChangesAsync();
            if (cambios) this.ToastSuccess($"Departamento {d.Nombre} actualizado.");
            else this.ToastInfo("No hubo cambios que guardar.");
            return RedirectToPage("Index");
        }
        await CargarAsync();
        ModalDepartamento = await ModalDepartamentoAsync(d, input);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var d = await _db.Departamentos.Include(x => x.Puestos).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        if (await _db.Personas.AnyAsync(p => p.DepartamentoId == id))
        {
            this.ToastWarning($"{d.Nombre} tiene personas asignadas. Muévelas a otro departamento antes de eliminarlo.");
            return RedirectToPage("Index");
        }
        (await _db.Trabajos.Where(t => t.DepartamentoId == id).ToListAsync()).ForEach(t => t.DepartamentoId = null);
        (await _db.Tareas.Where(t => t.DepartamentoId == id).ToListAsync()).ForEach(t => t.DepartamentoId = null);
        _db.Puestos.RemoveRange(d.Puestos);
        _db.Departamentos.Remove(d);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Departamento {d.Nombre} eliminado junto con sus puestos.");
        return RedirectToPage("Index");
    }

    // ---------- Puestos ----------

    public async Task<IActionResult> OnGetNuevoPuestoAsync(int? departamentoId)
    {
        if (!PuedeGestionar) return Forbid();
        return Partial("_PuestoModal", await ModalPuestoAsync(null, new PuestoInput { DepartamentoId = departamentoId }));
    }

    public async Task<IActionResult> OnGetEditarPuestoAsync(int id)
    {
        var p = await _db.Puestos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        return Partial("_PuestoModal", await ModalPuestoAsync(p, new PuestoInput { Nombre = p.Nombre, DepartamentoId = p.DepartamentoId, Descripcion = p.Descripcion }));
    }

    public async Task<IActionResult> OnPostCrearPuestoAsync([Bind(Prefix = "Puesto")] PuestoInput input)
    {
        if (!PuedeGestionar) return Forbid();
        await ValidarPuestoAsync(input, null);
        if (ModelState.IsValid)
        {
            var p = new Puesto { Nombre = input.Nombre.Trim(), DepartamentoId = input.DepartamentoId!.Value, Descripcion = Opciones.Limpio(input.Descripcion) };
            _db.Puestos.Add(p);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"Puesto {p.Nombre} creado.");
            return RedirectToPage("Index");
        }
        await CargarAsync();
        ModalPuesto = await ModalPuestoAsync(null, input);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarPuestoAsync(int id, [Bind(Prefix = "Puesto")] PuestoInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var p = await _db.Puestos.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        await ValidarPuestoAsync(input, id);
        if (input.DepartamentoId != p.DepartamentoId && await _db.Personas.AnyAsync(x => x.PuestoId == id))
        {
            ModelState.AddModelError(string.Empty, "Hay personas en este puesto; no se puede cambiar de departamento.");
        }
        if (ModelState.IsValid)
        {
            p.Nombre = input.Nombre.Trim();
            p.DepartamentoId = input.DepartamentoId!.Value;
            p.Descripcion = Opciones.Limpio(input.Descripcion);
            await _db.SaveChangesAsync();
            this.ToastSuccess($"Puesto {p.Nombre} actualizado.");
            return RedirectToPage("Index");
        }
        await CargarAsync();
        ModalPuesto = await ModalPuestoAsync(p, input);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarPuestoAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var p = await _db.Puestos.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        if (await _db.Personas.AnyAsync(x => x.PuestoId == id))
        {
            this.ToastWarning($"Hay personas en el puesto {p.Nombre}. Cámbialas de puesto antes de eliminarlo.");
            return RedirectToPage("Index");
        }
        _db.Puestos.Remove(p);
        await _db.SaveChangesAsync();
        this.ToastSuccess($"Puesto {p.Nombre} eliminado.");
        return RedirectToPage("Index");
    }

    // ---------- Apoyo ----------

    private async Task ValidarDepartamentoAsync(DepartamentoInput input, int? id)
    {
        var nombre = input.Nombre?.Trim() ?? "";
        if (nombre.Length > 0 && await _db.Departamentos.AnyAsync(d => d.Nombre == nombre && d.Id != id))
        {
            ModelState.AddModelError(string.Empty, $"Ya existe un departamento llamado {nombre}.");
        }
        if (input.ResponsableId is not null && !await _db.Personas.AnyAsync(p => p.Id == input.ResponsableId && p.Activa))
        {
            ModelState.AddModelError(string.Empty, "El responsable debe ser una persona activa del equipo.");
        }
    }

    private async Task ValidarPuestoAsync(PuestoInput input, int? id)
    {
        if (input.DepartamentoId is null || !await _db.Departamentos.AnyAsync(d => d.Id == input.DepartamentoId))
        {
            ModelState.AddModelError(string.Empty, "Elige el departamento del puesto.");
            return;
        }
        var nombre = input.Nombre?.Trim() ?? "";
        if (nombre.Length > 0 && await _db.Puestos.AnyAsync(p => p.DepartamentoId == input.DepartamentoId && p.Nombre == nombre && p.Id != id))
        {
            ModelState.AddModelError(string.Empty, $"Ese departamento ya tiene un puesto llamado {nombre}.");
        }
    }

    private async Task<DepartamentoModalVm> ModalDepartamentoAsync(Departamento? d, DepartamentoInput input) => new()
    {
        Id = d?.Id,
        Input = input,
        Personas = await _db.Personas.AsNoTracking().Where(p => p.Activa).OrderBy(p => p.NombreCompleto)
            .Select(p => new OpcionPersona(p.Id, p.NombreCompleto, p.DepartamentoId)).ToListAsync(),
        PuedeGestionar = PuedeGestionar,
        Integrantes = d is null ? 0 : await _db.Personas.CountAsync(p => p.DepartamentoId == d.Id),
        Puestos = d is null ? 0 : await _db.Puestos.CountAsync(p => p.DepartamentoId == d.Id),
        CreadoUtc = d?.CreadoUtc,
        CreadoPor = d?.CreadoPor,
        ModificadoUtc = d?.ModificadoUtc,
        ModificadoPor = d?.ModificadoPor
    };

    private async Task<PuestoModalVm> ModalPuestoAsync(Puesto? p, PuestoInput input) => new()
    {
        Id = p?.Id,
        Input = input,
        Departamentos = await _db.Departamentos.AsNoTracking().OrderBy(d => d.Orden).ThenBy(d => d.Nombre)
            .Select(d => new OpcionDepartamento(d.Id, d.Nombre, d.Color)).ToListAsync(),
        Ocupantes = p is null ? [] : await _db.Personas.Where(x => x.PuestoId == p.Id).Select(x => x.NombreCompleto).ToListAsync(),
        PuedeGestionar = PuedeGestionar
    };

    private async Task CargarAsync()
    {
        var departamentos = await _db.Departamentos.AsNoTracking()
            .OrderBy(d => d.Orden).ThenBy(d => d.Nombre)
            .Select(d => new
            {
                Departamento = d,
                Responsable = d.Responsable == null ? null : d.Responsable.NombreCompleto,
                Personas = d.Personas.Count(p => p.Activa),
                Horas = d.Personas.Where(p => p.Activa).Sum(p => p.HorasSemana ?? 0),
                Puestos = d.Puestos.OrderBy(p => p.Nombre)
                    .Select(p => new { p.Id, p.Nombre, Ocupantes = _db.Personas.Count(x => x.PuestoId == p.Id && x.Activa) }).ToList()
            })
            .ToListAsync();

        Departamentos = departamentos.Select(x => new Fila(x.Departamento, x.Responsable, x.Personas, x.Horas,
            x.Puestos.Select(p => new PuestoFila(p.Id, p.Nombre, p.Ocupantes)).ToList())).ToList();
    }
}
