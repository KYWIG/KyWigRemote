using System.Security.Cryptography;
using KyWigRemote.Core.Security;

namespace KyWigRemote.Tests.Security;

/// <summary>
/// Tests de la protection DPAPI des clés au repos. DPAPI est disponible sur tout Windows sans
/// contrôleur de domaine : ces tests s'exécutent partout (pas de catégorie RequiresAD).
/// </summary>
public class MachineKeyProtectorTests
{
    [Fact]
    public void Protect_PuisUnprotect_RestitueLaValeurDOrigine()
    {
        byte[] key = RandomNumberGenerator.GetBytes(CredentialProtector.KeySize);

        string stored = MachineKeyProtector.Protect(key);
        byte[] restored = MachineKeyProtector.Unprotect(stored);

        Assert.Equal(key, restored);
    }

    [Fact]
    public void Protect_ProduitUnBlobMarqueEtDifferentDuClair()
    {
        byte[] key = RandomNumberGenerator.GetBytes(CredentialProtector.KeySize);

        string stored = MachineKeyProtector.Protect(key);

        Assert.StartsWith(MachineKeyProtector.Prefix, stored);
        Assert.True(MachineKeyProtector.IsProtected(stored));
        // Le clair (base64) ne doit pas apparaître tel quel dans la valeur stockée.
        Assert.DoesNotContain(Convert.ToBase64String(key), stored);
    }

    [Fact]
    public void IsProtected_SurUneValeurEnClair_RetourneFaux()
    {
        // Ancien format : base64 brut, sans préfixe (cas migré au démarrage du serveur).
        string legacy = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Assert.False(MachineKeyProtector.IsProtected(legacy));
    }

    [Fact]
    public void Unprotect_SurUneValeurNonProtegee_Leve()
    {
        string legacy = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Assert.Throws<ArgumentException>(() => MachineKeyProtector.Unprotect(legacy));
    }
}
