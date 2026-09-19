using System.Text.Json;

namespace KyWigRemote.Client;

/// <summary>
/// Préférences locales du poste (côté présentation, hors base partagée) :
/// pour l'instant, la dernière adresse de serveur utilisée, afin de la
/// représenter par défaut au lancement.
/// Stockées dans %LOCALAPPDATA%\KyWigRemote\client.json.
/// </summary>
internal sealed class ClientSettings
{
    /// <summary>Dernière adresse de serveur saisie avec succès.</summary>
    public string? LastServerUrl { get; set; }

    /// <summary>
    /// Chemin explicite vers putty.exe (poste où PuTTY n'est pas installé à un emplacement
    /// standard). Vide par défaut : la localisation automatique s'en charge alors.
    /// </summary>
    public string? PuttyPath { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KyWigRemote", "client.json");

    /// <summary>Charge les préférences, ou des valeurs par défaut si le fichier est absent ou illisible.</summary>
    public static ClientSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<ClientSettings>(json) ?? new ClientSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Préférences illisibles : on repart de zéro, sans bloquer le lancement.
        }

        return new ClientSettings();
    }

    /// <summary>Enregistre les préférences ; les erreurs d'écriture sont silencieuses (confort, non critique).</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sans importance : la préférence n'est qu'un confort de saisie.
        }
    }
}
