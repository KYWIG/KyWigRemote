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

    public AdminMainForm(ServerClient server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));

        Text = "KyWigRemote — Administration";
        Icon = BrandAssets.AppIcon;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(820, 520);
        BackColor = DarkPalette.Background;
        ForeColor = DarkPalette.Text;
        Font = new Font("Segoe UI", 9f);
        ToolStripManager.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());

        var toolbar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = DarkPalette.PanelBackground,
            ForeColor = DarkPalette.Text,
        };
        var refresh = new ToolStripButton("Actualiser") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        refresh.Click += async (_, _) => await LoadAccountsAsync();
        var create = new ToolStripButton("Nouveau compte") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        create.Click += async (_, _) => await CreateAccountAsync();
        var connections = new ToolStripButton("Connexions…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        connections.Click += (_, _) => OpenConnections();
        var enforced = new ToolStripButton("Identifiants imposés…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        enforced.Click += (_, _) => OpenEnforcedCredentials();
        var auditButton = new ToolStripButton("Journal d'audit…") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        auditButton.Click += (_, _) => OpenAudit();
        toolbar.Items.AddRange(new ToolStripItem[]
        {
            refresh, new ToolStripSeparator(), create, new ToolStripSeparator(), connections, enforced,
            new ToolStripSeparator(), auditButton,
        });

        var header = new Label
        {
            Text = "Comptes locaux",
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            ForeColor = DarkPalette.Text,
            Font = new Font("Segoe UI Semibold", 9.5f),
        };

        _accountsList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.None,
            BackColor = DarkPalette.PanelBackground,
            ForeColor = DarkPalette.Text,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _accountsList.Columns.Add("Identifiant", 220);
        _accountsList.Columns.Add("Nom affiché", 240);
        _accountsList.Columns.Add("Admin", 80);
        _accountsList.Columns.Add("Désactivé", 100);

        var status = new StatusStrip
        {
            BackColor = DarkPalette.PanelBackground,
            ForeColor = DarkPalette.TextMuted,
            SizingGrip = false,
        };
        _statusLabel = new ToolStripStatusLabel("Chargement…") { ForeColor = DarkPalette.TextMuted };
        status.Items.Add(_statusLabel);

        Controls.Add(_accountsList);
        Controls.Add(header);
        Controls.Add(toolbar);
        Controls.Add(status);

        Load += async (_, _) => await LoadAccountsAsync();
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
                item.SubItems.Add(a.IsAdmin ? "oui" : string.Empty);
                item.SubItems.Add(a.Disabled ? "oui" : string.Empty);
                _accountsList.Items.Add(item);
            }
            _accountsList.EndUpdate();

            string me = _server.Session?.Username ?? "?";
            _statusLabel.Text = $"{accounts.Count} compte(s) — connecté : {me} (admin) — {_server.BaseAddress}";
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
