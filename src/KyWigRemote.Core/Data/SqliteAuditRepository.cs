using System.Text;
using Microsoft.Data.Sqlite;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Data;

/// <summary>Implémentation SQLite de <see cref="IAuditRepository"/> (table audit_log).</summary>
public sealed class SqliteAuditRepository : IAuditRepository
{
    private readonly Database _database;

    public SqliteAuditRepository(Database database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public void Append(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO audit_log " +
            "(occurred_at, user_name, action, target_type, target_id, credential_mode, result, details) " +
            "VALUES ($at, $user, $action, $ttype, $tid, $mode, $result, $details);";
        command.Parameters.AddWithValue("$at", auditEvent.OccurredAt.ToString("O"));
        command.Parameters.AddWithValue("$user", (object?)auditEvent.UserName ?? DBNull.Value);
        command.Parameters.AddWithValue("$action", auditEvent.Action);
        command.Parameters.AddWithValue("$ttype", (object?)auditEvent.TargetType ?? DBNull.Value);
        command.Parameters.AddWithValue("$tid", (object?)auditEvent.TargetId ?? DBNull.Value);
        command.Parameters.AddWithValue("$mode", (object?)auditEvent.CredentialMode ?? DBNull.Value);
        command.Parameters.AddWithValue("$result", (object?)auditEvent.Result ?? DBNull.Value);
        command.Parameters.AddWithValue("$details", (object?)auditEvent.Details ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<AuditEvent> Query(AuditQuery filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var sql = new StringBuilder(
            "SELECT id, occurred_at, user_name, action, target_type, target_id, credential_mode, result, details " +
            "FROM audit_log WHERE 1 = 1");

        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();

        if (!string.IsNullOrWhiteSpace(filter.UserName))
        {
            sql.Append(" AND user_name = $user COLLATE NOCASE");
            command.Parameters.AddWithValue("$user", filter.UserName);
        }
        if (!string.IsNullOrWhiteSpace(filter.Result))
        {
            sql.Append(" AND result = $result");
            command.Parameters.AddWithValue("$result", filter.Result);
        }
        if (filter.From is { } from)
        {
            sql.Append(" AND occurred_at >= $from");
            command.Parameters.AddWithValue("$from", from.ToString("O"));
        }
        if (filter.To is { } to)
        {
            sql.Append(" AND occurred_at <= $to");
            command.Parameters.AddWithValue("$to", to.ToString("O"));
        }

        sql.Append(" ORDER BY occurred_at DESC, id DESC LIMIT $limit;");
        command.Parameters.AddWithValue("$limit", filter.Limit);
        command.CommandText = sql.ToString();

        var events = new List<AuditEvent>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            events.Add(new AuditEvent
            {
                Id = reader.GetInt64(0),
                OccurredAt = DateTimeOffset.Parse(reader.GetString(1)),
                UserName = reader.IsDBNull(2) ? null : reader.GetString(2),
                Action = reader.GetString(3),
                TargetType = reader.IsDBNull(4) ? null : reader.GetString(4),
                TargetId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                CredentialMode = reader.IsDBNull(6) ? null : reader.GetString(6),
                Result = reader.IsDBNull(7) ? null : reader.GetString(7),
                Details = reader.IsDBNull(8) ? null : reader.GetString(8),
            });
        }
        return events;
    }
}
