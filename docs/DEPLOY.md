# Despliegue — demo pública (Render, un solo servicio)

Monolito: **un contenedor** sirve la API .NET **y** el frontend compilado (Vite → `wwwroot`).
Mismo origen ⇒ sin CORS, una sola URL, el SSE funciona directo. Mongo en Atlas es opcional.

## 1. Subir el repo a GitHub

```bash
cd "D:\Repositories\Daniela Benitez\RequirementsCopilot"
git remote add origin https://github.com/DanielaBenitezHipolito/RequirementsCopilot.git
git push -u origin feature/requirements-copilot-mvp
```
`appsettings.Development.json` (con secretos) está en `.gitignore`; confirma que NO aparezca en el push.

## 2. Servicio en Render

1. [render.com](https://render.com) → New → **Web Service** → conecta el repo de GitHub.
2. Render detecta `render.yaml` (Blueprint). Si lo configuras a mano:
   - Runtime: **Docker**, Dockerfile: `./Dockerfile`, Branch: `feature/requirements-copilot-mvp`, Plan: Free.
   - Health Check Path: `/api/analyses`.
3. Variables de entorno:
   - **Demo sin credenciales (recomendado para presentar):** `Providers__Chat=Fake`, `Providers__AnalysisRepository=InMemory`. Nada más.
   - **IA real:** `Providers__Chat=Foundry`, `Foundry__Endpoint=…`, `Foundry__ApiKey=…`, `Foundry__Chat__Model=gpt-5-mini`, y por cada agente publicado `Foundry__Chat__Agents__<agente>__Name` / `__Version`; `Providers__AnalysisRepository=Mongo`, `Mongo__ConnectionString=…`, `Mongo__Database=…`.
4. Deploy. La URL pública (ej. `https://requirements-copilot.onrender.com`) sirve **todo**: abre la raíz y ya está el front; el front llama a `/api/...` en el mismo dominio.

## Notas
- **Plan free:** el servicio duerme tras ~15 min de inactividad; el primer request tarda ~30-60s en despertar. Despiértalo con un request un minuto antes de presentar.
- **Mongo Atlas** (si lo usas): Network Access → permite la IP saliente de Render (o `0.0.0.0/0` solo para la demo); usuario con rol `readWrite`.
- **Foundry real:** requiere los agentes **publicados** en el portal (`docs/prompts-agentes.md`). Con `Providers__Chat=Fake` no hace falta nada.
- Secretos SOLO como variables de entorno del host, nunca en el repo.
- El frontend se compila con `VITE_API_BASE_URL=""` (rutas relativas) dentro del Dockerfile; no hay que configurarlo.

## ¿Front y back separados? (alternativa)
El monolito es lo más simple para una demo. Si más adelante quieres escalarlos por separado (front en Vercel/Render Static + back en Render Docker), habría que volver a exponer `VITE_API_BASE_URL` y configurar `Cors__Origin` con la URL del front. Para esta demo no hace falta.
