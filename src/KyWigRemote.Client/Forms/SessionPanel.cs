using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Client.Sessions;
using KyWigRemote.Shared.UI;
using KyWigRemote.Core.Model;
using WeifenLuo.WinFormsUI.Docking;

namespace KyWigRemote.Client.Forms;

/// <summary>
/// Onglet de session, affiché dans la zone centrale (ui-spec §1).
/// Il héberge le contenu d'une session pour une connexion donnée.
///
/// SSH (E6) est intégré : PuTTY est lancé puis sa fenêtre est reparentée dans cet onglet.
/// RDP (E5) est intégré via le contrôle ActiveX AxMsRdpClient9 hébergé dans l'onglet.
/// </summary>
internal sealed class SessionPanel : DockContent
{
    private readonly RemoteConnection _connection;
    private readonly string? _resolvedUsername;
    private PuttySshSession? _ssh;
    private RdpSessionControl? _rdp;

    // effectiveMode est conservé dans la signature pour l'appelant ; l'ouverture réelle
    // n'en dépend pas encore (l'authentification est saisie dans la session elle-même).
    public SessionPanel(RemoteConnection connection, CredentialMode effectiveMode, string? resolvedUsername)
    {
        _ = effectiveMode;
        _connection = connection;
        _resolvedUsername = resolvedUsername;

        Text = connection.Name;
        DockAreas = DockAreas.Document;
        BackColor = DarkPalette.Background;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Le reparentage / l'ActiveX exigent un handle : on ne démarre qu'ici, et une seule fois.
        if (_ssh is not null || _rdp is not null || Controls.Count > 0)
        {
            return;
        }

        if (_connection.Protocol == RemoteProtocol.Ssh)
        {
            StartSshSession();
        }
        else
        {
            StartRdpSession();
        }
    }

    private void StartRdpSession()
    {
        try
        {
            _rdp = new RdpSessionControl(_connection, _resolvedUsername) { Dock = DockStyle.Fill };
            _rdp.SessionEnded += (_, _) => Close();
            Controls.Add(_rdp);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            _rdp?.Dispose();
            _rdp = null;
            ShowMessage(
                "L'ouverture de la session RDP a échoué.\r\n\r\n" +
                "Le contrôle Bureau à distance n'a pas pu être initialisé sur ce poste.");
        }
    }

    private void StartSshSession()
    {
        string? puttyPath = PuttyLocator.Resolve(ClientSettings.Load().PuttyPath);
        if (puttyPath is null)
        {
            ShowMessage(
                "PuTTY est introuvable sur ce poste.\r\n\r\n" +
                "Installez PuTTY (putty.exe), ou renseignez son chemin dans les préférences,\r\n" +
                "puis rouvrez la session.");
            return;
        }

        try
        {
            _ssh = new PuttySshSession(this, _connection, _resolvedUsername, puttyPath);
            _ssh.SessionEnded += (_, _) => Close();
            _ssh.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _ssh?.Dispose();
            _ssh = null;
            ShowMessage(
                "Le lancement de PuTTY a échoué.\r\n\r\n" +
                "Vérifiez que le chemin de PuTTY est correct et que l'exécutable est accessible.");
        }
    }

    private void ShowMessage(string text)
    {
        var label = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = DarkPalette.TextMuted,
            Font = new Font("Segoe UI", 10f),
            Text = text,
        };
        Controls.Add(label);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Fermeture de l'onglet : on termine PuTTY (E6.3) et on déconnecte proprement le RDP.
        _ssh?.Dispose();
        _ssh = null;
        _rdp?.Dispose();
        _rdp = null;
        base.OnFormClosed(e);
    }
}
