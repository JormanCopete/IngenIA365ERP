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
#
# El binario NO se compila aqui: llega ya publicado. El CONTEXTO de construccion
# es la carpeta de publicacion, no la raiz del repositorio (ver la nota extensa en
# el Dockerfile de la API):
#
#   dotnet publish tools/IngenIA365ERP.DbMigrator -c Release -o artifacts/migrator
#   docker build -f tools/IngenIA365ERP.DbMigrator/Dockerfile -t migrator artifacts/migrator

# Base aspnet y NO runtime: aunque el DbMigrator es una app de consola, su grafo
# de referencias (Application, Persistence, Identity) declara el framework
# Microsoft.AspNetCore.App. Con la base "runtime" la imagen COMPILA pero al
# ejecutarse muere con "No frameworks were found" — y lo haria dentro del Job
# PreSync de produccion, con el despliegue ya en marcha.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Usuario sin privilegios: este contenedor tiene credenciales de BD con permisos
# de DDL, asi que es el ultimo lugar donde queremos root.
RUN useradd --uid 10001 --create-home --shell /usr/sbin/nologin migrator
COPY --chown=10001:10001 . .

# Un contexto vacio o equivocado no hace fallar el COPY; se comprueba aqui.
RUN test -f /app/IngenIA365ERP.DbMigrator.dll \
 || { echo "ERROR: el contexto no es la carpeta de publicacion del DbMigrator (falta IngenIA365ERP.DbMigrator.dll)."; exit 1; }

USER 10001

# La configuracion llega por variables de entorno (Database__Provider,
# Database__ConnectionStrings__*, etc.); no hay appsettings en la imagen.
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    TZ=UTC

ENTRYPOINT ["dotnet", "IngenIA365ERP.DbMigrator.dll"]
CMD ["migrate", "--scope", "all"]
