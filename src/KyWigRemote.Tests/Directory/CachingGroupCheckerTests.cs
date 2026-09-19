using KyWigRemote.Core.Directory;

namespace KyWigRemote.Tests.Ad;

/// <summary>Tests de la mise en cache des vérifications de groupe (E3.2), sans accès AD réel.</summary>
public class CachingGroupCheckerTests
{
    private sealed class CountingChecker : IGroupChecker
    {
        public int Calls { get; private set; }
        public bool Result { get; set; } = true;

        public bool IsMemberOf(string samAccountName, string groupName)
        {
            Calls++;
            return Result;
        }
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void DeuxAppelsIdentiques_DansLaFenetre_NInterrogentLAnnuaireQuUneFois()
    {
        var inner = new CountingChecker();
        var clock = new TestClock();
        var cache = new CachingGroupChecker(inner, TimeSpan.FromMinutes(5), clock);

        Assert.True(cache.IsMemberOf("alice", "GG_Users"));
        Assert.True(cache.IsMemberOf("alice", "GG_Users"));

        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public void ApresExpiration_LAnnuaireEstReinterroge()
    {
        var inner = new CountingChecker();
        var clock = new TestClock();
        var cache = new CachingGroupChecker(inner, TimeSpan.FromMinutes(5), clock);

        cache.IsMemberOf("alice", "GG_Users");
        clock.Now = clock.Now.AddMinutes(6); // au-delà du TTL
        cache.IsMemberOf("alice", "GG_Users");

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public void DesClesDifferentes_SontMisesEnCacheSeparement()
    {
        var inner = new CountingChecker();
        var cache = new CachingGroupChecker(inner, TimeSpan.FromMinutes(5), new TestClock());

        cache.IsMemberOf("alice", "GG_Users");
        cache.IsMemberOf("alice", "GG_Admins");
        cache.IsMemberOf("bob", "GG_Users");

        Assert.Equal(3, inner.Calls);
    }
}
