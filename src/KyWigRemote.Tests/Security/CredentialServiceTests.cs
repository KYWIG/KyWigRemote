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
        byte[] masterKey = RandomNumberGenerator.GetBytes(CredentialProtector.KeySize);
        return new CredentialService(repository, new CredentialProtector(), masterKey);
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
}
