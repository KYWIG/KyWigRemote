using KyWigRemote.Core.Model;
using KyWigRemote.Core.Security;

namespace KyWigRemote.Core.Data;

/// <summary>
/// Stockage des identifiants imposés : les métadonnées et le secret chiffré sont
/// conservés ensemble, mais cette couche ne manipule jamais de secret en clair.
/// </summary>
public interface ICredentialRepository
{
    /// <summary>Enregistre un identifiant imposé avec son secret déjà chiffré ; retourne son identifiant.</summary>
    int SaveEnforced(EnforcedCredential credential, EncryptedSecret secret);

    /// <summary>Retourne le secret chiffré d'un identifiant imposé, ou null s'il n'existe pas.</summary>
    EncryptedSecret? GetEnforcedSecret(int id);

    /// <summary>Liste les identifiants imposés (métadonnées uniquement, aucun secret).</summary>
    IReadOnlyList<EnforcedCredential> ListEnforced();
}
