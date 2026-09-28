using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Core.Model;
using KyWigRemote.Core.Remote;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Admin.Forms;

/// <summary>Boîte de saisie d'une nouvelle connexion, avec choix éventuel d'un identifiant imposé.</summary>
internal sealed class NewConnectionDialog : Form
{
    private readonly TextBox _nameBox;
    private readonly ComboBox _protocolBox;
    private readonly TextBox _hostBox;
    private readonly TextBox _portBox;
    private readonly TextBox _domainBox;
    private readonly TextBox _descriptionBox;
    private readonly ComboBox _modeBox;
    private readonly ComboBox _enforcedBox;
    private readonly Label _statusLabel;
    private readonly IReadOnlyList<EnforcedCredentialSummary> _enforced;

    public string? ConnectionName { get; private set; }
    public RemoteProtocol Protocol { get; private set; } = RemoteProtocol.Rdp;
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; } = 3389;
    public string? Domain { get; private set; }
    public string? Description { get; private set; }
    public CredentialMode Mode { get; private set; } = CredentialMode.Inherited;
    public int? EnforcedCredentialId { get; private set; }

    public NewConnectionDialog(
        string folderLabel,
        IReadOnlyList<EnforcedCredentialSummary> enforcedCredentials,
        RemoteConnection? existing = null)
    {
        _enforced = enforcedCredentials;

        Text = existing is null ? $"Nouvelle connexion — {folderLabel}" : $"Modifier la connexion — {folderLabel}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(420, 452);
        BackColor = Palette.Background;
        ForeColor = Palette.Text;
        Font = new Font("Segoe UI", 9f);

        _nameBox = Field("Nom :", 16);
        _hostBox = Field("Hôte :", 64);

        Controls.Add(Caption("Protocole :", 16, 112));
        _protocolBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Palette.InputBackground, ForeColor = Palette.Text,
            FlatStyle = FlatStyle.Flat, Location = new Point(16, 134), Width = 180,
        };
        _protocolBox.Items.AddRange(ProtocolChoices.Labels);
        _protocolBox.SelectedIndex = 0;
        _protocolBox.SelectedIndexChanged += (_, _) => ApplyDefaultPort();
        Controls.Add(_protocolBox);

        Controls.Add(Caption("Port :", 220, 112));
        _portBox = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle, BackColor = Palette.InputBackground, ForeColor = Palette.Text,
            Location = new Point(220, 134), Width = 184, Text = "3389",
        };
        Controls.Add(_portBox);

        _domainBox = Field("Domaine (facultatif) :", 168);
        _descriptionBox = Field("Description (facultative) :", 216);

        Controls.Add(Caption("Mode d'identifiants :", 16, 264));
        _modeBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Palette.InputBackground, ForeColor = Palette.Text,
            FlatStyle = FlatStyle.Flat, Location = new Point(16, 286), Width = 388,
        };
        _modeBox.Items.AddRange(CredentialModeChoices.Labels);
        _modeBox.SelectedIndex = CredentialModeChoices.IndexOf(CredentialMode.Inherited);
        Controls.Add(_modeBox);

        Controls.Add(Caption("Identifiant imposé (si mode « Imposé ») :", 16, 320));
        _enforcedBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Palette.InputBackground, ForeColor = Palette.Text,
            FlatStyle = FlatStyle.Flat, Location = new Point(16, 342), Width = 388,
        };
        _enforcedBox.Items.Add("(aucun)");
        foreach (EnforcedCredentialSummary c in _enforced)
        {
            _enforcedBox.Items.Add($"{c.Label} ({c.Username})");
        }
        _enforcedBox.SelectedIndex = 0;
        Controls.Add(_enforcedBox);

        _statusLabel = new Label { Text = string.Empty, ForeColor = Palette.Error, AutoSize = false, Location = new Point(16, 384), Size = new Size(388, 18) };

        var ok = new Button { Text = existing is null ? "Créer" : "Enregistrer", Location = new Point(228, 412), Width = 84, FlatStyle = FlatStyle.Flat, BackColor = Palette.InputBackground, ForeColor = Palette.Text };
        ok.Click += (_, _) => Submit();
        var cancel = new Button { Text = "Annuler", Location = new Point(320, 412), Width = 84, FlatStyle = FlatStyle.Flat, BackColor = Palette.InputBackground, ForeColor = Palette.Text, DialogResult = DialogResult.Cancel };

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { _statusLabel, ok, cancel });

        if (existing is not null)
        {
            // Pré-remplissage en mode édition. Le protocole est fixé avant le port pour que
            // le changement automatique de port par défaut n'écrase pas la valeur existante.
            _nameBox.Text = existing.Name;
            _protocolBox.SelectedIndex = ProtocolChoices.IndexOf(existing.Protocol);
            _hostBox.Text = existing.Host;
            _portBox.Text = existing.Port.ToString();
            _domainBox.Text = existing.Domain ?? string.Empty;
            _descriptionBox.Text = existing.Description ?? string.Empty;
            _modeBox.SelectedIndex = CredentialModeChoices.IndexOf(existing.CredentialMode);
            if (existing.EnforcedCredentialId is int enforcedId)
            {
                for (int i = 0; i < _enforced.Count; i++)
                {
                    if (_enforced[i].Id == enforcedId)
                    {
                        _enforcedBox.SelectedIndex = i + 1; // +1 : l'index 0 est « (aucun) »
                        break;
                    }
                }
            }
        }
    }

    private static Label Caption(string text, int x, int y) =>
        new() { Text = text, ForeColor = Palette.Text, AutoSize = true, Location = new Point(x, y) };

    private TextBox Field(string label, int top)
    {
        Controls.Add(Caption(label, 16, top));
        var box = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle, BackColor = Palette.InputBackground, ForeColor = Palette.Text,
            Location = new Point(16, top + 22), Width = 388,
        };
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

        Mode = CredentialModeChoices.FromIndex(_modeBox.SelectedIndex);
        // L'index 0 du combo est « (aucun) » ; au-delà, il pointe dans la liste des identifiants.
        EnforcedCredentialId = _enforcedBox.SelectedIndex > 0 ? _enforced[_enforcedBox.SelectedIndex - 1].Id : null;

        if (Mode == CredentialMode.Enforced && EnforcedCredentialId is null)
        {
            _statusLabel.Text = "Le mode « Imposé » exige de choisir un identifiant imposé.";
            return;
        }

        ConnectionName = _nameBox.Text.Trim();
        Protocol = ProtocolChoices.FromIndex(_protocolBox.SelectedIndex);
        Host = _hostBox.Text.Trim();
        Port = port;
        Domain = string.IsNullOrWhiteSpace(_domainBox.Text) ? null : _domainBox.Text.Trim();
        Description = string.IsNullOrWhiteSpace(_descriptionBox.Text) ? null : _descriptionBox.Text.Trim();
        DialogResult = DialogResult.OK;
        Close();
    }
}
