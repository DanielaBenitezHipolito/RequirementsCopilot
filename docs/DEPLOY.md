# Despliegue — demo pública (Vercel + Render)

Arquitectura: **frontend en Vercel**, **backend .NET en Render** (Docker), **Mongo en Atlas** (opcional).
Vercel NO puede hospedar el backend .NET (no corre servidores de larga vida ni SSE de minutos).

## 0. Subir el repo a GitHub

```bash
cd "D:\Repositories\Daniela Benitez\RequirementsCopilot"
git remote add origin https://github.com/<org-o-usuario>/requirements-copilot.git
git push -u origin feature/requirements-copilot-mvp
```
`appsettings.Development.json` (con secretos) ya está en `.gitignore`; verifica que NO aparezca en el push.

## 1. Backend en Render

1. [render.com](https://render.com) → New → **Web Service** → conecta el repo de GitHub.
2. Render detecta `render.yaml` (Blueprint) o configúralo a mano:
   - Runtime: **Docker**, Dockerfile: `./Dockerfile`, Branch: `feature/requirements-copilot-mvp`, Plan: Free.
   - Health Check Path: `/api/analyses`.
3. Variables de entorno (Environment):
   - **Demo sin credenciales (recomendado para presentar):** `Providers__Chat=Fake`, `Providers__AnalysisRepository=InMemory`. No necesita Mongo ni Foundry.
   - **IA real:** `Providers__Chat=Foundry`, `Foundry__Endpoint=…`, `Foundry__ApiKey=…`, `Foundry__Chat__Model=gpt-5-mini`, y el catálogo `Foundry__Chat__Agents__<agente>__Name` / `__Version` por cada agente; `Providers__AnalysisRepository=Mongo`, `Mongo__ConnectionString=…`, `Mongo__Database=…`.
   - `Cors__Origin` = la URL de Vercel del paso 2 (se rellena después; puedes poner un placeholder y editarlo).
4. Deploy. Copia la URL pública (ej. `https://requirements-copilot-api.onrender.com`).
   > Plan free: el servicio duerme tras ~15 min de inactividad; el primer request tarda ~30-60s en despertar. Aceptable para demo.

## 2. Frontend en Vercel

1. [vercel.com](https://vercel.com) → New Project → importa el mismo repo.
2. **Root Directory: `web`** (importante). Framework: Vite (autodetectado por `web/vercel.json`).
3. Environment Variable: `VITE_API_BASE_URL` = la URL de Render del paso 1 (sin barra final).
4. Deploy. Copia la URL de Vercel (ej. `https://requirements-copilot.vercel.app`).

## 3. Cerrar el círculo (CORS)

En Render, pon `Cors__Origin` = la URL exacta de Vercel del paso 2 → Redeploy del backend.
(Se admite lista separada por `;` si usas varios dominios/preview.)

## Notas
- Mongo Atlas: si usas Mongo, en Atlas → Network Access permite la IP saliente de Render (o `0.0.0.0/0` solo para la demo) y usa un usuario con rol `readWrite`.
- Foundry real requiere que los agentes estén **publicados** en el portal (`docs/prompts-agentes.md`); con `Providers__Chat=Fake` no hace falta nada.
- Secretos SOLO como variables de entorno del host, nunca en el repo.
