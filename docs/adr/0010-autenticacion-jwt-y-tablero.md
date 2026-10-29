# ADR 0010 — Autenticación JWT con usuarios/roles y tablero de consumo

- **Estado:** Aceptado (implementado)
- **Fecha:** 2026-10-29
- **Decisores:** Equipo Requirements Copilot

## Contexto

La API estaba completamente abierta: cualquiera con la URL podía leer, editar y borrar proyectos,
análisis y usuarios del negocio. Además no había visibilidad del consumo de tokens del LLM
(con imágenes y contexto acumulado el costo por análisis creció).

## Decisión

1. **JWT con clave simétrica**, activado por configuración: la autenticación se habilita al
   definir `Auth__JwtKey` (≥ 32 caracteres). Sin clave, la API queda abierta (demo local y
   pruebas automatizadas siguen sin fricción). Con clave, **todo** endpoint exige token
   (FallbackPolicy) salvo `POST /api/auth/login`, `GET /api/auth/status` y `GET /api/health`
   (health check anónimo para Render; `render.yaml` apunta ahí).
2. **Usuarios con dos roles**: `Admin` (administra usuarios y ve el tablero) y `Analista`
   (operación normal). Contraseñas con **PBKDF2** (SHA-256, sal por usuario, 100k iteraciones,
   formato autocontenible `iter.sal.hash`). Admin inicial sembrado al arrancar con
   `Auth__AdminEmail`/`Auth__AdminPassword` si el repositorio de usuarios está vacío.
3. **Nueva dependencia** (gobierno de librerías): `Microsoft.AspNetCore.Authentication.JwtBearer`
   8.0.8 en el proyecto Api — paquete oficial de Microsoft, sin alternativas razonables en
   stdlib para validar JWT. La emisión usa `System.IdentityModel.Tokens.Jwt` (transitivo).
4. **Registro de consumo**: `FoundryChatCompletion` guarda los tokens reportados por la
   Responses API (`usage.input_tokens/output_tokens`) por agente y modelo (`ILlmUsageRepository`,
   colección `llm_usage`; best effort, nunca tumba la llamada). `GET /api/admin/usage` (solo
   Admin) agrega por total, agente y día; el frontend lo muestra en la pestaña **Tablero**.
5. **Frontend**: pantalla de login cuando `/api/auth/status` reporta habilitado; el token viaja
   en `Authorization: Bearer` en todas las llamadas; 401 devuelve al login. Pestañas Usuarios y
   Tablero visibles solo para Admin.

## Alternativas consideradas

- **ASP.NET Identity**: arrastra EF Core y páginas que no se usan; desproporcionado. Rechazada.
- **Proveedor externo (Entra ID)**: candidato natural a futuro en Howden, pero exige registro
  de aplicación y tenant; el negocio necesita cerrar la API ya. Diferido (tech-debt).

## Consecuencias

- **Para habilitar en Render**: `Auth__JwtKey` (secreto ≥ 32 chars), `Auth__AdminEmail`,
  `Auth__AdminPassword`. Sin esas variables el despliegue sigue abierto como hasta ahora.
- El modo demo (`Providers__Chat=Fake`) no registra consumo: el tablero muestra ceros.
- Los endpoints de usuarios y tablero requieren autenticación: con auth deshabilitado responden
  401 y el frontend oculta sus pestañas.
- SSE (análisis y chat) funciona igual: van por fetch con header Authorization, no EventSource.
