using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Core.Remote;

namespace KyWigRemote.Shared.UI;

/// <summary>
/// Boîte de connexion affichée au lancement du client comme de l'administration :
/// choix du serveur KyWigRemote (« serveur d'authentification » où se trouve la base)
/// et authentification. La fenêtre n'est validée qu'après une connexion acceptée.
/// </summary>
public sealed class ServerConnectDialog : Form
{
    private readonly TextBox _urlBox;
    private readonly TextBox _userBox;
    private readonly TextBox _passwordBox;
    private readonly Button _connectButton;
    private readonly Label _statusLabel;

    /// <summary>Client connecté et authentifié, disponible après un <see cref="DialogResult.OK"/>.</summary>
    public ServerClient? ConnectedClient { get; private set; }

    /// <param name="caption">Titre de la fenêtre (permet de distinguer client et administration).</param>
    /// <param name="defaultUrl">Adresse pré-remplie, si connue.</param>
    public ServerConnectDialog(string caption, string? defaultUrl)
    {
        Text = caption;
        Icon = BrandAssets.LoginIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(420, 250);
        BackColor = DarkPalette.Background;
        ForeColor = DarkPalette.Text;
        Font = new Font("Segoe UI", 9f);

        _urlBox = MakeField("Adresse du serveur :", 20,
            string.IsNullOrWhiteSpace(defaultUrl) ? "http://localhost:5080" : defaultUrl);
        _userBox = MakeField("Utilisateur :", 74, string.Empty);
        _passwordBox = MakeField("Mot de passe :", 128, string.Empty);
        _passwordBox.UseSystemPasswordChar = true;

        _statusLabel = new Label
        {
            Text = "Renseignez le serveur et vos identifiants, puis Connecter.",
            ForeColor = DarkPalette.TextMuted,
            AutoSize = false,
            Location = new Point(16, 178),
            Size = new Size(388, 24),
        };

        _connectButton = new Button
        {
            Text = "Connecter",
            Location = new Point(228, 208),
            Width = 84,
            FlatStyle = FlatStyle.Flat,
            BackColor = DarkPalette.InputBackground,
            ForeColor = DarkPalette.Text,
        };
        _connectButton.FlatAppearance.BorderColor = DarkPalette.Border;
        _connectButton.Click += async (_, _) => await TryConnectAsync();

        var cancelButton = new Button
        {
            Text = "Annuler",
            Location = new Point(320, 208),
            Width = 84,
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = DarkPalette.InputBackground,
            ForeColor = DarkPalette.Text,
        };
        cancelButton.FlatAppearance.BorderColor = DarkPalette.Border;

        var windowsButton = new Button
        {
            Text = "Connexion Windows (AD)",
            Location = new Point(16, 208),
            Width = 170,
            FlatStyle = FlatStyle.Flat,
            BackColor = DarkPalette.InputBackground,
            ForeColor = DarkPalette.Text,
        };
        windowsButton.FlatAppearance.BorderColor = DarkPalette.Border;
        windowsButton.Click += async (_, _) => await TryWindowsConnectAsync();

        AcceptButton = _connectButton;
        CancelButton = cancelButton;

        Controls.Add(_statusLabel);
        Controls.Add(windowsButton);
        Controls.Add(_connectButton);
        Controls.Add(cancelButton);
    }

    /// <summary>Connexion par le compte Windows/AD (Negotiate) : ni utilisateur ni mot de passe à saisir.</summary>
    private async Task TryWindowsConnectAsync()
    {
        string url = _urlBox.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            SetStatus("Adresse invalide. Attendu : http(s)://serveur:port", DarkPalette.Error);
            return;
        }

        SetBusy(true, "Connexion Windows au serveur…");
        var client = new ServerClient(url);
        try
        {
            if (!await client.CheckHealthAsync())
            {
                client.Dispose();
                SetBusy(false, string.Empty);
                SetStatus("Serveur injoignable. Vérifiez l'adresse, qu'il est démarré, et le VPN.", DarkPalette.Error);
                return;
            }

            LoginResult? session = await client.WindowsLoginAsync();
            if (session is null)
            {
                client.Dispose();
                SetBusy(false, string.Empty);
                SetStatus("Accès refusé : votre compte AD n'est pas dans le groupe autorisé.", DarkPalette.Error);
                return;
            }

            ConnectedClient = client;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            client.Dispose();
            SetBusy(false, string.Empty);
            SetStatus("Échec de la connexion Windows. Le serveur accepte-t-il l'authentification AD ?", DarkPalette.Error);
        }
    }

    /// <summary>Crée un couple libellé + champ à l'ordonnée indiquée et retourne le champ.</summary>
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
            Width = 388,
        };
        Controls.Add(caption);
        Controls.Add(box);
        return box;
    }

    private async Task TryConnectAsync()
    {
        string url = _urlBox.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            SetStatus("Adresse invalide. Attendu : http(s)://serveur:port", DarkPalette.Error);
            return;
        }
        if (string.IsNullOrWhiteSpace(_userBox.Text) || _passwordBox.TextLength == 0)
        {
            SetStatus("Utilisateur et mot de passe requis.", DarkPalette.Error);
            return;
        }

        SetBusy(true, "Connexion au serveur…");
        var client = new ServerClient(url);

        try
        {
            if (!await client.CheckHealthAsync())
            {
                client.Dispose();
                SetBusy(false, string.Empty);
                SetStatus("Serveur injoignable. Vérifiez l'adresse, qu'il est démarré, et le VPN.", DarkPalette.Error);
                return;
            }

            LoginResult? session = await client.LoginAsync(_userBox.Text.Trim(), _passwordBox.Text);
            if (session is null)
            {
                client.Dispose();
                SetBusy(false, string.Empty);
                SetStatus("Identifiant ou mot de passe incorrect.", DarkPalette.Error);
                return;
            }

            ConnectedClient = client;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            client.Dispose();
            SetBusy(false, string.Empty);
            SetStatus("Échec de connexion au serveur. Vérifiez l'adresse et le réseau.", DarkPalette.Error);
        }
    }

    private void SetBusy(bool busy, string message)
    {
        _connectButton.Enabled = !busy;
        _urlBox.Enabled = !busy;
        _userBox.Enabled = !busy;
        _passwordBox.Enabled = !busy;
        if (!string.IsNullOrEmpty(message))
        {
            SetStatus(message, DarkPalette.TextMuted);
        }
    }

    private void SetStatus(string message, Color color)
    {
        _statusLabel.ForeColor = color;
        _statusLabel.Text = message;
    }
}
