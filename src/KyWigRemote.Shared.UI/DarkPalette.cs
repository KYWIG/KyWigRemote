using System.Drawing;

namespace KyWigRemote.Shared.UI;

/// <summary>
/// Palette du thème sombre, reprise de docs/04-ui-spec.md §2
/// (inspiration « Visual Studio Dark » comme le thème sombre de mRemoteNG).
/// Point unique de vérité pour les couleurs, partagé par le client et l'administration.
/// </summary>
public static class DarkPalette
{
    /// <summary>Fond principal de la fenêtre.</summary>
    public static readonly Color Background = FromHex("#2D2D30");

    /// <summary>Fond des panneaux dockés.</summary>
    public static readonly Color PanelBackground = FromHex("#252526");

    /// <summary>Fond des champs de saisie.</summary>
    public static readonly Color InputBackground = FromHex("#333337");

    /// <summary>Bordures et séparateurs.</summary>
    public static readonly Color Border = FromHex("#3F3F46");

    /// <summary>Texte principal.</summary>
    public static readonly Color Text = FromHex("#F1F1F1");

    /// <summary>Texte secondaire, libellés atténués.</summary>
    public static readonly Color TextMuted = FromHex("#9B9B9B");

    /// <summary>Couleur d'accent et de sélection.</summary>
    public static readonly Color Accent = FromHex("#007ACC");

    /// <summary>Fond de la ligne sélectionnée dans l'arbre.</summary>
    public static readonly Color SelectedRow = FromHex("#094771");

    /// <summary>Fond au survol.</summary>
    public static readonly Color Hover = FromHex("#3E3E42");

    /// <summary>Indication de succès (session connectée).</summary>
    public static readonly Color Success = FromHex("#4EC9B0");

    /// <summary>Indication d'alerte.</summary>
    public static readonly Color Warning = FromHex("#CE9178");

    /// <summary>Indication d'erreur.</summary>
    public static readonly Color Error = FromHex("#F44747");

    /// <summary>Convertit une couleur hexadécimale « #RRGGBB » en <see cref="Color"/>.</summary>
    private static Color FromHex(string hex) => ColorTranslator.FromHtml(hex);
}
