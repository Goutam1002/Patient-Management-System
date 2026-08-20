using Microsoft.Extensions.Configuration;
using PatientManagement.Api.Services;
using Xunit;

namespace PatientManagement.Api.Tests.Services;

public class AesPasswordCryptoTests
{
    private static IPasswordCrypto CreateCrypto()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Test-only key, unrelated to appsettings.Development.json's real dev key.
                ["Auth:EncryptionKey"] = Convert.ToBase64String(new byte[32]),
            })
            .Build();

        return new AesPasswordCrypto(config);
    }

    [Fact]
    public void Encrypt_then_decrypt_returns_the_original_plaintext()
    {
        var crypto = CreateCrypto();
        const string original = "ChangeMe123!";

        var encrypted = crypto.Encrypt(original);
        var decrypted = crypto.Decrypt(encrypted);

        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypted_value_is_never_equal_to_the_plaintext()
    {
        var crypto = CreateCrypto();
        const string original = "ChangeMe123!";

        var encrypted = crypto.Encrypt(original);

        Assert.NotEqual(original, encrypted);
    }

    [Fact]
    public void Encrypting_the_same_plaintext_twice_produces_different_ciphertext()
    {
        // Random IV per call -- proves this isn't a deterministic/ECB-style
        // encryption that would leak "these two passwords are identical".
        var crypto = CreateCrypto();
        const string original = "ChangeMe123!";

        var first = crypto.Encrypt(original);
        var second = crypto.Encrypt(original);

        Assert.NotEqual(first, second);
        Assert.Equal(original, crypto.Decrypt(first));
        Assert.Equal(original, crypto.Decrypt(second));
    }

    [Fact]
    public void Missing_encryption_key_throws_at_construction()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => new AesPasswordCrypto(config));
    }
}
