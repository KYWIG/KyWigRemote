using KyWigRemote.Core.Data;
using KyWigRemote.Core.Directory;
using KyWigRemote.Core.Model;
using KyWigRemote.Tests.Data;

namespace KyWigRemote.Tests.Ad;

/// <summary>
/// Tests de la synchronisation AD (logique de profil + upsert + désactivation), avec un annuaire
/// factice — sans dépendre d'un vrai Active Directory.
/// </summary>
public class AdSyncTests
{
    private sealed class FakeDirectory : IDirectory
    {
        public Dictionary<string, List<DirectoryMember>> Groups { get; } = new();

        public bool IsMemberOf(string samAccountName, string groupName) =>
            Groups.TryGetValue(groupName, out List<DirectoryMember>? m)
            && m.Exists(x => string.Equals(x.SamAccountName, samAccountName, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<DirectoryMember> GetGroupMembers(string groupName) =>
            Groups.TryGetValue(groupName, out List<DirectoryMember>? m) ? m : new List<DirectoryMember>();

        public bool TestConnection() => true;
    }

    private static readonly DirectoryMember Alice = new("S-1-5-21-alice", "alice", "Alice");
    private static readonly DirectoryMember Bob = new("S-1-5-21-bob", "bob", "Bob");

    [Fact]
    public void Synchronize_AttribueLeProfilLePlusPrivilegie_EtImporte()
    {
        using var temp = new TempDatabase();
        temp.Database.Initialize();
        var users = new SqliteUserRepository(temp.Database);
        var dir = new FakeDirectory
        {
            Groups =
            {
                ["Users"] = new() { Alice, Bob },
                ["ConnAdmins"] = new() { Bob }, // Bob est aussi admin des connexions
                ["Admins"] = new(),
            },
        };
        var sync = new AdSyncService(dir, users);

        AdSyncOutcome outcome = sync.Synchronize("Users", "ConnAdmins", "Admins");

        Assert.Equal(2, outcome.Imported);
        Assert.Equal(0, outcome.Updated);
        Assert.Equal(0, outcome.Deactivated);

        IReadOnlyList<AdUserRecord> list = users.ListAdUsers();
        Assert.Equal(UserRole.User, list.Single(u => u.SamAccountName == "alice").Role);
        Assert.Equal(UserRole.ConnectionAdmin, list.Single(u => u.SamAccountName == "bob").Role);
        Assert.All(list, u => Assert.True(u.Active));
    }

    [Fact]
    public void Synchronize_DesactiveLesAbsents_ALaSyncSuivante()
    {
        using var temp = new TempDatabase();
        temp.Database.Initialize();
        var users = new SqliteUserRepository(temp.Database);
        var dir = new FakeDirectory { Groups = { ["Users"] = new() { Alice, Bob } } };
        var sync = new AdSyncService(dir, users);

        sync.Synchronize("Users", "ConnAdmins", "Admins");
        Thread.Sleep(20); // garantit un horodatage de synchro strictement postérieur

        // Bob quitte le groupe.
        dir.Groups["Users"] = new() { Alice };
        AdSyncOutcome outcome = sync.Synchronize("Users", "ConnAdmins", "Admins");

        Assert.Equal(0, outcome.Imported);
        Assert.Equal(1, outcome.Updated);      // alice mise à jour
        Assert.Equal(1, outcome.Deactivated);  // bob désactivé

        IReadOnlyList<AdUserRecord> list = users.ListAdUsers();
        Assert.True(list.Single(u => u.SamAccountName == "alice").Active);
        Assert.False(list.Single(u => u.SamAccountName == "bob").Active);
    }
}
