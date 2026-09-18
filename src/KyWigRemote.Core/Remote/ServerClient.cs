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
        _http = new HttpClient
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
