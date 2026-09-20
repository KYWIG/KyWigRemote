using System.Windows.Forms;
using AxMSTSCLib;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Client.Sessions;

/// <summary>
/// Contrôle hébergeant une session RDP via le contrôle ActiveX Microsoft
/// (<c>AxMsRdpClient9NotSafeForScripting</c>, ADR-002). Il occupe tout l'onglet et
/// s'adapte à la taille (redimensionnement fluide du bureau distant).
///
/// À ce stade, aucun secret n'est injecté : le contrôle RDP demande lui-même
/// l'authentification (invite NLA). L'injection d'un identifiant imposé/personnel
/// est une évolution ultérieure qui devra récupérer le secret de façon sûre.
/// </summary>
internal sealed class RdpSessionControl : UserControl
{
    private readonly AxMsRdpClient9NotSafeForScripting _client;
    private readonly RemoteConnection _connection;
    private readonly SessionCredential? _credential;
    private bool _connectRequested;

    /// <summary>Déclenché à la déconnexion (fin de session ou échec) pour fermer l'onglet.</summary>
    public event EventHandler? SessionEnded;

    public RdpSessionControl(RemoteConnection connection, SessionCredential? credential)
    {
        _connection = connection;
        _credential = credential;

        _client = new AxMsRdpClient9NotSafeForScripting { Dock = DockStyle.Fill };
        _client.OnDisconnected += OnDisconnected;
        Controls.Add(_client);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Le contrôle ActiveX doit avoir son handle créé avant qu'on règle ses propriétés.
        if (_connectRequested)
        {
            return;
        }
        _connectRequested = true;
        BeginInvoke(ConnectClient);
    }

    private void ConnectClient()
    {
        _client.Server = _connection.Host;

        // Injection de l'identifiant résolu (imposé/personnel/demandé) si disponible.
        // ClearTextPassword est une string : dernière API exigeant une chaîne, tolérée par
        // la règle 3 de CLAUDE.md. Le secret n'est jamais journalisé.
        if (_credential is not null)
        {
            if (!string.IsNullOrWhiteSpace(_credential.Username))
            {
                _client.UserName = _credential.Username;
            }
            if (!string.IsNullOrWhiteSpace(_credential.Domain))
            {
                _client.Domain = _credential.Domain;
            }
            if (!string.IsNullOrEmpty(_credential.Password))
            {
                _client.AdvancedSettings9.ClearTextPassword = _credential.Password;
            }
        }
        else if (!string.IsNullOrWhiteSpace(_connection.Domain))
        {
            _client.Domain = _connection.Domain;
        }

        _client.AdvancedSettings9.RDPPort = _connection.Port;
        _client.AdvancedSettings9.EnableCredSspSupport = true; // NLA
        _client.AdvancedSettings9.AuthenticationLevel = 2;     // exige l'authentification serveur
        _client.AdvancedSettings9.SmartSizing = true;          // adapte le bureau distant à l'onglet

        _client.Connect();
    }

    private void OnDisconnected(object? sender, IMsTscAxEvents_OnDisconnectedEvent e)
    {
        SessionEnded?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _client.OnDisconnected -= OnDisconnected;
                if (_client.Connected != 0)
                {
                    _client.Disconnect();
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
            {
                // Contrôle déjà déconnecté ou détruit : rien à faire.
            }
        }
        base.Dispose(disposing);
    }
}
