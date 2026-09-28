using System.Text.Json;
using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Services.Ventas;

// Espejos de puntos de venta, cajas y la venta en el POS (feature 012, I3, T633; contracts/api.md §20.1–§20.3). Los enums entran como
// número (int) y se mandan por nombre (string, con TextosDeVentas). Sólo PublicId (Principio VI). De una tarjeta sólo viajan los últimos
// cuatro dígitos (LosPagosNoGuardanElNumeroDeTarjeta). (nuevos)

// ------------------------------------------------------------------------------------- puntos y cajas --

public sealed record PuntoDeVentaDto(
    Guid PointOfSalePublicId,
    string Code,
    string Name,
    Guid BranchPublicId,
    Guid SalesChannelPublicId,
    bool PosEnabled,
    Guid DefaultWarehousePublicId,
    string? Address,
    bool IsActive,
    int CashRegisterCount)
{
    public IReadOnlyList<CajaDto>? CashRegisters { get; init; }
}

public sealed record TipoDeCajaDto(int Role, Guid DocumentTypePublicId, string DocumentTypeCode, int Class, string? Prefix);

public sealed record SesionAbiertaDeCajaDto(Guid CashSessionPublicId, string CashierName, DateTime OpenedAt);

public sealed record CajaDto(
    Guid CashRegisterPublicId,
    Guid PointOfSalePublicId,
    string Code,
    string Name,
    Guid WarehousePublicId,
    string WarehouseCode,
    Guid? DefaultCardTerminalPublicId,
    int PrintFormat,
    string? DianCashRegisterPlate,
    byte PrintCopies,
    bool IsActive,
    IReadOnlyList<TipoDeCajaDto> DocumentTypes,
    SesionAbiertaDeCajaDto? OpenSession,
    string? DianCashRegisterTypeCode = null);

public sealed record PuntoCreadoDto(Guid PointOfSalePublicId);

public sealed record CajaCreadaDto(Guid CashRegisterPublicId);

/// <summary>El cuerpo de un punto (§20.1). La edición ignora código y sucursal.</summary>
public sealed record PuntoDeVentaRequest(string? Code, string Name, Guid? BranchPublicId, Guid SalesChannelPublicId, bool PosEnabled, Guid DefaultWarehousePublicId,
    bool IsActive, string? Address);

public sealed record TipoDeCajaRequest(string Role, Guid DocumentTypePublicId);

/// <summary>El cuerpo de una caja (§20.1): <c>printFormat</c> por nombre (<c>Ticket58</c>, <c>Ticket80</c>, <c>Letter</c>).</summary>
public sealed record CajaRequest(string? Code, string Name, Guid WarehousePublicId, Guid? DefaultCardTerminalPublicId, string PrintFormat,
    IReadOnlyList<TipoDeCajaRequest> DocumentTypes, string? DianCashRegisterPlate, byte? PrintCopies, bool IsActive,
    string? DianCashRegisterTypeCode = null);

// ----------------------------------------------------------------------------------------- la venta --

public sealed record RefDelPosDto(Guid PublicId, string Code, string Name);

public sealed record LecturaDelPosDto(RefDelPosDto Product, int Status, RefDelPosDto Unit, decimal Factor, decimal Price, RefDelPosDto? PriceList, bool IncludesTaxes,
    decimal? Available);

public sealed record TipoDeVentaPosDto(Guid PublicId, string Code, int Role);

public sealed record ClienteDelPosDto(Guid? PersonPublicId, string Name, bool IsFinalConsumer, string? Segment);

public sealed record VendedorDelPosDto(Guid SalespersonPublicId, string Name);

public sealed record AprobacionDeDescuentoDto(Guid? ApprovalRequestPublicId, string Status);

public sealed record DescuentoDeLineaPosDto(byte Sequence, int Source, bool FromDocumentDiscount, bool IsPriceOverride, decimal? Percent, decimal Amount,
    bool RequiresApproval, AprobacionDeDescuentoDto? Approval);

public sealed record ImpuestoDeLineaPosDto(string TaxRateCode, int Kind, decimal? Rate, decimal Base, decimal Amount);

public sealed record LineaDelPosDto(
    Guid LinePublicId,
    int LineNumber,
    RefDelPosDto Product,
    RefDelPosDto Unit,
    decimal Factor,
    decimal Quantity,
    decimal QuantityBase,
    decimal RoundingQuantity,
    decimal ListPrice,
    decimal UnitPrice,
    bool IncludesTaxes,
    RefDelPosDto? PriceList,
    IReadOnlyList<DescuentoDeLineaPosDto> Discounts,
    IReadOnlyList<ImpuestoDeLineaPosDto> Taxes,
    decimal Total,
    decimal? Available,
    bool BelowCost)
{
    /// <summary>La suma de sus descuentos (el manual, el prorrateado del total y la diferencia de un precio digitado).</summary>
    public decimal Descuento => Discounts.Sum(d => d.Amount);

    /// <summary>Tiene un descuento sobre el tope que espera aprobación.</summary>
    public bool EsperaAprobacion => Discounts.Any(d => d.RequiresApproval && d.Approval is { Status: not "Approved" });
}

public sealed record TotalesDelPosDto(decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal WithholdingTotal, decimal Total, decimal AmountDue);

public sealed record DescuentoDelDocumentoPosDto(decimal? Percent, decimal Amount, bool RequiresApproval);

public sealed record DatafonoDelPosDto(Guid PublicId, string Code);

public sealed record CreditoPropuestoDto(short? TermDays, short? Installments, short? PeriodicityDays, string? SuggestedLineCode);

public sealed record MedioDelPosDto(
    Guid PaymentMeansPublicId,
    string Code,
    string Name,
    int Class,
    string? QuickKey,
    bool RequiresReference,
    int? ReferenceKind,
    bool AllowsChange,
    bool AllowsPartial,
    int CountMethod,
    IReadOnlyList<DatafonoDelPosDto> CardTerminals,
    Guid? DefaultCardTerminalPublicId,
    CreditoPropuestoDto? CreditDefaults);

public sealed record SuspendidaDto(string? Label, DateTime? SuspendedAt, string? SuspendedByName);

public sealed record AprobacionPendienteDelPosDto(Guid ApprovalRequestPublicId, string Subject, string SourceType, int? LineNumber, string Status);

public sealed record VentaDelPosDto(
    Guid DraftPublicId,
    int Status,
    int Class,
    TipoDeVentaPosDto DocumentType,
    Guid? CashSessionPublicId,
    RefDelPosDto PointOfSale,
    RefDelPosDto CashRegister,
    RefDelPosDto Warehouse,
    DateOnly OperationDate,
    ClienteDelPosDto Customer,
    VendedorDelPosDto? Salesperson,
    IReadOnlyList<LineaDelPosDto> Lines,
    LineaDelPosDto? LastLine,
    DescuentoDelDocumentoPosDto? DocumentDiscount,
    TotalesDelPosDto Totals,
    IReadOnlyList<MedioDelPosDto> AvailablePaymentMeans,
    SuspendidaDto? Suspended,
    IReadOnlyList<AprobacionPendienteDelPosDto> PendingApprovals,
    IReadOnlyList<AvisoDeInventarioDto> Warnings,
    string? Notes,
    byte[]? RowVersion);

public sealed record ResumenDeVentaPosDto(Guid DraftPublicId, Guid? CashSessionPublicId, string? CashierName, int Lines, decimal Total, SuspendidaDto? Suspended);

// ---------------------------------------------------------------------------------------- pedidos --

public sealed record PrimeraLecturaRequest(string Code, decimal? Quantity = null);

public sealed record AbrirVentaRequest(Guid CashSessionPublicId, PrimeraLecturaRequest? FirstLine = null);

public sealed record DescuentoRequest(decimal? Percent = null, decimal? Amount = null);

/// <summary><c>PATCH /pos/drafts/{id}</c>: cliente, vendedor, rol (<c>PosSale</c> o <c>InvoiceOnRequest</c>), descuento por total y notas.</summary>
public sealed record CabeceraDeVentaRequest(Guid? CustomerPersonPublicId = null, bool? ClearCustomer = null, Guid? SalespersonPublicId = null,
    bool? ClearSalesperson = null, string? Role = null, DescuentoRequest? DocumentDiscount = null, string? Notes = null);

/// <summary>Una lectura: por código (el de empaque trae su unidad) o por producto y unidad.</summary>
public sealed record LecturaRequest(string? Code, Guid? ProductPublicId = null, Guid? UnitPublicId = null, decimal? Quantity = null);

public sealed record LineaRequest(decimal? Quantity = null, decimal? UnitPrice = null, DescuentoRequest? Discount = null, bool? ClearUnitPrice = null);

public sealed record RotuloRequest(string? Label);

public sealed record RecuperarVentaRequest(Guid CashSessionPublicId);

public sealed record MotivoRequest(string Reason);

public sealed record CreditoDelPagoRequest(short Installments, short TermDays, short PeriodicityDays, DateOnly? FirstDueDate = null, string? SuggestedLineCode = null);

/// <summary>
/// Un pago (<c>DocumentPaymentInput</c>, §22.4). De una tarjeta viajan el datáfono, la autorización, el lote y
/// <see cref="Last4"/>: nunca el número (<c>LosPagosNoGuardanElNumeroDeTarjeta</c>).
/// </summary>
public sealed record PagoRequest(
    Guid PaymentMeansPublicId,
    decimal Amount,
    decimal? Tendered = null,
    string? Reference = null,
    string? AuthorizationCode = null,
    Guid? CardTerminalPublicId = null,
    string? BatchNumber = null,
    string? Last4 = null,
    Guid? CashSessionPublicId = null,
    CreditoDelPagoRequest? Credit = null);

public sealed record CobroRequest(IReadOnlyList<PagoRequest> Payments, decimal ExpectedAmountDue, string? SendEmailTo);

// ----------------------------------------------------------------------------------------- tirilla --

public sealed record EncabezadoDeTirillaDto(string CompanyName, string Nit, string BranchName, string? Address, string? ResolutionText, string RegimeText);

public sealed record DocumentoDeTirillaDto(string ClassLabel, string Prefix, long? Number, DateTime? IssuedAt, string? CashRegisterCode, string? CashierName,
    string? SalespersonName);

public sealed record TerceroDeTirillaDto(string Name, string IdType, string IdNumber);

public sealed record LineaDeTirillaDto(string Code, string Description, decimal Quantity, string UnitCode, decimal UnitPrice, decimal Discount, decimal Total, string TaxMark);

public sealed record ImpuestoDeTirillaDto(string Label, decimal Base, decimal Amount);

public sealed record RetencionDeTirillaDto(string Label, decimal Amount);

public sealed record TotalesDeTirillaDto(decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal Total, decimal AmountDue);

public sealed record PagoDeTirillaDto(string MeansName, decimal Amount, string? Reference, string? Last4);

public sealed record ElectronicoDeTirillaDto(string UniqueCodeKind, string UniqueCode, string QrContent, string? Legend);

/// <summary>El modelo de la tirilla (§20.2) que pinta <c>TirillaDeVenta</c> y se imprime por <c>IImpresionDeDocumentos</c>.</summary>
public sealed record TirillaDto(
    int Format,
    bool Copy,
    EncabezadoDeTirillaDto Header,
    DocumentoDeTirillaDto Document,
    TerceroDeTirillaDto Party,
    IReadOnlyList<LineaDeTirillaDto> Lines,
    IReadOnlyList<ImpuestoDeTirillaDto> Taxes,
    IReadOnlyList<RetencionDeTirillaDto> Withholdings,
    TotalesDeTirillaDto Totals,
    IReadOnlyList<PagoDeTirillaDto> Payments,
    decimal Change,
    ElectronicoDeTirillaDto? Electronic,
    IReadOnlyList<string> Footer);

/// <summary>El resultado del cobro (§20.2): confirmado o en aprobación, con la tirilla si ya es entregable.</summary>
public sealed record ResultadoDeCobroDto(
    Guid DocumentPublicId,
    int Status,
    Guid? ApprovalRequestPublicId,
    int Class,
    string? Prefix,
    long? Number,
    decimal Total,
    decimal AmountDue,
    decimal Change,
    int? PostingMode,
    JsonElement? Electronic,
    TirillaDto? Ticket,
    IReadOnlyList<AvisoDeInventarioDto> Warnings);

// ------------------------------------------------------------------------------------- entrega --

/// <summary><c>POST …/deliver</c> y <c>/reprint</c>: formato por nombre, si se manda por correo y a cuál, y el motivo de la copia.</summary>
public sealed record EntregaRequest(string Format, bool? SendEmail = null, string? Email = null, string? Reason = null);

/// <summary>
/// La entrega o la reimpresión: una tirilla vuelve como modelo (<see cref="Ticket"/>); una carta, como archivo PDF
/// (<see cref="Archivo"/>).
/// </summary>
public sealed record EntregaDeVentaDto(Guid DocumentPublicId, int Format, bool Copy, TirillaDto? Ticket, string? FileName, bool EmailSent)
{
    public IngenIA365ERP.Shared.Services.Nomina.ArchivoDescargado? Archivo { get; init; }

    /// <summary>
    /// I4 (T758; §20.3): la carta de un documento electrónico no viaja como archivo sino como el enlace firmado de 60 s a su representación
    /// gráfica guardada. (nuevo)
    /// </summary>
    public IngenIA365ERP.Shared.Services.Adjuntos.EnlaceDeDescargaDto? Link { get; init; }
}

// ------------------------------------------------------------------------------ aprobador en persona --

/// <summary>El desafío del aprobador presente (§15.2): vence a los dos minutos; <see cref="PublicKeyOptions"/> va tal cual a webauthn.js.</summary>
public sealed record DesafioDePresenciaDto(Guid ChallengePublicId, DateTime ExpiresAt, IReadOnlyList<string> Methods, string? PublicKeyOptions);

public sealed record DesafioDePresenciaRequest(string ApproverEmail);

/// <summary>La prueba del aprobador presente: la aserción de su passkey o un código TOTP de un solo uso. Nunca una contraseña.</summary>
public sealed record PresenciaRequest(Guid ChallengePublicId, string? Assertion, string? TotpCode);

/// <summary>La decisión en persona (§15.2): <c>Approve</c> o <c>Reject</c>, método <c>InPersonPasskey</c> o <c>InPersonTotp</c>.</summary>
public sealed record DecisionEnPersonaRequest(string Decision, string? Reason, string Method, PresenciaRequest Presence, string? ExpectedContentSha256);
