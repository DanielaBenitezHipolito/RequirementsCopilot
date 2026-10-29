namespace RequirementsCopilot.Api.Auth;

/// <summary>
/// Configuración de autenticación (sección <c>Auth</c>). La autenticación se habilita al
/// configurar <see cref="JwtKey"/> (≥ 32 caracteres); sin clave la API queda abierta
/// (demo local / pruebas).
/// </summary>
public sealed class AuthOptions
{
    /// <summary>Clave simétrica para firmar y validar los JWT. Vacía = autenticación deshabilitada.</summary>
    public string JwtKey { get; set; } = string.Empty;

    /// <summary>Email del administrador inicial, sembrado al arrancar si no hay usuarios.</summary>
    public string? AdminEmail { get; set; }

    /// <summary>Contraseña del administrador inicial.</summary>
    public string? AdminPassword { get; set; }

    /// <summary>Vigencia del token en horas (default 12).</summary>
    public int TokenHours { get; set; } = 12;

    /// <summary>La autenticación está activa cuando hay una clave utilizable.</summary>
    public bool Enabled => JwtKey.Length >= 32;
}
