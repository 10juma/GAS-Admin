using Microsoft.AspNetCore.Html;

namespace GAS.Admin.Helpers;

/// <summary>Íconos SVG de línea para botones y chips: @Iconos.Svg("plus").</summary>
public static class Iconos
{
    private static readonly Dictionary<string, string> Trazos = new()
    {
        ["plus"] = "<path d='M12 5v14M5 12h14'/>",
        ["arrow-up"] = "<path d='M7 17 17 7M8 7h9v9'/>",
        ["arrow-down"] = "<path d='M17 7 7 17M16 17H7V8'/>",
        ["wallet"] = "<path d='M19 7V5a2 2 0 0 0-2-2H5a2 2 0 0 0 0 4h14a2 2 0 0 1 2 2v3M3 5v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-3'/><path d='M17 12h4v4h-4a2 2 0 0 1 0-4Z'/>",
        ["trend"] = "<path d='M22 7 13.5 15.5 8.5 10.5 2 17'/><path d='M16 7h6v6'/>",
        ["tag"] = "<path d='M12.6 2.6A2 2 0 0 0 11.2 2H4a2 2 0 0 0-2 2v7.2a2 2 0 0 0 .6 1.4l8.7 8.7a2.4 2.4 0 0 0 3.4 0l6.6-6.6a2.4 2.4 0 0 0 0-3.4Z'/><circle cx='7.5' cy='7.5' r='1.5'/>",
        ["swap"] = "<path d='m16 3 4 4-4 4M20 7H4M8 21l-4-4 4-4M4 17h16'/>",
        ["user-plus"] = "<path d='M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='M19 8v6M22 11h-6'/>",
        ["user-x"] = "<path d='M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='m17 8 5 5M22 8l-5 5'/>",
        ["user-check"] = "<path d='M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='m16 11 2 2 4-4'/>",
        ["users"] = "<path d='M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75'/>",
        ["check"] = "<path d='M20 6 9 17l-5-5'/>",
        ["save"] = "<path d='M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2Z'/><path d='M17 21v-8H7v8M7 3v5h8'/>",
        ["filter"] = "<path d='M22 3H2l8 9.46V19l4 2v-8.54Z'/>",
        ["search"] = "<circle cx='11' cy='11' r='8'/><path d='m21 21-4.3-4.3'/>",
        ["x"] = "<path d='M18 6 6 18M6 6l12 12'/>",
        ["arrow-left"] = "<path d='m12 19-7-7 7-7M19 12H5'/>",
        ["arrow-right"] = "<path d='M5 12h14M12 5l7 7-7 7'/>",
        ["pencil"] = "<path d='M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z'/>",
        ["eye"] = "<path d='M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7-10-7-10-7Z'/><circle cx='12' cy='12' r='3'/>",
        ["trash"] = "<path d='M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M10 11v6M14 11v6'/>",
        ["key"] = "<circle cx='7.5' cy='15.5' r='5.5'/><path d='m21 2-9.6 9.6M15.5 7.5l3 3L22 7l-3-3'/>",
        ["unlock"] = "<rect x='3' y='11' width='18' height='11' rx='2'/><path d='M7 11V7a5 5 0 0 1 9.9-1'/>",
        ["lock"] = "<rect x='3' y='11' width='18' height='11' rx='2'/><path d='M7 11V7a5 5 0 0 1 10 0v4'/>",
        ["copy"] = "<rect x='9' y='9' width='13' height='13' rx='2'/><path d='M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1'/>",
        ["log-out"] = "<path d='M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9'/>",
        ["shield"] = "<path d='M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z'/><path d='m9 12 2 2 4-4'/>",
        ["activity"] = "<path d='M22 12h-4l-3 9L9 3l-3 9H2'/>",
        ["alert"] = "<path d='M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z'/><path d='M12 9v4M12 17h.01'/>",
        ["calendar"] = "<rect x='3' y='4' width='18' height='18' rx='2'/><path d='M16 2v4M8 2v4M3 10h18'/>",
        ["layers"] = "<path d='m12 2 10 5-10 5L2 7Z'/><path d='m2 17 10 5 10-5M2 12l10 5 10-5'/>",
        ["home"] = "<path d='m3 9 9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2Z'/><path d='M9 22V12h6v10'/>",
        ["clock"] = "<circle cx='12' cy='12' r='10'/><path d='M12 6v6l4 2'/>",
        ["box"] = "<path d='M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z'/><path d='m3.3 7 8.7 5 8.7-5M12 22V12'/>",
        ["building"] = "<rect x='4' y='2' width='16' height='20' rx='2'/><path d='M9 22v-4h6v4M8 6h.01M12 6h.01M16 6h.01M8 10h.01M12 10h.01M16 10h.01M8 14h.01M12 14h.01M16 14h.01'/>",
        ["plug"] = "<path d='M12 22v-5M9 8V2M15 8V2M18 8v5a6 6 0 0 1-12 0V8Z'/>",
        ["card"] = "<rect x='2' y='5' width='20' height='14' rx='2'/><path d='M2 10h20'/>",
        ["globe"] = "<circle cx='12' cy='12' r='10'/><path d='M2 12h20M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10Z'/>",
        ["server"] = "<rect x='2' y='3' width='20' height='7' rx='2'/><rect x='2' y='14' width='20' height='7' rx='2'/><path d='M6 6.5h.01M6 17.5h.01'/>",
        ["git"] = "<circle cx='18' cy='18' r='3'/><circle cx='6' cy='6' r='3'/><path d='M13 6h3a2 2 0 0 1 2 2v7M6 9v12'/>",
        ["bug"] = "<path d='M8 2l1.9 1.9M16 2l-1.9 1.9M9 7.1V6a3 3 0 1 1 6 0v1.1'/><path d='M12 20c-3.3 0-6-2.7-6-6v-3a4 4 0 0 1 4-4h4a4 4 0 0 1 4 4v3c0 3.3-2.7 6-6 6ZM12 20v-9M6 13H2M22 13h-4M3 21l3-3M21 21l-3-3M3 7l3 3M21 7l-3 3'/>",
        ["phone"] = "<rect x='7' y='2' width='10' height='20' rx='2'/><path d='M11 18h2'/>",
        ["mail"] = "<rect x='3' y='5' width='18' height='14' rx='2'/><path d='m3 7 9 6 9-6'/>",
        ["rocket"] = "<path d='M4.5 16.5c-1.5 1.3-2 5-2 5s3.7-.5 5-2c.7-.8.7-2.1-.1-2.9a2.2 2.2 0 0 0-2.9-.1ZM12 15l-3-3a22 22 0 0 1 2-3.9A12.9 12.9 0 0 1 22 2c0 2.7-.8 7.5-6 11a22.4 22.4 0 0 1-4 2Z'/><path d='M9 12H4s.6-3 2-4c1.6-1.1 5 0 5 0M12 15v5s3-.6 4-2c1.1-1.6 0-5 0-5'/>",
        ["external"] = "<path d='M15 3h6v6M10 14 21 3M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6'/>",
        ["dollar"] = "<path d='M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6'/>",
        ["refresh"] = "<path d='M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8M21 3v5h-5M3 21v-5h5'/>",
        ["monitor"] = "<rect x='2' y='3' width='20' height='14' rx='2'/><path d='M8 21h8M12 17v4'/>",
        ["note"] = "<path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z'/><path d='M14 2v6h6M16 13H8M16 17H8'/>",
    };

    public static IHtmlContent Svg(string nombre, string? clase = null)
    {
        var trazo = Trazos.TryGetValue(nombre, out var t) ? t : Trazos["x"];
        return new HtmlString(
            $"<svg class='gas-icon {clase}' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' " +
            $"stroke-linecap='round' stroke-linejoin='round' aria-hidden='true'>{trazo}</svg>");
    }
}
