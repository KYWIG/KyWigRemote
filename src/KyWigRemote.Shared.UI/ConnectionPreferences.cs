using System.Text.Json;

namespace KyWigRemote.Shared.UI;

/// <summary>
/// Préférences de connexion partagées par le client et la console d'administration :
/// dernière adresse de serveur et dernier identifiant utilisés, pour les pré-remplir au
/// lancement et réduire la saisie. Jamais de mot de passe (règle de sécurité nº 5).
/// Stockées dans %LOCALAPPDATA%\KyWigRemote\connect.json.
/// </summary>
public sealed class ConnectionPreferences
{
    /// <summary>Dernière adresse de serveur ayant abouti à une connexion.</summary>
    public string? LastServerUrl { get; set; }

    /// <summary>Dernier identifiant utilisé (jamais le mot de passe).</summary>
    public string? LastUsername { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KyWigRemote", "connect.json");

    /// <summary>Charge les préférences, ou des valeurs par défaut si le fichier est absent ou illisible.</summary>
    public static ConnectionPreferences Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<ConnectionPreferences>(json) ?? new ConnectionPreferences();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Préférences illisibles : on repart de zéro, sans bloquer le lancement.
        }

        return new ConnectionPreferences();
    }

    /// <summary>Enregistre les préférences ; les erreurs d'écriture sont silencieuses (simple confort).</summary>
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
