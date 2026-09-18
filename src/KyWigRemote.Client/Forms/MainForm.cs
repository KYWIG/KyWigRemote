using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Shared.UI;
using KyWigRemote.Core.Model;
using KyWigRemote.Core.Remote;
using KyWigRemote.Core.Security;
using WeifenLuo.WinFormsUI.Docking;

namespace KyWigRemote.Client.Forms;

/// <summary>
/// Fenêtre principale de la console de connexion (ui-spec §1) : barre de menu,
/// barre d'outils, panneaux dockables (Connexions, Propriétés), zone centrale
/// d'onglets de session, et barre d'état.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly DockPanel _dockPanel;
    private readonly ConnectionsPanel _connections;
    private readonly PropertiesPanel _properties;

    // Dialogue avec le serveur KyWigRemote (source des données partagées).
    private readonly ServerClient _server;

    // Dernière arborescence chargée, pour résoudre le mode d'identifiants d'une connexion.
    private IReadOnlyList<ConnectionFolder> _roots = Array.Empty<ConnectionFolder>();

    private readonly ToolStripStatusLabel _sessionCountLabel;
    private readonly ToolStripButton _connectButton;
    private readonly ToolStripButton _disconnectButton;

    // Mémorisation de l'état fenêtré pour le retour depuis le plein écran (F11).
    private FormWindowState _previousWindowState;
    private FormBorderStyle _previousBorderStyle;
    private bool _isFullScreen;

    public MainForm(ServerClient server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));

        Text = "KyWigRemote";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(900, 600);
        BackColor = DarkPalette.Background;
        ForeColor = DarkPalette.Text;
        Font = new Font("Segoe UI", 9f);

        // Rendu sombre des barres (menu, outils, état).
        ToolStripManager.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());

        _dockPanel = new DockPanel
        {
            Dock = DockStyle.Fill,
            Theme = new VS2015DarkTheme(),
            DocumentStyle = DocumentStyle.DockingWindow,
        };

        _properties = new PropertiesPanel();

        _connections = new ConnectionsPanel();
        _connections.ConnectionActivated += connection => _ = OpenSessionAsync(connection);
        _connections.SelectionChanged += item => _properties.ShowFor(item);

        MenuStrip menu = BuildMenu();
        ToolStrip toolbar = BuildToolbar(out _connectButton, out _disconnectButton);
        StatusStrip status = BuildStatusBar(out _sessionCountLabel);

        // Ordre d'ajout : la zone dockable remplit ce qui reste ; les barres se
        // placent ensuite sur les bords (menu en haut, outils dessous, état en bas).
        Controls.Add(_dockPanel);
        Controls.Add(toolbar);
        Controls.Add(menu);
        Controls.Add(status);
        MainMenuStrip = menu;

        Load += (_, _) => InitializeLayout();
    }

    /// <summary>Affiche les panneaux et charge l'arborescence depuis le serveur une fois la fenêtre prête.</summary>
    private void InitializeLayout()
    {
        _connections.Show(_dockPanel, DockState.DockLeft);
        _properties.Show(_connections.Pane, DockAlignment.Bottom, 0.45);

        _dockPanel.ContentAdded += (_, _) => UpdateSessionCount();
        _dockPanel.ContentRemoved += (_, _) => UpdateSessionCount();

        UpdateSessionCount();
        _ = LoadTreeAsync();
    }

    /// <summary>
    /// Charge l'arborescence depuis le serveur. En cas d'échec réseau, affiche un message
    /// lisible (ui-spec §8) et laisse l'arbre vide plutôt que de planter.
    /// </summary>
    private async Task LoadTreeAsync()
    {
        try
        {
            IReadOnlyList<ConnectionFolder> roots = await _server.GetTreeAsync();
            _roots = roots;
            _connections.LoadTree(roots);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _connections.LoadTree(Array.Empty<ConnectionFolder>());
            MessageBox.Show(
                this,
                "Impossible de charger les connexions depuis le serveur " +
                $"({_server.BaseAddress}). Vérifiez qu'il est démarré, l'adresse et le VPN.",
                "KyWigRemote",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip
        {
            BackColor = DarkPalette.PanelBackground,
            ForeColor = DarkPalette.Text,
        };

        var fichier = new ToolStripMenuItem("&Fichier");
        var refresh = new ToolStripMenuItem("&Actualiser", null, (_, _) => ReloadTree())
        {
            ShortcutKeys = Keys.F5,
        };
        var quitter = new ToolStripMenuItem("&Quitter", null, (_, _) => Close());
        fichier.DropDownItems.Add(refresh);
        fichier.DropDownItems.Add(new ToolStripSeparator());
        fichier.DropDownItems.Add(quitter);

        var affichage = new ToolStripMenuItem("&Affichage");
        var fullscreen = new ToolStripMenuItem("&Plein écran", null, (_, _) => ToggleFullScreen())
        {
            ShortcutKeys = Keys.F11,
        };
        affichage.DropDownItems.Add(fullscreen);

        var outils = new ToolStripMenuItem("&Outils");
        outils.DropDownItems.Add(new ToolStripMenuItem("Mes &identifiants…", null, (_, _) => OpenMyCredentials()));

        var aide = new ToolStripMenuItem("&Aide");
        aide.DropDownItems.Add(new ToolStripMenuItem("À &propos…", null, (_, _) => ShowAbout()));

        menu.Items.AddRange(new ToolStripItem[] { fichier, affichage, outils, aide });
        return menu;
    }

    private ToolStrip BuildToolbar(out ToolStripButton connect, out ToolStripButton disconnect)
    {
        var toolbar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = DarkPalette.PanelBackground,
            ForeColor = DarkPalette.Text,
            ImageScalingSize = new Size(16, 16),
        };

        var nouvelle = new ToolStripButton("Nouvelle")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Enabled = false,
            ToolTipText = "Création réservée à la console d'administration (E7).",
        };

        connect = new ToolStripButton("Connecter")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = "Ouvrir la connexion sélectionnée dans un onglet.",
        };
        connect.Click += (_, _) => ConnectSelected();

        disconnect = new ToolStripButton("Déconnecter")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = "Fermer l'onglet de session actif.",
        };
        disconnect.Click += (_, _) => CloseActiveSession();

        var fullscreen = new ToolStripButton("Plein écran")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = "Basculer en plein écran (F11).",
        };
        fullscreen.Click += (_, _) => ToggleFullScreen();

        toolbar.Items.AddRange(new ToolStripItem[]
        {
            nouvelle, connect, disconnect, new ToolStripSeparator(), fullscreen,
        });
        return toolbar;
    }

    private StatusStrip BuildStatusBar(out ToolStripStatusLabel sessionCount)
    {
        var status = new StatusStrip
        {
            BackColor = DarkPalette.PanelBackground,
            ForeColor = DarkPalette.TextMuted,
            SizingGrip = false,
        };

        string identity = _server.Session is { } s
            ? $"{s.Username}{(s.IsAdmin ? " (admin)" : string.Empty)}"
            : Environment.UserName;
        var user = new ToolStripStatusLabel($"Connecté : {identity}")
        {
            ForeColor = DarkPalette.TextMuted,
        };
        sessionCount = new ToolStripStatusLabel("0 session ouverte")
        {
            ForeColor = DarkPalette.TextMuted,
            Spring = true,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        var source = new ToolStripStatusLabel($"Serveur : {_server.BaseAddress}")
        {
            ForeColor = DarkPalette.TextMuted,
        };

        status.Items.AddRange(new ToolStripItem[] { user, sessionCount, source });
        return status;
    }

    private void ConnectSelected()
    {
        // Ouvre la connexion sélectionnée dans l'arbre ; sans rien faire si un
        // dossier (ou rien) est sélectionné.
        RemoteConnection? connection = _connections.SelectedConnection;
        if (connection is not null)
        {
            _ = OpenSessionAsync(connection);
        }
    }

    /// <summary>Ouvre (ou ré-active) un onglet de session pour la connexion demandée.</summary>
    private async Task OpenSessionAsync(RemoteConnection connection)
    {
        // Résout le mode d'identifiants effectif (héritage des dossiers), puis obtient
        // l'identifiant selon le mode avant d'ouvrir l'onglet.
        CredentialMode mode = CredentialResolver.ResolveEffectiveMode(_roots, connection);
        string? resolvedUser = null;

        try
        {
            switch (mode)
            {
                case CredentialMode.Personal:
                {
                    RevealedCredential? existing = await _server.GetPersonalCredentialAsync(connection.Id);
                    if (existing is not null)
                    {
                        resolvedUser = existing.Username;
                    }
                    else
                    {
                        using var prompt = new CredentialPromptDialog(connection.Name, Environment.UserName, allowRemember: true);
                        if (prompt.ShowDialog(this) != DialogResult.OK)
                        {
                            return;
                        }
                        resolvedUser = prompt.Username;
                        if (prompt.Remember || prompt.RememberGlobal)
                        {
                            await _server.SavePersonalCredentialAsync(connection.Id,
                                new SavePersonalCredentialRequest(prompt.Username, null, prompt.Password, prompt.RememberGlobal));
                        }
                    }
                    break;
                }

                case CredentialMode.Prompt:
                {
                    using var prompt = new CredentialPromptDialog(connection.Name, Environment.UserName, allowRemember: false);
                    if (prompt.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                    resolvedUser = prompt.Username;
                    break;
                }

                case CredentialMode.Enforced:
                    // Le secret imposé sera injecté à l'ouverture réelle (E5) ; sa révélation est
                    // réservée à l'admin pour l'instant (contrôle par groupes AD = E3).
                    break;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(this,
                "Erreur de communication avec le serveur pour les identifiants.",
                "KyWigRemote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var session = new SessionPanel(connection, mode, resolvedUser);
        session.Show(_dockPanel, DockState.Document);
    }

    private void CloseActiveSession()
    {
        if (_dockPanel.ActiveDocument is SessionPanel session)
        {
            session.Close();
        }
    }

    private void ReloadTree()
    {
        // F5 : recharge l'arborescence depuis le serveur.
        _ = LoadTreeAsync();
    }

    private void UpdateSessionCount()
    {
        int count = _dockPanel.DocumentsCount;
        _sessionCountLabel.Text = count <= 1
            ? $"{count} session ouverte"
            : $"{count} sessions ouvertes";
        _disconnectButton.Enabled = count > 0;
    }

    private void ToggleFullScreen()
    {
        if (!_isFullScreen)
        {
            _previousWindowState = WindowState;
            _previousBorderStyle = FormBorderStyle;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal; // requis avant de re-maximiser sans bordure
            WindowState = FormWindowState.Maximized;
            _isFullScreen = true;
        }
        else
        {
            FormBorderStyle = _previousBorderStyle;
            WindowState = _previousWindowState;
            _isFullScreen = false;
        }
    }

    private void OpenMyCredentials()
    {
        using var dialog = new MyCredentialsDialog(_server);
        dialog.ShowDialog(this);
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            this,
            "KyWigRemote — console de connexion\r\n" +
            "Gestionnaire de connexions distantes RDP / SSH.\r\n\r\n" +
            "Composants tiers :\r\n" +
            "• DockPanel Suite (docking) — licence MIT\r\n\r\n" +
            "Inspiré de l'ergonomie de mRemoteNG (GPL-2.0) sans réutilisation de son code.",
            "À propos de KyWigRemote",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    /// <summary>Raccourcis globaux : Ctrl+F (recherche), Ctrl+W (fermer l'onglet actif).</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.F:
                _connections.FocusSearch();
                return true;
            case Keys.Control | Keys.W:
                CloseActiveSession();
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
