using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Security;

/// <summary>
/// Résout le mode d'identifiants effectif d'une connexion (algorithme du §5 de
/// docs/03-architecture.md). C'est le cœur du projet : logique pure, sans effet de bord,
/// pour être couverte au maximum par les tests (E4.11).
///
/// Règle : le mode de la connexion prime ; s'il vaut HÉRITÉ, on remonte les dossiers
/// parents (du plus proche au plus lointain) jusqu'au premier mode explicite ; si aucun
/// n'est trouvé, le mode effectif est DEMANDE.
/// </summary>
public static class CredentialResolver
{
    /// <summary>
    /// Résout le mode effectif à partir du mode de la connexion et de la chaîne de dossiers
    /// parents, ordonnée du plus proche au plus lointain. Le résultat n'est jamais HÉRITÉ.
    /// </summary>
    public static CredentialMode ResolveEffectiveMode(
        RemoteConnection connection, IReadOnlyList<ConnectionFolder> ancestorsNearestFirst)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(ancestorsNearestFirst);

        if (connection.CredentialMode != CredentialMode.Inherited)
        {
            return connection.CredentialMode;
        }

        foreach (ConnectionFolder folder in ancestorsNearestFirst)
        {
            if (folder.CredentialMode != CredentialMode.Inherited)
            {
                return folder.CredentialMode;
            }
        }

        // Aucun mode explicite dans toute la chaîne : on demande au lancement.
        return CredentialMode.Prompt;
    }

    /// <summary>
    /// Variante pratique : résout le mode effectif d'une connexion en la localisant dans
    /// l'arborescence fournie (pour retrouver ses dossiers parents).
    /// </summary>
    public static CredentialMode ResolveEffectiveMode(
        IReadOnlyList<ConnectionFolder> roots, RemoteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(connection);

        IReadOnlyList<ConnectionFolder> ancestors =
            FindAncestors(roots, connection.Id) ?? Array.Empty<ConnectionFolder>();
        return ResolveEffectiveMode(connection, ancestors);
    }

    /// <summary>
    /// Retourne les dossiers parents d'une connexion, du plus proche au plus lointain,
    /// ou null si la connexion est introuvable dans l'arborescence.
    /// </summary>
    public static IReadOnlyList<ConnectionFolder>? FindAncestors(
        IReadOnlyList<ConnectionFolder> roots, int connectionId)
    {
        ArgumentNullException.ThrowIfNull(roots);

        foreach (ConnectionFolder root in roots)
        {
            var path = new List<ConnectionFolder>();
            if (SearchConnection(root, connectionId, path))
            {
                return path; // rempli du plus proche (parent direct) au plus lointain
            }
        }
        return null;
    }

    private static bool SearchConnection(ConnectionFolder folder, int connectionId, List<ConnectionFolder> path)
    {
        foreach (RemoteConnection connection in folder.Connections)
        {
            if (connection.Id == connectionId)
            {
                path.Add(folder);
                return true;
            }
        }
        foreach (ConnectionFolder sub in folder.SubFolders)
        {
            if (SearchConnection(sub, connectionId, path))
            {
                path.Add(folder); // ajouté en remontant : garde l'ordre du plus proche au plus lointain
                return true;
            }
        }
        return false;
    }
}
