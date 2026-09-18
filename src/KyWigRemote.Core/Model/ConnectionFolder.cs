namespace KyWigRemote.Core.Model;

/// <summary>
/// Dossier de l'arborescence partagée. Un dossier peut contenir des sous-dossiers
/// et des connexions, et définir un mode d'identifiants hérité par ses enfants (FR-03).
/// Correspond à la table <c>folders</c> du schéma.
/// </summary>
public sealed class ConnectionFolder
{
    /// <summary>Identifiant en base ; 0 tant que le dossier n'est pas persisté.</summary>
    public int Id { get; set; }

    /// <summary>Nom affiché dans l'arborescence.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mode d'identifiants du dossier, hérité par ses enfants par défaut.</summary>
    public CredentialMode CredentialMode { get; set; } = CredentialMode.Inherited;

    /// <summary>
    /// Sous-dossiers, dans l'ordre d'affichage. Le setter (public) est nécessaire à la
    /// désérialisation JSON côté client : System.Text.Json ne remplit pas une collection
    /// en lecture seule. L'initialiseur garantit une liste jamais nulle.
    /// </summary>
    public List<ConnectionFolder> SubFolders { get; set; } = new();

    /// <summary>Connexions directement contenues dans ce dossier (voir remarque sur SubFolders).</summary>
    public List<RemoteConnection> Connections { get; set; } = new();
}
