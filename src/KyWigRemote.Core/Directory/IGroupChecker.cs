namespace KyWigRemote.Core.Directory;

/// <summary>
/// Teste l'appartenance d'un utilisateur à un groupe Active Directory.
/// L'interface permet de remplacer l'accès AD réel par un cache ou un double de test.
/// </summary>
public interface IGroupChecker
{
    /// <summary>
    /// Indique si l'utilisateur (nom de connexion sAMAccountName) est membre du groupe indiqué,
    /// directement ou par imbrication de groupes.
    /// </summary>
    bool IsMemberOf(string samAccountName, string groupName);
}
