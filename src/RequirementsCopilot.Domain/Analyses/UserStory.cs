namespace RequirementsCopilot.Domain.Analyses;

/// <summary>
/// Historia de usuario generada para un requerimiento: "Como (rol), quiero (acción), para
/// (beneficio)", con criterios de aceptación y puntos de complejidad relativa (Fibonacci).
/// </summary>
public sealed class UserStory
{
    /// <summary>Puntos permitidos (escala Fibonacci de estimación relativa).</summary>
    public static readonly int[] PuntosValidos = { 1, 2, 3, 5, 8, 13 };

    /// <summary>Título corto de la historia.</summary>
    public string Titulo { get; }

    /// <summary>Rol que ejecuta la acción.</summary>
    public string Como { get; }

    /// <summary>Acción deseada.</summary>
    public string Quiero { get; }

    /// <summary>Beneficio esperado.</summary>
    public string Para { get; }

    /// <summary>Criterios de aceptación verificables.</summary>
    public IReadOnlyList<string> CriteriosAceptacion { get; }

    /// <summary>Puntos de complejidad relativa (1, 2, 3, 5, 8 o 13). No son tiempo.</summary>
    public int Puntos { get; }

    /// <summary>Títulos de historias de las que depende.</summary>
    public IReadOnlyList<string> Dependencias { get; }

    private UserStory(string titulo, string como, string quiero, string para,
        IReadOnlyList<string> criterios, int puntos, IReadOnlyList<string> dependencias)
        => (Titulo, Como, Quiero, Para, CriteriosAceptacion, Puntos, Dependencias)
            = (titulo, como, quiero, para, criterios, puntos, dependencias);

    /// <summary>Crea una historia validando título y puntos.</summary>
    public static UserStory Create(string titulo, string? como, string? quiero, string? para,
        IEnumerable<string>? criteriosAceptacion, int puntos, IEnumerable<string>? dependencias)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            throw new ArgumentException("La historia requiere título.", nameof(titulo));
        }
        if (!PuntosValidos.Contains(puntos))
        {
            throw new ArgumentException($"Puntos inválidos: {puntos}. Use 1, 2, 3, 5, 8 o 13.", nameof(puntos));
        }
        return new UserStory(
            titulo.Trim(),
            como?.Trim() ?? string.Empty,
            quiero?.Trim() ?? string.Empty,
            para?.Trim() ?? string.Empty,
            Clean(criteriosAceptacion),
            puntos,
            Clean(dependencias));
    }

    private static IReadOnlyList<string> Clean(IEnumerable<string>? items)
        => (items ?? Array.Empty<string>())
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .ToArray();
}
