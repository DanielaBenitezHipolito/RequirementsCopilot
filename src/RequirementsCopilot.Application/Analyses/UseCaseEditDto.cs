namespace RequirementsCopilot.Application.Analyses;

/// <summary>Actor en una edición manual de caso de uso.</summary>
public sealed record ActorEditDto(string? Nombre, string? Descripcion);

/// <summary>Paso de flujo en una edición manual de caso de uso.</summary>
public sealed record PasoFlujoEditDto(int Numero, string? Accion, string? ResultadoEsperado);

/// <summary>Flujo de proceso en una edición manual de caso de uso.</summary>
public sealed record FlujoEditDto(string? Titulo, List<PasoFlujoEditDto>? Pasos);

/// <summary>
/// Edición manual completa de un caso de uso (las 12 secciones de la plantilla). Los campos nulos
/// se normalizan a vacío; Nombre y Objetivo son obligatorios (validados por el dominio).
/// </summary>
public sealed record UseCaseEditDto(string? Nombre, string? Objetivo, string? Descripcion,
    List<ActorEditDto>? Actores, List<string>? Precondiciones, string? Trigger,
    List<FlujoEditDto>? Flujos, List<string>? Extensiones, string? Frecuencia,
    string? Importancia, string? Urgencia, List<string>? Comentarios);
