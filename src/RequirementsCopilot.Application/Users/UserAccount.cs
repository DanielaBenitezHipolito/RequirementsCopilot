namespace RequirementsCopilot.Application.Users;

/// <summary>Cuenta de usuario de la aplicación, con rol y hash de contraseña (nunca en claro).</summary>
public sealed record UserAccount(string Email, string Name, string Role, string PasswordHash, DateTime CreatedAt)
{
    /// <summary>Rol con administración de usuarios y acceso al tablero de control.</summary>
    public const string AdminRole = "Admin";

    /// <summary>Rol operativo estándar (análisis, chat, proyectos).</summary>
    public const string AnalystRole = "Analista";

    /// <summary>Crea la cuenta validando email, nombre y rol.</summary>
    public static UserAccount Create(string email, string name, string role, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new ArgumentException("El usuario requiere un email válido.", nameof(email));
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El usuario requiere nombre.", nameof(name));
        }
        if (role is not (AdminRole or AnalystRole))
        {
            throw new ArgumentException($"Rol inválido: «{role}». Use {AdminRole} o {AnalystRole}.", nameof(role));
        }
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("El usuario requiere contraseña.", nameof(passwordHash));
        }
        return new UserAccount(email.Trim().ToLowerInvariant(), name.Trim(), role, passwordHash, DateTime.UtcNow);
    }
}
