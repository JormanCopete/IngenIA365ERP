# Inventario (feature 012): puesta en marcha de una cooperativa, bodega por bodega

> Qué deja la semilla, qué se carga por plantilla y en qué orden, cómo entra el saldo inicial de cada
> bodega, cómo se compara con SOLIDO y cómo se activa. Es el equivalente, para el inventario, de
> [contabilidad-primer-ejercicio.md](contabilidad-primer-ejercicio.md). Está escrito sobre la entrega
> **I1**: la parte del **cuadre contable** antes de activar llega con I2 y se completa aquí entonces (§6).
> Recorrido de ensayo: [quickstart.md](../../specs/012-inventario-comercial/quickstart.md) §2.5, §3.5 y §4.2.

La salida en vivo de COOFLOPAL es el 01/12/2026 con el módulo nuevo. SOLIDO no se apaga de un golpe:
**cada bodega pasa sola**, con su fecha de corte. Mientras una bodega no está activa, SOLIDO sigue siendo
el sistema de registro de esa bodega; desde que se activa, sólo la opera el módulo nuevo. **No hay
sincronización automática** de transacciones entre los dos (FR-091).

## 1. Antes de empezar

Lo que no tiene plantilla y tiene que existir (contracts/plantillas.md §0.2): sucursales contables con su
municipio DANE (`Maestros › Agencias`), centros de costo, personas del maestro (proveedores, clientes: la
importación **no crea personas**), bancos, canales de venta, roles y la UVT vigente. Las migraciones
`RetiroDelInventarioHeredado`, `PlataformaParaInventario` e `InventarioComercialNucleo` aplicadas (ver
[inventario-retiro-heredado.md](inventario-retiro-heredado.md)).

Lo que deja la semilla en toda base de cooperativa (y la plantilla **actualiza**, porque el código es la
llave):

| Semilla | Order | Qué deja |
|---|---|---|
| `InventoryUnitsSeeder` | 77 | unidades básicas con sus decimales y su código UN/ECE (`UND`, `KG`, `LT`…) |
| `WarehouseTypesSeeder` | 78 | tipos de bodega: principal, punto de venta, averías, cuarentena y `TRANSITO` (el único de tránsito) |
| `AdjustmentCausesSeeder` | 79 | causas de ajuste y baja (merma, daño, vencimiento, hurto, diferencia de conteo…) |
| `InventoryDocumentTypesSeeder` | 80 | un tipo por clase de I1 con su consecutivo; el de saldo inicial (`SIN`) con su política de aprobación de un nivel (`Inventory.OpeningBalance.Approve`) |
| `TaxCatalogSeeder` | 81 | catálogo tributario colombiano (impuestos, tarifas, conceptos de retención) |
| `DivipolaSeeder` | 82 | municipios DANE |
| `AlertTypesSeeder` | 83 | tipos de alerta |

Los permisos `Inventory.*` los siembra **la API al arrancar** (`InventoryPermissionCatalogSeeder`),
no el DbMigrator; los perfiles sugeridos (jefe de inventario, bodeguero, comprador…) se aplican desde
`/admin/roles` con «crear desde plantilla».

Comprobación:

```sql
SELECT "Code", "Name" FROM dbo."INV_DocumentTypes" ORDER BY "Code";          -- 17 tipos (15 + CONP, CONN)
SELECT "Code", "Behavior" FROM dbo."INV_WarehouseTypes" ORDER BY "Code";
SELECT COUNT(*) FROM dbo."SEC_Permissions" WHERE "Resource" LIKE 'Inventory.%';
```

## 2. Las plantillas, en orden

Todas en `/inventario/plantillas`, cada una con **revisar** (no guarda nada) y **aplicar** (todo o nada).
El orden, las hojas y las columnas son los de
[contracts/plantillas.md](../../specs/012-inventario-comercial/contracts/plantillas.md) §0.1 —cada una
sólo cita lo que cargaron las anteriores— y la mecánica, la de
[plantillas-de-importacion.md](../manual/plantillas-de-importacion.md):

1. impuestos, retenciones y conceptos (`/maestros/impuestos`);
2. grupos contables;
3. unidades de medida;
4. marcas;
5. categorías;
6. productos, con códigos de barras, conversiones e impuestos adicionales;
7. bodegas y ubicaciones: **toda bodega nace `NotActivated`** con su ubicación `GENERAL`, y la primera
   bodega operativa de cada sucursal trae su bodega de tránsito (la plantilla nunca activa);
8. tipos de documento (prefijo, consecutivo, aprobación, modo de paso);
9. vendedores (sobre personas que ya existen).

Las 10 a 13 (puntos de venta, medios de pago, listas de precios, topes de descuento) se **descargan** desde
I1 y se **importan** con I3; la 16 (matriz contable) es de I2. La 14 y la 15 son las de este runbook.

Con `?withData=true` cada libro sale lleno con lo que ya se cargó: se corrige sobre él y se vuelve a
subir sin duplicar.

## 3. Por cada bodega: conteo, cifras de SOLIDO y saldo inicial

Se repite **bodega por bodega**, en el orden que decida el jefe de inventario.

### 3.1 El conteo físico, antes de cada carga

El saldo inicial es la cantidad **contada**, no la que dice SOLIDO. Antes de cargar una bodega se cuenta
en el piso, y la cantidad de la plantilla es la del conteo **más o menos los movimientos que la bodega
tuvo en SOLIDO entre el conteo y el corte**. La otra opción, más simple si la bodega lo permite, es que
deje de operar en SOLIDO desde el conteo hasta su activación (FR-089).

Este conteo es del mundo real y se hace sobre las planillas del jefe de inventario: el conteo físico del
módulo (`/inventario/conteos`) sólo opera bodegas **activas**, y ésta todavía no lo es.

### 3.2 Las cifras de SOLIDO (plantilla 15)

`/inventario/cifras-solido` (`POST /api/inventory/legacy-figures/import`, permiso
`Inventory.LegacyFigures.Import`): existencias y valores de SOLIDO a una fecha, **sólo informativos**. No
mueven existencias ni costo; escriben `INV_LegacyFigures`.

- Llave `fecha` + `bodega` + `producto`. La bodega debe existir en el ERP (activa o no) y estar al alcance.
- Un código de producto que no está en el catálogo nuevo **no es error**: la fila exige `grupoContable`,
  queda «sin producto en el catálogo» y avisa `Inventory.LegacyFigures.CodeUnresolved`. Con producto, un
  grupo distinto del suyo avisa `…GroupMismatch`. La cantidad puede ser negativa (así estaba en SOLIDO).
- Cada importación es un lote; importar otra vez un par (fecha, bodega) deja el anterior como historia, y
  los comparativos usan siempre el lote más reciente.

Se cargan las cifras al corte de **todas** las bodegas, no sólo de la que se activa: las que siguen en
SOLIDO entran con ellas a la comparación de activación (FR-090, §6).

### 3.3 El saldo inicial (plantilla 14)

`/inventario/saldo-inicial` (`POST /api/inventory/opening-balances/import`, permiso
`Inventory.OpeningBalance.Load`). Columnas: `bodega`, `fechaDeCorte`, `producto`, `ubicacion` (opcional),
`cantidad` **en unidad base**, `costoUnitario` (lote, serie y vencimiento, con I6).

- La bodega tiene que ser operativa y **no activa**, sin un saldo confirmado vigente ni uno en aprobación.
- `fechaDeCorte` es una sola por bodega —la **víspera de su activación**— y, si ya estaba fijada, la misma;
  nunca futura, en período cerrado ni antes del inicio del módulo. El primer `apply` crea `INV_Setup` con
  `StartDate` = primer día del mes del corte más antiguo.
- Genera **borradores** `OpeningBalance` por bodega, de hasta 4.000 líneas cada uno. **La plantilla nunca
  confirma.** Volver a importar la misma bodega **reemplaza las líneas** del borrador conservando el mismo
  documento (las viejas quedan de baja lógica, Principio XI); si el archivo trae menos documentos, los que
  sobran se descartan.
- Costo cero se admite con aviso. La revisión devuelve `extra.byWarehouse[]` con cantidad y valor total y
  el **valor por grupo contable** redondeado: es el insumo de la comparación contra Contabilidad.

**Confirmar** cada borrador (`POST /api/inventory/opening-balances/{id}/confirm`) pasa **siempre** por
aprobación: la semilla deja la política de un nivel con umbral cero y el permiso
`Inventory.OpeningBalance.Approve`, y la regla `Approvals.Policy.RequiredForClass` no deja quitarla. Lo
aprueba **otra persona** en `/inventario/aprobaciones` (quien cargó no aprueba lo suyo). Al aprobarse, el
kardex recibe las entradas fechadas en el corte y sale el mensaje `SaldoInicialCargado`, **informativo**:
no genera comprobante, porque ese valor ya está en los libros (la apertura contable de la 009). Su
anulación también es informativa. Para cargar de nuevo una bodega con saldo confirmado, se anula primero.

### 3.4 La excepción de puesta en marcha (T18)

Con el costo en ámbito **cooperativa** (lo normal al activar bodega por bodega), el saldo inicial de la
segunda bodega llega **después** de que la primera ya vendió, pero con una fecha de corte **anterior** a
esas ventas. Un documento retroactivo cualquiera se rechaza en I1 (`Inventory.Costing.RetroactiveNotAllowed`),
pero el saldo inicial de una bodega `NotActivated` (y su anulación) **está exento**, sin mirar
`Costeo.RetroactivosPermitidos` (pregunta D8, propuesta por defecto):

- el kardex inserta sus entradas en su fecha (`OperationDate`, `Id`);
- las salidas posteriores de ese producto se recalculan con el promedio nuevo, con líneas `CostAdjustment`
  `Retroactive` separadas en lo que quedó en existencia y lo que ya se vendió —las ventas no se tocan—;
- sale un `AjusteDeCostoReconocido` por documento afectado. Aunque el saldo inicial sea informativo,
  **esos ajustes sí se contabilizan** (desde I2).

La revisión de la plantilla no los calcula: aparecen al confirmar. Es el caso dorado 17
(`Domain.Tests/Inventory/Costing/Casos/17-segunda-bodega-despues-de-ventas.json`) y lo repite la e2e
`SaldoInicialYActivacionTests`. Un ajuste digitado con la misma fecha, en cambio, sigue rechazado hasta
I5. Los ajustes de un conteo aprobado tienen la misma exención (D9).

## 4. Comparar con SOLIDO

Dos vistas del centro de informes (`/inventario/informes`, permiso `Inventory.Reports.View`):

- **`legacy-comparison-valuation`** — a una fecha (hoy por defecto), por grupo contable, bodega y
  producto: cantidad y valor de SOLIDO contra el valorizado del módulo a esa fecha, y la diferencia. Las
  cifras sin producto resuelto suman a su grupo en una fila «Sin producto en el catálogo». Exige
  `Inventory.Costs.Read` (sin él, el 404 genérico).
- **`legacy-comparison-kardex`** — para una bodega, en cada fecha con cifras de SOLIDO dentro del rango,
  por producto: cantidad y valor de cada sistema. Sin `Inventory.Costs.Read`, las columnas de valor van
  vacías y la nota lo dice.

Las dos usan el lote más reciente de cada par (fecha, bodega) y respetan el alcance por bodega.

## 5. La marcha paralela (SC-018)

Entre la carga de la primera bodega y la salida en vivo, las bodegas operan en los dos mundos: las activas
en el módulo nuevo, las demás en SOLIDO. Cada semana (o al corte que fije el jefe de inventario) se
importan las cifras de SOLIDO de todas las bodegas a esa fecha y se corren los dos comparativos.

**Cada diferencia** de kardex o de valorizado se explica —conteo mal digitado, movimiento de SOLIDO entre
el conteo y el corte que no se sumó, producto con otro código, costo distinto— y el jefe de inventario la
**aprueba por escrito** antes de la salida en vivo. La causa y la aprobación se llevan en el acta de la
marcha paralela; el sistema no tiene una pantalla para eso. Una diferencia sin causa detiene la salida en
vivo de esa bodega.

## 6. Activar la bodega

Desde `/inventario/saldo-inicial` o la ficha de la bodega («Activar», `ActivacionDeBodegaDialog`;
`GET/POST /api/inventory/warehouses/{id}/activation`, permiso `Inventory.Warehouses.Activate`). La vista
previa muestra lo que se compararía y lo que hoy impide activar; la activación vuelve a calcular siempre
(nunca confía en la vista previa):

- bloqueos duros: saldo inicial sin confirmar, corte distinto, ya activa, período cerrado (422 con su código);
- al activar: la bodega pasa a `Active`, queda la fila de `INV_WarehouseActivations` con la comparación
  entera y, si es la primera bodega activa de la sucursal, se activa también su tránsito. Si ninguna bodega
  tenía saldo inicial, la activación crea `INV_Setup`. Queda auditada con su motivo.

**Lo que FR-090 pide y en I1 todavía no hay**: comparar, a la fecha de corte y por grupo contable y
conjunto de cuentas mapeadas, el valorizado de todas las bodegas que usan esas cuentas (las activas y la
que se activa con el módulo nuevo; las no activas con sus cifras de SOLIDO) contra el saldo contable. Eso
necesita la matriz contable y la consulta de saldos de Contabilidad (`IContabilidadParaInventario`), que
son de **I2**. Hasta entonces:

- **fuera de producción**, se activa aceptando la diferencia: `acceptDifference`, el permiso
  `Inventory.Warehouses.AcceptActivationDifference` y motivo; el motivo guardado empieza con «sin
  comparación contable»;
- **en producción no se puede activar ninguna bodega**: el `POST` responde 422
  `Inventory.Activation.AccountingUnavailable` y no escribe nada. Lo fija la API por ambiente
  (`PuestaEnMarchaOptions.PermitirActivacionSinComparacion = !IsProduction()`), no la configuración.

En producción, con I1 sola, se puede **cargar y aprobar** saldos, importar cifras de SOLIDO y correr los
comparativos; activar espera a I2. Esta sección se completa con el cuadre contable cuando I2 esté.

## 7. Después de activar

- La bodega ya sólo la opera el módulo nuevo; en SOLIDO se cierra.
- El conteo físico del módulo (`/inventario/conteos`) ya la cubre: foto al abrir, captura, comparación,
  ajuste aprobado (`CONP`/`CONN`, con su política de un nivel).
- El período del inventario (`/inventario/periodos`) se cierra mes a mes; lo cerrado no admite
  documentos con fecha en él.
- La verificación nocturna del kardex (`inventario.integridad`) corre sobre ella.

## 8. Síntomas

| Síntoma | Causa | Qué hacer |
|---|---|---|
| `Inventory.OpeningBalance.WarehouseActive` en la revisión | la bodega ya está activa | ya no admite saldo inicial: lo que falte se corrige con ajustes (o con un conteo) |
| `Inventory.OpeningBalance.AlreadyConfirmed` en la revisión | ya hay un saldo confirmado en esa bodega (los nombra en `data`) | anularlo primero (informativo) y volver a importar |
| `Inventory.OpeningBalance.TransitNotAllowed` | la fila apunta a una bodega de tránsito | el tránsito no lleva saldo inicial |
| `Inventory.Activation.OpeningBalanceNotConfirmed` · `.CutoffMismatch` | hay un saldo sin aprobar, o su fecha no es el corte pedido | aprobarlo en `/inventario/aprobaciones`; activar con el mismo corte |
| `Inventory.Activation.Difference` · `.AcceptDifferenceNotAllowed` | fuera de producción, sin aceptar la diferencia o sin el permiso | marcar «aceptar la diferencia» con motivo, con `Inventory.Warehouses.AcceptActivationDifference` |
| `Inventory.Numbering.SequenceMissing` al confirmar el saldo | el tipo `SIN` no tiene consecutivo vigente a la fecha de corte | revisar `/inventario/tipos-de-documento`; los sembrados rigen desde siempre |
| `Inventory.Period.NotStarted` al cerrar un mes | no existe `INV_Setup` | se crea con el primer `apply` del saldo o con la primera activación |
| `Inventory.Activation.AccountingUnavailable` | producción antes de I2 | esperar I2; no hay atajo |
| El comparativo muestra filas «Sin producto en el catálogo» | códigos de SOLIDO que no están en el catálogo nuevo | crear el producto o aceptar la fila como obsoleta en el acta |
