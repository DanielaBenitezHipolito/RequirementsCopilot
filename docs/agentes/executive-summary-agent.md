# ExecutiveSummaryAgent (`executive-summary-agent`) — NUEVO

**Cuándo corre:** al final del pipeline de análisis (`AnalyzeAsync`), tras evaluar todos los requerimientos y
solo si hubo al menos uno — antes de marcar el análisis como `Completed`. Su fallo NO tumba el análisis:
si el LLM no responde JSON válido, el análisis se completa igual, sin resumen ni evento `summary`.

**Entrada:** JSON compacto construido en código, con las observaciones de score ≤3 (máx. 2 por requerimiento):

```json
{
  "archivo": "spec.pdf",
  "umbral": 3.5,
  "requerimientos": [
    { "codigo": "REQ-001", "area": "Pagos", "promedio": 4.2, "pasa": true, "observacionesClave": [] },
    { "codigo": "REQ-002", "area": "General", "promedio": 2.1, "pasa": false,
      "observacionesClave": ["\"Rápido\" y \"fácil\" son subjetivos.", "No define métricas ni alcance."] }
  ]
}
```

**Prompt (system):**

> Eres un analista senior de auditoría de requerimientos. A partir del JSON con los requerimientos evaluados
> de un documento (código, área, promedio, si pasa el umbral, y sus observaciones clave de mayor debilidad),
> redacta un resumen ejecutivo de la auditoría dirigido a un responsable de proyecto no técnico: estructura
> general del documento, calidad global de los requerimientos, vacíos o ambigüedades principales, y una
> recomendación concreta de siguiente paso. Entre 120 y 180 palabras, tono ejecutivo asegurador (claro,
> profesional, sin jerga técnica innecesaria). No inventes datos que el JSON no contenga.
> Responde ÚNICAMENTE este JSON: `{"resumen":"..."}`

**Salida esperada:**

```json
{ "resumen": "El documento presenta once requerimientos, de los cuales ocho cumplen el umbral de calidad..." }
```
