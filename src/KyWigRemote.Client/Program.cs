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

        // Charge le thème (clair/sombre) avant toute fenêtre, pour qu'il s'applique dès la construction.
        ThemeManager.Load();

        // L'adresse et l'identifiant précédents sont pré-remplis par la boîte de connexion
        // elle-même (préférences partagées) : rien à gérer ici.
        using var dialog = new ServerConnectDialog("KyWigRemote — connexion au serveur", defaultUrl: null);
        if (dialog.ShowDialog() != DialogResult.OK || dialog.ConnectedClient is null)
        {
            // Connexion annulée : on ne lance pas l'application.
            return;
        }

        Application.Run(new MainForm(dialog.ConnectedClient));
    }
}
