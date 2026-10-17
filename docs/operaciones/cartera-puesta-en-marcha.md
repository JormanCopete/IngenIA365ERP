# Runbook: la puesta en marcha de Cartera — migrar la cartera vigente y activar

> Feature 013, fase 13 (US8, K1). Para el jefe de cartera, la contadora y quien opera la salida en vivo. Molde:
> [inventario-puesta-en-marcha.md](inventario-puesta-en-marcha.md). Recorrido de ensayo:
> [quickstart.md](../../specs/013-cartera-financiera/quickstart.md) §3.3, §3.4 y §11. Las columnas de cada plantilla están en
> [contracts/plantillas.md](../../specs/013-cartera-financiera/contracts/plantillas.md) §5 y §6; la mecánica común, en
> [plantillas-de-importacion.md](../manual/plantillas-de-importacion.md). El retiro de lo heredado tiene su propio runbook:
> [cartera-retiro-heredado.md](cartera-retiro-heredado.md).

La cooperativa trae su cartera **con fecha de corte C** (el último día que la manejó el sistema anterior) en una **carga** de 17 partes.
Aplicar la carga **sólo la guarda**: valida entre partes, cuadra contra Contabilidad y concilia las ventas a crédito de Comercial. Los
créditos, sus saldos iniciales, el cierre base y todo lo del deudor **nacen al activar**, con fecha **C+1**, en una sola transacción
(todo o nada). Desde ese día Cartera es el sistema de registro de los créditos; no hay sincronización con el sistema anterior.

## 1. Lo que debe existir antes

Nada de esto tiene plantilla de la carga (plantillas.md §0.2). La pantalla `/cartera/activacion` lista cada requisito con su estado y
quién lo arregla; en producción, lo que falta **bloquea**; fuera de producción algunos aparecen como «falta (no bloquea)».

| Qué | Dónde | Por qué |
|---|---|---|
| Retiro de la cartera heredada hecho, con respaldo y segundo revisor | [cartera-retiro-heredado.md](cartera-retiro-heredado.md) | Las tablas `LND_` nuevas reemplazan a las heredadas (Principio XII) |
| Personas: deudores, codeudores, aseguradoras, pagadurías, fondos, abogados | Maestros › Personas | **Ninguna plantilla crea personas**: un documento que no está es un error de fila y columna |
| Valores nacionales al día (usura por modalidad, IBR, `SMMLV`, `UVT`, festivos) y el **Anexo 1** vigente, revisados por la contadora | `/admin/valores-nacionales` | La usura de cada línea activa y el Anexo 1 son requisitos (`UsuryForActiveLines`, `Annex1Current`) |
| Perfil regulatorio de la cooperativa | `/admin/perfil-regulatorio` | Si es **obligada** a la pérdida esperada, necesita K6a y el historial de mora completo |
| Roles de Cartera desde las plantillas | `POST /api/lending/activation/suggested-roles` (`CreateSuggestedLendingRolesCommand`) | `LendingRolesCreated` |
| Alcance de oficinas y líneas de cada usuario, y su persona (`SEC_Users.PersonId`) | `/cartera/alcances`, Administración › Usuarios | Quien carga, acepta, decide o vota necesita persona; el aviso `UsersWithoutScope` lista a los que no tienen alcance |
| Órganos de gobierno con sus miembros | `/cartera/actas` | La aceptación de diferencias y la activación citan un **punto de acta cerrada** |
| Oficinas vinculadas a su sucursal contable | Maestros › Agencias | `OfficesLinkedToAccountingBranches`; la plantilla rechaza una oficina sin vincular |
| Contabilidad iniciada, con la **apertura al corte** | `/contabilidad/apertura` | El cuadre compara contra los saldos contables a C; sin contabilidad, `Lending.Accounting.NotStarted` |
| Matriz contable de Cartera con la **completitud vacía** | `/contabilidad/cartera/matriz`, `/contabilidad/cartera/completitud` | Sin reglas, el cuadre sale «sin regla en la matriz» y todo es diferencia; y como `Contabilidad.FaltaDeRegla` arranca en **Bloquear** (decisión del dueño del 2026-10-14), con la contabilidad iniciada nada de lo que falte en la matriz se confirma: ni el desembolso, ni los pagos, ni la nómina, ni la causación. Para operar mientras se completa, fijar `Advertir` con su vigencia en `/cartera/parametros` |
| La regla `RecaudoPorAplicar` con clase de recaudador `Payroll` | Matriz contable | `PayrollBridgeRuleResolved` |
| `DESC_CARTERA` con cuentas en Nómina | `/nomina/conceptos` | Aviso `PayrollConceptAccounts`; la pantalla de activación lo pregunta a Nómina |
| Patrimonio técnico vigente | `/contabilidad/indicadores-regulatorios` (`ACC_RegulatoryIndicators`) | Sin él, el límite de concentración bloquea desembolsos y cupos |
| **Recaudo y Tesorería reales** registrados | `/admin/participantes-de-integracion` | En producción, `ReceiptsCollectorRegistered` y `TreasuryDestinationRegistered` sin dobles de prueba |

Comprobación del registro de participantes (PostgreSQL / SQL Server):

```sql
SELECT "Code", "Class", "Role", "Destination", "IsEnabled", "ValidTo" FROM dbo."COR_IntegrationParticipants" ORDER BY "Code";
SELECT [Code], [Class], [Role], [Destination], [IsEnabled], [ValidTo] FROM [dbo].[COR_IntegrationParticipants] ORDER BY [Code];
```

En producción no debe quedar ninguna fila con `Class = Test` vigente.

## 2. Las plantillas, en orden

El orden de carga de una cooperativa que inicia es el de plantillas.md §0.1:

1. Lo de §1, que no tiene plantilla.
2. Líneas de crédito (`/cartera/lineas`) y matriz contable, con la contadora, hasta dejar vacía la completitud.
3. Vinculados, saldos de aportes y listas restrictivas, en cualquier orden.
4. **La carga de migración** (§3): crear la carga con su corte, importar las partes en el orden de §5.2, revisar y aplicar.
5. Aceptar con acta las diferencias del cuadre y de la conciliación de ventas (§4).
6. Activar (§6), con el archivo de cobros provisionales (§5) si los hay.

## 3. La carga por partes

`/cartera/migracion` → «Carga nueva» con la **fecha de corte**. Un borrador anterior queda reemplazado.

Por parte (elegirla en la lista): «Plantilla vacía» (encabezados en la fila 1; instrucciones en otra hoja; admite `.csv` con «;»),
diligenciar, **Revisar** (no guarda nada; errores con fila y columna; «Revisión en Excel» devuelve el mismo libro con el resultado de cada
fila) y **Aplicar** (todo o nada para ese archivo). Las obligatorias son créditos, cuotas, saldos y mora/calificación/deterioro; las demás
según lo que traiga la cooperativa (plantillas.md §5.2).

- **Varios archivos por parte.** Una parte grande (las cuotas de miles de créditos) se sube en varios archivos: cada uno es una tanda.
  El mismo archivo otra vez responde «sin cambio»; una llave repetida en otra tanda es `Import.Row.Duplicate` y nombra la tanda.
- **Reemplazar una parte entera**: marcar «Reemplazar la parte» y subir el primer archivo; las tandas anteriores quedan `Superseded`
  (sus filas se conservan y dejan de contar). Después se suben los demás archivos sin la marca.
- Las referencias a otras partes que todavía no se cargaron son **avisos** al importar y **errores** al aplicar.

**Aplicar la carga** («Aplicar la carga…», con motivo): valida entre partes —el plan suma el capital, cada crédito tiene cuotas, saldos y
riesgo, las ventas migradas se corresponden con su crédito, los cupos no se cruzan, las personas siguen vivas— y con un error **no guarda
nada** y responde `Lending.Migration.CrossPartErrors` con la parte, la tanda, la fila y la columna. Si aplica, calcula el cuadre y la
conciliación de ventas, deja la carga `Applied` con su **plazo de activación** y emite `CargaDeCarteraAplicada`. Otra carga aplicada después
(con el mismo corte o uno nuevo) deja la anterior `Replaced`.

### La carga de ensayo en producción, sin activar

Se puede aplicar en producción una carga con un corte anterior para validar personas, cuadre y conciliación sin activar (quickstart
§11.3). **Mientras haya una carga aplicada sin activar**:

- cada nómina quincenal y cada liquidación definitiva con deudores de la carga exige reconocer «Cartera por activar»
  (`Payroll.LendingPendingActivationAckRequired`);
- Personas no deja borrar a sus deudores ni codeudores (`Person.HasActiveLoans`) y su detalle dice «migrada, sin activar»;
- Contabilidad avisa al cerrar el mes que contiene C+1 (`Accounting.Period.LendingLoadWillExpire`): cerrarlo ya no impide activar,
  pero lo que emita la activación quedará en el primer mes abierto siguiente.

Coordinarlo con Nómina y Contabilidad antes de aplicarla. La carga real la deja `Replaced`.

## 4. Cuadre, conciliación y aceptación por acta

- **Cuadre** (pestaña «Cuadre», `Lending.Reconciliation.View`): por concepto, modalidad, sub-segmento, categoría y conjunto de cuentas,
  lo de Cartera frente a lo contable a C. No trae documentos ni nombres.
- **Ventas** (pestaña «Ventas»): las ventas a crédito de Comercial hasta el corte, que esperan en la bandeja **sin consumirse**:
  migrada, cancelada antes del corte, no migrada con saldo. Una venta de servicios a asociados confirmada antes del crédito sin Cartera se
  marca desde la lista «fuera de Cartera» con motivo (D29); una pagada antes del corte que la parte de ventas no trae, «cancelada antes
  del corte».
- **Diferencias** (pestaña «Diferencias», `Lending.Migration.AcceptDifferences`): se eligen varias y se aceptan con motivo y el **punto
  de una sesión cerrada** del órgano que las aprueba (el sujeto «Diferencias de la activación» de esa carga). Con el par de segregación
  `CargarMigracion × AceptarDiferencias` activo, quien cargó no acepta.

## 5. Cobros provisionales

Si después del corte se recaudaron ventas a crédito confirmadas después del corte (crédito provisional de Comercial), en
`/cartera/activacion` → «Cobros provisionales»: plantilla vacía, subir y **Revisar**. La revisión valida contra esas ventas (no contra las de
la migración), deja el archivo como adjunto y no guarda filas; la activación lo vuelve a validar y **guarda** los cobros, que el consumidor
de Comercial aplica al nacer la obligación de cada venta. Errores: `Import.Invalid` con fila y columna, o
`Lending.ProvisionalCollection.ExceedsSale` si los cobros de una factura superan su valor a crédito.

## 6. La activación

`/cartera/activacion` (`Lending.Activation.Execute`):

1. **Requisitos** sin faltantes (en producción) y **avisos** revisados: lo que Comercial ajusta el mismo día (§7), las cuentas de
   `DESC_CARTERA` en Nómina (casilla de reconocimiento) y los usuarios sin alcance.
2. **La fecha es C+1** y no se edita. Se registra **dentro del plazo** (`Pagos.DiasMaximosFechaValor` días desde C; vencido,
   `Lending.Activation.DeadlinePassed`). Con la contabilidad iniciada (si no, falta `AccountingMonthOpen`). Si Contabilidad ya cerró el
   mes de C+1, **no limita, sólo informa** (respuesta del dueño del 2026-10-14): sale el aviso `AccountingMonthClosed` en los avisos y en
   la respuesta, la activación procede y lo que emite (`CarteraActivada`) queda fechado en el primer mes abierto —la fecha de activación
   sigue siendo C+1—.
3. **El acta de activación** (C61) es obligatoria: el punto de una sesión cerrada sobre la carga. Declara que el consejo conoce la carga,
   su corte, el cuadre y las diferencias aceptadas, los cobros provisionales y que la cuota puede diferir en centavos porque el interés lo
   causa el motor desde el corte.
4. **Activar**. Antes, la activación **recalcula** el cuadre y la conciliación con los saldos contables de ese momento: si cambiaron, deja
   una versión nueva del cuadre y responde `DifferencesNotAccepted` (hay que volver a aceptar). Si todo está en orden nacen, en una
   transacción guardada por tandas de 1.000 créditos: los créditos con su número de origen, sus condiciones, su plan, su control y su saldo
   inicial por concepto (operación `OpeningBalance`, **sin hechos contables**: los saldos ya están en la apertura contable); los castigados;
   codeudores, garantías y pagarés; el **cierre base**; cupos (los que exceden quedan «por revalidar»), bloqueos, autorizaciones, avisos
   previos, reclamos y vinculados (lo registrado a mano prevalece); las ventas enlazadas y las obligaciones en cero de las canceladas; los no
   aplicados de nómina; los cobros provisionales. Se habilita el destino `Lending` y se emite `CarteraActivada`.

Errores: `CutoffInvalid` (la fecha no es C+1), `DeadlinePassed`, `DifferencesNotAccepted`, `RequirementsMissing` (con la lista y quién
corrige cada uno), `AlreadyActivated`, `Lending.Session.ItemMismatch` (el punto no es de esa carga), `Import.Invalid` y `ExceedsSale` (cobros).

### Verificación (PostgreSQL)

```sql
SELECT "ActivationDate", "ActivationId" FROM dbo."LND_Setup";
SELECT "Number", "CutoffDate", "Status" FROM dbo."LND_MigrationLoads" ORDER BY "Number";             -- la activada en 4
SELECT "ActivationDate", "CheckVersion", "CountsJson", "ProvisionalCollectionsCount" FROM dbo."LND_Activations";
SELECT COUNT(*) FROM dbo."LND_Loans" WHERE "Origin" = 3;                                            -- créditos migrados
SELECT COUNT(*) FROM dbo."LND_LoanOperations" WHERE "Kind" = 2;                                     -- saldos iniciales
SELECT "Kind", "Year", "Month" FROM dbo."LND_MonthlyCloses" WHERE "Kind" = 1;                       -- el cierre base
SELECT "Code", "IsEnabled" FROM dbo."COR_IntegrationParticipants" WHERE "Code" = 'CAR';             -- destino Lending habilitado
SELECT "Type", COUNT(*) FROM dbo."COR_IntegrationMessages"
 WHERE "Type" IN ('CargaDeCarteraAplicada', 'CarteraActivada', 'CreditoDesembolsado') GROUP BY "Type"; -- ningún CreditoDesembolsado
```

### Verificación (SQL Server)

```sql
SELECT [ActivationDate], [ActivationId] FROM [dbo].[LND_Setup];
SELECT [Number], [CutoffDate], [Status] FROM [dbo].[LND_MigrationLoads] ORDER BY [Number];
SELECT [ActivationDate], [CheckVersion], [CountsJson], [ProvisionalCollectionsCount] FROM [dbo].[LND_Activations];
SELECT COUNT(*) FROM [dbo].[LND_Loans] WHERE [Origin] = 3;
SELECT COUNT(*) FROM [dbo].[LND_LoanOperations] WHERE [Kind] = 2;
SELECT [Kind], [Year], [Month] FROM [dbo].[LND_MonthlyCloses] WHERE [Kind] = 1;
SELECT [Code], [IsEnabled] FROM [dbo].[COR_IntegrationParticipants] WHERE [Code] = 'CAR';
SELECT [Type], COUNT(*) FROM [dbo].[COR_IntegrationMessages]
 WHERE [Type] IN ('CargaDeCarteraAplicada', 'CarteraActivada', 'CreditoDesembolsado') GROUP BY [Type];
```

Y por la API: la liquidación de una muestra de créditos a C (`GET /api/lending/loans/{id}/settlement?date=C`) es lo cargado.

## 7. Lo que Comercial ajusta el mismo día

- `Cartera.IntegracionHabilitadaDesde` = la fecha de activación.
- `Cartera.PoliticaSinRespuesta` revisada (qué hace el POS si Cartera no responde).
- `Cartera.CuentaPorCobrarRegistradaPor` fijada (desde ese día la cuenta por cobrar la registra Cartera).

Entre C+1 y el registro de la activación, Recaudo conserva los pagos que el contrato rechaza con «Cartera no activada» y los aplica después
con su fecha valor; un pago con fecha valor igual o anterior a C responde `Lending.Contract.BeforeCutoff`. Una venta fechada hasta C que llega
después de activar queda fuera de Cartera con la alerta `Cartera.VentaNoIncorporada`.

## 8. Los primeros días y el primer cierre

- Corridas diarias completas (`/cartera/bandeja-de-mensajes`, corridas), alertas de vigilancia, la bandeja sin rechazos sin explicar y la
  conciliación contable diaria (`/cartera/conciliacion-contable`).
- El despachador entrega las ventas acumuladas: Comercial recibe `ObligacionCreada` (migrada, o en cero si se canceló antes del corte) u
  `ObligacionRechazada` no reprocesable (la no migrada aceptada y la de fuera de Cartera). Los cobros provisionales se aplican al nacer
  cada obligación; los que no encuentran su venta quedan como «cobros sin obligación».
- **El primer cierre mensual** es el del mes que contiene la activación, contra el **cierre base**: emite sólo la diferencia
  ([cartera-cierre-mensual.md](cartera-cierre-mensual.md)). Después cierra Contabilidad.

## 9. Si falla

- **La activación es todo o nada**: si algo falla (una tanda, el cierre base, los cobros), no queda nada —ningún crédito, ningún cierre base,
  el destino `Lending` inactivo y la carga `Applied`—. Corregir la causa (en la carga: otra carga con el mismo corte, o reemplazar la parte) y
  volver a activar. La misma `Idempotency-Key` con el mismo cuerpo devuelve la misma activación.
- **Con el plazo vencido** (`DeadlinePassed`): pedir una carga con **corte nuevo**, aplicarla (la anterior queda `Replaced`), volver a
  cuadrar y aceptar, y activar en su C+1. El mes de C+1 cerrado en Contabilidad ya no obliga a esto: es un aviso.
- **Diferencias que cambiaron** al activar (`DifferencesNotAccepted` después de aceptar): alguien contabilizó en Contabilidad entre la
  aceptación y la activación; revisar la versión nueva del cuadre y volver a aceptar con acta.
- Un requisito de producción que no se cumple: la lista de `RequirementsMissing` dice quién lo corrige y en qué pantalla.

## 10. El ensayo general en QA y las condiciones de salida (quickstart §11)

> Fase 24 (T1073). La fuente es `specs/013-cartera-financiera/quickstart.md` §11; aquí va lo que el operador sigue. Todo con autorización
> expresa del dueño, en las fechas que él defina y con la evidencia de cada paso en las notas de release.

**El ensayo general en QA** (§11.2), antes de tocar producción:

1. Una cooperativa de QA con la parametrización real: líneas, matriz de la contadora con la completitud vacía, atribuciones, órganos con sus
   miembros, vinculados, plantillas de mensaje validadas, roles, alcances y perfil regulatorio.
2. Una contabilidad de ensayo con la apertura a un corte pasado y la carga de migración real a ese corte (las personas se registran antes).
3. El recorrido completo (quickstart §3) con los dobles de prueba hasta el **primer cierre mensual con la conciliación en cero**.
4. La comparación con cifras de referencia de una muestra por línea (saldo por concepto, cuota, días de mora, calificación), cada
   diferencia explicada y aprobada por el jefe de cartera.

**La salida** (§11.4) sigue este runbook (§1 a §8) en el orden: retiro con respaldo y segundo revisor
([cartera-retiro-heredado.md](cartera-retiro-heredado.md)) → valores nacionales al día ([valores-nacionales.md](valores-nacionales.md)) →
perfil regulatorio → seguridad → contabilidad → Comercial → corte C → carga real → cuadre y conciliación → activación C+1 → Comercial el
mismo día → primeros días → primer cierre.

**Las doce condiciones de salida** (§11.5): no se activa en producción si falta alguna.

| # | Condición | Se comprueba con |
|---|---|---|
| 1 | **Recaudo y Tesorería reales** registrados con versiones vigentes; ningún doble de prueba | `/admin/participantes-de-integracion`; la activación lo exige (`Lending.Activation.RequirementsMissing`) |
| 2 | Pérdida esperada (K6a) sólo si el perfil la hace obligada | perfil y selector; la activación lo exige |
| 3 | Retiro con diagnóstico, respaldo, segundo revisor y aprobación por base | [cartera-retiro-heredado.md](cartera-retiro-heredado.md) |
| 4 | Anexo 1 vigente y usura de las modalidades de las líneas activas | la activación lo exige |
| 5 | Cuadre en cero o aceptado con acta; conciliación de ventas resuelta | `/cartera/migracion` (cuadre y ventas) |
| 6 | Toda oficina con créditos vinculada a una sucursal contable | la activación lo exige |
| 7 | Roles, alcances y personas vinculadas a los usuarios que deciden | `/cartera/activacion` |
| 8 | Matriz completa, recaudo de Nómina resuelto, `DESC_CARTERA` con cuentas, patrimonio técnico vigente | `/contabilidad/cartera/completitud` |
| 9 | Líneas de origen comercial con el concepto tributario del interés | la línea no se activa sin él |
| 10 | Parámetros legales y plantillas de mensaje validados por la contadora y el oficial | parámetros sin «pendiente de validar» en lo que se usa |
| 11 | Ensayo general terminado con el primer cierre en cero y las diferencias aprobadas | acta del ensayo |
| 12 | QA por rol, pasos de SC-009 y la app MAUI probados a mano | notas de release |

## 11. Respaldo por entrega al promover a producción

Cada entrega se promueve con su `pg_dump -Fc` de **cada base** (la administrativa y la de cada cooperativa) antes de aplicar sus
migraciones: `<base>-AAAAMMDD-pre-f013.dump` para K1 (que trae la destructiva `RetiroDeLaCarteraHeredada`) y
`<base>-AAAAMMDD-pre-f013-kN.dump` para K2 a K6b (aditivas: `CarteraLibranza`, `CarteraNovedades`, `CarteraCobranza` y
`CarteraCobranzaSalida`, `CarteraInformes` y `CarteraComprobanteDeCargue`, `CarteraPerdidaEsperada`, `CarteraVivienda`). La verificación es **contra la base**
(tablas de la migración, permisos `Lending.*` sembrados por la API al arrancar, `/health/ready`, réplica de valores nacionales al día), no
contra el repositorio. El estado de cada entrega por ambiente se anota en [estado-y-pendientes.md](estado-y-pendientes.md).
