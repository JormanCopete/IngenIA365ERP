using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Recaudo;

// Feature 015, R2 (fase 38, T0435): lo que las pantallas de convenios leen y mandan. Los enums viajan como número (clase 1 banco, 2 corresponsal, 3 pasarela; modo 1 por
// producto, 2 por persona; dígito 0 ninguno, 1 módulo 10, 2 módulo 11; resultado de línea 1 aplicada, 2 partida, 3 omitida, 4 no aprobada). (nuevo)

public sealed record VersionDeConvenioDto(Guid PublicId, DateOnly ValidFrom, DateOnly? ValidTo, int ReferenceMode, int ReferenceLength, int CheckDigit, Guid FormatPublicId,
    string FormatCode, decimal? CommissionPerPayment, decimal? CommissionRate, string? Note);

public sealed record ConvenioDto(Guid PublicId, string Code, string Name, int Kind, Guid OwnBankAccountPublicId, string? OwnBankAccountCode, Guid? PartnerPersonPublicId,
    string? PartnerName, string? PartnerAgreementCode, bool IsActive, VersionDeConvenioDto? Current, IReadOnlyList<VersionDeConvenioDto> Versions);

public sealed record ConvenioRequest(string Code, string Name, int Kind, Guid OwnBankAccountPublicId, Guid FormatPublicId, int ReferenceMode, int ReferenceLength, int CheckDigit,
    DateOnly? ValidFrom, Guid? PartnerPersonPublicId, string? PartnerAgreementCode, decimal? CommissionPerPayment, decimal? CommissionRate, bool IsActive, string? Note);

public sealed record LineaDeArchivoDeConvenioDto(int Line, DateOnly PaymentDate, decimal Amount, string? Reference, string? PartnerSequence, string? PayerTaxId,
    string? PayerName, int Outcome, string? Code, string? Message, string? ReceiptNumber, string? UnidentifiedItemNumber, Guid? PersonPublicId, Guid? ProductPublicId,
    string? GatewayOutcome);

public sealed record ArchivoDeConvenioDto(Guid PublicId, string Number, bool IsGateway, Guid AgreementPublicId, string AgreementCode, string? FileName, DateOnly FileDate,
    int Lines, int Applied, int Unidentified, int Rejected, int Omitted, decimal TotalInformed, decimal TotalApplied, decimal TotalUnidentified, int Status,
    DateTime LoadedAt, Guid? AttachmentPublicId, Guid? ReconciliationPublicId, IReadOnlyList<LineaDeArchivoDeConvenioDto> Detail);

public sealed record LineaIlegibleDeConvenioDto(int Line, string? Field, string Message);

public sealed record CargaDeArchivoDeConvenioDto(bool Preview, ArchivoDeConvenioDto? File, int Lines, int ToApply, int ToUnidentified, int NotApproved, int Omitted, decimal Total,
    IReadOnlyList<LineaDeArchivoDeConvenioDto> Review, IReadOnlyList<LineaIlegibleDeConvenioDto> Illegible);

public sealed record RetencionDeConvenioDto(string Kind, decimal Amount, Guid? ThirdPartyPersonPublicId = null);

public sealed record ConciliacionDeConvenioDto(Guid PublicId, string Number, Guid AgreementPublicId, string AgreementCode, Guid? FilePublicId, string? FileNumber,
    Guid StatementLinePublicId, DateOnly CreditedOn, decimal InformedAmount, decimal CreditedAmount, decimal CommissionAmount, decimal VatAmount, decimal WithholdingsAmount,
    IReadOnlyList<RetencionDeConvenioDto> Withholdings, decimal UnexplainedAmount, decimal Tolerance, int Status, DateTime RegisteredAt, DateTime? ApprovedAt, string? Note);

public sealed record ConciliacionDeConvenioRequest(Guid FilePublicId, Guid StatementLinePublicId, decimal Commission, decimal Vat, IReadOnlyList<RetencionDeConvenioDto>? Withholdings,
    string? Note);

public sealed record ReferenciaDePagoDto(Guid PublicId, string Reference, string BaseNumber, string CheckDigit, int Algorithm, int Mode, Guid PersonPublicId, string PersonName,
    Guid? ProductPublicId, int? Target, string? ProductNumber, int Status, DateTime IssuedAt);

public sealed record ReferenciaDePagoRequest(Guid PersonPublicId, Guid? ProductPublicId, string? ProductNumber);

/// <summary>Cliente tipado de los convenios de recaudo, sus archivos, su conciliación y las referencias de pago (feature 015, R2, fase 38; contracts/api.md §8). (nuevo)</summary>
public sealed class ConveniosClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/receipts/collection-agreements";

    public Task<ResultadoDeInventario<IReadOnlyList<ConvenioDto>>> ConveniosAsync(bool soloActivos = false, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ConvenioDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery(Base, EnvioDeVentas.Query(("onlyActive", soloActivos ? "true" : "false"))), null, null, ct);

    public Task<ResultadoDeInventario<ConvenioDto>> GuardarAsync(Guid? convenio, ConvenioRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConvenioDto>(convenio is null ? HttpMethod.Post : HttpMethod.Put, convenio is null ? Base : $"{Base}/{convenio}", r, clave, ct);

    /// <summary>Los formatos activos de archivos de convenio (ámbito 8) y de pasarela (ámbito 10).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<FormatoBancarioResumenDto>>> FormatosAsync(bool pasarela, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<FormatoBancarioResumenDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery("/api/core/bank-file-formats",
            EnvioDeVentas.Query(("scope", pasarela ? "PaymentGatewayFile" : "CollectionAgreementFile"), ("onlyActive", "true"))), null, null, ct);

    public Task<ResultadoDeInventario<CargaDeArchivoDeConvenioDto>> CargarAsync(Guid convenio, string nombre, byte[] contenido, bool revisar, bool aceptarIlegibles,
        int? lineasDeclaradas, decimal? totalDeclarado, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeRecaudo.MultipartAsync<CargaDeArchivoDeConvenioDto>(http, auth, $"{Base}/{convenio}/files", nombre, contenido, new Dictionary<string, string?>
        {
            ["preview"] = revisar ? "true" : "false", ["acceptIllegible"] = aceptarIlegibles ? "true" : "false",
            ["declaredLines"] = lineasDeclaradas?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["declaredTotal"] = totalDeclarado?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ArchivoDeConvenioDto>>> ArchivosAsync(Guid? convenio, bool pasarela, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ArchivoDeConvenioDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery(pasarela ? "/api/receipts/gateway-files" : $"{Base}/files",
            EnvioDeVentas.Query(("agreement", EnvioDeVentas.Texto(convenio)))), null, null, ct);

    public Task<ResultadoDeInventario<ConciliacionDeConvenioDto>> ConciliarAsync(Guid convenio, ConciliacionDeConvenioRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ConciliacionDeConvenioDto>(HttpMethod.Post, $"{Base}/{convenio}/reconciliations", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ConciliacionDeConvenioDto>>> ConciliacionesAsync(Guid? convenio = null, int? estado = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ConciliacionDeConvenioDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/reconciliations",
            EnvioDeVentas.Query(("agreement", EnvioDeVentas.Texto(convenio)), ("state", EnvioDeVentas.Texto(estado)))), null, null, ct);

    public Task<ResultadoDeInventario<ConciliacionDeConvenioDto>> AprobarAsync(Guid conciliacion, string? nota, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConciliacionDeConvenioDto>(HttpMethod.Post, $"{Base}/reconciliations/{conciliacion}/approve", new NotaRequest(nota), clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ReferenciaDePagoDto>>> ReferenciasAsync(Guid? persona, string? referencia, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ReferenciaDePagoDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery("/api/receipts/payment-references",
            EnvioDeVentas.Query(("person", EnvioDeVentas.Texto(persona)), ("reference", referencia))), null, null, ct);

    public Task<ResultadoDeInventario<ReferenciaDePagoDto>> EmitirReferenciaAsync(ReferenciaDePagoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ReferenciaDePagoDto>(HttpMethod.Post, "/api/receipts/payment-references", r, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeRecaudo.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, null, ct);
}
