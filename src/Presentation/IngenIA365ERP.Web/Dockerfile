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
      ! -name 'global.json' \
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

# COMPROBACION QUE ROMPE LA CONSTRUCCION A PROPOSITO.
#
# App.razor arranca la aplicacion con @Assets["_framework/blazor.web.js"]. Ese
# script NO es un archivo del proyecto: llega en el paquete
# microsoft.aspnetcore.app.internal.assets, cuya version resuelve el SDK. Si el
# SDK del contenedor difiere del de desarrollo, el publish sale SIN el y sin
# ninguna advertencia.
#
# El resultado es la peor clase de fallo: la imagen se construye, el contenedor
# arranca, / responde HTTP 200 con el HTML del shell, la sonda de disponibilidad
# lo da por sano... y el navegador muestra una pantalla en blanco porque pide el
# script de arranque y recibe un 404. Paso exactamente eso en produccion.
#
# Comprobar el manifiesto convierte ese silencio en un fallo de construccion.
RUN test -f /app/publish/IngenIA365ERP.Web.staticwebassets.endpoints.json \
 && grep -q 'blazor\.web\.js' /app/publish/IngenIA365ERP.Web.staticwebassets.endpoints.json \
 || { echo "ERROR: el publish no incluye _framework/blazor.web.js."; \
      echo "La aplicacion arrancaria y mostraria una pantalla en blanco."; \
      echo "Revisar que el SDK del contenedor cumpla el global.json."; \
      exit 1; }

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
