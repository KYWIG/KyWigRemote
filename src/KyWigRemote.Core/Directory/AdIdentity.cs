using System.DirectoryServices.AccountManagement;
using System.Security.Principal;

namespace KyWigRemote.Core.Directory;

/// <summary>
/// Résout l'identité Active Directory de la session Windows courante (E3.1).
/// Le SID et le nom de connexion proviennent du jeton Windows (aucun accès AD requis) ;
/// le nom d'affichage est cherché dans l'annuaire, au mieux.
/// </summary>
public static class AdIdentity
{
    /// <summary>
    /// Retourne l'identité de l'utilisateur exécutant le processus courant.
    /// Le nom d'affichage vaut null si l'annuaire n'est pas joignable.
    /// </summary>
    public static WindowsUser Current()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();

        string sid = identity.User?.Value ?? string.Empty;
        string domain = string.Empty;
        string sam = identity.Name; // « DOMAINE\utilisateur »
        int separator = sam.IndexOf('\\');
        if (separator >= 0)
        {
            domain = sam[..separator];
            sam = sam[(separator + 1)..];
        }

        return new WindowsUser(sid, sam, TryResolveDisplayName(sam), domain);
    }

    /// <summary>
    /// Cherche le nom d'affichage d'un compte dans l'annuaire du domaine ; retourne null
    /// si l'annuaire est injoignable ou le compte introuvable (jamais d'exception propagée).
    /// </summary>
    public static string? TryResolveDisplayName(string samAccountName)
    {
        if (string.IsNullOrWhiteSpace(samAccountName))
        {
            return null;
        }

        try
        {
            using var context = new PrincipalContext(ContextType.Domain);
            using UserPrincipal? user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, samAccountName);
            return user?.DisplayName;
        }
        catch (Exception ex) when (ex is PrincipalException or InvalidOperationException or SystemException)
        {
            // Hors domaine ou annuaire injoignable : on se contente du nom de connexion.
            return null;
        }
    }
}
