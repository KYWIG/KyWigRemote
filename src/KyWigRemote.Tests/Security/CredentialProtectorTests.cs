using System.Security.Cryptography;
using System.Text;
using KyWigRemote.Core.Security;

namespace KyWigRemote.Tests.Security;

/// <summary>Tests de la primitive de chiffrement des secrets (AES-256-GCM).</summary>
public class CredentialProtectorTests
{
    private readonly CredentialProtector _protector = new();
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(CredentialProtector.KeySize);

    [Fact]
    public void ProtectPuisUnprotect_RestitueLeClairIntact()
    {
        byte[] clair = Encoding.UTF8.GetBytes("Secret-Factice-À-Protéger");

        EncryptedSecret encrypted = _protector.Protect(clair, _key);
        byte[] dechiffre = _protector.Unprotect(encrypted, _key);

        Assert.Equal(clair, dechiffre);
    }

    [Fact]
    public void Protect_GenereUnNonceDifferentAChaqueAppel()
    {
        byte[] clair = Encoding.UTF8.GetBytes("meme-clair");

        EncryptedSecret a = _protector.Protect(clair, _key);
        EncryptedSecret b = _protector.Protect(clair, _key);

        Assert.NotEqual(a.Nonce, b.Nonce);
        Assert.NotEqual(a.Cipher, b.Cipher); // conséquence du nonce différent
    }

    [Fact]
    public void Unprotect_AvecUneMauvaiseCle_Echoue()
    {
        EncryptedSecret encrypted = _protector.Protect(Encoding.UTF8.GetBytes("secret"), _key);
        byte[] autreCle = RandomNumberGenerator.GetBytes(CredentialProtector.KeySize);

        Assert.Throws<AuthenticationTagMismatchException>(() => _protector.Unprotect(encrypted, autreCle));
    }

    [Fact]
    public void Unprotect_SurUnMessageAltere_Echoue()
    {
        EncryptedSecret encrypted = _protector.Protect(Encoding.UTF8.GetBytes("secret"), _key);

        byte[] cipherAltere = (byte[])encrypted.Cipher.Clone();
        cipherAltere[0] ^= 0xFF; // on retourne un bit
        var altere = encrypted with { Cipher = cipherAltere };

        Assert.Throws<AuthenticationTagMismatchException>(() => _protector.Unprotect(altere, _key));
    }

    [Fact]
    public void Protect_AvecUneCleDeMauvaiseTaille_Refuse()
    {
        byte[] cleTropCourte = new byte[16];
        Assert.Throws<ArgumentException>(() => _protector.Protect(Encoding.UTF8.GetBytes("x"), cleTropCourte));
    }
}
