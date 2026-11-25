using GAS.Admin.Services.Respaldos;

// Descifra un respaldo de GAS-Admin (.bak.gz.gasr) sin necesitar GAS-Admin, por ejemplo si el servidor se perdió.
//
//   dotnet run --project herramientas/DescifrarRespaldo -- <archivo.bak.gz.gasr> [salida.bak]
//
// La llave de cifrado se pide en la terminal (no se escribe en pantalla) o se toma de la variable
// GAS_LLAVE_RESPALDOS. Es la misma que se guardó en GAS-Admin › Respaldos (Respaldos:LlaveCifrado).

if (args.Length is < 1 or > 2 || args[0] is "-h" or "--help")
{
    Console.Error.WriteLine("Uso: DescifrarRespaldo <archivo.bak.gz.gasr> [salida.bak]");
    return 2;
}
var entrada = args[0];
if (!File.Exists(entrada))
{
    Console.Error.WriteLine($"No existe {entrada}.");
    return 2;
}
var salida = args.Length == 2 ? args[1]
    : entrada.EndsWith(CifradoRespaldo.Extension, StringComparison.OrdinalIgnoreCase) ? entrada[..^CifradoRespaldo.Extension.Length] + ".bak"
    : entrada + ".bak";
if (File.Exists(salida))
{
    Console.Error.WriteLine($"{salida} ya existe; no se sobrescribe.");
    return 2;
}

var llave = Environment.GetEnvironmentVariable("GAS_LLAVE_RESPALDOS");
if (string.IsNullOrWhiteSpace(llave))
{
    Console.Write("Llave de cifrado: ");
    llave = LeerOculto();
}

try
{
    await using (var origen = File.OpenRead(entrada))
    await using (var destino = File.Create(salida))
        await CifradoRespaldo.DescifrarAsync(origen, destino, llave, CancellationToken.None);
    Console.WriteLine($"Listo: {salida} ({new FileInfo(salida).Length / 1024.0 / 1024.0:0.0} MB). Restáuralo con RESTORE DATABASE en SQL Server.");
    return 0;
}
catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or InvalidDataException or InvalidOperationException)
{
    try { File.Delete(salida); } catch (IOException) { }
    Console.Error.WriteLine($"No se pudo descifrar: {ex.Message}");
    return 1;
}

static string LeerOculto()
{
    if (Console.IsInputRedirected) return Console.ReadLine() ?? "";
    var texto = new System.Text.StringBuilder();
    while (true)
    {
        var k = Console.ReadKey(intercept: true);
        if (k.Key == ConsoleKey.Enter) break;
        if (k.Key == ConsoleKey.Backspace) { if (texto.Length > 0) texto.Length--; }
        else if (!char.IsControl(k.KeyChar)) texto.Append(k.KeyChar);
    }
    Console.WriteLine();
    return texto.ToString();
}
