# Quickstart — Novedades y Liquidación de Nómina (Feature 005)

Cómo levantar, sembrar, recorrer el ciclo completo y verificar. Para quien implementa
o prueba; el manual de usuario vive dentro de la aplicación (`/manual`).

## 1. Levantar

Ejecución por defecto **local** (`appsettings.Development.json`): PostgreSQL,
MongoDB y Redis del `docker compose -f docker/dev.yml`.

```bash
dotnet build IngenIA365ERP.slnx
dotnet run --project src/Presentation/IngenIA365ERP.API
dotnet run --project src/Presentation/IngenIA365ERP.Web
```

Parar ambos hosts antes de recompilar (`MSB3021`).

## 2. Semilla

La semilla paramétrica corre al arrancar y al aprovisionar una cooperativa
(`Database:Seed:RunParametricSeed = true`). Debe dejar, en la base de la cooperativa:

| Qué | Cómo comprobarlo |
|---|---|
| Plan de nómina por defecto | `select "Code","IsDefault" from dbo."PAY_PayrollPlans"` → una fila `DEFAULT`, `true` |
| ≈28 conceptos estándar (`Origin = Seed`) | `select count(*) from dbo."PAY_ConceptDefinitions" where "Origin" = 0` |
| Parámetros legales 2026 con rangos | `select "Code","ValidFrom" from dbo."PAY_LegalParameters" order by 1` (todos los `LegalParameterCodes`) |
| Tipo de comprobante `NM` | `select "Code" from dbo."ACC_VoucherTypes" where "Code" = 'NM'` |
| Permisos `Payroll.*` nuevos | `select "Code" from dbo."SEC_Permissions" where "Code" like 'Payroll.%'` |

Reaplicar sin duplicar: `POST /api/payroll/concept-definitions/seed` (o reiniciar la API).

## 3. Recorrido de extremo a extremo (con `curl` sobre la API, antes que por la UI)

Token: iniciar sesión como administrador de la cooperativa de prueba y guardar el
`accessToken` (segundo factor incluido). `T=$TOKEN; H="Authorization: Bearer $T"`.

```bash
# 1. Período abierto del plan por defecto (crear si no existe)
curl -s -H "$H" "https://localhost:7100/api/payroll/pay-periods?status=Open"

# 2. Un empleado (ficha con plan, afiliaciones y salario). Dos novedades:
curl -s -H "$H" -H "Content-Type: application/json" \
  -d '{"employeePublicId":"<emp>","conceptCode":"HEX_NOCTURNA","quantity":6}' \
  https://localhost:7100/api/payroll/pay-periods/<periodo>/novelties
curl -s -H "$H" -H "Content-Type: application/json" \
  -d '{"employeePublicId":"<emp>","conceptCode":"INCAP_GENERAL","startDate":"2026-09-10","endDate":"2026-09-12"}' \
  https://localhost:7100/api/payroll/pay-periods/<periodo>/novelties

# 3. Calcular (borrador v1)
curl -s -X POST -H "$H" https://localhost:7100/api/payroll/pay-periods/<periodo>/runs
# 4. Leer resumen, detalle del empleado con explicaciones, comparativo, cuadre
curl -s -H "$H" https://localhost:7100/api/payroll/runs/<run>
curl -s -H "$H" https://localhost:7100/api/payroll/runs/<run>/employees/<emp>
# 5. Corregir la novedad de horas (crea versión) → el borrador queda Stale → recalcular (v2)
# 6. Aprobar
curl -s -X POST -H "$H" -H "Content-Type: application/json" -d '{"confirm":true,"exceptions":[]}' \
  https://localhost:7100/api/payroll/runs/<run2>/approve
# 7. Relación de pago, marcar pagados, PDF y envío manual
curl -s -H "$H" https://localhost:7100/api/payroll/runs/<run2>/payments
curl -s -H "$H" -o comprobante.pdf https://localhost:7100/api/payroll/runs/<run2>/payslips/<emp>/pdf
```

Lo que debe cumplirse (ver en la base o en las respuestas):

- Tras el paso 3: una fila en `PAY_PayrollRuns` (`Status = Draft`, `Version = 1`);
  `PAY_PayPeriods.Status = 1` (Calculated); ninguna fila en `PAY_PayrollTransactions`
  nueva (tabla legada).
- Tras el paso 5: la corrida v1 pasa a `Superseded`, existe v2 `Draft`, y
  `changedEmployees = 1`.
- Tras el paso 6: `Status = Approved`, un `ACC_AccountingDocuments` con
  `VoucherTypeCode = 'NM'` cuadrado, `ACC_JournalEntries` por concepto y centro de
  costo, y `PAY_PayPeriods.Status = 2`. Un segundo `POST …/runs` responde
  `Payroll.PeriodApproved`.
- Tras marcar un pago: `POST …/reverse` responde `Payroll.PaymentBlocksReversal`.
- Un `POST …/runs` con `SMMLV` sin vigencia para la fecha del período responde
  `Payroll.LegalParameterMissing` con el código.

## 4. Pruebas

```bash
# Motor puro (casos dorados) y validadores/handlers
dotnet test tests/IngenIA365ERP.Domain.Tests --filter "FullyQualifiedName~Payroll"
dotnet test tests/IngenIA365ERP.Application.Tests --filter "FullyQualifiedName~Payroll"
# Arquitectura (VIII con los namespaces nuevos, XI, sin valores legales fijos)
dotnet test tests/IngenIA365ERP.Architecture.Tests
# Integración (exige Docker)
dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~Payroll"
```

Los casos dorados del motor están en
`tests/IngenIA365ERP.Domain.Tests/Payroll/Calculation/Casos/*.json`: cada archivo es
un empleado con su período, novedades y el resultado esperado línea a línea, escrito
para que la contadora lo pueda leer y firmar (SC-001). Añadir un caso es añadir un
archivo.

## 5. Migraciones

```powershell
.\tools\scripts\add-migration.ps1 -Name NominaPlanesYPeriodos     -Context Application
.\tools\scripts\add-migration.ps1 -Name NominaConceptosYParametros -Context Application
.\tools\scripts\add-migration.ps1 -Name NominaNovedades           -Context Application
.\tools\scripts\add-migration.ps1 -Name NominaLiquidacion         -Context Application
```

En par por proveedor; `Feature004_MigrationParity` caza la que falte. Las bases de
cooperativas existentes las migra `tools/IngenIA365ERP.DbMigrator` en el despliegue
(`--scope tenants`), nunca el arranque de la API (Principio IV).

## 6. Despliegue

Igual que cualquier cambio de `develop`: CI construye las imágenes `:develop`, Argo
sincroniza DEV y QA, y hay que reiniciar API y Web (`imagePullPolicy: Always` con
etiqueta móvil). Antes de la primera liquidación real en un ambiente, verificar la
tabla de la sección 2 contra **esa** base, no contra el repositorio. Producción sigue
en `release` con aprobación manual.

## 5. Desvíos observados al implementar (2026-09-05)

El recorrido de la sección 3 **no se ejecutó con `curl` en esta sesión**: exige la
credencial de un administrador de cooperativa (con segundo factor) y la implementación
corrió sin nadie que la digitara. Las reglas de la casa no permiten inventar ni pedir
credenciales por chat, así que queda para la validación de QA (T134). Lo que sí está
verificado es el mismo ciclo por debajo de HTTP: 92 pruebas de Application sobre los
handlers (novedades, cálculo, aprobación, pagos, comprobantes, importación, recurrentes,
reversión) y 54 de arquitectura. Al correrlo, tener presentes estos desvíos respecto de
los contratos originales:

- **Permisos de períodos de pago**: `GET /api/payroll/pay-periods` exige
  `Payroll.Runs.View` y las escrituras `Payroll.Runs.Calculate`. Los códigos
  `Payroll.PayrollPeriods.*` del contrato nunca existieron en la semilla.
- **Retención por empleado**: `GET|PUT /api/payroll/employees/{id}/withholding` usan
  `Payroll.Novelties.View|Create`.
- **Cuentas por concepto**: `PUT /api/payroll/concept-definitions/{code}/accounts` recibe
  `debitAccountCode` y `creditAccountCode` (códigos del plan de cuentas), no PublicIds.
- **Importación**: `POST …/novelties/import` responde **200** con
  `{applied, batchId, errors: []}` cuando entra todo y **422 con el mismo DTO**
  (`applied = 0`, `errors` con fila y columna) cuando algo falla; un archivo de más de
  5 MB responde 413 antes de leerlo. La plantilla se descarga en
  `GET /api/payroll/novelties/import-template`.
- **Recurrentes**: se materializan al **calcular** (no al crear el período) como
  novedades `Origin = Recurring` con su cuota; la cuota se cuenta al aprobar y se
  devuelve al reversar. Desactivar anula las novedades de períodos no aprobados.
- **Comprobantes**: sólo sobre corridas aprobadas o reversadas; `POST …/payslips/send`
  responde `Payroll.EmailNotConfigured` si la sección `Smtp` no declara host, puerto y
  remitente, antes de intentar un solo envío.
- **Reversión**: `POST /api/payroll/runs/{id}/reverse` contabiliza el reverso con la
  fecha del día (`clock.TodayUtc`); si el período contable de hoy está cerrado responde
  `Payroll.AccountingPeriodClosedForReversal`. El período vuelve a `Open` con
  `RunPublicId = null` y `StatusMessage` con el motivo.
- **Alias 308** de `/api/payroll/summary|detail|payslip`: retirados en esta versión
  (no tenían consumidores). Las rutas heredadas de reportes sí redirigen 308.
- **Migraciones**: una sola migración de esquema (`NominaNovedadesYLiquidacion`) más
  `NominaTablasPorRangos` y `NominaDetalleDeCorrida`, pareadas por proveedor. En local
  las dos últimas se aplican con `AutoMigrate` al siguiente arranque de la API.
