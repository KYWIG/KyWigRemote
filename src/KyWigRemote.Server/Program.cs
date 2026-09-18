using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using KyWigRemote.Core.Data;
using KyWigRemote.Core.Model;
using KyWigRemote.Core.Remote;
using KyWigRemote.Core.Security;
using KyWigRemote.Server.Configuration;
using KyWigRemote.Server.Contracts;
using KyWigRemote.Server.Security;

// Serveur KyWigRemote (architecture client/serveur).
// Tout le comportement (adresse d'écoute, base, authentification) vient de la
// configuration (appsettings.json, section « KyWigRemote ») — rien n'est codé en dur.

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Configuration lue et validée au démarrage.
var options = builder.Configuration.GetSection(KyWigRemoteOptions.SectionName).Get<KyWigRemoteOptions>()
              ?? new KyWigRemoteOptions();
options.Validate();
builder.Services.AddSingleton(options);

builder.WebHost.UseUrls(options.Server.Urls);

builder.Services.ConfigureHttpJsonOptions(json =>
{
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// --- Couche données + services d'authentification ---
Database database = OpenDatabase(options.Database);
var connectionRepository = new SqliteConnectionRepository(database);
if (database.WasCreated)
{
    DemoSeed.Populate(connectionRepository);
}

var localAccounts = new SqliteLocalAccountRepository(database);
var settingsStore = new SqliteSettingsStore(database);
var passwordHasher = new PasswordHasher();
var localAuthenticator = new LocalAuthenticator(localAccounts, passwordHasher);

// Clé de signature des jetons : configurée, sinon générée et persistée (jamais en dur).
byte[] signingKey = ResolveSigningKey(options.Authentication.Jwt, settingsStore);
var tokenService = new TokenService(signingKey, options.Authentication.Jwt);

builder.Services.AddSingleton<IConnectionRepository>(connectionRepository);
builder.Services.AddSingleton<ILocalAccountRepository>(localAccounts);
builder.Services.AddSingleton(passwordHasher);
builder.Services.AddSingleton(localAuthenticator);
builder.Services.AddSingleton(tokenService);

// Authentification par jeton JWT.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(jwt =>
    {
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Authentication.Jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Authentication.Jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization(auth =>
{
    // Politique « Admin » : réservée aux jetons portant le rôle administrateur.
    auth.AddPolicy("Admin", policy => policy.RequireRole(TokenService.AdminRole));
});

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Sonde de disponibilité (publique) : reflète la configuration effective.
app.MapGet("/health", (KyWigRemoteOptions cfg) => Results.Ok(new
{
    status = "ok",
    service = "KyWigRemote.Server",
    databaseVersion = Database.CurrentVersion,
    databaseProvider = cfg.Database.Provider.ToString(),
    authProviders = cfg.Authentication.Providers,
}));

// Amorçage du premier compte administrateur local (public tant qu'aucun compte n'existe).
app.MapPost("/api/auth/bootstrap-admin",
    (BootstrapAdminRequest request, KyWigRemoteOptions cfg, ILocalAccountRepository accounts, PasswordHasher hasher) =>
{
    if (!cfg.Authentication.IsEnabled(LocalAuthenticator.ProviderName))
    {
        return Results.BadRequest(new { error = "Le fournisseur d'authentification Local n'est pas activé." });
    }
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Identifiant et mot de passe sont requis." });
    }
    if (accounts.HasAnyAccount())
    {
        return Results.Conflict(new { error = "Un compte local existe déjà : amorçage impossible." });
    }

    var account = new LocalAccount
    {
        Username = request.Username,
        DisplayName = request.DisplayName,
        IsAdmin = true,
        PasswordHash = hasher.Hash(request.Password),
    };
    accounts.CreateAccount(account);
    return Results.Created($"/api/auth/local-accounts/{account.Id}", new { account.Username, account.IsAdmin });
});

// Connexion par compte local : renvoie un jeton signé + l'identité.
app.MapPost("/api/auth/login", (LoginRequest request, LocalAuthenticator local, TokenService tokens) =>
{
    AuthenticatedUser? user = local.Authenticate(request.Username, request.Password);
    if (user is null)
    {
        return Results.Json(new { error = "Identifiant ou mot de passe incorrect." },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    (string token, DateTimeOffset expiresAt) = tokens.Issue(user);
    return Results.Ok(new
    {
        token,
        expiresAt,
        user.Username,
        user.DisplayName,
        user.IsAdmin,
        user.Provider,
    });
});

// Arborescence partagée : désormais PROTÉGÉE (jeton requis).
app.MapGet("/api/tree", (IConnectionRepository connections) => Results.Ok(connections.GetTree()))
    .RequireAuthorization();

// --- Administration : réservée aux comptes administrateurs ---
RouteGroupBuilder admin = app.MapGroup("/api/admin").RequireAuthorization("Admin");

admin.MapGet("/local-accounts", (ILocalAccountRepository accounts) =>
{
    IEnumerable<LocalAccountSummary> list = accounts.ListAccounts()
        .Select(a => new LocalAccountSummary(a.Id, a.Username, a.DisplayName, a.IsAdmin, a.Disabled));
    return Results.Ok(list);
});

admin.MapPost("/local-accounts",
    (CreateLocalAccountRequest request, ILocalAccountRepository accounts, PasswordHasher hasher) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Identifiant et mot de passe sont requis." });
    }
    if (accounts.FindByUsername(request.Username) is not null)
    {
        return Results.Conflict(new { error = $"Le compte « {request.Username} » existe déjà." });
    }

    var account = new LocalAccount
    {
        Username = request.Username,
        DisplayName = request.DisplayName,
        IsAdmin = request.IsAdmin,
        PasswordHash = hasher.Hash(request.Password),
    };
    accounts.CreateAccount(account);
    return Results.Created($"/api/admin/local-accounts/{account.Id}",
        new LocalAccountSummary(account.Id, account.Username, account.DisplayName, account.IsAdmin, account.Disabled));
});

// Gestion de l'arborescence (création / suppression de dossiers et connexions).
admin.MapPost("/folders", (CreateFolderRequest request, IConnectionRepository connections) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Le nom du dossier est requis." });
    }
    int id = connections.AddFolder(
        new ConnectionFolder { Name = request.Name, CredentialMode = request.CredentialMode },
        request.ParentId);
    return Results.Created($"/api/admin/folders/{id}", new CreatedId(id));
});

admin.MapPost("/connections", (CreateConnectionRequest request, IConnectionRepository connections) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Host))
    {
        return Results.BadRequest(new { error = "Le nom et l'hôte de la connexion sont requis." });
    }
    int id = connections.AddConnection(new RemoteConnection
    {
        Name = request.Name,
        Protocol = request.Protocol,
        Host = request.Host,
        Port = request.Port,
        Domain = request.Domain,
        Description = request.Description,
        CredentialMode = request.CredentialMode,
    }, request.FolderId);
    return Results.Created($"/api/admin/connections/{id}", new CreatedId(id));
});

admin.MapDelete("/folders/{id:int}", (int id, IConnectionRepository connections) =>
{
    connections.DeleteFolder(id);
    return Results.NoContent();
});

admin.MapDelete("/connections/{id:int}", (int id, IConnectionRepository connections) =>
{
    connections.DeleteConnection(id);
    return Results.NoContent();
});

app.Run();

// --- Ouverture de la base selon la configuration ---
static Database OpenDatabase(DatabaseOptions databaseOptions)
{
    switch (databaseOptions.Provider)
    {
        case DatabaseProvider.Sqlite:
            var database = new Database(databaseOptions.ResolveSqlitePath());
            database.Initialize();
            return database;

        case DatabaseProvider.SqlServer:
            throw new NotSupportedException(
                "Le fournisseur SQL Server n'est pas encore implémenté. Utilisez Provider = Sqlite en attendant.");

        default:
            throw new InvalidOperationException($"Fournisseur de base inconnu : {databaseOptions.Provider}.");
    }
}

// --- Résolution de la clé de signature des jetons ---
// Priorité à la configuration ; sinon une clé aléatoire est générée puis persistée en base,
// pour rester stable entre redémarrages sans qu'aucun secret ne figure dans le code ou le dépôt.
static byte[] ResolveSigningKey(JwtOptions jwt, ISettingsStore settings)
{
    if (!string.IsNullOrEmpty(jwt.SigningKey))
    {
        return System.Text.Encoding.UTF8.GetBytes(jwt.SigningKey);
    }

    const string settingKey = "Jwt:SigningKey";
    string? stored = settings.Get(settingKey);
    if (string.IsNullOrEmpty(stored))
    {
        stored = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)); // 384 bits
        settings.Set(settingKey, stored);
    }
    return Convert.FromBase64String(stored);
}
