# ADR 0007 — Entrada multimodal: imágenes en documentos y en el chat

- **Estado:** Aceptado (implementado; requiere verificar visión en los agentes publicados en Foundry)
- **Fecha:** 2026-10-28
- **Decisores:** Equipo Requirements Copilot

## Contexto

Los documentos de requerimientos reales traen mockups, capturas de pantalla y diagramas pegados
en el Word/PDF. Hasta ahora el pipeline era 100 % texto:

- `DocxTextExtractor` y `PdfTextExtractor` **descartaban las imágenes en silencio** (OpenXML
  `InnerText` y PdfPig `page.Text` solo devuelven texto).
- Un **PDF escaneado** (imagen pura) extraía `""` y el análisis seguía con un documento vacío,
  sin ningún aviso.
- El **chat del entrevistador** solo aceptaba texto: no se podía pegar una captura.
- El adaptador `FoundryChatCompletion` hardcodeaba `input_text`, aunque la Responses API de
  Azure AI Foundry acepta `input_image` en el mismo arreglo `content` y `gpt-5-mini` tiene visión.

## Decisión

Entrada multimodal de punta a punta usando `input_image` de la Responses API, con las imágenes
viajando como **data URL base64** (sin almacenamiento intermedio de blobs):

1. **Contrato del puerto:** `ChatPrompt` gana `Images` (lista opcional de `ImageAttachment`
   `{MimeType, Base64Data}`). `FoundryChatCompletion` emite un `input_image` por imagen junto al
   `input_text`. `FakeChatCompletion` las ignora (sigue siendo determinista).
2. **Extracción:** `IDocumentTextExtractor` devuelve `DocumentContent {Text, Images}`.
   - DOCX: imágenes del cuerpo principal (`MainDocumentPart.ImageParts`), solo formatos con
     soporte de visión (png, jpeg, gif, webp).
   - PDF: `page.GetImages()`; PNG decodificado por PdfPig o bytes crudos si son JPEG; otros
     códecs se omiten.
   - **Imagen suelta** (`.png .jpg .jpeg .webp .gif`) como archivo de análisis: texto vacío +
     la imagen misma (`ImageFileExtractor`).
3. **Orquestador:** pasa las imágenes al `requirement-extractor-agent` con un aviso en el texto
   del prompt; si el documento no tiene **ni texto ni imágenes**, el análisis falla con mensaje
   claro (antes: análisis de un documento vacío).
4. **Chat:** `POST /api/conversations/messages` acepta `imagenes[]` (data URLs); el
   entrevistador las recibe en el turno. El frontend permite adjuntar y pegar (Ctrl+V) capturas.
5. **Topes (control de costos):** 10 imágenes por documento (`AnalysisOptions.MaxImages`),
   4 por mensaje de chat, 5 MB por imagen en el frontend; el límite de archivo sigue en 10 MB.

## Alternativas consideradas

- **OCR local (Tesseract u otro):** dependencia nueva y calidad irregular en español; la visión
  del modelo ya lee texto en imágenes. Rechazada.
- **Agente dedicado que describa la imagen a texto antes del pipeline:** una llamada extra de
  latencia y pérdida de detalle; los agentes existentes pueden ver la imagen directamente.

## Consecuencias

- **Acción manual en el portal Foundry:** los agentes publicados `requirement-extractor-agent` y
  `requirement-builder-agent` deben (a) usar un modelo con visión (gpt-5-mini la tiene) y
  (b) actualizar su prompt para instruir el análisis de imágenes adjuntas
  (ver [docs/agentes](../agentes/README.md)).
- El costo por análisis sube cuando hay imágenes (tokens de visión).
- Formatos emf/wmf/tiff embebidos en Word se omiten (sin soporte de visión).
- El historial de conversaciones persiste solo un marcador `[+N imagen(es)]`, no la imagen
  (registrado en [tech-debt](../tech-debt.md)).
