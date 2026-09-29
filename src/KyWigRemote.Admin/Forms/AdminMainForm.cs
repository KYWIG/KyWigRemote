using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Core.Remote;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Admin.Forms;

/// <summary>
/// Fenêtre principale de la console d'administration. Première fonction : la gestion
/// des comptes locaux (liste + création). La gestion des connexions, des identifiants
/// imposés et de l'audit viendra enrichir cet écran.
/// </summary>
internal sealed class AdminMainForm : Form
{
    private readonly ServerClient _server;
    private readonly ListView _accountsList;
    private readonly ToolStripStatusLabel _statusLabel;

    private readonly bool _isGlobalAdmin;

    public AdminMainForm(ServerClient server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _isGlobalAdmin = server.Session?.IsGlobalAdmin ?? false;

        Text = "KyWigRemote — Administration";
        Icon = BrandAssets.AdminIcon;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(820, 520);
        BackColor = Palette.Background;
        ForeColor = Palette.Text;
        Font = new Font("Segoe UI", 9f);
        ToolStripManager.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());

        var toolbar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = Palette.PanelBackground,
            ForeColor = Palette.Text,
        };
        var refresh = new ToolStripButton("Actualiser") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        refresh.Click += async (_, _) => await LoadAccountsAsync();
        var create = new ToolStripButton("Nouveau compte") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        create.Click += async (_, _) => await CreateAccountAsync();
        // Confort de dev : crée en un clic un administrateur global de test avec un mot de passe
        // aléatoire. Visible uniquement sur un serveur local (jamais sur un serveur distant/prod).
        var testAdmin = new ToolStripButton("Admin de test") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        testAdmin.Click += async (_, _) => await CreateTestAdminAsync();
        bool isLocalServer = _server.BaseAddress.IsLoopback;
        var connections = new ToolStripButton("Connexions…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        connections.Click += (_, _) => OpenConnections();
        var enforced = new ToolStripButton("Identifiants imposés…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        enforced.Click += (_, _) => OpenEnforcedCredentials();
        var auditButton = new ToolStripButton("Journal d'audit…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        auditButton.Click += (_, _) => OpenAudit();
        var adConfig = new ToolStripButton("Active Directory…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        adConfig.Click += (_, _) => OpenAdConfig();
        var adUsers = new ToolStripButton("Utilisateurs AD…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        adUsers.Click += (_, _) => OpenAdUsers();

        // La gestion des comptes, de l'AD et l'audit sont réservées à l'administrateur global ;
        // l'administrateur des connexions ne voit que la gestion des connexions.
        var items = new List<ToolStripItem> { refresh };
        if (_isGlobalAdmin)
        {
            items.Add(new ToolStripSeparator());
            items.Add(create);
            if (isLocalServer) { items.Add(testAdmin); }
        }
        items.Add(new ToolStripSeparator());
        items.Add(connections);
        items.Add(enforced);
        if (_isGlobalAdmin)
        {
            items.Add(new ToolStripSeparator());
            items.Add(adConfig);
            items.Add(adUsers);
            items.Add(auditButton);
        }

        // Bascule de thème (aligné à droite) : change et redémarre pour appliquer proprement.
        var themeButton = new ToolStripButton(ThemeManager.Mode == AppTheme.Light ? "Thème sombre" : "Thème clair")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Alignment = ToolStripItemAlignment.Right,
        };
        themeButton.Click += (_, _) =>
        {
            ThemeManager.SetMode(ThemeManager.Mode == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
            Application.Restart();
        };
        items.Add(themeButton);
        toolbar.Items.AddRange(items.ToArray());

        var header = new Label
        {
            Text = _isGlobalAdmin ? "Comptes locaux" : "Administration des connexions",
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            ForeColor = Palette.Text,
            Font = new Font("Segoe UI Semibold", 9.5f),
        };

        _accountsList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.None,
            BackColor = Palette.PanelBackground,
            ForeColor = Palette.Text,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Visible = _isGlobalAdmin,
        };
        _accountsList.Columns.Add("Identifiant", 220);
        _accountsList.Columns.Add("Nom affiché", 240);
        _accountsList.Columns.Add("Profil", 160);
        _accountsList.Columns.Add("Désactivé", 100);

        // Pour l'administrateur des connexions : pas de liste de comptes, un rappel d'usage.
        var connectionAdminHint = new Label
        {
            Text = "Utilisez « Connexions… » pour gérer les dossiers et connexions, "
                 + "et « Identifiants imposés… » pour les comptes de service partagés.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Palette.TextMuted,
            Visible = !_isGlobalAdmin,
        };

        var status = new StatusStrip
        {
            BackColor = Palette.PanelBackground,
            ForeColor = Palette.TextMuted,
            SizingGrip = false,
        };
        _statusLabel = new ToolStripStatusLabel("Chargement…") { ForeColor = Palette.TextMuted };
        status.Items.Add(_statusLabel);

        Controls.Add(connectionAdminHint);
        Controls.Add(_accountsList);
        Controls.Add(header);
        Controls.Add(toolbar);
        Controls.Add(status);

        Load += async (_, _) =>
        {
            if (_isGlobalAdmin)
            {
                await LoadAccountsAsync();
            }
            else
            {
                string me = _server.Session?.Username ?? "?";
                _statusLabel.Text = $"Connecté : {me} (Administrateur des connexions) — {_server.BaseAddress}";
            }
        };
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            IReadOnlyList<LocalAccountSummary> accounts = await _server.ListLocalAccountsAsync();

            _accountsList.BeginUpdate();
            _accountsList.Items.Clear();
            foreach (LocalAccountSummary a in accounts)
            {
                var item = new ListViewItem(a.Username);
                item.SubItems.Add(a.DisplayName ?? string.Empty);
                item.SubItems.Add(UserRoleChoices.Label(a.Role));
                item.SubItems.Add(a.Disabled ? "oui" : string.Empty);
                _accountsList.Items.Add(item);
            }
            _accountsList.EndUpdate();

            string me = _server.Session?.Username ?? "?";
            _statusLabel.Text = $"{accounts.Count} compte(s) — connecté : {me} (Administrateur global) — {_server.BaseAddress}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(this,
                "Impossible de charger les comptes depuis le serveur.",
                "KyWigRemote — Administration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenConnections()
    {
        using var form = new ConnectionsAdminForm(_server);
        form.ShowDialog(this);
    }

    private void OpenEnforcedCredentials()
    {
        using var form = new EnforcedCredentialsAdminForm(_server);
        form.ShowDialog(this);
    }

    private void OpenAudit()
    {
        using var form = new AuditViewerForm(_server);
        form.ShowDialog(this);
    }

    private void OpenAdConfig()
    {
        using var form = new AdConfigForm(_server);
        form.ShowDialog(this);
    }

    private void OpenAdUsers()
    {
        using var form = new AdUsersForm(_server);
        form.ShowDialog(this);
    }

    /// <summary>
    /// Crée un administrateur global de test avec un mot de passe aléatoire, puis l'affiche une
    /// fois. Confort de développement : le bouton n'est proposé que sur un serveur local.
    /// </summary>
    private async Task CreateTestAdminAsync()
    {
        string username = DevTestAdmin.NewUsername();
        string password = DevTestAdmin.NewPassword();
        var request = new CreateLocalAccountRequest(
            username, password, "Admin de test", KyWigRemote.Core.Model.UserRole.GlobalAdmin);

        try
        {
            LocalAccountSummary? created = await _server.CreateLocalAccountAsync(request);
            if (created is null)
            {
                // Collision d'identifiant (très improbable, suffixe aléatoire) : on réessaiera.
                MessageBox.Show(this,
                    "L'identifiant de test généré existe déjà. Réessayez.",
                    "KyWigRemote — Administration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new TestAdminCreatedDialog(username, password))
            {
                dialog.ShowDialog(this);
            }
            await LoadAccountsAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(this,
                "Échec de la création de l'admin de test côté serveur.",
                "KyWigRemote — Administration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task CreateAccountAsync()
    {
        using var dialog = new NewAccountDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Request is null)
        {
            return;
        }

        try
        {
            LocalAccountSummary? created = await _server.CreateLocalAccountAsync(dialog.Request);
            if (created is null)
            {
                MessageBox.Show(this,
                    $"Le compte « {dialog.Request.Username} » existe déjà.",
                    "KyWigRemote — Administration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            await LoadAccountsAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(this,
                "Échec de la création du compte côté serveur.",
                "KyWigRemote — Administration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
