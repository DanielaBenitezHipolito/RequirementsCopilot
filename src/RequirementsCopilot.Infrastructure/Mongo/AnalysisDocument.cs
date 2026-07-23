using MongoDB.Bson.Serialization.Attributes;
using RequirementsCopilot.Domain.Analyses;

namespace RequirementsCopilot.Infrastructure.Mongo;

public sealed class AnalysisDocument
{
    [BsonId]
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? Summary { get; set; }
    public List<RequirementDocument> Requirements { get; set; } = new();

    public static AnalysisDocument FromDomain(Analysis analysis) => new()
    {
        Id = analysis.Id,
        FileName = analysis.FileName,
        CreatedAt = analysis.CreatedAt,
        Status = analysis.Status.ToString(),
        Error = analysis.Error,
        Summary = analysis.Summary,
        Requirements = analysis.Requirements.Select(RequirementDocument.FromDomain).ToList(),
    };

    public Analysis ToDomain() => Analysis.Rehydrate(
        Id, FileName, CreatedAt, Enum.Parse<AnalysisStatus>(Status), Error, Summary,
        Requirements.Select(r => r.ToDomain()).ToArray());
}

public sealed class RequirementDocument
{
    public string Code { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public EvaluationDocument? Evaluation { get; set; }
    public List<ClarificationDocument> Clarifications { get; set; } = new();
    public UseCaseDocument? UseCase { get; set; }

    public static RequirementDocument FromDomain(Requirement requirement) => new()
    {
        Code = requirement.Code,
        Text = requirement.Text,
        Area = requirement.Area,
        Evaluation = requirement.Evaluation is null ? null : EvaluationDocument.FromDomain(requirement.Evaluation),
        Clarifications = requirement.Clarifications.Select(c => new ClarificationDocument
        {
            Question = c.Question, Answer = c.Answer,
        }).ToList(),
        UseCase = requirement.UseCase is null ? null : UseCaseDocument.FromDomain(requirement.UseCase),
    };

    public Requirement ToDomain()
    {
        var requirement = Requirement.Create(Code, Text, Area);
        if (Evaluation is not null)
        {
            requirement.Evaluate(Evaluation.ToDomain());
        }
        foreach (ClarificationDocument clarification in Clarifications)
        {
            requirement.AddClarification(Domain.Analyses.Clarification.Rehydrate(clarification.Question, clarification.Answer));
        }
        if (UseCase is not null)
        {
            requirement.SetUseCase(UseCase.ToDomain());
        }
        return requirement;
    }
}

public sealed class ClarificationDocument
{
    public string Question { get; set; } = string.Empty;
    public string? Answer { get; set; }
}

public sealed class EvaluationDocument
{
    public List<CriterionDocument> Scores { get; set; } = new();
    public double Threshold { get; set; }

    public static EvaluationDocument FromDomain(Evaluation evaluation) => new()
    {
        Scores = evaluation.Scores.Select(s => new CriterionDocument
        {
            Criterion = s.Criterion, Score = s.Score, Observation = s.Observation,
        }).ToList(),
        Threshold = evaluation.Threshold,
    };

    public Evaluation ToDomain() => Evaluation.Create(
        Scores.Select(s => CriterionScore.Create(s.Criterion, s.Score, s.Observation)).ToArray(), Threshold);
}

public sealed class CriterionDocument
{
    public string Criterion { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Observation { get; set; } = string.Empty;
}

public sealed class ActorDocument
{
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    public static ActorDocument FromDomain(Actor actor) => new() { Nombre = actor.Nombre, Descripcion = actor.Descripcion };

    public Actor ToDomain() => new(Nombre, Descripcion);
}

public sealed class PasoDocument
{
    public int Numero { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string ResultadoEsperado { get; set; } = string.Empty;

    public static PasoDocument FromDomain(PasoFlujo paso) => new()
    {
        Numero = paso.Numero, Accion = paso.Accion, ResultadoEsperado = paso.ResultadoEsperado,
    };

    public PasoFlujo ToDomain() => new(Numero, Accion, ResultadoEsperado);
}

public sealed class FlujoDocument
{
    public string Titulo { get; set; } = string.Empty;
    public List<PasoDocument> Pasos { get; set; } = new();

    public static FlujoDocument FromDomain(FlujoProceso flujo) => new()
    {
        Titulo = flujo.Titulo,
        Pasos = flujo.Pasos.Select(PasoDocument.FromDomain).ToList(),
    };

    public FlujoProceso ToDomain() => new(Titulo, Pasos.Select(p => p.ToDomain()).ToArray());
}

public sealed class UseCaseDocument
{
    public string Nombre { get; set; } = string.Empty;
    public string Objetivo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public List<ActorDocument> Actores { get; set; } = new();
    public List<string> Precondiciones { get; set; } = new();
    public string Trigger { get; set; } = string.Empty;
    public List<FlujoDocument> Flujos { get; set; } = new();
    public List<string> Extensiones { get; set; } = new();
    public string Frecuencia { get; set; } = string.Empty;
    public string Importancia { get; set; } = string.Empty;
    public string Urgencia { get; set; } = string.Empty;
    public List<string> Comentarios { get; set; } = new();

    public static UseCaseDocument FromDomain(UseCase useCase) => new()
    {
        Nombre = useCase.Nombre,
        Objetivo = useCase.Objetivo,
        Descripcion = useCase.Descripcion,
        Actores = useCase.Actores.Select(ActorDocument.FromDomain).ToList(),
        Precondiciones = useCase.Precondiciones.ToList(),
        Trigger = useCase.Trigger,
        Flujos = useCase.Flujos.Select(FlujoDocument.FromDomain).ToList(),
        Extensiones = useCase.Extensiones.ToList(),
        Frecuencia = useCase.Frecuencia,
        Importancia = useCase.Importancia,
        Urgencia = useCase.Urgencia,
        Comentarios = useCase.Comentarios.ToList(),
    };

    public UseCase ToDomain() => UseCase.Create(Nombre, Objetivo, Descripcion,
        Actores.Select(a => a.ToDomain()).ToArray(), Precondiciones, Trigger,
        Flujos.Select(f => f.ToDomain()).ToArray(), Extensiones, Frecuencia, Importancia, Urgencia, Comentarios);
}
