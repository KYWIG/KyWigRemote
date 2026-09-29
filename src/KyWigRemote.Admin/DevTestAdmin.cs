using System.Security.Cryptography;

namespace KyWigRemote.Admin;

/// <summary>
/// Fabrique d'identifiants pour l'admin de test (confort de développement uniquement).
/// Le mot de passe est tiré au hasard à chaque appel avec <see cref="RandomNumberGenerator"/>
/// (aucun secret en dur — règle nº 5) ; il n'est affiché qu'une fois à l'écran.
/// </summary>
internal static class DevTestAdmin
{
    // Alphabet sans caractères ambigus (0/O, 1/l/I) pour un mot de passe lisible et recopiable.
    private const string Alphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%*-_";

    private const int PasswordLength = 20;

    /// <summary>Propose un identifiant unique et daté, ex. « test-admin-0931-a4f2 ».</summary>
    public static string NewUsername()
    {
        string suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToLowerInvariant();
        return $"test-admin-{DateTime.Now:HHmm}-{suffix}";
    }

    /// <summary>Génère un mot de passe fort et aléatoire (tirage uniforme sans biais de modulo).</summary>
    public static string NewPassword()
    {
        var chars = new char[PasswordLength];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(chars);
    }
}
