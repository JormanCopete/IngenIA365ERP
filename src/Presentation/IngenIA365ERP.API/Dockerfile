# ── Etapa 0: manifiestos de proyecto ──
# Enumerar los .csproj a mano era una fuente constante de fallos: cada proyecto
# nuevo, o cada referencia transitiva que nadie recordo agregar, rompia el
# restore DENTRO de la imagen aunque la solucion compilara bien fuera (asi se
# perdieron corridas por Storage y por legacy/ERP.Core). Esta etapa copia el
# contexto y se queda solo con los manifiestos, de modo que restore los vea
# todos, hoy y cuando se agregue el proximo proyecto.
#
# No sacrifica la cache: si solo cambia codigo .cs, el resultado de esta etapa es
# identico byte a byte y BuildKit reutiliza la capa de restore.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS manifests
WORKDIR /src
COPY . .
RUN find . -type f \
      ! -name '*.csproj' ! -name '*.slnx' ! -name '*.sln' \
      ! -name '*.props' ! -name '*.targets' \
      ! -iname 'nuget.config' ! -name 'packages.lock.json' \
      ! -name 'global.json' \
      -delete \
 && find . -mindepth 1 -type d -empty -delete

# ── Stage 1: Build ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY --from=manifests /src .
RUN dotnet restore src/Presentation/IngenIA365ERP.API/IngenIA365ERP.API.csproj

# Codigo fuente. legacy/ no es opcional: IngenIA365ERP.Legacy referencia
# ERP.Core, que vive ahi.
COPY src/ src/
COPY tools/ tools/
COPY legacy/ legacy/

# Publicar
WORKDIR /src/src/Presentation/IngenIA365ERP.API
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Usuario sin privilegios: si alguien logra ejecucion remota, no es root.
RUN useradd --uid 10001 --create-home --shell /usr/sbin/nologin appuser \
 && mkdir -p /app/storage/attachments /app/logs \
 && chown -R 10001:10001 /app
COPY --from=build --chown=10001:10001 /app/publish .
USER 10001

EXPOSE 8080
# TZ=UTC: los contenedores SIEMPRE en UTC. La conversion a America/Bogota ocurre
# solo en presentacion/reportes (evita cierres contables corridos 5 horas).
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    TZ=UTC

ENTRYPOINT ["dotnet", "IngenIA365ERP.API.dll"]
