# ADR 0005 — Frontend Vite + React 19 + Zustand + Tailwind

- **Estado:** Aceptado
- **Fecha:** 2026-07-14
- **Decisores:** Equipo Requirements Copilot

## Contexto

El frontend tiene 3 vistas (Analizar, Historial, Detalle) y un requisito central: consumir un stream
SSE que emite eventos incrementales (`status`, `requirement`, `evaluation`, `story`, `testcase`,
`done`/`error`) y reflejarlos en pantalla en vivo mientras el pipeline de agentes corre en el
backend. El proyecto hereda las decisiones de stack de frontend de JYDE.OpenDataCopilot
(ADR-0008 Vite+React, ADR-0009 estado con Zustand, ADR-0016 Vitest), que ya resolvieron los mismos
problemas de build, estado y testing para un frontend con streaming similar.

## Decisión

- **Vite + React 19 + TypeScript** como base del proyecto (`web/`).
- **Zustand** para estado global: un store con el análisis en curso (eventos SSE acumulándose) y el
  historial. Se prefiere sobre Context/Redux por su API mínima (sin providers, sin boilerplate de
  reducers) para un estado que es esencialmente una lista de eventos acumulados + una selección
  actual.
- **Tailwind CSS** para estilos, vía el plugin oficial de Vite (`@tailwindcss/vite`).
- **Sin react-router**: solo 3 vistas y la navegación entre ellas es un caso simple de estado local
  (qué vista está activa), no un árbol de rutas con parámetros, guards o deep-linking. Añadir un
  router para 3 pantallas es complejidad sin beneficio (YAGNI) — se reconsiderará si el número de
  vistas o la necesidad de URLs directas crece.
- **SSE consumido con `fetch` streaming + parser propio** (no `EventSource` nativo): `EventSource`
  no permite añadir headers (p. ej. si se necesitara auth) ni cuerpo en la petición POST que dispara
  el análisis (multipart file), así que se lee el stream con `fetch` + `ReadableStream` y se
  parsean los eventos `event:`/`data:` a mano — mismo enfoque que JYDE para su Copilot conversacional.
- **Vitest** para tests (store de Zustand + parseo/reducción de eventos SSE), consistente con
  ADR-0016 de JYDE.

## Consecuencias

- **Positivas:** stack ya validado en otro proyecto de la familia (menor riesgo, menos decisiones
  nuevas); Zustand mantiene el manejo de eventos incrementales simple de testear (acciones puras
  sobre el store); sin router no hay rutas que sincronizar con el estado del análisis en curso;
  Tailwind evita CSS a medida para un UI de tamaño acotado; `fetch` streaming da control total sobre
  headers y body, necesario porque la petición que abre el stream es un `POST multipart`.
- **Negativas / trade-offs:** sin router, no hay URLs profundas para compartir un análisis
  específico (p. ej. `/analisis/{id}`) — aceptable para el alcance actual (herramienta interna, sin
  necesidad de compartir enlaces); el parser SSE manual es más código que usar `EventSource`, pero
  es la única opción viable dado que el POST inicial lleva un archivo.
- **Seguimiento:** si se necesita compartir análisis por URL o crece el número de vistas, evaluar
  introducir `react-router` en ese momento (no antes).

## Alternativas consideradas

- **`EventSource` nativo** — más simple, pero no soporta `POST` con body ni headers custom; el
  endpoint de análisis recibe un archivo multipart y devuelve el stream en la misma respuesta, lo
  que `EventSource` no puede expresar. Descartado.
- **react-router** — natural para apps con más de un puñado de vistas; con 3 vistas y navegación
  simple, es una dependencia y una capa de configuración (rutas, layouts) que no se usa lo
  suficiente para justificarse. Se descarta por ahora (YAGNI), no se cierra la puerta a futuro.
- **Redux / Context API** — Redux es más ceremonia (actions, reducers, slices) de la que este
  alcance necesita; Context obliga a providers y no da selectores eficientes para actualizaciones
  frecuentes (evento por evento del SSE). Zustand cubre ambos casos con menos código.
