# ADR 0003 — Pipeline secuencial de agentes especializados sin router

- **Estado:** Aceptado
- **Fecha:** 2026-07-14
- **Decisores:** Equipo Requirements Copilot

## Contexto

El análisis de un documento de requerimientos tiene un flujo fijo y conocido de antemano: extraer
requerimientos, evaluar cada uno contra una rúbrica, y si pasa el umbral, generar historias de
usuario y un caso de prueba por historia. No hay ambigüedad sobre "qué agente debe atender esta
solicitud" — a diferencia de JYDE.OpenDataCopilot (ADR-0015), donde el Copilot conversacional recibe
intención en lenguaje natural y debe **enrutar** dinámicamente hacia uno de varios agentes
(recomendar datasets, calcular cifras, comparar, explicar) mediante `IAgentRouter`.

Aquí el orden de los pasos es siempre el mismo y determinista: no hay decisión de "cuál agente
sigue", sino una secuencia fija donde el resultado de un paso condiciona si el siguiente paso ocurre
(guardrail de aprobación).

## Decisión

Pipeline **secuencial, sin router**, adaptado de la arquitectura multiagente de JYDE ADR-0015:

- **4 agentes especializados**, cada uno una clase con prompt propio (pequeño, acotado a una
  tarea), que consume `IChatCompletion` y parsea la respuesta a la defensiva
  (`JsonText.FirstJsonObject`, robusto ante prosa alrededor, vallas ``` ``` y JSON duplicado — mismo
  patrón que JYDE):
  1. **`RequirementExtractorAgent`** — texto del documento → lista de requerimientos (código, texto,
     área funcional).
  2. **`RequirementEvaluatorAgent`** — un requerimiento → 5 criterios de la rúbrica (score 1-5 +
     observación).
  3. **`StoryWriterAgent`** — requerimiento aprobado → historias de usuario con criterios de
     aceptación.
  4. **`TestCaseWriterAgent`** — una historia → un caso de prueba (precondiciones, pasos, resultado
     esperado).
- **`AnalysisOrchestrator`** (caso de uso) invoca los agentes en orden fijo, sin capa de decisión
  intermedia:

  ```
  ExtractText → ExtractorAgent → por cada requerimiento:
      EvaluatorAgent → si Passed: StoryWriterAgent → por cada historia: TestCaseWriterAgent
  → persistir Analysis → done
  ```

  Emite eventos incrementales por SSE: `status` → `requirement` → `evaluation` → `story` →
  `testcase` → … → `done` | `error`.
- **No se introduce `IAgentRouter`** (ni equivalente): el flujo no es conversacional ni depende de
  interpretar intención de usuario — es un pipeline de transformación de datos con un orden fijo.
  Añadir un router aquí sería indirección sin beneficio: no hay nada que enrutar.
- **Guardrails** (no negociables, igual espíritu que JYDE "nunca inventa cifras"):
  - Si un requerimiento **no pasa** el umbral (`Evaluation.Passed = false`), se declara el motivo
    (las observaciones de la rúbrica) y **no se generan historias** para ese requerimiento.
  - Si el LLM falla o el JSON no es parseable tras el reintento defensivo, el análisis completo
    termina en `Status = Failed` con evento `error`; **nunca se inventa contenido** para rellenar el
    hueco.
- Umbral de aprobación configurable vía **`Analysis:PassThreshold`** (default `3.5`); el dominio
  recibe el umbral como parámetro y no lee configuración directamente (mantiene `Domain` puro).

## Consecuencias

- **Positivas:** pipeline predecible y fácil de razonar (mismo orden siempre); prompts pequeños y
  especializados (menor costo de tokens, menor tasa de error que un prompt monolítico); cada agente
  se testea de forma aislada con `IChatCompletion` doble; el orquestador se testea end-to-end con
  dobles verificando orden de eventos, guardrail de no-aprobado y manejo de fallo intermedio; sin
  pieza de enrutamiento que mantener ni el costo de tokens que implicaría un router LLM.
- **Negativas / trade-offs:** si en el futuro se necesitara un flujo no lineal (p. ej. reintentar
  solo un requerimiento, o agregar un tipo de análisis distinto que no siga esta secuencia), el
  pipeline fijo no lo soporta sin cambios estructurales — se acepta porque el alcance actual es un
  único flujo de análisis.
- **Seguimiento:** si se agrega un segundo tipo de flujo de análisis, evaluar en ese momento si vale
  la pena introducir una capa de selección (no antes, YAGNI).

## Alternativas consideradas

- **Router LLM o por reglas (`IAgentRouter`, como JYDE ADR-0015)** — resuelve "qué agente atiende
  esta consulta en lenguaje natural"; no aplica aquí porque no hay consulta en lenguaje natural que
  interpretar, el flujo es siempre el mismo. Se descarta por indirección sin beneficio.
- **Un único prompt monolítico** (extraer + evaluar + generar todo en una llamada) — más simple de
  cablear, pero frágil (un solo JSON gigante y propenso a error), caro en tokens por intento, y
  difícil de testear paso a paso. Se descarta, igual razón que JYDE.
- **Agentes como pasos de una máquina de estados explícita** — mismo resultado con más ceremonia;
  el orquestador secuencial en código ya expresa el flujo con claridad suficiente para 4 pasos.
