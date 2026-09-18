using System.Security.Cryptography;
using System.Text;
using KyWigRemote.Core.Data;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Security;

/// <summary>
/// Orchestration du chiffrement des identifiants imposés : chiffre le secret avec la clé
/// maître avant stockage, et ne le déchiffre qu'à la demande (ouverture de session).
/// La clé maître est fournie au constructeur (résolue côté serveur, jamais codée en dur).
/// </summary>
public sealed class CredentialService
{
    private readonly ICredentialRepository _repository;
    private readonly IPersonalCredentialRepository _personalRepository;
    private readonly CredentialProtector _protector;
    private readonly byte[] _masterKey;

    public CredentialService(
        ICredentialRepository repository,
        IPersonalCredentialRepository personalRepository,
        CredentialProtector protector,
        byte[] masterKey)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _personalRepository = personalRepository ?? throw new ArgumentNullException(nameof(personalRepository));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _masterKey = masterKey ?? throw new ArgumentNullException(nameof(masterKey));
    }

    /// <summary>Chiffre puis enregistre un identifiant imposé ; retourne son identifiant.</summary>
    public int SaveEnforcedCredential(EnforcedCredential credential, string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        // Le clair ne vit que le temps du chiffrement, puis son tampon est effacé (règle nº 3).
        byte[] plaintext = Encoding.UTF8.GetBytes(secret);
        try
        {
            EncryptedSecret encrypted = _protector.Protect(plaintext, _masterKey);
            return _repository.SaveEnforced(credential, encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>
    /// Déchiffre le secret imposé pour l'injecter dans une session. Retourne null si
    /// l'identifiant n'existe pas. L'appelant utilise la valeur au dernier moment puis la lâche.
    /// </summary>
    public string? RevealEnforcedSecret(int credentialId)
    {
        EncryptedSecret? encrypted = _repository.GetEnforcedSecret(credentialId);
        if (encrypted is null)
        {
            return null;
        }

        byte[] plaintext = _protector.Unprotect(encrypted, _masterKey);
        try
        {
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>
    /// Chiffre puis enregistre l'identifiant personnel de l'utilisateur pour une connexion
    /// précise (ou toutes ses connexions si connectionId est null).
    /// </summary>
    public void SavePersonalCredential(string owner, int? connectionId, string username, string? domain, string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentException.ThrowIfNullOrEmpty(secret);

        byte[] plaintext = Encoding.UTF8.GetBytes(secret);
        try
        {
            EncryptedSecret encrypted = _protector.Protect(plaintext, _masterKey);
            _personalRepository.Save(owner, connectionId, username, domain, encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>
    /// Déchiffre l'identifiant personnel de l'utilisateur pour la connexion : d'abord
    /// l'entrée propre à la connexion, sinon l'entrée globale. Retourne null si aucune.
    /// </summary>
    public RevealedLogin? RevealPersonalCredential(string owner, int? connectionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);

        StoredPersonalCredential? stored = _personalRepository.Find(owner, connectionId);
        if (stored is null && connectionId is not null)
        {
            stored = _personalRepository.Find(owner, null); // repli sur l'identifiant global
        }
        if (stored is null)
        {
            return null;
        }

        byte[] plaintext = _protector.Unprotect(stored.Secret, _masterKey);
        try
        {
            return new RevealedLogin(stored.Username, stored.Domain, Encoding.UTF8.GetString(plaintext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}

/// <summary>Identifiant déchiffré prêt à injecter dans une session (à utiliser puis lâcher).</summary>
public sealed record RevealedLogin(string Username, string? Domain, string Secret);
