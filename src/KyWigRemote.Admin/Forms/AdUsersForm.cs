using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Core.Remote;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Admin.Forms;

/// <summary>
/// Utilisateurs Active Directory synchronisés depuis les groupes : liste (profil, activité,
/// dernière synchro) et bouton de synchronisation manuelle. Réservé à l'administrateur global.
/// </summary>
internal sealed class AdUsersForm : Form
{
    private readonly ServerClient _server;
    private readonly ListView _list;
    private readonly ToolStripStatusLabel _statusLabel;

    public AdUsersForm(ServerClient server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));

        Text = "KyWigRemote — Utilisateurs Active Directory";
        Icon = BrandAssets.AdminIcon;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 420);
        Size = new Size(720, 480);
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
        var sync = new ToolStripButton("Synchroniser maintenant") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        sync.Click += async (_, _) => await SyncAsync();
        var refresh = new ToolStripButton("Actualiser") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        refresh.Click += async (_, _) => await LoadAsync();
        toolbar.Items.AddRange(new ToolStripItem[] { sync, new ToolStripSeparator(), refresh });

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            BorderStyle = BorderStyle.None,
            BackColor = Palette.PanelBackground,
            ForeColor = Palette.Text,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _list.Columns.Add("Identifiant", 180);
        _list.Columns.Add("Nom affiché", 200);
        _list.Columns.Add("Profil", 160);
        _list.Columns.Add("Actif", 60);
        _list.Columns.Add("Dernière synchro", 120);

        var status = new StatusStrip
        {
            BackColor = Palette.PanelBackground,
            ForeColor = Palette.TextMuted,
            SizingGrip = false,
        };
        _statusLabel = new ToolStripStatusLabel("Chargement…") { ForeColor = Palette.TextMuted };
        status.Items.Add(_statusLabel);

        Controls.Add(_list);
        Controls.Add(toolbar);
        Controls.Add(status);

        Load += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            IReadOnlyList<AdUserSummary> users = await _server.ListAdUsersAsync();
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (AdUserSummary u in users)
            {
                var item = new ListViewItem(u.SamAccountName);
                item.SubItems.Add(u.DisplayName ?? string.Empty);
                item.SubItems.Add(UserRoleChoices.Label(u.Role));
                item.SubItems.Add(u.Active ? "oui" : "non");
                item.SubItems.Add(u.LastSyncedAt?.ToLocalTime().ToString("g") ?? string.Empty);
                if (!u.Active)
                {
                    item.ForeColor = Palette.TextMuted;
                }
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            _statusLabel.Text = $"{users.Count} utilisateur(s) AD.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _statusLabel.Text = "Impossible de charger les utilisateurs depuis le serveur.";
        }
    }

    private async Task SyncAsync()
    {
        _statusLabel.Text = "Synchronisation en cours…";
        try
        {
            AdSyncResult? result = await _server.SyncAdAsync();
            if (result is null)
            {
                MessageBox.Show(this,
                    "Synchronisation impossible : annuaire injoignable ou compte de service invalide. "
                    + "Vérifiez la configuration AD.",
                    "KyWigRemote — Synchronisation AD", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _statusLabel.Text = "Échec de la synchronisation.";
                return;
            }
            await LoadAsync();
            _statusLabel.Text =
                $"Synchronisation : {result.Imported} import(s), {result.Updated} mise(s) à jour, {result.Deactivated} désactivé(s).";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _statusLabel.Text = "Synchronisation impossible (serveur injoignable).";
        }
    }
}
