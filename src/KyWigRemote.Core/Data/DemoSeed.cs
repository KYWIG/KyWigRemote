using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Data;

/// <summary>
/// Jeu de données de démonstration inséré à la création de la base (E2.7), pour disposer
/// d'une arborescence exploitable en développement.
///
/// Aucune donnée réelle, aucun secret, aucune adresse de production : uniquement des
/// hôtes de documentation (plage 192.0.2.0/24, RFC 5737). À vider avant mise en service.
/// </summary>
public static class DemoSeed
{
    /// <summary>Insère les trois entités et quelques connexions d'exemple via le repository.</summary>
    public static void Populate(IConnectionRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        int kywig = repository.AddFolder(new ConnectionFolder { Name = "KyWig", CredentialMode = CredentialMode.Personal }, null);
        int serveurs = repository.AddFolder(new ConnectionFolder { Name = "Serveurs", CredentialMode = CredentialMode.Inherited }, kywig);
        int reseau = repository.AddFolder(new ConnectionFolder { Name = "Réseau", CredentialMode = CredentialMode.Inherited }, kywig);

        repository.AddConnection(new RemoteConnection
        {
            Name = "Serveur fichiers (démo)", Protocol = RemoteProtocol.Rdp,
            Host = "192.0.2.14", Port = 3389, CredentialMode = CredentialMode.Personal,
            Description = "Exemple de connexion RDP en mode personnel.",
        }, serveurs);
        repository.AddConnection(new RemoteConnection
        {
            Name = "Hyperviseur (démo)", Protocol = RemoteProtocol.Ssh,
            Host = "192.0.2.20", Port = 22, CredentialMode = CredentialMode.Enforced,
            Description = "Exemple de connexion SSH en mode imposé.",
        }, serveurs);
        repository.AddConnection(new RemoteConnection
        {
            Name = "Switch coeur (démo)", Protocol = RemoteProtocol.Ssh,
            Host = "192.0.2.2", Port = 22, CredentialMode = CredentialMode.Prompt,
            Description = "Exemple de connexion SSH en mode à la demande.",
        }, reseau);

        int kermazegan = repository.AddFolder(new ConnectionFolder { Name = "Kermazegan", CredentialMode = CredentialMode.Personal }, null);
        repository.AddConnection(new RemoteConnection
        {
            Name = "Serveur applicatif (démo)", Protocol = RemoteProtocol.Rdp,
            Host = "192.0.2.60", Port = 3389, CredentialMode = CredentialMode.Personal,
        }, kermazegan);

        int karantez = repository.AddFolder(new ConnectionFolder { Name = "Karantez", CredentialMode = CredentialMode.Enforced }, null);
        repository.AddConnection(new RemoteConnection
        {
            Name = "Switch site (démo)", Protocol = RemoteProtocol.Ssh,
            Host = "192.0.2.130", Port = 22, CredentialMode = CredentialMode.Enforced,
        }, karantez);
    }
}
