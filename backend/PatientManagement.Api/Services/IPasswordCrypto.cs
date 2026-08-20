namespace PatientManagement.Api.Services;

/// <summary>
/// Reversible symmetric encryption for the single doctor account's password.
/// Deliberately not a one-way hash -- see implementation-brd.md's Authentication
/// spec and its accepted-risk rationale (local-only, no network exposure, one account).
/// </summary>
public interface IPasswordCrypto
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
