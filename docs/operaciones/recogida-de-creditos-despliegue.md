# Runbook: desplegar la recogida de créditos (feature 028)

> Para quien despliega, la contadora y el administrador de Cartera de cada cooperativa. Qué hace la feature está en el manual
> [cartera-recogida-de-creditos.md](../manual/cartera-recogida-de-creditos.md); la especificación, en `specs/028-recogida-de-creditos/`.

La 028 llega con **dos migraciones aditivas** en cada base de cooperativa, sin cambios en la base administrativa:

| Migración | Qué agrega |
|---|---|
| `RecogidaDeCreditos` | Las tablas `LND_CreditApplicationPayoffs` (la recogida proyectada de cada solicitud) y `LND_DisbursementPayoffs` (la aplicada en cada desembolso). Al final del `Up`, un SQL escrito a mano copia cada `LND_CreditApplications.PayoffRequest` no vacío a filas de créditos **sin proyección** (un crédito repetido entra una vez, uno que no existe no entra). La columna `PayoffRequest` **se conserva** y deja de escribirse. |
| `CuotaDejadaALaPagaduria` | `LND_PayrollDeductionResults.LoanClosedAtResult` (falso de fábrica) y `LeftInstallmentNumber`, `LeftInstallmentAmount`, `LeftInstallmentSettledAt` en `LND_DisbursementPayoffs`. |

Las dos van en par (PostgreSQL y SQL Server), fechadas después de `EsquemaBase`. Ninguna es destructiva: no exigen el procedimiento del
Principio XII (respaldo y segundo revisor), aunque el respaldo de siempre antes de cada promoción sigue valiendo. Además, la API siembra al
arrancar el medio de pago `RECOGIDA` (clase «Recogida en el desembolso») y el parámetro del sistema
`Accounting.LendingDisbursementWithPayoffsInOneVoucher` en `false`. **Con los parámetros de fábrica nada cambia** para la cooperativa.

## 1. Antes de promover: el diagnóstico (DEV, QA y PDN)

Dos consultas de **sólo lectura** por base de cooperativa. Se corren antes de sincronizar cada ambiente y el resultado se le pasa a la contadora.

### 1.1 Desembolsos con recogidas ya contabilizados

Hasta la 028 el comprobante del desembolso (`DS`) acreditaba **el bruto** a «desembolsos por girar»; desde la 028 acredita el neto y lleva lo
recogido al puente de la recogida propia. Un `DS` viejo que se reverse después (orden rechazada, cheque no cobrado, anulación) se reversa ya con
el molde nuevo: si el puente `OwnLoanPayoff` y «desembolsos por girar» están en **cuentas distintas**, queda una diferencia entre las dos. Esta
consulta lista los desembolsos con recogidas, si su `DS` ya está contabilizado, el estado de sus órdenes y si ya se reversaron.

**Estados de la orden**: 1 emitida, 2 girada, 3 rechazada, 4 cheque no cobrado, 5 anulada. Los que importan son los contabilizados con una orden
todavía **emitida (1)**: son los que pueden reversarse después del despliegue.

PostgreSQL:

```sql
SELECT d."Number"      AS desembolso,
       l."Number"      AS credito_nuevo,
       d."ValueDate"   AS fecha_valor,
       d."Amount"      AS bruto,
       r.recogidas,
       r.recogido,
       r.reversadas,
       EXISTS (SELECT 1 FROM dbo."ACC_LendingPostings" ap
               WHERE ap."MessageType" = 'CreditoDesembolsado' AND ap."SourcePublicId" = d."PublicId"
                 AND ap."AccountingDocumentId" IS NOT NULL)                AS ds_contabilizado,
       (SELECT string_agg(o."Number" || '=' || o."Status", ', ' ORDER BY o."Id")
          FROM dbo."LND_DisbursementOrders" o WHERE o."DisbursementOperationId" = d."Id") AS ordenes,
       EXISTS (SELECT 1 FROM dbo."LND_LoanOperations" v
               WHERE v."Kind" = 5 AND v."ReversesOperationId" = d."Id")    AS desembolso_reversado
FROM dbo."LND_LoanOperations" d
JOIN dbo."LND_Loans" l ON l."Id" = d."LoanId"
CROSS JOIN LATERAL (
    SELECT count(*)                                                       AS recogidas,
           sum(p."Amount" - COALESCE(p."ExcessAmount", 0))                AS recogido,
           count(*) FILTER (WHERE p."ReversedByOperationId" IS NOT NULL)  AS reversadas
    FROM dbo."LND_LoanOperations" p
    WHERE p."Kind" = 3 AND p."CollectorCode" = 'CAR'
      AND p."IdempotencyKey" LIKE 'CAR:' || replace(d."PublicId"::text, '-', '') || ':%'
) r
WHERE d."Kind" = 1 AND r.recogidas > 0
ORDER BY d."ValueDate", d."Number";
```

SQL Server:

```sql
SELECT d.Number      AS desembolso,
       l.Number      AS credito_nuevo,
       d.ValueDate   AS fecha_valor,
       d.Amount      AS bruto,
       r.recogidas,
       r.recogido,
       r.reversadas,
       CASE WHEN EXISTS (SELECT 1 FROM dbo.ACC_LendingPostings ap
                         WHERE ap.MessageType = 'CreditoDesembolsado' AND ap.SourcePublicId = d.PublicId
                           AND ap.AccountingDocumentId IS NOT NULL) THEN 1 ELSE 0 END AS ds_contabilizado,
       (SELECT STRING_AGG(CONCAT(o.Number, '=', o.Status), ', ') WITHIN GROUP (ORDER BY o.Id)
          FROM dbo.LND_DisbursementOrders o WHERE o.DisbursementOperationId = d.Id)      AS ordenes,
       CASE WHEN EXISTS (SELECT 1 FROM dbo.LND_LoanOperations v
                         WHERE v.Kind = 5 AND v.ReversesOperationId = d.Id) THEN 1 ELSE 0 END AS desembolso_reversado
FROM dbo.LND_LoanOperations d
JOIN dbo.LND_Loans l ON l.Id = d.LoanId
CROSS APPLY (
    SELECT COUNT(*)                                                         AS recogidas,
           SUM(p.Amount - COALESCE(p.ExcessAmount, 0))                      AS recogido,
           SUM(CASE WHEN p.ReversedByOperationId IS NOT NULL THEN 1 ELSE 0 END) AS reversadas
    FROM dbo.LND_LoanOperations p
    WHERE p.Kind = 3 AND p.CollectorCode = 'CAR'
      AND p.IdempotencyKey LIKE 'CAR:' + REPLACE(LOWER(CONVERT(char(36), d.PublicId)), '-', '') + ':%'
) r
WHERE d.Kind = 1 AND r.recogidas > 0
ORDER BY d.ValueDate, d.Number;
```

**Qué hacer con el resultado**:

- **Sin filas**: nada que hacer.
- **Con filas y el puente en la misma cuenta que «desembolsos por girar»** (lo que hace hoy COOFLOPAL): nada que hacer; el reverso cuadra igual.
- **Con filas, cuentas distintas y alguna orden todavía emitida**: la contadora decide antes de promover. O deja el puente en la misma cuenta
  hasta que esas órdenes queden giradas, o, si una se reversa, reclasifica la diferencia con un comprobante manual `CG`.
- **Si la contadora cambia la regla del puente** después del despliegue, la vigencia nueva debe empezar **después de la fecha valor del último
  desembolso con recogidas ya contabilizado** de esta lista.

### 1.2 Solicitudes que ya autorizaban recoger créditos

La migración copia estas solicitudes a filas **sin proyección**. Lo que conviene saber de cada una:

- las **aprobadas** (5) se desembolsan sin comparación con lo proyectado: no piden reconocer diferencias por esas recogidas;
- las **radicadas o en decisión** (2, 3): la huella del enrutamiento cambia una vez al desplegar; quien tenga una abierta para decidir recibe
  `Lending.Decision.ContentChanged` y tiene que releerla;
- los **borradores y las devueltas** (1, 4) se pueden corregir con la pantalla nueva.

**Estados**: 1 borrador, 2 radicada, 3 en decisión, 4 devuelta, 5 aprobada, 6 negada, 7 desembolsada, 8 otorgada, 9 anulada.

PostgreSQL:

```sql
SELECT s."Number" AS solicitud, s."Status" AS estado, s."ExpectedDisbursementDate" AS fecha_esperada, s."PayoffRequest"
FROM dbo."LND_CreditApplications" s
WHERE s."IsDeleted" = FALSE AND s."PayoffRequest" IS NOT NULL AND btrim(s."PayoffRequest") NOT IN ('', '[]')
ORDER BY s."Status", s."Number";
```

SQL Server:

```sql
SELECT s.Number AS solicitud, s.Status AS estado, s.ExpectedDisbursementDate AS fecha_esperada, s.PayoffRequest
FROM dbo.LND_CreditApplications s
WHERE s.IsDeleted = 0 AND s.PayoffRequest IS NOT NULL AND LTRIM(RTRIM(s.PayoffRequest)) NOT IN ('', '[]')
ORDER BY s.Status, s.Number;
```

Anote cuántas hay por estado: la verificación (§3) compara contra este número.

### 1.3 Cancelaciones que usaron el anticipo, ya contabilizadas (enmienda E-17)

Este defecto **no es de la 028**: viene de la 013 y afecta también a la caja. La 028 lo corrige hacia adelante. Cuando una cancelación usaba el
anticipo del crédito, el comprobante (`RC`, `RR` o el del recibo) lo asentaba como un anticipo **recibido**. Pasaba en la caja con F7, en un pago que
cancela y en una recogida. El resultado: el puente del recaudo quedaba con **dos veces el anticipo usado** de más, y el anticipo (el pasivo con el
deudor) no bajaba.

Desde la 028 el comprobante debita el anticipo y acredita el puente. Lo ya contabilizado **no se corrige solo**: lo ajusta la contadora con un
comprobante manual por cada fila de esta consulta, por dos veces el anticipo usado (débito al puente, crédito al anticipo, con el deudor como tercero).
El valor del anticipo usado está en el comprobante (la línea del anticipo) y en el mensaje (`amounts` con `reasonCode = AdvanceUsed`).

PostgreSQL:

```sql
SELECT ap."LoanNumber"            AS credito,
       ap."OperationDate"         AS fecha,
       ap."SourceDocumentTypeCode" AS tipo,
       ap."SourceDocumentNumber"  AS documento,
       ap."AccountingDocumentId"  AS comprobante_id,
       m."PublicId"               AS mensaje
FROM dbo."ACC_LendingPostings" ap
JOIN dbo."COR_IntegrationMessages" m ON m."PublicId" = ap."MessagePublicId"
WHERE ap."MessageType" = 'PagoAplicado' AND ap."AccountingDocumentId" IS NOT NULL
  AND m."PayloadJson" LIKE '%AdvanceUsed%'
ORDER BY ap."OperationDate", ap."LoanNumber";
```

SQL Server:

```sql
SELECT ap.LoanNumber AS credito, ap.OperationDate AS fecha, ap.SourceDocumentTypeCode AS tipo, ap.SourceDocumentNumber AS documento,
       ap.AccountingDocumentId AS comprobante_id, m.PublicId AS mensaje
FROM dbo.ACC_LendingPostings ap
JOIN dbo.COR_IntegrationMessages m ON m.PublicId = ap.MessagePublicId
WHERE ap.MessageType = 'PagoAplicado' AND ap.AccountingDocumentId IS NOT NULL
  AND m.PayloadJson LIKE '%AdvanceUsed%'
ORDER BY ap.OperationDate, ap.LoanNumber;
```

Si un pago así **se reversa después** del despliegue, el reverso queda bien (ya invertía el signo del anticipo). El ajuste de la contadora sigue
haciendo falta, porque corrige el comprobante original.

## 2. Después de desplegar: qué configurar (por cooperativa)

Nada es obligatorio: de fábrica la recogida de créditos funciona como antes, ahora con pantalla. Lo que cada cooperativa decide:

1. **Los parámetros de Cartera** (`/cartera/parametros`), con su vigencia; la tabla completa con su pregunta y su defecto está en el manual, §12.
   - `Recogida.CuotaCausada` y `Pagos.CuotaEnviadaAlCancelar` en las líneas que se pagan por libranza o nómina propia.
   - `Recogida.GiroPosteriorDeCreditoRecogido`. Para COOFLOPAL la recomendada es `AlCreditoNuevo` (decisión D3).
   - `Recogida.InteresesProporcionales`, `Recogida.AlDesembolsar` y `Recogida.ToleranciaDeDiferencia`, si se quiere algo distinto de lo de
     siempre.
   - `Recogida.ProductosAdmitidos`, si se recogen aportes, ahorros o facturas.
2. **El grupo «Créditos que recoge»** en `Originacion.GruposEditablesAlDevolver`, si se quiere que no se pueda cambiar al corregir (de fábrica
   se puede).
3. **Si se recogen facturas a crédito de Comercial**:
   - crear en Recaudo → Configuración → Tipos de documento un tipo **activo** de la clase **«Recogida en el desembolso»** (no se siembra);
   - comprobar que el medio `RECOGIDA` está activo en Medios de pago (lo deja la semilla);
   - en la matriz de Recaudo (`/contabilidad/recaudo/matriz`), el rol **«Recogida en el desembolso»** de «Recibo de caja» y «Recibo anulado» con
     la **misma cuenta** que el puente `RecaudoPorAplicar`/`OwnLoanPayoff` de Cartera, y el cruce del recibo con tipo `FV`.
4. **La matriz de Cartera (la contadora)**:
   - la regla `RecaudoPorAplicar` con la clase `OwnLoanPayoff` ya se pedía; ahora puede quedarse en la cuenta de «desembolsos por girar» o pasar a
     una cuenta puente propia (ver §1.1 antes de cambiarla);
   - si se recogen cuentas, los mapeos de `AporteRecibido` y `ConsignacionEnCuentaAplicada` con la clase `OwnLoanPayoff`;
   - dejar vacía `/contabilidad/cartera/completitud`, sin el aviso `Accounting.LendingRule.InvoicePayoffBridgeMismatch`.
5. **Un solo comprobante** (opcional, la contadora): `Accounting.LendingDisbursementWithPayoffsInOneVoucher` en `true` en Administración →
   Parámetros del sistema (módulo `ACC`).
6. **Permisos**: no hay nuevos. El asesor necesita `Lending.Payoffs.Read` para ver la tabla de productos en la solicitud.

## 3. Verificar

1. **Las migraciones**, en cada base:

   ```sql
   SELECT "MigrationId" FROM dbo."__EFMigrationsHistory"
   WHERE "MigrationId" LIKE '%RecogidaDeCreditos' OR "MigrationId" LIKE '%CuotaDejadaALaPagaduria';
   ```

   (en SQL Server, sin comillas dobles). Deben salir las dos. `DbMigrator consolidacion` sigue diciendo el estado de cada base.
2. **La copia de las solicitudes**: cuántas solicitudes vivas tienen filas copiadas sin proyección; debe coincidir con el número de §1.2 (salvo
   los créditos que ya no existían).

   ```sql
   SELECT count(DISTINCT "CreditApplicationId") FROM dbo."LND_CreditApplicationPayoffs"
   WHERE "IsDeleted" = FALSE AND "CreatedBy" = 'migracion:RecogidaDeCreditos';
   ```

3. **La semilla del medio**: `SELECT "Code", "Class", "IsActive" FROM dbo."COR_PaymentMeans" WHERE "Code" = 'RECOGIDA';` → clase 22, activo.
4. **El parámetro del sistema**: `SELECT "SettingValue" FROM dbo."COR_SystemSettings" WHERE "SettingKey" =
   'Accounting.LendingDisbursementWithPayoffsInOneVoucher';` → `false` (o lo que haya fijado la contadora).
5. **A mano, en DEV o QA** (la guía rápida de `specs/028-recogida-de-creditos/quickstart.md`):
   - solicitud nueva de un asociado con dos créditos al día y uno en mora: la zona «Productos que recoge» lista los tres y el de mora no se marca;
   - radicar, decidir, pagar una cuota del crédito por caja y desembolsar: la vista previa muestra la diferencia con la causa «Pagos recibidos» y
     pide «Revisé las diferencias»;
   - el crédito viejo queda cancelado con «Recogido por el crédito …», la orden de giro es por el neto mostrado y el `DS` lleva «desembolsos por
     girar» por el neto con el puente en cero;
   - `/cartera/informes?vista=payoffs` lista el desembolso y se exporta.
6. **La conciliación** (`/cartera/conciliacion-contable`): «desembolsos por girar» igual a las órdenes emitidas y el puente `OwnLoanPayoff` en cero
   (salvo recogidas que esperan un giro nuevo).

## 4. Si hay que volver atrás

Las dos migraciones son aditivas. Su `Down` borra las tablas y columnas nuevas, y con ellas las proyecciones guardadas, las recogidas aplicadas
y la marca de los resultados de libranza sobre créditos cerrados. `PayoffRequest` sigue ahí, pero **ya no se escribió** desde el despliegue: una
solicitud radicada después con recogidas las perdería al bajar. No se baja una base que ya desembolsó con recogidas; se corrige hacia adelante.
