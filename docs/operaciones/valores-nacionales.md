# Valores nacionales: registro central, réplica y proyección de Nómina

> Feature 013 (cartera financiera), fase 2, T0099. Contrato: [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md)
> §21.3 y §21.5, [data-model.md](../../specs/013-cartera-financiera/data-model.md) parte 3 §3, §4.1–§4.4, §7.5 y §12.3–§12.5,
> [decisiones-transversales.md](../../specs/013-cartera-financiera/decisiones-transversales.md) §2.21, §2.25 y §7.6 (T37–T42, T93, C69,
> C82, C83). Esto es la receta de operación; aquello es el contrato.

## 1. Qué son y dónde viven

Los valores que fija la ley o publica una autoridad y que son iguales para todas las cooperativas: salario mínimo (`SMMLV`), `UVT`,
tasa de usura por modalidad (`USURA`), `IBR`, `IPC`, festivos (`FESTIVO`) y los 23 límites legales (`LIMITE`: aviso previo, horario
de cobranza, capacidad de descuento de libranza, plazos del titular…); y los **documentos normativos** versionados que los acompañan
(catálogo de modalidades de crédito, tipos de organización solidaria; los anexos del SARC y la regla de pérdida esperada llegan con
sus entregas).

Hay **dos copias y un solo sentido**:

| Dónde | Tablas | Quién escribe | Quién lee |
|---|---|---|---|
| Base administrativa (`IngenIA365ERP_Admin`) | `ADM_NationalValues`, `ADM_RegulatoryDocuments`, `ADM_NationalValueCounters` | el **maestro** con segundo factor, por `/api/saas/national-values` y `/api/saas/regulatory-documents`; la semilla central (`PlatformNationalValuesSeeder`, Admin, Order 6) | **sólo la réplica** (`SoloElReplicadorLeeElRegistroCentral`) |
| Base de cada cooperativa | `COR_NationalValues`, `COR_RegulatoryDocuments`, `COR_NationalValueReplications`, `COR_NationalValueReviews` | **sólo la réplica** | los tres lectores únicos: `IValoresNacionales`, `ICalendarioHabil`, `INormativaVigente` (`LosValoresNacionalesSeLeenEnUnSoloSitio`) |

Ningún cálculo lee la base administrativa. Nada se edita ni se borra en ninguna de las dos: son hechos (`IHechoInmutable`, también en
`AdminDbContext` desde C68). Un valor equivocado se **corrige** con otra fila que lo reemplaza (`SupersedesPublicId`) y dice por qué.

Las tablas nacen con las migraciones `ValoresNacionales` (contexto Admin) y `PlataformaParaCartera` (contexto de cooperativa), que se
aplican **antes** que las de la fase 3 de la 013 (C103). Sin ellas, la semilla central y la réplica no hacen nada (lo anotan en el log).

## 2. Quién registra: el maestro, con segundo factor (B5)

Pantalla **Administración › Valores nacionales** (`/admin/valores-nacionales`), sección «Registro central», que sólo ve el maestro
(también en la Consola SaaS). Las rutas no llevan código de permiso: el grupo entero exige al maestro
(`RequireMasterAdminAttribute.Check`), y como corren sin cooperativa no pasan por la idempotencia de cooperativa; un reintento lo
frena el propio registro (`NationalValues.Value.AlreadyRegistered`, o «sin cambio» en la plantilla).

- **Registrar un valor**: código, clave (la modalidad en `USURA`, el plazo o la serie en `IBR`/`IPC`, la fecha en `FESTIVO`, el código
  del límite en `LIMITE`), vigencia, fecha de referencia si es un índice, valor, unidad y **fuente** (la norma o la certificación). Las
  tasas y fracciones se escriben **como fracción**: 26,50 % es `0,2650`.
- **Corregir**: «Corregir» sobre la fila; exige motivo. La corrección de una corrección reemplaza a la corrección.
- **Documentos normativos**: «Registrar documento» con su JSON; se valida con el validador de su clase y se guarda su SHA-256.
- Cada registro toma su número de `ADM_NationalValueCounters` (un solo contador para valores y documentos), sin huecos, en el orden en
  que confirma. Ese número es el orden de la réplica.

### Lo que falta para los próximos días (C69)

La misma pantalla muestra arriba lo que falta (`GET /api/saas/national-values/missing?days=31`): el salario mínimo y la UVT, los 29
límites, la usura de cada modalidad del catálogo vigente y los festivos del año del horizonte. Se calcula **sin leer cooperativas**.
Es la lista de trabajo del maestro: la usura del mes siguiente aparece en cuanto el horizonte la alcanza.

## 3. La plantilla `saas.national-values` y el paquete entre ambientes

- **Plantilla** (`GET /api/saas/national-values/template.xlsx`, importar con «Importar valores»): una fila por valor, columnas `codigo`,
  `clave`, `vigenteDesde`, `vigenteHasta`, `fechaDeReferencia`, `valor`, `unidad`, `fuente`, `etiqueta`, `corrigeA`, `motivo`. Se
  **revisa** sin escribir y se **aplica** todo o nada en su propia transacción sobre la base administrativa; el número se toma en el
  orden del archivo. Una fila igual a una vigente es «sin cambio»; la misma llave con otro valor sin `corrigeA` es
  `NationalValues.ConflictingValue` (C98); `corrigeA` sin motivo es error de fila.
- **Paquete** (`GET /api/saas/national-values/export`, botón «Exportar paquete»): un `.json` con valores y documentos y sus huellas, para
  llevar lo registrado de un ambiente a otro. Se importa por la misma ruta de importación; conserva los `PublicId` de origen, así una
  corrección posterior apunta a lo mismo en los dos ambientes.

**DT §7.6 — nada que necesite valores ficticios en la central se ensaya en DEV ni en QA**: la central es una por ambiente y todas sus
cooperativas la replican. Un valor de prueba registrado en DEV llega a todas las cooperativas de DEV. Las pruebas que inventan valores
corren en las e2e, en un host propio («Valores nacionales e2e»).

### 3a. La UVR día a día y el tope de vivienda (K6b, 2026-10-17)

Desde la entrega K6b (vivienda Ley 546 y UVR) la central espera dos códigos más, sin clave y **sin semilla** (nadie los inventa):

| Código | Unidad | Cómo se registra | Sin él |
|---|---|---|---|
| `UVR` | `Pesos` (cuatro decimales o más, como la publica el Banco de la República) | **un valor por día**: `vigenteDesde` = `vigenteHasta` = ese día. El Banco la publica por adelantado para el período del 16 de un mes al 15 del siguiente: se carga ese período entero de una vez | un crédito en UVR no se causa ese día ni los siguientes (error de grupo `NationalValues.Missing` en la corrida, alerta `Cartera.ValorNacionalFaltante`); los créditos en pesos siguen. Una línea en UVR no simula ni desembolsa sin la UVR del día del desembolso |
| `TOPE_TASA_VIVIENDA` | `EA` (fracción: 12 % es `0,12`) | una fila con su vigencia; al cambiar el tope, otra fila desde la fecha nueva | ninguna línea de vivienda se crea ni se versiona, ninguna solicitud de vivienda se radica y los créditos de vivienda no se causan (error de grupo `NationalValues.Missing` con la clave del tope) |

**Cargar la UVR del período** (lo recomendado): plantilla `saas.national-values`, una fila por día:

| codigo | clave | vigenteDesde | vigenteHasta | fechaDeReferencia | valor | unidad | fuente |
|---|---|---|---|---|---|---|---|
| `UVR` | | 2026-10-16 | 2026-10-16 | | 405,1234 | `Pesos` | Banco de la República, serie UVR publicada el aaaa-mm-dd |
| `UVR` | | 2026-10-17 | 2026-10-17 | | 405,2045 | `Pesos` | ídem |
| … | | … | | | … | | |

(Los valores del ejemplo **no** son publicados: son de la forma.) Revisar primero (`mode=review`) y aplicar después; una fila igual a una
vigente es «sin cambio», así que volver a cargar el período entero no duplica. La lista de lo que falta (C69) pide la UVR **día por día
hasta siete días adelante** (`RegistroCentralQueries.DiasDeUvrPorAdelantado`) y el tope vigente: si aparece «La UVR del día», el período
siguiente no se cargó.

**Si falta la UVR de un día**: registrarla (con su fuente) y esperar la réplica; la corrida del día siguiente **pone al día** los créditos
en UVR desde el primer día sin UVR (no hay que reprocesar nada a mano). Una liquidación a una fecha **futura** sin UVR publicada se muestra
con la UVR **proyectada** desde la última publicada, con aviso y sólo para mostrar: nunca se causa ni se contabiliza una proyección.

**Corregir una UVR ya replicada**: «Corregir» con motivo, como cualquier valor. Desde la fase 24 (2026-10-17) la corrección se trata
como la de la usura: al replicarla, cada cooperativa con Cartera activada que tenga **créditos en UVR** causados en o después del día
corregido encola una corrida `NationalValueCorrection` (`EfectoDeReplicacionCorreccionDeValorNacional`; `ExecutionOrigin` =
`Replica:{valor}`) **desde el día corregido hasta la última marca** de los créditos en UVR: aunque la UVR diaria rige un solo día, cambia
también la corrección monetaria del día siguiente (la víspera se valora con ella) y, si ese día hubo pagos, el capital en UVR y lo que sigue
(decisión del dueño del 2026-10-17; antes la corrida tocaba sólo el día corregido y el siguiente quedaba descuadrado contra la reconstrucción).
La corrida recorre **sólo los créditos en UVR** y registra cada diferencia, fechada en el período abierto:

- la de la **corrección monetaria**, como `MonetaryCorrection` con su hecho `CorreccionMonetariaUvrCausada` (`runKind =
  NationalValueCorrection`);
- la del **reparto de un pago** aplicado en un día tocado (decisión del dueño del 2026-10-17): lo recibido en pesos es el mismo, pero a la UVR
  nueva el pago cubre otra mezcla —con la UVR más alta, más interés (fijo en UVR) y menos capital; con la más baja, al revés; y lo mismo con el
  anticipo o el saldo a favor que dejó—. Se registra como `PaymentReallocation` de la corrida, con el día valor del pago, y va a Contabilidad
  en su hecho `PagoRedistribuidoPorValorNacional` (`PRN`, comprobante `CA`): entre los roles de saldo (y el ingreso del suspenso o de la
  recuperación), sin el puente del recaudo, y cuadra solo. **El pago, sus filas y su comprobante no se tocan**; el certificado tributario sale
  de la reconstrucción y ya trae los intereses del reparto nuevo;
- el resto (la causación que cambia porque cambió el capital en UVR), como `AccrualAdjustment` con `AjusteDeCausacionPorFechaValor`
  (`cause = NationalValueCorrection`).

Desde la decisión del dueño del 2026-10-18, la corrida de una **usura** corregida separa el reparto de los pagos igual y lo emite en el
mismo `PagoRedistribuidoPorValorNacional`: con la usura más baja, un pago de un día tocado cubre menos interés y más capital (o deja más
anticipo o saldo a favor). El saldo a favor que deja un pago va en ese hecho; la corrida ya no emite por él un `SaldoAFavorReconocido`.

Sin créditos en UVR, o si el día corregido no está causado, no se encola nada (la liquidación y lo que se cause después ya usan el valor
corregido). Qué revisar: en `/cartera/procesos` la corrida termina `Completed`; si la política `Contabilidad.FaltaDeRegla` está en
«Bloquear» y falta la regla de `CMU`, `PRN` o `AFV` (para `PRN`: las de saldo de capital, intereses, cargos, anticipo y saldo a favor de la
línea, y el ingreso del suspenso si hubo), la tanda se detiene y alerta como cualquier proceso; se crea la regla y se reanuda. Después, el
libro del crédito cuadra con la reconstrucción (la verificación del libro, `VerifyLendingLedgerIntegrityQuery`, no la marca). Límite que queda: un pago que con la UVR
nueva cambiaría de **cancelar o no** el crédito (o su excedente) se redistribuye igual, pero el estado del crédito no cambia solo: escalarlo a
soporte.

Los **cinco límites de vivienda** (`VIVIENDA_PLAZO_MINIMO_MESES`, `VIVIENDA_PLAZO_MAXIMO_MESES`, `VIVIENDA_FINANCIABLE`,
`VIVIENDA_FINANCIABLE_VIS`, `VIVIENDA_CUOTA_INGRESO`) son `LIMITE` y **sí** vienen sembrados, «pendiente de validar» (60 y 360 meses;
0,70, 0,80 y 0,30), con la fuente de la Ley 546 de 1999 y el Decreto 145 de 2000 por cotejar. El dueño los confirma o los corrige con
«Corregir». Detalle funcional: [manual de vivienda y UVR](../manual/cartera-vivienda-y-uvr.md).

**Nunca en DEV ni QA con valores inventados** (DT §7.6): la e2e `ViviendaConUvrTests` registra una serie sintética marcada como de prueba
en la central de su propio host.

## 4. La réplica

El núcleo único es `ReplicacionDeValoresNacionales` (`Application/Common/NationalValues`), y lo corren dos caminos:

| Camino | Cuándo | Origen sellado |
|---|---|---|
| tarea `valores-nacionales.replicar` (`TareaDeReplicaDeValoresNacionales`) | en **cada pasada** del `ProgramadorDeTareas`, en cada cooperativa, también en las que no activaron Cartera | `ScheduledTask` |
| semilla `NationalValuesReplicaSeeder` (Tenant, Order 88) | al dar de alta una cooperativa, después de las semillas de Nómina (71, 74) | `TenantSeeder` |

Qué hace una pasada: copia de la central lo que tenga `Sequence` mayor que la última copiada (`MAX(SourceSequence)`), en orden, con
`KnownAt` = ahora; corre los efectos registrados (`IEfectoDeReplicacion`: la proyección de Nómina, los límites legales más estrictos) en
el **mismo** `SaveChanges`; deja una fila en `COR_NationalValueReplications` **sólo si copió algo**; y un evento de auditoría
`NationalValues.Replicated` (módulo `NationalValues`, encadenado) con conteos y rango. Si un efecto falla, no se copia nada. Repetirla no
duplica (`SourcePublicId` y `SourceSequence` son únicos en la copia).

Si la central no responde, la tarea no lanza: levanta `Cartera.ReplicaDeValoresNacionalesAtrasada` (una sola, por clave) y los cálculos
siguen con lo ya replicado; el alta de una cooperativa tampoco se detiene (lo anota y la tarea la pone al día).

La bitácora se ve en la pantalla, sección «Bitácora de réplicas» (`GET /api/core/national-values/replications`).

### Leer «lo que se sabía»: la regla bitemporal (C83)

Cada copia tiene su vigencia (D, `ValidFrom`/`ValidTo`) y el momento en que la cooperativa la conoció (R, `KnownAt`). La consulta con
«Vigente al» (`date`) y «Información hasta» (`recordedAsOf`) devuelve lo que regía en D según lo que se sabía en R: una liquidación de
antes de una corrección no ve la corrección. Sin R, rige lo último conocido.

## 5. La proyección de Nómina (T93, enmienda A7)

Nómina sigue leyendo sus tablas (`PAY_LegalParameters`, `PAY_Holidays`; `LectorDeUvt` no cambió), pero el **SMMLV, la UVT y los
festivos nacionales** los escribe sólo la réplica (`EfectoDeReplicacionEnNomina`; `SoloLaReplicaEscribeLosValoresNacionalesDeNomina`).
Las cuatro reglas:

1. **Lo igual se adopta**: una fila de Nómina con el mismo código y vigencia (o el mismo festivo) se marca como proyección
   (`Source = "Valor nacional {publicId}"`; festivo con origen `National` y `NationalValuePublicId`) sin insertar otra. Por eso una
   cooperativa sembrada antes de la réplica la recibe sin chocar con `UK_PAY_LegalParameters_Code_ValidFrom` ni `UK_PAY_Holidays_Date`.
2. **Lo nuevo se inserta**.
3. **La vigencia anterior se cierra la víspera**, como hace `AddLegalParameterVersionCommand`, y los borradores de nómina abiertos quedan
   desactualizados.
4. **Nómina ya no los registra**: `POST /api/payroll/legal-parameters/SMMLV/versions` (o `UVT`) responde 422
   `Payroll.LegalParameter.NationalValue`; un festivo `National` no se edita ni se retira desde Nómina (`Payroll.Holiday.National`). Los
   festivos `Manual` y los decretados siguen siendo de la cooperativa.

Las semillas de Nómina ya no traen SMMLV ni UVT (Order 71) y la de festivos (Order 74) se retiró: una cooperativa nueva los recibe de la
réplica (Order 88).

### Cómo resolver `Nomina.ValorNacionalDistinto`

Sale cuando la cooperativa ya tenía una fila **distinta** para la misma vigencia (regla 1 al revés): la réplica **conserva la de Nómina**
y avisa. No se corrige sola, porque puede ser un error de la central o de la cooperativa:

1. Comparar el valor de la alerta con la fuente oficial.
2. Si el error es de la **central**, el maestro registra la corrección; la réplica la lleva y la proyección se pone al día.
3. Si el error es de la **cooperativa**, se corrige su fila de Nómina por soporte (es una fila heredada de la semilla, anterior a la 013)
   y la siguiente réplica la adopta. Nunca se borra la copia de la cooperativa.
4. Antes de aprobar una nómina con la alerta abierta, recalcularla después de corregir.

## 6. La revisión de la cooperativa: «pendiente de validar»

La cooperativa no modifica su copia: la **revisa** (`Core.NationalValues.Review`). En la pantalla, «Revisar» sobre un valor o un documento:
**Conforme** u **Objetado** (con nota obligatoria). Vale la revisión más reciente; repetir la misma decisión y nota responde
`NationalValues.AlreadyReviewed`. Sin revisión, el valor se muestra **«Pendiente de validar»**: es un estado derivado que **no bloquea**
ningún cálculo. Lo mismo para los valores y documentos sembrados con `verified = false` (propuestas de E1, E4, E10 hasta que el dueño y la
contadora los coteje).

Una objeción no cambia el valor: deja constancia para que el maestro revise la fuente.

## 7. Permisos y alertas

| Qué | Quién |
|---|---|
| Registro central | el maestro (sin código de permiso) |
| Consultar la copia y la bitácora | `Core.NationalValues.View` |
| Revisar | `Core.NationalValues.Review` |

Alertas: `Cartera.ValorNacionalFaltante` (un lector no encontró el valor a la fecha: `NationalValues.Missing`), `Cartera.IndiceDesactualizado`
(el último IBR o IPC es más viejo de lo que admite su código), `Cartera.LimiteLegalMasEstricto` (llegó un límite más estricto que el
parámetro de la cooperativa), `Cartera.ReplicaDeValoresNacionalesAtrasada` y `Nomina.ValorNacionalDistinto`.

## 8. Dónde está cada cosa

| | |
|---|---|
| Registro central | `Application/Saas/NationalValues/` (comandos, consultas, importación y paquete); `API/Endpoints/Saas/NationalValuesEndpoints.cs` |
| Réplica | `Application/Common/NationalValues/{ReplicacionDeValoresNacionales,IEfectoDeReplicacion,TareaDeReplicaDeValoresNacionales}.cs`; `Persistence/Services/RegistroCentralDeValoresNacionales.cs`; `Persistence/Seeding/Parametric/NationalValuesReplicaSeeder.cs` |
| Lectores | `Application/Common/NationalValues/{LectorDeValoresNacionales,LectorDeDocumentoNormativo}.cs`, `Application/Common/Calendar/CalendarioDeDiasHabiles.cs` |
| Copia y revisión | `Application/Core/NationalValues/CopiaDeValoresNacionales.cs`; `API/Endpoints/Core/NationalValuesEndpoints.cs` |
| Proyección de Nómina | `Application/Payroll/NationalValues/EfectoDeReplicacionEnNomina.cs` |
| Semilla central | `Persistence/Seeding/Parametric/PlatformNationalValuesSeeder.cs` y `Data/NationalValues/*.json` |
| Pantalla | `Shared/Pages/Administracion/ValoresNacionales.razor` (`ValoresNacionalesClient`) |
| Pruebas | e2e «Valores nacionales e2e» (`ValoresNacionalesYReplicaTests`, `ReplicaSobreCooperativaSembradaTests`); arquitectura `LosValoresNacionalesSeLeenEnUnSoloSitio`, `SoloElReplicadorLeeElRegistroCentral`, `SoloLaReplicaEscribeLosValoresNacionalesDeNomina`, `LaUvtSeLeeEnUnSoloSitio` |
