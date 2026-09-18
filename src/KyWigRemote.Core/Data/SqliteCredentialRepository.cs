using Microsoft.Data.Sqlite;
using KyWigRemote.Core.Model;
using KyWigRemote.Core.Security;

namespace KyWigRemote.Core.Data;

/// <summary>Implémentation SQLite de <see cref="ICredentialRepository"/> (table enforced_credentials).</summary>
public sealed class SqliteCredentialRepository : ICredentialRepository
{
    private readonly Database _database;

    public SqliteCredentialRepository(Database database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public int SaveEnforced(EnforcedCredential credential, EncryptedSecret secret)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(secret);

        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO enforced_credentials " +
            "(label, username, domain, secret_cipher, secret_nonce, secret_tag, allowed_groups, created_at, updated_at) " +
            "VALUES ($label, $user, $domain, $cipher, $nonce, $tag, $groups, $now, $now); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$label", credential.Label);
        command.Parameters.AddWithValue("$user", credential.Username);
        command.Parameters.AddWithValue("$domain", (object?)credential.Domain ?? DBNull.Value);
        command.Parameters.AddWithValue("$cipher", secret.Cipher);
        command.Parameters.AddWithValue("$nonce", secret.Nonce);
        command.Parameters.AddWithValue("$tag", secret.Tag);
        command.Parameters.AddWithValue("$groups", (object?)credential.AllowedGroups ?? DBNull.Value);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

        int id = Convert.ToInt32(command.ExecuteScalar());
        credential.Id = id;
        return id;
    }

    public EnforcedCredential? GetEnforced(int id)
    {
        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, label, username, domain, allowed_groups FROM enforced_credentials WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }
        return new EnforcedCredential
        {
            Id = reader.GetInt32(0),
            Label = reader.GetString(1),
            Username = reader.GetString(2),
            Domain = reader.IsDBNull(3) ? null : reader.GetString(3),
            AllowedGroups = reader.IsDBNull(4) ? null : reader.GetString(4),
        };
    }

    public EncryptedSecret? GetEnforcedSecret(int id)
    {
        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT secret_cipher, secret_nonce, secret_tag FROM enforced_credentials WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        byte[] cipher = reader.GetFieldValue<byte[]>(0);
        byte[] nonce = reader.GetFieldValue<byte[]>(1);
        byte[] tag = reader.GetFieldValue<byte[]>(2);
        return new EncryptedSecret(nonce, cipher, tag);
    }

    public IReadOnlyList<EnforcedCredential> ListEnforced()
    {
        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, label, username, domain, allowed_groups FROM enforced_credentials ORDER BY label COLLATE NOCASE;";

        var result = new List<EnforcedCredential>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new EnforcedCredential
            {
                Id = reader.GetInt32(0),
                Label = reader.GetString(1),
                Username = reader.GetString(2),
                Domain = reader.IsDBNull(3) ? null : reader.GetString(3),
                AllowedGroups = reader.IsDBNull(4) ? null : reader.GetString(4),
            });
        }
        return result;
    }
}
