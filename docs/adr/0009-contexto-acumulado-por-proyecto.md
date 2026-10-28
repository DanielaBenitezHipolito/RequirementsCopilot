# ADR 0009 — Contexto acumulado por proyecto: casos de uso previos como memoria

- **Estado:** Aceptado (implementado)
- **Fecha:** 2026-10-28
- **Decisores:** Equipo Requirements Copilot

## Contexto

El contexto de un proyecto era únicamente su `.md` subido a mano. Los casos de uso generados
quedaban guardados en los análisis (con `Analysis.ProjectName`), pero ningún agente los veía:
cada caso de uso nuevo nacía "desde cero", repitiendo funcionalidad, renombrando actores y sin
consistencia con los anteriores. Para un proyecto nuevo (sin sistema existente que describir) ni
siquiera se podía crear el proyecto: el `.md` con contenido era obligatorio.

## Decisión

1. **Apéndice automático de casos de uso** en el bloque de contexto: `ProjectContextLoader`
   consulta los análisis del proyecto (`IAnalysisRepository.GetByProjectAsync`, nuevo) y anexa un
   resumen compacto de cada caso de uso ya generado (nombre, objetivo, actores) con la instrucción
   de no duplicarlos y mantener actores/terminología consistentes. Como todos los agentes ya
   reciben ese bloque (evaluador, clarificador, redactor de casos de uso, historias y el primer
   turno del chat), la memoria del proyecto llega a todo el pipeline sin cambiar firmas.
2. **Proyecto vacío**: `Project.Create` admite contenido vacío y `POST /api/projects` acepta solo
   `nombre` (sin archivo). Un proyecto nuevo arranca sin descripción y su contexto se construye
   con los casos de uso que genere.
3. **Importar casos de uso existentes**: `POST /api/projects/{nombre}/documents` extrae el texto
   de un Word/PDF/TXT/MD (reutiliza `IDocumentTextExtractor`) y lo anexa al contenido del proyecto
   bajo el encabezado `## Importado de «archivo»`.
4. **Control de costos**: el apéndice respeta `Analysis:MaxProjectContextChars` (60k por defecto);
   con muchos casos de uso se conservan los primeros en orden cronológico.

## Alternativas consideradas

- **Re-generar el `.md` del proyecto con cada caso de uso** (escribir la memoria en el archivo):
  mezcla contenido curado por el usuario con contenido generado y duplica la fuente de verdad —
  los casos de uso ya viven en los análisis. Rechazada; el apéndice se deriva en cada carga.
- **Resumir los casos de uso con un agente** antes de inyectarlos: una llamada LLM extra por
  análisis; el resumen estructurado (nombre/objetivo/actores) alcanza. Diferido hasta que el
  tope de contexto lo exija (registrado en tech-debt).

## Consecuencias

- Las imágenes embebidas en documentos importados al proyecto se ignoran (solo texto); el
  análisis multimodal (ADR-0007) aplica a los análisis, no al contexto del proyecto.
- Con decenas de casos de uso el apéndice se trunca (quedan los primeros); si eso duele en la
  práctica, el siguiente paso es el resumen por agente (ver tech-debt).
- Borrar un análisis (hoy no hay endpoint) sacaría sus casos de uso del contexto: la memoria es
  derivada, no copiada.
