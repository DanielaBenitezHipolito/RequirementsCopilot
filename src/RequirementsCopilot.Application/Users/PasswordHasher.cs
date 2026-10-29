using System.Security.Cryptography;

namespace RequirementsCopilot.Application.Users;

/// <summary>
/// Hash de contraseñas con PBKDF2 (SHA-256, sal aleatoria por usuario). Formato almacenado:
/// <c>{iteraciones}.{sal base64}.{hash base64}</c> — autocontenible para verificar y poder subir
/// las iteraciones a futuro sin migrar datos.
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    /// <summary>Hashea la contraseña para almacenarla.</summary>
    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>Verifica la contraseña contra el hash almacenado (comparación en tiempo constante).</summary>
    public static bool Verify(string password, string stored)
    {
        string[] parts = stored.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out int iterations))
        {
            return false;
        }
        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
