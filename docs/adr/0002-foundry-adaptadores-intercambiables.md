# ADR 0002 — IChatCompletion con adaptadores Fake/Foundry intercambiables

- **Estado:** Aceptado
- **Fecha:** 2026-07-14
- **Decisores:** Equipo Requirements Copilot

## Contexto

El pipeline de agentes (extractor, evaluador, historias, casos de prueba) necesita un modelo de
lenguaje para cada paso. Desarrollar y probar el pipeline no debe requerir credenciales de Azure ni
generar costo, y el equipo necesita poder demostrar el flujo completo (subir documento → historias →
casos) sin depender de que Foundry esté disponible o configurado.

Además, el pipeline es determinista (cada agente hace una llamada puntual con su propio prompt, sin
conversación), a diferencia de JYDE.OpenDataCopilot, donde el Copilot publica agentes conversacionales
en el portal de Azure AI Foundry (`agent_reference`, Responses API) para que Foundry gestione el
hilo de conversación y el versionado de instrucciones.

## Decisión

- Puerto único **`IChatCompletion`** en `Application`, con la forma
  `IAsyncEnumerable<string> CompleteAsync(instrucciones, input, ct)` (streaming, misma forma que
  JYDE).
- Dos adaptadores en `Infrastructure/Chat`:
  - **`FakeChatCompletion`** — respuestas predefinidas coherentes con el contrato JSON de cada
    agente. Permite correr el pipeline completo en desarrollo y demos a costo $0 y sin
    credenciales.
  - **`FoundryChatCompletion`** — Azure AI Foundry, modelo **GPT-4.1-mini**, streaming real. Lee
    `Foundry:Endpoint`, `Foundry:ApiKey`, `Foundry:Deployment` (secrets fuera del repo).
- Selección de adaptador por configuración: **`Providers:Chat = Fake | Foundry`** (default `Fake`).
- **Desviación deliberada de JYDE:** las instrucciones de cada agente viven **en el código**
  (`ChatPrompt.Instructions`, una constante/propiedad por agente), y se usa la **Chat Completions
  API estándar** de Foundry — no `agent_reference` ni la Responses API. JYDE publica y versiona
  agentes en el portal de Foundry porque son conversacionales y se benefician de gestión de hilo e
  historial gestionados por la plataforma. Requirements Copilot es un pipeline de llamadas puntuales
  y sin estado conversacional: no hay hilo que gestionar, así que ese nivel de indirección no aporta
  nada y sí añade una dependencia operativa (crear/versionar agentes en el portal antes de poder
  desarrollar). El proyecto queda **autocontenido**: cualquiera con el repo y una API key puede
  correr el pipeline sin pasos de configuración en Azure más allá del endpoint del modelo.

## Consecuencias

- **Positivas:** desarrollo y CI sin credenciales ni costo (`Fake` es el default); cambio de
  proveedor por configuración, sin tocar los agentes; instrucciones versionadas junto con el código
  (diff visible en PRs, sin desincronización entre el repo y el portal de Foundry); menor superficie
  operativa (no hay que crear/publicar agentes en Azure antes de poder ejecutar el proyecto).
- **Negativas / trade-offs:** si en el futuro se necesita conversación multi-turno o gestión de
  hilo por la plataforma, habría que migrar a `agent_reference`/Responses API — hoy no se necesita
  porque el flujo es de un solo turno por paso; las instrucciones no se pueden editar sin
  desplegar código (aceptable: son parte del comportamiento del sistema, no configuración de
  negocio).
- **Seguimiento:** si se agrega un agente conversacional (fuera del alcance actual), revisar si
  conviene el modelo de agentes publicados de Foundry solo para ese caso puntual.

## Actualización 2026-07-16

**Estado:** la desviación deliberada descrita arriba (instrucciones en código + Chat Completions API)
se **revierte**. Motivo: la v2 conversacional (`POST /api/conversations/messages`,
`POST /api/conversations/complete`) introduce un agente entrevistador
(`RequirementBuilderAgent`) que sí necesita memoria de hilo multi-turno — exactamente el escenario
que esta ADR identificaba como el único que justificaría migrar a `agent_reference`/Responses API.
En vez de mantener dos patrones distintos (agentes de pipeline con prompt en código vs. agente
conversacional con hilo gestionado), se unifica todo bajo el modelo de JYDE (ADR-0015 de JYDE):

- **Todos** los agentes (extractor, evaluador, clarificador, story-writer, test-case-writer y ahora
  requirement-builder) se publican como agentes en el portal de **Azure AI Foundry**.
- El adaptador `FoundryChatCompletion` deja de usar la Chat Completions API y pasa a usar la
  **Responses API** (`/openai/v1/responses`), invocando cada agente por
  `agent_reference { name, version }` en vez de mandar las instrucciones inline.
- El hilo de conversación se mantiene con `previous_response_id` — necesario para que el
  entrevistador recuerde las respuestas previas del usuario sin que el backend guarde estado.
- El catálogo de agentes (código lógico → `Name`/`Version`/`Model` en Foundry) se mapea en
  configuración: `Foundry:Chat:Agents`, una entrada por agente (ver `README.md` y
  `docs/agentes/` para el ejemplo completo y los pasos de publicación).
- `FakeChatCompletion` no cambia: sigue permitiendo correr el pipeline completo y la conversación
  sin credenciales, para desarrollo y CI.

**Consecuencias de la actualización:**

- Los prompts dejan de vivir solo en el código (`ChatPrompt.Instructions` por agente) y pasan a
  vivir en Foundry; `docs/agentes/` sigue siendo la referencia legible pero debe
  sincronizarse a mano — ya no hay diff automático en PRs que garantice consistencia.
- Se recupera la dependencia operativa que esta ADR había descartado originalmente: hay que crear y
  versionar los 6 agentes en el portal de Foundry antes de poder usar `Providers:Chat = Foundry` en
  real (no afecta a `Fake`, que sigue siendo el default para dev/CI).
- Se unifica el patrón de integración con Foundry entre JYDE.OpenDataCopilot y RequirementsCopilot,
  reduciendo la superficie de decisiones arquitectónicas distintas entre proyectos hermanos.

## Alternativas consideradas

- **`agent_reference` / Responses API (como JYDE)** — apropiado para agentes conversacionales con
  hilo gestionado por Foundry. Se descarta aquí porque el pipeline no es conversacional: cada
  llamada es independiente y el prompt cabe en el código sin necesidad de versionado en el portal.
- **Un único adaptador real desde el inicio (sin `Fake`)** — obligaría a tener credenciales de
  Foundry para correr tests y hacer demos; se descarta porque bloquea desarrollo y CI.
- **Modelo más grande (GPT-4.1 o GPT-4o) por defecto** — mayor costo y latencia sin beneficio claro
  para tareas de extracción/evaluación estructuradas; se elige GPT-4.1-mini como modelo inicial y se
  revisará si la calidad de las evaluaciones lo justifica.
