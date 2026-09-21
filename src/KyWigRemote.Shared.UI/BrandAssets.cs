using System.Drawing;
using System.Reflection;

namespace KyWigRemote.Shared.UI;

/// <summary>
/// Ressources de marque partagées par le client, l'administration et le gestionnaire de serveur.
/// L'icône (monogramme KyWigRemote) est embarquée dans cet assembly et chargée une seule fois,
/// afin que toutes les fenêtres affichent la même identité visuelle.
/// </summary>
public static class BrandAssets
{
    private const string IconResourceName = "KyWigRemote.Shared.UI.kywig.ico";

    private static Icon? _appIcon;

    /// <summary>Icône de l'application. Repli sur l'icône système si la ressource est absente.</summary>
    public static Icon AppIcon => _appIcon ??= LoadIcon();

    private static Icon LoadIcon()
    {
        Assembly assembly = typeof(BrandAssets).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(IconResourceName);
        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }
}
