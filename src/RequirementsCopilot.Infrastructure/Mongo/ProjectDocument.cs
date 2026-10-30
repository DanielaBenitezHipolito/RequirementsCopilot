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

    /// <summary>Documentos de ejemplo (solo lectura).</summary>
    public List<ProjectExampleDocument> Examples { get; set; } = new();

    /// <summary>Clave normalizada para un nombre.</summary>
    public static string KeyFor(string name) => name.Trim().ToLowerInvariant();

    /// <summary>Mapea desde el modelo de Application.</summary>
    public static ProjectDocument FromProject(Project project) => new()
    {
        Id = KeyFor(project.Name), Name = project.Name, Content = project.Content, UpdatedAt = project.UpdatedAt,
        Examples = project.Examples
            .Select(e => new ProjectExampleDocument { Name = e.Name, Content = e.Content }).ToList(),
    };

    /// <summary>Mapea al modelo de Application.</summary>
    public Project ToProject() => new Project(Name, Content, UpdatedAt)
    {
        Examples = Examples.Select(e => new ProjectExample(e.Name, e.Content)).ToArray(),
    };
}

/// <summary>Documento Mongo de un ejemplo de proyecto.</summary>
[BsonIgnoreExtraElements]
public sealed class ProjectExampleDocument
{
    /// <summary>Nombre del archivo original.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Texto extraído.</summary>
    public string Content { get; set; } = string.Empty;
}
