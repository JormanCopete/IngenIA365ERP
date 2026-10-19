# Contrato de cuentas y consultas de captaciones

**Feature**: 014 | **Fecha**: 2026-10-14 | **Entregas**: K7 (consultar, exigibles, simular, consignar, trasladar,
reversar; consulta de aportes v2) · K8 (retirar) | **Base**: `research.md` §0.5, R08, R09, R16–R18, R20, R21; spec
FR-008, FR-009, FR-030, FR-050–FR-059, FR-086–FR-095, FR-125, FR-126, E-01, E-02, E-04, E-05 | **Molde**:
`specs/013-cartera-financiera/contracts/interfaces.md` §2, §3, §6, §8 (en adelante **«la 013»**) | **Hermanos**:
`api.md`, `mensajes.md`, `contabilidad.md`, `plantillas-y-archivos.md`

Este contrato fija lo que la Cartera ofrece **en proceso, sin HTTP**, para mover dinero en las cuentas de captación
y de aportes, y las dos consultas que cambian. Es la ampliación de la lista cerrada (FR-008, E-01): un **puerto
propio** `IContratoDeCuentas` en vez de agrandar `IContratoDeObligaciones` (R09), porque depositar no es pagar una
deuda: no hay prelación, ni anticipos, ni saldo a favor.

Cuatro ideas lo ordenan, las mismas de la 013:

- **La lista sigue cerrada.** Nómina, Recaudo y Tesorería tocan las cuentas **sólo** por este puerto
  (`LasCuentasSeTocanSoloPorElContrato`); ninguno lee ni escribe tablas `LND_`.
- **Lo que escribe corre en la unidad de trabajo de quien llama** (I-4 de la 013). Consignar, retirar, trasladar y
  reversar exigen transacción en curso, nunca abren ni confirman la suya y se llaman directo, nunca por `ISender`.
- **Fuera del pipeline, con lo que el pipeline daría.** Cada escritura ejecuta su validador FluentValidation y
  escribe su intento y su hecho de auditoría (desviación T101 de la 013, que se extiende a este puerto).
- **Antes de activar, todo responde** con «no habilitado» y sin efectos (FR-093).

---

## 1. La lista cerrada después de E-01

**La Cartera ofrece** (se suman a los cinco de la 013 §1):

| Interfaz | Puerto o mensaje | A quién | Provisional → real | Detalle |
|---|---|---|---|---|
| Contrato de cuentas | `IContratoDeCuentas` (7 métodos + `VersionDelContrato = 1`) | Recaudo, Tesorería, Nómina, recaudador de prueba | `ContratoDeCuentasSinActivar` → `ContratoDeCuentasDeCartera` | §3 |
| Saldo de aportes y ahorro permanente | `IConsultaDeAportes` **v2**, ahora real y **ofrecido** (antes consumido) | la propia Cartera (cupo, arrastre, deterioro) y quien la consulte | `ConsultaDeAportesNoDisponible` → `ConsultaDeAportesDeCartera` | §4 |
| Productos de captación y aportes de una persona | `IConsultasDeCartera.ProductosDeCaptacionAsync` (método nuevo) | Personas, Asociados, Nómina | `ConsultasDeCarteraNoHabilitada` → `ConsultasDeCartera` | §5 |
| Mensajes nuevos | `RetiroDeAsociadoAprobado`, `ConsignacionReversadaPorCartera`, `OrdenDeGiroEmitida` v2, `DescuentosPorPagaduria` v2, `CargoDeCaptacionCausado`/`Reversado` | Asociados, recaudador, Tesorería, Recaudo, Comercial | — | `mensajes.md` |

**Consume además**: `ResultadoDelGiro` v1 para las órdenes de captaciones, `ResultadoDeDescuentos` v2 de Recaudo,
`FacturaDeCargoRegistrada` de Comercial, el perfil tributario de la persona (012) y los valores nacionales nuevos
(`COMPONENTE_INFLACIONARIO`, `LIMITE_INEMBARGABLE`, `TOPE_ENTREGA_SIN_SUCESION`).

`LaCarteraSoloSeTocaPorSusInterfaces` se enmienda (R29): **seis** puertos ofrecidos —`IConsultasDeCartera`,
`IReservasDeCartera`, `IConsultaDeObligaciones`, `IContratoDeObligaciones`, `IDimensionesDeCartera`,
`IContratoDeCuentas`— más `IConsultaDeAportes`, que pasa de «consumido» a la lista de ofrecidos. Fija los siete
métodos de §3.1 y `VersionDelContrato`.

## 2. Reglas comunes (las de la 013 §2, con lo que cambia)

| Regla | Detalle |
|---|---|
| Espacio de nombres | `IngenIA365ERP.Application.Common.Integration.Lending` para `IContratoDeCuentas`, sus DTO y sus enums (`ContratoDeCuentasDtos.cs`, `EnumsDelContratoDeCuentas.cs`). `IConsultaDeAportes` **se muda** aquí desde `…Integration.Savings` (R20); la carpeta `Savings` desaparece con el retiro de lo heredado. |
| Implementaciones | `ContratoDeCuentasSinActivar` (espacio del puerto, registrada hasta que nace el código de K7) y `Application/Lending/Integration/ContratoDeCuentasDeCartera` (la real). Responden por cooperativa según `LND_Setup.ContributionsActivationDate` y `.DepositsActivationDate`. |
| Idioma | Tipo de puerto, DTO y enum en español; propiedades y valores en inglés, idénticos a los del dominio (`ConceptoDeCuenta.Principal` ↔ `MemberAccountConcept.Principal`). Salida con `Dto`, entrada sin sufijo. |
| Identificadores | Sólo `PublicId` y números visibles. Ningún Id interno sale. |
| Sin alcance de Cartera | El puerto no aplica oficina ni línea: lo autoriza el permiso del módulo que llama (FR-004). **Sí** comprueba sobre el usuario que actúa las atribuciones con monto de `api.md` §1.6 (`Lending.Accounts.Withdraw`, `.Deposit`) por `IPermissionChecker`, como la 013 hace con `Payments.BackdateBeforeClose`. |
| Errores | Un rechazo de negocio nunca es excepción: `Rejected` con código y mensaje en español. Un fallo técnico se propaga y el recaudador reintenta su transacción entera. |
| Versionado | Un DTO de este archivo cambia sólo con `VersionDelContrato` nueva; el participante adopta la versión en el registro (`ContractName = "ProveedorDeCuentas"`). |

---

## 3. `IContratoDeCuentas`

### 3.1 Firma

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// El contrato «Proveedor de Cuentas» (feature 014, R09; FR-086–FR-093): todo dinero que entra o sale de una cuenta de
/// captación o de aportes desde otro módulo entra por aquí. Síncrono en la unidad de trabajo de quien recauda.
/// </summary>
public interface IContratoDeCuentas
{
    /// <summary>Versión que adopta cada participante en el registro («ProveedorDeCuentas»).</summary>
    const int VersionDelContrato = 1;

    // FR-086: cuentas de una persona con estado, saldo y disponible desglosado. No escribe.
    Task<RespuestaDelContratoDto<CuentasDePersonaDto>> ConsultarAsync(
        SolicitudDeConsultaDeCuentas solicitud, CancellationToken ct);

    // FR-087: cuotas exigibles de aportes, ahorro permanente y contractual de unas personas a una fecha. No escribe.
    Task<RespuestaDelContratoDto<ExigiblesDeCuentasDto>> ListarExigiblesAsync(
        SolicitudDeExigibles solicitud, CancellationToken ct);

    // US4: valida firmas, disponible, bloqueos, embargos, montos, atribución y calcula el GMF. No escribe.
    Task<RespuestaDelContratoDto<SimulacionDeCuentaDto>> SimularAsync(
        SolicitudDeSimulacionDeCuenta solicitud, CancellationToken ct);

    // FR-050, FR-087, FR-089: un lote de consignaciones (y los no descontados de Nómina); una respuesta por ítem.
    Task<RespuestaDeConsignacionDto> ConsignarAsync(
        SolicitudDeConsignacion solicitud, CancellationToken ct);

    // FR-051, FR-088: retiro autorizado por la Cartera y entregado por el recaudador.
    Task<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> RetirarAsync(
        SolicitudDeRetiro solicitud, CancellationToken ct);

    // FR-052: traslado entre cuentas, una sola operación.
    Task<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> TrasladarAsync(
        SolicitudDeTraslado solicitud, CancellationToken ct);

    // FR-092: reversa una operación propia por su clave.
    Task<RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>> ReversarAsync(
        SolicitudDeReversoDeCuenta solicitud, CancellationToken ct);
}
```

Se reutilizan de la 013 sin cambio: `RespuestaDelContratoDto<T>`, `ResultadoDelContrato` (`Applied`,
`AlreadyApplied`, `Rejected`, `Answered`), `Recaudador`, `ClaseDeMedioDeRecaudo`, `ReferenciaDeRecibo`,
`MotivoDeNoDescuento`, `FormaDeAplicacion`, `EstadoDeCartera`.

### 3.2 Enums del puerto (`EnumsDelContratoDeCuentas.cs`) (nuevo en contracts)

| Enum | Valores | Equivale a |
|---|---|---|
| `NaturalezaDeCuenta` | `Deposit = 2`, `Contribution = 3` | `LineNature` (sin `Placement`) |
| `ClaseDeCaptacion` | `Demand = 1`, `Contractual = 2`, `Permanent = 3`, `TermDeposit = 4` | `DepositClass` |
| `EstadoDeCuenta` | `Opening = 1`, `Active = 2`, `Inactive = 3`, `Matured = 4`, `Closing = 5`, `Closed = 6` | `MemberAccountStatus` |
| `ConceptoDeCuenta` | `Principal = 1` … `Bonus = 10` | `MemberAccountConcept` |
| `RolEnLaCuenta` | `Holder = 1`, `CoHolder = 2`, `Signatory = 3`, `LegalRepresentative = 4`, `Beneficiary = 5` | `MemberAccountPartyRole` |
| `ModalidadDeConsignacion` | `Free = 1` (al saldo), `Installments = 2` (cuotas exigibles en orden de vencimiento), `SpecificInstallments = 3`, `TermDepositFunding = 4` (fondeo exacto del CDAT en `Opening`) | — |
| `ClaseDeRecaudador` (existente, **valor agregado al final**) | `Treasury = 6` | Tesorería como recaudador de transferencias recibidas |

`LosEnumsDelContratoNumeranComoElDominio` se extiende a estos.

### 3.3 Quién puede llamar

| Clase | Quién | Registro | Métodos | Clases de medio | Clave |
|---|---|---|---|---|---|
| `Receipts` | Recaudo: caja, consignaciones, cheques, respuestas de pagadurías | Su feature: rol `ObligationsCollector`, adopción `ProveedorDeCuentas` v1 | todos | las de la 012 salvo crédito, `ExternalPaymasterDeduction` | `REC:…` (su feature) |
| `Treasury` | Tesorería: transferencias recibidas a una cuenta | Su feature: ídem | consultar, exigibles, simular, consignar, reversar lo suyo | `Transfer`, `BankDeposit` | `TES:{documentoPublicId:N}:{accountPublicId:N}` |
| `Payroll` | Nómina propia (`NOM`) | Semilla de la plataforma: la fila `NOM` **suma la adopción** `ProveedorDeCuentas` v1 (E-02) | exigibles, simular, consignar, reversar | `OwnPayrollDeduction` | `NOM:{runPublicId:N}:{accountPublicId:N}` |
| `Test` | `RecaudadorDePrueba` (`TST`) | Fila `Test`, nunca en producción | todos | todas salvo crédito | `TST:{guid:N}` |
| `OwnLoanPayoff` (`CAR`) | **No llama al puerto**: el débito automático, el cruce al retiro y el pago de intereses del CDAT a una cuenta son operaciones internas de la Cartera con `InternalOffset`, escritas por `RegistroDeCuentas` con el mismo único de clave | no se registra | — | `InternalOffset` | `CAR:debito-automatico:{loanPublicId:N}:{fecha}` (R16), `CAR:retiro-asociado:{withdrawalPublicId:N}:{accountPublicId:N}`, `CAR:cdat-intereses:{accountPublicId:N}:{periodo}`, `CAR:asamblea:{distributionPublicId:N}:{accountPublicId:N}` |

Un participante **cuenta** si `IRegistroDeIntegracion` le da fila activa a hoy con rol `ObligationsCollector`, su
clase coincide, **adoptó `ProveedorDeCuentas`** en una versión que este despliegue soporta
(`ContratosDeIntegracion.VersionesDelContratoDeCuentas = [1]`) y no es un doble en producción. Si no: `Rejected` con
`Lending.Contract.CollectorNotRegistered` (o `Integration.Participant.VersionNotSupported`). La adopción de
`ProveedorDeObligaciones` **no** habilita cuentas ni al revés.

`Contributions` (clase 3) queda **sin uso nuevo** (C3 de la spec): el cruce de aportes es interno.

### 3.4 Ejecución en la unidad de trabajo del recaudador

Pasos de `ConsignarAsync` (los demás que escriben siguen el mismo orden):

1. **Intento**: `Lending.AccountContract.Attempted` por un contexto aparte (sobrevive a la reversión).
2. **Validador** de la solicitud; un error de forma es `Rejected` con `Validation.Invalid` y `data.errors[]`.
3. **Transacción**: sin `TransaccionExplicita.HayEnCurso(db)`, `Lending.Contract.TransactionRequired`.
4. **Activación de la naturaleza** de cada cuenta: sin ella, `Lending.Savings.NotActivated` o
   `Lending.Contributions.NotActivated` (§3.13).
5. **Registro** del participante (§3.3).
6. **Validación previa contable**, fuera del cerrojo (`IContabilidadParaCartera.EvaluarAsync`): en el contrato
   **nunca bloquea**; sella `PrevalidationOutcome` y alerta `Cartera.HechoNoContabilizable` (enmienda A8, D23).
7. **Cerrojo** (`CerrojoDeCartera`, R08) en una sola pasada por todo el lote, orden fijo **setup → persona → crédito
   → cuenta**, por `Id`; las operaciones esperan (`FOR UPDATE` / `UPDLOCK, ROWLOCK, HOLDLOCK`).
8. **Idempotencia** bajo el cerrojo: único `(CollectorCode, IdempotencyKey)` en `LND_MemberAccountOperations` con
   `HuellaDeOperacion.Calcular("Lending.AccountContract.{Metodo}", cuerpo)`; misma huella → `AlreadyApplied` con el
   comprobante original; otra → `Lending.Contract.KeyReused`.
9. **Puesta al día** de la causación de la cuenta hasta la fecha valor (marca de agua `LastAccruedDate`); una fecha
   valor anterior a lo causado genera el **ajuste por fecha valor** enlazado (FR-062).
10. **Reglas** de la operación (§3.7–§3.10) con los motores puros (`GravamenMovimientosFinancieros`,
    `LimiteInembargable`, `CargosDeCuenta`).
11. **Escribe** por `RegistroDeCuentas` (escritor único, `NadieEscribeElLibroDeCuentasFueraDelRegistro`): la
    `MemberAccountOperation`, sus `MemberAccountTransaction` con tres fechas, la posición del día, el acumulado de GMF.
12. **Numera** al final con `NumeradorDeCartera`.
13. **Emite** los hechos (`mensajes.md`) y escribe `Lending.AccountContract.{Deposited|Withdrawn|Transferred|Reversed|
    Rejected}` en `COR_AuditOutbox` con el antes y el después por concepto.
14. **Responde** una `RespuestaDelContratoDto` por ítem.

### 3.5 Consultar — `ConsultarAsync`

```csharp
public sealed record SolicitudDeConsultaDeCuentas(
    Recaudador Collector,
    Guid PersonPublicId,
    DateOnly Date,
    bool IncludeAsSignatory = false);          // también las que firma (representante, firmante)

public sealed record CuentasDePersonaDto(
    EstadoDeCartera DepositsStatus,            // captaciones activadas o no
    EstadoDeCartera ContributionsStatus,       // aportes activados o no
    Guid PersonPublicId,
    DateOnly Date,
    IReadOnlyList<CuentaConsultadaDto> Accounts);

public sealed record CuentaConsultadaDto(
    Guid AccountPublicId,
    string Number,
    NaturalezaDeCuenta Nature,
    ClaseDeCaptacion? DepositClass,
    string LineCode,
    string LineName,
    RolEnLaCuenta Role,
    EstadoDeCuenta Status,
    string HandlingMode,                       // Indistinct | Joint
    IReadOnlyList<FirmanteDto> Signatories,    // quién debe firmar un retiro (FR-051)
    DisponibleDto Available,
    bool AcceptsDeposits,
    bool AcceptsWithdrawals,                   // falso en aportes, CDAT vigente, permanente sin retiros parciales
    bool GmfExempt,
    IReadOnlyList<string> BlockReasons);       // en español, sin detalle de listas restrictivas

public sealed record DisponibleDto(
    decimal Balance, decimal Holds, decimal Garnishments, decimal PendingClearance, decimal MinimumBalance,
    decimal Available);                        // FR-057: siempre desglosado

public sealed record FirmanteDto(Guid PersonPublicId, string FullName, RolEnLaCuenta Role, decimal? SignatureLimit);
```

Mismo saldo disponible que valida el retiro (SC-007): ambos salen de `DisponibleDeCuenta` (puro).

### 3.6 Exigibles — `ListarExigiblesAsync`

```csharp
public sealed record SolicitudDeExigibles(
    Recaudador Collector,
    IReadOnlyList<Guid> PersonPublicIds,       // Nómina pide sus empleados en bloque
    DateOnly Date,
    DateOnly? PeriodStart = null,              // Nómina: el período de la corrida
    DateOnly? PeriodEnd = null);

public sealed record ExigiblesDeCuentasDto(
    EstadoDeCartera DepositsStatus,
    EstadoDeCartera ContributionsStatus,
    IReadOnlyList<ExigibleDeCuentaDto> Installments,
    IReadOnlyList<Guid> MigratedNotActivated);  // personas de una carga aplicada sin activar (aviso a Nómina)

public sealed record ExigibleDeCuentaDto(
    Guid PersonPublicId,
    Guid AccountPublicId,
    string AccountNumber,
    NaturalezaDeCuenta Nature,
    ClaseDeCaptacion? DepositClass,            // Permanent o Contractual; nula en aportes
    string LineCode,
    Guid InstallmentPublicId,                  // AccountInstallment
    string Period,                             // AAAA-MM o AAAA-MM-Qn según la periodicidad
    DateOnly DueDate,
    decimal Amount,
    decimal Pending,
    bool OverpaymentAllowed,                   // FR-087: más que la cuota
    string OverpaymentGoesTo,                  // Extraordinary | CreditBalance (línea de aportes)
    string PayrollConcept,                     // DESC_APORTES | DESC_AHORROS (E-02)
    int Order);                                // prelación dentro de la persona: vencimiento, luego línea
```

- Lee en bloque (posición diaria + `LND_AccountInstallments`), nunca una consulta por cuenta.
- La cuota de un fondo de empleados ya viene repartida entre aportes y permanente con la fracción mínima a aportes
  (US3-4, FR-049).
- Con `PeriodStart`/`PeriodEnd`, sólo las que vencen en el período más las atrasadas que la línea manda incluir.

### 3.7 Simular — `SimularAsync`

```csharp
public sealed record SolicitudDeSimulacionDeCuenta(
    Recaudador Collector,
    string Operation,                          // Deposit | Withdrawal | Transfer
    Guid AccountPublicId,
    decimal Amount,
    DateOnly ValueDate,
    ClaseDeMedioDeRecaudo MeansClass,
    IReadOnlyList<Guid> SignerPersonPublicIds,
    Guid? TargetAccountPublicId = null,
    ModalidadDeConsignacion? Modality = null);

public sealed record SimulacionDeCuentaDto(
    string SimulationId,
    bool Allowed,
    DisponibleDto AvailableBefore,
    DisponibleDto AvailableAfter,
    GmfCalculadoDto? Gmf,
    IReadOnlyList<CargoCalculadoDto> Charges,
    IReadOnlyList<Guid> MissingSignatures,
    AtribucionRequeridaDto? Attribution,       // el retiro supera la atribución del usuario que actúa
    bool RequiresSourceOfFunds,                // efectivo sobre el umbral (FR-059)
    IReadOnlyList<DistribucionEnCuotaDto> InstallmentsCovered,
    IReadOnlyList<string> Notices);            // los rechazos que tendría, en español

public sealed record GmfCalculadoDto(decimal Base, decimal ExemptUsedBefore, decimal ExemptApplied, decimal TaxedBase,
    decimal Rate, decimal Amount, string Policy, string Explanation);
public sealed record CargoCalculadoDto(string ChargeCode, decimal Amount, bool Taxed, string Status); // Collected | Pending
public sealed record AtribucionRequeridaDto(string PermissionCode, decimal Amount, decimal MaxAmount);
public sealed record DistribucionEnCuotaDto(Guid InstallmentPublicId, string Period, decimal Amount);
```

### 3.8 Consignar — `ConsignarAsync`

```csharp
public sealed record SolicitudDeConsignacion(
    Recaudador Collector,
    FormaDeAplicacion Form,
    IReadOnlyList<ConsignacionAAplicar> Deposits,
    IReadOnlyList<NoDescontadoDeCuenta> NotDeducted);   // sólo Payroll y respuestas de pagaduría

public sealed record ConsignacionAAplicar(
    string IdempotencyKey,
    Guid AccountPublicId,
    ModalidadDeConsignacion Modality,
    decimal Amount,
    DateOnly ValueDate,
    ClaseDeMedioDeRecaudo MeansClass,
    Guid DepositorPublicId,                    // quien consigna (titular, tercero, pagaduría, empleador)
    ReferenciaDeRecibo Receipt,
    IReadOnlyList<Guid>? InstallmentPublicIds = null,   // SpecificInstallments
    Guid? SourceOfFundsAttachmentPublicId = null,       // FR-059
    Guid? DeductionFileLinePublicId = null,             // giro de una pagaduría (FR-125)
    string? SimulationId = null,
    Guid? ApprovalPublicId = null);                      // atribución de efectivo aprobada (api.md §1.6)

public sealed record NoDescontadoDeCuenta(
    string IdempotencyKey,
    Guid AccountPublicId,
    Guid InstallmentPublicId,
    decimal ExpectedAmount,
    decimal NotDeductedAmount,
    MotivoDeNoDescuento Reason,
    string? Detail,
    Guid? SourceItemPublicId);                 // la fila de Nómina o la línea de la respuesta

public sealed record RespuestaDeConsignacionDto(
    IReadOnlyList<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> Deposits,
    IReadOnlyList<RespuestaDelContratoDto<NoDescontadoDeCuentaRegistradoDto>> NotDeducted);

public sealed record NoDescontadoDeCuentaRegistradoDto(Guid InstallmentPublicId, decimal NotDeductedAmount, string Reason);
```

Reglas:

- **Estado**: admite `Active`, `Inactive` (un depósito del titular **la reactiva**, FR-084) y `Opening` sólo con
  `TermDepositFunding` por el valor exacto del CDAT (`Lending.TermDeposit.FundingAmountMismatch`) o el depósito
  inicial de la línea. `Closed`, `Closing` y titular fallecido: `Lending.Savings.AccountNotOperable`.
- **Sólo asociados** (FR-030): una cuenta de línea que lo exige rechaza si el titular dejó de ser asociado vigente,
  salvo las operaciones del proceso de retiro.
- **Cuotas** (`Installments`): cubre en orden de vencimiento; el excedente va como aporte extraordinario o como saldo
  según la línea (US3-1); sin cuotas pendientes, `Free`.
- **Límite de participación** (FR-043): un aporte que lo supera responde `Lending.Contributions.ParticipationLimitExceeded`.
- **Efectivo** (FR-059): sobre `Efectivo.UmbralDeclaracion` sin declaración, `Lending.Savings.SourceOfFundsRequired`;
  sobre el máximo de la línea, `Lending.Savings.CashLimitExceeded`; sobre la atribución `Lending.Accounts.Deposit`
  del usuario que actúa, `Rejected` con `Lending.Savings.PendingApproval` y `data.required` (el recaudador obtiene
  el aprobador presente y repite con `ApprovalPublicId`).
- **Canje** (FR-050): un medio con días de disponibilidad (`Captaciones.DiasDeCanjePorClaseDeMedio`, parámetro con
  vigencia **(nuevo en contracts)**, por defecto `Check` 3 días hábiles, el resto 0) deja el valor en
  `PendingClearance`: suma al saldo, no al disponible. Pasados los días lo libera el ciclo diario; antes, el
  recaudador puede reversarlo (cheque devuelto).
- **Cargo pendiente** (FR-128): si la cuenta tiene cargos pendientes, la consignación los cobra en la misma operación
  (acompañante `CargoPendienteCobrado`).
- **Nómina** (FR-089): lo descontado entra como `Installments` y lo no descontado como `NoDescontadoDeCuenta`; la
  cuota queda `PartiallyPaid` o `NotDeducted` con su motivo y **sigue exigible** (US3-2).
- **Comprobante** de cada ítem:

```csharp
public sealed record ComprobanteDeCuentaDto(
    Guid OperationPublicId,
    string Number,
    Guid AccountPublicId,
    string AccountNumber,
    string IdempotencyKey,
    string Kind,                               // Deposit | Withdrawal | Transfer
    DateOnly ValueDate,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    decimal Amount,
    IReadOnlyList<LineaDeCuentaDto> Transactions,   // concepto y valor de cada transacción escrita
    GmfCalculadoDto? Gmf,
    IReadOnlyList<CargoCalculadoDto> Charges,
    IReadOnlyList<DistribucionEnCuotaDto> InstallmentsCovered,
    DisponibleDto AvailableAfter,
    AutorizacionDeEfectivoDto? CashAuthorization,   // sólo en RetirarAsync con efectivo o cheque
    Guid? TargetAccountPublicId,                    // traslados
    bool Reactivated,                               // la cuenta salió de «inactiva»
    string InputsHash,
    string RulesVersion,
    IReadOnlyList<string> Warnings);

public sealed record LineaDeCuentaDto(ConceptoDeCuenta Concept, string TransactionKind, decimal Amount);
public sealed record AutorizacionDeEfectivoDto(string AuthorizationCode, decimal AmountToDeliver, string HolderName,
    IReadOnlyList<Guid> VerifiedSignerPersonPublicIds);
```

### 3.9 Retirar — `RetirarAsync` (K8)

```csharp
public sealed record SolicitudDeRetiro(
    Recaudador Collector,
    string IdempotencyKey,
    Guid AccountPublicId,
    decimal Amount,
    DateOnly ValueDate,
    ClaseDeMedioDeRecaudo MeansClass,          // Cash | Check (el recaudador entrega); otras: rechazo
    IReadOnlyList<Guid> SignerPersonPublicIds, // identidad verificada en ventanilla por el recaudador
    Guid ReceiverPersonPublicId,               // quien recibe el dinero (titular, firmante o representante)
    ReferenciaDeRecibo Receipt,
    bool CloseAccount = false,                 // cancelación con entrega en efectivo (FR-039)
    string? SimulationId = null,
    Guid? ApprovalPublicId = null);
```

**Un retiro en efectivo lo autoriza la Cartera y lo entrega Recaudo** (FR-088). La Cartera valida y debita en la
transacción del recaudador y devuelve `CashAuthorization`; Recaudo entrega el dinero y confirma **su** transacción.
Si Recaudo no confirma, el débito tampoco queda. Validaciones, en este orden (cada una un `Rejected` con su código de
`api.md` §1.8):

1. estado y naturaleza: los aportes no se retiran por el contrato (`Lending.Contributions.NotWithdrawable`); el CDAT
   vigente tampoco (`Lending.TermDeposit.NotMatured`); un permanente sin retiros parciales tampoco;
2. **manejo y firmas** (`Lending.Savings.SignaturesMissing`, US4-1): conjunto exige todos los titulares; un firmante
   con límite no firma sobre él; con titular menor sólo firma el representante vigente (FR-033); un retiro del
   representante en espera queda anulado si el titular cumplió la mayoría de edad;
3. **disponible** (`Lending.Savings.InsufficientAvailable` con el desglose, US4-2), incluido el GMF y los cargos;
   nunca deja la cuenta bajo el mínimo ni negativa (FR-056);
4. **montos máximos** por operación y por día de la línea (`Lending.Savings.WithdrawalLimitExceeded`) y **efectivo
   máximo** (`.CashLimitExceeded`);
5. **atribución** del usuario que actúa (`Lending.Accounts.Withdraw` con monto, y la matriz `DepositWithdrawal`):
   superada, `Rejected` con `Lending.Savings.PendingApproval` y **no mueve saldo** (US4-5); el recaudador consigue
   el aprobador presente con segundo factor por la mecánica de aprobaciones de la plataforma y repite con la misma
   clave y `ApprovalPublicId`;
6. **GMF** por el motor (FR-053), como transacción enlazada; el acumulado exento del mes se actualiza en la misma
   operación (`LND_GmfMonthlyUsage`).

`CloseAccount = true` liquida a la fecha (interés, retención), cobra GMF y cargos, retira **todo** el disponible y
deja la cuenta `Closed`; se niega con bloqueos, embargos o canje pendiente (`Lending.Savings.CloseBlocked`).

### 3.10 Trasladar — `TrasladarAsync`

```csharp
public sealed record SolicitudDeTraslado(
    Recaudador Collector,
    string IdempotencyKey,
    Guid SourceAccountPublicId,
    Guid TargetAccountPublicId,
    decimal Amount,
    DateOnly ValueDate,
    IReadOnlyList<Guid> SignerPersonPublicIds,
    ReferenciaDeRecibo Receipt,
    ModalidadDeConsignacion TargetModality = ModalidadDeConsignacion.Free,
    string? SimulationId = null,
    Guid? ApprovalPublicId = null);
```

Una sola operación con las dos cuentas bajo el cerrojo en orden de `Id`. Valida el retiro en el origen (pasos 1–5
de §3.9) y la consignación en el destino (§3.8). El GMF sigue `GMF_HECHOS_GENERADORES`: por defecto exento entre
cuentas de ahorro del mismo titular y **gravado** entre ahorro y CDAT o hacia otro titular (D32). Un traslado a una
cuenta de aportes paga cuotas si `TargetModality = Installments`.

### 3.11 Reversar — `ReversarAsync`

```csharp
public sealed record SolicitudDeReversoDeCuenta(
    Recaudador Collector,
    string IdempotencyKey,                     // la clave con que se aplicó la operación
    string Reason,
    ReferenciaDeRecibo? Receipt = null);

public sealed record ComprobanteDeReversoDeCuentaDto(
    Guid OperationPublicId,
    string Number,
    Guid ReversedOperationPublicId,
    string ReversedNumber,
    IReadOnlyList<Guid> AccountPublicIds,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    decimal ReversedAmount,
    IReadOnlyList<LineaDeCuentaDto> Transactions,   // espejos (operación, GMF, cargos, ajuste de causación)
    bool LeftTechnicalOverdraft,                    // FR-056: bloqueo y alerta
    IReadOnlyList<Guid> VoidedDocumentPublicIds);   // extractos y certificados anulados (documentos.md §4)
```

- Sólo reversa operaciones de **su** recaudador; clave derivada `{clave}:reverso`; un reverso no se reversa
  (`Lending.Savings.ReversalOfReversal`).
- Arrastra sus dependientes en la misma operación (FR-002): GMF y su acumulado del mes, cargos cobrados, retención y
  el ajuste de causación por fecha valor.
- **Consignación ya usada** (cheque devuelto): si dejaría la cuenta negativa, el recaudador **sí** puede reversar
  (el dinero no llegó): queda un **sobregiro técnico**, la cuenta se bloquea (`HoldReason.Internal`) y se alerta
  `Cartera.SobregiroTecnico` **(nuevo en contracts)** (FR-056). Desde la pantalla de la Cartera, en cambio, el reverso
  se rechaza (`Lending.Savings.ReversalWouldOverdraw`, US4-6).
- Fecha contable en el período abierto; la operación original no se reescribe.

**Consignación reversada por la Cartera** (FR-092): cuando un usuario con `Lending.Accounts.Reverse` reversa desde la
Cartera una consignación de un recaudador, se emite `ConsignacionReversadaPorCartera` al destino del recaudador
(`Receipts` para Recaudo y el doble; `Treasury` para Tesorería) con la clave y el recibo originales, para que anule su
recibo. Lo de Nómina **no** se reversa desde la Cartera (`Lending.Savings.ReversalOwnedByCollector`).

### 3.12 Gramática de claves (FR-009)

| Origen | Gramática | Único en |
|---|---|---|
| Nómina | `NOM:{runPublicId:N}:{accountPublicId:N}` (versión **aprobada** de la corrida) | `LND_MemberAccountOperations (CollectorCode, IdempotencyKey)` |
| Tesorería | `TES:{documentoPublicId:N}:{accountPublicId:N}` | ídem |
| Recaudo | `REC:…` | su feature |
| Recaudo, giro de una pagaduría | `REC:{giroPublicId:N}:{deductionFileLinePublicId:N}` | ídem |
| Recaudador de prueba | `TST:{guid:N}` | ídem |
| Internas de la Cartera | `CAR:debito-automatico:{loanPublicId:N}:{fecha}`, `CAR:retiro-asociado:{withdrawalPublicId:N}:{accountPublicId:N}`, `CAR:cdat-intereses:{accountPublicId:N}:{periodo}`, `CAR:asamblea:{distributionPublicId:N}:{accountPublicId:N}`, `CAR:cargo:{chargePublicId:N}` | ídem |
| Derivadas | `{clave}:reverso` | ídem |

Un no descontado usa la clave de su consignación (va a `LND_AccountInstallments`, con único por cuota y clave).
Las internas no pasan por el puerto pero comparten el único: un débito automático repetido no debita dos veces.

### 3.13 Antes de activar (FR-093, R21)

| Método | `ContratoDeCuentasSinActivar` | Real, naturaleza sin activar | Activada |
|---|---|---|---|
| `ConsultarAsync` · `ListarExigiblesAsync` | `Answered`, estados `NotActivated`, listas vacías, código `Lending.Savings.NotActivated` | igual, por naturaleza (una activada responde sus cuentas y la otra vacía) | §3.5, §3.6 |
| `SimularAsync` | `Answered`, `Allowed = false`, `Lending.Savings.NotActivated` o `Lending.Contributions.NotActivated` | igual | §3.7 |
| `ConsignarAsync` · `RetirarAsync` · `TrasladarAsync` · `ReversarAsync` | `Rejected` en cada ítem con el código de la naturaleza | igual, con intento y rechazo auditados | §3.8–§3.11 |

Recaudo conserva lo rechazado con «no habilitado» y lo aplica al activar con su fecha valor; lo de fecha valor
hasta el corte ya debe estar en la carga (`Lending.Savings.BeforeCutoff`).

### 3.14 Errores del contrato

| Código | Métodos | Qué hace quien llama |
|---|---|---|
| `Validation.Invalid` | todos | corrige la solicitud |
| `Lending.Contract.TransactionRequired` | escrituras | defecto del llamador |
| `Lending.Savings.NotActivated` · `Lending.Contributions.NotActivated` | todos | conserva y aplica al activar |
| `Lending.Contract.CollectorNotRegistered` · `Integration.Participant.VersionNotSupported` | todos | registrar o adoptar `ProveedorDeCuentas` |
| `Lending.Contract.KeyReused` | escrituras | otra clave; nunca reintentar con otros datos |
| `Generic.NotFound` | todos | cuenta, cuota u operación inexistente |
| `Lending.Savings.AccountNotOperable` · `.MembersOnly` · `.InsufficientAvailable` · `.SignaturesMissing` · `.WithdrawalLimitExceeded` · `.CashLimitExceeded` · `.SourceOfFundsRequired` · `.CloseBlocked` · `.BeforeCutoff` | según la operación | `api.md` §1.8 |
| `Lending.Savings.PendingApproval` | consignar en efectivo, retirar, trasladar | obtener el aprobador presente y repetir con `ApprovalPublicId` |
| `Lending.Savings.DistributionChanged` | forma interactiva | la simulación cambió: mostrar `data.simulation` y confirmar de nuevo |
| `Lending.Savings.MeansClassNotAllowed` | consignar, retirar | clase de crédito, desconocida o no admitida para el participante |
| `Lending.Savings.ReversalOfReversal` · `.ReversalOwnedByCollector` | reversar | — |
| `Lending.Contributions.ParticipationLimitExceeded` · `.NotWithdrawable` | consignar, retirar | — |
| `Lending.TermDeposit.FundingAmountMismatch` · `.NotMatured` | consignar, retirar | — |
| Avisos (no rechazan): `Lending.Prevalidation.NotPostable`, `.NoResponse`, `Lending.Savings.Overdrawn` | escrituras | informar |

### 3.15 Auditoría

| Evento | Cuándo | Dónde |
|---|---|---|
| `Lending.AccountContract.Attempted` | al entrar a una escritura | contexto aparte |
| `Lending.AccountContract.Deposited` · `.Withdrawn` · `.Transferred` · `.Reversed` · `.Rejected` | al terminar cada ítem | `COR_AuditOutbox`, dentro de la transacción, con el antes y el después por concepto |
| `Lending.AccountContract.Queried` · `.Listed` · `.Simulated` | lecturas | `LendingAuditEmitter` (lectura sensible de saldos de un titular, FR-003) |

### 3.16 Qué lo fija

`LaCarteraSoloSeTocaPorSusInterfaces` (siete métodos y `VersionDelContrato`), `LasCuentasSeTocanSoloPorElContrato`
(Nómina, Recaudo y Tesorería), `LosPuertosDeCarteraValidanYAuditanSusEscrituras` (ampliada a este puerto),
`NadieEscribeElLibroDeCuentasFueraDelRegistro`, `LosEnumsDelContratoNumeranComoElDominio`; e2e «Captaciones e2e»
(R30): idempotencia con tres reintentos (SC-005), retiro con firma faltante, atribución con aprobador, cheque
devuelto con sobregiro técnico, Nómina con cuota parcial, «no habilitado» antes de activar.

---

## 4. `IConsultaDeAportes` v2 (FR-094, R20, C2)

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Lending;   // se muda desde …Integration.Savings

public interface IConsultaDeAportes
{
    /// <summary>2 desde la 014: suma Minimum e InstallmentsInArrears; la 1 sigue para quien no la adopte.</summary>
    const int VersionDelContrato = 2;

    Task<SaldosDeAportesDto> SaldosAsync(SolicitudDeSaldoDeAportes solicitud, CancellationToken ct);
}

public sealed record SolicitudDeSaldoDeAportes(IReadOnlyList<Guid> PersonPublicIds, DateOnly Date);

public sealed record SaldosDeAportesDto(bool Available, IReadOnlyList<SaldoDeAportesDto> Balances)
{
    public static SaldosDeAportesDto NoDisponible { get; } = new(false, []);
}

public sealed record SaldoDeAportesDto(Guid PersonPublicId, decimal Contributions, decimal PermanentSavings, DateOnly AsOf)
{
    /// <summary>v2: mínimo no reducible del asociado a la fecha.</summary>
    public decimal Minimum { get; init; }

    /// <summary>v2: cuotas exigibles vencidas y no pagadas (aportes y permanente) a la fecha.</summary>
    public int InstallmentsInArrears { get; init; }
}
```

- **Compatibilidad**: los campos nuevos entran **al final** como propiedades `init`: quien compila contra la v1 no se
  rompe y los ve en cero. Un consumidor que dependa de ellos declara la v2.
- **Implementación real** `Application/Lending/Integration/ConsultaDeAportesDeCartera`, registrada desde K7. Responde
  `Available = true` **por cooperativa desde la activación de aportes**; antes, `NoDisponible`
  (`ConsultaDeAportesNoDisponible` sigue para las cooperativas sin aportes activados).
- **De dónde sale**: de `LND_MemberAccountDailyPositions` a la fecha; si la fecha no tiene posición, de
  `ReconstruccionDeCuenta` con la información hasta ahora (R04). `Contributions` = suma de las cuatro clases;
  `PermanentSavings` = capital de las cuentas `Permanent` vivas. Mismo saldo que el extracto de aportes (SC-010).
- **Fin de D19**: con `Available = true`, la plantilla `lending.contribution-balances` **deja de leerse** para esa
  cooperativa (US3-3). `ContextoDeAportes` deja de forzar a falso y lee los parámetros de la Cartera
  `Riesgo.ExcepcionAportesArrastre` y `Riesgo.DescuentoAportesDeterioro` (habilitar es una decisión por parámetro,
  no automática).
- **Aportes como garantía** (FR-044): la misma implementación responde a `GarantiasDeAportes` (interno de la Cartera)
  qué créditos vivos tiene la persona para impedir una devolución o cesión.

---

## 5. `IConsultasDeCartera.ProductosDeCaptacionAsync` (FR-095, E-04)

El puerto de la 012 suma un **tercer** método (la prueba `InventarioNoConoceContabilidadNiCartera` que fijaba dos se
enmienda: Inventario sigue usando sólo los dos suyos).

```csharp
public interface IConsultasDeCartera
{
    Task<EstadoCrediticioDto> EstadoCrediticioAsync(ConsultaCrediticia consulta, CancellationToken ct);
    Task<EstadoDeValidacionDto> EstadoDeValidacionAsync(Guid messagePublicId, CancellationToken ct);

    /// <summary>Feature 014 (FR-095): cuentas de captación, CDAT y aportes de unas personas, con estado y saldo.</summary>
    Task<ProductosDeCaptacionDto> ProductosDeCaptacionAsync(ConsultaDeProductosDeCaptacion consulta, CancellationToken ct);
}

public sealed record ConsultaDeProductosDeCaptacion(
    IReadOnlyList<Guid> PersonPublicIds, DateOnly Date, bool IncludeClosed = false, bool IncludeAsParty = true);

public sealed record ProductosDeCaptacionDto(
    EstadoDeCartera DepositsStatus,
    EstadoDeCartera ContributionsStatus,
    IReadOnlyList<ProductosDePersonaDto> People);

public sealed record ProductosDePersonaDto(
    Guid PersonPublicId,
    bool HasLiveProducts,                      // alguna cuenta no Closed como titular o cotitular
    bool MigratedNotActivated,                 // titular en una carga aplicada sin activar
    IReadOnlyList<ProductoDeCaptacionDto> Products);

public sealed record ProductoDeCaptacionDto(
    Guid AccountPublicId, string Number, NaturalezaDeCuenta Nature, ClaseDeCaptacion? DepositClass,
    string LineCode, string LineName, RolEnLaCuenta Role, EstadoDeCuenta Status,
    decimal Balance, decimal Available, DateOnly? MaturityDate, DateOnly? ClosedOn);
```

`ConsultasDeCarteraNoHabilitada` responde los dos estados `NotActivated` y listas vacías. No aplica el alcance de la
Cartera; lee en bloque.

| Consumidor | Llamada | Regla |
|---|---|---|
| `DeletePersonCommand` (Personas) | `ProductosDeCaptacionAsync([persona], hoy)` | bloquea con `Person.HasLiveDepositProducts` si `HasLiveProducts` o `MigratedNotActivated` (E-04) |
| `GetPersonDetailQuery` | ídem | el resumen de la persona muestra los productos (reemplaza lo que hoy sale de `SavingsAccounts`) |
| Asociados (detalle y consumo de `RetiroDeAsociadoAprobado`) | ídem | muestra lo que queda por entregar al retirado |
| Nómina (pantalla de la ficha y liquidación definitiva) | ídem | informa cuentas vivas del empleado que se retira; el cobro sigue por exigibles |

---

## 6. Cómo lo usan los demás módulos

### 6.1 Nómina — `DESC_APORTES` y `DESC_AHORROS` (E-02, FR-089, FR-122, R18)

Dos conceptos nuevos en `WellKnownConceptCodes` **(los nombra R18)**, deducciones autorizadas sin valor legal, con
cuentas por concepto como `DESC_CARTERA` (sin cuentas, la primera nómina con estos descuentos no se aprueba:
`Payroll.ConceptAccountsMissing`).

| Paso | Qué hace Nómina | Por el puerto |
|---|---|---|
| Calcular | `CalculationInputLoader` convierte cada exigible en novedad `AccountDeduction` **(nuevo en contracts)** con `PAY_Novelties.AccountPublicId` y `AccountNumber`; `DESC_APORTES` para `Nature = Contribution`, `DESC_AHORROS` para `Permanent` y `Contractual`. El motor respeta el tope de descuentos; lo que no cabe queda no descontado con motivo. La prelación entre `DESC_APORTES`, `DESC_CARTERA` y `DESC_AHORROS` la fija la política de empresa `PrelacionDescuentosDeCartera` **(nuevo en contracts)**, por defecto aportes → créditos → ahorros | `ListarExigiblesAsync(Recaudador.Nomina, empleados, hoy, inicio, fin)` |
| Vista previa | Simula el lote | `SimularAsync` por ítem |
| Aprobar | `ApprovePayrollRunCommand`, en su `TransaccionExplicita` y **antes** del NM, junto con el lote de obligaciones: lo descontado como `Installments` y lo no descontado como `NoDescontadoDeCuenta` | `ConsignarAsync`, `NonInteractive`, `Payroll`/`NOM`, `OwnPayrollDeduction`, depositante = el empleado, recibo = el NM, fecha valor = fin del período |
| Respuesta | Un `Rejected` no bloquea: la línea baja a lo aplicado, el neto sube, se guarda en `PAY_PayrollAccountDeductions` **(nuevo en contracts)** y se avisa `Payroll.AccountDeductionNotApplied` | `RespuestaDeConsignacionDto` |
| Liquidación definitiva | Propone el descuento de las cuotas causadas; las cuentas mismas se entregan por el retiro del asociado de la Cartera, no por Nómina | `ListarExigiblesAsync` + `ConsignarAsync` |
| Reversar | `ReversePayrollRunCommand` reversa sus consignaciones, todo o nada | `ReversarAsync` con la clave de cada consignación |

### 6.2 Recaudo (caja, consignaciones, cheques)

- Caja: `ConsultarAsync` → `SimularAsync` (forma `Interactive`) → `ConsignarAsync` o `RetirarAsync` con el
  `SimulationId`; para el retiro en efectivo entrega lo de `CashAuthorization` y confirma su transacción.
- Cheque devuelto: `ReversarAsync` con la clave de la consignación.
- Cuotas de aportes por caja: `ListarExigiblesAsync` y `ConsignarAsync` con `Installments` o `SpecificInstallments`.
- Hasta que exista Recaudo, en prueba, `RecaudadorDePrueba` (`api.md` §18.4); en producción nada sale en vivo sin
  Recaudo (Q3, FR-093).

### 6.3 Tesorería

- **Transferencias recibidas** a una cuenta: `ConsignarAsync` con `Treasury`, `Transfer` o `BankDeposit`, clave
  `TES:…`.
- **Pagos de créditos** (decisión del dueño del 2026-10-18, research §13 D19): si Tesorería adopta también
  `ProveedorDeObligaciones` en el registro de participantes (rol `ObligationsCollector`, clase `Treasury`), paga créditos por
  `IContratoDeObligaciones` con la misma clase de recaudador, los mismos medios (`Transfer`, `BankDeposit`) y la misma clave
  (`TES:{documentoPublicId:N}:{obligationPublicId:N}`): lo validan `GramaticaDeClaves`, `ClasesDeMedioDeRecaudo` y el registro
  (`RecaudadorAsync` exige la adopción vigente). Un pago suyo reversado desde la Cartera le avisa con `PagoReversadoPorCartera`
  al destino `Treasury` (el catálogo admite `Receipts` y `Treasury`), igual que `ConsignacionReversadaPorCartera`.
- **Giros**: no usa el puerto. Recibe `OrdenDeGiroEmitida` v2 de los retiros por transferencia, devoluciones,
  entregas a beneficiarios, intereses de CDAT a terceros y consignaciones a juzgados, y responde `ResultadoDelGiro`
  v1; una orden rechazada **reintegra el saldo** desde su origen (FR-091, `mensajes.md` §4).

### 6.4 Pagadurías — cuotas de aportes y ahorros por convenio (FR-125, E-05, R17)

1. **Generar** (Cartera, por grupo de archivo del convenio): `GeneratePayrollDeductionFileCommand` con
   `fileGroupCode` arma las líneas de **créditos y cuotas de cuentas** del grupo, agrupadas por el concepto equivalente
   de la pagaduría (`PayrollAgreementConceptEquivalence`), y emite `DescuentosPorPagaduria` **v2** a Recaudo.
2. **Respuesta** (Recaudo): lee el archivo de vuelta con el formato del grupo (`FlatFileReader`) y emite
   `ResultadoDeDescuentos` **v2**, con lo descontado **por empleado y por código de la pagaduría**.
3. **Reparto** (Cartera, `ConsumePayrollDeductionResultCommand`): reparte lo descontado de cada código entre las
   líneas del archivo con la regla del convenio (`Priority` o `Proportional`, prelación); lo no cubierto queda
   «no descontado por la pagaduría» con la causa equivalente; un código o un empleado sin equivalencia va a **no
   aplicados** con su motivo, sin aplicar esa fila (Edge Cases).
4. **Giro** (Recaudo, en **una** unidad de trabajo): por cada línea «descontado pendiente de giro», los créditos por
   `IContratoDeObligaciones.AplicarAsync` y las cuotas de cuentas por `IContratoDeCuentas.ConsignarAsync`
   (`ExternalPaymasterDeduction`, `DeductionFileLinePublicId` de la línea, clave `REC:{giro:N}:{línea:N}`).

### 6.5 Débito automático, cruce al retiro e intereses del CDAT (operaciones internas)

No usan el puerto: son comandos de la Cartera que escriben por `RegistroDeCuentas` y por `RegistroDeCartera` en una
sola transacción con los cerrojos de crédito y cuenta en orden (R08):

- **Débito automático** (FR-090, R16): en la fecha de la cuota el ciclo diario debita la cuenta (`InternalOffset`) y
  aplica el pago al crédito con `Recaudador("CAR", OwnLoanPayoff)`; sin disponible, no debita salvo débito parcial
  autorizado. El reverso del pago arrastra el reverso del débito (Edge Cases).
- **Cruce al retiro** (FR-077): `SettleMembershipWithdrawalCommand` aplica aportes y ahorros a los créditos con la
  aplicación de pagos de la Cartera, **todo o nada**.
- **Intereses del CDAT a una cuenta de ahorro** (FR-069): `CAR:cdat-intereses:…`.

---

## 7. Nombres fijados en este documento (nuevo en contracts)

- **Puerto y DTO**: `ContratoDeCuentasDtos.cs`, `EnumsDelContratoDeCuentas.cs`, `ContratoDeCuentasSinActivar` (nombre
  de research), records `SolicitudDeConsultaDeCuentas`, `CuentasDePersonaDto`, `CuentaConsultadaDto`, `DisponibleDto`,
  `FirmanteDto`, `SolicitudDeExigibles`, `ExigiblesDeCuentasDto`, `ExigibleDeCuentaDto`, `SolicitudDeSimulacionDeCuenta`,
  `SimulacionDeCuentaDto`, `GmfCalculadoDto`, `CargoCalculadoDto`, `AtribucionRequeridaDto`, `DistribucionEnCuotaDto`,
  `SolicitudDeConsignacion`, `ConsignacionAAplicar`, `NoDescontadoDeCuenta`, `RespuestaDeConsignacionDto`,
  `NoDescontadoDeCuentaRegistradoDto`, `ComprobanteDeCuentaDto`, `LineaDeCuentaDto`, `AutorizacionDeEfectivoDto`,
  `SolicitudDeRetiro`, `SolicitudDeTraslado`, `SolicitudDeReversoDeCuenta`, `ComprobanteDeReversoDeCuentaDto`,
  `ConsultaDeProductosDeCaptacion`, `ProductosDeCaptacionDto`, `ProductosDePersonaDto`, `ProductoDeCaptacionDto`.
- **Enums del puerto**: `NaturalezaDeCuenta`, `ClaseDeCaptacion`, `EstadoDeCuenta`, `ConceptoDeCuenta`,
  `RolEnLaCuenta`, `ModalidadDeConsignacion`; valor `ClaseDeRecaudador.Treasury = 6`.
- **Plataforma**: `ContratosDeIntegracion.ProveedorDeCuentas` (constante con el `ContractName` de research),
  `ContratosDeIntegracion.VersionesDelContratoDeCuentas`; la fila `NOM` adopta `ProveedorDeCuentas` v1.
- **Parámetros y políticas**: `Captaciones.DiasDeCanjePorClaseDeMedio`, `Efectivo.UmbralDeclaracion`, política de
  Nómina `PrelacionDescuentosDeCartera`.
- **Nómina**: novedad `AccountDeduction`, tabla `PAY_PayrollAccountDeductions`, columnas
  `PAY_Novelties.AccountPublicId`/`AccountNumber`, aviso `Payroll.AccountDeductionNotApplied`, rechazo
  `Payroll.ConceptAccountsMissing` aplicado a los dos conceptos.
- **Errores y eventos**: `Lending.Savings.{DistributionChanged, MeansClassNotAllowed, BeforeCutoff}`,
  `Lending.TermDeposit.NotMatured`, `Person.HasLiveDepositProducts`; eventos `Lending.AccountContract.*`; alerta
  `Cartera.SobregiroTecnico`; claves internas `CAR:retiro-asociado:…`, `CAR:cdat-intereses:…`, `CAR:asamblea:…`,
  `CAR:cargo:…`, `TES:…`, `REC:{giro:N}:{línea:N}`.

## 8. Conciliación

| Punto | Decisión |
|---|---|
| «Hasta que el recaudador lo confirme» (FR-050) sin método de confirmación | El puerto de research tiene siete métodos fijos; el canje se **libera solo** al cumplirse los días hábiles de la clase de medio, y el recaudador lo anula con `ReversarAsync`. Una confirmación explícita sería una v2 del contrato |
| Tesorería como recaudador | Necesita `ClaseDeRecaudador.Treasury`, valor agregado al final (research §0.3 no lo prohíbe; sólo fija `ClaseDeMedioDeRecaudo` e `IntegrationParticipantClass`, que no cambian: la clase del participante `Treasury = 4` ya existe) |
| `IConsultaDeAportes` «por clase» (FR-094) | La v2 de research suma sólo `Minimum` e `InstallmentsInArrears`; el detalle por clase lo da `GET /api/lending/contributions/summary` (`api.md` §7). Si un consumidor en proceso lo necesita, es una v3 |
| `PagoAplicado` originado por el proceso (débito automático) | `mensajes.md` §6: cambio de catálogo (`AdmiteProceso`), no de versión |
