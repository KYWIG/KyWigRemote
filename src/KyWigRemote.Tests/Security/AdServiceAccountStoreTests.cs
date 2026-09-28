using System.Security.Cryptography;
using KyWigRemote.Core.Data;
using KyWigRemote.Core.Security;

namespace KyWigRemote.Tests.Security;

/// <summary>Tests du stockage chiffré du compte de service AD (aller-retour + non-stockage en clair).</summary>
public class AdServiceAccountStoreTests
{
    // Valeur de test explicitement factice (règle nº 5).
    private const string FakeServicePassword = "Factice-CompteService-2026!";

    private sealed class InMemorySettings : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = new();
        public string? Get(string key) => _values.TryGetValue(key, out string? v) ? v : null;
        public void Set(string key, string value) => _values[key] = value;
    }

    [Fact]
    public void Save_Puis_Get_RetrouveLeCompte_SansStockerLeMotDePasseEnClair()
    {
        var settings = new InMemorySettings();
        byte[] masterKey = RandomNumberGenerator.GetBytes(CredentialProtector.KeySize);
        var store = new AdServiceAccountStore(settings, new CredentialProtector(), masterKey);

        Assert.False(store.IsConfigured);

        store.Save("svc_kywigremote", FakeServicePassword);

        Assert.True(store.IsConfigured);
        Assert.Equal("svc_kywigremote", store.Username);

        AdServiceAccount? account = store.Get();
        Assert.NotNull(account);
        Assert.Equal("svc_kywigremote", account!.Username);
        Assert.Equal(FakeServicePassword, account.Password);

        // Le mot de passe n'apparaît jamais en clair dans le stockage.
        Assert.DoesNotContain(FakeServicePassword, settings.Get("Ad:ServicePassword"));
    }

    [Fact]
    public void Get_AvecMauvaiseCle_Echoue()
    {
        var settings = new InMemorySettings();
        byte[] key = RandomNumberGenerator.GetBytes(CredentialProtector.KeySize);
        new AdServiceAccountStore(settings, new CredentialProtector(), key).Save("svc", FakeServicePassword);

        byte[] wrongKey = RandomNumberGenerator.GetBytes(CredentialProtector.KeySize);
        var store = new AdServiceAccountStore(settings, new CredentialProtector(), wrongKey);

        // GCM garantit l'intégrité : une mauvaise clé fait échouer le déchiffrement.
        Assert.Throws<AuthenticationTagMismatchException>(() => store.Get());
    }
}
