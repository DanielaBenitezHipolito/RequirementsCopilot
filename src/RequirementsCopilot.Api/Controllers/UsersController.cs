using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RequirementsCopilot.Application.Users;

namespace RequirementsCopilot.Api.Controllers;

/// <summary>Administración de usuarios: solo el rol Admin (ADR-0010).</summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = UserAccount.AdminRole)]
public sealed class UsersController : ControllerBase
{
    /// <summary>Alta o reemplazo de un usuario.</summary>
    public sealed record UserRequest(string? Email, string? Nombre, string? Rol, string? Password);

    private readonly IUserRepository _users;

    /// <summary>Crea el controlador sobre el repositorio de usuarios.</summary>
    public UsersController(IUserRepository users) => _users = users;

    /// <summary>Lista los usuarios (sin hashes).</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<UserAccount> users = await _users.GetAllAsync(cancellationToken);
        return Ok(users.Select(u => new { email = u.Email, nombre = u.Name, rol = u.Role, createdAt = u.CreatedAt }));
    }

    /// <summary>Crea o reemplaza un usuario (la contraseña siempre se re-hashea).</summary>
    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UserRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Password))
        {
            return BadRequest(new { mensaje = "Debe enviar la contraseña del usuario." });
        }
        UserAccount user;
        try
        {
            user = UserAccount.Create(request.Email ?? string.Empty, request.Nombre ?? string.Empty,
                request.Rol ?? UserAccount.AnalystRole, PasswordHasher.Hash(request.Password));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        await _users.SaveAsync(user, cancellationToken);
        return Ok(new { email = user.Email, nombre = user.Name, rol = user.Role });
    }

    /// <summary>Elimina un usuario (nunca a sí mismo, para no quedar sin acceso).</summary>
    [HttpDelete("{email}")]
    public async Task<IActionResult> Delete(string email, CancellationToken cancellationToken)
    {
        string? current = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.Equals(current, email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { mensaje = "No puede eliminar su propio usuario." });
        }
        return await _users.DeleteAsync(email, cancellationToken)
            ? NoContent()
            : NotFound(new { mensaje = "Usuario no encontrado." });
    }
}
