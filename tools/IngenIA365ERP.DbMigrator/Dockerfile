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

# ── Etapa 1: compilacion ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Los .csproj primero, para aprovechar la cache de restore.
COPY IngenIA365ERP.slnx ./
COPY src/Core/IngenIA365ERP.Domain/IngenIA365ERP.Domain.csproj src/Core/IngenIA365ERP.Domain/
COPY src/Core/IngenIA365ERP.Application/IngenIA365ERP.Application.csproj src/Core/IngenIA365ERP.Application/
COPY src/Infrastructure/IngenIA365ERP.Persistence/IngenIA365ERP.Persistence.csproj src/Infrastructure/IngenIA365ERP.Persistence/
COPY src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer/IngenIA365ERP.Persistence.Migrations.SqlServer.csproj src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer/
COPY src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/IngenIA365ERP.Persistence.Migrations.PostgreSql.csproj src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/
COPY src/Infrastructure/IngenIA365ERP.Audit/IngenIA365ERP.Audit.csproj src/Infrastructure/IngenIA365ERP.Audit/
COPY tools/IngenIA365ERP.DbMigrator/IngenIA365ERP.DbMigrator.csproj tools/IngenIA365ERP.DbMigrator/

RUN dotnet restore tools/IngenIA365ERP.DbMigrator/IngenIA365ERP.DbMigrator.csproj

COPY src/ src/
COPY tools/IngenIA365ERP.DbMigrator/ tools/IngenIA365ERP.DbMigrator/

RUN dotnet publish tools/IngenIA365ERP.DbMigrator/IngenIA365ERP.DbMigrator.csproj \
      -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Etapa 2: ejecucion ──
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
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
