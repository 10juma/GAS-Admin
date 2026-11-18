using System.Text.Json;
using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Auditoria;
using GAS.Admin.Services.Navegacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Administracion.Usuarios;

[Authorize(Policy = Permisos.Usuarios.Ver)]
public class IndexModel : PageModel
{
    private const string TempDataPassword = "GAS.PasswordTemporal";

    private readonly GasDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditoria _auditoria;
    private readonly ContadoresMenu _contadores;

    public IndexModel(
        GasDbContext db,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditoria auditoria,
        ContadoresMenu contadores)
    {
        _db = db;
        _userManager = userManager;
        _signInManager = signInManager;
        _auditoria = auditoria;
        _contadores = contadores;
    }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Estado { get; set; }

    [BindProperty]
    public UsuarioInput Input { get; set; } = new();

    public sealed record Fila(
        string Id, string Nombre, string Email, IReadOnlyList<string> Roles,
        bool Activo, bool Bloqueado, bool DebeCambiarPassword, DateTime? UltimoAccesoUtc);

    public List<Fila> Usuarios { get; private set; } = [];
    public int Total { get; private set; }
    public int Activos { get; private set; }
    public int Inactivos { get; private set; }
    public int Bloqueados { get; private set; }
    public bool PuedeGestionar => User.TienePermiso(Permisos.Usuarios.Gestionar);

    /// <summary>Modal que se abre al cargar (por errores de validación).</summary>
    public UsuarioModalVm? ModalUsuario { get; private set; }

    /// <summary>Modal con la contraseña temporal recién generada.</summary>
    public PasswordTemporalVm? ModalPassword { get; private set; }

    // ---------- Lista ----------

    public async Task OnGetAsync()
    {
        if (TempData[TempDataPassword] is string json)
        {
            ModalPassword = JsonSerializer.Deserialize<PasswordTemporalVm>(json);
        }
        await CargarListaAsync();
    }

    // ---------- Contenido de los modales ----------

    public async Task<IActionResult> OnGetNuevoAsync()
    {
        if (!PuedeGestionar) return Forbid();
        return Partial("_UsuarioModal", await ModalNuevoAsync(new UsuarioInput()));
    }

    public async Task<IActionResult> OnGetEditarAsync(string id)
    {
        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null) return NotFound();
        return Partial("_UsuarioModal", await ModalEdicionAsync(usuario, null));
    }

    // ---------- Acciones ----------

    public async Task<IActionResult> OnPostCrearAsync()
    {
        if (!PuedeGestionar) return Forbid();

        var roles = await RolesHelper.ObtenerAsync(_db);
        Input.Roles = Input.Roles.Where(roles.Select(r => r.Nombre).Contains).Distinct().ToList();
        if (Input.Roles.Contains(RolesBase.Admin) && !EsAdmin)
        {
            ModelState.AddModelError(string.Empty, "Solo un administrador puede crear otro administrador.");
            Input.Roles.Remove(RolesBase.Admin);
        }
        if (Input.Roles.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Asigna al menos un rol al usuario.");
        }

        if (ModelState.IsValid)
        {
            var email = Input.Email.Trim();
            var usuario = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                NombreCompleto = Input.NombreCompleto.Trim(),
                Activo = true,
                DebeCambiarPassword = true,
                FechaAltaUtc = DateTime.UtcNow
            };

            var password = GeneradorPassword.Generar();
            var resultado = await _userManager.CreateAsync(usuario, password);
            if (resultado.Succeeded)
            {
                await _userManager.AddToRolesAsync(usuario, Input.Roles);
                await _auditoria.RegistrarAsync(
                    AccionesAuditoria.UsuarioCreado,
                    detalle: $"{usuario.Email} · Roles: {string.Join(", ", Input.Roles)}",
                    entidad: "Usuario", entidadId: usuario.Id);
                _contadores.Invalidar();

                GuardarPassword(new PasswordTemporalVm(usuario.NombreVisible, usuario.Email!, password, EsNuevo: true));
                this.ToastSuccess($"Usuario {usuario.NombreVisible} creado.");
                return VolverALista();
            }
            IdentityErrores.AgregarA(ModelState, resultado);
        }

        // Reabre el modal con lo capturado.
        await CargarListaAsync();
        ModalUsuario = await ModalNuevoAsync(Input);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(string id)
    {
        if (!PuedeGestionar) return Forbid();
        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null) return NotFound();

        var esActual = usuario.Id == User.UsuarioId();
        var validos = (await RolesHelper.ObtenerAsync(_db)).Select(r => r.Nombre).ToHashSet();
        var nuevos = Input.Roles.Where(validos.Contains).Distinct().ToList();
        var actuales = (await _userManager.GetRolesAsync(usuario)).ToList();

        if (nuevos.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Asigna al menos un rol al usuario.");
        }

        if (!EsAdmin && (actuales.Contains(RolesBase.Admin) || nuevos.Contains(RolesBase.Admin)))
        {
            this.ToastWarning("Solo un administrador puede dar el rol Admin o modificar a un administrador.");
            return VolverALista();
        }

        var quitaAdmin = actuales.Contains(RolesBase.Admin) && !nuevos.Contains(RolesBase.Admin);
        if (quitaAdmin && esActual)
        {
            ModelState.AddModelError(string.Empty, "No puedes quitarte el rol Admin a ti mismo.");
        }
        else if (quitaAdmin && usuario.Activo && !await HayOtroAdminActivoAsync(usuario))
        {
            ModelState.AddModelError(string.Empty, "Debe existir al menos un administrador activo.");
        }

        var cambios = new List<string>();
        if (ModelState.IsValid)
        {
            var nombre = Input.NombreCompleto.Trim();
            if (nombre != usuario.NombreCompleto)
            {
                usuario.NombreCompleto = nombre;
                cambios.Add("nombre");
            }

            var email = Input.Email.Trim();
            if (!string.Equals(email, usuario.Email, StringComparison.OrdinalIgnoreCase))
            {
                var r1 = await _userManager.SetEmailAsync(usuario, email);
                var r2 = r1.Succeeded ? await _userManager.SetUserNameAsync(usuario, email) : r1;
                if (r2.Succeeded) cambios.Add($"correo a {email}");
                else IdentityErrores.AgregarA(ModelState, r2);
            }
        }

        if (ModelState.IsValid)
        {
            var resultado = await _userManager.UpdateAsync(usuario);
            if (!resultado.Succeeded) IdentityErrores.AgregarA(ModelState, resultado);
        }

        if (!ModelState.IsValid)
        {
            Input.Roles = nuevos;
            await CargarListaAsync();
            ModalUsuario = await ModalEdicionAsync(usuario, Input);
            return Page();
        }

        var agregar = nuevos.Except(actuales).ToList();
        var quitar = actuales.Except(nuevos).ToList();
        if (quitar.Count > 0) await _userManager.RemoveFromRolesAsync(usuario, quitar);
        if (agregar.Count > 0) await _userManager.AddToRolesAsync(usuario, agregar);
        if (agregar.Count > 0) cambios.Add("+" + string.Join(", +", agregar));
        if (quitar.Count > 0) cambios.Add("−" + string.Join(", −", quitar));

        if (cambios.Count == 0)
        {
            this.ToastInfo("No hubo cambios que guardar.");
            return VolverALista();
        }

        await _auditoria.RegistrarAsync(
            AccionesAuditoria.UsuarioActualizado,
            detalle: $"{usuario.Email} · {string.Join("; ", cambios)}",
            entidad: "Usuario", entidadId: usuario.Id);

        this.ToastSuccess($"Cambios de {usuario.NombreVisible} guardados.");
        if (esActual)
        {
            await _signInManager.RefreshSignInAsync(usuario);
        }
        else if (agregar.Count > 0 || quitar.Count > 0)
        {
            this.ToastInfo("Los cambios de rol se aplican en su sesión en unos minutos.");
        }
        return VolverALista();
    }

    public async Task<IActionResult> OnPostRestablecerAsync(string id)
    {
        if (!PuedeGestionar) return Forbid();
        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null) return NotFound();
        if (await ProtegidoAsync(usuario)) return VolverALista();

        if (usuario.Id == User.UsuarioId())
        {
            this.ToastWarning("Para tu propia cuenta usa \"Cambiar contraseña\".");
            return VolverALista();
        }

        var password = GeneradorPassword.Generar();
        var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await _userManager.ResetPasswordAsync(usuario, token, password);
        if (!resultado.Succeeded)
        {
            foreach (var e in resultado.Errors) this.ToastError(IdentityErrores.Traducir(e));
            return VolverALista();
        }

        usuario.DebeCambiarPassword = true;
        await _userManager.UpdateAsync(usuario);
        await _userManager.SetLockoutEndDateAsync(usuario, null);
        await _userManager.ResetAccessFailedCountAsync(usuario);

        await _auditoria.RegistrarAsync(
            AccionesAuditoria.PasswordRestablecida, detalle: usuario.Email,
            entidad: "Usuario", entidadId: usuario.Id);

        GuardarPassword(new PasswordTemporalVm(usuario.NombreVisible, usuario.Email!, password, EsNuevo: false));
        this.ToastSuccess("Contraseña restablecida. Sus sesiones abiertas se cerrarán.");
        return VolverALista();
    }

    public async Task<IActionResult> OnPostDesactivarAsync(string id)
    {
        if (!PuedeGestionar) return Forbid();
        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null) return NotFound();
        if (await ProtegidoAsync(usuario)) return VolverALista();

        if (usuario.Id == User.UsuarioId())
        {
            this.ToastWarning("No puedes desactivar tu propia cuenta.");
            return VolverALista();
        }
        if (await _userManager.IsInRoleAsync(usuario, RolesBase.Admin) && !await HayOtroAdminActivoAsync(usuario))
        {
            this.ToastWarning("Debe existir al menos un administrador activo.");
            return VolverALista();
        }

        usuario.Activo = false;
        await _userManager.UpdateAsync(usuario);
        await _userManager.UpdateSecurityStampAsync(usuario);
        await _auditoria.RegistrarAsync(
            AccionesAuditoria.UsuarioDesactivado, detalle: usuario.Email,
            entidad: "Usuario", entidadId: usuario.Id);

        this.ToastSuccess($"{usuario.NombreVisible} fue desactivado. Ya no podrá iniciar sesión.");
        return VolverALista();
    }

    public async Task<IActionResult> OnPostActivarAsync(string id)
    {
        if (!PuedeGestionar) return Forbid();
        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null) return NotFound();
        if (await ProtegidoAsync(usuario)) return VolverALista();

        usuario.Activo = true;
        await _userManager.UpdateAsync(usuario);
        await _auditoria.RegistrarAsync(
            AccionesAuditoria.UsuarioActivado, detalle: usuario.Email,
            entidad: "Usuario", entidadId: usuario.Id);

        this.ToastSuccess($"{usuario.NombreVisible} fue activado.");
        return VolverALista();
    }

    public async Task<IActionResult> OnPostDesbloquearAsync(string id)
    {
        if (!PuedeGestionar) return Forbid();
        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null) return NotFound();
        if (await ProtegidoAsync(usuario)) return VolverALista();

        await _userManager.SetLockoutEndDateAsync(usuario, null);
        await _userManager.ResetAccessFailedCountAsync(usuario);
        await _auditoria.RegistrarAsync(
            AccionesAuditoria.UsuarioDesbloqueado, detalle: usuario.Email,
            entidad: "Usuario", entidadId: usuario.Id);

        this.ToastSuccess($"{usuario.NombreVisible} fue desbloqueado.");
        return VolverALista();
    }

    public async Task<IActionResult> OnPostEliminarAsync(string id)
    {
        if (!PuedeGestionar) return Forbid();
        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is null) return NotFound();
        if (await ProtegidoAsync(usuario)) return VolverALista();

        if (usuario.Id == User.UsuarioId())
        {
            this.ToastWarning("No puedes eliminar tu propia cuenta.");
            return VolverALista();
        }
        if (await _userManager.IsInRoleAsync(usuario, RolesBase.Admin) && !await HayOtroAdminActivoAsync(usuario))
        {
            this.ToastWarning("Debe existir al menos un administrador activo.");
            return VolverALista();
        }

        var email = usuario.Email;
        var resultado = await _userManager.DeleteAsync(usuario);
        if (!resultado.Succeeded)
        {
            foreach (var e in resultado.Errors) this.ToastError(IdentityErrores.Traducir(e));
            return VolverALista();
        }

        await _auditoria.RegistrarAsync(
            AccionesAuditoria.UsuarioEliminado, detalle: email,
            entidad: "Usuario", entidadId: id);
        _contadores.Invalidar();

        this.ToastSuccess($"Usuario {email} eliminado.");
        return VolverALista();
    }

    // ---------- Apoyo ----------

    /// <summary>Solo un Admin puede dar o quitar el rol Admin, o actuar sobre un usuario Admin.</summary>
    private bool EsAdmin => User.IsInRole(RolesBase.Admin);

    private async Task<bool> ProtegidoAsync(ApplicationUser usuario)
    {
        if (EsAdmin || !await _userManager.IsInRoleAsync(usuario, RolesBase.Admin)) return false;
        this.ToastWarning("Solo un administrador puede modificar a otro administrador.");
        return true;
    }

    private IActionResult VolverALista() =>
        RedirectToPage("Index", new
        {
            estado = string.IsNullOrEmpty(Estado) || Estado == "todos" ? null : Estado,
            q = string.IsNullOrWhiteSpace(Q) ? null : Q
        });

    private void GuardarPassword(PasswordTemporalVm vm) =>
        TempData[TempDataPassword] = JsonSerializer.Serialize(vm);

    private async Task<UsuarioModalVm> ModalNuevoAsync(UsuarioInput input) => new()
    {
        Input = input,
        RolesDisponibles = await RolesHelper.ObtenerAsync(_db),
        PuedeGestionar = PuedeGestionar,
        Estado = Estado,
        Q = Q
    };

    private async Task<UsuarioModalVm> ModalEdicionAsync(ApplicationUser usuario, UsuarioInput? input) => new()
    {
        Id = usuario.Id,
        Input = input ?? new UsuarioInput
        {
            NombreCompleto = usuario.NombreCompleto,
            Email = usuario.Email ?? string.Empty,
            Roles = (await _userManager.GetRolesAsync(usuario)).ToList()
        },
        RolesDisponibles = await RolesHelper.ObtenerAsync(_db),
        PuedeGestionar = PuedeGestionar,
        EsUsuarioActual = usuario.Id == User.UsuarioId(),
        Nombre = usuario.NombreVisible,
        Email = usuario.Email ?? string.Empty,
        Activo = usuario.Activo,
        Bloqueado = usuario.LockoutEnd > DateTimeOffset.UtcNow,
        BloqueadoHastaUtc = usuario.LockoutEnd?.UtcDateTime,
        DebeCambiarPassword = usuario.DebeCambiarPassword,
        FechaAltaUtc = usuario.FechaAltaUtc,
        UltimoAccesoUtc = usuario.UltimoAccesoUtc,
        Estado = Estado,
        Q = Q
    };

    private async Task<bool> HayOtroAdminActivoAsync(ApplicationUser usuario)
    {
        var admins = await _userManager.GetUsersInRoleAsync(RolesBase.Admin);
        return admins.Any(a => a.Activo && a.Id != usuario.Id);
    }

    private async Task CargarListaAsync()
    {
        var ahoraUtc = DateTimeOffset.UtcNow;
        Total = await _db.Users.CountAsync();
        Activos = await _db.Users.CountAsync(u => u.Activo);
        Inactivos = Total - Activos;
        Bloqueados = await _db.Users.CountAsync(u => u.LockoutEnd > ahoraUtc);

        var query = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            query = query.Where(u => u.NombreCompleto.Contains(q) || u.Email!.Contains(q));
        }

        query = Estado switch
        {
            "activos" => query.Where(u => u.Activo),
            "inactivos" => query.Where(u => !u.Activo),
            "bloqueados" => query.Where(u => u.LockoutEnd > ahoraUtc),
            _ => query
        };

        var usuarios = await query
            .OrderBy(u => u.NombreCompleto == "" ? u.Email : u.NombreCompleto)
            .ToListAsync();

        var ids = usuarios.Select(u => u.Id).ToList();
        var roles = await (
                from ur in _db.UserRoles
                join r in _db.Roles on ur.RoleId equals r.Id
                where ids.Contains(ur.UserId)
                select new { ur.UserId, r.Name })
            .ToListAsync();
        var rolesPorUsuario = roles
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name!)
                .OrderBy(n => n == RolesBase.Admin ? 0 : 1).ThenBy(n => n).ToList());

        Usuarios = usuarios.Select(u => new Fila(
                u.Id, u.NombreVisible, u.Email ?? "",
                rolesPorUsuario.GetValueOrDefault(u.Id, []),
                u.Activo, u.LockoutEnd > ahoraUtc, u.DebeCambiarPassword, u.UltimoAccesoUtc))
            .ToList();
    }
}
