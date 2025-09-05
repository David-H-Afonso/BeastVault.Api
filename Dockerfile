# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .

# --- DIAGNÓSTICO (para ver por qué falla en Actions) ---
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

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet","BeastVault.Api.dll"]
