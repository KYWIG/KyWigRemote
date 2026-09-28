using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Security;

/// <summary>
/// Identité d'un utilisateur authentifié, indépendante du fournisseur (compte local,
/// AD ou Microsoft 365). Ne contient jamais de secret.
/// </summary>
/// <param name="Username">Identifiant de connexion.</param>
/// <param name="DisplayName">Nom d'affichage, s'il est connu.</param>
/// <param name="Role">Profil d'accès de l'utilisateur.</param>
/// <param name="Provider">Fournisseur ayant validé l'identité (« Local », « ActiveDirectory », « Microsoft365 »).</param>
public sealed record AuthenticatedUser(string Username, string? DisplayName, UserRole Role, string Provider)
{
    /// <summary>Administrateur global (gère tout).</summary>
    public bool IsGlobalAdmin => Role == UserRole.GlobalAdmin;

    /// <summary>Peut gérer les connexions (Administrateur des connexions ou global).</summary>
    public bool CanManageConnections => Role >= UserRole.ConnectionAdmin;
}
