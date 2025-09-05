# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .

# 👇 ADICIÓN: detecta automáticamente el primer .csproj y lo usa en restore/publish
# (busca hasta 3 niveles por si el proyecto está en subcarpeta)
RUN set -eux; \
    PROJECT=$(find . -maxdepth 3 -name "*.csproj" | head -n 1); \
    echo ">> Usando proyecto: $PROJECT"; \
    dotnet restore "$PROJECT"; \
    dotnet publish "$PROJECT" -c Release -o /out --no-restore

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
