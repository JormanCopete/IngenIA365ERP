# Quickstart: Inventario comercial renovado — catálogo, existencias, compras, ventas y facturación

**Feature**: 012 | **Date**: 2026-09-24 | **Rama**: `012-inventario-comercial`

Cómo comprobar la feature de punta a punta en una **cooperativa de ensayo**, fuera de producción
(decisión 1 de los Supuestos, FR-095), entrega por entrega y con los casos dorados como patrón. Todo
lo que aquí se afirma sobre un ambiente se comprueba **contra la base del ambiente**, nunca contra el
repositorio. Los nombres de tablas, rutas, permisos, parámetros, alertas y códigos de error son los
canónicos de [decisiones-transversales.md](decisiones-transversales.md) (nombres en su §2, decisiones
T1–T52 en su §3), que repiten `research.md`, `data-model.md` y `contracts/`.

Cuatro decisiones del dueño mandan sobre todo lo que sigue, y este quickstart ya las asume:

- **Inventario no escribe asientos ni conoce cuentas** (Q2 B). Al confirmar emite mensajes de
  negocio en la misma transacción del documento; antes de confirmar pregunta a Contabilidad si es
  contabilizable; y el **modo de paso** (`EnLinea`, `PorLotes`, `NoPasa`) se sella en el documento.
  Contabilidad los consume con su matriz de reglas (enmienda de la 009, en esta feature).
- **La factura electrónica sale por un adaptador** (Q3 C): proveedor tecnológico externo primero,
  software propio por el servicio central de la 010 cuando exista. En CI y en el ensayo se usa el
  `CanalSimulado`; el proveedor real, en su sandbox, cuando COOFLOPAL lo contrate.
- **La comunicación con Cartera (IC) está pendiente.** Mientras tanto se vende a **crédito
  provisional**: sin consultar cupo, con aprobación, la cuenta por cobrar la registra Contabilidad
  desde «VentaFacturada» y los mensajes para Cartera se acumulan, visibles, sin perderse.
- **Ninguna fecha fija.** El ensayo, el conteo, la carga del saldo inicial y la salida los fija el
  dueño cuando la aplicación esté lista (Q1). Los meses de este documento se nombran «M» (el mes
  anterior al de hoy en el ensayo) y «M+1» (el mes en curso).

## 1. Compilar y pruebas

### 1.1 Sin contenedores

```bash
dotnet build IngenIA365ERP.CI.slnf -c Release
```

**Motores puros con casos dorados JSON** (Domain). Cada caso es un archivo que la contadora puede
leer y firmar, calculado a mano:

```bash
dotnet test tests/IngenIA365ERP.Domain.Tests --filter "FullyQualifiedName~Inventory|FullyQualifiedName~Taxes|FullyQualifiedName~Sales|FullyQualifiedName~Approvals|FullyQualifiedName~ElectronicInvoicing"
```

Deben quedar verdes, con estos valores como mínimo:

- **Costeo** (`Inventory/Costing/Casos/*.json`, `MotorDeCosteo`): 01 promedio 10 × 1.000 + 10 × 1.300
  → **1.150** (US3-1); 02 devolución de cliente a 1.150 aunque el promedio cambió después; 03
  devolución a proveedor al costo de entrada, con la diferencia como ajuste de costo; 04 y 05
  traslado neutro en ámbito cooperativa y en ámbito bodega, con el tránsito saliendo al costo de la
  línea de despacho; 06 existencia cero que conserva el último costo; 07 negativo al último costo y
  su regularización; 08 anular una entrada ya consumida; 12 residuo de redondeo (valor 0 cuando la
  cantidad es 0); 13 caja × 12 con conversión no exacta y cantidad de redondeo visible; 14 IVA no
  descontable e INC al costo (US3-6); 15 ajuste de conteo al promedio de la fecha de la foto, con
  salidas posteriores en otra bodega, sin depender de `Costeo.RetroactivosPermitidos` (FR-041); 17
  segunda bodega activada después de ventas en la primera, ámbito cooperativa: el saldo inicial entra
  en su corte, las salidas posteriores se recalculan y sale un «AjusteDeCostoReconocido» por documento
  afectado (excepción de puesta en marcha, retroactivo mínimo de I1). Desde I5:
  09 compra retroactiva antes de tres ventas (US16-2), 10 prorrateo con parte vendida, 11 PEPS con
  dos capas y venta de 15 → **16.500** (US16-1), 16 valorizado por los dos métodos, y
  `Inventory/Costing/Prorrateo`: flete de 100.000 sobre 600.000 y 400.000 → **60.000 / 40.000**
  (US13-3). `PropiedadesDelKardexTests` corre secuencias aleatorias: la suma del kardex iguala la
  proyección y la disponible nunca queda negativa.
- **Impuestos** (`Taxes/Casos`, `MotorTributario`, `IvaDescontable`): base **justo en el mínimo**
  (sí retiene: «igual o superior»), gran contribuyente frente a no gran contribuyente,
  autorretenedor, ReteIVA sobre el IVA, ReteICA por municipio con caída a la fila `*`, bolsas por
  unidad, exento frente a excluido, nota parcial con la foto del original sin volver a probar el
  mínimo.
- **Precios** (`Sales/Pricing/Casos`, `ResolutorDeListaDePrecios`): cliente > segmento > canal >
  sucursal > general, y respaldo por producto en la siguiente lista aplicable.
- **Caja** (`Sales/Cash/Casos`, `CalculadoraDeEsperado`, `EvaluadorDeArqueo`): esperado por medio =
  base (sólo efectivo) + ventas − devoluciones ± movimientos confirmados ± reclasificaciones; un caso
  con 300 ventas y 6 medios (SC-025).
- **Aprobaciones** (`Approvals/Casos`, `EvaluadorDePolitica`): dos niveles por umbral, exclusión del
  creador, del solicitante, de los participantes declarados y de quien aprobó otro nivel; monto
  máximo por encima del límite sin política.
- **Facturación electrónica** (Domain/ElectronicInvoicing): `TransicionesDelDocumentoElectronico`
  con **todas** las transiciones prohibidas (nunca `Validated` sin código único y sin respuesta de
  validación; un `Sent` no se corrige; los casos b y c sólo desde `Rejected` confirmado);
  `ReglaDeCorreccionFiscal` (la huella económica separa el caso a del b); `PlazoDeContingencia`.
- **Sello de integridad**: los casos dorados de canonicalización de `SelloDeIntegridad` (claves
  ordenadas, UTC ISO, decimales invariantes).

**Application** (InMemory, reloj fijo, servicios reales):

```bash
dotnet test tests/IngenIA365ERP.Application.Tests --filter "FullyQualifiedName~Inventory|FullyQualifiedName~Core.Taxes|FullyQualifiedName~Core.PaymentMeans|FullyQualifiedName~Accounting.Inventory|FullyQualifiedName~ElectronicInvoicing|FullyQualifiedName~Common.Integration|FullyQualifiedName~Common.Approvals|FullyQualifiedName~Common.Alerts|FullyQualifiedName~Common.Parameters"
```

Entre otras: reglas de cada clase (`ClasesDeDocumento`); anular una sola vez
(`Inventory.Document.AlreadyVoided`) y nombrar a los dependientes
(`Inventory.Document.HasDependents`, `data.dependents`); `LectorDeParametros` con caída ámbito →
general → defecto y valor no admitido como error (`Parameters.ValueNotAllowed`); cruces de vigencia
(`Parameters.Overlaps`) y método de costeo fuera del inicio de período (`Parameters.RequiresPeriodStart`);
cadena de modo de paso (`Inventory.PostingMode.ChainMismatch`,
`Inventory.PostingMode.FiscalRequiresConfirmation`); matriz (`Accounting.InventoryRule.Missing`,
`.Overlaps`, `.RetroactiveOverPosted`, `.TaxRateMismatch`); `GuardiaDeEmisionFiscal.Evaluar`
(`Electronic` / `NonElectronic` / `Blocked` con la lista de motivos); `PerfilesSugeridos` contra el
catálogo de permisos. **Lo transaccional no se prueba aquí**: idempotencia, cerrojo, numeración y
concurrencia sólo se prueban con Testcontainers en los dos motores, porque InMemory ignora las
transacciones (T14).

**Arquitectura**:

```bash
dotnet test tests/IngenIA365ERP.Architecture.Tests
```

Nuevas: `InventarioNoConoceContabilidadNiCartera` (la comprobación automática de FR-014, incluida la
lista exacta de métodos de `IContabilidadParaInventario` e `IConsultasDeCartera`),
`NingunTrabajoDeFondoOperaSinCooperativa`, `LosComandosDeConsumoNoTienenRuta`,
`LosComandosDeInventarioLlevanClave`, `LosHechosInmutablesNoSeModifican`,
`NadieEscribeElKardexFueraDelRegistro`, `SoloElNumeradorNumera`, `ElComercioNoTieneValoresLegalesFijos`,
`LasCantidadesYCostosTienenSuPrecision`, `LoDeInventarioNoSeReversa`, `LaUvtSeLeeEnUnSoloSitio`,
`LosParametrosSeLeenEnUnSoloSitio`, `LosPagosNoGuardanElNumeroDeTarjeta`,
`ElProveedorTecnologicoSoloLoConoceSuAdaptador`, `LasCredencialesDeFacturacionNoTocanLaBase`,
`LasConsultasDeInventarioRespetanElAlcance`, `LaSegregacionNoUsaUserIdDelToken`. Ampliadas:
`LosEndpointsProtegidosExigenPermiso` (rutas de Inventario, facturación electrónica, impuestos, medios
de pago e informes de inventario), `LasPantallasDicenQueEstanCargando` (Inventario, Compras, Ventas y
Pos en `ModulosMigrados`), `PrincipioXI_ContableImmutable` (los `Transactions` nuevos),
`LaPersonaSeEscribeEnUnSoloSitio`, `TodoEnlaceDelMenuTieneSuPagina`, `ManualCatalogoTests`,
`PrincipioXII_MigracionesDestructivas` y `Feature004_MigrationParity`.
`NingunModuloEscribeMovimientosFueraDelContrato` y `LaContabilidadNoTieneCuentasEnCodigo` quedan
**sin cambios y verdes**: si alguna cae, algo de Inventario tocó el libro por fuera del contrato.

### 1.2 Con Docker Desktop encendido (e2e)

Docker Desktop está apagado por defecto en la máquina de desarrollo. La fixture apaga los trabajos de
fondo (`Integration:Dispatcher:Enabled = false` y los demás) y conduce el ciclo a mano, para que cada
prueba sepa cuándo se procesa un mensaje. Se corre **en los dos motores**, porque el cerrojo tiene SQL
propio de cada uno:

```bash
F_I1="FullyQualifiedName~ElProcesoEnSegundoPlanAuditaEnSuCooperativaTests|FullyQualifiedName~IdempotenciaDeOperacionesTests|FullyQualifiedName~ConcurrenciaDeExistenciasTests|FullyQualifiedName~CompraDirectaTests|FullyQualifiedName~TrasladoEnDosPasosTests|FullyQualifiedName~ConteoYAjusteTests|FullyQualifiedName~CierreDePeriodoTests|FullyQualifiedName~AprobacionMultinivelTests|FullyQualifiedName~AlcancePorBodegaTests|FullyQualifiedName~IntegridadDeAuditoriaTests"
F_I2="FullyQualifiedName~ContabilizacionPorMensajesTests|FullyQualifiedName~EntregaGarantizadaTests|FullyQualifiedName~LotesProgramadosTests|FullyQualifiedName~SaldoInicialYActivacionTests"
F_I3="FullyQualifiedName~VentaPosCompletaTests|FullyQualifiedName~BonoUnicoConcurrenteTests|FullyQualifiedName~UnaSesionPorCajaTests|FullyQualifiedName~BusquedaDeProductos50kTests|FullyQualifiedName~CreditoProvisionalTests|FullyQualifiedName~ContabilizacionDeVentasPorMensajesTests"
F_I4="FullyQualifiedName~DocumentosElectronicosTests"

DB_PROVIDER=PostgreSql dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "$F_I1|$F_I2|$F_I3|$F_I4"
DB_PROVIDER=SqlServer  dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "$F_I1|$F_I2|$F_I3|$F_I4"
```

(No se filtra por `~Integration`: el espacio de nombres del proyecto ya contiene «IntegrationTests» y
el filtro tomaría la suite entera.)

Colección «Inventario e2e». Las pruebas que cambian todo el libro corren en **cooperativas aisladas
del mismo host** (patrón `ContabilidadE2E.CooperativaAisladaAsync`), no en la compartida. Lo que
recorren por HTTP:

- `ElProcesoEnSegundoPlanAuditaEnSuCooperativaTests`: dos cooperativas; un comando con actor proceso
  por `IEjecutorEnCooperativa` en la A deja su auditoría en `IngenIA365ERP_Audit_{A}`, nada en la B,
  **nada nuevo en la global**, y la fila SQL en la base de A; sin cooperativa, lanza y no escribe
  (D-03).
- `IdempotenciaDeOperacionesTests`: la misma `Idempotency-Key` tres veces → un efecto, la misma
  respuesta y `Idempotent-Replayed: true`; otra carga con la misma clave → 422 `Operation.KeyReused`;
  sin clave → 400 `Operation.KeyRequired` (SC-002).
- `ConcurrenciaDeExistenciasTests`: 50 salidas simultáneas de 1 unidad sobre existencia 10 → **10
  confirmadas, 40 rechazadas** con `Inventory.Stock.Insufficient` y la disponible real, nunca negativa;
  y la numeración sin huecos ni repetidos (SC-001, FR-038). Una ronda corta en cada corrida; las
  **1.000 repeticiones** con `RUN_PERF_TESTS=1`.
- `CompraDirectaTests`, `TrasladoEnDosPasosTests`, `ConteoYAjusteTests`, `CierreDePeriodoTests`: los
  recorridos de §3.6, §3.9, §3.10 y §3.11, con el kardex, el costo y los mensajes esperados.
- `AprobacionMultinivelTests`, `AlcancePorBodegaTests`, `IntegridadDeAuditoriaTests`: §3.13.
- `ContabilizacionPorMensajesTests`, `EntregaGarantizadaTests`, `LotesProgramadosTests`,
  `SaldoInicialYActivacionTests`: §4.
- `VentaPosCompletaTests`, `BonoUnicoConcurrenteTests`, `UnaSesionPorCajaTests`,
  `CreditoProvisionalTests`, `ContabilizacionDeVentasPorMensajesTests` (lote resumido FV, anulación en un
  resumido, venta que no pasa; espera a I2): §5. `BusquedaDeProductos50kTests` mide SC-009 con 50.000 productos.
- `DocumentosElectronicosTests`: §6 contra `CanalSimulado`.

Las de volumen —SC-001 al pie de la letra, los 50.000 productos, el lote de 5.000 documentos de
SC-020— reportan **Skip explícito** sin `RUN_PERF_TESTS=1`. Ninguna hace `return` al principio: un
`return` cuenta como aprobada sin haber comprobado nada, y la suite ya tiene siete así.

## 2. La cooperativa de ensayo

Todo esto es **dato**, no despliegue. Se hace en QA (o en local) sobre una cooperativa creada para el
ensayo, con su propia base (Principio IV). Nada del ensayo llega a la cooperativa de producción
(FR-095).

### 2.1 Crear, migrar y retirar el módulo actual

1. El maestro crea la cooperativa `coop_ensayo` desde la consola SaaS (`POST
   /api/saas/tenants/with-admin`). Migrar:

   ```bash
   dotnet run --project tools/IngenIA365ERP.DbMigrator -- migrate --scope cooperativas
   ```

   Pares de migración por entrega: `RetiroDelInventarioHeredado` (destructiva, commit propio),
   `PlataformaParaInventario` e `InventarioComercialNucleo` (I1); `IntegracionContableDeInventario`
   (I2); `VentasYPuntoDeVenta` (I3); `DocumentosElectronicos` (I4). `check-migration-parity.ps1` en
   verde para PostgreSQL y SQL Server. En PostgreSQL, la migración del núcleo crea `pg_trgm`
   (`CREATE EXTENSION IF NOT EXISTS`): comprobar que el rol del ambiente pudo crearla.
2. **Guarda del retiro** (FR-092, Principio XII). En una base nueva las 23 tablas heredadas están
   vacías y la guarda pasa. Para probar que muerde, en una copia de DEV con una fila en
   `INV_ProductGroups`: la migración se niega nombrando la tabla y la cantidad. Sólo pasa con la fila
   `COR_SystemSettings.SettingKey = 'INV.RetiroHeredado.Aprobado'` (respaldo, segundo revisor y
   fecha), que se inserta a mano después del `pg_dump`. Antes de fusionar, correr
   `specs/012-inventario-comercial/diagnostico-inventario-heredado.sql` en DEV, QA y PDN.
3. `INV_Salespeople` sigue ahí, con `UK_INV_Salespeople_PersonId` filtrado a no borrados.

### 2.2 Semillas y permisos

Los **permisos** los siembra la API al arrancar (`InventoryPermissionCatalogSeeder`,
`ElectronicInvoicingPermissionCatalogSeeder` y los códigos nuevos de Core, Accounting y `AuditLog`),
**no el DbMigrator**. Las semillas paramétricas (`RunParametricSeed`) dejan, por entrega:

| Order | Semilla | Qué comprobar |
|---|---|---|
| 77 | `InventoryUnitsSeeder` | unidades con decimales admitidos y código Rec. 20 |
| 78 | `WarehouseTypesSeeder` | principal, punto de venta, averías, cuarentena y tránsito |
| 79 | `AdjustmentCausesSeeder` | merma, daño, vencimiento, hurto, diferencia de conteo, destrucción, reclamación al transportador |
| 80 | `InventoryDocumentTypesSeeder` | un tipo por clase de I1, incluido `Voiding`, con su secuencia |
| 81 | `TaxCatalogSeeder` | IVA 19/5/exento/excluido, INC, bolsas por unidad, ReteFuente por concepto en UVT, ReteIVA; marcado «pendiente de validar por la contadora» |
| 82 | `DivipolaSeeder` | `COR_Cities.DaneCode` |
| 83 | `AlertTypesSeeder` | los tipos de alerta con sus permisos destinatarios |
| 84, 60 | `InventoryVoucherMappingsSeeder`, `VoucherTypesSeeder` | `NV`, `CP`, `TR`, `AC`, `CJ` (Module/INV) y el mapeo operación → comprobante (I2) |
| 85–87 | `CashDenominationsSeeder`, `DefaultPaymentMeansSeeder`, `ConsumidorFinalSeeder` | denominaciones, sólo `EFECTIVO`, y la persona «Consumidor final» (I3) |

```bash
H="Authorization: Bearer $TOKEN"; API=https://<host-del-ambiente>
curl -s -H "$H" "$API/api/admin/permissions/mine" | jq '[.[] | select(startswith("Inventory."))] | length'
curl -s -H "$H" "$API/api/admin/roles/templates?module=Inventory" | jq '.[].key'
```

La segunda devuelve las ocho plantillas: `inventario.administrador`, `.jefe`, `.bodeguero`,
`.comprador`, `.cajero`, `.aprobador`, `.contador`, `.auditor`.

### 2.3 Contabilidad de ensayo y UVT

La activación y la conciliación comparan contra los libros, así que el ensayo necesita su
contabilidad (receta: `docs/operaciones/contabilidad-primer-ejercicio.md`):

1. Contabilidad iniciada con el CUIF, sucursales contables S1 y S2, centros de costo y los períodos
   contables de M y M+1 abiertos.
2. Apertura `AP` con los saldos de las cuentas de inventario **iguales al valorizado de SOLIDO al
   corte**, salvo el grupo `ABARROTES`, que se deja **250.000** por debajo a propósito para §4.2.
3. UVT vigente en Parámetros legales (`PAY_LegalParameters`, código `UVT`). Sin ella, el motor
   tributario falla visible: «No hay UVT vigente al {fecha}; regístrela en Parámetros legales».
4. Parámetros tributarios de la cooperativa (`Tributario.*`) y `COR_Branches.MunicipalityDaneCode`
   de S1 y S2.

### 2.4 Usuarios, perfiles, alcances y montos

Roles creados desde las plantillas (`POST /api/admin/roles/from-template`, `Security.Roles.Create`);
quedan editables. Alcances en la pestaña «Alcance comercial» de `/admin/usuarios`
(`/api/inventory/scopes/users/{userPublicId}`); montos en `/inventario/politicas-de-aprobacion`
(`/api/inventory/amount-limits`).

| Usuario | Plantilla | Alcance | Ajuste para el ensayo |
|---|---|---|---|
| `jefe` | `inventario.jefe` | todas (`Inventory.Scope.AllWarehouses`) | + `Inventory.Approvals.Management` (nivel 2 de §3.13) |
| `bodega.a` | `inventario.bodeguero` | PRIN y PV1 | + `Inventory.Adjustments.Confirm`, para enviar a aprobación |
| `bodega.b` | `inventario.bodeguero` | PV2 | — |
| `comprador` | `inventario.comprador` | PRIN | `Inventory.Purchases.Confirm` hasta **5.000.000** |
| `cajero.1`, `cajero.2` | `inventario.cajero` | punto PV1, cajas 1 y 2 | tope de descuento 5 %; `Inventory.Sales.SellOnCredit` con monto máximo **100.000** |
| `supervisor` | `inventario.aprobador` + `Inventory.Discounts.Authorize` + `Inventory.Sales.SellOnCredit` | punto PV1, bodegas PV1 y PRIN | tope 15 %; `Inventory.Sales.SellOnCredit` con monto máximo 1.000.000 (aprobador del crédito provisional) |
| `aprobador` | `inventario.aprobador` | todas | — |
| `contadora` | `inventario.contador` + roles contables de la 009 | todas | `Accounting.InventoryRules.Manage`, `Accounting.InventoryBatches.Run` |
| `auditor` | `inventario.auditor` | todas | `AuditLog.VerifyIntegrity` |
| `lectura` | rol integrado `ReadOnly` | — | ninguno |

### 2.5 Plantillas, en orden

Cada catálogo tiene `GET …/template.xlsx` (con `?withData=true`, lo ya cargado) y
`POST …/import?mode=review` (revisión previa: nada se guarda) seguido de `?mode=apply` (todo o nada;
con un error, 422 `Import.Invalid`). La lista está en `/inventario/plantillas`. Orden, hojas, columnas y
respuesta (`ImportResultDto`) son los de `contracts/plantillas.md` §0.1 (dieciséis plantillas, T49):
impuestos y retenciones (`/api/core/taxes`), grupos contables, unidades, marcas, categorías, productos,
bodegas y ubicaciones, tipos de documento (prefijo, consecutivo, aprobación, modo de paso y
granularidad), vendedores. Las de puntos y cajas, medios de pago, listas de precios y topes se
descargan desde I1 y se **importan** cuando existe I3; las de saldo inicial y cifras de SOLIDO, en I1;
la de la matriz contable, en I2.

Datos del ensayo que usan los escenarios:

- **Grupos contables** `ASEO` y `ABARROTES`. **Unidades** `UND` (0 decimales), `KG` (3), `CAJA12`.
- **Productos**: P1 jabón en barra, `UND`, compra por `CAJA12` (factor 12), IVA 19 %, grupo `ASEO`,
  con código de barras de unidad y de caja; P1 a P5 para la venta de cinco productos de §5.2; P4,
  además, con saldo inicial 10 × 1.000 en PV1 para el costo de §3.8; P6 para la carrera de §5.3; P7
  excluido de IVA. Mínimo 10, máximo 50 y punto de reorden 15 para P2 en PV1.
- **Bodegas**: PRIN y PV1 en la sucursal S1, PV2 en S2; PV3 en S2 sólo para el paso 5 de §4.2. Las
  bodegas de tránsito de S1 y S2 se crean con la primera bodega operativa de cada sucursal, con el
  código que propone el sistema (`TR` + código de la sucursal) o el que fije quien crea.
- **Personas** (desde el diálogo único): proveedor A (responsable de IVA, gran contribuyente),
  proveedor B (no obligado a facturar), asociados X y Y (activos, misma clase de asociado), asociado Z
  (retirado), cliente W (agente retenedor de IVA). Las marcas tributarias van en la sección «Datos
  tributarios» del diálogo.

### 2.6 Parámetros del ensayo

En `/inventario/parametros` (`AddParameterVersionCommand`, motivo obligatorio). Los demás, con su
defecto seguro.

| Clave | Valor en el ensayo | Para |
|---|---|---|
| `Contabilidad.ModoDePaso` | general `EnLinea`; cadena de ventas `PorLotes`; cadena de traslados `NoPasa` | §4 (US7) |
| `Contabilidad.Granularidad` / `DisparadorDeLote` / `HoraDeLote` | cadena de ventas: `Resumido`, `HoraDiaria`, una hora cercana del día del ensayo | §4.5 |
| `Contabilidad.PoliticaSinRespuesta` | `ConfirmarConPendiente` (defecto) | §4.9 |
| `Existencias.StockNegativoPermitido` | `false` (defecto) | §3.7, §5.3 |
| `Conteo.BloquearMovimientos` / `Conteo.ToleranciaReconteoUnidades` | `true` / `1` | §3.10 |
| `Caja.BaseModo` / `Caja.TratamientoFaltante` | `FondoFijo` / `Gasto` (defectos) | §5.9 |
| `Ventas.BajoCosto` | `Alertar` (defecto) | §5.12 |
| `Dian.ObligadaAFacturar` | `true` (defecto) | §6 |

El modo de paso se cambia **para todos los tipos de una cadena a la vez**: cambiar uno solo responde
`Inventory.PostingMode.ChainMismatch`. Dejar sin paso una cadena con tipos fiscales exige
`Inventory.DocumentTypes.DisableFiscalPosting` y la confirmación explícita que los nombra
(`Inventory.PostingMode.FiscalRequiresConfirmation`).

Políticas de aprobación (`/inventario/politicas-de-aprobacion`): ajustes en dos niveles (umbral 0 con
`Inventory.Adjustments.Approve`; umbral 1.000.000 con `Inventory.Approvals.Management`); compras con
un nivel desde 20.000.000; saldo inicial, diferencias de traslado, ajustes de conteo, diferencias de
arqueo, movimientos de caja, descuentos sobre tope y crédito provisional con un nivel.

## 3. I1 · Núcleo

**Orden del recorrido.** Los escenarios van agrupados por la entrega que trae lo que prueban, pero
el ensayo se hace con I1 a I4 desplegadas (FR-095) y en este orden: §2 → §3.1–§3.5 → §4.1–§4.2
(sin bodegas activas no hay movimientos) → §3.6–§3.14, con §4.3, §4.4 y §4.6 al paso de sus
documentos → §6.1 (sin preparación DIAN una cooperativa obligada no vende) → §5 → §4.2 paso 5 (la
bodega que se activa después de haber ventas) → §6.2–§6.10 → §4.5 y §4.7–§4.10 → §6.11 cuando haya
proveedor. Con sólo I1 desplegada, todo documento nace con sus
mensajes y la entrega a Contabilidad queda `Pending` sin consumidor: es lo esperado (T7).

### 3.1 Retiro del módulo actual (FR-092, FR-031)

1. El menú ya no muestra las 20 pantallas del módulo actual ni sus temas del manual; muestra los
   grupos **Inventario**, **Compras**, **Ventas** y **Punto de venta** (`TodoEnlaceDelMenuTieneSuPagina`).
2. Ninguna ruta del módulo responde sin permiso: con el token de `lectura`, cualquier escritura de
   `/api/inventory/*` responde el mismo 404 que algo inexistente (antes, las 87 rutas sólo pedían
   sesión).
3. `/ventas/vendedores`: crear un vendedor sobre una persona existente escribe la fila del rol y la
   marca «Vendedor» juntas; retirarlo y volver a crearlo **restaura** la misma fila. La plantilla de
   vendedores con una persona inexistente rechaza esa fila nombrándola y no guarda nada.

### 3.2 Importar el catálogo (US1-1, US1-4, US1-6; FR-030)

1. Plantilla de productos de **500 filas con tres errores**: un código de barras repetido, una unidad
   inexistente y un impuesto inexistente. `?mode=review` → no se guarda nada y la respuesta
   (`ImportResultDto`, `valid = false`) trae los tres en `errors[] { sheet, row, column, code, message }`
   con qué hacer; el de código de barras nombra al producto que ya lo tiene
   (`Inventory.Barcode.Duplicate`). `?mode=apply` con el mismo archivo → 422 `Import.Invalid`.
2. Corregida, `?mode=review` lista las 500 altas; `?mode=apply` las guarda en una sola transacción.
3. Cambiar por importación el grupo contable de un producto con existencia exige
   `Inventory.Catalog.ReclassifyAccountingGroup` (sin él, `Import.Cell.PermissionRequired`) y motivo
   (`requiresReason = true`; sin él, `Import.Cell.Required`), como en la pantalla (§3.3).
4. Todas las plantillas del orden de §2.5 pasan por la misma revisión previa, todo o nada.

### 3.3 Unidades, códigos de barras, búsqueda y estados (US1-2 a US1-5; SC-009)

1. En cualquier campo de producto, leer con el escáner el código de la caja de P1: queda elegido P1
   con la unidad `CAJA12`, sin más pasos. Leer el de la unidad: P1 con `UND`.
2. Búsqueda mientras se escribe (`/api/inventory/products/search?q=`): resultados en menos de 1 s por
   código, código de barras, nombre o referencia, sin distinguir tildes ni mayúsculas. Con 50.000
   productos lo mide `BusquedaDeProductos50kTests` en los dos motores.
3. Digitar 1,5 en una línea con unidad `UND` → `Inventory.Unit.DecimalsNotAllowed`.
4. Borrar P1 después de §3.6 → no se permite; se ofrece inactivar o bloquear. Inactivo, no aparece en
   documentos nuevos y su kardex se sigue consultando; bloqueado, sólo admite conteo y la recepción de
   lo que ya estaba en tránsito.
5. Cambiar el grupo de P1 con existencia (`/api/inventory/products/{id}/accounting-group`): exige
   permiso y motivo, afecta sólo lo futuro y emite «GrupoContableReclasificado» con cantidad y valor
   por bodega. Una imagen del producto se sube como adjunto `InventoryProduct`.

### 3.4 Bodegas, ubicaciones y tránsito (FR-032 a FR-035)

1. Al crear PRIN se crea con ella la bodega de tránsito de S1, con el código propuesto (`TR` + código
   de la sucursal) o el que se fije en `transitWarehouse { code, name }`; al crear PV2, la de S2. Crear
   a mano otra bodega de tipo tránsito → `Inventory.WarehouseType.TransitIsSystem`. Cada bodega tiene
   una ubicación por defecto. Una bodega de tránsito no se inactiva mientras tenga existencia, no
   vende ni despacha.
2. Mínimo, máximo y punto de reorden de P2 en PV1 (`/api/inventory/reorder-policies`): 10 / 50 / 15.
3. Las bodegas nacen **no activas**: cualquier documento que no sea su saldo inicial responde
   `Inventory.Warehouse.NotActive`.

### 3.5 Saldo inicial y cifras de SOLIDO (US4-1, US4-4, US4-5, US4-6; FR-089, FR-091; SC-016)

1. `/inventario/cifras-solido`: importar las cifras de SOLIDO al corte (fecha, códigos tal como vienen,
   cantidad y valor) de PRIN, PV1 y PV2 (`ImportLegacyFiguresCommand`).
2. `/inventario/saldo-inicial`: plantilla de **20.000 líneas** con un producto y una bodega
   inexistentes → no se guarda nada y se listan los dos errores con fila y columna. Corregida: un
   documento `OpeningBalance` por bodega (hasta 4.000 líneas cada uno), fechado en la fecha de corte
   de la bodega, la víspera de su activación. Las líneas llevan lote y serie sólo cuando el producto
   los controla (I6).
3. Se confirma con aprobación (`Inventory.OpeningBalance.Approve`, otra persona). Emite
   «SaldoInicialCargado», **informativo**: no pasa por la validación previa ni por el modo de paso.
4. Vistas `legacy-comparison-kardex` y `legacy-comparison-valuation`: por producto y bodega, existencia
   y valor de cada sistema y la diferencia.
5. Se sigue en §4.2 (activación). Toda la jornada —cargar, conciliar y activar 20.000 líneas— cabe en un
   día de trabajo (SC-016).

### 3.6 Compra directa, factura del proveedor y devolución (US9, US3-6; FR-044, FR-050, FR-051)

Requiere PRIN activa (§4.2).

1. `/compras/compra-directa` (`POST /api/inventory/purchases/direct`), `comprador`, proveedor A: 3
   cajas de P1 a 15.600 la caja. Confirmada: el kardex registra **36 unidades** a 1.300 y el documento
   muestra 3 cajas (US1-2); emite «CompraRecibida». La respuesta trae el número del tipo sin huecos.
2. Registrar la factura del proveedor contra la recepción (`/compras/facturas-proveedor`) con prefijo,
   número y CUFE: impuestos y retenciones por el catálogo vigente, según el concepto de cada producto,
   el régimen de las dos partes y el municipio de la bodega que recibe
   (`OperationMunicipalityDaneCode`). Una base por debajo del mínimo no retiene; **igual** al mínimo,
   sí. Emite «FacturaProveedorRegistrada» con el IVA descontable separado del IVA al costo.
3. Una compra con un tipo marcado como no descontable (consumo interno): el IVA entra al costo; el INC
   siempre (US3-6, caso dorado 14).
4. Factura a crédito sin acuse (030) ni recibo del bien (032): a los `Compras.DiasAlertaEventosRadian`
   días aparece la alerta `Compras.EventosRadianFaltantes`. Registrar que la cooperativa los emitió
   por fuera (`POST /api/inventory/purchases/supplier-invoices/{id}/radian-events`,
   `Inventory.Purchases.RegisterRadianEvent`) la cierra; la vista `radian-events` muestra el estado.
5. Devolución a proveedor de 6 unidades (`/compras/devoluciones`): salen al costo con que entraron,
   enlazadas a la recepción; la diferencia contra el promedio vigente, si la hay, sale como ajuste de
   costo («DevolucionRegistrada» + «AjusteDeCostoReconocido», caso dorado 03).
6. Con **otra** compra directa y su factura: anular la recepción → `Inventory.Document.HasDependents`,
   que nombra la factura. Anulada primero la factura (documento contrario, «DocumentoAnulado»), la
   recepción sí se anula. (La compra de los pasos 1 a 5 se conserva: la usan §3.9 y §4.3.)

### 3.7 Kardex inmutable, anulación, integridad e idempotencia (US2-2, US2-3; SC-002, SC-006, SC-013)

1. Un ajuste positivo confirmado en PRIN no tiene «Editar» ni «Borrar», y ninguna ruta lo modifica.
2. Anularlo con motivo (`POST /api/inventory/adjustments/{id}/void`): aparece un documento contrario
   enlazado en los dos sentidos, con **su propia fecha** y al costo del original; el kardex muestra las
   dos líneas; la existencia vuelve a la de antes. Anularlo otra vez →
   `Inventory.Document.AlreadyVoided`.
3. **Idempotencia** (SC-002). Repetir tres veces la misma confirmación con la misma clave:

   ```bash
   K=$(uuidgen)
   for i in 1 2 3; do
     curl -s -o /dev/null -D - -X POST -H "$H" -H "Idempotency-Key: $K" \
       "$API/api/inventory/adjustments/$DOC/confirm" | grep -iE "^HTTP|Idempotent-Replayed"
   done
   ```

   Un solo documento confirmado con un solo número; la segunda y la tercera respuesta traen
   `Idempotent-Replayed: true` y la auditoría registra `Operation.Replayed`. Doble clic en
   «Confirmar» en la pantalla: lo mismo.
4. **Integridad** (`/inventario/integridad`, `/api/inventory/integrity/verify`,
   `Inventory.Integrity.Verify`): 0 diferencias entre kardex y proyecciones (SC-006). Alterar a mano,
   por SQL y sólo en la base de ensayo, una fila de `INV_StockBalances`: la verificación informa el
   incidente con producto y bodega y levanta `Inventario.IncidenteDeIntegridad`. Reconstruir
   (`integrity/rebuild`, `Inventory.Integrity.Rebuild`) → 0 diferencias. El kardex no se toca: es la
   fuente.

### 3.8 Costo promedio contra los casos dorados (US3-1, US3-2; SC-007)

La contadora reproduce en la cooperativa los casos 01, 02, 04 y 06 y firma que coinciden **al peso**:

1. P4, sin otros movimientos, con saldo inicial 10 × 1.000 en PV1; recepción de 10 × 1.300 en PV1 →
   promedio **1.150** en `/inventario/kardex` (ámbito cooperativa).
2. La venta de §5.2 saca P4 a 1.150. Una recepción de 10 × 1.500 cambia el promedio; la devolución de
   P4 en §5.6 entra a **1.150**, el costo con que salió (caso 02).
3. El traslado de §3.9 viaja al costo de origen y el valorizado total no cambia (casos 04 y 05).

### 3.9 Traslados en dos pasos (US10, US3-2; FR-039)

Requiere PV2 activa (§4.2). `bodega.a` despacha, `bodega.b` recibe.

1. Despachar 10 de P1 de PRIN a PV2 (`/api/inventory/transfers/{id}/dispatch`): PRIN baja 10; el
   tránsito de **S1** (la sucursal de origen) sube 10; PV2 los ve «en tránsito» y no puede venderlos;
   la vista `valuation` da el mismo total antes y después. Emite «TrasladoDespachado».
2. Recibir 9 (`/receive`): entran 9 y se emite «TrasladoRecibido»; la unidad que falta queda en
   tránsito como faltante pendiente (`INV_TransferDiscrepancies`). Resolverla
   (`/api/inventory/transfers/discrepancies/{id}/resolve`) exige aprobación de otra persona: baja
   desde tránsito con la causa «reclamación al transportador» («AjusteInventarioAprobado»). Las otras
   dos salidas —devolución al origen y recepción tardía— se prueban en `TrasladoEnDosPasosTests`.
3. Otro traslado recibido con 11 de 10: el sobrante queda registrado fuera de la existencia hasta que
   se aprueba su ajuste positivo al costo vigente.
4. Anular un despacho no recibido: la mercancía vuelve a PRIN. Uno ya recibido sólo se corrige con un
   traslado contrario.
5. Mover P1 entre dos ubicaciones de PRIN: un solo paso, sin cambio de costo ni mensajes contables.
6. Bloquear P1 con unidades en tránsito: la recepción del traslado sí se admite.
7. `bodega.b` no ve los documentos de PRIN, pero sí los traslados que llegan a PV2 (alcance por
   origen **o** destino).

### 3.10 Conteo físico y ajuste aprobado (US11; FR-040, FR-041)

1. `jefe` abre un conteo cíclico de una ubicación de PV1 (`/api/inventory/counts/{id}/open`): la foto
   queda fija y el conteo **sin número** (se numera al cerrarlo). Con `Conteo.BloquearMovimientos =
   true`, una salida de un producto incluido se rechaza (`Inventory.Count.ProductsLocked`). Un conteo
   abierto se descarta con motivo (`/discard`) sin dejar hueco; uno cerrado sólo se anula con un
   documento contrario (`/void`).
2. `bodega.a` captura con el lector (`/captures`): tres lecturas del mismo código cuentan 3. Con conteo
   ciego no ve el teórico.
3. Una diferencia de 3 unidades con tolerancia de 1 exige reconteo antes de proponer el ajuste.
4. El ajuste propuesto no afecta la existencia hasta que lo aprueba alguien que no abrió ni contó: el
   `jefe` y `bodega.a` reciben `Approvals.SelfApprovalForbidden`; `aprobador` lo aprueba. Se fecha en
   la fecha de la foto (`Conteo.FechaDelAjuste = Foto`), al promedio de esa fecha, y emite
   «AjusteInventarioAprobado». Aunque después de la foto haya salidas del mismo producto en PRIN
   (ámbito cooperativa), se confirma con `Costeo.RetroactivosPermitidos = false`: el ajuste entra en
   su lugar y el costo de esas salidas no cambia (caso dorado 15). Vista `count-differences`.

### 3.11 Cierre y reapertura del período (US3-3 a US3-5, US17-1; FR-047)

1. Con un conteo abierto con foto en M, `POST /api/inventory/periods/{year}/{month}/close` no se
   completa y lo nombra. Cerrado el conteo, el cierre **avisa** (sin bloquear) de borradores, tránsitos
   sin resolver y mensajes pendientes, en lote o rechazados con fecha en M; fija el valorizado por
   grupo y bodega y emite «PeriodoInventarioCerrado». Si la cadena de ventas tuviera
   `DisparadorDeLote = CierreDePeriodo`, el cierre crea además su lote (`BatchTrigger.PeriodClose`).
2. Confirmar un documento fechado en M → `Inventory.Period.Closed`, que nombra el período.
3. Un traslado despachado en M y recibido en M+1: la recepción se fecha en M+1 y el tránsito aparece
   en el valorizado de los dos cortes.
4. Reabrir M sin `Inventory.Periods.Reopen` → 404. Con el permiso y motivo → abierto, auditado, emite
   «PeriodoInventarioReabierto». Reabrir un período anterior al último cerrado se rechaza.
5. El valorizado a una fecha de corte pasada se reconstruye desde el kardex y coincide con el que se
   fijó al cerrar (US17-1).

### 3.12 Reorden, quiebre y alertas (US17-2 en su parte de I1; FR-035, FR-022; SC-022)

1. P2 en PV1 con 12 disponibles y 5 en tránsito hacia PV1 (posición 17): no aparece en la vista
   `reorder-alerts` ni levanta alerta.
2. Con 8 disponibles y 5 en tránsito (posición 13): alerta `Inventario.Reorden` con un sugerido de
   **37** (50 − 13) y `Inventario.Quiebre` porque 8 está bajo el mínimo. Las levanta el
   `ProgramadorDeTareas`, llegan a `/inventario/alertas` de quien tiene el permiso destinatario y
   alcance sobre PV1, y quien la atiende deja una nota.
3. **SC-022**: quitar el permiso destinatario de todos los usuarios activos y provocar otra alerta:
   llega a los titulares de `CompanyAdmin` marcada `WithoutRecipient`. El reporte de completitud lista
   el tipo cuyo permiso nadie tiene.

### 3.13 Seguridad, aprobaciones, parámetros y auditoría (US12, US2-4, US2-5; SC-012 a SC-014)

1. **Permiso** (US12-1, SC-014): `lectura` pide por `curl` confirmar un ajuste → 404, igual que un
   documento inexistente; en pantalla no ve el botón (`PermissionGate`). La puerta es el servidor
   (`LosEndpointsProtegidosExigenPermiso`).
2. **Alcance** (US2-5): `bodega.b` busca documentos de PRIN y no aparecen; pedir por su `PublicId` el
   detalle, el kardex de PRIN o crear un ajuste en PRIN → 404 (`AlcancePorBodegaTests`,
   `LasConsultasDeInventarioRespetanElAlcance`). La existencia de la bodega de tránsito sólo la ve
   quien tiene alcance total o asignación explícita.
3. **Dos niveles** (US12-2, US2-4): `bodega.a` crea y envía un ajuste negativo de **3.000.000**: queda
   `PendingApproval`, sin número y sin tocar la existencia (alerta `Aprobaciones.Pendiente`).
   `aprobador` aprueba el nivel 1: sigue en aprobación. `bodega.a` intenta aprobar →
   `Approvals.SelfApprovalForbidden`; el `aprobador` no puede aprobar también el nivel 2. `jefe`
   aprueba el nivel 2: se confirma y recibe número en esa misma transacción. Rechazar exige motivo y
   devuelve el documento a borrador.
4. **Monto máximo** (US12-6): `comprador` confirma una compra de **8.000.000** (su límite es
   5.000.000; la política de compras empieza en 20.000.000): la compra exige igual el nivel 1. Con un
   tipo de compra sin política → `Inventory.Approval.AmountExceedsLimit` con `data.maxAmount =
   5000000`.
5. **Parámetro con vigencia** (US12-3): `Existencias.StockNegativoPermitido = true` desde el primer día
   de M+2, con motivo. Los documentos de hoy siguen con `false`; el historial muestra los dos valores,
   quién y por qué. `Costeo.Metodo` con vigencia a mitad de período → `Parameters.RequiresPeriodStart`.
6. **Auditoría** (US12-4, SC-012): en `/admin/auditoria`, cada acción de §3 con actor, fecha y hora,
   IP, canal (`web`, `app`, `pos` o `proceso`), entidad con antes y después y, cuando se exige, motivo.
   Un rechazo aparece como `Rejected` con su `Error.Code`; la clave de idempotencia va en la metadata;
   exportar e imprimir tienen su evento; abrir una pantalla deja `Module=Navigation`.
7. **Integridad de la auditoría** (US12-5, SC-012): sólo en la cooperativa de ensayo y con la
   credencial administrativa de Mongo, cambiar un campo de un evento y borrar otro en
   `IngenIA365ERP_Audit_{PublicId}`. `auditor`, en la pestaña «Integridad» de `/admin/auditoria`
   (`POST /api/audit/integrity/verify`, `AuditLog.VerifyIntegrity`, cuerpo `{ from, to }`): informa
   **alterado** (`Altered`) en el primero y **eliminado** (`Deleted`, salto de secuencia) en el segundo,
   en `incidents[]`; la verificación misma queda como `AuditLog.IntegrityVerified`. Un rango de más de
   10 años → 400 `Validation.Invalid`. `IntegridadDeAuditoriaTests` repite lo mismo, más intercalado y ancla
   inválida.
8. **Inmutabilidad** (SC-013): ninguna ruta ni pantalla edita un documento confirmado, una línea de
   kardex ni un mensaje emitido (`LosHechosInmutablesNoSeModifican`, `PrincipioXI_ContableImmutable`
   y la guarda de `SaveChangesAsync`). La única excepción es el caso a de §6.5.

### 3.14 Informes de I1 (FR-086, FR-087)

`/inventario/informes?vista=` sobre `/api/reports/inventory/{vista}?format=`: `kardex` (con enlace al
documento), `valuation` (con tránsito), `stock`, `documents`, `count-differences`, `reorder-alerts`,
`radian-events`, `legacy-comparison-kardex`, `legacy-comparison-valuation`. En JSON con
`Inventory.Reports.View`; en Excel o PDF con `Inventory.Reports.Export`, cada exportación auditada.
`bodega.b` sólo ve PV2 en todas. Con 50.000 productos en 50 bodegas, el valorizado a una fecha
pasada tiene que estar listo en menos de 30 s (SC-017, medición en QA).

## 4. I2 · Integración con Contabilidad

### 4.1 Matriz, tipos de comprobante y completitud (FR-073, FR-082; D-07)

1. `contadora` descarga `/api/accounting/inventory/rules/template.xlsx`, la diligencia (las columnas de
   `contracts/plantillas.md` §16: operación, rol, grupo, bodega, punto, medio de pago, tarifa,
   `tarifaPorcentaje`, causa → cuenta, con vigencia) y la importa con
   revisión previa (`/contabilidad/inventario/matriz`). Una regla con una cuenta que no es de
   movimiento o no está habilitada para Inventario se rechaza con la regla de la 009; dos versiones
   que se cruzan → `Accounting.InventoryRule.Overlaps`.
2. `/contabilidad/inventario/tipos-de-comprobante`: el mapeo sembrado (`FV`, `EI`, `SI`, `NV`, `CP`,
   `TR`, `AC`, `CJ`) y sus cruces.
3. `/contabilidad/inventario/completitud` (`/api/accounting/inventory/completeness`): combinaciones en
   uso sin regla, medios de pago sin cuenta, reglas con cuentas que dejaron de ser elegibles e
   impuestos cuya cuenta tiene otra tarifa (C8). Para probar lo último, poner en la cuenta del IVA 5 %
   la tarifa 0,19: aparece `Accounting.InventoryRule.TaxRateMismatch` con el impuesto, la cuenta y
   las dos tarifas. Restaurada, el reporte debe quedar **vacío** antes de seguir (D-07).

### 4.2 Activación de bodegas con cuadre (US4-2, US4-3, US4-5; FR-090; SC-016)

1. `/inventario/activacion` › PRIN (`/api/inventory/warehouses/{id}/activation`): por grupo contable y
   conjunto de cuentas mapeadas, el saldo contable al corte, el valorizado de PRIN (módulo nuevo), el
   de PV1 y PV2 **con sus cifras de SOLIDO** y la diferencia. PV1 y PV2 suman al valorizado del
   conjunto y se muestran aparte para identificarlas. `ASEO` cuadra; `ABARROTES` difiere en 250.000
   (§2.3). El inventario de PV1 y PV2 no aparece como diferencia.
2. `bodega.a` intenta activar → 404. Un rol con `Inventory.Warehouses.Activate` y sin
   `Inventory.Warehouses.AcceptActivationDifference` no puede activar con diferencia. `jefe` (su
   plantilla trae `Warehouses.*`), con motivo → PRIN queda activa, la comparación se guarda
   (`INV_WarehouseActivations`) y queda auditada.
3. Activar PV1 y después PV2 del mismo modo. Desde ahí sólo el módulo nuevo opera esas bodegas.
4. «SaldoInicialCargado» aparece en Contabilidad como registro **sin comprobante**
   (`ACC_InventoryPostings` sin `AccountingDocumentId`), y la conciliación lo cuenta como incluido en
   el saldo contable (US4-5).
5. **Bodegas activadas en fechas distintas** (FR-089, FR-091; excepción de puesta en marcha, caso
   dorado 17). Después de §5.2, con P4 ya vendido en PV1 en días posteriores a su activación: cargar
   las cifras de SOLIDO y el saldo inicial de P4 en PV3 (S2, todavía `NotActivated`), con un costo
   distinto del promedio y fechado en su corte, que cae **antes** de esas ventas (ámbito
   cooperativa). Se confirma aunque `Costeo.RetroactivosPermitidos = false`: el kardex de P4 inserta
   el saldo en su fecha (`OperationDate`, `Id`), agrega líneas de ajuste de costo a las salidas
   posteriores sin tocar las ventas, y sale un «AjusteDeCostoReconocido» por documento afectado.
   Activar PV3 con su cuadre. Un ajuste digitado con la misma fecha, en cambio, sigue rechazado como
   retroactivo hasta I5. Lo repite `SaldoInicialYActivacionTests`.
6. Antes de I2 no hay comparación contra los libros: activar aceptando la diferencia sólo es posible
   fuera de producción. En un ambiente de producción, el `POST` responde 422
   `Inventory.Activation.AccountingUnavailable` mientras no exista la consulta de saldos.

### 4.3 La compra de §3.6 en Contabilidad (US7-1; SC-011)

1. `/inventario/bandeja-de-mensajes`: «CompraRecibida» en `Processed` **antes de un minuto** desde la
   confirmación, sin que nadie lo ordene, con el comprobante (tipo y número) que guardó la entrega:
   la bandeja no consulta tablas `ACC_`.
2. En Contabilidad, un `EI` con origen enlazado al documento de Inventario; la factura del proveedor,
   un `CP`. Actor de la auditoría: «Proceso de integración», canal `proceso`, origen `Mensaje:{id}`,
   sin IP; `comprador` figura como usuario de origen, nunca como actor.
3. Los relacionados siguen al original: la devolución de §3.6 llega detrás de su recepción.

### 4.4 Validación previa que detiene la confirmación (US7-2; FR-074; SC-021)

Cuatro documentos que **no** se confirman; cada uno responde 422 `Inventory.Prevalidation.NotPostable`
con `data.errors[] { lineNumber, account, rule, whoFixes }`, no consume número y no emite nada:

1. un producto de un grupo sin regla en la matriz (`Accounting.InventoryRule.Missing`, lo corrige quien
   tiene `Accounting.InventoryRules.Manage`);
2. un consumo interno sin centro de costo cuando la cuenta de gasto lo exige;
3. un producto cuyo impuesto tiene otra tarifa en su cuenta (`TaxRateMismatch`);
4. una compra fechada en un mes contable cerrado.

Lo que la validación respondió «contabilizable» queda con `PrevalidationOutcome = Postable` en el
mensaje, para medir SC-021.

### 4.5 El lote resumido de las ventas del día (US7-3; FR-077; SC-020)

Después de las ventas de §5 y §6.

1. Antes de la hora del lote: los mensajes de ventas están `InBatch`. `contadora` pide la vista previa
   (`/contabilidad/inventario/lotes`, `/api/accounting/inventory/batches/preview`): documentos y
   comprobantes que saldrían, y el `cutoffMessagePublicId` del último mensaje listado (ningún Id
   interno sale en la respuesta).
2. A la hora (`Scheduled`) —o al ordenarlo con `Accounting.InventoryBatches.Run` y ese
   `cutoffMessagePublicId`, que responde 202 y procesa exactamente lo previsualizado—: **un comprobante `FV` por fecha, tipo y sucursal**, fechado
   en la fecha de las ventas y no en la del lote. Las líneas cuya cuenta exige tercero, documento cruce
   o base gravable conservan su detalle; los débitos y créditos no se netean. El lote lista sus
   documentos; el actor es el proceso de integración y los cajeros quedan como usuarios de origen.
3. Ordenar el mismo rango otra vez: el lote queda `Empty`; no hay comprobante nuevo.
4. SC-020: 5.000 documentos en menos de 10 minutos, y volver a procesarlos no crea nada. Lo mide
   `LotesProgramadosTests` con `RUN_PERF_TESTS=1`.

### 4.6 El traslado que no pasa (US7-4)

Los mensajes de §3.9 están en `NotApplicable` y no llegaron a Contabilidad; la conciliación los muestra
como partida aparte.

### 4.7 Rechazo por período cerrado, bandeja y reproceso (US7-5, US7-6; FR-080; SC-002)

1. Con ventas de un mes todavía `InBatch` (en local o DEV, con el despachador apagado como en §4.9),
   cerrar ese mes contable en la 009: responde `Accounting.Period.InventoryPending` con cuántos hay
   por estado. «Procesar ahora» lanza el lote; «Cerrar de todos modos» (`AcknowledgeInventoryPending`)
   cierra y queda auditado.
2. Cerrado así, el lote rechaza esos mensajes: `Rejected` con el motivo en la bandeja, alerta
   `Integracion.MensajeRechazado` a Inventario y a Contabilidad. Los documentos siguen confirmados.
3. Reabrir el mes según la 009 y reprocesar (`POST /api/inventory/messages/reprocess`,
   `Inventory.Messages.Reprocess`, motivo): se crea un lote `Reprocess`, las entregas pasan de
   `Rejected` a `InBatch` con ese lote, se contabilizan con su **fecha original** y arrastran a sus
   dependientes. El actor es la persona que reprocesó.
4. Un mensaje ya procesado que llega otra vez (lo fuerza `EntregaGarantizadaTests`): el consumidor
   responde `AlreadyProcessed` por el recibo único de `ACC_InventoryPostings`; no hay segundo
   comprobante.

### 4.8 Anulaciones que siguen al original y envío posterior (US7-9, US7-10; FR-078, FR-079)

En dos días del ensayo (o con el reloj de `ContabilizacionPorMensajesTests`):

1. Día 1: cadena de ventas en `NoPasa` (con la confirmación fiscal de §2.6); vender. Día 2: cadena en
   `PorLotes`; anular la venta del día 1 con su documento de corrección: su mensaje queda
   `NotApplicable`, como el original, aunque el tipo ya cambió de modo.
2. `POST /api/inventory/messages/send-not-applicable` (`Inventory.Messages.SendNotApplicable`, motivo,
   rango del día 1): se crea un lote `SendNotApplicable` y la venta **y** su corrección pasan de
   `NotApplicable` a `InBatch`; se envían en orden y en la misma operación, con su fecha original. Si alguno cae en un mes contable cerrado, el envío de ese documento se rechaza
   completo a la bandeja.

### 4.9 Contabilidad no disponible (SC-010; FR-015)

En local o DEV, apagar el despachador (`Integration:Dispatcher:Enabled = false`) y vender de contado:
las ventas se confirman sin error y sus entregas quedan `Pending` o `InBatch`, visibles. Con la
validación previa sin respuesta (lo simula `EntregaGarantizadaTests` con un doble que no responde):
`ConfirmarConPendiente` confirma con `PrevalidationOutcome = NoResponse`; `Bloquear` no confirma.
Encenderlo: lo de tipos en línea se procesa solo y lo de tipos por lotes, en su lote siguiente. En la
bandeja sólo queda lo rechazado por reglas. Los reintentos esperan cada vez más y, a los 3 intentos
o 15 minutos, levantan `Integracion.MensajeSinEntregar`.

### 4.10 Conciliación (US7-7; FR-081; SC-005)

`/inventario/conciliacion` (vista `reconciliation`, exige además `Inventory.Reconciliation.View`) a
una fecha de corte con **cero** mensajes pendientes, en lote o rechazados: diferencia **cero** por
grupo contable y conjunto de cuentas. El saldo inicial cuenta como incluido; los traslados que no
pasan y las ventas a crédito de tipos sin paso van aparte; una bodega que siga en SOLIDO y comparta
cuentas entra con sus cifras al corte, suma al valorizado del conjunto y se muestra aparte para
identificarla (no se le atribuye diferencia). Dejar un mensaje rechazado: la diferencia aparece y la explica ese mensaje. La conciliación
ignora el alcance de sucursal contable del usuario (sólo agrega por cuenta).

## 5. I3 · Ventas y POS

### 5.1 Medios de pago, puntos, cajas, listas y topes (FR-096, FR-058, FR-053)

1. `/maestros/medios-de-pago` (pestañas Medios, Franquicias, Adquirentes, Datáfonos de cobro,
   Denominaciones): además de `EFECTIVO`, crear `VISARB` (tarjeta de crédito, Visa, adquirente Redeban,
   su datáfono), `MCCB` (Mastercard, Credibanco), `BONOMERC` (bono, número único, arqueo por
   referencias), `TRANSFBCO` (transferencia, banco y cuenta destino como dato), `CREDASOC` y `CREDCLI`
   (crédito, sin arqueo, con plazo y cuotas como datos del medio). Cada uno con su código DIAN sugerido
   «pendiente de validar por la contadora».
2. Importar las plantillas de I3: punto PV1 (canal mostrador, POS habilitado) con cajas 1 y 2 y sus
   tipos por rol (`PosSale`, `InvoiceOnRequest`, `PosAdjustmentNote`, `InvoiceCreditNote`,
   `PosSaleContingency` con una resolución que respalda al documento equivalente POS e
   `InvoiceContingency` con una que respalda a la factura); listas `GENERAL`, una por la clase de X y
   Y, y una para X; topes por rol.
3. En la matriz, una cuenta por medio de pago (y por punto, para el efectivo). La completitud vuelve a
   quedar vacía.
4. Cambiar la tolerancia de arqueo de `EFECTIVO` exige motivo (`PUT` del medio) y queda auditado; la
   tolerancia y la comisión esperada se copian a cada línea de arqueo y pago que las usa, así una
   sesión ya cerrada conserva la tolerancia con que se arqueó.

### 5.2 Turno y venta de cinco productos con lector (US5-1, US5-5, US5-6; SC-004, SC-008)

1. `cajero.1` abre sesión en la caja 1 con su base (`FondoFijo`). Abrir otra en la caja 1, o que
   `cajero.1` abra otra en la caja 2, se rechaza (`UnaSesionPorCajaTests`). Una caja sin sesión no
   vende (`Inventory.CashSession.NotOpen`).
2. En `/pos`, sólo con teclado y lector: **6 lecturas de 5 productos** (P3 leído dos veces suma
   cantidad), consumidor final, F10, efectivo con el billete entregado: las vueltas en pantalla. La
   venta confirmada tiene su documento equivalente POS validado (con el `CanalSimulado` respondiendo
   `Validated`) y la tirilla lista **en menos de 30 s desde la primera lectura** (SC-004). Los atajos
   están a la vista (F2 buscar, F3 cantidad, F4 cliente, F6 vendedor, F7 descuento, F8 suspender, F9
   recuperar, F10 cobrar, Supr quitar, Esc volver); F5, F11, F12 y Ctrl+W no se usan.
3. Una venta hecha pasadas las 19:00 hora de Colombia queda con la fecha de **hoy**, no la de mañana
   (`HoyLocal`).
4. SC-008: vender, recibir, despachar o recibir un traslado, capturar un conteo y consultar una
   existencia, en 3 pasos o menos cada uno (un paso es un cambio de pantalla o una confirmación
   explícita; lecturas y cantidades no cuentan).
5. SC-019: con 30 cajas vendiendo a la vez en QA, el p95 del tiempo del punto 2 sigue bajo 30 s.

### 5.3 Dos cajeros por las últimas unidades (US2-1; SC-001)

P6 con 5 disponibles en PV1. `cajero.1` y `cajero.2` agregan 4 cada uno y cobran a la vez: una venta se
confirma; la otra responde `Inventory.Stock.Insufficient` con **1 disponible**. La existencia nunca
queda negativa. La versión de 50 ventas y 1.000 repeticiones está en §1.2.

### 5.4 Pago mixto con cuatro medios y bono único (US5-2, US5-9; FR-097, FR-098; SC-024)

1. Venta de **500.000** pagada con 200.000 `VISARB`, 150.000 `MCCB`, 100.000 `BONOMERC` y el resto en
   efectivo. Si los pagos no suman el total, no se confirma: `Payments.TotalMismatch` y la pantalla
   dice cuánto falta o sobra.
2. Cada pago queda con medio, valor y referencia: aprobación de cada tarjeta (y sólo los cuatro
   últimos dígitos, nunca el número completo), número del bono.
3. El mismo número de bono en la caja 2, a la vez o después → `Payments.VoucherAlreadyUsed`, que nombra
   la venta que lo usó (`BonoUnicoConcurrenteTests`).
4. En el lote (§4.5), cada pago llega a la cuenta de su medio sin digitar nada.
5. SC-024: inactivar la regla de `MCCB` y cobrar con él → la validación previa detiene la venta y dice
   qué cuenta falta; la completitud lo lista.

### 5.5 Listas de precios y descuento sobre el tope (US5-3, US5-7; FR-053, FR-054)

1. Vender P1 a X: aplica la lista de X y la pantalla la nombra. Venderle a Y (misma clase): la de la
   clase. A consumidor final: `GENERAL`. Un producto que la lista de X no trae toma el precio de la
   siguiente aplicable.
2. `cajero.1` aplica 12 % sobre una línea con su tope de 5 %: se crea la solicitud. `supervisor`
   aprueba desde su sesión en `/inventario/aprobaciones` (el POS consulta cada 2 s) o en la caja con
   su llave o su código de un solo uso; **nunca con contraseña**. Queda quién aprobó y cómo (vista
   `discount-approvals`). Cambiar la línea después invalida la aprobación.

### 5.6 Devolución en mostrador (US5-8, US3-1; FR-066)

La venta de §5.2 ya entregada; después de la recepción de §3.8 paso 2, el cliente devuelve P4:

1. Se emite la **nota de ajuste** del documento equivalente POS, con su propio número y CUDE (con una
   factura sería la nota crédito).
2. El dinero sale por el mismo medio de la venta; por otro medio exige
   `Inventory.Sales.RefundOtherMeans` y queda auditado.
3. P4 vuelve a la existencia a **1.150**, el costo con que salió, aunque el promedio ya sea otro.
   Mensajes: «NotaCreditoEmitida» + «DevolucionRegistrada».

### 5.7 Suspender, quitar, reimprimir y cliente nuevo (FR-059, FR-011)

1. Suspender una venta (F8) con rótulo y recuperarla (F9) en otra caja del mismo punto: sigue igual,
   porque el borrador vive en el servidor. No se cierra una sesión con ventas suspendidas
   (`Inventory.CashSession.HasOpenDrafts`).
2. Quitar una línea antes de cobrar, reimprimir (marca «COPIA», usa la copia fiscal guardada), aplicar
   precio manual o descuento, descartar: cada acción queda auditada una a una.
3. Alta de un cliente desde el POS (F4): se abre el diálogo único de personas en modo compacto con el
   aviso de privacidad; registra «autoriza» o «no autoriza» en el mismo guardado. Sin política
   publicada, el alta se permite con la constancia «sin política vigente» y la alerta
   `Personas.SinPoliticaDeDatos` llega a quien tiene `Compliance.HabeasData.RecordConsent`.

### 5.8 Movimientos de caja (FR-100)

Retiro a caja fuerte de 300.000 (`/pos/movimientos-de-caja`, `/api/inventory/cash-movements`): con
motivo y la aprobación de su política; emite «MovimientoDeCajaRegistrado» y un comprobante con firmas
(`CashMovementReceiptReport`). Una reclasificación entre medios corrige una Visa registrada como
Mastercard sin tocar la venta confirmada.

### 5.9 Cierre de turno por medio de pago (US5-4, US5-10; FR-099; SC-025)

1. `cajero.1` cierra (`/pos/sesiones/{id}/cierre`): por medio de pago, lo esperado (base + ventas −
   devoluciones ± movimientos) y cómo se cuenta: efectivo por denominaciones, tarjetas por el lote de
   cada datáfono (con opción de cotejar referencia por referencia), transferencias y bonos por
   referencias, créditos sin arqueo.
2. Efectivo **20.000 menos** de lo esperado: el cierre es inmediato y crea el documento de arqueo con
   diferencia. Dentro de la tolerancia del medio basta el motivo; por encima exige aprobación, que
   `cajero.1` no puede dar. Aprobada, emite «DiferenciaDeArqueoAprobada» con el tratamiento de
   `Caja.TratamientoFaltante` (gasto), que llega a su cuenta sola (`CJ`).
3. Informe de cierre (vista `cash-session`, `CashCountReport`): ventas, devoluciones y retiros por
   medio, con el detalle de tarjetas por adquirente y datáfono. Con `DisparadorDeLote =
   CierreDeTurno`, el cierre crea además su lote (`BatchTrigger.CashSessionClose`).
4. SC-025: una sesión con **300 ventas y 6 medios** se cierra en menos de 5 minutos con el informe
   listo para imprimir.

### 5.10 Cierre del día por punto (FR-099)

`/pos/cierre-del-dia` exige todas las sesiones de PV1 de ese día cerradas; consolida por medio con el
mismo detalle (vista `day-close`). Después no se abre otra sesión con esa fecha en PV1. Reabrirlo
exige `Inventory.DayClose.Reopen` y motivo.

### 5.11 Crédito provisional (US6-6; F2; SC-023)

1. Venta de **300.000** a X con `CREDASOC`, cobrada por `cajero.1`: no se consulta cupo
   (`CarteraNoHabilitada`). La venta supera el monto de `Inventory.Sales.SellOnCredit` del cajero
   (100.000) y crea una solicitud (`Subject = ProvisionalCredit`, `SourceType = DocumentPayment`) que
   aprueba `supervisor` (monto máximo 1.000.000); no puede el cajero, y `cajero.2` tampoco alcanza el
   monto. Aprobada, el pago
   queda `PendingValidation`.
2. A consumidor final o al asociado retirado Z, el medio de crédito se rechaza.
3. En Contabilidad, la cuenta por cobrar sale de «VentaFacturada» contra la cuenta que la matriz da a
   `CREDASOC`, con tercero y documento cruce `FV`: la vista `pending-documents` de la 009 la muestra
   como cartera provisional por persona.
4. En la bandeja, «VentaACreditoRegistrada» en `Pending` con destino Cartera: «Pendiente — destino
   aún no disponible (IC)», sin intentos ni alertas, con `AccountsReceivableRecordedBy = Contabilidad`
   sellado. Una nota crédito sobre esa venta agrega «AjusteDeVentaACredito», también pendiente y
   dependiente del original.
5. SC-023, primera mitad: el 100 % de las ventas a crédito del ensayo tiene su mensaje para Cartera
   guardado y visible (`CreditoProvisionalTests`). La segunda mitad espera IC (§8).

### 5.12 Retiro gravado y venta bajo costo (FR-037, FR-057)

1. Un consumo interno de tipo «retiro gravado» de P1: base = precio de la lista general vigente, IVA
   del producto; su mensaje lleva costo, base e IVA (operación `RetiroGravado`). Con un producto sin
   precio en esa lista, no se confirma y dice qué precio falta.
2. Vender P7 por debajo de su costo: alerta `Inventario.VentaBajoCosto`; con `Ventas.BajoCosto =
   Bloquear`, no se vende.

### 5.13 Cooperativa no obligada a facturar (US8-9; decisión 20)

En una segunda cooperativa de ensayo con `Dian.ObligadaAFacturar = false`: vender emite el
comprobante de venta no electrónico (`NonElectronicSalesReceipt`, representación en carta con
`SalesDocumentReport`) y ningún documento electrónico; su devolución, la nota no electrónica; anularlo
es un documento contrario. En `coop_ensayo`, obligada, esos tipos no se ofrecen.

### 5.14 Informes de ventas y caja (FR-086, FR-087)

Vistas `sales-by-session`, `sales-by-register`, `sales-by-payment-means`, `cash-session`, `day-close`,
`card-payments`, `cash-movements`, `cash-differences`, `voucher-redemptions`, `discount-approvals`,
`impairment`. Las que traen datos de clientes exigen además `Inventory.Reports.ExportPersonalData` para
exportar; las sesiones de otros cajeros, `Inventory.CashSessions.ViewAll`.

## 6. I4 · Documentos electrónicos DIAN

Primero todo con el `CanalSimulado`, que responde según un dato del documento: validado, validado con
notificaciones, rechazado, en proceso (sin respuesta), DIAN no disponible o canal no disponible.
Después, lo mismo en el sandbox del proveedor (§6.11).

### 6.1 Configuración, resoluciones y preparación (FR-064, FR-065; US8-9)

1. Sin configuración, `GET /api/electronic-invoicing/readiness` lista lo que falta y una venta fiscal no
   se confirma (`ElectronicInvoicing.NotReady`, con `data.missing[]`); abrir sesión de caja ya lo avisa.
2. `/admin/facturacion-electronica`: modo `TechnologyProvider`, canal simulado (`SIMULADO`), ambiente
   de pruebas, con vigencia y motivo. Credenciales en el Secret `erp-fe-credenciales`, clave
   `{tenantPublicId}.{channelCode}.json`, creada con
   `tools/scripts/crear-secreto-facturacion-electronica.ps1 -Ambiente qa -Tenant <publicId> -Canal
   <código>` y montada en `/secrets/facturacion-electronica/`; **nunca** en la base ni en el
   repositorio. «Verificar credencial» (`settings/verify-credential`) deja la fecha.
3. `/maestros/resoluciones-dian`: factura, documento equivalente POS, documento soporte y dos de
   contingencia (una que respalda al documento equivalente POS y otra a la factura), con prefijo, rango, vigencia y ambiente; asociadas al canal desde una
   fecha, con la clave técnica sólo en las de factura, enmascarada y fuera del diff de auditoría.
4. `readiness` vacío. Las representaciones del ambiente de pruebas dicen «SIN VALIDEZ FISCAL».

### 6.2 Factura validada y entregada después (US8-1; SC-015)

1. `/ventas/facturas/nueva` al cliente W: el número siguiente del rango, CUFE, QR y PDF. Queda
   `Validated` al responder el canal y **sólo entonces** se entrega la representación gráfica y sale
   el correo (lo envía el ERP, `Dian.EntregaCorreo = Erp`).
2. W es agente retenedor: la venta registra las retenciones que practica; `AmountDue` = total −
   retenciones sufridas, y los pagos deben igualar `AmountDue`. La retención viaja como línea de
   retención, no como medio de pago.
3. El canónico, el XML firmado, la respuesta y el PDF quedan como adjuntos `ElectronicSalesDocument`:
   se leen con `Inventory.Sales.View` (también el enlace de descarga, `POST …/documents/{id}/download-link`;
   sin ese permiso, 404), no se borran ni admiten subidas.

### 6.3 Documento equivalente POS dentro de la espera de la caja (SC-004, SC-015)

1. Con el canal respondiendo a tiempo, la tirilla se imprime tras la validación.
2. Con el canal «en proceso»: pasados `Dian.EsperaMaximaPosSegundos` (15), la venta queda confirmada,
   la caja libre y el documento en «pendientes de entrega»; nunca se imprime un comprobante sin
   validar. Al validarse, se reimprime o se envía. Si pasa `Dian.MinutosAlertaSinValidar`, alerta
   `Dian.DocumentoSinValidar`.
3. **SC-004 cuando el canal no responde.** Cada espera agotada cuenta como falla para
   `CircuitoDeCanal`. Con el canal «en proceso», vender `Dian.UmbralFallasCircuito` (3) veces seguidas:
   la venta siguiente abre la contingencia 03 (§6.6), toma la numeración de `PosSaleContingency` y su
   representación de papel se imprime en el acto. Medir desde la primera lectura hasta la tirilla de
   esa venta: menos de 30 s. Las tres ventas anteriores siguen en «pendientes de entrega» con su
   número normal, sin renumerarse (`VentaPosCompletaTests`).

### 6.4 Anular una factura validada con una acción (US8-2, US7-8; SC-003)

1. En `/ventas/documentos/{id}` de la factura de §6.2, «Anular»: **una sola acción** emite la nota
   crédito total, con su propio número (consecutivo propio, sin resolución), CUDE y fecha; devuelve la
   existencia al costo con que salió. Un documento contrario (`Voiding`) sobre la factura →
   `Inventory.Document.FiscalUseCorrection`. Sobre un documento equivalente POS validado, la
   corrección es la nota de ajuste.
2. Mensajes: «NotaCreditoEmitida» marcada como anulación total + «DevolucionRegistrada». Como la
   cadena de ventas va por lotes resumidos, en el lote siguiente sale un `NV` **nuevo** con la fecha de
   la nota, enlazado a la factura y a su comprobante resumido en `ACC_InventoryPostings`. El resumido
   original **no se toca** ni se marca reversado, y Contabilidad no lo reversa desde Comprobantes
   (`Accounting.Document.ModuleOwned`, con el mensaje propio de Inventario).
3. Si la factura tenía parte a crédito: «AjusteDeVentaACredito» pendiente para Cartera (§5.11).
4. El enlace al original se ve desde Inventario y desde Contabilidad.

### 6.5 Rechazo: casos a, b y c (US8-3; FR-066)

1. El canal rechaza una factura: estado `Rejected`, motivos traducidos en
   `/ventas/documentos-electronicos`, alerta `Dian.DocumentoRechazado`. Antes de corregir se consulta
   el estado (`documents/{id}/query-status`); un documento `Sent` sin respuesta no admite corrección
   (`ElectronicInvoicing.Document.AwaitingResponse`).
2. **Caso a** (`documents/{id}/correct`, `ElectronicInvoicing.Documents.Correct`): corregir el correo
   del comprador en el diálogo de personas; versión nueva del documento con **el mismo número**, sin
   mensajes nuevos; la copia fiscal pasa a la versión 2 con antes y después auditados. Intentar el caso
   a cambiando una cantidad responde el campo económico que cambió
   (`ElectronicInvoicing.Document.EconomicFootprintChanged`, `data.fields[]`) y remite al caso b.
3. **Caso b** (`documents/{id}/replace`): el documento del ERP se anula sin efecto fiscal (documento
   contrario con sus mensajes de anulación a existencia y Contabilidad) y un reemplazo enlazado se
   confirma con **el mismo número fiscal**.
4. **Caso c** (`documents/{id}/cancel`): anulado sin reemplazo, estado `CancelledWithoutReplacement`
   con motivo y responsable. El número no cuenta como hueco.
5. Un `Validated` no admite ninguno de los tres (`ElectronicInvoicing.Document.NotRejected`).

### 6.6 Contingencia del facturador, tipo 03 (US8-4; FR-067)

1. Canal «no disponible», o sin respuesta dentro de la espera del POS (§6.3),
   `Dian.UmbralFallasCircuito` (3) veces seguidas: se abre sola la contingencia 03 (o la declara a mano
   quien tiene `ElectronicInvoicing.Contingencies.Declare`, con motivo). Alerta
   `Dian.ContingenciaAbierta`. La constancia y las evidencias del evento se suben como adjuntos
   `DianContingencyEvent` (se leen con `ElectronicInvoicing.Contingencies.View`, no se borran).
2. Las ventas **siguen**: las nuevas toman la numeración de la resolución de contingencia que respalda
   al rol de la venta en la caja (`PosSaleContingency` o `InvoiceContingency`), se imprime
   la representación de papel sin CUFE con la leyenda de la norma, y quedan en cola visible en
   `/ventas/contingencias-dian`. Un documento ya numerado con resultado ambiguo **no** se renumera.
3. El canal vuelve: al cerrar la contingencia se transmiten en orden como tipo 03, referidos a su
   número de contingencia. Plazo = cierre + `Dian.PlazoContingenciaHoras` (48); si se acerca, alerta
   `Dian.PlazoDeContingencia` `Dian.AlertaHorasAntesDelPlazo` (6) horas antes.

### 6.7 Contingencia de la DIAN, tipo 04 (US8-5; FR-067)

El canal responde «DIAN no disponible»: el documento queda `DianContingency` con su numeración
**normal** y CUFE, se entrega en el acto marcado «pendiente de validación de la DIAN» y se transmite
cuando la DIAN vuelve. Si entonces lo rechaza, se corrige como en §6.5, y su nota en cola se transmite
referida al documento que quede validado con ese número.

### 6.8 Resoluciones por agotar, vencidas y agotadas (US8-6; FR-065)

Con una resolución de rango 1 a 10: al consumir el 9 sale `Dian.ResolucionPorAgotar`; a 30 días de su
fin, `Dian.ResolucionPorVencer`. Agotada o vencida, la venta se detiene con un mensaje claro
(`ElectronicInvoicing.Resolution.Exhausted` / `.Expired`) y alerta a quien administra resoluciones;
nunca se numera fuera de resolución. Una nota crédito sobre una factura de esa resolución sí se emite.

### 6.9 Documento soporte y su nota de ajuste (US8-7; FR-063)

Compra al proveedor B, no obligado a facturar (`/compras/documentos-soporte`,
`/api/inventory/purchases/support-documents`): se emite el documento soporte con su CUDS
(`DocumentoSoporte.Generacion = PorOperacion`) y «FacturaProveedorRegistrada». Devolverle mercancía
emite su nota de ajuste. Los artefactos, como adjuntos `ElectronicPurchaseDocument`, se leen con
`Inventory.Purchases.View`. El documento soporte sigue su propia norma: no depende de
`Dian.ObligadaAFacturar`.

### 6.10 Cambio de canal con vigencia y credenciales (US8-8; FR-064)

1. Configurar otro canal desde mañana, con una resolución cuyo prefijo esté asociado a él. Lo numerado
   desde esa fecha sale por el nuevo, sin cambiar pantallas; lo pendiente, rechazado o en contingencia
   del anterior sigue por el canal con que se numeró. Las notas nuevas salen por el vigente.
2. Retirar el canal anterior con un documento pendiente: «Transmitir por el canal vigente»
   (`documents/{id}/transmit-by-current-channel`, `ElectronicInvoicing.Documents.TransmitByCurrentChannel`)
   sólo si su resolución está asociada al canal vigente; queda auditado.
3. Cambiar a mano `CredentialKey` en la base: la emisión falla con
   `ElectronicInvoicing.CredentialMismatch`. La ruta del archivo se deriva sólo de la cooperativa
   resuelta: otra cooperativa no puede usar estas credenciales.

### 6.11 El mismo recorrido en el sandbox del proveedor (US8, prueba independiente; SC-015)

Cuando COOFLOPAL contrate el proveedor (§11), con su adaptador en lugar del `CanalSimulado` y contra el
ambiente de pruebas de la DIAN: una factura y la nota crédito que la anula (§6.2, §6.4), un documento
equivalente POS con su nota de ajuste (§6.3, §5.6), un documento soporte (§6.9), un rechazo corregido
(§6.5) y las dos contingencias (§6.6, §6.7; la 04 sólo si el proveedor permite simularla, si no queda
cubierta por el `CanalSimulado`). Medir el p95 de la emisión con 30 cajas: ≤ 5 s. SC-015: el 99 % de los
documentos validados al primer envío, ninguno entregado antes de su validación, ninguno pasado del
plazo sin alerta.

## 7. I5 e I6 (después de la salida)

**I5 · Compras completas y costeo avanzado.** Orden de 100 unidades con dos recepciones parciales (60
y 38) y factura por 100 a un precio 2 % mayor con tolerancia de 1 %: la línea queda retenida por
cantidad y por precio (`/compras/cruce`, vista `purchase-matches`); aprobada, la diferencia ajusta el
costo de lo existente y el de lo vendido con «AjusteDeCostoReconocido» dividido por documento
afectado (US13-1, US13-2). Flete de 100.000 prorrateado por valor → 60.000 y 40.000 (US13-3). Con la
factura a crédito y su recepción confirmadas, el ERP emite el 030 y el 032 por el canal de FR-064
(`POST /api/inventory/purchases/supplier-invoices/{id}/radian-events/emit`) y la alerta de §3.6 se
cierra (US13-4). PEPS con dos capas, venta de 15 → 16.500 (US16-1); compra retroactiva antes de tres
ventas: antes de confirmar se ve el impacto (`POST /api/inventory/documents/{id}/cost-impact`, que
simula sin guardar), al confirmar el kardex agrega
líneas de ajuste de costo sin tocar las ventas (US16-2); con el parámetro apagado o en período cerrado
no se admite (US16-3); cambio de promedio a PEPS con justificación y la vista `method-change-valuation`
(US16-4). Pruebas: `Inventory/Purchasing/Casos` (US13-1 a US13-3) y los casos dorados 09, 10, 11 y 16.

**I6 · Comercio ampliado y analítica.** Pedido de 4 con 10 disponibles → 4 reservadas y 6 disponibles;
vencido, la reserva se libera sola (US14-1). Remisión que descarga y reconoce el costo, factura desde
remisiones sin volver a descargar, alerta `Inventario.RemisionSinFacturar`, y el cierre de período que
las lista y exige aceptarlas con `Inventory.Periods.AcceptUnbilledShipments` (US14-2). Nota crédito
sin devolución sin efecto en existencia; promoción 3×2 como descuento no condicionado visible (US14-3,
US14-4); nota débito sobre una venta a crédito (US14-5, con la consulta a Cartera sólo cuando exista
IC). Plantilla con 2 tallas × 2 colores → 4 variantes; combo; ensamble de 5 kits; lote que vence
primero sugerido y lote vencido bloqueado; serie repetida rechazada (US15). Conteo por clase ABC.
Vistas `margin?by=`, `turnover`, `abc`, `no-movement`, `expiring`, `purchase-suggestion` (el sugerido
de 37 de §3.12), `shrinkage-cap` y el tablero `/inventario/tablero`; exportar datos de clientes sin
`Inventory.Reports.ExportPersonalData` se niega (US17-3).

## 8. IC · Crédito con Cartera (pendiente de D-02)

No se puede validar hasta que exista la especificación de Cartera. Hoy se comprueba sólo lo que deja
listo el crédito provisional: §5.11, la bandeja con los mensajes para Cartera acumulados, y que
`InventarioNoConoceContabilidadNiCartera` fija que `IConsultasDeCartera` tiene exactamente sus dos
métodos (`EstadoCrediticioAsync`, `EstadoDeValidacionAsync`) y que su implementación es
`CarteraNoHabilitada`.

Cuando llegue IC, el recorrido será:

1. Registrar el destino `Lending` y `Cartera.IntegracionHabilitadaDesde`: el despachador entrega lo
   acumulado por orden, respetando dependencias (la venta antes que su nota); Cartera deduplica por
   `MessageId`. SC-023, segunda mitad: **0 perdidos, 0 repetidos**.
2. Cartera evalúa cada venta pendiente a **su fecha**, sin contarla; las que no pasan quedan
   `ValidationFailed` con alerta `Integracion.ValidacionFallida`, y nada se anula solo.
3. US6-1 a US6-5: asociado activo con cupo de 500.000 que compra 300.000 a 6 cuotas; asociado en mora o
   bloqueado; cupo de 200.000 para una venta de 300.000, con la oferta de pagar la diferencia de
   contado; Cartera sin respuesta con cada `Cartera.PoliticaSinRespuesta` y `Cartera.ConsultaSegundos`;
   anulación y nota crédito que ajustan la obligación.

## 9. QA por rol

Con los usuarios de §2.4. Los permisos se leen por `GET /api/admin/permissions/mine` y `PermissionGate`
esconde lo que no se puede, pero la puerta es el servidor. Los perfiles son plantillas: la tabla es lo
que traen antes de editarlos.

| Acción | Admin. | Jefe | Bodeguero | Comprador | Cajero | Aprobador | Contador | Auditor |
|---|---|---|---|---|---|---|---|---|
| Ver existencias (`Inventory.Stock.View`) | sí | sí | sí | sí | sí | sí | sí | sí |
| Ver costos (`Inventory.Costs.Read`) | sí | sí | no | sí | no | no | sí | sí |
| Crear ajuste, traslado, recepción | sí | sí | sí | recepción | no | no | no | no |
| Confirmar ajuste | sí | sí | no | no | no | no | no | no |
| Aprobar un nivel | sí | nivel 1 | no | no | no | sí | no | no |
| Despachar y recibir traslado | sí | sí | sí | no | no | no | no | no |
| Abrir y aprobar conteo; capturar | sí | sí | sólo capturar | no | no | aprobar | no | no |
| Confirmar compra | sí | no | no | hasta su monto | no | no | no | no |
| Vender en POS, abrir y cerrar su sesión | sí | no | no | no | sí | no | no | no |
| Descuento sobre su tope, diferencia de arqueo | sí | no | no | no | no | sí | no | no |
| Cerrar y reabrir período | sí | sí | no | no | no | no | no | no |
| Parámetros, políticas, alcances | sí | no | no | no | no | no | no | no |
| Reprocesar y enviar «no aplica» | sí | no | no | no | no | no | no | no |
| Exportar informes | sí | sí | no | no | no | no | sí | no |
| Verificar integridad del kardex / de la auditoría | sí / no | sí / no | no | no | no | no | no | sí / sí |

La matriz, el lote manual y la completitud son de Contabilidad (`Accounting.InventoryRules.*`,
`Accounting.InventoryBatches.*`); la configuración DIAN, de `ElectronicInvoicing.*`. A los roles
integrados no se les agrega escritura de inventario: `Operator`, `ReadOnly` y `Auditor` reciben sólo
lo que alcanza el patrón `*.View`, y por eso las lecturas sensibles usan otra acción (`Costs.Read`,
`CashSessions.ViewAll`, `Reports.ExportPersonalData`).

Además:

1. **Segregación**: quien crea nunca aprueba, y esto no es parametrizable; tampoco quien abrió o contó
   un conteo, el cajero en su diferencia o en su crédito, ni quien ya aprobó otro nivel. Se compara el
   `SEC_Users.Id` de `IActorActual`, nunca el entero del token ni el correo
   (`LaSegregacionNoUsaUserIdDelToken`).
2. **Por `curl`**: toda acción que la tabla dice «no» responde 404 `Generic.NotFound`, también fuera de
   alcance. Sólo los permisos que dependen del cuerpo sobre un recurso que ya se ve (costo indicado,
   aceptar la diferencia de activación, remisiones sin facturar, tipo fiscal sin paso, clave de
   parámetro) responden 422 con código propio (pregunta C10 de `research.md`).
3. **Cambio de cooperativa** en la misma sesión: los botones se recalculan sin F5 y no aparece un solo
   documento, mensaje o documento electrónico de otra cooperativa (base distinta).
4. **App** (FR-094): el POS y las pantallas de Inventario en la app MAUI en Windows y Android, con
   impresión nativa (`IImpresionDeDocumentos`). La aprobación presencial con llave en la app se prueba
   a mano: no hay pruebas de navegador en el repositorio.
5. **Indicador de carga**: toda grilla, formulario y panel nuevo muestra «Cargando…» o «Guardando…»
   por zona, sin colores literales ni `<style>`.

## 10. Salida de COOFLOPAL, bodega por bodega

Sólo con autorización expresa del dueño y en las fechas que él defina, con evidencia de cada paso en
las notas de release.

1. **Antes de desplegar I1**: el diagnóstico de §2.1 en PDN; si alguna base tiene filas en las tablas
   heredadas, `pg_dump -Fc`, segundo revisor nombrado y la fila de aprobación en esa base. Verificar el
   usuario de Mongo de la API y que la clave de `AuditSignature` de producción no sea la de desarrollo.
2. **Migraciones** por el Job PreSync de Argo, base por base; `AutoMigrate` sigue apagado en
   producción. Comprobar tablas y permisos contra la base, no contra el repositorio.
3. **Facturación electrónica**: el Secret `erp-fe-credenciales` con la clave de COOFLOPAL (el guion
   exige escribir `PRODUCCION`) y su volumen en GitOps; resoluciones reales, incluida la de
   contingencia; `readiness` vacío en ambiente de producción.
4. **Datos**: las plantillas diligenciadas por COOFLOPAL, la matriz de su contadora con la completitud
   vacía, usuarios y alcances.
5. **Marcha paralela en la cooperativa de ensayo** (SC-018): cada diferencia de kardex y valorizado
   contra SOLIDO con su causa documentada y aprobada por el jefe de inventario antes de salir.
6. **Por bodega**: conteo físico, saldo inicial fechado en su corte, activación con cuadre (§4.2). La
   segunda bodega y las siguientes entran con la excepción de puesta en marcha (§4.2 paso 5, pregunta
   D8). Hasta activarse, SOLIDO sigue registrando sus ventas; no hay sincronización automática.

## 11. Lo que el dueño y COOFLOPAL deben aportar

Sin cada ítem, el paso indicado no se puede probar ni promover.

| # | Qué | Para |
|---|---|---|
| 1 | Plantillas de parametrización diligenciadas (D-07), con el largo de los códigos confirmado (D1) | §2.5, §10.4 |
| 2 | Validación de la contadora de la semilla tributaria, los tipos de comprobante y cruces, y los códigos DIAN por medio de pago (A8); los impuestos que vende COOFLOPAL (A7); la traducción de `TaxRegime`, `SourceWithholding` e `IcaType` de SOLIDO (E3) | §2.2, §2.5, §5.1 |
| 3 | La matriz contable diligenciada con la contadora y el modo de paso de cada tipo (D-07) | §4.1, §2.6 |
| 4 | Cifras de SOLIDO para el ensayo y la marcha paralela, y el conteo físico antes de cada carga (D-07) | §3.5, §4.2, §10.5 |
| 5 | Proveedor tecnológico contratado, con credenciales por empresa y sandbox (A3) | §6.11, §10.3 |
| 6 | Resoluciones y prefijos de COOFLOPAL, software asociado, resolución de contingencia y numeración del documento equivalente POS (A4) | §6.1, §10.3 |
| 7 | Buzón y dominio del correo al comprador (A5) e impresión de las cajas (A6) | §6.2, §5.2 |
| 8 | Aprobación del retiro por base y segundo revisor si hay filas heredadas (A1); verificación de Mongo y de `AuditSignature` (A2) | §2.1, §10.1 |
| 9 | Las fechas del ensayo, del conteo, de la carga y de la salida (Q1) | §10 |
| 10 | La especificación de Cartera (D-02) | §8 |
| 11 | La confirmación de la excepción de puesta en marcha y de los ajustes de conteo frente a `Costeo.RetroactivosPermitidos` (D8, D9), y de la respuesta 422 para los permisos que dependen del cuerpo (C10) | §3.10, §4.2, §9, §10.6 |

## 12. Qué prueba cada criterio

| SC | Automática | En el ensayo |
|---|---|---|
| SC-001 | `ConcurrenciaDeExistenciasTests` (1.000 repeticiones con `RUN_PERF_TESTS=1`) | §5.3 |
| SC-002 | `IdempotenciaDeOperacionesTests`, `EntregaGarantizadaTests`, `BonoUnicoConcurrenteTests` | §3.7, §4.5, §4.7 |
| SC-003 | `DocumentosElectronicosTests`, `ContabilizacionPorMensajesTests` | §6.4 |
| SC-004 | `VentaPosCompletaTests` | §5.2, §6.3 (incluido el paso 3), §6.6 |
| SC-005 | `ContabilizacionPorMensajesTests` | §4.10 |
| SC-006 | `PropiedadesDelKardexTests`; verificación nocturna del `ProgramadorDeTareas` | §3.7 |
| SC-007 | casos dorados de costeo (§1.1) | §3.8 |
| SC-008 | — | §5.2 |
| SC-009 | `BusquedaDeProductos50kTests` | §3.3 |
| SC-010 | `EntregaGarantizadaTests` | §4.9 |
| SC-011 | `ContabilizacionPorMensajesTests` | §4.3 |
| SC-012 | `IntegridadDeAuditoriaTests`, `ElProcesoEnSegundoPlanAuditaEnSuCooperativaTests` | §3.13 |
| SC-013 | `LosHechosInmutablesNoSeModifican`, `PrincipioXI_ContableImmutable` | §3.7, §3.13 |
| SC-014 | `LosEndpointsProtegidosExigenPermiso`, `LasConsultasDeInventarioRespetanElAlcance`, `AlcancePorBodegaTests` | §3.13, §9 |
| SC-015 | `DocumentosElectronicosTests` | §6.2, §6.11 |
| SC-016 | `SaldoInicialYActivacionTests`; caso dorado 17 | §3.5, §4.2 (incluido el paso 5) |
| SC-017 | — | §3.14 (medición en QA) |
| SC-018 | — | §10.5 |
| SC-019 | — | §5.2 (medición en QA) |
| SC-020 | `LotesProgramadosTests` con `RUN_PERF_TESTS=1` | §4.5 |
| SC-021 | `ContabilizacionPorMensajesTests` (`PrevalidationOutcome`) | §4.4 |
| SC-022 | casos de alertas en Application | §3.12 |
| SC-023 | `CreditoProvisionalTests` (primera mitad) | §5.11, §8 |
| SC-024 | `VentaPosCompletaTests` | §5.4 |
| SC-025 | casos dorados de caja (§1.1) | §5.9 |

Historias: US1 §3.2–§3.4 · US2 §3.7, §3.13, §5.3 · US3 §3.8, §3.11, §5.6 · US4 §3.5, §4.2 · US5 §5.2–§5.10
· US6 §5.11, §8 · US7 §4 · US8 §6 · US9 §3.6 · US10 §3.9 · US11 §3.10 · US12 §3.13, §9 · US13 y US16 §7
(I5) · US14, US15 y US17 §7 (I6) y §3.12.
