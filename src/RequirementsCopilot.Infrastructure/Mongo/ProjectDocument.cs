using MongoDB.Bson.Serialization.Attributes;
using RequirementsCopilot.Application.Projects;

namespace RequirementsCopilot.Infrastructure.Mongo;

/// <summary>Documento Mongo de un proyecto; el nombre normalizado (minúsculas) es el _id.</summary>
[BsonIgnoreExtraElements]
public sealed class ProjectDocument
{
    /// <summary>Clave: nombre en minúsculas.</summary>
    [BsonId]
    public string Id { get; set; } = string.Empty;

    /// <summary>Nombre tal como lo escribió el usuario.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Contenido Markdown.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Última actualización (UTC).</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Clave normalizada para un nombre.</summary>
    public static string KeyFor(string name) => name.Trim().ToLowerInvariant();

    /// <summary>Mapea desde el modelo de Application.</summary>
    public static ProjectDocument FromProject(Project project) => new()
    {
        Id = KeyFor(project.Name), Name = project.Name, Content = project.Content, UpdatedAt = project.UpdatedAt,
    };

    /// <summary>Mapea al modelo de Application.</summary>
    public Project ToProject() => new(Name, Content, UpdatedAt);
}
