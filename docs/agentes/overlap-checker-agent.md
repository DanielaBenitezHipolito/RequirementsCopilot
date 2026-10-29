# OverlapCheckerAgent (`overlap-checker-agent`)

**Cuándo corre:** por cada requerimiento extraído (documento o chat), solo si el proyecto elegido
ya tiene casos de uso definidos (el contexto trae el bloque "CASOS DE USO YA DEFINIDOS").

**Entrada:** contexto del proyecto + `Requerimiento nuevo {codigo} (área {area}):\n{texto}`.

**Prompt (system):** fuente de verdad en [`prompts/overlap-checker-agent.md`](../../prompts/overlap-checker-agent.md) (ADR-0008).

**Salida esperada:** `{"solapado":true|false,"con":"nombre del caso de uso"|null,"motivo":"..."|null}`.

**Comportamiento:** best effort — una respuesta inválida o un fallo del agente se tratan como
"sin solapamiento" y el análisis continúa. Si solapa, el requerimiento guarda la advertencia
(`solapamiento` en el DTO) y el SSE emite el evento `overlap`.
