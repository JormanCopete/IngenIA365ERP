# Runbook: desplegar K4 (cobranza) en Cartera

> Feature 013, fase 18 (US12, entrega K4). Para quien despliega y para el jefe de cartera de cada cooperativa. Qué hace la cobranza está en
> el manual [cartera-cobranza-y-aviso-previo.md](../manual/cartera-cobranza-y-aviso-previo.md); el procesador y las corridas, en
> [cartera-proceso-diario.md](cartera-proceso-diario.md); los canales externos, en [canales-externos.md](canales-externos.md). Contrato:
> [data-model.md](../../specs/013-cartera-financiera/data-model.md) §22.1 y §26, decisión C64.

K4 llega con la migración aditiva `CarteraCobranza` (once tablas `LND_` de cobranza y las columnas nulas `LND_DebtorMessages.CampaignId` y
`LND_LoanNovelties.PaymentAgreementId`), seguida de `CarteraCobranzaSalida` (bloque D: `CollectorUserId` de `LND_CollectionActions` y de
`LND_CollectorAssignments` pasa a nulo, para la gestión de sistema y el cierre de la asignación cuando un crédito sale de cobranza) y con `CatalogoDeCartera.EntregaVigente = K4`, que enciende el paso 7 del ciclo diario (etapas,
reparto, mensajes, aviso previo, gastos, promesas, reclamos). Al desplegar hay **tres cosas distintas**:

| | Quién la hace | Cuándo |
|---|---|---|
| Sembrar las cuatro etapas por defecto | la API, sola, al arrancar | en cada cooperativa, en cada arranque (idempotente) |
| Registrar la etapa migrada de los créditos vivos (C64) | una **corrida** por cooperativa, no la semilla | una vez, después del despliegue |
| SMS y WhatsApp reales | operaciones, por cooperativa | cuando haya proveedor (hoy **no** hay adaptador) |

## 1. Antes de desplegar

1. Respaldo de cada base de cooperativa (Principio XII), aunque la migración sea aditiva.
2. Que `CarteraCobranza` y `CarteraCobranzaSalida` estén en los dos proyectos de migraciones (PostgreSQL y SQL Server) detrás de
   `CarteraNovedades`. La segunda sólo relaja dos columnas a nulas; su `Down` las vuelve obligatorias y falla si ya hay gestiones de sistema
   o cierres de asignación (filas con `CollectorUserId` nulo): no se revierte en una base que ya corrió el ciclo con K4.
3. Que las plantillas de mensaje de Cartera ya estén sembradas (`LendingMessageTemplatesSeeder`, Order 91): las etapas citan
   `RECORDPREV`, `MORAADMIN` y `PREJURID`, y la cobranza usa `AVISOPREV` y `GASTOCOBR`.

## 2. La semilla de etapas (sola, al arrancar)

`CollectionStagesSeeder` (Order **92**, semilla paramétrica de cooperativa, archivo `Data/lending-collection-stages.json`) siembra
`PREVENTIVA` (−5 a 0), `ADMIN` (1 a 30), `PREJURID` (31 a 90) y `JURIDICA` (91+, jurídica), vigentes desde el 2000-01-01, con
`CreatedBy = system:seed`, que la pantalla muestra como «pendiente de validar».

- Si la base todavía no tiene `CarteraCobranza`, no hace nada y lo dice en el log: `[Cartera.EtapasSinTabla]`.
- Si la cooperativa ya tiene **alguna** etapa (sembrada, propia o dada de baja), no hace nada: una cooperativa que ya versionó sus etapas
  no las recibe otra vez.
- **No registra la etapa de ningún crédito.** Los créditos sin etapa de origen entran por el ciclo diario.

Verificar: `/cartera/etapas-de-cobranza` muestra la versión 1 con las cuatro etapas, o `GET /api/lending/collection-stages`.

## 3. La etapa migrada: una corrida por cooperativa (C64)

Los créditos migrados traen su etapa de origen (`LND_MigrationLoans.CollectionStageCode`). Para que la cobranza no los vea «sin etapa» el
primer día, cada crédito vivo migrado (activo o castigado) recibe **una** primera entrada `Source = Migration`, fechada al corte de su
carga:

- con la etapa cuyo código (normalizado) coincide entre las vigentes al corte;
- si el código no coincide con ninguna, con la etapa que corresponde a sus días de mora en su última posición diaria;
- si no tiene código de origen, no se registra: entra por el ciclo.

Es la corrida `LendingProcessKind.MigratedCollectionStages` (= 6) que conduce el **procesador de Cartera** dentro de
`IEjecutorEnCooperativa`, por tandas de 250 créditos enviadas por MediatR (`RegisterMigratedCollectionStagesCommand`). Hace falta Cartera
activada y el procesador encendido (`Lending:Process:Enabled`).

### 3.1 Dispararla

**Forma normal: la tarea de una sola vez.** `cartera.etapas-migradas-k4` corre una vez al día con el programador de tareas
(`Integration:ScheduledTasks:Enabled`). En cada cooperativa con Cartera activada, créditos migrados y **ninguna** corrida
`MigratedCollectionStages` que no esté fallida, deja una pedida (`Requested`) con el actor del proceso, origen
`Tarea:cartera.etapas-migradas-k4` y el motivo «Registro de la etapa de cobranza migrada al desplegar K4 (C64)». Después no vuelve a hacer
nada. No hay que hacer nada a mano: el día del despliegue, a más tardar el siguiente, queda pedida en cada cooperativa activada.

**Orden manual** (un administrador con `Lending.Processes.Run`):

```http
POST /api/lending/process-runs
Idempotency-Key: <guid>
{ "kind": "MigratedCollectionStages", "reason": "Registro de la etapa migrada al desplegar K4" }
```

Responde 202 con la corrida; si hay otra de la misma clase pedida o corriendo, `Lending.Process.AlreadyRunning`; sin `reason`, 400
«Indique el motivo del registro de la etapa migrada». La pantalla no tiene botón para esto. (Hasta el bloque D la ruta no leía `reason` y
respondía siempre 400; corregido el 2026-10-16.)

### 3.2 Saber que terminó, cooperativa por cooperativa

- Pestaña «Corridas del proceso» de `/cartera/bandeja-de-mensajes`, o `GET /api/lending/process-runs?kind=MigratedCollectionStages`
  (`Lending.Processes.View`): estado **Completed** y el avance con todos los créditos migrados recorridos. El detalle
  (`GET /api/lending/process-runs/{id}`) trae `totalsJson` (§3.4) y los errores por crédito si los hubo.
- Con SQL, en la base de la cooperativa:

```sql
-- PostgreSQL
SELECT "PublicId", "Status", "ProcessedLoans", "TotalLoans", "FinishedAt", "TotalsJson"
FROM "LND_ProcessRuns" WHERE "Kind" = 6 AND NOT "IsDeleted" ORDER BY "Id" DESC;
SELECT "StageCode", COUNT(*) FROM "LND_CollectionStageHistory" WHERE "Source" = 'Migration' GROUP BY "StageCode";

-- SQL Server
SELECT PublicId, Status, ProcessedLoans, TotalLoans, FinishedAt, TotalsJson
FROM LND_ProcessRuns WHERE Kind = 6 AND IsDeleted = 0 ORDER BY Id DESC;
SELECT StageCode, COUNT(*) FROM LND_CollectionStageHistory WHERE Source = 'Migration' GROUP BY StageCode;
```

`Status`: 0 pedida, 1 corriendo, 2 terminada, 3 terminada con errores, 4 fallida. Una fallida o con errores se reintenta con
`POST /api/lending/process-runs/{id}/retry` y motivo (vuelve a recorrer desde el principio sin duplicar); la tarea de una sola vez también
vuelve a pedir una corrida nueva si la única que hay está **fallida**.

### 3.3 Repetirla sin duplicar

Repetir es seguro: el índice único `UK_LND_CollectionStageHistory_Migration` (un registro `Migration` por crédito) y la lectura previa
impiden registrar dos veces la etapa de un crédito; una segunda corrida sólo cuenta los ya registrados (`alreadyRegistered`). La orden
manual se admite aunque haya una terminada; la tarea, en cambio, no vuelve a pedirla.

### 3.4 El evento resumen y la tabla de equivalencia

Al terminar, la corrida guarda sus totales en `TotalsJson` y emite el evento de auditoría **`Lending.CollectionStages.MigratedRegistered`**
(entidad `LendingProcessRun`, con el `PublicId` de la corrida), visible en `/admin/auditoria`. Los dos llevan:

| Campo | Qué es |
|---|---|
| `registered` | créditos con su primera entrada `Migration` |
| `matchedByDays` | de ellos, los que entraron por sus días de mora porque el código no coincidía |
| `withoutCode` | créditos sin etapa de origen (entran por el ciclo) |
| `unmatched` | con código, pero sin etapa ni por código ni por días |
| `alreadyRegistered` | ya tenían su entrada (repetición) |
| `equivalence` | la tabla **código de origen → etapa** registrada; vacío = sin etapa |

Revisar la tabla con el jefe de cartera: un código de SOLIDO que cayó «por días» o quedó sin etapa suele pedir una versión de etapas con
los códigos de la cooperativa (§5). Lo ya registrado no se corrige: el ciclo diario lo mueve a la etapa que le toca desde el primer día.

## 4. SMS y WhatsApp por cooperativa

**Hoy no hay adaptador real de SMS ni de WhatsApp** (T0952 espera que el dueño elija proveedor): esos canales salen por `SIMULADO`, que
en producción no produce efecto legal y **no es constancia del aviso previo**. Mientras tanto, en producción el aviso previo con efecto
sale por correo o por carta con guía (`/cartera/avisos-previos`, «Registrar carta…»). Cuando exista el adaptador, por cooperativa:

1. **Credencial**: `tools/scripts/crear-secreto-canales-externos.ps1 -Ambiente <amb> -Tenant <guid> -Canal <CÓDIGO> -Campos …` agrega la
   clave `{tenantPublicId}.{CÓDIGO}.json` al Secret `erp-canales-externos` sin tocar las demás ([canales-externos.md](canales-externos.md) §3).
2. **Vigencia del canal**: `/admin/canales-externos`, clase SMS o WhatsApp, el código del proveedor y la clave de la credencial.
3. **Verificar**: el botón llama `ProbarAsync` del adaptador y sella `VerifiedAt` sólo si respondió; sin archivo,
   `Core.ExternalChannel.CredentialMissing`.
4. **Plantillas aprobadas**: en `/cartera/plantillas-de-mensaje` (`Lending.MessageTemplates.Manage`) una versión de WhatsApp de cada
   plantilla de cobranza (`RECORDPREV`, `MORAADMIN`, `PREJURID`, `AVISOPREV`, `GASTOCOBR`, las de las campañas) con el
   `ProviderTemplateId` que aprobó el proveedor. Sin él, la cobranza salta WhatsApp y prueba el canal siguiente.
5. Las autorizaciones de canal de cada deudor (con evidencia) siguen mandando: sin la de WhatsApp, el mensaje queda excluido con su motivo.

## 5. Validar las etapas sembradas con la cooperativa

Con el jefe de cartera y la contadora (T0962), en `/cartera/etapas-de-cobranza`:

1. Rangos de días, acciones (sobre todo cuál etapa abre el **aviso previo**) y plantillas de cada etapa; si cambian, «Nueva versión»
   desde una fecha, con motivo. La versión nueva deja de mostrarse «pendiente de validar».
2. **Reglas de gasto** por etapa (base, tarifa, tope): la semilla no trae ninguna. El **tope de la cooperativa**
   (`Cobranza.TopeGasto.Porcentaje` y `.ValorMaximo`, bloque D) nace vacío = sin tope; si se fija, hacerlo **antes** de versionar etapas con
   gasto: una regla con tarifa mayor que el porcentaje se rechaza (`Lending.CollectionStage.FeeAboveCap`) y el gasto propuesto se recorta.
3. Parámetros: `Cobranza.ToleranciaDePromesa` (0 por defecto, pendiente de validar), `AvisoPrevio.DiasMinimos` (20) y
   `AvisoPrevio.UmbralDosComunicaciones` (vacío = límite legal), y el horario y el tope diario de la Ley 2300.
4. **Reglas de reparto** y gestores (`cartera.gestor-cobranza`, con sus oficinas). Desde el bloque D las plantillas traen al jefe aprobar
   acuerdos, cobro jurídico, campañas y avisos previos, y al gestor enviar y registrar avisos previos (con subir adjuntos); los roles que una
   cooperativa ya creó desde las plantillas **no** se actualizan solos: agregar esos permisos en Seguridad › Roles.
5. **Salida de cobranza** (bloque D): desde el despliegue, el ciclo saca de cobranza a los créditos que se pusieron al día (entrada `Exit`,
   gestión de sistema y cierre de la asignación). El primer ciclo después de desplegar puede sacar de una vez a muchos créditos que la versión
   anterior dejaba con su última etapa: es lo esperado. Para contarlos:
   `SELECT COUNT(*) FROM LND_CollectionStageHistory WHERE Source = 'Exit'` (en PostgreSQL con las comillas de los nombres).

## 6. Problemas frecuentes

| Síntoma | Causa y salida |
|---|---|
| No hay etapas después de desplegar | la base no tenía `CarteraCobranza` al arrancar (`[Cartera.EtapasSinTabla]`): aplicar la migración y reiniciar la API |
| La corrida de etapa migrada no aparece | Cartera no está activada, no hay créditos migrados, el programador de tareas está apagado o ya hay una corrida no fallida |
| La corrida queda `Requested` | el procesador está apagado (`Lending:Process:Enabled`) o no tiene el arrendamiento `lending.process`: ver [cartera-proceso-diario.md](cartera-proceso-diario.md) |
| `unmatched` mayor que cero | códigos de origen sin etapa y días de mora fuera de todos los rangos: versionar las etapas y dejar que el ciclo los ubique |
| El ciclo no mueve etapas | `EntregaVigente` menor que K4 en el despliegue, o el crédito está excluido (fallecido con reclamación, insolvencia en negociación) |
| Un crédito al día desapareció de la cartera por etapa | salió de cobranza (bloque D): su historia termina en `Exit` y tiene la gestión de sistema; vuelve a entrar solo si recae en mora |
| Un proceso jurídico sigue suspendido aunque la insolvencia terminó | es a propósito: se reanuda a mano con motivo en `/cartera/juridico` («Reanudar…») |
