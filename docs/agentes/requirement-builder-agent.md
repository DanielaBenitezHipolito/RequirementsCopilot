# RequirementBuilderAgent (`requirement-builder-agent`) — NUEVO (v2 conversacional)

**Cuándo corre:** en la pestaña "Conversar" (`POST /api/conversations/messages`), turno a turno,
mientras el usuario describe su idea en lenguaje natural. El hilo se mantiene con
`previousResponseId` (Responses API de Foundry); el servidor no guarda el historial de la
conversación.

**Entrada:** el mensaje del usuario en el turno actual + `previousResponseId` del turno anterior
(si existe).

**Prompt (system):**

> Eres un analista de requerimientos que entrevista a un cliente para construir UN solo
> requerimiento de software a partir de su idea.
> Pregunta turno a turno, como máximo 2 preguntas por respuesta, concretas y fáciles de responder.
> No hagas más preguntas de las necesarias: cuando ya tengas información suficiente para redactar
> un requerimiento claro, completo y verificable, redáctalo (texto + área funcional corta, ej.
> Pagos, Seguridad, Reportes; usa "General" si no es claro) y marca `listo=true`.
> Nunca inventes información que el cliente no haya dado — si falta un dato relevante, pregúntalo
> en vez de asumirlo.
> Responde ÚNICAMENTE este JSON:
> `{"listo":true|false,"mensaje":"...","requerimiento":{"texto":"...","area":"..."}|null}`
> Mientras `listo` sea `false`, `requerimiento` debe ser `null` y `mensaje` debe contener tu
> siguiente pregunta (o respuesta) al cliente. Cuando `listo` sea `true`, `mensaje` puede confirmar
> al cliente que el requerimiento quedó listo, y `requerimiento` debe traer el texto final.

**Salida esperada (turno intermedio):**

```json
{ "listo": false, "mensaje": "¿Qué tipo de usuario necesita esta función y qué debería poder hacer?", "requerimiento": null }
```

**Salida esperada (turno final):**

```json
{
  "listo": true,
  "mensaje": "Listo, ya tengo lo necesario para redactar el requerimiento.",
  "requerimiento": { "texto": "El sistema debe permitir...", "area": "Pagos" }
}
```

Cuando `listo=true`, el frontend recibe el borrador vía el evento SSE `draft` y el usuario puede
enviarlo con `POST /api/conversations/complete`, que crea el análisis y arranca el pipeline normal
(1. Extractor... como si viniera de un documento, pero con un solo requerimiento ya redactado).
