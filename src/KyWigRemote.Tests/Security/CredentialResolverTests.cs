using KyWigRemote.Core.Model;
using KyWigRemote.Core.Security;

namespace KyWigRemote.Tests.Security;

/// <summary>
/// Tests du résolveur de mode d'identifiants (E4.5) : les 4 modes, l'héritage sur
/// plusieurs niveaux et les cas d'absence (E4.11).
/// </summary>
public class CredentialResolverTests
{
    private static ConnectionFolder Folder(CredentialMode mode) => new() { CredentialMode = mode };
    private static RemoteConnection Connection(CredentialMode mode) => new() { CredentialMode = mode };

    [Theory]
    [InlineData(CredentialMode.Personal)]
    [InlineData(CredentialMode.Enforced)]
    [InlineData(CredentialMode.Prompt)]
    public void ModeExplicite_SurLaConnexion_Prime(CredentialMode explicite)
    {
        // Même avec un parent d'un autre mode, le mode explicite de la connexion l'emporte.
        var ancestors = new[] { Folder(CredentialMode.Enforced) };
        CredentialMode resolu = CredentialResolver.ResolveEffectiveMode(Connection(explicite), ancestors);
        Assert.Equal(explicite, resolu);
    }

    [Fact]
    public void Herite_PrendLeModeDuParentDirect()
    {
        var ancestors = new[] { Folder(CredentialMode.Enforced), Folder(CredentialMode.Personal) };
        CredentialMode resolu = CredentialResolver.ResolveEffectiveMode(Connection(CredentialMode.Inherited), ancestors);
        Assert.Equal(CredentialMode.Enforced, resolu);
    }

    [Fact]
    public void Herite_RemonteSurTroisNiveauxJusquAuPremierModeExplicite()
    {
        // Parent direct hérité, grand-parent hérité, arrière-grand-parent = Personnel.
        var ancestors = new[]
        {
            Folder(CredentialMode.Inherited),
            Folder(CredentialMode.Inherited),
            Folder(CredentialMode.Personal),
        };
        CredentialMode resolu = CredentialResolver.ResolveEffectiveMode(Connection(CredentialMode.Inherited), ancestors);
        Assert.Equal(CredentialMode.Personal, resolu);
    }

    [Fact]
    public void Herite_SansAucunModeExplicite_DonneDemande()
    {
        var ancestors = new[] { Folder(CredentialMode.Inherited), Folder(CredentialMode.Inherited) };
        CredentialMode resolu = CredentialResolver.ResolveEffectiveMode(Connection(CredentialMode.Inherited), ancestors);
        Assert.Equal(CredentialMode.Prompt, resolu);
    }

    [Fact]
    public void Herite_SansAucunParent_DonneDemande()
    {
        CredentialMode resolu = CredentialResolver.ResolveEffectiveMode(
            Connection(CredentialMode.Inherited), Array.Empty<ConnectionFolder>());
        Assert.Equal(CredentialMode.Prompt, resolu);
    }

    [Fact]
    public void FindAncestors_RetrouveLaChaineDuPlusProcheAuPlusLointain()
    {
        var connection = new RemoteConnection { Id = 42, Name = "cible" };
        var serveurs = new ConnectionFolder { Id = 2, Name = "Serveurs" };
        serveurs.Connections.Add(connection);
        var racine = new ConnectionFolder { Id = 1, Name = "KyWig" };
        racine.SubFolders.Add(serveurs);

        IReadOnlyList<ConnectionFolder>? ancestors = CredentialResolver.FindAncestors(new[] { racine }, 42);

        Assert.NotNull(ancestors);
        Assert.Equal(new[] { "Serveurs", "KyWig" }, ancestors!.Select(f => f.Name));
    }

    [Fact]
    public void ResolveEffectiveMode_ViaArborescence_UtiliseLesParents()
    {
        // KyWig (Enforced) > Serveurs (Inherited) > connexion (Inherited) => Enforced.
        var connection = new RemoteConnection { Id = 7, CredentialMode = CredentialMode.Inherited };
        var serveurs = new ConnectionFolder { Id = 2, CredentialMode = CredentialMode.Inherited };
        serveurs.Connections.Add(connection);
        var racine = new ConnectionFolder { Id = 1, CredentialMode = CredentialMode.Enforced };
        racine.SubFolders.Add(serveurs);

        CredentialMode resolu = CredentialResolver.ResolveEffectiveMode(new[] { racine }, connection);

        Assert.Equal(CredentialMode.Enforced, resolu);
    }
}
