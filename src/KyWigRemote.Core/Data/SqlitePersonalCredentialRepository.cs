using Microsoft.Data.Sqlite;
using KyWigRemote.Core.Remote;
using KyWigRemote.Core.Security;

namespace KyWigRemote.Core.Data;

/// <summary>Implémentation SQLite de <see cref="IPersonalCredentialRepository"/>.</summary>
public sealed class SqlitePersonalCredentialRepository : IPersonalCredentialRepository
{
    private readonly Database _database;

    public SqlitePersonalCredentialRepository(Database database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public void Save(string owner, int? connectionId, string username, string? domain, EncryptedSecret secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentNullException.ThrowIfNull(secret);

        using SqliteConnection connection = _database.OpenConnection();

        // Mise à jour si l'entrée (owner, portée) existe déjà, sinon insertion.
        using (SqliteCommand update = connection.CreateCommand())
        {
            update.CommandText =
                "UPDATE personal_credentials SET username = $user, domain = $domain, " +
                "secret_cipher = $cipher, secret_nonce = $nonce, secret_tag = $tag, updated_at = $now " +
                "WHERE owner = $owner AND IFNULL(connection_id, -1) = IFNULL($conn, -1);";
            BindCommon(update, owner, connectionId, username, domain, secret);
            if (update.ExecuteNonQuery() > 0)
            {
                return;
            }
        }

        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO personal_credentials " +
            "(owner, connection_id, username, domain, secret_cipher, secret_nonce, secret_tag, created_at, updated_at) " +
            "VALUES ($owner, $conn, $user, $domain, $cipher, $nonce, $tag, $now, $now);";
        BindCommon(insert, owner, connectionId, username, domain, secret);
        insert.ExecuteNonQuery();
    }

    public StoredPersonalCredential? Find(string owner, int? connectionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);

        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT username, domain, secret_cipher, secret_nonce, secret_tag FROM personal_credentials " +
            "WHERE owner = $owner AND IFNULL(connection_id, -1) = IFNULL($conn, -1);";
        command.Parameters.AddWithValue("$owner", owner);
        command.Parameters.AddWithValue("$conn", (object?)connectionId ?? DBNull.Value);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var secret = new EncryptedSecret(
            reader.GetFieldValue<byte[]>(3),
            reader.GetFieldValue<byte[]>(2),
            reader.GetFieldValue<byte[]>(4));
        return new StoredPersonalCredential(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            secret);
    }

    public IReadOnlyList<PersonalCredentialSummary> ListByOwner(string owner)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);

        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT pc.id, pc.connection_id, c.name, pc.username, pc.domain " +
            "FROM personal_credentials pc LEFT JOIN connections c ON c.id = pc.connection_id " +
            "WHERE pc.owner = $owner ORDER BY c.name COLLATE NOCASE, pc.id;";
        command.Parameters.AddWithValue("$owner", owner);

        var result = new List<PersonalCredentialSummary>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new PersonalCredentialSummary(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }
        return result;
    }

    public bool Delete(string owner, int id)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);

        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM personal_credentials WHERE id = $id AND owner = $owner;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$owner", owner);
        return command.ExecuteNonQuery() > 0;
    }

    private static void BindCommon(
        SqliteCommand command, string owner, int? connectionId, string username, string? domain, EncryptedSecret secret)
    {
        command.Parameters.AddWithValue("$owner", owner);
        command.Parameters.AddWithValue("$conn", (object?)connectionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$user", username);
        command.Parameters.AddWithValue("$domain", (object?)domain ?? DBNull.Value);
        command.Parameters.AddWithValue("$cipher", secret.Cipher);
        command.Parameters.AddWithValue("$nonce", secret.Nonce);
        command.Parameters.AddWithValue("$tag", secret.Tag);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
    }
}
