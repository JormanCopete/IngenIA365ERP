# Runbook: desplegar captaciones y aportes (feature 014) en DEV, QA y producción

> Feature 014 (ahorros, aportes y CDAT dentro de la Cartera), entregas K7 a K11. Para quien despliega y para quien autoriza la promoción.
> Molde: [cartera-despliegue-k4.md](cartera-despliegue-k4.md). Lo que hay que hacer **después** del despliegue en cada cooperativa (líneas,
> carga, cuadre, activación por naturaleza) está en [captaciones-puesta-en-marcha.md](captaciones-puesta-en-marcha.md); el retiro de lo
> heredado, en [captaciones-retiro-heredado.md](captaciones-retiro-heredado.md). Base: [research.md](../../specs/014-ahorros-aportes-cdt/research.md)
> §5 (E06, E10, E11, E15), §7 (E30–E34), §13 (D14, D15, D18) y el cierre de T225.

## 0. Qué se despliega y qué no

La 014 sale **entera**: las cinco entregas de captaciones se abren **juntas** con `CatalogoDeCartera.EntregaVigente = K11` (decisión Q1 del
dueño: ninguna entrega de captaciones sale sola, y **nada de captaciones sale en vivo a producción sin Recaudo y Tesorería reales**).

| Entrega | Qué trae | Número en `EntregaDeCartera` |
|---|---|---|
| K7 | aportes, traslados, retiro del asociado | 8 |
| K8 | ahorro a la vista: consignación, retiro, causación, liquidación, retención, GMF, cargos, embargos | 9 |
| K9 | CDAT y ahorro contractual, tasa negociada | 10 |
| K10 | asamblea (revalorización, excedentes, amortización), inactividad, no reclamados, fallecimiento | 11 |
| K11 | fondo de liquidez, depósitos asegurados, informes y reportes regulatorios | 12 |

Lo que el despliegue **no** hace: activar aportes ni captaciones en ninguna cooperativa. Eso es un acto de cada cooperativa, con sus
requisitos, en `/cartera/captaciones/activacion` ([captaciones-puesta-en-marcha.md](captaciones-puesta-en-marcha.md)). Mientras una naturaleza
no se active, el contrato de cuentas y la consulta de aportes responden «no habilitado» y el paso de captaciones del ciclo diario no recorre nada.

Requisito previo: la **013 desplegada** en el ambiente (todas sus migraciones, hasta `CarteraVivienda`) o desplegada en la misma promoción,
antes que éstas.

## 1. Antes de desplegar

1. **Diagnóstico** de las 45 tablas heredadas en cada base de cooperativa y en `ingenia365erp`
   ([diagnostico-captaciones-heredadas.sql](../../specs/014-ahorros-aportes-cdt/diagnostico-captaciones-heredadas.sql);
   [captaciones-retiro-heredado.md](captaciones-retiro-heredado.md) §3). Anotar el resultado en la tabla §9 de ese runbook.
2. **Respaldo de cada base** (la administrativa y la de cada cooperativa) **inmediatamente antes**, aunque la segunda migración sea aditiva
   (Principio XII): `pg_dump -Fc` a `/root/respaldos/<base>-AAAAMMDD-pre-f014.dump`, con su tamaño y la cabecera `PGDMP` anotados.
3. **Aprobación y segundo revisor** (decisión del dueño del 2026-10-18, D15): no hay clientes en producción y el **dueño autoriza** el retiro en
   todas las bases y **es el segundo revisor**. Su nombre, la referencia del respaldo y el diagnóstico van en las líneas `Diagnóstico:`,
   `Respaldo:` y `Segundo revisor:` de la cabecera `MIGRACION-DESTRUCTIVA-APROBADA` de los dos archivos de `RetiroDeCaptacionesHeredadas`, en un
   commit que **sólo toca esos comentarios** (la huella de la prueba de congelamiento no los incluye: ver §2.3 antes de hacerlo).
4. **La fila de la guarda en cada base**, antes de que el DbMigrator aplique el retiro: `COR_SystemSettings`
   `LND.CaptacionesHeredadas.RetiroAprobado` con el texto de D15 ([captaciones-retiro-heredado.md](captaciones-retiro-heredado.md) §5 y §9a). Con
   todas las tablas vacías la guarda pasa sola, pero D15 pide la fila en todas las bases para esta promoción.
5. Para producción: **autorización expresa del dueño** para la fecha, y Recaudo y Tesorería reales en el plan (sin ellos se puede desplegar, pero
   ninguna cooperativa podrá activar: `AccountsCollectorRegistered` y `TreasuryDestinationRegistered` faltan sin dobles de prueba).

## 2. Las migraciones, en orden

Dos migraciones pares (PostgreSQL / SQL Server), detrás de las de la 013:

| # | Migración | PostgreSQL | SQL Server | Tipo |
|---|---|---|---|---|
| — | las de la 013, hasta `CarteraVivienda` | `20261017062235` | `20261017062205` | ya congeladas |
| 1 | `RetiroDeCaptacionesHeredadas` | `20261018143710` | `20261018143627` | **destructiva con guarda** |
| 2 | `CaptacionesAportes` | `20261019104128` | `20261019104056` | aditiva |

Se aplican como toda migración del clúster: el **Job PreSync del DbMigrator**, base por base. Si la guarda del retiro se niega en una base
(`RetiroDeCaptacionesHeredadas: las captaciones heredadas tienen filas: …`; en SQL Server, `THROW 50014`), el Job falla en ella y la
sincronización no avanza: se resuelve con [captaciones-retiro-heredado.md](captaciones-retiro-heredado.md) §10 y se vuelve a sincronizar. En local,
`AutoMigrate` las aplica al arrancar.

### 2.1 `RetiroDeCaptacionesHeredadas`

Borra las 45 tablas heredadas (38 `LND_` que la 013 había conservado y 7 `CDT_`) después de la guarda. `Down()` las recrea **vacías**: en
datos es irreversible, lo que había vuelve sólo del respaldo. Detalle completo en [captaciones-retiro-heredado.md](captaciones-retiro-heredado.md) §2.

### 2.2 `CaptacionesAportes`

**Una sola migración par** para toda la feature. El plan preveía cinco (`CaptacionesAportes`, `CaptacionesAhorro`, `CaptacionesCdat`,
`CaptacionesAsamblea`, `CaptacionesLiquidez`); por decisión del dueño, mientras fue regenerable todo cambio de esquema de la 014 entró en ésta
(research E35) y **las otras cuatro no existen**. Trae:

- **53 tablas nuevas**: las líneas de captación y de aportes con sus versiones, tramos y cargos; las cuentas, sus partes, versiones de
  condiciones, controles, posiciones diarias, operaciones y transacciones (el libro); cuotas, débitos automáticos, exenciones y uso del GMF,
  liquidaciones de intereses y acumulados de retención; tasas negociadas; órdenes de giro de cuentas; retiro del asociado; embargos y
  bloqueos; asamblea; fallecimiento y no reclamados; fondo de liquidez; las tablas de preparación de la migración; los grupos de archivo y
  resultados de pagadurías; `PAY_PayrollAccountDeductions` (descuentos de cuentas en Nómina) y `COR_AssociateInboundMessages` (lo que recibe
  Asociados).
- **Columnas nuevas** en tablas existentes: las cinco dimensiones de captaciones en `ACC_LendingPostingRules`; seis fechas e identificadores de
  activación por naturaleza en `LND_Setup`; la naturaleza en `LND_CreditLines` y `LND_MigrationLoads` (más las cifras de referencia); cinco campos
  del perfil regulatorio en `COR_RegulatoryProfileVersions`; y otras en `LND_Activations`, `LND_AccountingFactParts`, `LND_PayrollAgreementVersions`,
  `LND_PayrollDeductionFiles`/`FileLines`, `LND_RegulatoryFiles`, `LND_IssuedDocument*`, `LND_ProcessRunErrors`, `LND_AttributionRules` y
  `PAY_Novelties`. `LND_Activations.MigrationLoadId` y `SessionItemId` pasan a nulos (la activación sin carga).
- **SQL escrito a mano** al final de `Up()` (y su inverso al principio de `Down()`), en los dos motores e idempotente:
  1. toda regla existente de `ACC_LendingPostingRules` recalcula su `DimensionKey` con **`|*|*|*|*|*`** al final (sólo las claves de 14
     posiciones; repetirlo no hace nada): ninguna regla de crédito cambia de resolución;
  2. el participante **`NOM`** (Nómina) adopta **`ProveedorDeCuentas`** v1, si existe vigente y no la tiene;
  3. el participante **`ASO`** (destino `Associates`) se registra si no existe y adopta `RetiroDeAsociadoAprobado` v1.

### 2.3 Congeladas desde el 2026-10-19 (T225)

`RetiroDeCaptacionesHeredadas` y `CaptacionesAportes` quedaron **congeladas** al cierre de la feature: `has-pending-model-changes` limpio en
los dos motores y paridad en par. La prueba de arquitectura `LasMigracionesCongeladasNoSeRegeneran` fija la **huella SHA-256** del `.cs` y del
`.Designer.cs` de cada migración de la 013 y de la 014 (desde `PlataformaParaCartera` hasta `CaptacionesAportes`, en los dos motores; texto
sin BOM, con saltos LF y sin blancos al final). Desde aquí **todo cambio de esquema es una migración nueva**; regenerar una de éstas o tocar su
SQL escrito a mano cambia la huella y la prueba falla.

**Cuidado con la cabecera del retiro** (paso 3 de §1): la huella cubre el archivo entero, comentarios incluidos. Escribir el nombre del
segundo revisor y la referencia del respaldo en la cabecera **cambia la huella**: ese commit tiene que actualizar a propósito la huella de los
dos archivos en `LasMigracionesCongeladasNoSeRegeneran`, con su anotación (la prueba lo admite «sólo antes de promoverla fuera de local»).
Hacerlo antes de la primera promoción, no después.

## 3. `EntregaVigente = K11`

Está en el código (`CatalogoDeCartera.EntregaVigente`), no es configuración: llega con la imagen. Al subir de K6b a K11:

- se **ofrecen y se aceptan** las claves de parámetros de captaciones (`/cartera/parametros`), que hasta entonces se leían con sus defectos;
- la semilla central inserta los valores nacionales que estaban **en espera** (§5);
- se pueden crear y activar líneas de aportes (desde K7) y de captación (desde K8): antes respondían `Lending.Savings.DeliveryNotAvailable`
  con `availableFrom`;
- se emiten las clases de documentos al titular de captaciones;
- corren los subpasos de captaciones del ciclo diario y el corte del fondo de liquidez (§6);
- el requisito `DeliveriesComplete` de la activación se cumple.

## 4. Permisos y roles

- **Los permisos `Lending.*` los siembra la API al arrancar**, en cada cooperativa, no el DbMigrator (`LendingPermissionCatalogSeeder`, desde
  `PhaseZeroSecuritySeeder`; idempotente, pone al día las descripciones). Al cierre de la 014 son **163** códigos `Lending.*` (los de la 013 más
  los de captaciones: `Lending.DepositLines.*`, `Lending.ContributionLines.*`, `Lending.Accounts.*`, `Lending.TermDeposits.*`,
  `Lending.NegotiatedRates.*`, `Lending.Contributions.*`, `Lending.Garnishments.*`, `Lending.Holds.*`, `Lending.MembershipWithdrawals.*`,
  `Lending.Assembly.*`, `Lending.DeceasedCases.*`, `Lending.Unclaimed.*`, `Lending.LiquidityFund.*`, `Lending.Deposits.Migration`,
  `Lending.Deposits.Activate`…).
- El administrador de la empresa (`CompanyAdmin`) **no** recibe `Lending.NegotiatedRates.Approve` (D17), como no recibe las decisiones ni el voto.
- Los **roles sugeridos** de captaciones (cuatro plantillas, D18) **no se crean solos**: los crea cada cooperativa antes de activar
  ([captaciones-puesta-en-marcha.md](captaciones-puesta-en-marcha.md) §4). Los roles que una cooperativa ya creó no se actualizan solos.

## 5. Semillas

Las semillas paramétricas las corre la API al arrancar, en cada cooperativa y en cada arranque (idempotentes; también
`DbMigrator seed --category parametric`). Lo que la 014 sumó o cambió:

| Semilla (Order) | Qué hace en la 014 |
|---|---|
| `PlatformNationalValuesSeeder` (6, base administrativa) | registra `COMPONENTE_INFLACIONARIO` 2025 = 0,5543 y `TOPE_SEGURO_DEPOSITOS` = 50.000.000 (desde 2024-08-19), y los `LIMITE` de la 014; un código de una entrega posterior a la vigente queda **en espera** sin error (con K11, ya no espera ninguno) |
| `VoucherTypesSeeder` (60) | los cinco tipos `MA`, `CC`, `TD`, `AO`, `RA` (29 de Cartera); `AH` y `CD` nacen inactivos y, si una cooperativa los tenía activos **sin comprobantes**, los inactiva; con comprobantes los deja y lo registra (`[Semilla.TipoDeComprobanteRetiradoEnUso]`) |
| `CrossDocumentTypesSeeder` (61) | el tipo de documento cruce `CTA` «Cuenta de captación o aportes» |
| `PayrollConceptDefinitionsSeeder` (70) | los conceptos `DESC_APORTES` y `DESC_AHORROS` (sin cuentas: las pone la cooperativa) |
| `NationalValuesReplicaSeeder` (88) | replica a `COR_` de cada cooperativa los valores nacionales nuevos |
| `LendingVoucherMappingsSeeder` (89) | el mapeo hecho → tipo de comprobante de los 30 hechos de negocio, con cruce `CTA`, y las dos excepciones de Nómina (`DN`): 66 filas en total |
| `LendingFactTypesSeeder` (90) | los 33 tipos de hecho de captaciones en `LND_FactTypes` (70 en total) |
| `LendingMessageTemplatesSeeder` (91) | las plantillas de mensaje `CAMBIOTASA`, `MAYOREDAD`, `CONTRATINC`, `PREINACTIV`, `INACTIVA` y `NORECLAMA` por correo, SMS y WhatsApp, «pendientes de validar» |

**No se siembra ninguna regla de la matriz contable**: la plantilla de la matriz trae filas sugeridas sin cuenta
([manual contable §10.7](../manual/contabilidad-contrato-de-contabilizacion.md)).

### Valores nacionales que registra el maestro (sin semilla)

Con su fuente, en `/admin/valores-nacionales` ([valores-nacionales.md](valores-nacionales.md)), antes de que una cooperativa los necesite:
`LIMITE_INEMBARGABLE` (embargos sobre depósitos, P5), `TOPE_ENTREGA_SIN_SUCESION` (entrega por fallecimiento, P6), `LIMITE`
`APORTES_LIMITE_PARTICIPACION_PN` y `_PJ` (P12), el `COMPONENTE_INFLACIONARIO` de cada año gravable nuevo y el `IPC` (revalorización de aportes).
Todos los sembrados van «pendientes de validar» (T235).

## 6. El ciclo diario y las tareas

No hay tareas programadas nuevas en el programador: todo lo diario de captaciones es el **paso de captaciones** del ciclo diario de la Cartera
(`PasosDelCiclo.Captaciones = 9`), que corre al terminar la corrida `DailyCycle`, **después de la causación de los créditos**, y sólo en
cooperativas con aportes o captaciones activados con fecha ≤ el día. Recorre las cuentas vivas en tandas de `Lending:Process:BatchSize` y pasa
cada tanda por sus subpasos: mayoría de edad, cuotas de aportes y del permanente, marcas que rigen, inactividad, no reclamados, cargos
periódicos, débito automático, embargos, causación, liquidación periódica, ciclo del CDAT y ciclo del contractual. Al terminar el día corre el
**corte del fondo de liquidez** (foto y riesgo de liquidez en los días de corte). Lo que falla queda en los errores de la corrida con el número
de la cuenta, sin detener el resto.

Hace falta lo mismo que para los créditos: el procesador de Cartera encendido (`Lending:Process:Enabled`) con su arrendamiento
([cartera-proceso-diario.md](cartera-proceso-diario.md)). La causación de captaciones va a Contabilidad **por lotes resumida** (un `CC` por día)
al terminar la causación.

## 7. Verificaciones después del despliegue

En cada base de cooperativa (PostgreSQL; en SQL Server, con corchetes y `= 0` en lugar de `NOT`):

```sql
-- las dos migraciones aplicadas, en orden
SELECT "MigrationId" FROM dbo."__EFMigrationsHistory"
 WHERE "MigrationId" LIKE '%RetiroDeCaptacionesHeredadas' OR "MigrationId" LIKE '%CaptacionesAportes' ORDER BY "MigrationId";

-- lo heredado ya no está; lo nuevo sí
SELECT table_name FROM information_schema.tables
 WHERE table_schema = 'dbo' AND table_name IN ('LND_SavingsAccounts', 'CDT_Certificates', 'LND_MemberAccounts', 'LND_DepositLineVersions')
 ORDER BY table_name;                                                    -- sólo LND_DepositLineVersions y LND_MemberAccounts

-- las reglas existentes con la clave de 19 posiciones
SELECT COUNT(*) FROM dbo."ACC_LendingPostingRules"
 WHERE length("DimensionKey") - length(replace("DimensionKey", '|', '')) <> 18;                     -- 0

-- Nómina adopta el contrato de cuentas y Asociados recibe el retiro
SELECT p."Code", a."ContractName", a."Version" FROM dbo."COR_IntegrationParticipants" p
  JOIN dbo."COR_IntegrationParticipantAdoptions" a ON a."ParticipantId" = p."Id"
 WHERE a."ContractName" IN ('ProveedorDeCuentas', 'RetiroDeAsociadoAprobado') AND NOT a."IsDeleted";

-- semillas
SELECT "Code", "IsActive" FROM dbo."ACC_VoucherTypes" WHERE "Code" IN ('MA', 'CC', 'TD', 'AO', 'RA', 'AH', 'CD');
SELECT COUNT(*) FROM dbo."LND_FactTypes";                                                          -- 70
SELECT "Code" FROM dbo."ACC_CrossDocumentTypes" WHERE "Code" = 'CTA';

-- ninguna naturaleza de captación activada por el despliegue
SELECT "ActivationDate", "ContributionsActivationDate", "DepositsActivationDate" FROM dbo."LND_Setup";
```

Y además:

- `/health/ready` sano; la API arrancó sin errores de semilla (buscar `[Semilla.` en el log).
- Con un usuario de la cooperativa: el grupo de menú «Captaciones y aportes» aparece; `/cartera/lineas?naturaleza=aportes` permite crear una
  línea (no responde `DeliveryNotAvailable`); `/cartera/captaciones/activacion` muestra `DeliveriesComplete` **cumplido**.
- En la base administrativa: `COMPONENTE_INFLACIONARIO` y `TOPE_SEGURO_DEPOSITOS` registrados, y la réplica en cada cooperativa al día.
- Si el NOM no existía en la base cuando corrió la migración, su adopción de `ProveedorDeCuentas` no se hizo: registrarla en
  `/admin/participantes-de-integracion` antes de que Nómina consigne cuotas de aportes o ahorros.

## 8. Vuelta atrás

- **Antes de que una cooperativa active aportes o captaciones**: restaurar el **respaldo** de cada base (`<base>-AAAAMMDD-pre-f014.dump`) y volver
  a la imagen anterior. Es la vuelta atrás normal: el retiro no devuelve datos.
- **`Down()` de las migraciones**, sólo si no hay respaldo útil y la base no operó captaciones: bajar primero `CaptacionesAportes` (borra sus 53
  tablas y columnas, quita el sufijo `|*|*|*|*|*` de las claves de la matriz, retira las adopciones y el `ASO` que sembró) y después
  `RetiroDeCaptacionesHeredadas` (recrea **vacías** las 45 tablas). La imagen anterior **no** debe arrancar con `EntregaVigente = K11` sobre una
  base sin `CaptacionesAportes`.
- **Después de operar** (cuentas activadas, movimientos, hechos contabilizados): volver atrás es perder lo operado. No se hace sin decisión del
  dueño; se corrige hacia adelante con una migración nueva.

## 9. Estado por ambiente

| Ambiente | Diagnóstico | Respaldo | Fila de la guarda | Migraciones | Imagen / release | Verificado (§7) |
|---|---|---|---|---|---|---|
| Local (PostgreSQL) | 0 filas (2026-10-18) | — | no necesaria | aplicadas por `AutoMigrate` | rama `014-ahorros-aportes-cdt` | pendiente |
| DEV | pendiente | pendiente | pendiente | pendiente | — | pendiente |
| QA | pendiente | pendiente | pendiente | pendiente | — | pendiente |
| Producción | pendiente (con autorización expresa) | pendiente | pendiente | pendiente | — | pendiente |

La promoción a DEV y QA es T236 (dueño). El estado se anota también en [estado-y-pendientes.md](estado-y-pendientes.md) (P20).
