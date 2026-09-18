using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Data;

/// <summary>
/// Accès aux dossiers et connexions de l'arborescence partagée.
/// L'interface isole le reste de l'application du stockage : basculer vers SQL Server
/// (porte de sortie d'ADR-003) revient à écrire une autre implémentation.
/// </summary>
public interface IConnectionRepository
{
    /// <summary>Charge l'arborescence complète, dossiers imbriqués et connexions.</summary>
    IReadOnlyList<ConnectionFolder> GetTree();

    /// <summary>Ajoute un dossier sous le parent indiqué (null = racine) et retourne son identifiant.</summary>
    int AddFolder(ConnectionFolder folder, int? parentId);

    /// <summary>Met à jour le nom et le mode d'identifiants d'un dossier existant.</summary>
    void UpdateFolder(ConnectionFolder folder);

    /// <summary>Supprime un dossier ; ses sous-dossiers sont supprimés en cascade.</summary>
    void DeleteFolder(int id);

    /// <summary>Ajoute une connexion dans le dossier indiqué et retourne son identifiant.</summary>
    int AddConnection(RemoteConnection connection, int folderId);

    /// <summary>Met à jour une connexion existante.</summary>
    void UpdateConnection(RemoteConnection connection);

    /// <summary>Supprime une connexion.</summary>
    void DeleteConnection(int id);
}
