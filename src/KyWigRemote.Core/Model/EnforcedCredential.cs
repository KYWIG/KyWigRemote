namespace KyWigRemote.Core.Model;

/// <summary>
/// Métadonnées d'un identifiant imposé (partagé). Ne contient jamais le secret :
/// celui-ci est stocké chiffré à part et n'est déchiffré qu'au moment d'ouvrir une session.
/// </summary>
public sealed class EnforcedCredential
{
    /// <summary>Identifiant en base ; 0 tant que non persisté.</summary>
    public int Id { get; set; }

    /// <summary>Libellé lisible du compte (ex. « Compte admin switchs »).</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Nom d'utilisateur du compte imposé.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Domaine d'ouverture de session (facultatif).</summary>
    public string? Domain { get; set; }

    /// <summary>Groupes AD autorisés, séparés par « ; » (contrôle d'accès, appliqué plus haut).</summary>
    public string? AllowedGroups { get; set; }
}
