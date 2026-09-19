namespace KyWigRemote.Core.Directory;

/// <summary>
/// Vérificateur de groupes qui refuse tout, utilisé quand l'authentification Active Directory
/// n'est pas activée : aucun accès n'est accordé par groupe (seul l'administrateur passe).
/// </summary>
public sealed class DenyAllGroupChecker : IGroupChecker
{
    public bool IsMemberOf(string samAccountName, string groupName) => false;
}
