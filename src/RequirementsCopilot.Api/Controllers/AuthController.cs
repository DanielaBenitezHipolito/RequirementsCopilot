using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RequirementsCopilot.Api.Auth;
using RequirementsCopilot.Application.Users;

namespace RequirementsCopilot.Api.Controllers;

/// <summary>Login y estado de la autenticación (ADR-0010).</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Credenciales de inicio de sesión.</summary>
    public sealed record LoginRequest(string? Email, string? Password);

    private readonly IUserRepository _users;
    private readonly JwtTokenService _tokens;
    private readonly AuthOptions _options;

    /// <summary>Crea el controlador sobre el repositorio de usuarios y el emisor de tokens.</summary>
    public AuthController(IUserRepository users, JwtTokenService tokens, AuthOptions options)
        => (_users, _tokens, _options) = (users, tokens, options);

    /// <summary>Indica si la autenticación está habilitada (el frontend decide si pide login).</summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public IActionResult Status() => Ok(new { habilitado = _options.Enabled });

    /// <summary>Valida credenciales y devuelve el JWT con nombre y rol.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest? request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return BadRequest(new { mensaje = "La autenticación no está habilitada en este despliegue." });
        }
        if (string.IsNullOrWhiteSpace(request?.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { mensaje = "Debe enviar email y contraseña." });
        }

        UserAccount? user = await _users.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            // Mensaje único: no revelar si el email existe.
            return Unauthorized(new { mensaje = "Email o contraseña incorrectos." });
        }

        return Ok(new { token = _tokens.Issue(user), email = user.Email, nombre = user.Name, rol = user.Role });
    }
}
