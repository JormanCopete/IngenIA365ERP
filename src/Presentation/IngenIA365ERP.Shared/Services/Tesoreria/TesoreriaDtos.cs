namespace IngenIA365ERP.Shared.Services.Tesoreria;

// Feature 015, US10 y US11 (T0147, T0162; contracts/api.md §9, §10): lo que las cuentas bancarias propias, el fondo de liquidez, la bandeja de órdenes, los lotes, los
// archivos de pago y los resultados devuelven y reciben. Espejo de los DTO de Application/Treasury; los enums viajan como número. (nuevo)

public sealed record CuentaBancariaPropiaDto(Guid PublicId, string Code, string Name, Guid BankPublicId, string BankName, string? BankTransferCode,
    string AccountNumber, int AccountType, Guid BranchPublicId, string BranchName, int Purposes, bool IsGmfExempt, Guid? BankThirdPartyPersonPublicId,
    int Status, DateOnly ValidFrom, DateOnly? ValidTo, Guid? LinkedChartAccountPublicId, string? LinkedChartAccountCode, decimal? LastStatementBalance,
    DateOnly? LastStatementDate);

public sealed record SaldosDeCuentaDto(Guid PublicId, string Code, DateOnly Date, decimal BookBalance, decimal? StatementBalance, DateOnly? StatementDate,
    decimal ApprovedNotSent, decimal ChecksInTransit, decimal RemittancesInTransit, decimal ProjectedAvailable);

public sealed record CuentaContableBancariaDto(Guid ChartAccountPublicId, string Code, string Name, Guid? BankPublicId, string? BankAccountNumber);

public sealed record ElementoDeAlcanceDto(Guid PublicId, string Code, string Name);

public sealed record AlcanceDeTesoreriaDto(Guid UserPublicId, string Name, bool HasAll, IReadOnlyList<ElementoDeAlcanceDto> BankAccounts);

public sealed record UsoDelFondoDto(Guid PublicId, string Number, Guid BankAccountPublicId, string BankAccountCode, int Cause, decimal Amount, string Reason,
    Guid SupportAttachmentPublicId, Guid? SupervisorNoticeAttachmentPublicId, int Status, Guid? ApprovalRequestPublicId, DateTime RequestedAt, DateTime? DecidedAt,
    string? DecisionReason, Guid? UsedByDocumentPublicId);

public sealed record OrdenDePagoDto(Guid PublicId, string Number, int Origin, string OriginOrderNumber, string OrderKind, int Form, Guid BeneficiaryPersonPublicId,
    string BeneficiaryName, string BeneficiaryTaxId, string? DestinationBankCode, string? DestinationAccountNumberMasked, decimal GrossAmount, decimal NetAmount,
    DateOnly PayDate, string Concept, int Status, IReadOnlyList<string> PendingReasons, int Flags, bool PaidByLendingFile, bool VoidRequested, bool GmfExempt,
    string? GmfExemptionReason, decimal? GmfEstimate, Guid? ReplacesOrderPublicId, string? BatchNumber, DateTime ReceivedAt);

public sealed record ItemDeLoteDto(Guid PublicId, Guid OrderPublicId, string OrderNumber, string BeneficiaryName, string BeneficiaryTaxId, int Form, decimal Amount,
    decimal GmfEstimate, int Flags, bool RequiresReview, string? ReviewedByName, DateTime? ReviewedAt, int Status);

public sealed record OrdenPendienteDto(Guid OrderPublicId, string OrderNumber, string Reason);

public sealed record ResumenDeArchivoDto(Guid PublicId, int Version, string FileName, string Sha256, int LineCount, decimal Total, DateTime GeneratedAt,
    DateTime? DownloadedAt, DateTime? SentAt, DateTime? VoidedAt, DateTime? RejectedByBankAt);

public sealed record LoteDePagoDto(Guid PublicId, string Number, Guid BankAccountPublicId, string BankAccountCode, int Form, string? FormatCode, DateOnly PayDate,
    int OrderCount, decimal Total, decimal GmfEstimate, int Status, Guid? ApprovalRequestPublicId, string PreparedByName, DateTime PreparedAt, DateTime? SentAt,
    string? UploadedAtBankBy, string? AuthorizedAtBankBy, string? Note, IReadOnlyList<ItemDeLoteDto> Items, IReadOnlyList<OrdenPendienteDto> Pending,
    IReadOnlyList<ResumenDeArchivoDto> Files);

public sealed record LineaDeVistaPreviaDto(int LineNumber, string OrderNumber, string BeneficiaryTaxId, string BeneficiaryName, string? BankCode,
    string? AccountNumberMasked, decimal Amount, string? Text);

public sealed record VistaPreviaDeLoteDto(Guid BatchPublicId, string Number, int Form, string? FormatCode, string? FileName, int LineCount, decimal Total,
    decimal GmfEstimate, IReadOnlyList<LineaDeVistaPreviaDto> Lines, IReadOnlyList<string> Warnings);

public sealed record ArchivoDePagoDto(Guid PublicId, Guid BatchPublicId, string BatchNumber, int Version, string FormatCode, string FileName, string Sha256,
    long SizeBytes, int LineCount, decimal Total, DateTime GeneratedAt, string GeneratedByName, DateTime? DownloadedAt, DateTime? SentAt, string? UploadedAtBankBy,
    string? AuthorizedAtBankBy, DateTime? VoidedAt, string? VoidReason, DateTime? RejectedByBankAt);

public sealed record EnlaceDeArchivoDto(bool Direct, string? Url, DateTimeOffset? ExpiresAt);

// ------------------------------------------------------------------------------------------------------------- cuerpos --

public sealed record CrearCuentaBancariaRequest(string Code, string Name, Guid BankPublicId, string AccountNumber, int AccountType, Guid BranchPublicId,
    int Purposes, bool IsGmfExempt, Guid? BankThirdPartyPersonPublicId, Guid? ChartAccountPublicId, DateOnly? ValidFrom);

public sealed record ActualizarCuentaBancariaRequest(string Name, Guid BranchPublicId, int Purposes, bool IsGmfExempt, Guid? BankThirdPartyPersonPublicId,
    Guid? ChartAccountPublicId);

public sealed record FijarAlcanceDeTesoreriaRequest(IReadOnlyList<Guid> BankAccountPublicIds);

public sealed record PedirUsoDelFondoRequest(Guid BankAccountPublicId, int Cause, decimal Amount, string Reason, Guid SupportAttachmentPublicId,
    Guid? SupervisorNoticeAttachmentPublicId);

public sealed record MotivoRequest(string? Reason);

public sealed record CrearLoteRequest(Guid BankAccountPublicId, int Form, DateOnly PayDate, IReadOnlyList<Guid> OrderPublicIds, Guid? BankFileFormatPublicId,
    Guid? LiquidityFundUsePublicId, string? Note);

public sealed record ItemsDeLoteRequest(IReadOnlyList<Guid> Add, IReadOnlyList<Guid> Remove);

public sealed record EnvioAlBancoRequest(string UploadedAtBankBy, string AuthorizedAtBankBy);
