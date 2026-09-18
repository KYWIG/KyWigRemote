using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Core.Remote;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Admin.Forms;

/// <summary>Boîte de saisie d'un nouveau compte local (administration).</summary>
internal sealed class NewAccountDialog : Form
{
    private readonly TextBox _userBox;
    private readonly TextBox _displayBox;
    private readonly TextBox _passwordBox;
    private readonly CheckBox _adminBox;
    private readonly Label _statusLabel;

    /// <summary>Demande construite après validation, ou null si annulé.</summary>
    public CreateLocalAccountRequest? Request { get; private set; }

    public NewAccountDialog()
    {
        Text = "Nouveau compte local";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(400, 280);
        BackColor = DarkPalette.Background;
        ForeColor = DarkPalette.Text;
        Font = new Font("Segoe UI", 9f);

        _userBox = MakeField("Identifiant :", 20, string.Empty);
        _displayBox = MakeField("Nom affiché (facultatif) :", 74, string.Empty);
        _passwordBox = MakeField("Mot de passe :", 128, string.Empty);
        _passwordBox.UseSystemPasswordChar = true;

        _adminBox = new CheckBox
        {
            Text = "Compte administrateur",
            ForeColor = DarkPalette.Text,
            AutoSize = true,
            Location = new Point(16, 182),
        };

        _statusLabel = new Label
        {
            Text = string.Empty,
            ForeColor = DarkPalette.Error,
            AutoSize = false,
            Location = new Point(16, 208),
            Size = new Size(368, 20),
        };

        var okButton = new Button
        {
            Text = "Créer",
            Location = new Point(208, 238),
            Width = 84,
            FlatStyle = FlatStyle.Flat,
            BackColor = DarkPalette.InputBackground,
            ForeColor = DarkPalette.Text,
        };
        okButton.FlatAppearance.BorderColor = DarkPalette.Border;
        okButton.Click += (_, _) => Submit();

        var cancelButton = new Button
        {
            Text = "Annuler",
            Location = new Point(300, 238),
            Width = 84,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = DarkPalette.InputBackground,
            ForeColor = DarkPalette.Text,
        };
        cancelButton.FlatAppearance.BorderColor = DarkPalette.Border;

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(_adminBox);
        Controls.Add(_statusLabel);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }

    private TextBox MakeField(string label, int top, string initialValue)
    {
        var caption = new Label
        {
            Text = label,
            ForeColor = DarkPalette.Text,
            AutoSize = true,
            Location = new Point(16, top),
        };
        var box = new TextBox
        {
            Text = initialValue,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = DarkPalette.InputBackground,
            ForeColor = DarkPalette.Text,
            Location = new Point(16, top + 22),
            Width = 368,
        };
        Controls.Add(caption);
        Controls.Add(box);
        return box;
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(_userBox.Text) || _passwordBox.TextLength == 0)
        {
            _statusLabel.Text = "Identifiant et mot de passe requis.";
            return;
        }

        Request = new CreateLocalAccountRequest(
            _userBox.Text.Trim(),
            _passwordBox.Text,
            string.IsNullOrWhiteSpace(_displayBox.Text) ? null : _displayBox.Text.Trim(),
            _adminBox.Checked);
        DialogResult = DialogResult.OK;
        Close();
    }
}
