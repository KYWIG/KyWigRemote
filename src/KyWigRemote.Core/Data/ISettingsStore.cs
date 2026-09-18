namespace KyWigRemote.Core.Data;

/// <summary>
/// Accès à la table clé/valeur <c>settings</c> : petits paramètres persistants du serveur
/// (ex. clé de signature de jetons générée au premier démarrage, pour ne pas la coder en dur
/// ni la mettre dans un fichier versionné).
/// </summary>
public interface ISettingsStore
{
    /// <summary>Retourne la valeur associée à la clé, ou null si absente.</summary>
    string? Get(string key);

    /// <summary>Crée ou remplace la valeur associée à la clé.</summary>
    void Set(string key, string value);
}
