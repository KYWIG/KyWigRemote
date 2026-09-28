namespace KyWigRemote.Core.Directory;

/// <summary>
/// Accès à l'annuaire Active Directory : vérification d'appartenance (héritée de
/// <see cref="IGroupChecker"/>), énumération des membres d'un groupe (pour la synchronisation)
/// et test de la connexion (compte de service). Remplaçable par un double en test.
/// </summary>
public interface IDirectory : IGroupChecker
{
    /// <summary>
    /// Retourne les membres (utilisateurs) d'un groupe, imbrication comprise. Liste vide si le
    /// groupe est introuvable ; lève en cas d'annuaire injoignable.
    /// </summary>
    IReadOnlyList<DirectoryMember> GetGroupMembers(string groupName);

    /// <summary>Teste la connexion à l'annuaire avec les identifiants configurés (true si OK).</summary>
    bool TestConnection();
}
