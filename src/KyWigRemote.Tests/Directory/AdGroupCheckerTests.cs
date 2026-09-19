using KyWigRemote.Core.Directory;

namespace KyWigRemote.Tests.Ad;

/// <summary>
/// Tests de la vérification de groupe réelle contre Active Directory. Nécessitent un
/// contrôleur de domaine : marqués RequiresAD et exclus des exécutions par défaut.
/// </summary>
public class AdGroupCheckerTests
{
    [Trait("Category", "RequiresAD")]
    [Fact]
    public void IsMemberOf_SExecuteContreLeDomaine_EtRepondFalsePourUnGroupeInexistant()
    {
        WindowsUser me = AdIdentity.Current();
        var checker = new AdGroupChecker();

        bool result = checker.IsMemberOf(me.SamAccountName, "GG_Inexistant_" + Guid.NewGuid().ToString("N"));

        Assert.False(result); // le vrai intérêt : l'appel a atteint l'AD sans lever d'exception
    }
}
