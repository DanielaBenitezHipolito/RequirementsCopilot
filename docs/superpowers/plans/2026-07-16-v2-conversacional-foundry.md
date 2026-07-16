# RequirementsCopilot v2 — Plan de implementación (conversacional + agentes Foundry)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Migrar los agentes al patrón JYDE (publicados en Foundry, Responses API con hilo) y añadir modo conversacional: entrevistador que arma el requerimiento por chat y lo inyecta como análisis nuevo.

**Architecture:** Se conserva todo el pipeline v1 (dominio, agentes, endpoints manuales, front). Cambia la forma del puerto `IChatCompletion` (instrucciones fuera del código) y se añaden: agente entrevistador, 2 endpoints y la vista Conversar. Spec: sección "v2" de `docs/superpowers/specs/2026-07-14-requirements-copilot-design.md`.

**Tech Stack:** igual que v1. Referencia de código a copiar: `D:\Repositories\Daniela Benitez\JYDE.OpenDataCopilot\src\JYDE.OpenDataCopilot.Infrastructure\Chat\FoundryChatCompletion.cs` y `...Infrastructure\Foundry\FoundryAgentSettings.cs|FoundryChatSettings.cs|FoundryOptions.cs`.

## Global Constraints

- Repo: `D:\Repositories\Daniela Benitez\RequirementsCopilot`, rama `feature/requirements-copilot-mvp`. TreatWarningsAsErrors=true. Commits en español. UI/mensajes en español.
- Tests SIEMPRE `dotnet test -c Release`; ante `FileLoadException "Access is denied"`: `dotnet build -c Release -p:Deterministic=false` + `dotnet test -c Release --no-build` (Sophos, ver README). Nunca commitear sin corrida verde.
- Puerto (forma JYDE exacta): `sealed record ChatPrompt(string Agent, string Input, string? PreviousResponseId = null)`; `sealed record ChatResult(string Text, string? ResponseId = null)`.
- Contrato entrevistador (campos exactos): `{"respuesta":"...","borrador":{"texto":"...","area":"..."},"listo":true}` — `borrador` puede ser `null`; `AgentName = "requirement-builder-agent"`.
- Eventos SSE de `/api/interview`: `token {texto}` → `draft {borrador:{texto,area}}` (solo si hay) → `done {responseId}`; `error {mensaje}` ante fallo. Mismo estilo de escritura SSE que `AnalysesController.WriteEventAsync`.
- Errores API `{ "mensaje": "..." }`. Api sigue sin referenciar Domain.
- `Providers:Chat = Fake | Foundry` se mantiene; con Fake TODO debe funcionar sin credenciales (incluida la entrevista de 2 turnos).

---

### Task V1: Puerto JYDE + Foundry Responses API + migración de los 6 call-sites

**Files:**
- Modify: `src/RequirementsCopilot.Application/Analyses/ChatPrompt.cs`, `ChatResult.cs`
- Modify: los 5 agentes en `src/RequirementsCopilot.Application/Analyses/Agents/*.cs` — eliminar el const `Instructions` y su envío; la llamada queda `new ChatPrompt(AgentName, input)`. NO tocar nombres de agente, construcción del input ni parseo.
- Rewrite: `src/RequirementsCopilot.Infrastructure/Chat/FoundryChatCompletion.cs` — copiar el de JYDE (ruta arriba) adaptando namespaces: `POST {Endpoint sin slash}/openai/v1/responses`, header `api-key`, payload `{model, input:[{role:"user",type:"message",content:[{type:"input_text",text:prompt.Input}]}], agent_reference:{name,type:"agent_reference"[,version]}}` + `previous_response_id` si viene; `ExtractText` sobre `output[].content[]` con `type=="output_text"`; devuelve `ChatResult(text, root.id)`.
- Rewrite: `src/RequirementsCopilot.Infrastructure/Chat/FoundryOptions.cs` → forma JYDE: `Endpoint`, `ApiKey`, `Chat { Model, Agents: Dictionary<string, FoundryAgentSettings { Name, Version?, Model? }> }` (copiar los 3 records de settings de JYDE, namespace nuestro). Resolución: `agent = Chat.Agents.GetValueOrDefault(prompt.Agent)`; `model = agent?.Model ?? Chat.Model`; `name = agent?.Name ?? prompt.Agent`.
- Modify: `FakeChatCompletion.cs` y `StubChatCompletion.cs` (tests) a la nueva firma; Fake devuelve `ChatResult(text, "fake-response-id")`.
- Modify: `FoundryChatCompletionTests.cs` — reescribir asserts: URL `/openai/v1/responses`, header, `agent_reference.name`, `previous_response_id` presente/ausente, extracción de `output_text`, respuesta sin `output` → texto vacío.
- Modify: `src/RequirementsCopilot.Api/Program.cs` (binding de la nueva sección Foundry) y `appsettings.json`: `"Foundry": { "Endpoint": "", "ApiKey": "", "Chat": { "Model": "gpt-4.1-mini", "Agents": {} } }`.
- Tests de agentes: solo cambian construcciones de ChatPrompt/asserts de `Prompts[0]` (ya no hay Instructions).

**Interfaces — Produces:** puerto nuevo estable para V2/V3; `FoundryOptions` con catálogo; resto del pipeline intacto (mismos eventos, mismos endpoints).

- [ ] Migrar puerto y call-sites; suite completa `-c Release` verde (los 52+ tests actuales ajustados, sin perder cobertura)
- [ ] Commit: `refactor: puerto IChatCompletion forma JYDE y Foundry Responses API con agent_reference`

---

### Task V2: InterviewerAgent

**Files:**
- Create: `src/RequirementsCopilot.Application/Interview/InterviewerAgent.cs` (+ records de reply privados y `RequirementDraft(string Texto, string Area)` público en ese namespace)
- Modify: `src/RequirementsCopilot.Infrastructure/Chat/FakeChatCompletion.cs` — caso `requirement-builder-agent`
- Test: `tests/RequirementsCopilot.Tests/Application/InterviewerAgentTests.cs`

**Interfaces:**
- Consumes: `IChatCompletion`, `JsonText`, `StubChatCompletion`.
- Produces: `class InterviewerAgent { public const string AgentName = "requirement-builder-agent"; ctor(IChatCompletion); Task<InterviewTurn> ChatAsync(string message, string? previousResponseId, CancellationToken ct = default); }` con `sealed record InterviewTurn(string Respuesta, RequirementDraft? Borrador, bool Listo, string? ResponseId)`.
- Comportamiento: envía `ChatPrompt(AgentName, message, previousResponseId)`; parsea con `JsonText.FirstJsonObject`; sin JSON → `InvalidOperationException` español; `respuesta` vacía → excepción; borrador con `texto` vacío → se trata como null.
- Fake: sin `PreviousResponseId` → `{"respuesta":"¿Qué problema resuelve y quién lo usa?","borrador":null,"listo":false}`; con él → respuesta con borrador Pagos listo=true (texto de requerimiento realista).

- [ ] TDD: tests (turno pregunta, turno borrador listo, sin JSON lanza, hilo pasa `previousResponseId` al prompt) → RED → implementar → GREEN → suite completa
- [ ] Commit: `feat: agente entrevistador de requerimientos con hilo`

---

### Task V3: Endpoints de entrevista y análisis desde requerimiento

**Files:**
- Create: `src/RequirementsCopilot.Application/Interview/CreateAnalysisFromRequirement.cs` — use case: `Task<Guid> ExecuteAsync(string texto, string area, CancellationToken ct)`: `Analysis.Create($"Conversación {texto[..min(40)]}…")` (nombre legible), `Requirement.Create("REQ-001", texto, area)`, corre `RequirementEvaluatorAgent` (umbral de `AnalysisOptions`) y, si es ambiguo (mismas condiciones que el orquestador: no pasa o Claridad/Completitud < 4), `ClarifierAgent`; `Complete()`, persiste, devuelve Id. Errores del LLM → `Fail(...)` + persistir + relanzar `InvalidOperationException` (el controller lo traduce a 502 `{mensaje}`).
- Create: `src/RequirementsCopilot.Api/Controllers/InterviewController.cs` — `POST /api/interview` body `{mensaje, previousResponseId?}`: valida mensaje no vacío (400), SSE: emite `token {texto: turn.Respuesta}`, `draft {borrador}` si `turn.Borrador != null`, `done {responseId: turn.ResponseId}`; excepción → `error {mensaje}`.
- Modify: `AnalysesController` — `POST /api/analyses/from-requirement` body `{texto, area}`: valida texto (400), llama use case, `200 {analysisId}`.
- Modify: `Program.cs` — registrar `InterviewerAgent`, `CreateAnalysisFromRequirement`.
- Test: `tests/RequirementsCopilot.Tests/Api/InterviewEndpointTests.cs` (integración con Fake): entrevista 2 turnos (1º sin previousResponseId → token sin draft; 2º con él → draft + done), mensaje vacío → 400; from-requirement → 200 con analysisId y GET detalle muestra el requerimiento evaluado; texto vacío → 400. Ajustar tests del use case con stubs si aplica.

- [ ] TDD → suite completa `-c Release` verde
- [ ] Commit: `feat: endpoints de entrevista SSE y análisis desde requerimiento`

---

### Task V4: Front — vista Conversar

**Files:**
- Create: `web/src/features/interview/store.ts` — Zustand: `{ messages: {role:'user'|'agent', text}[], draft?: {texto,area}, previousResponseId?, status: 'idle'|'sending'|'error', error?, send(text), applyEvent(evt), reset() }`; reducer de eventos `token/draft/done/error`.
- Create: `web/src/features/interview/InterviewView.tsx` — chat (burbujas, input + enviar con Enter), panel lateral/inferior del borrador (texto + área) con botón "Aprobar y analizar" → `POST /api/analyses/from-requirement` → `onAnalyzed(analysisId)`.
- Modify: `web/src/shared/api/client.ts` — `sendInterviewMessage(mensaje, previousResponseId?): Promise<Response>` (POST JSON, devuelve Response para parseSse) y `createAnalysisFromRequirement(texto, area): Promise<{analysisId}>`.
- Modify: `web/src/App.tsx` — pestaña "Conversar"; al aprobar navega a Detalle (`view='detail'`, `selectedId=analysisId`).
- Test: `web/src/features/interview/store.test.ts` — reducer: token acumula mensaje agente, draft setea borrador, done guarda previousResponseId, error marca estado.
- Reusa `parseSse` existente. UI en español, estilo Tailwind consistente con las vistas actuales.

- [ ] TDD store → `npm test` verde (6+ tests) y `npm run build` verde
- [ ] Commit: `feat: vista Conversar con chat SSE y aprobación de borrador`

---

### Task V5: Docs

**Files:**
- Modify: `docs/adr/0002-foundry-adaptadores-intercambiables.md` — sección "Actualización 2026-07-16": desviación revertida; agentes publicados en Foundry, Responses API + `agent_reference` + `previous_response_id`; motivo (v2 conversacional necesita hilo; se unifica el patrón con JYDE).
- Modify: `docs/prompts-agentes.md` — añadir prompt del entrevistador (instrucciones completas: rol, preguntar turno a turno máx 2 preguntas, redactar borrador cuando haya suficiente info, contrato JSON exacto, nunca inventar); nueva sección "Publicación en Foundry": pasos del portal (Agents → New agent → pegar instrucciones → asignar modelo → Publish) y mapeo a `Foundry:Chat:Agents` en config; advertencia de que Foundry es ahora la fuente de verdad de los prompts en el proveedor real (el MD debe mantenerse sincronizado).
- Modify: `README.md` — config `Foundry:Chat:{Model,Agents}` con ejemplo JSON, endpoint `/api/interview` y `/api/analyses/from-requirement`, pestaña Conversar.

- [ ] Commit: `docs: ADR-0002 actualizado, prompt del entrevistador y publicación en Foundry`

---

### Task V6: Validación end-to-end (controller)

- [ ] Suites completas backend/front + build
- [ ] En vivo con Fake: entrevista de 2 turnos por curl (SSE con token/draft/done), from-requirement → detalle con requerimiento evaluado, flujo UI Conversar
- [ ] Ledger + revisión final de rama (diff v2)
