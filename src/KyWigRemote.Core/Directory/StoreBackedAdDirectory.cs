using KyWigRemote.Core.Security;

namespace KyWigRemote.Core.Directory;

/// <summary>
/// Annuaire AD qui lit le compte de service courant (chiffré en base) à chaque opération, puis
/// délègue à un <see cref="AdDirectory"/>. Ainsi, modifier le compte de service prend effet
/// immédiatement, sans redémarrer le serveur. La mise en cache des appartenances
/// (<see cref="CachingGroupChecker"/>) limite le coût des lectures répétées.
/// </summary>
public sealed class StoreBackedAdDirectory : IDirectory
{
    private readonly string? _domain;
    private readonly AdServiceAccountStore _store;

    public StoreBackedAdDirectory(string? domain, AdServiceAccountStore store)
    {
        _domain = domain;
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    private AdDirectory Build()
    {
        AdServiceAccount? account = _store.Get();
        return new AdDirectory(_domain, account?.Username, account?.Password);
    }

    public bool IsMemberOf(string samAccountName, string groupName) =>
        Build().IsMemberOf(samAccountName, groupName);

    public IReadOnlyList<DirectoryMember> GetGroupMembers(string groupName) =>
        Build().GetGroupMembers(groupName);

    public bool TestConnection() => Build().TestConnection();
}
