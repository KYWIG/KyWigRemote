using Microsoft.Data.Sqlite;
using KyWigRemote.Core.Directory;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Data;

/// <summary>Implémentation SQLite de <see cref="IUserRepository"/> (table <c>users</c>).</summary>
public sealed class SqliteUserRepository : IUserRepository
{
    private readonly Database _database;

    public SqliteUserRepository(Database database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public void Upsert(WindowsUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(user.Sid);

        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO users (sid, sam_account, display_name, enrolled_at, last_seen_at) " +
            "VALUES ($sid, $sam, $display, $now, $now) " +
            "ON CONFLICT(sid) DO UPDATE SET sam_account = excluded.sam_account, " +
            "display_name = excluded.display_name, last_seen_at = excluded.last_seen_at;";
        command.Parameters.AddWithValue("$sid", user.Sid);
        command.Parameters.AddWithValue("$sam", user.SamAccountName);
        command.Parameters.AddWithValue("$display", (object?)user.DisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public WindowsUser? GetBySid(string sid)
    {
        ArgumentException.ThrowIfNullOrEmpty(sid);

        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT sid, sam_account, display_name FROM users WHERE sid = $sid;";
        command.Parameters.AddWithValue("$sid", sid);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }
        return new WindowsUser(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            null);
    }

    public bool UpsertSynced(string sid, string samAccountName, string? displayName, UserRole role, DateTimeOffset syncedAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(sid);
        ArgumentException.ThrowIfNullOrEmpty(samAccountName);

        using SqliteConnection connection = _database.OpenConnection();

        bool existed;
        using (SqliteCommand check = connection.CreateCommand())
        {
            check.CommandText = "SELECT EXISTS (SELECT 1 FROM users WHERE sid = $sid);";
            check.Parameters.AddWithValue("$sid", sid);
            existed = Convert.ToInt64(check.ExecuteScalar()) != 0;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO users (sid, sam_account, display_name, role, source, active, last_synced_at, enrolled_at, last_seen_at) " +
            "VALUES ($sid, $sam, $display, $role, 'AD', 1, $now, $now, $now) " +
            "ON CONFLICT(sid) DO UPDATE SET sam_account = excluded.sam_account, " +
            "display_name = excluded.display_name, role = excluded.role, source = 'AD', " +
            "active = 1, last_synced_at = excluded.last_synced_at;";
        command.Parameters.AddWithValue("$sid", sid);
        command.Parameters.AddWithValue("$sam", samAccountName);
        command.Parameters.AddWithValue("$display", (object?)displayName ?? DBNull.Value);
        command.Parameters.AddWithValue("$role", role.ToString());
        command.Parameters.AddWithValue("$now", syncedAt.ToString("O"));
        command.ExecuteNonQuery();

        return !existed;
    }

    public int DeactivateAdUsersNotSyncedSince(DateTimeOffset cutoff)
    {
        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "UPDATE users SET active = 0 WHERE source = 'AD' AND active = 1 " +
            "AND (last_synced_at IS NULL OR last_synced_at < $cutoff);";
        command.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
        return command.ExecuteNonQuery();
    }

    public IReadOnlyList<AdUserRecord> ListAdUsers()
    {
        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT sid, sam_account, display_name, role, active, last_synced_at " +
            "FROM users WHERE source = 'AD' ORDER BY sam_account COLLATE NOCASE;";

        var users = new List<AdUserRecord>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new AdUserRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                Enum.TryParse(reader.GetString(3), ignoreCase: true, out UserRole role) ? role : UserRole.User,
                reader.GetInt32(4) != 0,
                reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5))));
        }
        return users;
    }
}
