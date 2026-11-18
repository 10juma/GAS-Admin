namespace GAS.Admin.Helpers;

/// <summary>Las fechas se guardan en UTC y se muestran en hora del centro de México.</summary>
public static class Fechas
{
    private static readonly TimeZoneInfo ZonaMexico = Buscar();

    private static TimeZoneInfo Buscar()
    {
        foreach (var id in new[] { "America/Mexico_City", "Central Standard Time (Mexico)" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zona))
            {
                return zona;
            }
        }
        return TimeZoneInfo.Local;
    }

    public static DateTime ALocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ZonaMexico);

    public static DateTime ALocalUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), ZonaMexico);

    public static string Formato(DateTime? utc) =>
        utc is null ? "—" : ALocal(utc.Value).ToString("dd/MM/yyyy HH:mm");

    public static string Relativo(DateTime? utc)
    {
        if (utc is null) return "Nunca";
        var diff = DateTime.UtcNow - utc.Value;
        if (diff.TotalMinutes < 1) return "Hace un momento";
        if (diff.TotalMinutes < 60) return $"Hace {(int)diff.TotalMinutes} min";
        if (diff.TotalHours < 24) return $"Hace {(int)diff.TotalHours} h";
        if (diff.TotalDays < 7) return $"Hace {(int)diff.TotalDays} d";
        return Formato(utc);
    }
}
