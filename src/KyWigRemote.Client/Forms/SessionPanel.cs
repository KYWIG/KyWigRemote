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
/// RDP (E5) reste à implémenter (contrôle ActiveX AxMsRdpClient9) et affiche donc un état
/// d'attente explicite plutôt qu'une fausse session.
/// </summary>
internal sealed class SessionPanel : DockContent
{
    private readonly RemoteConnection _connection;
    private readonly CredentialMode _effectiveMode;
    private readonly string? _resolvedUsername;
    private PuttySshSession? _ssh;

    public SessionPanel(RemoteConnection connection, CredentialMode effectiveMode, string? resolvedUsername)
    {
        _connection = connection;
        _effectiveMode = effectiveMode;
        _resolvedUsername = resolvedUsername;

        Text = connection.Name;
        DockAreas = DockAreas.Document;
        BackColor = DarkPalette.Background;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Le reparentage exige un handle : on ne démarre la session qu'ici, et une seule fois.
        if (_ssh is not null || Controls.Count > 0)
        {
            return;
        }

        if (_connection.Protocol == RemoteProtocol.Ssh)
        {
            StartSshSession();
        }
        else
        {
            ShowPending();
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

    private void ShowPending()
    {
        string identity = _resolvedUsername is null
            ? string.Empty
            : $"Identifiant résolu : {_resolvedUsername}.\r\n";

        ShowMessage(
            $"Session RDP vers « {_connection.Name} » ({_connection.Host}:{_connection.Port})\r\n\r\n" +
            $"Mode d'identifiants résolu : {DescribeMode(_effectiveMode)}.\r\n" +
            identity + "\r\n" +
            "L'ouverture réelle de la session RDP est à implémenter — story E5.\r\n" +
            "Cet onglet est le conteneur qui accueillera le contrôle de session.");
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
        // Fermeture de l'onglet : on termine PuTTY pour ne laisser aucun processus résiduel (E6.3).
        _ssh?.Dispose();
        _ssh = null;
        base.OnFormClosed(e);
    }

    private static string DescribeMode(CredentialMode mode) => mode switch
    {
        CredentialMode.Personal => "Personnel",
        CredentialMode.Enforced => "Imposé",
        CredentialMode.Prompt => "À la demande",
        CredentialMode.Inherited => "Hérité",
        _ => mode.ToString(),
    };
}
