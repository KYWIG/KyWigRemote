using System.Drawing;
using System.Windows.Forms;

namespace KyWigRemote.Shared.UI;

/// <summary>
/// Table de couleurs sombre pour les <see cref="ToolStrip"/> (menu, barre d'outils,
/// barre d'état). Sans elle, ces barres resteraient gris clair et trahiraient le thème.
/// </summary>
public sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuStripGradientBegin => DarkPalette.PanelBackground;
    public override Color MenuStripGradientEnd => DarkPalette.PanelBackground;

    public override Color ToolStripGradientBegin => DarkPalette.PanelBackground;
    public override Color ToolStripGradientMiddle => DarkPalette.PanelBackground;
    public override Color ToolStripGradientEnd => DarkPalette.PanelBackground;
    public override Color ToolStripContentPanelGradientBegin => DarkPalette.PanelBackground;
    public override Color ToolStripContentPanelGradientEnd => DarkPalette.PanelBackground;
    public override Color ToolStripPanelGradientBegin => DarkPalette.PanelBackground;
    public override Color ToolStripPanelGradientEnd => DarkPalette.PanelBackground;

    public override Color ToolStripBorder => DarkPalette.Border;
    public override Color MenuBorder => DarkPalette.Border;
    public override Color MenuItemBorder => DarkPalette.Accent;

    public override Color ImageMarginGradientBegin => DarkPalette.PanelBackground;
    public override Color ImageMarginGradientMiddle => DarkPalette.PanelBackground;
    public override Color ImageMarginGradientEnd => DarkPalette.PanelBackground;

    public override Color MenuItemSelected => DarkPalette.Hover;
    public override Color MenuItemSelectedGradientBegin => DarkPalette.Hover;
    public override Color MenuItemSelectedGradientEnd => DarkPalette.Hover;
    public override Color MenuItemPressedGradientBegin => DarkPalette.PanelBackground;
    public override Color MenuItemPressedGradientEnd => DarkPalette.PanelBackground;

    public override Color ButtonSelectedGradientBegin => DarkPalette.Hover;
    public override Color ButtonSelectedGradientMiddle => DarkPalette.Hover;
    public override Color ButtonSelectedGradientEnd => DarkPalette.Hover;
    public override Color ButtonPressedGradientBegin => DarkPalette.SelectedRow;
    public override Color ButtonPressedGradientMiddle => DarkPalette.SelectedRow;
    public override Color ButtonPressedGradientEnd => DarkPalette.SelectedRow;
    public override Color ButtonSelectedBorder => DarkPalette.Accent;

    public override Color SeparatorDark => DarkPalette.Border;
    public override Color SeparatorLight => DarkPalette.Border;

    public override Color StatusStripGradientBegin => DarkPalette.PanelBackground;
    public override Color StatusStripGradientEnd => DarkPalette.PanelBackground;
}
