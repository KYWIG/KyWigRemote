using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Admin.Forms;

/// <summary>
/// Affiche, une seule fois, les identifiants de l'admin de test qui vient d'être créé.
/// Le mot de passe n'est pas stocké en clair : il faut le noter ou le copier maintenant.
/// Réservé au développement (bouton visible uniquement sur un serveur local).
/// </summary>
internal sealed class TestAdminCreatedDialog : Form
{
    public TestAdminCreatedDialog(string username, string password)
    {
        Text = "Admin de test créé";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(440, 236);
        BackColor = Palette.Background;
        ForeColor = Palette.Text;
        Font = new Font("Segoe UI", 9f);

        var intro = new Label
        {
            Text = "Compte administrateur global de test créé. Notez ou copiez le mot de passe : "
                 + "il ne sera plus affiché.",
            AutoSize = false,
            Location = new Point(16, 14),
            Size = new Size(408, 40),
            ForeColor = Palette.TextMuted,
        };

        TextBox userBox = MakeReadonly("Identifiant :", 62, username);
        TextBox passBox = MakeReadonly("Mot de passe :", 116, password);

        var copyButton = new Button
        {
            Text = "Copier identifiant + mot de passe",
            Location = new Point(16, 170),
            Width = 250,
            FlatStyle = FlatStyle.Flat,
            BackColor = Palette.InputBackground,
            ForeColor = Palette.Text,
        };
        copyButton.FlatAppearance.BorderColor = Palette.Border;
        copyButton.Click += (_, _) =>
        {
            try { Clipboard.SetText($"{username}\t{password}"); }
            catch (System.Runtime.InteropServices.ExternalException) { /* presse-papiers indisponible */ }
            copyButton.Text = "Copié ✓";
        };

        var closeButton = new Button
        {
            Text = "Fermer",
            Location = new Point(340, 170),
            Width = 84,
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Palette.InputBackground,
            ForeColor = Palette.Text,
        };
        closeButton.FlatAppearance.BorderColor = Palette.Border;

        AcceptButton = closeButton;
        CancelButton = closeButton;

        Controls.Add(intro);
        Controls.Add(copyButton);
        Controls.Add(closeButton);
        _ = userBox;
        _ = passBox;
    }

    private TextBox MakeReadonly(string label, int top, string value)
    {
        var caption = new Label
        {
            Text = label,
            AutoSize = true,
            Location = new Point(16, top),
            ForeColor = Palette.Text,
        };
        var box = new TextBox
        {
            Text = value,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Palette.InputBackground,
            ForeColor = Palette.Text,
            Location = new Point(16, top + 22),
            Width = 408,
        };
        box.Enter += (_, _) => box.SelectAll(); // sélection au clic pour copier facilement
        Controls.Add(caption);
        Controls.Add(box);
        return box;
    }
}
