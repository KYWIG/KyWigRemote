namespace KyWigRemote.ServerManager;

/// <summary>
/// Suit le fichier de journal le plus récent d'un dossier (logs Serilog tournants) et retourne
/// le texte ajouté depuis la dernière lecture. Résiste au changement de fichier (rotation) et à
/// l'absence de dossier. Aucune écriture, lecture partagée pour ne pas gêner le serveur.
/// </summary>
internal sealed class LogTailer
{
    private readonly string? _logDirectory;
    private string? _currentFile;
    private long _position;

    public LogTailer(string? logDirectory)
    {
        _logDirectory = logDirectory;
    }

    /// <summary>Retourne le nouveau texte depuis le dernier appel (chaîne vide s'il n'y a rien).</summary>
    public string ReadNew()
    {
        if (string.IsNullOrEmpty(_logDirectory) || !Directory.Exists(_logDirectory))
        {
            return string.Empty;
        }

        string? newest;
        try
        {
            newest = Directory.EnumerateFiles(_logDirectory, "kywig-*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (IOException)
        {
            return string.Empty;
        }

        if (newest is null)
        {
            return string.Empty;
        }

        // Changement de fichier (rotation) : on repart du début du nouveau fichier.
        if (!string.Equals(newest, _currentFile, StringComparison.OrdinalIgnoreCase))
        {
            _currentFile = newest;
            _position = 0;
        }

        try
        {
            using var stream = new FileStream(newest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length < _position)
            {
                _position = 0; // le fichier a été tronqué/recréé
            }
            stream.Seek(_position, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            string text = reader.ReadToEnd();
            _position = stream.Length;
            return text;
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    /// <summary>Force la prochaine lecture à repartir de zéro (nouveau fichier détecté ensuite).</summary>
    public void Reset()
    {
        _currentFile = null;
        _position = 0;
    }
}
