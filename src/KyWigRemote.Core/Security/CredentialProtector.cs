using System.Security.Cryptography;

namespace KyWigRemote.Core.Security;

/// <summary>
/// Chiffre et déchiffre les secrets stockés, en AES-256-GCM (chiffrement authentifié).
/// Primitive .NET uniquement (<see cref="AesGcm"/>, <see cref="RandomNumberGenerator"/>) —
/// aucune primitive maison (règle de sécurité nº 1).
///
/// GCM garantit à la fois la confidentialité et l'intégrité : toute altération du texte
/// chiffré, du nonce ou de l'étiquette fait échouer le déchiffrement plutôt que de rendre
/// un clair falsifié.
/// </summary>
public sealed class CredentialProtector
{
    /// <summary>Taille de clé attendue : 32 octets (AES-256).</summary>
    public const int KeySize = 32;

    private const int NonceSize = 12; // 96 bits, taille recommandée pour GCM
    private const int TagSize = 16;   // 128 bits

    /// <summary>Chiffre le clair fourni avec la clé maître ; génère un nonce aléatoire neuf.</summary>
    public EncryptedSecret Protect(ReadOnlySpan<byte> plaintext, byte[] key)
    {
        ValidateKey(key);

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] cipher = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, cipher, tag);

        return new EncryptedSecret(nonce, cipher, tag);
    }

    /// <summary>
    /// Déchiffre un secret. Lève <see cref="AuthenticationTagMismatchException"/> si la clé
    /// est mauvaise ou si le message a été altéré (garantie d'intégrité de GCM).
    /// </summary>
    public byte[] Unprotect(EncryptedSecret secret, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ValidateKey(key);

        byte[] plaintext = new byte[secret.Cipher.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(secret.Nonce, secret.Cipher, secret.Tag, plaintext);
        return plaintext;
    }

    private static void ValidateKey(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"La clé doit faire {KeySize} octets (AES-256).", nameof(key));
        }
    }
}
