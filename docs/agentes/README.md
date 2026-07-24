# Prompts de los agentes — RequirementsCopilot

Un archivo por agente en esta carpeta. **Fuente de verdad del proveedor real:** los agentes
publicados en **Azure AI Foundry** (portal). Estos documentos replican sus instrucciones para
revisión de producto y para publicarlos/actualizarlos; si editas un prompt en Foundry, actualiza
también el archivo correspondiente aquí.

Con `Providers:Chat=Fake` no se usan estos prompts (el adaptador `Fake` devuelve datos de ejemplo).

## Reglas comunes a todos los agentes

- Responden **únicamente JSON** con el contrato indicado (el parseo es defensivo: la prosa extra se descarta y se reintenta una vez si no hay JSON).
- No inventan contenido que el insumo no mencione.
- El modelo se consume vía el puerto `IChatCompletion` (`Fake` en dev, Azure AI Foundry `gpt-5-mini` en real).

## Agentes (6)

| Código lógico | Archivo | Rol |
|---|---|---|
| `requirement-extractor-agent` | [requirement-extractor-agent.md](requirement-extractor-agent.md) | Extrae requerimientos por unidad funcional |
| `requirement-evaluator-agent` | [requirement-evaluator-agent.md](requirement-evaluator-agent.md) | Rúbrica de 5 criterios |
| `clarifier-agent` | [clarifier-agent.md](clarifier-agent.md) | Preguntas de clarificación (lenguaje de negocio) |
| `use-case-writer-agent` | [use-case-writer-agent.md](use-case-writer-agent.md) | Redacta el caso de uso (plantilla corporativa) |
| `requirement-builder-agent` | [requirement-builder-agent.md](requirement-builder-agent.md) | Entrevistador conversacional (v2) |
| `executive-summary-agent` | [executive-summary-agent.md](executive-summary-agent.md) | Resumen ejecutivo de la auditoría |

## Publicación en Foundry

Desde la actualización 2026-07-16 (ver `docs/adr/0002-foundry-adaptadores-intercambiables.md`),
**todos** los agentes de este documento se publican en Azure AI Foundry y se invocan por
`agent_reference` (Responses API) en vez de mandar las instrucciones inline. Pasos en el portal
([ai.azure.com](https://ai.azure.com)):

1. **Agents → New agent.**
2. Pegar las instrucciones del agente **tal cual están en este documento** (la sección "Prompt
   (system)" de cada uno).
3. Asignar un modelo — por ejemplo `gpt-5-mini` (gpt-4.1-mini ya no está disponible en el catálogo).
4. **Publish.**
5. Anotar el **nombre** y la **versión** que asigna Foundry al publicar — se necesitan para el
   mapeo en configuración.

El mapeo de código lógico → agente publicado vive en `Foundry:Chat:Agents`:

```json
{
  "Foundry": {
    "Endpoint": "https://<recurso>.services.ai.azure.com/api/projects/<proyecto>",
    "ApiKey": "...",
    "Chat": {
      "Model": "gpt-5-mini",
      "Agents": {
        "requirement-extractor-agent": { "Name": "requirement-extractor-agent", "Version": "1" },
        "requirement-evaluator-agent": { "Name": "requirement-evaluator-agent", "Version": "1" },
        "clarifier-agent": { "Name": "clarifier-agent", "Version": "1" },
        "use-case-writer-agent": { "Name": "use-case-writer-agent", "Version": "1" },
        "requirement-builder-agent": { "Name": "requirement-builder-agent", "Version": "1" },
        "executive-summary-agent": { "Name": "executive-summary-agent", "Version": "1" }
      }
    }
  }
}
```

**Advertencia:** con `Providers:Chat = Foundry`, la fuente de verdad de los prompts pasa a ser lo
publicado en el portal de Foundry, no este documento. Si editas un prompt en Foundry, **actualiza
también este archivo a mano** — ya no hay diff de código que lo garantice.

---

## Flujo completo

```
Subir documento
   └─► 1. Extractor  ──► requerimientos REQ-001..N
          └─► 2. Evaluador (por c/u) ──► rúbrica + Pasa/No pasa
                 └─► 3. Clarificador (solo ambiguos) ──► preguntas al cliente
   └─► 6. ExecutiveSummaryAgent (si hubo ≥1 requerimiento) ──► resumen ejecutivo (evento `summary`)
Ciclo responder → re-evaluar (POST reevaluate):
   Cliente responde preguntas pendientes
      └─► 2. Evaluador CON aclaraciones respondidas ──► nueva rúbrica, reemplaza la evaluación
             └─► si sigue ambiguo: 3. Clarificador CON aclaraciones ──► preguntas NUEVAS (append)
   Se itera hasta aprobar.
Cliente pulsa "Generar historias" (POST stories) — solo si aprobado o todo respondido
   └─► 4. UseCaseWriter (requerimiento + aclaraciones) ──► caso de uso (formato plantilla corporativa)
```
