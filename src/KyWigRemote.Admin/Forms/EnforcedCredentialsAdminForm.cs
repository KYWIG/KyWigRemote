using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Core.Remote;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Admin.Forms;

/// <summary>
/// Gestion des identifiants imposés (comptes partagés). On les crée ici ; leur secret
/// est chiffré côté serveur et n'est jamais réaffiché — pas même à l'administrateur
/// (ui-spec §7 : s'il doit le connaître, c'est lui qui l'a saisi).
/// </summary>
internal sealed class EnforcedCredentialsAdminForm : Form
{
    private readonly ServerClient _server;
    private readonly ListView _list;
    private readonly ToolStripStatusLabel _statusLabel;

    public EnforcedCredentialsAdminForm(ServerClient server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));

        Text = "KyWigRemote — Identifiants imposés";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(640, 480);
        BackColor = Palette.Background;
        ForeColor = Palette.Text;
        Font = new Font("Segoe UI", 9f);
        ToolStripManager.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());

        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, BackColor = Palette.PanelBackground, ForeColor = Palette.Text };
        var refresh = new ToolStripButton("Actualiser") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        refresh.Click += async (_, _) => await LoadAsync();
        var create = new ToolStripButton("Nouvel identifiant") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        create.Click += async (_, _) => await CreateAsync();
        toolbar.Items.AddRange(new ToolStripItem[] { refresh, new ToolStripSeparator(), create });

        _list = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, BorderStyle = BorderStyle.None,
            BackColor = Palette.PanelBackground, ForeColor = Palette.Text, HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _list.Columns.Add("Libellé", 200);
        _list.Columns.Add("Utilisateur", 160);
        _list.Columns.Add("Domaine", 120);
        _list.Columns.Add("Groupes autorisés", 140);

        var status = new StatusStrip { BackColor = Palette.PanelBackground, ForeColor = Palette.TextMuted, SizingGrip = false };
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
            IReadOnlyList<EnforcedCredentialSummary> creds = await _server.ListEnforcedCredentialsAsync();
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (EnforcedCredentialSummary c in creds)
            {
                var item = new ListViewItem(c.Label);
                item.SubItems.Add(c.Username);
                item.SubItems.Add(c.Domain ?? string.Empty);
                item.SubItems.Add(c.AllowedGroups ?? string.Empty);
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            _statusLabel.Text = $"{creds.Count} identifiant(s) imposé(s) — le secret n'est jamais réaffiché";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Warn("Impossible de charger les identifiants imposés.");
        }
    }

    private async Task CreateAsync()
    {
        using var dialog = new NewEnforcedCredentialDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Request is null)
        {
            return;
        }
        try
        {
            await _server.CreateEnforcedCredentialAsync(dialog.Request);
            await LoadAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Warn("Échec de la création de l'identifiant imposé.");
        }
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "KyWigRemote — Identifiants imposés", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
