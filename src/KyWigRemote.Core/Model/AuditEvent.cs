namespace KyWigRemote.Core.Model;

/// <summary>
/// Événement du journal d'audit (table audit_log). Ne contient JAMAIS de secret :
/// on journalise des identifiants d'objets et des libellés, jamais des mots de passe
/// (règle de sécurité nº 2, FR-44).
/// </summary>
public sealed class AuditEvent
{
    /// <summary>Identifiant en base ; 0 tant que non persisté.</summary>
    public long Id { get; set; }

    /// <summary>Horodatage de l'événement (UTC).</summary>
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Nom de l'utilisateur concerné, s'il est connu.</summary>
    public string? UserName { get; set; }

    /// <summary>Action : LOGIN, SESSION_OPEN, REVEAL_ENFORCED, CONN_CREATE, FOLDER_CREATE…</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Type de cible : CONNECTION, FOLDER, CREDENTIAL, ACCOUNT.</summary>
    public string? TargetType { get; set; }

    /// <summary>Identifiant de la cible.</summary>
    public int? TargetId { get; set; }

    /// <summary>Mode d'identifiants en jeu (pour l'ouverture de session).</summary>
    public string? CredentialMode { get; set; }

    /// <summary>Résultat : OK, DENIED, ERROR.</summary>
    public string? Result { get; set; }

    /// <summary>Détail lisible, sans aucun secret.</summary>
    public string? Details { get; set; }
}
