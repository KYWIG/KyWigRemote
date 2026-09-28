using System.Drawing;

namespace KyWigRemote.Shared.UI;

/// <summary>Thème d'interface disponible.</summary>
public enum AppTheme
{
    /// <summary>Thème sombre (par défaut, style outil d'administration).</summary>
    Dark,

    /// <summary>Thème clair.</summary>
    Light,
}

/// <summary>
/// Jeu de couleurs d'un thème. Deux instances figées : <see cref="Dark"/> et <see cref="Light"/>.
/// Les couleurs sont exposées à l'interface via la façade <see cref="Palette"/>, qui reflète le
/// thème actif (<see cref="ThemeManager"/>).
/// </summary>
public sealed class ThemeColors
{
    public required Color Background { get; init; }
    public required Color PanelBackground { get; init; }
    public required Color InputBackground { get; init; }
    public required Color Border { get; init; }
    public required Color Text { get; init; }
    public required Color TextMuted { get; init; }
    public required Color Accent { get; init; }
    public required Color SelectedRow { get; init; }
    public required Color Hover { get; init; }
    public required Color Success { get; init; }
    public required Color Warning { get; init; }
    public required Color Error { get; init; }

    private static Color Hex(string hex) => ColorTranslator.FromHtml(hex);

    /// <summary>Thème sombre (inspiration « Visual Studio Dark », comme mRemoteNG).</summary>
    public static readonly ThemeColors Dark = new()
    {
        Background = Hex("#2D2D30"),
        PanelBackground = Hex("#252526"),
        InputBackground = Hex("#333337"),
        Border = Hex("#3F3F46"),
        Text = Hex("#F1F1F1"),
        TextMuted = Hex("#9B9B9B"),
        Accent = Hex("#007ACC"),
        SelectedRow = Hex("#094771"),
        Hover = Hex("#3E3E42"),
        Success = Hex("#4EC9B0"),
        Warning = Hex("#CE9178"),
        Error = Hex("#F44747"),
    };

    /// <summary>Thème clair (fond clair, texte sombre ; même accent bleu).</summary>
    public static readonly ThemeColors Light = new()
    {
        Background = Hex("#F3F3F3"),
        PanelBackground = Hex("#FFFFFF"),
        InputBackground = Hex("#FFFFFF"),
        Border = Hex("#C8CACC"),
        Text = Hex("#1E1E1E"),
        TextMuted = Hex("#6E6E6E"),
        Accent = Hex("#007ACC"),
        SelectedRow = Hex("#CCE4F7"),
        Hover = Hex("#E5F1FB"),
        Success = Hex("#1A7F37"),
        Warning = Hex("#B25E00"),
        Error = Hex("#C42B1C"),
    };
}
