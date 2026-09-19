using Microsoft.Data.Sqlite;
using KyWigRemote.Core.Directory;

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
}
