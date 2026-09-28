using System.Drawing;
using System.Reflection;

namespace KyWigRemote.Shared.UI;

/// <summary>
/// Icônes de marque embarquées dans cet assembly, partagées par les applications.
/// Chaque application utilise l'icône correspondant à son rôle (chargée une seule fois).
/// </summary>
public static class BrandAssets
{
    private static Icon? _serverManager;
    private static Icon? _admin;
    private static Icon? _login;
    private static Icon? _client;

    /// <summary>Icône du gestionnaire de serveur (serveur + engrenage).</summary>
    public static Icon ServerManagerIcon => _serverManager ??= Load("server-manager");

    /// <summary>Icône de la console d'administration (serveur + outils).</summary>
    public static Icon AdminIcon => _admin ??= Load("admin");

    /// <summary>Icône des écrans de connexion (utilisateur + clé).</summary>
    public static Icon LoginIcon => _login ??= Load("login");

    /// <summary>Icône du client de connexion (mallette).</summary>
    public static Icon ClientIcon => _client ??= Load("client");

    private static Icon Load(string name)
    {
        Assembly assembly = typeof(BrandAssets).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream($"KyWigRemote.Shared.UI.{name}.ico");
        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }
}
