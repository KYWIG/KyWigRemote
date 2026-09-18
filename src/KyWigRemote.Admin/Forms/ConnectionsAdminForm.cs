using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Core.Model;
using KyWigRemote.Core.Remote;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Admin.Forms;

/// <summary>
/// Gestion de l'arborescence partagée depuis l'administration : création de dossiers
/// et de connexions, suppression. Les modifications sont écrites sur le serveur et
/// apparaissent ensuite dans le client.
/// </summary>
internal sealed class ConnectionsAdminForm : Form
{
    private readonly ServerClient _server;
    private readonly TreeView _tree;
    private readonly ToolStripStatusLabel _statusLabel;

    public ConnectionsAdminForm(ServerClient server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));

        Text = "KyWigRemote — Connexions";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(560, 640);
        BackColor = DarkPalette.Background;
        ForeColor = DarkPalette.Text;
        Font = new Font("Segoe UI", 9f);
        ToolStripManager.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());

        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, BackColor = DarkPalette.PanelBackground, ForeColor = DarkPalette.Text };
        AddButton(toolbar, "Actualiser", async () => await LoadAsync());
        toolbar.Items.Add(new ToolStripSeparator());
        AddButton(toolbar, "Nouveau dossier", async () => await CreateFolderAsync());
        AddButton(toolbar, "Nouvelle connexion", async () => await CreateConnectionAsync());
        toolbar.Items.Add(new ToolStripSeparator());
        AddButton(toolbar, "Supprimer", async () => await DeleteSelectedAsync());

        _tree = new TreeView
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
            BackColor = DarkPalette.PanelBackground, ForeColor = DarkPalette.Text,
            HideSelection = false, FullRowSelect = true, Indent = 16, ItemHeight = 20,
        };

        var status = new StatusStrip { BackColor = DarkPalette.PanelBackground, ForeColor = DarkPalette.TextMuted, SizingGrip = false };
        _statusLabel = new ToolStripStatusLabel("Chargement…") { ForeColor = DarkPalette.TextMuted };
        status.Items.Add(_statusLabel);

        Controls.Add(_tree);
        Controls.Add(toolbar);
        Controls.Add(status);

        Load += async (_, _) => await LoadAsync();
    }

    private static void AddButton(ToolStrip bar, string text, Func<Task> action)
    {
        var button = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        button.Click += async (_, _) => await action();
        bar.Items.Add(button);
    }

    private async Task LoadAsync()
    {
        try
        {
            IReadOnlyList<ConnectionFolder> roots = await _server.GetTreeAsync();
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            foreach (ConnectionFolder root in roots)
            {
                _tree.Nodes.Add(BuildFolderNode(root));
            }
            _tree.ExpandAll();
            _tree.EndUpdate();
            _statusLabel.Text = $"{roots.Count} dossier(s) racine — {_server.BaseAddress}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Warn("Impossible de charger l'arborescence depuis le serveur.");
        }
    }

    private static TreeNode BuildFolderNode(ConnectionFolder folder)
    {
        var node = new TreeNode($"{folder.Name}") { Tag = folder };
        foreach (ConnectionFolder sub in folder.SubFolders)
        {
            node.Nodes.Add(BuildFolderNode(sub));
        }
        foreach (RemoteConnection connection in folder.Connections)
        {
            node.Nodes.Add(new TreeNode($"{connection.Name}  ({connection.Protocol.ToString().ToUpperInvariant()} {connection.Host})") { Tag = connection });
        }
        return node;
    }

    /// <summary>Dossier ciblé par une création : le dossier sélectionné, ou le parent d'une connexion sélectionnée.</summary>
    private ConnectionFolder? TargetFolder()
    {
        TreeNode? node = _tree.SelectedNode;
        return node?.Tag switch
        {
            ConnectionFolder folder => folder,
            RemoteConnection when node!.Parent?.Tag is ConnectionFolder parent => parent,
            _ => null,
        };
    }

    private async Task CreateFolderAsync()
    {
        ConnectionFolder? target = TargetFolder();
        using var dialog = new NewFolderDialog(target?.Name ?? "Racine");
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.FolderName is null)
        {
            return;
        }
        try
        {
            await _server.CreateFolderAsync(new CreateFolderRequest(dialog.FolderName, target?.Id, dialog.Mode));
            await LoadAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Warn("Échec de la création du dossier.");
        }
    }

    private async Task CreateConnectionAsync()
    {
        ConnectionFolder? target = TargetFolder();
        if (target is null)
        {
            Warn("Sélectionnez d'abord un dossier pour y créer la connexion.");
            return;
        }
        IReadOnlyList<EnforcedCredentialSummary> enforced;
        try
        {
            enforced = await _server.ListEnforcedCredentialsAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Warn("Impossible de charger les identifiants imposés.");
            return;
        }

        using var dialog = new NewConnectionDialog(target.Name, enforced);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ConnectionName is null)
        {
            return;
        }
        try
        {
            await _server.CreateConnectionAsync(new CreateConnectionRequest(
                target.Id, dialog.ConnectionName, dialog.Protocol, dialog.Host, dialog.Port,
                dialog.Domain, dialog.Description, dialog.Mode, dialog.EnforcedCredentialId));
            await LoadAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Warn("Échec de la création de la connexion.");
        }
    }

    private async Task DeleteSelectedAsync()
    {
        TreeNode? node = _tree.SelectedNode;
        if (node?.Tag is null)
        {
            Warn("Sélectionnez un dossier ou une connexion à supprimer.");
            return;
        }

        string label = node.Text;
        bool isFolder = node.Tag is ConnectionFolder;
        string question = isFolder
            ? $"Supprimer le dossier « {label} » et tout son contenu ?"
            : $"Supprimer la connexion « {label} » ?";
        if (MessageBox.Show(this, question, "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            if (node.Tag is ConnectionFolder folder)
            {
                await _server.DeleteFolderAsync(folder.Id);
            }
            else if (node.Tag is RemoteConnection connection)
            {
                await _server.DeleteConnectionAsync(connection.Id);
            }
            await LoadAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Warn("Échec de la suppression.");
        }
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "KyWigRemote — Connexions", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
