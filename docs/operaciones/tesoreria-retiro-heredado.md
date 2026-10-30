# Tesorería (feature 015): retirar la Tesorería anterior

> Qué hace la migración destructiva `RetiroDeLaTesoreriaHeredada`, qué hay que tener antes de aplicarla en una base, cómo se
> exporta y se carga lo pendiente, cómo se comprueba y por qué no se vuelve atrás. Es la migración destructiva de la feature
> (FR-161; data-model §24; C3 = A; Principio XII). Sigue el molde de [captaciones-retiro-heredado.md](captaciones-retiro-heredado.md)
> (014), [cartera-retiro-heredado.md](cartera-retiro-heredado.md) (013) e [inventario-retiro-heredado.md](inventario-retiro-heredado.md)
> (012), con una diferencia: **esta es irreversible** —su `Down()` lanza— y su cabecera llega **sin aprobar**.

**Estado (2026-10-28)**: el código y la migración están construidos en la rama `015-recaudo-tesoreria` y **no se han aplicado
en ningún ambiente**. El dueño corrió el diagnóstico (0 filas en todas las bases), la exportación y la carga no aplican, y **aprobó
por escrito como segundo revisor el 2026-10-28**; la cabecera de los dos motores está completa y el par congelado. Lo único que
falta es el **respaldo `-pre-f015.dump` de cada base antes de aplicar** (§6, §9).

## 1. Qué se retira

Las tres tablas del módulo de Tesorería anterior, que convivieron sin cambios con la Tesorería nueva hasta T2:

| Tabla | Entidad | Qué guardaba |
|---|---|---|
| `TRS_Checks` | `Check` | cheques girados: concepto, banco, número, valor, estado (`E` girado, `C` cobrado, `A` anulado, `D` devuelto) |
| `TRS_Concepts` | `TreasuryConcept` | conceptos de tesorería con su cuenta débito y crédito (feature 009, R16) |
| `TRS_Invoices` | `TreasuryInvoice` | facturas por pagar y por cobrar, con su estado (`P` pendiente) |

Con ellas sale todo su código, **sin alias**: las tres entidades de la raíz de `Domain/Entities/Treasury`,
`Application/Treasury/{Checks, Invoices, TreasuryConcepts}`, sus tres configuraciones, las rutas
`/api/treasury/{checks, concepts, invoices, cash-flow}` (responden 404), las pantallas `/tesoreria/{cheques, conceptos, facturas,
flujo-caja}`, el grupo de menú «Tesorería · heredada», sus cuatro temas del manual (reemplazados por los de las pantallas nuevas),
la rama «Conceptos de tesorería» de «dónde se usa una cuenta contable» y las marcas `E3 (feature 009)` que quedaban en esos
handlers. También la exportación para archivo (§4), que lee esas tablas y por eso se va con ellas.

Sus reemplazos ya están en la Tesorería nueva:

| Lo anterior | Lo nuevo |
|---|---|
| Cheques | `/tesoreria/chequeras` y `/tesoreria/cheques-girados` (T0) |
| Facturas por pagar | `/tesoreria/cuentas-por-pagar` (T2): las facturas de proveedor de Comercial y las cuentas por pagar contables |
| Flujo de caja | `/tesoreria/flujo-de-caja` (T2, `/api/treasury/cash-flow-projections`) |
| Conceptos de tesorería | los conceptos varios de Recaudo (FR-027) |

**No se toca** lo heredado de tarjeta débito y convenios con redes (`Domain/Entities/Debit`, `/tarjeta-debito/*`): rige D32
mientras el dueño no decida P9 (T0468).

## 2. El orden: dos versiones, no una

La migración se aplica sola al desplegar (Job PreSync del DbMigrator, o `AutoMigrate` en local), así que la versión que trae el
retiro **no puede** ser la que exporta y carga: para entonces las tablas ya no estarían. Por eso hay dos commits:

1. **La versión anterior al retiro** (commit `feat(015): fase 46, diagnóstico, exportación y carga…`): trae el diagnóstico, la
   exportación para archivo y la plantilla `tesoreria.pendientes-heredados`, **sin borrar nada**. Se despliega primero.
2. **La versión del retiro** (commit propio `feat(015)!: fase 46, retiro de la Tesorería anterior…`): la migración, el borrado del
   código y sus pruebas. Se despliega después, cuando el dueño aprobó cada base (§7).

## 3. Diagnóstico

Por **cada base de cooperativa** (y la base por defecto `ingenia365erp`; la administrativa no tiene ninguna de las tres), en
**sólo lectura**, antes de cargar y otra vez el mismo día de aplicar el retiro:

```sql
-- en ingenia365erp_admin: las bases de cooperativa
SELECT "Name", "DatabaseName" FROM dbo."ADM_Tenants" WHERE NOT "IsDeleted";
```

```bash
k3s kubectl -n <ns> exec -i erp-db-1 -c postgres -- psql -U postgres -d <base> -f - < specs/015-recaudo-tesoreria/diagnostico-tesoreria-heredada.sql
```

[specs/015-recaudo-tesoreria/diagnostico-tesoreria-heredada.sql](../../specs/015-recaudo-tesoreria/diagnostico-tesoreria-heredada.sql)
trae las dos partes (PostgreSQL y SQL Server; se corre la que toque) y responde cuatro cosas: las filas de cada tabla —incluidas las
dadas de baja—, los pendientes (cheques `E` sin cobrar y facturas `P`) con cuántos **faltan por cargar** a la Tesorería nueva, el
detalle de esos pendientes (es lo que va a la plantilla) y si la base ya tiene la aprobación.

| Resultado | Decisión |
|---|---|
| Todo en 0 | la guarda pasa sola; no se exporta, no se carga ni se siembra nada |
| Filas, sin pendientes | se exporta para archivo (§4) y decide el dueño (§7) |
| Pendientes sin cargar | se exporta, se cargan (§5) y se vuelve a diagnosticar hasta que no falte ninguno; después decide el dueño |

El resultado se anota en la tabla de §9 y en la línea `Diagnóstico:` de la cabecera de los dos archivos de la migración.

## 4. Exportación para archivo (versión anterior al retiro)

Toda fila existente de las tres tablas, también las dadas de baja, queda guardada antes de borrar (data-model §24 paso 2):

```http
GET  /api/treasury/legacy-export/tables     (Treasury.Imports.Run)            → las tres tablas, para revisarlas
POST /api/treasury/legacy-export            (Treasury.Imports.Run, Idempotency-Key)
```

El `POST` arma un **xlsx y un csv por tabla** (seis archivos; el csv en UTF-8 con BOM, separado por `;`, números y fechas en
formato invariable) y los guarda como adjuntos **no borrables** `TreasuryLegacyExport` de la configuración de Tesorería, con su
**SHA-256**; responde la lista (`fileName`, `attachmentPublicId`, `sha256`, `bytes`) y las filas por tabla, y deja el evento
`Treasury.LegacyExport.Archived` en la auditoría con lo mismo. Se anotan las huellas en §9. Los adjuntos sobreviven al retiro: se
leen con `Treasury.Imports.Run`.

## 5. Carga de los pendientes (versión anterior al retiro)

Plantilla `tesoreria.pendientes-heredados` en la puesta en marcha de Tesorería
(`GET/POST /api/treasury/imports/tesoreria.pendientes-heredados/{template.xlsx|import?mode=review|apply}`, `Treasury.Imports.Run`),
todo o nada como toda plantilla. Una fila por pendiente:

| Columna | Cheque sin cobrar | Factura por pagar |
|---|---|---|
| `clase` | `ChequeSinCobrar` | `FacturaPorPagar` |
| `referencia` | el `Id` de la fila en la exportación (no se repite por clase) | ídem |
| `cuenta`, `numero`, `entregado` | la cuenta bancaria propia, el número —dentro de una chequera registrada de esa cuenta— y si ya se entregó | — |
| `beneficiario` | documento de la persona | documento del proveedor |
| `valor`, `fecha` | el valor y la fecha de giro | el saldo por pagar y el vencimiento |
| `factura` | — | el **documento cruce** de su cuenta por pagar contable |

Qué nace:
- **Cheque sin cobrar** → un cheque girado en tránsito de su chequera (`IsOpeningLoad`, como los de `tesoreria.cheques-en-transito`),
  que la conciliación cruzará con el extracto cuando el banco lo pague.
- **Factura por pagar** → una orden de la bandeja contra la cuenta por pagar contable del proveedor con ese documento cruce, por el
  mismo camino de `POST /api/treasury/payables/orders`: si Contabilidad no la tiene (la dejan los saldos de apertura) o lo pendiente
  menos lo que ya está en órdenes no alcanza, la fila se rechaza (`Treasury.Imports.LegacyPayableNotFound`). La orden se paga desde
  su vencimiento, o desde hoy si ya venció.
- Cada fila queda en `TES_LegacyPendingLoads` con su referencia y lo que nació (`MigratedToPublicId`). **Sin hechos contables**: lo
  heredado ya está en los saldos de apertura.

**Cotejo**: si se cargaron cifras de referencia con los conceptos `PendientesHeredadosCheques` o `PendientesHeredadosFacturas`
(plantilla `tesoreria.cifras-de-referencia`), la revisión avisa cuando el total cargado de esa clase no coincide
(`Treasury.Imports.LegacyTotalDiffers`, no bloquea). En todo caso se coteja a mano contra los totales de la exportación (§4) y se
vuelve a correr el diagnóstico: «sin cargar» tiene que dar 0.

## 6. Respaldo

`pg_dump -Fc` de cada base, **inmediatamente antes** de desplegar la versión del retiro, en `/root/respaldos/` del nodo:

```bash
k3s kubectl -n <ns> exec erp-db-1 -c postgres -- pg_dump -Fc -U postgres -d <base> > /root/respaldos/<base>-AAAAMMDD-pre-retiro-tes.dump
```

Se anota el tamaño y que el archivo empieza con `PGDMP`. La referencia va en la línea `Respaldo:` de la cabecera.

**Decisión del dueño del 2026-10-28**: el respaldo es `pg_dump -Fc` de cada base de DEV y QA **antes del despliegue a develop**, con
el sufijo `-pre-f015.dump` (`<base>-AAAAMMDD-pre-f015.dump`), y lo toma el dueño; producción lleva el suyo, con el mismo sufijo,
antes del release. Es un paso **obligatorio** antes de aplicar: sin él no se despliega.

## 7. Segundo revisor y aprobación

El **dueño es el segundo revisor**. Revisa el commit del retiro, el diagnóstico con su resultado, la exportación y la carga de
pendientes, y aprueba **por escrito** en la tabla de §9, base por base. Con eso:

1. Se completan las tres líneas de la cabecera de los dos archivos de la migración (`Diagnóstico:`, `Respaldo:`, `Segundo revisor:`;
   completadas el 2026-10-28), en un commit que sólo toca esos comentarios.
2. En cada base **con filas**, y sólo en ella, se siembra a mano la fila que abre la guarda:

```sql
INSERT INTO dbo."COR_SystemSettings" ("SettingKey", "SettingValue", "ValueType", "ModulePrefix", "Description", "PublicId", "CreatedAt", "CreatedBy", "IsDeleted")
VALUES ('TES.RetiroHeredado.Aprobado',
        '<fecha> <quien aprueba>; exportación <huellas>; pendientes cargados; respaldo <base>-AAAAMMDD-pre-retiro-tes.dump; segundo revisor <nombre>',
        'String', 'TES',
        'Aprobación del retiro de la Tesorería anterior (feature 015, FR-161)',
        gen_random_uuid(), now() at time zone 'utc', '<quien la siembra>', false);
```

(Comprobar las columnas de `COR_SystemSettings` antes de insertar; la guarda sólo mira `SettingKey` e `IsDeleted`.) **Nunca** en
una base sin filas: ahí la guarda pasa sola.

## 8. Qué hace la migración, cómo se aplica y cómo se verifica

Par en `…Persistence.Migrations.PostgreSql/Application/20261026192926_RetiroDeLaTesoreriaHeredada.cs` y
`…Persistence.Migrations.SqlServer/Application/20261026192828_RetiroDeLaTesoreriaHeredada.cs`, generado sobre el modelo sin las tres
entidades, después de `TesoreriaAmpliada`. Las tablas no tienen llaves desde ninguna tabla viva (las de `TRS_Concepts` hacia el plan
de cuentas se van con ella). Su `Up()`:

1. **Guarda.** Cuenta las filas —**incluidas las dadas de baja**— de las tres tablas (saltando las que no existan). Si alguna tiene
   filas **y** la base no trae la fila viva `TES.RetiroHeredado.Aprobado`, se niega —PostgreSQL con `RAISE EXCEPTION`, SQL Server con
   `THROW 50016` (el 50015 es de los grupos del asociado)— nombrando cada tabla con su cantidad y cuántos pendientes **no se cargaron**,
   y **no toca nada**:

   ```
   RetiroDeLaTesoreriaHeredada: la Tesoreria anterior tiene filas: TRS_Checks (12 filas), TRS_Invoices (3 filas); pendientes sin
   cargar a la Tesoreria nueva: 2 cheques sin cobrar y 1 facturas por pagar. La migracion borra (...)
   ```

2. Borra las tres tablas.

**`Down()` lanza** (`NotSupportedException`): la migración es **irreversible**. Lo prueban `PrincipioXII_MigracionesDestructivas`
(cabecera, guarda antes del primer borrado, `THROW 50016` / `RAISE EXCEPTION`, `Down()` sin `CreateTable`) y la e2e
`RetiroDeLaTesoreriaHeredadaTests` (los dos motores, cooperativa aislada: la guarda se niega con un cheque y una factura, cuenta
menos pendientes al cargar uno, pasa con la aprobación; el `Down()` lanza; las rutas responden 404). Como el `Down()` no existe, las
e2e que recorren el historial hacia atrás (los retiros de la 012, 013 y 014 y `ComercioAmpliadoMigracionTests`) llaman antes a
`RetiroDeLaTesoreriaHeredadaTests.SacarElRetiroAsync`, que recrea vacías las tres tablas con el modelo de la migración anterior y saca
el retiro del historial —sólo en bases de prueba—. **Pruebas escritas, sin correr**: van en la corrida final.

**Aplicación**: como toda migración del clúster, por el Job PreSync del DbMigrator, base por base, después de `TesoreriaAmpliada`. Si
la guarda se niega en una base, el Job falla en ella y la sincronización no avanza: se diagnostica, se completa lo que falte y se
vuelve a sincronizar.

**Verificación**:

```sql
SELECT table_name FROM information_schema.tables WHERE table_schema = 'dbo' AND table_name LIKE 'TRS\_%';     -- 0 filas
SELECT "MigrationId" FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" LIKE '%RetiroDeLaTesoreriaHeredada';
SELECT "Kind", "Status", COUNT(*) FROM dbo."TES_LegacyPendingLoads" WHERE NOT "IsDeleted" GROUP BY 1, 2;     -- lo cargado sigue
```

Por HTTP: `GET /api/treasury/checks`, `/concepts`, `/invoices` y `/cash-flow?year=2026` responden 404. Nada más cambia: el resto del
esquema queda igual (comparar `\dt dbo.*` antes y después).

Quien tenga `ui.pagina-inicio` en una pantalla retirada (`/tesoreria/cheques`, `/conceptos`, `/facturas`, `/flujo-caja`) aterriza en
«Página no encontrada»: se le avisa y se borra la preferencia.

**Restauración** (no hay vuelta atrás por migración): antes de operar sobre lo cargado, se restaura el respaldo de esa base y se
vuelve a la imagen anterior. Lo exportado (§4) queda como constancia aunque no se restaure.

## 9. Estado y aprobación por base

| Fecha | Ambiente | Base | Filas (Checks / Concepts / Invoices) | Pendientes sin cargar | Exportación (huellas) | Respaldo | Aprobación del dueño |
|---|---|---|---|---|---|---|---|
| 2026-10-27/28 | DEV | `ingenia365erp` | 0 / 0 / 0 | ninguno | no aplica | `-pre-f015.dump` antes del despliegue a develop (obligatorio) | Jorman Copete, por escrito, 2026-10-28 |
| 2026-10-27/28 | QA | `coop_prueba`, `ingenia365erp` | 0 / 0 / 0 | ninguno | no aplica | `-pre-f015.dump` antes del despliegue a develop (obligatorio) | Jorman Copete, por escrito, 2026-10-28 |
| 2026-10-27/28 | PDN | `cooflopal`, `ingenia365erp` | 0 / 0 / 0 | ninguno | no aplica | `-pre-f015.dump` antes del release (obligatorio) | Jorman Copete, por escrito, 2026-10-28 |

**Segundo revisor**: Jorman Copete aprobó por escrito el retiro el 2026-10-28, como segundo revisor (Principio XII), con el
diagnóstico de arriba. Con las tres tablas vacías la guarda deja pasar sin la fila `TES.RetiroHeredado.Aprobado`.

## 10. Síntomas

| Síntoma | Causa | Qué hacer |
|---|---|---|
| El Job PreSync falla con `RetiroDeLaTesoreriaHeredada: la Tesoreria anterior tiene filas: …` | esa base tiene filas y no tiene la aprobación | diagnóstico, exportación, carga, respaldo, revisor y la fila de §7; volver a sincronizar |
| SQL Server: error `50016` con el mismo texto | ídem, en SQL Server | ídem |
| «pendientes sin cargar» no baja a 0 | la referencia de la carga no es el `Id` de la exportación, o la fila quedó rechazada | revisar la columna `referencia` y el resultado de la revisión |
| La carga de una factura responde `Treasury.Imports.LegacyPayableNotFound` | Contabilidad no tiene esa cuenta por pagar con ese documento cruce, o ya está en órdenes | cargarla en los saldos de apertura con su tercero y documento cruce, o corregir el valor |
| `POST /api/treasury/legacy-export` responde 404 | la versión desplegada ya trae el retiro | la exportación es de la versión anterior (§2); si las tablas ya no están, lo exportado es lo que quedó en §9 |
| Una integración recibe 404 en `/api/treasury/checks` o vecinas | se retiraron sin alias | pasar a las rutas de la Tesorería nueva (§1) |
