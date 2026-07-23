namespace RequirementsCopilot.Domain.Analyses;

/// <summary>Caso de uso redactado en el formato de la plantilla corporativa, con sus 12 secciones.</summary>
public sealed class UseCase
{
    /// <summary>Título del caso de uso (ej. "Módulo de Pólizas – Sistema HC Consulting").</summary>
    public string Nombre { get; }

    /// <summary>Objetivo del caso de uso.</summary>
    public string Objetivo { get; }

    /// <summary>Descripción del caso de uso.</summary>
    public string Descripcion { get; }

    /// <summary>Actores que participan en el caso de uso.</summary>
    public IReadOnlyList<Actor> Actores { get; }

    /// <summary>Precondiciones que deben cumplirse antes de ejecutar el caso de uso.</summary>
    public IReadOnlyList<string> Precondiciones { get; }

    /// <summary>Evento que dispara el caso de uso.</summary>
    public string Trigger { get; }

    /// <summary>Flujos del proceso (uno o varios), cada uno con sus pasos numerados.</summary>
    public IReadOnlyList<FlujoProceso> Flujos { get; }

    /// <summary>Extensiones: reglas o errores que alteran el flujo principal.</summary>
    public IReadOnlyList<string> Extensiones { get; }

    /// <summary>Frecuencia con la que ocurre el caso de uso.</summary>
    public string Frecuencia { get; }

    /// <summary>Importancia del caso de uso para el negocio.</summary>
    public string Importancia { get; }

    /// <summary>Urgencia de implementación del caso de uso.</summary>
    public string Urgencia { get; }

    /// <summary>Comentarios adicionales sobre el caso de uso.</summary>
    public IReadOnlyList<string> Comentarios { get; }

    private UseCase(string nombre, string objetivo, string descripcion, IReadOnlyList<Actor> actores,
        IReadOnlyList<string> precondiciones, string trigger, IReadOnlyList<FlujoProceso> flujos,
        IReadOnlyList<string> extensiones, string frecuencia, string importancia, string urgencia,
        IReadOnlyList<string> comentarios)
    {
        Nombre = nombre;
        Objetivo = objetivo;
        Descripcion = descripcion;
        Actores = actores;
        Precondiciones = precondiciones;
        Trigger = trigger;
        Flujos = flujos;
        Extensiones = extensiones;
        Frecuencia = frecuencia;
        Importancia = importancia;
        Urgencia = urgencia;
        Comentarios = comentarios;
    }

    /// <summary>Crea un caso de uso validando Nombre y Objetivo, normalizando listas nulas a vacías.</summary>
    public static UseCase Create(string nombre, string objetivo, string? descripcion, IReadOnlyList<Actor>? actores,
        IReadOnlyList<string>? precondiciones, string? trigger, IReadOnlyList<FlujoProceso>? flujos,
        IReadOnlyList<string>? extensiones, string? frecuencia, string? importancia, string? urgencia,
        IReadOnlyList<string>? comentarios)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException("El caso de uso requiere nombre.", nameof(nombre));
        }
        if (string.IsNullOrWhiteSpace(objetivo))
        {
            throw new ArgumentException("El caso de uso requiere objetivo.", nameof(objetivo));
        }

        return new UseCase(nombre.Trim(), objetivo.Trim(), descripcion?.Trim() ?? string.Empty,
            actores?.ToArray() ?? Array.Empty<Actor>(),
            precondiciones?.ToArray() ?? Array.Empty<string>(),
            trigger?.Trim() ?? string.Empty,
            flujos?.ToArray() ?? Array.Empty<FlujoProceso>(),
            extensiones?.ToArray() ?? Array.Empty<string>(),
            frecuencia?.Trim() ?? string.Empty,
            importancia?.Trim() ?? string.Empty,
            urgencia?.Trim() ?? string.Empty,
            comentarios?.ToArray() ?? Array.Empty<string>());
    }
}
