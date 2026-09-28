using Microsoft.Data.Sqlite;

namespace KyWigRemote.Core.Data;

/// <summary>
/// Point d'accès unique à la base SQLite : fabrique les connexions (avec les pragmas
/// WAL / foreign_keys / busy_timeout d'ADR-003 et E2.6), crée le schéma si la base
/// est absente, et applique les migrations selon <c>schema_version</c> (E2.2).
///
/// Toute la couche données passe par cette classe ; ni Client ni Admin n'ouvrent
/// SQLite directement (voir CLAUDE.md, section Structure).
/// </summary>
public sealed class Database
{
    private readonly string _connectionString;

    /// <summary>Version de schéma attendue par ce code.</summary>
    public const int CurrentVersion = SchemaScript.Version;

    /// <summary>Chemin du fichier de base utilisé.</summary>
    public string FilePath { get; }

    /// <summary>Vrai si la dernière initialisation venait de créer la base (utile pour l'amorçage).</summary>
    public bool WasCreated { get; private set; }

    /// <summary>
    /// Prépare l'accès à la base située au chemin indiqué. Le fichier n'est pas
    /// touché tant que <see cref="Initialize"/> n'est pas appelé.
    /// </summary>
    public Database(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // Le cache partagé est déconseillé ici : chaque connexion est courte et isolée.
            Pooling = true,
        }.ToString();
    }

    /// <summary>
    /// Ouvre une connexion prête à l'emploi : pragmas appliqués. À utiliser dans un
    /// <c>using</c> pour garantir une durée de vie courte (transactions brèves, ADR-003).
    /// </summary>
    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        ApplyPragmas(connection);
        return connection;
    }

    /// <summary>
    /// Crée la base et son schéma si nécessaire, puis applique les migrations manquantes.
    /// Retourne <c>true</c> si la base venait d'être créée (utile pour l'amorçage des données).
    /// Lève <see cref="InvalidOperationException"/> si la base est plus récente que le code.
    /// </summary>
    public bool Initialize()
    {
        EnsureDirectoryExists();

        using SqliteConnection connection = OpenConnection();

        EnsureVersionTable(connection);
        int existingVersion = ReadCurrentVersion(connection);

        if (existingVersion > CurrentVersion)
        {
            throw new InvalidOperationException(
                $"La base est en version {existingVersion}, plus récente que cette application " +
                $"(version {CurrentVersion}). Mettez l'application à jour avant de continuer.");
        }

        bool created = existingVersion == 0;
        WasCreated = created;

        foreach ((int version, string sql) in Migrations())
        {
            if (version <= existingVersion)
            {
                continue;
            }

            using SqliteTransaction tx = connection.BeginTransaction();
            ExecuteScript(connection, tx, sql);
            RecordVersion(connection, tx, version);
            tx.Commit();
        }

        return created;
    }

    /// <summary>
    /// Suite ordonnée des migrations. Une nouvelle version = une nouvelle entrée ici,
    /// jamais une modification d'une migration déjà livrée.
    /// </summary>
    private static IEnumerable<(int Version, string Sql)> Migrations()
    {
        yield return (1, SchemaScript.V1);
        yield return (2, SchemaScript.V2);
        yield return (3, SchemaScript.V3);
        yield return (4, SchemaScript.V4);
    }

    private void EnsureDirectoryExists()
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
        if (!string.IsNullOrEmpty(directory))
        {
            // Pleinement qualifié : le namespace KyWigRemote.Core.Directory masque System.IO.Directory.
            System.IO.Directory.CreateDirectory(directory);
        }
    }

    private static void ApplyPragmas(SqliteConnection connection)
    {
        // WAL : lectures concurrentes non bloquées par une écriture (ADR-003).
        // foreign_keys : contraintes réellement appliquées (désactivées par défaut en SQLite).
        // busy_timeout : attente avant échec sur verrou, pour l'accès partagé.
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "PRAGMA journal_mode = WAL;" +
            "PRAGMA foreign_keys = ON;" +
            "PRAGMA busy_timeout = 5000;";
        command.ExecuteNonQuery();
    }

    private static void EnsureVersionTable(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS schema_version (" +
            "version INTEGER NOT NULL, applied_at TEXT NOT NULL);";
        command.ExecuteNonQuery();
    }

    private static int ReadCurrentVersion(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
        object? result = command.ExecuteScalar();
        return Convert.ToInt32(result);
    }

    private static void ExecuteScript(SqliteConnection connection, SqliteTransaction tx, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void RecordVersion(SqliteConnection connection, SqliteTransaction tx, int version)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "INSERT INTO schema_version (version, applied_at) VALUES ($v, $t);";
        command.Parameters.AddWithValue("$v", version);
        command.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }
}
