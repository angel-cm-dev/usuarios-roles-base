using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Domain.Entities;
using UsuariosRoles.Domain.Security;

namespace UsuariosRoles.Infrastructure.Security;

/// <summary>
/// Genera access tokens JWT firmados con HMAC-SHA256. Incluye roles y permisos como claims para que
/// la autorización no consulte la BD en cada petición (trade-off documentado en docs/decisiones).
/// </summary>
internal sealed class JwtAccessTokenGenerator(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenGenerator
{
    private readonly JsonWebTokenHandler handler = new();

    public AccessToken Create(User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        claims.AddRange(permissions.Select(permission => new Claim(Permissions.ClaimType, permission)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(handler.CreateToken(descriptor), expires);
    }
}
