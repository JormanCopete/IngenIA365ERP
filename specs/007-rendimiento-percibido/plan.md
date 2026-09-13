# Implementation Plan: Rendimiento percibido

**Branch**: `develop` | **Date**: 2026-09-13 | **Spec**: [spec.md](spec.md)

## Summary

Tres rondas. La **ronda 1** (esta) es la que no cambia contratos ni modelo de datos salvo
índices: pipeline de CI, empaquetado, huellas de estáticos, paquetes Syncfusion por
componente, CPU explícita en Kubernetes, Serilog obediente, IP real del visitante,
`no-store` en `/api`, viajes en paralelo en nómina, indicador de arranque, fuente
autoalojada, `/health` en la Web, ReadyToRun en API y migrador, e índices de lectura.
La **ronda 2** exige spec propia por ítem (contratos de Contabilidad/Inventario,
paginación en servidor, caché de catálogos, estimado de novedades, Kestrel temprano,
siembra selectiva). La **ronda 3** son mediciones tras la ronda 1 (auditoría diferida,
GC de la Web, contador de rate limit en Redis).

## Technical Context

**Language/Version**: .NET 10.0.3xx (global.json), Blazor Web App InteractiveWebAssembly
**Dependencies nuevas**: `Serilog.Sinks.Async`; paquetes `Syncfusion.Blazor.*` 33.2.8 por
componente (sustituyen al meta-paquete)
**Infra**: k3s en un nodo de 6 CPU / 12 GB por ambiente; Cloudflare Tunnel → Traefik →
pods; Argo CD (DEV/QA automático, PDN manual)
**Testing**: `dotnet test` de las cuatro suites sin contenedores; integración con Docker
para `no-store`; verificación en QA por `curl` al pod (Cloudflare Access impide el navegador)

## Constitution Check

- **I (spec antes de código)**: esta carpeta. Los ítems de ronda 2 llevan spec propia.
- **IV (una base por cooperativa)**: nada de aquí cachea datos de cooperativa fuera de su
  base; la caché de catálogos se difirió precisamente por esto.
- **IX (errores visibles)**: `Task.WhenAll` conserva el manejo de error de cada rama;
  `blockWhenFull: true` en el sink asíncrono para no perder un `LogError`.
- **XI (inmutabilidad contable)**: los índices no cambian datos; el `PUT` de comprobantes
  se retira en ronda 2, no se «arregla».
- **XII (cambios destructivos)**: la migración `IndicesDeLectura` sólo crea índices; se
  aplica con el Job PreSync tras `pg_dump` como toda migración de producción.

## Project Structure (archivos tocados en la ronda 1)

```
.github/workflows/ci.yml                         # compilar una vez, matriz docker, sin caché GHA
IngenIA365ERP.CI.slnf                            # filtro de solución sin MAUI ni Docker
src/Presentation/*/Dockerfile, tools/IngenIA365ERP.DbMigrator/Dockerfile   # una etapa
tools/scripts/construir-imagenes.ps1
src/Presentation/IngenIA365ERP.Shared/IngenIA365ERP.Shared.csproj (+Web, Web.Client, App)
src/Presentation/IngenIA365ERP.Web/Components/App.razor            # @Assets, Core, fuente, bundle
src/Presentation/IngenIA365ERP.Web/Program.cs                     # /health
src/Presentation/IngenIA365ERP.API/Program.cs, appsettings.json, csproj   # Serilog, IP, R2R
src/Presentation/IngenIA365ERP.API/Services/IpAddressAccessor.cs
src/Presentation/IngenIA365ERP.API/Middleware/SecurityHeadersMiddleware.cs
src/Presentation/IngenIA365ERP.Shared/Pages/Security/Login.razor, Layout/MainLayout.razor
src/Presentation/IngenIA365ERP.Shared/wwwroot/{css,fonts}
src/Presentation/IngenIA365ERP.Shared/Pages/Nomina/*.razor, Components/Nomina/SelectorDePeriodo.razor
src/Infrastructure/IngenIA365ERP.Persistence/Configurations/{Lending,Accounting}/*.cs
src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/*IndicesDeLectura*
ingenia365-gitops/workloads/erp/base/{api,web,dependencias}.yaml, overlays/pdn/disponibilidad.yaml
```

## Decisiones

| Decisión | Alternativa descartada | Por qué |
|---|---|---|
| Publicar en el runner y Dockerfiles de una etapa | `scope` por imagen + `mode=min` en la caché GHA | Elimina la causa (recompilar tres veces) en vez de mitigarla; el Web tarda igual |
| Paquetes Syncfusion por componente | `PublishTrimmed=false`; `TrimMode=full` | El meta-paquete no es recortable; `full` rompe reflexión de Syncfusion |
| Script combinado de Syncfusion desde `Core` con `@Assets` | Borrar el script / `IgnoreScriptIsolation` | Sin `window.sfBlazor` no funciona ninguna pantalla; la opción no existe en 33.2.8. El recorte del script al 25 % va en ronda 2 con el Custom Resource Generator |
| `CF-Connecting-IP` para el limitador | Primer valor de `X-Forwarded-For` | Falsificable si algún día el origen es alcanzable sin túnel; documentada la condición |
| `limits.cpu` explícito por workload | Quitar sólo el default del LimitRange | Un pod sin límite podría ahogar a PostgreSQL en el único nodo |
| Índices sin filtro `IsDeleted` | Índices filtrados por proveedor | Los borrados lógicos son pocos; el filtro exige traducción T-SQL↔PostgreSQL |
| ReadyToRun sólo API y migrador | También Web | El publish del Web ya es el techo del pipeline y comprueba `blazor.web.js`; segunda pasada |
