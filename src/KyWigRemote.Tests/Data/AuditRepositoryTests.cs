using KyWigRemote.Core.Data;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Tests.Data;

/// <summary>Tests du journal d'audit : écriture, ordre, et filtres (E9).</summary>
public class AuditRepositoryTests
{
    private static SqliteAuditRepository NewRepository(TempDatabase temp)
    {
        temp.Database.Initialize();
        return new SqliteAuditRepository(temp.Database);
    }

    [Fact]
    public void Append_PuisQuery_RetourneLEvenement()
    {
        using var temp = new TempDatabase();
        SqliteAuditRepository repo = NewRepository(temp);

        repo.Append(new AuditEvent
        {
            UserName = "alice", Action = "LOGIN", Result = "OK",
        });

        AuditEvent e = Assert.Single(repo.Query(new AuditQuery()));
        Assert.Equal("alice", e.UserName);
        Assert.Equal("LOGIN", e.Action);
        Assert.Equal("OK", e.Result);
    }

    [Fact]
    public void Query_FiltreParUtilisateurEtResultat()
    {
        using var temp = new TempDatabase();
        SqliteAuditRepository repo = NewRepository(temp);
        repo.Append(new AuditEvent { UserName = "alice", Action = "LOGIN", Result = "OK" });
        repo.Append(new AuditEvent { UserName = "bob", Action = "LOGIN", Result = "DENIED" });

        Assert.Single(repo.Query(new AuditQuery { UserName = "alice" }));
        AuditEvent denied = Assert.Single(repo.Query(new AuditQuery { Result = "DENIED" }));
        Assert.Equal("bob", denied.UserName);
    }

    [Fact]
    public void Query_RetourneLePlusRecentDAbord_EtRespecteLaLimite()
    {
        using var temp = new TempDatabase();
        SqliteAuditRepository repo = NewRepository(temp);
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;
        repo.Append(new AuditEvent { Action = "A", OccurredAt = baseTime.AddMinutes(-2) });
        repo.Append(new AuditEvent { Action = "B", OccurredAt = baseTime.AddMinutes(-1) });
        repo.Append(new AuditEvent { Action = "C", OccurredAt = baseTime });

        IReadOnlyList<AuditEvent> limited = repo.Query(new AuditQuery { Limit = 2 });
        Assert.Equal(2, limited.Count);
        Assert.Equal("C", limited[0].Action); // le plus récent d'abord
        Assert.Equal("B", limited[1].Action);
    }
}
