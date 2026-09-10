# ADR 0006 — Historias de usuario con estimaciones como paso posterior al requerimiento

- **Estado:** Aceptado (implementado; agente pendiente de publicar en Foundry)
- **Fecha:** 2026-08-26
- **Decisores:** Equipo Requirements Copilot

## Contexto

En una conversación real, el entrevistador (`requirement-builder-agent`) terminó pidiendo fechas
de puesta en producción y generando estimaciones de esfuerzo (semanas, FTEs) e historias de
usuario. Eso está fuera de su alcance: su salida no entra al pipeline (evaluación, clarificación,
caso de uso), las respuestas largas truncaban el JSON del turno, y una estimación hecha a mitad
de entrevista —sin requerimiento evaluado ni contexto de proyecto— no es confiable.

El equipo sí quiere la capacidad: generar el backlog de historias con estimaciones a partir de un
requerimiento ya trabajado.

## Decisión

1. **Restringir el prompt del entrevistador** (hecho en
   [requirement-builder-agent.md](../agentes/requirement-builder-agent.md); debe replicarse en el
   agente publicado en Foundry): solo construye el requerimiento; si le piden estimaciones,
   historias o cronogramas, responde que eso se genera después desde el requerimiento aprobado.

2. **Nuevo agente `user-story-writer-agent`** (prompt propuesto en
   [user-story-writer-agent.md](../agentes/user-story-writer-agent.md)), invocado **manualmente**
   por requerimiento listo — mismo patrón que el caso de uso:

   - **Endpoint:** `POST /api/analyses/{id}/requirements/{code}/user-stories`.
   - **Precondición:** requerimiento aprobado o con clarificaciones respondidas
     (`ReadyForStories`, la misma regla del caso de uso).
   - **Input:** texto del requerimiento + aclaraciones respondidas + caso de uso (si existe) +
     contexto del proyecto (`ProjectContextLoader`) — así las historias no re-inventan lo que el
     sistema ya resuelve (permisos, adjuntos, exportes…).
   - **Output JSON:** lista de historias
     `{titulo, como, quiero, para, criteriosAceptacion[], puntos, dependencias[]}` con `puntos`
     en escala Fibonacci (1,2,3,5,8,13). **Sin fechas ni días-hombre:** los puntos son
     complejidad relativa; convertirlos a tiempo depende de la velocidad del equipo, que la IA no
     conoce. La UI mostrará la leyenda "estimación orientativa generada por IA, no es un
     compromiso de entrega".
   - **Dominio y persistencia:** `UserStory` en `Domain`, lista en `Requirement`
     (`SetUserStories`), espejo en `RequirementDocument` (Mongo) y en los DTO.
   - **UI:** sección "Historias de usuario" en el detalle del requerimiento, junto al caso de
     uso, con exportación a Word/PDF reutilizando `useCaseDoc.ts`.

## Consecuencias

- **Positivas:** cada agente conserva una sola responsabilidad (ADR-0003); las historias salen de
  un requerimiento evaluado y con contexto, no de una charla a medias; el chat deja de producir
  respuestas kilométricas que rompen el contrato JSON.
- **Negativas / trade-offs:** un paso manual más para el usuario; nuevo agente que publicar y
  mantener en Foundry; los puntos Fibonacci sin días-hombre pueden decepcionar a quien esperaba
  fechas (decisión consciente: la IA no debe prometer plazos).
- **Seguimiento:** publicar el agente en Foundry, implementar backend + UI, y actualizar la tabla
  de [README de agentes](../agentes/README.md) cuando pase a Aceptado.

## Alternativas consideradas

- **Dejar que el entrevistador estime en el chat** — descartada: sin evaluación ni contexto la
  estimación es humo, y las respuestas largas rompen el turno JSON.
- **Generar historias automáticamente al aprobar el requerimiento** — descartada: costo LLM en
  requerimientos donde nadie las necesita; el patrón del caso de uso (generación manual) ya
  demostró funcionar.
- **Estimar en días-hombre además de puntos** — descartada por ahora: exige conocer velocidad y
  composición del equipo; se puede agregar como parámetro del equipo más adelante.
