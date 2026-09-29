namespace KyWigRemote.Server.Configuration;

/// <summary>
/// Configuration du serveur, liée à la section « KyWigRemote » de appsettings.json.
/// Tout est configurable ici : rien n'est codé en dur. Validée au démarrage
/// (<see cref="Validate"/>) pour échouer proprement plutôt que tard et sans explication.
/// </summary>
public sealed class KyWigRemoteOptions
{
    public const string SectionName = "KyWigRemote";

    /// <summary>Paramètres d'hébergement (adresse d'écoute).</summary>
    public ServerOptions Server { get; set; } = new();

    /// <summary>Choix et paramètres de la base de données.</summary>
    public DatabaseOptions Database { get; set; } = new();

    /// <summary>Fournisseurs d'authentification activés et leurs paramètres.</summary>
    public AuthenticationOptions Authentication { get; set; } = new();

    /// <summary>Distribution du client par ClickOnce, servie en HTTP par ce serveur.</summary>
    public DistributionOptions Distribution { get; set; } = new();

    /// <summary>Valide la cohérence de la configuration ; lève une exception listant les erreurs.</summary>
    public void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Server.Urls))
        {
            errors.Add("KyWigRemote:Server:Urls est vide (ex. \"http://0.0.0.0:5080\").");
        }

        if (Authentication.Providers.Count == 0)
        {
            errors.Add("KyWigRemote:Authentication:Providers ne peut pas être vide (ex. [\"ActiveDirectory\"]).");
        }

        foreach (string provider in Authentication.Providers)
        {
            if (!AuthenticationOptions.KnownProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Fournisseur d'authentification inconnu : « {provider} ». " +
                    $"Valeurs possibles : {string.Join(", ", AuthenticationOptions.KnownProviders)}.");
            }
        }

        if (Authentication.IsEnabled("ActiveDirectory")
            && string.IsNullOrWhiteSpace(Authentication.ActiveDirectory.Domain))
        {
            errors.Add("KyWigRemote:Authentication:ActiveDirectory:Domain est requis quand ActiveDirectory est activé.");
        }

        if (Authentication.IsEnabled("Microsoft365"))
        {
            if (string.IsNullOrWhiteSpace(Authentication.Microsoft365.TenantId))
            {
                errors.Add("KyWigRemote:Authentication:Microsoft365:TenantId est requis quand Microsoft365 est activé.");
            }
            if (string.IsNullOrWhiteSpace(Authentication.Microsoft365.ClientId))
            {
                errors.Add("KyWigRemote:Authentication:Microsoft365:ClientId est requis quand Microsoft365 est activé.");
            }
        }

        if (Authentication.Jwt.LifetimeMinutes <= 0)
        {
            errors.Add("KyWigRemote:Authentication:Jwt:LifetimeMinutes doit être strictement positif.");
        }

        if (!string.IsNullOrEmpty(Authentication.Jwt.SigningKey)
            && Authentication.Jwt.SigningKey.Length < 32)
        {
            errors.Add("KyWigRemote:Authentication:Jwt:SigningKey doit faire au moins 32 caractères " +
                "(ou être laissé vide pour une génération automatique).");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Configuration KyWigRemote invalide :" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(e => " - " + e)));
        }
    }
}

/// <summary>Paramètres d'hébergement du serveur.</summary>
public sealed class ServerOptions
{
    /// <summary>
    /// Adresse(s) d'écoute, séparées par « ; ». En production, utiliser HTTPS
    /// (ex. « https://0.0.0.0:5443 ») : l'outil transporte des mots de passe, ils ne doivent
    /// jamais circuler en clair sur le réseau. Le HTTP en clair reste toléré vers la boucle
    /// locale (développement) ; vers le réseau il est refusé sur les endpoints sensibles, sauf
    /// <see cref="AllowInsecureHttp"/>.
    /// </summary>
    public string Urls { get; set; } = "http://localhost:5080";

    /// <summary>
    /// Autorise explicitement le HTTP en clair depuis le réseau sur les endpoints sensibles
    /// (déconseillé : à réserver à un réseau de confiance ou à un proxy TLS en amont). Par
    /// défaut, seule la boucle locale et HTTPS sont acceptés pour l'authentification et la
    /// révélation de secrets.
    /// </summary>
    public bool AllowInsecureHttp { get; set; }
}

/// <summary>
/// Distribution du client par ClickOnce, hébergée par ce même serveur. Permet aux techniciens
/// d'installer le client depuis une page web, sans partage de fichiers ni déploiement manuel.
/// </summary>
public sealed class DistributionOptions
{
    /// <summary>
    /// Dossier contenant la publication ClickOnce du client : page « index.html », manifeste
    /// « KyWigRemote.Client.application » et dossier « Application Files ». Laissé vide,
    /// l'hébergement est désactivé. Les variables d'environnement %VAR% sont développées.
    /// </summary>
    public string? WebRoot { get; set; }

    /// <summary>Chemin web où la page d'installation est servie (par défaut « /install »).</summary>
    public string RequestPath { get; set; } = "/install";

    /// <summary>
    /// Dossier de distribution résolu (variables d'environnement développées, chemin relatif
    /// rendu absolu par rapport au dossier de l'exécutable), ou <c>null</c> si désactivé.
    /// Le relatif permet à l'installeur de placer « web » à côté du serveur sans connaître
    /// le dossier d'installation à l'avance (ex. « ..\web »).
    /// </summary>
    public string? ResolveWebRoot()
    {
        if (string.IsNullOrWhiteSpace(WebRoot))
        {
            return null;
        }
        string expanded = Environment.ExpandEnvironmentVariables(WebRoot);
        return Path.IsPathRooted(expanded)
            ? expanded
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
    }
}

/// <summary>Moteur de base de données supporté. SQLite est le seul moteur retenu (SQL Server abandonné).</summary>
public enum DatabaseProvider
{
    /// <summary>SQLite — moteur unique de KyWigRemote (serveur unique, quelques utilisateurs).</summary>
    Sqlite,
}

/// <summary>Choix et paramètres de la base.</summary>
public sealed class DatabaseOptions
{
    /// <summary>Moteur retenu (SQLite uniquement).</summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    /// <summary>Chemin du fichier SQLite (les variables d'environnement %VAR% sont développées).</summary>
    public string SqlitePath { get; set; } = @"%LOCALAPPDATA%\KyWigRemote\kywigremote.db";

    /// <summary>Chemin SQLite avec les variables d'environnement résolues.</summary>
    public string ResolveSqlitePath() => Environment.ExpandEnvironmentVariables(SqlitePath);
}

/// <summary>Fournisseurs d'authentification activés et leurs paramètres.</summary>
public sealed class AuthenticationOptions
{
    /// <summary>Noms de fournisseurs reconnus.</summary>
    public static readonly string[] KnownProviders = { "Local", "ActiveDirectory", "Microsoft365" };

    /// <summary>
    /// Fournisseurs activés, dans l'ordre de préférence. Volontairement vide par défaut :
    /// le binding de configuration AJOUTE à la liste existante au lieu de la remplacer, donc
    /// une valeur par défaut créerait des doublons. La validation impose d'en déclarer au moins un.
    /// </summary>
    public List<string> Providers { get; set; } = new();

    public ActiveDirectoryOptions ActiveDirectory { get; set; } = new();
    public Microsoft365Options Microsoft365 { get; set; } = new();
    public LocalAccountsOptions Local { get; set; } = new();

    /// <summary>Paramètres des jetons émis à la connexion.</summary>
    public JwtOptions Jwt { get; set; } = new();

    /// <summary>Indique si un fournisseur est activé (insensible à la casse).</summary>
    public bool IsEnabled(string provider) =>
        Providers.Contains(provider, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Paramètres Active Directory. Les trois groupes correspondent aux trois profils.</summary>
public sealed class ActiveDirectoryOptions
{
    public string Domain { get; set; } = string.Empty;

    /// <summary>Groupe des utilisateurs (profil « Utilisateur »).</summary>
    public string UserGroup { get; set; } = "GG_KyWigRemote_Users";

    /// <summary>Groupe des administrateurs des connexions (profil « Administrateur des connexions »).</summary>
    public string ConnectionAdminGroup { get; set; } = "GG_KyWigRemote_ConnectionAdmins";

    /// <summary>Groupe des administrateurs globaux (profil « Administrateur global »).</summary>
    public string AdminGroup { get; set; } = "GG_KyWigRemote_Admins";

    /// <summary>Planification de la synchronisation des utilisateurs AD.</summary>
    public AdSyncOptions Sync { get; set; } = new();
}

/// <summary>Planification de la synchronisation des utilisateurs AD (import périodique).</summary>
public sealed class AdSyncOptions
{
    /// <summary>Active la synchronisation automatique planifiée (la synchro manuelle reste possible).</summary>
    public bool Enabled { get; set; }

    /// <summary>Intervalle entre deux synchronisations, en heures (12 par défaut).</summary>
    public int IntervalHours { get; set; } = 12;
}

/// <summary>Paramètres Microsoft 365 / Entra ID.</summary>
public sealed class Microsoft365Options
{
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
}

/// <summary>Paramètres des comptes locaux applicatifs.</summary>
public sealed class LocalAccountsOptions
{
    public bool Enabled { get; set; } = true;
}

/// <summary>Paramètres des jetons JWT émis à la connexion.</summary>
public sealed class JwtOptions
{
    /// <summary>Émetteur attendu du jeton.</summary>
    public string Issuer { get; set; } = "KyWigRemote";

    /// <summary>Audience attendue du jeton.</summary>
    public string Audience { get; set; } = "KyWigRemote";

    /// <summary>Durée de validité du jeton, en minutes.</summary>
    public int LifetimeMinutes { get; set; } = 480;

    /// <summary>
    /// Clé de signature (symétrique). Laissée vide, le serveur en génère une aléatoire
    /// au premier démarrage et la persiste en base — ainsi aucun secret n'est codé en dur
    /// ni placé dans un fichier versionné (règles de sécurité nº 5 et 6).
    /// </summary>
    public string? SigningKey { get; set; }
}
