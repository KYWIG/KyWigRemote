using KyWigRemote.Core.Directory;
using KyWigRemote.Core.Security;

namespace KyWigRemote.Tests.Security;

/// <summary>Tests de l'autorisation d'accès à un identifiant imposé par groupes AD (FR-15).</summary>
public class EnforcedCredentialAuthorizerTests
{
    /// <summary>Vérificateur factice : l'utilisateur est membre des groupes listés à la construction.</summary>
    private sealed class FakeGroups : IGroupChecker
    {
        private readonly HashSet<string> _memberships;
        public FakeGroups(params string[] groups) => _memberships = new HashSet<string>(groups, StringComparer.OrdinalIgnoreCase);
        public bool IsMemberOf(string samAccountName, string groupName) => _memberships.Contains(groupName);
    }

    [Fact]
    public void MembreDUnGroupeAutorise_EstAutorise()
    {
        var groups = new FakeGroups("GG_Reseau");
        Assert.True(EnforcedCredentialAuthorizer.IsAllowedByGroups("alice", "GG_Serveurs;GG_Reseau", groups));
    }

    [Fact]
    public void NonMembre_EstRefuse()
    {
        var groups = new FakeGroups("GG_Autre");
        Assert.False(EnforcedCredentialAuthorizer.IsAllowedByGroups("alice", "GG_Serveurs;GG_Reseau", groups));
    }

    [Fact]
    public void AucunGroupeDefini_EstRefuse()
    {
        var groups = new FakeGroups("GG_Reseau");
        Assert.False(EnforcedCredentialAuthorizer.IsAllowedByGroups("alice", null, groups));
        Assert.False(EnforcedCredentialAuthorizer.IsAllowedByGroups("alice", "  ", groups));
    }
}
