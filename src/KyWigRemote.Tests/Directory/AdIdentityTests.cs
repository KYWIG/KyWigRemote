using KyWigRemote.Core.Directory;

namespace KyWigRemote.Tests.Ad;

/// <summary>
/// Tests de la résolution de l'identité Windows courante (E3.1). Ne nécessitent pas de
/// contrôleur de domaine : le SID et le nom de connexion viennent du jeton Windows local.
/// </summary>
public class AdIdentityTests
{
    [Fact]
    public void Current_RetourneUnSidEtUnNomDeConnexion()
    {
        WindowsUser user = AdIdentity.Current();

        Assert.StartsWith("S-1-", user.Sid);          // format d'un SID Windows
        Assert.False(string.IsNullOrWhiteSpace(user.SamAccountName));
        Assert.DoesNotContain("\\", user.SamAccountName); // le domaine est séparé du nom
    }
}
