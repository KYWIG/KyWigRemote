using KyWigRemote.Core.Model;

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

/// <summary>Demande de création d'un dossier dans l'arborescence.</summary>
/// <param name="Name">Nom du dossier.</param>
/// <param name="ParentId">Dossier parent (null = racine).</param>
/// <param name="CredentialMode">Mode d'identifiants du dossier.</param>
public sealed record CreateFolderRequest(string Name, int? ParentId, CredentialMode CredentialMode);

/// <summary>Demande de création d'une connexion dans un dossier.</summary>
public sealed record CreateConnectionRequest(
    int FolderId,
    string Name,
    RemoteProtocol Protocol,
    string Host,
    int Port,
    string? Domain,
    string? Description,
    CredentialMode CredentialMode,
    int? EnforcedCredentialId);

/// <summary>
/// Identifiant révélé pour ouvrir une session (mode imposé). Contient le secret en clair :
/// à n'utiliser qu'au moment d'ouvrir la session, puis à lâcher.
/// </summary>
public sealed record RevealedCredential(string Username, string? Domain, string Secret);

/// <summary>Réponse minimale d'une création : l'identifiant attribué.</summary>
public sealed record CreatedId(int Id);

/// <summary>Demande de création d'un identifiant imposé. Le secret est chiffré côté serveur.</summary>
/// <param name="Label">Libellé lisible.</param>
/// <param name="Username">Nom d'utilisateur du compte.</param>
/// <param name="Domain">Domaine (facultatif).</param>
/// <param name="Secret">Mot de passe en clair (chiffré immédiatement, jamais stocké tel quel).</param>
/// <param name="AllowedGroups">Groupes AD autorisés, séparés par « ; » (facultatif).</param>
public sealed record CreateEnforcedCredentialRequest(
    string Label, string Username, string? Domain, string Secret, string? AllowedGroups);

/// <summary>Vue d'un identifiant imposé pour l'administration : jamais de secret.</summary>
public sealed record EnforcedCredentialSummary(
    int Id, string Label, string Username, string? Domain, string? AllowedGroups);
