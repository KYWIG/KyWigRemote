namespace KyWigRemote.Core.Model;

/// <summary>
/// Mode de résolution des identifiants d'une connexion ou d'un dossier.
/// Les valeurs correspondent à la contrainte CHECK du schéma SQL
/// (voir docs/03-architecture.md, tables folders et connections).
/// </summary>
public enum CredentialMode
{
    /// <summary>Identifiant propre à l'utilisateur, chiffré DPAPI pour lui seul.</summary>
    Personal,

    /// <summary>Identifiant partagé imposé par l'administrateur, chiffré par enveloppe.</summary>
    Enforced,

    /// <summary>Rien n'est stocké : saisie au lancement de la session.</summary>
    Prompt,

    /// <summary>La connexion reprend le mode du dossier parent.</summary>
    Inherited,
}
