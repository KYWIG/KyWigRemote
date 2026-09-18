using Microsoft.Data.Sqlite;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Data;

/// <summary>Implémentation SQLite de <see cref="ILocalAccountRepository"/>.</summary>
public sealed class SqliteLocalAccountRepository : ILocalAccountRepository
{
    private readonly Database _database;

    public SqliteLocalAccountRepository(Database database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public bool HasAnyAccount()
    {
        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM local_accounts);";
        return Convert.ToInt64(command.ExecuteScalar()) != 0;
    }

    public IReadOnlyList<LocalAccount> ListAccounts()
    {
        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, username, display_name, password_hash, is_admin, disabled " +
            "FROM local_accounts ORDER BY username COLLATE NOCASE;";

        var accounts = new List<LocalAccount>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            accounts.Add(new LocalAccount
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                DisplayName = reader.IsDBNull(2) ? null : reader.GetString(2),
                PasswordHash = reader.GetString(3),
                IsAdmin = reader.GetInt32(4) != 0,
                Disabled = reader.GetInt32(5) != 0,
            });
        }
        return accounts;
    }

    public LocalAccount? FindByUsername(string username)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);

        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, username, display_name, password_hash, is_admin, disabled " +
            "FROM local_accounts WHERE username = $u COLLATE NOCASE;";
        command.Parameters.AddWithValue("$u", username);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new LocalAccount
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            DisplayName = reader.IsDBNull(2) ? null : reader.GetString(2),
            PasswordHash = reader.GetString(3),
            IsAdmin = reader.GetInt32(4) != 0,
            Disabled = reader.GetInt32(5) != 0,
        };
    }

    public int CreateAccount(LocalAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrEmpty(account.Username);
        ArgumentException.ThrowIfNullOrEmpty(account.PasswordHash);

        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO local_accounts (username, display_name, password_hash, is_admin, disabled, created_at, updated_at) " +
            "VALUES ($u, $d, $h, $admin, $disabled, $now, $now); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$u", account.Username);
        command.Parameters.AddWithValue("$d", (object?)account.DisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("$h", account.PasswordHash);
        command.Parameters.AddWithValue("$admin", account.IsAdmin ? 1 : 0);
        command.Parameters.AddWithValue("$disabled", account.Disabled ? 1 : 0);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

        int id = Convert.ToInt32(command.ExecuteScalar());
        account.Id = id;
        return id;
    }
}
