# Despliegue monolito: un solo contenedor sirve la API .NET + el frontend compilado.
# Mismo origen => sin CORS, una sola URL, SSE funciona directo.

# --- Frontend (Vite) ---
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
# Mismo origen: la API se llama con rutas relativas (/api/...).
ENV VITE_API_BASE_URL=""
RUN npm run build

# --- Backend (.NET) ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/RequirementsCopilot.Domain/RequirementsCopilot.Domain.csproj src/RequirementsCopilot.Domain/
COPY src/RequirementsCopilot.Application/RequirementsCopilot.Application.csproj src/RequirementsCopilot.Application/
COPY src/RequirementsCopilot.Infrastructure/RequirementsCopilot.Infrastructure.csproj src/RequirementsCopilot.Infrastructure/
COPY src/RequirementsCopilot.Api/RequirementsCopilot.Api.csproj src/RequirementsCopilot.Api/
RUN dotnet restore src/RequirementsCopilot.Api/RequirementsCopilot.Api.csproj
COPY src/ src/
RUN dotnet publish src/RequirementsCopilot.Api/RequirementsCopilot.Api.csproj -c Release -o /app/publish
# El SPA compilado va a wwwroot para que ASP.NET lo sirva.
COPY --from=web /web/dist /app/publish/wwwroot

# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "RequirementsCopilot.Api.dll"]
