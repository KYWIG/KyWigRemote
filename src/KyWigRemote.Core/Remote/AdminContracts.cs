namespace KyWigRemote.Core.Remote;

/// <summary>
/// Vue d'un compte local exposée à l'administration. Ne contient jamais le hachage
/// du mot de passe.
/// </summary>
/// <param name="Id">Identifiant du compte.</param>
/// <param name="Username">Identifiant de connexion.</param>
/// <param name="DisplayName">Nom d'affichage.</param>
/// <param name="IsAdmin">Le compte est administrateur.</param>
/// <param name="Disabled">Le compte est désactivé.</param>
public sealed record LocalAccountSummary(int Id, string Username, string? DisplayName, bool IsAdmin, bool Disabled);

/// <summary>Demande de création d'un compte local depuis l'administration.</summary>
/// <param name="Username">Identifiant souhaité.</param>
/// <param name="Password">Mot de passe initial (haché côté serveur, jamais stocké en clair).</param>
/// <param name="DisplayName">Nom d'affichage (facultatif).</param>
/// <param name="IsAdmin">Attribuer les droits d'administration.</param>
public sealed record CreateLocalAccountRequest(string Username, string Password, string? DisplayName, bool IsAdmin);
