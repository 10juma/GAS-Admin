using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace GAS.Admin.Infrastructure.Toasts;

/// <summary>Tipos de toast: error, advertencia, nota o éxito.</summary>
public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}

public sealed record Toast(string Type, string Message);

/// <summary>
/// Guarda toasts en TempData para mostrarlos en la siguiente vista,
/// tanto si la página se vuelve a dibujar como si hay una redirección.
/// </summary>
public static class ToastExtensions
{
    public const string TempDataKey = "GAS.Toasts";

    public static void AddToast(this ITempDataDictionary tempData, ToastType type, string message)
    {
        var toasts = tempData.PeekToasts();
        toasts.Add(new Toast(type.ToString().ToLowerInvariant(), message));
        tempData[TempDataKey] = JsonSerializer.Serialize(toasts);
    }

    /// <summary>Lee y consume los toasts pendientes.</summary>
    public static List<Toast> ReadToasts(this ITempDataDictionary tempData)
    {
        var toasts = tempData.PeekToasts();
        tempData.Remove(TempDataKey);
        return toasts;
    }

    private static List<Toast> PeekToasts(this ITempDataDictionary tempData) =>
        tempData.Peek(TempDataKey) is string json
            ? JsonSerializer.Deserialize<List<Toast>>(json) ?? []
            : [];

    public static void ToastSuccess(this PageModel page, string message) =>
        page.TempData.AddToast(ToastType.Success, message);

    public static void ToastError(this PageModel page, string message) =>
        page.TempData.AddToast(ToastType.Error, message);

    public static void ToastWarning(this PageModel page, string message) =>
        page.TempData.AddToast(ToastType.Warning, message);

    public static void ToastInfo(this PageModel page, string message) =>
        page.TempData.AddToast(ToastType.Info, message);
}
