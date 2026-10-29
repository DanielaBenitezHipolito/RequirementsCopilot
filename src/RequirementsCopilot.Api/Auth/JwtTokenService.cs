using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using RequirementsCopilot.Application.Users;

namespace RequirementsCopilot.Api.Auth;

/// <summary>Emite los JWT de la aplicación (clave simétrica de <see cref="AuthOptions"/>).</summary>
public sealed class JwtTokenService
{
    private readonly AuthOptions _options;

    /// <summary>Crea el emisor sobre la configuración de autenticación.</summary>
    public JwtTokenService(AuthOptions options) => _options = options;

    /// <summary>Token firmado con email, nombre y rol del usuario.</summary>
    public string Issue(UserAccount user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtKey));
        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Email),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Role),
            },
            expires: DateTime.UtcNow.AddHours(_options.TokenHours),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
