using System.Text;
using KyWigRemote.Core.Data;

namespace KyWigRemote.Core.Security;

/// <summary>Compte de service AD (identifiant + mot de passe en clair, le temps de se lier à l'annuaire).</summary>
public sealed record AdServiceAccount(string Username, string Password);

/// <summary>
/// Conserve le compte de service Active Directory : l'identifiant en clair et le mot de passe
/// chiffré en AES-256-GCM (clé maître serveur), dans la table <c>settings</c>. Le mot de passe
/// n'est jamais stocké ni journalisé en clair (règles 2 et 5).
/// </summary>
public sealed class AdServiceAccountStore
{
    private const string UsernameKey = "Ad:ServiceUsername";
    private const string PasswordKey = "Ad:ServicePassword"; // nonce.cipher.tag en base64

    private readonly ISettingsStore _settings;
    private readonly CredentialProtector _protector;
    private readonly byte[] _masterKey;

    public AdServiceAccountStore(ISettingsStore settings, CredentialProtector protector, byte[] masterKey)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _masterKey = masterKey ?? throw new ArgumentNullException(nameof(masterKey));
    }

    /// <summary>Identifiant du compte de service (sans déchiffrer le mot de passe), ou null.</summary>
    public string? Username => _settings.Get(UsernameKey);

    /// <summary>Indique si un compte de service est configuré (identifiant et mot de passe présents).</summary>
    public bool IsConfigured =>
        !string.IsNullOrEmpty(_settings.Get(UsernameKey)) && !string.IsNullOrEmpty(_settings.Get(PasswordKey));

    /// <summary>Enregistre le compte de service (le mot de passe est chiffré avant stockage).</summary>
    public void Save(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrEmpty(password);

        EncryptedSecret enc = _protector.Protect(Encoding.UTF8.GetBytes(password), _masterKey);
        string blob = string.Join('.',
            Convert.ToBase64String(enc.Nonce),
            Convert.ToBase64String(enc.Cipher),
            Convert.ToBase64String(enc.Tag));

        _settings.Set(UsernameKey, username);
        _settings.Set(PasswordKey, blob);
    }

    /// <summary>Retourne le compte de service déchiffré, ou <c>null</c> s'il n'est pas configuré.</summary>
    public AdServiceAccount? Get()
    {
        string? username = _settings.Get(UsernameKey);
        string? blob = _settings.Get(PasswordKey);
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(blob))
        {
            return null;
        }

        string[] parts = blob.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var secret = new EncryptedSecret(
            Convert.FromBase64String(parts[0]),
            Convert.FromBase64String(parts[1]),
            Convert.FromBase64String(parts[2]));
        byte[] plain = _protector.Unprotect(secret, _masterKey);
        return new AdServiceAccount(username, Encoding.UTF8.GetString(plain));
    }
}
