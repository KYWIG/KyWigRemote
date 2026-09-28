using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using KyWigRemote.Shared.UI;
using KyWigRemote.Core.Model;
using WeifenLuo.WinFormsUI.Docking;

namespace KyWigRemote.Client.Forms;

/// <summary>
/// Panneau « Connexions » : arborescence partagée + champ de recherche filtrant (FR-01, FR-04).
/// Côté client, l'arbre est en lecture seule ; l'édition est réservée à la console d'administration.
/// </summary>
internal sealed class ConnectionsPanel : DockContent
{
    private readonly TextBox _search;
    private readonly TreeView _tree;
    private IReadOnlyList<ConnectionFolder> _roots = Array.Empty<ConnectionFolder>();

    /// <summary>Déclenché lorsqu'une connexion est activée (double-clic ou Entrée).</summary>
    public event Action<RemoteConnection>? ConnectionActivated;

    /// <summary>Déclenché quand la sélection change ; l'argument est un dossier, une connexion, ou null.</summary>
    public event Action<object?>? SelectionChanged;

    public ConnectionsPanel()
    {
        Text = "Connexions";
        DockAreas = DockAreas.DockLeft | DockAreas.DockRight | DockAreas.Float;
        BackColor = Palette.PanelBackground;

        _search = new TextBox
        {
            Dock = DockStyle.Top,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Palette.InputBackground,
            ForeColor = Palette.Text,
            Margin = new Padding(0),
        };
        _search.TextChanged += (_, _) => RebuildTree();

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Palette.PanelBackground,
            ForeColor = Palette.Text,
            LineColor = Palette.Border,
            HideSelection = false,
            FullRowSelect = true,
            ShowRootLines = true,
            Indent = 16,
            ItemHeight = 20,
            ImageList = BuildTreeImages(),
        };
        // Dossiers : icône ouverte quand déplié, fermée quand replié.
        _tree.AfterExpand += (_, e) => SetFolderImage(e.Node, expanded: true);
        _tree.AfterCollapse += (_, e) => SetFolderImage(e.Node, expanded: false);
        _tree.NodeMouseDoubleClick += (_, e) => Activate(e.Node);
        _tree.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                Activate(_tree.SelectedNode);
                e.Handled = true;
            }
        };
        _tree.AfterSelect += (_, e) => SelectionChanged?.Invoke(e.Node?.Tag);

        // L'arbre remplit l'espace, le champ de recherche coiffe le panneau.
        Controls.Add(_tree);
        Controls.Add(_search);
    }

    /// <summary>Renseigne l'arborescence à afficher et la (re)construit.</summary>
    public void LoadTree(IReadOnlyList<ConnectionFolder> roots)
    {
        _roots = roots;
        RebuildTree();
    }

    /// <summary>Place le focus dans le champ de recherche (raccourci Ctrl+F).</summary>
    public void FocusSearch() => _search.Focus();

    /// <summary>Connexion actuellement sélectionnée dans l'arbre, ou null si la sélection est un dossier.</summary>
    public RemoteConnection? SelectedConnection => _tree.SelectedNode?.Tag as RemoteConnection;

    private void Activate(TreeNode? node)
    {
        if (node?.Tag is RemoteConnection connection)
        {
            ConnectionActivated?.Invoke(connection);
        }
    }

    /// <summary>
    /// Reconstruit l'arbre à partir du modèle en appliquant le filtre de recherche.
    /// Les dossiers ne contenant aucune correspondance sont masqués (ui-spec §5).
    /// </summary>
    private void RebuildTree()
    {
        string query = _search.Text.Trim();

        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (ConnectionFolder root in _roots)
        {
            TreeNode? node = BuildFolderNode(root, query);
            if (node is not null)
            {
                _tree.Nodes.Add(node);
            }
        }

        // À vide, tout est déplié pour donner une vue d'ensemble ; en recherche,
        // on déplie aussi pour montrer les correspondances trouvées.
        _tree.ExpandAll();
        _tree.EndUpdate();
    }

    private static TreeNode? BuildFolderNode(ConnectionFolder folder, string query)
    {
        var node = new TreeNode(folder.Name)
        {
            Tag = folder,
            ImageKey = FolderClosed,
            SelectedImageKey = FolderClosed,
        };

        foreach (ConnectionFolder sub in folder.SubFolders)
        {
            TreeNode? subNode = BuildFolderNode(sub, query);
            if (subNode is not null)
            {
                node.Nodes.Add(subNode);
            }
        }

        foreach (RemoteConnection connection in folder.Connections)
        {
            if (MatchesQuery(connection, query))
            {
                string key = connection.Protocol == RemoteProtocol.Ssh ? "ssh" : "rdp";
                node.Nodes.Add(new TreeNode(connection.Name)
                {
                    Tag = connection,
                    ImageKey = key,
                    SelectedImageKey = key,
                });
            }
        }

        // Un dossier vide après filtrage n'est pas affiché, sauf en l'absence de recherche.
        bool keep = query.Length == 0 || node.Nodes.Count > 0;
        return keep ? node : null;
    }

    private static bool MatchesQuery(RemoteConnection connection, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        return connection.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || connection.Host.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private const string FolderClosed = "folder-closed";
    private const string FolderOpen = "folder-open";

    /// <summary>Bascule l'icône d'un nœud dossier entre « ouvert » et « fermé ».</summary>
    private static void SetFolderImage(TreeNode? node, bool expanded)
    {
        if (node?.Tag is ConnectionFolder)
        {
            string key = expanded ? FolderOpen : FolderClosed;
            node.ImageKey = key;
            node.SelectedImageKey = key;
        }
    }

    /// <summary>
    /// Construit la liste d'images de l'arbre (dossiers + protocoles) à partir des PNG embarqués,
    /// redimensionnés proprement en 16×16.
    /// </summary>
    private static ImageList BuildTreeImages()
    {
        var list = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        foreach (string name in new[] { FolderClosed, FolderOpen, "rdp", "ssh" })
        {
            using Stream? stream = typeof(ConnectionsPanel).Assembly
                .GetManifestResourceStream($"KyWigRemote.Client.{name}.png");
            if (stream is null)
            {
                continue;
            }
            using var source = new Bitmap(stream);
            var icon = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(icon))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, 0, 0, 16, 16);
            }
            list.Images.Add(name, icon);
        }
        return list;
    }
}
