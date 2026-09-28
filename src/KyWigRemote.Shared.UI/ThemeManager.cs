namespace KyWigRemote.Shared.UI;

/// <summary>
/// Gère le thème actif (clair/sombre) et sa persistance, partagé par toutes les applications.
/// La préférence est stockée dans %LOCALAPPDATA%\KyWigRemote\theme (une ligne : Dark ou Light).
/// À appeler <see cref="Load"/> au démarrage, avant de créer les fenêtres, pour que la palette
/// prenne le bon thème dès la construction.
/// </summary>
public static class ThemeManager
{
    /// <summary>Thème actif (sombre par défaut).</summary>
    public static AppTheme Mode { get; private set; } = AppTheme.Dark;

    /// <summary>Couleurs du thème actif.</summary>
    public static ThemeColors Current => Mode == AppTheme.Light ? ThemeColors.Light : ThemeColors.Dark;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KyWigRemote", "theme");

    /// <summary>Charge la préférence enregistrée (sans échouer si le fichier est absent ou illisible).</summary>
    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath)
                && Enum.TryParse(File.ReadAllText(FilePath).Trim(), ignoreCase: true, out AppTheme mode))
            {
                Mode = mode;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Préférence illisible : on garde le thème par défaut.
        }
    }

    /// <summary>Change le thème actif et enregistre la préférence (écriture silencieuse si impossible).</summary>
    public static void SetMode(AppTheme mode)
    {
        Mode = mode;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, mode.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sans importance : la préférence n'est qu'un confort d'affichage.
        }
    }
}
