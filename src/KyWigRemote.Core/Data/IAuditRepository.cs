using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Data;

/// <summary>Écriture et lecture filtrée du journal d'audit (E9).</summary>
public interface IAuditRepository
{
    /// <summary>Ajoute un événement au journal.</summary>
    void Append(AuditEvent auditEvent);

    /// <summary>Retourne les événements correspondant au filtre, du plus récent au plus ancien.</summary>
    IReadOnlyList<AuditEvent> Query(AuditQuery filter);

    /// <summary>
    /// Supprime les événements antérieurs à la date indiquée (rétention, E9.5) et retourne
    /// le nombre d'événements supprimés.
    /// </summary>
    int PurgeOlderThan(DateTimeOffset cutoff);
}

/// <summary>Filtre de consultation du journal d'audit (tous les critères sont facultatifs).</summary>
public sealed class AuditQuery
{
    /// <summary>Filtrer sur un utilisateur.</summary>
    public string? UserName { get; set; }

    /// <summary>Filtrer sur un résultat (OK, DENIED, ERROR).</summary>
    public string? Result { get; set; }

    /// <summary>Ne garder que les événements à partir de cette date.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Ne garder que les événements jusqu'à cette date.</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>Nombre maximum d'événements retournés.</summary>
    public int Limit { get; set; } = 500;
}
