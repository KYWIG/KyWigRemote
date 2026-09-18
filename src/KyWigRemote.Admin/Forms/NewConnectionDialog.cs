using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Core.Model;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Admin.Forms;

/// <summary>Boîte de saisie d'une nouvelle connexion.</summary>
internal sealed class NewConnectionDialog : Form
{
    private readonly TextBox _nameBox;
    private readonly ComboBox _protocolBox;
    private readonly TextBox _hostBox;
    private readonly TextBox _portBox;
    private readonly TextBox _domainBox;
    private readonly TextBox _descriptionBox;
    private readonly ComboBox _modeBox;
    private readonly Label _statusLabel;

    public string? ConnectionName { get; private set; }
    public RemoteProtocol Protocol { get; private set; } = RemoteProtocol.Rdp;
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; } = 3389;
    public string? Domain { get; private set; }
    public string? Description { get; private set; }
    public CredentialMode Mode { get; private set; } = CredentialMode.Inherited;

    public NewConnectionDialog(string folderLabel)
    {
        Text = "Nouvelle connexion";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(420, 360);
        BackColor = DarkPalette.Background;
        ForeColor = DarkPalette.Text;
        Font = new Font("Segoe UI", 9f);

        var folder = new Label { Text = $"Dossier : {folderLabel}", ForeColor = DarkPalette.TextMuted, AutoSize = true, Location = new Point(16, 14) };

        _nameBox = Field("Nom :", 40);
        _protocolBox = Combo("Protocole :", 94, ProtocolChoices.Labels);
        _protocolBox.SelectedIndex = 0;
        _protocolBox.SelectedIndexChanged += (_, _) => ApplyDefaultPort();

        _hostBox = Field("Hôte :", 148);
        _portBox = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle, BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text,
            Location = new Point(300, 170), Width = 104, Text = "3389",
        };
        Controls.Add(new Label { Text = "Port :", ForeColor = DarkPalette.Text, AutoSize = true, Location = new Point(300, 148) });

        _domainBox = Field("Domaine (facultatif) :", 202);
        _descriptionBox = Field("Description (facultative) :", 256);

        _modeBox = Combo("Mode d'identifiants :", 300, CredentialModeChoices.Labels);
        _modeBox.SelectedIndex = CredentialModeChoices.IndexOf(CredentialMode.Inherited);
        _modeBox.Width = 200;

        _statusLabel = new Label { Text = string.Empty, ForeColor = DarkPalette.Error, AutoSize = false, Location = new Point(16, 326), Size = new Size(388, 18) };

        var ok = new Button { Text = "Créer", Location = new Point(228, 322), Width = 84, FlatStyle = FlatStyle.Flat, BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text };
        ok.Click += (_, _) => Submit();
        var cancel = new Button { Text = "Annuler", Location = new Point(320, 322), Width = 84, FlatStyle = FlatStyle.Flat, BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text, DialogResult = DialogResult.Cancel };

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { folder, _statusLabel, ok, cancel });
    }

    private TextBox Field(string label, int top)
    {
        Controls.Add(new Label { Text = label, ForeColor = DarkPalette.Text, AutoSize = true, Location = new Point(16, top) });
        var box = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle, BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text,
            Location = new Point(16, top + 22), Width = 388,
        };
        Controls.Add(box);
        return box;
    }

    private ComboBox Combo(string label, int top, object[] items)
    {
        Controls.Add(new Label { Text = label, ForeColor = DarkPalette.Text, AutoSize = true, Location = new Point(16, top) });
        var box = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text,
            FlatStyle = FlatStyle.Flat, Location = new Point(16, top + 22), Width = 264,
        };
        box.Items.AddRange(items);
        Controls.Add(box);
        return box;
    }

    private void ApplyDefaultPort()
    {
        RemoteProtocol protocol = ProtocolChoices.FromIndex(_protocolBox.SelectedIndex);
        _portBox.Text = RemoteProtocolDefaults.DefaultPort(protocol).ToString();
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text) || string.IsNullOrWhiteSpace(_hostBox.Text))
        {
            _statusLabel.Text = "Le nom et l'hôte sont requis.";
            return;
        }
        if (!int.TryParse(_portBox.Text.Trim(), out int port) || port is < 1 or > 65535)
        {
            _statusLabel.Text = "Port invalide (1–65535).";
            return;
        }

        ConnectionName = _nameBox.Text.Trim();
        Protocol = ProtocolChoices.FromIndex(_protocolBox.SelectedIndex);
        Host = _hostBox.Text.Trim();
        Port = port;
        Domain = string.IsNullOrWhiteSpace(_domainBox.Text) ? null : _domainBox.Text.Trim();
        Description = string.IsNullOrWhiteSpace(_descriptionBox.Text) ? null : _descriptionBox.Text.Trim();
        Mode = CredentialModeChoices.FromIndex(_modeBox.SelectedIndex);
        DialogResult = DialogResult.OK;
        Close();
    }
}
