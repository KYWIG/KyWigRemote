namespace KyWigRemote.Core.Directory;

/// <summary>
/// Membre d'un groupe Active Directory, tel que retourné par l'énumération lors de la
/// synchronisation. Le SID est l'identifiant stable.
/// </summary>
/// <param name="Sid">Identifiant de sécurité (stable).</param>
/// <param name="SamAccountName">Nom de connexion.</param>
/// <param name="DisplayName">Nom d'affichage, s'il est connu.</param>
public sealed record DirectoryMember(string Sid, string SamAccountName, string? DisplayName);
