using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Core.Model;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Admin.Forms;

/// <summary>Boîte de saisie d'un nouveau dossier.</summary>
internal sealed class NewFolderDialog : Form
{
    private readonly TextBox _nameBox;
    private readonly ComboBox _modeBox;
    private readonly Label _statusLabel;

    /// <summary>Nom saisi, après validation.</summary>
    public string? FolderName { get; private set; }

    /// <summary>Mode d'identifiants choisi.</summary>
    public CredentialMode Mode { get; private set; } = CredentialMode.Inherited;

    public NewFolderDialog(string parentLabel)
    {
        Text = "Nouveau dossier";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(400, 210);
        BackColor = DarkPalette.Background;
        ForeColor = DarkPalette.Text;
        Font = new Font("Segoe UI", 9f);

        var parent = new Label
        {
            Text = $"Emplacement : {parentLabel}",
            ForeColor = DarkPalette.TextMuted,
            AutoSize = true,
            Location = new Point(16, 16),
        };

        var nameCaption = new Label { Text = "Nom :", ForeColor = DarkPalette.Text, AutoSize = true, Location = new Point(16, 44) };
        _nameBox = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle, BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text,
            Location = new Point(16, 66), Width = 368,
        };

        var modeCaption = new Label { Text = "Mode d'identifiants :", ForeColor = DarkPalette.Text, AutoSize = true, Location = new Point(16, 100) };
        _modeBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text,
            FlatStyle = FlatStyle.Flat, Location = new Point(16, 122), Width = 200,
        };
        _modeBox.Items.AddRange(CredentialModeChoices.Labels);
        _modeBox.SelectedIndex = CredentialModeChoices.IndexOf(CredentialMode.Inherited);

        _statusLabel = new Label { Text = string.Empty, ForeColor = DarkPalette.Error, AutoSize = false, Location = new Point(16, 152), Size = new Size(368, 20) };

        var ok = MakeButton("Créer", 208);
        ok.Click += (_, _) => Submit();
        var cancel = MakeButton("Annuler", 300);
        cancel.DialogResult = DialogResult.Cancel;

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { parent, nameCaption, _nameBox, modeCaption, _modeBox, _statusLabel, ok, cancel });
    }

    private Button MakeButton(string text, int x) => new()
    {
        Text = text, Location = new Point(x, 176), Width = 84, FlatStyle = FlatStyle.Flat,
        BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text,
    };

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            _statusLabel.Text = "Le nom est requis.";
            return;
        }
        FolderName = _nameBox.Text.Trim();
        Mode = CredentialModeChoices.FromIndex(_modeBox.SelectedIndex);
        DialogResult = DialogResult.OK;
        Close();
    }
}
