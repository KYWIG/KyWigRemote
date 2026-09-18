using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Data;

/// <summary>
/// Accès aux comptes locaux applicatifs. Le hachage du mot de passe est fourni/retourné
/// tel quel : cette couche ne connaît pas le mot de passe en clair.
/// </summary>
public interface ILocalAccountRepository
{
    /// <summary>Indique s'il existe au moins un compte local (utile pour l'amorçage du premier admin).</summary>
    bool HasAnyAccount();

    /// <summary>Liste tous les comptes locaux (le hachage est présent mais ne doit pas être exposé au client).</summary>
    IReadOnlyList<LocalAccount> ListAccounts();

    /// <summary>Retourne le compte correspondant à l'identifiant, ou null s'il n'existe pas.</summary>
    LocalAccount? FindByUsername(string username);

    /// <summary>Crée un compte (le hachage est déjà calculé) et retourne son identifiant.</summary>
    int CreateAccount(LocalAccount account);
}
