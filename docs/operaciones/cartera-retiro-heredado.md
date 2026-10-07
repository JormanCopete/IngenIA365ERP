# Cartera (feature 013): retirar la cartera heredada

> Qué hace la migración destructiva `RetiroDeLaCarteraHeredada`, qué hay que tener antes de aplicarla en una base, cómo
> comprobarlo y cómo se vuelve atrás. Es la única migración destructiva de la feature (FR-144, Principio XII; data-model
> parte 3 §13; decisiones transversales T86–T89, C103). Sigue el molde de
> [inventario-retiro-heredado.md](inventario-retiro-heredado.md) (`RetiroDelInventarioHeredado`, feature 012).

## 1. Qué se retira y qué se conserva

El traslado a medias del sistema anterior había dejado **93 tablas `LND_*`**. La Cartera nueva no usa ninguna.

**Se retiran las 55 de crédito** (lista exacta en data-model parte 3 §13.1): créditos y movimientos
(`LND_LoanPortfolios`, `LND_Transactions`, cuotas, abonos extra), solicitudes, garantías, seguros, vivienda,
reestructuraciones, causación y mora, clasificación y provisión, scoring y tasas, líneas (`LND_CreditLineParameters`,
`LND_CreditLineAudit`, `LND_CreditParameters`), cobranza, deducciones, facturas de cartera y las dos dependientes de la línea
(`LND_OnlineQueries`, `LND_RecreationApplications`). Con ellas sale todo su código: las 63 rutas de `Endpoints/Lending`,
`aging`, `aging/pdf` y `loan-statement/{id}/pdf` de `/api/reports/lending` (responden 404, sin alias), las 21 pantallas de
crédito, sus enlaces de menú, sus temas del manual y sus tarjetas del centro de reportes, los PDF de extracto de crédito y
cartera por edades, el bloque `Lending` de permisos de `IdentitySeedData` con su rol `LoanOfficer` y el proyecto
`IngenIA365ERP.Legacy`.

**Se conservan 38, con sus filas y sin cambio de nombre**: ahorros y depósitos (7), caja y chequeras (4), aportes (2),
retiros (2), SIPLA (5), compartidas (5: actas, lista negra, códigos de movimiento, documentos), asociados y servicios (10) y
zonas (3). Su código se mudó de `Lending` a `Savings` (enmienda A10):

| Antes | Después |
|---|---|
| `Domain.Entities.Lending`, `Configurations/Lending`, `Application.Lending` | `Domain.Entities.Savings`, `Configurations/Savings`, `Application.Savings` |
| `/api/lending/{contributions, savings-accounts, savings-interest-liquidation, savings-parameters, sipla-parameters, statements, sub-zones, transaction-codes, withdrawal-statuses, withdrawals, zone-types, zones}` | `/api/savings/{mismo sufijo}` |
| `/api/reports/lending/person-portfolio/{id}` (+ `/pdf`), `/savings-statement/{id}/pdf` | `/api/reports/savings/…` (el portafolio ya no trae créditos) |
| `Pages/CarteraFinanciera` (18 pantallas) | `Pages/AhorrosYAportes`, **con las mismas rutas `/cartera/...`** |
| grupo de menú «Cartera Financiera» | grupo «Ahorros y aportes» (16 enlaces) |

Las rutas `/api/savings/*` conservan el permiso de hoy (sólo sesión) hasta la feature de Ahorros. La auditoría de lo mudado
sale con módulo `Savings` (no encadenado); la de Cartera, con módulo `Lending`, va sellada por `COR_AuditOutbox` y con
retención de diez años.

`LND_SiplaGroupParameters` se conserva pero **suelta su llave** a `LND_CreditLineParameters`: `CreditLineId` queda como dato
requerido, sin FK ni índice. Nómina suelta `PAY_SettlementDeductions.LoanPortfolioId` (con su FK, su IX y
`UK_PAY_SettlementDeductions_Termination_Loan`) y `PAY_Novelties.LoanPortfolioId`: la referencia a la obligación ya la
copió `NominaRecaudaPorElContrato` a `ObligationPublicId`/`ObligationNumber` (C73).

**Los comprobantes contables con origen `CAR` (`ACC_Documents.OriginModule = 'CAR'`) no se tocan**: son inmutables
(Principio XI). Los tipos `DS`, `RC`, `CA`, `PV`, `DN` siguen sembrados.

## 2. Qué hace la migración, en orden

Par PostgreSQL / SQL Server en
`…Persistence.Migrations.PostgreSql/Application/20261007172102_RetiroDeLaCarteraHeredada.cs` y
`…Persistence.Migrations.SqlServer/Application/20261007172025_RetiroDeLaCarteraHeredada.cs`, generado sobre el **modelo
intermedio** (sin las 55 entidades y sin ninguna entidad `LND_` nueva), después de `ValoresNacionales`,
`PlataformaParaCartera`, `NominaRecaudaPorElContrato` y `ComercioConCartera` y antes de `CarteraNucleo` (C103), en
**commit propio**. Su `Up()` empieza con la cabecera `MIGRACION-DESTRUCTIVA-APROBADA` que vigila
`PrincipioXII_MigracionesDestructivas`, con tres líneas: **Diagnóstico** (lo completa T0183/T0198 antes de fusionar),
**Respaldo** y **Segundo revisor** (los completan T1084 y T1083, en un commit que sólo toca esos comentarios, antes de aplicar
en producción).

1. **Guarda.** Cuenta las filas —**incluidas las dadas de baja**— de las 55 tablas (saltando las que no existan en esa base),
   de `LND_SiplaGroupParameters`, de `PAY_SettlementDeductions` y `PAY_Novelties` con `LoanPortfolioId` no nulo y de
   `ACC_Documents` con `OriginModule = 'CAR'`. Si algo es mayor que cero **y** la base no trae la fila viva
   `COR_SystemSettings.SettingKey = 'LND.RetiroHeredado.Aprobado'`, se niega nombrando cada tabla con su cantidad
   —PostgreSQL con `RAISE EXCEPTION`, SQL Server con `THROW 50013` (el 50012 es de la 012)— y **no toca nada**:

   ```
   RetiroDeLaCarteraHeredada: la cartera heredada tiene filas: LND_LoanPortfolios (3 filas), ACC_Documents con
   OriginModule CAR (12 filas). La migracion borra las 55 tablas de credito y la columna LoanPortfolioId de Nomina; solo
   se aplica sobre tablas vacias o con la aprobacion del dueno para esta cooperativa (fila
   COR_SystemSettings.SettingKey = 'LND.RetiroHeredado.Aprobado'), previo pg_dump y segundo revisor (...).
   ```

   Con todo vacío, la guarda pasa sola: no hace falta la fila de aprobación.
2. Suelta las **40 llaves** de data-model §13.2 (33 hacia `LND_CreditLineParameters`, una de ellas la de SIPLA; 5 hacia
   `LND_LoanPortfolios`; `LND_LoanPortfolios → COR_People`; `PAY_SettlementDeductions → LND_LoanPortfolios`) y los índices que
   dependían de ellas, para que el orden de los borrados no importe.
3. Borra `PAY_SettlementDeductions.LoanPortfolioId` y `PAY_Novelties.LoanPortfolioId`.
4. Borra las 55 tablas.

`Down()` las **recrea vacías** con sus llaves y las columnas de Nómina, y lo declara: en datos es irreversible, lo que había
se recupera del respaldo (§7).

Lo prueba `RetiroDeLaCarteraHeredadaTests` (colección «Cartera e2e», los dos motores, cooperativa aislada): baja a la
migración anterior, inserta una fila en `LND_LoanPortfolios` y otra en `LND_SavingsAccounts`, comprueba que la guarda nombra la
retirada y su cantidad, siembra la aprobación, comprueba que pasa, que las 55 desaparecen y que la fila de ahorros sigue; y por
HTTP, que las rutas retiradas responden 404 y las conservadas responden en `/api/savings/*` y `/api/reports/savings/*`.

## 3. Diagnóstico

Por **cada base** que la migración va a tocar —cada base de cooperativa y la base por defecto `ingenia365erp`; la
administrativa no tiene tablas `LND_`—, **antes de fusionar** (local, DEV y QA) y **el mismo día de cada promoción**
(producción, con autorización expresa del dueño):

```sql
-- en ingenia365erp_admin: las bases de cooperativa
SELECT "Name", "DatabaseName" FROM dbo."ADM_Tenants" WHERE NOT "IsDeleted";
```

```bash
k3s kubectl -n <ns> exec -i erp-db-1 -c postgres -- psql -U postgres -d <base> -f - < specs/013-cartera-financiera/diagnostico-cartera-heredada.sql
```

[specs/013-cartera-financiera/diagnostico-cartera-heredada.sql](../../specs/013-cartera-financiera/diagnostico-cartera-heredada.sql)
es de **sólo lectura** (bloques PostgreSQL y SQL Server). Informa, como `tabla | filas`:

1. las 55 retiradas (−1 si la tabla no existe) y su total;
2. las 38 conservadas (informativo);
3. ahorro o aportes metidos en tablas de crédito: `LND_Transactions` con `SavingsLineId` o `CdatAccrualDate`,
   `LND_PayrollDeductions` y `LND_PayrollDeductionPeriods` con `ContributionAmount > 0`;
4. `LND_SiplaGroupParameters`;
5. Nómina: `PAY_SettlementDeductions` con `LoanPortfolioId` por estado, `PAY_Novelties` con `LoanPortfolioId`, y las filas
   que la copia de C73 **no encontraría** (un `LoanPortfolioId` sin crédito heredado que lo respalde);
6. `ACC_Documents` con origen `CAR` por tipo de origen y comprobante;
7. si ya existe la aprobación.

**Qué se decide con el resultado** (preguntas H1, H3, H4 de las transversales):

| Resultado | Decisión |
|---|---|
| Todo en 0 | la guarda pasa sola; no se siembra nada |
| Filas en una de las 55 | el retiro se detiene en esa base hasta que el dueño apruebe perderlas (§5) |
| Ahorro o aportes dentro de tablas de crédito (punto 3) | **se detiene** y decide el dueño: esos datos no se migran solos |
| `LND_SiplaGroupParameters` con filas | **se detiene** y decide el dueño (propuesta por defecto: conservar SIPLA sin FK) |
| `ACC_Documents` `CAR` | no se tocan; propuesta por defecto: aprobar con constancia (H4) |
| Filas que la copia de C73 no encontraría | **pérdida declarada** (§8): la definitiva o la novedad pierden la referencia a la obligación |

Los resultados se anotan en la tabla de §9 y en la línea `Diagnóstico:` de la cabecera de los dos archivos (T0198).

## 4. Respaldo

`pg_dump -Fc` de **cada base**, **inmediatamente antes** de aplicar, con la convención de la feature, en `/root/respaldos/`
del nodo —**también de la administrativa** (`ingenia365erp_admin`): en la misma promoción viaja `ValoresNacionales`—:

```bash
k3s kubectl -n <ns> exec erp-db-1 -c postgres -- pg_dump -Fc -U postgres -d <base> > /root/respaldos/<base>-AAAAMMDD-pre-f013.dump
```

Se anota el tamaño y que el archivo empieza con `PGDMP`. En DEV el respaldo es el propio contenedor. La referencia del
respaldo de producción se escribe en la línea `Respaldo:` de la cabecera (T1084) antes de aplicar.

## 5. Aprobación (sólo si hay filas)

Si una base tiene filas que la guarda cuenta, no se aplica sin la **aprobación explícita del dueño para esa cooperativa**, el
**respaldo hecho** y el **nombre del segundo revisor**. Con las tres cosas se siembra, **a mano y sólo en esa base**, la fila
que abre la guarda:

```sql
INSERT INTO dbo."COR_SystemSettings" ("SettingKey", "SettingValue", "ValueType", "ModulePrefix", "Description", "PublicId", "CreatedAt", "CreatedBy", "IsDeleted")
VALUES ('LND.RetiroHeredado.Aprobado',
        '<fecha> <quien aprueba>; respaldo <base>-AAAAMMDD-pre-f013.dump; segundo revisor <nombre>; decision <H1/H3/H4>',
        'String', 'LND',
        'Aprobación del retiro de la cartera heredada (feature 013, FR-144)',
        gen_random_uuid(), now() at time zone 'utc', '<quien la siembra>', false);
```

(Comprobar las columnas de `COR_SystemSettings` en la base antes de insertar; la guarda sólo mira `SettingKey` e `IsDeleted`.)
El valor lleva el respaldo, el revisor y la decisión: queda como constancia en la propia base. **Nunca** se siembra en una base
sin filas: ahí la guarda pasa sola y la fila sólo abriría la puerta a una pérdida que nadie revisó.

## 6. Segundo revisor y cómo se aplica

- **Segundo revisor** (pregunta H7): lo nombra el dueño. Su nombre va en la línea `Segundo revisor:` de la cabecera (T1083)
  antes de aplicar en producción, en un commit que sólo toca esos comentarios.
- Como toda migración del clúster: por el **Job PreSync del DbMigrator** en la sincronización de Argo (`AutoMigrate` está
  apagado en QA y producción), base por base, en el orden de K1 (C103): `ValoresNacionales` (Admin) →
  `PlataformaParaCartera` → `NominaRecaudaPorElContrato` → `ComercioConCartera` → **`RetiroDeLaCarteraHeredada`** →
  `CarteraNucleo` → `CarteraRiesgo` → `IntegracionContableDeCartera`. Si la guarda se niega en una base, el Job falla en ella
  y la sincronización no avanza: se diagnostica esa base, se decide con el dueño y se vuelve a sincronizar. En DEV local,
  `AutoMigrate` la aplica al arrancar.

## 7. Verificación posterior

```sql
-- Las 55 ya no están (muestra).
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'dbo' AND table_name IN ('LND_LoanPortfolios','LND_Transactions','LND_CreditLineParameters',
  'LND_PayrollDeductions','LND_Guarantees','LND_InterestRates','LND_OnlineQueries','LND_RecreationApplications');   -- 0 filas

-- Las conservadas siguen, con el mismo conteo que dio el diagnóstico.
SELECT count(*) FROM dbo."LND_SavingsAccounts";
SELECT count(*) FROM dbo."LND_SiplaGroupParameters";

-- Nómina ya no tiene la columna heredada.
SELECT column_name FROM information_schema.columns
WHERE table_schema = 'dbo' AND table_name IN ('PAY_SettlementDeductions','PAY_Novelties') AND column_name = 'LoanPortfolioId';  -- 0 filas

-- Los comprobantes CAR siguen intactos (mismo total que el diagnóstico).
SELECT count(*) FROM dbo."ACC_Documents" WHERE "OriginModule" = 'CAR';

-- La migración quedó registrada.
SELECT "MigrationId" FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" LIKE '%RetiroDeLaCarteraHeredada';
```

Por HTTP: `GET /api/savings/zones` responde (con sesión); `GET /api/lending/portfolios` y
`GET /api/reports/lending/aging` responden 404.

**Usuarios con página de inicio retirada.** Quien tenga `ui.pagina-inicio` en una pantalla de crédito retirada (por ejemplo
`/cartera/recaudos`) aterriza en «Página no encontrada» al entrar. Se revisan y se corrigen:

```sql
SELECT "CentralUserId", "TenantPublicId", "SettingValue" FROM dbo."ADM_UserSettings"
WHERE "SettingKey" = 'ui.pagina-inicio'
  AND "SettingValue" IN ('/cartera/solicitudes','/cartera/creditos','/cartera/recaudos','/cartera/mora','/cartera/desembolsos',
    '/cartera/lineas-credito','/cartera/tasas-interes','/cartera/tasas-plazo','/cartera/parametros-provision','/cartera/scoring',
    '/cartera/conceptos-descuento','/cartera/parametros-vivienda','/cartera/parametros-periodicidad','/cartera/cuentas-cartera',
    '/cartera/causacion','/cartera/calificacion','/cartera/descuento-nomina','/reportes/extracto-credito','/reportes/cartera-edades');
```

(Comprobar el nombre de la tabla de preferencias en la base administrativa antes de correrlo.) A esas personas se les avisa y
se borra la preferencia —vuelven al inicio por defecto— o se cambia por la que elijan.

## 8. Volver atrás y pérdida declarada

- **Antes de que la cooperativa opere con la Cartera nueva**, lo correcto es **restaurar el respaldo** de esa base
  (`pg_restore` del `*-pre-f013.dump` sobre una base recreada) y volver la imagen. El nodo no tiene `pg_restore` y el
  contenedor no acepta el archivo por `stdin`: se copia el archivo al pod (`kubectl cp`) y se restaura desde allí.
- **El `Down()` no devuelve datos**: recrea las 55 tablas vacías. Con `CarteraNucleo` aplicada, bajar el retiro exige bajar
  antes esa migración y las siguientes, que borran todo lo que la Cartera nueva haya escrito. Sirve en una base de ensayo; en
  un ambiente compartido, es restore.
- **Después de que la cooperativa operó**, volver atrás es perder lo que operó: no se hace sin decisión del dueño.
- **Pérdida declarada (Principio XII).** Si el diagnóstico encontró filas de Nómina que la copia de C73 no encontraría, tras
  el retiro esas definitivas o novedades conservan su importe y su comprobante pero **pierden la referencia a la obligación**
  (no hay crédito heredado con qué cruzarla). Se anotan base, cantidad y la aceptación del dueño en §9 antes de aplicar.

## 9. Notas de la promoción y estado

En las notas de la promoción se avisa a las cooperativas: el grupo de menú «Cartera Financiera» pasa a llamarse **«Ahorros y
aportes»** (mismas pantallas y mismas direcciones `/cartera/...`); las pantallas de crédito heredado ya no existen —la Cartera
nueva trae las suyas—; y quien integre por API cambia `/api/lending/{familia}` por **`/api/savings/{familia}`** y
`/api/reports/lending/person-portfolio` por **`/api/reports/savings/person-portfolio`**.

| Fecha | Ambiente | Base | Retiradas | Conservadas | Ahorro en crédito | SIPLA | Nómina sin cruce | `ACC_Documents` CAR | Aprobación | Decisión |
|---|---|---|---|---|---|---|---|---|---|---|
| 2026-10-07 | local (5432) | `ingenia365erp` | 0 | 0 | 0 | 0 | 0 | 0 | no | la guarda pasa sola |
| 2026-10-07 | local (5432) | `coop_alfa` | 0 | 0 | 0 | 0 | 0 | 0 | no | la guarda pasa sola |
| 2026-10-07 | local (5432) | `coop_beta` | 0 | 0 | 0 | 0 | 0 | 0 | no | la guarda pasa sola |
| pendiente | DEV | cada base de cooperativa e `ingenia365erp` | | | | | | | | T0183 (dueño) |
| pendiente | QA | cada base de cooperativa e `ingenia365erp` | | | | | | | | T0183 (dueño) |
| pendiente | PDN | cada base, con autorización expresa | | | | | | | | Polish (fase 24) |

## 10. Síntomas

| Síntoma | Causa | Qué hacer |
|---|---|---|
| El Job PreSync falla con `RetiroDeLaCarteraHeredada: la cartera heredada tiene filas: …` | esa base tiene datos que la guarda cuenta y no tiene la aprobación | diagnóstico, decisión del dueño, respaldo, revisor y la fila de §5; volver a sincronizar |
| SQL Server: error `50013` con el mismo texto | ídem, en el motor SQL Server | ídem |
| Una persona entra y ve «Página no encontrada» | su página de inicio era una pantalla de crédito retirada | §7, «Usuarios con página de inicio retirada» |
| Una integración recibe 404 en `/api/lending/zones` (u otra familia conservada) | la ruta se mudó a `/api/savings/*` | cambiar el prefijo (§9) |
| Una cooperativa nueva no está en el diagnóstico | se aprovisionó después de correrlo | repetir el diagnóstico el día de la promoción |
