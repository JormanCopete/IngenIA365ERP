# ── Etapa 0: manifiestos de proyecto ──
# Ver la nota extensa en el Dockerfile de la API: se copian todos los .csproj
# derivandolos del contexto en vez de enumerarlos, porque la lista manual se
# desactualiza en silencio y rompe el restore dentro de la imagen.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS manifests
WORKDIR /src
COPY . .
RUN find . -type f \
      ! -name '*.csproj' ! -name '*.slnx' ! -name '*.sln' \
      ! -name '*.props' ! -name '*.targets' \
      ! -iname 'nuget.config' ! -name 'packages.lock.json' \
      -delete \
 && find . -mindepth 1 -type d -empty -delete

# ── Stage 1: Build ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY --from=manifests /src .
RUN dotnet restore src/Presentation/IngenIA365ERP.Web/IngenIA365ERP.Web.csproj

# Codigo fuente. legacy/ no es opcional: IngenIA365ERP.Legacy referencia
# ERP.Core, que vive ahi.
COPY src/ src/
COPY tools/ tools/
COPY legacy/ legacy/

# Publicar Web (incluye Shared y Web.Client)
# Nota: NO se instala el workload wasm-tools a proposito. Con el, el publish de
# Blazor hace relinking nativo con emscripten (necesita Python y mucha RAM). Sin
# el se usa el runtime WebAssembly prearmado: build mas rapido y sin dependencias.
WORKDIR /src/src/Presentation/IngenIA365ERP.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# El publish "hosted" del host deja el index.html con el marcador de fingerprint
# SIN resolver (#[.{fingerprint}]); el publish del Client si lo resuelve. Se publica
# el Client y se usa su index.html ya resuelto (los hashes son deterministas por
# contenido, asi que coinciden).
WORKDIR /src
RUN dotnet publish src/Presentation/IngenIA365ERP.Web.Client/IngenIA365ERP.Web.Client.csproj \
      -c Release -o /app/client --no-restore \
 && if [ -f /app/client/wwwroot/index.html ]; then \
      cp /app/client/wwwroot/index.html /app/publish/wwwroot/index.html; \
    fi

# ── Stage 2: Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Usuario sin privilegios: si alguien logra ejecucion remota, no es root.
RUN useradd --uid 10001 --create-home --shell /usr/sbin/nologin appuser
COPY --from=build --chown=10001:10001 /app/publish .
USER 10001

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    TZ=UTC

ENTRYPOINT ["dotnet", "IngenIA365ERP.Web.dll"]
