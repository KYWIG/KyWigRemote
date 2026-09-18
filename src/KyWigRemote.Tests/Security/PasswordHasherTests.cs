using KyWigRemote.Core.Security;

namespace KyWigRemote.Tests.Security;

/// <summary>Tests du hachage de mots de passe (PBKDF2).</summary>
public class PasswordHasherTests
{
    // Valeur factice de test, jamais un vrai mot de passe (règle de sécurité nº 5).
    private const string FakePassword = "MotDePasse-Factice-123!";

    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Verify_AvecLeBonMotDePasse_RetourneVrai()
    {
        string hash = _hasher.Hash(FakePassword);
        Assert.True(_hasher.Verify(FakePassword, hash));
    }

    [Fact]
    public void Verify_AvecUnMauvaisMotDePasse_RetourneFaux()
    {
        string hash = _hasher.Hash(FakePassword);
        Assert.False(_hasher.Verify("mauvais", hash));
    }

    [Fact]
    public void Hash_DeuxFois_ProduitDesResultatsDifferents_GraceAuSel()
    {
        Assert.NotEqual(_hasher.Hash(FakePassword), _hasher.Hash(FakePassword));
    }

    [Fact]
    public void Verify_AvecUnHashMalforme_RetourneFauxSansLever()
    {
        Assert.False(_hasher.Verify(FakePassword, "pas-un-hash-valide"));
    }
}
