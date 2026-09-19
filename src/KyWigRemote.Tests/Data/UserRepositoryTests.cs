using KyWigRemote.Core.Data;
using KyWigRemote.Core.Directory;

namespace KyWigRemote.Tests.Data;

/// <summary>Tests de l'enregistrement des utilisateurs AD (E3.5).</summary>
public class UserRepositoryTests
{
    [Fact]
    public void Upsert_CreePuisMetAJour_LeMemeSid()
    {
        using var temp = new TempDatabase();
        temp.Database.Initialize();
        var repo = new SqliteUserRepository(temp.Database);

        repo.Upsert(new WindowsUser("S-1-5-21-1", "n.marchand", "Nolhan", "KYWIG"));
        WindowsUser? first = repo.GetBySid("S-1-5-21-1");
        Assert.NotNull(first);
        Assert.Equal("n.marchand", first!.SamAccountName);

        // Même SID, compte renommé : mise à jour, pas de doublon.
        repo.Upsert(new WindowsUser("S-1-5-21-1", "n.marchand2", "Nolhan M.", "KYWIG"));
        WindowsUser? updated = repo.GetBySid("S-1-5-21-1");
        Assert.Equal("n.marchand2", updated!.SamAccountName);
        Assert.Equal("Nolhan M.", updated.DisplayName);
    }

    [Fact]
    public void GetBySid_Inconnu_RetourneNull()
    {
        using var temp = new TempDatabase();
        temp.Database.Initialize();
        var repo = new SqliteUserRepository(temp.Database);

        Assert.Null(repo.GetBySid("S-1-5-21-999"));
    }
}
