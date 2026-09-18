using Microsoft.Data.Sqlite;

namespace KyWigRemote.Core.Data;

/// <summary>Implémentation SQLite de <see cref="ISettingsStore"/> (table <c>settings</c>).</summary>
public sealed class SqliteSettingsStore : ISettingsStore
{
    private readonly Database _database;

    public SqliteSettingsStore(Database database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public string? Get(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = $k;";
        command.Parameters.AddWithValue("$k", key);
        object? result = command.ExecuteScalar();
        return result is null or DBNull ? null : (string)result;
    }

    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);
        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO settings (key, value) VALUES ($k, $v) " +
            "ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
        command.Parameters.AddWithValue("$k", key);
        command.Parameters.AddWithValue("$v", value);
        command.ExecuteNonQuery();
    }
}
