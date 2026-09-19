using System.DirectoryServices.AccountManagement;

namespace KyWigRemote.Core.Directory;

/// <summary>
/// Vérifie l'appartenance à un groupe via Active Directory
/// (<see cref="System.DirectoryServices.AccountManagement"/>). Nécessite l'accès au domaine.
/// La mise en cache est assurée séparément par <see cref="CachingGroupChecker"/>.
/// </summary>
public sealed class AdGroupChecker : IGroupChecker
{
    private readonly string? _domain;

    /// <param name="domain">Domaine à interroger, ou null pour le domaine du poste.</param>
    public AdGroupChecker(string? domain = null)
    {
        _domain = string.IsNullOrWhiteSpace(domain) ? null : domain;
    }

    public bool IsMemberOf(string samAccountName, string groupName)
    {
        if (string.IsNullOrWhiteSpace(samAccountName) || string.IsNullOrWhiteSpace(groupName))
        {
            return false;
        }

        using PrincipalContext context = _domain is null
            ? new PrincipalContext(ContextType.Domain)
            : new PrincipalContext(ContextType.Domain, _domain);

        using UserPrincipal? user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, samAccountName);
        if (user is null)
        {
            return false;
        }

        using GroupPrincipal? group = GroupPrincipal.FindByIdentity(context, IdentityType.SamAccountName, groupName);
        if (group is null)
        {
            return false;
        }

        // IsMemberOf couvre l'imbrication des groupes (membre indirect).
        return user.IsMemberOf(group);
    }
}
