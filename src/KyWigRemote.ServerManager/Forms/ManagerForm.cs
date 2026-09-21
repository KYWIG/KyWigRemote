using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Security.Principal;
using System.Windows.Forms;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.ServerManager.Forms;

/// <summary>
/// Console de gestion du serveur KyWigRemote en service Windows : installation, suppression,
/// démarrage, arrêt, redémarrage, et affichage en direct du journal du serveur.
/// </summary>
internal sealed class ManagerForm : Form
{
    private static readonly Color Green = Color.FromArgb(96, 190, 110);
    private static readonly Color Red = Color.FromArgb(210, 100, 100);
    private static readonly Color Orange = Color.FromArgb(220, 170, 90);

    private readonly WindowsServiceManager _service = new();
    private readonly string? _serverExe = ServerLocator.FindServerExe();
    private readonly LogTailer _tailer;
    private readonly bool _isAdmin = IsElevated();

    private readonly Label _stateLabel;
    private readonly Label _pathLabel;
    private readonly Button _installButton;
    private readonly Button _uninstallButton;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly Button _restartButton;
    private readonly TextBox _logBox;
    private readonly System.Windows.Forms.Timer _statusTimer;
    private readonly System.Windows.Forms.Timer _logTimer;

    private bool _busy;

    public ManagerForm()
    {
        _tailer = new LogTailer(ServerLocator.FindLogDirectory(_serverExe));

        Text = "KyWigRemote — Gestion du serveur";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 520);
        Size = new Size(760, 560);
        BackColor = DarkPalette.Background;
        ForeColor = DarkPalette.Text;
        Font = new Font("Segoe UI", 9f);

        var header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = DarkPalette.PanelBackground };
        _stateLabel = new Label
        {
            Text = "État : …", AutoSize = true, Location = new Point(16, 14),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = DarkPalette.Text,
        };
        _pathLabel = new Label
        {
            AutoSize = false, Location = new Point(16, 44), Size = new Size(700, 20),
            ForeColor = DarkPalette.TextMuted,
            Text = _serverExe is null
                ? "Exécutable du serveur introuvable — compile ou publie le serveur."
                : $"Serveur : {_serverExe}",
        };
        var adminLabel = new Label
        {
            AutoSize = false, Location = new Point(16, 66), Size = new Size(520, 20),
            ForeColor = _isAdmin ? DarkPalette.TextMuted : Orange,
            Text = _isAdmin
                ? "Droits administrateur : présents."
                : "Droits administrateur absents — les actions sur le service sont désactivées.",
        };
        header.Controls.Add(_stateLabel);
        header.Controls.Add(_pathLabel);
        header.Controls.Add(adminLabel);

        if (!_isAdmin)
        {
            var elevate = new Button
            {
                Text = "Relancer en administrateur", Location = new Point(540, 62), Width = 190, Height = 26,
                FlatStyle = FlatStyle.Flat, BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text,
            };
            elevate.FlatAppearance.BorderColor = DarkPalette.Border;
            elevate.Click += (_, _) => RelaunchElevated();
            header.Controls.Add(elevate);
        }

        var buttonBar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = DarkPalette.Background };
        _installButton = MakeButton("Installer", 16, buttonBar, OnInstall);
        _uninstallButton = MakeButton("Désinstaller", 130, buttonBar, OnUninstall);
        _startButton = MakeButton("Démarrer", 260, buttonBar, OnStart);
        _stopButton = MakeButton("Arrêter", 374, buttonBar, OnStop);
        _restartButton = MakeButton("Redémarrer", 488, buttonBar, OnRestart);

        _logBox = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(24, 26, 28), ForeColor = DarkPalette.Text, BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 9f), WordWrap = false,
        };

        var logHeader = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = DarkPalette.Background };
        logHeader.Controls.Add(new Label
        {
            Text = "Journal du serveur", AutoSize = true, Location = new Point(16, 6), ForeColor = DarkPalette.TextMuted,
        });
        var clear = new Button
        {
            Text = "Effacer", Location = new Point(650, 2), Width = 90, Height = 24, Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat, BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text,
        };
        clear.FlatAppearance.BorderColor = DarkPalette.Border;
        clear.Click += (_, _) => _logBox.Clear();
        logHeader.Controls.Add(clear);

        Controls.Add(_logBox);
        Controls.Add(logHeader);
        Controls.Add(buttonBar);
        Controls.Add(header);

        _statusTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _logTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _logTimer.Tick += (_, _) => PumpLog();

        Load += (_, _) =>
        {
            RefreshStatus();
            PumpLog();
            _statusTimer.Start();
            _logTimer.Start();
        };
    }

    private Button MakeButton(string text, int x, Control parent, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text, Location = new Point(x, 8), Width = 108, Height = 30,
            FlatStyle = FlatStyle.Flat, BackColor = DarkPalette.InputBackground, ForeColor = DarkPalette.Text,
        };
        button.FlatAppearance.BorderColor = DarkPalette.Border;
        button.Click += onClick;
        parent.Controls.Add(button);
        return button;
    }

    private void RefreshStatus()
    {
        ServiceState state = _service.QueryState();
        (string text, Color color) = state switch
        {
            ServiceState.NotInstalled => ("Service non installé", DarkPalette.TextMuted),
            ServiceState.Stopped => ("Service arrêté", Red),
            ServiceState.Running => ("Service démarré", Green),
            ServiceState.Pending => ("Transition en cours…", Orange),
            _ => ("État inconnu", Orange),
        };
        _stateLabel.Text = $"État : {text}";
        _stateLabel.ForeColor = color;

        bool canAct = _isAdmin && !_busy;
        bool haveExe = _serverExe is not null;
        _installButton.Enabled = canAct && haveExe && state == ServiceState.NotInstalled;
        _uninstallButton.Enabled = canAct && (state is ServiceState.Stopped or ServiceState.Running);
        _startButton.Enabled = canAct && state == ServiceState.Stopped;
        _stopButton.Enabled = canAct && state == ServiceState.Running;
        _restartButton.Enabled = canAct && state == ServiceState.Running;
    }

    private async void OnInstall(object? sender, EventArgs e)
    {
        if (_serverExe is null) { return; }
        await RunAction(() => _service.Install(_serverExe));
    }

    private async void OnUninstall(object? sender, EventArgs e)
    {
        DialogResult confirm = MessageBox.Show(this,
            "Désinstaller le service KyWigRemote ? Le serveur sera arrêté.",
            "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm == DialogResult.Yes)
        {
            await RunAction(_service.Uninstall);
        }
    }

    private async void OnStart(object? sender, EventArgs e) => await RunAction(_service.Start);

    private async void OnStop(object? sender, EventArgs e) => await RunAction(_service.Stop);

    private async void OnRestart(object? sender, EventArgs e) => await RunAction(_service.Restart);

    /// <summary>Exécute une action de service hors du fil UI, puis rafraîchit l'affichage.</summary>
    private async Task RunAction(Func<ServiceActionResult> action)
    {
        _busy = true;
        RefreshStatus();
        ServiceActionResult result = await Task.Run(action);
        _busy = false;
        RefreshStatus();

        AppendLine($"[gestion] {result.Message}");
        if (!result.Success)
        {
            MessageBox.Show(this, result.Message, "KyWigRemote — Gestion du serveur",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void PumpLog()
    {
        string text = _tailer.ReadNew();
        if (text.Length > 0)
        {
            Append(text);
        }
    }

    private void AppendLine(string line) => Append(line + Environment.NewLine);

    private void Append(string text)
    {
        // Plafonne la taille pour ne pas gonfler indéfiniment.
        if (_logBox.TextLength > 200_000)
        {
            _logBox.Text = _logBox.Text[^100_000..];
        }
        _logBox.AppendText(text);
    }

    private void RelaunchElevated()
    {
        string? exe = Environment.ProcessPath;
        if (exe is null) { return; }
        try
        {
            Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true, Verb = "runas" });
            Application.Exit();
        }
        catch (Win32Exception)
        {
            // L'utilisateur a refusé l'élévation (UAC) : on reste en mode restreint.
        }
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
