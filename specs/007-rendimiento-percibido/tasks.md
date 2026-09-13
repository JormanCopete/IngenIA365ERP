# Tasks: Rendimiento percibido

**Input**: [spec.md](spec.md), [plan.md](plan.md). Ronda 1 = quick wins; ronda 2 = con spec propia.

## Phase 1: Despliegue (US1, US2, US3)

- [x] T001 Filtro de solución `IngenIA365ERP.CI.slnf` (sin MAUI ni pruebas con Docker)
- [x] T002 `.github/workflows/ci.yml`: `build-and-test` compila una vez y publica API/Migrator; `publish-web` en paralelo; `docker` en matriz sin caché GHA; `gitops` recoge digests por artefacto; `setup-dotnet` con `global-json-file`
- [x] T003 Dockerfiles de una etapa (API, Web, Migrator) que fallan si el contexto no trae la DLL
- [x] T004 `tools/scripts/construir-imagenes.ps1` para reproducir el flujo en local
- [x] T005 Paquetes Syncfusion por componente en Shared/Web/Web.Client/App; usings sin uso fuera de `_Imports.razor`
- [x] T006 `App.razor` con `@Assets[...]` en todo CSS/JS; script de Syncfusion desde `Syncfusion.Blazor.Core`
- [x] T007 Medir: corrida `34726837245` 9:02; `publish` del Web 341 s; Syncfusion 9,1 MB / 2,35 MB br

## Phase 2: Producción y API (US4, US5)

- [x] T010 GitOps: `limits.cpu` explícito en API (2), Web (1), Mongo (1), Redis (500m); `LimitRange` sin default de CPU; quota `requests.cpu: 4`; sondas sin retardo; Web en `/health` — commit `ab23242` en la rama `rendimiento` del clon local, **pendiente de autorización para empujar**
- [x] T011 [B1] Serilog `ReadFrom.Configuration` + sink asíncrono; archivo sólo fuera del contenedor; overrides en `appsettings.json` (src/Presentation/IngenIA365ERP.API/Program.cs, appsettings.json, csproj)
- [x] T012 [B2] `RealIpHeader = CF-Connecting-IP`; `IpAddressAccessor` con la misma prioridad; `ForwardedHeaders` con red de pods conocida (Program.cs, Services/IpAddressAccessor.cs, Endpoints/CentralAuthModule.cs)
- [x] T013 [B3] `Cache-Control: no-store` por defecto bajo `/api` (Middleware/SecurityHeadersMiddleware.cs) + prueba de integración
- [x] T014 [G1] `PublishReadyToRun` condicionado (`ContainerPublish=true`, `linux-x64`) en API y DbMigrator; `ci.yml` publica con `-r linux-x64 -p:ContainerPublish=true`

## Phase 3: Cliente (US6, US8)

- [x] T020 [C1] Indicador de arranque fuera de `AuthorizeView` con `--blazor-load-percentage`; login con botón e inputs inactivos hasta la hidratación (Login.razor, MainLayout.razor, componentes.css)
- [x] T021 [C2] Inter autoalojada (wwwroot/fonts, `@font-face` en tokens.css, preload en App.razor; sin Google Fonts)
- [x] T022 [C3] Bundle de Shared enlazado directo (un viaje menos en serie)
- [x] T023 [C5] El login no espera a la API en prerender; promociones con timeout de 3 s
- [x] T024 [C6] `/health` en el Web (Program.cs) para las sondas
- [x] T025 [E1] `Task.WhenAll` en Liquidación, Novedades, Períodos, Conceptos, Empleados, SelectorDePeriodo y afines, conservando el error de cada rama

## Phase 4: Base de datos (US7)

- [x] T030 Índices en `DepositEntryConfiguration`, `AccountingDocumentConfiguration`, `AccountBalanceConfiguration`, `JournalEntryConfiguration`
- [x] T031 Migración en par `IndicesDeLectura` con `tools/scripts/add-migration.ps1 -Name IndicesDeLectura -Context Application`
- [x] T032 Suites sin contenedores verdes; `DosContextosUnaTablaTests`/paridad de migraciones

## Phase 5: Despliegue y verificación de la ronda 1

- [x] T040 Merge a `develop` (`bff13d5`, `3b9…` publish-api paralelo), CI verde, QA verificado por `curl` al pod: huellas, `/health` 200, `no-store`, 0 `Executed DbCommand`, login prerenderizado con `disabled`/`readonly`, fuente local, migración `IndicesDeLectura` aplicada en `ingenia365erp` y `coop_prueba`; Started→Ready de la API 44 s (sin sondas nuevas aún)
- [ ] T041 Empujar GitOps `ab23242` (autorización del usuario) y verificar `cpu.max`, `nr_throttled`, Started→Ready en DEV/QA
- [ ] T042 Promover a `release`; `pg_dump` previo; sincronizar PDN; verificar índices creados, logs, IP real en `ADM_CentralUserLoginAttempts`, estilos nuevos
- [ ] T043 Webhook GitHub → Argo CD en nonprod (hoy Argo descubre el commit por sondeo: hubo que refrescar a mano) y Cache Rule de Cloudflare para `/_framework/` (panel; lo hace el propietario) y purga de `_content/Syncfusion.Blazor/scripts/syncfusion-blazor.min.js`
- [ ] T044 Corregir en producción la vigencia base 2026 de nómina (HORAS_MES 220, recargo 0,80, extras 2,05/2,55) — decisión del usuario/contadora

## Phase 6: Ronda 2 (cada una con spec propia)

- [ ] T050 [US9] Contabilidad e Inventario: alinear siete pantallas al contrato real; retirar `PUT`; `by-code` como Queries; `PersonSearchPicker`; `AccountSearchDialog` contra el plan de cuentas real; `@key` en tablas de líneas — **segundo revisor**
- [ ] T051 [US10] `PagedListAdaptor<T>` y paginación en servidor en las grillas grandes; tope de `PageSize` después de la caché de catálogos
- [ ] T052 [US10] Caché de catálogos en el cliente (clave por cooperativa, vaciado al cambiar sesión/cooperativa) + `GET /api/catalogos/{catalogo}/lookup`
- [ ] T053 [US10] Estimado de novedades fuera del listado (`GetNoveltyEstimatesQuery`)
- [ ] T054 [US11] Kestrel escucha durante la verificación de la base (503 con envelope) y siembra selectiva al arrancar
- [ ] T055 `CreateDocumentCommand` sin N+1 ni choque de clave en `ACC_AccountBalances`
- [ ] T056 Script y CSS de Syncfusion recortados a los 15 componentes (Custom Resource Generator)
- [ ] T057 ReadyToRun también en el Web; `fluent2-lite`
