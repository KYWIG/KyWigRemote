using KyWigRemote.Admin.Forms;
using KyWigRemote.Core.Remote;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Admin;

/// <summary>
/// Point d'entrée de la console d'administration (membres administrateurs).
/// Au lancement, l'utilisateur se connecte au serveur ; l'accès est refusé si le
/// compte n'est pas administrateur.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        ThemeManager.Load();

        using var dialog = new ServerConnectDialog("KyWigRemote — Administration", "http://localhost:5080");
        if (dialog.ShowDialog() != DialogResult.OK || dialog.ConnectedClient is null)
        {
            return;
        }

        ServerClient client = dialog.ConnectedClient;
        if (client.Session is null || !client.Session.CanManageConnections)
        {
            MessageBox.Show(
                "Accès refusé. Cette console est réservée aux administrateurs (des connexions ou globaux).",
                "KyWigRemote — Administration",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            client.Dispose();
            return;
        }

        Application.Run(new AdminMainForm(client));
    }
}
