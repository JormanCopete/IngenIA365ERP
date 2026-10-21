namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 014, US4 (T124; contracts/api.md §4, §9 y §10 de la 014): lo que la ficha de la cuenta manda y recibe para sus movimientos, sus cargos y el
// débito automático. Los enums llegan como número (los textos están en TextosDeCaptaciones); los pedidos llevan los nombres del contrato. Sólo los campos
// que la pantalla muestra: el resto del contrato se ignora al leer. (nuevo)

// ---------------------------------------------------------------------------------------------------------- lecturas --

/// <summary>Una operación de la cuenta.</summary>
public sealed record OperacionDeCuentaDto(
    Guid PublicId,
    string Number,
    int Kind,
    DateOnly ValueDate,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    decimal Amount,
    RecaudadorDeOperacionDto? Collector, // el mismo de las operaciones de crédito
    string? MeansClass,
    string? ReceiptReference,
    Guid? ReversedBy,
    Guid? Reverses,
    string Origin,
    string? Reason,
    DateOnly? ClearingUntil,
    bool Cleared);

/// <summary>Una fila del libro.</summary>
public sealed record FilaDelLibroDeCuentaDto(long Id, int Kind, int Concept, decimal Amount, DateOnly ValueDate, DateOnly AccountingDate, DateTime RegisteredAt,
    Guid? OperationPublicId, bool CountsAsHolderActivity);

/// <summary>Un hecho de la operación con su estado en la bandeja.</summary>
public sealed record HechoDeOperacionDto(string Type, Guid MessagePublicId, string Status);

/// <summary>El GMF de una operación.</summary>
public sealed record GmfDeOperacionDto(decimal Base, decimal ExemptUsedBefore, decimal ExemptApplied, decimal TaxedBase, decimal Rate, decimal Amount, string Policy,
    string Explanation);

/// <summary>Un cargo de una operación.</summary>
public sealed record CargoDeOperacionDto(string ChargeCode, decimal Amount, bool Taxed, string Status);

/// <summary>El comprobante de una operación con su libro, sus hechos, su GMF y sus cargos.</summary>
public sealed record DetalleDeOperacionDto(OperacionDeCuentaDto Operation, IReadOnlyList<FilaDelLibroDeCuentaDto> Transactions,
    IReadOnlyList<HechoDeOperacionDto> Messages, GmfDeOperacionDto? Gmf, IReadOnlyList<CargoDeOperacionDto> Charges);

/// <summary>El disponible desglosado.</summary>
public sealed record DisponibleDeCuentaDto(decimal Balance, decimal Holds, decimal Garnishments, decimal PendingClearance, decimal MinimumBalance, decimal Available);

/// <summary>El GMF de una simulación.</summary>
public sealed record GmfSimuladoDto(decimal Base, decimal ExemptUsed, decimal ExemptRemaining, decimal Amount, string Explanation);

/// <summary>Las firmas que pide una simulación.</summary>
public sealed record FirmasSimuladasDto(IReadOnlyList<Guid> Required, IReadOnlyList<Guid> Missing);

/// <summary>La atribución que pediría la operación.</summary>
public sealed record AtribucionSimuladaDto(bool RequiresApproval, string? PermissionCode, decimal? MaxAmount);

/// <summary>La simulación de un retiro o un traslado.</summary>
public sealed record SimulacionDeMovimientoDto(bool Allowed, DisponibleDeCuentaDto Available, DisponibleDeCuentaDto AvailableAfter, GmfSimuladoDto? Gmf,
    IReadOnlyList<CargoDeOperacionDto> Charges, FirmasSimuladasDto Signatures, AtribucionSimuladaDto Attribution, IReadOnlyList<string> Warnings);

/// <summary>El comprobante de un retiro o un traslado de la pantalla.</summary>
public sealed record ComprobanteDeMovimientoDto(Guid OperationPublicId, string Number, decimal Amount, GmfDeOperacionDto? Gmf, IReadOnlyList<string> Warnings);

/// <summary>La orden de giro de un retiro.</summary>
public sealed record OrdenDeGiroDeMovimientoDto(Guid PublicId, string Number, string Status);

/// <summary>Lo que responde un retiro o un traslado: <c>Applied</c> o <c>PendingApproval</c> (espera un aprobador presente).</summary>
public sealed record ResultadoDeMovimientoDto(string Status, ComprobanteDeMovimientoDto? Operation, OrdenDeGiroDeMovimientoDto? Order, Guid? ApprovalPublicId)
{
    public const string PendienteDeAprobacion = "PendingApproval";
}

/// <summary>El comprobante de un reverso.</summary>
public sealed record ReversoDeMovimientoDto(Guid OperationPublicId, string Number, string ReversedNumber, decimal ReversedAmount, bool LeftTechnicalOverdraft);

/// <summary>La factura de Comercial de un cargo gravado.</summary>
public sealed record FacturaDeCargoDto(string? Number, string? Status);

/// <summary>Un cargo causado.</summary>
public sealed record CargoDeCuentaDto(Guid PublicId, int Kind, string Code, decimal Amount, bool Vat, int Status, DateTime CausedAt, DateOnly? PeriodFrom,
    DateOnly? PeriodTo, string? OperationNumber, string? WaiverReason, FacturaDeCargoDto? Invoice);

/// <summary>Un débito hecho por una autorización.</summary>
public sealed record DebitoHechoDto(Guid OperationPublicId, string Number, DateOnly ValueDate, decimal Amount, bool Reversed);

/// <summary>Una autorización de débito automático.</summary>
public sealed record AutorizacionDeDebitoDto(Guid PublicId, Guid AccountPublicId, string AccountNumber, PersonaDeCuentaDto AuthorizedBy, int Scope,
    Guid? LoanPublicId, string? LoanNumber, bool AllowsPartial, DateOnly ValidFrom, DateOnly? ValidTo, string Status, string? RevokeReason,
    IReadOnlyList<DebitoHechoDto> LastDebits);

/// <summary>El uso del tope exento del GMF de una persona en un mes.</summary>
public sealed record UsoDeGmfDto(int Policy, decimal CapUvt, decimal CapAmount, decimal UsedExempt, decimal Remaining, decimal Taxed, decimal GmfCharged);

// ----------------------------------------------------------------------------------------------------------- pedidos --

/// <summary>La simulación: <c>Withdrawal</c> o <c>Transfer</c>.</summary>
public sealed record SimularMovimientoRequest(string Kind, decimal Amount, Guid? TargetAccountPublicId, IReadOnlyList<Guid> Signatures);

/// <summary>El beneficiario de un retiro por giro (sin datos: el titular con su cuenta registrada).</summary>
public sealed record BeneficiarioDeRetiroRequest(Guid PersonPublicId, Guid? BankPublicId, byte? AccountType, string? AccountNumber);

/// <summary>El retiro por orden de giro.</summary>
public sealed record RetiroPorGiroRequest(decimal Amount, DateOnly? ValueDate, BeneficiarioDeRetiroRequest? Payee, IReadOnlyList<Guid> Signatures, string? Reason,
    Guid? ApprovalPublicId = null);

/// <summary>El traslado entre cuentas; la modalidad en el destino por nombre (<c>Free</c> o <c>Installments</c>).</summary>
public sealed record TrasladoRequest(Guid TargetAccountPublicId, decimal Amount, DateOnly? ValueDate, IReadOnlyList<Guid> Signatures, string? Reason,
    string TargetModality = "Installments", Guid? ApprovalPublicId = null);

/// <summary>Un cargo por evento de pantalla.</summary>
public sealed record CargoDePantallaRequest(string ChargeCode, string? EventReference, string Reason);

/// <summary>La exoneración individual de cargos.</summary>
public sealed record ExoneracionDeCargosRequest(IReadOnlyList<string> ChargeCodes, DateOnly ValidFrom, DateOnly? ValidTo, string Reason);

/// <summary>Una autorización de débito automático: alcance por nombre (<c>SingleLoan</c> o <c>AllLoansOfHolder</c>).</summary>
public sealed record AutorizarDebitoRequest(Guid AccountPublicId, string Scope, Guid? LoanPublicId, bool AllowsPartial, DateOnly? ValidFrom, DateOnly? ValidTo,
    Guid AuthorizationAttachmentPublicId);

/// <summary>La revocación de una autorización.</summary>
public sealed record RevocarDebitoRequest(DateOnly? ValidTo, string Reason);
