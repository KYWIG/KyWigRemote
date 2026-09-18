using KyWigRemote.Core.Remote;
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

    /// <summary>Liste les identifiants personnels d'un utilisateur (métadonnées, sans secret).</summary>
    IReadOnlyList<PersonalCredentialSummary> ListByOwner(string owner);

    /// <summary>Supprime un identifiant personnel s'il appartient à l'utilisateur ; retourne vrai si supprimé.</summary>
    bool Delete(string owner, int id);
}

/// <summary>Identifiant personnel lu en base : métadonnées + secret chiffré (jamais de clair).</summary>
public sealed record StoredPersonalCredential(string Username, string? Domain, EncryptedSecret Secret);
