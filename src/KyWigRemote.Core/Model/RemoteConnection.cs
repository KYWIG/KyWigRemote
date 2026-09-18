namespace KyWigRemote.Core.Model;

/// <summary>
/// Connexion distante (RDP ou SSH) de l'arborescence partagée.
/// Correspond à la table <c>connections</c> du schéma (docs/03-architecture.md).
/// </summary>
public sealed class RemoteConnection
{
    /// <summary>Identifiant en base ; 0 tant que la connexion n'est pas persistée.</summary>
    public int Id { get; set; }

    /// <summary>Nom affiché dans l'arborescence et en titre d'onglet.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Protocole d'accès.</summary>
    public RemoteProtocol Protocol { get; set; } = RemoteProtocol.Rdp;

    /// <summary>Nom d'hôte ou adresse IP du serveur cible.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Port d'écoute ; déduit du protocole par défaut, modifiable.</summary>
    public int Port { get; set; } = 3389;

    /// <summary>Domaine d'ouverture de session (facultatif).</summary>
    public string? Domain { get; set; }

    /// <summary>Description libre (facultative).</summary>
    public string? Description { get; set; }

    /// <summary>Mode d'identifiants de la connexion.</summary>
    public CredentialMode CredentialMode { get; set; } = CredentialMode.Inherited;
}
