using System.Security.Cryptography;
using System.Text;
using KyWigRemote.Core.Data;
using KyWigRemote.Core.Model;
using KyWigRemote.Core.Security;
using KyWigRemote.Tests.Data;

namespace KyWigRemote.Tests.Security;

/// <summary>Cycle complet : chiffrer + stocker un identifiant imposé, puis le retrouver déchiffré.</summary>
public class CredentialServiceTests
{
    // Valeur factice de test, explicitement nommée comme telle (règle nº 5).
    private const string FakeSecret = "MotDePasseCompteImpose-Factice!";

    private static CredentialService NewService(TempDatabase temp, out ICredentialRepository repository)
    {
        temp.Database.Initialize();
        repository = new SqliteCredentialRepository(temp.Database);
        var personal = new SqlitePersonalCredentialRepository(temp.Database);
        byte[] masterKey = RandomNumberGenerator.GetBytes(CredentialProtector.KeySize);
        return new CredentialService(repository, personal, new CredentialProtector(), masterKey);
    }

    // Crée une vraie connexion (la table personal_credentials a une clé étrangère vers connections).
    private static int NewConnection(TempDatabase temp)
    {
        var repo = new SqliteConnectionRepository(temp.Database);
        int folder = repo.AddFolder(new KyWigRemote.Core.Model.ConnectionFolder { Name = "F" }, null);
        return repo.AddConnection(new KyWigRemote.Core.Model.RemoteConnection
        {
            Name = "C", Protocol = KyWigRemote.Core.Model.RemoteProtocol.Rdp, Host = "192.0.2.1", Port = 3389,
        }, folder);
    }

    [Fact]
    public void SavePuisReveal_RetourneLeSecretDOrigine()
    {
        using var temp = new TempDatabase();
        CredentialService service = NewService(temp, out _);

        int id = service.SaveEnforcedCredential(
            new EnforcedCredential { Label = "Compte switchs", Username = "svc-net", AllowedGroups = "GG_Reseau" },
            FakeSecret);

        string? revealed = service.RevealEnforcedSecret(id);
        Assert.Equal(FakeSecret, revealed);
    }

    [Fact]
    public void RevealEnforcedSecret_IdInconnu_RetourneNull()
    {
        using var temp = new TempDatabase();
        CredentialService service = NewService(temp, out _);

        Assert.Null(service.RevealEnforcedSecret(999));
    }

    [Fact]
    public void ListEnforced_NExposeAucunSecret()
    {
        using var temp = new TempDatabase();
        CredentialService service = NewService(temp, out ICredentialRepository repository);
        service.SaveEnforcedCredential(new EnforcedCredential { Label = "Compte switchs", Username = "svc-net" }, FakeSecret);

        IReadOnlyList<EnforcedCredential> list = repository.ListEnforced();

        EnforcedCredential item = Assert.Single(list);
        Assert.Equal("Compte switchs", item.Label);
        // Le modèle de métadonnées ne porte tout simplement aucun champ de secret.
    }

    [Fact]
    public void LeSecretEnClairNApparaitPasDansLeFichierDeBase()
    {
        using var temp = new TempDatabase();
        CredentialService service = NewService(temp, out _);
        service.SaveEnforcedCredential(new EnforcedCredential { Label = "L", Username = "u" }, FakeSecret);

        // Vide le pool pour que le WAL soit bien écrit et lisible depuis le disque.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        byte[] bytes = File.ReadAllBytes(temp.Path);
        string wal = temp.Path + "-wal";
        string haystack = Encoding.UTF8.GetString(bytes) +
            (File.Exists(wal) ? Encoding.UTF8.GetString(File.ReadAllBytes(wal)) : string.Empty);

        Assert.DoesNotContain(FakeSecret, haystack);
    }

    [Fact]
    public void PersonalSavePuisReveal_RetourneLeSecret_ParConnexion()
    {
        using var temp = new TempDatabase();
        CredentialService service = NewService(temp, out _);
        int connId = NewConnection(temp);

        service.SavePersonalCredential("alice", connId, "alice.admin", "kywig", FakeSecret);

        RevealedLogin? revealed = service.RevealPersonalCredential("alice", connId);
        Assert.NotNull(revealed);
        Assert.Equal("alice.admin", revealed!.Username);
        Assert.Equal(FakeSecret, revealed.Secret);
    }

    [Fact]
    public void PersonalReveal_ReplieSurLIdentifiantGlobal_QuandRienDeSpecifique()
    {
        using var temp = new TempDatabase();
        CredentialService service = NewService(temp, out _);

        // Un identifiant global (connexion null), rien de spécifique à la connexion 9.
        service.SavePersonalCredential("bob", connectionId: null, "bob.global", null, FakeSecret);

        RevealedLogin? revealed = service.RevealPersonalCredential("bob", 9);
        Assert.NotNull(revealed);
        Assert.Equal("bob.global", revealed!.Username);
    }

    [Fact]
    public void PersonalReveal_NeFuitPasEntreUtilisateurs()
    {
        using var temp = new TempDatabase();
        CredentialService service = NewService(temp, out _);
        int connId = NewConnection(temp);
        service.SavePersonalCredential("alice", connId, "alice.admin", null, FakeSecret);

        // Bob n'a rien enregistré : il ne doit rien obtenir sur la même connexion.
        Assert.Null(service.RevealPersonalCredential("bob", connId));
    }
}
