using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GAS.Admin.Data.Context;

/// <summary>
/// Los folios (TRB, COT, INC, TKT) se calculan como "máximo + 1" y tienen índice único. Si dos
/// personas crean al mismo tiempo, una choca: se toma el siguiente folio y se reintenta.
/// </summary>
public static class GuardadoConFolio
{
    public static bool EsDuplicado(DbUpdateException ex) =>
        ex.InnerException is SqlException s && (s.Number == 2601 || s.Number == 2627);

    public static async Task GuardarConFolioAsync(this GasDbContext db, Func<Task> asignarSiguiente, CancellationToken ct = default)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (intento < 4 && EsDuplicado(ex))
            {
                await asignarSiguiente();
            }
        }
    }
}
