using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Directory;

/// <summary>Utilisateur AD synchronisé, tel que stocké dans la table <c>users</c>.</summary>
/// <param name="Sid">Identifiant de sécurité (stable).</param>
/// <param name="SamAccountName">Nom de connexion.</param>
/// <param name="DisplayName">Nom d'affichage.</param>
/// <param name="Role">Profil déduit de l'appartenance aux groupes.</param>
/// <param name="Active">Encore présent dans les groupes lors de la dernière synchronisation.</param>
/// <param name="LastSyncedAt">Date de la dernière synchronisation.</param>
public sealed record AdUserRecord(
    string Sid, string SamAccountName, string? DisplayName, UserRole Role, bool Active, DateTimeOffset? LastSyncedAt);
