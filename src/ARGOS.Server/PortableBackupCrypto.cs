using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ARGOS.Server;

/// <summary>Streaming, password-based encryption for backups that may leave the server.</summary>
public static class PortableBackupCrypto
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("ARGOSB01");
    private const int SaltLength = 16;
    private const int IvLength = 16;
    private const int TagLength = 32;
    private const int Iterations = 210_000;

    public static async Task EncryptAsync(string source, string destination, string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase) || passphrase.Length < 12)
            throw new InvalidOperationException("La clave de recuperación debe tener al menos 12 caracteres.");
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var iv = RandomNumberGenerator.GetBytes(IvLength);
        var keys = Derive(passphrase, salt);
        try
        {
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous))
            {
                await output.WriteAsync(Magic);
                await output.WriteAsync(salt);
                await output.WriteAsync(iv);
                using var aes = Aes.Create();
                aes.Key = keys.Encryption;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using var crypto = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true);
                await input.CopyToAsync(crypto);
                crypto.FlushFinalBlock();
            }
            var tag = await ComputeTagAsync(destination, keys.Authentication, 0);
            await using var append = new FileStream(destination, FileMode.Append, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            await append.WriteAsync(tag);
            CryptographicOperations.ZeroMemory(tag);
        }
        finally { CryptographicOperations.ZeroMemory(keys.Encryption); CryptographicOperations.ZeroMemory(keys.Authentication); }
    }

    public static async Task DecryptAsync(string source, string destination, string passphrase)
    {
        var length = new FileInfo(source).Length;
        if (length <= Magic.Length + SaltLength + IvLength + TagLength) throw new InvalidOperationException("El respaldo cifrado está incompleto.");
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var magic = new byte[Magic.Length]; var salt = new byte[SaltLength]; var iv = new byte[IvLength];
        await input.ReadExactlyAsync(magic); await input.ReadExactlyAsync(salt); await input.ReadExactlyAsync(iv);
        if (!CryptographicOperations.FixedTimeEquals(Magic, magic)) throw new InvalidOperationException("El archivo no es un respaldo portable de ARGOS.");
        var keys = Derive(passphrase, salt);
        try
        {
            input.Position = length - TagLength;
            var tag = new byte[TagLength]; await input.ReadExactlyAsync(tag);
            var computed = await ComputeTagAsync(source, keys.Authentication, TagLength);
            var valid = CryptographicOperations.FixedTimeEquals(tag, computed);
            CryptographicOperations.ZeroMemory(tag); CryptographicOperations.ZeroMemory(computed);
            if (!valid) throw new InvalidOperationException("La clave no corresponde o el respaldo fue alterado.");
            input.Position = Magic.Length + SaltLength + IvLength;
            using var aes = Aes.Create(); aes.Key = keys.Encryption; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous);
            await using var crypto = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read, leaveOpen: true);
            await crypto.CopyToAsync(output);
        }
        finally { CryptographicOperations.ZeroMemory(keys.Encryption); CryptographicOperations.ZeroMemory(keys.Authentication); }
    }

    private static (byte[] Encryption, byte[] Authentication) Derive(string passphrase, byte[] salt)
    {
        var material = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passphrase), salt, Iterations, HashAlgorithmName.SHA256, 64);
        return (material[..32], material[32..]);
    }
    private static async Task<byte[]> ComputeTagAsync(string path, byte[] key, int excludedTail)
    {
        using var hmac = new HMACSHA256(key);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var remaining = stream.Length - excludedTail;
        var buffer = new byte[131072];
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)));
            if (read == 0) throw new EndOfStreamException();
            hmac.TransformBlock(buffer, 0, read, null, 0); remaining -= read;
        }
        hmac.TransformFinalBlock([], 0, 0);
        return hmac.Hash!;
    }
}
