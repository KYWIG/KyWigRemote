namespace KyWigRemote.Core.Model;

/// <summary>
/// Compte local applicatif, géré par le serveur (indépendant de l'AD).
/// Le <see cref="PasswordHash"/> est le hachage stocké, jamais le mot de passe :
/// il ne doit jamais quitter le serveur.
/// </summary>
public sealed class LocalAccount
{
    /// <summary>Identifiant en base ; 0 tant que le compte n'est pas persisté.</summary>
    public int Id { get; set; }

    /// <summary>Identifiant de connexion (unique, insensible à la casse).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Nom d'affichage (facultatif).</summary>
    public string? DisplayName { get; set; }

    /// <summary>Hachage du mot de passe (format PBKDF2). Ne jamais exposer côté client.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Le compte dispose-t-il des droits d'administration ?</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Compte désactivé : refus de connexion sans suppression.</summary>
    public bool Disabled { get; set; }
}
