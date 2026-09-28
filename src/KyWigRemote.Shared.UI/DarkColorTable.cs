using System.Drawing;
using System.Windows.Forms;

namespace KyWigRemote.Shared.UI;

/// <summary>
/// Table de couleurs sombre pour les <see cref="ToolStrip"/> (menu, barre d'outils,
/// barre d'état). Sans elle, ces barres resteraient gris clair et trahiraient le thème.
/// </summary>
public sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuStripGradientBegin => Palette.PanelBackground;
    public override Color MenuStripGradientEnd => Palette.PanelBackground;

    public override Color ToolStripGradientBegin => Palette.PanelBackground;
    public override Color ToolStripGradientMiddle => Palette.PanelBackground;
    public override Color ToolStripGradientEnd => Palette.PanelBackground;
    public override Color ToolStripContentPanelGradientBegin => Palette.PanelBackground;
    public override Color ToolStripContentPanelGradientEnd => Palette.PanelBackground;
    public override Color ToolStripPanelGradientBegin => Palette.PanelBackground;
    public override Color ToolStripPanelGradientEnd => Palette.PanelBackground;

    public override Color ToolStripBorder => Palette.Border;
    public override Color MenuBorder => Palette.Border;
    public override Color MenuItemBorder => Palette.Accent;

    public override Color ImageMarginGradientBegin => Palette.PanelBackground;
    public override Color ImageMarginGradientMiddle => Palette.PanelBackground;
    public override Color ImageMarginGradientEnd => Palette.PanelBackground;

    public override Color MenuItemSelected => Palette.Hover;
    public override Color MenuItemSelectedGradientBegin => Palette.Hover;
    public override Color MenuItemSelectedGradientEnd => Palette.Hover;
    public override Color MenuItemPressedGradientBegin => Palette.PanelBackground;
    public override Color MenuItemPressedGradientEnd => Palette.PanelBackground;

    public override Color ButtonSelectedGradientBegin => Palette.Hover;
    public override Color ButtonSelectedGradientMiddle => Palette.Hover;
    public override Color ButtonSelectedGradientEnd => Palette.Hover;
    public override Color ButtonPressedGradientBegin => Palette.SelectedRow;
    public override Color ButtonPressedGradientMiddle => Palette.SelectedRow;
    public override Color ButtonPressedGradientEnd => Palette.SelectedRow;
    public override Color ButtonSelectedBorder => Palette.Accent;

    public override Color SeparatorDark => Palette.Border;
    public override Color SeparatorLight => Palette.Border;

    public override Color StatusStripGradientBegin => Palette.PanelBackground;
    public override Color StatusStripGradientEnd => Palette.PanelBackground;
}
