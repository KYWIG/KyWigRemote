using System.Diagnostics;
using System.Windows.Forms;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Client.Sessions;

/// <summary>
/// Session SSH ouverte par lancement de PuTTY puis reparentage de sa fenêtre dans un panneau
/// de l'application (ADR-004). Le mot de passe n'est <b>jamais</b> passé en ligne de commande
/// (règle 4 de CLAUDE.md : <c>-pw</c> exposerait le secret dans le gestionnaire de tâches) ;
/// PuTTY demande lui-même l'authentification dans son terminal, désormais intégré à l'onglet.
///
/// Le contrôle réel (voir la doc pour l'usage) : construire l'instance, appeler
/// <see cref="Start"/> une fois le panneau hôte affiché (handle créé), puis <see cref="Dispose"/>
/// à la fermeture de l'onglet pour ne laisser aucun putty.exe résiduel (E6.3).
/// </summary>
internal sealed class PuttySshSession : IDisposable
{
    private const int SW_SHOW = 5;

    private readonly Control _host;
    private readonly string _puttyPath;
    private readonly string _arguments;

    private Process? _process;
    private IntPtr _puttyWindow = IntPtr.Zero;
    private System.Windows.Forms.Timer? _attachTimer;
    private int _attachAttempts;
    private bool _disposed;

    /// <summary>Déclenché quand PuTTY se termine (fin de session, déconnexion) pour fermer l'onglet.</summary>
    public event EventHandler? SessionEnded;

    /// <param name="host">Panneau qui accueillera la fenêtre PuTTY (doit avoir son handle créé).</param>
    /// <param name="connection">Connexion cible (hôte, port).</param>
    /// <param name="resolvedUsername">Utilisateur résolu, ou <c>null</c> pour laisser PuTTY le demander.</param>
    /// <param name="puttyPath">Chemin de putty.exe déjà localisé.</param>
    public PuttySshSession(Control host, RemoteConnection connection, string? resolvedUsername, string puttyPath)
    {
        _host = host;
        _puttyPath = puttyPath;
        _arguments = BuildArguments(connection, resolvedUsername);
    }

    /// <summary>
    /// Construit les arguments PuTTY. Volontairement sans <c>-pw</c> : l'authentification
    /// (mot de passe ou clé) est saisie dans le terminal PuTTY lui-même.
    /// </summary>
    private static string BuildArguments(RemoteConnection connection, string? resolvedUsername)
    {
        string target = string.IsNullOrWhiteSpace(resolvedUsername)
            ? connection.Host
            : $"{resolvedUsername}@{connection.Host}";

        return $"-ssh {target} -P {connection.Port}";
    }

    /// <summary>
    /// Lance PuTTY et démarre l'intégration de sa fenêtre. Lève une exception si le processus
    /// ne peut pas démarrer ; l'appelant traduit cela en message utilisateur.
    /// </summary>
    public void Start()
    {
        var info = new ProcessStartInfo
        {
            FileName = _puttyPath,
            Arguments = _arguments,
            UseShellExecute = false,
        };

        _process = Process.Start(info)
            ?? throw new InvalidOperationException("Le processus PuTTY n'a pas pu être démarré.");
        _process.EnableRaisingEvents = true;
        _process.Exited += OnProcessExited;

        // La fenêtre PuTTY n'existe pas immédiatement : on l'attend par sondage sur le fil UI.
        _attachTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _attachTimer.Tick += TryAttachWindow;
        _attachTimer.Start();
    }

    private void TryAttachWindow(object? sender, EventArgs e)
    {
        if (_disposed || _process is null)
        {
            StopAttachTimer();
            return;
        }

        _attachAttempts++;
        _process.Refresh();

        IntPtr handle = _process.MainWindowHandle;
        if (handle != IntPtr.Zero)
        {
            StopAttachTimer();
            AttachWindow(handle);
            return;
        }

        // ~5 s d'attente : au-delà, PuTTY a probablement échoué à s'ouvrir.
        if (_process.HasExited || _attachAttempts > 50)
        {
            StopAttachTimer();
        }
    }

    private void AttachWindow(IntPtr handle)
    {
        _puttyWindow = handle;
        NativeMethods.MakeChildWindow(handle);
        NativeMethods.SetParent(handle, _host.Handle);
        NativeMethods.ShowWindow(handle, SW_SHOW);
        ResizeToHost();
        _host.Resize += OnHostResize;
    }

    private void OnHostResize(object? sender, EventArgs e) => ResizeToHost();

    private void ResizeToHost()
    {
        if (_puttyWindow != IntPtr.Zero)
        {
            NativeMethods.MoveWindow(_puttyWindow, 0, 0, _host.ClientSize.Width, _host.ClientSize.Height, true);
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        // L'événement arrive sur un fil de pool : on revient sur le fil UI pour toucher aux contrôles.
        if (_disposed)
        {
            return;
        }

        if (_host.IsHandleCreated)
        {
            _host.BeginInvoke(() => SessionEnded?.Invoke(this, EventArgs.Empty));
        }
    }

    private void StopAttachTimer()
    {
        if (_attachTimer is not null)
        {
            _attachTimer.Stop();
            _attachTimer.Tick -= TryAttachWindow;
            _attachTimer.Dispose();
            _attachTimer = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        StopAttachTimer();
        _host.Resize -= OnHostResize;

        if (_process is not null)
        {
            _process.Exited -= OnProcessExited;
            try
            {
                if (!_process.HasExited)
                {
                    // Fermeture propre d'abord ; sinon on force pour ne pas laisser de putty.exe (E6.3).
                    _process.Kill();
                    _process.WaitForExit(2000);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Le processus est déjà parti : rien à faire.
            }
            _process.Dispose();
            _process = null;
        }
    }
}
