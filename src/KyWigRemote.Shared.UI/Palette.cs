using System.Drawing;

namespace KyWigRemote.Shared.UI;

/// <summary>
/// Palette de couleurs de l'interface. Point unique de vérité pour l'ensemble des écrans
/// (client, administration, gestionnaire). Reflète le <b>thème actif</b> : chaque couleur est lue
/// au moment de l'accès depuis <see cref="ThemeManager.Current"/>. Fixer le thème
/// (<see cref="ThemeManager.Load"/>) avant de construire les fenêtres.
/// </summary>
public static class Palette
{
    /// <summary>Fond principal de la fenêtre.</summary>
    public static Color Background => ThemeManager.Current.Background;

    /// <summary>Fond des panneaux dockés.</summary>
    public static Color PanelBackground => ThemeManager.Current.PanelBackground;

    /// <summary>Fond des champs de saisie.</summary>
    public static Color InputBackground => ThemeManager.Current.InputBackground;

    /// <summary>Bordures et séparateurs.</summary>
    public static Color Border => ThemeManager.Current.Border;

    /// <summary>Texte principal.</summary>
    public static Color Text => ThemeManager.Current.Text;

    /// <summary>Texte secondaire, libellés atténués.</summary>
    public static Color TextMuted => ThemeManager.Current.TextMuted;

    /// <summary>Couleur d'accent et de sélection.</summary>
    public static Color Accent => ThemeManager.Current.Accent;

    /// <summary>Fond de la ligne sélectionnée dans l'arbre.</summary>
    public static Color SelectedRow => ThemeManager.Current.SelectedRow;

    /// <summary>Fond au survol.</summary>
    public static Color Hover => ThemeManager.Current.Hover;

    /// <summary>Indication de succès (session connectée).</summary>
    public static Color Success => ThemeManager.Current.Success;

    /// <summary>Indication d'alerte.</summary>
    public static Color Warning => ThemeManager.Current.Warning;

    /// <summary>Indication d'erreur.</summary>
    public static Color Error => ThemeManager.Current.Error;
}
