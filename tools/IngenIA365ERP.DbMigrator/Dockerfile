# Imagen del DbMigrator: aplica migraciones y ejecuta seeds sin levantar la API.
#
# La usa el Job PreSync de Argo CD en produccion, donde Database__AutoMigrate=false
# y la API se niega a arrancar con migraciones pendientes. Tambien sirve para
# generar los scripts idempotentes que revisa el DBA.
#
# Uso:
#   docker run ... migrator migrate --scope all
#   docker run ... migrator seed --category parametric --scope all
#   docker run ... migrator script --provider PostgreSQL

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

# ── Etapa 1: compilacion ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY --from=manifests /src .
RUN dotnet restore tools/IngenIA365ERP.DbMigrator/IngenIA365ERP.DbMigrator.csproj

# Codigo fuente. legacy/ no es opcional: IngenIA365ERP.Legacy referencia
# ERP.Core, que vive ahi.
COPY src/ src/
COPY tools/ tools/
COPY legacy/ legacy/

RUN dotnet publish tools/IngenIA365ERP.DbMigrator/IngenIA365ERP.DbMigrator.csproj \
      -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Etapa 2: ejecucion ──
# Base aspnet y NO runtime: aunque el DbMigrator es una app de consola, su grafo
# de referencias (Application, Persistence, Identity) declara el framework
# Microsoft.AspNetCore.App. Con la base "runtime" la imagen COMPILA pero al
# ejecutarse muere con "No frameworks were found" — y lo haria dentro del Job
# PreSync de produccion, con el despliegue ya en marcha.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Usuario sin privilegios: este contenedor tiene credenciales de BD con permisos
# de DDL, asi que es el ultimo lugar donde queremos root.
RUN useradd --uid 10001 --create-home --shell /usr/sbin/nologin migrator
COPY --from=build --chown=10001:10001 /app/publish .
USER 10001

# La configuracion llega por variables de entorno (Database__Provider,
# Database__ConnectionStrings__*, etc.); no hay appsettings en la imagen.
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    TZ=UTC

ENTRYPOINT ["dotnet", "IngenIA365ERP.DbMigrator.dll"]
CMD ["migrate", "--scope", "all"]
