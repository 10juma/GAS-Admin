using System.ComponentModel.DataAnnotations;
using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Catalogos;
using GAS.Admin.Services.Despliegues;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura.Servidores;

public class ServidorInput
{
    [Required(ErrorMessage = "Ingresa un nombre para el servidor.")]
    [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;
    [StringLength(253, ErrorMessage = "La dirección no puede exceder 253 caracteres.")]
    public string? Host { get; set; }
    [Range(1, 65535, ErrorMessage = "El puerto debe estar entre 1 y 65535.")]
    public int Puerto { get; set; } = 22;
    [StringLength(2000, ErrorMessage = "Las notas no pueden exceder 2000 caracteres.")]
    public string? Notas { get; set; }
}

public class ServidorModalVm
{
    public int? Id { get; init; }
    public bool EsNuevo => Id is null;
    public ServidorInput Input { get; init; } = new();
    public Servidor? Servidor { get; init; }
    public List<string> Ambientes { get; init; } = [];
    public string? Volver { get; init; }
}

public class PrepararModalVm
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? Host { get; init; }
    public bool YaPreparado { get; init; }
    public ResultadoServidor? Resultado { get; init; }
}

[Authorize(Policy = Permisos.Infraestructura.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioServidores _servidores;

    public IndexModel(GasDbContext db, ServicioServidores servidores)
    {
        _db = db;
        _servidores = servidores;
    }

    /// <summary>Servidores, llaves y despliegues: solo Admin (manejan credenciales de producción).</summary>
    public bool EsAdmin => User.IsInRole(RolesBase.Admin);

    public sealed record Fila(Servidor Servidor, string? Host, int Ambientes);

    public List<Fila> Servidores { get; private set; } = [];
    public List<EjecucionDespliegue> Ejecuciones { get; private set; } = [];
    public Dictionary<int, string> NombresAmbientes { get; private set; } = [];
    public ServidorModalVm? Modal { get; private set; }
    public PrepararModalVm? ModalPreparar { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    public IActionResult OnGetNuevo()
    {
        if (!EsAdmin) return Forbid();
        return Partial("_ServidorModal", new ServidorModalVm());
    }

    public async Task<IActionResult> OnGetEditarAsync(int id)
    {
        if (!EsAdmin) return Forbid();
        var s = await _db.Servidores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        return Partial("_ServidorModal", await ModalAsync(s, new ServidorInput { Nombre = s.Nombre, Host = _servidores.Host(s.Id), Puerto = s.Puerto, Notas = s.Notas }));
    }

    public async Task<IActionResult> OnPostCrearAsync([Bind(Prefix = "Input")] ServidorInput input)
    {
        if (!EsAdmin) return Forbid();
        await ValidarAsync(input, null);
        if (ModelState.IsValid)
        {
            var s = new Servidor { Nombre = input.Nombre.Trim(), Puerto = input.Puerto, Notas = Opciones.Limpio(input.Notas) };
            _db.Servidores.Add(s);
            await _db.SaveChangesAsync();
            if (await _servidores.GuardarHostAsync(s.Id, input.Host!, s.Nombre) is { } error) this.ToastWarning(error);
            this.ToastSuccess($"Servidor {s.Nombre} registrado. Ahora usa Preparar para que GAS-Admin pueda entrar.");
            return RedirectToPage("Index");
        }
        await CargarAsync();
        Modal = new ServidorModalVm { Input = input };
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync(int id, [Bind(Prefix = "Input")] ServidorInput input)
    {
        if (!EsAdmin) return Forbid();
        var s = await _db.Servidores.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        await ValidarAsync(input, id);
        if (ModelState.IsValid)
        {
            var hostAnterior = _servidores.Host(s.Id);
            var cambioDestino = !string.Equals(hostAnterior, input.Host!.Trim(), StringComparison.OrdinalIgnoreCase) || s.Puerto != input.Puerto;
            s.Nombre = input.Nombre.Trim();
            s.Puerto = input.Puerto;
            s.Notas = Opciones.Limpio(input.Notas);
            if (cambioDestino && s.Estado != EstadoServidor.SinPreparar)
            {
                // Otro servidor: su huella y la llave ya no aplican.
                s.Estado = EstadoServidor.SinPreparar;
                s.HuellaHost = null;
                this.ToastInfo("Cambió la dirección: vuelve a preparar el servidor.");
            }
            await _db.SaveChangesAsync();
            if (await _servidores.GuardarHostAsync(s.Id, input.Host, s.Nombre) is { } error) this.ToastWarning(error);
            this.ToastSuccess($"Servidor {s.Nombre} actualizado.");
            return RedirectToPage("Index");
        }
        await CargarAsync();
        Modal = await ModalAsync(s, input);
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int id)
    {
        if (!EsAdmin) return Forbid();
        var s = await _db.Servidores.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        if (await _db.ConfiguracionesDespliegue.AnyAsync(c => c.ServidorId == id) || await _db.EjecucionesDespliegue.AnyAsync(e => e.ServidorId == id))
        {
            this.ToastWarning($"{s.Nombre} tiene ambientes o despliegues ligados; quita primero su configuración de despliegue.");
            return RedirectToPage("Index");
        }
        _db.Servidores.Remove(s);
        await _db.SaveChangesAsync();
        await _servidores.QuitarLlavesAsync(s);
        this.ToastSuccess($"Servidor {s.Nombre} eliminado de GAS-Admin, junto con su llave. La cuenta {s.Usuario} sigue en el servidor.");
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnGetPrepararAsync(int id)
    {
        if (!EsAdmin) return Forbid();
        var s = await _db.Servidores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        return Partial("_PrepararModal", new PrepararModalVm { Id = s.Id, Nombre = s.Nombre, Host = _servidores.Host(s.Id), YaPreparado = s.Estado != EstadoServidor.SinPreparar });
    }

    public async Task<IActionResult> OnPostPrepararAsync(int id, string? usuario, string? contrasena, CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();
        var s = await _db.Servidores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        ResultadoServidor resultado;
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrEmpty(contrasena))
            resultado = new(false, "Escribe el usuario y la contraseña de administración del servidor.", []);
        else
            resultado = await _servidores.PrepararAsync(id, usuario, contrasena, User.Identity?.Name ?? "", ct);

        if (resultado.Ok) this.ToastSuccess(resultado.Mensaje);
        await CargarAsync();
        ModalPreparar = new PrepararModalVm { Id = s.Id, Nombre = s.Nombre, Host = _servidores.Host(s.Id), Resultado = resultado, YaPreparado = s.Estado != EstadoServidor.SinPreparar };
        return Page();
    }

    public async Task<IActionResult> OnPostProbarAsync(int id, CancellationToken ct)
    {
        if (!EsAdmin) return Forbid();
        if (!await _db.Servidores.AnyAsync(x => x.Id == id)) return NotFound();
        var r = await _servidores.ProbarAsync(id, ct);
        if (r.Ok) this.ToastSuccess($"{r.Mensaje} {string.Join(" ", r.Pasos)}");
        else this.ToastError(r.Mensaje);
        return RedirectToPage("Index");
    }

    private async Task ValidarAsync(ServidorInput i, int? id)
    {
        if (string.IsNullOrWhiteSpace(i.Host) || i.Host.Trim().Contains(' ') || Uri.CheckHostName(i.Host.Trim()) == UriHostNameType.Unknown)
            ModelState.AddModelError(string.Empty, "Escribe la IP o el nombre del servidor (sin https:// ni rutas).");
        if (await _db.Servidores.AnyAsync(s => s.Id != id && s.Nombre == i.Nombre.Trim()))
            ModelState.AddModelError(string.Empty, $"Ya existe un servidor llamado {i.Nombre.Trim()}.");
    }

    private async Task<ServidorModalVm> ModalAsync(Servidor s, ServidorInput input) => new()
    {
        Id = s.Id,
        Input = input,
        Servidor = s,
        Ambientes = await _db.ConfiguracionesDespliegue.AsNoTracking().Where(c => c.ServidorId == s.Id)
            .Select(c => c.Ambiente.Producto.Nombre + " · " + c.Ambiente.Nombre).ToListAsync()
    };

    private async Task CargarAsync()
    {
        var servidores = await _db.Servidores.AsNoTracking().OrderBy(s => s.Nombre).ToListAsync();
        var usos = await _db.ConfiguracionesDespliegue.GroupBy(c => c.ServidorId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        Servidores = servidores.Select(s => new Fila(s, EsAdmin ? _servidores.Host(s.Id) : null, usos.GetValueOrDefault(s.Id))).ToList();
        Ejecuciones = await _db.EjecucionesDespliegue.AsNoTracking().Include(e => e.Producto).Include(e => e.Servidor)
            .OrderByDescending(e => e.CreadoUtc).Take(20)
            .Select(e => new EjecucionDespliegue
            {
                Id = e.Id, Producto = e.Producto, Servidor = e.Servidor, Ambientes = e.Ambientes, Referencia = e.Referencia, Commit = e.Commit,
                MensajeCommit = e.MensajeCommit, Estado = e.Estado, Resumen = e.Resumen, IniciadoPor = e.IniciadoPor,
                CreadoUtc = e.CreadoUtc, InicioUtc = e.InicioUtc, FinUtc = e.FinUtc
            })
            .ToListAsync();
        var ids = Ejecuciones.SelectMany(e => e.IdsAmbientes()).Distinct().ToList();
        NombresAmbientes = await _db.Ambientes.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Nombre);
    }
}
