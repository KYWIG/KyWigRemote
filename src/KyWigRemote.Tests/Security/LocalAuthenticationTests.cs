using KyWigRemote.Core.Data;
using KyWigRemote.Core.Model;
using KyWigRemote.Core.Security;
using KyWigRemote.Tests.Data;

namespace KyWigRemote.Tests.Security;

/// <summary>
/// Tests du repository des comptes locaux et de l'authentificateur local (bout en bout).
/// </summary>
public class LocalAuthenticationTests
{
    // Valeurs factices de test, explicitement nommées comme telles (règle nº 5).
    private const string FakePassword = "Factice-Admin-2026!";

    private static (SqliteLocalAccountRepository Accounts, PasswordHasher Hasher) NewStack(TempDatabase temp)
    {
        temp.Database.Initialize();
        return (new SqliteLocalAccountRepository(temp.Database), new PasswordHasher());
    }

    [Fact]
    public void HasAnyAccount_EstFauxAvantCreationPuisVraiApres()
    {
        using var temp = new TempDatabase();
        (SqliteLocalAccountRepository accounts, PasswordHasher hasher) = NewStack(temp);

        Assert.False(accounts.HasAnyAccount());

        accounts.CreateAccount(new LocalAccount
        {
            Username = "admin", IsAdmin = true, PasswordHash = hasher.Hash(FakePassword),
        });

        Assert.True(accounts.HasAnyAccount());
    }

    [Fact]
    public void FindByUsername_EstInsensibleALaCasse()
    {
        using var temp = new TempDatabase();
        (SqliteLocalAccountRepository accounts, PasswordHasher hasher) = NewStack(temp);
        accounts.CreateAccount(new LocalAccount
        {
            Username = "Admin", IsAdmin = true, PasswordHash = hasher.Hash(FakePassword),
        });

        Assert.NotNull(accounts.FindByUsername("ADMIN"));
        Assert.Null(accounts.FindByUsername("inconnu"));
    }

    [Fact]
    public void Authenticate_AvecBonsIdentifiants_RetourneIdentiteAdmin()
    {
        using var temp = new TempDatabase();
        (SqliteLocalAccountRepository accounts, PasswordHasher hasher) = NewStack(temp);
        accounts.CreateAccount(new LocalAccount
        {
            Username = "admin", DisplayName = "Administrateur", IsAdmin = true,
            PasswordHash = hasher.Hash(FakePassword),
        });
        var authenticator = new LocalAuthenticator(accounts, hasher);

        AuthenticatedUser? user = authenticator.Authenticate("admin", FakePassword);

        Assert.NotNull(user);
        Assert.Equal("admin", user!.Username);
        Assert.True(user.IsAdmin);
        Assert.Equal("Local", user.Provider);
    }

    [Fact]
    public void Authenticate_AvecMauvaisMotDePasse_RetourneNull()
    {
        using var temp = new TempDatabase();
        (SqliteLocalAccountRepository accounts, PasswordHasher hasher) = NewStack(temp);
        accounts.CreateAccount(new LocalAccount
        {
            Username = "admin", IsAdmin = true, PasswordHash = hasher.Hash(FakePassword),
        });
        var authenticator = new LocalAuthenticator(accounts, hasher);

        Assert.Null(authenticator.Authenticate("admin", "mauvais"));
    }

    [Fact]
    public void Authenticate_CompteDesactive_RetourneNull()
    {
        using var temp = new TempDatabase();
        (SqliteLocalAccountRepository accounts, PasswordHasher hasher) = NewStack(temp);
        accounts.CreateAccount(new LocalAccount
        {
            Username = "admin", IsAdmin = true, Disabled = true,
            PasswordHash = hasher.Hash(FakePassword),
        });
        var authenticator = new LocalAuthenticator(accounts, hasher);

        Assert.Null(authenticator.Authenticate("admin", FakePassword));
    }
}
