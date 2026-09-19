namespace KyWigRemote.Core.Directory;

/// <summary>
/// Identité d'un utilisateur Windows / Active Directory. Le SID est l'identifiant stable
/// (il ne change pas si le compte est renommé) ; il sert de clé dans la table <c>users</c>.
/// </summary>
/// <param name="Sid">Identifiant de sécurité (stable).</param>
/// <param name="SamAccountName">Nom de connexion (ex. « n.marchand_t1 »).</param>
/// <param name="DisplayName">Nom d'affichage, s'il est connu.</param>
/// <param name="Domain">Domaine du compte (ex. « KYWIG »).</param>
public sealed record WindowsUser(string Sid, string SamAccountName, string? DisplayName, string? Domain);
