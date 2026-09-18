using KyWigRemote.Client.Forms;
using KyWigRemote.Shared.UI;

namespace KyWigRemote.Client;

/// <summary>
/// Point d'entrée de la console de connexion (tous les utilisateurs).
/// Au lancement, l'utilisateur choisit le serveur KyWigRemote ; l'application ne
/// démarre que si un serveur répond.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Prépare la configuration WinForms (DPI, police par défaut).
        ApplicationConfiguration.Initialize();

        ClientSettings settings = ClientSettings.Load();

        using var dialog = new ServerConnectDialog("KyWigRemote — connexion au serveur", settings.LastServerUrl);
        if (dialog.ShowDialog() != DialogResult.OK || dialog.ConnectedClient is null)
        {
            // Connexion annulée : on ne lance pas l'application.
            return;
        }

        // Mémorise le serveur retenu pour le prochain lancement.
        settings.LastServerUrl = dialog.ConnectedClient.BaseAddress.ToString();
        settings.Save();

        Application.Run(new MainForm(dialog.ConnectedClient));
    }
}
