# Backend .NET 8 (API) — para Render / Railway / cualquier host de contenedores.
# El frontend (web/) se despliega aparte en Vercel; este Dockerfile NO lo incluye.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore: se copian solo los .csproj + props para cachear la capa de dependencias.
COPY Directory.Build.props ./
COPY src/RequirementsCopilot.Domain/RequirementsCopilot.Domain.csproj src/RequirementsCopilot.Domain/
COPY src/RequirementsCopilot.Application/RequirementsCopilot.Application.csproj src/RequirementsCopilot.Application/
COPY src/RequirementsCopilot.Infrastructure/RequirementsCopilot.Infrastructure.csproj src/RequirementsCopilot.Infrastructure/
COPY src/RequirementsCopilot.Api/RequirementsCopilot.Api.csproj src/RequirementsCopilot.Api/
RUN dotnet restore src/RequirementsCopilot.Api/RequirementsCopilot.Api.csproj

# Build + publish
COPY src/ src/
RUN dotnet publish src/RequirementsCopilot.Api/RequirementsCopilot.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
# Producción por defecto; el host inyecta PORT y las variables de configuración.
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "RequirementsCopilot.Api.dll"]
