using System.Security.Cryptography;

namespace GAS.Admin.Security;

/// <summary>Genera contraseñas temporales que cumplen la política de Identity.</summary>
public static class GeneradorPassword
{
    private const string Mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Minusculas = "abcdefghijkmnpqrstuvwxyz";
    private const string Numeros = "23456789";

    public static string Generar(int longitud = 14)
    {
        var todos = Mayusculas + Minusculas + Numeros;
        var chars = new char[longitud];
        chars[0] = Mayusculas[RandomNumberGenerator.GetInt32(Mayusculas.Length)];
        chars[1] = Minusculas[RandomNumberGenerator.GetInt32(Minusculas.Length)];
        chars[2] = Numeros[RandomNumberGenerator.GetInt32(Numeros.Length)];
        for (var i = 3; i < longitud; i++)
        {
            chars[i] = todos[RandomNumberGenerator.GetInt32(todos.Length)];
        }
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}
