using KyWigRemote.Core.Data;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Tests.Data;

/// <summary>
/// Tests du repository SQLite : CRUD dossiers/connexions et reconstruction de l'arbre (E2.3).
/// </summary>
public class SqliteConnectionRepositoryTests
{
    private static SqliteConnectionRepository NewRepository(TempDatabase temp)
    {
        temp.Database.Initialize();
        return new SqliteConnectionRepository(temp.Database);
    }

    [Fact]
    public void AddFolder_PuisGetTree_RetourneLeDossierEnRacine()
    {
        using var temp = new TempDatabase();
        SqliteConnectionRepository repo = NewRepository(temp);

        int id = repo.AddFolder(new ConnectionFolder { Name = "KyWig", CredentialMode = CredentialMode.Personal }, null);

        IReadOnlyList<ConnectionFolder> roots = repo.GetTree();
        Assert.Single(roots);
        Assert.Equal(id, roots[0].Id);
        Assert.Equal("KyWig", roots[0].Name);
        Assert.Equal(CredentialMode.Personal, roots[0].CredentialMode);
    }

    [Fact]
    public void GetTree_ReconstruitLaHierarchieEtLesConnexions()
    {
        using var temp = new TempDatabase();
        SqliteConnectionRepository repo = NewRepository(temp);

        int racine = repo.AddFolder(new ConnectionFolder { Name = "KyWig" }, null);
        int sous = repo.AddFolder(new ConnectionFolder { Name = "Serveurs" }, racine);
        repo.AddConnection(new RemoteConnection
        {
            Name = "Serveur (démo)", Protocol = RemoteProtocol.Rdp, Host = "192.0.2.14", Port = 3389,
            CredentialMode = CredentialMode.Personal,
        }, sous);

        IReadOnlyList<ConnectionFolder> roots = repo.GetTree();

        ConnectionFolder kywig = Assert.Single(roots);
        ConnectionFolder serveurs = Assert.Single(kywig.SubFolders);
        RemoteConnection connection = Assert.Single(serveurs.Connections);
        Assert.Equal("Serveur (démo)", connection.Name);
        Assert.Equal(RemoteProtocol.Rdp, connection.Protocol);
        Assert.Equal("192.0.2.14", connection.Host);
        Assert.Equal(3389, connection.Port);
    }

    [Fact]
    public void UpdateConnection_ModifieLesChamps()
    {
        using var temp = new TempDatabase();
        SqliteConnectionRepository repo = NewRepository(temp);
        int folder = repo.AddFolder(new ConnectionFolder { Name = "KyWig" }, null);
        int id = repo.AddConnection(new RemoteConnection
        {
            Name = "Avant", Protocol = RemoteProtocol.Ssh, Host = "192.0.2.2", Port = 22,
        }, folder);

        repo.UpdateConnection(new RemoteConnection
        {
            Id = id, Name = "Après", Protocol = RemoteProtocol.Rdp, Host = "192.0.2.9", Port = 3390,
            CredentialMode = CredentialMode.Enforced,
        });

        RemoteConnection updated = Assert.Single(repo.GetTree()[0].Connections);
        Assert.Equal("Après", updated.Name);
        Assert.Equal(RemoteProtocol.Rdp, updated.Protocol);
        Assert.Equal("192.0.2.9", updated.Host);
        Assert.Equal(3390, updated.Port);
        Assert.Equal(CredentialMode.Enforced, updated.CredentialMode);
    }

    [Fact]
    public void DeleteFolder_SupprimeEnCascadeSousDossiersEtConnexions()
    {
        using var temp = new TempDatabase();
        SqliteConnectionRepository repo = NewRepository(temp);
        int racine = repo.AddFolder(new ConnectionFolder { Name = "KyWig" }, null);
        int sous = repo.AddFolder(new ConnectionFolder { Name = "Serveurs" }, racine);
        repo.AddConnection(new RemoteConnection { Name = "C", Protocol = RemoteProtocol.Rdp, Host = "192.0.2.1", Port = 3389 }, sous);

        repo.DeleteFolder(racine);

        Assert.Empty(repo.GetTree());
    }

    [Fact]
    public void DemoSeed_Populate_CreeLesTroisEntitesRacines()
    {
        using var temp = new TempDatabase();
        SqliteConnectionRepository repo = NewRepository(temp);

        DemoSeed.Populate(repo);

        IReadOnlyList<ConnectionFolder> roots = repo.GetTree();
        Assert.Equal(3, roots.Count);
        Assert.Contains(roots, r => r.Name == "KyWig");
        Assert.Contains(roots, r => r.Name == "Kermazegan");
        Assert.Contains(roots, r => r.Name == "Karantez");
    }
}
