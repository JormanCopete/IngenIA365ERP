# Captaciones (feature 014): retirar las captaciones heredadas

> Qué hace la migración destructiva `RetiroDeCaptacionesHeredadas`, qué hay que tener antes de aplicarla en una base, cómo
> comprobarlo y cómo se vuelve atrás. Es la única migración destructiva de la feature (FR-119, FR-120, Principio XII;
> research R28 y C19). Sigue el molde de [cartera-retiro-heredado.md](cartera-retiro-heredado.md)
> (`RetiroDeLaCarteraHeredada`, feature 013) y de [inventario-retiro-heredado.md](inventario-retiro-heredado.md).

## 1. Qué se retira

La 013 retiró las 55 tablas de crédito heredado y **conservó 38 `LND_*`** de ahorros y aportes con su código mudado a
`Savings`. La 014 construye las captaciones y los aportes desde cero (Q2: retirar todo y renovar lo necesario), así que **ya no
hay conservado**: se retiran esas 38 y las **7 `CDT_*`** del CDT heredado, **45 tablas**:

| Grupo | Tablas |
|---|---|
| Ahorros y depósitos (7) | `LND_SavingsAccounts`, `LND_SavingsParameters`, `LND_DepositAccounts`, `LND_DepositEntries`, `LND_DepositAudit`, `LND_DepositSeals`, `LND_DepositSignatures` |
| Caja y chequeras (4) | `LND_Cashiers`, `LND_CashBases`, `LND_Checkbooks`, `LND_CheckClearing` |
| Aportes y retiros (4) | `LND_ContributionReductions`, `LND_ContributionReductionParams`, `LND_AssociateWithdrawals`, `LND_WithdrawalStatuses` |
| SIPLA (5) | `LND_SiplaParameters`, `LND_SiplaGroupParameters`, `LND_UnusualTransactions`, `LND_UnusualTransactionEntries`, `LND_MoneyLaunderingDeclarations` |
| Compartidas (5) | `LND_Minutes`, `LND_MinuteAttendees`, `LND_Blacklist`, `LND_TransactionCodes`, `LND_Documents` |
| Asociados y bienestar (10) | `LND_ActivityEnrollments`, `LND_AssociateActivities`, `LND_AssociateDiseases`, `LND_BiometricRecords`, `LND_PersonAssets`, `LND_Subsidies`, `LND_AuxiliaryApplications`, `LND_AuxiliaryApplicationLines`, `LND_AuxAppInstallments`, `LND_AuxAppInstallmentBeneficiaries` |
| Zonas (3) | `LND_Zones`, `LND_SubZones`, `LND_ZoneTypes` |
| CDT (7) | `CDT_AssociateReferences`, `CDT_Audit`, `CDT_CertificateEntries`, `CDT_Certificates`, `CDT_ParameterAudit`, `CDT_Parameters`, `CDT_RatesByTerm` |

Con ellas sale todo su código (T007, T008): `Domain/Entities/Savings` y `Domain/Entities/CDT`, `Application/Savings` y
`Application/CDT`, sus configuraciones, las 12 familias de `/api/savings/*`, las de `/api/cdt/*` y `/api/reports/savings/*`
(responden 404, **sin alias**), el PDF de portafolio de la persona, las 18 pantallas de `Pages/AhorrosYAportes` y las 2 de
`Pages/CDT`, el grupo de menú «Ahorros y aportes» (16 enlaces) y el grupo «CDT» (2), sus temas del manual y sus dos tarjetas del
centro de reportes, y la regla de auditoría `Savings` (lo que venga de captaciones es `Lending`, encadenado y de diez años).

Efectos fuera de Cartera, declarados:
- **Tarjeta débito**: emitir y mover una tarjeta leía la cuenta de ahorros heredada. Hasta que las tarjetas se vinculen a una
  cuenta de captación (fuera de la 014, spec «Fuera de esta feature»), `POST /api/debit/cards` y el movimiento responden
  `DebitCard.SavingsAccountUnavailable` sin efectos.
- **Detalle de la persona**: el conteo de cuentas de ahorro sale en cero hasta que `ProductosDeCaptacionAsync` (T043) lo informe.
- **Dónde se usa una cuenta contable**: las ramas de parámetros de ahorro y de CDT salen; las cuentas de captaciones entran por la
  matriz de Cartera.

Lo que esta feature necesita de SIPLA no se migra: se renueva como parámetros de efectivo y de origen de fondos (R28, Q2).

## 2. Qué hace la migración, en orden

Par PostgreSQL / SQL Server en
`…Persistence.Migrations.PostgreSql/Application/20261018143710_RetiroDeCaptacionesHeredadas.cs` y
`…Persistence.Migrations.SqlServer/Application/20261018143627_RetiroDeCaptacionesHeredadas.cs`, generado sobre el modelo sin
las 45 entidades y **sin ninguna entidad nueva**, después de todas las de la 013 (`CarteraVivienda` es la última) y **antes** de
`CaptacionesAportes` (C19), en commit propio. Las tablas no tienen llaves desde ninguna tabla viva: EF no generó ningún
`DropForeignKey`. Su `Up()` empieza con la cabecera `MIGRACION-DESTRUCTIVA-APROBADA` que vigila
`PrincipioXII_MigracionesDestructivas`, con sus tres líneas **Diagnóstico**, **Respaldo** y **Segundo revisor**.

1. **Guarda.** Cuenta las filas —**incluidas las dadas de baja**— de las 45 tablas (saltando las que no existan en esa base). Si
   alguna tiene filas **y** la base no trae la fila viva `COR_SystemSettings.SettingKey = 'LND.CaptacionesHeredadas.RetiroAprobado'`,
   se niega nombrando cada tabla con su cantidad —PostgreSQL con `RAISE EXCEPTION`, SQL Server con `THROW 50014` (el 50013 es de
   la 013)— y **no toca nada**:

   ```
   RetiroDeCaptacionesHeredadas: las captaciones heredadas tienen filas: LND_SavingsAccounts (120 filas), CDT_Certificates
   (8 filas). La migracion borra las 38 tablas LND_ que la feature 013 conservo (...) y las 7 CDT_; solo se aplica sobre
   tablas vacias o con la aprobacion del dueno para esta cooperativa (...).
   ```

   Con todo vacío, la guarda pasa sola: no hace falta la fila de aprobación.
2. Borra las 45 tablas.

`Down()` las **recrea vacías** con sus índices: en datos es irreversible, lo que había se recupera del respaldo (§7).

Lo prueba `RetiroDeCaptacionesHeredadasTests` (colección «Captaciones e2e», los dos motores, cooperativa aislada): baja a la
migración anterior, inserta una fila en `LND_SavingsAccounts` y otra en `CDT_Certificates`, comprueba que la guarda las nombra con
su cantidad, siembra la aprobación y comprueba que pasa y que las 45 desaparecen. **e2e en la corrida final** (decisión del dueño:
las e2e contra base de datos se corren una sola vez al final de todas las fases).

## 3. Diagnóstico

Por **cada base de cooperativa** que la migración va a tocar —y la base por defecto `ingenia365erp`; la administrativa no tiene
ninguna de las 45—, **antes de fusionar** (local, DEV y QA) y **el mismo día de cada promoción** (producción, con autorización
expresa del dueño):

```sql
-- en ingenia365erp_admin: las bases de cooperativa
SELECT "Name", "DatabaseName" FROM dbo."ADM_Tenants" WHERE NOT "IsDeleted";
```

```bash
k3s kubectl -n <ns> exec -i erp-db-1 -c postgres -- psql -U postgres -d <base> -f - < specs/014-ahorros-aportes-cdt/diagnostico-captaciones-heredadas.sql
```

[specs/014-ahorros-aportes-cdt/diagnostico-captaciones-heredadas.sql](../../specs/014-ahorros-aportes-cdt/diagnostico-captaciones-heredadas.sql)
es de **sólo lectura** e informa `tabla | filas` de las 45, ordenado por filas. Las tablas viven en el esquema `dbo`: correrlo con
`SET search_path TO dbo;` antes (o con las tablas calificadas). Una tabla que no exista en esa base da error: se quita de la lista
para esa base y se anota.

**Qué se decide con el resultado**:

| Resultado | Decisión |
|---|---|
| Todo en 0 | la guarda pasa sola; no se siembra nada |
| Filas en ahorros, depósitos, aportes o CDT | **se detiene** y decide el dueño: esos saldos no se migran solos; se cargan con las plantillas de migración de captaciones (US9) antes de aprobar perder las tablas |
| Filas en SIPLA, caja, zonas o bienestar | **se detiene** y decide el dueño (propuesta por defecto: aprobar con constancia; lo de SIPLA se renueva como parámetros) |

Los resultados se anotan en la tabla de §9 y en la línea `Diagnóstico:` de la cabecera de los dos archivos.

## 4. Respaldo

`pg_dump -Fc` de **cada base de cooperativa**, **inmediatamente antes** de aplicar, en `/root/respaldos/` del nodo:

```bash
k3s kubectl -n <ns> exec erp-db-1 -c postgres -- pg_dump -Fc -U postgres -d <base> > /root/respaldos/<base>-AAAAMMDD-pre-f014.dump
```

Se anota el tamaño y que el archivo empieza con `PGDMP`. En DEV el respaldo es el propio contenedor. La referencia del respaldo de
producción se escribe en la línea `Respaldo:` de la cabecera antes de aplicar.

## 5. Aprobación (sólo si hay filas)

Si una base tiene filas, no se aplica sin la **aprobación explícita del dueño para esa cooperativa**, el **respaldo hecho** y el
**nombre del segundo revisor**. Con las tres cosas se siembra, **a mano y sólo en esa base**, la fila que abre la guarda:

```sql
INSERT INTO dbo."COR_SystemSettings" ("SettingKey", "SettingValue", "ValueType", "ModulePrefix", "Description", "PublicId", "CreatedAt", "CreatedBy", "IsDeleted")
VALUES ('LND.CaptacionesHeredadas.RetiroAprobado',
        '<fecha> <quien aprueba>; respaldo <base>-AAAAMMDD-pre-f014.dump; segundo revisor <nombre>; decision <...>',
        'String', 'LND',
        'Aprobación del retiro de las captaciones heredadas (feature 014, FR-119)',
        gen_random_uuid(), now() at time zone 'utc', '<quien la siembra>', false);
```

(Comprobar las columnas de `COR_SystemSettings` antes de insertar; la guarda sólo mira `SettingKey` e `IsDeleted`.) **Nunca** se
siembra en una base sin filas: ahí la guarda pasa sola y la fila sólo abriría la puerta a una pérdida que nadie revisó.

## 6. Segundo revisor y cómo se aplica

- **Segundo revisor**: lo nombra el dueño. Su nombre va en la línea `Segundo revisor:` de la cabecera antes de aplicar en
  producción, en un commit que sólo toca esos comentarios.
- Como toda migración del clúster: por el **Job PreSync del DbMigrator**, base por base, en orden: las de la 013 →
  **`RetiroDeCaptacionesHeredadas`** → `CaptacionesAportes` → las demás de la 014. Si la guarda se niega en una base, el Job falla
  en ella y la sincronización no avanza: se diagnostica, se decide con el dueño y se vuelve a sincronizar. En DEV local,
  `AutoMigrate` la aplica al arrancar.

## 7. Verificación posterior y volver atrás

```sql
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'dbo' AND (table_name LIKE 'CDT\_%' OR table_name IN ('LND_SavingsAccounts','LND_DepositAccounts',
  'LND_ContributionReductions','LND_SiplaParameters','LND_Zones','LND_Cashiers'));                                 -- 0 filas
SELECT "MigrationId" FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" LIKE '%RetiroDeCaptacionesHeredadas';
```

Por HTTP: `GET /api/savings/zones`, `GET /api/cdt/parameters` y `GET /api/reports/savings/person-portfolio/{id}` responden 404.

Quien tenga `ui.pagina-inicio` en una pantalla retirada (`/cartera/depositos`, `/cartera/retiros`, `/cartera/extractos`,
`/cartera/parametros-ahorro`, `/cartera/parametros-sipla`, `/cartera/codigos-movimiento`, `/cartera/zonas`, `/cartera/tipos-zona`,
`/cartera/estados-retiro`, `/cartera/cdt…`, `/cdt/…`) aterriza en «Página no encontrada»: se le avisa y se borra la preferencia
(misma consulta de la §7 de la 013, con estas rutas). `/cartera/ahorros*`, `/cartera/aportes*` y `/cartera/retiros-asociado*`
vuelven con las pantallas nuevas de cada historia.

**Volver atrás**: antes de que la cooperativa opere con captaciones, se **restaura el respaldo** de esa base y se vuelve la
imagen. El `Down()` no devuelve datos (recrea vacías las 45) y, con `CaptacionesAportes` aplicada, exige bajar antes esa y las
siguientes. Después de operar, volver atrás es perder lo operado: no se hace sin decisión del dueño.

## 8. Notas de la promoción

Se avisa a las cooperativas: el grupo de menú «Ahorros y aportes» y el grupo «CDT» desaparecen —el menú «Captaciones y aportes»
llega con cada historia—; las integraciones por API que usaban `/api/savings/*`, `/api/cdt/*` o `/api/reports/savings/*` reciben
404 y pasan a las rutas de captaciones (`/api/lending/accounts`, `/api/lending/term-deposits`, `/api/lending/contributions`,
contracts/api.md); la tarjeta débito queda sin emitir ni mover hasta su propia feature.

## 9. Estado del diagnóstico (T005)

| Fecha | Ambiente | Base | Filas en las 45 | Aprobación | Decisión |
|---|---|---|---|---|---|
| 2026-10-18 | local (PostgreSQL 5433, `ingenia365-dev-postgres`) | `ingenia365erp` | 0 (las 45 existen) | no | la guarda pasa sola |
| 2026-10-18 | local (PostgreSQL 5433) | `ingenia365erp_admin` | no tiene ninguna de las 45 | — | no aplica |
| pendiente | local (SQL Server, si hay bases) | cada base de cooperativa | | | dueño |
| pendiente | DEV | cada base de cooperativa e `ingenia365erp` | | | dueño (T005) |
| pendiente | QA | cada base de cooperativa e `ingenia365erp` | | | dueño (T005) |
| pendiente | PDN | cada base, con autorización expresa | | | dueño (T005) |

## 10. Síntomas

| Síntoma | Causa | Qué hacer |
|---|---|---|
| El Job PreSync falla con `RetiroDeCaptacionesHeredadas: las captaciones heredadas tienen filas: …` | esa base tiene datos y no tiene la aprobación | diagnóstico, decisión del dueño, respaldo, revisor y la fila de §5; volver a sincronizar |
| SQL Server: error `50014` con el mismo texto | ídem, en SQL Server | ídem |
| Una integración recibe 404 en `/api/savings/…` o `/api/cdt/…` | se retiraron sin alias | pasar a las rutas de captaciones (§8) |
| Emitir una tarjeta débito responde `DebitCard.SavingsAccountUnavailable` | la tarjeta se vinculaba a la cuenta de ahorros heredada | esperar la feature de tarjetas (fuera de la 014) |
| Una cooperativa nueva no está en el diagnóstico | se aprovisionó después de correrlo | repetir el diagnóstico el día de la promoción |
