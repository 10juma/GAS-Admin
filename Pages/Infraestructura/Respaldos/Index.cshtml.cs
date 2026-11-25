using GAS.Admin.Data.Context;
using GAS.Admin.Helpers;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Models.Entities;
using GAS.Admin.Security;
using GAS.Admin.Services.Configuracion;
using GAS.Admin.Services.Respaldos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Pages.Infraestructura.Respaldos;

/// <summary>Respaldos de las bases en Backblaze B2: configuración, bases incluidas e historial.</summary>
[Authorize(Policy = Permisos.Infraestructura.Ver)]
public class IndexModel : PageModel
{
    private readonly GasDbContext _db;
    private readonly ServicioRespaldos _respaldos;
    private readonly ProgramadorRespaldos _programador;
    private readonly ServicioLlaves _llaves;

    public IndexModel(GasDbContext db, ServicioRespaldos respaldos, ProgramadorRespaldos programador, ServicioLlaves llaves)
    {
        _db = db;
        _respaldos = respaldos;
        _programador = programador;
        _llaves = llaves;
    }

    public sealed record FilaEjecucion(EjecucionRespaldo Ejecucion, int Correctas, int Errores, long Bytes);

    public bool EsAdmin => User.IsInRole(RolesBase.Admin);
    public bool B2 => _respaldos.B2Configurado;
    public bool Cifrado => _respaldos.CifradoConfigurado;
    public bool Configurado => _respaldos.Configurado;
    public bool Programa => _programador.Programa;
    public string? Bucket => _respaldos.Bucket;
    public string? CorreoAlertas => _respaldos.CorreoAlertas;
    public TimeOnly Hora => _respaldos.Hora;
    public string GrupoLlaves => CatalogoLlaves.Respaldos.Id;
    public Dictionary<string, EstadoLlave> Estados { get; private set; } = [];

    public List<BaseRespaldable> Bases { get; private set; } = [];
    public List<string> Avisos { get; private set; } = [];
    public List<FilaEjecucion> Ejecuciones { get; private set; } = [];
    public EjecucionRespaldo? UltimoExitoso { get; private set; }
    public bool EnCurso { get; private set; }
    public int ArchivosGuardados { get; private set; }
    public long BytesGuardados { get; private set; }

    /// <summary>Próximo respaldo programado (hora local), si esta instancia programa.</summary>
    public DateTime? Proximo
    {
        get
        {
            if (!Programa || !Configurado) return null;
            var local = Fechas.ALocal(DateTime.UtcNow);
            var hoy = DateOnly.FromDateTime(local);
            var yaHoy = Ejecuciones.Any(f => f.Ejecucion.ClaveProgramada == hoy.ToString("yyyy-MM-dd"));
            // Si hoy ya corrió, mañana; si no, hoy a la hora (si ya pasó, arranca en el siguiente minuto).
            return (yaHoy ? hoy.AddDays(1) : hoy).ToDateTime(Hora);
        }
    }

    public async Task OnGetAsync()
    {
        Estados = await _llaves.EstadoAsync(CatalogoLlaves.Respaldos.Campos.Select(c => c.Clave).ToArray());
        (Bases, Avisos) = await _respaldos.BasesAsync();
        var ejecuciones = await _db.EjecucionesRespaldo.AsNoTracking().OrderByDescending(e => e.Id).Take(40)
            .Select(e => new
            {
                Ejecucion = new EjecucionRespaldo
                {
                    Id = e.Id, Tipo = e.Tipo, Estado = e.Estado, ClaveProgramada = e.ClaveProgramada, IniciadoPor = e.IniciadoPor,
                    InicioUtc = e.InicioUtc, FinUtc = e.FinUtc, Resumen = e.Resumen
                },
                Correctas = e.Archivos.Count(a => a.Error == null),
                Errores = e.Archivos.Count(a => a.Error != null),
                Bytes = e.Archivos.Where(a => a.Error == null).Sum(a => (long?)a.Bytes) ?? 0
            }).ToListAsync();
        Ejecuciones = ejecuciones.Select(x => new FilaEjecucion(x.Ejecucion, x.Correctas, x.Errores, x.Bytes)).ToList();
        EnCurso = Ejecuciones.Any(f => f.Ejecucion.Estado == EstadoRespaldo.EnCurso);
        UltimoExitoso = await _db.EjecucionesRespaldo.AsNoTracking().Where(e => e.Estado == EstadoRespaldo.Exitoso)
            .OrderByDescending(e => e.Id).FirstOrDefaultAsync();
        var guardados = _db.ArchivosRespaldo.AsNoTracking().Where(a => a.IdArchivoB2 != null && a.BorradoUtc == null);
        ArchivosGuardados = await guardados.CountAsync();
        BytesGuardados = await guardados.SumAsync(a => (long?)a.Bytes) ?? 0;
    }

    public async Task<IActionResult> OnPostRespaldarAsync()
    {
        if (!EsAdmin) return Forbid();
        var (e, error) = await _respaldos.IniciarManualAsync(User.Identity?.Name ?? "?");
        if (e is null)
        {
            this.ToastError(error!);
            return RedirectToPage("Index");
        }
        return RedirectToPage("Ejecucion", new { id = e.Id });
    }
}
