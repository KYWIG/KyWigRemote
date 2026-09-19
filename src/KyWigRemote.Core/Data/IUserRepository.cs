using KyWigRemote.Core.Directory;

namespace KyWigRemote.Core.Data;

/// <summary>
/// Enregistre les utilisateurs Active Directory connus (table <c>users</c>), identifiés par
/// leur SID stable. Mis à jour à chaque connexion (E3.5, <c>last_seen_at</c>).
/// </summary>
public interface IUserRepository
{
    /// <summary>Crée l'utilisateur s'il est inconnu, sinon met à jour ses informations et sa dernière vue.</summary>
    void Upsert(WindowsUser user);

    /// <summary>Retourne l'utilisateur enregistré pour ce SID, ou null.</summary>
    WindowsUser? GetBySid(string sid);
}
