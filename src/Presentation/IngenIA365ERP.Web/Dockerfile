# Imagen de ejecucion del Web (Blazor Web App: servidor + cliente WebAssembly).
#
# El binario NO se compila aqui: llega ya publicado. El CONTEXTO de construccion
# es la carpeta de publicacion, no la raiz del repositorio (ver la nota extensa en
# el Dockerfile de la API):
#
#   dotnet publish src/Presentation/IngenIA365ERP.Web -c Release -o artifacts/web
#   docker build -f src/Presentation/IngenIA365ERP.Web/Dockerfile -t web artifacts/web
#
# Dos cosas del publish que importan y viven ahora en el pipeline
# (.github/workflows/ci.yml, job publish-web):
#   - va SIN --no-restore, porque _framework/blazor.web.js llega en un paquete que
#     el SDK resuelve durante el restore del publish;
#   - se comprueba que el manifiesto de assets incluya blazor.web.js, porque si el
#     SDK no lo genera la app arranca, responde 200 y el navegador queda en blanco.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Usuario sin privilegios: si alguien logra ejecucion remota, no es root.
RUN useradd --uid 10001 --create-home --shell /usr/sbin/nologin appuser
COPY --chown=10001:10001 . .

# Un contexto vacio o equivocado no hace fallar el COPY; se comprueba aqui, y se
# repite la comprobacion del script de arranque por si alguien construye la
# imagen a mano saltandose el pipeline.
RUN test -f /app/IngenIA365ERP.Web.dll \
 || { echo "ERROR: el contexto no es la carpeta de publicacion del Web (falta IngenIA365ERP.Web.dll)."; exit 1; }
RUN test -f /app/IngenIA365ERP.Web.staticwebassets.endpoints.json \
 && grep -q 'blazor\.web\.js' /app/IngenIA365ERP.Web.staticwebassets.endpoints.json \
 || { echo "ERROR: el publish no incluye _framework/blazor.web.js; la aplicacion arrancaria en blanco."; \
      echo "Revisar que el SDK cumpla global.json."; exit 1; }

USER 10001

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    TZ=UTC

ENTRYPOINT ["dotnet", "IngenIA365ERP.Web.dll"]
