using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Core.Remote;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Admin.Forms;

/// <summary>
/// Configuration Active Directory : rappel des groupes (issus de appsettings) et saisie du
/// compte de service (identifiant + mot de passe), stocké chiffré côté serveur, avec test de
/// connexion. Réservé à l'administrateur global.
/// </summary>
internal sealed class AdConfigForm : Form
{
    private readonly ServerClient _server;
    private readonly Label _groupsLabel;
    private readonly TextBox _userBox;
    private readonly TextBox _passwordBox;
    private readonly Label _statusLabel;

    public AdConfigForm(ServerClient server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));

        Text = "KyWigRemote — Configuration Active Directory";
        Icon = BrandAssets.AdminIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(520, 360);
        BackColor = Palette.Background;
        ForeColor = Palette.Text;
        Font = new Font("Segoe UI", 9f);

        var header = new Label
        {
            Text = "Groupes et compte de service",
            Location = new Point(16, 14),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10f),
            ForeColor = Palette.Text,
        };
        _groupsLabel = new Label
        {
            Location = new Point(16, 42),
            Size = new Size(488, 90),
            ForeColor = Palette.TextMuted,
            Text = "Chargement…",
        };

        _userBox = MakeField("Compte de service — identifiant :", 148);
        _passwordBox = MakeField("Compte de service — mot de passe :", 202);
        _passwordBox.UseSystemPasswordChar = true;

        _statusLabel = new Label
        {
            Location = new Point(16, 256),
            Size = new Size(488, 40),
            ForeColor = Palette.TextMuted,
        };

        var testButton = MakeButton("Tester la connexion", 16, 312, async () => await TestAsync());
        var saveButton = MakeButton("Enregistrer", 300, 312, async () => await SaveAsync());
        var closeButton = MakeButton("Fermer", 412, 312, () => Close());

        Controls.Add(header);
        Controls.Add(_groupsLabel);
        Controls.Add(_statusLabel);
        Controls.Add(testButton);
        Controls.Add(saveButton);
        Controls.Add(closeButton);

        Load += async (_, _) => await LoadConfigAsync();
    }

    private TextBox MakeField(string label, int top)
    {
        Controls.Add(new Label { Text = label, Location = new Point(16, top), AutoSize = true, ForeColor = Palette.Text });
        var box = new TextBox
        {
            Location = new Point(16, top + 20),
            Width = 488,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Palette.InputBackground,
            ForeColor = Palette.Text,
        };
        Controls.Add(box);
        return box;
    }

    private Button MakeButton(string text, int x, int y, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Width = text.Length > 12 ? 150 : 100,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = Palette.InputBackground,
            ForeColor = Palette.Text,
        };
        button.FlatAppearance.BorderColor = Palette.Border;
        button.Click += (_, _) => onClick();
        return button;
    }

    private async Task LoadConfigAsync()
    {
        try
        {
            AdConfigView? config = await _server.GetAdConfigAsync();
            if (config is null)
            {
                _groupsLabel.Text = "Configuration indisponible.";
                return;
            }
            _groupsLabel.Text =
                $"Active Directory : {(config.Enabled ? "activé" : "désactivé")}\r\n" +
                $"Domaine : {config.Domain}\r\n" +
                $"Groupe Utilisateurs : {config.UserGroup}\r\n" +
                $"Groupe Admin des connexions : {config.ConnectionAdminGroup}\r\n" +
                $"Groupe Admin global : {config.AdminGroup}";
            _userBox.Text = config.ServiceUsername ?? string.Empty;
            _statusLabel.ForeColor = Palette.TextMuted;
            _statusLabel.Text = config.ServiceConfigured
                ? "Un compte de service est déjà enregistré (mot de passe masqué)."
                : "Aucun compte de service enregistré.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _groupsLabel.Text = "Impossible de charger la configuration depuis le serveur.";
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_userBox.Text) || _passwordBox.TextLength == 0)
        {
            Status("Identifiant et mot de passe du compte de service requis.", Palette.Error);
            return;
        }
        try
        {
            await _server.SetAdServiceAccountAsync(_userBox.Text.Trim(), _passwordBox.Text);
            _passwordBox.Clear();
            Status("Compte de service enregistré.", Palette.Success);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Status("Enregistrement impossible (serveur injoignable).", Palette.Error);
        }
    }

    private async Task TestAsync()
    {
        Status("Test en cours…", Palette.TextMuted);
        try
        {
            AdTestResult result = await _server.TestAdAsync();
            Status(result.Message, result.Ok ? Palette.Success : Palette.Error);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Status("Test impossible (serveur injoignable).", Palette.Error);
        }
    }

    private void Status(string text, Color color)
    {
        _statusLabel.ForeColor = color;
        _statusLabel.Text = text;
    }
}
