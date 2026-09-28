using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Core.Remote;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Client.Forms;

/// <summary>
/// Écran « Mes identifiants » : l'utilisateur consulte et supprime ses propres identifiants
/// personnels (FR-16). Aucun secret n'est affiché — uniquement l'utilisateur et la portée.
/// </summary>
internal sealed class MyCredentialsDialog : Form
{
    private readonly ServerClient _server;
    private readonly ListView _list;
    private readonly Button _deleteButton;
    private readonly ToolStripStatusLabel _statusLabel;

    public MyCredentialsDialog(ServerClient server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));

        Text = "KyWigRemote — Mes identifiants";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(560, 420);
        BackColor = Palette.Background;
        ForeColor = Palette.Text;
        Font = new Font("Segoe UI", 9f);
        ToolStripManager.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());

        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, BackColor = Palette.PanelBackground, ForeColor = Palette.Text, Dock = DockStyle.Top };
        var refresh = new ToolStripButton("Actualiser") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        refresh.Click += async (_, _) => await LoadAsync();
        toolbar.Items.Add(refresh);

        _list = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false,
            BorderStyle = BorderStyle.None, BackColor = Palette.PanelBackground, ForeColor = Palette.Text,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _list.Columns.Add("Portée", 260);
        _list.Columns.Add("Utilisateur", 160);
        _list.Columns.Add("Domaine", 110);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Palette.Background };
        _deleteButton = new Button
        {
            Text = "Supprimer", Location = new Point(8, 8), Width = 100, FlatStyle = FlatStyle.Flat,
            BackColor = Palette.InputBackground, ForeColor = Palette.Text, Enabled = false,
        };
        _deleteButton.FlatAppearance.BorderColor = Palette.Border;
        _deleteButton.Click += async (_, _) => await DeleteSelectedAsync();
        bottom.Controls.Add(_deleteButton);

        var status = new StatusStrip { BackColor = Palette.PanelBackground, ForeColor = Palette.TextMuted, SizingGrip = false };
        _statusLabel = new ToolStripStatusLabel("Chargement…") { ForeColor = Palette.TextMuted };
        status.Items.Add(_statusLabel);

        _list.SelectedIndexChanged += (_, _) => _deleteButton.Enabled = _list.SelectedItems.Count > 0;

        Controls.Add(_list);
        Controls.Add(bottom);
        Controls.Add(toolbar);
        Controls.Add(status);

        Load += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            IReadOnlyList<PersonalCredentialSummary> creds = await _server.ListMyPersonalCredentialsAsync();
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (PersonalCredentialSummary c in creds)
            {
                string scope = c.ConnectionId is null
                    ? "Toutes mes connexions (global)"
                    : c.ConnectionName ?? $"Connexion #{c.ConnectionId}";
                var item = new ListViewItem(scope) { Tag = c.Id };
                item.SubItems.Add(c.Username);
                item.SubItems.Add(c.Domain ?? string.Empty);
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            _deleteButton.Enabled = _list.SelectedItems.Count > 0;
            _statusLabel.Text = $"{creds.Count} identifiant(s) personnel(s) — aucun secret n'est affiché";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Warn("Impossible de charger vos identifiants depuis le serveur.");
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not int id)
        {
            return;
        }
        if (MessageBox.Show(this, "Supprimer cet identifiant enregistré ?", "Confirmation",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }
        try
        {
            await _server.DeletePersonalCredentialAsync(id);
            await LoadAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Warn("Échec de la suppression.");
        }
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "KyWigRemote — Mes identifiants", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
