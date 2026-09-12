# Imagen de ejecucion de la API.
#
# El binario NO se compila aqui: llega ya publicado. El CONTEXTO de construccion
# es la carpeta de publicacion (la salida de `dotnet publish -c Release`), no la
# raiz del repositorio:
#
#   dotnet publish src/Presentation/IngenIA365ERP.API -c Release -o artifacts/api
#   docker build -f src/Presentation/IngenIA365ERP.API/Dockerfile -t api artifacts/api
#
# (tools/scripts/construir-imagenes.ps1 hace las tres imagenes de una vez.)
#
# Hasta el 2026-09-13 este Dockerfile traia el SDK y compilaba el grafo entero
# dentro de la imagen, y el pipeline lo hacia tres veces en serie (API, Web,
# Migrator) despues de haber compilado lo mismo para las pruebas: 24 minutos de
# docker-build aun para un commit que solo tocaba documentacion. Compilar una sola
# vez en el runner y empaquetar el resultado baja la construccion de cada imagen a
# segundos, y elimina la clase entera de fallos "compila fuera pero no dentro"
# (la lista de .csproj desactualizada, el restore del publish que no bajaba
# blazor.web.js, el SDK del contenedor distinto del de desarrollo).
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Usuario sin privilegios: si alguien logra ejecucion remota, no es root.
RUN useradd --uid 10001 --create-home --shell /usr/sbin/nologin appuser \
 && mkdir -p /app/storage/attachments /app/logs \
 && chown -R 10001:10001 /app
COPY --chown=10001:10001 . .

# COPY de un contexto vacio o equivocado NO falla: la imagen se construiria sin la
# aplicacion y moriria al arrancar, ya desplegada. Se comprueba aqui.
RUN test -f /app/IngenIA365ERP.API.dll \
 || { echo "ERROR: el contexto no es la carpeta de publicacion de la API (falta IngenIA365ERP.API.dll)."; exit 1; }

USER 10001

EXPOSE 8080
# TZ=UTC: los contenedores SIEMPRE en UTC. La conversion a America/Bogota ocurre
# solo en presentacion/reportes (evita cierres contables corridos 5 horas).
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    TZ=UTC

ENTRYPOINT ["dotnet", "IngenIA365ERP.API.dll"]
