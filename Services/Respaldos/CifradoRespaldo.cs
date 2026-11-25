using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace GAS.Admin.Services.Respaldos;

/// <summary>
/// Formato de los respaldos en B2 (.bak.gz.gasr): el .bak se comprime con gzip y se cifra con AES-256-CBC;
/// al final va un HMAC-SHA256 de todo lo anterior (cifrar y luego firmar), así que un archivo alterado o una
/// llave equivocada se detectan antes de descifrar.
///
///   "GASR1" (5 bytes) | IV (16) | datos cifrados | HMAC-SHA256 (32)
///
/// Las llaves de AES y de HMAC se derivan con HKDF-SHA256 de la llave de cifrado capturada en GAS-Admin
/// (Respaldos:LlaveCifrado). La herramienta herramientas/DescifrarRespaldo usa exactamente este formato.
/// </summary>
public static class CifradoRespaldo
{
    public const string Extension = ".bak.gz.gasr";
    private static readonly byte[] Magia = "GASR1"u8.ToArray();
    private const int LargoIv = 16, LargoMac = 32;

    public static (byte[] Cifrado, byte[] Firma) DerivarLlaves(string llave)
    {
        if (string.IsNullOrWhiteSpace(llave) || llave.Trim().Length < 32)
            throw new InvalidOperationException("La llave de cifrado de respaldos debe tener al menos 32 caracteres.");
        var ikm = Encoding.UTF8.GetBytes(llave.Trim());
        return (HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 32, info: "gas-respaldos/aes"u8.ToArray()),
                HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 32, info: "gas-respaldos/hmac"u8.ToArray()));
    }

    /// <summary>Comprime y cifra <paramref name="origen"/> en <paramref name="destino"/> (que debe permitir leer y buscar).</summary>
    public static async Task CifrarAsync(Stream origen, Stream destino, string llave, CancellationToken ct)
    {
        var (kAes, kMac) = DerivarLlaves(llave);
        var iv = RandomNumberGenerator.GetBytes(LargoIv);
        var inicio = destino.Position;
        await destino.WriteAsync(Magia, ct);
        await destino.WriteAsync(iv, ct);

        using (var aes = Aes.Create())
        {
            aes.Key = kAes;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            await using var cifrado = new CryptoStream(destino, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true);
            await using (var gzip = new GZipStream(cifrado, CompressionLevel.Optimal, leaveOpen: true))
                await origen.CopyToAsync(gzip, ct);
            await cifrado.FlushFinalBlockAsync(ct);
        }

        // HMAC de magia + IV + datos cifrados (todo lo escrito hasta aquí).
        destino.Position = inicio;
        var mac = await HMACSHA256.HashDataAsync(kMac, new Recortado(destino, destino.Length - inicio), ct);
        destino.Seek(0, SeekOrigin.End);
        await destino.WriteAsync(mac, ct);
    }

    /// <summary>Verifica la firma y descifra/descomprime <paramref name="origen"/> en <paramref name="destino"/>.</summary>
    public static async Task DescifrarAsync(Stream origen, Stream destino, string llave, CancellationToken ct)
    {
        var (kAes, kMac) = DerivarLlaves(llave);
        var largo = origen.Length - origen.Position;
        if (largo < Magia.Length + LargoIv + 16 + LargoMac) throw new InvalidDataException("El archivo es demasiado corto para ser un respaldo cifrado.");
        var inicio = origen.Position;

        var cabecera = new byte[Magia.Length + LargoIv];
        await origen.ReadExactlyAsync(cabecera, ct);
        if (!cabecera.AsSpan(0, Magia.Length).SequenceEqual(Magia)) throw new InvalidDataException("No es un respaldo cifrado de GAS-Admin (GASR1).");
        var iv = cabecera[Magia.Length..];

        origen.Position = inicio + largo - LargoMac;
        var macEsperado = new byte[LargoMac];
        await origen.ReadExactlyAsync(macEsperado, ct);
        origen.Position = inicio;
        var mac = await HMACSHA256.HashDataAsync(kMac, new Recortado(origen, largo - LargoMac), ct);
        if (!CryptographicOperations.FixedTimeEquals(mac, macEsperado))
            throw new CryptographicException("La firma no coincide: la llave de cifrado es otra o el archivo está dañado.");

        origen.Position = inicio + cabecera.Length;
        using var aes = Aes.Create();
        aes.Key = kAes;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        await using var cifrado = new CryptoStream(new Recortado(origen, largo - cabecera.Length - LargoMac), aes.CreateDecryptor(), CryptoStreamMode.Read);
        await using var gzip = new GZipStream(cifrado, CompressionMode.Decompress);
        await gzip.CopyToAsync(destino, ct);
    }

    /// <summary>Vista de solo lectura de los siguientes <c>largo</c> bytes de un stream.</summary>
    private sealed class Recortado(Stream interno, long largo) : Stream
    {
        private long _restante = largo;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            if (_restante <= 0) return 0;
            var n = interno.Read(buffer[..(int)Math.Min(buffer.Length, _restante)]);
            _restante -= n;
            return n;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_restante <= 0) return 0;
            var n = await interno.ReadAsync(buffer[..(int)Math.Min(buffer.Length, _restante)], ct);
            _restante -= n;
            return n;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
