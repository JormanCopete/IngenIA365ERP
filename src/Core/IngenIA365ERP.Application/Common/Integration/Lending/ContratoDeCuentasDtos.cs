namespace IngenIA365ERP.Application.Common.Integration.Lending;

// Los DTO del contrato de cuentas (feature 014, T039; contracts/contrato-de-cuentas.md §3.5–§3.11). Viajan en proceso, sin HTTP. Salida con
// sufijo Dto, entrada sin sufijo; sólo PublicId y números visibles, nunca un Id interno. Un DTO cambia sólo con una VersionDelContrato nueva.
// Se reutilizan de la 013 sin cambio: RespuestaDelContratoDto<T>, ResultadoDelContrato, Recaudador, ClaseDeMedioDeRecaudo, ReferenciaDeRecibo,
// MotivoDeNoDescuento, FormaDeAplicacion y EstadoDeCartera.

// ------------------------------------------------------------------------------------------------ §3.5 consultar --

/// <summary>Las cuentas de una persona a una fecha; con <see cref="IncludeAsSignatory"/>, también las que firma. (nuevo)</summary>
public sealed record SolicitudDeConsultaDeCuentas(
    Recaudador Collector,
    Guid PersonPublicId,
    DateOnly Date,
    bool IncludeAsSignatory = false);

/// <summary>Las cuentas de la persona con el estado de cada naturaleza (activada o no). (nuevo)</summary>
public sealed record CuentasDePersonaDto(
    EstadoDeCartera DepositsStatus,
    EstadoDeCartera ContributionsStatus,
    Guid PersonPublicId,
    DateOnly Date,
    IReadOnlyList<CuentaConsultadaDto> Accounts);

/// <summary>Una cuenta consultada: estado, saldo y disponible desglosado, firmas y bloqueos (FR-086). (nuevo)</summary>
public sealed record CuentaConsultadaDto(
    Guid AccountPublicId,
    string Number,
    NaturalezaDeCuenta Nature,
    ClaseDeCaptacion? DepositClass,
    string LineCode,
    string LineName,
    RolEnLaCuenta Role,
    EstadoDeCuenta Status,
    string HandlingMode,
    IReadOnlyList<FirmanteDto> Signatories,
    DisponibleDto Available,
    bool AcceptsDeposits,
    bool AcceptsWithdrawals,
    bool GmfExempt,
    IReadOnlyList<string> BlockReasons);

/// <summary>El disponible siempre desglosado (FR-057): el mismo que valida el retiro (SC-007). (nuevo)</summary>
public sealed record DisponibleDto(
    decimal Balance,
    decimal Holds,
    decimal Garnishments,
    decimal PendingClearance,
    decimal MinimumBalance,
    decimal Available);

/// <summary>Quién debe firmar un retiro (FR-051). (nuevo)</summary>
public sealed record FirmanteDto(Guid PersonPublicId, string FullName, RolEnLaCuenta Role, decimal? SignatureLimit);

// ------------------------------------------------------------------------------------------------ §3.6 exigibles --

/// <summary>Las cuotas exigibles de unas personas a una fecha; Nómina pide sus empleados en bloque con el período de la corrida. (nuevo)</summary>
public sealed record SolicitudDeExigibles(
    Recaudador Collector,
    IReadOnlyList<Guid> PersonPublicIds,
    DateOnly Date,
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEnd = null);

/// <summary>Las cuotas exigibles de aportes, ahorro permanente y contractual, y las personas de una carga aplicada sin activar. (nuevo)</summary>
public sealed record ExigiblesDeCuentasDto(
    EstadoDeCartera DepositsStatus,
    EstadoDeCartera ContributionsStatus,
    IReadOnlyList<ExigibleDeCuentaDto> Installments,
    IReadOnlyList<Guid> MigratedNotActivated);

/// <summary>Una cuota exigible (FR-087): lo pendiente, si admite más que la cuota y adónde va el exceso, y el concepto de nómina. (nuevo)</summary>
public sealed record ExigibleDeCuentaDto(
    Guid PersonPublicId,
    Guid AccountPublicId,
    string AccountNumber,
    NaturalezaDeCuenta Nature,
    ClaseDeCaptacion? DepositClass,
    string LineCode,
    Guid InstallmentPublicId,
    string Period,
    DateOnly DueDate,
    decimal Amount,
    decimal Pending,
    bool OverpaymentAllowed,
    string OverpaymentGoesTo,
    string PayrollConcept,
    int Order);

// ------------------------------------------------------------------------------------------------- §3.7 simular --

/// <summary>Una operación por simular: valida firmas, disponible, bloqueos, embargos, montos y atribución y calcula el GMF. No escribe. (nuevo)</summary>
public sealed record SolicitudDeSimulacionDeCuenta(
    Recaudador Collector,
    string Operation,
    Guid AccountPublicId,
    decimal Amount,
    DateOnly ValueDate,
    ClaseDeMedioDeRecaudo MeansClass,
    IReadOnlyList<Guid> SignerPersonPublicIds,
    Guid? TargetAccountPublicId = null,
    ModalidadDeConsignacion? Modality = null);

/// <summary>
/// Lo que pasaría: si se admite, el disponible antes y después, GMF, cargos, firmas faltantes, atribución y avisos. Feature 015, R1 (E-02 d): con la operación
/// <c>Close</c> (la cancelación con entrega), <see cref="AmountToDeliver"/> es lo que el recaudador entrega y pide en <c>RetirarAsync</c> con
/// <c>CloseAccount = true</c>. (nuevo)
/// </summary>
public sealed record SimulacionDeCuentaDto(
    string SimulationId,
    bool Allowed,
    DisponibleDto AvailableBefore,
    DisponibleDto AvailableAfter,
    GmfCalculadoDto? Gmf,
    IReadOnlyList<CargoCalculadoDto> Charges,
    IReadOnlyList<Guid> MissingSignatures,
    AtribucionRequeridaDto? Attribution,
    bool RequiresSourceOfFunds,
    IReadOnlyList<DistribucionEnCuotaDto> InstallmentsCovered,
    IReadOnlyList<string> Notices,
    decimal? AmountToDeliver = null);

/// <summary>El GMF calculado por su motor (FR-053), con la base, lo exento usado y aplicado, la tarifa y la explicación. (nuevo)</summary>
public sealed record GmfCalculadoDto(decimal Base, decimal ExemptUsedBefore, decimal ExemptApplied, decimal TaxedBase, decimal Rate, decimal Amount,
    string Policy, string Explanation);

/// <summary>Un cargo de la cuenta (FR-128): cobrado o pendiente. (nuevo)</summary>
public sealed record CargoCalculadoDto(string ChargeCode, decimal Amount, bool Taxed, string Status);

/// <summary>La atribución que supera el usuario que actúa (api.md §1.6). (nuevo)</summary>
public sealed record AtribucionRequeridaDto(string PermissionCode, decimal Amount, decimal MaxAmount);

/// <summary>Lo que cubre una consignación en cada cuota. (nuevo)</summary>
public sealed record DistribucionEnCuotaDto(Guid InstallmentPublicId, string Period, decimal Amount);

// ------------------------------------------------------------------------------------------------ §3.8 consignar --

/// <summary>Un lote de consignaciones y, de Nómina o de una respuesta de pagaduría, los no descontados. (nuevo)</summary>
public sealed record SolicitudDeConsignacion(
    Recaudador Collector,
    FormaDeAplicacion Form,
    IReadOnlyList<ConsignacionAAplicar> Deposits,
    IReadOnlyList<NoDescontadoDeCuenta> NotDeducted);

/// <summary>Una consignación con su clave, su modalidad, su medio y su recibo (FR-050, FR-087). (nuevo)</summary>
public sealed record ConsignacionAAplicar(
    string IdempotencyKey,
    Guid AccountPublicId,
    ModalidadDeConsignacion Modality,
    decimal Amount,
    DateOnly ValueDate,
    ClaseDeMedioDeRecaudo MeansClass,
    Guid DepositorPublicId,
    ReferenciaDeRecibo Receipt,
    IReadOnlyList<Guid>? InstallmentPublicIds = null,
    Guid? SourceOfFundsAttachmentPublicId = null,
    Guid? DeductionFileLinePublicId = null,
    string? SimulationId = null,
    Guid? ApprovalPublicId = null,
    PartidaBancariaCitada? BankItem = null);

/// <summary>Una cuota que Nómina o la pagaduría no descontó, con su motivo (FR-089); la cuota sigue exigible. (nuevo)</summary>
public sealed record NoDescontadoDeCuenta(
    string IdempotencyKey,
    Guid AccountPublicId,
    Guid InstallmentPublicId,
    decimal ExpectedAmount,
    decimal NotDeductedAmount,
    MotivoDeNoDescuento Reason,
    string? Detail,
    Guid? SourceItemPublicId);

/// <summary>Una respuesta por ítem, en el orden del lote. (nuevo)</summary>
public sealed record RespuestaDeConsignacionDto(
    IReadOnlyList<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> Deposits,
    IReadOnlyList<RespuestaDelContratoDto<NoDescontadoDeCuentaRegistradoDto>> NotDeducted);

/// <summary>El no descontado registrado. (nuevo)</summary>
public sealed record NoDescontadoDeCuentaRegistradoDto(Guid InstallmentPublicId, decimal NotDeductedAmount, string Reason);

/// <summary>El comprobante de una consignación, un retiro o un traslado: la operación, lo escrito por concepto y el disponible después. (nuevo)</summary>
public sealed record ComprobanteDeCuentaDto(
    Guid OperationPublicId,
    string Number,
    Guid AccountPublicId,
    string AccountNumber,
    string IdempotencyKey,
    string Kind,
    DateOnly ValueDate,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    decimal Amount,
    IReadOnlyList<LineaDeCuentaDto> Transactions,
    GmfCalculadoDto? Gmf,
    IReadOnlyList<CargoCalculadoDto> Charges,
    IReadOnlyList<DistribucionEnCuotaDto> InstallmentsCovered,
    DisponibleDto AvailableAfter,
    AutorizacionDeEfectivoDto? CashAuthorization,
    Guid? TargetAccountPublicId,
    bool Reactivated,
    string InputsHash,
    string RulesVersion,
    IReadOnlyList<string> Warnings);

/// <summary>Una transacción escrita: concepto, clase y valor. (nuevo)</summary>
public sealed record LineaDeCuentaDto(ConceptoDeCuenta Concept, string TransactionKind, decimal Amount);

/// <summary>La autorización de un retiro en efectivo o cheque que entrega el recaudador (FR-088). (nuevo)</summary>
public sealed record AutorizacionDeEfectivoDto(string AuthorizationCode, decimal AmountToDeliver, string HolderName,
    IReadOnlyList<Guid> VerifiedSignerPersonPublicIds);

// ------------------------------------------------------------------------------------------------- §3.9 retirar --

/// <summary>Un retiro que autoriza la Cartera y entrega el recaudador (FR-051, FR-088); <see cref="CloseAccount"/> cancela la cuenta (FR-039). (nuevo)</summary>
public sealed record SolicitudDeRetiro(
    Recaudador Collector,
    string IdempotencyKey,
    Guid AccountPublicId,
    decimal Amount,
    DateOnly ValueDate,
    ClaseDeMedioDeRecaudo MeansClass,
    IReadOnlyList<Guid> SignerPersonPublicIds,
    Guid ReceiverPersonPublicId,
    ReferenciaDeRecibo Receipt,
    bool CloseAccount = false,
    string? SimulationId = null,
    Guid? ApprovalPublicId = null);

// ------------------------------------------------------------------------------------------------ §3.10 trasladar --

/// <summary>Un traslado entre cuentas, una sola operación con las dos bajo el cerrojo (FR-052). (nuevo)</summary>
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

// ------------------------------------------------------------------------------------------------ §3.11 reversar --

/// <summary>El reverso de una operación propia por su clave (FR-092). (nuevo)</summary>
public sealed record SolicitudDeReversoDeCuenta(
    Recaudador Collector,
    string IdempotencyKey,
    string Reason,
    ReferenciaDeRecibo? Receipt = null);

/// <summary>El comprobante del reverso: los espejos, el sobregiro técnico y los documentos anulados. (nuevo)</summary>
public sealed record ComprobanteDeReversoDeCuentaDto(
    Guid OperationPublicId,
    string Number,
    Guid ReversedOperationPublicId,
    string ReversedNumber,
    IReadOnlyList<Guid> AccountPublicIds,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    decimal ReversedAmount,
    IReadOnlyList<LineaDeCuentaDto> Transactions,
    bool LeftTechnicalOverdraft,
    IReadOnlyList<Guid> VoidedDocumentPublicIds);

/// <summary>
/// Los códigos del contrato de cuentas que el recaudador distingue (feature 015, R1, fase 26; api.md §1.6): sobre la atribución, Captaciones deja la solicitud de
/// aprobación y responde este código con <c>approvalPublicId</c> en los datos; el recaudador la pide presente y repite con la misma clave. (nuevo)
/// </summary>
public static class CodigosDelContratoDeCuentas
{
    public const string PendienteDeAprobacion = "Lending.Savings.PendingApproval";
}
