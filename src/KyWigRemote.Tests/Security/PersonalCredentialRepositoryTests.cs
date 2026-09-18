using System.Security.Cryptography;
using System.Text;
using KyWigRemote.Core.Data;
using KyWigRemote.Core.Model;
using KyWigRemote.Core.Remote;
using KyWigRemote.Core.Security;
using KyWigRemote.Tests.Data;

namespace KyWigRemote.Tests.Security;

/// <summary>Tests de la liste et de la suppression des identifiants personnels (FR-16).</summary>
public class PersonalCredentialRepositoryTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(CredentialProtector.KeySize);
    private static readonly CredentialProtector Protector = new();

    private static EncryptedSecret Secret(string value) =>
        Protector.Protect(Encoding.UTF8.GetBytes(value), Key);

    private static int NewConnection(TempDatabase temp)
    {
        var repo = new SqliteConnectionRepository(temp.Database);
        int folder = repo.AddFolder(new ConnectionFolder { Name = "F" }, null);
        return repo.AddConnection(new RemoteConnection { Name = "C", Protocol = RemoteProtocol.Rdp, Host = "192.0.2.1", Port = 3389 }, folder);
    }

    [Fact]
    public void ListByOwner_NeRetourneQueLesIdentifiantsDuProprietaire()
    {
        using var temp = new TempDatabase();
        temp.Database.Initialize();
        var repo = new SqlitePersonalCredentialRepository(temp.Database);
        int conn = NewConnection(temp);

        repo.Save("alice", conn, "alice.c", null, Secret("s1"));
        repo.Save("alice", null, "alice.global", null, Secret("s2"));
        repo.Save("bob", conn, "bob.c", null, Secret("s3"));

        Assert.Equal(2, repo.ListByOwner("alice").Count);
        PersonalCredentialSummary bobItem = Assert.Single(repo.ListByOwner("bob"));
        Assert.Equal("bob.c", bobItem.Username);
    }

    [Fact]
    public void Delete_NeSupprimeQueSiLeProprietaireCorrespond()
    {
        using var temp = new TempDatabase();
        temp.Database.Initialize();
        var repo = new SqlitePersonalCredentialRepository(temp.Database);
        int conn = NewConnection(temp);
        repo.Save("alice", conn, "alice.c", null, Secret("s1"));
        int id = repo.ListByOwner("alice").Single().Id;

        // Bob ne peut pas supprimer l'identifiant d'Alice.
        Assert.False(repo.Delete("bob", id));
        Assert.Single(repo.ListByOwner("alice"));

        // Alice le supprime.
        Assert.True(repo.Delete("alice", id));
        Assert.Empty(repo.ListByOwner("alice"));
    }
}
