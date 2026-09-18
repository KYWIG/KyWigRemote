using Microsoft.Data.Sqlite;
using KyWigRemote.Core.Data;

namespace KyWigRemote.Tests.Data;

/// <summary>
/// Base SQLite jetable dans un dossier temporaire, pour les tests.
/// Le nettoyage vide d'abord le pool de connexions afin de pouvoir supprimer le fichier
/// (le WAL crée aussi des fichiers -wal et -shm).
/// </summary>
internal sealed class TempDatabase : IDisposable
{
    public string Path { get; }
    public Database Database { get; }

    public TempDatabase()
    {
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kwr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Path = System.IO.Path.Combine(dir, "test.db");
        Database = new Database(Path);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            string? dir = System.IO.Path.GetDirectoryName(Path);
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Fichier encore verrouillé : sans importance pour un dossier temporaire.
        }
    }
}
