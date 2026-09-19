using KyWigRemote.Core.Directory;

namespace KyWigRemote.Core.Security;

/// <summary>
/// Décide si un utilisateur a le droit d'accéder à un identifiant imposé selon les groupes
/// AD autorisés (FR-15, E4.7). Logique pure : l'accès réel à l'annuaire est délégué à
/// <see cref="IGroupChecker"/>.
/// </summary>
public static class EnforcedCredentialAuthorizer
{
    /// <summary>
    /// Indique si l'utilisateur appartient à au moins un des groupes autorisés.
    /// Retourne false si aucun groupe n'est défini : un identifiant imposé n'est accessible
    /// par les groupes que si une liste est explicitement fixée (l'administrateur garde un
    /// accès par ailleurs).
    /// </summary>
    /// <param name="samAccountName">Compte de l'utilisateur.</param>
    /// <param name="allowedGroups">Groupes autorisés, séparés par « ; » (peut être vide/null).</param>
    /// <param name="groups">Vérificateur d'appartenance (AD, éventuellement mis en cache).</param>
    public static bool IsAllowedByGroups(string samAccountName, string? allowedGroups, IGroupChecker groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        if (string.IsNullOrWhiteSpace(samAccountName) || string.IsNullOrWhiteSpace(allowedGroups))
        {
            return false;
        }

        string[] list = allowedGroups.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string group in list)
        {
            if (groups.IsMemberOf(samAccountName, group))
            {
                return true;
            }
        }
        return false;
    }
}
