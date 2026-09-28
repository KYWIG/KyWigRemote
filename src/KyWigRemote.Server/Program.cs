using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using KyWigRemote.Core.Data;
using KyWigRemote.Core.Directory;
using KyWigRemote.Core.Model;
using KyWigRemote.Core.Remote;
using KyWigRemote.Core.Security;
using KyWigRemote.Server.Configuration;
using KyWigRemote.Server.Contracts;
using KyWigRemote.Server.Security;
using Serilog;

// Serveur KyWigRemote (architecture client/serveur).
// Tout le comportement (adresse d'écoute, base, authentification) vient de la
// configuration (appsettings.json, section « KyWigRemote ») — rien n'est codé en dur.

// Journal Serilog : console + fichier tournant quotidien sous logs\. Aucun secret n'y est
// écrit (règle 2 de CLAUDE.md) : on ne journalise que méthodes, chemins, identités et libellés.
string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(logDirectory, "kywig-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    Log.Information("Démarrage du serveur KyWigRemote.");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Host.UseSerilog();

// Permet l'exécution en service Windows (sans session ouverte). Sans effet en console/dev.
builder.Host.UseWindowsService();

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
var auditRepository = new SqliteAuditRepository(database);
var userRepository = new SqliteUserRepository(database);
var passwordHasher = new PasswordHasher();
var localAuthenticator = new LocalAuthenticator(localAccounts, passwordHasher);

// Clé de signature des jetons : configurée, sinon générée et persistée (jamais en dur).
byte[] signingKey = ResolveSigningKey(options.Authentication.Jwt, settingsStore);
var tokenService = new TokenService(signingKey, options.Authentication.Jwt);

// Chiffrement des identifiants imposés : clé maître générée et persistée si absente.
byte[] credentialKey = ResolveCredentialMasterKey(settingsStore);
var credentialRepository = new SqliteCredentialRepository(database);
var personalRepository = new SqlitePersonalCredentialRepository(database);
var credentialService = new CredentialService(
    credentialRepository, personalRepository, new CredentialProtector(), credentialKey);

builder.Services.AddSingleton<IConnectionRepository>(connectionRepository);
builder.Services.AddSingleton<ILocalAccountRepository>(localAccounts);
builder.Services.AddSingleton<ICredentialRepository>(credentialRepository);
builder.Services.AddSingleton<IPersonalCredentialRepository>(personalRepository);
builder.Services.AddSingleton<IAuditRepository>(auditRepository);
builder.Services.AddSingleton<IUserRepository>(userRepository);
builder.Services.AddSingleton(passwordHasher);
builder.Services.AddSingleton(localAuthenticator);
builder.Services.AddSingleton(tokenService);
builder.Services.AddSingleton(credentialService);

// Vérificateur de groupes AD (avec cache) si l'AD est activé ; sinon « tout refuser ».
IGroupChecker groupChecker = options.Authentication.IsEnabled("ActiveDirectory")
    ? new CachingGroupChecker(new AdGroupChecker(options.Authentication.ActiveDirectory.Domain))
    : new DenyAllGroupChecker();
builder.Services.AddSingleton(groupChecker);

// Authentification par jeton JWT (schéma par défaut pour toute l'API).
AuthenticationBuilder authentication = builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

// Authentification Windows/AD (Negotiate), uniquement pour l'endpoint de login AD.
if (options.Authentication.IsEnabled("ActiveDirectory"))
{
    authentication.AddNegotiate();
}

builder.Services.AddAuthorization(auth =>
{
    // Trois profils. Les claims de rôle étant cumulatifs (voir TokenService), la politique
    // ConnectionAdmin est aussi satisfaite par un GlobalAdmin.
    auth.AddPolicy("GlobalAdmin", policy => policy.RequireRole(TokenService.RoleGlobalAdmin));
    auth.AddPolicy("ConnectionAdmin", policy => policy.RequireRole(TokenService.RoleConnectionAdmin));
});

WebApplication app = builder.Build();

// Journalise chaque requête (méthode, chemin, code, durée) — sans corps ni secret.
app.UseSerilogRequestLogging();

// Distribution ClickOnce du client (page d'installation publique), si un dossier est configuré.
// Servie avant l'authentification : l'installeur doit être accessible sans jeton.
ConfigureClickOnceHosting(app, options.Distribution);

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
        Role = UserRole.GlobalAdmin,
        PasswordHash = hasher.Hash(request.Password),
    };
    accounts.CreateAccount(account);
    return Results.Created($"/api/auth/local-accounts/{account.Id}", new { account.Username, account.Role });
});

// Connexion par compte local : renvoie un jeton signé + l'identité.
app.MapPost("/api/auth/login", (LoginRequest request, LocalAuthenticator local, TokenService tokens, IAuditRepository audit) =>
{
    AuthenticatedUser? user = local.Authenticate(request.Username, request.Password);
    if (user is null)
    {
        WriteAudit(audit, request.Username, "LOGIN", result: "DENIED");
        return Results.Json(new { error = "Identifiant ou mot de passe incorrect." },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    WriteAudit(audit, user.Username, "LOGIN", result: "OK");
    (string token, DateTimeOffset expiresAt) = tokens.Issue(user);
    return Results.Ok(new
    {
        token,
        expiresAt,
        user.Username,
        user.DisplayName,
        role = user.Role,
        user.Provider,
    });
});

// Connexion Active Directory (Windows/Negotiate) : l'utilisateur s'authentifie avec son compte
// AD, l'appartenance au groupe utilisateurs est requise, l'admin découle du groupe admin
// (E3.3/E3.4). L'utilisateur est enregistré (E3.5) et un jeton est renvoyé comme pour le login local.
if (options.Authentication.IsEnabled("ActiveDirectory"))
{
    AuthorizationPolicy negotiatePolicy = new AuthorizationPolicyBuilder(NegotiateDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();

    app.MapPost("/api/auth/windows-login",
        (ClaimsPrincipal windows, KyWigRemoteOptions cfg, IGroupChecker groups,
         IUserRepository users, TokenService tokens, IAuditRepository audit) =>
    {
        string fullName = windows.Identity?.Name ?? string.Empty; // « DOMAINE\utilisateur »
        int slash = fullName.IndexOf('\\');
        string sam = slash >= 0 ? fullName[(slash + 1)..] : fullName;
        string? domain = slash >= 0 ? fullName[..slash] : null;
        string sid = windows.FindFirst(ClaimTypes.PrimarySid)?.Value ?? string.Empty;

        ActiveDirectoryOptions ad = cfg.Authentication.ActiveDirectory;
        bool authorized;
        UserRole role;
        try
        {
            // Le profil découle du groupe le plus privilégié auquel l'utilisateur appartient.
            bool inAdmin = groups.IsMemberOf(sam, ad.AdminGroup);
            bool inConnAdmin = groups.IsMemberOf(sam, ad.ConnectionAdminGroup);
            bool inUser = groups.IsMemberOf(sam, ad.UserGroup);
            authorized = inAdmin || inConnAdmin || inUser;
            role = inAdmin ? UserRole.GlobalAdmin
                 : inConnAdmin ? UserRole.ConnectionAdmin
                 : UserRole.User;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            WriteAudit(audit, sam, "LOGIN", result: "ERROR", details: "vérification des groupes impossible");
            return Results.Problem("Vérification des droits impossible (annuaire injoignable).",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!authorized)
        {
            WriteAudit(audit, sam, "LOGIN", result: "DENIED", details: "hors des groupes KyWigRemote");
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        string? display = AdIdentity.TryResolveDisplayName(sam);
        users.Upsert(new WindowsUser(sid, sam, display, domain)); // E3.5
        WriteAudit(audit, sam, "LOGIN", result: "OK");

        var identity = new AuthenticatedUser(sam, display, role, "ActiveDirectory");
        (string token, DateTimeOffset expiresAt) = tokens.Issue(identity);
        return Results.Ok(new { token, expiresAt, username = sam, displayName = display, role, provider = "ActiveDirectory" });
    }).RequireAuthorization(negotiatePolicy);
}

// Arborescence partagée : désormais PROTÉGÉE (jeton requis).
app.MapGet("/api/tree", (IConnectionRepository connections) => Results.Ok(connections.GetTree()))
    .RequireAuthorization();

// Identifiants personnels : chaque utilisateur gère les siens (propriétaire = jeton).
app.MapGet("/api/connections/{id:int}/personal-credential",
    (int id, ClaimsPrincipal user, CredentialService credentials) =>
{
    string owner = user.Identity?.Name ?? string.Empty;
    RevealedLogin? login = credentials.RevealPersonalCredential(owner, id);
    return login is null
        ? Results.NotFound()
        : Results.Ok(new RevealedCredential(login.Username, login.Domain, login.Secret));
}).RequireAuthorization();

app.MapPost("/api/connections/{id:int}/personal-credential",
    (int id, SavePersonalCredentialRequest request, ClaimsPrincipal user, CredentialService credentials) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Utilisateur et mot de passe sont requis." });
    }
    string owner = user.Identity?.Name ?? string.Empty;
    int? connectionId = request.Global ? null : id;
    credentials.SavePersonalCredential(owner, connectionId, request.Username, request.Domain, request.Password);
    return Results.NoContent();
}).RequireAuthorization();

// L'utilisateur consulte et supprime SES propres identifiants personnels (FR-16).
app.MapGet("/api/personal-credentials",
    (ClaimsPrincipal user, IPersonalCredentialRepository personal) =>
{
    string owner = user.Identity?.Name ?? string.Empty;
    return Results.Ok(personal.ListByOwner(owner));
}).RequireAuthorization();

app.MapDelete("/api/personal-credentials/{id:int}",
    (int id, ClaimsPrincipal user, IPersonalCredentialRepository personal) =>
{
    string owner = user.Identity?.Name ?? string.Empty;
    return personal.Delete(owner, id) ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

// Compte-rendu d'ouverture de session (le client signale l'ouverture d'un onglet) — FR-42.
app.MapPost("/api/audit/session-open",
    (SessionOpenReport report, ClaimsPrincipal user, IAuditRepository audit) =>
{
    WriteAudit(audit, user.Identity?.Name, "SESSION_OPEN", "CONNECTION", report.ConnectionId,
        mode: report.CredentialMode, result: report.Result);
    return Results.NoContent();
}).RequireAuthorization();

// --- Administration ---
// Deux périmètres : la gestion des connexions (Administrateur des connexions ou global) et
// la gestion globale (comptes, audit, AD — Administrateur global uniquement).
RouteGroupBuilder connAdmin = app.MapGroup("/api/admin").RequireAuthorization("ConnectionAdmin");
RouteGroupBuilder globalAdmin = app.MapGroup("/api/admin").RequireAuthorization("GlobalAdmin");

globalAdmin.MapGet("/local-accounts", (ILocalAccountRepository accounts) =>
{
    IEnumerable<LocalAccountSummary> list = accounts.ListAccounts()
        .Select(a => new LocalAccountSummary(a.Id, a.Username, a.DisplayName, a.Role, a.Disabled));
    return Results.Ok(list);
});

globalAdmin.MapPost("/local-accounts",
    (CreateLocalAccountRequest request, ClaimsPrincipal user, ILocalAccountRepository accounts, PasswordHasher hasher, IAuditRepository audit) =>
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
        Role = request.Role,
        PasswordHash = hasher.Hash(request.Password),
    };
    accounts.CreateAccount(account);
    WriteAudit(audit, user.Identity?.Name, "ACCOUNT_CREATE", "ACCOUNT", account.Id, details: $"{request.Username} ({request.Role})");
    return Results.Created($"/api/admin/local-accounts/{account.Id}",
        new LocalAccountSummary(account.Id, account.Username, account.DisplayName, account.Role, account.Disabled));
});

// Gestion de l'arborescence (création / suppression de dossiers et connexions).
connAdmin.MapPost("/folders",
    (CreateFolderRequest request, ClaimsPrincipal user, IConnectionRepository connections, IAuditRepository audit) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Le nom du dossier est requis." });
    }
    int id = connections.AddFolder(
        new ConnectionFolder { Name = request.Name, CredentialMode = request.CredentialMode },
        request.ParentId);
    WriteAudit(audit, user.Identity?.Name, "FOLDER_CREATE", "FOLDER", id, details: request.Name);
    return Results.Created($"/api/admin/folders/{id}", new CreatedId(id));
});

connAdmin.MapPost("/connections",
    (CreateConnectionRequest request, ClaimsPrincipal user, IConnectionRepository connections, IAuditRepository audit) =>
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
        EnforcedCredentialId = request.EnforcedCredentialId,
    }, request.FolderId);
    WriteAudit(audit, user.Identity?.Name, "CONN_CREATE", "CONNECTION", id, details: request.Name);
    return Results.Created($"/api/admin/connections/{id}", new CreatedId(id));
});

connAdmin.MapDelete("/folders/{id:int}",
    (int id, ClaimsPrincipal user, IConnectionRepository connections, IAuditRepository audit) =>
{
    connections.DeleteFolder(id);
    WriteAudit(audit, user.Identity?.Name, "FOLDER_DELETE", "FOLDER", id);
    return Results.NoContent();
});

connAdmin.MapDelete("/connections/{id:int}",
    (int id, ClaimsPrincipal user, IConnectionRepository connections, IAuditRepository audit) =>
{
    connections.DeleteConnection(id);
    WriteAudit(audit, user.Identity?.Name, "CONN_DELETE", "CONNECTION", id);
    return Results.NoContent();
});

// Identifiants imposés (chiffrés au repos). Le secret n'est jamais renvoyé par ces endpoints.
connAdmin.MapGet("/enforced-credentials", (ICredentialRepository credentials) =>
{
    IEnumerable<EnforcedCredentialSummary> list = credentials.ListEnforced()
        .Select(c => new EnforcedCredentialSummary(c.Id, c.Label, c.Username, c.Domain, c.AllowedGroups));
    return Results.Ok(list);
});

connAdmin.MapPost("/enforced-credentials",
    (CreateEnforcedCredentialRequest request, ClaimsPrincipal user, CredentialService credentials, IAuditRepository audit) =>
{
    if (string.IsNullOrWhiteSpace(request.Label)
        || string.IsNullOrWhiteSpace(request.Username)
        || string.IsNullOrWhiteSpace(request.Secret))
    {
        return Results.BadRequest(new { error = "Libellé, utilisateur et secret sont requis." });
    }

    int id = credentials.SaveEnforcedCredential(
        new EnforcedCredential
        {
            Label = request.Label,
            Username = request.Username,
            Domain = request.Domain,
            AllowedGroups = request.AllowedGroups,
        },
        request.Secret);
    WriteAudit(audit, user.Identity?.Name, "ENFORCED_CREATE", "CREDENTIAL", id, details: request.Label);
    return Results.Created($"/api/admin/enforced-credentials/{id}",
        new EnforcedCredentialSummary(id, request.Label, request.Username, request.Domain, request.AllowedGroups));
});

// Révélation du secret imposé rattaché à une connexion, pour ouvrir la session.
// INTERIM : réservé aux administrateurs (groupe admin). Le contrôle par groupes AD
// pour les techniciens viendra avec E3 (authentification Active Directory).
// Révélation du secret imposé pour ouvrir une session. Autorisée aux administrateurs OU
// aux membres d'un groupe AD autorisé sur l'identifiant (FR-15). Accès et refus sont tracés.
app.MapGet("/api/connections/{id:int}/enforced-secret",
    (int id, ClaimsPrincipal user, IConnectionRepository connections, ICredentialRepository credentials,
     CredentialService credentialService, IGroupChecker groupChecker, IAuditRepository audit) =>
{
    RemoteConnection? connection = connections.GetConnection(id);
    if (connection is null)
    {
        return Results.NotFound(new { error = "Connexion introuvable." });
    }
    if (connection.EnforcedCredentialId is not int credentialId)
    {
        return Results.BadRequest(new { error = "Cette connexion n'a pas d'identifiant imposé rattaché." });
    }
    EnforcedCredential? meta = credentials.GetEnforced(credentialId);
    if (meta is null)
    {
        return Results.NotFound(new { error = "Identifiant imposé introuvable." });
    }

    string caller = user.Identity?.Name ?? string.Empty;
    bool authorized;
    try
    {
        authorized = user.IsInRole(TokenService.RoleGlobalAdmin)
            || EnforcedCredentialAuthorizer.IsAllowedByGroups(caller, meta.AllowedGroups, groupChecker);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        // Annuaire injoignable : on refuse par sécurité et on trace, sans exposer le détail.
        WriteAudit(audit, caller, "REVEAL_ENFORCED", "CREDENTIAL", credentialId, result: "ERROR",
            details: "vérification des groupes impossible");
        return Results.Problem("Vérification des droits impossible (annuaire injoignable).",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (!authorized)
    {
        WriteAudit(audit, caller, "REVEAL_ENFORCED", "CREDENTIAL", credentialId, result: "DENIED",
            details: $"connexion #{id}");
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    string? secret = credentialService.RevealEnforcedSecret(credentialId);
    if (secret is null)
    {
        return Results.NotFound(new { error = "Identifiant imposé introuvable." });
    }

    WriteAudit(audit, caller, "REVEAL_ENFORCED", "CREDENTIAL", credentialId, result: "OK", details: $"connexion #{id}");
    return Results.Ok(new RevealedCredential(meta.Username, meta.Domain, secret));
}).RequireAuthorization();

// Consultation du journal d'audit (filtres facultatifs : utilisateur, résultat).
globalAdmin.MapGet("/audit", (string? user, string? result, int? limit, IAuditRepository audit) =>
{
    IReadOnlyList<AuditEvent> events = audit.Query(new AuditQuery
    {
        UserName = user,
        Result = result,
        Limit = limit is > 0 and <= 2000 ? limit.Value : 500,
    });
    IEnumerable<AuditEventSummary> summaries = events.Select(e => new AuditEventSummary(
        e.Id, e.OccurredAt, e.UserName, e.Action, e.TargetType, e.TargetId, e.CredentialMode, e.Result, e.Details));
    return Results.Ok(summaries);
});

// Purge du journal d'audit selon une rétention en jours (E9.5). La purge est elle-même tracée.
globalAdmin.MapPost("/audit/purge",
    (PurgeAuditRequest request, ClaimsPrincipal user, IAuditRepository audit) =>
{
    if (request.RetentionDays < 1)
    {
        return Results.BadRequest(new { error = "La rétention doit être d'au moins 1 jour." });
    }
    DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-request.RetentionDays);
    int deleted = audit.PurgeOlderThan(cutoff);
    WriteAudit(audit, user.Identity?.Name, "AUDIT_PURGE", result: "OK",
        details: $"{deleted} événement(s) antérieur(s) à {request.RetentionDays} jour(s)");
    return Results.Ok(new PurgeAuditResult(deleted));
});

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Arrêt inattendu du serveur KyWigRemote au démarrage.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// --- Hébergement de la distribution ClickOnce du client ---
// Sert un dossier (page d'installation + manifestes + fichiers) avec les types MIME que
// ClickOnce exige (.application, .manifest, .deploy). Sans dossier configuré : aucun effet.
static void ConfigureClickOnceHosting(WebApplication app, DistributionOptions distribution)
{
    string? webRoot = distribution.ResolveWebRoot();
    if (webRoot is null || !Directory.Exists(webRoot))
    {
        if (webRoot is not null)
        {
            Log.Warning("Distribution ClickOnce désactivée : dossier introuvable ({WebRoot}).", webRoot);
        }
        return;
    }

    var contentTypes = new FileExtensionContentTypeProvider();
    contentTypes.Mappings[".application"] = "application/x-ms-application";
    contentTypes.Mappings[".manifest"] = "application/x-ms-manifest";
    contentTypes.Mappings[".deploy"] = "application/octet-stream";
    contentTypes.Mappings[".msi"] = "application/octet-stream";

    var fileProvider = new PhysicalFileProvider(webRoot);
    string requestPath = distribution.RequestPath.TrimEnd('/');

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider,
        RequestPath = requestPath,
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        RequestPath = requestPath,
        ContentTypeProvider = contentTypes,
        // Autorise les extensions inconnues (ex. « .deploy ») avec un type par défaut binaire.
        ServeUnknownFileTypes = true,
        DefaultContentType = "application/octet-stream",
    });

    // Confort : la racine renvoie vers la page d'installation (évite un 404 déroutant).
    app.MapGet("/", () => Results.Redirect($"{requestPath}/"));

    Log.Information("Distribution ClickOnce servie sur {RequestPath} depuis {WebRoot}.", requestPath, webRoot);
}

// --- Ouverture de la base selon la configuration ---
static Database OpenDatabase(DatabaseOptions databaseOptions)
{
    // SQLite est le seul moteur supporté (SQL Server abandonné).
    var database = new Database(databaseOptions.ResolveSqlitePath());
    database.Initialize();
    return database;
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

// --- Écriture d'un événement d'audit ---
// Ne journalise que des identifiants et libellés, jamais un secret (FR-44).
static void WriteAudit(IAuditRepository audit, string? userName, string action,
    string? targetType = null, int? targetId = null, string? mode = null,
    string result = "OK", string? details = null)
{
    audit.Append(new AuditEvent
    {
        OccurredAt = DateTimeOffset.UtcNow,
        UserName = userName,
        Action = action,
        TargetType = targetType,
        TargetId = targetId,
        CredentialMode = mode,
        Result = result,
        Details = details,
    });
}

// --- Résolution de la clé maître de chiffrement des identifiants ---
// Générée aléatoirement au premier démarrage puis persistée en base (jamais dans le code
// ni un fichier versionné). La perdre rend les secrets stockés indéchiffrables.
static byte[] ResolveCredentialMasterKey(ISettingsStore settings)
{
    const string settingKey = "Credential:MasterKey";
    string? stored = settings.Get(settingKey);
    if (string.IsNullOrEmpty(stored))
    {
        stored = Convert.ToBase64String(RandomNumberGenerator.GetBytes(CredentialProtector.KeySize));
        settings.Set(settingKey, stored);
    }
    return Convert.FromBase64String(stored);
}
