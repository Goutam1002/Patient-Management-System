using System.Security.Cryptography;
using System.Text;

namespace PatientManagement.Api.Services;

public class AesPasswordCrypto : IPasswordCrypto
{
    private readonly byte[] _key;

    public AesPasswordCrypto(IConfiguration configuration)
    {
        var keyBase64 = configuration["Auth:EncryptionKey"]
            ?? throw new InvalidOperationException(
                "Auth:EncryptionKey is not configured. Set a base64-encoded 256-bit key in appsettings (see appsettings.Development.json).");

        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length != 32)
        {
            throw new InvalidOperationException(
                $"Auth:EncryptionKey must decode to 32 bytes (AES-256), got {_key.Length}.");
        }
    }

    public string Encrypt(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Store IV alongside the ciphertext -- IV isn't secret, it just has to
        // travel with the data so decryption can reconstruct the same stream.
        var combined = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string ciphertext)
    {
        var combined = Convert.FromBase64String(ciphertext);

        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = new byte[aes.IV.Length];
        var cipherBytes = new byte[combined.Length - iv.Length];
        Buffer.BlockCopy(combined, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(combined, iv.Length, cipherBytes, 0, cipherBytes.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
