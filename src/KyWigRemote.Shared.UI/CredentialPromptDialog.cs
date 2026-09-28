using System.Drawing;
using System.Windows.Forms;

namespace KyWigRemote.Shared.UI;

/// <summary>
/// Boîte de saisie d'identifiants (ui-spec §6), affichée en mode PERSONNEL sans identifiant
/// enregistré, et en mode DEMANDE. Les cases « retenir » n'apparaissent qu'en mode personnel.
/// </summary>
public sealed class CredentialPromptDialog : Form
{
    private readonly TextBox _userBox;
    private readonly TextBox _passwordBox;
    private readonly CheckBox? _rememberBox;
    private readonly CheckBox? _rememberGlobalBox;

    /// <summary>Nom d'utilisateur saisi.</summary>
    public string Username => _userBox.Text.Trim();

    /// <summary>Mot de passe saisi.</summary>
    public string Password => _passwordBox.Text;

    /// <summary>Retenir pour cette connexion (mode personnel uniquement).</summary>
    public bool Remember => _rememberBox?.Checked ?? false;

    /// <summary>Retenir pour toutes les connexions de l'utilisateur (mode personnel uniquement).</summary>
    public bool RememberGlobal => _rememberGlobalBox?.Checked ?? false;

    /// <param name="connectionName">Nom de la connexion, affiché en titre.</param>
    /// <param name="defaultUsername">Nom d'utilisateur pré-rempli.</param>
    /// <param name="allowRemember">Affiche les cases « retenir » (mode personnel) ou non (mode demande).</param>
    public CredentialPromptDialog(string connectionName, string? defaultUsername, bool allowRemember)
    {
        Text = $"Identifiants — {connectionName}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(380, allowRemember ? 220 : 170);
        BackColor = Palette.Background;
        ForeColor = Palette.Text;
        Font = new Font("Segoe UI", 9f);

        Controls.Add(new Label { Text = "Utilisateur", ForeColor = Palette.Text, AutoSize = true, Location = new Point(16, 18) });
        _userBox = new TextBox
        {
            Text = defaultUsername ?? string.Empty,
            BorderStyle = BorderStyle.FixedSingle, BackColor = Palette.InputBackground, ForeColor = Palette.Text,
            Location = new Point(16, 40), Width = 348,
        };

        Controls.Add(new Label { Text = "Mot de passe", ForeColor = Palette.Text, AutoSize = true, Location = new Point(16, 72) });
        _passwordBox = new TextBox
        {
            UseSystemPasswordChar = true,
            BorderStyle = BorderStyle.FixedSingle, BackColor = Palette.InputBackground, ForeColor = Palette.Text,
            Location = new Point(16, 94), Width = 348,
        };

        Controls.Add(_userBox);
        Controls.Add(_passwordBox);

        int buttonsTop;
        if (allowRemember)
        {
            _rememberBox = new CheckBox { Text = "Retenir pour cette connexion", ForeColor = Palette.Text, AutoSize = true, Location = new Point(16, 128), Checked = true };
            _rememberGlobalBox = new CheckBox { Text = "Retenir pour toutes mes connexions", ForeColor = Palette.Text, AutoSize = true, Location = new Point(16, 152) };
            Controls.Add(_rememberBox);
            Controls.Add(_rememberGlobalBox);
            Controls.Add(new Label
            {
                Text = "Chiffré côté serveur, associé à votre compte.",
                ForeColor = Palette.TextMuted, AutoSize = true, Location = new Point(16, 178),
            });
            buttonsTop = 186;
        }
        else
        {
            Controls.Add(new Label
            {
                Text = "Rien ne sera conservé : saisie pour cette session uniquement.",
                ForeColor = Palette.TextMuted, AutoSize = true, Location = new Point(16, 126),
            });
            buttonsTop = 136;
        }

        var ok = new Button { Text = "Connecter", Location = new Point(188, buttonsTop), Width = 84, FlatStyle = FlatStyle.Flat, BackColor = Palette.InputBackground, ForeColor = Palette.Text };
        ok.Click += (_, _) => Submit();
        var cancel = new Button { Text = "Annuler", Location = new Point(280, buttonsTop), Width = 84, FlatStyle = FlatStyle.Flat, BackColor = Palette.InputBackground, ForeColor = Palette.Text, DialogResult = DialogResult.Cancel };

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(ok);
        Controls.Add(cancel);
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(_userBox.Text) || _passwordBox.TextLength == 0)
        {
            return; // champs requis ; on ne ferme pas
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
