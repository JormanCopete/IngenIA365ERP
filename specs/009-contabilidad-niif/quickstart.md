# Quickstart: Contabilidad NIIF

**Feature**: 009 | **Date**: 2026-09-14

Cómo comprobar la feature de punta a punta, entrega por entrega (R17).

## 1. Compilar y pruebas sin contenedores

```bash
dotnet build IngenIA365ERP.CI.slnf -c Release
```

```bash
dotnet test tests/IngenIA365ERP.Application.Tests --filter "FullyQualifiedName~Accounting|FullyQualifiedName~Audit.RegisterOptionAccess"
```

```bash
dotnet test tests/IngenIA365ERP.Architecture.Tests
```

Deben quedar verdes, entre otras: `AccountLineRulesTests` (cada regla de
`contracts/contabilizacion.md` §2 con su código de error), `AccountingPosterTests` (agrega sin
guardar; descuadre no agrega nada; número asignado; `FirstMovementAt`), `PostDocumentCommandHandlerTests`
(cuatro ojos; período cerrado), `ReverseDocumentCommandHandlerTests` (no reversar dos veces ni una
reversión; documento de módulo → `ModuleOwned`), `InitializeAccountingCommandHandlerTests`
(catálogo completo; bloqueo tras la primera auxiliar), `CreateAccountCommandHandlerTests`
(prefijo, longitud, nivel de movimiento), `ImportAccountCatalogCommandHandlerTests` y
`LasSemillasJsonSonCoherentes` (padre, longitud, naturaleza, rubro), `ReintentoPorConcurrenciaBehaviorTests`,
`RepartoDePermisosTests` (Operator: `Vouchers.Create` sí, `Vouchers.Post` no; ReadOnly sólo
`View`), y las de arquitectura `NingunModuloEscribeMovimientosFueraDelContrato`,
`LaContabilidadNoTieneCuentasEnCodigo`, `LosEndpointsProtegidosExigenPermiso` (con los archivos
de Accounting), `PrincipioVIII` (con `Application.Accounting` dentro), `PrincipioXI`
(`Entities.Accounting.Transactions`), `LasPantallasDicenQueEstanCargando` (con `Contabilidad`),
`PrincipioXII_MigracionesDestructivas` (la migración lleva el marcador).

## 2. Pruebas de integración (Docker Desktop encendido)

```bash
dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~Accounting|FullyQualifiedName~ContabilidadDeNomina"
```

`Accounting/ContabilidadNiifTests` (colección «Nomina e2e», sobre `NominaE2E.PrepararAsync`):
iniciar la contabilidad con PUC Solidario y nivel 6 → crear auxiliares con reglas → borrador con
línea sin tercero rechazada en `/validate` → contabilizar con dos clientes concurrentes (números
consecutivos, sin duplicados) → reversar → balance de prueba cuadra tras cada paso
(`ElBalanceDePruebaCuadra`) → cerrar mes con borrador (422) → contabilizar → cerrar → nómina con
fecha en el mes cerrado (422 que llega a Nómina) → reabrir con motivo → auditoría
(`RegisterOptionAccess`, `Accounting.Report.Exported`, antes/después de una regla de cuenta) →
sólo lectura recibe 404 `Generic.NotFound` en toda escritura. `Payroll/ContabilidadDeNominaTests`:
aprobar corrida → comprobante `NM` contabilizado con la EPS como tercero en aportes → concepto a
cuenta de agrupación → aprobación falla nombrándolo, cero huérfanos → reversar → neto cero.
E2 añade `AperturaTests`, `CierreDeEjercicioTests`, `InformesTests` (los tres formatos con los
mismos totales); E3 una prueba por módulo (una operación → un comprobante cuadrado o
`Accounting.Parameterization.Missing`); E4 `ConciliacionTests`, `PresupuestoTests`,
`CertificadosTests`, `ExogenaTests` (XML bien formado y estructura `Dmuisca`), `ActivosTests`
(una corrida por período).

## 3. Migración y semilla en local

```bash
dotnet run --project tools/IngenIA365ERP.DbMigrator -- migrate --scope all
```

Antes, en cualquier base con datos, correr `specs/009-contabilidad-niif/diagnostico-libros.sql`:
si `documentos` o `movimientos` no es cero, la migración se niega (guarda de `Up()`); en ese caso
**no se despliega** y se decide con el dueño. Tras migrar, `ACC_AccountCatalogs` debe tener
`SOLIDARIO` y `COMERCIAL` con sus conteos, `ACC_FinancialStatementItems` los rubros del grupo 2,
`ACC_VoucherTypes` los tipos sembrados y `ACC_CrossDocumentTypes` los ocho cruces. Los 30 códigos `Accounting.*` de `SEC_Permissions` los siembra la **API al arrancar** (`AccountingPermissionCatalogSeeder`), no el migrador: recién migrada la base, esa cuenta da 0 hasta el primer arranque. Con dos cooperativas declaradas con base propia, `--scope all` se niega (Principio IV): usar `migrate --scope cooperativas`, que migra y siembra cada base.

## 4. Verificación manual en QA — E1 (núcleo)

Con tres usuarios de `coop_prueba` (administrador, Operador, sólo lectura) y el contador:

1. **Contador valida los catálogos**: abre Configuración inicial › Catálogos, revisa Solidario y
   Comercial contra la norma (muestreo por clase) y pulsa «Validar»; queda `validatedAt/By`.
2. **Administrador inicia** con Solidario (CUIF, 2.110 cuentas), nivel 6, grupo 2, ejercicio
   2026, sucursal principal, cuatro ojos **activo**; ve el plan hasta nivel 4; intenta cambiar el
   nivel tras crear la primera auxiliar → rechazado con la razón.
3. **Auxiliares**: crea `11050501` (agrupación) y `1105050101` (movimiento, exige sucursal
   explícita), `16050501` (exige tercero y documento `FV`), `24352501` (impuesto retefuente 4 %,
   exige base), `51050301` (Nómina, exige centro); Cartera no puede parametrizar `16050501`
   hasta habilitarla; una de agrupación no se ofrece en ningún buscador. **Cuentas propias**: bajo
   `3205 RESERVA PROTECCIÓN DE APORTES` (el CUIF no trae subcuentas) crea `320505` y bajo ella
   `32050501`; bajo `1105` (que sí trae `110505`) el botón no se habilita y dice por qué.
4. **Operador digita** un comprobante `CG` de 20 líneas sólo con teclado; línea sin tercero
   marcada al salir; base con valor descuadrado avisa; guarda borrador; **no** ve «Contabilizar».
5. **Administrador contabiliza** el borrador de otro; intenta contabilizar uno propio → cuatro
   ojos lo rechaza; el número es consecutivo; el comprobante ya no se edita; «Anular» pide motivo
   y crea la reversión referenciada.
6. **Nómina**: vincular la EPS a una persona; aprobar una liquidación; abrir el `NM` desde
   Contabilidad: sólo lectura, «Ver en Nómina»; reversar desde Nómina; el neto queda en cero.
7. **Auditoría**: en `/admin/auditoria` filtrar `Module=Navigation` y ver las opciones abiertas
   por cada usuario; ver el antes/después de la regla cambiada en la auxiliar.
8. **Sólo lectura**: ve plan, comprobantes e informes; ningún botón de escritura; cambiar de
   cooperativa recalcula los botones sin F5.

## 5. Verificación manual en QA — E2, E3, E4

- **E2**: cargar la apertura con la plantilla (200 filas, dos erróneas a propósito), corregir,
  contabilizar; balance de prueba del 1 de enero = totales del archivo; libro auxiliar de 1105
  hasta la línea en 4 clics; exportar la vista por terceros a Word con los mismos totales;
  cerrar los doce meses y el ejercicio; estado de resultados sin/con cierre; reabrir el año.
- **E3**: una operación por módulo (desembolso, recaudo, factura de venta, cheque, CDT) y su
  comprobante en `/documents/by-source`; la anulación del módulo reversa **ese** comprobante;
  un módulo sin parametrización falla con el mensaje que dice qué configurar.
- **E4**: extracto real de un banco (definir mapeo con vista previa, cargar dos veces, conciliar,
  nota bancaria → borrador, informe a cero, cerrar, reabrir el período → «desactualizada»);
  presupuesto copiado +5 % y ejecución con profundización; certificados 2026 (PDF, correo,
  reexpedir tras una reversión); exógena 1001 con un tercero sin dirección y otro bajo la
  cuantía, exportar Excel y XML y pasar el XML por el **prevalidador de la DIAN** (SC-012);
  activo de 60 meses, dos corridas, reversar la segunda, cambiar vida útil, baja.

## 6. Producción

Sólo con autorización expresa del dueño («sí, empujalo»). Antes: `pg_dump` de cada base de
cooperativa aunque el libro esté vacío (Principio XII: la migración es destructiva y lleva
`MIGRACION-DESTRUCTIVA-APROBADA` con la referencia de ese respaldo), `diagnostico-libros.sql`
antes y después, y notas de release con los códigos de permiso nuevos y la instrucción de
vincular EPS/ARL/fondos/cajas/bancos a personas antes de aprobar la primera nómina.
