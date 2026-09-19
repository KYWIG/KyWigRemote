using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KyWigRemote.Core.Model;

namespace KyWigRemote.Core.Remote;

/// <summary>
/// Client HTTP du serveur KyWigRemote. Point d'accès unique du côté client pour
/// dialoguer avec le backend (santé, authentification, lecture de l'arborescence).
/// Une fois connecté via <see cref="LoginAsync"/>, le jeton est joint à chaque requête.
/// </summary>
public sealed class ServerClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    /// <summary>Adresse de base du serveur (ex. http://serveur:5080).</summary>
    public Uri BaseAddress { get; }

    /// <summary>Utilisateur authentifié, disponible après un <see cref="LoginAsync"/> réussi.</summary>
    public LoginResult? Session { get; private set; }

    public ServerClient(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("L'adresse du serveur est vide.", nameof(baseUrl));
        }

        BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        // UseDefaultCredentials : permet l'authentification Windows/AD (Negotiate) en
        // envoyant automatiquement l'identité de la session, sans mot de passe applicatif.
        _http = new HttpClient(new HttpClientHandler { UseDefaultCredentials = true })
        {
            BaseAddress = BaseAddress,
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    /// <summary>Vérifie qu'un serveur KyWigRemote répond à cette adresse.</summary>
    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await _http.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Authentifie l'utilisateur auprès du serveur. Retourne l'identité et arme le jeton
    /// pour les requêtes suivantes, ou null si les identifiants sont refusés (401).
    /// Les erreurs réseau se propagent.
    /// </summary>
    public async Task<LoginResult?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            "/api/auth/login", new { username, password }, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();

        LoginResult? result = await response.Content.ReadFromJsonAsync<LoginResult>(JsonOptions, cancellationToken);
        if (result is not null)
        {
            Session = result;
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Token);
        }
        return result;
    }

    /// <summary>
    /// Authentifie l'utilisateur via son compte Windows/AD (Negotiate) et arme le jeton.
    /// Retourne null si le compte n'est pas autorisé (403, hors du groupe requis).
    /// Les erreurs réseau se propagent.
    /// </summary>
    public async Task<LoginResult?> WindowsLoginAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.PostAsync("/api/auth/windows-login", content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();

        LoginResult? result = await response.Content.ReadFromJsonAsync<LoginResult>(JsonOptions, cancellationToken);
        if (result is not null)
        {
            Session = result;
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Token);
        }
        return result;
    }

    /// <summary>Récupère l'arborescence partagée depuis le serveur (jeton requis).</summary>
    public async Task<IReadOnlyList<ConnectionFolder>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        List<ConnectionFolder>? roots =
            await _http.GetFromJsonAsync<List<ConnectionFolder>>("/api/tree", JsonOptions, cancellationToken);
        return roots ?? new List<ConnectionFolder>();
    }

    /// <summary>Liste les comptes locaux (réservé aux administrateurs côté serveur).</summary>
    public async Task<IReadOnlyList<LocalAccountSummary>> ListLocalAccountsAsync(CancellationToken cancellationToken = default)
    {
        List<LocalAccountSummary>? list = await _http.GetFromJsonAsync<List<LocalAccountSummary>>(
            "/api/admin/local-accounts", JsonOptions, cancellationToken);
        return list ?? new List<LocalAccountSummary>();
    }

    /// <summary>
    /// Crée un compte local. Retourne le compte créé, ou null si l'identifiant existe déjà (409).
    /// Les autres erreurs (dont 403 si non-admin) se propagent.
    /// </summary>
    public async Task<LocalAccountSummary?> CreateLocalAccountAsync(
        CreateLocalAccountRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            "/api/admin/local-accounts", request, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LocalAccountSummary>(JsonOptions, cancellationToken);
    }

    /// <summary>Crée un dossier et retourne son identifiant (réservé aux administrateurs).</summary>
    public async Task<int> CreateFolderAsync(CreateFolderRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            "/api/admin/folders", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        CreatedId? created = await response.Content.ReadFromJsonAsync<CreatedId>(JsonOptions, cancellationToken);
        return created?.Id ?? 0;
    }

    /// <summary>Crée une connexion et retourne son identifiant (réservé aux administrateurs).</summary>
    public async Task<int> CreateConnectionAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            "/api/admin/connections", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        CreatedId? created = await response.Content.ReadFromJsonAsync<CreatedId>(JsonOptions, cancellationToken);
        return created?.Id ?? 0;
    }

    /// <summary>Supprime un dossier (et son contenu en cascade).</summary>
    public async Task DeleteFolderAsync(int id, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.DeleteAsync($"/api/admin/folders/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Supprime une connexion.</summary>
    public async Task DeleteConnectionAsync(int id, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.DeleteAsync($"/api/admin/connections/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Liste les identifiants imposés (métadonnées, sans secret).</summary>
    public async Task<IReadOnlyList<EnforcedCredentialSummary>> ListEnforcedCredentialsAsync(CancellationToken cancellationToken = default)
    {
        List<EnforcedCredentialSummary>? list = await _http.GetFromJsonAsync<List<EnforcedCredentialSummary>>(
            "/api/admin/enforced-credentials", JsonOptions, cancellationToken);
        return list ?? new List<EnforcedCredentialSummary>();
    }

    /// <summary>Crée un identifiant imposé (le secret est chiffré côté serveur).</summary>
    public async Task<EnforcedCredentialSummary?> CreateEnforcedCredentialAsync(
        CreateEnforcedCredentialRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            "/api/admin/enforced-credentials", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EnforcedCredentialSummary>(JsonOptions, cancellationToken);
    }

    /// <summary>Retourne l'identifiant personnel de l'utilisateur pour cette connexion, ou null s'il n'en a pas.</summary>
    public async Task<RevealedCredential?> GetPersonalCredentialAsync(int connectionId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.GetAsync(
            $"/api/connections/{connectionId}/personal-credential", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RevealedCredential>(JsonOptions, cancellationToken);
    }

    /// <summary>Enregistre l'identifiant personnel de l'utilisateur pour cette connexion (ou globalement).</summary>
    public async Task SavePersonalCredentialAsync(
        int connectionId, SavePersonalCredentialRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            $"/api/connections/{connectionId}/personal-credential", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Liste les identifiants personnels de l'utilisateur connecté (sans secret).</summary>
    public async Task<IReadOnlyList<PersonalCredentialSummary>> ListMyPersonalCredentialsAsync(CancellationToken cancellationToken = default)
    {
        List<PersonalCredentialSummary>? list = await _http.GetFromJsonAsync<List<PersonalCredentialSummary>>(
            "/api/personal-credentials", JsonOptions, cancellationToken);
        return list ?? new List<PersonalCredentialSummary>();
    }

    /// <summary>Supprime l'un des identifiants personnels de l'utilisateur connecté.</summary>
    public async Task DeletePersonalCredentialAsync(int id, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.DeleteAsync($"/api/personal-credentials/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Signale au serveur l'ouverture d'une session (pour le journal d'audit).</summary>
    public async Task ReportSessionOpenAsync(SessionOpenReport report, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            "/api/audit/session-open", report, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Consulte le journal d'audit (réservé aux administrateurs), avec filtres facultatifs.</summary>
    public async Task<IReadOnlyList<AuditEventSummary>> ListAuditAsync(
        string? user = null, string? result = null, int limit = 500, CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"limit={limit}" };
        if (!string.IsNullOrWhiteSpace(user))
        {
            query.Add($"user={Uri.EscapeDataString(user)}");
        }
        if (!string.IsNullOrWhiteSpace(result))
        {
            query.Add($"result={Uri.EscapeDataString(result)}");
        }
        string url = "/api/admin/audit?" + string.Join("&", query);

        List<AuditEventSummary>? list = await _http.GetFromJsonAsync<List<AuditEventSummary>>(url, JsonOptions, cancellationToken);
        return list ?? new List<AuditEventSummary>();
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>Résultat d'une connexion réussie : jeton et identité de l'utilisateur.</summary>
/// <param name="Token">Jeton JWT à joindre aux requêtes suivantes.</param>
/// <param name="ExpiresAt">Date d'expiration du jeton.</param>
/// <param name="Username">Identifiant de l'utilisateur.</param>
/// <param name="DisplayName">Nom d'affichage, s'il est connu.</param>
/// <param name="IsAdmin">L'utilisateur dispose des droits d'administration.</param>
/// <param name="Provider">Fournisseur ayant validé l'identité.</param>
public sealed record LoginResult(
    string Token,
    DateTimeOffset ExpiresAt,
    string Username,
    string? DisplayName,
    bool IsAdmin,
    string Provider);
