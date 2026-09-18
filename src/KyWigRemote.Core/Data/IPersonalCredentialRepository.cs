using KyWigRemote.Core.Security;

namespace KyWigRemote.Core.Data;

/// <summary>
/// Stockage des identifiants personnels : chaque enregistrement appartient à un utilisateur
/// (« owner ») et vise une connexion précise, ou toutes ses connexions (connectionId null).
/// Cette couche ne manipule jamais de secret en clair.
/// </summary>
public interface IPersonalCredentialRepository
{
    /// <summary>Crée ou remplace l'identifiant personnel de l'utilisateur pour la portée indiquée.</summary>
    void Save(string owner, int? connectionId, string username, string? domain, EncryptedSecret secret);

    /// <summary>Retourne l'identifiant personnel de l'utilisateur pour la portée exacte, ou null.</summary>
    StoredPersonalCredential? Find(string owner, int? connectionId);
}

/// <summary>Identifiant personnel lu en base : métadonnées + secret chiffré (jamais de clair).</summary>
public sealed record StoredPersonalCredential(string Username, string? Domain, EncryptedSecret Secret);
