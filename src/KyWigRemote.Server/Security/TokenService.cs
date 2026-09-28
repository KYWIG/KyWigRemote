using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using KyWigRemote.Core.Security;
using KyWigRemote.Server.Configuration;

namespace KyWigRemote.Server.Security;

/// <summary>
/// Émet les jetons JWT signés remis aux clients après une authentification réussie.
/// La clé de signature est fournie au constructeur (résolue au démarrage : configurée
/// ou générée et persistée), jamais codée en dur.
/// </summary>
public sealed class TokenService
{
    /// <summary>Nom de rôle « Utilisateur » dans le jeton.</summary>
    public const string RoleUser = "User";

    /// <summary>Nom de rôle « Administrateur des connexions » dans le jeton.</summary>
    public const string RoleConnectionAdmin = "ConnectionAdmin";

    /// <summary>Nom de rôle « Administrateur global » dans le jeton.</summary>
    public const string RoleGlobalAdmin = "GlobalAdmin";

    private readonly SigningCredentials _credentials;
    private readonly JwtOptions _options;

    public TokenService(byte[] signingKey, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(signingKey), SecurityAlgorithms.HmacSha256);
    }

    /// <summary>Produit un jeton signé pour l'utilisateur authentifié, et sa date d'expiration.</summary>
    public (string Token, DateTimeOffset ExpiresAt) Issue(AuthenticatedUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset expires = now.AddMinutes(_options.LifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Username),
            new(ClaimTypes.Name, user.Username),
            new("provider", user.Provider),
        };
        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            claims.Add(new Claim("display_name", user.DisplayName));
        }

        // Claims de rôle cumulatifs : un profil supérieur porte aussi les rôles inférieurs,
        // pour que RequireRole("ConnectionAdmin") soit satisfait par un GlobalAdmin.
        claims.Add(new Claim(ClaimTypes.Role, RoleUser));
        if (user.Role >= Core.Model.UserRole.ConnectionAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, RoleConnectionAdmin));
        }
        if (user.Role == Core.Model.UserRole.GlobalAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, RoleGlobalAdmin));
        }
        // Rôle exact pour l'affichage côté client.
        claims.Add(new Claim("role", user.Role.ToString()));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: _credentials);

        string encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return (encoded, expires);
    }
}
