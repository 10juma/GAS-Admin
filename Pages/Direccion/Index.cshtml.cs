using System.Globalization;
using GAS.Admin.Data.Context;
using GAS.Admin.Infrastructure.Toasts;
using GAS.Admin.Security;
using GAS.Admin.Services.Direccion;
using GAS.Admin.Services.Finanzas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GAS.Admin.Pages.Direccion;

[Authorize(Policy = Permisos.Direccion.Ver)]
public class IndexModel : PageModel
{
    private static readonly CultureInfo Mx = new("es-MX");
    private readonly ServicioIndicadores _indicadores;

    public IndexModel(ServicioIndicadores indicadores) => _indicadores = indicadores;

    /// <summary>Mes a mostrar (yyyy-MM). Por defecto, el actual en vivo.</summary>
    [BindProperty(SupportsGet = true)] public string? Mes { get; set; }

    public DateOnly MesVista { get; private set; }
    public ValoresMes Actual { get; private set; } = null!;
    public ValoresMes Anterior { get; private set; } = null!;
    /// <summary>Serie por indicador: meses cerrados + el mes en vivo.</summary>
    public Dictionary<string, List<(DateOnly Mes, decimal? Valor)>> Series { get; private set; } = [];
    public List<DateOnly> MesesDisponibles { get; private set; } = [];
    public int MesesSinCierre { get; private set; }
    public bool AnteriorSinCierreCompleto { get; private set; }

    public static string NombreMes(DateOnly m) => Mx.TextInfo.ToTitleCase(m.ToString("MMMM yyyy", Mx));

    public async Task OnGetAsync()
    {
        var actual = ServicioIndicadores.MesActual;
        MesVista = Mes is { Length: 7 } && DateOnly.TryParseExact(Mes + "-01", "yyyy-MM-dd", out var m) && m <= actual && m >= actual.AddMonths(-24)
            ? m : actual;

        var historico = await _indicadores.HistoricoAsync(12);
        Actual = MesVista == actual ? await _indicadores.ValoresAsync(actual) : await _indicadores.ValoresAsync(MesVista);
        Anterior = await _indicadores.ValoresAsync(MesVista.AddMonths(-1));
        var enVivo = MesVista == actual ? Actual : await _indicadores.ValoresAsync(actual);

        var meses = historico.Select(c => (c.Mes, Valores: ServicioIndicadores.Deserializar(c.IndicadoresJson))).ToList();
        meses.Add((actual, enVivo.Valores));
        foreach (var i in ServicioIndicadores.Todos)
            Series[i.Clave] = meses.Select(x => (x.Mes, x.Valores.GetValueOrDefault(i.Clave))).ToList();

        MesesDisponibles = Enumerable.Range(0, 13).Select(n => actual.AddMonths(-n)).ToList();
        var cerrados = historico.Select(c => c.Mes).ToHashSet();
        MesesSinCierre = Enumerable.Range(1, 12).Count(n => !cerrados.Contains(actual.AddMonths(-n)));
        AnteriorSinCierreCompleto = !historico.Any(c => c.Mes == actual.AddMonths(-1) && !c.Parcial);
    }

    public bool PuedeCerrar => User.IsInRole(RolesBase.Admin);

    public async Task<IActionResult> OnPostReconstruirAsync()
    {
        if (!PuedeCerrar) return Forbid();
        var n = await _indicadores.ReconstruirAsync(12);
        if (n == 0) this.ToastInfo("Los últimos 12 meses ya tienen cierre.");
        else this.ToastSuccess($"Se reconstruyeron {n} mes(es) con los indicadores del periodo. Los puntuales (MRR, por cobrar…) no se pueden recuperar hacia atrás.");
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostCerrarAnteriorAsync()
    {
        if (!PuedeCerrar) return Forbid();
        var mes = ServicioIndicadores.MesActual.AddMonths(-1);
        await _indicadores.CerrarAsync(mes, true);
        this.ToastSuccess($"Cierre de {NombreMes(mes).ToLowerInvariant()} guardado con los indicadores de este momento.");
        return RedirectToPage("Index", new { mes = mes.ToString("yyyy-MM") });
    }
}
