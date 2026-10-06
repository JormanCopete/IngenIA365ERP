# Interfaces síncronas de Cartera con otros módulos

**Feature**: 013 | **Fecha**: 2026-10-05 | **Entrega**: K1 (lo marcado K2 nace declarado en K1) | **Base**:
`decisiones-transversales.md` §2.7, §2.9, §2.12, §2.17, T13, T16, T20–T22, T31–T36, T56, T58, T59, T62, T68, T69,
T73, T84, T85, T90–T95, T102; spec FR-003, FR-004, FR-008, FR-009, FR-012, FR-041, FR-045–FR-048, FR-057–FR-061,
FR-071–FR-089, FR-100, FR-103, FR-112–FR-115, FR-127, FR-130, FR-141–FR-149, I-4, D4, D5, D7, D15, D16, D19,
D23, D24, D26, D27, D29, enmienda A1 | **Hermanos**: `mensajes.md` (sobre, catálogo, destinos y registro de
participantes), `contabilidad.md` (matriz, consumidor y conciliación del lado contable), `api.md` (rutas y
permisos), `adaptadores-y-archivos.md` (canales externos, archivos planos y documentos)

Este contrato fija **lo que Cartera ofrece y consume en proceso**, sin HTTP, dentro de la base de la misma
cooperativa: el contrato de obligaciones, las consultas y reservas de Comercial, la consulta de obligaciones, los
puertos con Contabilidad, Aportes y Cumplimiento, y la orden de giro que adopta Tesorería. Los mensajes viajan por
la bandeja de la plataforma: aquí se nombran y se fija lo que cada participante adopta; su sobre, su catálogo, sus
claves de emisión y su orden los fija `mensajes.md`.

Cuatro ideas lo ordenan:

- **La lista es cerrada** (FR-008). Ningún módulo lee ni escribe tablas `LND_`; Cartera no lee ni escribe tablas de
  Nómina, Comercial, Contabilidad ni Aportes (`PAY_`, `INV_`, `ACC_`, las de `Savings`). Lo de Core, Seguridad y la
  plataforma (personas, oficinas, bancos, parámetros, bandeja, adjuntos, valores nacionales) lo usa como el resto del
  ERP, por los lectores únicos donde existen (§11). Lo que no está en §1 es un defecto, y
  `LaCarteraSoloSeTocaPorSusInterfaces` lo detecta.
- **Lo que escribe corre en la unidad de trabajo de quien llama** (I-4). Aplicar, reversar y resolver un pago, y
  reservar o liberar un número, nunca abren ni confirman una transacción propia: confirma o revierte el recaudador
  (o Comercial), y Cartera queda igual que él.
- **Fuera del pipeline, con lo que el pipeline daría.** Esos métodos no pasan por MediatR (un reintento anidado
  vaciaría el `ChangeTracker` de quien llama), así que cada uno ejecuta su validador FluentValidation y escribe su
  evento de auditoría. Es la desviación de los Principios III y X que el plan registra (T101), y la fija
  `LosPuertosDeCarteraValidanYAuditanSusEscrituras`.
- **Antes de activar, todo responde** (FR-142). Desplegada y sin activar, cada interfaz contesta sin error con su
  forma de «Cartera no activada» o «Cartera no habilitada»; §12 lo resume.

---

## 1. La lista cerrada

Lista de la spec («Interfaces que Cartera ofrece y consume») con la enmienda A1 (T97): «Dimensiones de Cartera
para Contabilidad», «Perfil regulatorio», «Autorizaciones del titular», «Oficinas, sucursales contables y
usuario–persona» y la consulta de obligaciones también para Cumplimiento.

**Ofrece**

| Interfaz | Puerto o mensaje | A quién | Provisional → real | Detalle |
|---|---|---|---|---|
| Consulta de cupo y estado; estado de validación | `IConsultasDeCartera` (existe, 2 métodos) | Comercial | `ConsultasDeCarteraNoHabilitada` → `ConsultasDeCartera` | §5.1 |
| Reservar y liberar número de crédito | `IReservasDeCartera` (2 métodos) | Comercial | `ReservasDeCarteraNoHabilitada` → `ReservasDeCartera` | §5.2 |
| Consulta de obligaciones de una persona | `IConsultaDeObligaciones` (2 métodos) | Personas, Nómina, Aportes (retiro), Cumplimiento | `ConsultaDeObligacionesSinActivar` → `ObligacionesDeCartera` | §6.1, §7, §8, §10 |
| Contrato de obligaciones | `IContratoDeObligaciones` (5 métodos + `VersionDelContrato`) | Recaudo, Nómina, Aportes, recogida de créditos propios, recaudador de prueba | `ContratoDeObligacionesSinActivar` → `ContratoDeObligacionesDeCartera` | §3 |
| No descontados de la nómina propia | dentro de `AplicarAsync` (`NoDescontado`) | Nómina | — | §3.7, §6.3 |
| Pago reversado por Cartera | mensaje `PagoReversadoPorCartera` | el recaudador del pago | — | §3.15 |
| Orden de giro y su anulación | mensajes `OrdenDeGiroEmitida`, `AnulacionDeOrdenDeGiro` | Tesorería (y `TesoreriaDePrueba`) | — | §4 |
| Obligación creada y rechazada | mensajes `ObligacionCreada`, `ObligacionRechazada` | Comercial | — | `mensajes.md`; §5.4 |
| Hechos de Cartera | mensajes `CAR → Accounting` | Contabilidad | — | `mensajes.md`, `contabilidad.md` |
| Descuentos por pagaduría (K2) | mensaje `DescuentosPorPagaduria` | Recaudo | — | `mensajes.md` |
| Dimensiones de Cartera (A1) | `IDimensionesDeCartera` (2 métodos) | Contabilidad | `DimensionesDeCartera` | §9.2 |

**Consume**

| Interfaz | Puerto o mensaje | De quién | Detalle |
|---|---|---|---|
| Venta a crédito registrada y su ajuste | mensajes `VentaACreditoRegistrada` v1 y v2, `AjusteDeVentaACredito` v1 | Comercial | `mensajes.md` |
| ¿Es contabilizable?, saldos mapeados, estado del período, indicadores, cuentas bancarias | `IContabilidadParaCartera` (5 métodos) | Contabilidad | §9.1 |
| Resultado del giro | mensaje `ResultadoDelGiro` v1 | Tesorería | §4 |
| Confirmación del envío y resultado de descuentos (K2) | mensajes `EnvioDeDescuentosConfirmado`, `ResultadoDeDescuentos` | Recaudo | `mensajes.md` |
| Saldo de aportes y ahorro permanente | `IConsultaDeAportes` | Aportes y Ahorros | §8.1 |
| Autorizaciones del titular (A1) | `IAutorizacionDeDatos` (ampliado) | Cumplimiento | §10.1 |
| Listas restrictivas | `IListasRestrictivas` | Plataforma (adaptador) | §10.2; `adaptadores-y-archivos.md` |
| Perfil regulatorio (A1) | `IPerfilRegulatorio` | Plataforma | §11.1 |
| Oficinas, sucursales contables y usuario–persona (A1) | `IAlcanceDeCartera`, `OficinasDelUsuario`, `COR_Branches`, `IActorActual` | Seguridad y plataforma | §11.2 |
| Registro de recaudadores y destinos | `IRegistroDeIntegracion` | Plataforma | §11.3; `mensajes.md` |
| Valores nacionales | `IValoresNacionales`, `ICalendarioHabil`, `INormativaVigente` | Plataforma | §11.4 |
| Personas, alta y buscador | maestro de personas (`PersonaDialog`, `PersonSearchPicker`, `COR_People`) | Personas | I-1 |
| Adjuntos, parámetros, archivos planos, bancos, notificaciones, auditoría | los de la plataforma | Plataforma | `api.md`, `adaptadores-y-archivos.md` |
| Cobros provisionales al activar | plantilla `lending.provisional-collections` | Plantilla | `plantillas.md` |

---

## 2. Reglas comunes de los puertos

| Regla | Detalle |
|---|---|
| Espacio de nombres | Los puertos que Cartera ofrece, sus DTO y sus enums viven en `IngenIA365ERP.Application.Common.Integration.Lending` (también `IDimensionesDeCartera`: son **cinco** puertos ofrecidos, T90, C27). Los de Contabilidad para Cartera, en `…Common.Integration.Accounting`; el de aportes, en `…Common.Integration.Savings`. Los records de mensaje, en `…Common.Integration.Contracts.Lending`. |
| Implementaciones | Las provisionales («no habilitada» para Comercial, «sin activar» para obligaciones) viven en el mismo espacio del puerto y se registran desde el paso 1 de T88. Las reales viven en `Application/Lending/Integration` y las reemplazan en `DependencyInjection` cuando nace `CarteraNucleo`. Responden **por cooperativa** según su activación (`LND_Activations`). |
| Idioma | Tipo de puerto, DTO y enum de puerto en español; propiedades en inglés, como `ConsultaCrediticia` y `EstadoCrediticioDto` de la 012; valores de enum en inglés e idénticos a los del motor (`ModalidadDePago.FullPayoff` ↔ `PaymentModality.FullPayoff`). DTO de salida con `Dto`, de entrada sin sufijo (§2.1 de las transversales; desviación T101). |
| Identificadores | Sólo `PublicId` y números visibles (Principio VI). Ningún Id interno de Cartera sale de Cartera. |
| Sin alcance de Cartera | Las interfaces para otros módulos **no** aplican el alcance de oficina, línea ni monto de Cartera (FR-004): las autoriza el permiso de la operación del módulo que llama. Sólo las atribuciones operativas que la spec ata a un acto (fecha valor antes de un cierre, D16) se comprueban sobre el usuario que actúa: el contrato consulta su permiso y su límite de monto por `IPermissionChecker`, nunca su alcance (C41, §3.7). |
| Cooperativa | La del ámbito de quien llama (`ITenantDbContextFactory` / `IApplicationDbContext` del ámbito). Ninguna interfaz recibe un identificador de cooperativa. |
| Tiempo | Toda consulta recibe el `CancellationToken` de quien llama, que pone su propio límite (Comercial: `Cartera.ConsultaSegundos`). Una excepción o un tiempo agotado es «Cartera no responde» y lo resuelve quien llama con su política. |
| Errores | Las consultas devuelven su DTO; un fallo técnico es excepción. El contrato de obligaciones nunca lanza por un rechazo de negocio: responde `Rejected` con código y mensaje (§3.4). Los mensajes de error van en español, dicen qué hacer y nunca muestran Id internos. |
| Escrituras | `AplicarAsync`, `ReversarAsync`, `ResolverSaldoAFavorAsync`, `ReservarAsync` y `LiberarAsync`: (1) exigen transacción en curso (`TransaccionExplicita.HayEnCurso(db)`); (2) ejecutan su validador FluentValidation; (3) escriben el intento por un contexto aparte y el hecho dentro de la transacción (§3.13); (4) nunca llaman `ISender` ni son `IOperacionIdempotente` ni `IReintentableAnteConcurrencia`; (5) pueden llamar `SaveChangesAsync` dentro (decide la transacción de afuera). |
| Versionado | Un DTO del contrato de obligaciones cambia sólo con `VersionDelContrato` nueva; los demás puertos son internos del despliegue y cambian con su prueba de arquitectura. |

---

## 3. Contrato de obligaciones — `IContratoDeObligaciones`

El contrato «Proveedor de Obligaciones» (FR-083, D15): todo dinero que reduce una obligación entra por aquí, salvo
los cobros provisionales incorporados al activar (FR-087). Lo define esta feature y Recaudo lo adopta; si Recaudo
necesita otro detalle, se ajusta con una versión nueva.

### 3.1 Firma

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Lending;

public interface IContratoDeObligaciones
{
    /// <summary>Versión que adopta cada recaudador en el registro de participantes («ProveedorDeObligaciones»).</summary>
    const int VersionDelContrato = 1;

    // FR-084: obligaciones de un tercero, como deudor y opcionalmente como codeudor. No escribe.
    Task<RespuestaDelContratoDto<ListadoDeObligacionesDto>> ListarAsync(
        SolicitudDeListado solicitud, CancellationToken ct);

    // FR-073, FR-085: distribución, saldos, plan nuevo e identificador de simulación. No escribe.
    Task<RespuestaDelContratoDto<SimulacionDePagoDto>> SimularAsync(
        SolicitudDeSimulacion solicitud, CancellationToken ct);

    // FR-085, FR-086, FR-112: un lote de pagos (y los no descontados de la nómina propia); una respuesta por ítem.
    Task<RespuestaDeAplicacionDto> AplicarAsync(
        SolicitudDeAplicacion solicitud, CancellationToken ct);

    // FR-080, FR-081, FR-085: reversa un pago por su clave.
    Task<RespuestaDelContratoDto<ComprobanteDeReversoDto>> ReversarAsync(
        SolicitudDeReverso solicitud, CancellationToken ct);

    // FR-076, FR-083: devuelve o aplica un saldo a favor o un anticipo.
    Task<RespuestaDelContratoDto<ComprobanteDeResolucionDto>> ResolverSaldoAFavorAsync(
        SolicitudDeResolucion solicitud, CancellationToken ct);
}
```

Enums de puerto (tipo en español, valores en inglés; §2.7 de las transversales y §14 de este documento):

| Enum | Valores | Equivale a |
|---|---|---|
| `ClaseDeRecaudador` | `Receipts`, `Payroll`, `Contributions`, `OwnLoanPayoff`, `Test` | Lista cerrada de FR-087 |
| `ClaseDeMedioDeRecaudo` | los de `PaymentMeansClass` (`Cash = 1` … `Check = 9`, `Other = 99`) más `OwnPayrollDeduction = 101`, `ExternalPaymasterDeduction = 102`, `InternalOffset = 103` | FR-082 (§3.14) |
| `ModalidadDePago` | `FullPayoff = 1`, `PastDue = 2`, `CycleInstallment = 3`, `Advance = 4`, `ExtraordinaryPrincipal = 5`, `SpecificInstallment = 6`, `FreeAmount = 7` | `PaymentModality` |
| `EleccionDeAbono` | `ReduceTerm = 1`, `ReduceInstallment = 2` | `PrepaymentChoice` |
| `FormaDeAplicacion` | `Interactive = 1`, `NonInteractive = 2` | FR-073 |
| `MotivoDeNoDescuento` | `DeductionCap`, `InsufficientNet`, `DeductionPriority`, `Termination`, `Leave`, `Disability`, `Other` | FR-112, FR-115; `DeductionPriority` (no cupo por la prelación entre obligaciones, FR-115) lo suma C56, en el puerto y en el dominio, antes de fijar la v1 |
| `ResultadoDelContrato` | `Applied`, `AlreadyApplied`, `Rejected`, `Answered` | FR-086; `Answered` = consulta atendida (listar y simular), C28 |
| `EstadoDeCartera` | `NotActivated`, `Active` | FR-142 |
| `EstadoDeObligacion` | `PendingDisbursement`, `Active`, `PaidOff`, `WrittenOff`, `Voided`, `Novated` | `LoanStatus` |
| `ConceptoDeObligacion` | `Principal`, `CurrentInterest`, `DefaultInterest`, `CurrentInterestInSuspense`, `DefaultInterestInSuspense`, `Insurance`, `CollectionFee`, `OtherCharge`, `Advance`, `CreditBalance` | `LoanConcept` |
| `RolEnLaObligacion` | `Debtor`, `CoDebtor` | — |
| `FormaDeResolucion` | `RefundThroughCollector`, `ApplyToOtherObligation`, `ApplyAsExtraordinaryPrincipal` | FR-083 |

### 3.2 El recaudador

Cada llamada dice quién recauda con `Recaudador(string Code, ClaseDeRecaudador Class)`. `Code` es el de su fila en
`COR_IntegrationParticipants` y forma, con la clave, el único `(CollectorCode, IdempotencyKey)` de
`LND_LoanOperations`.

| Clase | Quién | Registro | Formas | Clases de medio admitidas | Pagador típico |
|---|---|---|---|---|---|
| `Receipts` | Recaudo: caja, consignaciones, débito automático, respuestas de pagadurías, pago de aseguradoras | Su feature registra su fila y adopta «ProveedorDeObligaciones» v1 | `Interactive` (caja) y `NonInteractive` (lo demás) | las de la 012 salvo las de crédito (§3.14), `ExternalPaymasterDeduction` | deudor, codeudor, pagaduría, aseguradora u otro tercero |
| `Payroll` | Nómina: definitiva y ordinaria | Semilla de `PlataformaParaCartera`, código `NOM`, contrato v1 (E-03) | sólo `NonInteractive` | sólo `OwnPayrollDeduction` | el empleado deudor (G7) |
| `Contributions` | Aportes, al cruzar aportes contra deudas en un retiro | Su feature | `Interactive` o `NonInteractive` | sólo `InternalOffset` | el deudor |
| `OwnLoanPayoff` | La propia Cartera, recogida de créditos en un desembolso (FR-046) | **No se registra** (T91), código reservado `CAR` | sólo `NonInteractive` | sólo `InternalOffset` | el deudor |
| `Test` | `RecaudadorDePrueba` (FR-089) | Fila `Class = Test`, código `TST`, nunca en producción (T20) | las dos | todas salvo las de crédito | cualquiera |

Un recaudador **cuenta** si `IRegistroDeIntegracion` le da fila activa a hoy con rol `ObligationsCollector`, su
clase coincide, adoptó una versión del contrato que este despliegue soporta y no es un doble en producción. Si no,
`Rejected` con `Lending.Contract.CollectorNotRegistered` (o `Integration.Participant.VersionNotSupported` si
adoptó una versión que no existe aquí). `OwnLoanPayoff` queda exenta: es Cartera dentro de su propio desembolso.

La clase viaja como texto en los hechos (`CollectorClass` de `LoanFactDimensionsV1`) y resuelve el puente
`RecaudoPorAplicar` de la matriz (T53). El tipo de comprobante de `PagoAplicado` y `PagoReversado` es `RC`, salvo
`Payroll`, que va a `DN` (§2.19 de las transversales).

### 3.3 Ejecución dentro de la unidad de trabajo del recaudador (I-4)

`ContratoDeObligacionesDeCartera` usa el `IApplicationDbContext` del ámbito de quien llama, en la base de la misma
cooperativa. Si el recaudador no confirma, la aplicación tampoco queda (FR-086). Pasos de `AplicarAsync`
(`ReversarAsync` y `ResolverSaldoAFavorAsync` siguen el mismo orden):

1. **Intento.** Escribe `Lending.ObligationContract.Attempted` por un contexto aparte, con la clave, el recaudador,
   el método y la huella (§3.13). Sobrevive a cualquier reversión.
2. **Validador.** Ejecuta `SolicitudDeAplicacionValidator` (el mismo que tendría como comando). Un error de forma es
   `Rejected` con `Validation.Invalid` y `data.errors[] { field, code }`.
3. **Transacción.** Sin transacción en curso (`TransaccionExplicita.HayEnCurso(db)` falso):
   `Lending.Contract.TransactionRequired`. Nunca abre ni confirma la suya.
4. **Activación.** Sin Cartera activada en la cooperativa: `Lending.NotActivated` (§3.17).
5. **Registro.** El recaudador cuenta (§3.2).
6. **Validación previa contable**, fuera del cerrojo: una pregunta por combinación de dimensiones a
   `IContabilidadParaCartera.EvaluarAsync` (§3.12). **Nunca bloquea** en el contrato.
7. **Cerrojo** (`ICerrojoDeCartera`, T34), en una sola pasada para todo el lote: `LND_Setup` compartido →
   `LND_DebtorControls` exclusivos por Id (deudor y codeudores, la fila se crea si falta) → `LND_LoanControls`
   exclusivos por Id, todos en una sentencia. Las operaciones **esperan** (`FOR UPDATE` /
   `UPDLOCK, ROWLOCK, HOLDLOCK`); los cerrojos que la misma transacción ya tiene (la recogida dentro del
   desembolso) no esperan. No se usa `ICerrojoPorClave`.
8. **Idempotencia**, bajo el cerrojo: si ya hay operación con `(CollectorCode, IdempotencyKey)`, misma huella →
   `AlreadyApplied` con el comprobante original; otra huella → `Lending.Contract.KeyReused` (§3.10).
9. **Puesta al día.** Pone al día la causación del crédito hasta la fecha valor (T6), en esta misma transacción. Lo
   causado así crea sus propias partes en `LND_AccountingFactParts` (una por tipo, con la operación como tanda) y sus
   `LoanTransaction` llevan el `MessagePublicId` del hecho de su parte (C04, `mensajes.md` §7.1).
10. **Reconstruye y distribuye** con `ReconstruccionDeCredito` y `AplicadorDePagos.Distribuir`, la misma función que
    simula (FR-072). En la forma interactiva compara la huella (§3.7).
11. **Escribe** por `RegistroDeCartera`: la `LoanOperation` (comprobante), sus `LoanTransaction`, el plan nuevo si
    cambia y la posición del día; `Sequence` y `RegisteredAt` monótonos bajo el cerrojo. Si el pagador es la
    aseguradora de una reclamación `InProcess` de esa obligación, la cierra (`Paid`) en el mismo paso (T102).
12. **Numera** al final con `NumeradorDeCartera` (`LND_NumberSequences.NextValue`), en el orden del lote.
13. **Emite** los hechos por `EmisionDeCartera` (§3.7) y escribe `Lending.ObligationContract.Applied` (o
    `.Rejected`) en `COR_AuditOutbox` con la distribución completa por cuota y concepto (T23, T68).
14. **Responde** una `RespuestaDelContratoDto` por ítem.

Un fallo **técnico** (base caída, interbloqueo) se propaga como excepción y aborta la transacción del recaudador,
que la reintenta entera con su estrategia (`TransaccionExplicita`): por eso el contrato relee todo adentro.

### 3.4 Respuesta común

```csharp
public sealed record RespuestaDelContratoDto<T>(
    ResultadoDelContrato Result,   // Applied | AlreadyApplied | Rejected | Answered
    string Code,                   // éxito: el evento del hecho; rechazo: el código del error
    string Message,                // español, dice qué hacer
    T? Value,                      // el comprobante, el listado o la simulación; nulo en un rechazo
    IReadOnlyDictionary<string, object?>? Data = null); // p. ej. «simulation» en DistributionChanged

public sealed record RespuestaDeAplicacionDto(
    IReadOnlyList<RespuestaDelContratoDto<ComprobanteDeAplicacionDto>> Payments,           // en el orden del lote
    IReadOnlyList<RespuestaDelContratoDto<NoDescontadoRegistradoDto>> NotDeducted);         // en el orden del lote

public sealed record NoDescontadoRegistradoDto(
    Guid NonDeductionPublicId, Guid ObligationPublicId, int? InstallmentNumber, decimal NotDeductedAmount);
```

| Caso | `Result` | `Code` |
|---|---|---|
| Listado o simulación atendidos | `Answered` (C28: una consulta no «aplica») | `Lending.ObligationContract.Listed` / `.Simulated` |
| Listado o simulación antes de activar | `Answered`, valor vacío | `Lending.NotActivated` |
| Pago aplicado | `Applied` | `Lending.ObligationContract.Applied` |
| Pago reversado | `Applied` | `Lending.ObligationContract.Reversed` |
| Saldo a favor o anticipo resuelto | `Applied` | `Lending.ObligationContract.CreditBalanceResolved` |
| No descontado registrado | `Applied` | `Lending.ObligationContract.Applied` (el mensaje dice que la cuota sigue exigible) |
| Misma clave, misma huella | `AlreadyApplied`, con el comprobante original | el del hecho original |
| Cualquier rechazo | `Rejected`, `Value` nulo | el código de §3.16 |

Un rechazo de un ítem no rechaza a los demás: quien recauda decide (Nómina definitiva revierte todo; la ordinaria
sigue, §6). Un rechazo de lote (`TransactionRequired`, `NotActivated`, `CollectorNotRegistered`, validación del
lote) se repite en cada ítem.

### 3.5 Listar — `ListarAsync`

```csharp
public sealed record SolicitudDeListado(
    Recaudador Collector,
    Guid PersonPublicId,
    DateOnly Date,                    // normalmente HoyLocal
    bool IncludeAsCoDebtor = false);

public sealed record ListadoDeObligacionesDto(
    EstadoDeCartera LendingStatus,
    Guid PersonPublicId,
    DateOnly Date,
    IReadOnlyList<ObligacionListadaDto> Obligations);

public sealed record ObligacionListadaDto(
    Guid ObligationPublicId,
    string Number,
    string CreditLineCode,
    string CreditLineName,
    RolEnLaObligacion Role,
    EstadoDeObligacion Status,
    int DaysPastDue,
    decimal DueToday,                 // exigible hoy
    decimal CycleInstallment,         // cuota del ciclo: vencido + cuota del período
    decimal PastDue,                  // vencido
    decimal PayoffAmount,             // total para cancelar a la fecha (aplica el anticipo del mismo crédito, D7)
    decimal Advance,                  // anticipo
    decimal CreditBalance,            // saldo a favor (no se aplica solo, D7)
    IReadOnlyList<ModalidadDePago> AdmittedModalities,
    IReadOnlyList<string> BlockReasons,
    bool HasPaymentAgreement,         // acuerdo de pago vigente (K4; falso hasta entonces)
    bool ValidationFailed);           // marca de FR-052: informa, no impide pagar
```

- Lista las obligaciones vivas (`Active`, `WrittenOff`) y las que tengan anticipo o saldo a favor por resolver
  aunque estén `PaidOff`. Un castigado se lista: su pago es `RecuperacionDeCastigada`.
- `AdmittedModalities` excluye `SpecificInstallment` si la línea no la permite y `Advance` en un castigado.
- No escribe ni cambia estado; deja la lectura sensible `Lending.ObligationContract.Listed` por
  `LendingAuditEmitter` (T67), que incluye D, R y la huella de la liquidación.
- Rendimiento: dentro de los dos segundos de SC-008 para un deudor con diez obligaciones.

### 3.6 Simular — `SimularAsync`

```csharp
public sealed record SolicitudDeSimulacion(
    Recaudador Collector,
    Guid ObligationPublicId,
    ModalidadDePago Modality,
    decimal Amount,
    DateOnly ValueDate,
    EleccionDeAbono? Choice = null,           // abono extraordinario
    int? InstallmentNumber = null,            // cuota específica
    string? PaymentIdempotencyKeyToReverse = null); // simula el reverso de ese pago (FR-081, C38)

public sealed record SimulacionDePagoDto(
    string SimulationId,                       // SHA-256 canónico (HuellaDeCartera) de entradas y distribución
    Guid ObligationPublicId,
    ModalidadDePago Modality,
    decimal Amount,
    DateOnly ValueDate,
    EleccionDeAbono? Choice,
    decimal AppliedAmount,
    decimal AdvanceAmount,
    decimal CreditBalanceAmount,
    decimal Surplus,                           // vuelve al recaudador (D5)
    IReadOnlyList<LineaDeDistribucionDto> Distribution,
    SaldosDeObligacionDto BalancesAfter,
    PlanResumidoDto? NewPlan,
    IReadOnlyList<SimulacionDePagoDto> Alternatives, // abono extraordinario sin elección: una por elección
    IReadOnlyList<string> Notices);            // «el anticipo no reduce intereses…» (FR-074)

public sealed record LineaDeDistribucionDto(int? InstallmentNumber, ConceptoDeObligacion Concept, decimal Amount);

public sealed record SaldosDeObligacionDto(
    decimal Principal, decimal CurrentInterest, decimal DefaultInterest, decimal InterestInSuspense,
    decimal Insurance, decimal Charges, decimal Advance, decimal CreditBalance,
    decimal PastDue, decimal DueToday, decimal PayoffAmount, int DaysPastDue);

public sealed record PlanResumidoDto(
    int InstallmentCount, decimal InstallmentAmount, DateOnly? NextDueDate, DateOnly? LastDueDate,
    IReadOnlyList<CuotaDelPlanDto> Installments);

public sealed record CuotaDelPlanDto(
    int Number, DateOnly DueDate, decimal Principal, decimal Interest, decimal Insurance, decimal Other, decimal Total);
```

Reglas (T32, M10, M11):

- Simular es reconstruir a la fecha valor y distribuir **sin escribir**; la simulación no se guarda. Deja la lectura
  `Lending.ObligationContract.Simulated`.
- La huella se calcula de forma canónica (orden, decimales y fechas) sobre crédito, modalidad, valor, fecha valor,
  elección y la distribución resultante. Comparar la distribución, no un contador, evita «cambió» falsos cuando sólo
  corrió la causación.
- **Abono extraordinario sin `Choice`**: `Alternatives` trae dos simulaciones (reducir plazo, reducir cuota), cada una
  con su `SimulationId` y su plan (SC-003). Con `Choice`, una sola.
- **Anticipo** (`Advance`): `Notices` informa que cubre cada cuota en su vencimiento y no reduce intereses, y ofrece el
  abono a capital como alternativa (FR-074).
- **Cuota específica**: sólo si las anteriores están cubiertas, salvo que la línea lo permita; si no,
  `Lending.Payment.ModalityNotAllowed`.
- **Simular un reverso** (`PaymentIdempotencyKeyToReverse`, C38): ignora modalidad y valor; busca el pago por
  `(Collector.Code, PaymentIdempotencyKeyToReverse)` como `ReversarAsync` (§3.8) y devuelve en `Distribution` las
  diferencias que registraría el reverso y en `NewPlan` el plan resultante, para mostrar la redistribución de los pagos
  posteriores antes de confirmar (FR-081). Sin pago con esa clave, `Generic.NotFound`.
- Una fecha futura cuya usura todavía no está certificada se proyecta con la última conocida (D11) y lo dice
  `Notices`; una fecha pasada sin valor nacional responde `NationalValues.Missing`.
- Nómina usa `SimularAsync` en el ajuste de un descuento de la definitiva para mostrar el saldo exacto que queda
  (DE-06).

### 3.7 Aplicar — `AplicarAsync`

```csharp
public sealed record SolicitudDeAplicacion(
    Recaudador Collector,
    FormaDeAplicacion Form,
    IReadOnlyList<PagoAAplicar> Payments,
    IReadOnlyList<NoDescontado> NotDeducted);      // sólo Payroll; vacío para los demás

public sealed record PagoAAplicar(
    string IdempotencyKey,                // §3.10
    Guid ObligationPublicId,
    ModalidadDePago Modality,
    decimal Amount,
    DateOnly ValueDate,
    ClaseDeMedioDeRecaudo MeansClass,     // la clase, nunca el medio de la cooperativa (FR-082)
    Guid PayerPublicId,                   // persona del maestro
    ReferenciaDeRecibo Receipt,
    EleccionDeAbono? Choice = null,
    int? InstallmentNumber = null,
    string? SimulationId = null,          // obligatorio en Interactive, prohibido en NonInteractive
    Guid? DeductionFileLinePublicId = null); // K2: línea «descontado pendiente de giro» que cubre

public sealed record ReferenciaDeRecibo(
    string Number,                        // recibo del recaudador, NM o liquidación de Nómina
    DateOnly Date,
    Guid? OfficePublicId,                 // oficina del recaudador (TenantBranchPublicId)
    string? MeansReference,               // aprobación, consignación, cheque…
    string? DocumentKind);                // texto del recaudador: «Recibo», «NM», «Liquidacion»

public sealed record NoDescontado(
    string IdempotencyKey,                // la misma gramática que el pago de esa obligación
    Guid ObligationPublicId,
    int? InstallmentNumber,
    decimal ExpectedAmount,
    decimal NotDeductedAmount,
    MotivoDeNoDescuento Reason,
    string? Detail,
    Guid? SourceItemPublicId);            // el ítem que lo informó (p. ej. PAY_PayrollLoanDeductions)

public sealed record ComprobanteDeAplicacionDto(
    Guid OperationPublicId,               // la LoanOperation es el comprobante (T23)
    string Number,
    Guid ObligationPublicId,
    string ObligationNumber,
    string IdempotencyKey,
    ModalidadDePago Modality,
    DateOnly ValueDate,
    DateOnly AccountingDate,              // T36
    DateTime RegisteredAt,
    decimal Amount,                       // lo recibido
    decimal AppliedAmount,                // Amount − Surplus
    decimal AdvanceAmount,
    decimal CreditBalanceAmount,
    decimal Surplus,                      // vuelve al recaudador; nunca entra a Cartera
    IReadOnlyList<LineaDeDistribucionDto> Distribution,
    SaldosDeObligacionDto BalancesAfter,
    PlanResumidoDto? NewPlan,
    bool PaidOff,
    DateOnly? FinalityUntil,              // firmeza de la cancelación (FR-082, D23)
    string InputsHash,
    string RulesVersion,
    IReadOnlyList<string> Warnings);      // p. ej. Lending.Payment.PreferenceMissing, Lending.Prevalidation.NotPostable
```

**Las dos formas** (FR-073):

| | `Interactive` (caja, oficina) | `NonInteractive` (Nómina, débito automático, pagadurías, terceros) |
|---|---|---|
| Entrada | cada ítem con el `SimulationId` que vio el deudor | modalidad, valor y fecha valor; sin `SimulationId` |
| Bajo el cerrojo | reconstruye, distribuye y compara la huella | distribuye con lo vigente |
| Si cambió | `Rejected` con `Lending.Payment.DistributionChanged` y `Data["simulation"]` = la simulación nueva; no escribe | nunca responde «cambió» |
| Abono extraordinario | `Choice` obligatorio (la elección del deudor en ese pago) | `Choice` nulo: se aplica la preferencia vigente del deudor (`LND_PrepaymentPreferences`, D24); sin preferencia, el valor queda como saldo a favor y el comprobante lleva el aviso `Lending.Payment.PreferenceMissing` |
| Dos recaudadores a la vez | el segundo espera el cerrojo y recibe «la distribución cambió» | el segundo espera y aplica sobre el estado nuevo («cambió» rige sólo para la forma interactiva, C39) |

**Fechas** (FR-012, FR-142, D4, D16; enmienda A9):

| Regla | Si falla |
|---|---|
| `ValueDate` ≤ `HoyLocal` (T9) | `Lending.Payment.ValueDateOutOfRange` |
| `ValueDate` ≥ `HoyLocal` − `Pagos.DiasMaximosFechaValor` (5). **No rige** para `Payroll` ni, desde K2, para el pago de pagaduría con `DeductionFileLinePublicId` (A9) | `Lending.Payment.ValueDateOutOfRange` |
| Si `ValueDate` cae en un mes con cierre de Cartera vigente: el usuario que actúa (`IActorActual`) tiene `Lending.Payments.BackdateBeforeClose` y el pago no supera su límite de monto (`SEC_PermissionAmountLimits`). **El contrato lo verifica** por `IPermissionChecker` (permiso y límite; nunca el alcance de oficina, línea ni monto de Cartera, C41). Misma exención de A9 | `Lending.Payment.ValueDateBeforeClosing` |
| `ValueDate` > fecha de corte de la carga activada (los pagos que Recaudo conservó con fecha valor hasta el corte ya están en la carga) | `Lending.Payment.BeforeCutoff` |
| La fecha contable la calcula `FechaContable` con `IContabilidadParaCartera.EstadoDelPeriodoAsync` (T36); nunca la pone el recaudador | — |

**Pagador, clase de medio y referencia de libranza** (FR-082, FR-085):

- `PayerPublicId` es una persona viva del maestro: el deudor, un codeudor, la pagaduría, la aseguradora u otro
  tercero. Es el tercero del hecho `PagoAplicado` (T13).
- La clase de medio debe estar en la lista de §3.14 y ser admitida para la clase del recaudador (§3.2); si no,
  `Lending.Payment.MeansClassNotAllowed`.
- **Pagaduría externa** (`ExternalPaymasterDeduction`, desde K2): si la obligación tiene una línea «descontado
  pendiente de giro» de esa misma pagaduría, el pago **debe** traer `DeductionFileLinePublicId` de la línea que cubre
  (si no, `Lending.Payment.PayrollReferenceRequired`); con ella, Cartera levanta el pendiente de giro, la fecha valor
  es la del descuento y el hecho lleva la pagaduría como tercero. Los demás pagos de una pagaduría (períodos sin
  archivo de Cartera, cancelaciones al retiro, atrasados) van sin referencia y no levantan pendientes. En K1 el campo
  se rechaza si viene lleno (`Validation.Invalid`).
- **Nómina propia** (`OwnPayrollDeduction`): sin referencia; pagador el empleado deudor.
- **Aseguradora** con reclamación `InProcess` de esa obligación: el pago la cierra (`Paid`) con la referencia de la
  operación; lo que quede del saldo sigue su cobro (T102, FR-097).

**Obligación y modalidad**:

- Una obligación `PendingDisbursement`, `PaidOff`, `Voided` o `Novated` no recibe pagos:
  `Lending.Payment.ObligationNotPayable` (un saldo a favor que quede en una `PaidOff` se resuelve por §3.9). Una
  `WrittenOff` sí, y emite `RecuperacionDeCastigada`.
- Lo que supera lo exigible de la modalidad, o el total en una cancelación, es `Surplus` y vuelve al recaudador (D5).
  Un abono extraordinario mayor que el capital se trata como cancelación.
- Todo abono o anticipo cubre primero lo vencido (FR-072). Pagar la cuota del ciclo antes de su vencimiento no reduce
  intereses: el resto queda como anticipo de esa cuota (D8).

**No descontados** (FR-112): sólo con `Collector.Class = Payroll` y forma `NonInteractive` (si no,
`Validation.Invalid`). Cada uno se guarda en `LND_InstallmentNonDeductions` con origen `Contract`, el recaudador,
el motivo y `SourceItemPublicId`: es la marca «no aplicado» del crédito hasta K2. La cuota sigue exigible, se alerta
`Cartera.CuotaNoDescontadaPorNomina` al jefe de cartera y no se emite hecho contable. Desde K2 rige la regla del
convenio de la nómina propia (FR-115).

**Lo que emite** (T12, C01, C03, C04; tipos, claves y lista de acompañantes en `mensajes.md` §7.2): en la unidad de la
operación, `PagoAplicado` y, si corresponde, sus acompañantes `AnticipoRecibido`, `CobroDeInteresEnSuspenso`,
`RecuperacionDeCastigada`, `SaldoAFavorReconocido` (abono sin preferencia) y `AjusteDeCausacionPorFechaValor` (fecha
valor anterior a lo causado, T33), que sellan su modo; aparte, `CausacionReanudada` y `DimensionContableCambiada` si
el pago los provoca, y las partes de la causación que la puesta al día del paso 9 registra (C04). Comprobante de la
unidad `RC`, o `DN` con `Payroll`. Ningún hecho lleva cuentas.

**Ejemplo** (ilustrativo; los puertos no serializan). Nómina ordinaria aprueba la quincena: una cuota completa, otra
parcial por tope y su no descontado.

```json
{
  "collector": { "code": "NOM", "class": "Payroll" },
  "form": "NonInteractive",
  "payments": [
    {
      "idempotencyKey": "NOM:6f1c2a9e4b7d4c0e9a1f3b5d7c9e1a2b:0d4e6f8a1b3c4d5e6f7a8b9c0d1e2f3a",
      "obligationPublicId": "0d4e6f8a-1b3c-4d5e-6f7a-8b9c0d1e2f3a",
      "modality": "CycleInstallment", "amount": 412350.00, "valueDate": "2026-12-15",
      "meansClass": "OwnPayrollDeduction",
      "payerPublicId": "a1b2c3d4-0000-4000-8000-000000000001",
      "receipt": { "number": "NM-000318", "date": "2026-12-16", "documentKind": "NM" }
    },
    {
      "idempotencyKey": "NOM:6f1c2a9e4b7d4c0e9a1f3b5d7c9e1a2b:9a8b7c6d5e4f40a1b2c3d4e5f6a7b8c9",
      "obligationPublicId": "9a8b7c6d-5e4f-40a1-b2c3-d4e5f6a7b8c9",
      "modality": "CycleInstallment", "amount": 150000.00, "valueDate": "2026-12-15",
      "meansClass": "OwnPayrollDeduction",
      "payerPublicId": "a1b2c3d4-0000-4000-8000-000000000001",
      "receipt": { "number": "NM-000318", "date": "2026-12-16", "documentKind": "NM" }
    }
  ],
  "notDeducted": [
    {
      "idempotencyKey": "NOM:6f1c2a9e4b7d4c0e9a1f3b5d7c9e1a2b:9a8b7c6d5e4f40a1b2c3d4e5f6a7b8c9",
      "obligationPublicId": "9a8b7c6d-5e4f-40a1-b2c3-d4e5f6a7b8c9",
      "installmentNumber": 7, "expectedAmount": 236900.00, "notDeductedAmount": 86900.00,
      "reason": "DeductionCap", "detail": "Tope de descuentos del 50 %",
      "sourceItemPublicId": "c0ffee00-1111-4222-8333-444455556666"
    }
  ]
}
```

```json
{
  "payments": [
    { "result": "Applied", "code": "Lending.ObligationContract.Applied", "message": "Pago aplicado.",
      "value": { "number": "PAG-2026-004811", "appliedAmount": 412350.00, "surplus": 0.00,
                 "accountingDate": "2026-12-15", "paidOff": false } },
    { "result": "Applied", "code": "Lending.ObligationContract.Applied", "message": "Pago aplicado.",
      "value": { "number": "PAG-2026-004812", "appliedAmount": 150000.00, "surplus": 0.00,
                 "accountingDate": "2026-12-15", "paidOff": false } }
  ],
  "notDeducted": [
    { "result": "Applied", "code": "Lending.ObligationContract.Applied",
      "message": "No descontado registrado: la cuota sigue exigible.",
      "value": { "obligationPublicId": "9a8b7c6d-5e4f-40a1-b2c3-d4e5f6a7b8c9", "installmentNumber": 7,
                 "notDeductedAmount": 86900.00 } }
  ]
}
```

### 3.8 Reversar — `ReversarAsync`

```csharp
public sealed record SolicitudDeReverso(
    Recaudador Collector,
    string PaymentIdempotencyKey,      // la clave con que se aplicó el pago
    string Reason,                     // obligatorio (FR-080)
    ReferenciaDeRecibo? Receipt = null); // documento del recaudador que anula su recibo

public sealed record ComprobanteDeReversoDto(
    Guid OperationPublicId,            // LoanOperation PaymentReversal
    string Number,
    Guid ReversedOperationPublicId,
    string ReversedNumber,
    Guid ObligationPublicId,
    string ObligationNumber,
    DateOnly AccountingDate,           // período abierto (FR-080)
    DateTime RegisteredAt,
    decimal ReversedAmount,
    string Reason,
    IReadOnlyList<LineaDeDistribucionDto> Adjustments,      // diferencias de la reconstrucción (T33)
    IReadOnlyList<string> ReallocatedPaymentNumbers,        // pagos posteriores redistribuidos (FR-081)
    SaldosDeObligacionDto BalancesAfter,
    bool ObligationReopened,           // el pago reversado había cancelado el crédito
    IReadOnlyList<Guid> VoidedDocumentPublicIds);           // paz y salvo y certificados anulados (VigenciaDeDocumentos)
```

Reglas (FR-002, FR-080, FR-081, T31, T33):

- Sólo reversa pagos de **su** recaudador: busca el pago por `(Collector.Code, PaymentIdempotencyKey)`; sin pago,
  `Generic.NotFound`. Un pago de clase `Payroll` sólo lo reversa Nómina por aquí; `ReverseLoanPaymentCommand` (la
  pantalla de Cartera) lo rechaza con `Lending.Reversal.OwnedByCollector`.
- Un reverso no se reversa (`Lending.Reversal.OfReversal`); un pago tiene a lo sumo un reverso (índice único filtrado
  sobre la operación reversada).
- La operación de reverso guarda la clave `{PaymentIdempotencyKey}:reverso` en `LND_LoanOperations`, así no choca
  con el único del pago. Repetir con la misma clave y el mismo motivo responde `AlreadyApplied` con el reverso
  original; otro motivo, `Lending.Contract.KeyReused`.
- Cada fila propia del pago recibe su espejo con `ReversesTransactionId`; las diferencias que quedan por día, concepto
  y cuota se registran como transacciones nuevas enlazadas, con fecha contable del período abierto. El crédito queda
  exactamente como si el pago no hubiera existido (SC-004); lo cerrado no se reescribe.
- Un excedente que el recaudador ya devolvió lo resuelve el recaudador (FR-081).
- Emite `PagoReversado` con `Related` = el pago y, si la reconstrucción deja diferencias de causación, su acompañante
  `AjusteDeCausacionPorFechaValor` (C03); el reverso **hereda el modo del pago** (C08). No emite
  `PagoReversadoPorCartera`: el reverso lo pidió el recaudador.
- Fechas: no hay fecha valor que elegir; el reverso se fecha contablemente en el período abierto (T36).

### 3.9 Resolver un saldo a favor o un anticipo — `ResolverSaldoAFavorAsync`

```csharp
public sealed record SolicitudDeResolucion(
    Recaudador Collector,
    string IdempotencyKey,
    Guid ObligationPublicId,                 // donde está el saldo a favor o el anticipo
    ConceptoDeObligacion Concept,            // sólo CreditBalance o Advance
    decimal Amount,
    FormaDeResolucion Form,
    ReferenciaDeRecibo Receipt,              // egreso del recaudador al devolver; recibo interno al aplicar
    Guid? TargetObligationPublicId = null,   // ApplyToOtherObligation
    ModalidadDePago? TargetModality = null,  // ApplyToOtherObligation
    EleccionDeAbono? Choice = null,          // ApplyAsExtraordinaryPrincipal, o un abono en la obligación destino
    string? SimulationId = null,             // forma interactiva sobre la obligación destino
    string? Reason = null);

public sealed record ComprobanteDeResolucionDto(
    Guid OperationPublicId,                  // LoanOperation CreditBalanceResolution
    string Number,
    Guid ObligationPublicId,
    ConceptoDeObligacion Concept,
    FormaDeResolucion Form,
    decimal Amount,
    ComprobanteDeAplicacionDto? TargetApplication, // el pago en la obligación destino o el abono a capital
    SaldosDeObligacionDto BalancesAfter);
```

| Forma | Qué hace | Hecho |
|---|---|---|
| `RefundThroughCollector` | Baja el saldo a favor o el anticipo; el recaudador entrega el dinero por su propio proceso | `SaldoAFavorResuelto` (devuelto), contra el puente `RecaudoPorAplicar` de su clase (T53) |
| `ApplyToOtherObligation` | Lo aplica como pago (`InternalOffset`, pagador el deudor) a otra obligación **del mismo deudor**, con `TargetModality` | `SaldoAFavorResuelto` (aplicado) y `PagoAplicado` en la destino |
| `ApplyAsExtraordinaryPrincipal` | Sólo para un saldo a favor que nació de un abono sin preferencia (FR-075): lo aplica a capital de la misma obligación con la `Choice` del deudor | `SaldoAFavorResuelto` y `PagoAplicado` |

- `Amount` ≤ lo disponible del concepto; si no, `Lending.CreditBalance.Insufficient`. Una destino de otra persona:
  `Lending.CreditBalance.TargetNotOwned`. `ApplyAsExtraordinaryPrincipal` sobre un saldo de otro origen:
  `Lending.Payment.ModalityNotAllowed`.
- La operación de la obligación destino lleva la clave `{IdempotencyKey}:destino`, para no chocar con el único de
  la operación de origen.
- En una cancelación el anticipo del mismo crédito se aplica solo; el saldo a favor no (D7): se resuelve aquí.
- Mismas fechas, cerrojo (ahora de las dos obligaciones, en orden de Id), idempotencia y auditoría que aplicar.

### 3.10 Idempotencia y claves (T21)

El contrato no usa `COR_OperationKeys` (su clave es un Guid, compara el usuario central y abre su propia
transacción). Usa una clave de negocio de texto con índice único en el documento de Cartera,
`(CollectorCode, IdempotencyKey)` de `LND_LoanOperations`, y guarda junto a ella
`HuellaDeOperacion.Calcular("Lending.ObligationContract.{Metodo}", cuerpo)`. El cuerpo de la huella excluye
`SimulationId`: después de un `DistributionChanged` no se escribió nada y la misma clave sigue libre.

| Recaudador | Gramática | Quién la fija |
|---|---|---|
| Nómina | `NOM:{runPublicId:N}:{obligationPublicId:N}`, con `runPublicId` la versión **aprobada** de la nómina ordinaria o de la liquidación definitiva | T21 |
| Recogida de créditos propios | `CAR:{operaciónDeDesembolso:N}:{obligationPublicId:N}` | T21 |
| Recaudo | `REC:…` | su feature |
| Aportes | su código de módulo | su feature |
| Recaudador de prueba | `TST:{guid:N}`: la pantalla de dobles la propone con un Guid nuevo y deja repetirla, para probar la idempotencia; `TEST:` no se usa | C35 |
| Derivadas | `{clave}:reverso` (operación de reverso), `{clave}:destino` (aplicación de una resolución) | este documento |

`{MÓDULO}` es el código de módulo de `ModuloContable`, no el prefijo de las tablas. Un no descontado usa la clave
del pago de su obligación (está en otra tabla, `LND_InstallmentNonDeductions`, con su propio único). Recalcular una
nómina crea otra versión y por tanto otra clave; volver a aprobar la misma versión responde `AlreadyApplied` (SC-010).

### 3.11 Concurrencia (T34, FR-079)

- El contrato usa `ICerrojoDeCartera`, el mismo de pantallas y procesos, en el orden de §3.3 paso 7. Un lote toma
  todos sus créditos en una sola sentencia ordenada por Id: dos nóminas sobre los mismos créditos no se interbloquean
  por orden distinto; si la base elige víctima, `TransaccionExplicita` repite el trabajo entero.
- La causación masiva **salta** los créditos ocupados (`SKIP LOCKED` / `READPAST`) y nunca deja esperando a una caja;
  el contrato pone al día el crédito que encuentre atrasado (paso 9).
- Las validaciones previas (registro, validación previa contable) corren **fuera** del cerrojo.

### 3.12 Validación previa contable en el contrato (T56, enmienda A8)

`ValidacionPreviaContableDeCartera` reduce los hechos que produciría el lote a una combinación de dimensiones por
regla y pregunta una vez cada una a `IContabilidadParaCartera.EvaluarAsync`, con el tiempo de
`Contabilidad.ValidacionPreviaSegundos`. En el contrato la falta de regla **siempre advierte**, aunque
`Contabilidad.FaltaDeRegla = Bloquear`: sella `PrevalidationOutcome` en el mensaje, suma el aviso
`Lending.Prevalidation.NotPostable` al comprobante y alerta. Sin respuesta rige D7 (`Lending.Prevalidation.NoResponse`
como aviso). Un pago que ya ocurrió no se rechaza por un vacío de la matriz, y la nómina es «todo o nada».

### 3.13 Auditoría del intento y del hecho (T68, enmienda A11)

| Evento | Cuándo | Dónde | Contenido |
|---|---|---|---|
| `Lending.ObligationContract.Attempted` | Al entrar a `AplicarAsync`, `ReversarAsync` o `ResolverSaldoAFavorAsync`, antes de validar | Contexto aparte (sobrevive a la reversión del recaudador) | `OperationKey` (cada clave del lote), recaudador, método, cantidad de ítems, total, huella, usuario, oficina, dispositivo |
| `Lending.ObligationContract.Applied` · `.Reversed` · `.CreditBalanceResolved` · `.Rejected` | Al terminar cada ítem | `COR_AuditOutbox`, dentro de la transacción | lo anterior más la **distribución completa** por cuota y concepto (el antes y después de FR-003) o el código del rechazo |
| `Lending.ObligationContract.Listed` · `.Simulated` | Lecturas | `LendingAuditEmitter` | persona u obligación, D, R y huella de la liquidación |

El desenlace «revertido junto con el recaudador» **se deriva**: un intento sin hecho con la misma clave, pasado el
tiempo máximo de una transacción, fue revertido. La consulta de auditoría filtra por `OperationKey` y muestra los dos
eventos juntos. Si el dueño rechaza A11, `cartera.vigilancia` escribe `Lending.ObligationContract.RolledBack` por cada
intento sin hecho pasado su plazo.

### 3.14 Clases de medio y firmeza (FR-082, D23)

| Valor | Origen | Firmeza por defecto (`Pagos.FirmezaDiasPorClaseDeMedio`) | Admitida en el contrato |
|---|---|---|---|
| `Cash = 1`, `CreditCard = 2`, `DebitCard = 3`, `BankDeposit = 6`, `Transfer = 7`, `Voucher = 8`, `Check = 9`, `Other = 99` | `PaymentMeansClass` (012) | 10 días hábiles | sí (`Receipts`, `Test`) |
| `AssociateCredit = 4`, `CustomerCredit = 5` | `PaymentMeansClass` (012) | — | **no** (`Lending.Payment.MeansClassNotAllowed`): se declaran y se rechazan, pagar un crédito con crédito es recogida o novación (C30) |
| `OwnPayrollDeduction = 101` | Cartera | 10 días hábiles | sólo `Payroll` |
| `ExternalPaymasterDeduction = 102` | Cartera | 10 días hábiles | `Receipts`, `Test` |
| `InternalOffset = 103` | Cartera (recogida, aportes, saldo a favor) | 0: en firme al aplicarse | `OwnLoanPayoff`, `Contributions`, resoluciones, `Test` |

Una clase desconocida se rechaza. `FinalityUntil` del comprobante de una cancelación = fecha de aplicación + días
hábiles de su clase (calendario nacional, `ICalendarioHabil`); hasta entonces el paz y salvo dice «sujeta a firmeza» y
pagaré y garantías no se liberan.

### 3.15 Pago reversado por Cartera (FR-080)

Cuando un usuario con atribución reversa un pago desde Cartera (`ReverseLoanPaymentCommand`, p. ej. un cheque
devuelto), Cartera emite `PagoReversadoPorCartera` al destino del recaudador del pago
(`SolicitudDeEmision.Destino`): `Receipts` para Recaudo y para `RecaudadorDePrueba`. Los pagos de `Payroll` no se
reversan desde Cartera; tampoco los de `Contributions` mientras la feature de Aportes no declare su destino (C37):
los dos responden `Lending.Reversal.OwnedByCollector`. Los de `OwnLoanPayoff` los reversa el reverso del desembolso,
sin mensaje (§4.3, C11).

Sus campos los fija este documento (C13); `mensajes.md` §11.5 fija sobre, clave, destino y orden, con un ejemplo.

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

public sealed record PagoReversadoPorCarteraV1
{
    public const string Type = "PagoReversadoPorCartera";
    public LoanRefV1 Loan { get; init; } = new();                  // crédito
    public OriginalFactRefV1 Payment { get; init; } = new();       // el PagoAplicado reversado: mensaje, operación y número
    public Guid ReversalOperationPublicId { get; init; }           // la operación de reverso de Cartera
    public string ReversalNumber { get; init; } = string.Empty;    // su número visible
    public CollectorRefV1 Collector { get; init; } = new();        // recaudador, la clave con que aplicó y su recibo original
    public decimal ReversedAmount { get; init; }
    public DateOnly ValueDate { get; init; }                       // fecha valor del pago
    public string Reason { get; init; } = string.Empty;
    public UserRefV1 ReversedBy { get; init; } = new();            // quien reversó
    public decimal SurplusAlreadyReturned { get; init; }           // excedente ya devuelto: lo resuelve el recaudador (FR-081)
    public bool Reopened { get; init; }                            // el crédito vuelve a quedar vivo
}
```

Con `Collector.IdempotencyKey` y el recibo original el recaudador encuentra su recibo y lo anula por su propio
proceso.

### 3.16 Errores del contrato

| Código | Método | Cuándo | Qué hace quien recauda |
|---|---|---|---|
| `Validation.Invalid` | todos | forma inválida (`data.errors[] { field, code }`) | corrige la solicitud |
| `Lending.Contract.TransactionRequired` | escrituras | sin transacción en curso | defecto del recaudador: soporte |
| `Lending.NotActivated` | escrituras (`Rejected`); listar y simular (`Answered` vacío) | Cartera no activada en la cooperativa | Recaudo conserva el pago y lo aplica al activar (FR-142) |
| `Lending.Contract.CollectorNotRegistered` · `Integration.Participant.VersionNotSupported` | todos | el recaudador no cuenta en el registro | registrar o actualizar el participante (`/admin/participantes-de-integracion`) |
| `Lending.Contract.KeyReused` | escrituras | misma clave, otra huella | usar otra clave; nunca reintentar con datos distintos |
| `Generic.NotFound` | todos | obligación, persona o pago inexistente | revisar la referencia |
| `Lending.Payment.DistributionChanged` | aplicar interactivo | la distribución cambió entre simular y aplicar | mostrar `data.simulation` y volver a confirmar |
| `Lending.Payment.ValueDateOutOfRange` · `.ValueDateBeforeClosing` · `.BeforeCutoff` | aplicar, resolver | §3.7 | pedir la atribución o corregir la fecha |
| `Lending.Payment.ModalityNotAllowed` | simular, aplicar, resolver | modalidad no admitida por la línea o el estado | elegir otra modalidad |
| `Lending.Payment.MeansClassNotAllowed` | aplicar | clase desconocida, de crédito o no admitida para el recaudador | corregir la clase |
| `Lending.Payment.ObligationNotPayable` | aplicar | obligación sin desembolsar, cancelada, anulada o novada | aplicar a otra |
| `Lending.Payment.PayrollReferenceRequired` (K2) | aplicar | pago de pagaduría sin la línea pendiente que cubre | traer la referencia |
| `Lending.Reversal.OfReversal` · `.OwnedByCollector` | reversar | §3.8; `OwnedByCollector` también ante un pago de `Payroll` o `Contributions` reversado desde Cartera (§3.15, C37) | — |
| `Lending.CreditBalance.Insufficient` · `.TargetNotOwned` | resolver | §3.9 | corregir valor o destino |
| `NationalValues.Missing` | simular, aplicar | falta un valor nacional de una fecha ya causada | esperar la réplica; alerta a quien lo registra |
| Avisos (no rechazan): `Lending.Payment.PreferenceMissing`, `Lending.Prevalidation.NotPostable`, `Lending.Prevalidation.NoResponse` | aplicar | §3.7, §3.12 | informar |

### 3.17 Antes de activar (FR-142, T85)

| Método | `ContratoDeObligacionesSinActivar` (antes de `CarteraNucleo`) | `ContratoDeObligacionesDeCartera`, desplegada y sin activar | Activada |
|---|---|---|---|
| `ListarAsync` | `Answered`, `LendingStatus = NotActivated`, lista vacía, `Lending.NotActivated` | igual | §3.5 |
| `SimularAsync` | `Answered`, valor vacío, `Lending.NotActivated` | igual | §3.6 |
| `AplicarAsync` · `ReversarAsync` · `ResolverSaldoAFavorAsync` | `Rejected` con `Lending.NotActivated` en cada ítem | igual (con intento y rechazo auditados) | §3.7–§3.9 |

Recaudo conserva los pagos rechazados con «Cartera no activada» y los aplica al activar con su fecha valor; los de
fecha valor hasta el corte ya deben estar en la carga y responden `Lending.Payment.BeforeCutoff`.

### 3.18 Qué lo fija

| Prueba | Fija |
|---|---|
| `LaCarteraSoloSeTocaPorSusInterfaces` | los cinco métodos y `VersionDelContrato`; que fuera de Cartera sólo las implementaciones provisionales y los dobles lo implementan |
| `LosPuertosDeCarteraValidanYAuditanSusEscrituras` | que `AplicarAsync`, `ReversarAsync` y `ResolverSaldoAFavorAsync` ejecutan su validador y escriben su evento |
| `LosDoblesDePruebaNoSeRegistranEnProduccion` | que `RecaudadorDePrueba` no se registra con ambiente de producción |
| `NadieEscribeElLibroDeCarteraFueraDelRegistro` | que el contrato escribe sólo por `RegistroDeCartera` |
| Application: el contrato sin transacción responde `TransactionRequired`; idempotencia (misma huella, otra huella); forma interactiva con «cambió»; no descontados | reglas de §3.3 a §3.10 |
| e2e `NominaRecaudaPorElContratoTests`, `CarteraDesplegadaSinActivarTests`, `ConcurrenciaPagoYCausacionTests` (dos motores) | I-4, FR-142, FR-079 |

---

## 4. Orden de giro — Tesorería (FR-045, FR-047, FR-048, D27)

No es un puerto síncrono: son tres mensajes de la bandeja que esta feature define y Tesorería adopta, como Recaudo
adopta el contrato (D15). **Sus campos los fija este documento** (C13); el sobre, la emisión, la clave y el catálogo
(`CatalogoDeMensajesDeCarteraV1`, `CatalogoDeMensajesHaciaCarteraV1`) los fija `mensajes.md` (§11.3, §11.4, §12.4),
que remite aquí para el contenido; aquí, lo que cada lado se compromete a mandar y a hacer.

**La orden nace al desembolsar**, siempre (FR-045, T12, C10): también la que se pagará por transferencia en un archivo
de desembolsos de Cartera (FR-047). Ésa lleva `PaidByFile = true`: Tesorería no la paga por otra vía y sólo informa el
resultado. La referencia al archivo queda en la orden de Cartera (`LND_DisbursementOrders`), nunca en un mensaje, y
anular un archivo devuelve sus órdenes a la espera de otro (T78; `adaptadores-y-archivos.md` §6.2).

### 4.1 Participantes

| Mensaje | Origen → destino | Kind, entrega | Origen del mensaje, clave y dependencias |
|---|---|---|---|
| `OrdenDeGiroEmitida` v1 | `CAR` → `Treasury` | Business, `Always` | la `DisbursementOrder`, en el guardado del desembolso (`originEventKey` `Confirmation`); un giro nuevo, `Related` = la orden anterior |
| `AnulacionDeOrdenDeGiro` v1 | `CAR` → `Treasury` | Business, `Always` | la misma orden; clave `Confirmation:{ordenPublicId:N}`, para no formar unidad con la orden (C12); `Related` = la orden como documento (`DocumentRefV1`), y espera a su `OrdenDeGiroEmitida` por la arista de su propio origen |
| `ResultadoDelGiro` v1 | `TES` → `Lending` | Business | el documento de Tesorería, clave `Confirmation:{ordenPublicId:N}`; `Related` = la orden; el mensaje de la orden va en `OrderMessagePublicId` (C12) |

- Tesorería se registra en `COR_IntegrationParticipants` como destino `Treasury` (rol `MessageDestination`) y adopta
  `OrdenDeGiroEmitida` v1 y `AnulacionDeOrdenDeGiro` v1. La activación en producción exige un destino `Treasury` real
  con una versión que Cartera tenga vigente (FR-142); `TesoreriaDePrueba` no cuenta.
- `TesoreriaDePrueba` (fuera de producción, T20) consume `Treasury` y emite `ResultadoDelGiro` como `TES`, con la
  persona que prueba como usuario de origen; rutas `/api/lending/test-doubles/treasury/…` con
  `Lending.TestDoubles.Operate`.
- Sobre (T13): `PersonPublicId` = beneficiario; `BranchPublicId` = sucursal contable de la oficina del crédito;
  `OperationDate` = fecha contable del desembolso; `OriginDocumentClass` = `DisbursementOrder` (texto).

### 4.2 Contenido

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

public sealed record OrdenDeGiroEmitidaV1
{
    public const string Type = "OrdenDeGiroEmitida";
    public Guid OrderPublicId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public LoanRefV1 Loan { get; init; } = new();          // obligación: PublicId y número
    public int TrancheNumber { get; init; }
    public string Form { get; init; } = string.Empty;       // Transfer | Check | ThirdPartyPayment
    public PayeeV1 Payee { get; init; } = new();
    public decimal GrossAmount { get; init; }
    public IReadOnlyList<PayoffDeductionV1> PayoffDeductions { get; init; } = [];  // recogidas (FR-046)
    public decimal NetAmount { get; init; }                 // lo que Tesorería gira
    public DateOnly PayDate { get; init; }
    public string Concept { get; init; } = string.Empty;    // el número del crédito
    public bool PaidByFile { get; init; }                   // sale en un archivo de desembolsos de Cartera (FR-047): no pagar por otra vía (C10)
    public Guid? ReplacesOrderPublicId { get; init; }       // giro nuevo de una orden fallida
}

public sealed record PayeeV1
{
    public Guid PersonPublicId { get; init; }
    public string TaxIdType { get; init; } = string.Empty;
    public string TaxId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? BankCode { get; init; }                  // código ACH (COR_Banks.TransferCode)
    public string? BankAccountNumber { get; init; }
    public string? BankAccountType { get; init; }
}

public sealed record PayoffDeductionV1
{
    public LoanRefV1 Loan { get; init; } = new();           // el crédito recogido
    public Guid OperationPublicId { get; init; }            // su comprobante de aplicación (OwnLoanPayoff)
    public decimal Amount { get; init; }
}

public sealed record AnulacionDeOrdenDeGiroV1
{
    public const string Type = "AnulacionDeOrdenDeGiro";
    public Guid OrderPublicId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed record ResultadoDelGiroV1
{
    public const string Type = "ResultadoDelGiro";
    public Guid OrderPublicId { get; init; }
    public Guid OrderMessagePublicId { get; init; }         // la OrdenDeGiroEmitida a la que responde (C12)
    public string Outcome { get; init; } = string.Empty;    // Disbursed | Rejected | CheckNotCashed | Voided
    public DateOnly OccurredOn { get; init; }
    public decimal Amount { get; init; }
    public string TreasuryReference { get; init; } = string.Empty;
    public string? CheckNumber { get; init; }
    public string? Reason { get; init; }
}
```

El beneficiario y su cuenta se **sellan** en la orden al desembolsar (FR-045; la cuenta se propone desde la persona y
se corrige al desembolsar, pregunta I10). Ningún mensaje lleva cuentas contables (FR-126).

### 4.3 Ciclo de vida (`DisbursementOrderStatus`)

`ConsumeTreasuryResultCommand` (sin ruta) consume cada `ResultadoDelGiro` en un `SaveChanges` con su recibo
`LND_InboundMessages` (único por `MessagePublicId`) y su evaluación:

| Estado de la orden | Resultado | Estado nuevo | Efecto en Cartera | Respuesta de la entrega |
|---|---|---|---|---|
| `Issued` | `Disbursed` | `Disbursed` | ninguno: el dinero salió; desde aquí un error se gestiona como pago (FR-048) | `Processed` |
| `Issued` | `Rejected` | `Rejected` | reverso del desembolso **desde su origen** (`DisbursementReversal`, espejos de la causación, `DesembolsoReversado` con `Related` = el desembolso, `cause = Rejected`); el crédito queda `PendingDisbursement` (espera un giro nuevo) | `Processed` |
| `Issued` con anulación emitida | `Voided` | `Voided` | el mismo reverso (`cause = Voided`) | `Processed` |
| `Issued` con anulación emitida | `Disbursed` | `Disbursed` | la anulación no tuvo efecto: el desembolso no se reversa y se gestiona como pago (FR-045) | `Processed` |
| `Disbursed` (forma `Check`) | `CheckNotCashed` | `CheckNotCashed` | el mismo reverso desde su origen (`cause = CheckNotCashed`) | `Processed` |
| `Disbursed` con `Amount` ≠ `NetAmount` | — | sin cambio | evaluación negativa | `Rejected` con `Lending.DisbursementOrder.AmountMismatch` |
| cualquier otra combinación | — | sin cambio | evaluación negativa, para revisión en la bandeja (un `CheckNotCashed` que llegó antes que su `Disbursed` se reprocesa cuando éste llegue) | `Rejected` con `Lending.DisbursementOrder.InvalidTransition` |
| orden inexistente | — | — | — | `Rejected` con `Generic.NotFound` |

- **Créditos recogidos en el desembolso** (FR-046, C11). Mientras el crédito queda `PendingDisbursement`, los pagos de
  recogida (`OwnLoanPayoff`, claves `CAR:`) se conservan: el giro nuevo sólo paga el neto. Si el crédito se **anula**
  (`Voided`, con motivo, por quien tenga `Lending.Disbursements.Void`), la misma transacción reversa cada pago de
  recogida (`PagoReversado` por crédito recogido, clave `{clave CAR}:reverso`, sin `PagoReversadoPorCartera`) y los
  recogidos vuelven a su estado anterior.
- Una orden puede recibir más de un resultado (girada y luego cheque no cobrado). Repetir el mismo mensaje no cambia
  nada (recibo único, SC-010).
- Sin resultado en `Giros.DiasSinResultado`, la tarea `cartera.giros-sin-resultado` alerta `Cartera.GiroSinResultado`.
- Anular una orden emitida exige `Lending.DisbursementOrders.Void` y motivo, y emite `AnulacionDeOrdenDeGiro`; la orden
  sigue `Issued`, con la anulación emitida, hasta que Tesorería responde. Un giro nuevo es una orden nueva enlazada a
  la anterior (`ReplacesOrderPublicId`, `Related`).
- Una orden con `PaidByFile = true` sale en un archivo de desembolsos de Cartera y Tesorería no la paga por otra vía;
  el archivo se genera después del desembolso, no viaja en ningún mensaje y, si se anula sin resultados del giro, sus
  órdenes quedan disponibles para otro (C10, `adaptadores-y-archivos.md` §6.2).
- Antes de activar no hay desembolsos, así que no hay órdenes.

---

## 5. Comercial (E-02, FR-145)

### 5.1 Consulta de cupo y estado; estado de validación — `IConsultasDeCartera`

El puerto de la 012 conserva sus **dos** métodos; cambian sus DTO (DE-12) sin romper `CarteraNoHabilitada`, porque
lo nuevo entra como propiedades `init`:

```csharp
public interface IConsultasDeCartera
{
    Task<EstadoCrediticioDto> EstadoCrediticioAsync(ConsultaCrediticia consulta, CancellationToken ct);
    Task<EstadoDeValidacionDto> EstadoDeValidacionAsync(Guid messagePublicId, CancellationToken ct);
}

// Sin cambio: persona, clase del medio (asociado o cliente), valor, fecha y canal (FR-059).
public sealed record ConsultaCrediticia(
    Guid PersonPublicId, PaymentMeansClass MeansClass, decimal Amount, DateOnly OperationDate, string? SalesChannelCode);

public record EstadoCrediticioDto(
    string Status, decimal? AvailableQuota, IReadOnlyList<LineaDeCreditoDto> Lines, string? EvidenceId)
{
    public decimal? TotalQuota { get; init; }      // nuevo
    public int? DaysPastDue { get; init; }         // nuevo, con InArrears
    public string? BlockReason { get; init; }      // nuevo, con Blocked; en español y sin detalle de listas
    public bool AllowsSale { get; init; }          // nuevo: lo decide Cartera por estado y parámetro
    public virtual bool LendingEnabled => true;
}

public sealed record LineaDeCreditoDto(string Code, string Name, int? MaxTermMonths, int? MaxInstallments)
{
    public IReadOnlyList<string> Periodicities { get; init; } = [];  // nuevo: periodicidades de Comercial con equivalencia
}

public static class EstadosCrediticios     // valores de Status
{
    public const string Active = "Active";
    public const string InArrears = "InArrears";
    public const string Blocked = "Blocked";
    public const string NoRelationship = "NoRelationship";
    public const string NotEnabled = "NotEnabled";   // el de CarteraNoHabilitada
}

public static class EstadosDeValidacion
{
    public const string Pending = "Pending";
    public const string Validated = "Validated";
    public const string Failed = "Failed";
    public const string NotIncorporated = "NotIncorporated";  // nuevo (FR-143)
}
```

**`EstadoCrediticioAsync`** (FR-058, FR-059, FR-060):

- `Status`: `Active`; `InArrears` con `DaysPastDue`; `Blocked` con `BlockReason` (bloqueo manual o automático,
  validación fallida, castigo, listas, retiro, fallecimiento, insolvencia); `NoRelationship` si la persona no es
  asociada ni tiene cupo otorgado como cliente.
- `AllowsSale`: verdadero en `Active`; en `InArrears` sólo si `DaysPastDue` ≤ `Cupo.MoraMaximaParaVender`; falso en
  los demás.
- `TotalQuota` y `AvailableQuota` = cupo − saldos vivos − reservas vigentes − solicitudes aprobadas sin desembolsar
  (FR-058), como deudor y como codeudor.
- `Lines`: líneas activas de origen comercial que admiten el tipo de tercero, con plazo y cuotas máximas y las
  periodicidades de Comercial que tienen fila en `LND_CommercialTermEquivalences` (FR-022).
- Vuelve a verificar listas restrictivas (`IListasRestrictivas`) y la autorización vigente
  (`IAutorizacionDeDatos.VigenteAsync(persona, CreditProcessing)`); una coincidencia o la falta de autorización da
  `Blocked`.
- **Es una consulta**: no cambia el estado del negocio y verifica listas **sin escribir** `LND_ScreeningResults`
  (C31). Deja la lectura `Lending.CreditStatus.Queried` por `LendingAuditEmitter` con la huella de entradas y
  respuesta; `EvidenceId` es el `EventId` de ese evento (FR-059). El resultado de listas se guarda en la reserva y al
  consumir la venta, que son operaciones. Se llama varias veces por venta; SC-008: el 95 % en menos de un segundo.
- Respeta el `CancellationToken` de `Cartera.ConsultaSegundos`; una respuesta tardía no cambia la venta (012, §23.3).

**`EstadoDeValidacionAsync`** lee el recibo `LND_InboundMessages` del mensaje:

| Recibo | `Status` | `Reason` |
|---|---|---|
| no existe todavía | `Pending` | — |
| `Processed` (obligación creada, migrada o cancelada antes del corte) | `Validated` | — |
| `ConfirmedByException` | `Validated` | «confirmada por excepción» y el motivo |
| `ValidationFailed` | `Failed` | el motivo de la evaluación |
| `NotIncorporated` | `NotIncorporated` | «aceptada sin migrar en la activación» |
| `Rejected` (no se pudo convertir), reprocesable | `Pending` | el motivo y «queda para reproceso» (C40, C95) |
| `Rejected` marcado no reprocesable (`NotRetryableAt`) | `NotIncorporated` | «no se creó la obligación» y el motivo de la `ObligacionRechazada` `NoConvertible` (C95, C102: no hay crédito, como en la venta no incorporada; `Failed` queda sólo para el crédito creado con validación fallida) |

### 5.2 Reservar y liberar número — `IReservasDeCartera` (FR-057, T22)

```csharp
public interface IReservasDeCartera
{
    Task<ResultadoDeReservaDto> ReservarAsync(SolicitudDeReserva solicitud, CancellationToken ct);
    Task<ResultadoDeReservaDto> LiberarAsync(SolicitudDeLiberacion solicitud, CancellationToken ct);
}

public sealed record SolicitudDeReserva(
    string IdempotencyKey,          // INV:{paymentPublicId:N}:{monto}
    Guid PaymentPublicId,           // el pago de crédito de la venta: un pago = una obligación
    Guid DocumentPublicId,          // el documento de venta
    Guid PersonPublicId,
    PaymentMeansClass MeansClass,   // AssociateCredit | CustomerCredit
    decimal Amount,
    DateOnly OperationDate,
    string CreditLineCode,          // la línea elegida (DocumentPayment.CreditLineCode)
    string? SalesChannelCode);

public sealed record SolicitudDeLiberacion(string IdempotencyKey, Guid PaymentPublicId, string Reason);

public sealed record ResultadoDeReservaDto(
    string Status,                  // EstadosDeReserva
    string? LoanNumber,
    Guid? ReservationPublicId,
    DateOnly? ExpiresOn,
    string? Code,
    string? Message);

public static class EstadosDeReserva
{
    public const string Reserved = "Reserved";
    public const string Used = "Used";
    public const string Released = "Released";
    public const string Expired = "Expired";
    public const string NotEnabled = "NotEnabled";
    public const string Rejected = "Rejected";
    public const string NoResponse = "NoResponse";
}
```

Reglas:

- **Dentro de la confirmación de Comercial**, también cuando la venta queda en aprobación, sin guardar por su cuenta
  (puede llamar `SaveChangesAsync`; confirma Comercial). Exige transacción en curso.
- **Cerrojos** (orden global de la venta a crédito, C24): filas de Inventario → `ReservarAsync` (`LND_Setup`
  compartido → `LND_DebtorControls` del deudor, la fila se crea si falta → `LND_NumberSequences` por
  `NumeradorDeCartera`) → numerador de Inventario. Ninguna operación de Cartera toma cerrojos de Inventario, así que el
  orden es único; se mide en la prueba de volumen del POS.
- **Cupo** bajo el cerrojo: si `Amount` supera el disponible, `Rejected` con `Lending.Reservation.QuotaExceeded`
  (`data.availableQuota`); deudor bloqueado o en mora que no permite vender, `Lending.Reservation.Blocked`. La reserva
  lleva el valor y **descuenta cupo**: no hay tabla aparte de reservas de cupo.
- **Idempotencia**: único por `IdempotencyKey` en `LND_LoanNumberReservations` con su huella. Misma clave y misma
  huella devuelven el mismo número; otra huella, `Lending.Reservation.KeyReused`. Si el monto cambió, Comercial libera
  y pide otra con la clave nueva (el monto va en la clave: `{monto}` con dos decimales y punto, cultura invariante).
- **Liberar** por la clave de la reserva: `Reserved` → `Released`; repetir responde `Released`; una `Used` o `Expired`
  responde su estado sin cambiarlo; sin reserva, `Lending.Reservation.NotFound`. `DiscardInventoryDraftCommand` y
  `DiscardPosDraftCommand` liberan.
- **Vencimiento**: sin confirmar ni liberar en `Reservas.DiasDeVigencia`, la tarea `cartera.reservas-vencidas` la deja
  `Expired` con alerta `Cartera.ReservaVencida`; el número nunca se reasigna, y si después llega la v2 con ese número
  lo toma (`Expired` → `Used`) con la alerta informativa `Cartera.ReservaVencida` (C21). Una v2 cuya reserva no existe
  no se rechaza: la obligación nace con otro número (`numberSource = Assigned`); `Lending.Reservation.NotFound` queda
  para las rutas de reserva (C20).
- **Sin evaluación contable**: la reserva no produce hecho; la falta de regla nunca la frena (T56).
- **Auditoría**: `Lending.NumberReservation.Attempted` por contexto aparte; `Lending.NumberReservation.Reserved` o
  `.Released` en `COR_AuditOutbox`, dentro de la transacción (T22, T68). Validador explícito, como el contrato.
- **Falla técnica**: la reserva corre bajo un punto de guardado propio dentro de la transacción de Comercial, en los
  dos motores; si falla, vuelve a él y responde `NoResponse`, y la venta sigue sin número (D26, C25).
- **Antes de activar**: `NotEnabled` (`ReservasDeCarteraNoHabilitada` y la real sin activar).

### 5.3 Qué hace Comercial con cada respuesta (T95, DE-12, DE-13)

| Respuesta | `CreditoEnLaVenta` |
|---|---|
| `NotEnabled` (consulta o reserva) | crédito provisional, como hoy: persona identificada, aprobación por monto, pago «pendiente de validar»; sello «Contabilidad» |
| `Blocked`, o `AllowsSale = false` | `Inventory.Credit.LendingBlocked` |
| `Amount` > `AvailableQuota` | `Inventory.Credit.QuotaExceeded` con `availableQuota` |
| sin respuesta en `Cartera.ConsultaSegundos` | `Cartera.PoliticaSinRespuesta`: `Bloquear` → `Inventory.Credit.LendingNoResponse`; `PermitirConAprobacion` → origen `LendingNoResponse`, aprobación `ProvisionalCredit`, `PendingValidation` |
| consulta válida | origen `Validated`, `PendingValidation = false`, sin aprobación provisional, `ConsultationEvidence` = `EvidenceId` |
| reserva `Reserved` | `DocumentPayment.ReservedLoanNumber`, impreso en la factura como referencia del pago (`FuenteDeEmisionDeInventario`) y enviado en `VentaACreditoRegistrada` v2 con `ReservationPublicId` |
| reserva `Rejected` | la confirmación no procede: `Inventory.Credit.QuotaExceeded` o `.LendingBlocked` según el código |
| reserva `NoResponse` | la venta sigue sin número; éste llega con `ObligacionCreada` (D26) |
| pago con `CreditWithoutLending` (A6, D29) | no consulta ni reserva, sella «Contabilidad» y no emite a Cartera |

### 5.4 Respuestas por mensaje

`ObligacionCreada` (número, línea, valor, resumen del plan y estado `Validated`, `ValidationFailed`, `Migrated` o
`CancelledBeforeCutoff`) y `ObligacionRechazada` (código, motivo, reprocesable o no; una venta no convertible no la
recibe mientras siga reprocesable: se informa `Pending`, C95) llegan al destino `Inventory`;
`DestinoInventario` las guarda en `INV_LendingObligationResponses`: número y estado, nunca saldos ni cuotas
(enmienda de FR-062 de la 012). Su forma la fija `mensajes.md`.

### 5.5 Qué lo fija

| Prueba | Fija |
|---|---|
| `El_puerto_de_Cartera_declara_exactamente_sus_dos_consultas` (ampliada) | los dos métodos de `IConsultasDeCartera` y los dos de `IReservasDeCartera` |
| `InventarioNoConoceContabilidadNiCartera` · `LaCarteraSoloSeTocaPorSusInterfaces` | que Comercial sólo toca Cartera por `Common.Integration.Lending` |
| `LosPuertosDeCarteraValidanYAuditanSusEscrituras` | validador y evento de `ReservarAsync` y `LiberarAsync` |
| e2e `CreditoConCarteraTests`, `CarteraDesplegadaSinActivarTests` (venta a crédito), `Ventas/CreditoProvisionalTests` (sigue verde) | antes y después de activar |

---

## 6. Nómina (E-03, FR-088, FR-112, FR-147)

### 6.1 Consulta de obligaciones — `IConsultaDeObligaciones`

El mismo puerto sirve a Nómina, Personas, Aportes y Cumplimiento (§7, §8, §10):

```csharp
public interface IConsultaDeObligaciones
{
    // FR-061: por persona y por obligación, como deudor o codeudor.
    Task<ObligacionesDePersonasDto> ObligacionesAsync(ConsultaDeObligaciones consulta, CancellationToken ct);

    // FR-112: la cuota a descontar de cada empleado en un período de la nómina ordinaria.
    Task<CuotasDeNominaDto> CuotasDeNominaAsync(ConsultaDeCuotasDeNomina consulta, CancellationToken ct);
}

public sealed record ConsultaDeObligaciones(
    IReadOnlyList<Guid> PersonPublicIds,
    DateOnly Date,
    bool IncludeAsCoDebtor,
    bool IncludeClosed = false);           // también las cerradas, con ClosedOn (Cumplimiento)

public sealed record ObligacionesDePersonasDto(
    EstadoDeCartera LendingStatus,
    IReadOnlyList<ObligacionesDePersonaDto> People);

public sealed record ObligacionesDePersonaDto(
    Guid PersonPublicId,
    bool MigratedNotActivated,              // deudor o codeudor de la carga aplicada vigente, antes de activar (C26)
    IReadOnlyList<ObligacionDto> Obligations);

public sealed record ObligacionDto(
    Guid ObligationPublicId,
    string Number,
    string CreditLineCode,
    string CreditLineName,
    ClaseDeObligacion Kind,                 // Standard | PayrollDeduction (libranza)
    Guid? PaymasterPublicId,                // la pagaduría, persona jurídica del maestro
    RolEnLaObligacion Role,
    EstadoDeObligacion Status,
    bool IsLive,                            // Active o WrittenOff: castigadas incluidas
    DateOnly? ClosedOn,
    decimal TotalBalance,                   // total para cancelar a la fecha
    decimal DueAmount,                      // exigible a la fecha
    int AccruedInstallments,                // cuotas causadas impagas
    int DaysPastDue,
    decimal Principal,
    decimal CurrentInterest,
    decimal DefaultInterest,
    int PendingInstallments,
    decimal InstallmentAmount);

public sealed record ConsultaDeCuotasDeNomina(
    IReadOnlyList<Guid> PersonPublicIds, DateOnly PeriodStart, DateOnly PeriodEnd);

public sealed record CuotasDeNominaDto(
    EstadoDeCartera LendingStatus,
    IReadOnlyList<CuotaDeNominaDto> Installments,
    IReadOnlyList<Guid> MigratedNotActivated);  // personas de la consulta que están «migradas, sin activar» (C26)

public sealed record CuotaDeNominaDto(
    Guid PersonPublicId,
    Guid ObligationPublicId,
    string ObligationNumber,
    string CreditLineCode,
    int? InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,                         // a descontar en el período
    decimal PastDueIncluded,                // parte vencida incluida (0 en K1)
    int Order);                             // prelación entre obligaciones: fecha de la libranza (FR-111)
```

- No escribe ni aplica el alcance de Cartera; lee en bloque (`CargadorDeReconstruccion`) para una nómina de cientos de
  empleados en una llamada. Los saldos salen de la reconstrucción a `Date` con la información hasta ahora (T24).
- `CuotasDeNominaAsync` devuelve las obligaciones vivas como deudor con forma de pago nómina propia (desde K2, las del
  convenio de la nómina propia) y cuotas con vencimiento en el período, sin las cubiertas por anticipo. En K1 no
  incluye lo vencido: lo no descontado de un período sigue exigible, en cobro por caja y alertado (FR-112, C32); desde
  K2 decide el convenio de la nómina propia.
- Una excepción o el tiempo agotado es «Cartera no responde»: Nómina avisa `Payroll.Settlement.PortfolioUnavailable`
  y no bloquea el cálculo.

### 6.2 Liquidación definitiva: propuesta y aprobación «todo o nada» (FR-088, T92, DE-04)

| Paso | Qué hace Nómina | Por la interfaz |
|---|---|---|
| Proponer | `PropuestaDeDescuentosDeCartera` según `DeduccionAlRetiroModo`: `SaldoTotal` → `TotalBalance`; `SoloCuotasCausadas` → `DueAmount`; `NoProponer` → nada. Todas las vivas como deudor, castigadas incluidas, de cualquier forma de pago; nunca las de codeudor (G4). Desglose JSON con las mismas claves de hoy (`capital`, `intereses`, `mora`, `cuotasPendientes`, `cuota`, `saldoTotal`). `DeudaPropuesta` lleva `ObligationPublicId` y `ObligationNumber` | `ObligacionesAsync([empleado], hoy, IncludeAsCoDebtor: false)` |
| Ajustar | Baja el valor con motivo y muestra el saldo exacto que queda | `SimularAsync` (`FreeAmount`), sin escribir |
| Aprobar | En el gancho `antesDeContabilizar` de `SettlementRunWorkflow`, dentro de `TransaccionDeLiquidacion`: vuelve a consultar (si el conjunto vivo cambió, `Payroll.Settlement.LoanProposalOutdated`) y aplica el lote | `AplicarAsync`, `NonInteractive`, `Payroll`/`NOM`, `OwnPayrollDeduction`, pagador = el empleado, recibo = la liquidación, fecha valor = la de aprobación; modalidad `FullPayoff` (`SaldoTotal` sin ajuste), `CycleInstallment` (`SoloCuotasCausadas`) o `FreeAmount` (ajustado) |
| «Todo o nada» | Si un ítem sale `Rejected` o con `Surplus` > 0, responde `Payroll.Settlement.LoanAmountsChanged` con los valores de Cartera y se revierte todo, también lo que Cartera aplicó | — |
| Guardar | `CarteraTransactionPublicId` = `OperationPublicId` del comprobante; `RemainingBalanceAfter` = `BalancesAfter.PayoffAmount` | — |
| Reversar | Por cada descuento aplicado, dentro de la misma transacción; un rechazo revierte la reversión entera; guarda `CarteraReversalPublicId` | `ReversarAsync` con la clave del pago y el motivo |

`DESC_CARTERA` contabiliza en el NM y en la definitiva contra el puente «recaudos por nómina para Cartera» (T53,
enmienda D-08 de la 010); sin cuentas del concepto, la primera nómina con descuentos de Cartera no se aprueba
(`Payroll.Settlement.ConceptAccountsMissing`).

### 6.3 Nómina ordinaria como pagaduría interna (FR-112, T92, DE-07)

| Paso | Qué hace Nómina | Por la interfaz |
|---|---|---|
| Calcular | `CalculationInputLoader` convierte cada cuota en novedad `LoanDeduction`, una por obligación, con `PAY_Novelties.ObligationPublicId` y `ObligationNumber`. El motor no salta el tope: la cuota de Cartera va primera entre las deducciones autorizadas, después de las de ley; lo que no cabe queda no descontado con motivo. `PAY_PayrollLoanDeductions` guarda esperado, descontado, no descontado, motivo y estado (`PayrollLoanDeductionStatus`) | `CuotasDeNominaAsync(personas, inicio, fin)` |
| Vista previa | Simula el lote antes de aprobar | `SimularAsync` por ítem |
| Aprobar | `ApprovePayrollRunCommand` pasa a `TransaccionExplicita` y aplica el lote **antes** del NM, con lo descontado y lo no descontado en la misma llamada | `AplicarAsync`, `NonInteractive`, `Payroll`/`NOM`, `OwnPayrollDeduction`, `CycleInstallment` por lo descontado, pagador = el empleado, recibo = el NM, fecha valor = fin del período (exenta de D4 y de la atribución antes de un cierre, A9); `NoDescontado` con `MotivoDeNoDescuento` (`DeductionCap`, `InsufficientNet`, `DeductionPriority`, `Termination`, `Leave`, `Disability`, `Other`; C56) y `SourceItemPublicId` = la fila de `PAY_PayrollLoanDeductions` |
| Respuesta | Un `Rejected` o un `Surplus` **no** bloquea: la línea `DESC_CARTERA` baja a lo aplicado, el neto del empleado sube, la fila guarda código y mensaje (`Rejected`, `PartiallyApplied`) y se avisa `Payroll.LoanDeductionNotApplied` | `RespuestaDeAplicacionDto` |
| Reversar | `ReversePayrollRunCommand` reversa sus recaudos, todo o nada (G3) | `ReversarAsync` |
| Consultar | `GET /api/payroll/runs/{runId}/loan-deductions` (`Payroll.Runs.View`) muestra recaudos y no descontados | — |

Cartera guarda cada no descontado en `LND_InstallmentNonDeductions` (origen `Contract`) y alerta
`Cartera.CuotaNoDescontadaPorNomina` (§3.7). Un usuario de Cartera no reversa estos pagos
(`Lending.Reversal.OwnedByCollector`).

### 6.4 Claves, registro y «Cartera por activar»

- Clave: `NOM:{runPublicId:N}:{obligationPublicId:N}`, con la versión **aprobada** de la nómina o de la liquidación;
  las obligaciones del lote van en orden ascendente de `ObligationPublicId` (§3.10).
- Registro: semilla idempotente de `PlataformaParaCartera` en cada cooperativa, clase `Payroll`, código `NOM`, rol
  `ObligationsCollector`, adopción «ProveedorDeObligaciones» v1 (T16, CH16).
- Antes de activar no hay obligaciones exigibles: las dos nóminas se aprueban sin descuentos de Cartera. Si alguna
  persona viene en `MigratedNotActivated`, Nómina avisa `Payroll.LendingPendingActivation` al calcular y la aprobación
  exige `AcknowledgeLendingPendingActivation` (si falta, `Payroll.LendingPendingActivationAckRequired` con los
  nombres); el reconocimiento queda en `PAY_PayrollRuns.LendingActivationAckBy/At` y en la auditoría (DE-10).

### 6.5 Festivos, salario mínimo y UVT (FR-147, T93, enmienda A7)

No es una interfaz de Cartera: es la réplica de valores nacionales de la plataforma. Cuando la réplica trae `SMMLV`,
`UVT` o `FESTIVO`, `EfectoDeReplicacionEnNomina` (`IEfectoDeReplicacion`) escribe en la misma transacción la
proyección de Nómina: fila de `PAY_LegalParameters` con fuente «Valor nacional {SourcePublicId}» y cierre de la
vigencia anterior, o fila de `PAY_Holidays` con `HolidayOrigin.National`.

- Adopta lo que ya existe: misma fecha y mismo valor → se marca como proyección; valor distinto → se conserva la de
  Nómina y se alerta `Nomina.ValorNacionalDistinto`.
- Sólo la réplica escribe esas filas: `AddLegalParameterVersionCommand` rechaza `SMMLV` y `UVT`
  (`Payroll.LegalParameter.NationalValue`) y los festivos `National` no se editan. Los `Manual` siguen siendo de
  Nómina y Cartera no los usa.
- Los 13 lectores de `PAY_LegalParameters`, `LectorDeUvt` y los motores no cambian.
- Lo fijan `SoloLaReplicaEscribeLosValoresNacionalesDeNomina`, `LaUvtSeLeeEnUnSoloSitio` y la e2e
  `ReplicaSobreCooperativaSembradaTests`.

### 6.6 Errores y avisos de Nómina

| Código | Tipo | Cuándo |
|---|---|---|
| `Payroll.Settlement.PortfolioUnavailable` | aviso | la consulta falló o no respondió; la propuesta de créditos sale vacía |
| `Payroll.Settlement.LoanProposalOutdated` | rechazo | al aprobar, las obligaciones vivas difieren de las propuestas: recalcular |
| `Payroll.Settlement.LoanAmountsChanged` | rechazo | un ítem rechazado o con excedente en la definitiva |
| `Payroll.Settlement.ConceptAccountsMissing` | rechazo | `DESC_CARTERA` sin cuentas |
| `Payroll.LendingPendingActivation` | aviso | hay personas «migradas, sin activar» |
| `Payroll.LendingPendingActivationAckRequired` | rechazo | aprobar sin reconocer lo anterior |
| `Payroll.LoanDeductionNotApplied` | aviso | la ordinaria aprobó con ítems rechazados o con excedente |
| `Payroll.LegalParameter.NationalValue` | rechazo | intentar escribir a mano `SMMLV` o `UVT` |

### 6.7 Qué lo fija

`LaCarteraSoloSeTocaPorSusInterfaces` (Nómina no depende de la Cartera nueva salvo `Common.Integration.Lending`;
ninguna FK ni navegación de `PAY_` a `LND_`); `LosUnicosDeDescuentosSoloCuentanFilasVivasTests`
(`UK_PAY_SettlementDeductions_Termination_Obligation`); casos dorados de Nómina con la cuota de Cartera, tope y neto
insuficiente; Application: «todo o nada» de la definitiva, ordinaria con rechazo y excedente que no bloquean, reversas
de las dos nóminas, reconocimiento de «Cartera por activar»; e2e `NominaRecaudaPorElContratoTests` (incluye que un
usuario de Cartera no reversa un pago de Nómina) y `CarteraDesplegadaSinActivarTests` (definitiva y ordinaria).

---

## 7. Personas (E-04, FR-148)

Usa `IConsultaDeObligaciones.ObligacionesAsync` (§6.1).

| Consumidor | Llamada | Regla |
|---|---|---|
| `DeletePersonCommand` | `ObligacionesAsync([persona], hoy, IncludeAsCoDebtor: true)` | bloquea con `Person.HasActiveLoans` (mensaje nuevo: «tiene obligaciones vivas como deudor o codeudor, o está en una carga de Cartera por activar») si hay alguna con `IsLive` o si `MigratedNotActivated` |
| `GetPersonDetailQuery` | la misma | `PortfolioSummaryDto` conserva su forma y suma `LendingStatus` (`NotActivated`, `Active` o «migrada, sin activar»); los ahorros siguen en `SavingsAccounts` |

| Estado de Cartera | Lo que ve Personas |
|---|---|
| `ConsultaDeObligacionesSinActivar` | ninguna obligación; se borra como hoy |
| Desplegada y sin activar, con carga aplicada | deudores y codeudores de la carga vigente «migrados, sin activar»: no se borran |
| Activada | las obligaciones reales, como deudor o codeudor |

Se agrega la prueba que hoy falta de la rama de borrado (deudor, codeudor y migrado); la e2e
`CarteraDesplegadaSinActivarTests` cubre el borrado con Cartera sin activar.

---

## 8. Aportes

### 8.1 Saldo de aportes y ahorro permanente — `IConsultaDeAportes` (Cartera consume)

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Savings;

public interface IConsultaDeAportes
{
    Task<SaldosDeAportesDto> SaldosAsync(SolicitudDeSaldoDeAportes solicitud, CancellationToken ct);
}

public sealed record SolicitudDeSaldoDeAportes(IReadOnlyList<Guid> PersonPublicIds, DateOnly Date);

public sealed record SaldosDeAportesDto(bool Available, IReadOnlyList<SaldoDeAportesDto> Balances);

public sealed record SaldoDeAportesDto(
    Guid PersonPublicId, decimal Contributions, decimal PermanentSavings, DateOnly AsOf);
```

- La implementa la feature de Aportes y Ahorros. Hasta entonces rige `ConsultaDeAportesNoDisponible`:
  `Available = false`, sin saldos.
- Con «no disponible» (D19, lo más conservador): el cupo calculado usa el saldo cargado por plantilla
  (`LND_ContributionBalanceLoads`, `lending.contribution-balances`) con su fecha y alerta; el deterioro **no**
  descuenta aportes (FR-103); el arrastre **no** aplica la excepción de aportes (FR-100;
  `Riesgo.ExcepcionAportesArrastre` forzado a falso).
- Cartera no lee `Entities.Savings` ni `Application.Savings`: sólo este puerto (§13).

### 8.2 Retiro de asociado (Aportes consume la consulta de obligaciones)

`WithdrawalQueries` y `ProcessWithdrawalCommand` (mudados a `Application/Savings`, T3) dejan de leer
`LND_LoanPortfolios` y llaman `ObligacionesAsync([asociado], hoy, IncludeAsCoDebtor: true)`: es el consumidor
«Aportes, en el retiro» de FR-061 (T94, A5). Antes de activar ven «migrado, sin activar» igual que Personas.

### 8.3 Aportes como recaudador (feature de Aportes)

Al cruzar aportes contra deudas en un retiro, Aportes recauda por el contrato con clase `Contributions`, clase de
medio `InternalOffset` (en firme al aplicarse), pagador el asociado y la clave con su código de módulo. Se registra
en su feature; hasta entonces el cruce no existe y el retiro sólo informa las obligaciones.

---

## 9. Contabilidad (E-01, FR-146)

Cartera nunca llama al poster ni conoce una cuenta: emite hechos (`mensajes.md`, `contabilidad.md`) y pregunta por
estos dos puertos. La frontera la fija `CarteraNoConoceContabilidad` en las dos direcciones.

### 9.1 Lo que Cartera consume — `IContabilidadParaCartera`

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Accounting;

public interface IContabilidadParaCartera
{
    // FR-127: ¿las líneas que generarían estos hechos cumplen las reglas y está abierto el período?
    Task<Result<ResultadoDeContabilizacionDto>> EvaluarAsync(
        IReadOnlyList<MensajeContableDto> hechos, CancellationToken ct);

    // FR-130, FR-141: saldo contable de las cuentas que la matriz vigente al corte asigna a estas combinaciones.
    Task<Result<IReadOnlyList<ConjuntoDeCuentasDeCarteraDto>>> SaldosDeCuentasMapeadasAsync(
        DateOnly corte, IReadOnlyList<CombinacionDeCartera> combinaciones, CancellationToken ct);

    // FR-012, FR-107: iniciada, período existente, abierto o cerrado, y primer mes abierto desde la fecha.
    Task<Result<EstadoDelPeriodoDto>> EstadoDelPeriodoAsync(DateOnly fecha, CancellationToken ct);

    // FR-041, FR-100, FR-103: pérdidas derivadas de las cuentas; patrimonio técnico y solvencia registrados.
    Task<Result<IndicadoresDeLaEntidadDto>> IndicadoresDeLaEntidadAsync(DateOnly fecha, CancellationToken ct);

    // FR-047: cuentas bancarias del plan, opcionalmente de un banco.
    Task<Result<IReadOnlyList<CuentaBancariaDto>>> CuentasBancariasAsync(Guid? bancoPublicId, CancellationToken ct);
}

// Se reutilizan de la 012, sin cambios: MensajeContableDto(Envelope, Payload), ResultadoDeContabilizacionDto,
// HallazgoContableDto, QuienCorrigeDto, CuentaDelConjuntoDto y SaldoPorSucursalDto.

public sealed record CombinacionDeCartera(
    string Role,                  // rol de saldo de RolesDeCuentaDeCartera, como texto; los admitidos los publica contabilidad.md (C29)
    string? CreditLineCode,
    string? ModalityCode,
    string? SubsegmentCode,
    string? RiskCategory,
    string? ConceptCode,
    bool? IsEmployee,
    bool? InGracePeriod,
    bool? OnBehalfOfAssociate,
    bool? Restructured,
    string? CollectorClass,
    Guid? BranchPublicId);

public sealed record ConjuntoDeCuentasDeCarteraDto(
    IReadOnlyList<CombinacionDeCartera> Combinations,   // componente conexo combinación ↔ cuenta (T58)
    IReadOnlyList<CuentaDelConjuntoDto> Accounts,       // para mostrar; Cartera no las guarda ni las emite
    decimal Balance,
    bool RuleMissing);                                   // combinaciones sin regla al corte

public sealed record EstadoDelPeriodoDto(
    bool AccountingInitialized,
    int Year,
    int Month,
    bool PeriodExists,
    bool IsOpen,
    DateOnly? FirstOpenMonth);                           // primer día del primer mes abierto desde la fecha

public sealed record IndicadoresDeLaEntidadDto(
    DateOnly Date,
    decimal? CurrentYearResult,                          // clases 4 a 7 del ejercicio, sin el CI
    decimal? AccumulatedLosses,                          // saldos de 3510, 3610 y 3910
    IndicadorRegistradoDto? TechnicalCapital,            // último registrado con corte ≤ fecha
    IndicadorRegistradoDto? SolvencyRatio);

public sealed record IndicadorRegistradoDto(
    decimal Value, DateOnly CutoffDate, string Source, Guid? SupportAttachmentPublicId);

public sealed record CuentaBancariaDto(
    Guid AccountPublicId,
    string DisplayName,                                  // lo arma Contabilidad para el selector
    Guid BankPublicId,
    string BankName,
    string? AchCode,                                     // COR_Banks.TransferCode
    string BankAccountNumber,
    string? BankAccountType);
```

| Método | Quién lo usa en Cartera | Reglas |
|---|---|---|
| `EvaluarAsync` | `ValidacionPreviaContableDeCartera` | Delega en `EvaluateLendingPostingQuery`, que arma las líneas con el mismo `ConstructorDeLineasDeCartera` del consumidor y las valida con `ValidarVariosAsync` más las comprobaciones de la matriz. Cartera manda **un mensaje por combinación** de dimensiones. Errores y avisos con `WhoFixes` (`contabilidad.md`). Bloquea sólo en las pantallas de desembolso, novedad, condonación y castigo con `Contabilidad.FaltaDeRegla = Bloquear` (`Lending.Prevalidation.NotPostable`, 422 con `data.errors[]`); en el contrato, el consumidor de Comercial y los procesos, advierte (A8). Tiempo: `Contabilidad.ValidacionPreviaSegundos`; sin respuesta, `Contabilidad.PoliticaSinRespuesta` (D7). La reserva de número y el alta de una obligación desde una venta no se evalúan para frenar (T56). |
| `SaldosDeCuentasMapeadasAsync` | `ConciliacionContableDeCartera` (FR-130), cuadre de la carga y de la activación (FR-141), requisito de activación de la regla `RecaudoPorAplicar` con `CollectorClass = Payroll` (T84) | Delega en `GetLendingAccountBalancesQuery`: resuelve cada combinación con la matriz vigente al corte, arma conjuntos por familia de rol (`ConjuntosDeCuentas`) y lee saldos sólo por `MovimientosContables`, apertura incluida y sin restricción de sucursal. Sin contabilidad iniciada, `Accounting.NotInitialized`. Cartera compara por **fecha contable** y explica cada diferencia con sus hechos pendientes, en lote, rechazados o que no pasan; descuadre sin explicar → `Cartera.DescuadreContable`. |
| `EstadoDelPeriodoAsync` | `FechaContable` (T36), reabrir un cierre (D12), ordenar un cierre (T47), activar (FR-142) | La fecha contable es la fecha valor si su mes no tiene cierre de Cartera y está abierto en Contabilidad; si no, el primer día del primer mes que cumpla las dos cosas. Contabilidad nunca la corre. Activar exige contabilidad iniciada y abierto el mes del día siguiente al corte. |
| `IndicadoresDeLaEntidadAsync` | concentración (FR-041), arrastre (FR-100), deterioro (FR-103) | Cartera trata un indicador como «no disponible» si falta o su corte es más viejo que `Contabilidad.IndicadoresVigenciaMeses` (6): el puerto devuelve el último registrado con su corte y decide Cartera (C33). Sin patrimonio técnico disponible: se **bloquean** el desembolso, la asignación de cupo y la novedad que aumenten el endeudamiento (`Lending.Concentration.TechnicalCapitalUnavailable`) y se alerta `Cartera.PatrimonioTecnicoNoDisponible`; en FR-100 y FR-103, lo conservador de D19. |
| `CuentasBancariasAsync` | archivo de desembolsos (FR-047, T78) | Delega en `ListBankAccountsQuery`. La cuenta de origen se elige entre las del banco del formato; Cartera guarda `AccountPublicId`, nunca el código de la cuenta, y ningún hecho lleva cuentas. |

Implementación: el adaptador `ContabilidadParaCartera` de `Application/Accounting/Lending`. Responde igual antes y
después de activar Cartera: la migración y la activación lo usan antes.

### 9.2 Lo que Cartera ofrece — `IDimensionesDeCartera` (enmienda A1)

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Lending;

public interface IDimensionesDeCartera
{
    // Los códigos vivos de cada dimensión de la matriz de Cartera.
    Task<CatalogoDeDimensionesDeCarteraDto> CatalogoAsync(CancellationToken ct);

    // Las combinaciones en uso de los tipos de hecho cuyo modo vigente a la fecha no es «no pasa».
    Task<IReadOnlyList<CombinacionEnUsoDeCarteraDto>> CombinacionesEnUsoAsync(DateOnly fecha, CancellationToken ct);
}

public sealed record CatalogoDeDimensionesDeCarteraDto(
    IReadOnlyList<LineaDeDimensionDto> CreditLines,
    IReadOnlyList<CodigoDeDimensionDto> Modalities,      // del CatalogoDeModalidades vigente
    IReadOnlyList<CodigoDeDimensionDto> Subsegments,
    IReadOnlyList<CodigoDeDimensionDto> Concepts,        // valores de LoanConcept
    IReadOnlyList<CodigoDeDimensionDto> CollectorClasses,// valores de ClaseDeRecaudador
    IReadOnlyList<CodigoDeDimensionDto> ReasonCodes);    // motivos que Cartera pone en sus hechos

public sealed record LineaDeDimensionDto(
    string Code, string Name, string ModalityCode, string? SubsegmentCode, bool IsActive);

public sealed record CombinacionEnUsoDeCarteraDto(
    string Operation,                                    // tipo de hecho (OperacionesDeCartera)
    string? CreditLineCode,
    string? ModalityCode,
    string? SubsegmentCode,
    string? RiskCategory,
    string? ConceptCode,
    string? CollectorClass,
    bool? IsEmployee,
    bool? InGracePeriod,
    bool? Restructured,
    bool FromMigrationLoad,                              // antes de activar: combinaciones de la carga aplicada
    DateOnly? LastUsedAt);
```

- Lo usa Contabilidad para validar los códigos al guardar o importar una regla (`CreateLendingPostingRuleCommand`,
  `ImportLendingPostingRulesCommand`) y para la completitud (`GetLendingRulesCompletenessQuery`). Sin él, una regla
  con un código mal escrito no resolvería nunca y sólo se notaría como «regla faltante».
- Antes de activar devuelve las combinaciones de la carga aplicada vigente (`FromMigrationLoad = true`), para que la
  contadora complete la matriz antes del cuadre.
- Implementación `DimensionesDeCartera` en `Application/Lending/Integration`. `CodigoDeDimensionDto` es el de la 012.

### 9.3 Qué lo fija

| Prueba | Fija |
|---|---|
| `CarteraNoConoceContabilidad` | los cinco métodos de `IContabilidadParaCartera`; Cartera no depende de `*.Accounting*` salvo `Common.Integration.Accounting`; `Application/Accounting/Lending` no depende de `Domain.Entities.Lending` |
| `LaCarteraSoloSeTocaPorSusInterfaces` | los dos métodos de `IDimensionesDeCartera` |
| `LosHechosDeCarteraNoLlevanCuentas` | ningún contrato de mensaje de Cartera tiene cuentas |
| `LoDeCarteraSeCorrigeConComprobanteNuevo` | nada de Cartera usa `PrepareReversalAsync` |

---

## 10. Cumplimiento (enmienda A1)

### 10.1 Autorizaciones del titular — `IAutorizacionDeDatos` (Cartera consume)

El registro de autorizaciones es de la plataforma (`CMP_HabeasDataConsents`, `CMP_HabeasDataPolicyVersions`, T69);
Cartera no lo duplica. El puerto existente (`Application.Compliance.HabeasData`) se amplía:

```csharp
public interface IAutorizacionDeDatos
{
    // Existente: la última decisión de tratamiento general (la usan la 012 y las promociones).
    Task<AutorizacionVigente?> VigenteAsync(Guid personPublicId, CancellationToken ct);

    // Nuevo: por finalidad y, para ContactChannel, por canal.
    Task<AutorizacionVigente?> VigenteAsync(
        Guid personPublicId, DataAuthorizationPurpose finalidad, string? canal, CancellationToken ct);

    // Nuevo: agrega la autorización a la unidad de trabajo sin guardar (la guarda quien llama).
    void Agregar(AutorizacionAAgregar autorizacion);
}

public sealed record AutorizacionAAgregar(
    Guid PersonPublicId,
    DataAuthorizationPurpose Purpose,       // GeneralTreatment | CreditProcessing | CreditBureauQuery | CreditBureauReport | ContactChannel
    DecisionDeAutorizacion Decision,
    Guid? PolicyVersionPublicId,
    string? ContactChannel,                 // Sms | WhatsApp | Email | Call | Visit
    Guid? EvidenceAttachmentPublicId,       // adjunto DataAuthorization
    string Origin,                          // PersonRegistration | CreditApplication | Migration | TitularRequest | Import
    DateTime DecidedAt);

// AutorizacionVigente suma (init): Purpose y HasEvidence.
```

| Quién en Cartera | Finalidad | Sin autorización |
|---|---|---|
| Radicación (la escribe con `Agregar` en su transacción) | `CreditProcessing`, `CreditBureauQuery`, `CreditBureauReport`, `ContactChannel` | `Lending.Authorization.Missing`: se bloquea y se explica cómo obtenerla |
| Consulta a centrales y archivo de centrales | `CreditBureauQuery`, `CreditBureauReport` | bloquea; migrada sin evidencia → `Lending.Authorization.WithoutEvidence` |
| Mensajes al deudor de cobranza | `ContactChannel` del canal | `Lending.DebtorMessage.ChannelNotAuthorized` |
| Consulta de cupo y venta (§5.1) | `CreditProcessing` | `Blocked` |
| Activación (nacen las migradas) | todas | — (se crean con su evidencia o sin ella) |

Una autorización migrada sin evidencia deja el crédito vivo y cobrable: sólo bloquea la consulta y el reporte a
centrales y los mensajes de cobranza (FR-006).

### 10.2 Listas restrictivas — `IListasRestrictivas` (Cartera consume)

Puerto de plataforma (`Application/Common/Compliance`) con el canal `CARGA-PROPIA` sobre `COR_RestrictiveListLoads`.
Cartera consulta en la radicación, al consumir una venta y al otorgar cupo, y guarda cada resultado de esas
operaciones en `LND_ScreeningResults`; una coincidencia bloquea, alerta `Cartera.CoincidenciaEnListas` y sólo la
libera quien tenga `Compliance.RestrictiveLists.Release`, con motivo, y no haya radicado
(`ReleaseScreeningMatchCommand`). El contrato del adaptador está en `adaptadores-y-archivos.md`.

### 10.3 Cumplimiento consume la consulta de obligaciones

Al atender una revocación o una supresión (`COR_DataSubjectRequests`), `ReglaDeRevocacion` pregunta si hay un deber
legal de conservar o reportar:

- llama `ObligacionesAsync([titular], hoy, IncludeAsCoDebtor: true, IncludeClosed: true)`;
- hay deber si alguna obligación `IsLive`, si la persona está `MigratedNotActivated` o, para `CreditBureauReport`, si una
  cerrada sigue dentro de la permanencia del dato negativo (límite legal con fuente);
- `ContactChannel` procede siempre;
- la respuesta queda como constancia en la solicitud (FR-006).

---

## 11. Plataforma y seguridad (enmienda A1)

No son puertos de Cartera: son lo que Cartera consume de la plataforma y de Seguridad. Se listan porque la lista
cerrada los nombra.

### 11.1 Perfil regulatorio — `IPerfilRegulatorio`

```csharp
namespace IngenIA365ERP.Application.Common.NationalValues;

public interface IPerfilRegulatorio
{
    Task<PerfilRegulatorioDto?> VigenteAsync(DateOnly fecha, DateTime? informacionHasta, CancellationToken ct);
}

public sealed record PerfilRegulatorioDto(
    Guid VersionPublicId,
    DateOnly ValidFrom,
    string OrganizationTypeCode,            // del documento TiposDeOrganizacion
    bool FinancialActivityAuthorized,
    string SupervisionLevel,
    string? Category,                       // fondos de empleados
    string NiifGroup,
    bool IsPayrollOperator,                 // registro como operador de libranza (FR-108)
    string? PayrollOperatorRegistrationNumber,
    DateOnly? PayrollOperatorRegistrationDate);
```

Lee `COR_RegulatoryProfileVersions` con la regla bitemporal (T38). Lo usan `SelectorDeModeloDeDeterioro`, la
originación a no asociados y por libranza (FR-108), las mayorías para vinculados, la obligación sobrevenida (D28) y la
activación (cooperativa obligada, FR-142). Sin perfil vigente, la activación lo lista como faltante.

### 11.2 Oficinas, sucursales contables y usuario–persona

| Qué | Cómo lo lee Cartera | Para qué |
|---|---|---|
| Oficinas del usuario | `IAlcanceDeCartera` sobre `OficinasDelUsuario` (`SEC_UserBranchAssignments` → `ADM_Branches` → `COR_Branches` por `TenantBranchPublicId`) y `LND_UserCreditLineScopes` | alcance de pantallas y rutas, que falla cerrado (T62) |
| Sucursal contable de una oficina | `COR_Branches.TenantBranchPublicId` | `BranchPublicId` del sobre (T13); una oficina con créditos sin sucursal vinculada bloquea la operación y la activación (CH49) |
| Persona del usuario | `SEC_Users.PersonId` por `IActorActual` | segregación por persona (T66); sin persona, `Lending.Security.UserWithoutPerson` |

Lo fijan `LasConsultasDeCarteraRespetanElAlcance` y `LaSegregacionNoUsaUserIdDelToken`.

### 11.3 Registro de recaudadores y destinos

`IRegistroDeIntegracion` (lector) y `EscritorDelRegistroDeIntegracion` (agrega sin guardar): el contrato lo usa para
validar al recaudador (§3.2), la activación para exigir Recaudo y Tesorería reales y habilitar el destino `Lending`
en el mismo guardado (T16, T84). Detalle en `mensajes.md`.

### 11.4 Valores nacionales

`IValoresNacionales` (`LeerAsync`, `LeerUltimoPublicadoAsync`, `LeerSerieAsync`), `ICalendarioHabil` (`EsHabil`,
`SiguienteHabil`, `Contar`) e `INormativaVigente` son los únicos lectores de `COR_NationalValues` y
`COR_RegulatoryDocuments` (T40); `ISeriesRegulatorias` de Cartera se construye sobre ellos. Un valor faltante es
`NationalValues.Missing`. Lo fijan `LosValoresNacionalesSeLeenEnUnSoloSitio` y `SoloElReplicadorLeeElRegistroCentral`.

---

## 12. Antes y después de activar (FR-142, T85)

Tres momentos: las implementaciones provisionales (desde el paso 1 de T88, antes de `CarteraNucleo`), las reales con
Cartera desplegada y sin activar en la cooperativa, y Cartera activada.

| Interfaz | Provisional | Desplegada, sin activar | Activada |
|---|---|---|---|
| `IConsultasDeCartera.EstadoCrediticioAsync` | `CarteraNoHabilitada` (`NotEnabled`) | igual | estado real (§5.1) |
| `IConsultasDeCartera.EstadoDeValidacionAsync` | `Pending` con «Pendiente: el destino aún no está disponible (IC)» | igual | según el recibo (§5.1) |
| `IReservasDeCartera` | `NotEnabled` | igual | reserva real (§5.2) |
| Mensajes de Comercial | se acumulan sin intentos ni alertas | igual: el destino `Lending` está inactivo en el registro | se consumen; las ventas hasta el corte entran sólo por la migración (FR-143) |
| `IConsultaDeObligaciones` | `NotActivated`, sin obligaciones, `MigratedNotActivated = false` | `NotActivated`, sin obligaciones exigibles; `MigratedNotActivated = true` para deudores y codeudores de la carga aplicada vigente | obligaciones reales |
| `IContratoDeObligaciones` | listar y simular vacíos con `Lending.NotActivated`; escrituras `Rejected` con `Lending.NotActivated` | igual, con el intento y el rechazo auditados | §3 |
| Orden de giro | no hay desembolsos | igual | §4 |
| `IContabilidadParaCartera` · `IDimensionesDeCartera` | — | responden (migración, cuadre, completitud con la carga) | responden |
| `IConsultaDeAportes` | `ConsultaDeAportesNoDisponible` (D19) | igual | igual hasta la feature de Aportes |

Consecuencias en los demás módulos: Nómina aprueba la definitiva y la ordinaria sin descuentos de Cartera y, si hay
personas «migradas, sin activar», exige reconocer «Cartera por activar»; Personas no deja borrar a los deudores de la
carga; Comercial vende con su crédito provisional; Contabilidad cierra sus meses como hoy y sólo avisa si va a cerrar
el mes del día siguiente al corte de una carga sin activar (T57). Lo recorre `CarteraDesplegadaSinActivarTests`
(definitiva, ordinaria, borrado de persona y venta a crédito), en los dos motores.

**Requisitos de interfaces para activar en producción** (FR-142, T84): un recaudador `Receipts` y un destino
`Treasury` reales, con versiones del contrato y de la orden de giro que Cartera tenga vigentes; la regla
`RecaudoPorAplicar` con `CollectorClass = Payroll` resuelta en la matriz; toda oficina con créditos vinculada a una
sucursal contable; el mes del día siguiente al corte abierto en Contabilidad. Si falta algo, la activación se niega y
lo lista.

---

## 13. Pruebas que fijan estas interfaces

| Prueba | Tipo | Qué fija |
|---|---|---|
| `LaCarteraSoloSeTocaPorSusInterfaces` | arquitectura | Nadie depende de la Cartera nueva salvo por `Common.Integration.Lending`; métodos exactos de `IConsultasDeCartera` (2), `IReservasDeCartera` (2), `IConsultaDeObligaciones` (2), `IContratoDeObligaciones` (5 + `VersionDelContrato`) e `IDimensionesDeCartera` (2); fuera de Cartera sólo las implementaciones provisionales y los dobles los implementan; ninguna FK ni navegación de `PAY_` o `INV_` a `LND_`. Y la dirección inversa (C34): Cartera no depende de `Entities`/`Application` de Nómina, Comercial, Personas ni `Savings` salvo por los puertos que consume (§1) |
| `El_puerto_de_Cartera_declara_exactamente_sus_dos_consultas` (ampliada) · `InventarioNoConoceContabilidadNiCartera` | arquitectura | lo de Comercial (§5) |
| `CarteraNoConoceContabilidad` · `LosHechosDeCarteraNoLlevanCuentas` · `LoDeCarteraSeCorrigeConComprobanteNuevo` | arquitectura | la frontera contable (§9) |
| `LosPuertosDeCarteraValidanYAuditanSusEscrituras` | arquitectura | validador y evento de cada escritura del contrato y de las reservas (T101) |
| `LosDoblesDePruebaNoSeRegistranEnProduccion` · `UnSoloConsumidorPorDestino` | arquitectura | `RecaudadorDePrueba` y `TesoreriaDePrueba` (FR-089) |
| `NadieEscribeElLibroDeCarteraFueraDelRegistro` · `LaCarteraUsaLaFechaLocal` | arquitectura | el contrato escribe sólo por `RegistroDeCartera` y fecha con `HoyLocal` |
| `SoloLaReplicaEscribeLosValoresNacionalesDeNomina` · `LaUvtSeLeeEnUnSoloSitio` | arquitectura | §6.5 |
| `LosUnicosDeDescuentosSoloCuentanFilasVivasTests` | arquitectura | `UK_PAY_SettlementDeductions_Termination_Obligation` |
| Contrato: sin transacción → `TransactionRequired`; misma clave y huella → `AlreadyApplied`; otra huella → `KeyReused`; interactiva con «cambió»; no descontados; reversos de Nómina rechazados desde Cartera | Application | §3 |
| Nómina: «todo o nada» de la definitiva (rechazo, excedente, propuesta desactualizada); ordinaria con rechazo y excedente que no bloquean; reversas; «Cartera por activar» | Application | §6 |
| Personas: borrado con deudor, codeudor y migrado | Application | §7 |
| Reserva y liberación; `CreditWithoutLending` no emite a Lending | Application | §5 |
| `NominaRecaudaPorElContratoTests` · `CreditoConCarteraTests` · `CarteraDesplegadaSinActivarTests` · `ConcurrenciaPagoYCausacionTests` · `ReplicaSobreCooperativaSembradaTests` | e2e, dos motores, colección «Cartera e2e» | I-4, FR-142, FR-079, T93 |

---

## 14. Nombres fijados en este documento

Ningún informe ni las transversales los nombraban; siguen T1–T4 (tipo en español, propiedades y valores en inglés;
entrada sin sufijo, salida con `Dto`).

**Contrato de obligaciones**
- Entradas: `Recaudador`, `SolicitudDeListado`, `SolicitudDeSimulacion`, `SolicitudDeResolucion`.
- Salidas: `ListadoDeObligacionesDto`, `ObligacionListadaDto`, `SimulacionDePagoDto`, `LineaDeDistribucionDto`,
  `SaldosDeObligacionDto`, `PlanResumidoDto`, `CuotaDelPlanDto`, `RespuestaDeAplicacionDto`,
  `NoDescontadoRegistradoDto`, `ComprobanteDeReversoDto`, `ComprobanteDeResolucionDto`.
- Enums de puerto: `EleccionDeAbono`, `FormaDeAplicacion`, `EstadoDeObligacion`, `ConceptoDeObligacion`,
  `RolEnLaObligacion`, `ClaseDeObligacion` (`Standard`, `PayrollDeduction`), `FormaDeResolucion`.
- Números de `ClaseDeMedioDeRecaudo`: los de `PaymentMeansClass` y `OwnPayrollDeduction = 101`,
  `ExternalPaymasterDeduction = 102`, `InternalOffset = 103`.
- Propiedades de los DTO que las transversales nombran sin detalle (`SolicitudDeAplicacion`, `PagoAAplicar`,
  `NoDescontado`, `ReferenciaDeRecibo`, `SolicitudDeReverso`, `RespuestaDelContratoDto<T>`,
  `ComprobanteDeAplicacionDto`), incluidas `DeductionFileLinePublicId`, `PaymentIdempotencyKeyToReverse` y
  `SourceItemPublicId`.
- Validador: `SolicitudDeAplicacionValidator` (y uno por cada solicitud de escritura).
- Códigos de recaudador (C35): `NOM` (Nómina, semilla), `CAR` (recogida, sin fila), `TST` (dobles de prueba).
- Claves derivadas: `{clave}:reverso`, `{clave}:destino`; clave del recaudador de prueba `TST:{guid:N}` (C35);
  formato de `{monto}` en la clave de reserva (dos decimales, punto).
- Valor de enum: `ResultadoDelContrato.Answered` (C28), `MotivoDeNoDescuento.DeductionPriority` (C56); propiedad
  `MigratedNotActivated` (C26).

**Códigos de resultado y error**
- Éxito de consultas y eventos de lectura: `Lending.ObligationContract.Listed`, `Lending.ObligationContract.Simulated`.
- Errores: `Lending.Payment.ModalityNotAllowed`, `Lending.Payment.MeansClassNotAllowed`,
  `Lending.Payment.BeforeCutoff`, `Lending.Payment.ObligationNotPayable`, `Lending.Payment.PayrollReferenceRequired`
  (K2), `Lending.CreditBalance.Insufficient`, `Lending.CreditBalance.TargetNotOwned`,
  `Lending.Reservation.QuotaExceeded`, `Lending.Reservation.Blocked`.

**Auditoría**
- `Lending.NumberReservation.Attempted`, `Lending.CreditStatus.Queried`.

**Comercial**
- `EstadosCrediticios` (`Active`, `InArrears`, `Blocked`, `NoRelationship`, `NotEnabled`); `EstadosDeReserva`
  (`Reserved`, `Used`, `Released`, `Expired`, `NotEnabled`, `Rejected`, `NoResponse`); propiedades nuevas
  `EstadoCrediticioDto.TotalQuota/DaysPastDue/BlockReason/AllowsSale` (las nombra DE-12) y
  `LineaDeCreditoDto.Periodicities`; propiedades de `SolicitudDeReserva`, `SolicitudDeLiberacion` y
  `ResultadoDeReservaDto`.

**Consulta de obligaciones**
- Propiedades de `ConsultaDeObligaciones` (con `IncludeClosed`), `ObligacionDto`, `ConsultaDeCuotasDeNomina`,
  `CuotasDeNominaDto` (con `MigratedNotActivated`) y `CuotaDeNominaDto`.

**Orden de giro**
- `PayeeV1`, `PayoffDeductionV1` y los campos de `OrdenDeGiroEmitidaV1` (con `PaidByFile`, C10),
  `AnulacionDeOrdenDeGiroV1` y `ResultadoDelGiroV1` (`Outcome`: `Disbursed`, `Rejected`, `CheckNotCashed`, `Voided`;
  `Form`: `Transfer`, `Check`, `ThirdPartyPayment`); los códigos `Lending.DisbursementOrder.InvalidTransition` y
  `.AmountMismatch` (§4.3).
- Los campos de `PagoReversadoPorCarteraV1` (§3.15, C13).

**Contabilidad**
- `CombinacionDeCartera`, `ConjuntoDeCuentasDeCarteraDto`, `EstadoDelPeriodoDto`, `IndicadoresDeLaEntidadDto`,
  `IndicadorRegistradoDto`, `CuentaBancariaDto`; `CatalogoDeDimensionesDeCarteraDto`, `LineaDeDimensionDto`,
  `CombinacionEnUsoDeCarteraDto`; métodos `CatalogoAsync` y `CombinacionesEnUsoAsync` de `IDimensionesDeCartera`.

**Aportes, Cumplimiento y plataforma**
- `IConsultaDeAportes.SaldosAsync`, `SolicitudDeSaldoDeAportes`, `SaldosDeAportesDto`, `SaldoDeAportesDto`.
- `IAutorizacionDeDatos.Agregar(AutorizacionAAgregar)`, `AutorizacionAAgregar`; `AutorizacionVigente.Purpose` y
  `.HasEvidence`.
- `IPerfilRegulatorio` (`VigenteAsync`), `PerfilRegulatorioDto`, en `Application/Common/NationalValues`.

---

## 15. Conciliación

Los puntos que este documento dejó para conciliar se decidieron en `decisiones-transversales.md` §7 (origen `INT-Pn`)
y ya están aplicados en el cuerpo.

| Punto | Decisión |
|---|---|
| INT-P1 · propiedad «migrada, sin activar» | C26: `MigratedNotActivated` en `ObligacionesDePersonaDto` y en `CuotasDeNominaDto` (§6.1) |
| INT-P2 · dónde vive `IDimensionesDeCartera` | C27: `Common.Integration.Lending`; cinco puertos ofrecidos (§2, §9.2) |
| INT-P3 · resultado de una consulta | C28: `ResultadoDelContrato.Answered` en listar y simular (§3.4, §3.17) |
| INT-P4 · rol de saldo en el contrato | C29: `CombinacionDeCartera.Role` como texto; `contabilidad.md` publica los admitidos (§9.1) |
| INT-P5 · medios de crédito | C30: se declaran y se rechazan con `Lending.Payment.MeansClassNotAllowed` (§3.14) |
| INT-P6 · orden pagada por archivo y recogida | C10: la orden nace al desembolsar con `PaidByFile`, sin `DisbursementFilePublicId` (§4); C11: anular el crédito reversa la recogida, `PendingDisbursement` la conserva (§4.3) |
| INT-P7 · consulta de cupo y listas | C31: verifica sin escribir; se guarda en la reserva y al consumir la venta (§5.1) |
| INT-P8 · orden de cerrojos de la venta | C24: Inventario → `ReservarAsync` → numerador de Inventario (§5.2) |
| INT-P9 · reserva sin respuesta | C25: punto de guardado propio y `NoResponse`, en los dos motores (§5.2) |
| INT-P10 · cuota de nómina | C32: la del período en K1; desde K2, el convenio (§6.1) |
| INT-P11 · vigencia de indicadores | C33: el puerto devuelve el último con su corte; decide Cartera (§9.1) |
| INT-P12 · frontera en las dos direcciones | C34: `LaCarteraSoloSeTocaPorSusInterfaces` vigila las dos (§13) |
| INT-P13 · códigos de participante y claves derivadas | C35: `NOM`, `CAR`, `TST`, `CNT`, `INV`; `:reverso`, `:destino`; prueba `TST:{guid:N}` (§3.10) |
| INT-P14 · campos de los mensajes de giro | C13: los fija este documento, también `PagoReversadoPorCarteraV1`; `mensajes.md` remite (§3.15, §4.2) |
| INT-P15 · reverso de pagos de Aportes | C37: `Lending.Reversal.OwnedByCollector` hasta que Aportes declare su destino (§3.15) |
| INT-P16 · simular un reverso | C38: `SolicitudDeSimulacion.PaymentIdempotencyKeyToReverse` (§3.6) |
| INT-P17 · «la distribución cambió» | C39: sólo en la forma interactiva (§3.7) |
| INT-P18 · mensaje no convertible | C40: `Pending` con motivo y «para reproceso» (§5.1) |
| INT-P19 · modalidad de la ordinaria | C32: `CycleInstallment` por lo descontado y `NoDescontado` por el resto (§6.3) |
| API-9 · fecha valor antes de un cierre | C41: el contrato verifica `Lending.Payments.BackdateBeforeClose` y el límite del usuario que actúa por `IPermissionChecker` (§3.7) |
| DM2-9 · prelación entre obligaciones | C56: `MotivoDeNoDescuento.DeductionPriority` (§3.1, §6.3) |

