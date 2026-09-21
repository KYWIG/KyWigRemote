namespace KyWigRemote.ServerManager;

/// <summary>
/// Localise l'exécutable du serveur (KyWigRemote.Server.exe) et son dossier de journaux.
/// Gère le cas « publié côte à côte » (même dossier) et le cas « développement »
/// (bin\ parallèle sous src\).
/// </summary>
internal static class ServerLocator
{
    private const string ServerExeName = "KyWigRemote.Server.exe";

    /// <summary>Retourne le chemin de l'exécutable du serveur, ou <c>null</c> s'il est introuvable.</summary>
    public static string? FindServerExe()
    {
        string baseDir = AppContext.BaseDirectory;

        // 1. Publié : le serveur est dans le même dossier que le gestionnaire.
        string sideBySide = Path.Combine(baseDir, ServerExeName);
        if (File.Exists(sideBySide))
        {
            return sideBySide;
        }

        // 2. Développement : ...\KyWigRemote.ServerManager\bin\... -> ...\KyWigRemote.Server\bin\...
        if (baseDir.Contains("KyWigRemote.ServerManager", StringComparison.OrdinalIgnoreCase))
        {
            string devPath = Path.Combine(
                baseDir.Replace("KyWigRemote.ServerManager", "KyWigRemote.Server", StringComparison.OrdinalIgnoreCase),
                ServerExeName);
            if (File.Exists(devPath))
            {
                return devPath;
            }
        }

        return null;
    }

    /// <summary>Retourne le dossier des journaux du serveur (à côté de son exécutable), ou <c>null</c>.</summary>
    public static string? FindLogDirectory(string? serverExePath)
    {
        if (string.IsNullOrEmpty(serverExePath))
        {
            return null;
        }
        string? dir = Path.GetDirectoryName(serverExePath);
        return dir is null ? null : Path.Combine(dir, "logs");
    }
}
