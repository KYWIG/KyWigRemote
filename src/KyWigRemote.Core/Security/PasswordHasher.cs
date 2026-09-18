using System.Security.Cryptography;
using System.Text;

namespace KyWigRemote.Core.Security;

/// <summary>
/// Hachage et vérification des mots de passe des comptes locaux applicatifs.
/// Utilise exclusivement des primitives .NET (PBKDF2 / Rfc2898, sel aléatoire,
/// comparaison à temps constant) — aucune primitive maison (règle de sécurité nº 1).
///
/// Format stocké : « pbkdf2-sha256$&lt;iterations&gt;$&lt;sel b64&gt;$&lt;hash b64&gt; ».
/// Il embarque ses paramètres, ce qui permet de les faire évoluer sans casser l'existant.
/// </summary>
public sealed class PasswordHasher
{
    private const string Prefix = "pbkdf2-sha256";
    private const int SaltSize = 16;   // 128 bits
    private const int HashSize = 32;   // 256 bits
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>Produit un hachage salé à stocker pour le mot de passe fourni.</summary>
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, Iterations, Algorithm, HashSize);
            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }
        finally
        {
            // Efface la copie en clair du mot de passe dès que possible (règle nº 3).
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    /// <summary>
    /// Vérifie un mot de passe candidat contre un hachage stocké, en temps constant.
    /// Retourne false plutôt que de lever si le format stocké est invalide.
    /// </summary>
    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        string[] parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out int iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, iterations, Algorithm, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }
}
