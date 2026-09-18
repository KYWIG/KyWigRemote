using Microsoft.Data.Sqlite;
using KyWigRemote.Core.Data;

namespace KyWigRemote.Tests.Data;

/// <summary>
/// Tests de l'initialisation et du versionnement de la base (E2.1, E2.2).
/// </summary>
public class DatabaseTests
{
    [Fact]
    public void Initialize_SurBaseAbsente_CreeLeSchemaEtSignaleLaCreation()
    {
        using var temp = new TempDatabase();

        bool created = temp.Database.Initialize();

        Assert.True(created);
        Assert.True(File.Exists(temp.Path));
        Assert.Equal(Database.CurrentVersion, ReadVersion(temp));
    }

    [Fact]
    public void Initialize_AppeleeDeuxFois_NeRecreePasEtNeSignalePlusDeCreation()
    {
        using var temp = new TempDatabase();

        bool firstRun = temp.Database.Initialize();
        bool secondRun = temp.Database.Initialize();

        Assert.True(firstRun);
        Assert.False(secondRun);
        Assert.Equal(Database.CurrentVersion, ReadVersion(temp));
    }

    [Fact]
    public void Initialize_QuandLaBaseEstPlusRecenteQueLeCode_Refuse()
    {
        using var temp = new TempDatabase();
        temp.Database.Initialize();

        // Simule une base écrite par une future version de l'application.
        using (SqliteConnection connection = temp.Database.OpenConnection())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO schema_version (version, applied_at) VALUES ($v, $t);";
            command.Parameters.AddWithValue("$v", Database.CurrentVersion + 1);
            command.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => temp.Database.Initialize());
        Assert.Contains("plus récente", ex.Message);
    }

    private static int ReadVersion(TempDatabase temp)
    {
        using SqliteConnection connection = temp.Database.OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
