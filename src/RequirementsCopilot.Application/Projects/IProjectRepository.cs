namespace RequirementsCopilot.Application.Projects;

/// <summary>Puerto de persistencia de proyectos (contexto .md). El nombre es la clave.</summary>
public interface IProjectRepository
{
    /// <summary>Crea o reemplaza el proyecto con ese nombre.</summary>
    Task SaveAsync(Project project, CancellationToken cancellationToken = default);

    /// <summary>Busca por nombre (sin distinguir mayúsculas).</summary>
    Task<Project?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Todos los proyectos ordenados por nombre.</summary>
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Elimina el proyecto; devuelve false si no existía.</summary>
    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default);
}
