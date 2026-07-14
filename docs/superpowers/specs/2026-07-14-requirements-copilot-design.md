# RequirementsCopilot — Diseño

- **Fecha:** 2026-07-14
- **Estado:** Aprobado
- **Basado en:** patrones de `JYDE.OpenDataCopilot` (ADRs 0001, 0003, 0004, 0006, 0008, 0009, 0010, 0011, 0012, 0015, 0016)

## Objetivo

Analizador de requerimientos con IA, sencillo. Flujo único:

1. Usuario sube documento de especificación (PDF, DOCX, TXT, MD).
2. Pipeline de agentes extrae requerimientos y evalúa cada uno contra una rúbrica fija de calidad.
3. Si el requerimiento **pasa** (promedio ≥ umbral configurable), se generan automáticamente historias de usuario y, por cada historia, un caso de prueba.
4. Resultado se transmite en vivo (SSE) y se persiste en MongoDB.

Fuera de alcance: autenticación, base de conocimiento/embeddings, reportes Excel/Word, edición de requerimientos, multi-tenancy.

## Decisiones

| Tema | Decisión |
|---|---|
| Arquitectura backend | Hexagonal (puertos y adaptadores) + DDD, .NET (espejo ADR-0001 JYDE) |
| IA | Azure AI Foundry GPT-4.1-mini tras puerto `IChatCompletion`; adaptador `Fake` para dev sin credenciales, selección por `Providers:Chat` (ADR-0003/0004) |
| Multiagente | Pipeline secuencial de 4 agentes especializados, **sin router** (el flujo es determinista, no conversacional) — adaptación de ADR-0015 |
| Persistencia | MongoDB, colección `analyses` (ADR-0012) |
| Criterios | Rúbrica fija (Claridad, Completitud, Verificabilidad, Consistencia, Factibilidad), score 1–5 + observación; umbral de aprobación configurable (`Analysis:PassThreshold`, default 3.5) |
| Generación | Automática en cadena: aprobado → historias → 1 caso de prueba por historia |
| Formatos | PDF (PdfPig), DOCX (OpenXML), TXT/MD (lectura directa); límite 10 MB |
| Frontend | Vite + React 19 + TypeScript + Zustand + Tailwind (ADR-0008/0009); Vitest (ADR-0016) |
| Streaming | SSE de extremo a extremo, eventos incrementales |
| Testing | xUnit, TDD por convención (ADR-0006); agentes testeados con `IChatCompletion` doble |

## Estructura del repositorio

```
RequirementsCopilot\
├── RequirementsCopilot.slnx
├── src\
│   ├── RequirementsCopilot.Domain          # entidades puras, sin dependencias
│   ├── RequirementsCopilot.Application     # casos de uso, puertos, agentes
│   ├── RequirementsCopilot.Infrastructure  # adaptadores: Foundry, Fake, Mongo, extractores
│   └── RequirementsCopilot.Api             # controllers, composition root; NO referencia Domain (ADR-0011)
├── tests\
│   └── RequirementsCopilot.Tests
├── web\                                    # frontend
└── docs\
    ├── adr\
    └── superpowers\specs\
```

Regla de dependencias hacia adentro: `Domain ← Application ← Infrastructure ← Api`.

## Dominio

- **`Analysis`** (agregado raíz): `Id`, `FileName`, `CreatedAt`, `Status` (`Processing | Completed | Failed`), `Requirements[]`, `Error?`.
- **`Requirement`**: `Code` (REQ-001…), `Text`, `Area` (área funcional detectada del documento, ej. Pagos, Seguridad, Reportes), `Evaluation`, `UserStories[]`.
- **`Evaluation`**: 5 `CriterionScore` (criterio, score 1–5, observación), `Average`, `Passed` (average ≥ umbral). Umbral llega como parámetro — el dominio no lee configuración.
- **`UserStory`**: `Role`, `Goal` ("quiero"), `Benefit` ("para"), `AcceptanceCriteria[]`, `TestCase`.
- **`TestCase`**: `Title`, `Preconditions[]`, `Steps[]`, `ExpectedResult`.

Entidades con factory methods; invariantes en el dominio (score 1–5, código no vacío).

## Application

### Puertos (interfaces)

- `IChatCompletion` — `IAsyncEnumerable<string> CompleteAsync(instrucciones, input, ct)` (misma forma que JYDE, streaming).
- `IAnalysisRepository` — `SaveAsync`, `GetByIdAsync`, `GetAllAsync`.
- `IDocumentTextExtractor` — `ExtractAsync(stream, fileName)` → texto plano.

### Agentes (pipeline secuencial)

Cada agente es una clase con prompt propio (pequeño, especializado) que consume `IChatCompletion` y parsea JSON a la defensiva (utilidad `JsonText.FirstJsonObject`, patrón JYDE: robusto ante prosa, vallas ``` y JSON duplicado):

1. **`RequirementExtractorAgent`** — texto del documento → `{ requerimientos: [{ codigo, texto, area }] }`. El área es el eje de clasificación/filtrado (no el rol de la historia).
2. **`RequirementEvaluatorAgent`** — un requerimiento → `{ criterios: [{ nombre, score, observacion }] }`.
3. **`StoryWriterAgent`** — requerimiento aprobado → `{ historias: [{ rol, quiero, para, criteriosAceptacion[] }] }`.
4. **`TestCaseWriterAgent`** — una historia → `{ titulo, precondiciones[], pasos[], resultadoEsperado }`.

### Orquestador

`AnalysisOrchestrator` (caso de uso, punto de entrada):

```
ExtractText → ExtractorAgent → por cada requerimiento:
    EvaluatorAgent → si Passed: StoryWriterAgent → por cada historia: TestCaseWriterAgent
→ persistir Analysis → done
```

Emite `IAsyncEnumerable<AnalysisEvent>`:
`status` → `requirement` → `evaluation` → `story` → `testcase` → … → `done` | `error`.

Guardrails: si un requerimiento no pasa, se declara el motivo (observaciones de la rúbrica) y **no** se generan historias. Si el LLM falla o el JSON no es parseable tras reintento defensivo, el análisis termina en `Failed` con evento `error`; nunca se inventa contenido.

## Infrastructure

- `Chat/FakeChatCompletion` — respuestas predefinidas coherentes con los contratos JSON de cada agente; permite desarrollo y demo a $0 sin credenciales.
- `Chat/FoundryChatCompletion` — Azure AI Foundry, GPT-4.1-mini, streaming; settings `Foundry:Endpoint`, `Foundry:ApiKey`, `Foundry:ChatDeployment` (secrets fuera del repo).
- `Mongo/MongoAnalysisRepository` — MongoDB.Driver, colección `analyses`, documento = agregado completo serializado.
- `Documents/CompositeTextExtractor` — despacha por extensión: `PdfPigTextExtractor` (.pdf), `OpenXmlTextExtractor` (.docx), `PlainTextExtractor` (.txt/.md). Extensión no soportada → error de validación.

Selección de adaptador por configuración: `Providers:Chat = Fake | Foundry`.

## Api

Controladores (ADR-0010), referencia solo Application (ADR-0011). Composition root en `Program.cs`.

| Endpoint | Descripción |
|---|---|
| `POST /api/analyses` | multipart file → valida extensión y tamaño (≤ 10 MB) → responde `text/event-stream` con los eventos del pipeline en vivo; al finalizar el análisis queda persistido |
| `GET /api/analyses` | historial (id, archivo, fecha, estado, conteos) |
| `GET /api/analyses/{id}` | detalle completo del agregado |

Errores de API: `{ "mensaje": "..." }`. CORS abierto solo al origen del frontend en dev.

## Frontend (`web/`)

Vite + React 19 + TS. Estado global con **Zustand** (store de análisis en curso + historial). Estilos **Tailwind**.

Vistas:

1. **Analizar** — drag & drop del archivo; al enviar consume el SSE (fetch streaming) y muestra progreso incremental: requerimientos apareciendo, rúbrica con veredicto pasa/no pasa, historias y casos desplegándose en vivo.
2. **Historial** — lista de análisis previos (GET /api/analyses).
3. **Detalle** — agregado completo: requerimientos con su rúbrica (scores + observaciones), historias con criterios de aceptación y caso de prueba expandible. **Filtro por área funcional** (chips con las áreas presentes en el análisis).

Tests con Vitest (store + parseo de eventos SSE).

## Manejo de errores

- Validación de archivo (tipo, tamaño, vacío) → 400 `{ mensaje }` antes de abrir el stream.
- Fallo LLM / JSON inválido → evento SSE `error`, `Analysis.Status = Failed`, se persiste lo alcanzado.
- Frontend: toast de error y estado visual del análisis fallido.

## Testing

- **Dominio:** invariantes, cálculo de promedio y veredicto por umbral.
- **Agentes:** con `IChatCompletion` fake — contrato JSON, parseo defensivo (prosa alrededor, vallas, JSON malformado).
- **Orquestador:** pipeline end-to-end con dobles — orden de eventos, guardrail de no-pasa, persistencia final, fallo intermedio.
- **Frontend:** Vitest sobre store Zustand y reducción de eventos SSE.

## ADRs propios a escribir

1. Stack .NET hexagonal + DDD (hereda razones de JYDE ADR-0001).
2. Modelos vía Azure AI Foundry, adaptadores Fake/Foundry intercambiables.
3. Pipeline secuencial de agentes especializados sin router (adaptación de ADR-0015; el router se descarta porque el flujo no es conversacional).
4. Persistencia MongoDB.
5. Frontend Vite + Zustand + Tailwind.
