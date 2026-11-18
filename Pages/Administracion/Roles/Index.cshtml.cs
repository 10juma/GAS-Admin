using System.Security.Claims;
using System.Text.RegularExpressions;
using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Pages.Administracion.Usuarios;
using GAS.Admin.Security;
using GAS.Admin.Services.Auditoria;
using GAS.Admin.Services.Navegacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Administracion.Roles;

[Authorize(Policy = Permisos.Roles.Ver)]
public partial class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditoria _auditoria;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, RoleManager<IdentityRole> roleManager, IAuditoria auditoria, ContadoresMenu contadores)
    {
        _db = db;
        _roleManager = roleManager;
        _auditoria = auditoria;
        _contadores = contadores;
    }

    public sealed record Fila(string Id, string Nombre, string Descripcion, int Usuarios, int Permisos, bool EsBase);

    public List<Fila> Roles { get; private set; } = [];
    public bool PuedeGestionar => User.TienePermiso(Permisos.Roles.Gestionar);

    /// <summary>Modal de alta que se reabre si hubo errores.</summary>
    public RolNuevoVm? ModalNuevo { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    // ---------- Contenido de los modales ----------

    public IActionResult OnGetNuevo()
    {
        if (!PuedeGestionar) return Forbid();
        return Partial("_RolNuevoModal", new RolNuevoVm(string.Empty));
    }

    public async Task<IActionResult> OnGetEditarAsync(string id)
    {
        var vm = await ModalAsync(id);
        return vm is null ? NotFound() : Partial("_RolModal", vm);
    }

    // ---------- Acciones ----------

    public async Task<IActionResult> OnPostCrearAsync(string? nombre)
    {
        if (!PuedeGestionar) return Forbid();

        nombre = (nombre ?? string.Empty).Trim();
        if (nombre.Length is < 2 or > 50)
        {
            ModelState.AddModelError(string.Empty, "El nombre debe tener entre 2 y 50 caracteres.");
        }
        else if (!NombreValido().IsMatch(nombre))
        {
            ModelState.AddModelError(string.Empty, "El nombre solo puede tener letras, números y espacios.");
        }

        if (ModelState.IsValid)
        {
            var resultado = await _roleManager.CreateAsync(new IdentityRole(nombre));
            if (resultado.Succeeded)
            {
                var rol = await _roleManager.FindByNameAsync(nombre);
                await _auditoria.RegistrarAsync(AccionesAuditoria.RolCreado, detalle: nombre, entidad: "Rol", entidadId: rol?.Id);
                _contadores.Invalidar();

                this.ToastSuccess($"Rol {nombre} creado. Ahora asígnale permisos.");
                // Al volver a la lista se abre el modal de permisos del rol nuevo.
                TempData["GAS.AbrirModal"] = Url.Page("Index", "Editar", new { id = rol!.Id });
                return RedirectToPage("Index");
            }
            IdentityErrores.AgregarA(ModelState, resultado);
        }

        await CargarAsync();
        ModalNuevo = new RolNuevoVm(nombre);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(string id, List<string> seleccionados)
    {
        if (!PuedeGestionar) return Forbid();
        var vm = await ModalAsync(id);
        if (vm is null) return NotFound();

        if (vm.EsAdmin)
        {
            this.ToastInfo("El rol Admin siempre tiene acceso total.");
            return RedirectToPage("Index");
        }

        var nuevos = seleccionados.Where(Permisos.Todos.Contains).ToHashSet();

        // Nadie puede otorgar permisos que no tiene (evita escalar privilegios con Roles.Gestionar).
        if (!User.IsInRole(RolesBase.Admin))
        {
            var actuales = (await _roleManager.GetClaimsAsync((await _roleManager.FindByIdAsync(id))!))
                .Where(c => c.Type == Permisos.ClaimType).Select(c => c.Value).ToHashSet();
            var ajenos = nuevos.Where(p => !actuales.Contains(p) && !User.TienePermiso(p)).ToList();
            if (ajenos.Count > 0)
            {
                this.ToastWarning("No puedes otorgar permisos que tú no tienes. Pídelo a un administrador.");
                return RedirectToPage("Index");
            }
        }

        // "Gestionar" implica "Ver" en el mismo módulo.
        foreach (var modulo in Permisos.Modulos)
        {
            var ver = modulo.Permisos.FirstOrDefault(p => p.Nombre == "Ver");
            if (ver is not null && modulo.Permisos.Any(p => p.Nombre == "Gestionar" && nuevos.Contains(p.Valor)))
            {
                nuevos.Add(ver.Valor);
            }
        }

        var agregar = nuevos.Except(vm.Asignados).ToList();
        var quitar = vm.Asignados.Except(nuevos).ToList();
        if (agregar.Count == 0 && quitar.Count == 0)
        {
            this.ToastInfo("No hubo cambios que guardar.");
            return RedirectToPage("Index");
        }

        var rol = (await _roleManager.FindByIdAsync(id))!;
        foreach (var permiso in quitar)
        {
            await _roleManager.RemoveClaimAsync(rol, new Claim(Permisos.ClaimType, permiso));
        }
        foreach (var permiso in agregar)
        {
            await _roleManager.AddClaimAsync(rol, new Claim(Permisos.ClaimType, permiso));
        }

        var detalle = new List<string>();
        if (agregar.Count > 0) detalle.Add("+" + string.Join(", +", agregar.Order()));
        if (quitar.Count > 0) detalle.Add("−" + string.Join(", −", quitar.Order()));
        await _auditoria.RegistrarAsync(
            AccionesAuditoria.PermisosActualizados,
            detalle: $"{rol.Name} · {string.Join("; ", detalle)}",
            entidad: "Rol", entidadId: rol.Id);

        this.ToastSuccess($"Permisos de {rol.Name} actualizados.");
        if (vm.Usuarios > 0)
        {
            this.ToastInfo("Los usuarios con este rol verán los cambios en unos minutos o al volver a iniciar sesión.");
        }
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostEliminarAsync(string id)
    {
        if (!PuedeGestionar) return Forbid();
        var vm = await ModalAsync(id);
        if (vm is null) return NotFound();

        if (!vm.PuedeEliminar)
        {
            this.ToastWarning("Solo se pueden eliminar roles personalizados sin usuarios asignados.");
            return RedirectToPage("Index");
        }

        var rol = (await _roleManager.FindByIdAsync(id))!;
        var resultado = await _roleManager.DeleteAsync(rol);
        if (!resultado.Succeeded)
        {
            foreach (var e in resultado.Errors) this.ToastError(IdentityErrores.Traducir(e));
            return RedirectToPage("Index");
        }

        await _auditoria.RegistrarAsync(AccionesAuditoria.RolEliminado, detalle: vm.Nombre, entidad: "Rol", entidadId: id);
        _contadores.Invalidar();
        this.ToastSuccess($"Rol {vm.Nombre} eliminado.");
        return RedirectToPage("Index");
    }

    // ---------- Apoyo ----------

    [GeneratedRegex(@"^[\p{L}\p{N} ]+$")]
    private static partial Regex NombreValido();

    private async Task<RolModalVm?> ModalAsync(string id)
    {
        var rol = await _roleManager.FindByIdAsync(id);
        if (rol is null) return null;

        return new RolModalVm
        {
            Id = rol.Id,
            Nombre = rol.Name ?? string.Empty,
            EsAdmin = rol.Name == RolesBase.Admin,
            EsBase = RolesBase.EsBase(rol.Name),
            PuedeGestionar = PuedeGestionar,
            Usuarios = await _db.UserRoles.CountAsync(ur => ur.RoleId == rol.Id),
            Asignados = (await _roleManager.GetClaimsAsync(rol))
                .Where(c => c.Type == Permisos.ClaimType)
                .Select(c => c.Value)
                .ToHashSet()
        };
    }

    private async Task CargarAsync()
    {
        var roles = await _db.Roles.AsNoTracking()
            .Select(r => new
            {
                r.Id,
                r.Name,
                Usuarios = _db.UserRoles.Count(ur => ur.RoleId == r.Id),
                Permisos = _db.RoleClaims.Count(c => c.RoleId == r.Id && c.ClaimType == Security.Permisos.ClaimType)
            })
            .ToListAsync();

        Roles = roles
            .OrderBy(r => r.Name == RolesBase.Admin ? 0 : RolesBase.EsBase(r.Name) ? 1 : 2)
            .ThenBy(r => r.Name)
            .Select(r => new Fila(r.Id, r.Name!, RolesHelper.Describir(r.Name!, r.Permisos), r.Usuarios,
                r.Name == RolesBase.Admin ? Security.Permisos.Todos.Count : r.Permisos, RolesBase.EsBase(r.Name)))
            .ToList();
    }
}
