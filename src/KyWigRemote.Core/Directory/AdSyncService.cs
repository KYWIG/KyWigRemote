using KyWigRemote.Core.Data;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Directory;

/// <summary>Bilan d'une synchronisation AD.</summary>
/// <param name="Imported">Nouveaux utilisateurs ajoutés.</param>
/// <param name="Updated">Utilisateurs déjà connus, mis à jour.</param>
/// <param name="Deactivated">Utilisateurs désactivés (absents des groupes cette fois).</param>
public sealed record AdSyncOutcome(int Imported, int Updated, int Deactivated);

/// <summary>
/// Synchronise les utilisateurs des trois groupes AD vers la table <c>users</c>. Le profil d'un
/// utilisateur est celui du groupe le plus privilégié auquel il appartient (Admin global >
/// Admin des connexions > Utilisateur). Les utilisateurs AD absents de la synchronisation
/// courante sont désactivés (jamais supprimés).
/// </summary>
public sealed class AdSyncService
{
    private readonly IDirectory _directory;
    private readonly IUserRepository _users;

    public AdSyncService(IDirectory directory, IUserRepository users)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _users = users ?? throw new ArgumentNullException(nameof(users));
    }

    /// <summary>Exécute la synchronisation et retourne le bilan. Lève si l'annuaire est injoignable.</summary>
    public AdSyncOutcome Synchronize(string userGroup, string connectionAdminGroup, string adminGroup)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Profil retenu par SID = le plus privilégié rencontré.
        var byRole = new Dictionary<string, (DirectoryMember Member, UserRole Role)>(StringComparer.OrdinalIgnoreCase);
        Collect(adminGroup, UserRole.GlobalAdmin, byRole);
        Collect(connectionAdminGroup, UserRole.ConnectionAdmin, byRole);
        Collect(userGroup, UserRole.User, byRole);

        int imported = 0, updated = 0;
        foreach ((DirectoryMember member, UserRole role) in byRole.Values)
        {
            bool isNew = _users.UpsertSynced(member.Sid, member.SamAccountName, member.DisplayName, role, now);
            if (isNew) { imported++; } else { updated++; }
        }

        int deactivated = _users.DeactivateAdUsersNotSyncedSince(now);
        return new AdSyncOutcome(imported, updated, deactivated);
    }

    private void Collect(string group, UserRole role, Dictionary<string, (DirectoryMember, UserRole)> map)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            return;
        }
        foreach (DirectoryMember member in _directory.GetGroupMembers(group))
        {
            if (!map.TryGetValue(member.Sid, out (DirectoryMember, UserRole) existing) || role > existing.Item2)
            {
                map[member.Sid] = (member, role);
            }
        }
    }
}
