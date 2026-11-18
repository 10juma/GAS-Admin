using GAS.Admin.Security;
using GAS.Admin.Services.Finanzas;
using GAS.Admin.Services.Proyectos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GAS.Admin.Pages.Finanzas;

[Authorize(Policy = Permisos.Finanzas.Ver)]
public class IndexModel : PageModel
{
    private readonly ReportesFinanzas _reportes;
    private readonly ServicioFinanzas _finanzas;

    public IndexModel(ReportesFinanzas reportes, ServicioFinanzas finanzas)
    {
        _reportes = reportes;
        _finanzas = finanzas;
    }

    /// <summary>mes (por defecto), anterior, trimestre o anio.</summary>
    [BindProperty(SupportsGet = true)] public string? Periodo { get; set; }

    public bool PuedeGestionar => User.TienePermiso(Permisos.Finanzas.Gestionar);
    public ResumenFinanzas R { get; private set; } = new();
    public int RenovacionesSinCargo { get; private set; }
    public string NombrePeriodo { get; private set; } = string.Empty;

    public async Task OnGetAsync()
    {
        var hoy = ServicioFinanzas.Hoy;
        var mes = ServicioTrabajos.InicioMes(hoy);
        (DateOnly desde, DateOnly hasta, string nombre) = Periodo switch
        {
            "anterior" => (mes.AddMonths(-1), mes.AddDays(-1), ReportesFinanzas.NombreMes(mes.AddMonths(-1))),
            "trimestre" => (mes.AddMonths(-2), hoy, "Últimos 3 meses"),
            "anio" => (new DateOnly(hoy.Year, 1, 1), hoy, $"Año {hoy.Year}"),
            _ => (mes, hoy, ReportesFinanzas.NombreMes(mes))
        };
        NombrePeriodo = nombre;
        R = await _reportes.ResumenAsync(desde, hasta);
        RenovacionesSinCargo = (await _finanzas.RenovacionesSinCargoAsync()).Count;
    }
}
