# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .

# --- Diagnóstico + build explícito sobre el .csproj de la raíz ---
# (si tu .csproj se llama distinto, cambia el nombre)
RUN set -eux; \
    echo "Contenido en /src:"; ls -la; \
    dotnet --info; \
    echo ">> RESTORE"; dotnet restore ./BeastVault.Api.csproj -v minimal; \
    echo ">> PUBLISH"; dotnet publish ./BeastVault.Api.csproj -c Release -o /out --no-restore -v minimal

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /out .

# Crear directorios para persistencia de datos
RUN mkdir -p /app/data
RUN mkdir -p /app/pokemon
RUN mkdir -p /app/pokemon/backup

# Configurar volúmenes para persistencia de datos
VOLUME ["/app/data", "/app/pokemon"]

# Variables de entorno para configuración de rutas (configurables desde CasaOS)
ENV BEASTVAULT_DB_PATH=/app/data/beastvault.db
ENV BEASTVAULT_POKEMON_PATH=/app/pokemon

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Puerto configurable
EXPOSE 8080

ENTRYPOINT ["dotnet","BeastVault.Api.dll"]
