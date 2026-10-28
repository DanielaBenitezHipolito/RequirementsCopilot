# Deuda técnica

Registro de diferimientos conscientes (regla del proyecto: todo diferimiento se documenta aquí
con su condición de disparo).

## Entrada multimodal de imágenes (ADR-0007, 2026-10-28)

| Deuda | Disparo para pagarla |
| --- | --- |
| Las imágenes del chat **no se persisten** en el historial de conversaciones (solo el marcador `[+N imagen(es)]`): al reabrir una conversación no se ven las capturas. | Cuando reabrir conversaciones con contexto visual sea un caso de uso real (requiere almacenamiento de blobs, p. ej. GridFS/Blob Storage). |
| **Sin redimensionado/recompresión** de imágenes antes de enviarlas al LLM: una foto de 5 MB viaja completa en base64. | Si los costos de visión o los límites de payload de Foundry se vuelven un problema. |
| Imágenes en **headers/footers de DOCX** se ignoran (solo se extraen las del cuerpo principal). | Si llegan documentos reales con mockups en encabezados. |
| En PDF, los códecs de imagen que no son PNG/JPEG (JBIG2, CCITT…) **se omiten en silencio**. | Si usuarios reportan imágenes faltantes en el análisis de un PDF. |
| Sin **OCR de respaldo**: un PDF escaneado depende de que la visión del modelo lea el escaneo; no hay extracción de texto local. | Si la calidad de lectura de escaneos por visión resulta insuficiente. |

## Contexto acumulado por proyecto (ADR-0009, 2026-10-28)

| Deuda | Disparo para pagarla |
| --- | --- |
| El apéndice de casos de uso se **trunca** al tope de contexto conservando los primeros (cronológico); no hay resumen inteligente. | Proyectos con decenas de casos de uso donde el truncado deje fuera información relevante → resumir con un agente. |
| Los documentos importados al proyecto (`POST /projects/{nombre}/documents`) **ignoran las imágenes** embebidas: solo se anexa el texto. | Si los Word de casos de uso existentes dependen de sus mockups para entenderse. |
