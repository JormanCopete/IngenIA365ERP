# Contrato: plantillas de importación de Cartera (FR-033, FR-139–FR-143)

**Feature**: 013 | **Date**: 2026-10-05 | **Entregas**: K1 (líneas, vinculados, saldos de aportes, listas
restrictivas, matriz contable, migración por partes, cobros provisionales, valores nacionales), K2 (convenios de
pagaduría), K6a (historial de mora complementario) | **Base**: decisiones transversales T82, T83, T84, con T35, T37,
T44, T49, T52, T64, T66, T69, T70, T95 (`decisiones-transversales.md`) | **Molde**: `specs/012-inventario-comercial/
contracts/plantillas.md` (ejecutor, tipos de valor, revisión y aplicación) | **Hermanos**: `api.md`,
`adaptadores-y-archivos.md`, `contabilidad.md`, `data-model.md`

Requisitos que cubre: FR-006, FR-019–FR-033, FR-040, FR-043, FR-058, FR-060, FR-105, FR-108, FR-139, FR-140,
FR-141, FR-142, FR-143; D17, D19, D29; US1, US8; SC-013.

**Por qué este contrato existe antes que las pantallas.** COOFLOPAL diligencia sus líneas y prepara su migración
desde ya. Una columna que cambie después invalida lo diligenciado, así que el modelo de cada plantilla se **congela
antes de publicarla**: cambiar una columna de este documento después de publicada exige una versión nueva de la
plantilla que siga leyendo la anterior.

## 0. Reglas comunes

### 0.1 Las plantillas y su orden de carga

Cada plantilla sólo cita lo que cargaron las anteriores o lo que ya existe en el ERP (§0.2).

| # | Clave | Hojas | Ruta base | Comando | E |
|---|---|---|---|---|---|
| 1 | `lending.credit-lines` | `Lineas`, `Tasas`, `Documentos`, `Seguros`, `Garantias`, `Equivalencias` | `/api/lending/credit-lines` | `ImportCreditLinesCommand` | K1 |
| 2 | `lending.related-parties` | `Datos` | `/api/lending/related-parties` | `ImportRelatedPartiesCommand` | K1 |
| 3 | `lending.contribution-balances` | `Datos` | `/api/lending/contribution-balances` | `ImportContributionBalancesCommand` | K1 |
| 4 | `compliance.restrictive-lists` | `Datos` | `/api/compliance/restrictive-lists` | `ImportRestrictiveListCommand` | K1 |
| 5 | Matriz contable de Cartera | `Datos` | `/api/accounting/lending/rules` | `ImportLendingPostingRulesCommand` (+ `GetLendingRulesTemplateQuery`) | K1 |
| 6 | `lending.migration.{parte}` (17 partes, §5) | `Datos` en cada parte | `/api/lending/migration/loads/{id}/parts/{parte}` | `ImportMigrationPartCommand`; después `ApplyMigrationLoadCommand` | K1 |
| 7 | `lending.provisional-collections` | `Datos` | `/api/lending/activation/provisional-collections` | revisión: `ReviewProvisionalCollectionsCommand`; validación y guardado: dentro de `ActivateLendingCommand`; aplicación: el consumidor de Comercial al nacer la obligación (C62) | K1 |
| 8 | `lending.arrears-history` | `Datos` | `/api/lending/risk/arrears-history` | `ImportArrearsHistoryCommand` | K6a |
| 9 | `lending.payroll-agreements` | `Convenios`, `Contactos`, `CodigosDeRespuesta` | `/api/lending/payroll-agreements` | `ImportPayrollAgreementsCommand` | K2 |
| 10 | `saas.national-values` (registro central, maestro) | `Datos` | `/api/saas/national-values` | `ImportNationalValuesCommand` | K1 |

Los comandos de las filas 1, 2, 3, 4, 6, 9 y la clave de la fila 10 se fijan aquí; el de la 7 lo fija `api.md` §19.3
(comando, porque la revisión deja el archivo como adjunto); los demás nombres son de las transversales. Las
enumeraciones de las columnas usan los nombres y la numeración de `data-model.md` (§9 y la tabla de enumeraciones de
la parte 1; C65): una celda admite el nombre en inglés, el número o la etiqueta en español.

En cada ruta base: `GET {base}/template.xlsx` y `POST {base}/import?mode=review|apply`, salvo la 7 (§6), que sólo
revisa. `GET /api/lending/templates` (`Lending.Templates.View`) devuelve esta tabla para la pantalla
`/cartera/plantillas`, con `canDownload` y `canImport` según los permisos de quien pregunta y «se importa con K2» (o
K6a) cuando la entrega todavía no está desplegada (`CatalogoDeCartera.EntregaVigente`). Las plantillas 4 y 10 viven
en sus pantallas de plataforma (`/compliance/listas-restrictivas`, `/admin/valores-nacionales`) y no aparecen en el
registro de Cartera.

**Orden de carga de una cooperativa que inicia** (US8):

1. Lo de §0.2, que no tiene plantilla.
2. Líneas (1) y matriz contable (5), con la contadora, hasta dejar vacío el reporte de completitud.
3. Vinculados (2), saldos de aportes (3) y listas restrictivas (4), en cualquier orden.
4. Carga de migración (6): crear la carga con su corte, importar las partes en el orden de §5.2, revisar y aplicar.
5. Aceptar con acta las diferencias del cuadre y de la conciliación de ventas, si las hay (D17).
6. Activar, con el archivo de cobros provisionales (7) si los hay.
7. Desde K2, convenios de pagaduría (9). Desde K6a y si hace falta, historial complementario (8).

### 0.2 Lo que no tiene plantilla y debe existir antes

| Qué | Dónde | Por qué |
|---|---|---|
| Personas: deudores, codeudores, aseguradoras, pagadurías, fondos de garantías, abogados, miembros de órganos | Diálogo único de personas (008) | **Ninguna plantilla crea personas** (T82). Un documento que no está en el maestro es un error de fila y columna. No hay plantilla de personas (I11) |
| Oficinas con su sucursal contable vinculada | Maestros › Agencias (T62, T13) | Una oficina sin sucursal contable no emite hechos (C9, CH49) |
| Bancos con su código de transferencia (`COR_Banks.TransferCode`) | Maestros › Bancos | Archivo de desembolsos y cuentas de beneficiario |
| Valores nacionales replicados: usura por modalidad, `CatalogoDeModalidades`, `SarcAnexo1` | Registro central (§10) y réplica (T39) | La línea se valida contra la usura; la modalidad sale del catálogo |
| Perfil regulatorio de la cooperativa | `/admin/perfil-regulatorio` | Decide si se admiten no asociados y el modelo de deterioro |
| Órganos de gobierno y sus miembros | `/cartera/actas` | La aceptación de diferencias referencia un punto de sesión (T64) |
| Roles de Cartera y alcance de los usuarios | `CreateSuggestedLendingRolesCommand`; alcance en `/api/lending/scopes/users/{userPublicId}` | Permisos de las plantillas y alcance de oficina y línea (§0.5) |
| Plan de cuentas | Contabilidad (009) | La matriz cita auxiliares de movimiento |
| Formatos bancarios de pagaduría (K2) | `/maestros/formatos-bancarios` | Los convenios los citan por código |

### 0.3 El archivo

Igual que §0.3 de la 012: libro `.xlsx` de `PlantillaDeImportacion`, **una hoja de datos por sección con sólo los
encabezados en la fila 1** y una última hoja «Instrucciones» que el lector no mira; las de una sola sección llaman a
su hoja `Datos` y aceptan `.csv`. Encabezados en `camelCase` español, comparados sin mayúsculas, tildes, espacios ni
guiones bajos; el orden no importa; una columna desconocida es el aviso `Import.Column.Unknown`; falta una columna
obligatoria → 400 `Archivo.ColumnaFaltante`; falta una hoja obligatoria → 400 `Archivo.HojaFaltante`. El número de
fila de un error es el de Excel.

Topes del ejecutor común (`EjecutorDeImportacion`): 16 MiB por archivo, 60.000 filas por hoja, 1.000 errores y 5.000
cambios en la respuesta (el resto, en la revisión en Excel). **Las partes de la migración admiten varios archivos**
(§5.1), porque las cuotas de 50.000 créditos no caben en una hoja.

### 0.4 Tipos de valor

Los de `TipoDeValor` de la 012, con su sentido en Cartera:

| Tipo | Cómo se escribe | En Cartera |
|---|---|---|
| `Codigo` | `CodigoDeCatalogo`, mayúsculas, 10 caracteres salvo que la columna diga otro | Línea, documento, seguro, convenio |
| `Texto` | Libre, se recorta | Nombres, soportes, referencias |
| `Entero` | Sin decimales | Plazos en cuotas, días, conteos |
| `Monto` | Hasta 2 decimales, punto o coma decimal, sin separador de miles | Alias `Monto` (18,2) |
| `Porcentaje` | **En puntos**, hasta 4 decimales: `26,8243` es 26,8243 % | **Tasas** (efectiva anual) y tarifas. Se guarda como fracción de 6 decimales (`TasaAnual` 9,6): 0,268243. Más de 4 decimales en puntos se rechaza (`Import.Cell.Format`, T35) |
| `Fecha` | `yyyy-MM-dd` o fecha de Excel | Hora de Colombia, sin hora |
| `SiNo` | `sí`, `si`, `s`, `x`, `1` / `no`, `n`, `0` o vacío | |
| `Lista` | Valores separados por coma; `*` = todos | Periodicidades, formas de pago, tipos de garantía |
| `Enumeracion` | El nombre del enum en inglés o la etiqueta en español, sin mayúsculas ni tildes | `DayCountConvention`: `Thirty360` o `30/360` |
| `Persona` | Documento sin dígito de verificación; debe existir y estar viva en el maestro | Deudor, codeudor, aseguradora, pagaduría |
| `Sucursal` | Código (`LegacyCode`) o nombre de `COR_Branches` | Oficina del crédito: la sucursal debe estar vinculada a una oficina |
| `CuentaContable` | Código completo de una auxiliar de movimiento | Sólo en la matriz (5) |

Dos formatos de texto con patrón fijo (fijados aquí): **número de crédito**, texto 20 con `^[A-Za-z0-9-]+$`, se guarda
en mayúsculas; **mes**, texto `AAAA-MM`. **Factura**: el número completo del documento de venta de Comercial (prefijo y
consecutivo), tal como lo muestra la venta.

Las fechas de los ejemplos son ilustrativas: el corte y la activación los fija el dueño.

### 0.5 Revisión, aplicación y errores

Igual que §0.5 de la 012, que es la única fuente de la mecánica:

- `mode` obligatorio (`Import.ModeRequired`). **`review`** corre las mismas reglas que `apply` y no guarda nada;
  responde 200 con `valid = false` si hay errores y, con `format=xlsx`, devuelve el mismo libro con las columnas
  `resultado` y `errores`. **`apply`** es **todo o nada**: una `TransaccionExplicita`, un `SaveChanges`; con un error,
  422 `Import.Invalid` con el mismo `ImportResultDto` en `data`.
- **Llave y actualización**: la llave de cada plantilla identifica la fila; si existe, actualiza; si queda igual,
  «sin cambio». La plantilla **nunca borra**. Las excepciones las dice cada plantilla: las que cargan **hechos**
  (listas restrictivas, historial de mora) crean siempre una carga nueva, y las vigencias (líneas, matriz, valores
  nacionales) crean una versión nueva.
- **Motivo**: `requiresReason = true` cuando el archivo crea o cierra una vigencia o cambia algo que una operación ya
  usó; `apply` exige entonces `reason`, que se copia a cada vigencia y al evento de auditoría.
- **Idempotencia**: cada llamada lleva `Idempotency-Key`; revisar y aplicar son dos operaciones.
- **Auditoría**: `Import.Reviewed` e `Import.Applied` con plantilla, modo, nombre y SHA-256 del archivo, conteos,
  quién, IP y canal, en el módulo `Lending` (`Compliance` en la 4, `NationalValues` en la 10); en `apply`, además, el
  antes y después de cada entidad. El archivo no se guarda (en la migración queda su SHA-256 en la parte, §5.1).
- **Errores**: los `Import.*` de la 012 (`Import.Cell.Required`, `.Format`, `.NotFound`, `.NotYetAvailable`,
  `.PermissionRequired`, `Import.Row.Duplicate`). Las reglas de negocio responden con **el mismo código que el alta
  unitaria** (`Catalogo.CodigoDuplicado`, `Lending.Usury.Exceeded`, `Lending.Schedule.PrincipalMismatch`,
  `Lending.Parameter.BelowLegalFloor`, `NationalValues.Missing`…): no hay una segunda puerta con reglas propias. Los
  códigos propios de la migración están en §5.5.
- **Alcance**: una plantilla de Cartera que cita oficinas o líneas exige que estén en el alcance de quien importa
  (T62); fuera de él, `Import.Cell.NotFound`, igual que si no existieran.

### 0.6 Descarga con datos

`GET {base}/template.xlsx?withData=true` devuelve la plantilla llena con lo que hoy tiene la cooperativa, para
corregir y volver a importar. Exige el permiso de consulta del área y, si trae datos personales (vinculados, saldos de
aportes, partes de la migración), además `Lending.Reports.ExportPersonalData`. Se audita como exportación.

### 0.7 Permisos por plantilla

| # | Descargar vacía | Importar | Descargar con datos | Por columna |
|---|---|---|---|---|
| 1 | `Lending.CreditLines.View` | `Lending.CreditLines.Manage` | `Lending.CreditLines.View` | — |
| 2 | `Lending.Templates.View` | `Lending.RelatedParties.Manage` | `Lending.RelatedParties.Read` + `Lending.Reports.ExportPersonalData` | — |
| 3 | `Lending.Templates.View` | `Lending.CreditLimits.Assign` (reutilizado: esos saldos sólo alimentan el cupo, C85) | `Lending.CreditLimits.Read` + `Lending.Reports.ExportPersonalData` | — |
| 4 | `Compliance.RestrictiveLists.View` | `Compliance.RestrictiveLists.Load` | `Compliance.RestrictiveLists.View` | — |
| 5 | `Accounting.LendingRules.View` | `Accounting.LendingRules.Manage` | `Accounting.LendingRules.View` | — |
| 6 | `Lending.Templates.View` | `Lending.Migration.Load` | `Lending.Migration.Load` + `Lending.Reports.ExportPersonalData` | — |
| 7 | `Lending.Templates.View` | revisar y activar: `Lending.Activation.Execute` | — | — |
| 8 | `Lending.Templates.View` | `Lending.ArrearsHistory.Import` | `Lending.Ratings.Read` + `Lending.Reports.ExportPersonalData` | — |
| 9 | `Lending.PayrollAgreements.View` | `Lending.PayrollAgreements.Manage` | `Lending.PayrollAgreements.View` | — |
| 10 | maestro (`RequireMasterAdminAttribute`, con segundo factor) | maestro | maestro | — |

Sin el permiso de la ruta, el 404 genérico.

### 0.8 El ejecutor común y su generalización (T82)

- `CatalogoDePlantillasDeCartera` (`Application/Lending/Imports`) declara las `DefinicionDePlantilla` de las claves
  `lending.*` (módulo de auditoría `Lending`).
- `RutasDePlantilla.MapPlantilla` se **generaliza**: hoy está atada a `CatalogoDePlantillas` de Inventario y fija el
  permiso de datos personales (`Inventory.Reports.ExportPersonalData`) y el evento de exportación. Pasa a recibir por
  parámetro la búsqueda de la definición, el permiso de datos personales y el evento. Es un refactor pequeño: se corren
  las pruebas de las dieciséis plantillas de la 012 antes de fusionar.
- La revisión carga los catálogos citados en bloque (diccionarios código → Id; documento → persona), nunca una
  consulta por fila.
- `ImportNationalValuesCommand` usa la misma definición, el mismo lector y las mismas reglas de revisión y aplicación,
  pero escribe en la base administrativa con **su propia transacción sobre `IAdminDbContext`** (C82), no con la
  transacción del ejecutor, que es la de la base de la cooperativa.

---

## 1. Líneas de crédito (`lending.credit-lines`, FR-019–FR-033)

Escribe `LND_CreditLines`, `LND_CreditLineVersions`, `LND_CreditLineRateBands`, `LND_CreditLineDocumentRequirements`,
`LND_CreditLineInsurances`, `LND_CreditLineCollateralRules` y `LND_CommercialTermEquivalences`, con **las mismas
reglas que el alta y la versión unitarias** de `api.md` (la importación las reutiliza, como `ImportAccountsCommand`
de la 009). El puntaje (FR-037) y los parámetros con ámbito de línea (`Mora.ToleranciaDeSaldoVencido`,
`Causacion.ContinuaTrasFallecimiento`) no van aquí: se configuran en pantalla.

**Versiones.** Cada fila de `Lineas` es una versión: llave `codigo` + `vigenteDesde`. La misma línea con una
`vigenteDesde` posterior crea una versión nueva y cierra la anterior la víspera; los créditos vivos conservan lo
pactado (FR-032). Las hojas `Tasas`, `Documentos`, `Seguros` y `Garantias` cuelgan de una versión (`linea` +
`vigenteDesde`) y la reemplazan entera: lo que la versión nueva no trae, no lo tiene. Una versión que empieza antes de
hoy en una línea con solicitudes o créditos se rechaza: lo pactado no se reescribe. Crear o cerrar una versión pide
`reason`.

**Usura.** Cada tasa (de la versión y de cada rango) pasa por `ControlDeUsura` con la usura vigente a `vigenteDesde`
**y** a hoy de la modalidad certificada que el `CatalogoDeModalidades` asigna a la combinación modalidad +
sub-segmento, sumando los seguros que computan (los que retienen parte de la prima). Si la supera,
`Lending.Usury.Exceeded` en la fila y columna, con la usura usada y su fuente; si falta la usura de esa fecha,
`NationalValues.Missing`.

### Hoja `Lineas`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | Nomenclatura de la cooperativa; `Catalogo.CodigoDuplicado` contra otra línea |
| `vigenteDesde` | fecha | sí | Fecha desde de la versión |
| `nombre` | texto 120 | sí | |
| `destino` | texto 200 | sí | |
| `modalidad` | código del `CatalogoDeModalidades` vigente | sí | `VIVIENDA` se rechaza hasta K6b (`Import.Cell.NotYetAvailable`, D18) |
| `subsegmento` | código del catálogo (`ConLibranza`, `SinLibranza`, `PersonaNatural`, `PersonaJuridica`, `Empleados`, `Inmobiliario`, `Empresarial`) | según la modalidad | La combinación debe existir en el catálogo vigente |
| `paraEmpleados` | sí/no | no (no) | `ForEmployees` de la versión: línea para empleados de la cooperativa (FR-019). La condición «empleado» de tasas y documentos se expresa aquí, no en `tipoDeSolicitante` (C96) |
| `activa` | sí/no | no (sí) | «no» cierra la línea a créditos nuevos; una línea con créditos vivos nunca se borra |
| `admiteNoAsociados` | sí/no | no (no) | Sólo si el perfil regulatorio lo permite (FR-018) |
| `admiteOrigenComercial` | sí/no | no (no) | Ventas de Comercial |
| `conceptoTributarioDelInteres` | `InterestTaxTreatment`: `NotTaxed`, `Taxed` | con origen comercial | Sin él, una línea de origen comercial con tasa mayor que cero no se activa: `Lending.CreditLine.InterestTaxConceptMissing` (D20) |
| `soporteDelConceptoTributario` | texto 200 | con el anterior | Referencia del concepto de la contadora |
| `tipoDeTasa` | `RateKind`: `Fixed`, `Variable` | sí | |
| `tasaCorrienteEA` | porcentaje | con `Fixed` | Mayor que cero; usura |
| `indice` | `IBR`, `IPC` u otro código de valor nacional de índice | con `Variable` | |
| `claveDelIndice` | texto 20 | con `Variable` | Plazo o serie (p. ej. `3M`) |
| `margen` | porcentaje | con `Variable` | Se suma en la expresión del índice, luego a EA, piso, techo y usura (D10) |
| `mesesDeReajuste` | entero | con `Variable` | Periodicidad de reajuste |
| `tasaPiso` · `tasaTecho` | porcentaje | no | |
| `tipoDeTasaDeMora` | `DefaultRateKind`: `Fixed`, `RelativeToCurrent` | sí | |
| `tasaDeMoraEA` | porcentaje | con `Fixed` | Nunca por encima de la usura del día |
| `factorDeMora` | decimal, hasta 6 decimales | con `RelativeToCurrent` | Múltiplo de la corriente (1,5 = una vez y media); se guarda en `DefaultRateFactor`. **Pendiente de validar por la contadora** (§7.7 de las transversales): si la quiere como puntos adicionales, la columna cambia antes de publicar la plantilla |
| `convencionDeDias` | `DayCountConvention`: `Thirty360`, `Actual365`, `Actual360` | sí | |
| `equivalencia` | `RateEquivalence`: `Compound`, `Linear` | no (`Compound`) | |
| `redondeoUnidad` | `0,01`, `1`, `10`, `100`, `1000` | no (`0,01`) | |
| `redondeoModo` | `RoundingMode` | no (`AwayFromZero`) | |
| `redondeoMomento` | `RoundingMoment` | no (`PerInstallment`) | |
| `sistemaDeAmortizacion` | `AmortizationSystem`: `LevelPayment`, `ConstantPrincipal`, `Bullet` | sí | |
| `admiteExtraordinarias` | sí/no | no (no) | Cuotas extraordinarias (primas) |
| `gracia` | `GraceKind`: `None`, `Principal`, `Total` | no (`None`) | |
| `graciaMaximaCuotas` | entero | con gracia | |
| `interesEnGracia` | `GraceInterestTreatment`: `FirstInstallment`, `Distributed` | con `Total` | Nunca se capitaliza |
| `montoMinimo` · `montoMaximo` | monto | sí | Mínimo ≤ máximo |
| `plazoMinimo` · `plazoMaximo` | entero (cuotas) | sí | |
| `periodicidades` | lista de `RepaymentPeriodicity`: `SinglePayment` 0, `Weekly` 7, `TenDay` 10, `Biweekly` 15, `Monthly` 30, `Bimonthly` 60, `Quarterly` 90, `SemiAnnual` 180, `Annual` 360 | sí | Pago único, semanal, decadal, quincenal, mensual, bimestral, trimestral, semestral y anual (FR-022); el número es la base de proporción |
| `periodicidadPorDefecto` | `RepaymentPeriodicity` | sí | Debe estar en la lista |
| `diaDePago` | entero 1–31 | no | Vacío: el día del desembolso |
| `prelacion` | `AllocationOrder` | no (`OldestInstallmentFirst`) | |
| `recalculoPorNotas` | `PrepaymentChoice`: `ReduceTerm`, `ReduceInstallment` | con origen comercial | No aplica a los abonos del deudor (FR-031) |
| `formasDePago` | lista de `RepaymentChannel`: `Cashier` 1, `OwnPayroll` 2, `PaymasterDeduction` 3, `DirectDebit` 4 | sí | `PaymasterDeduction` (libranza con pagaduría externa) se puede declarar desde K1 para los créditos migrados; originar por ella exige K2 |
| `formaDePagoPorDefecto` | `RepaymentChannel` | sí | Debe estar en la lista |
| `admiteTramos` | sí/no | no (no) | |
| `relacionCupoAportes` | decimal | no | N veces los aportes (FR-029) |
| `endeudamientoMaximo` | monto | no | |
| `saldoMinimoDeAportes` | monto | no | Mientras rija D19, se compara con el saldo cargado por la plantilla 3 |
| `exigeConsultaACentrales` | sí/no | no (no) | Con «sí», en producción no se aprueba con un resultado simulado (`adaptadores-y-archivos.md` §3.6) |
| `notas` | texto 400 | no | |

### Hoja `Tasas` (rangos, FR-020)

Llave: `linea` + `vigenteDesde` + `montoDesde` + `plazoDesde` + `tipoDeSolicitante` + `paraEmpleados`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `linea` · `vigenteDesde` | código · fecha | sí | Una versión de la hoja `Lineas` o ya existente |
| `montoDesde` · `montoHasta` | monto | no | Vacío = sin límite |
| `plazoDesde` · `plazoHasta` | entero | no | |
| `tipoDeSolicitante` | `DebtorKind`: `Associate` 1, `Customer` 2 | no (vacío = cualquiera) | Sin valor «empleado» (C96) |
| `paraEmpleados` | sí/no | no (vacío = indiferente) | `ForEmployees` de la banda: la tasa para empleados de la cooperativa (C96) |
| `antiguedadMinimaMeses` | entero | no | `MinSeniorityMonths`: antigüedad como asociado |
| `tasaCorrienteEA` | porcentaje | en línea `Fixed` | |
| `margen` | porcentaje | en línea `Variable` | |

Dos rangos de la misma versión que se cruzan: `Lending.CreditLine.RateBandOverlap`.

### Hoja `Documentos` (FR-025)

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `linea` · `vigenteDesde` | código · fecha | sí | |
| `codigo` | código 10 | sí | Llave dentro de la versión |
| `nombre` | texto 120 | sí | |
| `obligatorio` | sí/no | no (sí) | Sin los obligatorios la solicitud no pasa a análisis |
| `montoDesde` · `montoHasta` | monto | no | Condición por monto |
| `tipoDeSolicitante` | `DebtorKind` | no (vacío = cualquiera) | |
| `formasDePago` | lista de `RepaymentChannel` | no (todas) | Por ejemplo, la certificación laboral sólo en libranza |

### Hoja `Seguros` (FR-028)

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `linea` · `vigenteDesde` | código · fecha | sí | |
| `codigo` | código 10 | sí | Llave dentro de la versión |
| `nombre` | texto 120 | sí | |
| `aseguradora` | persona | sí | Persona jurídica del maestro |
| `base` | `InsuranceBase`: `OutstandingBalance` 1, `InitialAmount` 2 | sí | |
| `tarifa` | porcentaje | sí | Por período de cobro |
| `periodicidad` | `RepaymentPeriodicity` | no (la del crédito) | Se cobra con la cuota y se causa según ella (D9) |
| `obligatorio` | sí/no | no (sí) | |
| `retieneParteDeLaPrima` | sí/no | no (no) | Con «sí», el seguro computa en la usura (FR-021) |
| `porcentajeRetenido` | porcentaje | con el anterior | |

### Hoja `Garantias` (FR-026)

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `linea` · `vigenteDesde` | código · fecha | sí | |
| `montoDesde` · `montoHasta` · `plazoDesde` · `plazoHasta` | monto · entero | no | Rangos que no se cruzan |
| `codeudoresMinimos` | entero | no (0) | |
| `tiposAdmitidos` | lista de `CollateralKind`: `PromissoryNote` 1, `CoDebtor` 2, `Pledge` 3, `Mortgage` 4, `GuaranteeFund` 5, `Contributions` 6 | sí | |
| `coberturaMinima` | porcentaje | no | Sobre el monto |
| `aportesCuentanComoGarantia` | sí/no | no (no) | Mientras rija D19, el deterioro no descuenta aportes aunque diga «sí» |

### Hoja `Equivalencias` (FR-022, `LND_CommercialTermEquivalences`)

Traduce los términos que llegan en `VentaACreditoRegistrada` (`terms`: `Periodicity` = `Weekly`, `Biweekly`,
`Monthly`, `SinglePayment`; `TermUnit` = `Days`, `Months`; `Term`; `Installments`) a los de Cartera. Es de la
cooperativa, no de una línea. Llave: `periodicidadComercial` + `unidadDePlazo` + `plazoComercial` + `vigenteDesde`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `periodicidadComercial` | `Weekly`, `Biweekly`, `Monthly`, `SinglePayment` | sí | Como la envía Comercial |
| `unidadDePlazo` | `Days`, `Months` | no (cualquiera) | |
| `plazoComercial` | entero | no (cualquiera) | |
| `periodicidad` | `RepaymentPeriodicity` | sí | |
| `cuotas` | entero | no | Vacío: las cuotas de la venta |
| `vigenteDesde` | fecha | sí | |

Una venta cuya periodicidad no tiene fila responde `ObligacionRechazada` reprocesable (T17): se agrega la fila y se
reprocesa el mensaje.

| codigo | vigenteDesde | nombre | modalidad | subsegmento | tipoDeTasa | tasaCorrienteEA | tipoDeTasaDeMora | factorDeMora | convencionDeDias | sistemaDeAmortizacion | periodicidades | formasDePago |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| LIBRE | AAAA-MM-01 | Libre inversión | CONSUMO | SinLibranza | Fixed | 22,5000 | RelativeToCurrent | 1,5 | Actual365 | LevelPayment | Monthly | Cashier, DirectDebit |
| LIBRANZA | AAAA-MM-01 | Libranza | CONSUMO | ConLibranza | Fixed | 19,8000 | RelativeToCurrent | 1,5 | Actual365 | LevelPayment | Monthly, Biweekly | PaymasterDeduction, OwnPayroll |

(Tasas ilustrativas: cada una pasa por la usura vigente de su modalidad certificada.)

## 2. Personas vinculadas (`lending.related-parties`, FR-040)

Escribe `LND_RelatedParties`. Llave: `persona` + `fundamento` + `personaRelacionada` + `vigenteDesde`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `persona` | persona | sí | La vinculada |
| `fundamento` | `RelatedPartyBasis`: `BoardMember` 1, `OversightBoardMember` 2, `LegalRepresentative` 3, `SignificantContributor` 4, `Spouse` 5, `Relative` 6 | sí | |
| `personaRelacionada` | persona | con `Spouse` y `Relative` | El miembro, representante o asociado del que se deriva el vínculo; debe estar en el archivo o ya registrado con otro fundamento |
| `claseDeParentesco` **(nuevo, US17)** | `KinshipKind`: `Consanguinity` 1, `Affinity` 2, `Civil` 3 | con `Relative` | La clase del parentesco, que `LND_RelatedParties.KinshipKind` exige al pariente |
| `grado` | entero | con `Relative` | Dentro de los grados legales (límite nacional `VINCULADOS_GRADO_PARENTESCO`; fuera, `Lending.RelatedParty.KinshipDegreeExceeded`) |
| `porcentajeDeAportes` | porcentaje | con `SignificantContributor` | Igual o mayor que el porcentaje que fija la norma |
| `vigenteDesde` · `vigenteHasta` | fecha | sí · no | |
| `soporte` | texto 200 | sí | Acta de elección, certificado, registro civil |

Antes de activar, registrar vinculados es una de las pocas operaciones admitidas (FR-142). Lo registrado aquí
**prevalece** sobre la parte `related-parties` de la migración para la misma persona y fundamento.

## 3. Saldos de aportes y ahorro permanente (`lending.contribution-balances`, D19)

Provisional mientras Aportes y Ahorros no ofrezcan su consulta (`IConsultaDeAportes` responde «no disponible»). Lo
carga el jefe de cartera. Escribe `LND_ContributionBalanceLoads`. Llave: `persona` + `fechaDeCorte`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `persona` | persona | sí | |
| `fechaDeCorte` | fecha | sí | No futura |
| `saldoDeAportes` | monto ≥ 0 | sí | |
| `saldoDeAhorroPermanente` | monto ≥ 0 | no | |
| `fuente` | texto 120 | sí | Sistema o reporte de donde salió |

Cartera usa el último con `fechaDeCorte` ≤ D. Sólo alimenta el cupo (relación cupo/aportes y saldo mínimo): mientras
rija D19, el deterioro no descuenta aportes y el arrastre no aplica su excepción. Un cupo calculado sella el saldo y
la fecha que usó, así que corregir una fila después no cambia cupos ya otorgados.

## 4. Listas restrictivas (`compliance.restrictive-lists`, plataforma, T70)

Escribe una **carga nueva** cada vez: `COR_RestrictiveListLoads` (lista, fuente, fecha de publicación, vigencia,
versión, SHA-256, filas, quién) y sus `COR_RestrictiveListEntries`, inmutables. La carga anterior de la misma lista
queda `Replaced`. Las entradas **no son personas** del maestro. Rutas `/api/compliance/restrictive-lists` (+
`template.xlsx`, `import`); pantalla `/compliance/listas-restrictivas`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `lista` | código 10 | sí | `OFAC`, `ONU`, `PROPIA` u otra; la misma en todas las filas del archivo |
| `fuente` | texto 200 | sí | La misma en todas las filas |
| `publicadaEl` | fecha | sí | La misma en todas las filas |
| `vigenteDesde` | fecha | sí | La misma en todas las filas; no anterior a la carga vigente |
| `tipoDeEntidad` | `RestrictiveListEntityKind`: `Person` 1, `Organization` 2, `Other` 3 | sí | |
| `tipoDeDocumento` | texto 10 | no | |
| `numeroDeDocumento` | texto 30 | no | Se normaliza (sin puntos, guiones ni espacios); exige `tipoDeDocumento` |
| `nombreCompleto` | texto 300 | sí | Se normaliza con `CoincidenciaDeNombres` |
| `alias` | texto 1000 | no | Separados por `;` |
| `programa` | texto 120 | no | |
| `notas` | texto 400 | no | |

Reglas: dos filas con el mismo documento normalizado son `Import.Row.Duplicate`. Al aplicar, la tarea
`cartera.re-escaneo-de-listas` vuelve a consultar a deudores y codeudores vivos (`adaptadores-y-archivos.md` §3.8).

---

## 5. Migración por partes (`lending.migration.{parte}`, FR-139–FR-143, T83)

### 5.1 La carga, sus partes y sus estados

Una **carga** (`LND_MigrationLoads`) tiene una fecha de corte y se arma por **partes**; cada parte es una plantilla de
una hoja (`Datos`, admite `.csv`) que escribe su tabla de preparación (`LND_Migration*`, entidades
`Migration{…}Row`). Aplicar la carga **sólo guarda la carga**, su revisión, su cuadre y su conciliación de ventas: los
créditos, sus transacciones de saldo inicial y el cierre base nacen al activar (FR-142, T84).

| Paso | Ruta | Comando | Permiso |
|---|---|---|---|
| Crear la carga | `POST /api/lending/migration/loads` (`cutoffDate`, `notes`) | `CreateMigrationLoadCommand` (fijado aquí) | `Lending.Migration.Load` |
| Plantilla de una parte | `GET /api/lending/migration/loads/{id}/parts/{parte}/template.xlsx` | — | `Lending.Templates.View` |
| Importar un archivo de una parte | `POST /api/lending/migration/loads/{id}/parts/{parte}/import?mode=review\|apply[&replace=true]` | `ImportMigrationPartCommand` (fijado aquí) | `Lending.Migration.Load` |
| Aplicar la carga | `POST /api/lending/migration/loads/{id}/apply` | `ApplyMigrationLoadCommand` | `Lending.Migration.Load` |
| Ver el cuadre | `GET /api/lending/migration/loads/{id}/balance-check` | `GetMigrationBalanceCheckQuery` | `Lending.Reconciliation.View` (C85) |
| Ver la conciliación de ventas | `GET /api/lending/migration/loads/{id}/reconciliation` | `GetMigrationSalesReconciliationQuery` | `Lending.Migration.Load` |
| Aceptar diferencias | `POST /api/lending/migration/loads/{id}/differences/accept` (`differencePublicIds[]`, `sessionItemPublicId`, `reason`) | `AcceptMigrationDifferencesCommand` (la ruta y el nombre los fija `api.md` §19.2) | `Lending.Migration.AcceptDifferences` |

**Estados** (`Draft`, `Applied`, `Replaced`, `Activated`):

- Las partes sólo se importan en una carga `Draft` (`Lending.Migration.LoadNotDraft`).
- Aplicar deja la carga `Applied` y emite el informativo `CargaDeCarteraAplicada` (T57). Si ya había otra `Applied`,
  pasa a `Replaced`: no se borra y queda como ensayo. Con el mismo corte o con uno nuevo (FR-142).
- Sólo una carga `Applied` se activa; al activar queda `Activated`.
- Personas ve a los deudores de la carga `Applied` como «migrada, sin activar» y no deja borrarlos (T94).

**Varios archivos por parte** (C64). Una parte con más de 60.000 filas o 16 MiB (las cuotas de 50.000 créditos son
cientos de miles de filas) se sube en **varios archivos**. Cada `apply` de una parte agrega su archivo como una tanda de
la parte: `LND_MigrationLoadParts` lleva **una fila por archivo** (carga, clave de parte, nombre y SHA-256 del archivo,
filas, quién y cuándo), no una por parte, y el `apply` es todo o nada **para ese archivo**. Reglas:

- Subir otra vez el mismo archivo (mismo SHA-256) a la misma parte responde «sin cambio».
- La llave de cada parte es única entre **todas** las tandas de la parte: una llave repetida en otra tanda es
  `Import.Row.Duplicate` y nombra la tanda.
- `replace=true` da de baja todas las tandas anteriores de la parte (borrado lógico de sus filas de preparación) antes
  de agregar el archivo: es la forma de corregir una parte entera.
- Al importar una parte se validan las filas, el maestro y los catálogos; las referencias a otras partes (un número de
  crédito que todavía no se cargó) son **avisos** y se vuelven errores al aplicar la carga (§5.4).

**Segregación.** `CargarMigracion × AceptarDiferencias` es un par configurable de `Segregacion.Incompatibilidades`
(T66): con el par activo, quien cargó no acepta diferencias.

### 5.2 Las partes, en el orden de carga

| # | `{parte}` (clave `lending.migration.{parte}`) | Tabla de preparación | Oblig. | Qué trae (FR-139) |
|---|---|---|---|---|
| 1 | `loans` | `LND_MigrationLoans` | sí | Créditos vivos al corte con sus condiciones pactadas |
| 2 | `installments` | `LND_MigrationInstallments` | sí | Cuotas pendientes |
| 3 | `balances` | `LND_MigrationBalances` | sí | Saldos por concepto |
| 4 | `risk-data` | `LND_MigrationRiskData` | sí | Mora, calificación y deterioro al corte |
| 5 | `restructurings` | `LND_MigrationRestructurings` | no | Datos de reestructuración |
| 6 | `co-debtors` | `LND_MigrationCoDebtors` | no | Codeudores y avalistas |
| 7 | `collaterals` | `LND_MigrationCollaterals` | no | Garantías y pagarés |
| 8 | `charged-off` | `LND_MigrationChargedOff` | no | Cartera castigada con su saldo |
| 9 | `sales` | `LND_MigrationSales` | si hubo ventas a crédito de Comercial hasta el corte | Ventas por factura (FR-143) |
| 10 | `credit-limits` | `LND_MigrationCreditLimits` | no | Cupos |
| 11 | `blocks` | `LND_MigrationBlocks` | no | Bloqueos, embargos, fallecimientos e insolvencias |
| 12 | `authorizations` | `LND_MigrationAuthorizations` | no | Autorizaciones del titular |
| 13 | `prior-notices` | `LND_MigrationPriorNotices` | no | Avisos previos enviados |
| 14 | `bureau-reports` | `LND_MigrationBureauReports` | no | Reportes vigentes en centrales |
| 15 | `claims` | `LND_MigrationClaims` | no | Reclamos en trámite sobre el reporte |
| 16 | `related-parties` | `LND_MigrationRelatedParties` | no | Personas vinculadas |
| 17 | `arrears-history` | `LND_MigrationArrearsHistory` | condicional (§5.3.17) | Historial de mora mensual |

Las autorizaciones, los avisos, los reportes y los reclamos pueden faltar, pero sin ellos el crédito nace sin
autorización de consulta, reporte ni canales de contacto: se cobra, pero no se consulta ni se reporta a centrales ni
recibe mensajes de cobranza (FR-006).

### 5.3 Columnas de cada parte

**5.3.1 `loans`** — llave `numero`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numero` | número de crédito | sí | Único en la carga (también frente a `charged-off`) y frente a los créditos de Cartera. **Es el número que conserva el crédito** (FR-143) |
| `deudor` | persona | sí | |
| `oficina` | sucursal | sí | Vinculada a una oficina (T13) |
| `linea` | código | sí | Con versión vigente al corte. Modalidad `VIVIENDA`: `Lending.Migration.HousingNotAvailable` hasta K6b (D18) |
| `asesor` | persona | no | Para la colocación por asesor (FR-133) |
| `factura` | texto 40 | si nació de una venta | Debe estar en la parte `sales` con resultado `Migrated` y este número |
| `fechaDeDesembolso` | fecha | sí | Anterior o igual al corte |
| `montoDesembolsado` | monto > 0 | sí | |
| `tipoDeTasa` | `RateKind` | sí | |
| `tasaCorrienteEA` | porcentaje | con `Fixed` | La pactada. Si supera la usura vigente al corte es **aviso** (`Lending.Usury.Exceeded`): un crédito vivo no se rechaza, el motor topa la tasa del día (FR-021) |
| `indice` · `claveDelIndice` · `margen` · `tasaPiso` · `tasaTecho` · `mesesDeReajuste` | como en `Lineas` | con `Variable` | |
| `tipoDeTasaDeMora` · `tasaDeMoraEA` · `factorDeMora` | como en `Lineas` | sí | |
| `convencionDeDias` · `equivalencia` · `redondeoUnidad` · `redondeoModo` · `redondeoMomento` · `sistemaDeAmortizacion` · `prelacion` | como en `Lineas` | no (los de la versión de la línea) | Se sellan en el crédito al activar |
| `periodicidad` | `RepaymentPeriodicity` | sí | |
| `plazo` | entero | sí | Cuotas totales pactadas |
| `preferenciaDeAbono` | `PrepaymentChoice`: `ReduceTerm` 1, `ReduceInstallment` 2 | no | Vacío: un abono extraordinario sin elección queda como saldo a favor (D24) |
| `formaDePago` | `RepaymentChannel` | sí | |
| `pagaduria` | persona | con `PaymasterDeduction` | Persona jurídica; el convenio se asocia en K2 |
| `idEmpleadoEnPagaduria` | texto 30 | con `PaymasterDeduction` | |
| `fechaDeLibranza` | fecha | con `PaymasterDeduction` | Para la prelación (FR-111) |
| `etapaDeCobranza` | texto 30 | no | Se conserva en la preparación desde K1 y se registra al desplegar K4, que la traduce a sus etapas (paso de despliegue, C64) |
| `enJuridico` · `abogado` | sí/no · persona | no | |
| `notas` | texto 400 | no | |

**5.3.2 `installments`** — llave `numeroDeCredito` + `cuota`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numeroDeCredito` | número | sí | En `loans` |
| `cuota` | entero ≥ 1 | sí | |
| `fechaDeVencimiento` | fecha | sí | Creciente con la cuota |
| `capital` | monto ≥ 0 | sí | Capital pendiente al corte |
| `interesCorriente` | monto ≥ 0 | no (0) | Causado hasta el corte y no pagado, incluido el que está en suspenso; sólo en cuotas vencidas o en la cuota en curso |
| `interesDeMora` | monto ≥ 0 | no (0) | Causado y no pagado |
| `seguro` · `otros` | monto ≥ 0 | no (0) | Seguros y otros cargos causados y no pagados |
| `interesProyectado` | monto | no | **Informativo**: el del sistema anterior para las cuotas futuras. El motor causa el suyo desde el corte (D5), así que la cuota puede diferir en centavos |

El plan pendiente debe sumar el saldo de capital: `Lending.Schedule.PrincipalMismatch` (US8-1), en la fila y columna.

**5.3.3 `balances`** — llave `numeroDeCredito` + `concepto`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numeroDeCredito` | número | sí | En `loans` |
| `concepto` | `LoanConcept`: `Principal`, `CurrentInterest`, `DefaultInterest`, `CurrentInterestInSuspense`, `DefaultInterestInSuspense`, `Insurance`, `CollectionFee`, `OtherCharge`, `Advance`, `CreditBalance` | sí | `Principal` obligatorio en cada crédito |
| `valor` | monto ≥ 0 | sí | |

Cuadres por crédito (`Lending.Migration.BalanceMismatch` con el concepto y las dos sumas): `Principal` = Σ `capital`;
`CurrentInterest` + `CurrentInterestInSuspense` = Σ `interesCorriente`; `DefaultInterest` +
`DefaultInterestInSuspense` = Σ `interesDeMora`; `Insurance` = Σ `seguro`; `CollectionFee` + `OtherCharge` = Σ
`otros`. Anticipos (`Advance`) y saldos a favor (`CreditBalance`) sólo vienen aquí. Al activar, cada concepto nace
como una transacción de saldo inicial fechada al corte, **sin hechos contables** (FR-140).

**5.3.4 `risk-data`** — llave `numeroDeCredito`; una fila por crédito de `loans`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numeroDeCredito` | número | sí | |
| `diasDeMora` | entero ≥ 0 | sí | Al corte. Si difiere de lo que dicen las cuotas más allá de la tolerancia, aviso `Lending.Migration.ArrearsMismatch` |
| `calificacion` | `RatingCategory`: `A`..`E` | sí | |
| `subcategoria` | `RatingSubcategory` | no (`None`) | |
| `causa` | `RatingCause` | no | |
| `deterioroCapital` · `deterioroIntereses` · `deterioroOtros` | monto ≥ 0 | sí · no · no | Deterioro individual por concepto |
| `deterioroGeneral` | monto ≥ 0 | no | La parte del crédito en el general; el cierre base la suma por dimensión |
| `fechaDeIngresoAE` | fecha | con `E` | Tiempo en E (K6b) |
| `enSuspenso` | sí/no | no (no) | El crédito ya había suspendido la causación |

El cierre base (`MonthlyClose` `Kind = Base`, fechado al corte, sin hechos) nace al activar con estos datos; el primer
cierre mensual emite sólo la diferencia (FR-140, SC-013).

**5.3.5 `restructurings`** — llave `numeroDeCredito`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numeroDeCredito` | número | sí | |
| `clase` | `TermsVersionCause`: `Restructuring` 5, `Modification` 6 | sí | Se guarda en `LND_MigrationRestructurings.Cause` (C97). La clase la da la causa, no el mecanismo (FR-090): una novación o refinanciación por deterioro de la capacidad de pago es `Restructuring` |
| `vecesReestructurado` | entero ≥ 1 | sí | |
| `fechaUltima` | fecha | sí | |
| `calificacionAlReestructurar` | `RatingCategory` | sí | |
| `cuotasConsecutivasPagadas` | entero ≥ 0 | sí | |
| `creditoAnterior` | número | no | El crédito del que nació; lo sigue el historial de mora (T49) aunque no esté en la carga |

**5.3.6 `co-debtors`** — llave `numeroDeCredito` + `codeudor`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numeroDeCredito` | número | sí | En `loans` o `charged-off` |
| `codeudor` | persona | sí | Distinta del deudor |
| `rol` | `CreditPartyRole`: `CoDebtor` 2, `Guarantor` 3 | no (`CoDebtor`) | |
| `desde` | fecha | no | |

**5.3.7 `collaterals`** — llave `numeroDeCredito` + `tipo` + `referencia`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numeroDeCredito` | número | sí | En `loans` o `charged-off` |
| `tipo` | `CollateralKind` salvo `CoDebtor` | sí | El codeudor va en `co-debtors` |
| `referencia` | texto 40 | sí | Número del pagaré, matrícula, placa, certificado del fondo |
| `valor` · `fechaDeConstitucion` · `vigenteHasta` | monto · fecha · fecha | sí · sí · no | |
| `estado` | `CollateralStatus`: `Constituted` 1, `Valid` 2, `Released` 3, `Extinguished` 4, `Executed` 5 | no (`Valid`) | |
| `descripcion` | texto 400 | no | |
| `valorAvaluo` · `fechaAvaluo` · `poliza` · `polizaVence` · `aseguradora` | monto · fecha · texto · fecha · persona | en prenda e hipoteca | |
| `clasePagare` | `PromissoryNoteKind`: `Physical` 1, `Dematerialized` 2 | con `PromissoryNote` | |
| `canalDeCustodia` · `idExternoPagare` | texto 30 · texto 60 | con `Dematerialized` | Se sellan como `CustodianChannelCode` y `ExternalNoteId` |
| `fondoDeGarantias` · `cobertura` · `comision` | persona · porcentaje · porcentaje | con `GuaranteeFund` | |

**5.3.8 `charged-off`** — llave `numero`. Nace al activar como crédito `WrittenOff`, con saldos en las cuentas de orden
y sin hechos, para registrar sus recuperaciones (`RecuperacionDeCastigada`).

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numero` | número de crédito | sí | No puede estar también en `loans` |
| `deudor` · `oficina` · `linea` | persona · sucursal · código | sí | |
| `fechaDeDesembolso` | fecha | no | |
| `fechaDeCastigo` | fecha | sí | Anterior o igual al corte |
| `capitalCastigado` | monto > 0 | sí | |
| `interesesCastigados` · `otrosCastigados` | monto ≥ 0 | no | |
| `soporteDelCastigo` | texto 200 | sí | Referencia del acta del sistema anterior **como dato** (`HistoricalApprovalReference`, sólo en la preparación), no un punto de sesión: T64 rige lo que se decide en el ERP, no lo migrado (C64) |

**5.3.9 `sales`** — llave `factura` (FR-143). Las ventas de servicios a asociados no vienen aquí (D29).

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `factura` | texto 40 | sí | |
| `fechaDeVenta` | fecha | sí | Anterior o igual al corte |
| `deudor` | persona | sí | |
| `valorOriginal` | monto > 0 | sí | |
| `cobrosExternos` | monto ≥ 0 | no (0) | Cobros recibidos por fuera durante el crédito provisional de la 012, **sumados por factura** con su referencia en `referenciaDeLosCobros` (propuesta de la pregunta I17; si el dueño los quiere uno por uno, hace falta una parte más) |
| `notasYAjustes` | monto | no (0) | Neto de notas crédito (−) y débito (+) hasta el corte |
| `saldoAlCorte` | monto ≥ 0 | sí | = `valorOriginal` − `cobrosExternos` + `notasYAjustes` (`Lending.Migration.BalanceMismatch`) |
| `resultado` | `Migrated`, `CancelledBeforeCutoff` | sí | `Migrated` exige saldo > 0 y un crédito de `loans` con esta factura; `CancelledBeforeCutoff`, saldo 0 |
| `numeroDeCredito` | número | con `Migrated` | |
| `linea` | código | no | En `CancelledBeforeCutoff`, la línea de la obligación en cero; vacío = la línea por defecto de origen comercial |
| `referenciaDeLosCobros` | texto 400 | no | |

**5.3.10 `credit-limits`** — llave `persona` + `tipo` + `linea` + `vigenteDesde`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `persona` | persona | sí | |
| `tipo` | `CreditLimitKind`: `Consolidated` 1, `ByLine` 2, `Revolving` 3 | sí | |
| `linea` | código | con `ByLine` y `Revolving` | En `Revolving`, una línea de origen comercial |
| `valor` | monto > 0 | sí | |
| `vigenteDesde` · `vigenteHasta` | fecha | sí · no | |
| `motivo` | texto 200 | sí | |
| `preferenciaDeAbono` | `PrepaymentChoice` | no | La heredan los créditos de las compras (FR-049) |

Al aplicar, cada cupo se evalúa contra concentración y vinculados con lo que trae la carga (aviso); al activar, el que
exceda un límite queda bloqueado con «cupo por revalidar» (`Cartera.CupoPorRevalidar`, FR-142).

**5.3.11 `blocks`** — llave `persona` + `motivo` + `desde`. Los bloqueos automáticos (mora, validación fallida,
castigado, listas) no se migran: Cartera los deriva después de activar. **Las reclamaciones al seguro y las
insolvencias viajan en esta parte** (C64): no tienen parte propia, van con los motivos `Deceased` e `Insolvency` y sus
columnas, y `LND_InsuranceClaims` y `LND_InsolvencyProceedings` nacen al activar.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `persona` | persona | sí | |
| `motivo` | `DebtorBlockReason`: `Manual` 1, `AssociateWithdrawal` 6, `Deceased` 7, `Insolvency` 8, `Garnishment` 9 | sí | Los demás valores del enum son automáticos y no se migran |
| `numeroDeCredito` | número | no | Si el bloqueo es de un crédito (embargo) |
| `desde` · `hasta` | fecha | sí · no | |
| `soporte` | texto 200 | sí | |
| `aseguradora` · `estadoDeLaReclamacion` | persona · `InsuranceClaimStatus`: `InProcess` 1, `Paid` 2, `Denied` 3, `Withdrawn` 4 | con `Deceased` | Al activar abre la reclamación (`LND_InsuranceClaims`, `InProcess` por defecto, T102) |
| `estadoDeLaInsolvencia` | `InsolvencyProceedingStatus`: `Negotiating` 1, `Agreement` 2, `Failed` 3 | con `Insolvency` | Al activar abre el proceso (`LND_InsolvencyProceedings`, `Negotiating` por defecto) |

**5.3.12 `authorizations`** — llave `persona` + `finalidad` + `canal`. Nacen al activar en las autorizaciones de la
plataforma (`IAutorizacionDeDatos.Agregar`, `Origin = Migration`, T69).

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `persona` | persona | sí | Deudor o codeudor de la carga |
| `finalidad` | `DataAuthorizationPurpose`: `GeneralTreatment` 1, `CreditProcessing` 2, `CreditBureauQuery` 3, `CreditBureauReport` 4, `ContactChannel` 5 | sí | |
| `canal` | `DataAuthorizationChannel`: `Sms` 1, `WhatsApp` 2, `Email` 3, `Call` 4, `Visit` 5 | con `ContactChannel` | |
| `estado` | `Accepted`, `Revoked` | no (`Accepted`) | |
| `fecha` | fecha | sí | |
| `versionDelTexto` | texto 30 | no | Versión de la política con esa finalidad; vacío = sin versión |
| `evidencia` | texto 200 | no | Dónde está la evidencia (formato físico, archivo). **Vacío = sin evidencia**: el crédito vive y se cobra; se bloquean la consulta y el reporte a centrales y los mensajes de cobranza hasta obtenerla (FR-006). La evidencia se adjunta después como `DataAuthorization` |
| `canalDeObtencion` | texto 60 | no | |

**5.3.13 `prior-notices`** — llave `numeroDeCredito` + `episodio`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numeroDeCredito` | número | sí | |
| `episodio` | fecha | sí | Vencimiento de la primera cuota impaga del episodio; si no es el episodio vigente según las cuotas, aviso `Lending.Migration.EpisodeNotCurrent` y no se migra |
| `comunicacionesExigidas` | 1 o 2 | sí | |
| `fechaDeEnvio1` · `canal1` · `referencia1` | fecha · `MessageChannel` · texto 100 | sí | La referencia (guía, id del envío) es la constancia; sin ella, el aviso nace `AwaitingEvidence` |
| `fechaDeEnvio2` · `canal2` · `referencia2` | | con 2 | En un día distinto del primero |

**5.3.14 `bureau-reports`** — llave `numeroDeCredito` + `central`. Un reporte negativo vigente nace al activar en
`LND_PriorNotices.MigratedReportedNegativeSince` del episodio (C64).

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numeroDeCredito` | número | sí | En `loans` o `charged-off` |
| `central` | texto 20 | sí | Código de la central (`BureauCode`) |
| `ultimoPeriodoReportado` | mes | sí | |
| `novedadReportada` | texto 20 | sí | Tal como se reportó |
| `datoNegativo` | sí/no | sí | |
| `episodio` | fecha | con negativo | |
| `conAvisoPrevio` | sí/no | con negativo | «no»: el primer archivo de esa central lo **retira** (FR-123) |

**5.3.15 `claims`** — llave `numeroDeCredito` + `central` + `fechaDeRadicacion`. Sólo reclamos en trámite.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numeroDeCredito` | número | sí | |
| `central` | texto 20 | sí | |
| `tipo` | `CreditReportClaimKind`: `Claim` 1, `Impersonation` 2 | sí | |
| `fechaDeRadicacion` | fecha | sí | El plazo legal corre desde aquí (15 + 8 días hábiles) |
| `prorrogado` | sí/no | no | |
| `descripcion` | texto 400 | no | |

**5.3.16 `related-parties`** — las mismas columnas y reglas de la plantilla 2 (§2). Lo registrado a mano antes de
activar prevalece.

**5.3.17 `arrears-history`** — llave `numeroDeCredito` + `mes`. Información, sin transacciones ni hechos (origen
`Migration` en `LND_LoanArrearsHistory`).

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numeroDeCredito` | número | sí | En `loans` o `charged-off` |
| `mes` | mes | sí | Anterior al mes del corte |
| `moraMaximaDias` | entero ≥ 0 | sí | Mora máxima del mes |
| `moraAlCierreDias` | entero ≥ 0 | no | |

**Opcional**, salvo para los créditos de los segmentos cubiertos de una cooperativa obligada a la pérdida esperada a
la fecha de activación, o con una obligación registrada desde una fecha posterior (FR-139, FR-142): para ellos, el
historial debe estar completo en los meses que pide `ReglaDePerdidaEsperada` para **activar** (`ArrearsHistoryComplete`,
US8-10). Al aplicar, lo que falte queda como **aviso** `Lending.Migration.ArrearsHistoryRequired` por crédito y meses —no niega,
porque la carga complementaria de §7 sólo se admite sobre una carga aplicada (FR-140)— y la carga complementaria lo completa antes
de activar (K6a; corregido en la corrida final, T1064, 2026-10-17).

### 5.4 Validaciones entre partes al aplicar (`ApplyMigrationLoadCommand`)

Todo o nada sobre la carga; con errores responde 422 `Lending.Migration.CrossPartErrors` y cada error lleva
`sheet` = la clave de la parte, la tanda, la fila y la columna:

1. Las partes obligatorias tienen filas (`Lending.Migration.PartMissing`).
2. Cada número de crédito citado existe en `loans` (o en `charged-off` donde se admite).
3. Cada crédito de `loans` tiene sus cuotas, sus saldos y su fila de `risk-data`.
4. Los cuadres de §5.3.3 y la suma del plan (`Lending.Schedule.PrincipalMismatch`).
5. `sales` y `loans` se corresponden uno a uno en las ventas migradas.
6. El historial de mora está completo donde es obligatorio (§5.3.17).
7. Los cupos de una persona no se cruzan en el tiempo para el mismo tipo y línea.
8. Las personas citadas siguen vivas en el maestro.
9. Lo registrado a mano antes de activar para la misma persona y concepto (cupos, bloqueos, autorizaciones,
   vinculados) **prevalece** sobre la carga, y la revisión lo informa en `extra.manualPrevails[]`.

Después calcula y guarda:

- **El cuadre** (FR-141): por concepto, modalidad, sub-segmento, categoría y cuenta, contra
  `IContabilidadParaCartera.SaldosDeCuentasMapeadasAsync(corte, combinaciones)` (T58).
- **La conciliación de ventas** (FR-143): lista sin consumirlos los mensajes `VentaACreditoRegistrada` acumulados
  hasta el corte (`IMensajesEntrantes.PendientesAsync`, T19) y los concilia por factura con la parte `sales`:
  migrada, cancelada antes del corte o no migrada con saldo.

Cada diferencia queda en `LND_MigrationLoadDifferences` con su clase (`MigrationDifferenceKind` de `data-model.md`):
`BalanceCheck`, `SaleNotMigrated`, `SaleOutsideLending` (ventas de servicios a asociados confirmadas antes de
`CreditWithoutLending`, marcadas desde la lista como «aceptadas fuera de Cartera (D29)», T83).

### 5.5 Aceptación de diferencias y activación

- Cada diferencia se acepta con motivo y la referencia a un **punto de sesión** del órgano que la aprueba
  (`SessionItem`, T64): no hay texto libre de «acta». La ruta recibe una lista y cada diferencia guarda su propia
  aceptación (venta por venta en las de ventas). La aceptación es de esa carga (D17).
- La activación se niega mientras haya diferencias sin aceptar (`Lending.Activation.DifferencesNotAccepted`), y
  **recalcula** cuadre y conciliación con los saldos contables de ese momento: si difieren de los aceptados, la
  aceptación deja de valer y hay que volver a aceptar (FR-142). Exige el punto de acta de la activación (C61).
- Al activar, a cada venta conciliada se le responde a Comercial (`ObligacionCreada` con estado `Migrated` o
  `CancelledBeforeCutoff`; `ObligacionRechazada` no reprocesable a la aceptada sin migrar o fuera de Cartera). Una
  venta fechada hasta el corte que llega **después** de activar queda fuera de Cartera con
  `Lending.Sale.NotIncorporated` (C63, pregunta I16).

Errores y avisos de la migración: los de `data-model.md` §10.1, literales (C71) —`Lending.Migration.LoadNotDraft`,
`.CrossPartErrors`, `.ArrearsHistoryRequired`— y los que fija esta plantilla: `Lending.Migration.PartMissing`,
`.BalanceMismatch`, `.HousingNotAvailable`; avisos `Lending.Migration.ArrearsMismatch` y `.EpisodeNotCurrent`.

```jsonc
// POST /api/lending/migration/loads/{id}/apply   (Idempotency-Key)  →  200
{ "loadPublicId": "8a10…", "cutoffDate": "2026-11-30", "status": "Applied",
  "counts": { "loans": 4210, "installments": 61873, "chargedOff": 96, "sales": 312 },
  "balanceCheck": { "differences": 2, "rows": [
     { "concept": "Principal", "modality": "CONSUMO", "subsegment": "ConLibranza", "category": "A",
       "account": "14420501", "lending": 1840220500.00, "accounting": 1840219800.00, "difference": 700.00,
       "differencePublicId": "d1e2…" } ] },
  "salesReconciliation": { "migrated": 288, "cancelledBeforeCutoff": 20, "notMigratedWithBalance": 4,
                           "acceptedOutsideLending": 0 },
  "manualPrevails": [ { "person": "…", "concept": "CreditLimit", "detail": "Cupo otorgado a mano el 2026-11-20" } ],
  "warnings": 37 }
```

(Cifras y cuenta ilustrativas.)

---

## 6. Cobros provisionales (`lending.provisional-collections`, FR-142)

Los cobros recibidos por fuera, durante el crédito provisional de la 012, de **ventas confirmadas después del
corte**, que Contabilidad o Tesorería registraron contra la cuenta por cobrar provisional. La activación los **valida
y guarda**; los **aplica** el consumidor de Comercial cuando nace la obligación de su venta, porque al activar esa
obligación todavía no existe (C44, C62).

**Cómo se usa.**

1. `GET /api/lending/activation/provisional-collections/template.xlsx`.
2. `POST /api/lending/activation/provisional-collections/review` (`ReviewProvisionalCollectionsCommand`, `api.md`
   §19.3): revisión previa contra las ventas de la carga; deja el archivo como adjunto y devuelve
   `stagedAttachmentPublicId`, el SHA-256, las filas y los errores. No guarda filas; no hay `apply` en esta ruta.
3. `POST /api/lending/activation` (`ActivateLendingCommand`, `Lending.Activation.Execute`) con
   `provisionalCollectionsAttachmentPublicId` = el adjunto revisado. La activación vuelve a correr la revisión
   **dentro de su transacción**: con un error, no se activa y responde la lista (todo o nada con la activación). El
   SHA-256 del archivo entra en la huella de idempotencia de la activación.
4. Las filas válidas quedan guardadas con la activación en `LND_ProvisionalCollections` (H; `LoanId`, `OperationId`,
   `AppliedAt` y `MessagePublicId` son `[EscrituraUnica]`). Cuando el consumidor de Comercial crea la obligación de la
   venta, aplica en la misma transacción los cobros de esa factura:
   cada uno es una `LoanOperation` `ProvisionalCollection` con su fecha valor, sin recaudador y en firme desde que se
   aplica, y emite `CobroProvisionalIncorporado` (contra la cuenta por cobrar provisional, nunca contra caja ni
   bancos). La clave `{activación}:{fila}` hace que reprocesar la venta no lo aplique dos veces.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `factura` | texto 40 | sí | Una venta con `VentaACreditoRegistrada` acumulado, confirmada **después** del corte; no una venta de la migración |
| `valor` | monto > 0 | sí | La suma de los cobros de una factura no supera el valor a crédito de la venta (`Lending.ProvisionalCollection.ExceedsSale`) |
| `fechaValor` | fecha | sí | Entre la fecha de la venta y el registro de la activación |
| `referenciaContable` | texto 60 | sí | El comprobante o registro que redujo la cuenta por cobrar provisional |
| `observaciones` | texto 400 | no | |

Llave: `factura` + `referenciaContable`. Un cobro de una venta cuya obligación no llega a crearse (mensaje rechazado)
queda listado en `/cartera/activacion` como «cobro sin obligación» hasta que la venta se reprocesa.

## 7. Historial de mora complementario (`lending.arrears-history`, K6a, FR-140, T49)

Completa el historial de los créditos de la carga **como información**, sin transacciones ni hechos.
`ImportArrearsHistoryCommand`, `/api/lending/risk/arrears-history` (+ `template.xlsx`, `import`),
`Lending.ArrearsHistory.Import`. Cada aplicación es una carga nueva (`LND_ArrearsHistoryLoads`, con su SHA-256) y sus
filas entran a `LND_LoanArrearsHistory` con origen `ComplementaryLoad`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `numeroDeCredito` | número | sí | Un crédito de la carga `Applied` (antes de activar) o un crédito migrado (después) |
| `mes` | mes | sí | Un mes **sin** cierre mensual de Cartera (`Lending.ArrearsHistory.MonthClosed`) |
| `moraMaximaDias` | entero ≥ 0 | sí | |
| `moraAlCierreDias` | entero ≥ 0 | no | |
| `fuente` | texto 120 | sí | La misma en todas las filas |

- **Cuándo**: desde que hay una carga aplicada (también antes de activar y antes de registrar una adopción
  voluntaria) hasta el primer cierre en pérdida esperada de los segmentos cubiertos; después,
  `Lending.ArrearsHistory.LoadClosed` (FR-140).
- **Qué manda por mes** (`HistorialDeMora.Construir`): el cierre mensual sobre la carga complementaria más reciente, y
  ésta sobre lo migrado. Volver a cargar un mes no borra nada: la carga más reciente manda.
- La revisión devuelve en `extra.completeness[]`, por segmento cubierto, cuántos créditos quedan completos con los
  meses que pide `ReglaDePerdidaEsperada` y cuáles faltan.

## 8. Convenios de pagaduría (`lending.payroll-agreements`, K2, FR-108)

Escribe `LND_PayrollAgreements`, `LND_PayrollAgreementVersions`, `LND_PayrollAgreementContacts` y
`LND_PayrollAgreementResultCodes`. `ImportPayrollAgreementsCommand`, `/api/lending/payroll-agreements` (+
`template.xlsx`, `import`).

### Hoja `Convenios`

Llave: `codigo` + `vigenteDesde` (una versión del convenio).

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | |
| `vigenteDesde` · `vigenteHasta` | fecha | sí · no | |
| `pagaduria` | persona | sí | Persona jurídica. La nómina propia exige la persona jurídica con el NIT de la cooperativa (I13) |
| `esNominaPropia` | sí/no | no (no) | Sin archivo: Nómina aplica por el contrato (FR-112) |
| `nombre` | texto 120 | sí | |
| `diaDeCorte` · `diaDeEnvio` · `diaDePago` | entero 1–31 | sí, salvo nómina propia | |
| `antelacionDias` | entero | sí, salvo nómina propia | Días antes del envío en que se genera el archivo |
| `formatoDeIda` · `formatoDeVuelta` | código de formato bancario | sí, salvo nómina propia | Ámbitos `PayrollDeductionRequest` y `PayrollDeductionResult` |
| `topeDeDescuento` | porcentaje | sí | No más que el límite `LIBRANZA_CAPACIDAD_DESCUENTO` (`Lending.Parameter.BelowLegalFloor`) |
| `incluyeVencido` | sí/no | no (no) | |
| `ventanaDeGiroDias` | entero | no (`Libranza.VentanaDeGiroDias`) | No más que el límite legal |
| `accionNoAplicado` | `NonDeductionFollowUpAction`: `IncludedInNextFile`, `MovedToCashPayment`, `SentToCollection` | no (`MovedToCashPayment`) | Regla de FR-115 |
| `formaDePagoAlterna` | `RepaymentChannel` | no (`Cashier`) | Al retirarse el deudor sin nuevo empleador (FR-116) |
| `notas` | texto 400 | no | Reglas de novedades del convenio |

### Hoja `Contactos`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `convenio` | código | sí | |
| `persona` | persona | sí | Correo y teléfono salen del maestro |
| `cargo` | texto 80 | no | |
| `recibeArchivo` | sí/no | no (no) | |

### Hoja `CodigosDeRespuesta`

Llave: `convenio` + `vigenteDesde` + `codigo`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `convenio` · `vigenteDesde` | código · fecha | sí | Una versión del convenio |
| `codigo` | texto 10 | sí | Como lo escribe la pagaduría en su archivo de vuelta |
| `descripcion` | texto 120 | sí | |
| `descontado` | sí/no | sí | |
| `motivo` | `MotivoDeNoDescuento`: `DeductionCap` 1, `InsufficientNet` 2, `Termination` 3, `Leave` 4, `Disability` 5, `Other` 99, más `DeductionPriority` (C56) | con «no» | |

## 9. Matriz contable de Cartera (Contabilidad, enmienda E-01)

`ImportLendingPostingRulesCommand` y `GetLendingRulesTemplateQuery` en `/api/accounting/lending/rules`
(`template.xlsx`, `import`). Escribe `ACC_LendingPostingRules` con las mismas reglas que crear una a una. La
semántica (qué roles exige cada operación, resolución, especificidad, completitud) es de `contabilidad.md`; esta
sección fija sólo la forma del archivo. La contadora la diligencia hasta dejar vacío el reporte de completitud.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `operacion` | `OperacionesDeCartera` | sí | Un hecho de §2.11 de las transversales |
| `rol` | `RolesDeCuentaDeCartera` | sí | Un rol de la operación |
| `linea` · `modalidad` · `subsegmento` | código o `*` | según el rol | Validados contra `IDimensionesDeCartera` (Contabilidad no lee `LND_`) |
| `categoria` | `A`..`E` o `*` | según el rol | Las subcategorías van a su letra |
| `concepto` | `LoanConcept` o `*` | según el rol | |
| `empleado` · `enGracia` · `porCuentaDelAsociado` · `reestructurado` | sí/no/indiferente | no | |
| `claseDeRecaudador` | valor de `ClaseDeRecaudador` o `*` | según el rol | Obligatorio en `RecaudoPorAplicar` |
| `motivo` | texto 30 o `*` | según el rol | |
| `sucursal` · `centroDeCosto` | sucursal · centro de costo, o `*` | no | |
| `cuenta` | cuenta contable | sí | Auxiliar de movimiento, activa y habilitada para `CAR` |
| `vigenteDesde` · `vigenteHasta` | fecha | sí · no | |
| `notas` | texto 400 | no | Quién decidió y por qué |

Vacío y `*` son lo mismo. Llave: `DimensionKey` + `vigenteDesde`; dos versiones que se cruzan,
`Accounting.LendingRule.Overlaps`; una versión que empieza en o antes del último hecho ya contabilizado de esa
operación, `Accounting.LendingRule.RetroactiveOverPosted`. La hoja «Instrucciones» se genera de
`OperacionesDeCartera` y `RolesDeCuentaDeCartera`, no de este texto.

| operacion | rol | modalidad | subsegmento | categoria | concepto | empleado | cuenta | vigenteDesde |
|---|---|---|---|---|---|---|---|---|
| CreditoDesembolsado | Capital | CONSUMO | ConLibranza | A | Principal | no | 14420501 | AAAA-MM-01 |
| CreditoDesembolsado | Capital | CONSUMO | * | A | Principal | sí | 14650501 | AAAA-MM-01 |
| InteresCausado | Ingreso | CONSUMO | * | * | CurrentInterest | | 41502501 | AAAA-MM-01 |

(Cuentas ilustrativas: salen del plan de cuentas de la cooperativa.)

## 10. Valores nacionales (`saas.national-values`, registro central, T37)

`ImportNationalValuesCommand` (`Application/Saas/NationalValues`), `/api/saas/national-values` (+ `template.xlsx`,
`import`, `export`). Sólo el maestro, con segundo factor (`RequireMasterAdminAttribute`). Escribe `ADM_NationalValues`
en la base administrativa, con su propia transacción sobre `IAdminDbContext` (C82, §0.8); la réplica los copia a cada cooperativa en su pasada siguiente (T39). Los documentos
normativos (`CatalogoDeModalidades`, `SarcAnexo1`…) son JSON y no van por plantilla: `/api/saas/regulatory-documents`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | `CodigosDeValorNacional`: `USURA`, `IBR`, `IPC`, `TOPE_TASA_VIVIENDA`, `UVR`, `SMMLV`, `UVT`, `FESTIVO`, `LIMITE` | sí | `TOPE_TASA_VIVIENDA` y `UVR`: `Import.Cell.NotYetAvailable` hasta K6b |
| `clave` | texto 40 | según el código | `USURA`: una modalidad certificada del `CatalogoDeModalidades` vigente; `IBR`/`IPC`: plazo o serie; `FESTIVO`: la fecha; `LIMITE`: un código de `CatalogoDeLimitesLegales` |
| `vigenteDesde` | fecha | sí | |
| `vigenteHasta` | fecha | no | Sólo cuando la norma lo trae (período certificado de la usura) |
| `fechaDeReferencia` | fecha | no | El período que mide un índice, distinto de cuándo se publica |
| `valor` | decimal, hasta 10 decimales | sí | Las tasas (`EA`, `NominalMV`) y las fracciones van **como fracción**, no en puntos: 26,50 % se escribe `0,2650`. `FESTIVO`: 1 |
| `unidad` | `EA`, `NominalMV`, `Pesos`, `Fraccion`, `Dias`, `Meses`, `Horas` | sí | La que admite el código (y, en `LIMITE`, la del catálogo) |
| `fuente` | texto 300 | sí | Norma, resolución o certificación, con su enlace |
| `corrigeA` | `PublicId` | no | Corrección de un valor ya registrado: fila nueva con `SupersedesPublicId`; exige `reason` |

Reglas:

- **Sólo inserción** (T38). Una fila igual a una vigente («mismo código, clave, desde y valor») es «sin cambio». La
  misma llave con otro valor y sin `corrigeA` es `NationalValues.ConflictingValue`: el mensaje dice que una corrección
  se registra con `corrigeA`.
- Cada fila recibe su `Sequence` en el orden del archivo y `RegisteredAt` = el instante de aplicar.
- Una corrección que afecta días ya causados dispara, al replicarse, la corrida `NationalValueCorrection` en cada
  cooperativa (T28).
- `GET /api/saas/national-values/export?codes=&from=` devuelve el mismo formato, para llevar los valores de un ambiente
  a otro (B5).

| codigo | clave | vigenteDesde | vigenteHasta | valor | unidad | fuente |
|---|---|---|---|---|---|---|
| USURA | CONSUMO | AAAA-MM-01 | AAAA-MM-DD | 0,2650000000 | EA | Certificación de la Superintendencia Financiera (ilustrativo) |
| FESTIVO | AAAA-MM-DD | AAAA-MM-DD | | 1 | Dias | Ley 51 de 1983 |
| LIMITE | AVISO_PREVIO_DIAS | AAAA-MM-01 | | 20 | Dias | Ley 2157 de 2021 |

---

## 11. Lo que este contrato fija y deben recoger los demás artefactos

- Esta plantilla es la **única fuente** de las hojas, columnas, llaves y orden de carga de Cartera; `api.md` la cita y
  no la repite. La mecánica de revisión y aplicación es la de la 012 (§0.5).
- `RutasDePlantilla.MapPlantilla` generalizado (§0.8) y las pruebas de las dieciséis plantillas de la 012.
- Varios archivos por parte de la migración (§5.1): `LND_MigrationLoadParts` es una fila **por archivo** (tanda), no
  por parte.
- Los cobros provisionales se guardan con la activación y se aplican al crearse la obligación de su venta (§6).

## Nombres fijados en este documento

Aplican T1–T4; `data-model.md`, `api.md` y `tasks.md` los copian o este contrato se corrige antes de publicarse.
Ante duda rige el dueño del tema (§7.6 de las transversales): este contrato, las columnas de importación; `api.md`,
rutas y permisos; `data-model.md`, tablas, enums y errores.

- **Comandos y consultas**: `ImportCreditLinesCommand`, `ImportRelatedPartiesCommand`,
  `ImportContributionBalancesCommand`, `ImportRestrictiveListCommand`, `CreateMigrationLoadCommand`,
  `ImportMigrationPartCommand`, `ImportPayrollAgreementsCommand`. De `api.md`: `AcceptMigrationDifferencesCommand`
  (lista de diferencias) y `ReviewProvisionalCollectionsCommand`.
- **Clave de plantilla**: `saas.national-values`; claves de parte `lending.migration.{loans, installments, balances,
  risk-data, restructurings, co-debtors, collaterals, charged-off, sales, credit-limits, blocks, authorizations,
  prior-notices, bureau-reports, claims, related-parties, arrears-history}`.
- **Rutas** (las fija `api.md`): `POST /api/lending/migration/loads/{id}/differences/accept`; `?replace=true` en la
  importación de una parte; `provisionalCollectionsAttachmentPublicId` en `POST /api/lending/activation`;
  `GET /api/lending/activation/provisional-collections/template.xlsx` y `POST …/provisional-collections/review`.
- **Tabla**: `LND_ProvisionalCollections` (`ProvisionalCollection`, H), adoptada por `data-model.md` (C44).
- **Enums**: todos los de `data-model.md`, con su numeración (C65): `RepaymentPeriodicity`, `RepaymentChannel`,
  `InterestTaxTreatment`, `DebtorKind`, `InsuranceBase`, `CollateralKind`, `CollateralStatus`, `PromissoryNoteKind`,
  `PrepaymentChoice`, `CreditLimitKind`, `DebtorBlockReason`, `CreditPartyRole`, `RelatedPartyBasis`,
  `RestrictiveListEntityKind`, `MigrationDifferenceKind`, `NonDeductionFollowUpAction`, `MotivoDeNoDescuento`,
  `DataAuthorizationPurpose`, `DataAuthorizationChannel`, `InsuranceClaimStatus`, `InsolvencyProceedingStatus`,
  `CreditReportClaimKind`, `TermsVersionCause` (clase de reestructuración), `LoanConcept`, `RatingCategory`,
  `RatingSubcategory`, `RatingCause`. Ninguno se fija aquí.
- **Errores**: de `data-model.md` §10, literales (C71): `Lending.Migration.LoadNotDraft`, `.CrossPartErrors`,
  `.ArrearsHistoryRequired`, `Lending.Activation.DifferencesNotAccepted`, `Lending.Sale.NotIncorporated`. Fijados aquí:
  `Lending.CreditLine.RateBandOverlap`; `Lending.Migration.PartMissing`, `.BalanceMismatch`, `.HousingNotAvailable`,
  avisos `.ArrearsMismatch` y `.EpisodeNotCurrent`; `Lending.ProvisionalCollection.ExceedsSale`;
  `Lending.ArrearsHistory.MonthClosed`, `.LoadClosed`; `NationalValues.ConflictingValue`.
- **Formatos de texto**: número de crédito (`^[A-Za-z0-9-]+$`, 20, mayúsculas) y mes (`AAAA-MM`).
- **Columnas** de cada hoja, con los nombres de las tablas de §1 a §10.

## Conciliación

Los puntos que este contrato dejó para conciliar se decidieron en `decisiones-transversales.md` §7 (siglas `PLT-n`).
El cuerpo ya aplica cada decisión.

| Punto | Decisión |
|---|---|
| PLT-1 cobros provisionales | C44 y C62: tabla `LND_ProvisionalCollections` (K1, `[EscrituraUnica]`); la activación valida y guarda, el consumidor de Comercial aplica al nacer la obligación con clave idempotente (§6) |
| PLT-2 reclamaciones e insolvencias migradas | C64: viajan en la parte `blocks` (motivos `Deceased`, `Insolvency`) y nacen al activar (§5.3.11) |
| PLT-3 permiso de saldos de aportes | C85: se reutiliza `Lending.CreditLimits.Assign` (§0.7) |
| PLT-4 enumeraciones | C65: rigen nombres y numeración de `data-model.md` §9 y su tabla de la parte 1; la clase de reestructuración es la de `TermsVersionCause` |
| PLT-5 tasa de mora relativa | Sin cambio: `factorDeMora` como factor; queda **para validar por la contadora** (§7.7) |
| PLT-6 soporte del castigo migrado | C64: dato histórico (`soporteDelCastigo` → `HistoricalApprovalReference`, sólo en la preparación); T64 rige lo que se decide en el ERP |
| PLT-7 cobros externos por factura | Pregunta I17 (§7.7): rige la propuesta, sumados por factura con su referencia (§5.3.9) |
| PLT-8 topes del ejecutor | C64: varios archivos por parte; `LND_MigrationLoadParts` lleva una fila por archivo (§5.1) |
| PLT-9 valores nacionales en la base administrativa | C82: `ImportNationalValuesCommand` reutiliza definición, lector y reglas con su propia transacción sobre `IAdminDbContext` (§0.8, §10) |

