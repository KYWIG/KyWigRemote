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
    private readonly CredentialProtector _protector;
    private readonly byte[] _masterKey;

    public CredentialService(ICredentialRepository repository, CredentialProtector protector, byte[] masterKey)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
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
}
