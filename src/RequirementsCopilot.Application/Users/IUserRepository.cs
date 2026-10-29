namespace RequirementsCopilot.Application.Users;

/// <summary>Puerto de persistencia de cuentas de usuario (email como clave, sin distinguir mayúsculas).</summary>
public interface IUserRepository
{
    /// <summary>Guarda o reemplaza la cuenta (upsert por email).</summary>
    Task SaveAsync(UserAccount user, CancellationToken cancellationToken = default);

    /// <summary>Cuenta por email, o null si no existe.</summary>
    Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Todas las cuentas, ordenadas por email.</summary>
    Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Elimina la cuenta; false si no existe.</summary>
    Task<bool> DeleteAsync(string email, CancellationToken cancellationToken = default);
}
