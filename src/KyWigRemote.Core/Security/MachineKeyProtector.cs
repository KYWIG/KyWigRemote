using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace KyWigRemote.Core.Security;

/// <summary>
/// Protège une valeur secrète (clé maître de chiffrement, clé de signature des jetons) au
/// repos avec DPAPI, liée à la machine (<see cref="DataProtectionScope.LocalMachine"/>).
///
/// Objectif : ne jamais stocker une clé en clair à côté des données qu'elle protège. Le blob
/// produit peut être conservé en base (table <c>settings</c>) sans exposer la clé : copié sur
/// une autre machine (sauvegarde, disque volé, partage réseau), il est indéchiffrable.
///
/// Primitive .NET uniquement (<see cref="ProtectedData"/>) — aucune primitive maison
/// (règle de sécurité nº 1). Le scope machine (et non utilisateur) est nécessaire car le
/// serveur tourne en service Windows, potentiellement sous un compte différent de celui qui
/// a initialisé la base.
/// </summary>
[SupportedOSPlatform("windows")]
public static class MachineKeyProtector
{
    /// <summary>
    /// Marqueur en tête de la valeur stockée. Distingue un blob protégé par DPAPI d'une
    /// ancienne valeur en clair, ce qui permet une migration transparente au démarrage.
    /// </summary>
    public const string Prefix = "DPAPI:";

    /// <summary>Chiffre les octets fournis et retourne une chaîne stockable (préfixe + base64).</summary>
    public static string Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        byte[] blob = ProtectedData.Protect(plaintext, optionalEntropy: null, DataProtectionScope.LocalMachine);
        return Prefix + Convert.ToBase64String(blob);
    }

    /// <summary>Indique si la valeur stockée est déjà protégée par DPAPI (préfixe présent).</summary>
    public static bool IsProtected(string? stored) =>
        stored is not null && stored.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Déchiffre une valeur produite par <see cref="Protect"/>.</summary>
    public static byte[] Unprotect(string stored)
    {
        ArgumentException.ThrowIfNullOrEmpty(stored);
        if (!IsProtected(stored))
        {
            throw new ArgumentException("La valeur n'est pas protégée par DPAPI.", nameof(stored));
        }
        byte[] blob = Convert.FromBase64String(stored[Prefix.Length..]);
        return ProtectedData.Unprotect(blob, optionalEntropy: null, DataProtectionScope.LocalMachine);
    }
}
