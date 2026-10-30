namespace IngenIA365ERP.Application.Common.Integration.Treasury;

// Feature 015, T0040 (R-05; FR-003; contracts/interfaces.md §2): los puertos que Tesorería ofrece y Recaudo usa. Síncronos: corren en la unidad
// de trabajo de quien llama (misma base, misma transacción), validan y auditan sus escrituras (LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras)
// y lo que cruza se guarda como …PublicId y número, sin FK entre RCD_ y TES_. Las implementaciones llegan con sus historias (fases 7, 11 y 14).

/// <summary>Una cuenta bancaria propia, como la ve Recaudo (interfaces.md §2). (nuevo)</summary>
public sealed record CuentaBancariaPropiaDto(
    Guid PublicId,
    string Code,
    string BankName,
    string? BankTransferCode,
    string AccountType,
    string AccountNumberMasked,
    Guid? BranchPublicId,
    bool IsCollection,
    bool IsActive);

/// <summary>Una página de resultados de un puerto. (nuevo)</summary>
public sealed record PaginaDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>
/// Qué abonos del extracto pide Recaudo para identificar. <see cref="SoloPendientes"/> deja fuera los ya identificados del todo; <see cref="OficinaPublicIds"/> es el
/// alcance de Recaudo de quien pide (nulo = todas): Tesorería no aplica su propio alcance a una persona de Recaudo. (nuevo)
/// </summary>
public sealed record FiltroDeAbonos(Guid? OwnBankAccountPublicId, DateOnly? Desde, DateOnly? Hasta, string? Referencia, int Page = 1, int PageSize = 50,
    bool SoloPendientes = true, IReadOnlyCollection<Guid>? OficinaPublicIds = null);

/// <summary>
/// Una línea de extracto (un abono) con lo que falta por identificar (US6, T0193 suma la cuenta, la oficina, el banco, la huella y el estado de Recaudo; DT §2.7).
/// (nuevo)
/// </summary>
public sealed record LineaDeExtractoDto(
    Guid PublicId,
    Guid OwnBankAccountPublicId,
    DateOnly MovementDate,
    DateOnly? ValueDate,
    decimal Amount,
    decimal Identified,
    string? Reference,
    string? DepositorTaxId,
    string? DepositorName,
    string? Description,
    bool PossibleDuplicate,
    string OwnBankAccountCode = "",
    Guid? BranchPublicId = null,
    Guid? BankPublicId = null,
    string? BankTransferCode = null,
    string RecaudoStatus = "Pending",
    string? DuplicateDecision = null,
    string StatementNumber = "",
    Guid? PossibleDuplicateOfPublicId = null);

/// <summary>Lo que Recaudo pide al despachar efectivo al banco (remesa), en la transacción del despacho. (nuevo)</summary>
public sealed record SolicitudDeRemesa(
    Guid IdempotencyKey,
    Guid OwnBankAccountPublicId,
    Guid OfficePublicId,
    decimal Amount,
    DateOnly DispatchDate,
    string SourceDocumentNumber,
    Guid SourceDocumentPublicId,
    Guid? VaultPublicId = null,
    string? BagSeal = null,
    Guid? MinutesAttachmentPublicId = null);

public sealed record RemesaRegistradaDto(Guid RemittancePublicId, string Number);

public sealed record EstadoDeRemesaDto(Guid RemittancePublicId, string Number, string State, DateOnly? ConfirmedOn);

/// <summary>
/// La orden de pago que Recaudo pide a Tesorería (interfaces.md §2): la devolución de un saldo a favor o de una partida. <see cref="OrderKind"/> es
/// <c>PayerCreditBalanceRefund</c>, <c>LendingCreditBalanceRefund</c> o <c>UnidentifiedItemRefund</c>. (nuevo)
/// </summary>
public sealed record SolicitudDeOrdenDePago(
    Guid IdempotencyKey,
    string OrderKind,
    Guid BeneficiaryPersonPublicId,
    Guid? PersonBankAccountPublicId,
    string Form,
    decimal NetAmount,
    DateOnly PayDate,
    string Concept,
    Guid SourceDocumentPublicId);

public sealed record OrdenDePagoEmitidaDto(Guid PaymentOrderPublicId, string Number, string State);

public sealed record EstadoDeOrdenDto(Guid PaymentOrderPublicId, string Number, string State, DateOnly? OccurredOn, string? Reason);

/// <summary>Ofrece Tesorería → usa Recaudo (R0): las cuentas bancarias propias de recaudo, con alcance. (nuevo)</summary>
public interface ICuentasBancariasPropias
{
    Task<IReadOnlyList<CuentaBancariaPropiaDto>> DeRecaudoAsync(Guid? branchPublicId, CancellationToken ct);

    Task<CuentaBancariaPropiaDto?> PorPublicIdAsync(Guid publicId, CancellationToken ct);
}

/// <summary>Ofrece Tesorería → usa Recaudo (R0): el extracto es uno, de Tesorería; Recaudo lo identifica y marca lo identificado. (nuevo)</summary>
public interface IMovimientosBancariosParaRecaudo
{
    Task<PaginaDto<LineaDeExtractoDto>> AbonosPorIdentificarAsync(FiltroDeAbonos filtro, CancellationToken ct);

    /// <summary>Escribe <c>RecaudoStatus</c> de la línea: cuánto quedó identificado.</summary>
    Task MarcarIdentificacionAsync(Guid statementLinePublicId, decimal identificado, CancellationToken ct);

    /// <summary>Un abono por su <c>PublicId</c> (nulo si no existe o no es un abono) (US6, T0193; nuevo).</summary>
    Task<LineaDeExtractoDto?> LineaAsync(Guid statementLinePublicId, CancellationToken ct);

    /// <summary>
    /// Escribe la decisión de una persona sobre un posible duplicado (FR-197): <paramref name="esDuplicado"/> lo deja fuera de lo que Recaudo identifica; si no, sigue
    /// pendiente como un movimiento distinto (US6, T0195; nuevo).
    /// </summary>
    Task DecidirDuplicadoAsync(Guid statementLinePublicId, bool esDuplicado, CancellationToken ct);

    /// <summary>
    /// La última carga de extracto de cada cuenta propia de recaudo activa, para la alerta de la cuenta sin extracto (FR-196; US6, T0197; nuevo): Recaudo no lee
    /// <c>TES_</c>.
    /// </summary>
    Task<IReadOnlyList<UltimaCargaDeExtractoDto>> UltimasCargasAsync(CancellationToken ct);
}

/// <summary>La última carga de extracto de una cuenta de recaudo (nula si nunca se cargó). (nuevo)</summary>
public sealed record UltimaCargaDeExtractoDto(Guid OwnBankAccountPublicId, string Code, Guid? BranchPublicId, DateOnly? LastStatementTo, DateTime? LastLoadedAt);

/// <summary>Ofrece Tesorería → usan Recaudo (T0) y Comercial (T1): la remesa de efectivo al banco. (nuevo)</summary>
public interface IRemesasDeTesoreria
{
    /// <summary>En la transacción del despacho.</summary>
    Task<RemesaRegistradaDto> RegistrarAsync(SolicitudDeRemesa solicitud, CancellationToken ct);

    Task<EstadoDeRemesaDto> EstadoAsync(Guid remittancePublicId, CancellationToken ct);

    /// <summary>
    /// Feature 015, US19 (T0227): la bóveda recibió la remesa que Tesorería sacó del banco (<c>BankToVault</c>); la confirma en la transacción del movimiento de bóveda, sin
    /// guardar. Lanza si la remesa no es de esa bóveda o no es por ese valor. (nuevo)
    /// </summary>
    Task<EstadoDeRemesaDto> RecibidaEnBovedaAsync(Guid remittancePublicId, Guid vaultPublicId, Guid vaultMovementPublicId, decimal amount, CancellationToken ct);
}

/// <summary>Ofrece Tesorería → usa Recaudo (T0): la orden de pago de una devolución. (nuevo)</summary>
public interface IOrdenesDePagoDeTesoreria
{
    Task<OrdenDePagoEmitidaDto> EmitirAsync(SolicitudDeOrdenDePago solicitud, CancellationToken ct);

    Task<EstadoDeOrdenDto> EstadoAsync(Guid paymentOrderPublicId, CancellationToken ct);
}
