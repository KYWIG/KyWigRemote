using KyWigRemote.Core.Directory;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Data;

/// <summary>
/// Enregistre les utilisateurs Active Directory connus (table <c>users</c>), identifiés par
/// leur SID stable. Mis à jour à chaque connexion (E3.5, <c>last_seen_at</c>) et lors de la
/// synchronisation des groupes AD.
/// </summary>
public interface IUserRepository
{
    /// <summary>Crée l'utilisateur s'il est inconnu, sinon met à jour ses informations et sa dernière vue.</summary>
    void Upsert(WindowsUser user);

    /// <summary>Retourne l'utilisateur enregistré pour ce SID, ou null.</summary>
    WindowsUser? GetBySid(string sid);

    /// <summary>
    /// Enregistre (ou met à jour) un utilisateur issu de la synchronisation AD : profil, actif,
    /// date de synchronisation. Retourne <c>true</c> s'il s'agit d'un nouvel utilisateur.
    /// </summary>
    bool UpsertSynced(string sid, string samAccountName, string? displayName, UserRole role, DateTimeOffset syncedAt);

    /// <summary>
    /// Désactive les utilisateurs AD dont la dernière synchronisation est antérieure au seuil
    /// (donc absents de la synchronisation courante). Retourne le nombre de désactivations.
    /// </summary>
    int DeactivateAdUsersNotSyncedSince(DateTimeOffset cutoff);

    /// <summary>Liste les utilisateurs AD synchronisés (actifs et inactifs).</summary>
    IReadOnlyList<AdUserRecord> ListAdUsers();
}
