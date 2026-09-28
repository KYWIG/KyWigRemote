using System.DirectoryServices.AccountManagement;

namespace KyWigRemote.Core.Directory;

/// <summary>
/// Accès réel à Active Directory via <see cref="System.DirectoryServices.AccountManagement"/>.
/// Si un compte de service (identifiant + mot de passe) est fourni, toutes les requêtes se font
/// sous cette identité ; sinon, sous l'identité du processus (utile si le service tourne déjà
/// sous un compte de domaine). La mise en cache des appartenances est assurée séparément par
/// <see cref="CachingGroupChecker"/>.
/// </summary>
public sealed class AdDirectory : IDirectory
{
    private readonly string? _domain;
    private readonly string? _username;
    private readonly string? _password;

    /// <param name="domain">Domaine à interroger, ou null pour le domaine du poste.</param>
    /// <param name="username">Compte de service (ou null pour l'identité du processus).</param>
    /// <param name="password">Mot de passe du compte de service.</param>
    public AdDirectory(string? domain = null, string? username = null, string? password = null)
    {
        _domain = string.IsNullOrWhiteSpace(domain) ? null : domain;
        _username = string.IsNullOrWhiteSpace(username) ? null : username;
        _password = password;
    }

    private PrincipalContext CreateContext()
    {
        if (_username is not null)
        {
            return new PrincipalContext(ContextType.Domain, _domain, container: null, _username, _password);
        }
        return _domain is null
            ? new PrincipalContext(ContextType.Domain)
            : new PrincipalContext(ContextType.Domain, _domain);
    }

    public bool IsMemberOf(string samAccountName, string groupName)
    {
        if (string.IsNullOrWhiteSpace(samAccountName) || string.IsNullOrWhiteSpace(groupName))
        {
            return false;
        }

        using PrincipalContext context = CreateContext();
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

    public IReadOnlyList<DirectoryMember> GetGroupMembers(string groupName)
    {
        var members = new List<DirectoryMember>();
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return members;
        }

        using PrincipalContext context = CreateContext();
        using GroupPrincipal? group = GroupPrincipal.FindByIdentity(context, IdentityType.SamAccountName, groupName);
        if (group is null)
        {
            return members;
        }

        // recursive: true pour inclure les membres des sous-groupes.
        foreach (Principal principal in group.GetMembers(recursive: true))
        {
            using (principal)
            {
                if (principal is UserPrincipal user)
                {
                    string? sid = user.Sid?.Value;
                    string? sam = user.SamAccountName;
                    if (!string.IsNullOrEmpty(sid) && !string.IsNullOrEmpty(sam))
                    {
                        members.Add(new DirectoryMember(sid, sam, user.DisplayName));
                    }
                }
            }
        }
        return members;
    }

    public bool TestConnection()
    {
        try
        {
            using PrincipalContext context = CreateContext();
            // L'accès à ConnectedServer force la liaison à l'annuaire (échoue si identifiants invalides).
            return context.ConnectedServer is not null;
        }
        catch (Exception ex) when (ex is PrincipalException or SystemException)
        {
            return false;
        }
    }
}
