# ADR 0008 — Prompts versionados en el repo, sin agentes publicados en Foundry

- **Estado:** Aceptado (implementado)
- **Fecha:** 2026-10-28
- **Decisores:** Equipo Requirements Copilot

## Contexto

Hasta ahora los 7 agentes se invocaban como **agentes publicados** en el portal de Azure AI
Foundry (`agent_reference` + `version` en la Responses API, ADR-0002). El prompt de cada agente
vivía en el portal; el repo solo tenía una copia documental en `docs/agentes/`.

Eso hizo el cambio de prompts en producción operativamente frágil:

- Cambiar un prompt exigía editar y **republicar el agente en el portal** y luego actualizar
  `Foundry__Chat__Agents__<agente>__Version` en Render — dos pasos manuales, fuera del repo,
  sin revisión de código ni historial.
- El repo y el portal se desincronizaban (p. ej. `user-story-writer-agent` quedó sin `Version`
  pinneada en un ambiente).
- ADR-0007 (imágenes) volvió a requerir tocar prompts en el portal, evidenciando el problema.

## Decisión

Los prompts son **código versionado**:

1. **Carpeta `/prompts` en la raíz del repo**: un `.md` por agente, nombre de archivo = código
   lógico del agente (`requirement-extractor-agent.md`, …). Es la **fuente única de verdad**;
   `docs/agentes/` queda como documentación de contratos (cuándo corre, entrada/salida).
2. **El prompt viaja en cada request** como `instructions` de la Responses API. Se elimina
   `agent_reference`/`version`: ya no hay agentes publicados que mantener. El hilo conversacional
   (`previous_response_id`) no cambia.
3. **`FilePromptCatalog`** (Infrastructure) carga la carpeta al arranque: un prompt faltante
   tumba el arranque, no una llamada en producción. El csproj del Api copia `/prompts` al output
   y al publish; el Dockerfile la incluye en el contexto (con excepción en `.dockerignore`).
4. **Config simplificada**: desaparecen `Foundry__Chat__Agents__<agente>__Name/__Version`;
   queda el override opcional de modelo por agente (`__Model`) y el modelo global.

Cambiar un prompt en producción ahora es: editar `prompts/<agente>.md` → commit → deploy.

## Alternativas consideradas

- **Seguir con agentes publicados** y automatizar la publicación por CI: más piezas móviles y
  la API de administración de agentes no aporta nada que el repo no dé. Rechazada.
- **Prompts en variables de entorno**: ilegibles para textos de 20+ líneas y sin diff. Rechazada.

## Consecuencias

- `instructions` se envía (y se cobra) en cada llamada; es el mismo costo que tenía el system
  prompt del agente publicado, así que no hay sobrecosto real.
- Los agentes publicados en el portal Foundry quedan huérfanos: se pueden archivar/borrar.
  Las env vars `__Name/__Version` del dashboard de Render pueden eliminarse.
- `previous_response_id` conserva solo la conversación; como `instructions` viaja en cada
  request, el comportamiento del agente queda determinado por el deploy vigente.
- Actualiza parcialmente a ADR-0002: el proveedor sigue siendo intercambiable por configuración;
  lo que cambia es que el prompt ya no vive en el proveedor.
