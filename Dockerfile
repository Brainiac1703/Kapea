# ---------------------------------------------------------------------------
# Imagen de Kapea. Un solo Dockerfile con dos destinos:
#
#   docker build --target api  -t kapea-api:dev  .
#   docker build --target sync -t kapea-sync:dev .
#
# Comparten la fase de compilación, así que construir los dos cuesta poco más
# que construir uno: el restore y el publish se reutilizan entre destinos.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Con gestión centralizada de versiones el restore no funciona sin estos tres
# ficheros: Directory.Packages.props tiene todas las versiones,
# Directory.Build.props las propiedades comunes, y NuGet.config restringe los
# orígenes para que la restauración dentro del contenedor sea reproducible.
COPY Directory.Packages.props Directory.Build.props NuGet.config global.json ./

# Los csproj se copian antes que el código para que la capa de restauración se
# reutilice mientras no cambien las dependencias. Tienen que estar los siete:
# Api y Sync referencian al resto, y el restore falla si alguno no existe.
COPY src/Kapea.Domain/Kapea.Domain.csproj                 src/Kapea.Domain/
COPY src/Kapea.Shared/Kapea.Shared.csproj                 src/Kapea.Shared/
COPY src/Kapea.Application/Kapea.Application.csproj       src/Kapea.Application/
COPY src/Kapea.Infrastructure/Kapea.Infrastructure.csproj src/Kapea.Infrastructure/
COPY src/Kapea.Client/Kapea.Client.csproj                 src/Kapea.Client/
COPY src/Kapea.Api/Kapea.Api.csproj                       src/Kapea.Api/
COPY src/Kapea.Sync/Kapea.Sync.csproj                     src/Kapea.Sync/

RUN dotnet restore src/Kapea.Api/Kapea.Api.csproj \
 && dotnet restore src/Kapea.Sync/Kapea.Sync.csproj

COPY src/ src/

# Sin --no-restore, y no es un descuido: el restore de arriba se ejecuta con solo
# los csproj presentes, y publicar reutilizando ese resultado deja el cliente
# WebAssembly sin wwwroot/_framework. La aplicación arrancaría y respondería 200,
# pero ningún botón funcionaría porque no habría runtime de Blazor en el
# navegador. Los paquetes ya están en la caché, así que cuesta segundos.
RUN dotnet publish src/Kapea.Api/Kapea.Api.csproj -c Release -o /app/api
RUN dotnet publish src/Kapea.Sync/Kapea.Sync.csproj -c Release -o /app/sync

# ---------------------------------------------------------------------------
# Web API. Sirve además los estáticos del cliente WebAssembly, que el publish de
# Kapea.Api ya ha dejado dentro de su wwwroot.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app

# Container Apps enruta al 8080 y termina el TLS en su propio proxy.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/api .

USER $APP_UID

ENTRYPOINT ["dotnet", "Kapea.Api.dll"]

# ---------------------------------------------------------------------------
# Proceso de sincronización. Vive aparte porque su ciclo de vida es distinto
# —ejecución programada, sin tráfico entrante— y para que un fallo suyo hablando
# con Kraken o Bit2Me no arrastre a la API.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS sync
WORKDIR /app

COPY --from=build /app/sync .

USER $APP_UID

ENTRYPOINT ["dotnet", "Kapea.Sync.dll"]
