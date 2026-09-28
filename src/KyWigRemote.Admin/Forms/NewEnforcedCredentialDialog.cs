using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Core.Remote;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Admin.Forms;

/// <summary>Boîte de saisie d'un identifiant imposé (le secret part chiffré côté serveur).</summary>
internal sealed class NewEnforcedCredentialDialog : Form
{
    private readonly TextBox _labelBox;
    private readonly TextBox _userBox;
    private readonly TextBox _domainBox;
    private readonly TextBox _secretBox;
    private readonly TextBox _groupsBox;
    private readonly Label _statusLabel;

    /// <summary>Demande construite après validation, ou null si annulé.</summary>
    public CreateEnforcedCredentialRequest? Request { get; private set; }

    public NewEnforcedCredentialDialog()
    {
        Text = "Nouvel identifiant imposé";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(420, 340);
        BackColor = Palette.Background;
        ForeColor = Palette.Text;
        Font = new Font("Segoe UI", 9f);

        _labelBox = Field("Libellé :", 20);
        _userBox = Field("Utilisateur :", 74);
        _domainBox = Field("Domaine (facultatif) :", 128);
        _secretBox = Field("Mot de passe :", 182);
        _secretBox.UseSystemPasswordChar = true;
        _groupsBox = Field("Groupes AD autorisés (séparés par ;) :", 236);

        _statusLabel = new Label { Text = string.Empty, ForeColor = Palette.Error, AutoSize = false, Location = new Point(16, 296), Size = new Size(388, 18) };

        var ok = new Button { Text = "Créer", Location = new Point(228, 300), Width = 84, FlatStyle = FlatStyle.Flat, BackColor = Palette.InputBackground, ForeColor = Palette.Text };
        ok.Click += (_, _) => Submit();
        var cancel = new Button { Text = "Annuler", Location = new Point(320, 300), Width = 84, FlatStyle = FlatStyle.Flat, BackColor = Palette.InputBackground, ForeColor = Palette.Text, DialogResult = DialogResult.Cancel };

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { _statusLabel, ok, cancel });
    }

    private TextBox Field(string label, int top)
    {
        Controls.Add(new Label { Text = label, ForeColor = Palette.Text, AutoSize = true, Location = new Point(16, top) });
        var box = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle, BackColor = Palette.InputBackground, ForeColor = Palette.Text,
            Location = new Point(16, top + 22), Width = 388,
        };
        Controls.Add(box);
        return box;
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(_labelBox.Text)
            || string.IsNullOrWhiteSpace(_userBox.Text)
            || _secretBox.TextLength == 0)
        {
            _statusLabel.Text = "Libellé, utilisateur et mot de passe sont requis.";
            return;
        }

        Request = new CreateEnforcedCredentialRequest(
            _labelBox.Text.Trim(),
            _userBox.Text.Trim(),
            string.IsNullOrWhiteSpace(_domainBox.Text) ? null : _domainBox.Text.Trim(),
            _secretBox.Text,
            string.IsNullOrWhiteSpace(_groupsBox.Text) ? null : _groupsBox.Text.Trim());
        DialogResult = DialogResult.OK;
        Close();
    }
}
