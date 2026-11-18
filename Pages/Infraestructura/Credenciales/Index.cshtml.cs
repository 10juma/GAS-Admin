using System.ComponentModel.DataAnnotations;
using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Infraestructura;
using GAS.Admin.Services.Navegacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura.Credenciales;

public class CredencialInput
{
    [Required(ErrorMessage = "Ingresa un nombre para la credencial.")]
    [StringLength(150, ErrorMessage = "El nombre no puede exceder 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;
    public TipoCredencial Tipo { get; set; } = TipoCredencial.ApiKey;
    public int? ProductoId { get; set; }
    public int? AmbienteId { get; set; }
    public int? ServicioExternoId { get; set; }
    [Required(ErrorMessage = "Indica dónde está guardada (gestor de secretos).")]
    [StringLength(300, ErrorMessage = "La referencia no puede exceder 300 caracteres.")]
    public string Referencia { get; set; } = string.Empty;
    public string? ResponsableId { get; set; }
    public DateOnly? UltimaRotacion { get; set; }
    [Range(1, 3650, ErrorMessage = "La rotación debe ser entre 1 y 3650 días.")]
    public int? RotacionDias { get; set; }
    public DateOnly? Vence { get; set; }
    public EstadoCredencial Estado { get; set; } = EstadoCredencial.Activa;
    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }
}

public class CredencialModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public CredencialInput Input { get; init; } = new();
    public List<Opcion> Productos { get; init; } = [];
    public List<Opcion> Ambientes { get; init; } = [];
    public List<Opcion> Servicios { get; init; } = [];
    public List<OpcionUsuario> Responsables { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public DateTime? CreadoUtc { get; init; }
    public string? CreadoPor { get; init; }
    public DateTime? ModificadoUtc { get; init; }
    public string? ModificadoPor { get; init; }
}

[Authorize(Policy = Permisos.Infraestructura.Ver)]
public class IndexModel : PageModel
{
    private const int Aviso = 30;
    private readonly GasDbContext _db;
    private readonly ContadoresMenu _contadores;

    public IndexModel(GasDbContext db, ContadoresMenu contadores)
    {
        _db = db;
        _contadores = contadores;
    }

    /// <summary>activas (por defecto), atender o revocadas.</summary>
    [BindProperty(SupportsGet = true)] public string? Vista { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Infraestructura.Gestionar);
    public DateOnly Hoy { get; } = ServicioInfraestructura.Hoy;

    public sealed record Fila(Credencial C, string? Producto, string? Ambiente, string? Servicio, string? Responsable);

    public List<Fila> Credenciales { get; private set; } = [];
    public int Activas { get; private set; }
    public int PorAtender { get; private set; }
    public int Revocadas { get; private set; }
    public CredencialModalVm? Modal { get; private set; }

    public bool Atender(Credencial c) => c.Estado == EstadoCredencial.Activa && c.ProximaAccion is { } f && f <= Hoy.AddDays(Aviso);

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnGetNuevoAsync()
    {
        if (!PuedeGestionar) return Forbid();
        return Partial("_CredencialModal", await ModalAsync(null, new CredencialInput { ResponsableId = User.UsuarioId(), UltimaRotacion = Hoy, RotacionDias = 180 }));
    }

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        var c = await _db.Credenciales.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        return Partial("_CredencialModal", await ModalAsync(c, new CredencialInput
        {
            Nombre = c.Nombre, Tipo = c.Tipo, ProductoId = c.ProductoId, AmbienteId = c.AmbienteId, ServicioExternoId = c.ServicioExternoId,
            Referencia = c.Referencia, ResponsableId = c.ResponsableId, UltimaRotacion = c.UltimaRotacion, RotacionDias = c.RotacionDias,
            Vence = c.Vence, Estado = c.Estado, Notas = c.Notas
        }));
    }

    public async Task<IActionResult> OnPostGuardarAsync(int? id, [Bind(Prefix = "Input")] CredencialInput input)
    {
        if (!PuedeGestionar) return Forbid();
        var c = id is null ? new Credencial() : await _db.Credenciales.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        if (DetectorSecretos.PareceSecreto(input.Referencia) || DetectorSecretos.PareceSecreto(input.Notas))
        {
            ModelState.AddModelError(string.Empty, "Eso parece el secreto mismo. Guarda solo dónde está (por ejemplo: 1Password › GAS › Stripe live).");
            // No volver a mostrar el posible secreto.
            input.Referencia = string.Empty;
            input.Notas = null;
        }
        if (input.AmbienteId is not null)
        {
            var prod = await _db.Ambientes.Where(a => a.Id == input.AmbienteId).Select(a => (int?)a.ProductoId).FirstOrDefaultAsync();
            if (prod is null) input.AmbienteId = null; else input.ProductoId = prod;
        }
        if (input.ProductoId is not null && !await _db.Productos.AnyAsync(p => p.Id == input.ProductoId)) input.ProductoId = null;
        if (input.ServicioExternoId is not null && !await _db.ServiciosExternos.AnyAsync(s => s.Id == input.ServicioExternoId)) input.ServicioExternoId = null;
        if (input.ResponsableId is not null && !await _db.Users.AnyAsync(u => u.Id == input.ResponsableId)) input.ResponsableId = null;
        if (input.UltimaRotacion > Hoy) ModelState.AddModelError(string.Empty, "La última rotación no puede ser futura.");

        if (!ModelState.IsValid)
        {
            await CargarAsync();
            Modal = await ModalAsync(id is null ? null : c, input);
            return Page();
        }
        c.Nombre = input.Nombre.Trim();
        c.Tipo = input.Tipo;
        c.ProductoId = input.ProductoId;
        c.AmbienteId = input.AmbienteId;
        c.ServicioExternoId = input.ServicioExternoId;
        c.Referencia = input.Referencia.Trim();
        c.ResponsableId = input.ResponsableId;
        c.UltimaRotacion = input.UltimaRotacion;
        c.RotacionDias = input.RotacionDias;
        c.Vence = input.Vence;
        c.Estado = input.Estado;
        c.Notas = Opciones.Limpio(input.Notas);
        if (id is null) _db.Credenciales.Add(c);
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        this.ToastSuccess(id is null ? $"Credencial {c.Nombre} registrada." : $"Credencial {c.Nombre} actualizada.");
        return RedirectToPage("Index", new { vista = Vista });
    }

    public async Task<IActionResult> OnPostRotadaAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await _db.Credenciales.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        c.UltimaRotacion = Hoy;
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        this.ToastSuccess(c.ProximaRotacion is null ? $"{c.Nombre} marcada como rotada hoy." : $"{c.Nombre} rotada. Próxima rotación: {c.ProximaRotacion:dd/MM/yyyy}.");
        return RedirectToPage("Index", new { vista = Vista });
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!PuedeGestionar) return Forbid();
        var c = await _db.Credenciales.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        _db.Credenciales.Remove(c);
        await _db.SaveChangesAsync();
        _contadores.Invalidar();
        this.ToastSuccess($"Credencial {c.Nombre} eliminada del inventario.");
        return RedirectToPage("Index");
    }

    private async Task<CredencialModalVm> ModalAsync(Credencial? c, CredencialInput input) => new()
    {
        Id = c?.Id,
        Input = input,
        Productos = await Listas.ProductosAsync(_db),
        Ambientes = await Listas.AmbientesAsync(_db),
        Servicios = await Listas.ServiciosAsync(_db),
        Responsables = await Listas.ResponsablesAsync(_db, input.ResponsableId),
        PuedeGestionar = PuedeGestionar,
        CreadoUtc = c?.CreadoUtc,
        CreadoPor = c?.CreadoPor,
        ModificadoUtc = c?.ModificadoUtc,
        ModificadoPor = c?.ModificadoPor
    };

    private async Task CargarAsync()
    {
        var todas = await _db.Credenciales.AsNoTracking()
            .Select(c => new Fila(c,
                c.Producto == null ? null : c.Producto.Nombre,
                c.Ambiente == null ? null : c.Ambiente.Nombre,
                c.ServicioExterno == null ? null : c.ServicioExterno.Nombre,
                c.Responsable == null ? null : (c.Responsable.NombreCompleto == "" ? c.Responsable.Email : c.Responsable.NombreCompleto)))
            .ToListAsync();
        Activas = todas.Count(f => f.C.Estado == EstadoCredencial.Activa);
        Revocadas = todas.Count - Activas;
        PorAtender = todas.Count(f => Atender(f.C));
        IEnumerable<Fila> q = Vista switch
        {
            "atender" => todas.Where(f => Atender(f.C)),
            "revocadas" => todas.Where(f => f.C.Estado == EstadoCredencial.Revocada),
            _ => todas.Where(f => f.C.Estado == EstadoCredencial.Activa)
        };
        Credenciales = q.OrderBy(f => f.C.ProximaAccion ?? DateOnly.MaxValue).ThenBy(f => f.Producto ?? "~").ThenBy(f => f.C.Nombre).ToList();
    }
}
