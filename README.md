# Requirements Copilot

Analizador de requerimientos con IA. Sube un documento de especificación (PDF, DOCX, TXT o MD) y un
pipeline de agentes:

1. **Extrae** los requerimientos del documento (código, texto, área funcional).
2. **Evalúa** cada requerimiento contra una rúbrica fija (Claridad, Completitud, Verificabilidad,
   Consistencia, Factibilidad; score 1-5 por criterio).
3. Si el requerimiento **pasa** el umbral de aprobación, genera automáticamente **historias de
   usuario** y, por cada historia, un **caso de prueba**.
4. Todo el proceso se transmite **en vivo por SSE** y queda persistido para consultarlo después.

Fuera de alcance de este MVP: autenticación, base de conocimiento/embeddings, reportes Excel/Word,
edición de requerimientos, multi-tenancy.

## Stack

- **Backend:** .NET 8, arquitectura hexagonal (puertos y adaptadores) + DDD, xUnit.
- **IA:** pipeline de 4 agentes especializados tras el puerto `IChatCompletion` (adaptadores `Fake`
  para desarrollo/demo y `Foundry` para Azure AI Foundry, GPT-4.1-mini).
- **Persistencia:** MongoDB (adaptador `InMemory` para desarrollo/tests).
- **Streaming:** Server-Sent Events (SSE) de extremo a extremo.
- **Frontend:** Vite + React 19 + TypeScript + Zustand + Tailwind CSS, Vitest.

Ver el detalle de cada decisión en [`docs/adr/`](docs/adr/).

## Cómo correr el proyecto

### Backend

```bash
dotnet run --project src/RequirementsCopilot.Api
```

- API en `http://localhost:5100`.
- Swagger en `http://localhost:5100/swagger` (solo entorno `Development`).

Con la configuración por defecto (`Providers:Chat = Fake`, `Providers:AnalysisRepository =
InMemory`) el backend corre sin credenciales ni Mongo instalado.

### Frontend

```bash
cd web
npm install
npm run dev
```

- SPA en `http://localhost:5173`.

### Tests

```bash
# Backend (45 tests xUnit)
dotnet test -c Release

# Frontend (Vitest)
cd web
npm test
```

## Configuración

Toda la configuración vive en `src/RequirementsCopilot.Api/appsettings.json` (valores por defecto,
sin secrets) y se sobreescribe en `appsettings.Development.json` (gitignored, no commitear) o
variables de entorno.

| Clave | Valores / default | Descripción |
|---|---|---|
| `Providers:Chat` | `Fake` \| `Foundry` (default `Fake`) | Adaptador de `IChatCompletion` que usan los agentes. `Fake` responde con datos predefinidos, sin costo ni credenciales. |
| `Providers:AnalysisRepository` | `InMemory` \| `Mongo` (default `InMemory`) | Adaptador de `IAnalysisRepository`. `InMemory` no requiere Mongo instalado. |
| `Analysis:PassThreshold` | `3.5` (default) | Umbral de aprobación: promedio de los 5 criterios de la rúbrica a partir del cual un requerimiento genera historias. |
| `Foundry:Endpoint` | — | Endpoint de Azure AI Foundry. Solo necesario con `Providers:Chat = Foundry`. |
| `Foundry:ApiKey` | — | API key de Foundry. **Secret** — no commitear. |
| `Foundry:Deployment` | `gpt-4.1-mini` | Nombre del deployment del modelo en Foundry. |
| `Foundry:ApiVersion` | — | Versión de la API de Foundry a usar. |
| `Mongo:ConnectionString` | — | Cadena de conexión a MongoDB. **Secret** — no commitear. Solo necesario con `Providers:AnalysisRepository = Mongo`. |
| `Mongo:Database` | `requirements_copilot` | Base de datos de Mongo donde vive la colección `analyses`. |
| `Cors:Origin` | `http://localhost:5173` | Origen permitido por CORS (el frontend en dev). |

**Secrets:** nunca en `appsettings.json` ni en el repo. Usar `appsettings.Development.json` (ya en
`.gitignore`) o variables de entorno con `__` como separador de sección, por ejemplo:

```bash
Foundry__ApiKey=...
Mongo__ConnectionString=...
```

## Estructura del repositorio

```
RequirementsCopilot\
├── RequirementsCopilot.sln
├── src\
│   ├── RequirementsCopilot.Domain          # entidades puras: Analysis, Requirement, Evaluation, UserStory, TestCase
│   ├── RequirementsCopilot.Application     # casos de uso, agentes, puertos (IChatCompletion, IAnalysisRepository, IDocumentTextExtractor)
│   ├── RequirementsCopilot.Infrastructure  # adaptadores: Chat (Fake/Foundry), Mongo, extractores de documentos
│   └── RequirementsCopilot.Api             # controllers, composition root (Program.cs); no referencia Domain
├── tests\
│   └── RequirementsCopilot.Tests           # 45 tests xUnit (Domain, Application, Infrastructure, Api)
├── web\                                    # frontend Vite + React + Zustand + Tailwind
└── docs\
    ├── adr\                                # decisiones de arquitectura (ver abajo)
    └── superpowers\specs\                  # spec de diseño del proyecto
```

## Decisiones de arquitectura (ADRs)

- [0001 — Stack .NET hexagonal + DDD](docs/adr/0001-stack-dotnet-hexagonal.md)
- [0002 — IChatCompletion con adaptadores Fake/Foundry intercambiables](docs/adr/0002-foundry-adaptadores-intercambiables.md)
- [0003 — Pipeline secuencial de agentes especializados sin router](docs/adr/0003-pipeline-agentes-sin-router.md)
- [0004 — Persistencia en MongoDB](docs/adr/0004-persistencia-mongodb.md)
- [0005 — Frontend Vite + Zustand + Tailwind](docs/adr/0005-frontend-vite-zustand-tailwind.md)

## Solución de problemas

**`dotnet test` falla con `FileLoadException: Access is denied` sobre una DLL recién compilada.**

En máquinas con Sophos u otro EDR, el antivirus puede bloquear momentáneamente la carga de
ensamblados recién compilados por el testhost, incluso cuando el build fue exitoso. Workaround:

```bash
dotnet build -c Release -p:Deterministic=false
dotnet test -c Release --no-build
```

Compilar con `-p:Deterministic=false` genera un binario con un hash distinto en cada build, lo que
evita que el EDR lo reconozca como el mismo archivo bloqueado previamente; `--no-build` evita
recompilar (y volver a disparar el bloqueo) al ejecutar los tests.
