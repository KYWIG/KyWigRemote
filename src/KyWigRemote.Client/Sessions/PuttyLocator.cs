namespace KyWigRemote.Client.Sessions;

/// <summary>
/// Localise l'exécutable PuTTY. On ne l'embarque pas encore : on cherche une installation
/// connue, puis le PATH. Le chemin explicite des préférences a la priorité (poste atypique).
/// </summary>
internal static class PuttyLocator
{
    /// <summary>
    /// Retourne le chemin de putty.exe, ou <c>null</c> s'il est introuvable.
    /// </summary>
    /// <param name="configuredPath">Chemin imposé par les préférences, éventuellement vide.</param>
    public static string? Resolve(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        foreach (string candidate in DefaultLocations())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return FindOnPath();
    }

    private static IEnumerable<string> DefaultLocations()
    {
        foreach (string variable in new[] { "ProgramFiles", "ProgramW6432", "ProgramFiles(x86)" })
        {
            string? root = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(root))
            {
                yield return Path.Combine(root, "PuTTY", "putty.exe");
            }
        }
    }

    private static string? FindOnPath()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(directory.Trim(), "putty.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // Entrée de PATH invalide (caractères interdits) : on l'ignore.
            }
        }

        return null;
    }
}
