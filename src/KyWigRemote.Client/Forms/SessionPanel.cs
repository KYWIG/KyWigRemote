using System.Drawing;
using System.Windows.Forms;
using KyWigRemote.Shared.UI;
using KyWigRemote.Core.Model;
using WeifenLuo.WinFormsUI.Docking;

namespace KyWigRemote.Client.Forms;

/// <summary>
/// Onglet de session, affiché dans la zone centrale (ui-spec §1).
/// Il héberge le contenu d'une session pour une connexion donnée.
///
/// À ce stade, l'ouverture réelle n'est pas implémentée : RDP est la story E5
/// (contrôle ActiveX AxMsRdpClient9) et SSH la story E6 (reparentage de PuTTY).
/// Ce panneau affiche donc un état d'attente explicite plutôt qu'une fausse session.
/// </summary>
internal sealed class SessionPanel : DockContent
{
    public SessionPanel(RemoteConnection connection, CredentialMode effectiveMode, string? resolvedUsername)
    {
        Text = connection.Name;
        DockAreas = DockAreas.Document;
        BackColor = DarkPalette.Background;

        string protocol = connection.Protocol.ToString().ToUpperInvariant();
        string story = connection.Protocol == RemoteProtocol.Rdp ? "E5 (RDP)" : "E6 (SSH)";
        string identity = resolvedUsername is null
            ? string.Empty
            : $"Identifiant résolu : {resolvedUsername}.\r\n";

        var label = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = DarkPalette.TextMuted,
            Font = new Font("Segoe UI", 10f),
            Text =
                $"Session {protocol} vers « {connection.Name} » ({connection.Host}:{connection.Port})\r\n\r\n" +
                $"Mode d'identifiants résolu : {DescribeMode(effectiveMode)}.\r\n" +
                identity + "\r\n" +
                $"L'ouverture réelle de la session est à implémenter — story {story}.\r\n" +
                "Cet onglet est le conteneur qui accueillera le contrôle de session.",
        };

        Controls.Add(label);
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
