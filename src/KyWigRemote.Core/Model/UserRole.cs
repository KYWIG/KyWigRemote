namespace KyWigRemote.Core.Model;

/// <summary>
/// Profil d'accès d'un utilisateur (comptes AD comme comptes locaux). Ordonné du moins
/// au plus privilégié : un profil supérieur inclut les droits des inférieurs.
/// </summary>
public enum UserRole
{
    /// <summary>Utilisateur : ouvre la console de connexion et se connecte aux ressources autorisées.</summary>
    User = 0,

    /// <summary>Administrateur des connexions : User + gestion des dossiers et connexions.</summary>
    ConnectionAdmin = 1,

    /// <summary>Administrateur global : gère tout (comptes, AD, identifiants imposés, audit, connexions).</summary>
    GlobalAdmin = 2,
}
