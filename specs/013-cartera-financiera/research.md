# Research — 013 Cartera financiera (fase 0)

Un solo archivo con dos mitades: R1–R61 (mensajes, contabilidad, motor, riesgo y valores nacionales; secciones 1 a 7) y
R101–R154 (enmiendas, retiro, seguridad, adaptadores; secciones 8 a 14). No existen R62–R100. Los nombres y decisiones
mandan desde `decisiones-transversales.md`; sus puntos para conciliar se decidieron en la §7 de ese documento
(C01..C90) y las decisiones afectadas lo dicen en su línea **Conciliación**.

# Research (1 de 2): Cartera financiera — mensajes, contabilidad, motor, riesgo y valores nacionales

**Feature**: 013 | **Fecha**: 2026-10-05 | **Rama**: `013-cartera-financiera` | **Spec**: [spec.md](spec.md) |
**Nombres y decisiones**: [decisiones-transversales.md](decisiones-transversales.md)

Primera mitad de `research.md` (fase 0). Consolida cuatro de los ocho frentes investigados: mensajes, procesos
de fondo e idempotencia; lado contable (enmienda E-01); motor financiero; riesgo, valores nacionales y cierres.
La segunda mitad (enmiendas a otros módulos, retiro de lo heredado, seguridad, adaptadores) sigue en este mismo
archivo, desde la sección 8 (R101–R154).

Decisiones del dueño que se dan por fijas: **Q1 B** (sin recaudo ni giros provisionales; sin Recaudo y
Tesorería reales no hay activación en producción), **Q2 A** (Anexo 1 en K1, pérdida esperada en K6a, vivienda
en K6b, modelo parametrizable por cooperativa y segmento), **Q3** (las cuotas de servicios a asociados no son
cartera, D29), los defectos D1–D29 y las interpretaciones I-1–I-6.

**Convenciones.**

- **Gana la transversal.** Los nombres salen literales de `decisiones-transversales.md` (§2) y cada decisión
  remite a sus T. Cuando un informe propuso otra cosa, la línea **Difiere del informe** lo dice y la propuesta
  queda como alternativa descartada, para que nadie la reintroduzca.
- **Numeración.** Decisiones **R1–R61** en esta mitad (la segunda mitad numera R101–R154; no hay R62–R100). Para no confundirlas,
  las siglas de origen se citan así: `PM-xx` (mensajes), `DC-xx` (contabilidad), `M1–M16` (motor) e
  **`inf. R1–R18`** (informe de riesgo y valores). Choques: `CH1–CH50`; preguntas: códigos de §5 de las
  transversales.
- **Formato.** Cada decisión: **Decision** / **Rationale** / **Alternatives considered** y una línea **Trazas**
  (T, FR, origen). Cada frente abre con sus **hechos del código** (con ruta) y cierra con sus **riesgos**.
- **Rutas** relativas a la raíz del repositorio. Los números de línea son los del día de la investigación
  (2026-10-05) y orientan, no obligan.
- Al final: preguntas de estos frentes (remisión a §5), nombres fijados aquí y la conciliación (remisión a la §7 de las
  transversales).

## Índice de decisiones

| R | Tema | T | Entrega |
|---|---|---|---|
| R1 | Bandeja con varios emisores, definida por el catálogo | T10 | K1 |
| R2 | El proceso origina mensajes de Cartera; actor de lo automático | T11, T8 | K1 |
| R3 | Origen, unidad y dependencias de un mensaje | T12 | K1 |
| R4 | El sobre de Cartera | T13 | K1 |
| R5 | Hechos masivos por partes | T14 | K1 |
| R6 | Catálogo de mensajes declarado entero en K1 | T10, T12, T14, T18, T56, T57 | K1 (K2–K4 se emiten después) |
| R7 | Destinos nuevos y un solo consumidor por destino | T15 | K1 |
| R8 | Registro de participantes y habilitación del destino | T16 | K1 |
| R9 | Consumidor de Cartera: recibo y evaluaciones | T17 | K1 (K2 suma los de libranza) |
| R10 | Venta v2, reserva de número y respuestas a Comercial | T18, T22 | K1 |
| R11 | Idempotencia en cuatro capas | T21 | K1 |
| R12 | `ProcesadorDeCartera`: un servicio de fondo y una tabla de corridas | T5, T6, T8 | K1 |
| R13 | Tareas livianas por `ITareaProgramada` | T7 | K1 · K2 |
| R14 | Bandeja de Cartera, turnos entre destinos y lectura sin consumir | T19 | K1 |
| R15 | Dobles de prueba de Recaudo y Tesorería | T20 | K1 |
| R16 | Frontera contable propia (`ACC_Lending*`) | T51 | K1 |
| R17 | Destino `Accounting` con dos módulos de origen | T15 | K1 |
| R18 | Catálogo de hechos y roles; dimensiones de la matriz | T52 | K1 (roles de K3–K4 sembrados) |
| R19 | Puente por clase de recaudador y `DESC_CARTERA` con asiento | T53 | K1 |
| R20 | Tipos de comprobante y documento cruce | T54 | K1 |
| R21 | Fecha contable | T36 | K1 |
| R22 | Lo de Cartera se corrige con comprobante nuevo | T55 | K1 |
| R23 | Modo de paso, disparadores y resumido diario | T56 | K1 |
| R24 | Validación previa | T56 | K1 |
| R25 | Cierre de Cartera en el libro: reclasificación completa y deterioro en delta | T48 | K1 |
| R26 | Contabilidad no cierra un mes que Cartera no cerró | T57 | K1 |
| R27 | Conciliación y cuadre de activación por conjuntos | T58 | K1 |
| R28 | Puertos de la frontera e indicadores de la entidad | T59 | K1 |
| R29 | Retiro de los escritores heredados y enmienda de la 009 | T96 | K1 |
| R30 | Dónde vive el motor y fecha local | T4, T9 | K1 |
| R31 | Tres capas: hecho primario, libro y proyecciones | T23 | K1 |
| R32 | Liquidación a la fecha D con la información hasta R | T24 | K1 |
| R33 | Causación por diferencia de acumulados | T25, T6 | K1 |
| R34 | Aritmética decimal y equivalencia de tasas | T26 | K1 |
| R35 | Convenciones de días y calendario hábil | T27 | K1 |
| R36 | Usura dentro del motor | T28 | K1 |
| R37 | Plan de pagos y versión de reglas | T29 | K1 (K3 regenera por novedad) |
| R38 | Mora, altura y suspensión derivadas de la reconstrucción | T30 | K1 (K2 suma la pagaduría) |
| R39 | Aplicación de pagos | T31 | K1 |
| R40 | Simulación sin estado con huella | T32 | K1 |
| R41 | Fecha valor anterior y reversos | T33 | K1 |
| R42 | Concurrencia por deudor y por crédito | T34 | K1 |
| R43 | Causación masiva: tandas, carga en bloque, auditoría resumida y rendimiento | T5, T14, T67, T101 | K1 |
| R44 | Precisión y redondeo | T35 | K1 (`ValorUvr` en K6b) |
| R45 | Casos dorados y pruebas que fijan el motor | §2.25 | K1 |
| R46 | Registro central y copia por cooperativa | T37 | K1 |
| R47 | Valores escalares y documentos normativos tipados | T37, T45 | K1 |
| R48 | Bitemporalidad con una sola regla de lectura | T38 | K1 |
| R49 | Réplica idempotente y sus efectos | T39 | K1 |
| R50 | Lectores únicos de valores nacionales | T40 | K1 |
| R51 | Nómina: proyección del SMMLV, la UVT y los festivos | T93 | K1 |
| R52 | «Pendiente de validar» como hecho aparte | T41 | K1 |
| R53 | Pisos y techos legales | T42 | K1 |
| R54 | Normativa SARC tipada y política de la cooperativa | T45 | K1 (`SarcAnexo2` en K6a) |
| R55 | Motores de riesgo puros | T46 | K1 (pérdida esperada en K6a) |
| R56 | Cierre mensual inmutable con R sellado | T47 | K1 |
| R57 | Cierre base e historial de mora | T49 | K1 (carga complementaria en K6a) |
| R58 | Perfil regulatorio, selector de modelo y obligación sobrevenida | T50 | K1 · K6a |
| R59 | Catálogo de modalidades como documento nacional | T44 | K1 |
| R60 | Parámetros de Cartera en la plataforma | T43 | K1 |
| R61 | Pruebas de arquitectura de valores nacionales y cierres | §2.25, T93 | K1 |

---

## 1. Frente de mensajes, procesos de fondo e idempotencia

### 1.1 Hechos del código

**Bandeja y emisor**

- **Un emisor, fijo a Inventario.** `EmisorDeMensajes` es el único escritor de `COR_IntegrationMessages` y nunca
  guarda (molde `AccountingPoster`). Tiene `public const string ModuloDeOrigen = "INV"` y lo escribe en todo
  mensaje; sólo clasifica contra `CatalogoDeMensajesV1.Buscar` (un record ajeno lanza `ArgumentException`) y
  rechaza la doble emisión de (origen, tipo, clave) en memoria y en la base. Ninguna prueba fija que sea el único
  escritor. — `src/Core/IngenIA365ERP.Application/Common/Integration/EmisorDeMensajes.cs`
- **El proceso no emite.** `EmitirAsync` lanza si `actor.EsProceso` («el proceso automático no emite mensajes»)
  o si falta `CentralUserId`; `OriginUserCentralId` es `Guid` requerido. Hoy ni la causación, ni un cierre
  programado, ni el consumidor de Cartera (que corre como «Proceso de integración») podrían emitir. —
  `EmisorDeMensajes.cs`; `src/Core/IngenIA365ERP.Domain/Entities/Integration/Transactions/IntegrationMessage.cs`;
  `src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Integration/IntegrationMessageConfiguration.cs`
- **El modo se resuelve por solicitud, no por contenido.** `SolicitudDeEmision.Modo` es uno por emisión;
  `LlevaModo` sólo aplica a (`Accounting`, `Business`). `NuevaEntrega` hace nacer lo `PorLotes` en `InBatch`
  con `ScheduleKey` y `BatchScopeKey`, lo `NoPasa` en `NotApplicable` y el resto en `Pending`; lo que va a otro
  destino nace `Always`/`Pending`. Consecuencia: los hechos de una operación con modos distintos van en
  solicitudes distintas. — `EmisorDeMensajes.cs` (`NuevaEntrega`)
- **Catálogo y claves.** `CatalogoDeMensajesV1` tiene 20 tipos, cada uno con **un** destino y sin módulo de
  origen; los dos hacia Cartera son `VentaACreditoRegistrada` v1 y `AjusteDeVentaACredito` v1 (destino
  `Lending`, `Business`). `ClavesDeEvento` admite exactamente cinco formas: `Confirmation`,
  `Confirmation:{32 hex}`, `Close:{n}`, `Reopen:{n}`, `Reclassification`. —
  `src/Core/IngenIA365ERP.Application/Common/Integration/Contracts/Inventory/CatalogoDeMensajesV1.cs`;
  `src/Core/IngenIA365ERP.Application/Common/Integration/ClavesDeEvento.cs`
- **Sobre ligado a tipos de Inventario.** `IntegrationEnvelopeV1.OriginModule` está documentado «Siempre "INV"»;
  `MessageOriginV1.DocumentClass` y `DocumentRefV1.DocumentClass` usan `Domain.Enums.Inventory.DocumentClass`, y
  `MensajesEntrantes` convierte el texto guardado con `TryParse`: una clase de Cartera llegaría nula. Deserializar
  sólo usa `CatalogoDeMensajesV1`. El sobre se arma con columnas; los enums se escriben como texto
  (`OpcionesDeMensajes`). — `Contracts/Inventory/IntegrationEnvelopeV1.cs`, `Contracts/Inventory/BloquesComunesV1.cs`,
  `Common/Integration/MensajesEntrantes.cs`
- **Límites del esquema.** `COR_IntegrationMessages`: `Type` 60, `OriginModule` 3 (`CAR` cabe y coincide con
  `ModuloContable.Cartera`), `OriginEventKey` 80, `OriginDocumentClass` 40, `OriginDocumentTypeCode` 10,
  `OriginNumber` 30; UK (`OriginPublicId`, `Type`, `OriginEventKey`); `IX_COR_IntegrationMessages_ChainRoot`.
  `COR_IntegrationMessageDeliveries`: `Destination` 20, `ScheduleKey` **60**, UK (`MessageId`, `Destination`).
  `ChainRootPublicId` sólo lo usan el emisor (heredar el modo) y el sobre. — `Configurations/Integration/*.cs`
- **Clave de lote.** `ClavesDeLote.Horario` arma `{tipo raíz}|{disparador}|{hora}|{granularidad}` en los 60
  caracteres de `ScheduleKey`; `HorarioDeLote` usa `PostingGranularity` de Inventario. —
  `src/Core/IngenIA365ERP.Application/Common/Integration/ClavesDeLote.cs`

**Consumo, despacho y habilitación**

- **Destinos y consumidores.** `IntegrationDestinations` sólo tiene `Accounting` y `Lending`, como texto.
  `IDestinoDeMensajes` declara `Destino`, `Acepta`, `ConsumirAsync`, `PlanearLote` y `TotalesDelLoteAsync`. El
  despachador toma un consumidor por destino con `FirstOrDefault`: dos registrados se tapan sin aviso.
  `ResultadoDeConsumo` sólo tiene `Processed`, `AlreadyProcessed`, `Rejected` y `Retry`. —
  `src/Core/IngenIA365ERP.Domain/Enums/Integration/IntegrationDestinations.cs`; `Common/Integration/IDestinoDeMensajes.cs`;
  `src/Presentation/IngenIA365ERP.API/Integration/DespachadorDeMensajes.cs`
- **Unidad de consumo.** `EntregasElegibles` agrupa las entregas elegibles por (origen, clave) dentro de un
  destino y de un estado (`Pending` en línea, `InBatch` del lote); una unidad no se parte entre tandas. —
  `Common/Integration/EntregasElegibles.cs`
- **Elegibilidad y equidad.** Elegible: `Pending`, reintento vencido y ninguna dependencia del mismo destino en
  `Pending`, `InBatch` o `Rejected` (`ValidationFailed` no bloquea); orden por `Id`. El despachador recorre los
  destinos en orden y **drena cada uno** antes del siguiente, dentro de 60 s por cooperativa: un atraso en
  `Accounting` retrasa a `Lending`. — `DespachadorDeMensajes.cs`, `EntregasElegibles.cs`
- **`ValidationFailed` sin escritor.** `DeliveryStatus.ValidationFailed = 5` y la alerta
  `Integracion.ValidacionFallida` existen; nadie los escribe. `RegisterDeliveryResultCommand` sólo registra
  entregas en `Pending` o `InBatch` y traduce cuatro desenlaces. —
  `src/Core/IngenIA365ERP.Domain/Enums/Integration/IntegrationEnums.cs`; `Common/Integration/RegisterDeliveryResultCommand.cs`
- **Lending habilitado por un parámetro de Comercial.** `DestinosHabilitadosAsync` recorre `Lending` sólo si
  `ParametrosDeInventario.CarteraIntegracionHabilitadaDesde` ≤ hoy: la plataforma depende de
  `Domain.Inventory.Parameters`, y FR-142 dice que lo habilita la activación de Cartera. — `DespachadorDeMensajes.cs`
- **Acoplamientos a Inventario dentro de la plataforma.** `ScheduleIntegrationBatchesCommand` cae a
  `InventoryDocumentTypes` y `ContabilidadHoraDeLote` si la clave no trae hora; `SendNotApplicableMessagesCommand`
  y `ListIntegrationMessagesQuery` dependen de tipos, parámetros y alcance de Inventario; `ReprocessMessagesCommand`
  ya es genérico (recibe destino). La bandeja sólo tiene rutas `/api/inventory/messages`. — `Common/Integration/*.cs`;
  `src/Presentation/IngenIA365ERP.API/Endpoints/Inventory/MessagesEndpoints.cs`
- **Señal, reintentos y alertas.** El guardado con mensajes despierta al despachador (`ISenalDeMensajes`);
  reintento min(15 s·2^(n−1), 15 min) más 10 %; `Integracion.MensajeSinEntregar` a los 3 intentos o 15 min;
  `Rejected` levanta `Integracion.MensajeRechazado`; `RaiseAlertCommand` admite `DedupKey`. —
  `Common/Integration/ReintentosDeIntegracion.cs`, `ApplicationDbContext.cs`

**Idempotencia, ejecución y procesos**

- **Idempotencia de pantalla.** `COR_OperationKeys` (UK `Key` Guid, `Operation` 120, `RequestSha256`,
  `CentralUserId`, `ResultJson`); `IdempotencyBehavior` actúa sólo sobre `IOperacionIdempotente`, abre
  `TransaccionExplicita` y exige misma operación, huella y `CentralUserId` (`Operation.KeyReused`);
  `HuellaDeOperacion.Calcular` es reutilizable; `LosComandosDeInventarioLlevanClave` lo exige en Inventario. —
  `Common/Behaviors/IdempotencyBehavior.cs`, `Common/Behaviors/HuellaDeOperacion.cs`, `Domain/Entities/Core/OperationKey.cs`
- **Recibo del destino (012).** Cuatro capas (`specs/012-inventario-comercial/contracts/mensajes.md` §10.2); el
  recibo por mensaje va en el mismo `SaveChanges` que el efecto (`ACC_InventoryPostings`, UK `MessagePublicId`,
  colisión → `AlreadyProcessed`). §13 exige a Cartera recibo único, orden, versiones declaradas y aceptar v1 y v2.
- **Transacción explícita.** Única forma de abrir transacción desde Application; una anidada se une a la de
  afuera; ante interbloqueo repite el trabajo **entero** tras descartar cambios (hay que releer adentro). —
  `Common/Persistence/TransaccionExplicita.cs`
- **Ejecución por cooperativa.** `IEjecutorEnCooperativa` abre un ámbito por llamada, fija `ContextoAmbiental`
  (AsyncLocal), lanza si hay `HttpContext` (una orden manual sólo encola) y salta la base con migraciones
  pendientes. `Actor.ProcesoDeIntegracion` firma con canal `Process` y sin IP; orígenes `Mensaje:`, `Lote:`,
  `Tarea:` (`Tarea:cartera.diario:2026-10-05` es válido). `IActorActual` devuelve `SEC_Users.Id`. —
  `Common/Execution/{IEjecutorEnCooperativa, Actor, ContextoAmbiental}.cs`; `API/Integration/EjecutorEnCooperativa.cs`
- **Programador de tareas.** `ProgramadorDeTareas` corre en serie (cooperativas y, dentro, tareas), con un solo
  arrendamiento `scheduled.tasks` renovado **sólo antes de cada tarea**, sin presupuesto, y recuerda la última
  corrida en memoria; pasa cada 60 s. Una tarea de más de 120 s deja vencer el arrendamiento. —
  `API/Integration/ProgramadorDeTareas.cs`; `Common/Execution/ITareaProgramada.cs`
- **Arrendamientos.** `COR_BackgroundLeases` se toma con un `UPDATE` condicional (TTL 120 s). Hay cinco nombres
  (`integration.dispatch`, `audit.forward`, `einvoicing.process`, `scheduled.tasks`, `email.dispatch`) que siembra
  la migración `PlataformaParaInventario`; un nombre sin fila nunca se toma. La exactitud no depende del
  arrendamiento. — `Common/Execution/NombresDeArrendamiento.cs`; `Persistence/Services/ArrendamientosEnBase.cs`
- **Producción y numeración.** «Producción» ya se decide con `!builder.Environment.IsProduction()`
  (`PuestaEnMarchaOptions`, `TirillaOptions`); la DIAN tiene `SimulatedInProduction`. No hay `HasSequence`: toda
  numeración usa filas contador. — `API/Program.cs`

**Frontera con Comercial**

- **Puerto existente.** `IConsultasDeCartera` declara exactamente `EstadoCrediticioAsync` y
  `EstadoDeValidacionAsync` (prueba `El_puerto_de_Cartera_declara_exactamente_sus_dos_consultas`, en
  `InventarioNoConoceContabilidadNiCartera`); DTO provisionales (`LineaDeCreditoDto` sin periodicidades,
  `EstadosDeValidacion` sin «no incorporado»); stub `ConsultasDeCarteraNoHabilitada` registrado en DI; no existe
  reservar ni liberar. — `Common/Integration/Lending/*.cs`; `Application/DependencyInjection.cs`
- **Lo que Comercial ya emite y acumula.** `EmisionDeInventario` emite `VentaACreditoRegistradaV1` por pago a
  crédito (`CreditLineCode` y `ConsultationEvidence` nulos, clave `Confirmation:{paymentPublicId:N}`) y
  `AjusteDeVentaACreditoV1`; `CreditoEnLaVenta` consulta dentro de la confirmación; `SalesQueries` da el destino por
  «disponible» si hay consumidor; el pago no tiene columna para el número del crédito. I1–I6 están en producción
  desde el 2026-10-01: hay ventas acumuladas. — `Application/Inventory/Integration/EmisionDeInventario.cs`;
  `Application/Inventory/Sales/{CreditoEnLaVenta, SalesQueries}.cs`

### 1.2 Decisiones

#### R1 · Bandeja con varios emisores, definida por el catálogo

**Decision**
- `TipoDeMensaje` gana `OriginModule` (`INV`, `CAR`, `TES`, `REC`), `Destinos` (conjunto admitido) y
  `AdmiteProceso`. `EmisorDeMensajes` deja la constante `"INV"` y toma el módulo del tipo.
- Una solicitud sólo trae contenidos de un mismo módulo; dice `SolicitudDeEmision.Destino` cuando el tipo admite
  varios (`PagoReversadoPorCartera` va a `Receipts` o al destino de su recaudador).
- `CatalogoDeMensajes` (agregado de plataforma) une `CatalogoDeMensajesV1` (INV, sin renombrar ni mover; cada
  entrada recibe los campos nuevos y suma `VentaACreditoRegistradaV2`), `CatalogoDeMensajesDeCarteraV1` (CAR) y
  `CatalogoDeMensajesHaciaCarteraV1` (TES, REC). El emisor, `MensajesEntrantes` y las versiones aceptadas leen el
  agregado.
- Los records de Cartera viven en `Application/Common/Integration/Contracts/Lending/<Type>V1.cs` con bloques
  propios sin enums de Inventario: `LoanRefV1`, `LoanFactDimensionsV1`, `ConceptAmountV1`, `OriginalFactRefV1`,
  `ExternalDocumentRefV1` (clase como texto), `LoanFactLineV1`, `LoanPlanSummaryV1`.
- `MensajeEntrante` gana `OriginDocumentClassText` y `RelatedDocumentClassText`; el comentario «Siempre "INV"» del
  sobre se corrige.

**Rationale**: es el cambio que menos toca la 012 (los bytes de los contratos INV no cambian y
`ContratosDeMensajesTests` lo sigue comparando), y el catálogo sigue siendo lo único que dice qué módulo emite
qué y adónde (T8 de la 012). Lo fijan `ElCatalogoDeMensajesEsUnico` (tipo + versión únicos, módulo declarado) y
`SoloElEmisorEscribeLaBandeja`, que hoy no existe.

**Alternatives considered**
- Un emisor por módulo: dos escritores de la bandeja (rompe T7 de la 012).
- Módulo de origen en la solicitud (DC-03): un módulo podría emitir tipos de otro (CH4).
- `MessageOriginV1.DocumentClass` como texto: bytes iguales, pero obliga a reescribir el consumidor contable de
  Inventario; se pospone.

**Trazas**: T10 · FR-008, FR-055, FR-125, FR-149, D15, D27 · PM-01, DC-03 (CH4, CH7).

**Difiere del informe**: PM-01 nombraba los catálogos sin el sufijo `V1`; DC-03 ponía el módulo en la solicitud.

#### R2 · El proceso origina mensajes de Cartera; actor de lo automático

**Decision**
- `COR_IntegrationMessages` gana `OriginActorKind` (`ActorKind`, defecto `Person`) y `OriginUserCentralId` pasa a
  nulo (aditivo, en `PlataformaParaCartera`). `SolicitudDeEmision` gana `UsuarioDeOrigen` (`UserRefV1`).
- El emisor resuelve el usuario de origen: la persona que actúa; si no hay, el `UsuarioDeOrigen` (el del mensaje
  consumido o el de quien ordenó la corrida); si tampoco, sella el proceso (`OriginActorKind = Process`, central
  nulo).
- Sólo los tipos con `AdmiteProceso = true` aceptan proceso: los masivos de la causación y del cierre,
  `CausacionSuspendida`, `CausacionReanudada`, `AnticipoAplicado`, `DimensionContableCambiada` detectada por el
  proceso, `AjusteDeCausacionPorFechaValor` y `SaldoAFavorReconocido` (también los emite la corrida
  `NationalValueCorrection`), los informativos de cierre y las respuestas a Comercial. Todos los INV quedan en
  `false`. Lo fija `EmisionesDeProcesoPermitidas`.
- Lo automático firma como `Actor.ProcesoDeIntegracion` (canal `Process`, sin IP); no hay usuarios técnicos. Una
  orden manual (`RequestLendingProcessRunCommand`, `OrderMonthlyCloseCommand`) responde 202, sólo encola, y la
  corrida corre con quien la ordenó como actor. Los dobles de prueba emiten con quien prueba como usuario de
  origen.

**Rationale**: sin esto el emisor lanza y Cartera no puede emitir ningún hecho automático. Respeta I-2: el actor
es el proceso de la cooperativa y el usuario que originó el hecho viaja como dato. Para Inventario la regla «el
mensaje lo origina la persona» sigue intacta.

**Alternatives considered**
- Usuario técnico en `SEC_Users`: lo prohíbe T6 de la 012.
- `Guid.Empty` como centinela: opaco; `UserRefV1.CentralUserId` ya admite nulo.
- Usuario de origen con nombre fijo «Proceso de la cooperativa» (DC-03): falsea la auditoría (CH3).

**Trazas**: T11, T8 · FR-003, FR-057, FR-062–FR-066, FR-107, FR-125, FR-149, I-2 · PM-02, PM-13, PM-17, DC-03
(CH3). Enmienda de la regla de la 012 sometida en B1.

**Difiere del informe**: PM-02 contaba la «liberación de reservas» entre los tipos de proceso; no hay mensaje de
liberación por vencimiento: la reserva vencida sólo pasa a `Expired` y alerta (T22).

**Conciliación**: C02 — los hechos por crédito que nace un proceso (`AnticipoAplicado`, `CausacionSuspendida`,
`CausacionReanudada`, `DimensionContableCambiada` del ciclo, `SaldoAFavorReconocido` de la corrida) tienen origen la
parte de su tanda en `LND_AccountingFactParts`, modo propio del tipo y clave `Confirmation:{loanPublicId:N}`. C15 —
los tres contingentes (`CreditoAprobadoSinDesembolsar`, `CupoRotativoOtorgado`, `GarantiaConstituida`) también
llevan `AdmiteProceso = true`. C14 — `PagoAplicado` y sus acompañantes **no** admiten proceso; la feature 014 aplica
cada pago automático a nombre de la persona que cargó o aprobó el lote. C23 — el nombre del usuario de origen del
proceso es el del actor existente, `Actor.NombreDelProceso` («Proceso de integración»).

#### R3 · Origen, unidad y dependencias de un mensaje

**Decision**
- El origen es el documento u operación de Cartera que produce el hecho (`LoanOperation`, la parte de una
  corrida, la parte de un cierre o de su reapertura, la orden de giro, la activación), **nunca el crédito**. En
  los masivos del cierre el origen es la parte (`LND_AccountingFactParts`), porque el único (origen, tipo, clave)
  no admite dos partes del mismo tipo con `Close:{versión}`; los informativos del cierre sí nacen del cierre.
- Claves, sólo de las formas existentes: `Confirmation`; `Confirmation:{publicId:N}` (partes, evaluaciones);
  `Close:{versión}`; `Reopen:{versión}`.
- **Unidad de emisión = una operación y su modo** (C01): el hecho principal y sus acompañantes —lista fija por
  tipo en `contracts/mensajes.md` (p. ej. pago + anticipo recibido + cobro de suspenso + recuperación de castigada)—
  comparten `originEventKey` y los acompañantes **sellan el modo del principal**; `Contabilidad.ModoDePaso` no se
  ofrece para un acompañante. Cartera no conoce tipos de comprobante: Contabilidad resuelve **un comprobante por
  unidad** a partir del tipo del hecho principal. En los masivos la unidad es la parte y el resumido diario las
  agrupa por lote.
- Dependencias sólo hacia el original (`Related`): reverso → pago; anulación → orden; orden nueva → anterior;
  parte del reverso de cierre → parte del cierre que revierte.
- `SolicitudDeEmision.CadenaDeOrden` (nueva) se usa sólo en las respuestas a Comercial, con la venta como raíz.
  No hay cadena por crédito hacia Contabilidad.
- La `LoanTransaction` guarda el `MessagePublicId` del hecho que la llevó.

**Rationale**: FR-055 y FR-126 piden orden frente al original y en la cadena de la venta; una cadena por crédito
detendría toda la contabilidad posterior del crédito por una regla faltante, y la conciliación ya explica los
rechazados (T58).

**Alternatives considered**
- El crédito como origen y el evento en la clave: el sobre describiría el crédito y no el documento, y harían
  falta formas de clave nuevas.
- Cadena por crédito para todos sus hechos (PM-03): un rechazo congela el crédito (CH5).
- Sin orden entre documentos del mismo crédito: un reverso podría contabilizarse antes que su pago.

**Trazas**: T12 · FR-045, FR-055, FR-080, FR-081, FR-126 · PM-03, DC-04 (CH5).

**Difiere del informe**: PM-03 hacía del crédito la cadena de orden de todos sus hechos.

**Conciliación**: C01 — rige la unidad por operación y modo de arriba. Se descarta la lectura previa de una solicitud
y una unidad por grupo (modo, tipo de comprobante) con clave adicional `Confirmation:{uuidv5}` por grupo. Los hechos
por crédito que nace un proceso (C02), el `AjusteDeCausacionPorFechaValor` de la corrida (C03) y la causación que
pone al día una operación (C04) tienen como origen una parte de `LND_AccountingFactParts`, no la operación; el
ajuste provocado por una operación fechada atrás es acompañante de esa operación (C03, «origen = la operación»,
la causa en `causeFact`). Clave de `AnulacionDeOrdenDeGiro`: `Confirmation:{ordenPublicId:N}`, con la orden como
`related` (C12).

#### R4 · El sobre de Cartera

**Decision**
- `OperationDate` = fecha contable (R21); fecha valor y fecha de registro van en el contenido.
- `BranchPublicId` = sucursal contable de la oficina del crédito (`COR_Branches.TenantBranchPublicId`).
- `PersonPublicId` = tercero del hecho (deudor, pagaduría o aseguradora); nulo en los masivos.
- `OriginDocumentClass` = clase de Cartera como texto (`LoanDisbursement`, `PaymentApplication`…);
  `DocumentTypeCode` = tipo de documento de Cartera (≤ 10); `WarehouseCode` y `FiscalUniqueCode` nulos;
  `Currency = COP`. El contenido nunca lleva cuentas (FR-126).
- Una oficina con créditos sin sucursal contable vinculada no emite: la operación se bloquea antes de confirmar y
  la activación exige el vínculo de toda oficina con créditos.

**Rationale**: Contabilidad fecha con `OperationDate` y filtra por la sucursal del sobre; el emisor ya exige
`BranchPublicId`.

**Alternatives considered**
- Fecha valor en el sobre: contabilizaría en meses cerrados.
- Sucursal en el contenido: rompe el filtro que ya usan bandeja y lotes.
- Sucursal principal con alerta para la oficina sin vínculo (DC P14): deja saldos en una sucursal que la
  conciliación por sucursal no explica (CH49).

**Trazas**: T13, T84 · FR-012, FR-126, FR-127 · PM-05, DC-09 (CH49; pregunta C9).

#### R5 · Hechos masivos por partes

**Decision**
- `InteresCausado`, `MoraCausada`, `SeguroCausado`, `DeterioroCalculado`, `CarteraReclasificadaPorCalificacion`
  y `ReversoDeCierre` se emiten por partes: una por fecha o cierre × sucursal contable × tipo de hecho × **tanda**.
  La parte cabe en la tanda que la produce: no hay tamaño de parte aparte.
- Cada parte tiene su fila en `LND_AccountingFactParts`, que es el origen del mensaje, con totales por
  combinación de dimensiones y **siempre** la lista por crédito (número, tercero, valores por concepto).
- Clave: `Confirmation:{partePublicId:N}` en la causación y en la corrida `NationalValueCorrection`;
  `Close:{versión}` / `Reopen:{versión}` en el cierre y su reapertura.
- En la causación cada parte nace en el `SaveChanges` de su tanda y la `LoanTransaction` guarda el
  `MessagePublicId` de su parte; en el cierre, todas nacen en su transacción final (R56). Los demás hechos van
  por crédito.

**Rationale**: un mensaje por crédito y día serían unos 18 millones al año; uno por día y cooperativa, megabytes y
sucursales mezcladas en un sobre de una sola sucursal. Con tandas de 250 y 50.000 créditos salen unas 200 tandas:
cientos de mensajes al día. Una parte mayor que la tanda no podría nacer en la transacción de lo que cuenta (T5).

**Alternatives considered**
- Un mensaje por crédito y día: el volumen no cabe.
- Un mensaje por día y cooperativa: contenido de decenas de MB y varias sucursales en un sobre.
- Partes de N = 2.000 créditos como valor técnico (PM-04, DC-04): la parte excedería la tanda y no nacería con lo
  que cuenta.

**Trazas**: T14 · FR-128, FR-130, SC-007 · PM-04, DC-04.

**Difiere del informe**: PM-04 y DC-04 proponían partes de 2.000 créditos y la corrida o el cierre como origen; la
transversal hace caber la parte en la tanda y la vuelve origen.

**Conciliación**: C03 — `AjusteDeCausacionPorFechaValor` emitido por la corrida `NationalValueCorrection` va por
partes (origen la parte, `PorLotes`, `CA`, `Confirmation:{partePublicId:N}`) y entra a la lista de masivos de T14.
C04 — la causación que pone al día una operación crea sus propias partes (una por tipo, la operación como tanda),
con clave `Confirmation:{partePublicId:N}` y el modo de los masivos; sus `LoanTransaction` llevan ese
`MessagePublicId`. C02 — `LND_AccountingFactParts` deja de ser sólo «parte de un hecho masivo»: también es origen de
los hechos por crédito que nace un proceso (clave `Confirmation:{loanPublicId:N}`). C46 — la tabla de partes es
`[SinDiffDeAuditoria]` con evento resumen por corrida.

#### R6 · Catálogo de mensajes declarado entero en K1

**Decision**: todos los tipos de la §2.11 de las transversales nacen en versión 1 (salvo `VentaACreditoRegistrada`
v2) y se **declaran en K1**, aunque los de novedades, castigo y cobranza se emitan desde K3 y K4; los campos que
piden K6a y K6b (modelo de deterioro, segmento, unidad UVR) van en `LoanFactDimensionsV1` desde V1.

| Grupo | Tipos | Modo por defecto (R23) | Comprobante |
|---|---|---|---|
| Por crédito, CAR → `Accounting` | `CreditoDesembolsado`, `DesembolsoReversado`, `CreditoAjustadoDesdeComercial` | EnLinea | DS |
| | `PagoAplicado` (con `AnticipoRecibido`, `CobroDeInteresEnSuspenso`, `RecuperacionDeCastigada` en su unidad), `PagoReversado`, `AnticipoAplicado`, `SaldoAFavorReconocido`, `SaldoAFavorResuelto`, `CobroProvisionalIncorporado` | EnLinea | RC (DN si recauda Nómina) |
| | `AjusteDeCausacionPorFechaValor` (de una operación fechada atrás: acompañante, C03) | el de la operación | el de la operación |
| | `DimensionContableCambiada` | EnLinea; PorLotes si la emite el proceso y el tipo no tiene vigencia propia | CN |
| | `CreditoReestructurado`, `CreditoModificado`, `CreditoNovado`, `CondonacionAprobada` (K3) | EnLinea | CN |
| | `CreditoCastigado` (K3) · `GastoDeCobranzaCausado` (K4) | EnLinea | PV · CA |
| Masivos, CAR → `Accounting` | `InteresCausado`, `MoraCausada`, `SeguroCausado`; `AjusteDeCausacionPorFechaValor` de la corrida `NationalValueCorrection` (C03) | PorLotes resumido, `FinDeCausacion` | CA |
| | `DeterioroCalculado`, `CarteraReclasificadaPorCalificacion`, `ReversoDeCierre` | PorLotes resumido, `CierreDeCartera` | PV |
| Informativos, CAR → `Accounting` | `CausacionSuspendida`, `CausacionReanudada`, `CarteraActivada`, `CargaDeCarteraAplicada`, `CierreDeCarteraRealizado`, `CierreDeCarteraReabierto` | Always, sin comprobante | — |
| CAR → `Inventory` | `ObligacionCreada`, `ObligacionRechazada` | Always | — |
| CAR → `Treasury` | `OrdenDeGiroEmitida`, `AnulacionDeOrdenDeGiro` | Always | — |
| CAR → `Receipts` | `PagoReversadoPorCartera`; `DescuentosPorPagaduria` (K2) | Always | — |
| INV → `Lending` | `VentaACreditoRegistrada` v1 y v2, `AjusteDeVentaACredito` v1 | — | — |
| TES → `Lending` · REC → `Lending` | `ResultadoDelGiro` v1 · `EnvioDeDescuentosConfirmado` v1 y `ResultadoDeDescuentos` v1 (K2) | — | — |

**Rationale**: cerrar el contrato V1 en K1 evita una V2 cuando lleguen K3, K4, K6a y K6b; los nombres son los
literales de FR-125, y los informativos de cierre son los cuatro de R26.

**Alternatives considered**
- Declarar cada tipo en su entrega: obliga a reabrir catálogos y pruebas de contrato en cada entrega.
- Los nombres del frente contable (`CausacionAjustadaPorFechaValor`, `CastigadaRecuperada`,
  `CarteraReclasificada`, `CierreDeCarteraReversado`): no son los de la spec (CH7).
- Dos informativos de cierre (`CierreMensualDeCarteraRealizado`, `…Reabierto`, PM): la regla de cierre contable
  necesita además la activación y la carga (CH8).

**Trazas**: T10, T12, T14, T18, T56, T57, §2.11 · FR-125, FR-126, FR-045, FR-053, FR-080, FR-113, FR-149, D15,
D27 · PM-01, PM-04, DC-05 (CH7, CH8).

**Difiere del informe**: PM ponía `AjusteDeCausacionPorFechaValor` en la unidad del pago.

**Conciliación**: C01 y C03 — provocado por una operación fechada atrás, `AjusteDeCausacionPorFechaValor` es
**acompañante** de esa operación y sella su modo (va en su unidad y su comprobante); sólo el que emite la corrida
`NationalValueCorrection` va por partes con `PorLotes` y `CA`. La fila de la tabla se lee así. C15 — con la
propuesta por defecto de A3 se suman tres tipos contingentes: `CreditoAprobadoSinDesembolsar` (9115),
`CupoRotativoOtorgado` (913005) y `GarantiaConstituida` (9110), `Business`, modo `NoPasa`, comprobante `CN`,
`AdmiteProceso = true`, clave `Confirmation` sobre su operación, K1, con los roles `ContingenteAcreedora` y
`ContraContingenteAcreedora` (C91; códigos cortos `CAD`, `CRO`, `GCT`); el catálogo pasa de 44 a 47 filas (se quitan si el dueño rechaza A3). C08 — los
reversos (`DesembolsoReversado`, `PagoReversado`, `ReversoDeCierre`) heredan el modo de su original. C10 —
`OrdenDeGiroEmitida` lleva `PaidByFile` y no lleva `DisbursementFilePublicId`. C17 — `ObligacionCreada` se envía
también al consumir un `AjusteDeVentaACredito` `Replacement`. C19 — `DescuentosPorPagaduria` lleva líneas
estructuradas.

#### R7 · Destinos nuevos y un solo consumidor por destino

**Decision**
- `IntegrationDestinations` suma `Inventory` (Comercial), `Treasury` (Tesorería) y `Receipts` (Recaudo); siguen
  `Accounting` y `Lending`. Módulos de origen de 3 caracteres: `INV`, `CAR` (= `ModuloContable.Cartera`), `TES`,
  `REC`; lo declara el tipo en el catálogo, nunca la solicitud.
- Los dobles de prueba consumen esos mismos destinos, no destinos aparte.
- El arranque falla con `Integration.Destination.DuplicateConsumer` si dos `IDestinoDeMensajes` declaran el
  mismo destino; lo fija `UnSoloConsumidorPorDestino`.
- El reparto del destino `Accounting` entre INV y CAR es R17.

**Rationale**: FR-149 pide que Comercial, Tesorería y Recaudo reciban lo suyo; con `FirstOrDefault` la Tesorería
real y su doble competirían sin aviso.

**Alternatives considered**
- Destinos propios para los dobles (`TestTreasury`): Cartera tendría que saber a qué destino mandar según el
  ambiente.
- `Commercial` como destino de Comercial: no coincide con su módulo de código ni con sus permisos (CH9).
- `Collections` para Recaudo (PM-06): en inglés de banca es «cobranza» y choca con el área de cobranza (CH10).

**Trazas**: T15 · FR-045, FR-053, FR-080, FR-089, FR-113, FR-145, FR-149 · PM-06 (CH9, CH10; pregunta B2).

**Difiere del informe**: PM-06 llamaba `Collections` al destino y a la clase de Recaudo; es `Receipts`.

#### R8 · Registro de participantes y habilitación del destino

**Decision**
- Plataforma, base de cada cooperativa: `COR_IntegrationParticipants` (`Code`, `Class`, `Role`, `IsTestDouble`,
  vigencia, motivo, quién) y `COR_IntegrationParticipantAdoptions` (contrato o tipo de mensaje adoptado y su
  versión). Enums `IntegrationParticipantClass {Receipts = 1, Payroll = 2, Contributions = 3, Treasury = 4,
  Test = 5, Accounting = 6, Lending = 7, Inventory = 8}` e `IntegrationParticipantRole {ObligationsCollector = 1,
  MessageDestination = 2}`.
- Lee `IRegistroDeIntegracion`; escribe `EscritorDelRegistroDeIntegracion`, que agrega sin guardar (la activación
  de Cartera y la habilitación de su destino van en el mismo guardado). Administración:
  `RegisterIntegrationParticipantCommand`, `RetireIntegrationParticipantCommand`,
  `ListIntegrationParticipantsQuery`, rutas `/api/core/integration-participants`, permisos
  `Core.IntegrationParticipants.View/Manage`.
- Un participante cuenta si tiene fila activa, su implementación está en el despliegue con esa versión y no es un
  doble en producción (`Integration.Participant.TestDoubleInProduction`).
- Semilla por migración (`PlataformaParaCartera`), idempotente en los dos motores: `Accounting` (destino, activo),
  `Inventory` (destino de las respuestas, activo con E-02), `Payroll` (recaudador, contrato v1, activo con E-03);
  `Lending` inactivo hasta la activación. Recaudo, Tesorería y Aportes los registra su propia feature. La recogida
  de créditos propios (`OwnLoanPayoff`) no se registra (T91).
- `DestinosHabilitadosAsync` **deja de leer** `ParametrosDeInventario.CarteraIntegracionHabilitadaDesde`: un
  destino se recorre si tiene consumidor y fila activa a hoy. Antes de activar, los mensajes de Comercial se
  acumulan sin intentos ni alertas. `SalesQueries` calcula «disponible» con el registro. El parámetro de Comercial
  sigue sirviendo a Comercial y la pantalla de activación muestra que debe igualarlo.

**Rationale**: FR-142 y FR-149 piden un registro por cooperativa con clase y versiones adoptadas; activar Cartera
habilita la recepción y Cartera no escribe parámetros de Comercial. Quita además la dependencia de la plataforma a
`Domain.Inventory.Parameters`.

**Alternatives considered**
- Registro sólo en código (DI): no distingue cooperativas ni deja auditoría de la adopción.
- Tabla en la base administrativa: cruza bases (Principio IV).
- Exigir el parámetro de Comercial **y** el registro (DE-15): Cartera no escribe ese parámetro (CH13).
- Registrar a Nómina en el arranque (DE-03): el despachador y la activación lo leen con la versión del esquema;
  el precedente es la semilla de `COR_BackgroundLeases` (CH16).

**Trazas**: T16, T91 · FR-086, FR-089, FR-142, FR-143, FR-145, FR-149, Principio IV · PM-07, PM-08 (CH13, CH16).

**Difiere del informe**: PM-07 usaba la clase `Collections` (ahora `Receipts`).

**Conciliación**: C36 — `COR_IntegrationParticipants` suma `Destination` e `IsEnabled`, con único
`(Code, Role, Destination)`; `RegisterIntegrationParticipantCommand` lleva `destination`. C35 — códigos sembrados
`NOM`, `CAR` (recaudador sin fila), `TST`, `CNT`, `INV`, con claves derivadas `:reverso` y `:destino`. C60 — la
activación y la habilitación del destino van en **la misma transacción** explícita, guardada por tandas de 1.000
créditos, no en un único guardado.

#### R9 · Consumidor de Cartera: recibo y evaluaciones

**Decision**
- `DestinoCartera` (destino `Lending`, en `Application/Lending/Integration`) acepta `VentaACreditoRegistrada` v1 y
  v2, `AjusteDeVentaACredito` v1, `ResultadoDelGiro` v1 y, desde K2, `EnvioDeDescuentosConfirmado` y
  `ResultadoDeDescuentos` (`VersionesAceptadasDeCartera`).
- Cada familia se consume con un comando sin ruta, `IReintentableAnteConcurrencia`, molde
  `PostInventoryMessagesCommand`: `ConsumeCommercialCreditMessageCommand`, `ConsumeTreasuryResultCommand`,
  `ConsumePayrollDeductionResultCommand` (K2). En **un** `SaveChanges` guarda el recibo `LND_InboundMessages`
  (UK `MessagePublicId`), una `LND_InboundMessageEvaluations` por evaluación (sólo inserción), el efecto y los
  mensajes que emite.
- Desenlaces:

| Caso | Recibo (`InboundMessageStatus`) | Entrega | Respuesta a Comercial |
|---|---|---|---|
| Obligación creada | `Processed` | `Processed` (referencia = PublicId del crédito, número en `VoucherNumber`) | `ObligacionCreada` |
| Evaluación negativa (FR-052; la obligación se crea marcada, D9) | `ValidationFailed` | `ValidationFailed` por la variante nueva `ResultadoDeConsumo.ValidationFailed(ref, número, código, motivo)`; levanta `Integracion.ValidacionFallida` | `ObligacionCreada` con estado `ValidationFailed` |
| Mensaje no convertible (incluida una periodicidad o un plazo sin fila en `LND_CommercialTermEquivalences`) | `Rejected`, reprocesable (C40, C95) | `Rejected`; la venta se informa `Pending` con motivo, «para reproceso» (C40) | ninguna mientras sea reprocesable; `ObligacionRechazada` no reprocesable cuando un reproceso falla de forma definitiva o una persona lo marca así (C95) |
| Venta anterior al corte (FR-143) | según la conciliación: `Processed` (migrada o cancelada antes del corte) o `NotIncorporated` | `Processed` | `ObligacionCreada` (`Migrated`, `CancelledBeforeCutoff`) u `ObligacionRechazada` no reprocesable |
| Ajuste de una venta aceptada sin migrar | `NotIncorporated` | `Processed` | `ObligacionRechazada` con motivo `SinObligacion`, no reprocesable |

- Confirmar por excepción cambia el recibo (`ConfirmedByException`), no la entrega; `EstadoDeValidacionAsync`
  responde `Validated` «confirmada por excepción».
- `RegisterDeliveryResultCommand` gana el desenlace `DeliveryAttemptOutcome.ValidationFailed = 5`.

**Rationale**: contrato §13 de la 012 (recibo único, orden, versiones) y FR-051 (la evaluación ocurre al
consumir): la entrega va directo a `ValidationFailed` sin una segunda escritura.

**Alternatives considered**
- `Processed` y después `ValidationFailed` por consulta (T32 de la 012): dos escrituras, y la 013 evalúa al
  consumir.
- Recibo sólo con éxito (como `ACC_InventoryPostings`): no deja responder «no incorporado» ni guardar la respuesta
  rechazada.
- Variante `ProcessedWithFailedValidation` (DE-14): no coincide con `DeliveryStatus.ValidationFailed` (CH12).

**Trazas**: T17 · FR-009, FR-022, FR-050–FR-055, FR-143, SC-005, SC-010 · PM-09 (CH12; preguntas B8, G11).

**Difiere del informe**: PM-09 respondía la venta aceptada sin migrar con `Processed` y la referencia «no
incorporada»; la transversal la deja `NotIncorporated` con `ObligacionRechazada` no reprocesable, y suma el caso del
ajuste (`SinObligacion`). La carpeta es `Integration`, no `Integracion` (CH41).

**Conciliación**: C40 — el mensaje no convertible **no** queda `Rejected`: queda `Pending` con motivo y marcado «para
reproceso» (FR-053); la fila de la tabla se lee así. C16 — `DestinoCartera.Acepta` admite toda versión de la familia
de la venta; una versión no aceptada la responde el consumidor con `ObligacionRechazada`. C18 — `ValidationFailed`
puede llegar directo desde `Pending`/`InBatch` (enmienda a la 012, lista de T97). C63 — «venta anterior al corte»
incluye la que llega después de activar fechada hasta el corte: `NotIncorporated`, `ObligacionRechazada` no
reprocesable (`Lending.Sale.NotIncorporated`) y alerta (pregunta I16). C20 — una reserva desconocida en una v2 no
rechaza: la obligación nace con otro número (`numberSource = Assigned`).

#### R10 · Venta v2, reserva de número y respuestas a Comercial

**Decision**
- `VentaACreditoRegistradaV2` = v1 + `ReservedLoanNumber` (string?) + `ReservationPublicId` (Guid?).
  `CreditLineCode` ya existe en v1 y llega lleno cuando Cartera respondió. Es de Comercial: su record va en
  `Contracts/Inventory` y en `CatalogoDeMensajesV1` (`OriginModule = INV`, `AdmiteProceso = false`). Comercial
  emite sólo v2; Cartera acepta las dos mientras quede alguna entrega v1; `AjusteDeVentaACredito` sigue en v1.
- Puerto `IReservasDeCartera` (`ReservarAsync(SolicitudDeReserva)`, `LiberarAsync(SolicitudDeLiberacion)` →
  `ResultadoDeReservaDto`); stub `ReservasDeCarteraNoHabilitada` (`NotEnabled` antes de activar); real
  `ReservasDeCartera`.
- Corre **dentro** de la unidad de trabajo de la confirmación de Comercial, también si la venta queda en
  aprobación, sin guardar por su cuenta; toma el cerrojo de Cartera (setup y control del deudor, R42) y el
  numerador de Cartera al final de su tramo. Ejecuta explícitamente su validador y escribe su evento
  (`Lending.NumberReservation.Reserved` / `.Released`) en `COR_AuditOutbox` dentro de la transacción.
- Clave `INV:{paymentPublicId:N}:{monto}`; repetirla devuelve el mismo número; otra huella →
  `Lending.Reservation.KeyReused`. Si cambia el monto, Comercial libera y pide otra.
- `LND_LoanNumberReservations`, estados `Reserved`, `Used`, `Released`, `Expired`. La reserva lleva el valor y
  **descuenta cupo**. Una vencida (tarea `cartera.reservas-vencidas`) nunca se reasigna; si después llega la v2 con
  ese número, lo toma (`Expired` → `Used`) con alerta informativa. `DiscardInventoryDraftCommand` y
  `DiscardPosDraftCommand` liberan.
- Respuestas al destino `Inventory`, con la venta como raíz de cadena: `ObligacionCreada` (número, línea, valor,
  resumen del plan, estado `Validated` / `ValidationFailed` / `Migrated` / `CancelledBeforeCutoff`) y
  `ObligacionRechazada` (código, motivo, reprocesable o no), una por evaluación
  (`Confirmation:{evaluationPublicId:N}`). `DestinoInventario` (Comercial) escribe `INV_LendingObligationResponses`
  (UK `MessagePublicId`): número y estado, nunca saldos.
- `EstadoCrediticioDto` suma `TotalQuota`, `DaysPastDue`, `BlockReason`, `AllowsSale`; `LineaDeCreditoDto` suma
  `Periodicities`; `EstadosDeValidacion` suma `NotIncorporated`.

**Rationale**: la consulta de cupo no escribe y se llama varias veces; reservar sí escribe (FR-057). Dentro de la
confirmación, reserva y número impreso son atómicos y no quedan reservas huérfanas. El número viaja sin que
Comercial lea a Cartera.

**Alternatives considered**
- Agregar los métodos a `IConsultasDeCartera`: mezcla consultas con operaciones.
- Reservar en transacción propia antes de confirmar: deja reservas huérfanas; queda como plan B (G6).
- Secuencia de base de datos: no hay precedente.
- Clave (módulo, pago) sin monto (PM-10): un cambio de monto quedaría como `KeyReused` sin salida (CH11).
- Tabla aparte de reservas de cupo (R-06): segunda tabla para el mismo hecho (CH29).

**Trazas**: T18, T22, T90 · FR-053, FR-055, FR-057, FR-058, FR-059, FR-145, D3, D26, SC-005, SC-008, SC-010 ·
PM-10, DE-13, DE-14 (CH11, CH29; preguntas G6, G9).

**Difiere del informe**: PM-10 llamaba `ReservaDto` al resultado y no ponía el monto en la clave.

**Conciliación**: C24 — orden global de cerrojos en la venta a crédito: filas de Inventario → `ReservarAsync`
(`LND_Setup` compartido → `LND_DebtorControls` → numerador de Cartera) → numerador de Inventario; ninguna operación
de Cartera toma cerrojos de Inventario; se mide en la prueba de volumen del POS. C25 — la reserva corre bajo un
**punto de guardado propio** dentro de la transacción de Comercial y responde `NoResponse` ante una falla técnica
(D26), en los dos motores. C67 — `INV_DocumentPayments` suma `ReservationPublicId` y `ConsultationEvidence` (en
`ComercioConCartera`). C20 — una reserva desconocida en una v2 no rechaza: la obligación nace con otro número
(`numberSource = Assigned`) y queda como evaluación. C31 — la consulta de cupo verifica listas sin escribir; el
resultado se guarda en la reserva y al consumir la venta.

#### R11 · Idempotencia en cuatro capas

**Decision**
1. **Pantalla**: comandos `IOperacionIdempotente`, cabecera `Idempotency-Key`, `COR_OperationKeys`
   (`LosComandosDeCarteraLlevanClave`).
2. **Contrato y reserva** (llamados dentro de la unidad de trabajo de otro módulo): clave de negocio de texto más
   `HuellaDeOperacion.Calcular`, con índice único en el documento de Cartera — `LND_LoanOperations`
   (`CollectorCode`, `IdempotencyKey`) y `LND_LoanNumberReservations`. Misma clave y misma huella → «ya aplicado»
   con el comprobante original; otra huella → `Lending.Contract.KeyReused` / `Lending.Reservation.KeyReused`. No
   son `IOperacionIdempotente` ni `IReintentableAnteConcurrencia`.
3. **Mensajes**: recibido → `LND_InboundMessages`; emitido → el único (origen, tipo, clave) de la bandeja.
4. **Procesos**: marca de agua (`LoanControl.LastAccruedDate`) más índice único de la causación por crédito,
   concepto, cuota y fecha valor.

Gramática de claves de recaudador `{MÓDULO}:{operación}:{obligationPublicId:N}`, con `{MÓDULO}` = código de
`ModuloContable`: Nómina `NOM:{runPublicId:N}:{obligationPublicId:N}`; recogida
`CAR:{operaciónDeDesembolso:N}:{obligationPublicId:N}`; Recaudo y Aportes la fijan en su feature. La reserva es
`INV:{paymentPublicId:N}:{monto}` porque todavía no hay obligación.

**Rationale**: `COR_OperationKeys` usa un Guid, compara el usuario central y abre su propia transacción, cuando el
contrato corre en la del recaudador (I-4); el «ya aplicado» de negocio de FR-086 exige el índice en el documento.

**Alternatives considered**
- UUIDv5 de la clave de negocio sobre `COR_OperationKeys`: sigue comparando usuario y no da el «ya aplicado».
- Ampliar `COR_OperationKeys` con clave de texto y ámbito: toca la 012 sin ganar nada.
- `Payroll:{runVersionPublicId:N}:{loanPublicId:N}` (PM-12) o `PAY:…` (DE-03): `PAY` es prefijo de tablas, no
  código de módulo (CH17).

**Trazas**: T21 · FR-009, FR-057, FR-066, FR-086–FR-088, FR-112, SC-010, I-4 · PM-12, DE-03, DE-13, M2 (CH17).

**Difiere del informe**: PM-12 proponía la clave de Nómina con prefijo `Payroll` y el vocabulario «loan».

**Conciliación**: C50 — el índice único de la capa 4 es filtrado a `Kind = Accrual` y `ReversesTransactionId` nulo.
C35 — códigos de participante sembrados: `NOM` (Nómina), `CAR` (recogida, recaudador sin fila), `TST` (dobles de
prueba), `CNT`, `INV`, con las claves derivadas `:reverso` y `:destino`; la clave del recaudador de prueba es
`TST:{guid:N}` (la pantalla la propone y deja repetirla para probar la idempotencia). Se retira `TEST:`.

#### R12 · `ProcesadorDeCartera`: un servicio de fondo y una tabla de corridas

**Decision**
- `BackgroundService` `ProcesadorDeCartera` en `API/Integration`, registrado sólo en `Program.cs`, con el
  arrendamiento nuevo `lending.process` (`NombresDeArrendamiento.ProcesoDeCartera`, fila sembrada por
  `PlataformaParaCartera`).
- Opciones técnicas `Lending:Process` (clase `LendingProcessOptions`): `Enabled`, `IntervalSeconds` 30,
  `BudgetSeconds` 60, `LeaseTtlSeconds` 120, `MaxParallelCooperatives` 2, `StartTimeLocal` «00:30», `BatchSize` 250.
- Recorre las cooperativas activas con paralelismo 2 y presupuesto de 60 s por cooperativa y pasada; salta las
  que no tienen Cartera activada.
- Corridas en `LND_ProcessRuns` / `LND_ProcessRunErrors` con `LendingProcessKind {DailyCycle, MonthlyClose,
  CloseReopen, NationalValueCorrection}` y `LendingProcessRunStatus {Requested, Running, Completed,
  CompletedWithErrors, Failed}`. Cierre y causación de una cooperativa nunca corren a la vez.
- Cada tanda es un `RunDailyAccrualCommand` (o `RunNationalValueCorrectionCommand`) por `IEjecutorEnCooperativa`
  con origen `Tarea:cartera.diario:{fecha}`, y guarda transacciones, partes y mensajes en **un** `SaveChanges`:
  **un hecho nace en la transacción que guarda lo que cuenta**.
- Día de causación: la del día d son los intereses de (d−1, d]; el ciclo de d corre desde las 00:30 locales.
  Causar d exige `LastAccruedDate = d−1`; si un día no corrió, la pasada siguiente recorre desde la marca + 1.
  Toda operación pone primero al día su crédito, en su propia transacción. El proceso bloquea con salto.
- Pasos del ciclo, en orden: réplica de valores nacionales; reajuste de tasas variables; causación corriente, mora
  y seguros; suspensión y reanudación; anticipos al vencer; bloqueos y detección de dimensión cambiada; desde K4,
  etapas, promesas y reclamos; verificación del libro sobre una muestra.
- Un crédito que falla se reintenta solo; si vuelve a fallar queda en `LND_ProcessRunErrors` con alerta y su marca
  no avanza; los demás siguen. La exactitud la dan la marca de agua y la unicidad, no el arrendamiento.
- Una orden manual (`RequestLendingProcessRunCommand`) responde 202 y crea una corrida `Requested`.

**Rationale**: `ProgramadorDeTareas` corre en serie, sin presupuesto y renueva el arrendamiento sólo entre tareas:
una causación de minutos lo perdería y atrasaría a las demás cooperativas (SC-007). Un servicio y un arrendamiento
evitan que cierre y causación compitan por los mismos créditos.

**Alternatives considered**
- `ITareaProgramada` con presupuesto: el ciclo crece N × presupuesto y bloquea `scheduled.tasks`.
- Una transacción por cooperativa y día: no cumple el tiempo y se pierde todo si algo falla.
- Dos servicios y dos arrendamientos (`ProcesoDiarioDeCartera`/`lending.accrual` de M14 y
  `lending.monthly-close` del inf. R13): compiten por los mismos créditos (CH1).
- Ciclo de D al día siguiente a la 01:00 (PM-13): un pago con fecha valor d no vería el interés de d (CH2).

**Trazas**: T5, T6, T8, §2.13 · FR-062–FR-067, FR-107, FR-117, FR-119, FR-142, SC-007, SC-020, Principio IV ·
PM-13, M14, inf. R13 (CH1, CH2).

**Difiere del informe**: PM-13 usaba `StartHourLocal` 1 y partes de 2.000; M14 un servicio y una tabla
(`LND_AccrualRuns`, `LND_AccrualRunFailures`) propios.

**Conciliación**: C05 — la corrida `NationalValueCorrection` la encola la **réplica**, no el ciclo:
`EfectoDeReplicacionCorreccionDeValorNacional` (`IEfectoDeReplicacion` de Cartera) inserta en la misma transacción
una corrida `Requested` cuando llega una fila con `SupersedesPublicId` que cubre días ya causados, sólo si Cartera
está activada. C59 — las clases de corrida son cuatro, las de `LendingProcessKind` de arriba. C04 — la puesta al día
de una operación emite su causación por partes propias. C58 — la reapertura de un cierre es una corrida
`CloseReopen` (`ReopenMonthlyCloseCommand` responde 202).

#### R13 · Tareas livianas por `ITareaProgramada`

**Decision**: singletons en `Program.cs` sobre `scheduled.tasks`; terminan en segundos, salen si Cartera no está
activada (salvo la réplica) y son idempotentes por su estado y por el `DedupKey` de las alertas.

| Tarea | Cadencia | Qué hace | E |
|---|---|---|---|
| `valores-nacionales.replicar` | cada pasada | `ReplicateNationalValuesCommand` en cada cooperativa (R49) | K1 |
| `cartera.vigilancia` | cada hora | `Cartera.ProcesoDiarioNoCorrio`, `Cartera.CreditoSinCausar`, `Cartera.CierreMensualPendiente` | K1 |
| `cartera.reservas-vencidas` | diaria | Vence reservas; `Cartera.ReservaVencida` | K1 |
| `cartera.giros-sin-resultado` | diaria | `Cartera.GiroSinResultado` | K1 |
| `cartera.cierre-programado` | diaria | Crea la corrida `MonthlyClose` si `Riesgo.CierreProgramado` está activo | K1 |
| `cartera.garantias-por-vencer` | diaria | Garantías por vencer | K1 |
| `cartera.mensajes` · `cartera.entregas` | cada minuto · periódica | Cola de mensajes al deudor; estado de entrega | K1 |
| `cartera.pagares` | periódica | Firma y custodia de pagarés desmaterializados | K1 |
| `cartera.archivos-de-descuento` · `cartera.giros-de-pagaduria` | diaria | Libranza | K2 |

**Rationale**: caben en el programador existente; su estado vive en la base, así que la memoria por proceso no
importa. La mensajería corre desde K1 porque los informativos de D24 y de FR-112 existen desde K1.

**Alternatives considered**
- Meterlas en `ProcesadorDeCartera`: esperarían detrás de la causación.
- `cartera.ventana-de-giro` y `cartera.mensajeria` cada 5 minutos desde K4 (PM-14): nombres y entrega reemplazados
  (CH38).

**Trazas**: T7, §2.13 · FR-016, FR-045, FR-057, FR-075, FR-107, FR-114, FR-122, SC-020 · PM-14, F-06, F-11, F-19,
inf. R4 (CH38).

#### R14 · Bandeja de Cartera, turnos entre destinos y lectura sin consumir

**Decision**
- La parte de Inventario de `ListIntegrationMessagesQuery` y `SendNotApplicableMessagesCommand` pasa a una
  estrategia `IVistaDeBandejaDelModulo` (alcance, modo vigente por tipo, nombre del documento). Inventario la
  implementa con lo de hoy; Cartera con alcance por oficina y modo por tipo de hecho.
- Rutas `/api/lending/messages` (+ `{id}`, `reprocess`, `send-not-applicable`) sobre `ReprocessMessagesCommand`
  (ya genérico); permisos `Lending.Messages.Read`, `.Reprocess`, `.SendNotApplicable` (`Read` porque el detalle y
  la lista por crédito de cada parte traen deudores). Pantalla `/cartera/bandeja-de-mensajes`.
- Los destinatarios de `Integracion.MensajeRechazado` e `Integracion.MensajeSinEntregar` se eligen por módulo de
  origen y destino.
- El despachador **turna** los destinos: una tanda por destino por vuelta, hasta agotar el presupuesto.
- `IMensajesEntrantes` gana `PendientesAsync(destino, tipos, hastaFechaDeOperacion, página)`: lista sin consumir
  las ventas acumuladas para la conciliación de FR-143.

**Rationale**: un rechazo debe llegar a quien puede actuar (Principio IX); un atraso en `Accounting` no puede
comerse los 5 s de SC-005; el consumidor no lee la bandeja por su cuenta.

**Alternatives considered**
- Comandos propios de Cartera sobre `COR_Integration*`: duplican lógica delicada (clausura de relacionados).
- Una bandeja única de plataforma: alcance y permisos ambiguos entre módulos.
- Poner `Lending` primero en el despachador: traslada el problema.
- Que Cartera lea `COR_Integration*` directamente: rompe T11 de la 012.

**Trazas**: T19, T60 · FR-016, FR-128, FR-130, FR-143, SC-005 · PM-15, PM-16, PM-18 (CH32, CH33).

**Difiere del informe**: PM-15 usaba `Lending.Messages.View`; es `.Read` (T60).

#### R15 · Dobles de prueba de Recaudo y Tesorería

**Decision**
- `TesoreriaDePrueba` (consume `Treasury`, emite `ResultadoDelGiro` como TES) y `RecaudadorDePrueba` (consume
  `Receipts`, llama al contrato y desde K2 emite como REC) se registran en DI sólo si
  `Lending:TestDoubles:Enabled = true` **y** `!IHostEnvironment.IsProduction()`; si la configuración los pide en
  producción, la API no arranca.
- En cada cooperativa se habilitan con una fila `Class = Test` que el escritor rechaza en producción. Rutas
  `/api/lending/test-doubles/{treasury, collector}/…` y pantalla `/cartera/dobles-de-prueba` con
  `Lending.TestDoubles.Operate`.
- No son canales simulados (`SIMULADO`, con efecto legal nulo en producción) ni dobles unitarios (sólo en
  `tests/`).

**Rationale**: FR-089 exige que no puedan registrarse en producción y que una prueba lo fije
(`LosDoblesDePruebaNoSeRegistranEnProduccion`, que recorre el DI con ambiente de producción); el carácter de
producción lo da el despliegue, no la cooperativa.

**Alternatives considered**
- Interruptor por cooperativa: la spec dice que lo decide el despliegue.
- Módulo de origen `TST`: el catálogo tendría que aceptar dos orígenes por tipo.

**Trazas**: T20 · FR-089, FR-142, FR-149, D27 · PM-17 (CH20; pregunta B4).

**Conciliación**: C35 — `TST` es el **código de participante** de los dobles (clave del recaudador de prueba
`TST:{guid:N}`), no un módulo de origen: los mensajes de los dobles siguen saliendo como `TES` y `REC`. C36 — el
registro suma `Destination` e `IsEnabled`, único `(Code, Role, Destination)`.

### 1.3 Riesgos

| Riesgo | Mitigación |
|---|---|
| Un error del catálogo sólo se ve al ejecutar | `ElCatalogoDeMensajesEsUnico`, `SoloElEmisorEscribeLaBandeja`, `EmisionesDeProcesoPermitidas`; `ContratosDeMensajesTests` byte a byte en los casos INV |
| Migración sobre una tabla de hechos (`OriginActorKind`, nulabilidad) | Aditiva y par; probada en los dos motores |
| Refactor de bandeja, despachador y lotes que ya están en producción | Estrategia delgada; «Inventario e2e» verde sin cambios en PostgreSQL y SQL Server |
| Paralelismo entre cooperativas con `ContextoAmbiental` (AsyncLocal) nunca ejercido | Prueba de dos cooperativas en paralelo; la exactitud la dan marca de agua y unicidad |
| Cerrojo y numerador de Cartera dentro de la confirmación de Comercial | Medir en la prueba de volumen del POS; plan B de G6 (transacción propia con `Expired` sin reasignar) |
| Dos fuentes del registro (fila y consumidor en código) | `IRegistroDeIntegracion` exige fila activa, implementación y versión; semillas `INSERT … WHERE NOT EXISTS` |
| Mensajes a `Treasury` o `Receipts` sin consumidor en producción se acumulan sin alerta | Aceptado: sin Recaudo y Tesorería no hay activación en producción (Q1 B) |
| Volumen de ventas acumuladas desde 2026-10-01 | `PendientesAsync` paginado |
| Crecimiento de `COR_IntegrationMessages` | Sin purga; medir con 50.000 créditos (B3) |
| QA corre como `Production` y se queda sin dobles | Confirmar el ambiente (B4) |
| La Tesorería real llega con el doble habilitado | La API no arranca: hay que retirar el doble de la configuración |
| Una reserva `Expired` tomada por una v2 tardía | Transición `Expired` → `Used` admitida, con alerta informativa |

---

## 2. Frente contable (enmienda E-01)

### 2.1 Hechos del código

**Contrato al libro y molde de la 012**

- **Un solo camino al libro.** `AccountingPoster` es el único que crea comprobantes. `PrepareAsync` corre las
  reglas 1–11, numera y agrega sin guardar; `ValidarVariosAsync` carga una vez y sin seguimiento lo que necesita y
  devuelve un resultado por comprobante. Regla 2: un tipo `Usage = Module` sólo para el módulo de su `ModuleCode`.
  Regla 3: período existente y abierto; para un módulo la fecha **nunca se corre** (`PeriodClosed`,
  `PeriodNotFound`). `PrepareReversalAsync` rechaza INV con `Accounting.Document.InventoryCorrectsWithNewVoucher`.
  `NingunModuloEscribeMovimientosFueraDelContrato` prohíbe agregar documentos o líneas, o tocar `NextNumber`, fuera
  de `Accounting/Posting`. — `src/Core/IngenIA365ERP.Application/Accounting/Posting/AccountingPoster.cs`;
  `tests/IngenIA365ERP.Architecture.Tests/Principles/NingunModuloEscribeMovimientosFueraDelContrato.cs`
- **Módulo CAR.** `ModuloContable.Cartera = "CAR"` con `AccountingModules.Lending`; `AccountEligibility` sólo
  acepta en un comprobante o regla de Cartera cuentas con CAR habilitado. — `Accounting/Posting/ModuloContable.cs`;
  `Accounting/Accounts/AccountEligibility.cs`
- **Matriz de Inventario (molde).** `ACC_InventoryPostingRules`: operación, rol y dimensiones por código, sin FK a
  `INV_`; sólo cuenta, sucursal y centro con FK `Restrict`. `DimensionKey` con `*` y UK filtrado (`DimensionKey`,
  `ValidFrom`); `SpecificityWeight` short con pesos 16/8/4/2; una versión nueva cierra la anterior; nada se
  borra. `ResolutorDeReglas.CargarAsync` carga en bloque; `MatrizVigente.Resolver` compara exigidas por igualdad y
  opcionales por igualdad o `*`, gana el mayor peso y luego el `ValidFrom` más reciente; sin candidata
  `Accounting.InventoryRule.Missing`. No hay retroactividad sobre lo contabilizado. `RolesDeCuenta`: 17 roles. —
  `Domain/Entities/Accounting/Inventory/InventoryPostingRule.cs`; `Application/Accounting/Inventory/Reglas/*.cs`
- **Mapeo y recibo.** `ACC_InventoryVoucherMappings` (sin vigencia, `MappingKey` único entre vivas) lleva a
  `VoucherType` y `CrossDocumentType`. `ACC_InventoryPostings` es `IHechoInmutable` bigint, una fila por mensaje,
  UK `MessagePublicId` **sin filtro** (colisión → `AlreadyProcessed`), en el mismo `SaveChanges` que el
  comprobante; `NoVoucherReason` `Informational` o `ZeroValue`. —
  `Domain/Entities/Accounting/Transactions/InventoryPosting.cs`; `Configurations/Accounting/InventoryIntegrationConfigurations.cs`
- **Consumidor y resumido.** `PostInventoryMessagesCommand` sin ruta, `IReintentableAnteConcurrencia`, 11 pasos;
  `ConsumoDeInventario.RevisarAsync` revisa versión, moneda COP con tasa 1, recibo existente y original pendiente
  (`Retry`). `ConstructorDeLineasDeInventario` no hace IO y es el mismo en validación previa, consumo y vista
  previa. `AgrupadorDeResumidos.ClaveDe`: fecha, tipo, operación, sucursal y centro.
  `PostInventorySummaryGroupCommand` valida con `ValidarVariosAsync` y excluye lo que falla con sus relacionados;
  `Resumen` suma por cuenta, lado, sucursal y centro sin netear, salvo las cuentas que exigen tercero, cruce o base
  (`ConservaDetalle`). — `Application/Accounting/Inventory/{Contabilizacion, Lotes}/*.cs`
- **Un consumidor para `Accounting`.** `DestinoContabilidad` es el único registrado; `VersionesAceptadas` acepta
  todo lo que `CatalogoDeMensajesV1` manda a `Accounting`: si los hechos de Cartera entraran a ese catálogo, el
  consumidor de Inventario los procesaría. — `Accounting/Inventory/Contabilizacion/{DestinoContabilidad, VersionesAceptadas}.cs`
- **Modo de paso y ámbitos.** `ModoDePasoVigente` (en `Application/Inventory/Integration`) lee
  `Contabilidad.ModoDePaso` con ámbito `DocumentType`; sin vigencia y sin contabilidad iniciada → `NoPasa`.
  `ParameterScopeKind` = `None`, `Warehouse`, `DocumentType`, `PointOfSale`, `CashRegister`, `ThirdPartyKind` (0–5):
  ni línea de crédito ni tipo de hecho. `IResolutorDeAmbitoDeParametro` tiene una sola implementación,
  `ReglasDePlataformaDeInventario`. `ValidacionPreviaContable` (3 s, `PoliticaSinRespuesta`) sella
  `PrevalidationOutcome`. — `Inventory/Integration/{ModoDePasoVigente, ValidacionPreviaContable}.cs`;
  `Domain/Enums/Parameters/ParameterScopeKind.cs`
- **Lotes.** `BatchTrigger` = `Scheduled`, `CashSessionClose`, `PeriodClose`, `Manual`, `Reprocess`,
  `SendNotApplicable` (1–6). El lote de un cierre lo crea el comando del cierre en su propio `SaveChanges`. —
  `Domain/Enums/Integration/IntegrationEnums.cs`
- **Cierre contable.** `ClosePeriodCommand(Year, Month, AcknowledgeInventoryPending)` avisa con
  `Accounting.Period.InventoryPending`; `PendingInventoryMessagesQuery` filtra sólo `Destination == Accounting`,
  **sin mirar `OriginModule`**: contaría los hechos de Cartera como pendientes de Inventario.
  `ReopenPeriodCommand` no consulta a ningún módulo. — `Accounting/Periods/PeriodCommands.cs`;
  `Accounting/Inventory/Consultas/PendingInventoryMessagesQuery.cs`
- **Conciliación y puertos de la 012.** `InventoryAccountBalancesQuery` arma conjuntos cuenta↔grupo por
  unión-búsqueda y lee saldos sólo por `MovimientosContables.HastaInclusive` con `SinRestriccion`, apertura incluida
  (`Accounting.NotInitialized` sin contabilidad). `IContabilidadParaInventario` declara cinco métodos fijados por
  prueba; `IDimensionesDeInventario` le da a Contabilidad códigos y combinaciones. —
  `Accounting/Inventory/Consultas/InventoryAccountBalancesQuery.cs`; `Common/Integration/Accounting/*.cs`

**Lo sembrado y lo heredado**

- **Comprobantes y cruces.** `voucher-types.json` siembra `DS`, `RC`, `CA`, `PV` y `DN` (`Usage Module`, CAR) y
  advierte que «los sembrados no se eliminan ni cambian de uso»; `cross-document-types.json` siembra `PG`, `FV`,
  `NC`, `ND`. El contrato de la 009 (`specs/009-contabilidad-niif/contracts/contabilizacion.md`) asigna esos cinco a
  Cartera con cuentas de texto de `LND_CreditLineParameters`. `CN`, `KC` y `CR` están libres; `NV` es de Inventario.
  — `Persistence/Seeding/Parametric/Data/{voucher-types, cross-document-types}.json`
- **CUIF sembrado (hasta nivel 4; las auxiliares las crea la cooperativa).** Capital: 1404/1405 (vivienda con y
  sin libranza), 1441/1442 (consumo), 1448 (microcrédito inmobiliario), 1455 (microcrédito empresarial), 1462
  (comercial, sin separar libranza ni persona), **1469 (empleados, que gana a la división por libranza)**, 1476
  (productivos, una sola); subcuentas A–E. Intereses (1406, 1443…) y pagos por cuenta (1407, 1444…) por categoría,
  más una subcuenta «con periodos de gracia». E1/E2 sin cuenta. Deterioro individual por categoría (naturaleza
  crédito); general 1468 (146805 general, 146810 adicional); gasto 5115; recuperaciones 4225; ingresos 4150 con
  gracia y mora aparte. Orden: 8115 (en A y B sólo reestructurados), 8116, 8310/831015 (castigada), contras 86/88;
  contingentes 9115, 913005, 9110. — `Persistence/Seeding/Parametric/Data/puc-solidario.json`
- **Escritores heredados.** `DisburseLoanCommand` arma el `DS` con `CreditLineParameter.AccountCode`;
  `RecaudoDeCredito` el `RC` con cuentas de texto (lo llaman `ProcessPaymentCommand` y la definitiva, que le pasa
  la cuenta débito de `DESC_CARTERA` por `CuentaDebitoDelDescuentoAsync`); `AccrueInterestCommand`,
  `ClassifyPortfolioCommand` y `ProcessPayrollDeductionCommand` llevan el marcador «E3 (feature 009)».
  `LiquidateSavingsInterestCommand` vive en `Application/Lending/SavingsInterest` pero es de ahorros.
  `AccountReferenceFinder` busca cuentas de texto en `LND_CreditLineParameters`; `LND_PortfolioAccounts` y
  `LND_ProvisionParameters` también guardan cuentas como texto. — `Application/Lending/**`;
  `Application/Payroll/Settlements/Settlement/ApproveSettlementCommand.cs`; `Persistence/Services/AccountReferenceFinder.cs`
- **D-08 de Nómina.** `LineFactory` marca `DESC_CARTERA` como no contabilizable («Cartera ya contabilizó») y
  `CalculationInputLoader` lee Cartera directamente: hoy nadie contabiliza el descuento de la ordinaria (el `DN`
  quedó pendiente en la E3). — `Domain/Payroll/Calculation/Rules/ICalculationRule.cs`;
  `Application/Payroll/Services/CalculationInputLoader.cs`
- **Lo que Cartera necesitará de Contabilidad.** No hay puerto de estado del período (`ListPeriodsQuery` es de
  pantalla); patrimonio técnico y solvencia no existen; las pérdidas se derivan por `MovimientosContables`
  (clases 4–7 sin el CI; 3510, 3610, 3910); las cuentas bancarias ya salen de `ListBankAccountsQuery`
  (`GET /api/accounting/accounts/bank-accounts`, con `BankId` y código ACH). — `Accounting/Accounts/AccountQueries.cs`
- **Oficina, rutas y semillas.** `COR_Branches.TenantBranchPublicId` une sucursal contable y oficina
  (`Agencias.razor` ya lo ofrece); las rutas del lado contable de la 012 cuelgan de `/api/accounting/inventory/*`;
  el último Order paramétrico es 87. — `Domain/Entities/Core/Branch.cs`;
  `API/Endpoints/Accounting/InventoryIntegrationEndpoints.cs`

### 2.2 Decisiones

#### R16 · Frontera contable propia (`ACC_Lending*`)

**Decision**
- Cartera nunca llama a `AccountingPoster`: emite hechos y Contabilidad los convierte en comprobantes con piezas
  propias: `ACC_LendingPostingRules` (`LendingPostingRule`, la matriz), `ACC_LendingVoucherMappings`
  (`LendingVoucherMapping`), `ACC_LendingPostings` (`LendingPosting`, H, bigint, UK `MessagePublicId` sin filtro,
  `OriginUserCentralId` nulo admitido, `RelatedMessagePublicId`) y `ACC_RegulatoryIndicators`
  (`RegulatoryIndicator`). Migración aditiva par `IntegracionContableDeCartera`.
- Lógica en `Application/Accounting/Lending/{Reglas, Contabilizacion, Lotes, Consultas}` (espejo de
  `Accounting/Inventory`); adaptador `ContabilidadParaCartera`.
- Dimensiones de Cartera **por código**, sin FK a `LND_`; sólo cuenta, sucursal y centro con FK `Restrict`.
- Lo genérico del consumidor de Inventario (recibo, revisión de versión y moneda, rechazo con líneas, conjuntos de
  cuentas) se extrae a una base común en `Application/Accounting/Integration` sin cambiar el comportamiento de INV.
  `AccountReferenceFinder` y `ListInvalidParameterizationsQuery` miran también la matriz de Cartera.
- Eventos de auditoría del lado contable: `Accounting.Lending.Posted`, `Accounting.Lending.Rejected`,
  `Accounting.Lending.BatchProcessed`, `Accounting.LendingRules.Imported`, `Accounting.RegulatoryIndicator.Registered`.

**Rationale**: C3 y R6 prohíben la llamada directa; el molde de la 012 da un solo camino al libro y dos
atomicidades (operación + mensaje en Cartera; comprobante + recibo en Contabilidad). Las tablas propias no tocan
la matriz de Inventario en producción.

**Alternatives considered**
- Generalizar `ACC_InventoryPostingRules` y `ACC_InventoryPostings` con columna `Module`: otras dimensiones,
  migración de tablas productivas y pruebas mezcladas.
- Que Cartera siga llamando al contrato dentro de su operación, como Nómina: contrario a C3 y FR-008.

**Trazas**: T51, §2.4 · FR-008, FR-125, FR-126, FR-129, FR-146, C3 · DC-01.

#### R17 · Destino `Accounting` con dos módulos de origen

**Decision**
- `DestinoContabilidad` sigue siendo el único consumidor de `Accounting` y **reparte por `OriginModule`**: INV al
  consumo actual; CAR a `PostLendingFactsCommand` (en línea) o `PostLendingSummaryGroupCommand` (resumido), ambos
  sin ruta.
- `UnidadDeConsumo` gana `OriginModule`; las versiones aceptadas son por módulo; `PlanearLote` separa por módulo
  antes de agrupar; `TotalesDelLoteAsync` suma los dos recibos.
- Las `ScheduleKey` de Cartera son `CAR.{ShortCode}|{disparador}|{hora}|{granularidad}` (C06): `ShortCode` es
  columna de `LND_FactTypes` (3 a 4 letras, único; p. ej. `ICA`), la hora va vacía con los disparadores
  `FinDeCausacion` y `CierreDeCartera`, y el disparador `Hora` se sella como la constante `HoraDiaria` de
  `ClavesDeLote` (012); `ScheduleIntegrationBatchesCommand` usa el respaldo de Inventario (`InventoryDocumentTypes`,
  `ContabilidadHoraDeLote`) sólo con claves INV.
- `PendingInventoryMessagesQuery` filtra `OriginModule = "INV"` y su lógica pasa a
  `ListPendingIntegrationMessagesQuery(módulo)`, que usan el cierre contable y la conciliación.

**Rationale**: el despachador toma un consumidor por destino; sin filtro por módulo, el aviso del cierre contable
contaría los pendientes de Cartera como de Inventario.

**Alternatives considered**
- Destino aparte `AccountingLending`: bandeja, lotes y despachador mostrarían dos Contabilidades.
- Dos `IDestinoDeMensajes` para el mismo destino: el despachador no lo admite.

**Trazas**: T15 · FR-125, FR-128, FR-146, C3 · DC-02, PM-11 (CH6).

**Difiere del informe**: DC-02 nombraba `PendingIntegrationMessagesQuery` (sin verbo).

**Conciliación**: C06 — se adopta el código corto por tipo que proponía PM-11 (`CAR.ICA`), porque con el nombre del
tipo la clave pasaba de 60 caracteres. C07 — la granularidad `PorHecho` se suma a `PorDia` y `PorDiaYSucursal` y va
en la `ScheduleKey`; `COR_IntegrationBatches.Granularity` sigue con `PostingGranularity`.

#### R18 · Catálogo de hechos y roles; dimensiones de la matriz

**Decision**
- `OperacionesDeCartera` = los hechos de R6; `RolesDeCuentaDeCartera` = `Capital`, `InteresPorCobrar`,
  `PagoPorCuentaDelAsociado`, `CargoPorCobrar`, `Ingreso`, `SuspensoEnOrden`, `ContraOrdenContingente`,
  `CastigadaEnOrden`, `ContraOrdenControl`, `DeterioroIndividual`, `DeterioroGeneral`, `GastoDeterioro`,
  `RecuperacionDeterioro`, `GastoDeCastigoOCondonacion`, `IngresoPorRecuperacion`, `IngresoDiferido`,
  `RecaudoPorAplicar`, `DesembolsoPorGirar`, `PuenteComercial`, `CuentaPorCobrarComercial`, `Anticipo`,
  `SaldoAFavor`, `SeguroPorPagar`, `CuentaPorPagarATercero`. Datos fijos en código con su tabla débito/crédito; un
  importe negativo invierte el lado.
- Esquema de asientos, por ejemplo: desembolso D `Capital` | C `DesembolsoPorGirar` (en especie, `PuenteComercial`
  o `CuentaPorCobrarComercial` con cruce `FV`); causación D `InteresPorCobrar` | C `Ingreso` (suspendido: D
  `SuspensoEnOrden` | C `ContraOrdenContingente`); pago D `RecaudoPorAplicar` | C por concepto; deterioro
  `GastoDeterioro`/`RecuperacionDeterioro` contra `DeterioroIndividual` o `DeterioroGeneral`; reclasificación y
  cambio de dimensión: el mismo rol con dimensiones nuevas contra las anteriores. Informativos sin comprobante.
- Columnas de `LendingPostingRule`: `Operation`, `Role`, `CreditLineCode`, `ModalityCode`, `SubsegmentCode`,
  `RiskCategory`, `ConceptCode`, `IsEmployee`, `InGracePeriod`, `OnBehalfOfAssociate`, `Restructured`,
  `CollectorClass`, `ReasonCode`, `BranchId`, `CostCenterId`, `AccountId`, vigencia, `DimensionKey` (300),
  `SpecificityWeight` (int).
- Exigidas sin `*` según el rol (modalidad en los de saldo; categoría en los de categoría; concepto en los de
  varios conceptos; `CollectorClass` en `RecaudoPorAplicar`; motivo donde el rol lo pida). Opcionales con pesos en
  potencias de dos: Línea 512, Empleado 256, Gracia 128, Sub-segmento 64, Reestructurado 32, PorCuentaDelAsociado
  16, Sucursal 4 (más el hecho con peso 2 en los roles de resultado, C77). El **centro de costo** se asigna a la
  línea y no se compara: no pesa ni entra en `DimensionKey` (C77).
- La categoría contable de un saldo es la oficial del último cierre (o del cierre base), nunca la diaria; las
  subcategorías van a su letra. La dimensión «deudor empleado» sale del estado actual de la persona maestra (el
  ciclo diario detecta el cambio y emite `DimensionContableCambiada`).
- `ResolveLendingRuleQuery` responde «¿a qué cuenta va esta combinación?». Roles sembrados sin cuenta, «pendiente
  de validar».

**Rationale**: en el CUIF «empleados» (1469) gana a la división por libranza y los intereses, el deterioro y el
suspenso van por categoría; la 8115 sólo separa reestructurados en A y B, por eso la dimensión `Restructured`.
Con potencias de dos no hay empates. Si la categoría saliera de la calificación diaria, el saldo cambiaría cada
día sin un hecho.

**Alternatives considered**
- Un «segmento contable CUIF» calculado por Cartera como única dimensión: mete la estructura del CUIF en Cartera,
  y FR-126 y FR-129 piden que combine Contabilidad.
- Pesos de la 012 con la línea como grupo: con nueve opcionales la prioridad debe ser explícita.
- Categoría opcional en los intereses: el CUIF los separa por categoría.

**Trazas**: T52 · FR-056, FR-065, FR-099, FR-103, FR-104, FR-107, FR-125, FR-126, FR-129 · DC-05, DC-06
(preguntas C4, C7, C8).

**Conciliación**: C77 — el hecho (`Operation`) con `*` es obligatorio en los roles de saldo, orden, pasivo, puente,
diferido y contingentes, y opcional con peso 2 en los de resultado; el centro de costo sale de la comparación
(arriba). C15 y C91 — se suman los roles `ContingenteAcreedora` y `ContraContingenteAcreedora` para los tres contingentes.
C81 — el suspenso tras el castigo va a `CastigadaEnOrden` y sale de `SuspensoEnOrden` al castigar (pendiente de
validar, C7). C79 — cruce `FV` en `CuentaPorCobrarComercial` **y** en `PuenteComercial`. C29 — en el contrato,
`CombinacionDeCartera.Role` lleva el nombre del rol como texto. C70 — la matriz no trae filas sembradas: la plantilla
y la completitud presentan cada combinación sin cuenta como «pendiente de validar».

#### R19 · Puente por clase de recaudador y `DESC_CARTERA` con asiento

**Decision**
- `PagoAplicado`, `SaldoAFavorResuelto` (devuelto) y `RecuperacionDeCastigada` mueven el rol `RecaudoPorAplicar`,
  resuelto por `CollectorClass` (valor de `ClaseDeRecaudador` como texto). Cada recaudador contabiliza su lado
  contra ese puente: Recaudo, caja o banco; Nómina, salarios por pagar; Aportes, aportes.
- La recogida (`OwnLoanPayoff`) lleva su puente a la misma cuenta que `DesembolsoPorGirar`: el neto es lo que
  queda por girar.
- Enmienda D-08 de la 010: `DESC_CARTERA` vuelve a contabilizar en el NM y en la definitiva contra el puente
  «recaudos por nómina para Cartera» (`AffectsAccounting = true`); se retiran la excepción de `ICalculationRule`,
  de `ProposedDeductionsRule` y de `AdjustSettlementDeductionCommand`, y `CuentaDebitoDelDescuentoAsync`.
- La activación en producción exige la regla `RecaudoPorAplicar` con `CollectorClass = Payroll` resuelta en la
  matriz (T84; ver R107 en la segunda mitad de este archivo).

**Rationale**: Cartera no conoce cajas, medios ni pasivos del recaudador (FR-008); el saldo del puente es lo
recaudado que Cartera todavía no contabilizó y la conciliación se explica sola. Si la contadora lleva el puente a
la cuenta del recaudador, el neto es el de hoy.

**Alternatives considered**
- Mantener D-08 y que Cartera debite la cuenta del recaudador: parametrizar dos veces la misma cuenta y que
  Cartera conozca los medios de pago; inviable para Recaudo.

**Trazas**: T53, T84 · FR-046, FR-056, FR-083, FR-088, FR-112, FR-126, FR-130, FR-147 · DC-07, DE-08
(pregunta C1).

#### R20 · Tipos de comprobante y documento cruce

**Decision**: `ACC_LendingVoucherMappings` con clave `Operation`/`CollectorClass` (`*` = toda la operación) →
tipo de comprobante (`Usage Module`, CAR) y documento cruce. Se reutilizan los sembrados y se agrega uno.

| Código | Uso | Estado |
|---|---|---|
| `DS` | Desembolso, su reverso, ajustes desde Comercial | sembrado |
| `RC` | Pagos, reversos, anticipos, saldos a favor, cobro de suspenso, recuperación de castigada, cobro provisional | sembrado |
| `DN` | `PagoAplicado`, `PagoReversado` y `RecuperacionDeCastigada` con `CollectorClass = Payroll` (C79) | sembrado |
| `CA` | Interés, mora, seguro, gasto de cobranza, ajuste por fecha valor | sembrado |
| `PV` | Deterioro, reclasificación por calificación, castigo, reverso de cierre | sembrado |
| `CN` | «Novedades de cartera»: reestructuración, modificación, novación, condonación, cambio de dimensión; contingentes (C15) | **nuevo** en `voucher-types.json` |

Cruce por defecto `PG` con el número del crédito; `FV` con la factura en `CuentaPorCobrarComercial` y en
`PuenteComercial` (C79). Semilla
`LendingVoucherMappingsSeeder` (**Order 89**, `lending-voucher-mappings.json`), «pendiente de validar». Las filas de
Cartera de `contabilizacion.md` de la 009 remiten a esta enmienda. `VoucherTypesSeeder` registra en el log una
colisión con un `CN` existente.

**Rationale**: «los sembrados no se eliminan ni cambian de uso»; C3 reemplaza la llamada directa, no los tipos.

**Alternatives considered**
- Un tipo por hecho: demasiados para la contadora.
- Cruce nuevo `CR` en vez de `PG`: queda como respuesta si la contadora objeta que un crédito de Comercial no
  tiene pagaré (C6).

**Trazas**: T54, §2.19, §2.21 · FR-125, FR-126, FR-146, C3 · DC-08 (CH36; pregunta C6).

**Difiere del informe**: DC-08 pedía el Order 88, que es de `NationalValuesReplicaSeeder`.

**Conciliación**: C01 — Cartera no conoce tipos de comprobante: Contabilidad resuelve **un comprobante por unidad**
con el mapeo del tipo del hecho **principal**; los acompañantes van en ese comprobante aunque su fila propia del
mapeo diga otro tipo.

#### R21 · Fecha contable

**Decision**: Cartera calcula la fecha contable al emitir con `IContabilidadParaCartera.EstadoDelPeriodoAsync`: la
fecha valor si su mes no tiene cierre de Cartera y está abierto en Contabilidad; si no, el primer día del primer
mes que cumpla las dos cosas. La causación del día d se fecha d; los hechos del cierre de M, el último día de M.
Contabilidad fecha el comprobante con esa fecha y nunca la corre: `PeriodClosed` → `Rejected`; `PeriodNotFound`
(el ejercicio siguiente sin abrir el 1 de enero) → `Retry` con la alerta `Contabilidad.PeriodoInexistente`.

**Rationale**: la fecha contable es de Cartera (FR-012) y la conciliación la necesita idéntica en los dos lados;
rechazar `PeriodNotFound` dejaría rechazada la causación de cada 1 de enero.

**Alternatives considered**
- Fechar con la fecha valor y que Contabilidad la corra al primer mes abierto: la fecha dejaría de ser de Cartera
  y la conciliación por fecha contable no cuadraría.

**Trazas**: T36 · FR-012, FR-107, D4 · DC-09, PM-05.

#### R22 · Lo de Cartera se corrige con comprobante nuevo

**Decision**
- Nada de Cartera usa `PrepareReversalAsync`: la guarda del poster se extiende a CAR
  (`Accounting.Document.LendingCorrectsWithNewVoucher`) y `ReverseDocumentCommand` responde `ModuleOwned`.
- Cada reverso (pago, desembolso, cierre) es un comprobante regular de su propio hecho y fecha contable, enlazado
  por `RelatedMessagePublicId`, con las **dimensiones vigentes** del saldo que restituye (la categoría actual),
  resueltas con la matriz vigente en esa fecha; si los dos lados resuelven la misma cuenta, no hay líneas.
- En el libro de Cartera, el reverso es siempre fila espejo con `ReversesTransactionId` más las diferencias de la
  reconstrucción (R41); este comprobante contabiliza esas filas. Lo fija `LoDeCarteraSeCorrigeConComprobanteNuevo`.

**Rationale**: un pago de A reversado tras un cierre que pasó el crédito a C restituye capital que hoy vive en C;
espejar A rompería SC-006. Un resumido diario no se reversa en parte.

**Alternatives considered**
- Espejo con las dimensiones del original (como `DocumentoAnulado` de la 012): rompe la conciliación por categoría
  en cuanto hay un cierre de por medio.

**Trazas**: T55 · FR-002, FR-048, FR-080, FR-107, FR-130, I-5 · DC-10.

#### R23 · Modo de paso, disparadores y resumido diario

**Decision**
- Cada tipo de hecho sella su modo al emitirse (ámbito `LendingFactType` sobre `LND_FactTypes`, sembrado por
  `LendingFactTypesSeeder`, Order 90). `ModoDePasoDeCartera` repite la regla de `ModoDePasoVigente`, incluido
  `NoPasa` mientras la contabilidad no esté iniciada.
- Defectos (D8): `InteresCausado`, `MoraCausada`, `SeguroCausado`, `AjusteDeCausacionPorFechaValor` y
  `DimensionContableCambiada` emitida por el proceso → `PorLotes` resumido, disparador `FinDeCausacion`;
  `DeterioroCalculado`, `CarteraReclasificadaPorCalificacion` y `ReversoDeCierre` → `PorLotes` resumido,
  `CierreDeCartera`; todo lo demás `EnLinea`. Para `DimensionContableCambiada`, si el tipo no tiene vigencia propia,
  `EnLinea` cuando la emite una operación y `PorLotes` cuando la emite el proceso; con vigencia propia rige para
  los dos (`ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite`).
- `BatchTrigger.LendingAccrualRun = 7` y `LendingMonthlyClose = 8`: el lote nace en el guardado de la corrida o del
  cierre (`BatchScopeKey` `AccrualRun:{fecha}`, `LendingClose:{aaaa-mm}`).
- Resumido de la causación: **un comprobante por día** (clave fecha contable + tipo), cada línea con su sucursal;
  `AgrupadorDeResumidosDeCartera` suma sin netear por cuenta, lado, sucursal y centro y conserva una línea por
  crédito (tercero, cruce `PG`) en las cuentas que exigen tercero, cruce o base. `Contabilidad.Granularidad` admite
  `PorDiaYSucursal`. Origen `CAR · LendingPostingBatch · lote`.
- Parámetros (módulo `LND`): `Contabilidad.ModoDePaso`, `.Granularidad`, `.DisparadorDeLote`, `.HoraDeLote`,
  `.PoliticaSinRespuesta`, `.FaltaDeRegla`, `.ValidacionPreviaSegundos`.

**Rationale**: FR-128 pide el modo de la 012 por tipo de hecho y el emisor es quien lo sella; un disparador atado
al fin de la corrida evita la carrera entre la hora del lote y la causación que todavía corre.

**Alternatives considered**
- Una clave de parámetro por tipo (`Contabilidad.ModoDePaso.InteresCausado`…): unas cien claves.
- Tabla propia de modos: rompe el lector único de parámetros.
- Un comprobante por día y sucursal (clave de la 012): queda como granularidad elegible.
- Resumido mensual: el balance de mitad de mes no mostraría intereses causados.

**Trazas**: T56, T43 · FR-007, FR-128, FR-149, D8, US7-3 · DC-11, DC-12 (pregunta C5).

**Difiere del informe**: DC-11 ponía el módulo de parámetros como «Cartera»; es `LND` (CH36).

**Conciliación**: C01 — `Contabilidad.ModoDePaso` no se ofrece en el ámbito `LendingFactType` para un tipo
acompañante: sella el modo de su principal (un `AjusteDeCausacionPorFechaValor` provocado por una operación va en el
modo de ésta; el `PorLotes` de arriba es el de la corrida `NationalValueCorrection`, C03). C08 — los reversos
(`DesembolsoReversado`, `PagoReversado`, `ReversoDeCierre`) heredan el modo de su original. C09 — «clave fecha
contable + tipo» = tipo de **comprobante**; el alcance `FinDeCausacion` de un hecho nacido fuera de la corrida es el
día siguiente al de su registro, y el lote recoge también los alcances anteriores sin lote. C06 y C07 — forma de la
`ScheduleKey` y granularidad `PorHecho` (R17). C15 — los tres contingentes nacen con modo `NoPasa`. C58 — la
reapertura crea su propio lote `LendingMonthlyClose` con `BatchScopeKey` `LendingClose:{aaaa-mm}`.

#### R24 · Validación previa

**Decision**
- `IContabilidadParaCartera.EvaluarAsync` delega en `EvaluateLendingPostingQuery`, que arma las líneas con el
  mismo `ConstructorDeLineasDeCartera` del consumidor (sin IO) y las valida con `ValidarVariosAsync`, más regla
  faltante, tipo sin mapeo y descuadre.
- `ValidacionPreviaContableDeCartera` reduce los hechos a una pregunta por combinación de dimensiones por
  operación o lote.
- La falta de regla **bloquea sólo** en las operaciones que nacen en pantallas de Cartera (desembolso, novedad,
  condonación, castigo) si `Contabilidad.FaltaDeRegla = Bloquear`. En el contrato, el consumidor de Comercial y
  los procesos **siempre advierte**: sella `PrevalidationOutcome` y alerta. Sin respuesta en
  `Contabilidad.ValidacionPreviaSegundos` rige D7 (advierte y permite).
- La reserva de número no se evalúa (no produce hecho contable) y el alta de una obligación desde una venta
  tampoco se frena (D9): **la venta de Comercial nunca se detiene por la matriz de Cartera**.

**Rationale**: un pago que ya ocurrió no se rechaza por un vacío de la matriz y la nómina es «todo o nada» (I-4);
sin la reducción, una nómina de 500 empleados haría 500 evaluaciones dentro de su transacción.

**Alternatives considered**
- Que la falta bloquee en todas partes: rompe I-4 y el proceso diario (FR-066).
- Bloquear también la reserva y el alta de crédito comercial (DC-15): detendría la venta.

**Trazas**: T56, T97 · FR-016, FR-066, FR-088, FR-127, D7, US7-2 · DC-15. Recorta FR-127 y US7-2: enmienda A8.

**Difiere del informe**: DC-15 incluía la reserva y el alta de crédito comercial entre lo que «Bloquear» frena.

**Conciliación**: C80 — `Contabilidad.FaltaDeRegla` gobierna **todo** hallazgo de la evaluación (no sólo la regla
faltante), y `Contabilidad.PoliticaSinRespuesta = Bloquear` también se limita a las operaciones de pantalla (A8).
C21 — vocabulario: `PrevalidationOutcome.NotPostable`, `NoVoucherReason.SameAccounts` y la alerta
`Cartera.HechoNoContabilizable`.

#### R25 · Cierre de Cartera en el libro: reclasificación completa y deterioro en delta

**Decision**: al cerrar M, Cartera emite por partición y en este orden:
1. `CarteraReclasificadaPorCalificacion` de **todo** saldo con dimensión de categoría (capital, intereses, pagos
   por cuenta, deterioro individual de cada concepto y suspenso en orden), de la categoría anterior a la nueva.
2. `DeterioroCalculado` como **delta** en la categoría nueva: aumentos contra gasto; disminuciones primero contra
   el gasto del mismo ejercicio y el resto contra recuperación, según `Contabilidad.DisminucionDeDeterioro`
   (`MismoEjercicioPrimero` por defecto).
3. General y adicional, con su traslado entre general e individual.

La categoría es la oficial del cierre; el primer cierre compara con el cierre base y no emite saldos iniciales.
K6a reutiliza los mismos hechos.

**Rationale**: en el CUIF el deterioro y el suspenso también van por categoría; liberar todo y constituir todo
inflaría gasto e ingreso. Amplía FR-107, que sólo nombra capital e intereses.

**Alternatives considered**
- Liberar todo el deterioro de la categoría anterior y constituirlo entero en la nueva: infla el gasto y el
  ingreso del mes.
- Reclasificar sólo capital e intereses: el deterioro de un crédito que pasó de A a C quedaría en la cuenta de A.

**Trazas**: T48 · FR-103, FR-104, FR-105, FR-107, FR-140, SC-006, US7-4 · DC-13 (preguntas C2, C4; enmienda de
FR-107 en T97).

**Difiere del informe**: DC-13 nombraba el hecho `CarteraReclasificada` y el reverso `CierreDeCarteraReversado`
(CH7).

#### R26 · Contabilidad no cierra un mes que Cartera no cerró

**Decision**
- Cartera emite cuatro informativos con recibo en `ACC_LendingPostings` y sin comprobante: `CarteraActivada`,
  `CargaDeCarteraAplicada`, `CierreDeCarteraRealizado`, `CierreDeCarteraReabierto`.
- `ClosePeriodCommand` gana `AcknowledgeLendingPending` y `AcknowledgeLendingLoad` y comprueba:
  (a) **bloqueo sin reconocimiento** `Accounting.Period.LendingNotClosed` si hay activación con fecha ≤ fin de M y
  no hay cierre de Cartera de M posterior a su última reapertura;
  (b) aviso `Accounting.Period.LendingPending` si hay hechos CAR `Pending`, `InBatch` o `Rejected` con fecha
  contable en M;
  (c) aviso `Accounting.Period.LendingLoadWillExpire` si la última carga aplicada sin activar tiene su día siguiente
  al corte dentro de M.
- Sin `CarteraActivada`, Contabilidad cierra como hoy (lo cubre `CarteraDesplegadaSinActivarTests`).

**Rationale**: FR-146 sin que Contabilidad lea `LND_` y sin un puerto inverso; el sentido sigue siendo Cartera →
Contabilidad por la bandeja (molde `PeriodoInventarioCerradoV1`).

**Alternatives considered**
- Puerto síncrono por el que Contabilidad pregunta a Cartera: interfaz fuera de la lista cerrada y dependencia
  inversa.
- Deducir el cierre de `DeterioroCalculado`: frágil cuando el cierre no tiene diferencias.
- Sólo dos informativos de cierre (PM): no dicen desde cuándo Cartera está activa ni avisan del corte de una carga
  (CH8).

**Trazas**: T57, T97 · FR-107, FR-142, FR-146, D12, US8-9 · DC-14 (CH8; enmienda A2 de FR-125).

#### R27 · Conciliación y cuadre de activación por conjuntos

**Decision**
- `IContabilidadParaCartera.SaldosDeCuentasMapeadasAsync(corte, combinaciones)` delega en
  `GetLendingAccountBalancesQuery`: resuelve cada combinación con la matriz vigente al corte, arma conjuntos
  combinación↔cuenta por familia de rol (`ConjuntosDeCuentas`, generalizado de la 012) y lee saldos sólo por
  `MovimientosContables` (apertura incluida).
- `ConciliacionContableDeCartera` compara por **fecha contable** capital e intereses por modalidad, sub-segmento y
  categoría, deterioro individual y general, anticipos, saldos a favor, suspenso, castigada y puentes, y explica
  cada diferencia con los hechos pendientes, en lote, rechazados o que no pasan. La misma consulta sirve al cuadre
  de activación con las combinaciones de la carga.
- Descuadre sin explicar → `Cartera.DescuadreContable`. Rutas `/api/lending/accounting/reconciliation?date=` y
  `/api/lending/accounting/facts`; pantalla `/cartera/conciliacion-contable`.

**Rationale**: si la contadora lleva varias categorías a una auxiliar, sólo se compara por conjunto; la
resolución de reglas vive en Contabilidad y Cartera no conoce cuentas (FR-129).

**Alternatives considered**
- Que Contabilidad devuelva todas las reglas y Cartera las resuelva: lleva la lógica de la matriz a Cartera.

**Trazas**: T58 · FR-130, FR-141, FR-143, SC-006, SC-013, US7-5 · DC-16.

**Conciliación**: C51 — la conciliación toma modalidad, sub-segmento, oficina y categoría del estado del crédito al
corte; cambiarlas exige `DimensionContableCambiada`, y la oficina sólo cambia por novedad de K3.

#### R28 · Puertos de la frontera e indicadores de la entidad

**Decision**
- `IContabilidadParaCartera` (en `Application/Common/Integration/Accounting`) declara exactamente cinco métodos:
  `EvaluarAsync`, `SaldosDeCuentasMapeadasAsync`, `EstadoDelPeriodoAsync(fecha)` (contabilidad iniciada, período
  existente, abierto o cerrado, primer mes abierto desde la fecha), `IndicadoresDeLaEntidadAsync(fecha)` y
  `CuentasBancariasAsync(bancoPublicId?)` (sobre `ListBankAccountsQuery`). Consultas detrás:
  `EvaluateLendingPostingQuery`, `GetLendingAccountBalancesQuery`, `GetAccountingPeriodStatusQuery`,
  `GetEntityIndicatorsQuery`.
- `IDimensionesDeCartera` (Cartera → Contabilidad, implementación `DimensionesDeCartera`): líneas con su
  modalidad, códigos de modalidad y sub-segmento, conceptos, clases de recaudador y combinaciones en uso, para
  validar códigos al guardar una regla y para la completitud (`GetLendingRulesCompletenessQuery`). Es interfaz
  nueva de la lista cerrada (enmienda A1).
- Indicadores: las pérdidas se derivan de cuentas (clases 4–7 del ejercicio sin el CI; 3510, 3610, 3910);
  patrimonio técnico y solvencia los registra el contador en `ACC_RegulatoryIndicators` con fuente y soporte
  (`RegisterRegulatoryIndicatorCommand`, `/api/accounting/regulatory-indicators`,
  `Accounting.RegulatoryIndicators.View/Manage`). La consulta devuelve el último con corte ≤ fecha; más viejo que
  `Contabilidad.IndicadoresVigenciaMeses` (6) es «no disponible».
- Sin indicadores vigentes: en la excepción de aportes del arrastre y el descuento de aportes en el deterioro
  (FR-100, FR-103) rige lo conservador de D19; en el **límite de concentración** (FR-041) se **bloquean** el
  desembolso, la asignación de cupo y la novedad que aumenten el endeudamiento
  (`Lending.Concentration.TechnicalCapitalUnavailable`) y se alerta `Cartera.PatrimonioTecnicoNoDisponible`.
- Lo fijan `CarteraNoConoceContabilidad` (las dos direcciones, por reflexión) y `LosHechosDeCarteraNoLlevanCuentas`.

**Rationale**: FR-008 pide lista cerrada y verificada; el patrimonio técnico tiene deducciones de norma que el
plan de cuentas no da; FR-041 no admite que exceder el límite sólo se reporte.

**Alternatives considered**
- Calcular el patrimonio técnico por prefijos de cuenta: posible después, pero sin cotejo puede dar un límite
  equivocado.
- Concentración contra el último valor registrado, con advertencia (DC-18): alternativa sometida en C3.
- Dejar sin validar los códigos de línea en la matriz: una regla mal escrita nunca resolvería.

**Trazas**: T59, T90, T97 · FR-008, FR-012, FR-041, FR-047, FR-100, FR-103, FR-127, FR-129, FR-130 · DC-17,
DC-18 (CH45; preguntas A1, C3).

**Difiere del informe**: DC-18 dejaba pasar la concentración contra el último valor con alerta; la transversal
bloquea. DC-17 nombraba las consultas sin verbo (`LendingAccountBalancesQuery`, `AccountingPeriodStatusQuery`,
`EntityIndicatorsQuery`, `LendingRulesCompletenessQuery`).

**Conciliación**: C78 — las pérdidas se derivan por **clase 3 y naturaleza débito**, no por código (3510, 3610,
3910 son sólo ejemplos; respeta `LaContabilidadNoTieneCuentasEnCodigo`). C33 — Contabilidad devuelve el último
indicador registrado **con su corte** y es Cartera la que decide si está vigente con
`Contabilidad.IndicadoresVigenciaMeses`. C27 — `IDimensionesDeCartera` vive en `Common.Integration.Lending`, con los
otros cuatro puertos que ofrece Cartera (cinco en total, T90).

#### R29 · Retiro de los escritores heredados y enmienda de la 009

**Decision**: con el retiro de K1 se van `DisburseLoanCommand` (DS), `RecaudoDeCredito` (RC) con
`ProcessPaymentCommand` y la llamada desde la definitiva (pasa al contrato), `AccrueInterestCommand`,
`ClassifyPortfolioCommand` y `ProcessPayrollDeductionCommand` (marcador «E3 (feature 009)»), las cuentas como texto
de `LND_CreditLineParameters`, `LND_PortfolioAccounts` y `LND_ProvisionParameters` (con sus tablas) y la rama de
`AccountReferenceFinder` que las lee. Se conservan `LiquidateSavingsInterestCommand` (se muda a `Savings`) y
`LiquidateCDTInterestCommand` (sin cambios). Las filas de Cartera del contrato de la 009 remiten a esta enmienda.

**Rationale**: C3 y FR-146: un solo camino al libro; si el código de ahorros siguiera en `Lending`, la prueba de
frontera daría falsos positivos.

**Alternatives considered**
- Mantener `RecaudoDeCredito` como adaptador temporal de la definitiva: es un camino al libro que E-01 cierra.

**Trazas**: T96, T88 · FR-144, FR-146, FR-147, C3, C4 · DC-19. La definitiva no puede quedar sin recaudo: el
contrato sale en el mismo paso 1 de T88.

### 2.3 Riesgos

| Riesgo | Mitigación |
|---|---|
| Tocar el consumidor contable de Inventario (enrutador, base común extraída) | Capa delgada; «Inventario e2e» verde sin cambios |
| Roles que dependen del criterio de la contadora (`CargoPorCobrar` frente a `PagoPorCuentaDelAsociado`, `IngresoDiferido`) | Sembrados sin cuenta, «pendiente de validar»; la completitud lista lo que falta (C7) |
| Explosión de reglas (5 categorías × modalidades × conceptos) | Plantilla de importación (`ImportLendingPostingRulesCommand`) y semilla sugerida sin cuentas |
| Resumido diario con línea por crédito: unas 20.000 líneas al día con 20.000 créditos | `RUN_PERF_TESTS`; granularidad `PorDiaYSucursal`; la contadora puede quitar «exige tercero» en intereses (C5) |
| Nómina cambia en producción: `DESC_CARTERA` contabiliza | La activación exige la regla del puente de Nómina y avisa si el concepto no tiene cuentas (códigos que ya existen, §7.6 de las transversales: `Payroll.Settlement.ConceptAccountsMissing` en la definitiva, `Payroll.ConceptWithoutAccounts` en la ordinaria) |
| Pagos aplicados sin regla quedan rechazados en la bandeja | Conciliación y alerta; se crea la regla y se reprocesa |
| Cambio de la cuenta de un puente entre un pago y su reverso | Residuo en la cuenta vieja; se corrige con un `CG` de reclasificación |
| Latencia del informativo de cierre | Segundos en línea; el aviso (b) ve el reverso pendiente |
| Contabilidad cierra el mes entre la emisión y el consumo | Imposible para un mes que Cartera no cerró (R26); `PeriodClosed` → `Rejected` |
| Indicadores desactualizados | Vencen a los 6 meses con alerta al contador; la concentración bloquea (C3) |
| Nuevo ámbito de parámetros sobre una plataforma que sólo resuelve Inventario | Resolutor compuesto (R60); pruebas de los dos ámbitos |

---

## 3. Frente del motor financiero

### 3.1 Hechos del código

**Moldes que se copian**

- **Motores puros de Nómina.** `PayrollCalculationEngine` es sellado, sin IO ni EF, recibe un empleado y devuelve
  líneas explicadas; se niega nombrando el parámetro faltante (`ParameterSet.Missing`); redondea `AwayFromZero`.
  `InputsHasher` arma a mano un SHA-256 de la entrada normalizada (prefijo «v1|», colecciones ordenadas por código
  o id) para que el orden de EF no cambie la huella. `SettlementCalculationEngine` reutiliza `Explanation`,
  `ParameterSet` y el calendario 30/360 con su propio hasher: un módulo puede tener varios motores que comparten
  explicación y huella. — `src/Core/IngenIA365ERP.Domain/Payroll/Calculation/{PayrollCalculationEngine,
  InputsHasher, Explanation, ParameterSet}.cs`; `Domain/Payroll/Settlements/SettlementCalculationEngine.cs`
- **Motor de costeo de la 012.** `MotorDeCosteo` es fachada estática con `DateOnly`: recibe el estado de un
  ámbito, un movimiento y los parámetros, y propone líneas, estado nuevo y explicación sin escribir; el único
  escritor es `RegistroDeKardex`. `Retroactivo` inserta movimientos con fecha anterior en el orden
  (`OperationDate`, `Id`), recalcula los posteriores y propone ajustes enlazados sin reescribir nada: es el análogo
  más cercano a la reconstrucción por fecha valor. — `Domain/Inventory/Costing/{MotorDeCosteo, Retroactivo,
  ExplicacionDeCosto}.cs`; `Application/Inventory/Kardex/RegistroDeKardex.cs`
- **Redondeo de la 012.** Siempre `AwayFromZero`, al centavo o al peso (`RedondeoDeMontos`); `Repartir` deja el
  residuo visible. No hay unidad (decena, centena), modo (al par, arriba, abajo) ni momento (por concepto, cuota o
  total) como pide FR-010. — `Domain/Inventory/Costing/Redondeo.cs`
- **Precisión por alias.** `PrecisionDeInventario` (`Cantidad` 18,4; `Factor`, `CostoUnitario`, `PrecioUnitario`
  18,6; `Monto` 18,2; `Tarifa` 9,6 como fracción). La convención global es 18,2 y PostgreSQL `numeric(18,2)`
  redondea en silencio; `LasCantidadesYCostosTienenSuPrecision` exige el alias. Una tasa diaria equivalente
  (0,000611542…) no cabe en 9,6 sin perder centavos. — `Persistence/Configurations/Inventory/PrecisionDeInventario.cs`
- **Hechos inmutables.** `IHechoInmutable` es un marcador; `GuardaDeInmutabilidad` (desde `SaveChangesAsync`)
  rechaza `Deleted` y `Modified` salvo propiedades `[EscrituraUnica]` que pasan de nulo a valor;
  `IInmutableTrasConfirmar` admite una lista cerrada; los vigila `LosHechosInmutablesNoSeModifican`. —
  `Domain/Common/{IHechoInmutable, IInmutableTrasConfirmar, EscrituraUnicaAttribute}.cs`;
  `Persistence/DbContext/GuardaDeInmutabilidad.cs`
- **Hecho de alto volumen.** `KardexEntry` hereda `AuditableEntityLong` (bigint), es `IHechoInmutable` y lleva
  `[SinDiffDeAuditoria]`; su orden es (`OperationDate`, `Id`); no guarda saldo acumulado; ~9 millones de filas al
  año a escala de referencia. — `Domain/Entities/Inventory/Transactions/KardexEntry.cs`
- **Casos dorados y rendimiento.** 23 casos de Nómina y 27 de costeo (`CasoDoradoDeCosteo` lee JSON con
  comentarios y enums por nombre); `RendimientoTests` mide el motor solo (200 empleados < 10 s);
  `RepetibilidadTests` exige misma huella y mismas líneas; las de volumen de integración sólo con
  `RUN_PERF_TESTS=1`. — `tests/IngenIA365ERP.Domain.Tests/{Payroll/Calculation, Inventory/Costing}/*`

**Restricciones de la plataforma**

- **Principio XI ya prevé Cartera.** La constitución nombra `LND_LoanTransactions` entre las tablas inmutables y
  `ReversesTransactionId` como enlace del reverso; la heredada es `LND_Transactions` (`LendingTransaction`, con
  `AccountCode` y débito/crédito). `PrincipioXI_ContableImmutable` ya protege `Entities/Lending/Transactions`, hoy
  vacío: se activa sola. — `.specify/memory/constitution.md`; `tests/.../Principles/PrincipioXI_ContableImmutable.cs`
- **Cerrojo pesimista de la 012.** `ICerrojoDeInventario` fija orden canónico (setup compartido, exclusivo al
  cerrar; luego filas por `Id`; numeración al final) y lanza sin transacción; `SqlDelCerrojo` genera `FOR UPDATE`
  / `FOR SHARE` ordenado (PostgreSQL) y `UPDLOCK, ROWLOCK, HOLDLOCK` (SQL Server) y crea antes las filas de
  proyección que falten. **No hay ningún bloqueo con salto** (`SKIP LOCKED` / `READPAST`) en el código. —
  `Application/Inventory/Common/ICerrojoDeInventario.cs`; `Persistence/Inventory/{CerrojoDeInventario, SqlDelCerrojo}.cs`
- **Concurrencia optimista.** `ReintentoPorConcurrenciaBehavior` reintenta 5 veces por `RowVersion`; la 012 lo
  descartó para la contención de existencias. — `Common/Behaviors/ReintentoPorConcurrenciaBehavior.cs`
- **Fecha local.** `IDateTimeService.HoyLocal` (`DateOnly`, America/Bogota, −05:00 de respaldo); Inventario, POS y
  lotes lo usan, los demás módulos siguen con `TodayUtc`, que después de las 19:00 ya dice mañana. —
  `Common/Interfaces/IDateTimeService.cs`
- **Parámetros no bitemporales.** `AgregarVigenciasAsync` rechaza una vigencia que empiece el mismo día o antes
  (`Parameters.Overlaps`) y **sobrescribe** el `ValidTo` de la anterior: no se corrige un valor pasado y «conocido
  hasta R» sólo se reconstruye con `CreatedAt` (`AuditableEntityInterceptor`). Sin ámbito de línea. —
  `Common/Parameters/AddParameterVersion/AddParameterVersionCommandHandler.cs`
- **Literales legales.** `LaNominaNoTieneValoresLegalesFijos` y `ElComercioNoTieneValoresLegalesFijos` sólo admiten
  0, 1, 2, 0.5, 12, 15, 30, 100 y 360 (con `m`); 365 no está. — `tests/.../Principles/*ValoresLegalesFijos.cs`
- **Calendarios existentes.** `CalendarConventions` (Nómina) cuenta días comerciales **inclusivos** para
  prorratear; `DiasHabiles` es pura y recibe los festivos (`PAY_Holidays`, por cooperativa). Ninguno da la
  convención de causación (a excluida, b incluida) ni la fecha exigible del art. 829. —
  `Domain/Payroll/Calculation/CalendarConventions.cs`; `Domain/Payroll/Settlements/Calendar/DiasHabiles.cs`

**La cartera heredada**

- **Matemática no reutilizable.** `DisburseLoanCommand` calcula la tasa mensual nominal (`rate/100/12`) y la cuota
  francesa con `Math.Pow` en `double`, redondeada a 0 (la misma fórmula se repite en la aprobación, la solicitud
  y sus consultas). `AccrueInterestCommand` causa lineal /360 sobre un saldo mutable y la mora con
  `InterestRate * 1.5m` fijo. En `src/Core` no hay potencia ni raíz en `decimal`. —
  `Application/Lending/LoanApplications/Commands/DisburseLoan/DisburseLoanCommand.cs`;
  `Application/Lending/Accruals/Commands/AccrueInterest/AccrueInterestCommand.cs`
- **Lo que existe.** No hay `Domain/Lending` ni `Domain/Enums/Lending`; las 93 entidades heredadas viven en
  `Domain/Entities/Lending` con códigos de texto y saldos mutables (`LoanPortfolio.CurrentBalance`,
  `PendingInstallment`, `AccrualEntry`), mezclando crédito con ahorros, caja y SIPLA. — `Domain/Entities/Lending/`

### 3.2 Decisiones

#### R30 · Dónde vive el motor y fecha local

**Decision**
- Motor puro en `Domain/Lending/{Rates, DayCount, Schedules, Accrual, Delinquency, Payments, Replay}` con la
  fachada estática `MotorDeCartera` (molde `MotorDeCosteo`) y, en la raíz, `RedondeoDeCartera`,
  `ExplicacionDeCartera`, `HuellaDeCartera`, `VersionesDeReglas`. `CalendarioHabil` vive en
  `Domain/Common/Calendar` (lo usan la plataforma y Cartera). Sin IO, sin EF, `DateOnly`, sólo `decimal`; recibe
  todo resuelto y devuelve estado, libro teórico y explicación.
- Enums en `Domain/Enums/Lending`; entidades en `Domain/Entities/Lending/{Loans, Schedules, Operations,
  Transactions, Projections, …}`.
- Application: `CargadorDeReconstruccion` (lectura en bloque 1..N), `ISeriesRegulatorias` (sobre los lectores
  únicos, R50), `RegistroDeCartera` (único escritor del libro, la marca de agua y las posiciones),
  `ICerrojoDeCartera`. Persistencia: `Persistence/Lending/{CerrojoDeCartera, SqlDelCerrojoDeCartera}` y
  `PrecisionDeCartera`.
- Todo `Lending` usa `IDateTimeService.HoyLocal`; ningún `TodayUtc` (`LaCarteraUsaLaFechaLocal`).

**Rationale**: Principio II y el molde probado de 005, 010 y 012: motor determinista que se prueba sin base con
casos dorados (SC-023) y un único escritor. La reproducibilidad (FR-011) exige que el cálculo no dependa de lo que
EF tenga cargado. Causación, fecha valor y mora son por día local.

**Alternatives considered**
- Calcular en los handlers con EF, como el legado: no reproducible, `double`, saldos mutables.
- Motor instanciable con diccionario de reglas (molde Nómina): las formas son un enum cerrado; basta la fachada.
- `CalendarioHabil` en `Domain/Lending/DayCount` (M1): la plataforma no puede depender de un módulo (CH34).

**Trazas**: T4, T9, §2.2, §2.8 · FR-008, FR-010, FR-011, FR-012, FR-062–FR-082, SC-001, SC-023, Principio II ·
M1, M16 (CH34, CH48).

**Difiere del informe**: M1 ponía `CalendarioHabil` en `Domain/Lending/DayCount`, el servicio de fondo como
`ProcesoDiarioDeCartera` y `ISeriesRegulatorias` leyendo directamente usura, índices y festivos.

#### R31 · Tres capas: hecho primario, libro y proyecciones

**Decision**
- **Hecho primario**: `LoanOperation` (`LND_LoanOperations`, H, bigint) es la decisión de una persona o un
  proceso y **es el comprobante de la operación**: tipo (`LoanOperationKind`), fecha valor y contable,
  `RegisteredAt` y `Sequence` monótonos por crédito, entradas canónicas, huella, `RulesVersion`, `Number`,
  recaudador y clave (UK `CollectorCode`, `IdempotencyKey`), clase de medio, pagador, referencia del recibo, huella
  de la simulación. No hay `LND_PaymentApplications`.
- **Libro**: `LoanTransaction` (`LND_LoanTransactions`, H, bigint, en `Entities/Lending/Transactions`): una fila
  por concepto (`LoanConcept`) y cuota, `LoanTransactionKind`, importe con signo, tres fechas,
  `ReversesTransactionId` y el `MessagePublicId` del hecho; nunca se edita; saldo = Σ de sus filas.
- **Proyecciones**: `RepaymentScheduleVersion`/`RepaymentInstallment` (plan por versión, H),
  `LoanDailyPosition` (instantánea versionada por crédito y día, sólo inserción, bigint) y `LoanControl` (una fila
  por crédito **sin saldos**: `LastAccruedDate`, `Sequence`, `LastRegisteredAt`; fila del cerrojo y marca de
  agua). Más `LoanTermsVersion` (condiciones selladas, H) y `PrepaymentPreference` (H).
- La autoridad es la reconstrucción (R32); lo materializado se verifica contra ella.
- El evento de auditoría de cada `LoanOperation` lleva la **distribución completa** por cuota y concepto (el
  «antes y después» de FR-003).

**Rationale**: FR-081 y SC-004 exigen redistribuir pagos posteriores, posible sólo si el pago se guarda como
decisión y su distribución se deriva; Contabilidad y la conciliación necesitan filas con fecha contable; usar el
nombre del Principio XI resuelve I-5 sin enmienda.

**Alternatives considered**
- Saldos mutables en el crédito (`LoanPortfolio.CurrentBalance`): violan R1 de la spec e I-6.
- Sólo el libro, con la distribución como entrada: no se reconstruye un reverso con pagos posteriores.
- Event sourcing sin libro: Contabilidad necesita filas con fecha contable.
- Tabla de causación con columnas por concepto: rompe «una transacción = un concepto» (FR-001).
- Tablas `LND_PaymentApplications`/`…Lines` y nombres `LND_LoanScheduleVersions`, `LND_LoanInstallments`,
  `LND_LoanBalanceSnapshots` (R-06): duplican la distribución y la clave (CH25, CH27).

**Trazas**: T23, T67 · FR-001, FR-002, FR-003, FR-012, FR-069, FR-077, I-5, I-6, Principios VII y XI · M2
(CH25, CH27).

**Conciliación**: C45 — `RegistroDeCartera` escribe también `LND_LoanOperations`, `LND_LoanTermsVersions` y el
plan, y `NadieEscribeElLibroDeCarteraFueraDelRegistro` las vigila. C43 — `LND_RepaymentInstallments` va en `bigint`.
C47 — «registra la nueva tasa» (FR-067) es una operación `RateReset` (numerada, hecho) con su versión de
condiciones y sin filas de libro. C46 — `LoanControl` lleva `[SinDiffDeAuditoria]`.

#### R32 · Liquidación a la fecha D con la información hasta R

**Decision**
- `ReconstruccionDeCredito.Reconstruir(EntradaDeReconstruccion, D)` es la **única** fuente de saldos, cuotas, mora
  y suspensión.
- Entran (por `CargadorDeReconstruccion`): operaciones con `RegisteredAt ≤ R` sin reverso registrado ≤ R;
  versiones de condiciones ≤ R; valores nacionales y parámetros conocidos en R (R48).
- Algoritmo: ordena por (fecha valor, `Sequence`); plan inicial o migrado; avanza **por segmentos entre eventos**
  (vencimientos y fechas exigibles, operaciones, cambios de usura, índices y parámetros, cruces de umbral, fin de
  gracia, D) con acumulados en forma cerrada: costo por eventos, no por días.
- Sale `EstadoDelCredito` (saldos por cuota y concepto, capital vencido, altura, suspensión, tasa del día,
  anticipo, saldo a favor, plan), `LibroTeorico` por (día valor, concepto, cuota) y `ExplicacionDeCartera` por
  tramo.
- D futura: se proyecta sin pagos y devuelve `ValidaHasta`. Consultar no escribe; sólo audita la lectura. El
  documento emitido guarda `HuellaDeCartera` y `RulesVersion`.

**Rationale**: FR-011, D25 y SC-001: misma D y mismo R dan el mismo resultado al centavo aunque después se
registre un pago con fecha valor anterior. Por segmentos, menos de 1 ms por crédito aun a 20 años.

**Alternatives considered**
- Sumar el libro hasta R y proyectar lo que falte: depende de cuándo corrió el proceso y mezcla fuentes.
- Instantánea diaria como punto de partida: una fecha valor anterior la invalida.
- Reconstruir día por día: O(días × cuotas), innecesario.

**Trazas**: T24, T38 · FR-007, FR-011, FR-068, FR-069, D25, SC-001, US4-5, US4-6 · M3 (pregunta D11).

#### R33 · Causación por diferencia de acumulados

**Decision**
- Lo causado el día d por concepto y cuota es `U(A(d)) − U(A(d−1))`, con `A` el acumulado exacto desde el origen o
  el corte migrado y `U` el redondeo sellado. Ventana compuesta `C·((1+r_d)^n − 1)` o lineal `C·r_d·n`; se reinicia
  al cambiar el capital base o la tasa.
- Bases: corriente sobre capital no vencido (incluido el de una cuota vencida en día no hábil hasta su fecha
  exigible); mora sobre capital vencido (sobre intereses sólo si `Mora.SobreIntereses` y la regla sellada lo
  permiten, nunca en vivienda); seguros según su periodicidad. El interés nunca entra a la base.
- Idempotencia: causar d exige `LoanControl.LastAccruedDate = d−1`; repetir d no hace nada. El proceso recorre de
  la marca + 1 a hoy; toda operación primero pone al día su crédito, en su propia transacción.

**Rationale**: con equivalencia compuesta el período completo da exactamente la tasa del período (FR-010); el
interés de la cuota resulta igual al causado entre vencimientos, al centavo y sin deriva.

**Alternatives considered**
- Interés simple diario `C·r_d` (legado /360): el mes no da la tasa del período (26 % EA: 1,9266 % frente a
  1,9446 %).
- Redondear cada día por separado: deriva de centavos.
- Causar sólo al vencer: contradice FR-062 y D8.
- Una fila de control por crédito y día: 18 millones de filas al año sin necesidad.

**Trazas**: T25, T6 · FR-010, FR-023, FR-062–FR-064, FR-066, SC-020, US4-1, US4-2, US4-4 · M4 (CH2; preguntas D9,
D14).

**Conciliación**: C04 — la causación con que una operación pone al día su crédito crea sus propias partes en
`LND_AccountingFactParts` (una por tipo, la operación como tanda), con origen la parte, clave
`Confirmation:{partePublicId:N}` y el modo de los masivos; sus `LoanTransaction` llevan ese `MessagePublicId`.

#### R34 · Aritmética decimal y equivalencia de tasas

**Decision**
- `AritmeticaDecimal`: potencia por cuadrados, raíz por Newton hasta coincidir a 24 decimales (con tope), TIR por
  Newton con respaldo de bisección.
- `EquivalenciaDeTasas`: `r_d = RaizEntera(1+EA, B) − 1` redondeada a 18 decimales —la **única** aproximación del
  motor, escrita en la explicación—; B = 360 en 30/360 y real/360, 365 en real/365; tasa del período
  `(1+r_d)^n − 1`; lineal `NA_m = m·((1+EA)^(1/m) − 1)` y `r_d = NA_m/B`; la nominal informativa (FR-020) por la
  misma vía.
- Prohibidos en `Domain/Lending`: `double`, `float`, `Math.Pow`, `Math.Exp`, `Math.Log` (`ElMotorDeCarteraEsDecimal`).
  Los casos dorados se calculan con decimal de 40 dígitos, no en Excel.

**Rationale**: `decimal` es software y da lo mismo en toda plataforma; con 18 decimales el error sobre $1.000
millones es menor que 10⁻⁶ pesos. Una raíz de (1+EA) no es exacta en ningún tipo: la precisión a FR-010 queda
escrita.

**Alternatives considered**
- Series de ln y exp en decimal: más código y lento, mismo resultado.
- Guardar la tasa diaria en 9,6: unos $50 al día sobre $100 millones.
- Racionales con `BigInteger`: la raíz sigue inexacta.

**Trazas**: T26 · FR-010, FR-020, SC-001, SC-023 · M5 (pregunta D1).

#### R35 · Convenciones de días y calendario hábil

**Decision**
- `ContadorDeDias.Dias(convención, a, b)` cuenta de a excluida a b incluida: `Thirty360` (el 31 no causa; el fin
  de febrero completa 30), `Actual365` (base fija; el 29 de febrero causa), `Actual360`.
- `CalendarioHabil` (puro, `Domain/Common/Calendar`) recibe la lista de días no hábiles y da `FechaExigible(t)`.
  La plataforma la sirve por `ICalendarioHabil` (`CalendarioDeDiasHabiles`) con los festivos **nacionales**
  replicados; el motor los recibe en `EntradaDeReconstruccion`. La regla del art. 829 no se configura.
- `FechasDeVencimiento` genera vencimientos por periodicidad (semanal, decadal, quincenal, mensual, bimestral,
  trimestral, semestral, anual, pago único) con la regla del día de pago (si no existe, último del mes).
- No se reutilizan `CalendarConventions` ni `DiasHabiles` de Nómina.

**Rationale**: los casos límite de fechas piden tres convenciones selladas y la fecha exigible separada del
vencimiento, para que el interés de esos días se cause y se cobre en la cuota siguiente.

**Alternatives considered**
- Calendario de Nómina: prorrateo inclusivo y acople a `PAY_Holidays`.
- Actual/Actual ISDA: no la pide la spec.
- Puerto `ICalendarioDeFestivos` (F-05): duplica `ICalendarioHabil` (CH34).

**Trazas**: T27, T40 · FR-010, FR-022, FR-070 · M6, inf. R5, F-05 (CH34; preguntas D2, D3, E5).

#### R36 · Usura dentro del motor

**Decision**
- `ControlDeUsura` compara la **efectiva resultante** (TIR diaria de los flujos con fechas reales y cargos
  computables, anualizada) con la usura de su certificación al crear o modificar la línea (plan tipo por rango),
  simular, aprobar y desembolsar; bloquea con `Lending.Usury.Exceeded`.
- Al desembolsar sella la sobretasa de cargos (efectiva con cargos − sin cargos).
- `TasaDelDia` topa cada día la corriente (`min(pactada o reajustada, Tope(U_d − sobretasa))`) y la mora
  (`min(pactada o relativa, Tope(U_d))`); `Tope` convierte la usura a la tasa pactable cuya efectiva bajo la
  convención no la supera. Si topa, el estado lo marca y se alerta (`Cartera.UsuraTopada`).
- Una usura o cualquier valor nacional corregido después de causar días que lo usaban se aplica como
  «reliquidación por corrección de valor nacional»: corrida `NationalValueCorrection` del `ProcesadorDeCartera`,
  que nadie ordena a mano y corre como el proceso por tandas (`RunNationalValueCorrectionCommand`); registra ajustes
  (`AjusteDeCausacionPorFechaValor`) y, si se cobró de más, saldo a favor (`SaldoAFavorReconocido`).

**Rationale**: el caso límite prohíbe comparar la nominal; SC-002 exige cero días por encima; sellar la sobretasa
evita recalcular la TIR cada noche; la devolución no puede esperar a que alguien la ordene (la corrección la
registra el maestro en la central, fuera de la cooperativa).

**Alternatives considered**
- Comparar sólo la tasa pactada: prohibido por el caso límite.
- Topar el interés del período y no el del día: incumple SC-002.
- Recalcular la TIR cada día: costoso e inestable.

**Trazas**: T28, T11 · FR-020, FR-021, SC-002 · M7 (pregunta D10).

**Conciliación**: C05 — la corrida la encola la réplica (`EfectoDeReplicacionCorreccionDeValorNacional`, en la misma
transacción y sólo con Cartera activada), no el primer paso del ciclo. C03 — sus `AjusteDeCausacionPorFechaValor` van
por partes (`PorLotes`, `CA`, `Confirmation:{partePublicId:N}`). C02 — su `SaldoAFavorReconocido` va por crédito,
con origen la parte de su tanda, modo propio del tipo y clave `Confirmation:{loanPublicId:N}`.

#### R37 · Plan de pagos y versión de reglas

**Decision**
- `GeneradorDePlan` proyecta la causación de R33 suponiendo pagos a tiempo: cuota fija por valor presente con los
  factores reales de cada período (primer período irregular, extraordinarias, gracia), abono constante, pago
  único; gracia a capital (sólo interés) o total (sin cuotas; el interés no capitaliza y va a la primera cuota o se
  distribuye). Σ capital = monto (SC-017); la última cuota absorbe el resto.
- Cada operación que cambia el plan produce una versión **regenerada desde la decisión** (abono con elección,
  reajuste, nota de Comercial, tramo; desde K3, novedades).
- `LoanTermsVersion` sella `RulesVersion` (`LND-AAAA.n`): un cambio de algoritmo que mueva centavos es versión
  nueva y sólo aplica a créditos o novedades nuevos.

**Rationale**: regenerar desde la decisión permite FR-081 y SC-004; `RulesVersion` protege FR-011 frente a cambios
del programa (como el layout versionado de la PILA).

**Alternatives considered**
- Fórmula francesa con nominal mensual (legado): inexacta con días reales y primer período irregular.
- El plan como dato primario sellado: un reverso no lo reconstruye.

**Trazas**: T29 · FR-022, FR-023, FR-031, FR-034, FR-067, FR-075, SC-017 · M8 (preguntas D5, D12).

#### R38 · Mora, altura y suspensión derivadas de la reconstrucción

**Decision**
- El capital de la cuota vence el día siguiente a su fecha exigible; hasta ese día causa corriente, que se cobra
  en la cuota siguiente (FR-070).
- `AlturaDeMora`: desde la cuota impaga más antigua cuyo saldo vencido supere `Mora.ToleranciaDeSaldoVencido`,
  sin las cubiertas por anticipo (y desde K2 sin las «descontado pendiente de giro» dentro del umbral de la
  pagaduría; la entrada existe desde K1).
- `ReglaDeSuspension`: altura ≥ umbral vigente de la modalidad, reestructurado más de una vez, insolvencia o
  castigo. Lo suspendido se causa en `CurrentInterestInSuspense` / `DefaultInterestInSuspense` (el castigo usa los
  mismos con su marca); lo causado antes no se reclasifica (lo deteriora SARC al 100 %).
- Las transiciones salen del estado; Application emite `CausacionSuspendida` y `CausacionReanudada`.

**Rationale**: una marca puesta por el proceso no se reconstruye ante una fecha valor anterior; FR-114 y SC-016
piden que la pagaduría dentro de su ventana no impute mora.

**Alternatives considered**
- Suspensión como marca del proceso diario: no se reconstruye.
- Reclasificar lo causado antes de suspender: lo prohíbe FR-103.

**Trazas**: T30 · FR-063, FR-065, FR-069, FR-070, FR-096, FR-114 · M9.

**Difiere del informe**: M9 usaba el parámetro `Cartera.ToleranciaDeMora`; es `Mora.ToleranciaDeSaldoVencido`
(CH36).

#### R39 · Aplicación de pagos

**Decision**
- `AplicadorDePagos.Distribuir` simula y aplica con la **misma** función. Prelación sellada (`OldestInstallmentFirst`
  por defecto: gastos informados, seguros, mora con su suspenso, corriente con su suspenso, capital; alternativa
  `ConceptFirst`).
- Siete modalidades (`PaymentModality`): total (aplica el anticipo del mismo crédito); vencidas; cuota del ciclo
  (lo no causado queda como anticipo de esa cuota, no reduce intereses); anticipo (se aplica al vencer; parcial si
  no alcanza); abono extraordinario (elección del deudor o su preferencia vigente; sin preferencia, saldo a
  favor); cuota específica; valor libre. Lo que sobra es excedente y vuelve al recaudador.
- Todo dinero que reduce una obligación entra por el contrato: **no hay comando de pantalla para aplicar pagos**;
  Cartera simula con `SimulateLoanPaymentQuery` y reversa con `ReverseLoanPaymentCommand`, que rechaza las
  operaciones con `CollectorClass = Payroll` (`Lending.Reversal.OwnedByCollector`).

**Rationale**: FR-072 exige distribución idéntica al simular y al aplicar; D5, D6 y D24; el deudor decide en un
abono (Ley 2032); un pago de Nómina sólo lo reversa Nómina, así el NM y `PAY_PayrollLoanDeductions` nunca
conservan un descuento que en Cartera ya no existe.

**Alternatives considered**
- `ApplyLoanPaymentCommand` (M10): todo dinero entra por el contrato (CH47).
- Lo que sobra del valor libre a capital: le quita la decisión al deudor.
- Cuota del ciclo antes del vencimiento que reduce intereses: contradice FR-074.

**Trazas**: T31, T91 · FR-030, FR-071–FR-077, FR-080, FR-087, D5, D6, D24, SC-003 · M10 (CH47; preguntas D6, D7,
D8).

**Conciliación**: C37 — hasta que la feature de Aportes declare su destino, un pago `Contributions` tampoco se
reversa desde Cartera (`Lending.Reversal.OwnedByCollector`). C30 — los medios `AssociateCredit` y `CustomerCredit`
se declaran y el contrato los rechaza (`Lending.Payment.MeansClassNotAllowed`): pagar crédito con crédito es
recogida o novación. C85 — el cambio de preferencia de prepago exige el permiso nuevo
`Lending.PrepaymentPreferences.Manage`.

#### R40 · Simulación sin estado con huella

**Decision**: simular es reconstruir y distribuir sin escribir; `SimulationId` = SHA-256 canónico
(`HuellaDeCartera`, molde `InputsHasher`) de las entradas (crédito, modalidad, valor, fecha valor, elección) y la
distribución (líneas, plan nuevo, excedente). Aplicar en forma interactiva recalcula bajo el cerrojo y compara: si
difiere, `Lending.Payment.DistributionChanged` con la simulación nueva. La forma sin interacción nunca responde
«cambió». Un abono extraordinario devuelve los dos planes (plazo y cuota), cada uno con su huella. La simulación
no se guarda.

**Rationale**: comparar la distribución, no un contador, evita «cambió» falsos cuando sólo corrió la causación;
una consulta no cambia estado (Principio III); queda muy debajo de los 2 s de SC-008.

**Alternatives considered**
- Tabla de simulaciones con vencimiento: escribe en una consulta y necesita limpieza.
- Comparar `LoanControl.Sequence`: «cambió» falso.
- Huella firmada con HMAC: innecesaria; una huella falsa simplemente no coincide.

**Trazas**: T32 · FR-073, FR-079, FR-085, SC-003, SC-008, US5-6 · M11.

**Conciliación**: C39 — «la distribución cambió» rige sólo para la forma interactiva; la no interactiva espera el
cerrojo y aplica sobre el estado nuevo. C38 — `SimularAsync` del contrato admite `PaymentIdempotencyKeyToReverse`
para mostrar la redistribución antes de un reverso (FR-081).

#### R41 · Fecha valor anterior y reversos

**Decision**
- En un reverso (de pago o de desembolso, FR-048) **cada fila propia de la operación reversada recibe su fila
  espejo** con `ReversesTransactionId`; en el desembolso reversado también las de la causación del crédito.
  **Desembolso por tramos** (C49): el reverso de un tramo espeja **sólo el capital del tramo**; la diferencia de
  causación la produce el recálculo desde la fecha valor del tramo como `AccrualAdjustment` (el mismo mecanismo que lo
  fechado atrás) y no se espeja la causación de los demás tramos.
- Después se reconstruye con y sin la operación y `RegistroDeCartera.Conciliar` registra cada diferencia restante
  por (día valor, concepto, cuota) como transacción nueva (`AccrualAdjustment`, `PaymentReallocation`,
  `SuspenseReclassification`) con fecha contable del período abierto, enlazada a la operación de reverso. Lo
  mismo, sin espejos, para un pago con fecha valor anterior.
- Los pagos posteriores se redistribuyen con su fecha valor y elección, y se muestran antes de confirmar.
- Invariante: Σ libro a d = reconstrucción(d, ahora) para todo d ≤ `LastAccruedDate`
  (`VerifyLendingLedgerIntegrityQuery`: muestra nocturna en el ciclo y completa bajo pedido,
  `/api/lending/integrity/verify`, `Lending.Integrity.Verify`).
- El límite D4 (`Pagos.DiasMaximosFechaValor`) y la atribución antes de un cierre (`Lending.Payments.BackdateBeforeClose`)
  los aplica Application, con la excepción de los pagos sin interacción de fecha valor reglada (Nómina; desde K2,
  la pagaduría con referencia; enmienda A9).

**Rationale**: el crédito debe quedar exactamente como si el pago no hubiera existido, sin editar nada (FR-002,
SC-004); ajustar por día mantiene la igualdad libro = reconstrucción y permite estados de cuenta a cualquier fecha.
Lo fija `ElReversoEnlazaCadaFilaConSuOriginalTests`.

**Alternatives considered**
- Un ajuste agregado por concepto en la fecha valor: la Σ por día deja de coincidir.
- Reescribir las causaciones: lo prohíbe el Principio XI.
- No redistribuir los posteriores: rompe FR-081 y SC-004.
- Sólo ajustes por diferencia, sin espejo (M12): la cadena original → reverso no se vería desde los dos lados.

**Trazas**: T33, T92 · FR-002, FR-012, FR-048, FR-078, FR-080, FR-081, SC-004, US5-5 · M12 (preguntas D4, D15,
D16; enmienda A9).

**Difiere del informe**: M12 sólo espejaba las líneas del pago reversado; la transversal espeja toda fila propia,
también en el reverso de desembolso.

**Conciliación**: C49 — con varios tramos rige el espejo sólo del capital del tramo (arriba). C11 — si el desembolso
reversado recogió créditos y el crédito se anula, el reverso reversa en la misma transacción los pagos de recogida
(claves `CAR:`) y los recogidos vuelven a su estado; si queda `PendingDisbursement`, se conservan. C50 — el índice
único de la causación se filtra a `Kind = Accrual` y `ReversesTransactionId` nulo, así espejos y ajustes no chocan.
C41 — en el contrato, `Lending.Payments.BackdateBeforeClose` se consulta por `IPermissionChecker` con los permisos y
el límite del usuario que actúa (no su alcance).

#### R42 · Concurrencia por deudor y por crédito

**Decision**
- `ICerrojoDeCartera` en orden fijo dentro de `TransaccionExplicita` (lanza si no la hay): `LND_Setup` compartido
  (exclusivo al cerrar o reabrir) → `LND_DebtorControls` exclusivos por `Id` (deudor y codeudores de la operación;
  la fila se crea si falta) → `LND_LoanControls` exclusivos por `Id` (todos los de la operación en una sentencia)
  → numeración al final (`NumeradorDeCartera`).
- El paso por deudor lo toman la reserva, la aprobación, el desembolso y la asignación de cupo: sin él, dos de ellas
  del mismo deudor leerían el mismo disponible.
- Las operaciones esperan (`FOR UPDATE` / `UPDLOCK, ROWLOCK, HOLDLOCK`); el proceso **salta** (`SKIP LOCKED` /
  `READPAST`) y deja el crédito para la pasada siguiente.
- Bajo el cerrojo: `Sequence + 1` y `RegisteredAt = max(UtcNow, LastRegisteredAt + 1 tick)`, monótonos por crédito.
  `RowVersion` como segunda defensa. Validaciones previas fuera del cerrojo. El contrato usa este mismo cerrojo.

**Rationale**: una operación espera a la otra sin perderse ninguna (FR-079) y la causación masiva nunca deja
esperando a una caja; el orden por `Id` evita interbloqueos; el cupo (FR-058) no se excede porque una reserva o una
solicitud todavía no tienen `LoanControl`.

**Alternatives considered**
- `RowVersion` con reintentos (009): conflictos y «cambió» falsos; la 012 ya lo descartó.
- Bloqueos consultivos: dependen del motor de base.
- Bloquear la cabecera `LND_Loans`: mezcla cerrojo con cambios de estado.
- `ICerrojoPorClave` para el contrato (DE-02): dos mecanismos sobre el mismo crédito (CH19).

**Trazas**: T34, T22, T91 · FR-041, FR-058, FR-066, FR-079, FR-086 · M13, DE-02 (CH19).

**Difiere del informe**: M13 no tenía el paso por deudor (`LND_DebtorControls`).

#### R43 · Causación masiva: tandas, carga en bloque, auditoría resumida y rendimiento

**Decision**
- La causación corre en el `ProcesadorDeCartera` (R12), no en `ProgramadorDeTareas`. Por cada fecha pendiente, la
  corrida toma los `Id` de los créditos vivos con `LastAccruedDate < d` y los procesa en tandas de `BatchSize`
  (250).
- Cada tanda, por `IEjecutorEnCooperativa` y `TransaccionExplicita`: (1) cerrojo con salto; (2) carga en bloque
  (unas cinco consultas: condiciones, plan vigente, operaciones, controles; las series regulatorias una vez por
  corrida, por `ISeriesRegulatorias` sobre `LeerSerieAsync`); (3) motor; (4) `RegistroDeCartera` agrega causaciones,
  aplicaciones de anticipo del día, posiciones y la marca de agua, y emite sus partes (R5); (5) **un** `SaveChanges`.
- La causación del proceso no pasa por el `ChangeTracker`: `RegistroDeCartera` la escribe en bloque y se audita con
  un evento resumen por tanda y otro por corrida (conteos, totales, huella). `LoanDailyPosition`,
  `MonthlyCloseLoan` y `LND_ProcessRunErrors` llevan `[SinDiffDeAuditoria]`; `LoanTransaction` **no** (es la tabla del
  Principio XI y el atributo apagaría el diff de desembolsos, pagos y reversos).
- Si falta la usura o el índice de d, esas modalidades no se causan y se alerta (`NationalValues.Missing`,
  `Cartera.ValorNacionalFaltante`).
- Presupuesto: unas 200 tandas de ≤ 3 s ≈ 10 min frente a los 30 de SC-007; el motor ≤ 1 ms por crédito
  (`RendimientoDeCausacionTests`: 50.000 créditos < 60 s; `CausacionDiariaPerformanceTests` con `RUN_PERF_TESTS`).

**Rationale**: SC-007 (50.000 créditos en menos de 30 minutos sin afectar a las demás cooperativas) y FR-066 (una
cooperativa que falla no detiene a las otras); tandas cortas acotan la espera de un pago y el tamaño del
`ChangeTracker`; un diff por fila (~150.000 al día) saturaría la auditoría.

**Alternatives considered**
- Una transacción por cooperativa: bloqueos largos y `ChangeTracker` enorme.
- Paralelismo sin tope: satura la base compartida.
- Un mensaje resumido por día y corrida (M14): la parte cabe en la tanda (R5).
- Tamaño de tanda y hora como parámetros de la cooperativa (`Cartera.CausacionTamanoDeTanda`,
  `Cartera.CausacionHoraDeInicio`, M): son técnicos (CH36).

**Trazas**: T5, T14, T67, T101 · FR-003, FR-015, FR-062, FR-066, FR-128, SC-007, SC-014, SC-020, Principio IV ·
M14 (CH1, CH36). La auditoría resumida es desviación del Principio X registrada en `Complexity Tracking` (T101).

**Conciliación**: C46 — llevan además `[SinDiffDeAuditoria]` `LoanControl`, `DebtorControl`,
`LND_AccountingFactParts`, `LND_LoanArrearsHistory`, `LND_PayrollDeductionFileLines`, `LND_RegulatoryFileLines` y
las 17 tablas de preparación de la migración, cada una con su **evento resumen** por corrida, archivo o importación;
entra a T101 y a Complexity Tracking. C04 — las partes de la causación de una puesta al día nacen igual que las de
la tanda.

#### R44 · Precisión y redondeo

**Decision**
- `PrecisionDeCartera`: `Monto` (18,2), `TasaAnual` (9,6, fracción), `TasaEquivalente` (28,18), `ValorUvr` (18,4,
  K6b); `LosSaldosYTasasDeCarteraTienenSuPrecision` exige el alias.
- `RedondeoDeCartera` sella unidad (0,01; 1; 10; 100; 1.000), modo (`AwayFromZero`, `ToEven`, `Up`, `Down`) y momento
  (`PerConcept`, `PerInstallment`, `PerTotal`). El libro se registra en la unidad sellada por diferencia de
  acumulados; la cuota del plan se redondea según el momento y el residuo va a capital y queda visible. El motor no
  redondea en ningún otro punto.
- Una tasa pactada con más de 6 decimales de fracción se rechaza en la validación.

**Rationale**: FR-010 sella unidad, modo y momento; PostgreSQL redondea `numeric(18,2)` en silencio; las tasas
certificadas caben en 6 decimales de fracción, las derivadas no.

**Alternatives considered**
- 18,2 para tasas: trunca en silencio.
- Porcentajes (7,4): no caben las tasas diarias y descuadra con la 012.
- Guardar sólo montos y recalcular tasas: la explicación y la reproducibilidad quieren el valor sellado.

**Trazas**: T35 · FR-010, FR-020, SC-017, SC-023 · M15.

#### R45 · Casos dorados y pruebas que fijan el motor

**Decision**
- Casos dorados en `tests/IngenIA365ERP.Domain.Tests/Lending/Casos/*.json` (`CasoDoradoDeCartera`,
  `CasosDoradosDeCarteraTests`), ~28 en K1, con lo esperado por día, cuota y concepto al centavo, calculado con
  decimal de 40 dígitos (hoja Python versionada junto a los casos): cuota fija en las tres convenciones (con
  bisiesto 2028) y bloqueo por efectiva; abono constante, extraordinarias, pago único, gracia a capital y total;
  vencimiento en festivo; suspensión a 60 días y reanudación; usura que baja y corrección antes y después de R; tasa
  variable con piso y techo; abono que reduce plazo frente a cuota; anticipo de tres cuotas; fecha valor anterior;
  reverso con pago posterior y de un pago que canceló; dos días sin causar; liquidación futura; tolerancia
  residual; nota y anulación de Comercial; migrado con suspenso; 30/360 con día 31 y fin de febrero; semanal y
  quincenal; seguros sobre saldo.
- Propiedades: `SumaDeCapitalDelPlanTests`, `ReproducibilidadDeLiquidacionTests`,
  `ReversoComoSiNoHubieraExistidoTests`, `ElReversoEnlazaCadaFilaConSuOriginalTests`, `CausacionIdempotenteTests`;
  rendimiento `RendimientoDeCausacionTests`.
- Arquitectura: `LaCarteraNoTieneValoresLegalesFijos` (literales de Nómina, incluido 15, más 365; 7 y 10 nunca
  como literal: salen del valor del enum de periodicidad; sospechosos del Anexo con exención comentada por archivo),
  `ElMotorDeCarteraEsDecimal`, `NadieEscribeElLibroDeCarteraFueraDelRegistro`,
  `LosSaldosYTasasDeCarteraTienenSuPrecision`, `LaCarteraUsaLaFechaLocal`.
- Integración (colección «Cartera e2e», dos motores): `ConcurrenciaPagoYCausacionTests`,
  `CausacionDiariaPerformanceTests` (`RUN_PERF_TESTS=1`).

**Rationale**: es el método que dio confianza a Nómina y al costeo; deja los números a la vista de la contadora
(SC-001, SC-004, SC-017, SC-020, SC-023).

**Alternatives considered**
- Sólo pruebas de handler con dobles: no fijan el centavo.
- Casos en Excel: el `double` y los empates de redondeo dan diferencias.
- Admitir 7m y 10m como literales (M16): salen del enum de periodicidad (CH39).

**Trazas**: §2.25, T26, T35 · FR-014, SC-001, SC-002, SC-004, SC-017, SC-018, SC-020, SC-023 · M16, inf. R18
(CH39).

### 3.3 Riesgos

| Riesgo | Mitigación |
|---|---|
| Volumen: ~22 millones de filas de causación y ~18 millones de posiciones al año con 50.000 créditos; nada se borra (I-6) | Índices (`LoanId`, fecha valor), (`OperationId`); particionar por año pasados 20.000 créditos (D13); COOFLOPAL es mucho menor |
| La reproducibilidad depende de la bitemporalidad y de no editar parámetros fuera de su escritor | Regla única `VigenciaBitemporal` y lector con `informacionHasta` (R48); `LosParametrosSeLeenEnUnSoloSitio` |
| Un cambio de algoritmo cambia resultados | `RulesVersion` sellada; se mantienen las versiones mientras haya créditos que las usen |
| `SKIP LOCKED` y `READPAST` son nuevos en el código | `ConcurrenciaPagoYCausacionTests` en PostgreSQL y SQL Server |
| Dos nóminas concurrentes sobre los mismos créditos | Lote ordenado por `Id` en una llamada; reintento de `TransaccionExplicita` |
| El reinicio de ventana por un cambio de tope parte la composición | Diferencia mínima, fijada por un caso dorado |
| La sobretasa sellada es aproximación lineal en puntos | Conservadora; la contadora valida qué cargos computan (FR-021) |
| Una corrección de usura masiva genera muchas filas de ajuste | Corre en tandas como la causación |
| Real/365 en bisiesto da una efectiva levemente mayor | Caso dorado de 2028; el control de usura usa base 365 (D3) |
| Calcular 28 casos a mano | Hoja Python `decimal` versionada; firma de la contadora |
| `ToEven`, `Up`, `Down` de .NET frente a los casos | Casos dorados específicos por modo |
| Planes migrados que el motor no generó | Se conserva el capital por cuota; el interés lo causa el motor desde el corte y se declara en el acta (D5) |

---

## 4. Frente de riesgo, valores nacionales y cierres

### 4.1 Hechos del código

**Parámetros, vigencias y normativa como dato**

- **Parámetros de la plataforma.** `COR_ParameterVersions` (`Module`, `Key`, `ScopeKind`, `ScopeId`, `Value` texto
  ≤ 2000, `ValidFrom`, `ValidTo`, `Reason`, `LegalSource`; UK filtrado a vivas por módulo, clave, ámbito y
  `ValidFrom`). Un lector (`LectorDeParametros`, memoriza por petición y cae al «defecto seguro») y un escritor
  (`AddParameterVersionCommandHandler`). Las claves son un catálogo cerrado: `CatalogoDeParametros` une Inventario,
  Tributario y EINV, con puerta por entrega (`EntregaVigente = I6`, por `EntregaDelComercio`). —
  `Domain/Entities/Parameters/ParameterVersion.cs`; `Application/Common/Parameters/LectorDeParametros.cs`;
  `Domain/Common/Parametros/CatalogoDeParametros.cs`
- **Nada es bitemporal.** `AddParameterVersion` y `AddLegalParameterVersion` (Nómina) rechazan una vigencia que no
  sea posterior y reescriben el `ValidTo` de la anterior: no se corrige un valor que ya empezó ni se reconstruye
  «lo que se sabía en R»; sólo queda `CreatedAt`. — `AddParameterVersionCommandHandler.cs`;
  `Application/Payroll/LegalParameters/LegalParameterCommands.cs`
- **Ámbitos.** `ParameterScopeKind` 0–5 sin línea de crédito ni tipo de hecho; `ScopeId` es un `int` sin FK que
  valida el comando. — `Domain/Enums/Parameters/ParameterScopeKind.cs`
- **Parámetros legales de Nómina.** La tabla real es `PAY_LegalParameters` (con `PAY_LegalParameterRanges`), sin
  estado «pendiente de validar» ni fecha de registro propia. `PayrollLegalParametersSeeder` (Order 71) siembra
  SMMLV 1.750.905 y UVT 52.374 de 2026. La leen directamente unos 13 archivos (`CalculationInputLoader`,
  `SettlementInputLoader`, `PilaInputLoader`, `WithholdingRateInputLoader`, `VacationBalanceCalculator`,
  `TerminationQueries`, `DepositScheduleBuilder`, `GetMissingLegalParametersQuery`, `LegalParameterQueries`,
  `ConceptDefinitionCommands`, `PilaQueries`, `LectorDeUvt`, `PayrollSeedApplier`). —
  `Domain/Entities/Payroll/PayrollLegalParameter.cs`; `Persistence/Seeding/Parametric/PayrollLegalParametersSeeder.cs`
- **UVT con un lector.** `LectorDeUvt` (`IValorUvt`) es el único lector (falla con `Taxation.Uvt.Missing`); su
  comentario prevé que, al promover la UVT, sólo cambie él; lo vigila `LaUvtSeLeeEnUnSoloSitio`. —
  `Application/Common/Taxation/LectorDeUvt.cs`
- **Festivos.** `PAY_Holidays` (UK por `Date`; orígenes `Ley51Fixed`, `Ley51MovedToMonday`, `Ley51Easter`,
  `Decreed`, `Manual`); `HolidaysSeeder` (Order 74) siembra 2026–2028 con `FestivosLey51`, que vive en
  `Persistence/Seeding`; los leen las vacaciones y su pantalla. Hoy son de cada cooperativa y de Nómina. —
  `Domain/Entities/Payroll/Holiday.cs`; `Domain/Enums/Payroll/HolidayOrigin.cs`;
  `Persistence/Seeding/Parametric/{HolidaysSeeder, FestivosLey51}.cs`
- **Normativa como JSON embebido.** `PilaLayoutCatalog` carga JSON embebidos, los valida al cargar, elige el
  vigente por fecha y, con campos `verified = false`, sigue vigente pero alerta `Pila.LayoutSinCotejar` (D-43);
  `CatalogoDian` sigue el mismo patrón. — `Application/Payroll/Pila/PilaLayoutCatalog.cs`;
  `Application/ElectronicInvoicing/Catalogs/CatalogoDian.cs`
- **«Pendiente de validar».** `TaxRate.ReviewPending` lo deja la semilla y lo limpia una revisión con motivo, sin
  bloquear; `AccountCatalog.ValidatedAt` se vacía cuando `SincronizacionDeCatalogo` cambia la versión (única
  excepción a «nunca actualizar» las semillas): corrige en su sitio, inservible para valores bitemporales. —
  `Domain/Entities/Core/Taxes/TaxRate.cs`; `Persistence/Seeding/Parametric/SincronizacionDeCatalogo.cs`
- **Políticas de empresa.** `PAY_CompanyPolicies` con `PayrollPolicyReader` como único lector: antecedente de
  decisiones con vigencia, pero de Nómina. — `Application/Payroll/Services/PayrollPolicyReader.cs`

**Base administrativa, semillas y alta**

- **No hay valores nacionales ni réplica.** Ninguna tabla tiene usura, IBR, IPC, tope de vivienda ni UVR, y nada
  copia datos de la base administrativa a las cooperativas. (búsqueda en `src/**/*.cs`)
- **Base administrativa.** `AdminDbContext` (`IngenIA365ERP_Admin`) guarda identidad, cooperativas, oficinas
  (`ADM_Branches`), suscripciones, MFA y datos de plataforma (`ADM_PlatformMfaPolicy`, `ADM_PromoContenidos`,
  `ADM_DataProtectionKeys`); migraciones Admin propias en los dos motores; Application ya usa `IAdminDbContext`;
  rutas `/api/saas/*` protegidas por `RequireMasterAdminAttribute.Check`. — `Persistence/DbContext/AdminDbContext.cs`;
  `API/Endpoints/SaasAdminModule.cs`
- **Semillas.** `IDataSeeder` declara `Order`, `Category` y `Scope` (Admin/Tenant); `SeedOrchestrator` abre una
  transacción por semilla × base y crea por cooperativa un contexto con los interceptores (la auditoría corre al
  sembrar). El único seeder Admin es `MasterAdminSeeder` (Order 5); los de cooperativa llegan a 87. —
  `Persistence/Seeding/{IDataSeeder, SeedOrchestrator}.cs`
- **Alta de cooperativa.** `TenantDatabaseProvisioner` crea la base, migra y siembra dentro de
  `POST /api/saas/tenants/with-admin`; al arrancar, `DatabaseInitializerHostedService` migra y siembra todas. La
  constitución exige la cooperativa «creada, migrada y sembrada en el mismo acto del alta». —
  `Persistence/MultiTenancy/TenantDatabaseProvisioner.cs`; `Persistence/Initialization/DatabaseInitializerHostedService.cs`
- **Constitución.** Prefijos admitidos `COR_`, `ACC_`, `LND_`, `PAY_`, `INV_`, `CDT_`, `DEB_`, `TRS_`, `SEC_`, `AUD_`,
  `WEB_`, `ADM_`; prohíbe replicar tablas `ADM_*` dentro de la base de una cooperativa (la copia va en `COR_`). —
  `.specify/memory/constitution.md`
- **Auditoría encadenada.** `AuditoriaEncadenada.Modulos` = Inventory, ElectronicInvoicing, Integration,
  Approvals, Alerts, Parameters, Taxes, PaymentMeans, Navigation (sin Lending). `ModuloDeAuditoria.Inferir` saca el
  módulo del espacio de nombres: una regla `.NationalValues` debe ir antes de `.Core`. —
  `Application/Common/Audit/{AuditoriaEncadenada, ModuloDeAuditoria}.cs`

**Cierres, riesgo heredado y contabilidad**

- **Cierre con versión (antecedente).** `InventoryPeriod` (`CloseVersion`, reapertura con quién, cuándo y motivo;
  sólo el último) con `PeriodClosingBalance` (`Version`, `Superseded`). — `Domain/Entities/Inventory/Periods/*.cs`
- **Riesgo heredado.** `ClassifyPortfolioCommand` fija en el código una tabla única (A ≤ 30, B ≤ 60, C ≤ 90,
  D ≤ 120, E) para toda modalidad, sobrescribe `LND_PortfolioClassifications`, no calcula deterioro normativo y su
  comprobante quedó con el marcador E3; `LND_ProvisionParameters` y `LND_RiskAssessments` también son heredadas.
  Todo lo retira FR-144. — `Application/Lending/Classifications/Commands/ClassifyPortfolio/ClassifyPortfolioCommand.cs`
- **CUIF sin E1/E2.** Consumo con y sin libranza por categoría A–E (144105–144125) y deterioro de consumo por
  categoría (144510–144550); no hay cuentas para E1/E2: la reclasificación contable llega sólo a A–E. —
  `puc-solidario.json`
- **Grupo NIIF.** `AccountingSetup.NiifGroup` (1/2/3) es fila única sin vigencia; el perfil regulatorio lo pide con
  vigencia: habría dos fuentes. — `Domain/Entities/Accounting/AccountingSetup.cs`
- **Pruebas molde.** `LaNominaNoTieneValoresLegalesFijos` (literales admitidos más lista de sospechosos),
  `ElComercioNoTieneValoresLegalesFijos`, `LosParametrosSeLeenEnUnSoloSitio`, `LaUvtSeLeeEnUnSoloSitio`,
  `LosHechosInmutablesNoSeModifican`, `NingunTrabajoDeFondoOperaSinCooperativa`. — `tests/.../Principles/`

**Norma consultada (condiciona el esquema de los documentos)**

- **Anexo 1** (Supersolidaria, versión del 2025-01-31): rangos de mora por modalidad (vivienda, consumo,
  microcrédito, comercial) con límites distintos; pago único (D con más de 7 días, E con más de 90, salvo
  rotativos); arrastre mensual desde B dentro de la misma modalidad, sin codeudores, con excepción de aportes;
  porcentajes por categoría y modalidad; subcategorías **con dos criterios** (días de mora en consumo y comercial
  persona jurídica; tiempo en E en vivienda); garantía admisible por meses de mora (no hipotecaria e hipotecaria con
  tramos distintos); aportes deducibles con condiciones de pérdidas y solvencia.
- **Anexo 2** (versión del 2024-07-26, anterior a la CE 100): puntaje logístico con Z lineal por segmento (consumo
  con y sin libranza, comercial persona natural) sobre variables binarias de mora en ventanas de 3 a 36 meses y
  otras de asociado, organización, cuota y plazo; la tabla de cortes del puntaje es imagen; PI por segmento ×
  calificación × tipo de organización; VEA; PDI por garantía y días desde el incumplimiento; homologación
  obligatoria a la calificación contable.
- **Circular Externa 100** (2026-05-12): el modelo de referencia obliga sólo a las cooperativas de ahorro y crédito,
  multiactivas e integrales con sección de ahorro, fondos de empleados de categoría plena y mutuales con ahorro;
  las demás, Anexo 1. Crea la PI para la aplicación voluntaria; adoptar o desistir exige acta con estudio y aviso a
  la Superintendencia **15 días calendario antes del reporte financiero del mes de aplicación** (FR-105 mide contra
  la fecha desde: pregunta E2).
- Fuentes: supersolidaria.gov.co (`20250131_anexo_1_cap_2_tit_4.pdf`, `20240726_anexo_2.pdf`) y confecoop.coop
  (`20260513_circular_externa_100.pdf`).

### 4.2 Decisiones

#### R46 · Registro central y copia por cooperativa

**Decision**
- Los valores nacionales se registran **una vez** en la base administrativa — `ADM_NationalValues`
  (`PlatformNationalValue`, H) y `ADM_RegulatoryDocuments` (`PlatformRegulatoryDocument`, H) — por el maestro con
  segundo factor, en `/api/saas/national-values` (+ `{id}/corrections`, `import`, `export`) y
  `/api/saas/regulatory-documents` (+ `{id}/content`). Migración Admin `ValoresNacionales`.
- Se copian **sin modificar** a `COR_NationalValues` (`NationalValue`, H: `SourcePublicId` y `SourceSequence` únicos,
  `KnownAt`) y `COR_RegulatoryDocuments` (`RegulatoryDocument`, H) de cada cooperativa, en `PlataformaParaCartera`.
- Filas centrales con `Sequence` creciente, `RegisteredAt`, `RegisteredBy`, `Source` y `SupersedesPublicId`.
- **Ningún cálculo lee `ADM_`.** Comandos en `Application/Saas/NationalValues`: `RegisterNationalValueCommand`,
  `CorrectNationalValueCommand`, `RegisterRegulatoryDocumentCommand`, `ImportNationalValuesCommand` (plantilla de
  índices). Pantalla `/admin/valores-nacionales` (el maestro registra; la cooperativa consulta y revisa).

**Rationale**: I-3 y FR-015 (un registro, sin lecturas entre bases al calcular); la base administrativa ya guarda
datos de plataforma y tiene migraciones propias; la copia en `COR_` respeta la prohibición de replicar `ADM_`.

**Alternatives considered**
- Sólo JSON embebido: la usura cambia cada mes y el IBR cada día; habría que desplegar por cada valor.
- Leer `ADM_` al calcular: viola I-3 y el Principio IV.
- Que cada cooperativa registre lo suyo: rompe el «una vez» y los valores divergen.
- Reusar `COR_ParameterVersions`: no es bitemporal, no admite documentos y es de cada cooperativa.

**Trazas**: T37, §2.5 · FR-014, FR-015, FR-149, I-3, D28, Principio IV · inf. R1 (pregunta B5).

**Conciliación**: C44 — la `Sequence` central sale de un contador con cerrojo, `ADM_NationalValueCounters`. C82 —
`ImportNationalValuesCommand` reutiliza definición, lector y reglas del ejecutor de plantillas con su propia
transacción sobre `IAdminDbContext`. C69 — la central calcula, sin leer cooperativas, qué códigos esperados no
tienen vigencia próxima y lo muestra en `/admin/valores-nacionales`; la cooperativa recibe
`Cartera.ValorNacionalFaltante`. C83 — en `/api/core/national-values`, `date` = D y `recordedAsOf` = R.

#### R47 · Valores escalares y documentos normativos tipados

**Decision**
- **Escalares** (`NationalValue`): `Code` de `CodigosDeValorNacional` (`USURA` con clave = modalidad certificada;
  `IBR`, `IPC` con clave = plazo o serie; `TOPE_TASA_VIVIENDA` y `UVR` en K6b; `SMMLV`; `UVT`; `FESTIVO` con clave
  = fecha; `LIMITE` con clave = código del límite), `Key`, `ValidFrom`, `ValidTo?` (sólo cuando la norma lo trae),
  `ReferenceDate` (el período que mide un índice), `Value` (20,10), `Unit` (`EA`, `NominalMV`, `Pesos`,
  `Fraccion`, `Dias`, `Meses`, `Horas`) y `Source`.
- **Documentos** (`RegulatoryDocument`): `Kind` de `RegulatoryDocumentKind` (`CatalogoDeModalidades`,
  `TiposDeOrganizacion`, `SarcAnexo1`, `SarcAnexo2`, `ReglaDePerdidaEsperada`), `Code`, `ValidFrom`, `Content`
  (JSON), `ContentSha256`, `SchemaVersion` y `Verified` (cotejo de la plataforma, como la PILA).
- Ubicación de los tipados: los que la plataforma valida o consume (`CatalogoDeModalidades`, `TiposDeOrganizacion`,
  `CatalogoDeLimitesLegales`) en `Domain/Common/NationalValues`; los que sólo usa Cartera (`Anexo1`, `Anexo2`,
  `ReglaDePerdidaEsperada`) en `Domain/Lending/Risk/Normativa`, validados en la central por el punto de extensión
  `IValidadorDeDocumentoNormativo`, que Cartera implementa.
- Semilla desde JSON embebidos por `PlatformNationalValuesSeeder` (Admin, Order 6); después se registran versiones
  nuevas sin desplegar.

**Rationale**: los anexos cambian por circular y en bloque; el molde probado es `PilaLayoutCatalog` y
`CatalogoDian` (JSON versionado, validación tipada, `verified = false` hasta cotejar); los escalares frecuentes
necesitan filas para consultarse por fecha.

**Alternatives considered**
- Normalizar cada anexo en tablas: una versión repartida en muchas filas, por dos bases y dos motores.
- Expresiones libres en el documento: criterios como tipos cerrados con parámetros («sin fórmulas libres»).
- Documentos de Cartera en `Domain/Common` o la central dependiendo de `Domain/Lending`: la plataforma no depende de
  un módulo (CH34).

**Trazas**: T37, T45, §2.8, §2.21 · FR-007, FR-014, FR-015, FR-019, FR-099, FR-103, FR-106 · inf. R2.

**Difiere del informe**: el inf. R8 ponía `CatalogoDeLimitesLegales` en `Domain/Lending/Risk/Normativa`; va en
`Domain/Common/NationalValues` (T45).

#### R48 · Bitemporalidad con una sola regla de lectura

**Decision**
- Toda fila con vigencia de los valores nacionales es hecho de sólo inserción; corregir es una fila nueva con
  `SupersedesPublicId`; ninguna fila se cierra reescribiendo su `ValidTo`.
- `VigenciaBitemporal.Vigente(versiones, D, R)` (pura, `Domain/Common`): las versiones conocidas en R que cubren D
  y no fueron reemplazadas en R; gana el `ValidFrom` más reciente y, si empatan, la `Sequence` mayor. En la
  cooperativa, R se compara con `KnownAt` (una liquidación anterior a la réplica no pudo usar el valor).
- `LectorDeParametros` gana `informacionHasta` opcional con una regla **propia**, porque `COR_ParameterVersions` no
  tiene `Sequence`, `SupersedesPublicId` ni `KnownAt`: R se compara con `CreatedAt`; el `ValidTo` guardado se ignora
  y se deriva de la versión siguiente conocida en R; una baja posterior a R no cuenta. Sin `informacionHasta`,
  nada cambia para Inventario.
- Los parámetros de la cooperativa siguen sin corrección retroactiva (`SeCruza`): sólo los valores nacionales se
  corrigen.

**Rationale**: FR-011 y D25 piden reconstruir «lo que se sabía en R», y el caso de la usura corregida exige
corregir una vigencia que ya empezó; ninguno de los mecanismos actuales lo permite.

**Alternatives considered**
- Historial de cambios de `ValidTo` en una tabla de auditoría: la regla quedaría repartida.
- Tablas temporales del motor (SQL Server): PostgreSQL no las tiene igual; rompe la paridad.

**Trazas**: T38 · FR-007, FR-011, FR-012, D25 · inf. R3, M3.

#### R49 · Réplica idempotente y sus efectos

**Decision**
- Núcleo único `ReplicacionDeValoresNacionales.AplicarAsync`: copia en orden lo que tenga `Sequence` mayor que la
  última local, con `SourcePublicId` único como guarda; escribe `COR_NationalValueReplications`; guarda una vez.
- Tres entradas: la tarea `valores-nacionales.replicar` (cada pasada, `ReplicateNationalValuesCommand` sin ruta por
  `IEjecutorEnCooperativa`); el seeder `NationalValuesReplicaSeeder` (Tenant, **Order 88**, para que el alta nazca
  con la copia); el primer paso del ciclo diario y del cierre (que guardan la marca de agua usada).
- Sólo el replicador lee la central (`IRegistroCentralDeValoresNacionales`, sobre `IAdminDbContext`). Si la central
  no responde, el cierre oficial se niega con motivo.
- Los efectos en otros módulos van por `IEfectoDeReplicacion`, en la misma transacción: `EfectoDeReplicacionEnNomina`
  (R51) y `EfectoDeReplicacionObligacionSobrevenida` (R58).
- La réplica se audita con el módulo `NationalValues` (encadenado, diez años; regla antes de «.Core»).

**Rationale**: ninguna transacción cruza bases y reintentar es seguro; reusa programador, ejecutor y orquestador de
semillas; el alta queda completa en el mismo acto.

**Alternatives considered**
- Empujar a todas las bases al registrar: recorre todas las cooperativas desde una petición y deja el registro a
  medias si una base cae.
- Disparadores o vínculos entre bases: violan el Principio IV.
- Réplica a pedido de la cooperativa: falla en silencio.

**Trazas**: T39, T67, §2.21 · FR-015, FR-016, FR-066, FR-107, SC-020, I-3, Principios IV, IX y X · inf. R4 (CH36;
pregunta B7).

**Conciliación**: C05 — la corrección que afecta días ya causados la detecta el núcleo de la réplica, sea cual sea
la entrada: un tercer efecto, `EfectoDeReplicacionCorreccionDeValorNacional` (Cartera), inserta en la misma
transacción una corrida `NationalValueCorrection` `Requested` cuando llega una fila con `SupersedesPublicId` que
cubre días ya causados, sólo si Cartera está activada. C68 — `PAY_Holidays` suma `NationalValuePublicId` para la
proyección de festivos (R51).

#### R50 · Lectores únicos de valores nacionales

**Decision**
- Dentro de la cooperativa sólo leen `COR_NationalValues` y `COR_RegulatoryDocuments` tres puertos de
  `Application/Common/{NationalValues, Calendar}`: `IValoresNacionales` (`LectorDeValoresNacionales`:
  `LeerAsync(codigo, clave, fecha, informacionHasta?)`, `LeerUltimoPublicadoAsync` con alerta
  `Cartera.IndiceDesactualizado` si es viejo, `LeerSerieAsync` para la carga en bloque); `ICalendarioHabil`
  (`CalendarioDeDiasHabiles`: `EsHabil`, `SiguienteHabil`, `Contar`, sobre `FESTIVO`); `INormativaVigente`
  (`LectorDeDocumentoNormativo`: documento tipado con su `PublicId` y SHA-256 para sellarlo).
- Si falta el valor de una fecha, lo que lo necesita se bloquea con `NationalValues.Missing` y se alerta.
- `ISeriesRegulatorias` de Cartera se construye sobre ellos.
- Los lectores de Nómina y de la 012 que leen su propia proyección (`PAY_LegalParameters`, `PAY_Holidays`,
  `LectorDeUvt`) no cambian: leen lo que sólo escribe la réplica (R51).

**Rationale**: molde `LectorDeUvt` y `LectorDeParametros` (un sitio, memorizado, falla visible); desde un punto
único se vigila con pruebas de arquitectura.

**Alternatives considered**
- Que cada motor consulte las tablas: cálculos dispersos y no se prueba «nadie lee `ADM_`».
- Error `Lending.NationalValue.Missing` (M): el error es del lector de plataforma (CH35).

**Trazas**: T40 · FR-015, FR-016, FR-067, FR-070, SC-018 · inf. R5, M1, M14 (CH34, CH35, CH48).

#### R51 · Nómina: proyección del SMMLV, la UVT y los festivos

**Decision**: Nómina implementa `EfectoDeReplicacionEnNomina`: cuando la réplica trae `SMMLV`, `UVT` o `FESTIVO`,
escribe su proyección en la misma transacción (fila de `PAY_LegalParameters` con fuente «Valor nacional
{SourcePublicId}»; fila de `PAY_Holidays` con `HolidayOrigin.National = 6`), con cuatro reglas:
1. **Adopta lo que ya existe**: con la misma clave y el mismo valor marca la fila como proyección en vez de
   insertar (los UK `UK_PAY_LegalParameters_Code_ValidFrom` y `UK_PAY_Holidays_Date` harían fallar la réplica y el
   alta); si el valor difiere, conserva la de Nómina y alerta `Nomina.ValorNacionalDistinto`.
2. **Las semillas dejan de sembrarlo**: salen el SMMLV y la UVT de `PayrollLegalParametersSeeder` y el generador
   Ley 51 de `HolidaysSeeder`; en el alta los escribe `NationalValuesReplicaSeeder` (Order 88, después de 71 y 74);
   `FestivosLey51` pasa al seeder de la central.
3. **Cierra la vigencia anterior** como `AddLegalParameterVersionCommand`.
4. **Sólo la réplica escribe**: `AddLegalParameterVersionCommand` rechaza `SMMLV` y `UVT`
   (`Payroll.LegalParameter.NationalValue`) y los comandos de festivos no editan los `National`.

Los festivos `Manual` siguen locales de Nómina y Cartera no los usa. `LectorDeUvt` sigue siendo el único lector de
la UVT y lee la proyección. Lo vigilan `SoloLaReplicaEscribeLosValoresNacionalesDeNomina` y
`ReplicaSobreCooperativaSembradaTests`.

**Rationale**: una sola fuente (la central) sin tocar los 13 lectores de `PAY_LegalParameters` ni los motores en
producción; cada tabla la escribe su dueño (molde `IEfectoDeAltaDeParametro` de la 012).

**Alternatives considered**
- Reescribir los cargadores de Nómina para leer `IValoresNacionales` (`ValoresNacionalesParaNomina`, DE-18): más
  literal, pero regresión amplia de Nómina y PILA en producción; queda como mejora (CH14).
- Que la plataforma escriba en `PAY_`: viola la propiedad de las tablas.
- Mezclar los `Manual` en la réplica (DE-18): un día propio no es festivo del Código de Comercio (CH15).

**Trazas**: T93, T97 · FR-015, FR-147, FR-149, C8 · inf. R6, DE-18 (CH14, CH15; preguntas A7, B6, E5).

**Difiere del informe**: el inf. R6 sólo proyectaba SMMLV y festivos, no cubría la adopción de filas ya sembradas y
nombraba la prueba `SoloLaReplicaEscribeElSalarioMinimo`. Conservar una proyección es la enmienda A7 de FR-147 y C8.

#### R52 · «Pendiente de validar» como hecho aparte

**Decision**: la conformidad del contador u oficial es `COR_NationalValueReviews` (`NationalValueReview`, H, sobre la
copia, sin modificarla); sin revisión el valor está «pendiente de validar». **Calcular no se bloquea**: el cierre
guarda la marca `UnvalidatedParameters` con la lista, el reporte de cambios y el SICSES la muestran como
advertencia y se alerta `Cartera.NormativaPendienteDeValidar`. `Verified` del documento es el cotejo de la
plataforma. La activación en producción sí exige un Anexo 1 vigente y la usura de las modalidades de las líneas
activas. Rutas `/api/core/national-values` y `/api/core/regulatory-documents` (+ `{id}/review`),
`Core.NationalValues.View/Review`.

**Rationale**: precedentes D-43 (PILA) y `TaxRate.ReviewPending`; no frenar el primer cierre obligatorio por un
trámite; queda constancia por cooperativa de quién validó qué.

**Alternatives considered**
- Bloquear cálculo o activación hasta validar: puede impedir el cierre obligatorio (E1).
- Columna `ReviewPending` en la copia: rompe la inmutabilidad de la réplica.

**Trazas**: T41 · FR-014, FR-136, FR-142, Principio IX · inf. R7 (pregunta E1).

**Conciliación**: C76 — la marca es la columna `UnvalidatedParameters` (T41 decía `ParametrosSinValidar`). C69 —
el faltante de valores nacionales lo alerta la central en `/admin/valores-nacionales` y la cooperativa recibe
`Cartera.ValorNacionalFaltante`.

#### R53 · Pisos y techos legales

**Decision**: cada límite es un `NationalValue` `LIMITE` con su clave (`AVISO_PREVIO_DIAS`,
`COBRANZA_HORA_FIN_LV`, `LIBRANZA_CAPACIDAD_DESCUENTO`, `PREPAGO_TOPE_SMMLV`, `CONCENTRACION_INDIVIDUAL`,
`DETERIORO_GENERAL_MINIMO`, la fracción calificada de vinculados y los plazos del titular). `CatalogoDeLimitesLegales`
fija unidad, dirección de lo estricto y clave de Cartera que acota. El `IReglasDeParametros` de Cartera
(`ReglasDeParametrosDeCartera`) rechaza una vigencia menos estricta (`Lending.Parameter.BelowLegalFloor`, con el límite y su fuente); al leer rige lo más
estricto entre cooperativa y límite vigente en D; un límite nuevo más estricto alerta
`Cartera.LimiteLegalMasEstricto`.

**Rationale**: FR-014 («sólo más estricto») y SC-018 («cero parámetros bajo el piso») reusando el escritor único de
parámetros y su extensión existente, sin valores legales en el código.

**Alternatives considered**
- Límites dentro de `DefinicionDeParametro`: valores legales en el código.
- Validar sólo al registrar: un límite nuevo dejaría valores ilegales en uso.

**Trazas**: T42 · FR-014, FR-015, FR-040, FR-041, FR-122, FR-123, SC-018 · inf. R8, S5, S10 (pregunta F11).

#### R54 · Normativa SARC tipada y política de la cooperativa

**Decision**
- `Anexo1` (`SarcAnexo1`, K1): segmentos SARC (`VIVIENDA`, `CONSUMO`, `MICROCREDITO`, `COMERCIAL_PN`,
  `COMERCIAL_PJ`); rangos por segmento y categoría; reglas especiales tipadas (pago único, crédito nuevo);
  porcentajes por segmento × categoría × subcategoría, con condición de subcategoría tipada (días de mora o meses en
  E) y base (descubierto o saldo total); garantías por naturaleza con tramos en meses; aportes y ahorro permanente;
  arrastre; suspensión por segmento; reestructurados; deterioro general por perfil.
- `Anexo2` (`SarcAnexo2`, K6a): segmentos de pérdida esperada (`CONSUMO_LIBRANZA`, `CONSUMO_SIN_LIBRANZA`,
  `COMERCIAL_PN`); variables con tipo cerrado parametrizado; coeficientes, cortes, PI por tipo de organización con
  clave `VOLUNTARIA`, PDI, VEA y homologación. `ReglaDePerdidaEsperada` entra en K1 para detectar la obligación.
- Política del consejo: `LND_RiskPolicyVersions` (`RiskPolicyVersion`, H) con sobreescrituras del mismo esquema
  parcial, **sólo más estrictas celda por celda**, validadas con permiso propio (`AddRiskPolicyVersionCommand`,
  `ValidateRiskPolicyVersionCommand`, `Lending.RiskPolicy.Manage/Validate`) y ligadas a un punto de acta
  (`SessionItem`). `PoliticaSarcEfectiva.Combinar` (pura) da el máximo por celda; si la norma se endurece después,
  gana la norma.

**Rationale**: una circular nueva es dato, no código (salvo que traiga un criterio nuevo); las dos formas de
subcategoría verificadas en la norma caben en criterios tipados; separar norma y política deja ver de dónde sale
cada porcentaje.

**Alternatives considered**
- Un porcentaje por rango de días sin categorías: la norma fija el porcentaje por categoría.
- Columnas fijas para E1/E2: no cubren el criterio por tiempo en E.
- Política en `COR_ParameterVersions`: no admite tablas y 2000 caracteres no alcanzan.
- Copiar el Anexo 1 a la cooperativa para editarlo: lo no cambiado dejaría de seguir a la norma.
- Referencia de acta como texto libre (`MinutesReference`, inf. R11): no prueba nada (CH21).

**Trazas**: T45, T64 · FR-007, FR-014, FR-099, FR-103, FR-104, FR-106 · inf. R9, R10, R11 (CH21; pregunta E8).

**Difiere del informe**: el inf. R11 guardaba la referencia del acta como texto y un adjunto; es un punto de sesión.

**Conciliación**: C52 — `LendingDecisionSubjects` suma `RiskPolicyVersion` para ligar la política SARC a un punto de
acta.

#### R55 · Motores de riesgo puros

**Decision**
- `MotorDeCalificacion` en dos fases: por crédito (mora con `AlturaDeMora` y calendario hábil, modelo, pago único,
  crédito nuevo, reestructurado, evaluación, análisis, insolvencia) y luego arrastre por deudor y modalidad SARC.
  Devuelve la peor candidata con su causa (`RatingCause`), las candidatas, la subcategoría y la calificación
  contable A–E.
- `MotorDeDeterioroIndividual` (Anexo 1: base = capital − garantía admisible por meses de mora − aportes;
  porcentaje de la política efectiva; conceptos suspendidos al 100 %), `MotorDeDeterioroGeneral` (mínimo por
  perfil + adicional, con dimensiones para la matriz), `MotorDePerdidaEsperada` y `FuncionLogistica` en decimal
  (K6a), `SelectorDeModeloDeDeterioro`, `HomologacionDeCalificacion`.
- Mientras rija D19: sin excepción de aportes en el arrastre ni descuento de aportes en el deterioro
  (`IConsultaDeAportes` → `ConsultaDeAportesNoDisponible`).
- La calificación diaria informativa usa el mismo motor al leer y no se guarda (con arrastre, apagable con
  `Riesgo.CalificacionDiariaConArrastre`). Casos dorados en `Domain.Tests/Lending/Risk/Casos/*.json`.

**Rationale**: molde de Nómina: reproducible, al centavo (SC-021, SC-023) y sin valores legales; `Math.Exp` en
`double` puede diferir entre plataformas y cambiar una calificación en el borde de un corte.

**Alternatives considered**
- Calcular en SQL: no portable entre motores ni explicable por crédito.
- Guardar la calificación diaria: viola I-6 salvo como instantánea y no hace falta.
- Tabla de calificación precalculada por combinación de variables: acopla la tabla a los coeficientes.

**Trazas**: T46 · FR-093, FR-098–FR-106, SC-021, SC-023, D19, I-6 · inf. R10, R12 (preguntas E7, E9).

#### R56 · Cierre mensual inmutable con R sellado

**Decision**
- `OrderMonthlyCloseCommand` encola (202) una corrida `MonthlyClose` del `ProcesadorDeCartera`
  (`ExecuteMonthlyCloseCommand`, sin ruta). Exige el **cierre anterior vigente** (el mensual del mes anterior o, en
  el primero tras activar, el cierre base, aunque sea del mismo mes), período contable abierto, causación al día
  hasta fin de mes y réplica sincronizada.
- Fija `InformationCutoffAt` (R), la marca de agua de valores nacionales, los documentos usados con su SHA-256, la
  política y el modelo por segmento.
- Aplica la regla de R12: (1) las tandas calculan y guardan `LND_MonthlyCloseLoans` (H) enlazadas a la corrida, sin
  mensajes; (2) una **transacción final** inserta el encabezado `LND_MonthlyCloses` (H) con R y las marcas
  `ObligatoryModelUnavailable`, `UnvalidatedParameters`, `IncompleteHistory`, `LND_MonthlyCloseGeneralImpairments`,
  las partes (`LND_AccountingFactParts`, una por tanda × sucursal × tipo) con sus mensajes (R25) y el informativo
  `CierreDeCarteraRealizado`. Líneas sin encabezado no cuentan; el reintento las retoma.
- Reporte de cambios y cuadre se derivan al leer.
- Reabrir (`ReopenMonthlyCloseCommand`, `Lending.MonthlyClose.Reopen`): sólo el último, con motivo y atribución y
  antes del cierre contable (D12); en una transacción inserta `LND_MonthlyCloseReopenings` (H) y emite
  `ReversoDeCierre` por partes (enlazado a las del cierre) y `CierreDeCarteraReabierto`. Encabezado y líneas nunca
  cambian: el estado `Reopened` (`MonthlyCloseStatus`, calculado) y la vigencia de las líneas **se derivan** de la
  reapertura; volver a cerrar crea `Version + 1`.
- Cierre manual por defecto (`Riesgo.CierreProgramado` apagado; alerta `Cartera.CierreMensualPendiente` pasado
  `Riesgo.DiaLimiteCierre`, 10). Pantallas `/cartera/cierres`, `/cartera/cierres/{id:guid}`.

**Rationale**: FR-107 y FR-011 (el cierre se reproduce con su R y su marca de agua); I-5 incluye los cierres en el
Principio XI; 50.000 créditos no caben en una petición HTTP.

**Alternatives considered**
- Cierre dentro de la petición: no cabe en el tiempo de espera.
- Sólo totales: no hay diferencia por crédito ni reporte de cambios.
- `Status` en el encabezado y `SupersededAt [EscrituraUnica]` en las líneas (inf. R13): marca mutable sobre un
  hecho; se deriva de la reapertura.
- Arrendamiento propio `lending.monthly-close` y tarea `cartera.cierre-mensual` (inf. R13): compiten con la
  causación (CH1, CH38).

**Trazas**: T47, T5, T36 · FR-012, FR-107, FR-125, FR-128, FR-130, FR-146, D12, SC-006, I-5 · inf. R13, DC-13,
PM-13 (CH1, CH7, CH38; preguntas B7, E6).

**Difiere del informe**: el inf. R13 emitía `CierreDeCarteraReversado` (es `ReversoDeCierre`, CH7), escribía
`Status`/`SupersededAt` y usaba arrendamiento propio.

**Conciliación**: C58 — reabrir también es corrida: `ReopenMonthlyCloseCommand` responde 202 y encola una corrida
`CloseReopen`; la transacción única de T47 (reapertura, `ReversoDeCierre` por partes y `CierreDeCarteraReabierto`)
ocurre en la corrida, que crea su propio lote `LendingMonthlyClose` con `BatchScopeKey` `LendingClose:{aaaa-mm}`.
C59 — `CloseReopen` es una de las cuatro clases de corrida. C08 — `ReversoDeCierre` hereda el modo del cierre que
revierte. C46 — las líneas del cierre siguen sin diff, con evento resumen por corrida. C42 —
`LND_MonthlyCloseGeneralImpairments` es hecho (H).

#### R57 · Cierre base e historial de mora

**Decision**
- Al activar nace un `MonthlyClose` `Kind = Base` **fechado al corte**, con lo migrado y sin hechos
  (`CreateBaseCloseCommand`). El corte no tiene que ser fin de mes: el primer cierre mensual es el del mes que
  contiene la fecha de activación y toma el base como cierre anterior; el primer cierre emite sólo la diferencia
  (FR-140, SC-013).
- Historial en `LND_LoanArrearsHistory` (`LoanArrearsHistoryEntry`, H; origen `Migration`, `ComplementaryLoad`,
  `MonthlyClose`). `HistorialDeMora.Construir` elige la fuente que manda por mes, sigue novaciones y
  refinanciaciones y responde si está completo con los meses que pide `ReglaDePerdidaEsperada`; los faltantes
  siguen `Riesgo.ReglaMesesFaltantes` (`MayorObservada` por defecto, pendiente de validar).
- El historial opcional de la migración entra en K1 (`LND_MigrationArrearsHistory`); la carga complementaria
  (`LND_ArrearsHistoryLoads`, `ImportArrearsHistoryCommand`, plantilla `lending.arrears-history`) es K6a.

**Rationale**: el Anexo 2 pide la mora máxima mensual de hasta 36 meses y antes del corte no hay transacciones; los
cierres ya son instantáneas versionadas (I-6).

**Alternatives considered**
- Recalcular el historial desde las transacciones: antes del corte no las hay.
- Una fila mutable por crédito con un arreglo de meses: viola la inmutabilidad.
- Carga complementaria en K1 (`lending.delinquency-history`, F-16): es K6a (CH46).

**Trazas**: T49, T84 · FR-105, FR-139, FR-140, FR-142, SC-013, US8-10 · inf. R14 (CH26, CH46).

**Difiere del informe**: el inf. R14 llamaba al parámetro `Riesgo.PerdidaEsperada.ReglaMesesFaltantes`; es
`Riesgo.ReglaMesesFaltantes`.

#### R58 · Perfil regulatorio, selector de modelo y obligación sobrevenida

**Decision**
- Perfil de plataforma `COR_RegulatoryProfileVersions` (`RegulatoryProfileVersion`, H): tipo de organización (del
  documento `TiposDeOrganizacion`), actividad financiera, supervisión, categoría, grupo NIIF con vigencia (sembrado
  desde `AccountingSetup.NiifGroup`, alerta si difieren) y registro como operador de libranza (validado antes de
  originar por libranza). `AddRegulatoryProfileVersionCommand`, `/api/core/regulatory-profile`,
  `Core.RegulatoryProfile.View/Manage`, `/admin/perfil-regulatorio`.
- `SelectorDeModeloDeDeterioro` devuelve por segmento modelo (`ImpairmentModel`), fundamento (`ModelBasis`) y marca.
  K6a se habilita con `CatalogoDeCartera.EntregaVigente` (`EntregaDeCartera`); hasta entonces siempre Anexo 1, con
  marca si hay obligación.
- Obligación sobrevenida (D28): se evalúa al registrar el perfil y en el efecto de réplica de una
  `ReglaDePerdidaEsperada` registrada después de la activación (`EfectoDeReplicacionObligacionSobrevenida`); alerta
  `Cartera.ObligacionSobrevenida` y, sin K6a, los cierres llevan `ObligatoryModelUnavailable`.
- Adopción o desistimiento voluntario (`LND_ImpairmentModelElections`, `RegisterImpairmentModelElectionCommand`,
  K6a): se niega sin K6a, con historial incompleto o con aviso fuera de plazo.

**Rationale**: el tipo de entidad decide lo que la norma exige (FR-018) y otros módulos (SICSES, Aportes) lo usarán;
una constante de entrega reproduce el mecanismo de `EntregaDelComercio`.

**Alternatives considered**
- Perfil en `LND_`: otros módulos leerían datos de Cartera.
- Enum para el tipo de organización: la taxonomía cambia (la CE 100 eliminó tipos) y la PI se indexa por código.
- Bandera de configuración por ambiente para K6a: no corresponde a una entrega de código.

**Trazas**: T50 · FR-017, FR-018, FR-105, FR-108, FR-142, D28, Q2 · inf. R15 (preguntas A1, E2, E3).

#### R59 · Catálogo de modalidades como documento nacional

**Decision**: modalidades (`CONSUMO`, `COMERCIAL`, `VIVIENDA`, `MICROCREDITO`, `POPULAR_PRODUCTIVO_RURAL`,
`POPULAR_PRODUCTIVO_URBANO`, `PRODUCTIVO_RURAL`, `PRODUCTIVO_URBANO`, `PRODUCTIVO_MAYOR_MONTO`) y sub-segmentos
(`ConLibranza`/`SinLibranza`, `PersonaNatural`/`PersonaJuridica`, `Empleados` y, en `MICROCREDITO`,
`Inmobiliario`/`Empresarial`) son el documento `CatalogoDeModalidades` con vigencia. Por combinación da segmento SARC,
segmento de pérdida esperada, clave de `USURA` (modalidad certificada) o tope de vivienda, y si admite UVR. La
versión de la línea sella **códigos**, no una llave; la usura de un crédito en D se busca por la modalidad que el
catálogo vigente en D asigna. No hay `LND_CreditModalities`. Una modalidad retirada cierra las líneas a créditos
nuevos. El cambio de segmento efectivo (la libranza deja de estar activa) emite `DimensionContableCambiada`. La
versión de la línea sella además el **concepto tributario del interés** (D20): sin él, una línea de origen comercial
con tasa > 0 no se activa (`Lending.CreditLine.InterestTaxConceptMissing`).

**Rationale**: la clasificación es nacional (Decreto 455 de 2023, certificaciones de usura) y la cooperativa no
inventa modalidades; un solo dato conecta línea, usura, SARC, pérdida esperada y CUIF.

**Alternatives considered**
- Tablas de modalidades por cooperativa (R-06): clasificaciones distintas de la norma (CH28).
- Enum en el código: la norma agrega modalidades.

**Trazas**: T44 · FR-019–FR-021, FR-024, FR-125, FR-126, D18, D20 · inf. R16 (CH28; preguntas C11, E4, E10).

#### R60 · Parámetros de Cartera en la plataforma

**Decision**
- Módulo `LND` en `COR_ParameterVersions` (catálogo `ParametrosDeCartera`, `ParametrosDeCartera.Modulo = "LND"`
  sumado a `CatalogoDeParametros.Modulos`), con ámbitos nuevos `ParameterScopeKind.CreditLine = 6` y
  `LendingFactType = 7` (sobre `LND_FactTypes`). `IResolutorDeAmbitoDeParametro` pasa a compuesto
  (`ResolutorDeAmbitoCompuesto`, que delega por ámbito en `ReglasDePlataformaDeInventario` y en
  `ResolutorDeAmbitoDeCartera`).
- Claves por área, **sin** prefijo «Cartera.», de la §2.20 de las transversales; las de riesgo:
  `Riesgo.ReglaMesesFaltantes`, `Riesgo.DiaLimiteCierre`, `Riesgo.CierreProgramado`, `Riesgo.RedondeoDeterioro`,
  `Riesgo.CalificacionDiariaConArrastre`, `Riesgo.ExcepcionAportesArrastre` (forzado a `false` por D19), y la
  tolerancia única `Mora.ToleranciaDeSaldoVencido`. Los legales se siembran «pendiente de validar» y respetan el
  piso o techo nacional (R53).
- Entrega por clave: `DefinicionDeParametro.DisponibleDesde` y `CatalogoDeParametros.EntregaVigente` pasan a una
  entrega por módulo (`EntregaDeModulo`); una clave de Cartera de K2, K4 o K6a no se ofrece ni se acepta hasta que
  `CatalogoDeCartera.EntregaVigente` la alcance (`CadaParametroRespetaLaEntregaDeSuModulo`).
- Lo que no es decisión de la cooperativa (hora del ciclo, paralelismo, tanda) es configuración técnica
  (`Lending:Process`). Las decisiones con tablas (política SARC, atribuciones, órganos) tienen su tabla.

**Rationale**: un lector y un escritor únicos, auditados (módulo `Parameters`), con la pantalla de parámetros
existente; sin otra tabla para valores sueltos.

**Alternatives considered**
- Tabla `LND_` de parámetros: duplica la plataforma.
- `PAY_CompanyPolicies`: es de Nómina.
- Módulo «Cartera» y claves con prefijo «Cartera.»; dos tolerancias de mora (`Cartera.ToleranciaDeMora`,
  `Riesgo.ToleranciaSaldoVencido`): reemplazados (CH36).

**Trazas**: T43, §2.6, §2.20 · FR-007, FR-014, FR-105, FR-149, I-6 · inf. R17, DC-11, M (CH36).

**Conciliación**: C01 — en el ámbito `LendingFactType`, `Contabilidad.ModoDePaso` no se ofrece para un tipo
acompañante (sella el modo de su principal). C06 — `LND_FactTypes` suma `ShortCode` (3 a 4 letras, único) para la
`ScheduleKey`.

#### R61 · Pruebas de arquitectura de valores nacionales y cierres

**Decision**
- `LosValoresNacionalesSeLeenEnUnSoloSitio`: sólo `IValoresNacionales`, `ICalendarioHabil` e `INormativaVigente`
  leen `COR_NationalValues` y `COR_RegulatoryDocuments`; sólo el replicador los escribe.
- `SoloElReplicadorLeeElRegistroCentral`: nada de `Application/Lending` ni `Application/Payroll` referencia
  `IRegistroCentralDeValoresNacionales`, `IAdminDbContext` ni las entidades `Platform*`.
- `SoloLaReplicaEscribeLosValoresNacionalesDeNomina`: SMMLV, UVT y festivos `National` (reemplaza
  `SoloLaReplicaEscribeElSalarioMinimo`).
- `LaCarteraNoTieneValoresLegalesFijos` (R45) y `LosCierresDeCarteraNoSeReescriben` (amplía
  `LosHechosInmutablesNoSeModifican` a `MonthlyClose`, `MonthlyCloseLoan`, `MonthlyCloseReopening`, `LoanArrearsHistoryEntry`,
  `NationalValue` y `RegulatoryDocument`).
- `CadaParametroRespetaLaEntregaDeSuModulo` (R60). Se mantiene `LaUvtSeLeeEnUnSoloSitio` (sigue en `LectorDeUvt`).

**Rationale**: FR-014, FR-015 y SC-018 piden verificación automática; moldes existentes de Nómina y de la 012.

**Alternatives considered**
- Revisión manual: no escala y la spec pide verificación automática.

**Trazas**: §2.25, T40, T43, T93 · FR-014, FR-015, SC-018, Principios IV y XI · inf. R18 (CH39).

### 4.3 Riesgos

| Riesgo | Mitigación |
|---|---|
| La base administrativa crece en alcance; cada ambiente (DEV, QA, PDN) tiene su central | Dato de plataforma declarado en el plan; paquete de exportación e importación entre ambientes (B5) |
| Réplica atrasada: se calcula con datos viejos | `Cartera.ReplicaDeValoresNacionalesAtrasada`; el cierre exige réplica sincronizada y se niega si la central no responde (B7) |
| Una cooperativa con migraciones pendientes se salta en la réplica | La alerta de réplica atrasada la hace visible |
| El seeder de cooperativa necesita leer la central | `AdminDbContext` scoped disponible en el orquestador; sólo lo usa el replicador |
| Documento JSON mal formado registrado en caliente | Validación tipada antes de guardar (`IValidadorDeDocumentoNormativo`); `SchemaVersion` para leer versiones viejas y reproducir cierres |
| Consultas bitemporales más costosas | Memorizar por petición, como `LectorDeParametros` |
| `informacionHasta` en `LectorDeParametros` altera Inventario | Parámetro opcional; sin él, el comportamiento no cambia. «Una baja posterior a R no cuenta» exige leer filas dadas de baja con su fecha de baja |
| Filas de Nómina ya sembradas chocan con la réplica | Regla 1 de R51 (adopta o alerta `Nomina.ValorNacionalDistinto`); `ReplicaSobreCooperativaSembradaTests` |
| Cerrar con parámetros sin validar | Marca en el cierre, advertencia en SICSES y alerta; el dueño puede pedir que bloquee (E1) |
| Tablas que vienen como imagen (comercial persona jurídica, cortes, PDI, homologación) | Transcripción con `verified = false` y cotejo del dueño y la contadora (E8) |
| Segmento SARC de las modalidades productivas y usura certificada desde octubre de 2026 | Dato del catálogo, «pendiente de validar» (E4); sub-segmentos de microcrédito (E10) |
| Escribir unas 50.000 líneas de cierre en los dos motores | Por tandas; medir (SC-007 sólo cubre la causación) |
| Transacción final del cierre con todas las partes y sus listas por crédito | Medir con 50.000 créditos; si excede el TTL del arrendamiento, la exactitud la da la unicidad del encabezado por mes y versión |
| Historial migrado ausente o poco fiable | Carga complementaria (K6a) y `Riesgo.ReglaMesesFaltantes` |
| Dos fuentes del grupo NIIF | Alerta si difieren; enmienda posterior para que Contabilidad lea el perfil (E3) |
| Falsos positivos de literales sospechosos (30 como convención) | Exención comentada por archivo, como `ParametrosTributarios` |
| Antelación de la CE 100 frente a FR-105 | Rige la de la spec, más estricta, con la antelación como dato del documento (E2) |

---

## 5. Preguntas de estos frentes

Las preguntas de los cuatro informes, remitidas a la §5 de las transversales (no se repiten aquí). «Rige la
propuesta» quiere decir que, sin respuesta del dueño, se construye con la propuesta por defecto de §5.

| Frente | Pregunta del informe (resumen) | Código §5 | Estado |
|---|---|---|---|
| Mensajes | ¿El proceso origina mensajes de Cartera (enmienda de la 012)? | B1 | Rige la propuesta (R2) |
| Mensajes | Nombre del destino y del módulo de Recaudo | B2 | `Receipts` / `REC` (R7) |
| Mensajes | Informativos de cierre para FR-146 | A2 | Cuatro informativos (R26) |
| Mensajes | ¿«Producción» es `IsProduction()`? ¿Con qué ambiente corren DEV y QA? | B4 | Rige la propuesta; confirmar QA (R15) |
| Mensajes | ¿La reserva corre dentro de la unidad de trabajo de Comercial? | G6 | Rige la propuesta; plan B medido (R10) |
| Mensajes | ¿Los masivos llevan la lista por crédito? | C5 (fundida: volumen del resumido) | La lista va siempre (R5) |
| Mensajes | Confirmar por excepción: ¿la entrega pasa a `Processed`? | B8 | No; cambia el recibo (R9) |
| Mensajes | ¿Cómo queda Nómina registrada como recaudador? | — | Resuelta: semilla por migración (CH16, R8) |
| Mensajes | Retención de `COR_IntegrationMessages` | B3 | Sin purga; medir |
| Mensajes | Hora, paralelismo y tamaño de partes: ¿técnicos o de la cooperativa? | — | Resuelta: técnicos; la parte cabe en la tanda (CH36, R12, R5) |
| Mensajes | ¿Una `ObligacionRechazada` por evaluación? | — | Resuelta: sí (R10) |
| Contabilidad | P1 · `IDimensionesDeCartera` en la lista cerrada | A1 | Rige la propuesta (R28) |
| Contabilidad | P2 · Cuatro informativos en FR-125 | A2 | Rige la propuesta (R26) |
| Contabilidad | P3 · `DESC_CARTERA` contra un puente (D-08) | C1 | Rige la propuesta (R19) |
| Contabilidad | P4 · Dimensión «reestructurado» y reclasificación completa | C4 | Rige la propuesta (R18, R25) |
| Contabilidad | P5 · Disminución de deterioro: mismo ejercicio primero | C2 | Parámetro «pendiente de validar» (R25) |
| Contabilidad | P6 · Patrimonio técnico y solvencia; aplicación a COOFLOPAL | C3 | Registrados; la concentración bloquea sin dato (R28) |
| Contabilidad | P7 · Cuentas contingentes (9115, 913005, 9110) | A3 | Tipos por nombrar, modo `NoPasa` |
| Contabilidad | P8 · Resumido por día, por día y sucursal o mensual | C5 (fundida) | Por día; granularidad elegible (R23) |
| Contabilidad | P9 · ~20.000 líneas al día con tercero | C5 | Medir con `RUN_PERF_TESTS` |
| Contabilidad | P10 · Capitalización, castigo con deterioro insuficiente, condonación | C7 | Roles sin cuenta, pendientes de validar (R18) |
| Contabilidad | P11 · Origen de la dimensión «deudor empleado» | C8 | Estado actual de la persona (R18) |
| Contabilidad | P12 · Comprobante `CN` y cruce `PG` | C6 | Rige la propuesta (R20) |
| Contabilidad | P13 · Diferir comisiones (FR-017) | C10 | No se difieren |
| Contabilidad | P14 · Oficina sin sucursal contable | C9 | **Cambia la propuesta**: se bloquea y la activación exige el vínculo (CH49, R4) |
| Motor | Definición de la equivalencia lineal | D1 | Rige la propuesta (R34) |
| Motor | ¿El sábado es hábil (art. 829)? | D2 | Sí, «pendiente de validar» (R35) |
| Motor | Real/365 en bisiesto | D3 | Base fija 365 (R35) |
| Motor | ¿Una fecha valor anterior redistribuye un pago posterior? | D4 | Sí (R41) |
| Motor | Plan de los créditos migrados | D5 | Se conserva el capital por cuota (R37) |
| Motor | Anticipo que no alcanza la cuota | D6 | Se aplica parcial (R39) |
| Motor | Cancelación con anticipo y saldo a favor | D7 | El anticipo sí, el saldo a favor no (R39) |
| Motor | Cuota del ciclo antes del vencimiento | D8 | No reduce intereses (R39) |
| Motor | ¿Cuándo se causan los seguros? | D9 | Al vencer la cuota del período (R33) |
| Motor | Margen de la tasa variable | D10 | En la expresión del índice (R36) |
| Motor | Liquidación futura sin usura certificada | D11 | Proyecta con `ValidaHasta` (R32) |
| Motor | Plan en desembolso por tramos | D12 | Se regenera por tramo (R37) |
| Motor | Pagaduría con referencia fuera de D4 | D15 y A9 (fundida: fecha valor de Nómina y pagaduría) | Exención sometida como enmienda (R41) |
| Motor | Posiciones diarias o sólo en eventos | D13 | Diarias; particionar al pasar de 20.000 créditos (R31) |
| Motor | Hora de la causación | — | Resuelta: (d−1, d] desde las 00:30 (CH2, R12) |
| Motor | ¿Mora sobre intereses? | D14 | No por defecto (`Mora.SobreIntereses`) (R33) |
| Riesgo | ¿Quién registra en la central? | B5 | El maestro con segundo factor (R46) |
| Riesgo | ¿«Pendiente de validar» bloquea? | E1 | No; la activación exige Anexo 1 y usura (R52) |
| Riesgo | Perfil regulatorio en la lista cerrada | A1 (fundida: perfil y dimensiones) | Rige la propuesta (R58) |
| Riesgo | Fuente del grupo NIIF | E3 | El perfil, sembrado desde `AccountingSetup` (R58) |
| Riesgo | Antelación de la CE 100 frente a FR-105 | E2 | La de la spec (R58) |
| Riesgo | Segmento SARC de las modalidades productivas | E4 | Dato del catálogo, pendiente de validar (R59) |
| Riesgo | ¿La UVT entra ya en K1? | B6 | Sí, proyectada a Nómina (R51) |
| Riesgo | Nómina: proyección (A) o lectura directa (B) | A7 (antes G1) | Proyección en K1 (R51) |
| Riesgo | ¿Los festivos «Manual» cuentan para la mora? | E5 (fundida) | No (R35, R51) |
| Riesgo | La central no responde en el cierre | B7 | Se niega con motivo (R49, R56) |
| Riesgo | ¿Cierre manual o programado? | E6 | Manual (R56) |
| Riesgo | ¿La calificación diaria aplica arrastre? | E7 | Sí, informativa (R55) |
| Riesgo | Tablas en imagen de los anexos | E8 | Transcribir con `verified = false` y cotejar (R54) |
| Riesgo | Deterioro al centavo o al peso | E9 | Al centavo (R55, R60) |

**Agregadas por la armonización que tocan estos frentes**: A8 (falta de regla sólo bloquea en pantallas, R24), A9
(exención de fecha valor de los pagos sin interacción, R41), C11 (concepto tributario del interés de las líneas de
origen comercial, R59), D16 (atribución de Recaudo para pagos antes de un cierre, R41) y E10 (sub-segmentos de
microcrédito, R59).

---

## 6. Nombres fijados en este documento

Ninguno contradice las transversales; los que siguen faltaban y se fijan con las reglas T1–T4.

| Nombre | Qué es | Dónde | Regla |
|---|---|---|---|
| `LendingProcessOptions` | Clase de opciones de la sección técnica `Lending:Process` (R12) | `API/Integration` | T1: opciones técnicas en inglés, como `IntegrationOptions`; nombre del informe PM-13 |
| `ResolutorDeAmbitoDeCartera` | Implementación de Cartera de `IResolutorDeAmbitoDeParametro` para `CreditLine` y `LendingFactType` (R60) | `Application/Lending/Common` | T1 (servicio en español), T4 |
| `ResolutorDeAmbitoCompuesto` | Resolutor registrado en DI que delega por `ParameterScopeKind` en el de Inventario y el de Cartera (R60) | `Application/Common/Parameters` | T1, T4 (plataforma) |
| `ReglasDeParametrosDeCartera` | `IReglasDeParametros` de Cartera: pisos y techos legales (R53) | `Application/Lending/Common` | T1, T4 |
| `Application/Accounting/Integration` | Carpeta de la base común extraída del consumidor contable de Inventario (recibo, revisión de versión y moneda, rechazo con líneas) (R16) | Application | T1: carpeta nueva en inglés |
| `Accounting.Lending.Posted` · `Accounting.Lending.Rejected` · `Accounting.Lending.BatchProcessed` · `Accounting.LendingRules.Imported` · `Accounting.RegulatoryIndicator.Registered` | Eventos de auditoría del lado contable (R16) | Contabilidad | T1 (`Área.Recurso.Motivo`); literales del informe DC |
| `EfectoDeReplicacionCorreccionDeValorNacional` | `IEfectoDeReplicacion` de Cartera que encola la corrida `NationalValueCorrection` (C05) | `Application/Lending/Accrual` | T1, T4; molde `EfectoDeReplicacionObligacionSobrevenida` |

---

## 7. Conciliación

Los puntos se decidieron en `decisiones-transversales.md` §7. Donde una decisión de esta mitad quedó afectada, lleva al
final su línea **Conciliación**.

| Punto | Decisión |
|---|---|
| RS1-1 · Quién encola la corrida `NationalValueCorrection` | C05: la réplica, por `EfectoDeReplicacionCorreccionDeValorNacional`, en la misma transacción y sólo con Cartera activada |
| RS1-2 · Cómo emite la corrida `NationalValueCorrection` | C03: `AjusteDeCausacionPorFechaValor` de la corrida va por partes (`PorLotes`, `CA`, `Confirmation:{partePublicId:N}`) y entra a la lista de T14; provocado por una operación, es su acompañante. C02: los hechos por crédito que nace un proceso (`SaldoAFavorReconocido` de la corrida, entre otros) tienen origen la parte de su tanda, modo propio y clave `Confirmation:{loanPublicId:N}` |
| RS1-3 · Clave de los hechos de una operación con otro modo u otro comprobante | C01: unidad = una operación y su modo; los acompañantes son lista fija por tipo y sellan el modo del principal; un comprobante por unidad lo resuelve Contabilidad. Se descarta la clave `Confirmation:{uuidv5}` por grupo |
| RS1-4 · `ScheduleKey` de 60 caracteres | C06: `CAR.{ShortCode}|{disparador}|{hora}|{granularidad}`, `LND_FactTypes.ShortCode` de 3 a 4 letras; hora vacía en `FinDeCausacion` y `CierreDeCartera`; `Hora` se sella como `HoraDiaria` |
| RS1-5 · Orden de los numeradores en la confirmación de Comercial | C24: filas de Inventario → `ReservarAsync` (setup → deudor → numerador de Cartera) → numerador de Inventario |
| RS1-6 · Filtro del índice único de la causación | C50: filtrado a `Kind = Accrual` y `ReversesTransactionId` nulo |
| RS1-7 · Qué hecho emite la puesta al día de una operación | C04: partes propias (una por tipo, la operación como tanda), clave `Confirmation:{partePublicId:N}`, modo de los masivos |
| RS1-8 · Nombre de la marca de parámetros sin validar | C76: `UnvalidatedParameters` |
| RS1-9 · Clases de corrida en T5 | C59: `DailyCycle`, `MonthlyClose`, `CloseReopen`, `NationalValueCorrection` |


---

# Research (2 de 2): Cartera financiera — enmiendas, retiro de lo heredado, seguridad y adaptadores

**Feature**: 013 | **Fecha**: 2026-10-05 | **Rama**: `013-cartera-financiera` | **Spec**: [spec.md](spec.md) |
**Nombres y decisiones**: [decisiones-transversales.md](decisiones-transversales.md)

Segunda mitad de `research.md` (fase 0). Consolida los otros cuatro frentes investigados: enmiendas a otros módulos
(Nómina, Personas y Comercial por las interfaces de Cartera, E-02 a E-05); retiro de la cartera heredada; seguridad
(permisos, roles, alcance, atribuciones, actas, segregación, auditoría, habeas data y listas); y adaptadores (canales
externos, mensajería al deudor, cobranza, archivos, documentos, informes, plantillas y pantallas). La primera mitad
(mensajes, contabilidad, motor, riesgo y valores nacionales) precede en este mismo archivo (secciones 1 a 7, R1–R61).

Decisiones del dueño que se dan por fijas: **Q1 B** (sin recaudo ni giros provisionales; sin Recaudo y Tesorería
reales no hay activación en producción), **Q2 A**, **Q3** (las cuotas de servicios a asociados no son cartera, D29),
los defectos D1–D29 y las interpretaciones I-1–I-6.

**Convenciones.**

- **Gana la transversal.** Los nombres salen literales de `decisiones-transversales.md` (§2) y cada decisión remite a
  sus T. Cuando un informe propuso otra cosa, la línea **Difiere del informe** lo dice y la propuesta queda como
  alternativa descartada, para que nadie la reintroduzca.
- **Numeración.** Decisiones **R101–R154** en esta mitad, para no chocar con R1–R61 de la primera; no existen R62–R100
  (§7.6 de las transversales). Secciones numeradas desde la 8, a continuación de las siete de la primera mitad. Siglas de origen:
  `DE-xx` (enmiendas), `R-xx` (retiro, con guion), `S1–S11` (seguridad) y `F-xx` (adaptadores); choques `CH1–CH50`;
  preguntas: códigos de §5 de las transversales.
- **Formato.** Cada decisión: **Decision** / **Rationale** / **Alternatives considered** y una línea **Trazas** (T, FR,
  origen). Cada frente abre con sus **hechos del código** (con ruta) y cierra con sus **riesgos**. El frente del retiro
  publica además la clasificación tabla por tabla y las llaves entre ellas.
- **Rutas** relativas a la raíz del repositorio. Los números de línea son los del día de la investigación (2026-10-05,
  `6a565725`) y orientan, no obligan.
- Al final: preguntas de estos frentes (remisión a §5), nombres fijados aquí y la conciliación (remisión a la §7 de las
  transversales).

## Índice de decisiones

| R | Tema | T | Entrega |
|---|---|---|---|
| R101 | Puertos que ofrece Cartera | T90, T1, T4 | K1 |
| R102 | El contrato corre en la unidad de trabajo del recaudador | T91, T34, T21, T101 | K1 |
| R103 | Nómina como recaudador: clave, fecha valor y pagador | T92, T21, T16 | K1 |
| R104 | Liquidación definitiva: propuesta por la consulta y aprobación todo o nada | T92 | K1 |
| R105 | Reverso de la definitiva y ajuste con el saldo exacto | T92, T31 | K1 |
| R106 | Nómina ordinaria como pagaduría interna | T92 | K1 |
| R107 | `DESC_CARTERA` contra el puente (enmienda D-08 de la 010) | T53, T84 | K1 |
| R108 | Columnas de Nómina y migración `NominaRecaudaPorElContrato` | T87, T92 | K1 |
| R109 | «Cartera por activar» en las dos aprobaciones | T85, T84 | K1 |
| R110 | Personas, retiro de asociado, extractos y portafolio por la consulta | T94, T88 | K1 |
| R111 | Comercial: consultas reales antes y después de activar | T95 | K1 |
| R112 | Comercial: reserva y liberación del número de crédito | T22, T21, T34 | K1 |
| R113 | Venta a crédito v2 y respuestas de Cartera a Comercial | T18, T17, T15 | K1 |
| R114 | El destino `Lending` se habilita por el registro | T16 | K1 |
| R115 | Ventas a crédito sin Cartera: `CreditWithoutLending` | T95, T83 | K1 (sujeto a A6) |
| R116 | SMMLV, UVT y festivos de Nómina como proyección de la réplica | T93 | K1 |
| R117 | Lista cerrada verificada: `LaCarteraSoloSeTocaPorSusInterfaces` | T90 | K1 |
| R118 | Pruebas que se adaptan y se agregan por las enmiendas | §2.25 | K1 |
| R119 | Clasificación de las 93 tablas heredadas | T86 | K1 |
| R120 | Llaves entre lo que se conserva, lo que se retira y otros módulos | T87 | K1 |
| R121 | Estado intermedio: los consumidores pasan a los puertos «sin activar» | T88, T85 | K1 |
| R122 | Migración destructiva con guarda `RetiroDeLaCarteraHeredada` | T87 | K1 |
| R123 | Diagnóstico, runbook y pruebas del retiro | T87 | K1 |
| R124 | Nombres nuevos sin reutilizar ninguno heredado | T2 | K1–K6b |
| R125 | `Lending` sólo para la Cartera nueva; lo conservado a `Savings` | T3, T99 | K1 (sujeto a A10) |
| R126 | Rutas, pantallas, menú, manual y reportes heredados | T88, T89, T98, T99 | K1 |
| R127 | Permisos legados y proyecto `IngenIA365ERP.Legacy` | T89, T60 | K1 |
| R128 | Orden de los commits del retiro | T88, T96 | K1 |
| R129 | Catálogo `Lending.*` por acción, sembrado entero en K1 | T60 | K1 |
| R130 | Roles de Cartera como plantillas y un comando que los crea | T61 | K1 |
| R131 | Alcance por oficina y línea que falla cerrado | T62 | K1 |
| R132 | Atribuciones por niveles con matriz versionada | T63 | K1 (sujetos de K3) |
| R133 | Actas propias de Cartera | T64 | K1 |
| R134 | El motor `COR_Approval*` de la 012 no decide créditos | T65 | K1 |
| R135 | Segregación por persona | T66 | K1 |
| R136 | Cartera en la cadena de auditoría | T67 | K1 |
| R137 | Auditoría del contrato síncrono: el intento fuera, el hecho dentro | T68 | K1 |
| R138 | Habeas data por finalidad; reclamos del reporte en Cartera | T69 | K1 · K4 |
| R139 | Listas restrictivas en la plataforma; el resultado en Cartera | T70 | K1 |
| R140 | Adaptadores externos por puerto, registro y canal simulado | T71 | K1 |
| R141 | Configuración por cooperativa y credenciales fuera de la base | T72 | K1 |
| R142 | Guardia única del efecto legal de los canales simulados | T73 | K1 |
| R143 | Mensajería saliente a personas y cola de mensajes al deudor | T74 | K1 |
| R144 | Ventana legal de contacto y despacho por sondeo | T75, T7 | K1 · K4 |
| R145 | Plantillas de mensaje versionadas | T76 | K1 |
| R146 | Cobranza y aviso previo | T77 | K4 (datos en K1) |
| R147 | Archivos planos de Cartera con el motor común | T78 | K1 · K2 |
| R148 | Layouts regulatorios embebidos con cotejo por campo | T79 | K5 |
| R149 | Documentos emitidos con código de verificación | T80 | K1 (propuesto) · K5 |
| R150 | Adjuntos, consulta a centrales y pagaré | T81 | K1 |
| R151 | Plantillas de importación sobre el ejecutor común | T82 | K1 · K2 · K6a |
| R152 | Carga de migración por partes | T83 | K1 |
| R153 | Centro de informes y tablero | T99 | K1–K5 |
| R154 | Pantallas de Cartera y menú | T98, T100 | K1–K6b |

---

## 8. Frente de enmiendas a otros módulos (E-02 a E-05)

### 8.1 Hechos del código

**Nómina frente a la cartera heredada**

- **La definitiva lee Cartera directamente.** `SettlementInputLoader.ProponerDeudasAsync` (l. 630-721) lee
  `db.LoanPortfolios` (PersonId del empleado, `CurrentBalance > 0`, sin `ClosingDate`, por `DisbursementDate`). Con
  `SaldoTotal` propone el saldo; con `SoloCuotasCausadas`, `min(CurrentBalance, InstallmentAmount + DefaultBalanceCurrent)`;
  con `NoProponer`, nada. Arma el desglose JSON `{capital, intereses, mora, cuotasPendientes, cuota, saldoTotal, modo}`
  y una `DeudaPropuesta` con `LoanPortfolioId` entero, concepto `DESC_CARTERA` y `AccountedByOtherModule = true`. Si
  Cartera lanza, la propuesta sale vacía con el aviso `Payroll.Settlement.PortfolioUnavailable` (l. 665-669).
  `Conservar` (l. 727-737) mantiene un ajuste mientras el propuesto no cambie.
  _(src/Core/IngenIA365ERP.Application/Payroll/Services/SettlementInputLoader.cs)_
- **Política `DeduccionAlRetiroModo`**: `SaldoTotal` (defecto), `SoloCuotasCausadas`, `NoProponer`
  (`CompanyPolicyKeys.cs` l. 29, 62-67, 107-108; `PayrollPolicyReader.cs` l. 132-137, 192). FR-147 la conserva.
- **Aprobar la definitiva escribe en Cartera.** `ApproveSettlementCommand` es `IReintentableAnteConcurrencia` (l. 47-51)
  y corre en `TransaccionDeLiquidacion.EjecutarAsync` (l. 95-96). Su gancho `CerrarFichaYPagarAsync` (l. 116-215)
  llama sin `ISender` a `RecaudoDeCredito.AplicarAsync` por cada descuento `CooperativeLoan` aplicado, con
  `PaymentMethod = "NM"` (`FormaDePagoNomina`, l. 73), fecha de pago = fecha de retiro y `CashAccountCode` = la cuenta
  débito de `DESC_CARTERA` (`CuentaDebitoDelDescuentoAsync`, l. 222-234). Guarda `CarteraTransactionPublicId` y
  `RemainingBalanceAfter`. Un fallo de Cartera revierte todo (l. 149-150).
- **Orden comprobante → gancho.** `SettlementRunWorkflow.ApproveAsync` prepara el comprobante (`poster.PostAsync`)
  antes del gancho `antesDeGuardar` y guarda con un `SaveChanges` (l. 57-128): hoy el recaudo ocurre después de armar
  el NM. _(src/Core/IngenIA365ERP.Application/Payroll/Settlements/Common/SettlementRunWorkflow.cs)_
- **Transacciones.** `TransaccionDeLiquidacion` delega en `TransaccionExplicita` con `descartarAlRepetir = false`
  (l. 33-37); `TransaccionExplicita` se une a la transacción en curso (`ctx.Database.CurrentTransaction`) y revierte
  con un `Result` fallido. Prohíbe enviar por `ISender` un comando reintentable dentro de la transacción. **No existe**
  un método público que diga si hay una en curso. _(src/Core/IngenIA365ERP.Application/Common/Persistence/TransaccionExplicita.cs)_
- **`RecaudoDeCredito`** imputa cuota a cuota mora → interés → capital sobre `PendingInstallments`, actualiza el
  `LoanPortfolio`, escribe `LendingTransaction` RC, contabiliza por `AccountingPoster` con las cuentas de texto de la
  línea y llama `SaveChangesAsync` dentro. Lo usan `ProcessPaymentCommand` y la definitiva.
  _(src/Core/IngenIA365ERP.Application/Lending/Payments/Services/RecaudoDeCredito.cs)_
- **Reversar la definitiva no reversa Cartera**: marca los recaudos `Reverted`, los lista y pide reversarlos «por su
  propio flujo» (`ReverseAndDiscardSettlementCommands.cs` l. 17-26, 76-83, 123-125; `LiquidacionDefinitiva.razor`
  l. 115, 146, 373). Contradice FR-080 y FR-088.
- **Cálculo y ajuste.** `DefinitivaCalculator.SincronizarDeduccionesAsync` (l. 84-141) empareja por `LoanPortfolioId`,
  `RecurringNoveltyId` o `PublicId` (`Coincide`, l. 143-147). `AdjustSettlementDeductionCommand` sólo baja el valor, exige
  motivo, no crea versión y crea la línea con `AffectsAccounting = Kind != CooperativeLoan` (l. 92). La pantalla
  calcula el saldo que queda como «aproximado: Cartera aplica primero mora, luego intereses y luego capital»
  (`LiquidacionDefinitiva.razor` l. 320).
- **Llaves de Nómina hacia Cartera.** `SettlementDeduction.LoanPortfolioId` (int?) con
  `FK_PAY_SettlementDeductions_LND_LoanPortfolios_LoanPortfolioId` (Restrict), `IX_PAY_SettlementDeductions_LoanPortfolioId`
  y `UK_PAY_SettlementDeductions_Termination_Loan` filtrado a vivas (`SettlementDeductionConfiguration.cs` l. 36-44;
  migraciones `NominaPrestacionesYDian` y `SettlementDeductionsUnicosEntreVivas`). Es **la única FK física** de una tabla
  `PAY_` a una `LND_`. `PayrollNovelty.LoanPortfolioId` (l. 51, tabla real `PAY_Novelties`) no tiene FK ni índice y
  nadie la escribe ni la lee.
- **`DESC_CARTERA` sin asiento (D-08 de la 010).** `WellKnownConceptCodes.LoanDeduction = "DESC_CARTERA"` (l. 19). La
  excepción la aplican `LineFactory` (`ICalculationRule.cs` l. 136), `ProposedDeductionsRule` (l. 35-38) y
  `AdjustSettlementDeductionCommand` (l. 92); los contabilizadores omiten esas líneas (`SettlementAccountingPoster.cs`
  l. 29, 51; `PayrollAccountingPoster.cs` l. 77). El pasivo con el empleado lo bajaba el recaudo heredado con la cuenta
  que Nómina le pasaba.
- **Ordinaria: descuentos DN.** `CalculationInputLoader` lee `db.PayrollDeductionEntries` tipo `DN` con fechas de texto
  `yyyyMMdd` (l. 143-149) y las convierte en `NoveltyInput` de origen `LoanDeduction`, concepto `DESC_CARTERA`, sin
  guardarlas: la línea queda con `NoveltyId` nulo y sin enlace a la obligación. El motor les salta el tope y no las difiere
  (`PayrollCalculationEngine.cs` l. 222-227). Las genera el heredado `ProcessPayrollDeductionCommand`, que rebaja saldos
  antes de aprobar la nómina, sin transacción ni comprobante (marcador «E3 (feature 009)», l. 41-110).
- **Aprobar la ordinaria.** `ApprovePayrollRunCommand` es reintentable (l. 34-39), prepara el NM (l. 150-157) y guarda con
  un `SaveChanges` (l. 197), sin `TransaccionExplicita` ni llamada a Cartera. Ya tiene el patrón de confirmación expresa
  `ConfirmWithoutSegregation` (l. 39, 139). `CancelNovelty` y `CorrectNovelty` rechazan las novedades `LoanDeduction`.
  `ReintentoPorConcurrenciaBehaviorTests` (l. 120-132) fija que aprobar y reversar las dos son reintentables.
- **Festivos y SMMLV.** `PAY_Holidays` (orígenes `Ley51Fixed`, `Ley51MovedToMonday`, `Ley51Easter`, `Decreed`, `Manual`),
  leídos por `VacationQueries` (l. 35, 63). El SMMLV es la fila `SMMLV` de `PAY_PayrollLegalParameters`
  (`LegalParameterCodes.cs` l. 12), que cada cargador lee por su cuenta. Precedente de lector único: `LectorDeUvt`
  (`LaUvtSeLeeEnUnSoloSitio`).
- **Código de módulo de Nómina**: `ModuloContable.Nomina = "NOM"`; `Cartera = "CAR"`, `Inventario = "INV"`,
  `Tesoreria = "TES"` (`src/Core/IngenIA365ERP.Application/Accounting/Posting/ModuloContable.cs` l. 12-18). No hay
  código de Recaudo.

**Personas, retiro de asociado y extractos**

- `DeletePersonCommand` bloquea con `Person.HasActiveLoans` si hay `LoanPortfolios` del deudor con saldo (l. 35-40); no
  mira codeudores. Ninguna prueba cubre esa rama (`AltaDePersonaEnUnPasoTests` l. 100 borra a alguien sin créditos).
- `GetPersonDetailQuery` arma `PortfolioSummaryDto` con tres consultas a `LoanPortfolios` (l. 146-170) y los ahorros con
  `SavingsAccounts` (saldo fijo en 0); se expone en `GET /api/core/people/{id}/detail` (`Core.People.View`) y ninguna
  pantalla lo consume.
- **Consumidores no listados en E-04**: el retiro de asociado cuenta créditos vivos en `LoanPortfolios`
  (`WithdrawalQueries.cs` l. 68; `ProcessWithdrawalCommand.cs` l. 46); los extractos (`GenerateStatementsCommand.cs` l. 36,
  `StatementQueries.cs` l. 66 con `Include(CreditLine)`) y `PersonPortfolioReportQuery.cs` l. 71 leen `LoanPortfolios`.
  Todos dejan de compilar al retirar las tablas de crédito.
- **Ahorros conservados en el mismo espacio.** `SavingsAccount` (`LND_SavingsAccounts`) y `DepositEntry`
  (`LND_DepositEntries`) viven en `Domain.Entities.Lending` (93 archivos); Personas y Débito (`IssueDebitCard`,
  `ProcessDebitTransaction`) los leen. Una regla «nadie depende de `*.Lending*`» caería sobre lo conservado.

**Comercial frente a Cartera**

- **Puerto existente.** `IConsultasDeCartera` declara exactamente `EstadoCrediticioAsync(ConsultaCrediticia)` y
  `EstadoDeValidacionAsync(Guid)` (l. 13-23). `EstadoCrediticioDto(Status, AvailableQuota, Lines, EvidenceId)` con
  `LendingEnabled`; `LineaDeCreditoDto` sin periodicidades; `EstadosDeValidacion` = `Pending`, `Validated`, `Failed`.
  Registrada: `ConsultasDeCarteraNoHabilitada` (`DependencyInjection.cs` l. 344-347), que responde `CarteraNoHabilitada`
  (`NotEnabled`). _(src/Core/IngenIA365ERP.Application/Common/Integration/Lending/*.cs)_
- **Prueba de frontera.** `InventarioNoConoceContabilidadNiCartera` exige el espacio `Application.Common.Integration.Lending`,
  una sola `IConsultasDeCartera` con sus dos métodos (l. 118-127, hecho
  `El_puerto_de_Cartera_declara_exactamente_sus_dos_consultas`) y prohíbe a Inventario los espacios con segmento
  `Accounting` o `Lending` salvo `Common.Integration` (l. 41-42), los `DbSet` de `Entities.Accounting`/`Entities.Lending`
  (l. 81-82) y el literal `"LND_"` (l. 135).
- **Crédito en la venta.** `CreditoEnLaVenta.ValidarAsync` (l. 51-91) consulta a Cartera pero sólo usa `LendingEnabled`:
  siempre deja `PendingValidation = true` y `CreditOrigin = ProvisionalCredit`. Los parámetros
  `Cartera.PoliticaSinRespuesta` y `Cartera.ConsultaSegundos` existen (`ParametrosDeInventario.cs` l. 159-161) y nadie los
  lee. `EvaluateSaleCreditQuery` devuelve `ProvisionalCredit` fijo (l. 110). `CreditOrigin` ya trae `Validated = 3` y
  `LendingNoResponse = 2` (`InventoryEnums.cs` l. 158).
- **Pestaña Crédito.** `GetSalesDocumentCreditQueryHandler` (`SalesQueries.cs` l. 563-617) calcula
  `destinationAvailable` con `destinos.Any(Lending)`, global al proceso (l. 595). La e2e `Ventas/CreditoProvisionalTests`
  exige `lendingEnabled = false`, `destinationAvailable = false` y cero intentos hacia Lending (l. 45, 110, 139-141).
- **Emisión hacia Cartera.** `EmisionDeInventario` emite `VentaACreditoRegistradaV1` por cada pago de clase crédito
  (l. 508-544, `CreditLineCode = null`) y `AjusteDeVentaACredito` en notas, devoluciones, anulaciones, reemplazo por
  rechazo DIAN y nota débito (l. 450-650). Destino `Lending` (`CatalogoDeMensajesV1.cs` l. 69-70). La v1 no lleva número
  de crédito.
- **Despachador.** Salta `Lending` por cooperativa mientras `Cartera.IntegracionHabilitadaDesde` esté vacío o sea futuro
  (`DespachadorDeMensajes.cs` l. 407, 416-423); `EntregasElegibles` sólo entrega a destinos con `IDestinoDeMensajes` en DI
  (l. 24-35). Existen `DeliveryStatus.ValidationFailed = 5` y la alerta `Integracion.ValidacionFallida`, sin escritor.
  `IntegrationDestinations` sólo tiene `Accounting` y `Lending`.
- **Medio y pago.** `PaymentMeans.SuggestedCreditLineCode` es texto «sin llave a `LND_*`» (l. 99-100), copiado al pago sólo
  en clases de crédito (`DocumentPayment.cs` l. 129). `DocumentPayment` es inmutable al confirmar y no tiene campo para el
  número de crédito.
- **D29 no cabe hoy.** `ClasesDeMedio.EsCredito` = `AssociateCredit` o `CustomerCredit` (l. 13): toda venta con ese pago
  emite a Lending. La clase `Other` saldría de contado para la DIAN (`ConstructorDelCanonico.cs` l. 205-251) y sin tercero
  ni cruce `FV` en la matriz (`ConstructorDeLineasDeInventario.cs` l. 365, 572). `PaymentMeans` no tiene campo para
  excluir a Cartera.
- **012 que choca con FR-145**: su FR-062 prohíbe guardar datos de Cartera y su FR-060 deja la respuesta sólo como
  evidencia (spec 012 l. 1760-1788). Siguen abiertas sus tareas IC T661–T667, la rama Cartera de T886 y T1026–T1027.
- **Parámetros de Comercial.** `Cartera.CuentaPorCobrarRegistradaPor` (`Contabilidad` | `Cartera`, defecto
  `Contabilidad`, sellado al confirmar) y `Cartera.IntegracionHabilitadaDesde` (fecha, admite vacío). Descartan borradores
  `DiscardInventoryDraftCommand` (l. 18) y `DiscardPosDraftCommand` (l. 471). La referencia del pago la toma el canónico en
  `FuenteDeEmisionDeInventario.cs` l. 137-139.

**Plataforma que las enmiendas tocan**

- **Auditoría y tareas tras el commit.** `AuditBehavior` envuelve en `TransaccionExplicita` los comandos de módulos
  encadenados y escribe `COR_AuditOutbox` dentro; el rechazo va por otro contexto (`RechazoAsync`, l. 138). Sólo audita
  tipos `*Command` (l. 42). `TareasTrasElCommit` corre si la petición más externa terminó bien. Pipeline: Validation →
  Logging → TrasElCommit → Idempotency (`COR_OperationKeys`, Guid) → Audit → Reintento → Performance
  (`DependencyInjection.cs` l. 31-47): **los rechazos de validación no llegan a la auditoría**.
- **Cerrojos.** `ICerrojoPorClave` (`sp_getapplock` / `pg_advisory_xact_lock`) vive en `Application/Inventory/Common`.
- **Cadena.** `AuditoriaEncadenada.Modulos` (l. 38-49): Inventory, ElectronicInvoicing, Integration, Approvals, Alerts,
  Parameters, Taxes, PaymentMeans, Navigation. **Lending no está**, aunque `ModuloDeAuditoria` tiene la constante y la
  regla `.Lending` (l. 21, 48). No hay regla `.Compliance` (el habeas data cae en `General`). `AuditRetention.ModulosDeDiezAnios`
  (l. 27-31) tampoco incluye Lending. Sellado por tandas de 200, ancla cada 1.000 eventos; `[SinDiffDeAuditoria]` saca una
  entidad del diff (precedente `KardexEntry`); verificación sólo manual (`POST /api/audit/integrity/verify`,
  `AuditLog.VerifyIntegrity`). Contexto del evento: actor, IP, User-Agent, endpoint, canal (`X-Canal`), `OperationKey`,
  `Reason`; **sin oficina ni dispositivo**.
- **Parámetros.** `COR_ParameterVersions`, un lector (`LectorDeParametros`) y un escritor; módulos INV, TAX, EINV; ámbitos
  `None`, `Warehouse`, `DocumentType`, `PointOfSale`, `CashRegister`, `ThirdPartyKind`; sin ámbito de línea ni módulo LND.

### 8.2 Decisiones

#### R101 · Puertos que ofrece Cartera

**Decision**
- Viven en `Application/Common/Integration/Lending`, donde ya está `IConsultasDeCartera`. Cada uno declara exactamente sus
  métodos:

  | Puerto | Métodos | Provisional → real | Lo usan |
  |---|---|---|---|
  | `IConsultasDeCartera` (existe) | `EstadoCrediticioAsync`, `EstadoDeValidacionAsync` | `ConsultasDeCarteraNoHabilitada` → `ConsultasDeCartera` | Comercial |
  | `IReservasDeCartera` | `ReservarAsync(SolicitudDeReserva)`, `LiberarAsync(SolicitudDeLiberacion)` → `ResultadoDeReservaDto` | `ReservasDeCarteraNoHabilitada` → `ReservasDeCartera` | Comercial |
  | `IConsultaDeObligaciones` | `ObligacionesAsync`, `CuotasDeNominaAsync` | `ConsultaDeObligacionesSinActivar` → `ObligacionesDeCartera` | Personas, Nómina, Aportes (retiro), Cumplimiento |
  | `IContratoDeObligaciones` | `ListarAsync`, `SimularAsync`, `AplicarAsync`, `ReversarAsync`, `ResolverSaldoAFavorAsync`; `const VersionDelContrato = 1` | `ContratoDeObligacionesSinActivar` → `ContratoDeObligacionesDeCartera` | Recaudo, Nómina, Aportes, recogida, recaudador de prueba |
  | `IDimensionesDeCartera` | catálogo de códigos y combinaciones en uso | `DimensionesDeCartera` | Contabilidad (R28, T59) |

- Los provisionales viven en el mismo espacio. «No habilitada» (Comercial) responde `NotEnabled`; «sin activar»
  (obligaciones) responde con el motivo `Lending.NotActivated` («Cartera no activada»). Los reales viven en
  `Application/Lending/Integration` y los reemplazan en `DependencyInjection`.
- DTO en el mismo espacio, en español, entrada sin sufijo y salida con `Dto` (§2.1): `SolicitudDeReserva`,
  `SolicitudDeLiberacion`, `ResultadoDeReservaDto`; `ConsultaDeObligaciones`, `ObligacionesDePersonasDto`,
  `ObligacionesDePersonaDto` (con `MigratedNotActivated`, C26), `ObligacionDto`, `ConsultaDeCuotasDeNomina`, `CuotasDeNominaDto`,
  `CuotaDeNominaDto`; `SolicitudDeAplicacion` (lote de `PagoAAplicar`), `NoDescontado`, `ReferenciaDeRecibo`,
  `SolicitudDeReverso`, `RespuestaDelContratoDto<T>`, `ComprobanteDeAplicacionDto`.
- Enums de los puertos con tipo en español y valores en inglés idénticos al motor: `ClaseDeRecaudador {Receipts, Payroll,
  Contributions, OwnLoanPayoff, Test}`; `ClaseDeMedioDeRecaudo` = valores de `PaymentMeansClass` de la 012 más
  `OwnPayrollDeduction`, `ExternalPaymasterDeduction`, `InternalOffset`; `ModalidadDePago` = valores de `PaymentModality`;
  `MotivoDeNoDescuento {DeductionCap, InsufficientNet, Termination, Leave, Disability, Other}` más `DeductionPriority`
  (C56); `ResultadoDelContrato {Applied, AlreadyApplied, Rejected}` más `Answered` para listar y simular (C28);
  `EstadoDeCartera {NotActivated, Active}`.
- Se amplían los DTO de Comercial: `EstadoCrediticioDto` suma `TotalQuota`, `DaysPastDue`, `BlockReason`, `AllowsSale`;
  `LineaDeCreditoDto` suma `Periodicities`; `EstadosDeValidacion` suma `NotIncorporated`.
- Los mensajes que Cartera emite van en `Application/Common/Integration/Contracts/Lending`.

**Rationale**: es el molde de la 012 (T31: `IContabilidadParaInventario`, `IConsultasDeCartera`). Nómina, Personas y
Comercial sólo dependen de `Common.Integration`, que la regla de frontera ya admite. Las reservas van aparte porque una
consulta no cambia el estado del negocio (FR-059) y reservar sí; además la prueba de la 012 fija `IConsultasDeCartera` en
dos métodos. «Sin activar» y «no habilitada» son estados distintos: el primero es de Cartera (FR-142), el segundo es como
Comercial ya trata la ausencia de Cartera (crédito provisional).

**Alternatives considered**
- Sumar reservar y liberar a `IConsultasDeCartera`: rompe «una consulta no cambia nada» y la prueba existente.
- Un solo puerto `ICartera`: no permite fijar la lista cerrada por consumidor (FR-008).
- Que Nómina y Comercial envíen comandos de `Application.Lending` por `ISender`: acopla tipos y repite el defecto de
  reintento anidado de la 010.
- `IReservasDeCredito`, `ResultadoDeReserva`, `ConsultaDeObligacionesNoHabilitada`, `ContratoDeObligacionesNoHabilitado`
  (DE-01): CH11, CH43.
- Valores de enum en español (`Cancelacion`, `ValorLibre`, `Nomina`, `Prueba`) (DE): CH40.

**Trazas**: T90, T1, T4 · FR-008, FR-053, FR-057, FR-059, FR-061, FR-083, FR-086, FR-145, FR-147, FR-148, SC-018 · DE-01,
R-03.

**Difiere del informe**: DE-01 llamaba `IReservasDeCredito`, `ResultadoDeReserva`,
`ConsultaDeObligacionesNoHabilitada` y `ContratoDeObligacionesNoHabilitado`, y daba valores de enum en español;
contaba cuatro puertos (CH11, CH40, CH43; T90).

**Conciliación**: C26 — propiedades de puerto en inglés: `MigratedNotActivated` (también en `CuotasDeNominaDto`), no
`MigradaSinActivar`. C27 — `IDimensionesDeCartera` vive en `Common.Integration.Lending` con los otros cuatro: cinco
puertos. C28 — `ResultadoDelContrato` suma `Answered`. C56 — `MotivoDeNoDescuento` suma `DeductionPriority` (FR-115)
antes de fijar `IContratoDeObligaciones` v1. C30 — los medios `AssociateCredit` y `CustomerCredit` se declaran y el
contrato los rechaza (`Lending.Payment.MeansClassNotAllowed`). C38 — `SimularAsync` admite
`PaymentIdempotencyKeyToReverse`. C29 — `CombinacionDeCartera.Role` lleva el nombre del rol como texto. C35 — el
recaudador de prueba usa el código `TST` (`ClaseDeRecaudador.Test`).

#### R102 · El contrato corre en la unidad de trabajo del recaudador

**Decision**
- `ContratoDeObligacionesDeCartera` usa el `IApplicationDbContext` del ámbito de quien llama. `AplicarAsync`,
  `ReversarAsync` y `ResolverSaldoAFavorAsync` exigen una transacción en curso: se agrega
  `TransaccionExplicita.HayEnCurso(db)` y, sin transacción, responden `Lending.Contract.TransactionRequired`.
- Nunca abren ni confirman una transacción propia. Pueden guardar dentro (por ejemplo, para numerar), porque decide la de
  afuera.
- Bloquean con `ICerrojoDeCartera` en el orden fijo de T34 (`LND_Setup` compartido → `LND_DebtorControls` →
  `LND_LoanControls` por Id → numeración al final). No usan `ICerrojoPorClave`.
- Se llaman directo, nunca por `ISender` desde un comando reintentable.
- Como no pasan por el pipeline de MediatR, cada método de escritura ejecuta su validador FluentValidation (el mismo que
  tendría como comando) y escribe su evento de auditoría (R137). Lo fija `LosPuertosDeCarteraValidanYAuditanSusEscrituras`;
  la desviación de los Principios III y X va a `Complexity Tracking` (T101).
- Idempotencia de la capa 2 (T21): clave de negocio de texto más `HuellaDeOperacion.Calcular`, con el único
  (`CollectorCode`, `IdempotencyKey`) de `LND_LoanOperations`. Misma clave y misma huella → `AlreadyApplied` con el
  comprobante original; otra huella → `Lending.Contract.KeyReused`. No son `IOperacionIdempotente` ni
  `IReintentableAnteConcurrencia`.
- `AplicarAsync` recibe un lote, con una respuesta por ítem, para los recaudadores sin interacción.
- El recaudador debe estar en el registro (`Lending.Contract.CollectorNotRegistered`), salvo `OwnLoanPayoff`, que es la
  propia Cartera dentro de su desembolso.
- El reverso de un pago de clase `Payroll` sólo lo admite `ReversarAsync` del mismo recaudador; `ReverseLoanPaymentCommand`
  lo rechaza con `Lending.Reversal.OwnedByCollector` (T31).

**Rationale**: I-4 y FR-086 lo piden así. La lección de `TransaccionDeLiquidacion` es que un reintento anidado vacía el
`ChangeTracker`; `RecaudoDeCredito` ya se llama directo. `COR_OperationKeys` usa un Guid, compara el usuario central y abre
su propia transacción: no sirve dentro de la del recaudador.

**Alternatives considered**
- Mensaje asíncrono de Nómina a Cartera: rompe el todo o nada de FR-088.
- Contextos o transacciones separados: con una base por cooperativa no hacen falta.
- Un savepoint propio de Cartera: `TransaccionExplicita` ya se une a la de afuera.
- `ICerrojoPorClave` movido a `Common/Persistence` (DE-02): dos mecanismos sobre el mismo crédito y el proceso no puede
  saltar filas ocupadas (CH19).
- `TareasTrasElCommit.AlTerminar(bool confirmado)` para el desenlace (DE-02): CH18, ver R137.

**Trazas**: T91, T34, T21, T101 · FR-003, FR-009, FR-046, FR-079, FR-080, FR-086–FR-088, I-4 · DE-02, M13, S9.

**Difiere del informe**: DE-02 movía `ICerrojoPorClave` a `Common/Persistence` y registraba el desenlace con
`TareasTrasElCommit.AlTerminar` (CH18, CH19).

**Conciliación**: C25 — el punto de guardado propio se descarta para el contrato, pero **sí** rige para la reserva
dentro de la confirmación de Comercial (R112). C41 — `Lending.Payments.BackdateBeforeClose` se evalúa en el contrato
por `IPermissionChecker` con los permisos y el límite del usuario que actúa, no su alcance. C37 — un pago
`Contributions` tampoco se reversa desde Cartera hasta que Aportes declare su destino. C39 — la forma no interactiva
espera el cerrojo y aplica sobre el estado nuevo. C40 — un mensaje no convertible queda `Pending` con motivo y
«para reproceso».

#### R103 · Nómina como recaudador: clave, fecha valor y pagador

**Decision**
- Recaudador `ClaseDeRecaudador.Payroll`, registrado por cooperativa en `COR_IntegrationParticipants` por la semilla
  idempotente de `PlataformaParaCartera` (clase `Payroll`, rol `ObligationsCollector`, adopta «ProveedorDeObligaciones» v1),
  activo con E-03.
- Clave de la aplicación: `NOM:{runPublicId:N}:{obligationPublicId:N}`, con `runPublicId` = la versión aprobada de la
  ordinaria o de la liquidación. El reverso usa la clave del pago (FR-085).
- Forma sin interacción (FR-073: nunca responde «cambió»), clase de medio `OwnPayrollDeduction` (FR-082).
- Pagador: el empleado deudor. Recibo (`ReferenciaDeRecibo`): el NM o la liquidación.
- Fecha valor: en la ordinaria, el fin del período; en la definitiva, la fecha de aprobación. Las dos quedan exentas del
  límite `Pagos.DiasMaximosFechaValor` (D4) y de la atribución antes de un cierre (enmienda A9).
- El orden de cerrojos lo pone el contrato (Id ascendente); Nómina no ordena el lote por su cuenta.

**Rationale**: la clave sale literal de FR-088 y FR-112 con el código de módulo de `ModuloContable` (`NOM`). Con el empleado
como pagador, el hecho contable queda con el deudor como tercero. La fecha de aprobación es el día en que se retiene el
dinero y evita chocar con D4 cuando se aprueba semanas después del retiro.

**Alternatives considered**
- Fecha valor = fecha de retiro, como hoy (`ApproveSettlementCommand.cs` l. 144): choca con D4 si se aprueba tarde.
- La cooperativa como pagadora: confunde el tercero contable con el de una pagaduría externa.
- `PAY:{runPublicId:N}:…` (DE-03) y `Payroll:{runVersionPublicId:N}:{loanPublicId:N}` (PM-12): `PAY` es el prefijo de
  tablas, no el código de módulo (CH17).
- Registrar a Nómina al arrancar la API, como los permisos (DE-03): el registro lo leen el despachador y la activación con la
  versión del esquema; el precedente es la migración (CH16).

**Trazas**: T92, T21, T16 · FR-012, FR-073, FR-082, FR-085, FR-087, FR-088, FR-112, D4 · DE-03 (CH16, CH17).

**Difiere del informe**: DE-03 usaba el prefijo `PAY:`, sembraba el recaudador al arrancar la API y nombraba en
español la clase y el medio (`Nomina`, `DescuentoNominaPropia`) (CH16, CH17, CH40).

#### R104 · Liquidación definitiva: propuesta por la consulta y aprobación todo o nada

**Decision**
- **Propuesta.** `SettlementInputLoader` deja de leer `LoanPortfolios`. La parte de préstamos pasa a
  `PropuestaDeDescuentosDeCartera`, que llama `IConsultaDeObligaciones.ObligacionesAsync` por la persona del empleado, sin
  codeudas. Propone toda obligación viva como deudor, castigadas incluidas y de cualquier forma de pago (G4):
  `SaldoTotal` → total para cancelar; `SoloCuotasCausadas` → exigible; `NoProponer` → nada.
- El desglose JSON conserva sus claves (`capital`, `intereses`, `mora`, `cuotasPendientes`, `cuota`, `saldoTotal`, `modo`):
  `SettlementDeductionsReader` no cambia.
- `DeudaPropuesta` cambia `LoanPortfolioId` (int) por `ObligationPublicId` (Guid) y `ObligationNumber`; `Coincide` y
  `Conservar` comparan por `ObligationPublicId`. Una excepción o un vencimiento del tiempo sigue dando el aviso
  `Payroll.Settlement.PortfolioUnavailable`.
- **Aprobación.** `SettlementRunWorkflow.ApproveAsync` gana el gancho `antesDeContabilizar` (hoy el comprobante se arma
  antes de `antesDeGuardar`). En él, `ApproveSettlementCommand`:
  1. vuelve a consultar; si el conjunto vivo difiere del propuesto responde `Payroll.Settlement.LoanProposalOutdated` y hay
     que recalcular;
  2. aplica el lote por `IContratoDeObligaciones.AplicarAsync` con la modalidad que corresponde: `FullPayoff` (`SaldoTotal`
     sin ajuste), `CycleInstallment` (`SoloCuotasCausadas`), `FreeAmount` (descuento ajustado);
  3. si un ítem sale `Rejected` o lo aplicado no coincide con `AppliedAmount` (hubo excedente), responde
     `Payroll.Settlement.LoanAmountsChanged` con los valores de Cartera y se revierte todo (FR-088).
- `CarteraTransactionPublicId` conserva su nombre y guarda el comprobante de aplicación (el `PublicId` de la
  `LoanOperation`); `RemainingBalanceAfter`, el saldo que queda.
- Se retiran `RecaudoDeCredito`, `CuentaDebitoDelDescuentoAsync` y `FormaDePagoNomina`.
- La pantalla explica que aprobar otro día exige recalcular y ofrece el recálculo en un clic.

**Rationale**: FR-088 pide proponer según la política y aplicar todo o nada. Aplicar antes de contabilizar garantiza que el
comprobante y el recaudo dicen lo mismo. Volver a consultar cierra el hueco de una propuesta hecha con Cartera caída o
antes de activar.

**Alternatives considered**
- Aplicar después del comprobante y devolver el excedente con una línea nueva: el comprobante ya está armado.
- Tolerar diferencias de unos pesos: no hace falta si se recalcula el mismo día.

**Trazas**: T92 · FR-061, FR-071, FR-073, FR-088, FR-142, FR-147; G4 · DE-04.

**Difiere del informe**: DE-04 nombraba las modalidades `Cancelacion`, `CuotaDelCiclo` y `ValorLibre` (CH40).

#### R105 · Reverso de la definitiva y ajuste con el saldo exacto

**Decision**
- `ReverseSettlementCommandHandler`, en el mismo gancho y transacción, llama `ReversarAsync` con la clave del pago y el
  motivo, por cada descuento aplicado con comprobante de Cartera. Si Cartera rechaza uno, se revierte la reversión entera y
  se muestra el motivo. `SettlementDeduction` gana `CarteraReversalPublicId`. Cambian el mensaje de
  `SettlementReversedWithPortfolioDto` y la pantalla (`LiquidacionDefinitiva.razor` l. 115, 146, 373).
- En Cartera, un usuario no reversa un pago de clase `Payroll` (`Lending.Reversal.OwnedByCollector`).
- `AdjustSettlementDeductionCommand` llama `SimularAsync` (no escribe) con el valor aplicado y guarda el saldo que
  quedaría; la pantalla lo muestra en lugar del «aproximado» (l. 320). El ajuste sigue sin crear versión.

**Rationale**: FR-088 («reversar la liquidación reversa sus recaudos») y FR-080 («un pago de Nómina sólo lo reversa
Nómina»); la prelación la sella cada crédito (FR-072), no Nómina.

**Alternatives considered**: dejar la reversión manual en Cartera, como hoy (contraría FR-080); mantener el cálculo
aproximado.

**Trazas**: T92, T31 · FR-072, FR-080, FR-081, FR-085, FR-088, SC-008 · DE-05, DE-06.

#### R106 · Nómina ordinaria como pagaduría interna

**Decision**
1. `CalculationInputLoader` cambia la lectura de `PayrollDeductionEntries` tipo DN por
   `CuotasDeNominaAsync(personas, inicio, fin)`. Cada cuota alimenta una novedad de origen `LoanDeduction`, una por
   obligación, con `PAY_Novelties.ObligationPublicId` y `ObligationNumber`, concepto `DESC_CARTERA` (T92).
2. El motor deja de saltarse el tope: la cuota de Cartera va primera entre las deducciones autorizadas, después de las de
   ley, sujeta a `MAX_DEDUCCION_SALARIO_PCT` (G5). Lo que no cabe no se difiere: queda no descontado con motivo
   `DeductionCap` o `InsufficientNet`. Casos dorados nuevos.
3. `CalculatePayrollRunCommand` guarda una fila `PAY_PayrollLoanDeductions` por empleado y obligación: esperado,
   descontado, no descontado, motivo y la línea enlazada; estado `PayrollLoanDeductionStatus {Calculated, Applied,
   PartiallyApplied, NotApplied, Rejected, Reversed}`.
4. `ApprovePayrollRunCommand` pasa a `TransaccionExplicita` y, antes de armar el NM, aplica el lote por el contrato con lo
   descontado y los `NoDescontado` en la misma llamada. Cartera guarda cada `NoDescontado` en
   `LND_InstallmentNonDeductions` (origen `Contract`), la cuota sigue exigible y se alerta `Cartera.CuotaNoDescontadaPorNomina`.
5. Un ítem `Rejected` o con excedente **no bloquea**: la línea `DESC_CARTERA` baja a lo aplicado, el neto sube por la
   diferencia y la fila y la explicación guardan el motivo (aviso `Payroll.LoanDeductionNotApplied`). La vista previa de
   aprobación simula el lote (G2).
6. `ReversePayrollRunCommand` reversa por el contrato, todo o nada (G3).
7. Ruta `GET /api/payroll/runs/{runId}/loan-deductions` (`Payroll.Runs.View`).
8. `ProcessPayrollDeductionCommand` y `PayrollDeductionEntries` se retiran con la cartera heredada.

**Rationale**: FR-112 es literal: el rechazo no impide aprobar, el excedente vuelve al empleado y lo no descontado se informa
en la misma llamada. Hoy la línea no guarda la obligación y hace falta para recaudar, reversar e informar. Aplicar antes del
NM deja el comprobante igual al recaudo.

**Alternatives considered**
- Marcar el borrador `Stale` y obligar a recalcular: contradice «no impide aprobar».
- Guardar el resultado en columnas de `PAY_PayrollRunLines`: mezcla el contrato con la tabla grande de líneas.
- Diferir lo no descontado como las demás novedades: cobraría dos veces, porque la cuota sigue exigible en Cartera.
- La cuota como `NoveltyInput` sin guardar (DE-07): sin novedad no hay dónde poner la obligación de la línea (T92).

**Trazas**: T92 · FR-073, FR-087, FR-112, FR-115, FR-142, FR-147, US10-6; G2, G3, G5 · DE-07.

**Difiere del informe**: DE-07 pasaba la cuota como `NoveltyInput` sin guardar y usaba los motivos `TopeDeDescuentos`
y `NetoInsuficiente` (T92, CH40).

**Conciliación**: C55 — hay una `PAY_Novelties` `LoanDeduction` por empleado, obligación y período; el recálculo la
reemplaza y el descarte del borrador la anula (patrón del materializador de recurrentes de la 006). C32 — en K1 la
cuota es la del período; lo no descontado sigue exigible, alertado y en cobro por caja; desde K2 decide el convenio
de la nómina propia. La ordinaria aplica `CycleInstallment` por lo descontado y reporta `NoDescontado`. C56 —
`MotivoDeNoDescuento` suma `DeductionPriority`. §7.6 — sin cuentas en `DESC_CARTERA`, la ordinaria responde con el
código existente `Payroll.ConceptWithoutAccounts`.

#### R107 · `DESC_CARTERA` contra el puente (enmienda D-08 de la 010)

**Decision**
- `DESC_CARTERA` vuelve a contabilizar en el NM y en la definitiva (`AffectsAccounting = true`). Se retiran las excepciones
  de `ICalculationRule.cs` l. 136, `ProposedDeductionsRule.cs` l. 35-38 y `AdjustSettlementDeductionCommand.cs` l. 92, y los
  contabilizadores dejan de omitir esas líneas.
- El crédito va a la cuenta parametrizada del concepto, que es el puente «recaudos por nómina para Cartera». El hecho
  `PagoAplicado` con `CollectorClass = Payroll` mueve el rol `RecaudoPorAplicar` de la matriz de Cartera (T53, R19).
- La activación exige resuelta la regla `RecaudoPorAplicar` con `CollectorClass = Payroll` y avisa si `DESC_CARTERA` no tiene
  cuentas (la pantalla lo pregunta por `GET /api/payroll/concept-definitions/{code}/accounts`, porque el comando no lee
  `PAY_`); sin cuentas, la primera nómina con descuentos de Cartera no se aprueba, con los códigos que **ya existen**
  (§7.6 de las transversales): `Payroll.Settlement.ConceptAccountsMissing` (`SettlementErrors`) en la definitiva y
  `Payroll.ConceptWithoutAccounts` en la ordinaria. No hay código nuevo; §2.17 los lista como reutilizados.

**Rationale**: FR-126 prohíbe que el hecho lleve cuentas y hoy Nómina le pasa una (`ApproveSettlementCommand.cs` l. 131,
147). Así cada comprobante cuadra solo y el saldo del puente es lo pendiente de pasar (FR-130), como los puentes de la 012.

**Alternatives considered**: mantener D-08 y que la matriz de Cartera debite directamente «salarios por pagar» por clase de
medio: la cuenta de nómina quedaría en dos matrices y el pasivo con el empleado inflado mientras el hecho no pase.

**Trazas**: T53, T84 · FR-125, FR-126, FR-129, FR-130, FR-146, FR-147; C1 · DE-08, DC-07.

#### R108 · Columnas de Nómina y migración `NominaRecaudaPorElContrato`

**Decision**
- Migración par **aditiva** `NominaRecaudaPorElContrato`, antes del retiro:
  - crea `PAY_PayrollLoanDeductions`;
  - agrega `PAY_PayrollRuns.LendingActivationAckBy` y `LendingActivationAckAt`;
  - agrega `PAY_SettlementDeductions.ObligationPublicId` (uuid / uniqueidentifier, nulo), `ObligationNumber` y
    `CarteraReversalPublicId`, y `PAY_Novelties.ObligationPublicId` y `ObligationNumber`;
  - copia de forma idempotente `ObligationPublicId = LND_LoanPortfolios.PublicId` y `ObligationNumber = PortfolioNumber`
    en las dos tablas (historia, sin FK);
  - crea `UK_PAY_SettlementDeductions_Termination_Obligation` (`TerminationId`, `ObligationPublicId`), filtrado a no nulo y
    a filas vivas, con `HasDatabaseName`.
- La parte **destructiva** va en `RetiroDeLaCarteraHeredada` (R122): suelta
  `FK_PAY_SettlementDeductions_LND_LoanPortfolios_LoanPortfolioId`, `IX_PAY_SettlementDeductions_LoanPortfolioId`,
  `UK_PAY_SettlementDeductions_Termination_Loan` y las columnas `LoanPortfolioId` de `PAY_SettlementDeductions` y de
  `PAY_Novelties` (en SQL Server, el índice antes que la columna).
- Después, ninguna configuración `PAY_` referencia `Domain.Entities.Lending`.

**Rationale**: FR-008 pide que ninguna tabla de otro módulo apunte a Cartera (como `SuggestedCreditLineCode` en la 012); un Id
interno de otro módulo en `PAY_` viola el Principio VI; el retiro no corre con una FK `Restrict` desde `PAY_`. Copiar el
PublicId conserva la historia de las definitivas aprobadas con crédito heredado, y D10 descarta migrar datos.

**Alternatives considered**
- FK a la tabla de obligaciones nueva: acopla esquemas y obliga a migrar lo heredado.
- Conservar el entero sin FK: Id interno de otro módulo.
- Una segunda migración destructiva de Nómina que copia y suelta (DE-09), o soltar todo en el retiro sin copia (R-02):
  CH37.
- Nombrar la tabla `PAY_PayrollNovelties` (DE-09): la real es `PAY_Novelties` (CH37).

**Trazas**: T87, T92, §2.26 · FR-008, FR-144, FR-147; Principios VI, XII; G10 · DE-09, R-02 (CH37).

**Difiere del informe**: DE-09 hacía también lo destructivo en `NominaRecaudaPorElContrato`, borraba `LoanPortfolioId`
de novedades sin copia y nombraba la tabla `PAY_PayrollNovelties`; R-02 lo hacía todo en el retiro, sin copia (CH37).

**Conciliación**: C73 — la copia de `PublicId` y número no es semilla sino transformación idempotente de datos
existentes, necesaria antes del retiro; el Constitution Check de `plan.md` lo declara. La copia en `PAY_Novelties` se
conserva por simetría aunque no debería encontrar filas.

#### R109 · «Cartera por activar» en las dos aprobaciones

**Decision**
- Las dos consultas de `IConsultaDeObligaciones` dicen, por persona, si está «migrada, sin activar»
  (`ObligacionesDePersonaDto.MigratedNotActivated`, C26).
- Al calcular, Nómina deja el aviso `Payroll.LendingPendingActivation`.
- `ApprovePayrollRunCommand` y `ApproveSettlementCommand` ganan `AcknowledgeLendingPendingActivation`; sin él responden
  `Payroll.LendingPendingActivationAckRequired` con los nombres, como ya hace `ConfirmWithoutSegregation`. Con él, queda en
  `PAY_PayrollRuns.LendingActivationAckBy`/`LendingActivationAckAt` (la definitiva también es una corrida) y en la auditoría.
- Antes de activar no hay obligaciones exigibles: las dos nóminas se aprueban sin descuentos de Cartera. Al activar, las
  cuotas de nómina propia vencidas en el intervalo quedan en `LND_InstallmentNonDeductions` con origen `Activation` y
  motivo «Cartera sin activar» (T84).

**Rationale**: FR-142 y FR-147 piden reconocer, no bloquear, con el patrón de confirmación expresa que ya existe.

**Alternatives considered**: bloquear la aprobación.

**Trazas**: T85, T84 · FR-142, FR-147 · DE-10.

#### R110 · Personas, retiro de asociado, extractos y portafolio por la consulta

**Decision**
- `DeletePersonCommand` cambia `LoanPortfolios` (l. 35-40) por `ObligacionesAsync` con codeudas: bloquea si la persona
  tiene obligaciones vivas como deudor **o codeudor**, o está «migrada, sin activar». Conserva `Person.HasActiveLoans` con
  un mensaje nuevo. Se agrega la prueba que hoy falta.
- `GetPersonDetailQuery` cambia las tres consultas (l. 146-170) por la misma llamada; `PortfolioSummaryDto` conserva su
  forma y suma `LendingStatus`. El resumen de ahorros sigue en `SavingsAccounts`.
- `WithdrawalQueries` (l. 68) y `ProcessWithdrawalCommand` (l. 46) pasan a la consulta en K1: son el consumidor «Aportes,
  en el retiro» de FR-061 (A5).
- Los extractos (`Statements`) y `PersonPortfolioReportQuery` pierden la parte de crédito: los documentos al deudor traen el
  estado del crédito (R149).

**Rationale**: FR-148 y FR-061; si no se adaptan, no compilan al retirar las tablas (R121).

**Alternatives considered**: un código de error nuevo para el borrado (nadie lo consume); enmendar la lista cerrada para que
extractos y portafolio sigan viendo créditos.

**Trazas**: T94, T88 · FR-061, FR-142, FR-144, FR-148; A5 · DE-11, R-03.

**Difiere del informe**: R-03 quitaba la parte de crédito a extractos y portafolio sin someterlo; ahora es la pregunta
A5.

#### R111 · Comercial: consultas reales antes y después de activar

**Decision**
- Antes de activar, `ConsultasDeCartera` responde `NotEnabled` (`CarteraNoHabilitada`) por cooperativa y rige el crédito
  provisional.
- Activada, `CreditoEnLaVenta` aplica la respuesta (T662 de la 012):

  | Respuesta | Efecto |
  |---|---|
  | Bloqueado, o en mora sin permitir la venta | `Inventory.Credit.LendingBlocked` |
  | Cupo insuficiente | `Inventory.Credit.QuotaExceeded` con `availableQuota` |
  | Sin respuesta en `Cartera.ConsultaSegundos` (token de cancelación enlazado) | `Cartera.PoliticaSinRespuesta` por tipo de tercero: `Bloquear` → `Inventory.Credit.LendingNoResponse`; `PermitirConAprobacion` → origen `LendingNoResponse`, aprobación `ProvisionalCredit` y `PendingValidation` |
  | Validada | origen `Validated`, `PendingValidation = false`, sin aprobación provisional, `ConsultationEvidence` = `EvidenceId` |

- `EvaluateSaleCreditQuery` devuelve el origen real (hoy fijo en l. 110).
- `INV_DocumentPayments.CreditLineCode`: la línea elegida, por defecto la sugerida del medio.
- `Cartera.CuentaPorCobrarRegistradaPor` pasa su defecto a «Cartera» (D16), forzado a «Contabilidad» mientras Comercial no
  fije su fecha de habilitación.
- Se enmienda FR-062 de la 012 (Comercial guarda número y estado del crédito, nunca saldos ni cuotas) y se cierran sus
  tareas IC T661–T667, la rama Cartera de T886 y T1026–T1027.

**Rationale**: FR-059, FR-142, FR-145 y D16.

**Alternatives considered**: responder «Cartera no responde» antes de activar (FR-142 lo prohíbe).

**Trazas**: T95 · FR-049, FR-051, FR-056, FR-059, FR-060, FR-142, FR-145, D16 · DE-12.

#### R112 · Comercial: reserva y liberación del número de crédito

**Decision**
- `CreditoEnLaVenta` gana `ReservarAsync`, que llama `IReservasDeCartera` **dentro** de la unidad de trabajo de la
  confirmación, también si la venta queda en aprobación, sin guardar por su cuenta, bajo un **punto de guardado propio**
  que responde `NoResponse` ante una falla técnica (D26), en los dos motores (C25). Orden global de cerrojos (C24): filas
  de Inventario → `ReservarAsync` (`LND_Setup` compartido → `LND_DebtorControls` → numerador de Cartera) → numerador
  de Inventario; ninguna operación de Cartera toma cerrojos de Inventario.
- Clave `INV:{paymentPublicId:N}:{monto}`. Repetirla devuelve el mismo número; si el monto cambió, Comercial libera la
  reserva y pide otra con la clave nueva (G9). Los números no tienen que ser continuos (FR-057).
- Cada método ejecuta su validador y escribe `Lending.NumberReservation.Reserved` / `.Released` en `COR_AuditOutbox` dentro
  de la transacción (R137).
- El número queda en `INV_DocumentPayments.ReservedLoanNumber` y la reserva en `INV_DocumentPayments.ReservationPublicId`
  (fijado en la sección 13): no cambian tras confirmar. `FuenteDeEmisionDeInventario` (l. 137-139) pone el número como
  referencia del pago de crédito, así la factura lo lleva.
- `DiscardInventoryDraftCommand` y `DiscardPosDraftCommand` llaman `LiberarAsync`.
- `NotEnabled` o sin respuesta: la venta sigue sin número (D26).
- En Cartera, `LND_LoanNumberReservations` con estados `Reserved`, `Used`, `Released`, `Expired`. La tarea
  `cartera.reservas-vencidas` vence las que no se confirmaron ni liberaron (`Cartera.ReservaVencida`) y nunca las reasigna;
  si después llega la v2 con ese número, lo toma (`Expired` → `Used`) con alerta informativa. La reserva lleva el valor y
  descuenta cupo; no hay tabla aparte de reservas de cupo. No pasa por la validación previa contable (no produce hecho).

**Rationale**: FR-057, C2, D3 y D26. Dentro de la confirmación no quedan reservas huérfanas y la factura sale con el número.

**Alternatives considered**
- Reservar desde la pantalla al elegir el medio: deja reservas huérfanas.
- Reservar después de confirmar: la factura ya se firmó.
- La clave sin monto: un cambio de monto quedaría como `KeyReused` sin salida (CH11).
- `IReservasDeCredito`, `ReservedCreditNumber` y `CreditLineCode` en la v2 (DE-13): CH11.
- `LND_CreditLimitReservations` aparte (R-06): CH29.

**Trazas**: T22, T21, T34 · FR-009, FR-057, FR-058, FR-145, D3, D26, SC-008, SC-010; G6, G9 · DE-13, PM-10 (CH11,
CH29).

**Difiere del informe**: DE-13 usaba `IReservasDeCredito` y la columna `ReservedCreditNumber`; PM-10 no ponía el monto
en la clave (CH11).

**Conciliación**: C24 y C25 — orden de cerrojos y punto de guardado de arriba; se mide en la prueba de volumen del
POS. C67 — `ComercioConCartera` agrega a `INV_DocumentPayments` `ReservationPublicId` **y** `ConsultationEvidence`.
C31 — la consulta de cupo verifica listas sin escribir; el resultado se guarda en la reserva y al consumir la venta.
C20 — una reserva desconocida en una v2 no rechaza: la obligación nace con otro número (`numberSource = Assigned`);
`Lending.Reservation.NotFound` queda para las rutas de reserva.

#### R113 · Venta a crédito v2 y respuestas de Cartera a Comercial

**Decision**
- `VentaACreditoRegistradaV2` = v1 + `ReservedLoanNumber` (string?) + `ReservationPublicId` (Guid?); `CreditLineCode` ya
  existe en la v1 y llega lleno cuando Cartera respondió. Es de Comercial: su record va en `Contracts/Inventory` y entra en
  `CatalogoDeMensajesV1` (`OriginModule = INV`, `AdmiteProceso = false`). Los bytes de los contratos existentes no cambian
  (`ContratosDeMensajesTests`). Comercial emite sólo v2; Cartera acepta v1 y v2 mientras quede alguna entrega v1;
  `AjusteDeVentaACredito` sigue en v1.
- Cartera responde con `ObligacionCreada` (número, línea, valor, resumen del plan, estado `Validated`, `ValidationFailed`,
  `Migrated` o `CancelledBeforeCutoff`) y `ObligacionRechazada` (código, motivo —`SinObligacion` para el ajuste de una venta
  aceptada sin migrar—, reprocesable o no), al destino `Inventory`, con la venta como raíz de cadena (`CadenaDeOrden`); una
  `ObligacionRechazada` por evaluación (`Confirmation:{evaluationPublicId:N}`).
- `DestinoInventario` (`Application/Inventory/Integration`) escribe `INV_LendingObligationResponses` (UK `MessagePublicId`):
  número y estado, nunca saldos ni cuotas. `GetSalesDocumentCreditQuery` muestra número y estado.
- Una evaluación negativa al consumir usa la variante nueva `ResultadoDeConsumo.ValidationFailed(ref, número, código,
  motivo)`, que `RegisterDeliveryResultCommand` lleva a `DeliveryStatus.ValidationFailed` y con la que levanta
  `Integracion.ValidacionFallida` (hoy sin escritor). Confirmar por excepción cambia el recibo de Cartera, no la entrega (B8).

**Rationale**: FR-052, FR-053, FR-055, FR-145 y SC-005; el número viaja sin que Comercial lea a Cartera.

**Alternatives considered**
- Que el programador consulte el estado por sondeo (T664 de la 012): agrega latencia y carga cuando la respuesta ya llega
  como mensaje.
- Destino `Commercial` y consumidor `DestinoComercial` (DE-14): el módulo, los permisos y la clase de participante de
  Comercial son `Inventory` (CH9).
- `ProcessedWithFailedValidation` (DE-14): CH12.

**Trazas**: T18, T17, T15 · FR-052, FR-053, FR-055, FR-143, FR-145, FR-149, SC-005; B8 · DE-14, PM-09, PM-10 (CH9,
CH11, CH12).

**Difiere del informe**: DE-14 usaba el destino `Commercial`, el consumidor `DestinoComercial`, la variante
`ProcessedWithFailedValidation` y ponía `CreditLineCode` en la v2 (CH9, CH11, CH12).

**Conciliación**: C16 — `DestinoCartera.Acepta` admite toda versión de la familia de la venta y, si la versión no se
acepta, el consumidor responde `ObligacionRechazada` (enmienda a la 012 §12, lista de T97). C17 — `ObligacionCreada`
también se envía al consumir un `AjusteDeVentaACredito` `Replacement`. C18 — `ValidationFailed` puede llegar directo
desde `Pending`/`InBatch` (enmienda a la tabla de estados de la 012). C40 — un mensaje no convertible queda
`Pending` con motivo y «para reproceso». C63 — una venta fechada hasta el corte que llega después de activar queda
`NotIncorporated` con `ObligacionRechazada` no reprocesable (`Lending.Sale.NotIncorporated`) y alerta (pregunta I16).
C21 — `MotivoSinComprobante.NotIncorporated` es aditivo en la 012.

#### R114 · El destino `Lending` se habilita por el registro

**Decision**
- `DestinosHabilitadosAsync` del despachador deja de leer `ParametrosDeInventario.CarteraIntegracionHabilitadaDesde`: un
  destino se recorre si tiene consumidor en el despliegue y fila activa a hoy en `COR_IntegrationParticipants`
  (`IRegistroDeIntegracion`).
- `GetSalesDocumentCreditQuery` deja `destinos.Any(Lending)` (`SalesQueries.cs` l. 595) y calcula «disponible» con el mismo
  registro, por cooperativa.
- La fila del destino `Lending` nace inactiva y la activa `ActivateLendingCommand` en el mismo guardado
  (`EscritorDelRegistroDeIntegracion`). Antes, los mensajes de Comercial se acumulan sin intentos ni alertas, y
  `Ventas/CreditoProvisionalTests` sigue verde.
- El parámetro de Comercial sigue sirviendo a Comercial (sello de la cuenta por cobrar, crédito provisional); la pantalla de
  activación muestra que debe igualarlo.

**Rationale**: FR-142 dice que activar Cartera habilita la recepción; Cartera no escribe parámetros de Comercial; quita a la
plataforma la dependencia de `Domain.Inventory.Parameters`.

**Alternatives considered**: exigir el parámetro de Comercial **y** el registro (DE-15): CH13; que `DestinoCartera` responda
`Retry` antes de activar (genera intentos y alertas).

**Trazas**: T16 · FR-142, FR-149 · DE-15, PM-08 (CH13).

**Difiere del informe**: DE-15 exigía el parámetro de Comercial además del registro (CH13).

**Conciliación**: C36 — el registro suma `Destination` e `IsEnabled`, con único `(Code, Role, Destination)`;
`RegisterIntegrationParticipantCommand` lleva `destination`; `Lending` se siembra inactivo. C60 —
`ActivateLendingCommand` abre una **transacción explícita** y guarda por **tandas de 1.000 créditos** con
`ChangeTracker.Clear()`; la habilitación del destino `Lending` va en esa misma transacción (no en un único guardado).
Prueba de volumen con 50.000 créditos bajo `RUN_PERF_TESTS`. C61 — la activación exige acta
(`LND_Activations.SessionItemId` no nulo).

#### R115 · Ventas a crédito sin Cartera: `CreditWithoutLending`

**Decision** (sujeta a la enmienda A6)
- `COR_PaymentMeans.CreditWithoutLending`, sólo en clases de crédito; como la clase, no cambia una vez el medio tiene pagos.
  Se copia a `INV_DocumentPayments.CreditWithoutLending`.
- Con él no se consulta ni se reserva en Cartera, el sello es «Contabilidad» y la cadena no emite `VentaACreditoRegistrada`
  ni `AjusteDeVentaACredito`. Se conserva lo demás del crédito: vencimientos, forma de pago crédito para la DIAN, aprobación
  `ProvisionalCredit` y regla `MedioDePago` con tercero y cruce `FV`. La conciliación de FR-143 no ve esas ventas.
- Las ventas de servicios confirmadas **antes** del dato (I1–I6 en producción desde el 2026-10-01) ya emitieron la v1 hacia
  Lending: en la conciliación de FR-143 se marcan, venta por venta, con motivo y punto de acta (D17), como «aceptadas fuera de
  Cartera (D29)», una clase propia de `LND_MigrationLoadDifferences`; no bloquean la activación y al activar responden
  `ObligacionRechazada` no reprocesable (G11, R152).
- Las dos columnas van en la migración `ComercioConCartera`. Si el dueño rechaza A6, salen de ella y el dato queda como
  precondición de activación: un ajuste de la 012.

**Rationale**: D29 y Q3: los servicios vendidos a crédito quedan en Contabilidad y no llegan a Cartera. Sin el dato, al activar
cada uno nacería como obligación o bloquearía la activación como venta no migrada con saldo.

**Alternatives considered**
- Una clase nueva en `PaymentMeansClass`: toca `ClasesDeMedio`, la DIAN, el arqueo, la importación y la matriz.
- La clase `Other`: la factura saldría de contado y la cuenta por cobrar sin tercero ni cruce.
- Dejarlo fuera de la feature, como dice hoy «Dependencias»: deja expuesta la activación de COOFLOPAL.

**Trazas**: T95, T83 · FR-050, FR-143, FR-145, D29, Q3; A6, G11 · DE-16.

#### R116 · SMMLV, UVT y festivos de Nómina como proyección de la réplica

**Decision** (K1, sujeta a la enmienda A7)
- Nómina implementa `EfectoDeReplicacionEnNomina`: cuando la réplica trae `SMMLV`, `UVT` o `FESTIVO`, escribe en la misma
  transacción su propia proyección (fila de `PAY_LegalParameters` con fuente «Valor nacional {SourcePublicId}»; fila de
  `PAY_Holidays` con `HolidayOrigin.National = 6`), con cuatro reglas:
  1. **Adopta lo que existe.** Misma clave (`Code`/`ValidFrom`, o la fecha) y mismo valor → se marca como proyección; valor
     distinto → se conserva la de Nómina y se alerta `Nomina.ValorNacionalDistinto`. Sin esto, los únicos
     `UK_PAY_LegalParameters_Code_ValidFrom` y `UK_PAY_Holidays_Date` harían fallar la réplica y el alta de una cooperativa.
  2. **Las semillas dejan de sembrarlo**: `PayrollLegalParametersSeeder` (Order 71) pierde SMMLV y UVT; `HolidaysSeeder`
     (Order 74) pierde el generador Ley 51 y se retira; `FestivosLey51` pasa al seeder de la central; en una cooperativa
     nueva los escribe `NationalValuesReplicaSeeder` (Order 88).
  3. **Cierra la vigencia anterior** como `AddLegalParameterVersionCommand`.
  4. **Sólo la réplica escribe**: `AddLegalParameterVersionCommand` rechaza `SMMLV` y `UVT`
     (`Payroll.LegalParameter.NationalValue`) y los comandos de festivos no editan los `National`.
- Los festivos `Manual` siguen siendo de Nómina y Cartera no los usa (E5). `LectorDeUvt` sigue siendo el único lector de la
  UVT y lee la proyección.
- Lo vigilan `SoloLaReplicaEscribeLosValoresNacionalesDeNomina` y la e2e `ReplicaSobreCooperativaSembradaTests`. Leer
  directo desde los cargadores (`ValoresNacionalesParaNomina`) queda como mejora posterior.

**Rationale**: una sola fuente (la central) sin tocar los lectores de `PAY_LegalParameters` ni los motores en producción; cada
tabla la escribe su dueño.

**Alternatives considered**
- Lector único `ValoresNacionalesParaNomina` en `CalculationInputLoader`, `SettlementInputLoader`, PILA, procedimiento 2 y
  `VacationQueries`, y retiro de las filas Ley 51 y decretadas (DE-18): más literal, regresión amplia sobre Nómina y PILA
  (CH14).
- Los festivos `Manual` como fila local dentro de la réplica nacional (DE-18): un día no laboral propio no es festivo del
  Código de Comercio (CH15).
- Sincronizar la réplica hacia `PAY_` sin reglas: dos fuentes.

**Trazas**: T93 · FR-015, FR-147, FR-149, C8; A7, B6, E5 · R6, DE-18 (CH14, CH15).

**Difiere del informe**: DE-18 proponía el lector `ValoresNacionalesParaNomina` en cinco cargadores y los festivos
«Manual» dentro de la réplica (CH14, CH15).

**Conciliación**: C68 — `PAY_Holidays` suma `NationalValuePublicId` para la proyección. §7.6 —
`ReplicaSobreCooperativaSembradaTests` va en la colección «Valores nacionales e2e» (la central es una por host).

#### R117 · Lista cerrada verificada: `LaCarteraSoloSeTocaPorSusInterfaces`

**Decision**: prueba de arquitectura nueva que fija:
1. `Application.Payroll`, `Application.Core.People`, `Application.Inventory`, `Domain.Entities.Payroll` y
   `Domain.Entities.Inventory` no dependen de los espacios de la Cartera nueva ni de sus `DbSet`, salvo
   `Application.Common.Integration.Lending` (ArchUnit y fuente).
2. Los cinco puertos que Cartera ofrece declaran exactamente sus métodos (2, 2, 2, 5 más `VersionDelContrato`, y los de
   `IDimensionesDeCartera`).
3. Fuera de Cartera, sólo los provisionales y los dobles unitarios implementan los puertos.
4. Ninguna entidad `PAY_` o `INV_` tiene navegación ni FK hacia `LND_`.

Se conserva `InventarioNoConoceContabilidadNiCartera` y su hecho `El_puerto_de_Cartera_declara_exactamente_sus_dos_consultas`
suma los dos métodos de `IReservasDeCartera`. Con la mudanza a `Savings` (R125), «Cartera nueva» = todo `*.Lending*`; si A10
se rechaza, la prueba nombra los espacios de la Cartera nueva y exime lo conservado por nombre.

**Rationale**: FR-008 y SC-018 piden una verificación automática; molde T31 de la 012.

**Alternatives considered**: una expresión regular por el segmento `Lending` sin mudanza: caería sobre los ahorros que
Personas lee.

**Trazas**: T90 · FR-008, SC-018 · DE-17.

**Difiere del informe**: DE-17 fijaba cuatro puertos; la armonización suma `IDimensionesDeCartera` (T90).

**Conciliación**: C34 — la prueba vigila **las dos direcciones**: nadie depende de Cartera salvo por
`Common.Integration.Lending`, y Cartera sólo depende de otros módulos por los puertos que consume. C27 — los cinco
puertos viven en `Common.Integration.Lending`.

#### R118 · Pruebas que se adaptan y se agregan por las enmiendas

**Decision**
- **Se adaptan**, con dobles unitarios de los puertos (`ConsultaDeObligacionesDePrueba`, `ContratoDeObligacionesDePrueba`,
  sólo en `tests/`): `SettlementInputLoaderTests` (l. 174, 351, 374); `ApproveSettlementCommandHandlerTests` (l. 33, 115,
  136, 155) y `DefinitivaDePrueba`, que deja `LoanPortfolio` y `RecaudoDeCredito`; `ReverseSettlementCommandHandlerTests`
  (reversa por el contrato); `RecalculateSettlementCommandHandlerTests` (l. 17, 60); `RegisterTerminationCommandHandlerTests`
  (l. 26, 78, 158, 171; la l. 212 simula Cartera caída con el doble); `CalculatePayrollRunCommandHandlerTests`
  (`Los_descuentos_de_cartera_entran_como_lineas_sin_asiento` pasa a «con asiento al puente»);
  `LosUnicosDeDescuentosSoloCuentanFilasVivasTests` (`ObligationPublicId`); `TestDbContextFactory`; `CasoDoradoLiquidacion`
  (`AccountedByOtherModule`); `EvaluateSaleCreditQueryTests`, `CreditoProvisionalTests`, `CreditoDePrueba`;
  `LiquidacionDefinitivaTests` (reversión); `AuditoriaDeComandosTests` (l. 69). Se retiran `CreateLoanApplicationValidatorTests`
  y `ProcessPaymentValidatorTests`.
- **Nuevas en Domain**: casos dorados de Nómina con la cuota de Cartera, tope y neto insuficiente.
- **Nuevas en Application**: todo o nada de la definitiva (rechazo, excedente, propuesta desactualizada); ordinaria con
  rechazo y excedente que no bloquean; reverso de las dos nóminas; reconocimiento de «Cartera por activar»; borrado de
  persona con deudor, codeudor y migrado; reserva y liberación; `DestinoInventario` idempotente; `CreditWithoutLending` no
  emite a Lending; contrato sin transacción → `Lending.Contract.TransactionRequired`.
- **Nuevas e2e** (colección «Cartera e2e», dos motores, cooperativas aisladas para lo que activa Cartera):
  `CarteraDesplegadaSinActivarTests` (definitiva, ordinaria, borrado de persona y venta a crédito),
  `NominaRecaudaPorElContratoTests` (incluye que un usuario de Cartera no reversa un pago de Nómina), `CreditoConCarteraTests`
  (T667 de la 012), `ReplicaSobreCooperativaSembradaTests`.
- **Arquitectura**: `LaCarteraSoloSeTocaPorSusInterfaces`, `LosPuertosDeCarteraValidanYAuditanSusEscrituras`,
  `SoloLaReplicaEscribeLosValoresNacionalesDeNomina`.

**Rationale**: FR-142 y SC-018; es lo que hoy está atado al acceso directo.

**Trazas**: §2.25 · FR-008, FR-088, FR-112, FR-142, FR-148, SC-018 · DE-19.

**Conciliación**: §7.6 — `ReplicaSobreCooperativaSembradaTests` (y `ValoresNacionalesYReplicaTests`) no van en
«Cartera e2e» sino en la colección «Valores nacionales e2e», porque la central es una por host; las baterías de
conformidad de adaptadores van en `Application.Tests` y `TextosDeCarteraTests` en `Architecture.Tests/Principles`.

### 8.3 Riesgos

| Riesgo | Mitigación |
|---|---|
| Nómina en producción cambia: motor de la ordinaria (tope y casos dorados), aprobación en `TransaccionExplicita`, `DESC_CARTERA` con asiento | Dobles unitarios de los puertos, casos dorados nuevos, regresión de «Nomina e2e» y e2e de «Cartera e2e» en cooperativas aisladas (R118) |
| Transacciones largas en nóminas grandes, con cerrojos sobre créditos que la caja quiere pagar | Orden fijo de cerrojos (T34); el proceso salta filas ocupadas; medir con `RUN_PERF_TESTS` |
| Un comando reintentable repite el contrato entero y duplica los intentos auditados | Es lo correcto; el desenlace se deriva por `OperationKey` (R137) |
| La reserva toma cerrojos de Cartera con el de inventario tomado | Orden fijo inventario → Cartera; medir en el POS; plan B de G6 (transacción propia con `Expired` sin reasignar) |
| Aprobar la definitiva otro día que el cálculo (el interés corre a diario) | `Payroll.Settlement.LoanProposalOutdated` y recálculo en un clic |
| Aprobar la ordinaria cambia el neto frente a lo calculado | Vista previa que simula el lote (G2) |
| Si el dueño rechaza A9, una ordinaria aprobada más de cinco días después del fin del período choca con D4 | Parametrizar el límite por clase de recaudador |
| Cartera rechaza el reverso de una definitiva (por ejemplo, por una novación posterior) | La reversión entera se revierte y muestra el motivo; FR-081 reconstruye con los pagos posteriores |
| Faltan las cuentas de `DESC_CARTERA` o la regla del puente | La activación exige la regla y avisa por las cuentas (T84); `Payroll.Settlement.ConceptAccountsMissing` |
| Los PublicId copiados a `PAY_` no corresponden a obligaciones nuevas | Son historia sin FK; el runbook declara lo que la copia no encuentre (Principio XII); el largo de `ObligationNumber` iguala el número de `LND_Loans` |
| Entregas v1 pendientes al desplegar la v2; ventas de servicios a crédito ya emitidas hacia Lending | Cartera acepta v1 y v2; conciliación «aceptadas fuera de Cartera (D29)» (G11) |
| Con «Cartera» por defecto en el sello, la regla `MedioDePago` de los créditos debe apuntar al puente | T665 de la 012, dentro de E-01 |
| Comercial ya está en producción (I1–I6): `Ventas/CreditoProvisionalTests` debe seguir verde mientras Cartera no se active | Habilitación por el registro de participantes (R114); e2e `CarteraDesplegadaSinActivarTests` |
| La API llama al ajuste una vez más (`SimularAsync`) | Dentro de los 2 s de SC-008 |
| Las enmiendas A5, A6, A7, A9 y A11 condicionan este frente; FR-147 y C8 dicen que Nómina «lee» los valores nacionales | Cada decisión trae su salida si el dueño rechaza la enmienda |
| La prueba de la lista cerrada depende de la mudanza a `Savings` | Sin A10, la prueba nombra los espacios de la Cartera nueva y exime lo conservado (R117) |

---

## 9. Frente del retiro de lo heredado

### 9.1 Hechos del código

**La cartera heredada**

- **Inventario.** 93 entidades en `Domain/Entities/Lending`, 93 configuraciones en `Configurations/Lending`, 93 `DbSet` en
  `ApplicationDbContext` (l. 162-259); `IApplicationDbContext` expone 34 (l. 102-136). Todas `LND_*` en `dbo`; cada
  `<summary>` nombra su tabla de origen. Sólo `CreditLineParameterConfiguration` (l. 41) y `LoanPortfolioConfiguration`
  (l. 52) declaran relaciones; las demás FK salen por convención de la navegación `CreditLine`.
- **Sólo en el modelo de cooperativa.** El snapshot administrativo no menciona `LND_`; el de cooperativa sí (base de cada
  cooperativa y `ingenia365erp`). La fila XII de la spec habla de la base administrativa: no aplica (A4).
- **Migraciones.** Las crea `InitialSchema` (PG `20260804192805`, SQL Server `20260804192755`). `IndicesDeLectura` agrega
  índices a `LND_DepositEntries` (conservada). `ContabilidadNiif` agrega `LND_SavingsParameters.InterestExpenseAccount`
  (conservada) y `LND_CreditLineParameters.ProvisionAccountCode`/`ProvisionExpenseAccountCode` (retirada).
- **`LND_Transactions` no es `LND_LoanTransactions`.** El Principio XI (constitución l. 394-400) nombra
  `LND_LoanTransactions`, que no existe. La tabla heredada `LND_Transactions` (`LendingTransaction`, `cop_movimto`) mezcla
  columnas de ahorro, CDAT y auxilios (`SavingsLineId`, `CdatAccrualDate`, `AuxiliaryApplicationId`); en el ERP sólo la
  usan créditos (`DisburseLoanCommand`, `RecaudoDeCredito`, `LoanPortfolioQueries`, `PaymentQueries`). Los ahorros
  escriben en `LND_DepositEntries`.
- **Handlers conservados.** Ahorros, depósitos e interés usan `SavingsAccounts`, `SavingsParameters`, `DepositEntries` y
  `Associates`; aportes, `ContributionReductions`; zonas, SIPLA, códigos de movimiento y estados de retiro, sólo su tabla.
  Ninguno toca `LND_Transactions`, `LND_Documents` ni `LND_CreditLineParameters`. Nadie escribe `LND_SiplaGroupParameters`
  (sólo su entidad, su configuración y el `DbSet`).
- **Rutas.** `Endpoints/Lending`: 27 archivos, 115 rutas, todas `.RequireAuthorization()` **sin permiso**. Crédito: 15
  archivos, 63 rutas (Accruals 2, Classifications 3, Defaults 3, HousingParameters 5, InterestRates 5, LoanApplications 7,
  LoanPortfolios 4, Payments 2, PayrollDeductionConcepts 5, PayrollDeductions 2, PeriodicityParameters 5,
  PortfolioAccounts 5, ProvisionParameters 5, ScoringParameters 5, TermRates 5). Conservados: 12 archivos, 52 rutas
  (Contributions 3, SavingsAccounts 7, SavingsInterest 2, SavingsParameters 5, SiplaParameters 5, Statements 2, SubZones 5,
  TransactionCodes 5, WithdrawalStatuses 5, Withdrawals 3, ZoneTypes 5, Zones 5). `Reports/LendingReportsEndpoints`: 6 rutas
  bajo `/api/reports/lending` (`aging`, `aging/pdf`, `person-portfolio/{id}` y su `/pdf`, `loan-statement/{id}/pdf` y
  `savings-statement/{id}/pdf`; las dos últimas responden «not yet implemented»). `LosEndpointsProtegidosExigenPermiso` no
  mira `Endpoints/Lending` (l. 27-48).
- **Pantallas.** `Pages/CarteraFinanciera`: 37 `.razor` (38 `@page`) y 19 `.razor.css`; más `Reportes/CarteraPorEdades` y
  `Reportes/ExtractoCredito`: 39. Tres llamadas a rutas inexistentes (`LineasCredito` y `CausacionIntereses` a
  `/api/lending/credit-line-parameters`; `ExtractoCredito` a `/api/reports/lending/loans-by-person`). `Desembolsos.razor`
  dice «Modulo en construccion» y no está en el menú. `NavMenu.razor` l. 68-104: grupo «Cartera Financiera», 32 enlaces y 2
  de reportes, sin `PermissionGate`; `CentroReportes.razor` l. 85-131: 5 tarjetas; `ManualCatalogo.cs`:
  `Modulos.Cartera = "Cartera Financiera"` (l. 38), temas l. 604-758, reportes l. 1771-1772.
- **Permisos legados.** `IdentitySeedData.cs` declara 22 tuplas `Lending.*` (l. 52-74) y el rol `LoanOfficer = "Lending.*"`
  (l. 161); alimenta tablas heredadas de ASP.NET Identity y «nadie lo ejecuta» para `SEC_Permissions`
  (`AccountingPermissionCatalogSeeder.cs` l. 16). En `SEC_Permissions` no hay ningún `Lending.*`.
- **Adaptador muerto.** `LendingLegacyAdapter`: `IsAvailable => false` y tres métodos que lanzan `NotImplementedException`;
  `AddLegacyServices` sin llamadas; ningún `.csproj` referencia `IngenIA365ERP.Legacy`; sólo `IngenIA365ERP.slnx` (l. 14) y
  la exclusión de `RepoPath.cs` (l. 34).
- **Contabilización heredada.** `DisburseLoanCommand` (l. 245-259) y `RecaudoDeCredito` (l. 176-205) contabilizan con
  `AccountingOrigin(ModuloContable.Cartera, …)` (`ACC_Documents.OriginModule = 'CAR'`). `AccrueInterest` (l. 115),
  `ClassifyPortfolio` (l. 146) y `ProcessPayrollDeduction` (l. 108) llevan el marcador «E3 (feature 009)».
  `voucher-types.json` (l. 4-11) siembra `DS`, `RC`, `CA`, `PV`, `DN` con módulo `CAR` y dice que los sembrados «no se
  eliminan ni cambian de uso».
- **Molde del retiro (012).** `RetiroDelInventarioHeredado` (PG `20260925164923`, SQL Server `20260925164847`): guarda
  (PG `DO $$ … RAISE EXCEPTION`; SQL Server cursor con `THROW 50012`) que cuenta filas, salta tablas inexistentes y se
  niega salvo la fila viva `COR_SystemSettings.SettingKey = 'INV.RetiroHeredado.Aprobado'`; suelta FK; borra tablas;
  `Down()` recrea vacías. Se completa con `docs/operaciones/inventario-retiro-heredado.md`,
  `specs/012-inventario-comercial/diagnostico-inventario-heredado.sql` y la e2e `RetiroDelInventarioHeredadoTests`.
  `PrincipioXII_MigracionesDestructivas` exige el marcador `MIGRACION-DESTRUCTIVA-APROBADA` en un `Up()` destructivo.
- **Pruebas atadas a lo heredado.** `TestDbContextFactory` (l. 204-237, 660-669), `SettlementInputLoaderTests` (l. 174,
  351, 374), `ApproveSettlementCommandHandlerTests` (l. 33, 115, 136, 155) y su fixture `DefinitivaDePrueba` (l. 40-125),
  `ReverseSettlementCommandHandlerTests` (l. 19), `RecalculateSettlementCommandHandlerTests` (l. 17, 60),
  `RegisterTerminationCommandHandlerTests` (l. 26, 78, 158, 171, 212), `CalculatePayrollRunCommandHandlerTests`
  (`Los_descuentos_de_cartera_entran_como_lineas_sin_asiento`, cerca de l. 160), `CreateLoanApplicationValidatorTests`,
  `ProcessPaymentValidatorTests`, `AuditoriaDeComandosTests` (l. 69), `LosUnicosDeDescuentosSoloCuentanFilasVivasTests`,
  `CasoDoradoLiquidacion` (Domain), `EvaluateSaleCreditQueryTests`, `CreditoProvisionalTests`, `CreditoDePrueba`; e2e
  `Payroll/LiquidacionDefinitivaTests` (l. 146, 189) y `Core/AltaDePersonaEnUnPasoTests` (l. 100).
  `PreferenciasHandlersTests` usa `/cartera/recaudos` como texto de ejemplo. `LasPantallasDicenQueEstanCargando` no incluye
  `CarteraFinanciera` (l. 27).

### 9.2 Clasificación tabla por tabla

Las 93 tablas `LND_` del modelo de cooperativa, comprobadas contra `Configurations/Lending` (93 `ToTable`) y contra el
snapshot de PostgreSQL. La columna **Origen** es la tabla del sistema anterior que nombra el `<summary>` de la entidad
(«—» si no la nombra). La columna **Llaves** dice a qué apunta cada tabla: «línea» = `LND_CreditLineParameters.CreditLineId`;
«crédito» = `LND_LoanPortfolios.LoanPortfolioId`. Esta clasificación es una hipótesis por nombre, contenido y llave: la
confirma el diagnóstico de cada ambiente (R123). `data-model.md` la publica con las mismas columnas (T86).

#### Se retiran (55, crédito)

| # | Tabla | Entidad | Origen | Grupo | Llaves |
|---|---|---|---|---|---|
| 1 | `LND_LoanPortfolios` | `LoanPortfolio` | `cop_maecar` | Créditos y movimientos | línea (Cascade); `COR_People.PersonId` (Cascade) |
| 2 | `LND_Transactions` | `LendingTransaction` | `cop_movimto` | Créditos y movimientos | línea; crédito |
| 3 | `LND_PendingInstallments` | `PendingInstallment` | `cop_cuopen` | Créditos y movimientos | línea; crédito |
| 4 | `LND_PreviousInstallments` | `PreviousInstallment` | — | Créditos y movimientos | línea |
| 5 | `LND_ExtraPayments` | `ExtraPayment` | `cop_extras` | Créditos y movimientos | línea; crédito |
| 6 | `LND_ExtraPaymentBalances` | `ExtraPaymentBalance` | — | Créditos y movimientos | línea |
| 7 | `LND_LoanApplications` | `LoanApplication` | `cop_solcre` | Solicitud | línea |
| 8 | `LND_ApplicationAssets` | `ApplicationAsset` | `cop_solbienes` | Solicitud | — |
| 9 | `LND_ApplicationCodebtors` | `ApplicationCodebtor` | `cop_solcodeudor` | Solicitud | — |
| 10 | `LND_ApplicationExtras` | `ApplicationExtra` | `cop_extrasoli` | Solicitud | — |
| 11 | `LND_ApplicationReferences` | `ApplicationReference` | `cop_solreferencia` | Solicitud | — |
| 12 | `LND_ApplicationStatuses` | `ApplicationStatus` | `cop_estsol` | Solicitud | línea |
| 13 | `LND_HousingApplicationParams` | `HousingApplicationParam` | `cop_solparviv` | Solicitud | — |
| 14 | `LND_Guarantees` | `Guarantee` | `cop_garantia` | Garantías, seguros, vivienda | línea; crédito |
| 15 | `LND_InsurancePolicies` | `InsurancePolicy` | — | Garantías, seguros, vivienda | línea |
| 16 | `LND_InsuranceBeneficiaries` | `InsuranceBeneficiary` | — | Garantías, seguros, vivienda | línea |
| 17 | `LND_HousingParameters` | `HousingParameter` | — | Garantías, seguros, vivienda | línea |
| 18 | `LND_LoanRestructurings` | `LoanRestructuring` | — | Reestructuración | línea |
| 19 | `LND_AccrualEntries` | `AccrualEntry` | `cop_caunov` | Causación y mora | línea |
| 20 | `LND_AccrualPeriods` | `AccrualPeriod` | — | Causación y mora | — |
| 21 | `LND_DefaultRecords` | `DefaultRecord` | `cop_copmora` | Causación y mora | línea; crédito |
| 22 | `LND_DefaultLiquidations` | `DefaultLiquidation` | `cop_liqmor` | Causación y mora | línea |
| 23 | `LND_PortfolioClassifications` | `PortfolioClassification` | `cop_copclas` | Clasificación y provisión | — |
| 24 | `LND_PortfolioBalances` | `PortfolioBalance` | `cop_salmaecar` | Clasificación y provisión | línea |
| 25 | `LND_ShortLongTermPortfolio` | `ShortLongTermPortfolio` | — | Clasificación y provisión | línea |
| 26 | `LND_ProvisionParameters` | `ProvisionParameter` | — | Clasificación y provisión (cuentas de texto) | — |
| 27 | `LND_PortfolioAccounts` | `PortfolioAccount` | — | Clasificación y provisión (cuentas de texto) | — |
| 28 | `LND_RiskAssessments` | `RiskAssessment` | — | Clasificación y provisión | línea |
| 29 | `LND_ScoringParameters` | `ScoringParameter` | — | Scoring y tasas | — |
| 30 | `LND_ScoringRanges` | `ScoringRange` | — | Scoring y tasas | — |
| 31 | `LND_InterestRates` | `InterestRate` | `cop_claint` | Scoring y tasas (clasificación por línea) | — |
| 32 | `LND_RatesByTerm` | `RateByTerm` | — | Scoring y tasas | línea |
| 33 | `LND_TermRates` | `TermRate` | — | Scoring y tasas | línea |
| 34 | `LND_CreditLineParameters` | `CreditLineParameter` | `cop_concar12` | Líneas (destino de 33 FK; cuentas de texto) | — |
| 35 | `LND_CreditLineAudit` | `CreditLineAudit` | — | Líneas | línea (opcional, sin acción) |
| 36 | `LND_CreditParameters` | `CreditParameter` | — | Líneas | línea |
| 37 | `LND_CollectionCases` | `CollectionCase` | `cop_maegescob` | Cobranza | — |
| 38 | `LND_CollectionMasters` | `CollectionMaster` | `cop_gesmaes` | Cobranza | línea |
| 39 | `LND_CollectionDateEntries` | `CollectionDateEntry` | `cop_novfecgestion` | Cobranza | línea |
| 40 | `LND_CollectionNotices` | `CollectionNotice` | `cop_maecircobro` | Cobranza | — |
| 41 | `LND_CollectionNoticeDetails` | `CollectionNoticeDetail` | `cop_detcircobro` | Cobranza | línea |
| 42 | `LND_CollectionNoticeParams` | `CollectionNoticeParam` | `cop_paracircular` | Cobranza | — |
| 43 | `LND_CollectionPeriods` | `CollectionPeriod` | `cop_gespara` | Cobranza | — |
| 44 | `LND_PayrollDeductions` | `PayrollDeduction` | `cop_nomdes` | Deducciones (tiene `ContributionAmount`) | línea |
| 45 | `LND_PayrollDeductionConcepts` | `PayrollDeductionConcept` | `cop_nomconce` | Deducciones | línea |
| 46 | `LND_PayrollDeductionEntries` | `PayrollDeductionEntry` | `cop_nomnov` | Deducciones (las lee la ordinaria) | — |
| 47 | `LND_PayrollDeductionPeriods` | `PayrollDeductionPeriod` | `cop_nompla` | Deducciones (tiene `ContributionAmount`) | — |
| 48 | `LND_DeductionValues` | `DeductionValue` | `cop_valdesc` | Deducciones | — |
| 49 | `LND_PeriodicityParameters` | `PeriodicityParameter` | — | Deducciones | — |
| 50 | `LND_PortfolioInvoices` | `PortfolioInvoice` | — | Facturas de cartera | línea |
| 51 | `LND_InvoiceMasters` | `InvoiceMaster` | — | Facturas de cartera | — |
| 52 | `LND_InvoiceLineItems` | `InvoiceLineItem` | — | Facturas de cartera | línea |
| 53 | `LND_InvoiceDetails` | `InvoiceDetail` | — | Facturas de cartera | — |
| 54 | `LND_OnlineQueries` | `OnlineQuery` | `lin_consulta` | Dependiente de la línea | línea (requerida) |
| 55 | `LND_RecreationApplications` | `RecreationApplication` | — | Dependiente de la línea (recreación financiada con número de crédito) | línea (requerida) |

#### Se conservan (33 de otros módulos más las 5 compartidas: 38)

Las 38 pasan a `Domain.Entities.Savings` sin cambiar su `ToTable` (R125). Ninguna tiene llave hacia una retirada salvo
`LND_SiplaGroupParameters`.

| # | Tabla | Entidad | Origen | Grupo | Llaves |
|---|---|---|---|---|---|
| 56 | `LND_SavingsAccounts` | `SavingsAccount` | `cop_maeahor` | Ahorros y depósitos | — |
| 57 | `LND_SavingsParameters` | `SavingsParameter` | `cop_ahorro58` | Ahorros y depósitos | — |
| 58 | `LND_DepositAccounts` | `DepositAccount` | — | Ahorros y depósitos | — |
| 59 | `LND_DepositEntries` | `DepositEntry` | — | Ahorros y depósitos | — |
| 60 | `LND_DepositAudit` | `DepositAudit` | — | Ahorros y depósitos | — |
| 61 | `LND_DepositSeals` | `DepositSeal` | — | Ahorros y depósitos | — |
| 62 | `LND_DepositSignatures` | `DepositSignature` | — | Ahorros y depósitos | — |
| 63 | `LND_Cashiers` | `Cashier` | — | Caja y chequeras | — |
| 64 | `LND_CashBases` | `CashBase` | — | Caja y chequeras | — |
| 65 | `LND_Checkbooks` | `Checkbook` | — | Caja y chequeras | — |
| 66 | `LND_CheckClearing` | `CheckClearing` | — | Caja y chequeras | — |
| 67 | `LND_ContributionReductions` | `ContributionReduction` | — | Aportes | — |
| 68 | `LND_ContributionReductionParams` | `ContributionReductionParam` | — | Aportes | — |
| 69 | `LND_AssociateWithdrawals` | `AssociateWithdrawal` | — | Retiros | — |
| 70 | `LND_WithdrawalStatuses` | `WithdrawalStatus` | — | Retiros | — |
| 71 | `LND_SiplaParameters` | `SiplaParameter` | — | SIPLA | — |
| 72 | `LND_SiplaGroupParameters` | `SiplaGroupParameter` | — | SIPLA | línea (Cascade, requerida): **se suelta** (R120) |
| 73 | `LND_UnusualTransactions` | `UnusualTransaction` | — | SIPLA | — |
| 74 | `LND_UnusualTransactionEntries` | `UnusualTransactionEntry` | — | SIPLA | — |
| 75 | `LND_MoneyLaunderingDeclarations` | `MoneyLaunderingDeclaration` | — | SIPLA | — |
| 76 | `LND_ActivityEnrollments` | `ActivityEnrollment` | — | Asociados y servicios | — |
| 77 | `LND_AssociateActivities` | `AssociateActivity` | — | Asociados y servicios | — |
| 78 | `LND_AssociateDiseases` | `AssociateDisease` | — | Asociados y servicios | — |
| 79 | `LND_BiometricRecords` | `BiometricRecord` | — | Asociados y servicios | — |
| 80 | `LND_PersonAssets` | `PersonAsset` | — | Asociados y servicios | — |
| 81 | `LND_Subsidies` | `Subsidy` | — | Asociados y servicios | — |
| 82 | `LND_AuxiliaryApplications` | `AuxiliaryApplication` | `cop_solaux` | Asociados y servicios (auxilios) | — |
| 83 | `LND_AuxiliaryApplicationLines` | `AuxiliaryApplicationLine` | `cop_linsolaux` | Asociados y servicios (auxilios) | — |
| 84 | `LND_AuxAppInstallments` | `AuxAppInstallment` | `cop_solauxcuota` | Asociados y servicios (auxilios) | — |
| 85 | `LND_AuxAppInstallmentBeneficiaries` | `AuxAppInstallmentBeneficiary` | `cop_solauxcuotabenef` | Asociados y servicios (auxilios) | — |
| 86 | `LND_Zones` | `Zone` | — | Zonas | — |
| 87 | `LND_SubZones` | `SubZone` | — | Zonas | — |
| 88 | `LND_ZoneTypes` | `ZoneType` | — | Zonas | — |

#### Compartidas (5, se conservan; las decide su feature)

| # | Tabla | Entidad | Origen | Uso | Decisión |
|---|---|---|---|---|---|
| 89 | `LND_Minutes` | `Minute` | — | Actas con códigos de texto (`MinutesType`, `MinutesNumber`, `UserId` texto) | Se conserva; Cartera usa `LND_Sessions*` (R133) |
| 90 | `LND_MinuteAttendees` | `MinuteAttendee` | — | Asistentes de esas actas | Se conserva |
| 91 | `LND_Blacklist` | `BlacklistEntry` | — | Lista negra heredada | Se conserva; Cartera usa `COR_RestrictiveList*` y `LND_ScreeningResults` (R139) |
| 92 | `LND_TransactionCodes` | `TransactionCode` | `cop_codmov` | Códigos de movimiento (también de ahorros) | Se conserva; la decide la feature de Ahorros |
| 93 | `LND_Documents` | `LendingDocument` | `cop_docmto` | Documentos (también de ahorros) | Se conserva; la decide la feature de Ahorros |

#### Llaves entre ellas (40 FK del snapshot)

| Desde | Hacia | FK | Regla hoy | Qué hace el retiro |
|---|---|---|---|---|
| 32 tablas retiradas marcadas «línea» | `LND_CreditLineParameters.CreditLineId` | 32 | Cascade, requerida; `LND_CreditLineAudit` opcional y sin acción | Se sueltan antes de borrar las tablas |
| `LND_SiplaGroupParameters` (conservada) | `LND_CreditLineParameters.CreditLineId` | 1 | Cascade, requerida | Se suelta la FK y su índice `IX_LND_SiplaGroupParameters_CreditLineId`; la columna queda como dato (nadie la escribe) |
| `LND_DefaultRecords`, `LND_ExtraPayments`, `LND_Guarantees`, `LND_Transactions`, `LND_PendingInstallments` | `LND_LoanPortfolios.LoanPortfolioId` | 5 | Opcional, sin acción | Se sueltan |
| `LND_LoanPortfolios` | `COR_People.PersonId` | 1 | Cascade, requerida | Cae con la tabla; `COR_People` no pierde nada |
| `PAY_SettlementDeductions` | `LND_LoanPortfolios.LoanPortfolioId` | 1 | Restrict, opcional | Copia previa a `ObligationPublicId`/`ObligationNumber` (R108); el retiro suelta FK, `IX_…_LoanPortfolioId`, `UK_…_Termination_Loan` y la columna |

Son 38 FK entre tablas `LND_`, una de `LND_` a `COR_` y una de `PAY_` a `LND_`: 40. Ninguna entidad de otro módulo tiene
navegación hacia `Lending`.

**Referencias sin FK** que el retiro también considera:

| Dónde | Qué | Decisión |
|---|---|---|
| `PAY_Novelties.LoanPortfolioId` | int?, sin FK ni índice, nadie lo escribe | Copia (vacía en la práctica) a `ObligationPublicId`/`ObligationNumber` y se borra la columna (R108) |
| `ACC_Documents.OriginModule = 'CAR'` | Comprobantes de desembolsos o recaudos heredados | No se tocan (inmutables); la guarda los cuenta (R122, H4) |
| `voucher-types.json`: `DS`, `RC`, `CA`, `PV`, `DN` con módulo `CAR` | Tipos sembrados «que no se eliminan ni cambian de uso» | Se reutilizan para Cartera nueva (T54); el diagnóstico cuenta si se usaron |
| `COR_PaymentMeans.SuggestedCreditLineCode` | Texto, sin FK | Se conserva; ya nombra líneas de la Cartera nueva por código |
| `SEC_Users.CanApproveLoansMin/Max`, `CanOverrideLimits` | Columnas heredadas sin uso | No se usan para atribuciones (R132); no se retiran aquí |

#### Código fuera de la cartera heredada que la lee o escribe

| Archivo | Uso | Destino |
|---|---|---|
| `Core/People/Commands/DeletePerson/DeletePersonCommand.cs` l. 36 | `LoanPortfolios` para bloquear el borrado | `IConsultaDeObligaciones` (R110) |
| `Core/People/Queries/GetPersonDetailQuery.cs` l. 146-170 | Resumen de créditos | `IConsultaDeObligaciones` (R110) |
| `Payroll/Services/SettlementInputLoader.cs` l. 640 | Propuesta de descuentos | `PropuestaDeDescuentosDeCartera` (R104) |
| `Payroll/Settlements/Settlement/ApproveSettlementCommand.cs` l. 126-160 | `LoanPortfolios` y `RecaudoDeCredito` | `RecaudoPorNomina` por el contrato (R104) |
| `Payroll/Services/CalculationInputLoader.cs` l. 146 | `PayrollDeductionEntries` tipo DN | `CuotasDeNominaAsync` (R106) |
| `Persistence/Services/AccountReferenceFinder.cs` l. 29 | Códigos de cuenta en `CreditLineParameters` | Sale el bloque; la matriz de Cartera entra con `IntegracionContableDeCartera` (T51) |
| `Lending/Withdrawals/**` (`ProcessWithdrawalCommand` l. 46, `WithdrawalQueries` l. 68) | Créditos vivos del asociado | `IConsultaDeObligaciones` como consumidor «Aportes, en el retiro» (R110) |
| `Lending/Statements/**` (`GenerateStatementsCommand` l. 36, `StatementQueries` l. 66) | Extractos con créditos | Pierden la parte de crédito (R110, A5) |
| `Lending/Reports/PersonPortfolioReportQuery.cs` l. 71 | Portafolio de la persona | Sin créditos, a `Application/Savings` (R125) |
| `Debit/Cards/Commands/**` | `SavingsAccounts`, `DepositEntries` (conservadas) | Sólo cambian los `using` a `Savings` (R125) |

### 9.3 Decisiones

#### R119 · Clasificación de las 93 tablas heredadas

**Decision**: se retiran las 55 de crédito y se conservan 38 (33 de ahorros, depósitos, caja, aportes, retiros, SIPLA,
asociados y zonas, más 5 compartidas), según la clasificación de 9.2. Entran como crédito, aunque
la spec no las nombre, `LND_PeriodicityParameters` y `LND_DeductionValues` (sólo las consumía la deducción por nómina
heredada), `LND_InterestRates` (`cop_claint`, clasificación por línea), `LND_OnlineQueries` y `LND_RecreationApplications`
(FK requerida a la línea; H3). Las compartidas `LND_Minutes`, `LND_MinuteAttendees`, `LND_Blacklist`, `LND_TransactionCodes`
y `LND_Documents` **se conservan**: no son de crédito ni tienen FK a lo retirado, y las decide su feature (H2). Si una tabla
de crédito tiene datos de ahorro o de aportes, el retiro se detiene.

**Rationale**: C6 y D10 al pie de la letra («sólo las de crédito»). Las 33 FK a la línea y las 5 al crédito cierran el
conjunto: ninguna retirada queda referenciada por una conservada salvo SIPLA (R120).

**Alternatives considered**
- Retirar también las compartidas si el diagnóstico da cero filas (S): va contra C6 y no tienen feature dueña (CH22).
- Conservar `LND_OnlineQueries` y `LND_RecreationApplications` soltando su FK: su razón de ser es la línea y el número de
  crédito heredados; quedarían huérfanas.

**Trazas**: T86 · FR-144, C6, D10; H2, H3 · R-01 (CH22).

**Difiere del informe**: El informe de seguridad proponía retirar las compartidas si el diagnóstico daba cero filas
(CH22).

#### R120 · Llaves entre lo que se conserva, lo que se retira y otros módulos

**Decision**
1. `LND_SiplaGroupParameters` se conserva: pierde la navegación `CreditLine`, la FK
   `FK_LND_SiplaGroupParameters_LND_CreditLineParameters_CreditLineId` (Cascade, requerida) y el índice
   `IX_LND_SiplaGroupParameters_CreditLineId`. La columna `CreditLineId` queda como dato heredado sin llave; nadie la escribe.
   La guarda cuenta sus filas (H1).
2. `PAY_SettlementDeductions`: copia previa (R108) y en el retiro se sueltan FK, IX, UK y la columna `LoanPortfolioId`.
   `CarteraTransactionPublicId` se conserva y guarda el comprobante del contrato.
3. `PAY_Novelties.LoanPortfolioId` (sin FK, sin uso) se borra; la obligación de cada novedad `LoanDeduction` va en
   `ObligationPublicId`/`ObligationNumber` (R106, R108).
4. Los `ACC_Documents` con `OriginModule = 'CAR'` no se tocan: son inmutables. La guarda los cuenta; si hay, se aprueba por
   base con constancia (H4).
5. `LND_LoanPortfolios` → `COR_People` cae con la tabla; `COR_People` no pierde nada.

**Rationale**: FR-144 exige que ninguna tabla conservada apunte a una retirada y que SIPLA quede intacto (se conservan tabla y
datos; sólo se suelta la llave). El Principio VI pide identificar el crédito por su PublicId.

**Alternatives considered**
- Retirar `LND_SiplaGroupParameters`: choca con «SIPLA intacto».
- Conservar `LoanPortfolioId` como entero sin FK: Id interno de una tabla que ya no existe.
- Mover el cambio de `PAY_SettlementDeductions` a una migración posterior: el modelo intermedio no compilaría, porque
  `SettlementDeductionConfiguration` referencia `LoanPortfolio`.

**Trazas**: T87 · FR-144, FR-147; Principios VI, XII; H1, H4 · R-02.

**Difiere del informe**: R-02 soltaba la columna de `PAY_SettlementDeductions` sin copiar antes el PublicId (CH37).

**Conciliación**: §7.6 — `LND_SiplaGroupParameters.CreditLineId` queda **requerida y sin FK** (volverla anulable sería
una operación sobre una tabla conservada); la decide la feature de SARLAFT. El retiro suelta **40** llaves: 38 entre
tablas `LND_`, una de `LND_LoanPortfolios` a `COR_People` y una de `PAY_SettlementDeductions` (R-04 decía «39 entre
LND»).

#### R121 · Estado intermedio: los consumidores pasan a los puertos «sin activar»

**Decision**
- Antes de generar la migración destructiva, todo consumidor externo deja las entidades heredadas y pasa a
  `IConsultaDeObligaciones` (`ConsultaDeObligacionesSinActivar`: sin obligaciones exigibles) e `IContratoDeObligaciones`
  (`ContratoDeObligacionesSinActivar`: listar y simular vacíos con el motivo `Lending.NotActivated`; aplicar, reversar y
  resolver saldos rechazados con el mismo motivo).
- Se cambian los consumidores de la tabla de 9.2 «Código fuera de la cartera heredada que la lee o escribe»: Personas, la definitiva
  y la ordinaria de Nómina (sin `RecaudoDeCredito`, por el contrato en la misma unidad de trabajo), `AccountReferenceFinder`,
  retiro de asociado, extractos y portafolio.
- Las implementaciones reales llegan con `CarteraNucleo` y reemplazan el registro en `DependencyInjection`, como hizo la 012
  con `ConsultasDeCarteraNoHabilitada`.
- Al cerrar este paso, ningún archivo fuera de la cartera heredada nombra `LND_` heredadas ni sus `DbSet`, y
  `Ventas/CreditoProvisionalTests` sigue verde.

**Rationale**: es el comportamiento que FR-142 exige mientras Cartera no se active (Nómina aprueba sin descuentos de Cartera,
Personas no ve obligaciones). El modelo intermedio compila y es correcto en producción, y el retiro puede ir primero en
commit propio, como en la 012.

**Alternatives considered**
- Retirar después de construir `CarteraNucleo`: convivirían dos `Lending` y se reutilizarían nombres en la misma migración,
  lo que la 012 evitó.
- Dejar a Nómina leyendo `LND_LoanPortfolios` hasta E-03: impide el retiro y contradice C4 y FR-147.

**Trazas**: T88, T85 · FR-061, FR-083, FR-088, FR-142, FR-147, FR-148, I-4; A5 · R-03.

#### R122 · Migración destructiva con guarda `RetiroDeLaCarteraHeredada`

**Decision**: par PostgreSQL / SQL Server generado con `tools/scripts/add-migration.ps1` sobre el **modelo intermedio** (sin
las 55 entidades y sin ninguna nueva), con la cabecera `MIGRACION-DESTRUCTIVA-APROBADA` (referencia del respaldo y del
segundo revisor), en commit propio. `Up()`, en orden:
1. **Guarda**: PG `DO $$ … RAISE EXCEPTION`; SQL Server cursor con `THROW 50013` (50012 es de la 012). Cuenta filas,
   incluidas las dadas de baja: de las 55 tablas (saltando las que no existan), de `LND_SiplaGroupParameters`, de
   `PAY_SettlementDeductions` y `PAY_Novelties` con `LoanPortfolioId` no nulo y de `ACC_Documents` con
   `OriginModule = 'CAR'`. Si algo es mayor que cero y la base no tiene la fila viva
   `COR_SystemSettings.SettingKey = 'LND.RetiroHeredado.Aprobado'` (`ModulePrefix 'LND'`, insertada a mano tras el respaldo),
   se niega nombrando cada tabla y su cantidad y no toca nada.
2. Suelta las 40 FK de la tabla de llaves de 9.2.
3. Ajusta `PAY_SettlementDeductions` y `PAY_Novelties` (R120).
4. Borra las 55 tablas.

`Down()` recrea vacías las 55 tablas, sus FK y las columnas de `PAY_`: es irreversible en datos. Orden en K1:
`NominaRecaudaPorElContrato` y `ComercioConCartera` → `RetiroDeLaCarteraHeredada` → `ValoresNacionales` →
`PlataformaParaCartera` → `CarteraNucleo` → `CarteraRiesgo` → `IntegracionContableDeCartera`. Corre en el Job PreSync del
DbMigrator (AutoMigrate apagado en QA y PDN). La toca `PrincipioXII_MigracionesDestructivas` y `check-migration-parity.ps1`.

**Rationale**: molde de la 012 en la variante «guarda con aprobación por base», no la estricta de la 009; con un número de
error propio, un PreSync que falle se identifica sin ambigüedad. Cumple el Principio XII.

**Alternatives considered**
- Guarda estricta sin aprobación (009): una base con datos de prueba exigiría limpiarla a mano, otra operación destructiva.
- Una sola migración de retiro y núcleo: mezcla `DropTable` con `CreateTable`, sin `Down()` útil; la 012 documentó que
  reutilizar nombres en un scaffold produce `AlterColumn` sobre lo heredado.

**Trazas**: T87, §2.26 · FR-144; Principio XII; H4, H7 · R-04.

#### R123 · Diagnóstico, runbook y pruebas del retiro

**Decision**
- **Diagnóstico** `specs/013-cartera-financiera/diagnostico-cartera-heredada.sql`, de sólo lectura, con bloques POSTGRESQL y
  SQL SERVER. Informa: filas de las 55 tablas (−1 si no existe) y su total; filas de las 38 conservadas (informativo); filas
  de `LND_Transactions` con `SavingsLineId` o `CdatAccrualDate` no nulos; de `LND_PayrollDeductions` y
  `LND_PayrollDeductionPeriods` con `ContributionAmount > 0`; de `LND_SiplaGroupParameters`; de `PAY_SettlementDeductions`
  con `LoanPortfolioId` no nulo, por `Status`; de `PAY_Novelties` con `LoanPortfolioId` no nulo; de `ACC_Documents` con
  `OriginModule = 'CAR'`, por `SourceType` y tipo (`DS`, `RC`, `CA`, `PV`, `DN`); y si ya existe la aprobación. Se corre en
  DEV, QA y PDN, en cada base de cooperativa y en `ingenia365erp`, antes de fusionar y el día de cada promoción (PDN con
  autorización expresa del dueño).
- **Runbook** `docs/operaciones/cartera-retiro-heredado.md`, con las secciones del de inventario: diagnóstico, respaldo
  `<base>-AAAAMMDD-pre-f013.dump` (también de la base administrativa, por los valores nacionales), fila de aprobación, segundo
  revisor, PreSync, verificación y restauración.
- **e2e** `RetiroDeLaCarteraHeredadaTests`, en los dos motores y en una cooperativa aislada: baja a la migración anterior,
  inserta una fila en una tabla retirada y otra en `LND_SavingsAccounts`, la guarda nombra la retirada, se siembra la
  aprobación, la migración pasa y la de ahorros sigue.
- **Arquitectura**: `LaCarteraHeredadaNoVuelve` (ninguno de los 55 nombres vuelve al modelo ni al snapshot) y
  `NingunaTablaConservadaApuntaALaCarteraRetirada`.

**Rationale**: molde verificado de la 012 (T029, T030, T037); FR-144 exige el diagnóstico por ambiente y detenerse si hay
datos de crédito.

**Alternatives considered**: diagnosticar sólo con la guarda en el PreSync: falla tarde, en plena sincronización, y no
informa las compartidas.

**Trazas**: T87 · FR-144; Principio XII; H7 · R-05.

#### R124 · Nombres nuevos sin reutilizar ninguno heredado

**Decision**: toda tabla nueva de Cartera lleva `LND_` y un nombre distinto de los 93 heredados; la transacción de cartera es
`LND_LoanTransactions` (`LoanTransaction`), el nombre literal del Principio XI. Los UK e IX largos se nombran con
`HasDatabaseName` (PostgreSQL trunca a 63 caracteres). Equivalencias:

| Heredada | Nueva |
|---|---|
| `LND_LoanApplications` | `LND_CreditApplications` (+ `…Parties`, `…Documents`, `…Checks`) |
| `LND_LoanPortfolios` | `LND_Loans`, `LND_LoanParties`, `LND_LoanTermsVersions`, `LND_LoanControls` |
| `LND_Transactions` | `LND_LoanOperations` (hecho primario) y `LND_LoanTransactions` (libro) |
| `LND_PendingInstallments`, `LND_PreviousInstallments` | `LND_RepaymentScheduleVersions`, `LND_RepaymentInstallments` |
| `LND_CreditLineParameters`, `LND_CreditParameters`, `LND_RatesByTerm`, `LND_TermRates` | `LND_CreditLines`, `LND_CreditLineVersions`, `LND_CreditLineRateBands` |
| `LND_InsurancePolicies` | `LND_CreditLineInsurances` |
| `LND_Guarantees` | `LND_Collaterals`, `LND_CreditLineCollateralRules` |
| `LND_PortfolioClassifications`, `LND_PortfolioBalances` | `LND_MonthlyCloses`, `LND_MonthlyCloseLoans`, `LND_LoanDailyPositions` |
| `LND_Minutes`, `LND_MinuteAttendees` (conservadas) | `LND_Sessions`, `LND_SessionAttendances`, `LND_SessionItems`, `LND_SessionVotes` |
| `LND_Blacklist` (conservada) | `COR_RestrictiveListLoads`, `COR_RestrictiveListEntries`, `LND_ScreeningResults` |
| `LND_PayrollDeductions*`, `LND_DeductionValues` | `LND_PayrollAgreements*`, `LND_PayrollDeductionFiles*` (K2) |
| `LND_Collection*` | `LND_CollectionStages`, `LND_CollectionStageHistory`, `LND_CollectionActions` (K4) |

**Rationale**: con nombres nuevos `LaCarteraHeredadaNoVuelve` es estricta para siempre y el `Down()` del retiro se puede
aplicar en una base de ensayo (la 012 lo perdió con `INV_Documents`, `INV_Products`, `INV_Warehouses`); el prefijo `LND_`
conserva la protección de `InventarioNoConoceContabilidadNiCartera` (literal `"LND_"`); usar el nombre del Principio XI
resuelve I-5 sin enmienda.

**Alternatives considered**
- Reutilizar los nombres heredados, como la 012: bloquea el `Down()` y obliga a explicarlo en el runbook.
- Un prefijo nuevo (`CAR_`): la constitución sólo admite `LND_` para Cartera.
- Nombres de R-06 reemplazados por el frente dueño: `LND_LoanScheduleVersions`, `LND_LoanInstallments`,
  `LND_LoanBalanceSnapshots` (CH25); `LND_MonthlyClosings`, `LND_LoanDelinquencyHistory` (CH26); `LND_PaymentApplications`
  (CH27); `LND_CreditModalities`, `LND_CreditModalitySubsegments` (CH28); `LND_CreditLimitReservations` (CH29);
  `LND_RelatedPersons` (CH30); `LND_PaymasterAgreements`, `LND_DeductionFiles`, `LND_CollectionAssignments`,
  `LND_RepaymentAgreements`, `LND_LegalProceedings`, `LND_PriorNoticeDeliveries`, `LND_RegulatoryReports`,
  `LND_CreditBureauFiles` (CH31); `LND_CommitteeSessions` sobre `COR_Committees` (CH21); `LND_DataSubjectAuthorizations`,
  `LND_DataSubjectRequests` (CH23); `COR_RestrictiveListEntries` sobre `COR_ListParameters` (CH24).

**Trazas**: T2 · FR-144, I-5; Principio XI · R-06 (CH21–CH31).

**Difiere del informe**: R-06 proponía otros nombres para el libro, los cierres, el comprobante, las modalidades, las
reservas de cupo, los vinculados, la libranza, la cobranza, lo regulatorio, las actas, el habeas data y las listas
(CH21–CH31).

#### R125 · `Lending` sólo para la Cartera nueva; lo conservado a `Savings`

**Decision** (sujeta a la enmienda A10), en el commit 2 de R128:
- Las 38 entidades conservadas pasan a `Domain.Entities.Savings`; sus configuraciones a `Configurations/Savings`; sus
  handlers a `Application.Savings` (incluido `LiquidateSavingsInterestCommand`; `LiquidateCDTInterestCommand` sigue en
  `Application/CDT/InterestLiquidation`, T96); sus 12 grupos de rutas a `Endpoints/Savings` (`/api/savings/{mismo sufijo}`);
  sus 18 pantallas a `Pages/AhorrosYAportes`, **con las mismas rutas** `/cartera/...`.
- `PersonPortfolioReportQuery` (sin créditos) va a `Application/Savings`, y `person-portfolio` y `savings-statement` pasan a
  `SavingsReportsEndpoints` (`/api/reports/savings/…`), así `/api/reports/lending` queda sólo para la Cartera nueva.
- `ModuloDeAuditoria` suma la regla «.Savings» → `Savings` antes de «.Core» (módulo no encadenado).
- Los `ToTable` no cambian y el scaffold no debe traer operaciones sobre esas tablas (se verifica). `Endpoints/Savings` queda
  exento por nombre en `LosEndpointsProtegidosExigenPermiso` hasta su feature.
- Todo lo que se llame `Lending` es de la Cartera nueva (entidades, Application, configuraciones, endpoints, puertos, rutas
  `/api/lending/*`, permisos `Lending.*`, módulo de auditoría y destino de bandeja). Lo fija `ElEspacioLendingEsSoloCartera`.
- `Savings` es hogar provisional hasta las features de Ahorros, Aportes y SARLAFT.
- Si A10 se rechaza: lo conservado mantiene `/api/lending/*` y su espacio, y `ElEspacioLendingEsSoloCartera` lo exime por
  nombre, y `ModuloDeAuditoria` suma una regla por nombre que lleva los handlers de ahorros a `Savings` antes de la de
  `.Lending`, para que no queden encadenados ni atribuidos a Cartera (C89).

**Rationale**: la prueba de FR-008 y la de permisos por ruta nacen sin excepciones, y la cadena de auditoría de Cartera
(FR-149) no arrastra eventos de ahorros. Cambiar el espacio de una entidad sin cambiar su tabla no produce operaciones de
base.

**Alternatives considered**
- Dejar lo conservado en `Lending` con listas de excepción decrecientes (como `AprobadasAntesDeLaGuarda`): las pruebas nacen
  con agujeros.
- Poner la Cartera nueva en otro espacio (`Credit`, `Cartera`): choca con `/api/lending`, `Lending.*` y lo que la 012 ya
  fijó (`IConsultasDeCartera`, `IntegrationDestinations.Lending`).
- Repartir lo conservado entre `Deposits`, `Compliance` y `Core`: tres mudanzas y tablas `LND_` bajo espacios ajenos.

**Trazas**: T3, T99 · FR-008, FR-144, FR-149; A10 · R-07, DC-19, DE-17.

**Difiere del informe**: R-07 nombraba carpetas en español (`Migracion/Activacion`) (CH41).

#### R126 · Rutas, pantallas, menú, manual y reportes heredados

**Decision**
- **Se retiran** en el commit 3 de R128, con sus rutas (T88, CH50):
  - 66 rutas: las 63 de crédito de `Endpoints/Lending` (15 archivos, ver 9.1) y `aging`, `aging/pdf` y
    `loan-statement/{id}/pdf` de `/api/reports/lending`;
  - 21 pantallas con sus `.razor.css`: `Solicitudes`, `SolicitudCredito`, `CarteraCreditos`, `DetalleCredito`, `Recaudos`,
    `MoraYCobro`, `Desembolsos`, `LineasCredito`, `TasasInteres`, `TasasPlazo`, `ParametrosProvision`, `ParametrosScoring`,
    `ConceptosDescuento`, `ParametrosVivienda`, `ParametrosPeriodicidad`, `CuentasCartera`, `CausacionIntereses`,
    `CalificacionCartera`, `DescuentoNomina`, `Reportes/CarteraPorEdades`, `Reportes/ExtractoCredito`;
  - 18 entradas del menú (16 del grupo y 2 de reportes); 3 tarjetas de `CentroReportes` (extracto de crédito, cartera por
    edades, calificación; vuelven con las vistas nuevas);
  - 16 temas del manual (`solicitud-de-credito`, `cartera-de-creditos`, `recaudos-y-pagos`, `mora-y-cobro`,
    `causacion-de-intereses`, `calificacion-de-cartera`, `descuento-de-nomina`, `lineas-credito`, `tasas-interes`,
    `parametros-provision`, `scoring`, `conceptos-descuento`, `parametros-vivienda`, `parametros-periodicidad`,
    `tasas-plazo`, `cuentas-cartera`) y los de reportes `extracto-credito` y `cartera-edades`;
  - las clases PDF `LoanStatementReport` y `PortfolioAgingReport`, y en `Application/Lending` las carpetas de las rutas
    retiradas más `Reports/PortfolioAgingQuery`.
- **Se conservan**: 52 rutas (a `/api/savings`, R125), `person-portfolio` sin créditos y `savings-statement` (a
  `/api/reports/savings`); 18 pantallas (`Aportes`, `CDTDetalle`, `CDTNuevo`, `Certificados`, `CodigosMovimiento`,
  `CuentasAhorro`, `Depositos`, `DetalleAhorro`, `EstadosRetiro`, `Extractos`, `LiquidacionInteresAhorro`,
  `LiquidacionInteresCDT`, `ParametrosAhorro`, `ParametrosSipla`, `RetiroAsociado`, `Retiros`, `TiposZona`, `Zonas`) en
  `Pages/AhorrosYAportes`; sus 16 enlaces en el grupo de menú «Ahorros y aportes»; sus temas en
  `ManualCatalogo.Modulos.AhorrosYAportes`.
- **Nuevo**: grupo de menú «Cartera» con `PermissionGate` por enlace; `Pages/Cartera` en `ModulosMigrados`;
  `ManualCatalogo.Modulos.Cartera = "Cartera"`.
- **Rutas reservadas** a lo conservado: `/cartera/ahorros*`, `/cartera/depositos`, `/cartera/retiros`,
  `/cartera/retiros-asociado`, `/cartera/estados-retiro`, `/cartera/cdt*`, `/cartera/aportes`, `/cartera/parametros-ahorro`,
  `/cartera/parametros-sipla`, `/cartera/zonas`, `/cartera/tipos-zona`, `/cartera/codigos-movimiento`, `/cartera/extractos`.
- La rama acepta un tramo sin pantallas de crédito hasta que se publican las nuevas.

**Rationale**: FR-144 retira rutas y pantallas de crédito, conserva ahorros y aportes y prohíbe nombrar el sistema anterior.
`TodoEnlaceDelMenuTieneSuPagina` y `ManualCatalogoTests` no admiten enlaces sin página; separar las carpetas evita que la
prueba de `IndicadorDeCarga` alcance pantallas heredadas que no lo tienen.

**Alternatives considered**
- Ocultar las pantallas heredadas hasta tener las nuevas: llaman a rutas retiradas, y tres ya llamaban a rutas inexistentes.
- Retirar cada pantalla en el mismo cambio que publica su reemplazo (F-20): sobrevivirían a sus 63 rutas y responderían 404;
  conservar también las rutas obligaría a mantener la cartera heredada en el modelo intermedio (CH50).

**Trazas**: T88, T89, T98, T99 · FR-144; A10 · R-08, F-20 (CH44, CH50).

**Difiere del informe**: F-20 retiraba cada pantalla en el cambio que publica su reemplazo (CH50); F-15 retiraba
`person-portfolio` (CH44).

#### R127 · Permisos legados y proyecto `IngenIA365ERP.Legacy`

**Decision**
- Se borran el bloque `Lending` de `IdentitySeedData` (l. 52-74) y el rol `LoanOfficer` (l. 161). El catálogo real nace en
  `LendingPermissionCatalogSeeder` (R129).
- Se retira entero el proyecto `IngenIA365ERP.Legacy` (su única implementación es `LendingLegacyAdapter`), su línea en
  `IngenIA365ERP.slnx` y la exclusión de `RepoPath.cs` (l. 34).

**Rationale**: hoy ningún `Lending.*` llega a `SEC_Permissions` y habría dos fuentes del mismo prefijo; el adaptador es código
muerto sin consumidores.

**Alternatives considered**: conservar el proyecto vacío por si otro módulo lo usa: no tiene otro adaptador ni referencia.

**Trazas**: T89, T60 · FR-144; Principio III; H6 · R-09.

#### R128 · Orden de los commits del retiro

**Decision**
1. Puertos `IConsultaDeObligaciones` e `IContratoDeObligaciones` con sus implementaciones «sin activar», consumidores
   migrados a ellos (R121), `NominaRecaudaPorElContrato` y `ComercioConCartera`.
2. Mudanza a `Savings` (R125), sin operaciones de base.
3. Retiro de **todo** lo heredado de crédito: código (`RecaudoDeCredito`, `ProcessPaymentCommand`, `DisburseLoanCommand`,
   `AccrueInterestCommand`, `ClassifyPortfolioCommand`, `ProcessPayrollDeductionCommand`, `PayrollDeductionEntries` y las
   cuentas de texto de `LND_CreditLineParameters`, `LND_PortfolioAccounts` y `LND_ProvisionParameters`; T96), rutas,
   pantallas, menú, manual, tarjetas, PDF, permisos legados y el proyecto Legacy (R126, R127).
4. `RetiroDeLaCarteraHeredada` sobre el modelo intermedio, en commit propio (R122).
5. Después: `ValoresNacionales`, `PlataformaParaCartera`, `CarteraNucleo`, `CarteraRiesgo`, `IntegracionContableDeCartera` y
   las implementaciones reales.

Antes de fusionar y el día de cada promoción, en cada base: diagnóstico (PDN con autorización expresa), respaldo `pre-f013`
y segundo revisor en la cabecera. Cierran el retiro: `RetiroDeLaCarteraHeredadaTests` en los dos motores y
`LaCarteraHeredadaNoVuelve`, `NingunaTablaConservadaApuntaALaCarteraRetirada`, `ElEspacioLendingEsSoloCartera` y
`PrincipioXII_MigracionesDestructivas`. Los tipos `DS`, `RC`, `CA`, `PV`, `DN` de `voucher-types.json` se quedan (T54).

**Rationale**: el modelo intermedio compila y es correcto en producción; no se reutilizan nombres en un mismo scaffold (T4 de la
012); lo destructivo queda aislado y se revisa solo.

**Alternatives considered**: un solo commit con todo; retirar después de construir lo nuevo (R121).

**Trazas**: T88, T96 · FR-144, FR-146, FR-147, FR-148 · R-03, R-07, R-08, DE (pasos), DC-19.

### 9.4 Riesgos

| Riesgo | Mitigación |
|---|---|
| La guarda detiene el PreSync si un ambiente tiene datos de crédito o comprobantes `CAR` | Diagnóstico antes de fusionar y el día de cada promoción; aprobación por base con constancia (H4) |
| Columnas de ahorro o aportes en tablas de crédito (`LND_Transactions`, `LND_PayrollDeductions`, `LND_PayrollDeductionPeriods`) | El diagnóstico las cuenta; con filas, el retiro se detiene y decide el dueño |
| Clasificar por nombre es una hipótesis; «Periodicidad» y «Tasas de interés» pueden parecer de ahorros | Diagnóstico por base; pregunta H3 |
| `LND_SiplaGroupParameters` con filas queda con un `CreditLineId` sin sentido | La guarda lo cuenta; decide el dueño (H1) |
| `DropColumn` en dos tablas de Nómina hace destructiva esa parte; una definitiva en borrador con descuento heredado lo pierde al recalcular | La guarda cuenta las filas no nulas de `PAY_SettlementDeductions` y `PAY_Novelties`; copia previa (R108) |
| Una cooperativa aprovisionada después del diagnóstico | Repetir el diagnóstico el día de la promoción |
| El clasificador de comandos bloquea guiones grandes en `erp-pdn` | Diagnóstico en PDN con autorización expresa (H7) |
| Mudanza a `Savings`: unos 38 archivos de entidad, 38 de configuración, 50 handlers, 12 endpoints, 18 pantallas; cambian las URL de API de lo conservado | Cambio mecánico que cubre la compilación; el scaffold no debe traer operaciones sobre las 38 tablas (se revisa si aparece alguna) |
| Tramo de la rama sin pantallas de crédito; usuarios con `ui.pagina-inicio = /cartera/recaudos` | Aceptado (sin ciclo utilizable ni datos); revisar las preferencias guardadas |
| Retirar el proyecto Legacy cambia la compilación del pipeline | Build limpio y `check-migration-parity` |
| Rama larga con un modelo intermedio | Commits propios; paridad revisada en los dos pasos |
| El grupo de menú «Ahorros y aportes» es un cambio visible | Aviso en el manual y en las notas de la promoción |

---

## 10. Frente de seguridad, actas, habeas data y auditoría

### 10.1 Hechos del código

**Seguridad, roles y auditoría**

- **404 indistinguible.** `PermissionAuthorizationFilter` responde `Generic.NotFound` igual que una ruta inexistente;
  resuelve permisos con `PermisosDeLaPeticion`, falla cerrado; el maestro pasa por un atajo (l. 57). **El intento negado
  no se audita.** Variantes: `RequirePermission(string)` (todos), `RequirePermission(AlgunPermiso)` (alguno),
  `RequirePermissionWhenExporting` (`PermissionAuthorizationExtensions.cs` l. 32-65).
- **Siembra.** `PhaseZeroSecuritySeeder` (Order 20, alcance Tenant) siembra Domain, Payroll, Core, Accounting, Inventory y
  ElectronicInvoicing, después `BuiltInRolesSeeder`; invalida la caché de permisos (TTL 30 min) si inserta vínculos; lo
  corre la API al arrancar, nunca el DbMigrator. `DomainPermissionCatalogSeeder` ya siembra
  `Compliance.HabeasData.{Publish, RecordConsent, Revoke, ViewHistory}` (l. 91-94) y `AuditLog.VerifyIntegrity`.
- **Roles integrados.** CompanyAdmin `"*"`; Auditor `"*.View"`, `AuditLog.*` y dos exportaciones; Operator `"*.View"` más
  una lista; ReadOnly `"*.View"`. `SeedRolePermissionsAsync` reinserta vínculos en cada arranque: un rol integrado no se
  ajusta por cooperativa. `PermisosSaasGlobales` (l. 66-77) es el único mecanismo de exclusión sobre un glob. La 012 sirve
  plantillas `PerfilesSugeridos` (`GET /api/admin/roles/templates?module=`, `CreateRoleFromTemplateCommand`) y fijó que toda
  lectura sensible usa una acción distinta de `View`.
- **Alcance de la 009.** `IUserBranchScope`: `SEC_UserBranchAssignments` → `ADM_Branches.PublicId` → `COR_Branches` por
  `TenantBranchPublicId`; sin asignaciones no restringe (`UserBranchScope.cs` l. 35). La pantalla Agencias conserva el
  vínculo al editar (l. 156) pero no deja elegirlo.
- **Alcance de la 012.** `IAlcanceDeInventario` falla cerrado, total en segundo plano, memorizado por petición, aplicado con
  `FiltroDeAlcance`; lo vigila `LasConsultasDeInventarioRespetanElAlcance`.
- **Límites de monto.** `SEC_PermissionAmountLimits` (rol, permiso, `MaxAmount`, vigencia, motivo);
  `LimitesDePermisoPorUsuario` toma el mayor de los roles; `SetPermissionAmountLimitCommand` es el único escritor y sólo
  admite `PermisosLimitables.Todos` (4 códigos `Inventory.*`). `SEC_Users.CanApproveLoansMin/Max` y `CanOverrideLimits`
  existen y nadie los usa.
- **Motor de aprobaciones de la 012**: niveles por umbral de monto acumulativos, un aprobador por nivel (UK filtrado),
  `Approve`/`Reject`, segregación por `SEC_Users.Id`, presencia `OwnSession`/`InPersonPasskey`/`InPersonTotp`. Cosido a
  Inventario: `ApprovalPolicy.ModuloInventario` (`MotorDeAprobaciones.cs` l. 43, 74, 205), `IAlcanceDeInventario`,
  `IFuenteDeAprobacion.EnAlcanceAsync`, `ApprovalSubjects.Todos` cerrado (5) y `Inventory.Approval.AmountExceedsLimit`.
- **Usuario y persona.** `SEC_Users.PersonId` es opcional y sólo lo escribe `UpdateUserCommandHandler` (l. 44): la
  invitación no lo llena.
- **Pruebas reutilizables**: `LaSegregacionNoUsaUserIdDelToken`, `LosHechosInmutablesNoSeModifican`,
  `LaAuditoriaSeNombraPorElPublicIdDelTenant`, `LosEndpointsProtegidosExigenPermiso`.

**Habeas data**

- `CMP_HabeasDataPolicyVersions` (una vigente por cooperativa, SHA-256 del texto) y `CMP_HabeasDataConsents` (Accepted,
  Declined, Revoked, con `Channel` y `Notes`): **sin finalidad ni evidencia**. La 012 agregó `IAutorizacionDeDatos.VigenteAsync`
  y la escritura en el alta (`AltaConAutorizacion`) con la alerta `Personas.SinPoliticaDeDatos`.
- `RevokeConsentCommand(int PersonId, …)` revoca siempre; `HabeasDataRevokedEvent` no tiene consumidor; `GET
  …/consents/history/{personId:int}` usa el Id interno (Principio VI). No hay solicitudes ni reclamos del titular. El
  prefijo `CMP_` no está en la constitución (l. 447-448).
- Tablas heredadas `LND_Minutes`, `LND_MinuteAttendees` (códigos de texto, sin PublicId ni votos) y `LND_Blacklist`.

### 10.2 Decisiones

#### R129 · Catálogo `Lending.*` por acción, sembrado entero en K1

**Decision**
- `LendingPermissionCatalogSeeder` (`Identity/Seed`), hermano de `InventoryPermissionCatalogSeeder`: lo invoca
  `PhaseZeroSecuritySeeder` antes de `BuiltInRolesSeeder`; inserta lo que falta, actualiza descripciones, invalida la caché
  de permisos si inserta vínculos y nunca corre en el DbMigrator.
- Convención `Lending.{Recurso}.{Acción}`. Lectura con datos del deudor = `Read`; `View` sólo para catálogos, parámetros,
  tableros y conteos sin persona. Crear, decidir, ejecutar y anular son acciones separadas; cuatro códigos de decisión
  (`Decisions.Analyst`, `.Coordinator`, `.Management`, `.Exceptions`) para niveles individuales.
- **Se siembra entero en K1**, también lo de K2 a K6b. La lista canónica es la de §2.14 (configuración, originación,
  crédito y pagos, integración y procesos, riesgo, libranza, novedades, mensajes al deudor y cobranza, documentos e
  informes, migración y activación, alertas). La bandeja de integración y los archivos regulatorios traen deudores: son
  `Lending.Messages.Read` y `Lending.RegulatoryFiles.Read`.
- Otros prefijos de este frente: `Compliance.RestrictiveLists.View/Load/Release` y
  `Compliance.DataSubjectRequests.Read/Register/Respond` en `DomainPermissionCatalogSeeder`; `Core.ExternalChannels.View/Manage`.
- Toda ruta nueva lleva `.RequirePermission`; `Endpoints/Lending/*.cs` entra por glob a
  `LosEndpointsProtegidosExigenPermiso` y `Endpoints/Savings` queda exento por nombre. `LosCodigosViewDeCarteraNoTienenDatosPersonales`
  fija qué `.View` existen.

**Rationale**: FR-004 pide un permiso por acción y el catálogo sembrado. `*.View` llega a Operator, ReadOnly y Auditor en todo
módulo; `Read` cierra eso sin tocar los roles integrados. Sembrar entero evita que las plantillas cambien de forma entre
entregas. El 404 indistinguible ya existe.

**Alternatives considered**
- Reusar los códigos de `IdentitySeedData` (`Lending/LoanPortfolios/Read`…): son de otra tabla y no separan radicar de
  decidir.
- `.View` para créditos: los recibirían por glob los roles integrados.
- Permisos dinámicos por línea o nivel: `.RequirePermission` es fijo por ruta y la caché es por cooperativa.
- Sembrar por entrega: las plantillas cambiarían de forma.
- Nombres de S1 y F reemplazados (CH33): `Lending.Simulator.Use`, `Loans.View`, `Settlement.View`, `Accrual.*`,
  `ProcessRuns.*`, `Risk.View`, `NumberReservations.View`, `AccountingFacts.View`, `Bureau.Query`/`ViewReport`,
  `Documents.View`, `Regulatory.*`, `Disbursements.GenerateFile`, `PayrollFiles.*`, `Payrollers.*`, `DeductionFiles.*`,
  `Unapplied.Manage`, `LegalCollection.Manage`, `Collections.View`/`RegisterAction`, `PaymentAgreements.Manage`,
  `Lending.RegulatoryProfile.*`, `Lending.RestrictiveLists.*`, `Lending.Modalities.*`, `Lending.Messages.View` para el
  deudor (es `Lending.DebtorMessages.*`, CH32).

**Trazas**: T60 · FR-004, FR-005, FR-013, FR-138, SC-015; Principios III, VIII · S1, R-09 (CH32, CH33).

**Difiere del informe**: S1 y los demás informes usaban otros nombres para los mismos permisos y S1 ponía la bandeja
en `Lending.Messages.View` (CH32, CH33).

#### R130 · Roles de Cartera como plantillas y un comando que los crea

**Decision**
- Trece plantillas en `PerfilesSugeridos`, módulo `Lending` (`GET /api/admin/roles/templates?module=Lending`), cada una con
  su nota de alcance y montos:

  | Plantilla | Contenido principal (códigos de §2.14) |
  |---|---|
  | `cartera.administrador` | Todo `Lending.*` **salvo `Lending.Decisions.*` y `Lending.Sessions.Vote`** (C86); `Scope.AllOffices`, `Scope.AllLines` |
  | `cartera.asesor` | `Simulations.Run`, `Applications.Read/Create/Submit`, `CreditBureau.Query`, `Guarantees.Manage`, `Loans.Read`, `Payoffs.Read`, `Documents.Issue`, `Core.People.Create`, `Compliance.HabeasData.RecordConsent`; sus oficinas y líneas |
  | `cartera.analista` | `Applications.Read/Analyze`, `Decisions.Analyst`, `CreditBureau.ReadReports`, `Ratings.Read`, `Payoffs.Read` |
  | `cartera.jefe` | `Decisions.Coordinator`, `CreditLimits.Assign`, `Blocks.Manage`, `MonthlyClose.Execute`, `Novelties.Request`, `WriteOffs.Prepare`, `Sessions.Manage`, `Messages.*`, `Alerts.*`, `Disbursements.Execute`; `Scope.All*` |
  | `cartera.comite-credito` (secretaría) | `Sessions.Read/Manage/RecordVotes/Close`, `Applications.Read`, `RelatedParties.Read` |
  | `cartera.comite-riesgos` | `PortfolioEvaluations.Prepare`, `Ratings.Read`, `Sessions.*` |
  | `cartera.gerente` | `Decisions.Management`, `Decisions.Exceptions`, `Condonations.Request`, `ValidationFailures.Confirm`, `Payments.BackdateBeforeClose` |
  | `cartera.consejo` (secretaría del consejo) | `Sessions.*`, `RelatedParties.Read`, `WriteOffs.Apply`, `ImpairmentModel.Manage` |
  | `cartera.gestor-cobranza` | `Collections.Read/RecordActivity`, `PaymentAgreements.Create`, `Loans.Read`; sus oficinas |
  | `cartera.oficial-cumplimiento` | `Compliance.RestrictiveLists.View/Load/Release`, `RelatedParties.Read`, `Compliance.DataSubjectRequests.*`, `CreditReportClaims.*`, `AuditLog.View` |
  | `cartera.contador` | Todos los `.Read` y `.View`, `Reports.*`, `RegulatoryReports.Generate`, `CreditBureauFiles.Generate`, `Reconciliation.View`; `Scope.All*`; ninguna otra escritura |
  | `cartera.auditor` | Como el contador sin generar, más `AuditLog.View/Export/VerifyIntegrity` |
  | `cartera.revisor-fiscal` | Como el auditor, más `Sessions.Read` |

- `CreateSuggestedLendingRolesCommand` (`Security.Roles.Create`), idempotente: crea las que falten, una vez, como paso del
  asistente de activación. Después cada rol es de la cooperativa y se edita.
- No se tocan Auditor, Operator ni ReadOnly. A `CompanyAdmin` se le excluyen `Lending.Decisions.*` y
  `Lending.Sessions.Vote` con una lista de exclusión como `PermisosSaasGlobales` (F3): administrar no es aprobar crédito.

**Rationale**: la spec pide roles sembrados que cada cooperativa ajusta; `BuiltInRolesSeeder` reinserta vínculos en cada
arranque y no deja ajustar un rol integrado. La 012 resolvió lo mismo con plantillas (T48).

**Alternatives considered**: trece roles integrados (no se ajustan, se reinsertan); ampliar los globs de Operator (rompe la
segregación); sólo plantillas sin comando (la spec dice «sembrados»); `CrearRolesSugeridosDeCarteraCommand` (S2): T1.

**Trazas**: T61 · FR-004, US17-2; supuesto «Usuarios»; F2, F3 · S2.

**Difiere del informe**: S2 llamaba al comando `CrearRolesSugeridosDeCarteraCommand` (T1).

**Conciliación**: C86 — la plantilla `cartera.administrador` excluye `Lending.Decisions.*` y `Lending.Sessions.Vote`,
igual que `CompanyAdmin`: administrar no es aprobar crédito. C85 — `cartera.asesor` y `cartera.jefe` suman el permiso
nuevo `Lending.PrepaymentPreferences.Manage` (K1). C74 — los destinatarios por defecto de las alertas se cotejan con
`PerfilesSugeridos`.

#### R131 · Alcance por oficina y línea que falla cerrado

**Decision**
- `IAlcanceDeCartera` (`Application/Common/Interfaces/Security`) con el record `AlcanceDeCartera(TodasLasOficinas, Oficinas,
  OficinaPorDefecto, TodasLasLineas, Lineas)`; `AlcanceDeCarteraDeLaPeticion` en la API, memorizado por petición y total en
  segundo plano.
- **Oficinas**: `SEC_UserBranchAssignments` → `ADM_Branches` → `COR_Branches` por `TenantBranchPublicId`. El salto común se
  extrae a `OficinasDelUsuario`, que también usa `UserBranchScope` con su semántica abierta. En Cartera falla cerrado: sin
  filas, ninguna oficina, salvo `Lending.Scope.AllOffices`.
- **Líneas**: `LND_UserCreditLineScopes` (usuario, línea, por defecto), cerrada salvo `Lending.Scope.AllLines`;
  `PUT /api/lending/scopes/users/{userPublicId}` (`Lending.Scopes.Manage`).
- La oficina del crédito es la `COR_Branches` del crédito, la misma sucursal de sus hechos contables (T13).
- Toda consulta y reporte filtra con `FiltroDeAlcanceDeCartera` (`LasConsultasDeCarteraRespetanElAlcance`); todo comando
  valida oficina y línea y responde el 404 genérico; quien decide o desembolsa también necesita alcance. Los puertos para
  otros módulos (consultas, contrato, reservas) no aplican el alcance de Cartera: los autoriza el permiso de quien llama.
- En K1 la pantalla Agencias deja elegir el vínculo agencia → oficina, y el asistente de activación lista los usuarios con
  roles de Cartera sin alcance (F4).
- Leer oficinas, sucursales contables y el vínculo usuario–persona es una interfaz consumida que la lista cerrada suma por
  la enmienda A1.

**Rationale**: FR-004 y SC-015 exigen cero accesos fuera de oficina o línea. La oficina ya existe, la asigna Seguridad
(`Security.Users.AssignBranch`) y la usa Contabilidad. Fallar cerrado es lo que eligió la 012 (T35); las líneas no tienen
equivalente y llevan tabla propia, como `INV_UserWarehouseScopes`.

**Alternatives considered**: fallar abierto, como la 009 (incumple SC-015); `LND_UserOfficeScopes` (duplica
`SEC_UserBranchAssignments`); generalizar `IAlcanceDeInventario` (toca el motor de aprobaciones y los filtros de la 012 en
producción); un filtro global de EF (choca con el borrado lógico y los procesos); alcance de líneas por rol (no cabe en
`SEC_RolePermissions`).

**Trazas**: T62 · FR-004, FR-135, FR-138, US17-1, SC-015; A1, F4 · S3.

**Conciliación**: C68 — `COR_Alerts` suma oficina y línea para que las alertas respeten el alcance (SC-015). C84 —
las vistas con datos personales exigen `Lending.Reports.View` **más** `Lending.Loans.Read` para consultarlas y
`ExportPersonalData` para exportarlas.

#### R132 · Atribuciones por niveles con matriz versionada

**Decision**
- `LND_AttributionMatrices` (versión con vigencia, motivo y fuente; se sella en cada solicitud), `LND_AttributionLevels`
  (código, nombre, orden, clase individual o colegiada, código de permiso de la familia `Lending.Decisions.*` si es
  individual, órgano si es colegiada, techo de monto consolidado por sujeto) y `LND_AttributionRules` (sujeto, condiciones
  —línea o modalidad, rango de monto consolidado, plazo, clase de garantía, puntaje bajo el mínimo, alertas, excepción a la
  política, días de mora, concepto— y nivel mínimo).
- Sujetos (`LendingDecisionSubjects`, fijado en la sección 13): `CreditApplication`, `CreditLimit`, `Novelty`,
  `Condonation`, `WriteOffBatch`, `PortfolioEvaluation`, `ImpairmentModelChange`, `ActivationDifferences`,
  `ValidationFailureException`.
- `EnrutadorDeAtribuciones` (`Domain/Lending/Attributions`, puro) toma el nivel efectivo como el mayor entre: el del techo de
  monto consolidado, el de las reglas que casan, el consejo si hay persona vinculada (forzado, FR-040) y el de excepción si el
  puntaje está bajo el mínimo o hay alertas (FR-037). La solicitud va sola a ese nivel; no se acumulan. `NoExceptuables`
  rechaza siempre falta de capacidad de pago, falta de autorización del titular, coincidencia en listas y usura
  (`Lending.Attribution.NotExceptionable`).
- Cada decisión es un hecho `LND_CreditDecisions` (H): sujeto, fuente, nivel, versión de la matriz, resultado
  `CreditDecisionOutcome {Approve, ApproveWithConditions, Deny, Return}`, condiciones, SHA-256 de lo decidido y quién decidió
  (usuario o punto de sesión). Vence a `Originacion.DiasVigenciaDeAprobacion` (FR-042). Si el monto consolidado cambia entre
  radicar y decidir, la huella invalida la decisión y se reenruta (`Lending.Attribution.LevelRequired`).
- `SEC_PermissionAmountLimits` se usa sólo para las atribuciones **operativas** (`Disbursements.Execute`,
  `CreditLimits.Assign`, `ValidationFailures.Confirm`, `Payments.BackdateBeforeClose`) en `/api/lending/amount-limits`
  (`Lending.Attributions.Manage`, sólo códigos `Lending.*`); `PermisosLimitables` admite códigos por módulo.
- Pantalla `/cartera/atribuciones` con vista previa de a qué nivel iría una solicitud.

**Rationale**: FR-038 pide niveles de la cooperativa, condiciones más allá del monto, cuatro resultados y que la solicitud vaya
sola a su nivel. Una sola fuente de montos por clase de acto evita que un gerente con 50 M en crédito tenga el mismo límite
para condonar; la matriz siempre termina en un nivel colegiado, así nada queda sin quien decida.

**Alternatives considered**: montos de decisión en `SEC_PermissionAmountLimits` (no distinguen sujeto y abren bloqueos);
`SEC_Users.CanApproveLoansMin/Max` (por usuario, sin vigencia ni auditoría); niveles acumulativos como la 012 (contradice
«va sola a su nivel»); códigos de permiso dinámicos por nivel.

**Trazas**: T63 · FR-037, FR-038, FR-040, FR-042, FR-052, FR-058, FR-094; US2-5, US2-8, US17 · S4.

#### R133 · Actas propias de Cartera

**Decision**
- **Tablas**: `LND_GoverningBodies` (consejo, junta de vigilancia, comité de crédito, comité de riesgos, gerencia u otros;
  marca de si decide crédito; reglas de quórum y mayoría con vigencia; prefijo y consecutivo del acta);
  `LND_GoverningBodyMembers` (persona del maestro, cargo presidente/secretario/miembro/suplente, con o sin voto, vigencia,
  acta de nombramiento); `LND_Sessions` (número, ordinaria o extraordinaria, presencial/virtual/mixta, fecha de la sesión y
  fecha de registro separadas, estado borrador/abierta/cerrada, quórum exigido y presente, `ContentSha256` al cerrar, quién
  la cerró, PDF emitido); `LND_SessionAttendances`; `LND_SessionItems` (sujeto, fuente, `Decision` o `Recommendation`, regla
  `Majority` o `RelatedPartyQualified`, excluidos con motivo, resultado, condiciones); `LND_SessionVotes` (H, uno por persona
  y punto: a favor, en contra, abstención; registrado por la secretaría o por el miembro con su sesión o en presencia con
  passkey o TOTP por `IDesafiosDePresencia`).
- **Cálculo**: `EvaluadorDeVotacion` (`Domain/Lending/Sessions`, puro): quórum sobre la composición vigente (principales con
  voto); mayoría ordinaria por órgano con vigencia; vinculados en entidad con actividad financiera: 5 × favorables ≥ 4 ×
  composición, en aritmética exacta, con la fracción como límite legal de piso 4/5 que la cooperativa sólo endurece (T42) y la
  composición como base aunque haya excluidos. El radicador (por `SEC_Users.PersonId`) y los interesados no votan
  (`Lending.Session.VoterExcluded`); `Lending.Session.QuorumNotMet`; `Lending.Session.Closed`.
- **Cierre**: calcula cada resultado y, en la misma transacción, aplica cada punto a su fuente por `IAsuntoDeActa`
  (`AlDecidirAsync`, `DescribirAsync`), escribe `LND_CreditDecisions`, sella el SHA-256 y emite el acta
  (`/api/lending/sessions/{id}/minutes.pdf`). Un acta cerrada no se edita: se corrige con otra sesión.
- **Todo lo que la spec dice «con acta»** (decisiones colegiadas, vinculados, castigo, evaluación de cartera —recomienda el
  comité de riesgos, aprueba el consejo—, política SARC, adopción o desistimiento de la pérdida esperada, aceptación de
  diferencias de D17) referencia un `SessionItem` por su PublicId; no hay campos de texto «referencia del acta».
- **Defectos** «pendiente de validar» (F7): composición = principales con voto; el suplente vota sólo por un principal
  ausente; quórum = mayoría absoluta de la composición; mayoría ordinaria de los presentes elegibles; sin actividad
  financiera, la mayoría de los estatutos. Votos registrados por la secretaría por defecto; el miembro que sea usuario puede
  votar en el sistema (F8).
- Rutas `/api/lending/governing-bodies` y `/api/lending/sessions` (+ `{id}/attendance`, `items`, `items/{itemId}/votes`,
  `close`, `minutes.pdf`); pantalla `/cartera/actas`.

**Rationale**: FR-039 y FR-040 exigen sesión, asistentes, quórum, votos por solicitud o lote, condiciones, acta inmutable, voto
excluido del radicador y mayoría calificada sobre la composición; nada de eso existe. Los miembros son personas del maestro
(Principio V, I-1) porque los consejeros no suelen ser usuarios.

**Alternatives considered**: extender el motor de la 012 (R134); actas de plataforma `COR_` (sin segundo consumidor);
miembros siempre usuarios; reutilizar `LND_Minutes` (texto, sin PublicId ni votos); `LND_CommitteeSessions` sobre
`COR_Committees` (R-06: es el catálogo heredado de comités culturales, sin quórum ni composición con vigencia) y
`MinutesReference` de texto (R11, R15): CH21.

**Trazas**: T64 · FR-039, FR-040, FR-095, FR-102, FR-105, FR-141, D17; US2-5..US2-7, US14, US17-2; F7, F8 · S5 (CH21).

**Difiere del informe**: R-06 proponía `LND_CommitteeSessions` sobre `COR_Committees`; R11 y R15, una referencia de
acta como texto; S5 ponía el evaluador en `Domain/Lending/Actas` (CH21, CH41).

**Conciliación**: C75 — un voto mal registrado antes de cerrar el acta se corrige con un **voto de reemplazo** que
referencia al anterior; el único (punto, persona) se filtra a votos no reemplazados y `EvaluadorDeVotacion` cuenta el
vigente. C44 y C42 — las reglas de quórum y mayoría con vigencia van en la tabla nueva `LND_GoverningBodyRuleVersions`
(hecho). C48 — el prefijo del acta vive en `LND_GoverningBodies` y el consecutivo en `LND_NumberSequences`
(`SessionMinutes`); `NumeradorDeCartera` es el único que numera. C86 — `CompanyAdmin` y `cartera.administrador` no
tienen `Lending.Sessions.Vote`.

#### R134 · El motor `COR_Approval*` de la 012 no decide créditos

**Decision**: Cartera no crea `COR_ApprovalRequests`. Reutiliza de la plataforma la identidad (`IActorActual`, que compara
`SEC_Users.Id`), los límites de monto para lo operativo (`SEC_PermissionAmountLimits`, `LimitesDePermisoPorUsuario`), la
presencia (`IDesafiosDePresencia`, `IWebAuthnService`, TOTP de un solo uso), las alertas (`COR_Alerts`,
`IDestinatariosPorPermiso`), las vigencias (`COR_ParameterVersions`) y la auditoría encadenada. Sólo cambia
`PermisosLimitables`, que admite códigos por módulo; el motor no se toca. Vista «Mis pendientes» en `/cartera/aprobaciones`.

**Rationale**: el motor está cosido a Inventario (módulo fijo en tres sitios, `IAlcanceDeInventario`, la firma de
`IFuenteDeAprobacion`, sujetos cerrados, error `Inventory.Approval.AmountExceedsLimit`) y su semántica (umbral acumulativo,
un aprobador por nivel, dos resultados) no es la de FR-038/FR-039; corre en producción para I1–I6 con casos dorados.

**Alternatives considered**: generalizar el motor en E-05 (módulo parametrizable, alcance abstracto, niveles colegiados,
cuatro resultados): riesgo de regresión sobre lo productivo; queda como deuda si una tercera feature lo pide.

**Trazas**: T65 · FR-038, FR-039, FR-149; F1 · S6 (CH21).

#### R135 · Segregación por persona

**Decision**
- **Regla fija**: quien radica o solicita (solicitud, novedad, condonación, lote de castigo, cupo) no decide ni vota sobre lo
  suyo.
- **Pares configurables** en `COR_ParameterVersions` (módulo `LND`, clave `Segregacion.Incompatibilidades`, vigencia y
  motivo) con las acciones `Radicar`, `Analizar`, `Decidir`, `Votar`, `Desembolsar`, `AnularOrdenDeGiro`,
  `ArmarLoteDeCastigo`, `AplicarCastigo`, `CargarMigracion`, `AceptarDiferencias`. Activo por defecto `Decidir ×
  Desembolsar`, desactivable con motivo; los demás sugeridos e inactivos (F6).
- **Registro**: `LND_CaseParticipations` (H: clase y PublicId del expediente, acción, `SEC_Users.Id`, persona, instante), con
  un único escritor, `RegistroDeIntervenciones`, en la misma transacción del acto. El expediente une solicitud, crédito y
  desembolso.
- **Evaluación**: `ReglasDeSegregacion` (`Domain/Lending/Segregation`, puro) compara por `SEC_Users.Id` y, si los dos la
  tienen, por persona; responde `Lending.Segregation.Conflict` con la acción previa incompatible.
- Todo usuario que radique, decida, vote o desembolse debe tener `SEC_Users.PersonId`; si no,
  `Lending.Security.UserWithoutPerson` con instrucciones para vincularlo (F5).
- Las carpetas `Application/Lending/{Origination, Sessions, Attributions, Segregation}` y `Domain/Lending/{Attributions,
  Sessions, Segregation}` entran a `LaSegregacionNoUsaUserIdDelToken`.

**Rationale**: FR-005 pide una regla fija y otras configurables, comparadas por persona aunque tenga varios usuarios. Un solo
registro evita leer los actores de cinco tablas; las vigencias de la plataforma ya tienen lector único y auditoría. Para
votar no basta el usuario: se necesita la persona.

**Alternatives considered**: derivar las intervenciones de las columnas `*ByUserId` (frágil); tabla `LND_SegregationRules`
(duplica las vigencias); comparar sólo por `SEC_Users.Id` (no cubre a una persona con dos usuarios ni el voto); comparar por
correo (prohibido por la 012); carpetas en español `Originacion`, `Actas`, `Atribuciones`, `Segregacion` (CH41).

**Trazas**: T66 · FR-005, FR-039, US2-6, US17, SC-015; F5, F6 · S7 (CH41).

**Difiere del informe**: S7 nombraba las carpetas `Originacion`, `Actas`, `Atribuciones`, `Segregacion` (CH41).

#### R136 · Cartera en la cadena de auditoría

**Decision**
- `Lending`, `Compliance` y `NationalValues` entran a `AuditoriaEncadenada.Modulos` y a `AuditRetention.ModulosDeDiezAnios`;
  `ModuloDeAuditoria` gana las reglas «.Compliance», «.NationalValues» y «.Savings» antes de «.Core».
- **Contexto**: el evento lleva la oficina (marcador `IConOficina` con `BranchPublicId` → metadato `Office`, o la oficina por
  defecto del usuario) y el dispositivo (User-Agent más la cabecera opcional `X-Dispositivo` de MAUI) (F10).
- **Diff**: `LoanTransaction` **no** lleva `[SinDiffDeAuditoria]` (es la tabla del Principio XI y el atributo apagaría el
  antes y después de desembolsos, pagos y reversos). Lo llevan sólo `LoanDailyPosition`, `MonthlyCloseLoan` y
  `LND_ProcessRunErrors`, con un evento por cierre y por corrida (conteos, totales, huella). La causación del proceso la
  escribe `RegistroDeCartera` en bloque, fuera del `ChangeTracker`, con un evento resumen por tanda y por corrida: única
  excepción al diff por fila (T101). El evento de cada `LoanOperation` lleva la distribución completa por cuota y concepto.
- **Lecturas sensibles**: `LendingAuditEmitter` (molde `InventoryAuditEmitter`) con lista fija: liquidación a una fecha (con
  D, R y huella), resultados de centrales, reimpresión y descarga de documentos, exportaciones (`Lending.Report.Exported`),
  actas y vinculados, y la consulta de cupo de Comercial, cuyo `EventId` es el `EvidenceId`. Lo fija
  `LasLecturasSensiblesDeCarteraSeAuditan` (eventos en la sección 13).
- **Rechazos**: las reglas regulatorias (usura, límites, segregación, alcance, autorización) fallan en el handler y quedan
  como rechazo auditado. Los de forma también se auditan (F9): como `ValidationBehavior` corre antes que `AuditBehavior`, el
  metadato `.AuditarIntentoDenegado(ModuloDeAuditoria.Lending)` del grupo `/api/lending` registra `Lending.Request.Rejected`
  (comando, campos y códigos, sin valores) cuando la respuesta es un rechazo de validación, y `Lending.Access.Denied` antes
  del 404 (por `RegistrarAsync`, sin propagar errores). Las consultas fuera de alcance emiten `Lending.Access.OutOfScope`.
- **Verificación**: la de la plataforma (`AuditLog.VerifyIntegrity`), sin trabajo nocturno.

**Rationale**: FR-003 («incluidas las fallidas y rechazadas»), SC-014 (100 %), el Principio X y FR-149 («la auditoría
encadenada incluye a Cartera»). Un diff por cada transacción de la causación diaria saturaría la bandeja sin valor.

**Alternatives considered**: dejar Cartera fuera de la cadena (incumple E-05); diff por transacción; auditar todo 404 de la
API (ruido); no auditar los rechazos de forma (propuesta de S8, resuelta por F9); verificación nocturna (no la pide la spec).

**Trazas**: T67 · FR-003, FR-059, FR-138, FR-149, SC-014; Principio X; F9, F10 · S8, R4.

**Difiere del informe**: S8 no auditaba los rechazos de forma; la armonización los audita (F9).

**Conciliación**: C89 — la lista cerrada de lecturas sensibles es la de la sección 13 de este archivo, y la
liberación de una coincidencia es `Lending.ScreeningMatch.Released`; si A10 se rechaza, `ModuloDeAuditoria` suma una
regla por nombre que lleva los handlers de ahorros a `Savings` antes de la de `.Lending`. C46 — `[SinDiffDeAuditoria]`
alcanza además a `LoanControl`, `DebtorControl`, `LND_AccountingFactParts`, `LND_LoanArrearsHistory`,
`LND_PayrollDeductionFileLines`, `LND_RegulatoryFileLines` y las 17 tablas de preparación, cada una con su evento
resumen por corrida, archivo o importación (T101 y Complexity Tracking).

#### R137 · Auditoría del contrato síncrono: el intento fuera, el hecho dentro

**Decision**
- **Antes**: `Lending.ObligationContract.Attempted` por un contexto aparte (`ITenantDbContextFactory`, como
  `RechazoAsync`), con la clave y el recaudador; sobrevive a cualquier reversión.
- **Dentro** de la transacción del recaudador: `Lending.ObligationContract.Applied` (o `Reversed`, `CreditBalanceResolved`,
  `Rejected`) en `COR_AuditOutbox`, con la distribución completa por cuota y concepto.
- Igual para la reserva: `Lending.NumberReservation.Reserved` / `.Released` dentro de la transacción de Comercial.
- El desenlace «revertido junto con el recaudador» **se deriva**: un intento sin hecho con la misma clave. La consulta de
  auditoría muestra los dos eventos juntos filtrando por `OperationKey` (enmienda A11).
- Si el dueño rechaza A11: `cartera.vigilancia` escribe `Lending.ObligationContract.RolledBack` por cada intento sin hecho
  pasado su plazo.

**Rationale**: I-4 pide registrar el intento y su desenlace, y Cartera no puede saber al volver si el recaudador confirmará;
derivarlo no exige ganchos en el pipeline de otro módulo.

**Alternatives considered**: `TareasTrasElCommit.AlTerminar(bool confirmado)` con una bitácora del contrato (DE-02): toca el
pipeline de la plataforma y acopla Cartera a lo que pase tras el commit de otro módulo (CH18); auditar sólo dentro de la
transacción (se pierde el intento revertido).

**Trazas**: T68 · FR-003, FR-057, FR-088, I-4; A11 · S9, DE-02 (CH18).

**Difiere del informe**: DE-02 proponía `TareasTrasElCommit.AlTerminar` con una bitácora del contrato (CH18).

#### R138 · Habeas data por finalidad; reclamos del reporte en Cartera

**Decision**
- **(a) Autorizaciones, en la plataforma.** Columnas aditivas en `CMP_HabeasDataConsents`: `Purpose`
  (`DataAuthorizationPurpose {GeneralTreatment, CreditProcessing, CreditBureauQuery, CreditBureauReport, ContactChannel}`;
  las filas existentes quedan en `GeneralTreatment`), `ContactChannel` (SMS, WhatsApp, correo, llamada, visita),
  `EvidenceAttachmentPublicId` (adjunto `DataAuthorization`) y `Origin` (alta de persona, solicitud de crédito, migración,
  solicitud del titular, importación); en `CMP_HabeasDataPolicyVersions`, `Purpose` (una versión vigente por cooperativa y
  finalidad). `IAutorizacionDeDatos` gana `VigenteAsync(persona, finalidad, canal?)` (si autoriza, si hay evidencia, qué
  versión) y `Agregar(...)`, que no guarda: la radicación la escribe en su transacción, como la 012 en el alta. Una
  autorización migrada sin evidencia deja el crédito vivo y cobrable y bloquea sólo la consulta y el reporte a centrales y
  los mensajes de cobranza (`Lending.Authorization.Missing`, `.WithoutEvidence`); gestiones, etapas y cobro jurídico siguen.
- **(b) Solicitudes del titular, en la plataforma**: `COR_DataSubjectRequests` (consulta, actualización, rectificación,
  supresión, revocación) con plazos en días hábiles nacionales (`PlazosDelTitular`, `ICalendarioHabil`): consultas 10 + 5,
  reclamos 15 + 8, leyenda en 2, como límites legales con fuente «pendiente de validar» (F11). `ReglaDeRevocacion`
  (`Domain/Common/Compliance`) por finalidad: `ContactChannel` procede siempre; las de crédito no proceden mientras exista un
  deber, que se pregunta a `IConsultaDeObligaciones` y deja constancia (Cumplimiento como consumidor, A1).
- **(c) Reclamos sobre el reporte, en Cartera**: `LND_CreditReportClaims` (`CreditReportClaimKind {Claim, Impersonation}`;
  estados `InProcess`, `Answered`, `TacitlyAccepted`, `Withdrawn`), marca «reclamo en trámite» y aceptación tácita por el
  ciclo diario con alerta (K4; la tabla nace en K1 para lo migrado). La lee el generador del archivo de centrales.
- **(d) Rutas por PublicId**: `/api/compliance/habeas-data/authorizations?personPublicId=` y
  `/api/compliance/data-subject-requests`; las de `{personId:int}` quedan en desuso (Principio VI). Pantalla
  `/compliance/solicitudes-del-titular`.

**Rationale**: un solo registro de autorizaciones por persona evita dos verdades (la 012 ya escribe ahí en el alta). Los
derechos del titular son sobre sus datos del maestro (Principio V); el reclamo del reporte nace y muere en el archivo de
centrales, que es de Cartera. FR-006 exige finalidad, evidencia, versión y revocación con deber legal.

**Alternatives considered**: todo en Cartera (`LND_DataAuthorizations`, `LND_DataSubjectRequests`, R-06): duplica el
consentimiento (CH23); todo en la plataforma, incluidos los reclamos (la plataforma conocería episodios y obligaciones);
tablas nuevas en vez de columnas (rompe `VigenteAsync`); `CMP_DataSubjectRequests` (S10): el prefijo `CMP_` no está en la
constitución (CH23).

**Trazas**: T69 · FR-006, FR-036, FR-122, FR-123, FR-137, FR-139, SC-012, US2-4, US17-4, US17-5; A1, F11 · S10 (CH23).

**Difiere del informe**: S10 creaba `CMP_DataSubjectRequests`; R-06, tablas `LND_DataSubject*` (CH23).

**Conciliación**: C85 — `/cartera/autorizaciones` lee por `IAutorizacionDeDatos` con los permisos existentes
`Compliance.HabeasData.ViewHistory` y `RecordConsent`; no hay `Lending.*` para ella. C68 — los únicos de
`CMP_HabeasDataPolicyVersions` se rehacen con `Purpose`.

#### R139 · Listas restrictivas en la plataforma; el resultado en Cartera

**Decision**
- **Carga provisional de plataforma**: `COR_RestrictiveListLoads` (lista OFAC, ONU, PROPIA…, fuente, publicación, vigencia,
  versión, estado activa/reemplazada, SHA-256, filas, quién cargó) y `COR_RestrictiveListEntries` (H: carga, clase de
  entidad, tipo y número de documento normalizado, nombre y nombre normalizado, alias, programa, notas); no son personas del
  maestro. Rutas `/api/compliance/restrictive-lists` (+ `template.xlsx`, `import`), `Compliance.RestrictiveLists.View/Load`,
  plantilla `compliance.restrictive-lists`, pantalla `/compliance/listas-restrictivas`.
- **Consulta** por `IListasRestrictivas` (`Application/Common/Compliance`) con el canal `CARGA-PROPIA`
  (`CanalDeListasCargadas`, vale en producción): coincidencia exacta por documento (índice) y posible por nombre normalizado
  con `CoincidenciaDeNombres` (`Domain/Common/Compliance`), umbral `Listas.UmbralDeCoincidenciaDeNombre`, en memoria (SQL
  Server no tiene `pg_trgm`).
- **Resultado en Cartera**: cada consulta (radicación, venta, cupo) queda en `LND_ScreeningResults` (persona, operación,
  versión de las listas, resultado, liberación). Toda coincidencia bloquea y alerta `Cartera.CoincidenciaEnListas`. Sólo
  libera quien tenga `Compliance.RestrictiveLists.Release`, con motivo (`ReleaseScreeningMatchCommand`,
  `/api/lending/screening-results/{id}/release`), y no puede ser quien radicó. Sin destinatario, la alerta va a CompanyAdmin,
  como en la 012.
- `LND_Blacklist` no se reutiliza (se conserva intacta).
- Cada cooperativa carga las suyas; al cargar una versión nueva se re-escanea de noche a deudores y codeudores vivos, sólo
  con alerta al oficial (I12).

**Rationale**: el oficial de cumplimiento no es un rol de Cartera y Comercial y Aportes reusarán la carga; el resultado y su
liberación son de la operación de Cartera. La spec pide una carga con vigencia mantenida por el oficial, consultada por el
adaptador.

**Alternatives considered**: todo en `LND_` con `Lending.RestrictiveLists.*` y `ReleaseRestrictiveListMatchCommand` (S11):
CH24; `COR_RestrictiveListEntries` sobre el catálogo heredado `COR_ListParameters` (R-06): CH24; valor nacional replicado
(la spec dice que cada cooperativa las mantiene); reutilizar `LND_Blacklist` (texto, sin vigencia ni PublicId); liberar por el
nivel de atribución (no admite excepción).

**Trazas**: T70 · FR-016, FR-036, FR-038, FR-049, US2-3; I12 · F-18, S11 (CH24).

**Difiere del informe**: S11 lo ponía todo en `LND_` con `Lending.RestrictiveLists.*` y
`ReleaseRestrictiveListMatchCommand`; R-06 colgaba la carga de `COR_ListParameters` (CH24).

**Conciliación**: C84 — la lista y el detalle de coincidencias exigen `Compliance.RestrictiveLists.Release`. C89 — la
liberación se audita como `Lending.ScreeningMatch.Released`. C90 — al cargar una versión de listas corre la tarea
`cartera.re-escaneo-de-listas`: alerta y bloqueo de originar y vender, **no** del cobro. C31 — la consulta de cupo
verifica listas sin escribir; el resultado queda en la reserva y al consumir la venta. C42 — `ScreeningResult` es
hecho con la liberación `[EscrituraUnica]`.

### 10.3 Riesgos

| Riesgo | Mitigación |
|---|---|
| Un `Lending.*.View` mal nombrado filtra datos personales a todos los roles integrados | `LosCodigosViewDeCarteraNoTienenDatosPersonales` |
| Nadie corre el comando de roles y nadie tiene roles de Cartera | La activación en producción lo exige (T84) |
| El rol integrado Auditor no ve créditos (`.Read` no casa con `*.View`) | Plantillas `cartera.auditor` y `cartera.revisor-fiscal` |
| Alcance cerrado: un usuario sin asignaciones, o con oficinas sin vínculo a una agencia contable, no ve nada | Selector agencia–oficina en K1; el asistente de activación lista los usuarios sin alcance (F4) |
| Resolver alcance, permisos y límites en cada petición suma consultas | Memorizado por petición |
| Matriz de atribuciones compleja; el monto consolidado cambia entre radicar y decidir | Casos dorados, vista previa de nivel y reenrutamiento por huella |
| Suplentes y composición de los órganos; usuarios sin persona vinculada | Validación del dueño (F7); `Lending.Security.UserWithoutPerson` y vinculación antes de activar (F5) |
| `Decidir × Desembolsar` frena la operación diaria de una cooperativa pequeña | Desactivable con motivo; aviso en la activación |
| Dos bandejas (aprobaciones de inventario y decisiones de cartera); la spec cita «motor de aprobaciones» | Vista «Mis pendientes» en `/cartera/aprobaciones`; lectura sometida en F1 |
| Volumen de auditoría: consulta de cupo varias veces por venta, ráfagas de denegaciones | Una fila por llamada; limitador de la API |
| La consulta de auditoría debe mostrar juntos el intento y el hecho del contrato | Filtro por `OperationKey` |
| La lista cerrada no nombra a Cumplimiento; los plazos del titular dependen de los festivos; `CMP_` no está en la constitución | Enmienda A1; festivos nacionales replicados (E-05); sólo columnas en las dos tablas `CMP_` existentes |
| Falsos positivos por homónimos; nadie con el permiso de liberar | Umbral parametrizado; sin destinatario, la alerta va a CompanyAdmin |

---

## 11. Frente de adaptadores, mensajería, archivos, documentos, informes y pantallas

### 11.1 Hechos del código

**Adaptadores, mensajería, archivos y documentos**

- **Molde de la DIAN.** `ICanalDeEmisionElectronica` (`ChannelCode`, `Capacidades`, operaciones con `ResultadoDeCanal`
  uniforme, `ProbarAsync`); `ICanalesDeEmision` resuelve por el código **sellado** y lanza sin adaptador; `CanalesDeEmision`
  impide dos adaptadores del mismo código. El adaptador nunca lanza por rechazo de negocio ni deja subir excepciones de
  transporte. `CanalSimulado` decide por el último dígito de la identificación y marca «SIMULADO»;
  `GuardiaDeEmisionFiscal` lo bloquea en ambiente DIAN Production (`ElectronicInvoicing.Readiness.SimulatedInProduction`);
  `ca3cd434` permite verificarlo sin credencial. Pruebas: `ElProveedorTecnologicoSoloLoConoceSuAdaptador`,
  `LasCredencialesDeFacturacionNoTocanLaBase`, `ConformidadDelCanal`.
- **Credenciales.** `CredencialesEnArchivo` lee `/secrets/facturacion-electronica/{tenantPublicId}.{channelCode}.json`,
  caché de 5 min por huella, `CredentialKey` en la base; acoplada a `ElectronicEmissionSettings` y con métodos `internal`.
- **Notificaciones son para usuarios.** `NotificationPayload(RecipientUserPublicId, …)`; `NotificationChannels` =
  `InApp`, `Email`; `COR_Notifications.EmailStatus` mutable; `NotificationEmailDispatcher` (arrendamiento `email.dispatch`)
  resuelve el correo del usuario. Para personas sin usuario se usa `IEmailSender` directo (`PayslipEmailDispatcher`,
  `EntregaAlComprador`). `COR_People` tiene `Phone1`, `Phone2`, `Mobile`, `Email`; sin WhatsApp ni autorización por canal.
  `COR_References` guarda referencias personales (la Ley 2300 prohíbe contactarlas).
- **`COR_NotificationTemplates` sin uso**: sólo su configuración y el `DbSet`; sin versión, vigencia ni módulo. Los correos
  usan `.cshtml` con `{{Clave}}`.
- **Motor de archivos planos.** `COR_BankFileFormats` con vigencia (`BankId` nulo = genérico, `Scope`, `Kind`, `Encoding`…);
  `BankFileScope` = `PayrollDisbursement = 1`, `SeveranceDeposit = 2`, `SupplierPayments = 3`; `FlatFileWriter` puro, sólo
  escribe, devuelve SHA-256; `SolapadoAsync` compara `BankId` y `Scope` con nulos (dos genéricos del mismo ámbito no se
  cruzan); `InUse` congela un formato usado.
- **PILA como precedente de layouts**: `PilaLayoutCatalog` (JSON embebido con vigencia, `verified = false` por campo),
  advertencia `Pila.LayoutSinCotejar`, `PilaGeneration` inmutable por versión. `PilaLayout`/`PilaWriter` son específicos.
- **PDF**: QuestPDF sólo en API y Audit; interfaz en Application e implementación en `API/Reports`; QRCoder en
  `RepresentacionGraficaReport`; sin logotipo por cooperativa. No hay código de verificación de documentos; sólo el HMAC
  de `IAuditSignatureService` para el PDF de auditoría, verificado a nivel SaaS.
- **Centro de informes de la 012**: `TablaExportable` (columnas `_` ocultas), `FormatosDeInforme`, registro de vistas
  `GET /api/reports/inventory` leído de la metadata y filtrado por permiso; `MapVistaDeInventario` con permiso de ver,
  `RequirePermissionWhenExporting`, `ExportPersonalData`, rango de 5 años y una auditoría por exportación.
- **Importaciones**: `EjecutorDeImportacion` (revisión y aplicación todo o nada en un `SaveChanges`, permisos por hoja y
  columna, topes 16 MiB, 60.000 filas por hoja, 1.000 errores, 5.000 cambios; `.csv` sólo en una hoja; audita SHA-256).
  `RutasDePlantilla.MapPlantilla` está atada al catálogo de Inventario y fija `Inventory.Reports.ExportPersonalData`.
  Ninguna importación crea personas.
- **Adjuntos**: `AdjuntosDeModulo` decide lectura, subida y borrado; los tipos de módulo no se borran; descarga por enlace de
  60 s.
- **Pantallas**: `IndicadorDeCarga`, `EstadoDeCarga`, `PermissionGate` (`Required`, `AnyOf`, `Fallback`, `MostrarAviso`),
  `PersonSearchPicker`, `PersonaDialog`. `ModulosMigrados` = Maestros, Nomina, Asociados, Contabilidad, Inventario, Ventas,
  Compras, Pos. `TodoEnlaceDelMenuTieneSuPagina` revisa también los `NavigateTo` del centro de reportes;
  `LasPantallasDelCicloComercialEstanEnElMenu` es el molde de prueba por página, ruta y permiso.
- **Plataforma pendiente**: `EmisorDeMensajes` fija `ModuloDeOrigen = "INV"` (l. 55); `COR_Sequences` heredada sin uso;
  `COR_Companies` no tiene `PersonId`; `COR_EmployerCompanies` y `COR_LegalAdvisors` duplican datos de persona.

### 11.2 Decisiones

#### R140 · Adaptadores externos por puerto, registro y canal simulado

**Decision**
- Cuatro puertos con el molde de la DIAN:

  | Puerto · registro | Dónde | Canales | `SIMULADO` (último dígito del documento) |
  |---|---|---|---|
  | `ICanalDeMensajeria` · `ICanalesDeMensajeria` | `Application/Common/Messaging` (plataforma) | `CanalDeCorreo` (sobre `IEmailSender`), SMS y WhatsApp por proveedor, `MensajeriaSimulada` | 1 número inválido; 2 entregado en la consulta siguiente; 3 fallo de entrega; 5 no disponible |
  | `IDepositoDeValores` · `IDepositosDeValores` | `Application/Lending/Adapters` | proveedor, `DepositoDeValoresSimulado` | 1 firma rechazada; 3 firma pendiente; 5 no disponible |
  | `ICentralDeRiesgo` · `ICentralesDeRiesgo` | `Application/Lending/Adapters` | por central (`BureauCode`), `CentralDeRiesgoSimulada` | 1 reporte negativo; 2 sin historia; 3 puntaje bajo; 5 no disponible |
  | `IListasRestrictivas` · `IProveedoresDeListas` | `Application/Common/Compliance` (plataforma) | `CanalDeListasCargadas` (`CARGA-PROPIA`), `ListasSimuladas` | 8 posible coincidencia; 9 coincidencia |

- Cada puerto expone `ChannelCode`, `Capacidades` y `ProbarAsync`; sus operaciones devuelven `ResultadoExterno<T>` con
  `ExternalOutcome {Ok, Rejected, InProcess, NotFound, ChannelUnavailable, InvalidData}`; nunca lanzan por rechazo de negocio
  ni por transporte. El registro resuelve por el código **sellado** en la operación. Todo simulado marca «SIMULADO» en
  referencias y artefactos.
- Adaptadores en el proyecto nuevo `src/Infrastructure/IngenIA365ERP.ExternalChannels` (`Messaging`, `Securities`,
  `CreditBureaus`, `RestrictiveLists`, `Credentials`, `Circuit`). Sólo `Program.cs` llama `AddExternalChannels`, después de
  `AddApplication`, que deja los defectos «sin canal».
- Baterías de conformidad `ConformidadDeMensajeria`, `ConformidadDelDepositoDeValores`, `ConformidadDeCentral`; prueba
  `LosProveedoresExternosSoloLosConoceSuAdaptador`.

**Rationale**: la spec (supuestos, FR-044, FR-122) pide pasar a canal real con configuración y contrato, sin código nuevo en
Cartera; es la decisión T40 de la 012, probada en producción con la DIAN. Resultados uniformes permiten una batería por
puerto.

**Alternatives considered**: adaptadores dentro de `IngenIA365ERP.ElectronicInvoicing` (mezcla la DIAN con terceros); un
proyecto por adaptador; `HttpClient` desde Application (Principio II); puerto `ICalendarioDeFestivos` (F-05): duplica
`ICalendarioHabil` (CH34).

**Trazas**: T71 · FR-036, FR-044, FR-122, FR-137, FR-149 · F-01 (CH34).

**Difiere del informe**: F-05 proponía el puerto `ICalendarioDeFestivos` (CH34).

#### R141 · Configuración por cooperativa y credenciales fuera de la base

**Decision**
- `COR_ExternalChannelSettings` (clase `ExternalChannelKind {Sms, WhatsApp, SecuritiesDepository, CreditBureau,
  RestrictiveLists}`, `BureauCode` para centrales, `ChannelCode`, vigencia, `CredentialKey`, verificación con quién y cuándo)
  con `ConfigureExternalChannelCommand` y `VerifyExternalChannelCredentialCommand` (llama `ProbarAsync`) en
  `/api/core/external-channels` (+ `verify`), `Core.ExternalChannels.View/Manage`, pantalla `/admin/canales-externos`.
- Credenciales en el Secret `erp-canales-externos`, montado como directorio sin `subPath` en
  `/secrets/canales-externos/{tenantPublicId}.{channelCode}.json`. Las lee `CredencialesDeCanalExternoEnArchivo` con el
  algoritmo de la DIAN: ruta derivada sólo de la cooperativa resuelta y de un código validado, caché de 5 minutos por huella
  y error si `CredentialKey` no coincide. `SIMULADO` y `CARGA-PROPIA` no piden archivo. Guion
  `tools/scripts/crear-secreto-canales-externos.ps1` (en producción pide escribir PRODUCCION). Prueba
  `LasCredencialesDeCanalesExternosNoTocanLaBase`.
- Circuito en memoria por cooperativa y canal, `CircuitoDeCanalExterno`, con umbral `Canales.UmbralDeFallas` y alerta
  `Cartera.CanalExternoNoDisponible`.
- Cada cooperativa contrata sus proveedores, con credencial propia por canal (I1).

**Rationale**: el precedente de la DIAN (`contracts/dian.md` §11 de la 012) deja los secretos fuera de la base y del
repositorio y aísla a cada cooperativa. `CredencialesEnArchivo` depende de `ElectronicEmissionSettings` y su caché es
`internal`.

**Alternatives considered**: reutilizar `CredencialesEnArchivo` (acoplada); claves en `COR_ParameterVersions` (sin
`CredentialKey` ni verificación); AWS Secrets Manager con Roles Anywhere (evolución futura); refactorizar ya el código de la
DIAN hacia un lector común (toca producción).

**Trazas**: T72 · FR-003, FR-149; Principio IV; I1 · F-02.

#### R142 · Guardia única del efecto legal de los canales simulados

**Decision**
- `GuardiaDeCanalesExternos` (`Application/Lending/Adapters`) es el único sitio que decide.
  `CanalesExternosOptions.AmbienteDePruebas` sale de `!builder.Environment.IsProduction()` en `Program.cs`, como
  `PuestaEnMarchaOptions`.
- En un despliegue de producción, un resultado `SIMULADO` nunca perfecciona un pagaré desmaterializado, nunca cuenta como
  constancia de aviso previo ni como evidencia de centrales para aprobar, y nunca envía un mensaje real (queda con el evento
  `Simulated`). La operación que lo necesita responde `Lending.Adapter.SimulatedInProduction` y dice qué configurar.
- Configurar `SIMULADO` en producción se admite **antes y después** de activar (spec: «canal simulado hasta que la cooperativa
  tenga contrato»). Una cooperativa activada sin contratos —COOFLOPAL— opera con pagaré físico, aprueba sólo en las líneas sin
  «exige consulta a centrales», manda los mensajes por correo (SMS y WhatsApp quedan simulados) y registra la constancia del
  aviso previo a mano (I4).
- `CARGA-PROPIA` no es simulado y vale en producción. Fuera de producción todo documento lleva «AMBIENTE DE PRUEBAS – SIN
  VALIDEZ».
- Tres conceptos con nombres distintos (CH20): **dobles de prueba** de FR-089 (`TesoreriaDePrueba`, `RecaudadorDePrueba`;
  nunca en producción: la API no arranca), **canales simulados** (`SIMULADO`, sin efecto legal) y **dobles unitarios** (sólo en
  `tests/`). Un solo criterio de producción: `IHostEnvironment.IsProduction()` (B4).

**Rationale**: una evidencia falsa con efecto legal expone a sanciones (Leyes 1266 y 2157); prohibir el simulado tras activar
dejaría sin canal a quien todavía no contrata. Precedente: `ElectronicInvoicing.Readiness.SimulatedInProduction`.

**Alternatives considered**: prohibir el simulado en producción (impide ensayar); permitirlo con efecto (riesgo legal);
decidirlo por cooperativa (el carácter de producción lo da el despliegue, FR-142); admitirlo sólo antes de activar (F-03):
CH20.

**Trazas**: T73 · FR-036, FR-043, FR-044, FR-122, FR-123, FR-142; B4, I4 · F-03 (CH20).

**Difiere del informe**: F-03 admitía el simulado en producción sólo antes de activar (CH20).

#### R143 · Mensajería saliente a personas y cola de mensajes al deudor

**Decision**
- La plataforma ofrece `ICanalDeMensajeria` con `CanalDeCorreo` (sobre `IEmailSender`, admite adjuntos), SMS y WhatsApp.
  `NotificationChannels` no se amplía y `COR_Notifications` no se usa para deudores (I14).
- Cartera es dueña de `LND_DebtorMessages`, inmutable desde que nace: plantilla y versión, copia del cuerpo renderizado,
  canal, propósito (`DebtorMessagePurpose {Collection, PriorNotice, Informative, DocumentDelivery, CollectionFeeNotice}`),
  persona destinataria, copia del destino (I-1), instante pedido, crédito, campaña, autorización usada, clave
  `{origen}:{entidad}:{propósito}:{fecha}` y código de canal sellado. Su historia va en `LND_DebtorMessageEvents`, sólo
  inserción (`DebtorMessageEventKind {Queued, Deferred, Excluded, Sent, Delivered, Failed, Simulated, Cancelled}`).
- Sólo el deudor y los codeudores obligados son destinatarios (`Lending.DebtorMessage.RecipientNotObligated`); las referencias
  personales nunca (`LaCobranzaNuncaContactaReferencias`). El destino se lee de `COR_People` al enviar y se copia.
- Rutas `/api/lending/debtor-messages` (+ `{id}/cancel`), `Lending.DebtorMessages.Read/Send/Cancel`; pantalla
  `/cartera/mensajes`. Nace en K1: los informativos de D24 (abono aplicado) y de FR-112 (no descontado por la nómina propia)
  ya existen en K1.

**Rationale**: I-5 incluye los mensajes emitidos en el Principio XI y `COR_Notifications` es mutable (`EmailStatus`) y de
usuarios. La Ley 2300 regula la cobranza, que es de Cartera; los canales los reutilizarán Aportes y Comercial.

**Alternatives considered**: ampliar `COR_Notifications` con persona y banderas de SMS y WhatsApp (mezcla notificaciones
internas con comunicaciones reguladas, mutable); construir SMS y WhatsApp dentro de Cartera (contradice E-05);
`/api/lending/messages`, `Lending.Messages.*` y `Lending.Message.*` para el deudor (F-04, F-05): son de la bandeja de
integración (CH32).

**Trazas**: T74 · FR-003, FR-075, FR-112, FR-115, FR-122, FR-149, I-1, I-5, SC-011; I14 · F-04 (CH32).

**Difiere del informe**: F-04 y F-05 usaban `/api/lending/messages`, `Lending.Messages.*` y `Lending.Message.*` para
el deudor (CH32).

**Conciliación**: C57 — la copia del destino va en el evento `Sent` (`LND_DebtorMessageEvents.DestinationAddress`),
no en el mensaje, que no cambia tras nacer; «se copia» se lee así. C87 — `POST /simulations/schedule/send` admite a
la persona simulada con autorización y correo, sólo por correo y con propósito `Informative`. C90 — `Letter` no exige
autorización de canal; `Call` y `Visit` sólo son gestiones (K4).

#### R144 · Ventana legal de contacto y despacho por sondeo

**Decision**
- `VentanaLegalDeContacto` (`Domain/Lending/Collections`, puro). Entradas: instante local pedido, canal, propósito, historia
  de contactos del titular en la semana calendario (lunes a domingo, America/Bogota: mensajes enviados y gestiones con
  contacto directo) y el horario efectivo por día = techo nacional (límites `LIMITE` y parámetros
  `Cobranza.HorarioLunesAViernes`, `Cobranza.HorarioSabado`, `Cobranza.ContactosPorDia`) ∩ restricción de la cooperativa,
  festivos nacionales (`ICalendarioHabil`) y banderas del titular (autorización por canal, fallecido con reclamación al
  seguro, insolvencia, cobro suspendido). Salida: `Permitido(instante)`, `Diferido(siguiente, motivos)` o `Excluido(motivo)`.
- Reglas: nunca domingos ni festivos; a lo sumo un contacto por día; tras un contacto directo, en esa semana sólo el canal de
  ese contacto (I2). Se evalúa al programar y otra vez al despachar. Un envío manual inmediato fuera de ventana se rechaza
  (`Lending.DebtorMessage.OutsideLegalWindow` con `nextAllowedAt`; también `.WeeklyChannelUsed` y
  `.ChannelNotAuthorized`); uno programado o de campaña se difiere con el evento `Deferred`.
- Por propósito: `Collection` y `PriorNotice` aplican todo; `Informative` respeta horario y tope diario y no fija canal;
  `DocumentDelivery` pedido por el titular queda exento de las ventanas y exige autorización de tratamiento y correo registrado
  (I3). Casos dorados en `Domain.Tests/Lending/Collections/Casos`.
- **Despacho**: `cartera.mensajes` (`ITareaProgramada`, cada minuto) toma los `Queued` y `Deferred` vencidos, los reevalúa,
  los envía por el adaptador sellado y registra el evento con `RegisterDebtorMessageAttemptCommand` (sólo por comandos).
  `ChannelUnavailable` deja el mensaje en cola; los reintentos (`Mensajes.Reintentos`) caen sólo en ventanas permitidas; un
  fallo definitivo alerta `Cartera.MensajeAlDeudorFallido`. `cartera.entregas` consulta el estado de entrega al proveedor
  (`ConsultarEntregaAsync`). La referencia ante el proveedor es el PublicId del mensaje. **No hay webhooks.**

**Rationale**: FR-122 y SC-011 exigen cero envíos fuera de las reglas, verificable sobre el registro; un motor puro se prueba con
casos dorados; reevaluar al despachar cubre un contacto directo registrado después de programar. Un webhook sería una ruta
anónima sin cooperativa resuelta (Principio IV, mismo razonamiento de D21).

**Alternatives considered**: validar sólo al programar; delegar la regla al proveedor (no auditable); la regla como texto en
parámetros (FR-014 pide dato con piso y techo); un `BackgroundService` propio con `SaveChanges` propio; webhooks con firma del
proveedor (exigirían enmienda constitucional).

**Trazas**: T75, T7 · FR-014, FR-015, FR-016, FR-098, FR-122, SC-011; US12-2, US12-4; I2, I3 · F-05, F-06 (CH34,
CH38).

**Difiere del informe**: F-05 usaba `ICalendarioDeFestivos` y los errores `Lending.Message.*` (CH32, CH34).

#### R145 · Plantillas de mensaje versionadas

**Decision**: se adopta la tabla sin uso `COR_NotificationTemplates` con columnas aditivas `Code`, `Module` («Lending»),
`Purpose`, `Version`, `ValidFrom`, `ValidTo`, `ProviderTemplateId` (plantilla aprobada de WhatsApp), `Variables` (lista
permitida) e `IsSeeded`. Cambiar una plantilla crea una fila nueva y el mensaje sella su PublicId y versión.
`RenderizadorDePlantillasDeMensaje` sustituye `{{Variable}}` sólo desde la lista blanca del propósito, sin expresiones, escapa
el HTML del correo y avisa del largo del SMS (GSM-7 160, UCS-2 70). Semillas «pendiente de validar»: aviso previo,
recordatorio preventivo, mora administrativa, prejurídica, no aplicado de pagaduría, paso a caja, abono a capital aplicado
(D24), gasto de cobranza informado y envío de documento. Rutas `/api/lending/message-templates`
(`MessageTemplates.View/Manage`), pantalla `/cartera/plantillas-de-mensaje`. WhatsApp sólo con `ProviderTemplateId`; las
plantillas las registra la cooperativa ante su proveedor (I15).

**Rationale**: FR-007 y FR-117 piden plantillas versionadas con vigencia por etapa; la tabla existe vacía en cada cooperativa y
los `.cshtml` no los edita la cooperativa.

**Alternatives considered**: `LND_MessageTemplates` (duplica una tabla de plataforma vacía); archivos `.cshtml` (sin vigencia
ni edición).

**Trazas**: T76 · FR-007, FR-117, FR-121, FR-122, FR-123; I15 · F-07.

**Conciliación**: C68 — `COR_NotificationTemplates` cambia su único sin filtro por uno versionado. C90 — el código de
la plantilla es de 10 caracteres (`CodigoDeCatalogo`).

#### R146 · Cobranza y aviso previo

**Decision** (proceso en K4; las tablas de aviso y reclamo nacen en K1 para lo migrado)
- **Gestiones** `LND_CollectionActions`, inmutables: fecha, canal (teléfono, presencial, correo, SMS, WhatsApp, carta,
  visita), persona contactada (deudor o codeudor), contacto directo sí o no, resultado de catálogo y promesa opcional. Una
  corrección es una gestión nueva que anula la anterior con motivo.
- **Promesas** `LND_PaymentPromises` con estado derivado (cumplida al entrar el pago, incumplida al vencer, vuelve a la
  agenda). **Acuerdos** `LND_PaymentAgreements` y `LND_PaymentAgreementInstallments` sobre lo vencido, sin tocar las
  condiciones (FR-120).
- **Etapas** `LND_CollectionStages` versionadas (días mínimos y máximos, acciones, plantillas, reglas de gasto);
  `LND_CollectionStageHistory` y `LND_CollectorAssignments` sólo de inserción, con motivo. `RepartoDeGestores` (puro) reparte
  por número de créditos o por saldo, con reglas por oficina, etapa, saldo, línea y pagaduría. La agenda es una consulta.
- **Gasto de cobranza**: se causa sólo después del `Sent` o `Delivered` de un mensaje `CollectionFeeNotice` (FR-121), con el
  hecho `GastoDeCobranzaCausado`.
- **Jurídico** `LND_LegalProcesses` y `LND_LegalProcessActions`: abogado (persona del maestro), actuaciones, adjuntos y
  honorarios. Insolvencia y reclamación por fallecimiento suspenden la cobranza (T102).
- **Aviso previo** `LND_PriorNotices` por episodio (la primera cuota impaga del episodio de mora vigente): comunicaciones
  exigidas (una, o dos en días distintos bajo `AvisoPrevio.UmbralDosComunicaciones`), primera fecha de constancia y fecha más
  temprana de reporte (constancia + `AvisoPrevio.DiasMinimos`, piso 20). Cada comunicación es un `DebtorMessage` de propósito
  `PriorNotice`; la constancia es su evento `Sent` con referencia y artefacto del proveedor o, para carta, un registro manual
  con la guía adjunta (`LendingPriorNoticeEvidence`, no borrable). Una constancia `SIMULADO` no vale en producción (R142). El
  archivo de centrales sólo incluye un dato negativo con aviso válido de su episodio; lo excluido queda listado y un reporte
  hecho sin aviso se retira en el archivo siguiente. Alerta `Cartera.AvisoPrevioSinConstancia`.
- Rutas `/api/lending/{collection-actions, promises, payment-agreements, collection-stages, collector-assignments, agenda,
  legal-processes, prior-notices, credit-report-claims}`; permisos `Collections.*`, `PaymentAgreements.*`,
  `LegalProcesses.Manage`, `PriorNotices.*`, `CreditReportClaims.*`; pantallas de K4 de §2.16.

**Rationale**: SC-012 por episodio y con prueba; atar la constancia al evento del mensaje evita una segunda fuente; datos
inmutables y de sólo inserción reconstruyen quién tuvo cada crédito y cuándo (FR-118, I-6).

**Alternatives considered**: columnas mutables de etapa y gestor en el crédito (pierde historia); una tabla de contactos
aparte de las gestiones (duplica); una marca «aviso enviado» (no prueba nada ni distingue episodios); correo certificado
físico siempre (la ley admite medios electrónicos); `LND_CollectionAssignments`, `LND_RepaymentAgreements`,
`LND_LegalProceedings`, `LND_PriorNoticeDeliveries` (R-06): CH31.

**Trazas**: T77 · FR-117–FR-121, FR-123, FR-124, FR-137, SC-012; US12, US16-2 · F-08, F-09 (CH31).

**Difiere del informe**: R-06 proponía `LND_CollectionAssignments`, `LND_RepaymentAgreements`, `LND_LegalProceedings`
y `LND_PriorNoticeDeliveries` (CH31).

**Conciliación**: C88 — avisos previos y reclamos se **leen desde K1** y se escriben desde K4. C64 — los reportes
negativos vigentes en centrales al migrar van a `LND_PriorNotices.MigratedReportedNegativeSince`; la etapa de cobranza
se conserva en la preparación y se registra al desplegar K4 (paso de despliegue en `tasks.md`); insolvencias y
reclamaciones al seguro viajan en la parte `blocks` (motivos `Deceased`, `Insolvency`). C90 — `Letter` no exige
autorización de canal; `Call` y `Visit` sólo son gestiones. C71 — los límites de cobranza de inicio, sábado y
contactos por día los fija `data-model.md` §10 y §12.

#### R147 · Archivos planos de Cartera con el motor común

**Decision**
- **Desembolsos (K1)**: `BankFileScope.LoanDisbursement = 4`. `LoanDisbursementLines` (`Application/Lending/Disbursements`)
  arma las líneas desde las órdenes de giro por transferencia: beneficiario de la copia sellada en la orden, monto neto tras
  la recogida, concepto = número del crédito, referencia de lote = número del archivo. La cuenta de origen se elige entre las
  que informa `IContabilidadParaCartera.CuentasBancariasAsync` y debe ser del banco del formato. `LND_DisbursementFiles` y
  `LND_DisbursementFileLines` (H) con SHA-256 y adjunto `LendingDisbursementFile`. Cada orden lleva la referencia del archivo
  (Tesorería no la paga por otra vía, FR-045) y va a lo sumo a un archivo no anulado; anular el archivo, con motivo, sólo
  antes del primer resultado de Tesorería. Rutas `/api/lending/disbursement-files` (+ `{id}/download-link`),
  `DisbursementFiles.Generate`. La cuenta del beneficiario se propone desde la persona o el asociado, se corrige al
  desembolsar y se sella en la orden (I10).
- **Pagaduría (K2)**: `BankFileScope.PayrollDeductionRequest = 5` y `PayrollDeductionResult = 6`, siempre genéricos, elegidos
  por referencia desde la versión del convenio (`OutboundFormatId`, `ResultFormatId`) y marcados `SeleccionPorReferencia`:
  `SolapadoAsync` deja de aplicarles el cruce de vigencias (`LosFormatosElegidosPorReferenciaNoCompitenPorVigencia`) y
  `/maestros/formatos-bancarios` filtra por ámbito. `BankFieldSource` suma `PayerEmployeeId`, `LoanNumber`,
  `InstallmentNumber`, `DueDate`, `NoveltyKind`, `PastDueAmount`, `PeriodYear`, `PeriodMonth`, `LibranzaDate`,
  `ResultCode`, `DeductedAmount`, `DeductionDate`. `FlatFileReader` (`Application/Common/BankFiles`, puro) es el inverso del
  escritor: vista previa del formato de vuelta, y lo reutilizará Recaudo. `NovedadesDeLibranza` (`Domain/Lending/Payroll`)
  compara lo exigible con el último archivo `Sent` y da `New`, `Change`, `Cancellation` o `Unchanged`.
  `LND_PayrollAgreementResultCodes` traduce el código de la pagaduría al motivo de Cartera por versión del convenio.
  `LND_PayrollDeductionFiles` y `…Lines` (H) con SHA-256 y adjunto `LendingPayrollDeductionFile`; regenerar antes del envío
  crea la versión siguiente y deja la anterior `Superseded`; después del envío, `Lending.PayrollFile.AlreadySent` y la
  corrección va como novedad del período siguiente (I9). Se entrega a Recaudo con `DescuentosPorPagaduria` (PublicId del
  adjunto, SHA-256, totales; sin bytes) y queda `Sent` al llegar `EnvioDeDescuentosConfirmado`. Cartera no procesa el archivo
  de vuelta: consume `ResultadoDeDescuentos` (`ConsumePayrollDeductionResultCommand`); en pruebas lo emite el doble (I8).
  Tareas `cartera.archivos-de-descuento` y `cartera.giros-de-pagaduria`.

**Rationale**: un solo motor (D-42 de la 010); el layout real de cada pagaduría es dato que aporta el dueño; archivos
inmutables con huella (FR-113); SC-016 (5.000 obligaciones en menos de un minuto, en memoria). Es el patrón de la dispersión
de nómina (`PAY_BankDisbursementFiles`).

**Alternatives considered**: reutilizar `PAY_BankDisbursementFiles` (es de Nómina); formatos propios de Cartera o un catálogo
`LND_` (dos motores); quitar la regla de solapamiento a todos los genéricos (rompe la elección de la dispersión); dejar el
lector a Recaudo (el convenio no se podría verificar ahora); `CuentasBancariasDeOrigenAsync` (F-10): CH45;
`LND_PaymasterAgreements`, `LND_DeductionFiles` (R-06): CH31.

**Trazas**: T78 · FR-045, FR-047, FR-108–FR-116, FR-149, SC-016; I8, I9, I10 · F-10, F-11 (CH31, CH45).

**Difiere del informe**: F-10 llamaba `CuentasBancariasDeOrigenAsync`; R-06, `LND_PaymasterAgreements` y
`LND_DeductionFiles` (CH31, CH45).

**Conciliación**: C10 — la orden pagada por archivo nace **al desembolsar** con `PaidByFile = true` en
`OrdenDeGiroEmitidaV1`; la referencia al archivo queda en la orden de Cartera y **el archivo no viaja en ningún
mensaje** (se retira `DisbursementFilePublicId` del contenido). Anular un archivo lleva sus órdenes a otro archivo.
C42 — `DisbursementFile` es hecho con la anulación `[EscrituraUnica]`. C19 — `DescuentosPorPagaduria` lleva además
líneas estructuradas (`DeductionFileLinePublicId`, documento, obligación, valor). C46 —
`LND_PayrollDeductionFileLines` va sin diff, con evento resumen por archivo. C71 — `Libranza.UmbralDeMoraDePagaduria`
queda sin valor hasta cotejar la CBCF antes de K2.

#### R148 · Layouts regulatorios embebidos con cotejo por campo

**Decision**
- Motor genérico y puro en `Domain/Common/Layouts` (`LayoutDeArchivo`, `EscritorPorLayout`, `ValidadorDeLayout`): ancho fijo
  o delimitado, registros por código (cabecera, detalle, totales) y, por campo, número, nombre, inicio y largo u orden, tipo,
  formato, obligatorio, valores admitidos, ruta de la proyección y `verified`. La PILA no se toca.
- `CatalogoDeLayoutsDeCartera` carga `Application/Lending/Regulatory/Layouts/sicses-cartera-*.json` y
  `central-{CODIGO}-*.json` con vigencia y fuente. Un campo sin `verified` deja vigente el layout y la advertencia
  `Lending.Regulatory.LayoutSinCotejar` al generar.
- Proyecciones `ProyeccionSicsesCartera` (desde el cierre mensual) y `ProyeccionCentralDeRiesgo` (novedades, castigos,
  reclamos en trámite, retiros de reportes sin aviso, sólo quienes autorizaron el reporte).
- `LND_RegulatoryFiles` (`RegulatoryFileKind {Sicses, CreditBureau}`, `BureauCode`, período, versión, `RegulatoryFileStatus
  {Validated, Generated, Superseded, Uploaded}`, código de layout, advertencias `LayoutSinCotejar` y
  `ModeloObligatorioNoDisponible` —nunca dentro del archivo, D28—, conteos, inconsistencias, excluidos, SHA-256, adjunto
  `LendingRegulatoryFile`, radicado y fecha de carga). Validar no guarda; generar con bloqueantes deja una fila `Validated`
  sin archivo (`Lending.Regulatory.BlockingIssues`). Archivos dorados `.esperado.txt` byte a byte, regenerables a propósito con
  una variable de entorno, como la PILA.
- `ValidateSicsesFileQuery`, `GenerateSicsesFileCommand`, `GenerateCreditBureauFileCommand`, `MarkRegulatoryFileUploadedCommand`;
  rutas `/api/lending/regulatory/…`; permisos `RegulatoryFiles.Read/MarkUploaded`, `RegulatoryReports.Generate`,
  `CreditBureauFiles.Generate`; pantallas `/cartera/sicses` y `/cartera/centrales`.

**Rationale**: son layouts nacionales, iguales para todas las cooperativas: embebidos cumplen I-3 sin réplica, con el
precedente de la PILA (R9 y D-43 de la 010) y la misma advertencia que pide FR-136.

**Alternatives considered**: layouts en base como valores nacionales (réplica para cambios raros); reutilizar
`COR_BankFileFormats` (orígenes bancarios); refactorizar la PILA hacia el motor genérico (riesgo en producción);
`LND_RegulatoryReports`, `LND_CreditBureauFiles` (R-06): CH31.

**Trazas**: T79 · FR-014, FR-136, FR-137, SC-019, D28; I7 · F-12 (CH31).

**Difiere del informe**: R-06 proponía `LND_RegulatoryReports` y `LND_CreditBureauFiles` (CH31).

**Conciliación**: C72 — `Lending.Regulatory.LayoutSinCotejar` se conserva por el precedente `Pila.LayoutSinCotejar`.
C46 — `LND_RegulatoryFileLines` va sin diff, con evento resumen por archivo. §7.6 — los dorados de layouts van en
`Application.Tests` con la carpeta y la variable que fija `quickstart.md`. E11 — si el primer vencimiento del formato
de cartera del SICSES llega antes que K5, la propuesta es adelantar sólo ese formato a K1.

#### R149 · Documentos emitidos con código de verificación

**Decision**
- `LND_IssuedDocuments` (`IssuedLendingDocument`): clase `IssuedDocumentKind {PaymentPlan, Statement, LoanClearance,
  GeneralClearance, BalanceCertificate, TaxCertificate}`; número por clase con `NumeradorDeCartera` (`LND_NumberSequences`,
  `NextValue`); emitido por y cuándo; copia de la identificación del titular como se emitió (I-1); corte, período o año;
  `InformationCutoff` (R, instante UTC); código de verificación único de 16 caracteres Crockford base32 (80 bits), mostrado
  `XXXX-XXXX-XXXX-XXXX`, con QR (QRCoder) a `/cartera/verificar?codigo=`; SHA-256 del contenido; PDF guardado (adjunto
  `LendingIssuedDocument`, no borrable); datos esenciales; `FinalityUntil` (firmeza, D23); si cubre codeudas (FR-131); nota de
  ventas aceptadas fuera de Cartera. Le acompañan `LND_IssuedDocumentLoans` (créditos cubiertos y última transacción
  considerada) y `LND_IssuedDocumentEvents` (H: emitido, entregado, anulado con motivo y transacción causa).
- Estado **calculado** (`IssuedDocumentStatus`): `Voided` si hay anulación; `SubjectToFinality` mientras hoy <
  `FinalityUntil`; `Valid` en otro caso.
- `VigenciaDeDocumentos` (`Domain/Lending/Documents`, puro) corre dentro del comando que reversa, en la misma unidad de
  trabajo: anula todo documento que cubra ese crédito con `InformationCutoff` igual o posterior al registro de lo reversado.
  Hay anulación manual con permiso y motivo (`VoidLendingDocumentCommand`).
- Verificación: `GET /api/lending/documents/verify/{code}` (`Lending.Documents.Verify`, auditada) devuelve estado y datos
  esenciales; por la base por cooperativa nunca muestra documentos de otra (D21). En K5, comparar el SHA-256 de un PDF subido.
- **Render**: puerto `IDocumentoDeCarteraRenderer` (`Application/Lending/Documents`) con un modelo por clase;
  `DocumentosDeCarteraReport` (`API/Reports`, QuestPDF y QRCoder) con membrete (nombre y NIT de `COR_Companies`), firmante por
  `Documentos.Firmante.Nombre`/`.Cargo`, leyenda de ambiente de pruebas y, en el paz y salvo general, la leyenda de que sólo
  certifica obligaciones de Cartera y si incluye codeudas (`Documentos.PazYSalvoIncluyeCodeudas`).
- **Emitir en un paso**: `IssueLendingDocumentCommand` (clase, persona, crédito opcional, corte, período o año, entrega por
  descarga o correo) crea la fila, renderiza, guarda el adjunto y devuelve el enlace y el código; por correo,
  `SendLendingDocumentCommand` crea un `DebtorMessage` `DocumentDelivery` con el PDF por `CanalDeCorreo`. Reimprimir es
  descargar el PDF guardado, auditado. Errores `Lending.Document.ClearanceDenied` y `.VoidedCannotReissue`.
- El PDF del simulador (FR-034) **no** es documento emitido: sin número ni código, con la leyenda «Simulación – no constituye
  aprobación»; enviarlo por correo deja un `DebtorMessage` `Informative` y la simulación no registra nada del crédito.
- Entrega (I5): la infraestructura con plan de pagos, paz y salvo y certificado de saldo en K1; extracto, certificado
  tributario y comparación del PDF en K5; en K6b el certificado tributario separa los intereses de vivienda.
- Firma (I6): nombre y cargo por parámetro más código y QR; imagen de firma y logotipo como adjuntos opcionales de la
  cooperativa; la firma digital PAdES queda fuera.

**Rationale**: FR-132 y D21 piden verificación por usuarios de la cooperativa: basta buscar en su base, sin claves que rotar.
Guardar el PDF hace la reimpresión idéntica; el estado calculado evita una tarea que marque firmezas. FR-013 pide emitir en un
paso.

**Alternatives considered**: código HMAC con `IAuditSignatureService` (claves que administrar, responde a la verificación
SaaS); PAdES (certificado y costo); regenerar el PDF al reimprimir (no idéntico); generarlo en el navegador (sin huella);
emitir la simulación como documento numerado (FR-034 dice que no registra nada).

**Trazas**: T80 · FR-013, FR-034, FR-082, FR-131, FR-132, FR-143, D21, D23; US13; I5, I6 · F-13, F-14.

**Conciliación**: C88 — en K1 se emiten plan de pagos, paz y salvo y certificado de saldo, con `LND_IssuedDocument*`
en la migración `CarteraNucleo`; extracto y certificado tributario son de K5 y antes responden
`Lending.Document.KindNotAvailable`. C90 — si el dueño acepta I6, la imagen de firma y el logotipo son los adjuntos
`LendingSignatureImage` y `LendingLogo`. C87 — el envío de la simulación por correo es sólo `Informative`.

#### R150 · Adjuntos, consulta a centrales y pagaré

**Decision**
- `AdjuntosDeModulo` suma, **de módulo y no borrables**: `LendingIssuedDocument` (`Lending.Documents.Read`),
  `LendingDisbursementFile`, `LendingPayrollDeductionFile`, `LendingRegulatoryFile` y `LendingBureauReport`
  (`Lending.CreditBureau.ReadReports`); **con subida**: `LoanApplicationDocument` (mientras la solicitud no esté decidida),
  `LendingGuaranteeDocument`, `LendingPayrollCertification` (FR-109), `LendingPriorNoticeEvidence`,
  `LendingLegalProcessDocument`, `LendingNoveltyConsent` y `DataAuthorization` (R138). Decidido el dueño, no se borran
  (`Attachments.OwnerLocked`).
- `LND_BureauQueries`: persona, propósito, central, canal sellado, autorización obligatoria (FR-006), resumen (puntaje y
  marcas negativas), artefacto crudo como `LendingBureauReport` y marca de simulado; se audita como lectura sensible y vale
  `Centrales.DiasDeVigenciaDeConsulta` días. `QueryCreditBureauCommand`, `/api/lending/bureau-queries`
  (`CreditBureau.Query`, `.ReadReports`).
- Pagaré (`LND_PromissoryNotes`): sella el canal del depósito y el identificador externo del pagaré; sus estados vienen del
  adaptador (creado, pendiente de firma, firmado, en custodia, endosado, anulado) y los consulta la tarea `cartera.pagares`;
  el depósito notifica a los firmantes, el ERP no les escribe. Alerta `Cartera.PagarePendienteDeFirma`.

**Rationale**: `AdjuntosDeModulo` es el único sitio que decide; las evidencias regulatorias no se borran (Principio XI,
FR-003).

**Alternatives considered**: guardar el informe de la central en una columna JSON (el PDF o XML del proveedor no cabe ni se
descarga firmado); que el ERP envíe la firma del pagaré (duplica lo que hace el depósito).

**Trazas**: T81 · FR-006, FR-035, FR-036, FR-043, FR-044, FR-109, FR-124 · F-19, S10.

**Difiere del informe**: F-19 usaba `Lending.Documents.View` y `Lending.Bureau.ViewReport` (CH33).

**Conciliación**: C84 — `POST /bureau-queries` devuelve sólo el estado; el contenido sale por
`Lending.CreditBureau.ReadReports`. C42 — `BureauQuery` es hecho (H). C21 — el adjunto
`RegulatoryIndicatorSupport` (soporte de los indicadores del contador) se suma a la lista de `AdjuntosDeModulo`.

#### R151 · Plantillas de importación sobre el ejecutor común

**Decision**
- `RutasDePlantilla.MapPlantilla` se generaliza para recibir la búsqueda de la definición y el permiso de datos personales
  (hoy fijos a Inventario); se corren las pruebas de las 16 plantillas de la 012.
- `CatalogoDePlantillasDeCartera` (`Application/Lending/Imports`), registro en `GET /api/lending/templates`
  (`Lending.Templates.View`), pantalla `/cartera/plantillas`:

  | Clave | Qué carga | E |
  |---|---|---|
  | `lending.credit-lines` | Hojas `Lineas`, `Tasas`, `Documentos`, `Seguros`, `Garantias`, `Equivalencias` | K1 |
  | `lending.migration.{parte}` | Una hoja por parte de la carga (R152) | K1 |
  | `lending.provisional-collections` | Cobros provisionales al activar: factura, valor, fecha valor, referencia contable; todo o nada; emite `CobroProvisionalIncorporado` | K1 |
  | `lending.contribution-balances` | Saldo de aportes y ahorro permanente con fecha (D19) | K1 |
  | `lending.related-parties` | Personas vinculadas (FR-040) | K1 |
  | `lending.arrears-history` | Carga complementaria del historial | K6a |
  | `lending.payroll-agreements` | Convenios de pagaduría | K2 |
  | `compliance.restrictive-lists` | Listas restrictivas (plataforma, R139) | K1 |

- Revisión previa, todo o nada, errores por fila y columna. **Ninguna crea personas**: un documento que no existe en
  `COR_People` es un error de fila y columna.

**Rationale**: el ejecutor ya da lo que piden FR-033, FR-139, FR-140 y FR-142; las reglas son las del alta unitaria.

**Alternatives considered**: un ejecutor propio de Cartera; duplicar `RutasDePlantilla`; `lending.related-persons` (F-16):
CH30; `lending.delinquency-history` en K1 (F-16): la carga complementaria es de K6a y el historial opcional de la migración va
como parte de la carga (CH46).

**Trazas**: T82 · FR-033, FR-040, FR-139, FR-140, FR-142, D19; I11 · F-16 (CH30, CH46).

**Difiere del informe**: F-16 llamaba `lending.related-persons` y ponía `lending.delinquency-history` en K1 (CH30,
CH46).

**Conciliación**: C62 — los cobros provisionales los valida y guarda la activación en `LND_ProvisionalCollections`
(K1, `[EscrituraUnica]`), con la revisión previa que deja un adjunto; el consumidor de Comercial los **aplica al
nacer la obligación**, con clave idempotente (de ahí `CobroProvisionalIncorporado`), no la importación. C85 — la
plantilla de saldos de aportes exige `Lending.CreditLimits.Assign`. C82 — la plantilla de valores nacionales
(`ImportNationalValuesCommand`) reutiliza definición, lector y reglas del ejecutor con su propia transacción sobre
`IAdminDbContext`. C70 — la matriz contable no tiene filas sembradas: su plantilla presenta cada combinación sin
cuenta como «pendiente de validar».

#### R152 · Carga de migración por partes

**Decision**
- `LND_MigrationLoads` (corte; `Draft`, `Applied`, `Replaced`, `Activated`; quién revisó y aplicó; instantánea del cuadre y
  de la conciliación de ventas) y `LND_MigrationLoadParts` (clave de parte, archivo, SHA-256, filas, fecha de aplicación).
- Preparación por parte: `LND_Migration{Loans, Installments, Balances, RiskData, Restructurings, Collaterals, CoDebtors,
  CreditLimits, Blocks, Authorizations, PriorNotices, BureauReports, Claims, RelatedParties, ChargedOff, Sales,
  ArrearsHistory}`. Cada parte es una plantilla de una hoja (admite `.csv`) con revisión y aplicación todo o nada sobre el
  borrador.
- `ApplyMigrationLoadCommand` valida entre partes (el plan pendiente suma el capital, referencias cruzadas, historial de los
  segmentos cubiertos), todo o nada, y calcula el cuadre (FR-141) y la conciliación de ventas (FR-143) con
  `LND_MigrationLoadDifferences` y su aceptación por punto de acta (D17). Sólo una carga `Applied` se activa; una nueva deja la
  anterior `Replaced` como ensayo.
- Lo migrado de K2–K4 (pagaduría, etapa, avisos, reclamos, reestructuraciones) se conserva como dato desde K1. Los créditos
  migrados conservan el capital por cuota cargado y su interés lo causa el motor desde el corte (D5).
- Las ventas de servicios a crédito anteriores a `CreditWithoutLending` son la clase «aceptadas fuera de Cartera (D29)» de
  `LND_MigrationLoadDifferences` (R115, G11).
- Rutas `/api/lending/migration/loads` (+ `{id}/parts/{parte}/template.xlsx`, `…/import`, `{id}/apply`, `{id}/reconciliation`,
  `{id}/balance-check`), permisos `Migration.Load`, `.AcceptDifferences`; pantalla `/cartera/migracion`; vista
  `migration-reconciliation`. La activación que hace nacer los datos desde la carga es T84.

**Rationale**: una sola hoja con 50.000 créditos y sus cuotas pasa los topes del ejecutor (60.000 filas por hoja, 16 MiB) y un
`SaveChanges` con el libro entero en memoria es frágil; FR-142 dice que aplicar sólo guarda la carga.

**Alternatives considered**: un libro único con los topes subidos (memoria y tiempo); un guion directo a la base (salta la
validación y la auditoría, Principio III); crear los créditos al aplicar (contradice FR-142); las partes `Guarantees`,
`Quotas` y `RelatedPersons` de F-17 (renombradas en §2.3).

**Trazas**: T83 · FR-139–FR-143, SC-013, US8; D5, I11, G11 · F-17.

**Difiere del informe**: F-17 llamaba las partes `Guarantees`, `Quotas` y `RelatedPersons` (§2.3).

**Conciliación**: C64 — una parte admite **varios archivos** (`LND_MigrationLoadParts`, una fila por archivo); los
castigos llevan el soporte histórico como dato (`HistoricalApprovalReference` / `soporteDelCastigo`, sólo en la
preparación; T64 rige lo decidido en el ERP); reclamaciones al seguro e insolvencias viajan en la parte `blocks`
(motivos `Deceased`, `Insolvency`); los reportes vigentes en centrales van a
`LND_PriorNotices.MigratedReportedNegativeSince`. C63 — FR-143 «anterior a la activación» = anterior al **corte**; una
venta fechada hasta el corte que llega después de activar queda `NotIncorporated` (pregunta I16). C46 — las 17 tablas
de preparación van sin diff, con evento resumen por importación. C60 y C61 — la activación corre en una transacción
con guardados por tandas de 1.000 créditos y exige acta. C85 — el cuadre de una carga se lee con
`Lending.Reconciliation.View`. I17 — los cobros externos por factura van sumados con su referencia (propuesta).

#### R153 · Centro de informes y tablero

**Decision**
- `LendingReportsEndpoints` se reescribe como el de Inventario: registro `GET /api/reports/lending`, metadato
  `VistaDeInformeDeCartera`, `MapVistaDeCartera` con `Lending.Reports.View`,
  `RequirePermissionWhenExporting(Lending.Reports.Export)` y `Lending.Reports.ExportPersonalData`, rango de hasta cinco años y
  una auditoría `Lending.Report.Exported` por exportación. Filtros `FiltrosDeInformeDeCartera`: `from`, `to`, `asOf`,
  `branch`, `line`, `modality`, `subsegment`, `advisor`, `payer`, `collector`, `stage`, `format`. Toda consulta filtra con
  `IAlcanceDeCartera` (`LasConsultasDeCarteraRespetanElAlcance`).
- Vistas (`TablaExportable`): K1 `aging` (`Informes.RangosDeEdad`), `placement`, `receipts`, `transactions`, `usury`
  (`Informes.PuntosAlertaUsura`), `debtor-messages`, `migration-reconciliation`; K2 `payroll-agreements`,
  `payroll-deduction-reconciliation`; K3 `restructured`, `charge-offs`; K4 `collection-stages`, `promises`,
  `payment-agreements`, `prior-notices`; K5 `quality`, `vintages`, `concentration`, `issued-documents`, `bureau-exclusions`.
- Se retiran `aging`, `aging/pdf` y `loan-statement/{id}/pdf` heredadas; `person-portfolio` (sin créditos) y
  `savings-statement` pasan a `SavingsReportsEndpoints` (R125).
- Tablero `GET /api/lending/dashboard?branch=&line=&asOf=` (`GetLendingDashboardQuery`, `Lending.Dashboard.View`) sobre las
  posiciones diarias (`LND_LoanDailyPositions`, I-6) dentro del alcance. Pantallas `/cartera/informes?vista=` y
  `/cartera/tablero`.

**Rationale**: molde de la 012, con un solo punto de exportación auditada (FR-138) y el alcance de FR-004 y FR-135.

**Alternatives considered**: mantener las rutas heredadas (sin permisos, una llamaba a una ruta inexistente); calcular el
tablero sobre las transacciones al leer (no cumple SC-007 ni SC-008 con 50.000 créditos); la vista `collections` para el
recaudo (F-15): en inglés de banca es cobranza (CH10); retirar `person-portfolio` (F-15): se conserva sin créditos (CH44).

**Trazas**: T99 · FR-004, FR-133–FR-135, FR-138, FR-144; A10 · F-15 (CH10, CH44).

**Difiere del informe**: F-15 llamaba `collections` a la vista del recaudo y retiraba `person-portfolio` (CH10, CH44).

**Conciliación**: C83 — en informes y tablero `asOf` es la **fecha de corte** (como la 012); en `/loans/*` y
`/risk/ratings` se usan `date` = D y `recordedAsOf` = R. C84 — las vistas con datos personales exigen
`Lending.Reports.View` más `Lending.Loans.Read` para consultarlas y `ExportPersonalData` para exportarlas.

#### R154 · Pantallas de Cartera y menú

**Decision**
- Carpeta nueva `Shared/Pages/Cartera`, en `ModulosMigrados`, con las rutas de §2.16. Toda página: `<PermissionGate
  Required="Lending.…" MostrarAviso="true">`, `IndicadorDeCarga` con `_carga.Iniciar()` en toda zona que carga o guarda,
  `PersonSearchPicker` para buscar y `PersonaDialog` para el alta (FR-035), simulador visible en toda pantalla de originación,
  mensajes en español claro sin códigos técnicos, ningún color literal ni `<style>` propio, y nunca el nombre del sistema
  anterior. Las operaciones frecuentes de FR-013 se verifican en tres pasos o menos.
- Menú: grupo «Cartera» con `PermissionGate` por enlace; grupo «Ahorros y aportes» con lo conservado (R126).
- Pantallas de otras áreas de este frente: `/compliance/listas-restrictivas`, `/compliance/solicitudes-del-titular`,
  `/admin/canales-externos`; `/cartera/dobles-de-prueba` sólo fuera de producción.
- Las rutas de crédito heredadas (`/cartera/creditos`, `/cartera/solicitudes`) se reutilizan sólo después del commit 3 de R128
  (Blazor falla al ejecutar, no al compilar, ante rutas ambiguas).
- Pruebas: `LasPantallasDeCarteraEstanEnElMenu` (página, ruta y permiso, molde `LasPantallasDelCicloComercialEstanEnElMenu`),
  `TextosDeCarteraTests`, `ManualCatalogoTests`, `TodoEnlaceDelMenuTieneSuPagina`, `LasPantallasDicenQueEstanCargando`
  (`ModulosMigrados` += `Cartera`).

**Rationale**: Principio UI, FR-013 y Principio V; la lección de la 012 (§2.11) sobre rutas ambiguas; FR-144 conserva ahorros y
aportes.

**Alternatives considered**: reutilizar `Pages/CarteraFinanciera` (mezcla módulos y deja fuera la prueba de carga); mover
también las rutas de ahorros (fuera de alcance); `/cartera/conciliacion`, `/cartera/canales-externos` y
`/cumplimiento/listas-restrictivas` (F-18, F-20): CH44.

**Trazas**: T98, T100 · FR-004, FR-013, FR-035, FR-144 · F-20, R-08 (CH44, CH50).

**Difiere del informe**: F-18 y F-20 usaban `/cumplimiento/listas-restrictivas`, `/cartera/conciliacion` y
`/cartera/canales-externos` (CH44).

### 11.3 Riesgos

| Riesgo | Mitigación |
|---|---|
| Sin contratos externos, en producción el simulado no tiene efecto legal | COOFLOPAL opera con pagaré físico, correo, constancia manual del aviso y líneas sin «exige consulta a centrales» (T73) |
| Proveedores no elegidos; su API real puede pedir capacidades nuevas | Puertos con `Capacidades`; baterías de conformidad por puerto |
| WhatsApp sólo admite fuera de 24 h plantillas aprobadas por Meta, con el mismo texto | `ProviderTemplateId`; sin él no se usa WhatsApp (I15) |
| Algoritmo de credenciales duplicado frente a la DIAN; 1 MiB por Secret; circuito contado por réplica | Refactor posterior con pruebas; aceptado |
| Lecturas de la Ley 2300 (canal de la semana, informativos como contacto) | Casos dorados; las valida el abogado (I2) |
| El sondeo de entregas multiplica llamadas al proveedor | Cadencia técnica; sin webhooks por Principio IV |
| Dos caminos de correo (notificaciones de usuario y mensajes al deudor) | Explicarlo en el manual |
| Catálogo de resultados de gestión, promesas cumplidas a medias, definición de «episodio», guía física a tiempo | Validación del jefe de cartera; tolerancia como parámetro; alerta `Cartera.AvisoPrevioSinConstancia` |
| `COR_BankFileFormats` se estira más allá de «ligado al banco»; Recaudo y Tesorería sin especificar; origen de la cuenta del beneficiario | Ámbitos `SeleccionPorReferencia` y filtro por ámbito; dobles de prueba; propuesta de I10 |
| Layouts del SICSES, de las centrales y de las pagadurías no están en el repositorio; SC-019 depende del validador | Los aporta el dueño, cargados con `verified = false` y la advertencia `LayoutSinCotejar` (I7, I8) |
| «Firma de la cooperativa» sin definir; no hay logotipo | Nombre y cargo por parámetro, código y QR (I6) |
| Volumen de PDF y adjuntos; plazos legales de retención de evidencias de centrales | Adjuntos en S3; plazos como parámetro |
| Tocar `RutasDePlantilla` afecta a las 16 plantillas de la 012 | Correr sus pruebas |
| Sin plantilla de personas; volumen de la migración desconocido | Personas registradas antes; carga por partes (I11) |
| Rendimiento del tablero y de los informes con 50.000 créditos | Posiciones diarias versionadas (I-6) |
| La app MAUI no está verificada | Verificación manual, como en la 011 y la 012 |

---

## 12. Preguntas de estos frentes

Las preguntas de los cuatro informes, remitidas a la §5 de las transversales (no se repiten aquí). «Rige la propuesta»
quiere decir que, sin respuesta del dueño, se construye con la propuesta por defecto de §5.

| Frente | Pregunta del informe (resumen) | Código §5 | Estado |
|---|---|---|---|
| Enmiendas | ¿Con qué fecha valor recauda Nómina? ¿Queda exenta del límite D4? | D15 y A9 | Rige la propuesta: fin del período y aprobación, exentas (R103) |
| Enmiendas | ¿`DESC_CARTERA` contra un puente (enmienda D-08 de la 010)? | C1 | Rige la propuesta (R107) |
| Enmiendas | Rechazo o excedente en la ordinaria: ¿ajusta o recalcula? | G2 | Ajusta al aprobar, con vista previa (R106) |
| Enmiendas | ¿Reversar la ordinaria reversa sus recaudos? | G3 | Sí, todo o nada (R106) |
| Enmiendas | ¿Qué obligaciones propone la definitiva? | G4 | Todas las vivas como deudor, castigadas incluidas; nunca codeudas (R104) |
| Enmiendas | Prelación de la cuota de Cartera frente a otras deducciones y alimentos | G5 | Primera entre las autorizadas, sujeta al tope (R106) |
| Enmiendas | ¿Quién paga en la nómina propia: el empleado o la cooperativa? | G7 | El empleado deudor (R103) |
| Enmiendas | ¿Entra `CreditWithoutLending` en E-02? | A6 (antes G8) | Rige la propuesta; si no, precondición de activación (R115) |
| Enmiendas | ¿Se adapta en K1 el retiro de asociado? | A5 | Sí (R110) |
| Enmiendas | Cambio de monto de una venta con número reservado | G9 | Liberar y reservar con clave nueva (R112) |
| Enmiendas | Festivos «Manual» al pasar a valores nacionales | E5 (fundida) | **Cambia la propuesta del informe**: quedan locales de Nómina y Cartera no los usa (CH15, R116) |
| Enmiendas | ¿Basta conservar el PublicId heredado como historia? | G10 | Sí, en `PAY_SettlementDeductions` y `PAY_Novelties` (R108) |
| Retiro | ¿`LND_SiplaGroupParameters` se conserva sin su FK? | H1 | Rige la propuesta (R120) |
| Retiro | ¿Qué se hace con las compartidas (actas, listas, códigos, documentos)? | H2 | Se conservan (R119) |
| Retiro | ¿Se retiran como crédito periodicidades, valores de deducción, tasas de interés, consultas en línea y recreación? | H3 | Sí (R119) |
| Retiro | Extractos y portafolio de la persona: ¿sin crédito o se enmienda la lista? | A5 (fundida: retiro de asociado) | Sin la parte de crédito (R110) |
| Retiro | ¿Se muda lo conservado a `Savings`? | A10 (antes H5) | Rige la propuesta (R125) |
| Retiro | `ACC_Documents` con origen CAR | H4 | Aprobar con constancia (R120, R122) |
| Retiro | ¿Se corrige la fila XII de la spec (base administrativa)? | A4 | Sí, redacción (hechos 9.1) |
| Retiro | ¿Se retira entero el proyecto Legacy? | H6 | Sí (R127) |
| Retiro | ¿Quién decide sobre `DS`, `RC`, `CA`, `PV`, `DN`? | — | Resuelta: se reutilizan para la Cartera nueva (T54, R128) |
| Seguridad | ¿Modelo propio de decisión en vez del motor de la 012? | F1 | Rige la propuesta (R132–R134) |
| Seguridad | ¿Plantillas y comando en vez de roles integrados? | F2 | Rige la propuesta (R130) |
| Seguridad | ¿Se tocan Auditor y CompanyAdmin? | F3 | Auditor no; CompanyAdmin sin `Lending.Decisions.*` ni `Lending.Sessions.Vote` (R130) |
| Seguridad | La lista cerrada no incluye a Cumplimiento | A1 (fundida) | Rige la propuesta (R138) |
| Seguridad | ¿Alcance cerrado y selector agencia–oficina en K1? | F4 | Sí (R131) |
| Seguridad | ¿Persona vinculada obligatoria para los actores? | F5 | Sí (R135) |
| Seguridad | ¿Qué incompatibilidades vienen activas? | F6 | `Decidir × Desembolsar` (R135) |
| Seguridad | Suplentes, quórum, mayorías | F7 | Rige la propuesta, «pendiente de validar» (R133) |
| Seguridad | ¿Quién registra los votos? | F8 | La secretaría; el miembro usuario puede votar (R133) |
| Seguridad | Tablas compartidas `LND_Minutes`, `LND_MinuteAttendees`, `LND_Blacklist` | H2 (fundida) | **Cambia la propuesta del informe**: se conservan (CH22, R119) |
| Seguridad | ¿Quién tiene la atribución de fecha valor anterior a un cierre cuando paga Recaudo? | D16 | `Lending.Payments.BackdateBeforeClose` con límite (R102, R132) |
| Seguridad | ¿Se auditan los rechazos de forma? | F9 | **Cambia la propuesta del informe**: sí, `Lending.Request.Rejected` (R136) |
| Seguridad | ¿Qué es «dispositivo»? | F10 | User-Agent y `X-Dispositivo` (R136) |
| Seguridad | Plazos del titular como parámetros con fuente | F11 | Sí, como límites legales (R138) |
| Adaptadores | ¿SMS y WhatsApp por cooperativa? | I1 | Sí (R141) |
| Adaptadores | Ley 2300: canal de la semana e informativos | I2 | Rige la propuesta; la valida el abogado (R144) |
| Adaptadores | ¿Un documento pedido por el titular queda exento de ventanas? | I3 | Sí (R144, R149) |
| Adaptadores | ¿Simulado en producción antes de activar? ¿Líneas sin centrales? | I4 | **Cambia la propuesta del informe**: antes y después de activar, sin efecto legal (CH20, R142) |
| Adaptadores | Firma de la cooperativa y logotipo | I6 | Nombre y cargo, código y QR (R149) |
| Adaptadores | ¿Plan de pagos, paz y salvo y certificado de saldo en K1? | I5 | Sí (R149) |
| Adaptadores | Formato del SICSES y centrales de COOFLOPAL | I7 | Los aporta el dueño (R148) |
| Adaptadores | Layouts de pagaduría y quién procesa la vuelta | I8 | Ámbitos 5 y 6; el doble emite la vuelta (R147) |
| Adaptadores | ¿Se regenera un archivo enviado? | I9 | No (R147) |
| Adaptadores | Cuenta del beneficiario del desembolso | I10 | Propuesta desde la persona, sellada en la orden (R147) |
| Adaptadores | Volumen y personas de la migración | I11 | Carga por partes; personas antes (R151, R152) |
| Adaptadores | Listas por cooperativa y re-escaneo | I12 | Sí; re-escaneo nocturno con alerta (R139) |
| Adaptadores | La cooperativa como persona jurídica para su convenio | I13 | Sí (K2, R147) |
| Adaptadores | ¿Avisos internos por SMS? | I14 | No (R143) |
| Adaptadores | ¿Quién registra las plantillas de WhatsApp? | I15 | La cooperativa (R145) |
| Adaptadores | ¿Se conserva la ruta `savings-statement`? | — | Resuelta: sí, en `SavingsReportsEndpoints` (R125, R153) |

**Agregadas por la armonización que tocan estos frentes**: A7 (proyección de Nómina, R116), A9 (exención de fecha
valor, R103), A11 (desenlace derivado del contrato, R137), B2 (Recaudo = `Receipts`, R101), B4 (producción =
`IsProduction()`, R142), B6 (UVT en K1, R116), B8 (confirmación por excepción, R113), G6 (reserva dentro de la
confirmación, R112), G11 (ventas de servicios anteriores al dato, R115, R152) y H7 (segundo revisor y diagnóstico en PDN,
R123).

---

## 13. Nombres fijados en este documento

Ninguno contradice las transversales; los que siguen faltaban y se fijan con las reglas T1–T4.

| Nombre | Qué es | Dónde | Regla |
|---|---|---|---|
| `INV_DocumentPayments.ReservationPublicId` | Columna (Guid?) con la reserva, para emitir la v2 aunque la venta confirme después de una aprobación (R112) | Migración `ComercioConCartera` | T1, T2; el campo de la v2 es `ReservationPublicId` (T18) |
| `ManualCatalogo.Modulos.AhorrosYAportes` | Constante de módulo del manual para los temas conservados (R126) | `Shared/Services/Manual` | T1; nombre del informe R-08 |
| `ConsultaDeObligacionesDePrueba`, `ContratoDeObligacionesDePrueba` | Dobles unitarios de los puertos (R118); no son los dobles de FR-089 (CH20) | Sólo `tests/` | T1; nombres del informe DE-19 |
| `LendingDecisionSubjects` (`CreditApplication`, `CreditLimit`, `Novelty`, `Condonation`, `WriteOffBatch`, `PortfolioEvaluation`, `ImpairmentModelChange`, `ActivationDifferences`, `ValidationFailureException`) | Constantes de sujeto de la matriz y de los puntos de sesión (R132) | `Domain/Lending/Attributions` | T1; informe S4 |
| `RecordedBySecretary`, `OwnSession`, `InPersonPasskey`, `InPersonTotp` | Valores del método de registro de un voto (R133) | `LND_SessionVotes` | T1; informe S5 y pregunta F8 |
| `Lending.Payoff.Viewed`, `Lending.CreditBureauReport.Viewed`, `Lending.Document.Reprinted`, `Lending.CreditStatus.Queried`, `Lending.Session.Viewed`, `Lending.RelatedParty.Viewed` | Eventos de lecturas sensibles; con `Lending.Report.Exported` (§2.15) forman la lista de `LasLecturasSensiblesDeCarteraSeAuditan` (R136) | `LendingAuditEmitter` | T1 (`Área.Recurso.Motivo`); informe S8 |
| `Lending.Session.Closed`, `Lending.ScreeningMatch.Released` | Eventos de cierre de acta y de liberación de una coincidencia (R133, R139) | Auditoría encadenada | T1; S11 decía `Lending.RestrictiveListMatch.Released` (CH24) |
| `ICanalDeMensajeria.ConsultarEntregaAsync` | Método del puerto para el estado de entrega por sondeo (R144) | `Application/Common/Messaging` | T1; informe F-06 |
| `LendingDashboardDto` | DTO de respuesta de `GetLendingDashboardQuery` (R153) | `Application/Lending/Reports` | T1 (DTO de ruta con `Dto`); informe F-15 |
| `PAY_PayrollLoanDeductions`: `PayrollRunId`, `PayrollRunEmployeeId`, `PayrollRunLineId`, `ObligationPublicId`, `ObligationNumber`, `ExpectedAmount`, `DeductedAmount`, `NotDeductedAmount`, `NotDeductedReason`, `Status`, `CarteraReceiptPublicId`, `CarteraReversalPublicId`, `ResultCode`, `ResultMessage` | Columnas propuestas (R106) | `data-model.md` | T1; informe DE-07 |
| `INV_LendingObligationResponses`: `DocumentPaymentId`, `MessagePublicId` (único), `OriginalMessagePublicId`, `Outcome`, `ObligationPublicId`, `ObligationNumber`, `ValidationStatus`, `ReasonCode`, `Reason`, `ReceivedAt` | Columnas propuestas (R113) | `data-model.md` | T1; informe DE-14 |
| `LND_UserCreditLineScopes`: `UserId`, `CreditLineId`, `IsDefault` | Columnas propuestas (R131) | `data-model.md` | T1; informe S3 |
| `LND_PromissoryNotes`: `CustodianChannelCode`, `ExternalNoteId` | Canal sellado del depósito e identificador externo del pagaré (R150) | `data-model.md` | T1; informe F-19 |

---

## 14. Conciliación

Los puntos se decidieron en `decisiones-transversales.md` §7. Donde una decisión de esta mitad quedó afectada, lleva al
final su línea **Conciliación**.

| Punto | Decisión |
|---|---|
| RS2-1 · Columna de la reserva en Comercial | C67: `INV_DocumentPayments.ReservationPublicId` y `.ConsultationEvidence` en `ComercioConCartera` |
| RS2-2 · Novedad de Cartera en la ordinaria | C55: una `PAY_Novelties` `LoanDeduction` por empleado, obligación y período; el recálculo la reemplaza y el descarte la anula |
| RS2-3 · `Payroll.Settlement.ConceptAccountsMissing` | §7.6: ya existe (`SettlementErrors`); la ordinaria usa el existente `Payroll.ConceptWithoutAccounts`; ninguno es código nuevo |
| RS2-4 · Lecturas sensibles sin nombres | C89: la lista cerrada de la §13; la liberación es `Lending.ScreeningMatch.Released` |
| RS2-5 · Auditoría de lo conservado sin A10 | C89: `ModuloDeAuditoria` suma una regla por nombre que lleva los handlers de ahorros a `Savings` antes de la de `.Lending` |
| RS2-6 · Conteo de llaves | §7.6: 40 (38 entre `LND_`, una a `COR_People`, una de `PAY_SettlementDeductions`) |
| RS2-7 · `LND_SiplaGroupParameters.CreditLineId` | §7.6: requerida y sin FK; la decide la feature de SARLAFT |
| RS2-8 · Votos inmutables | C75: voto de reemplazo que referencia al anterior; único filtrado a no reemplazados |
| RS2-9 · `/cartera/autorizaciones` sin permiso | C85: `Compliance.HabeasData.ViewHistory`/`RecordConsent` |
| RS2-10 · Copia de datos en `NominaRecaudaPorElContrato` | C73: transformación idempotente de datos, no semilla; lo declara el Constitution Check |
| RS2-11 · `plan.md` contradice tres decisiones | §7.6: corregidas en `plan.md` |
| RS2-12 · Numeración de las dos mitades | §7.6: R1–R61 y R101–R154; ningún artefacto cita R62–R100 |
