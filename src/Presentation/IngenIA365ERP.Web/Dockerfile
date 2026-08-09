# ── Stage 1: Build ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar slnx y csproj primero para cache de restore
COPY IngenIA365ERP.slnx ./
COPY src/Core/IngenIA365ERP.Domain/IngenIA365ERP.Domain.csproj src/Core/IngenIA365ERP.Domain/
COPY src/Core/IngenIA365ERP.Application/IngenIA365ERP.Application.csproj src/Core/IngenIA365ERP.Application/
COPY src/Infrastructure/IngenIA365ERP.Persistence/IngenIA365ERP.Persistence.csproj src/Infrastructure/IngenIA365ERP.Persistence/
COPY src/Infrastructure/IngenIA365ERP.Identity/IngenIA365ERP.Identity.csproj src/Infrastructure/IngenIA365ERP.Identity/
COPY src/Infrastructure/IngenIA365ERP.Audit/IngenIA365ERP.Audit.csproj src/Infrastructure/IngenIA365ERP.Audit/
COPY src/Infrastructure/IngenIA365ERP.Caching/IngenIA365ERP.Caching.csproj src/Infrastructure/IngenIA365ERP.Caching/
COPY src/Infrastructure/IngenIA365ERP.Legacy/IngenIA365ERP.Legacy.csproj src/Infrastructure/IngenIA365ERP.Legacy/
# Feature 004: ensamblados de migraciones por proveedor (referenciados por la API,
# que forma parte del grafo de restore de la solucion).
COPY src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer/IngenIA365ERP.Persistence.Migrations.SqlServer.csproj src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer/
COPY src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/IngenIA365ERP.Persistence.Migrations.PostgreSql.csproj src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/
COPY src/Presentation/IngenIA365ERP.API/IngenIA365ERP.API.csproj src/Presentation/IngenIA365ERP.API/
COPY src/Presentation/IngenIA365ERP.Shared/IngenIA365ERP.Shared.csproj src/Presentation/IngenIA365ERP.Shared/
COPY src/Presentation/IngenIA365ERP.Web/IngenIA365ERP.Web.csproj src/Presentation/IngenIA365ERP.Web/
COPY src/Presentation/IngenIA365ERP.Web.Client/IngenIA365ERP.Web.Client.csproj src/Presentation/IngenIA365ERP.Web.Client/
COPY src/Presentation/IngenIA365ERP.App/IngenIA365ERP.App.csproj src/Presentation/IngenIA365ERP.App/
COPY tools/IngenIA365ERP.DataMigrator/IngenIA365ERP.DataMigrator.csproj tools/IngenIA365ERP.DataMigrator/
COPY tools/IngenIA365ERP.DbMigrator/IngenIA365ERP.DbMigrator.csproj tools/IngenIA365ERP.DbMigrator/
COPY tests/IngenIA365ERP.Domain.Tests/IngenIA365ERP.Domain.Tests.csproj tests/IngenIA365ERP.Domain.Tests/
COPY tests/IngenIA365ERP.Application.Tests/IngenIA365ERP.Application.Tests.csproj tests/IngenIA365ERP.Application.Tests/
COPY tests/IngenIA365ERP.API.IntegrationTests/IngenIA365ERP.API.IntegrationTests.csproj tests/IngenIA365ERP.API.IntegrationTests/
COPY tests/IngenIA365ERP.Architecture.Tests/IngenIA365ERP.Architecture.Tests.csproj tests/IngenIA365ERP.Architecture.Tests/

RUN dotnet restore src/Presentation/IngenIA365ERP.Web/IngenIA365ERP.Web.csproj

# Copiar todo el codigo fuente
COPY src/ src/

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
