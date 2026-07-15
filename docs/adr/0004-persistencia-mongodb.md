# ADR 0004 — Persistencia en MongoDB

- **Estado:** Aceptado
- **Fecha:** 2026-07-14
- **Decisores:** Equipo Requirements Copilot

## Contexto

El resultado de un análisis (`Analysis`) es un agregado con forma de árbol de profundidad variable:
un documento tiene N requerimientos, cada requerimiento tiene una evaluación de 5 criterios y (si
pasa) M historias de usuario, cada historia tiene un caso de prueba con listas de precondiciones y
pasos. El número de requerimientos, historias y pasos por documento no es fijo ni se conoce de
antemano.

Necesitamos persistir y recuperar este agregado completo (para el historial y el detalle), sin
necesidad de consultarlo por sus partes internas (no hay reportes tipo "todas las historias con tal
criterio" fuera del propio análisis) ni de transacciones multi-entidad — el agregado se guarda de
una vez al terminar el pipeline.

## Decisión

- **MongoDB**, colección **`analyses`** (`MongoDB.Driver`).
- El documento persistido es el **agregado `Analysis` completo**, mapeado explícitamente a
  **`AnalysisDocument`** (y sub-documentos equivalentes para `Requirement`, `Evaluation`,
  `UserStory`, `TestCase`) — **el dominio no se serializa directo**. Esto evita acoplar los tipos de
  dominio (con sus factory methods y invariantes) a atributos o convenciones del driver de Mongo, y
  deja un punto único de traducción si el esquema de persistencia diverge del modelo de dominio.
- Puerto **`IAnalysisRepository`** en `Application` (`SaveAsync`, `GetByIdAsync`, `GetAllAsync`),
  implementado por `MongoAnalysisRepository` en `Infrastructure/Mongo`.
- Selección de adaptador por configuración: **`Providers:AnalysisRepository = InMemory | Mongo`**
  (default `InMemory`) — el adaptador `InMemory` permite correr el backend y los tests sin una
  instancia real de Mongo.
- Configuración: `Mongo:ConnectionString`, `Mongo:Database` (secrets fuera del repo).

## Consecuencias

- **Positivas:** el esquema documental de Mongo encaja de forma natural con un agregado de forma
  arbórea y variable, sin necesidad de modelar tablas normalizadas ni JOINs para reconstruir el
  árbol completo; guardar/leer el agregado es una operación atómica por documento; `InMemory` permite
  desarrollo y tests sin infraestructura externa; cambiar de motor de persistencia es configuración,
  no reescritura de `Application`.
- **Negativas / trade-offs:** sin soporte nativo de transacciones multi-documento (no se necesita:
  un análisis se guarda de una vez) ni de JOINs para futuras consultas relacionales entre análisis;
  si más adelante se necesitan reportes agregados cruzando muchos análisis, Mongo requeriría
  agregaciones (`$match`/`$group`) en vez de SQL; se acepta porque el alcance actual no incluye
  reportes.
- **Seguimiento:** si se agregan consultas por campos internos del árbol (p. ej. "todos los
  requerimientos de un área con score bajo"), evaluar índices sobre los sub-documentos en ese
  momento.

## Alternativas consideradas

- **SQL Server** — motor ya usado en otros proyectos del equipo, pero exige un esquema rígido
  (tablas normalizadas para requerimientos → evaluaciones → historias → casos, con FKs) para un
  árbol de profundidad y cardinalidad variable; reconstruir el agregado completo implicaría varios
  JOINs por cada lectura. Se descarta por el desajuste entre la forma del dato y el modelo
  relacional.
- **Serializar el dominio directo (sin `AnalysisDocument`)** — menos código de mapeo, pero acopla las
  entidades de dominio a atributos/convenciones de `MongoDB.Driver` y hace frágil cualquier cambio
  de esquema de persistencia que no deba afectar al dominio. Se descarta a favor del mapeo explícito.
- **Almacenamiento en archivos planos (JSON en disco)** — suficiente para un prototipo muy pequeño,
  pero sin concurrencia segura ni consultas por id/lista; se descarta frente a `InMemory` (para dev)
  + `Mongo` (para producción), que ya cubren ambos casos sin ese riesgo.
