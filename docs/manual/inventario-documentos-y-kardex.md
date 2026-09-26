# Inventario: el documento genérico, su ciclo y el kardex

> Feature 012 (inventario comercial), entrega I1. Complementa
> [contracts/api.md](../../specs/012-inventario-comercial/contracts/api.md) §8 y §9 (tipos de
> documento y ciclo común), [data-model.md](../../specs/012-inventario-comercial/data-model.md) §0, §3
> y §5, y [decisiones-transversales.md](../../specs/012-inventario-comercial/decisiones-transversales.md)
> §1.3 (el flujo canónico). Aquellos son el contrato; esto es la receta para el equipo.

El inventario heredado de SOLIDO tenía una tabla por tipo de movimiento, cada una con su numeración y
su forma de tocar la existencia. Desde la 012 **todo lo que mueve inventario es una fila de
`INV_Documents`** con su clase, y hay **un solo camino** para confirmarlo: `ConfirmacionDeDocumento`
(`Application/Inventory/Documents`). Nadie más escribe el kardex, nadie más numera, y lo confirmado no
se edita: se anula con otro documento.

## 1. La regla en una frase

**La clase decide qué le pasa al inventario; el ciclo común decide si se puede, en qué orden y cómo
queda escrito.** La estrategia de una clase no abre transacciones, no numera, no guarda y no toca las
existencias con sus manos: le pide los movimientos a `RegistroDeKardex`.

## 2. Clases, tipos y grupos

- **Clase** (`DocumentClass`): fija del sistema. Lo que es y hace cada una (efecto, grupo, mensajes que
  emite, quién la numera, bodegas que admite, desde qué entrega opera) vive en
  `Domain/Inventory/Documents/ClasesDeDocumento.cs` como `DescripcionDeClase`, y **no es columna de
  nada**. Hay 34; en I1 operan las de compras recibidas, factura, nota y devolución al proveedor,
  ajustes, consumo interno, baja, saldo inicial, traslados, movimiento entre ubicaciones, conteo, ajuste
  de costo y anulación.
- **Tipo** (`INV_DocumentTypes`): lo crea la cooperativa. Elige una clase al nacer (no cambia) y
  parametriza consecutivo, campos obligatorios, bodegas permitidas, canal, fecha futura. La semilla
  (`InventoryDocumentTypesSeeder`, Order 80) deja uno por clase de I1 —`REC`, `FCP`, `NTP`, `DVP`, `AJP`,
  `AJN`, `CIN`, `BAJ`, `SIN`, `TRD`, `TRR`, `MUB`, `CON`, `AJC`, `ANU`— más `CONP`/`CONN` para los
  ajustes de conteo, cada uno con su consecutivo de prefijo vacío.
- **Grupo** (`DocumentClassGroup`): la ruta y la familia de permisos. `Adjustments`, `Transfers`,
  `Counts`, `OpeningBalance`, `Purchases`, `Costing` (sólo lo genera el sistema), y `Sales`/`Cash` desde
  I3. Los permisos de cada grupo están en `PermisosDeGrupo`.

La anulación (`Voiding`) no tiene grupo propio: toma el de su original y se pide por la ruta de éste
(`POST …/{id}/void`).

## 3. El ciclo común

Cada historia publica el ciclo sobre su ruta con una línea (`CicloDeDocumentoRutas.MapCicloDeDocumento`):

```csharp
app.MapGroup("/api/inventory/adjustments").RequireAuthorization()
   .MapCicloDeDocumento(DocumentClassGroup.Adjustments, "Inventory.Adjustments", "Inventory_Adjustments");
```

| Ruta | Permiso | Comando |
|---|---|---|
| `GET /`, `GET /{id}` | `{prefijo}.View` | consultas de `VistaDeDocumentos` |
| `POST /`, `PUT /{id}` | `{prefijo}.Create` | `SaveInventoryDraftCommand` (borrador, sin número) |
| `POST /{id}/discard` | `{prefijo}.Create` | `DiscardInventoryDraftCommand` (queda `Discarded`, no consume número) |
| `POST /{id}/confirm` | `{prefijo}.Confirm` | `ConfirmInventoryDocumentCommand` |
| `POST /{id}/void` | `{prefijo}.Void` | `VoidInventoryDocumentCommand` |

Toda escritura exige `Idempotency-Key` (§7). Sin permiso o fuera del alcance de bodegas, el mismo 404
que lo inexistente. Los traslados (`/dispatch`, `/receive`), los conteos (`/open`, `/close`) y el saldo
inicial publican variantes propias de estas rutas, pero entran al mismo `ConfirmacionDeDocumento`.

**Guardar un borrador** valida forma, existencia, alcance, tipo contra grupo y unidades. Lo que hoy
impediría confirmarlo (existencia, período, bodega no activa, campo obligatorio del tipo) **no bloquea**:
vuelve en `warnings[]` con el mismo código que daría la confirmación, para que la pantalla lo marque al
salir de cada campo. Un documento admite hasta 4.000 líneas (`InventoryDocument.MaxLineas`): más, y SQL
Server escalaría el bloqueo a tabla.

**Confirmar** es el flujo canónico (§1.3 de las decisiones transversales), en este orden:

1. relectura del borrador (con `RowVersion` si vino: si cambió, 409 `Concurrency.StaleRowVersion`),
   clase ↔ ruta, alcance, reglas comunes (`ReglasDelDocumento`: líneas, campos del tipo, fecha, corte,
   período, bodega), bloqueo por conteo abierto (`BloqueoPorConteo`) y reglas de la clase
   (`ValidarAsync`);
2. aprobaciones por el motor de plataforma: con niveles, el documento queda `PendingApproval`, **sin
   número y sin tocar cerrojo ni numerador**; la última aprobación reentra aquí en la transacción del
   aprobador (`FuenteDeAprobacionDeDocumento`);
3. guardia fiscal y validación previa contable, si hay implementación registrada (antes de I2 y de I4 no
   hay: se omiten y `prevalidation.outcome` es `NotApplicable`);
4. **cerrojo** en orden canónico (§5), efecto de la clase —o su reversión, en una anulación—,
   **numeración**, sellado del modo de paso, confirmación, mensajes en la bandeja, copia fiscal de la
   contraparte y **un solo** `SaveChanges`;
5. el aviso de reposición de las salidas (`AvisoDeReposicionAlConfirmar`), que no bloquea nunca.

`ConfirmacionDeDocumento` es un servicio y no un envío por `ISender`: lo llaman la confirmación, la
anulación y la última aprobación, y un comando reintentable anidado vaciaría el `ChangeTracker` de
afuera. No abre transacción: la pone quien lo llama (`TransaccionExplicita`, o la que ya abrió
`IdempotencyBehavior`).

## 4. Cómo se agrega una clase

1. Su `DescripcionDeClase` ya existe en `ClasesDeDocumento` (las 34 están desde la fase 3). Si la entrega
   que la habilita llegó, que `AvailableFrom` lo diga: una clase de una entrega futura responde
   `Inventory.DocumentClass.NotAvailable` al crear el tipo y al confirmar.
2. Escribir su estrategia en `Application/Inventory/Documents/Efectos/`, heredando de
   `EfectoDeClaseBase` y sobrescribiendo sólo lo suyo:

   | Método | Qué decide | Por defecto |
   |---|---|---|
   | `AvisosDelBorradorAsync` | lo que sólo la clase sabe que impediría confirmar (vuelve en `warnings[]`) | nada |
   | `ValidarAsync` | sus reglas, antes de la aprobación y fuera del cerrojo | nada |
   | `MontoParaAprobar` | el monto que evalúan la política y el monto máximo | `CostTotal` |
   | `Cerrojo` | qué filas bloquear (bodegas, orígenes, estados de costo, existencias, detalles) | origen, destino y tránsito |
   | `AplicarAsync` | el efecto, **dentro del cerrojo**, con `RegistroDeKardex` | nada |
   | `MensajesAsync` | los contenidos `*V1` que emite | ninguno |
   | `RevertirAsync` / `MensajesDeAnulacionAsync` | cómo se anula un documento de esta clase (con `ReversionDeKardex`) | nada |
   | `OrigenesDelModoAsync` | los orígenes de un derivado, cuyo modo de paso copia | ninguno |

3. Registrarla: `services.AddScoped<IEfectoDeClase, MiEfecto>()` en `Application/DependencyInjection.cs`.
   `EfectosDeClase` la resuelve por clase; dos estrategias para la misma clase, o una para `Voiding`,
   fallan al construir.
4. Publicar el ciclo sobre la ruta del grupo (§3) y, si la clase necesita un tipo sembrado, agregarlo a
   `InventoryDocumentTypesSeeder` (idempotente por código).
5. Casos dorados del costeo si la clase valora de una forma nueva (`Domain.Tests/Inventory/Costing/Casos/`)
   y una e2e en la colección «Inventario e2e».

**Un tipo nuevo** no es programa: se crea en `/inventario/tipos-de-documento` o con la plantilla 8, elige
su clase y hereda todo lo de ella. Cambiar de prefijo o de número es una fila nueva de
`INV_DocumentSequences` (`POST /document-types/{id}/sequences`) que cierra la anterior la víspera; el
siguiente número nunca queda en o por debajo de uno ya emitido (`Inventory.Sequence.NumberAlreadyIssued`).

## 5. El cerrojo en orden fijo (T15)

Dos cajeros que venden las últimas unidades, un traslado y un ajuste sobre el mismo producto: todos
confirman a la vez. `ICerrojoDeInventario` (implementación en `Persistence/Inventory/CerrojoDeInventario`,
SQL de cada motor en `SqlDelCerrojo`) bloquea dentro de la transacción en curso, **siempre en este orden
y ordenando por `Id` en cada tabla**:

1. `INV_Setup`, compartido (exclusivo sólo al cerrar o reabrir un período);
2. `INV_Warehouses`, compartido (exclusivo sólo al abrir un conteo, para copiar la foto sin que entre
   nadie);
3. `INV_Documents` de los orígenes, exclusivo (dos facturas contra la misma recepción no se pasan de lo
   pendiente);
4. `INV_CostStates`, `INV_StockBalances` e `INV_StockDetails`, exclusivos; las filas que faltan se crean
   antes de bloquearlas (`INSERT … ON CONFLICT DO NOTHING` en PostgreSQL, `WHERE NOT EXISTS … WITH
   (UPDLOCK, HOLDLOCK)` en SQL Server);
5. **al final**, la fila de numeración.

Como todos bloquean en el mismo orden, no hay abrazo mortal; `RowVersion` queda como segunda defensa.
El cerrojo exige una transacción abierta (fuera de ella los candados se soltarían al terminar cada
sentencia) y **nunca** se toma antes de la aprobación ni de la validación previa: esperar a Contabilidad
con las existencias bloqueadas pararía la bodega.

## 6. El kardex: un hecho que sólo crece

`INV_KardexEntries` es un hecho inmutable (`IHechoInmutable`): nadie lo modifica ni lo borra después de
insertarlo; `ApplicationDbContext.SaveChangesAsync` lo rechaza en ejecución y
`LosHechosInmutablesNoSeModifican` en el fuente. Las existencias (`INV_StockBalances`,
`INV_StockDetails`) y el costo (`INV_CostStates`) son **proyecciones**: sumas del kardex guardadas para
leer rápido, que se pueden reconstruir desde él.

**`RegistroDeKardex` es el único escritor** del kardex y de sus proyecciones. Lo llama la estrategia de
cada clase desde `AplicarAsync`, dentro del cerrojo, con los movimientos ya en unidad base:

- lee a la fecha de operación el método (`Costeo.Metodo`), el ámbito (`Costeo.Ambito`: cooperativa o
  bodega), el redondeo y el stock negativo por bodega (`Existencias.StockNegativoPermitido`);
- en una **primera pasada**, en memoria, pide a `MotorDeCosteo` (Domain, puro) las líneas de cada
  movimiento y junta **todas** las líneas que no caben (`Inventory.Stock.Insufficient` con
  `data.lines[]`); sólo si todas caben, la **segunda pasada** escribe. Un rechazo no deja nada a medio
  escribir;
- escribe el costo y la ubicación por defecto en la línea del documento; **nunca guarda**.

La otra pieza que puede escribir proyecciones es la reconstrucción (`RebuildInventoryProjectionsCommand`,
`POST /api/inventory/integrity/rebuild`, `Inventory.Integrity.Rebuild` y motivo), que recalcula producto
por producto desde el kardex y **nunca toca el kardex**. La verificación (`POST /integrity/verify`,
consulta) compara kardex contra proyecciones y, con diferencias, levanta `Inventario.IncidenteDeIntegridad`;
la tarea nocturna `inventario.integridad` la corre sobre todo en cada cooperativa desde las 02:00.

Lo fija `NadieEscribeElKardexFueraDelRegistro`: sólo esos dos archivos crean las entidades del kardex y
sus proyecciones, asignan `.Physical`, `.Reserved`, `.AverageCost`, `.LastUnitCost` o
`.LastMovementDate`, o las borran.

**Retroactivos.** En I1 un documento que deja un movimiento con fecha anterior a otro ya registrado del
mismo producto y ámbito se rechaza con `Inventory.Costing.RetroactiveNotAllowed`, nombrando el posterior,
**sin mirar `Costeo.RetroactivosPermitidos`** (el retroactivo general es de I5). Hay dos excepciones: el
saldo inicial de una bodega `NotActivated` (y su anulación) y los ajustes de un conteo aprobado. Para ellas
el registro inserta el movimiento en su fecha y agrega líneas `CostAdjustment` `Retroactive` a las
salidas posteriores (caso dorado 17; ver
[inventario-puesta-en-marcha.md](../operaciones/inventario-puesta-en-marcha.md)).

## 7. Idempotencia (T13)

Doble clic, reintento de red, dos pestañas: la operación se ejecuta **una vez**. Todo comando con ruta
de `Application/Inventory` implementa `IOperacionIdempotente`; la ruta copia la cabecera
`Idempotency-Key` a `OperationKey` con `.ConClaveDeOperacion()`, y `IdempotencyBehavior` hace el resto:

- sin clave: 400 `Operation.KeyRequired`, sin ejecutar;
- primera vez: abre `TransaccionExplicita`, inserta la fila de `COR_OperationKeys`, ejecuta y guarda el
  resultado **sólo si hubo éxito**, en la misma transacción. Si falla, se revierte todo y la clave no
  queda: reintentar con ella vuelve a ejecutar;
- repetición con la misma huella (ruta + cuerpo) y el mismo usuario: el mismo resultado, cabecera
  `Idempotent-Replayed: true` y el evento `Operation.Replayed`; un duplicado que llega mientras el
  primero corre espera en el índice único y recibe la respuesta guardada;
- misma clave con otro contenido u otro usuario: 422 `Operation.KeyReused` con `{ operation, firstUsedAt }`.

El cliente genera la clave al **iniciar** la operación (abrir el diálogo, cargar el borrador), la conserva
en cada reintento y la renueva tras un éxito o si cambia el contenido. Las consultas no llevan clave,
aunque sean `POST` (`integrity/verify`, `supplier-invoices/prefill`). Lo vigila
`LosComandosDeInventarioLlevanClave`.

## 8. Precisión (T19)

La convención global del ERP es `(18,2)`, y en PostgreSQL `numeric(18,2)` redondea **en silencio** un
costo promedio o una tarifa por mil. Por eso cada propiedad decimal de `Entities/Inventory` y de
`Entities/Core/Taxes` declara su precisión con un alias de `PrecisionDeInventario`
(`Persistence/Configurations/Inventory`):

| Alias | Precisión | Para |
|---|---|---|
| `.Cantidad()` | (18,4) | cantidades de línea y en unidad base, existencias, reservas, conteos, mínimos y máximos |
| `.Factor()` | (18,6) | factor de conversión de unidades |
| `.CostoUnitario()` | (18,6) | costo unitario del kardex, de la línea y del estado de costo |
| `.PrecioUnitario()` | (18,6) | precio unitario de compra o venta |
| `.Monto()` | (18,2) | pesos: totales, costo total, valor del estado de costo, impuestos |
| `.Tarifa()` | (9,6) | toda tarifa o porcentaje, **como fracción** (0,19; 0,00966) |

Una propiedad decimal nueva sin alias hace fallar `LasCantidadesYCostosTienenSuPrecision`. La plantilla
de importación lee los porcentajes en puntos y los guarda como fracción (19 → 0,19).

## 9. La anulación es un hecho nuevo

Lo confirmado **no se edita, no se borra y no se reversa**. Se corrige con un documento nuevo que deja su
propio rastro (FR-006): `VoidInventoryDocumentCommand` crea un `Voiding` del tipo de anulación, **con su
propia fecha** (hoy o la indicada, nunca la del original), motivo obligatorio y referencia en los dos
sentidos (`VoidsDocumentId` / `VoidedByDocumentId`), y lo confirma por el mismo flujo canónico: si el
tipo de anulación tiene política, pasa por aprobación.

- La estrategia de la **clase del original** revierte (`RevertirAsync` con `ReversionDeKardex`): cada
  entrada o salida del original tiene su contraria con `ReversesEntryId` y **el mismo** costo unitario.
  Si la entrada anulada ya entró al promedio, la diferencia es una línea `CostAdjustment`
  `VoidDifference` y un mensaje `AjusteDeCostoReconocido` por documento afectado.
- Se niega si ya está anulado (`AlreadyVoided`), si no está confirmado (`NotConfirmed`: uno en aprobación
  se retira por la bandeja), si es una anulación (`VoidingNotVoidable`), si tiene dependientes vigentes
  (`HasDependents`, nombrándolos: hay que anularlos primero) o si es un documento fiscal que emitió la
  cooperativa (`FiscalUseCorrection`: se corrige con su nota, I4).
- Anular una entrada cuyas unidades ya salieron: `Inventory.Stock.Insufficient` con `data.suggestion`
  (`SupplierReturn` o `NegativeAdjustment`).
- La anulación sigue el destino del mensaje del original: copia su modo de paso sin leer el parámetro.

La reversa en espejo es de Contabilidad (FR-038 de la 009), y la hace el módulo dueño del comprobante.
`LoDeInventarioNoSeReversa` rechaza cualquier comando, método o propiedad de reversa en las carpetas del
módulo comercial.

## 10. La numeración al final (T16)

`Numerador` (`Documents/Numeracion`) es el único que asigna número a un documento no fiscal: busca la
secuencia vigente del tipo a la fecha de operación, la bloquea **después** del cerrojo de existencias
(paso 5 del orden), la relee ya bloqueada, copia prefijo y número al documento e incrementa `NextValue`;
el `SaveChanges` de la confirmación lo guarda todo junto. Sin huecos ni repetidos, y el borrador, el
descarte y el documento que queda en aprobación **no consumen número**. Sin secuencia vigente:
`Inventory.Numbering.SequenceMissing`. `SoloElNumeradorNumera` rechaza a cualquier otro que escriba
`Number`, `NextValue` o `LastIssuedNumber` (ésta, la de la resolución DIAN, la escribirá el numerador
fiscal de I4).

## 11. Qué NO hacer

- No hacer `db.KardexEntries.Add(...)` ni asignar `Physical`/`AverageCost` fuera de `RegistroDeKardex`.
- No llamar a `SaveChangesAsync` desde una estrategia de clase: guarda el ciclo, una vez.
- No bloquear filas en otro orden ni antes de la aprobación; no tomar la numeración antes que las
  existencias.
- No escribir `Number` ni `NextValue`; no sembrar un consecutivo «a mano» en una prueba de módulo.
- No editar un documento confirmado ni sus líneas; no usar `Remove` sobre hechos. Se anula.
- No leer ni escribir tablas de Contabilidad o Cartera desde `Inventory`: lo que cruza, cruza por
  mensajes (`InventarioNoConoceContabilidadNiCartera`; ver
  [plataforma-de-integracion.md](plataforma-de-integracion.md)).
- No declarar un decimal sin su alias de precisión.

## 12. Dónde está cada cosa

| | |
|---|---|
| Descripción de las clases | `Domain/Inventory/Documents/ClasesDeDocumento.cs` |
| Ciclo común | `Application/Inventory/Documents/{SaveInventoryDraftCommand,ConfirmacionDeDocumento,ConfirmInventoryDocumentCommand,VoidInventoryDocumentCommand,DiscardInventoryDraftCommand}.cs`, `ReglasDelDocumento.cs` |
| Estrategias | `Application/Inventory/Documents/Efectos/` (`IEfectoDeClase`, `EfectosDeClase`, una por clase) |
| Rutas del ciclo | `API/Endpoints/Inventory/CicloDeDocumentoRutas.cs` |
| Cerrojo | `Application/Inventory/Common/ICerrojoDeInventario.cs`; `Persistence/Inventory/{CerrojoDeInventario,SqlDelCerrojo}.cs` |
| Numerador | `Application/Inventory/Documents/Numeracion/Numerador.cs` |
| Kardex | `Application/Inventory/Kardex/{RegistroDeKardex,ReversionDeKardex,VerifyInventoryIntegrityQuery,RebuildInventoryProjectionsCommand}.cs`; motor puro en `Domain/Inventory/Costing/` |
| Idempotencia | `Application/Common/Behaviors/{IOperacionIdempotente,IdempotencyBehavior,ErroresDeOperacion}.cs` |
| Precisión | `Persistence/Configurations/Inventory/PrecisionDeInventario.cs` |
| Casos dorados | `tests/…Domain.Tests/Inventory/Costing/Casos/*.json` |
| Pruebas de arquitectura | `NadieEscribeElKardexFueraDelRegistro`, `SoloElNumeradorNumera`, `LoDeInventarioNoSeReversa`, `LosHechosInmutablesNoSeModifican`, `LosComandosDeInventarioLlevanClave`, `LasCantidadesYCostosTienenSuPrecision` |
| e2e | `tests/…API.IntegrationTests/Inventory/` (colección «Inventario e2e», los dos motores) |
