# syntax=docker/dockerfile:1
# ---------- Etapa 1: compilación ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los archivos de proyecto: el restore queda en caché mientras no cambien las dependencias.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/UsuariosRoles.Domain/UsuariosRoles.Domain.csproj src/UsuariosRoles.Domain/
COPY src/UsuariosRoles.Application/UsuariosRoles.Application.csproj src/UsuariosRoles.Application/
COPY src/UsuariosRoles.Infrastructure/UsuariosRoles.Infrastructure.csproj src/UsuariosRoles.Infrastructure/
COPY src/UsuariosRoles.Api/UsuariosRoles.Api.csproj src/UsuariosRoles.Api/
RUN dotnet restore src/UsuariosRoles.Api/UsuariosRoles.Api.csproj

COPY src/ src/
RUN dotnet publish src/UsuariosRoles.Api/UsuariosRoles.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Etapa 2: runtime (sin SDK, más pequeña) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .

# Usuario sin privilegios incluido en las imágenes oficiales de .NET (UID 1654).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "UsuariosRoles.Api.dll"]
