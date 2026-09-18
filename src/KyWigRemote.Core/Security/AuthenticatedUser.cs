namespace KyWigRemote.Core.Security;

/// <summary>
/// Identité d'un utilisateur authentifié, indépendante du fournisseur (compte local,
/// AD ou Microsoft 365). Ne contient jamais de secret.
/// </summary>
/// <param name="Username">Identifiant de connexion.</param>
/// <param name="DisplayName">Nom d'affichage, s'il est connu.</param>
/// <param name="IsAdmin">Dispose des droits d'administration.</param>
/// <param name="Provider">Fournisseur ayant validé l'identité (« Local », « ActiveDirectory », « Microsoft365 »).</param>
public sealed record AuthenticatedUser(string Username, string? DisplayName, bool IsAdmin, string Provider);
