using Microsoft.Data.Sqlite;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Data;

/// <summary>
/// Implémentation SQLite de <see cref="IConnectionRepository"/>.
/// Chaque opération ouvre une connexion courte via <see cref="Database.OpenConnection"/>
/// (transactions brèves, ADR-003).
/// </summary>
public sealed class SqliteConnectionRepository : IConnectionRepository
{
    private readonly Database _database;

    public SqliteConnectionRepository(Database database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public IReadOnlyList<ConnectionFolder> GetTree()
    {
        using SqliteConnection connection = _database.OpenConnection();

        // Index des dossiers par identifiant, plus une liste ordonnée (ordre de tri lu
        // en base) pour préserver l'ordre d'affichage lors de l'assemblage.
        var foldersById = new Dictionary<int, ConnectionFolder>();
        var orderedFolders = new List<ConnectionFolder>();
        var parentOf = new Dictionary<int, int?>();

        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT id, parent_id, name, credential_mode FROM folders ORDER BY sort_order, name;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                var folder = new ConnectionFolder
                {
                    Id = id,
                    Name = reader.GetString(2),
                    CredentialMode = ParseMode(reader.GetString(3)),
                };
                foldersById[id] = folder;
                orderedFolders.Add(folder);
                parentOf[id] = reader.IsDBNull(1) ? null : reader.GetInt32(1);
            }
        }

        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT id, folder_id, name, protocol, host, port, domain, description, credential_mode " +
                "FROM connections ORDER BY sort_order, name;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                var conn = new RemoteConnection
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(2),
                    Protocol = ParseProtocol(reader.GetString(3)),
                    Host = reader.GetString(4),
                    Port = reader.GetInt32(5),
                    Domain = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Description = reader.IsDBNull(7) ? null : reader.GetString(7),
                    CredentialMode = ParseMode(reader.GetString(8)),
                };

                // Une connexion sans dossier (folder_id NULL) est ignorée de l'arbre
                // partagé ; ce cas n'apparaît qu'après suppression d'un dossier (ON DELETE SET NULL).
                if (!reader.IsDBNull(1) && foldersById.TryGetValue(reader.GetInt32(1), out ConnectionFolder? parent))
                {
                    parent.Connections.Add(conn);
                }
            }
        }

        // Reconstitue la hiérarchie et collecte les racines, dans l'ordre de tri.
        var roots = new List<ConnectionFolder>();
        foreach (ConnectionFolder folder in orderedFolders)
        {
            int? parentId = parentOf[folder.Id];
            if (parentId is int pid && foldersById.TryGetValue(pid, out ConnectionFolder? parent))
            {
                parent.SubFolders.Add(folder);
            }
            else
            {
                roots.Add(folder);
            }
        }

        return roots;
    }

    public int AddFolder(ConnectionFolder folder, int? parentId)
    {
        ArgumentNullException.ThrowIfNull(folder);
        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO folders (parent_id, name, credential_mode, created_at, updated_at) " +
            "VALUES ($parent, $name, $mode, $now, $now); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$parent", (object?)parentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", folder.Name);
        command.Parameters.AddWithValue("$mode", ToDbMode(folder.CredentialMode));
        command.Parameters.AddWithValue("$now", Now());
        int id = Convert.ToInt32(command.ExecuteScalar());
        folder.Id = id;
        return id;
    }

    public void UpdateFolder(ConnectionFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "UPDATE folders SET name = $name, credential_mode = $mode, updated_at = $now WHERE id = $id;";
        command.Parameters.AddWithValue("$name", folder.Name);
        command.Parameters.AddWithValue("$mode", ToDbMode(folder.CredentialMode));
        command.Parameters.AddWithValue("$now", Now());
        command.Parameters.AddWithValue("$id", folder.Id);
        command.ExecuteNonQuery();
    }

    public void DeleteFolder(int id)
    {
        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM folders WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public int AddConnection(RemoteConnection connection, int folderId)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using SqliteConnection db = _database.OpenConnection();
        using SqliteCommand command = db.CreateCommand();
        command.CommandText =
            "INSERT INTO connections " +
            "(folder_id, name, protocol, host, port, domain, description, credential_mode, created_at, updated_at) " +
            "VALUES ($folder, $name, $proto, $host, $port, $domain, $descr, $mode, $now, $now); " +
            "SELECT last_insert_rowid();";
        BindConnection(command, connection);
        command.Parameters.AddWithValue("$folder", folderId);
        command.Parameters.AddWithValue("$now", Now());
        int id = Convert.ToInt32(command.ExecuteScalar());
        connection.Id = id;
        return id;
    }

    public void UpdateConnection(RemoteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using SqliteConnection db = _database.OpenConnection();
        using SqliteCommand command = db.CreateCommand();
        command.CommandText =
            "UPDATE connections SET name = $name, protocol = $proto, host = $host, port = $port, " +
            "domain = $domain, description = $descr, credential_mode = $mode, updated_at = $now WHERE id = $id;";
        BindConnection(command, connection);
        command.Parameters.AddWithValue("$now", Now());
        command.Parameters.AddWithValue("$id", connection.Id);
        command.ExecuteNonQuery();
    }

    public void DeleteConnection(int id)
    {
        using SqliteConnection connection = _database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM connections WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private static void BindConnection(SqliteCommand command, RemoteConnection connection)
    {
        command.Parameters.AddWithValue("$name", connection.Name);
        command.Parameters.AddWithValue("$proto", ToDbProtocol(connection.Protocol));
        command.Parameters.AddWithValue("$host", connection.Host);
        command.Parameters.AddWithValue("$port", connection.Port);
        command.Parameters.AddWithValue("$domain", (object?)connection.Domain ?? DBNull.Value);
        command.Parameters.AddWithValue("$descr", (object?)connection.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$mode", ToDbMode(connection.CredentialMode));
    }

    private static string Now() => DateTimeOffset.UtcNow.ToString("O");

    // --- Conversions entre valeurs de base (texte majuscule) et énumérations ---

    private static string ToDbMode(CredentialMode mode) => mode switch
    {
        CredentialMode.Personal => "PERSONAL",
        CredentialMode.Enforced => "ENFORCED",
        CredentialMode.Prompt => "PROMPT",
        CredentialMode.Inherited => "INHERITED",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode d'identifiants inconnu."),
    };

    private static CredentialMode ParseMode(string value) => value switch
    {
        "PERSONAL" => CredentialMode.Personal,
        "ENFORCED" => CredentialMode.Enforced,
        "PROMPT" => CredentialMode.Prompt,
        "INHERITED" => CredentialMode.Inherited,
        _ => throw new InvalidOperationException($"Mode d'identifiants inattendu en base : « {value} »."),
    };

    private static string ToDbProtocol(RemoteProtocol protocol) => protocol switch
    {
        RemoteProtocol.Rdp => "RDP",
        RemoteProtocol.Ssh => "SSH",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Protocole inconnu."),
    };

    private static RemoteProtocol ParseProtocol(string value) => value switch
    {
        "RDP" => RemoteProtocol.Rdp,
        "SSH" => RemoteProtocol.Ssh,
        _ => throw new InvalidOperationException($"Protocole inattendu en base : « {value} »."),
    };
}
