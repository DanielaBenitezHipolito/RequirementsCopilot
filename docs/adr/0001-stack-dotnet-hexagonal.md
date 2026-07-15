# ADR 0001 — Stack .NET con arquitectura hexagonal + DDD

- **Estado:** Aceptado
- **Fecha:** 2026-07-14
- **Decisores:** Equipo Requirements Copilot

## Contexto

Requirements Copilot es un analizador de requerimientos con IA: sube un documento de especificación,
lo procesa con un pipeline de agentes LLM y expone el resultado en vivo por streaming (SSE). El
backend necesita aislar el dominio (reglas de evaluación, entidades) de los detalles de
infraestructura (proveedor de LLM, persistencia, extracción de texto), que van a cambiar durante el
proyecto (de `Fake` a `Foundry`, de `InMemory` a `Mongo`) sin tocar la lógica de negocio.

Este proyecto hereda las razones técnicas de JYDE.OpenDataCopilot ADR-0001, que ya resolvió esta
misma decisión para un problema de la misma familia (API conversacional con streaming + IA externa
vía HTTP + dominio que debe permanecer aislado):

- Tipado estático fuerte y verificación en compilación, que reduce defectos y permite refactorizar
  con seguridad.
- Soporte nativo de .NET para arquitectura hexagonal/DDD: inyección de dependencias, límites de
  proyecto explícitos vía referencias de proyecto.
- Rendimiento y concurrencia adecuados para streaming (`IAsyncEnumerable`, SSE) y llamadas HTTP a
  servicios de IA.
- Testabilidad: los puertos permiten probar dominio y aplicación con dobles, sin infraestructura
  real (Mongo, Foundry).

Al igual que en JYDE, el LLM se consume vía API REST, así que el lenguaje del backend es
independiente del proveedor de IA: la elección no depende de la disponibilidad de SDKs.

## Decisión

- **Backend en .NET 8** con **arquitectura hexagonal (puertos y adaptadores) + DDD**.
- **Frontend en React** (Vite) como SPA que consume la API REST y el stream SSE.
- Capas con regla de dependencias hacia adentro:

  ```
  Domain ← Application ← Infrastructure ← Api
  ```

  - `Domain`: entidades puras (`Analysis`, `Requirement`, `Evaluation`, `UserStory`, `TestCase`),
    factory methods, invariantes. Sin dependencias externas.
  - `Application`: casos de uso (`AnalysisOrchestrator`), agentes, puertos (`IChatCompletion`,
    `IAnalysisRepository`, `IDocumentTextExtractor`), DTOs.
  - `Infrastructure`: adaptadores concretos (Chat/Fake, Chat/Foundry, Mongo, extractores de
    documentos).
  - `Api`: controllers finos, composition root (`Program.cs`). **No referencia `Domain`
    directamente** — los endpoints consumen DTOs de `Application`, igual que ADR-0011 de JYDE.

## Consecuencias

- **Positivas:** límites de capa verificados por el compilador (referencias de proyecto); el
  dominio y la aplicación se prueban sin Mongo ni Foundry reales (dobles de `IChatCompletion` y
  `IAnalysisRepository`); cambiar de proveedor de LLM o de repositorio es configuración
  (`Providers:Chat`, `Providers:AnalysisRepository`), no reescritura; consistencia con el resto de
  la familia de proyectos (JYDE), facilitando el trabajo de asistentes de IA sobre el código.
- **Negativas / trade-offs:** más proyectos y archivos que un monolito de un solo layer; para un
  MVP de alcance acotado (sin auth, sin multi-tenancy) es más estructura de la estrictamente
  necesaria para el tamaño actual, pero se acepta por consistencia y porque el pipeline de agentes
  ya justifica puertos intercambiables.
- **Seguimiento:** mantener la regla de dependencias hacia adentro; no introducir referencias de
  `Api` a `Domain` ni de `Domain` hacia afuera.

## Alternativas consideradas

- **Python con FastAPI** — ecosistema de IA más maduro (LangChain, etc.), pero el acceso al LLM es
  vía REST en ambos casos, así que esa ventaja no aplica aquí. Se descarta por las mismas razones
  que en JYDE: menos garantías en compilación para un dominio con invariantes (scores 1-5,
  umbrales), y por mantener consistencia de stack con el resto de la familia de proyectos.
- **Un solo proyecto sin capas** — más rápido de arrancar, pero mezclar controllers con lógica de
  parseo de LLM y acceso a Mongo hace imposible testear el pipeline de agentes sin infraestructura
  real. Se descarta.
