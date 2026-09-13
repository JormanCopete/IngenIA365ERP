# Feature Specification: Rendimiento percibido — despliegue, arranque del cliente, pipeline de la API y lecturas

**Feature Branch**: `develop` (cambios transversales; sin rama propia)
**Created**: 2026-09-13
**Status**: En curso — ronda 1 implementada; rondas 2 y 3 planificadas
**Input**: Solicitud del usuario del 2026-09-12: «los despliegues tardan >25 minutos» y
«es un aplicativo transaccional y necesito que desde el Front tenga un alto rendimiento:
cargar información, registrar, consultar y editar deben ser muy fluidos»; y «la mejora del
menú no se ve reflejada en ningún ambiente».

## Contexto

Auditoría del 2026-09-13 (ocho lectores por dimensión, cada hallazgo verificado por dos
revisores adversariales; 24 agentes, 1.102 lecturas). Lo que se midió, no lo que se supuso:

1. **Despliegue**: 28–36 min por corrida. 24 eran `docker-build`: tres Dockerfiles con SDK
   recompilaban el grafo entero en serie después de que las pruebas ya lo habían compilado,
   y exportaban 7,7 min de caché a GitHub Actions que nunca acertaba (un solo `scope` para
   tres imágenes, repositorio en el tope de 10 GB, rama `release` sin acceso a la caché de
   `develop`).
2. **Cliente WebAssembly**: el meta-paquete `Syncfusion.Blazor` es una sola DLL de 27 MB
   (5,9 MB en Brotli) que el navegador descargaba entera; los CSS/JS se enlazaban sin huella
   y Cloudflare los cacheaba 4 h, por eso el rediseño del menú «no se veía» tras desplegar.
3. **Producción**: el `LimitRange` del namespace inyectaba `limits.cpu=500m` a la API y a la
   Web sin que ningún manifiesto lo dijera; medido en el pod: 97 s estrangulados de 132 s de
   uso con la plataforma ociosa, .NET con un solo procesador y arranque de 100–140 s.
4. **API**: Serilog ignoraba la configuración (sin `ReadFrom.Configuration`): cada request y
   cada `DbCommand` con su SQL salían en `Information` a dos sinks sincrónicos. El limitador
   de peticiones identificaba a todos los usuarios con la IP del pod de cloudflared
   (`10.42.0.25` en el 100 % de los intentos de login): 10 logins/min para toda la plataforma.
5. **Pantallas**: cargas en serie de 5 viajes (Liquidación) o 10 GET de catálogos por
   apertura (Empleados); grillas que descargan el conjunto completo; y **siete pantallas de
   Contabilidad e Inventario que no funcionan** (envían `VoucherTypeCode/AccountCode/Debit`
   donde la API exige `VoucherTypePublicId/AccountPublicId/DebitAmount`, hacen `PUT` a rutas
   que no existen).
6. **Base de datos**: listados y estados financieros sin índice por las columnas que filtran
   (`LND_DepositEntries`, `ACC_Documents`, `ACC_AccountBalances`, `ACC_JournalEntries`).

## User Scenarios & Testing

### US1 — Desplegar en menos de 10 minutos (P1) — ✅ hecha

Quien mezcla a `develop` ve DEV y QA actualizados en menos de 10 minutos y sabe qué tardó.

**Aceptación**: una corrida completa (`build-and-test`, `publish-web`, tres `docker`,
`gitops`) termina en ≤ 10 min de reloj; un commit que sólo toca `docs/` no recompila
imágenes con SDK. Medido: **9 min 2 s** (`34726837245`).

### US2 — Ver el cambio desplegado sin vaciar la caché (P1) — ✅ hecha

Tras un despliegue, la primera carga trae los estilos y scripts nuevos.

**Aceptación**: todos los `<link>`/`<script>` de `App.razor` llevan huella y
`Cache-Control: max-age=31536000, immutable`; el bundle de Shared se pide con la huella
nueva. Verificado en QA por `curl` al pod.

### US3 — Primer ingreso liviano (P1) — ✅ hecha (parcial: S2 pendiente)

El cliente WebAssembly no descarga componentes que no usa.

**Aceptación**: los ensamblados Syncfusion publicados pesan < 10 MB (eran 27,4); el
`publish` del Web baja de 500 s a < 400 s en el runner. Medido: 9,1 MB / 2,35 MB br; 341 s.

### US4 — La API tiene la CPU que pide (P1) — ⏳ GitOps preparado, pendiente de autorizar

Una petición que necesita CPU no se estira por cuota; el arranque no tarda minutos.

**Aceptación**: en el pod de la API `cpu.max = 200000 100000`, `nr_throttled ≈ 0` en
operación normal; `ProcessorCount = 2`; Started→Ready < 60 s. Los manifiestos declaran
CPU explícita y el `LimitRange` sólo completa memoria.

### US5 — Logs y límites por usuario, no por plataforma (P1) — ✅ hecha

**Aceptación**: en producción `grep -c 'Executed DbCommand'` en 500 líneas de log = 0; un
`LogError` sigue apareciendo; la tabla de intentos de login registra la IP pública del
visitante; toda respuesta bajo `/api` lleva `Cache-Control: no-store` salvo que el
endpoint declare otra cosa.

### US6 — Pantallas que no esperan de a uno (P2) — ✅ hecha

Liquidación, Novedades, Empleados y afines lanzan a la vez las cargas independientes.

**Aceptación**: en DevTools las peticiones de apertura se solapan; Liquidación pasa de 5 a
≤ 3 viajes en serie; ningún error de una rama tapa al otro.

### US7 — Lecturas con índice (P2) — ✅ hecha (migración `IndicesDeLectura`)

**Aceptación**: `EXPLAIN` del listado de comprobantes por fecha y del extracto de ahorro
usa índice; migración en par (PostgreSQL y SQL Server) aplicada por el Job PreSync.

### US8 — Indicador de arranque y login honesto (P2) — ✅ hecha

Mientras el cliente descarga, la persona ve progreso; el botón del login no dispara un
POST clásico que pierde lo escrito.

### US9 — Contabilidad e Inventario funcionan (P1, **ronda 2**) — ⏳ pendiente de decisión

Las pantallas Comprobantes, Movimientos, Saldos, Lista de comprobantes, Presupuestos,
Movimiento de inventario y Kardex envían y leen el contrato real de la API. Requiere
decidir: **un comprobante asentado no se edita** (Principio XI): la corrección es anular y
crear otro; el `PUT` de las pantallas desaparece.

### US10 — Datos paginados y catálogos en memoria (P2, **ronda 2**)

Grillas con paginación en servidor (`PagedListAdaptor`), catálogos cacheados en el cliente
con clave por cooperativa y vaciado al cambiar de sesión o de cooperativa (Principio IV),
estimado de novedades fuera del listado.

### US11 — Kestrel escucha mientras la base se verifica (P2, **ronda 2**)

Responde 503 con envelope canónico a todo salvo `/health/*` hasta que la base esté
verificada; sembrar al arrancar sólo lo que nadie sembró.

## Requirements

- **FR-001** Compilar una vez en el runner; las imágenes empaquetan la publicación.
- **FR-002** Ningún recurso estático de `App.razor` sin huella.
- **FR-003** Ninguna referencia al meta-paquete `Syncfusion.Blazor`; todos los paquetes
  Syncfusion a la misma versión.
- **FR-004** Cada workload declara `limits.cpu`; el `LimitRange` no completa CPU.
- **FR-005** Serilog lee `Serilog:*` de la configuración; `Microsoft.EntityFrameworkCore.
  Database.Command` en `Warning` o superior en producción.
- **FR-006** La IP del visitante sale de `CF-Connecting-IP`; documentado que vale sólo
  mientras el origen se alcance únicamente por el túnel.
- **FR-007** `Cache-Control: no-store` por defecto bajo `/api`.
- **FR-008** Índices `IX_LND_DepositEntries_Account_Date`, `IX_LND_DepositEntries_PersonCode`,
  `IX_ACC_Documents_Date_Number`, `IX_ACC_AccountBalances_Period_Account`,
  `IX_ACC_JournalEntries_Account_Date` en ambos proveedores.
- **FR-009** Nunca `InvariantGlobalization`, `TieredCompilation=0` ni `TieredPGO=0`
  (rompen es-CO o empeoran el estado estable).

## Lo que se descartó y por qué (no volver a proponer)

- Caché de `ADM_Tenants` por petición: 1–3 ms frente a viajes de 200–400 ms, y la fila
  decide **a qué base** va la petición (Principio IV).
- `max-age`/ETag en catálogos: la caché HTTP del navegador se indexa por URL y la
  cooperativa viaja en el claim; al cambiar de cooperativa se verían catálogos ajenos.
- Saldo inicial del libro mayor desde `ACC_AccountBalances`: la suma de asientos es la
  fuente de verdad (Principio XI).
- Modelo compilado de EF: no soporta `HasQueryFilter` ni `IModelCacheKeyFactory` propio.
- `prerender: false`: pierde el primer pintado; el indicador de arranque da el mismo
  resultado.
- `IgnoreScriptIsolation`: no existe en Syncfusion 33.2.8.

## Success Criteria

- **SC-001** Despliegue develop → QA ≤ 10 min (medido 9:02).
- **SC-002** Primer ingreso: ≤ 8,5 MB comprimidos (eran ≈ 12).
- **SC-003** API en producción sin throttling de CPU (`nr_throttled ≈ 0`) y arranque < 60 s.
- **SC-004** Cero `Executed DbCommand` en los logs de producción.
- **SC-005** Estilos nuevos visibles en la primera carga tras cada despliegue.
