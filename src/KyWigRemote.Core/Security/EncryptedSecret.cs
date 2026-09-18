namespace KyWigRemote.Core.Security;

/// <summary>
/// Secret chiffré en AES-256-GCM : le nonce (jamais réutilisé), le texte chiffré et
/// l'étiquette d'authentification. Les trois sont stockés ensemble ; aucun n'est secret
/// en soi, mais le nonce ne doit jamais être réutilisé avec la même clé.
/// </summary>
/// <param name="Nonce">Nonce aléatoire de 12 octets.</param>
/// <param name="Cipher">Texte chiffré (même longueur que le clair).</param>
/// <param name="Tag">Étiquette d'authentification GCM de 16 octets.</param>
public sealed record EncryptedSecret(byte[] Nonce, byte[] Cipher, byte[] Tag);
