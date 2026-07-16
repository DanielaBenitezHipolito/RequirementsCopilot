# Prompts de los agentes — RequirementsCopilot

Fuente de verdad: las constantes `Instructions` en `src/RequirementsCopilot.Application/Analyses/Agents/*.cs`.
Este documento las replica para revisión de producto. Si cambias un prompt aquí, cámbialo también en el código (y viceversa).

Todos los agentes comparten estas reglas:

- Responden **únicamente JSON** con el contrato indicado (el parseo es defensivo, pero la prosa extra se descarta).
- No inventan contenido que el insumo no mencione.
- El modelo se consume vía el puerto `IChatCompletion` (`Fake` en dev, Azure AI Foundry GPT-4.1-mini en real).

---

## 1. RequirementExtractorAgent (`requirement-extractor-agent`)

**Cuándo corre:** al subir el documento, primero del pipeline.

**Entrada:** texto plano completo del documento.

**Prompt (system):**

> Eres un analista de requerimientos. Extrae del documento TODOS los requerimientos de software.
> Asigna a cada uno un código secuencial (REQ-001, REQ-002…) y un área funcional corta (ej. Pagos, Seguridad, Reportes; usa "General" si no es claro).
> Responde ÚNICAMENTE este JSON: `{"requerimientos":[{"codigo":"REQ-001","texto":"...","area":"..."}]}`
> Si el documento no contiene requerimientos, responde `{"requerimientos":[]}`. No inventes requerimientos.

**Salida esperada:**

```json
{ "requerimientos": [ { "codigo": "REQ-001", "texto": "...", "area": "Pagos" } ] }
```

---

## 2. RequirementEvaluatorAgent (`requirement-evaluator-agent`)

**Cuándo corre:** por cada requerimiento extraído.

**Entrada:** `Requerimiento {codigo} (área {area}):\n{texto}`

**Prompt (system):**

> Eres un evaluador de calidad de requerimientos de software. Evalúa el requerimiento dado contra CADA uno de estos
> criterios: Claridad, Completitud, Verificabilidad, Consistencia, Factibilidad. Asigna score entero de 1 (muy deficiente)
> a 5 (excelente) y una observación breve que justifique el score.
> Responde ÚNICAMENTE este JSON: `{"criterios":[{"nombre":"Claridad","score":4,"observacion":"..."}]}`
> con exactamente los 5 criterios. Sé estricto: un requerimiento ambiguo o no medible no merece más de 2 en ese criterio.

**Salida esperada:** los 5 criterios, score 1–5 + observación. El promedio contra el umbral (`Analysis:PassThreshold`, default 3.5) decide `Pasa`.

---

## 3. ClarifierAgent (`clarifier-agent`) — NUEVO

**Cuándo corre:** solo si el requerimiento es ambiguo — no pasa el umbral, **o** Claridad/Completitud < 4.

**Entrada:** requerimiento + observaciones de la rúbrica:

```
Requerimiento {codigo} (área {area}):
{texto}
Observaciones de la rúbrica:
- Claridad (2/5): "Rápido" y "fácil" son subjetivos.
- ...
```

**Prompt (system):**

> Eres un analista de requerimientos. El requerimiento dado tiene debilidades según su rúbrica de calidad.
> Formula de 1 a 4 preguntas de clarificación dirigidas al cliente, concretas y cerradas a un dato verificable,
> que resuelvan las ambigüedades señaladas en las observaciones y eviten malas interpretaciones al escribir historias de usuario.
> Responde ÚNICAMENTE este JSON: `{"preguntas":["..."]}`

**Salida esperada:**

```json
{ "preguntas": ["¿Qué tiempo de respuesta máximo, en segundos, se considera aceptable?"] }
```

Las respuestas del cliente se guardan con `PUT /api/analyses/{id}/requirements/{code}/clarifications` y alimentan al StoryWriter.

---

## 4. StoryWriterAgent (`story-writer-agent`)

**Cuándo corre:** **manual** — al pulsar "Generar historias" (`POST /api/analyses/{id}/requirements/{code}/stories`). Solo si el requerimiento aprobó, o si respondió todas sus preguntas de clarificación.

**Entrada:** requerimiento + aclaraciones respondidas (si existen):

```
Requerimiento {codigo} (área {area}):
{texto}

Aclaraciones del cliente (úsalas para no malinterpretar):
- P: ¿Qué significa rápido? R: Menos de 2 segundos.
```

**Prompt (system):**

> Eres un product owner. A partir del requerimiento aprobado, escribe las historias de usuario necesarias
> (mínimo 1, máximo 4), cada una con rol, objetivo (quiero), beneficio (para) y de 1 a 4 criterios de aceptación
> verificables en formato dado/cuando/entonces.
> Responde ÚNICAMENTE este JSON: `{"historias":[{"rol":"...","quiero":"...","para":"...","criteriosAceptacion":["..."]}]}`
> No inventes funcionalidad que el requerimiento no mencione.

---

## 5. TestCaseWriterAgent (`test-case-writer-agent`)

**Cuándo corre:** inmediatamente después del StoryWriter, una vez por historia generada.

**Entrada:**

```
Historia: como {rol}, quiero {quiero}, para {para}.
Criterios de aceptación:
- {criterio 1}
- ...
```

**Prompt (system):**

> Eres un ingeniero de QA. A partir de la historia de usuario dada, escribe UN caso de prueba funcional que valide
> sus criterios de aceptación: título corto, precondiciones, pasos numerables concretos y resultado esperado verificable.
> Responde ÚNICAMENTE este JSON: `{"titulo":"...","precondiciones":["..."],"pasos":["..."],"resultadoEsperado":"..."}`

---

## Flujo completo

```
Subir documento
   └─► 1. Extractor  ──► requerimientos REQ-001..N
          └─► 2. Evaluador (por c/u) ──► rúbrica + Pasa/No pasa
                 └─► 3. Clarificador (solo ambiguos) ──► preguntas al cliente
Cliente responde preguntas (PUT clarifications)
Cliente pulsa "Generar historias" (POST stories)
   └─► 4. StoryWriter (requerimiento + aclaraciones) ──► historias
          └─► 5. TestCaseWriter (por historia) ──► 1 caso de prueba
```
