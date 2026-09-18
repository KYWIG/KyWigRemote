using KyWigRemote.Core.Data;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Security;

/// <summary>
/// Authentifie un utilisateur à partir d'un compte local (identifiant + mot de passe).
/// Fournisseur « Local » ; l'AD et Microsoft 365 auront leurs propres authentificateurs.
/// </summary>
public sealed class LocalAuthenticator
{
    /// <summary>Nom du fournisseur, tel qu'il apparaît dans la configuration et l'identité.</summary>
    public const string ProviderName = "Local";

    private readonly ILocalAccountRepository _accounts;
    private readonly PasswordHasher _hasher;

    public LocalAuthenticator(ILocalAccountRepository accounts, PasswordHasher hasher)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
    }

    /// <summary>
    /// Vérifie les identifiants. Retourne l'identité en cas de succès, null sinon
    /// (compte inexistant, désactivé, ou mot de passe erroné — sans distinguer les cas,
    /// pour ne pas renseigner un attaquant).
    /// </summary>
    public AuthenticatedUser? Authenticate(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        LocalAccount? account = _accounts.FindByUsername(username);
        if (account is null || account.Disabled)
        {
            return null;
        }

        if (!_hasher.Verify(password, account.PasswordHash))
        {
            return null;
        }

        return new AuthenticatedUser(account.Username, account.DisplayName, account.IsAdmin, ProviderName);
    }
}
