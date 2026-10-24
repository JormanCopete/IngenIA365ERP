using System.Net.Http.Json;
using System.Text.Json;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Recaudo;

// Feature 015, US3 (T0120; contracts/api.md §4): lo que la caja lee y manda. Enums por número. El listado de obligaciones, cuentas y exigibles llega como
// lo devuelven los contratos (JSON), del que la pantalla toma lo que muestra. (nuevo)

public sealed record ConceptoVarioPagableDto(Guid MiscConceptPublicId, string Code, string Name, bool RequiresThirdParty, bool RequiresCrossDocument,
    bool RequiresInvoice, decimal? MaxAmount);

public sealed record ObligacionPagableDto(Guid ObligationPublicId, string Number, string CreditLineName, int Role, int DaysPastDue, decimal DueToday,
    decimal CycleInstallment, decimal PastDue, decimal PayoffAmount, IReadOnlyList<int> AdmittedModalities, IReadOnlyList<string> BlockReasons);

public sealed record ListadoPagableDto(IReadOnlyList<ObligacionPagableDto> Obligations);

public sealed record CuentaPagableDto(Guid AccountPublicId, string Number, int Nature, string LineName, int Status, bool AcceptsDeposits, IReadOnlyList<string> BlockReasons);

public sealed record CuentasPagablesDto(IReadOnlyList<CuentaPagableDto> Accounts);

public sealed record ExigiblePagableDto(Guid AccountPublicId, string AccountNumber, Guid InstallmentPublicId, string Period, DateOnly DueDate, decimal Pending);

public sealed record ExigiblesPagablesDto(IReadOnlyList<ExigiblePagableDto> Installments);

public sealed record LoQuePuedePagarDto(Guid PersonPublicId, string PersonName, string? TaxId, bool IsAssociate, ListadoPagableDto? Obligations,
    CuentasPagablesDto? Accounts, ExigiblesPagablesDto? DueInstallments, IReadOnlyList<ConceptoVarioPagableDto> MiscConcepts, IReadOnlyList<string> Notices);

public sealed record ItemPedidoRequest(int Target, decimal Amount, Guid? ProductPublicId = null, Guid? BeneficiaryPersonPublicId = null, int? LoanModality = null,
    int? AccountModality = null, int? Choice = null, int? InstallmentNumber = null, IReadOnlyList<Guid>? InstallmentPublicIds = null,
    bool AdvanceNoticeAccepted = false, Guid? MiscConceptPublicId = null, string? CrossDocument = null, Guid? InvoicePublicId = null, Guid? ItemId = null);

public sealed record AlternativaDeAbonoDto(int? Choice, decimal AppliedAmount, JsonElement? NewPlan);

public sealed record ItemDeCajaDto(Guid ItemId, int Target, decimal Amount, Guid? ProductPublicId, string? ProductNumber, Guid BeneficiaryPersonPublicId,
    int? LoanModality, int? AccountModality, int? Choice, int? InstallmentNumber, IReadOnlyList<Guid>? InstallmentPublicIds, bool AdvanceNoticeAccepted,
    Guid? MiscConceptPublicId, string? CrossDocument, Guid? InvoicePublicId, string? SimulationId, decimal? SimulatedApplied, decimal? SimulatedSurplus,
    bool AdvanceWithoutPrincipal, bool RequiresSourceOfFunds, IReadOnlyList<AlternativaDeAbonoDto> Alternatives, IReadOnlyList<string> Notices);

public sealed record MedioDeCajaRequest(Guid PaymentMeansPublicId, decimal Amount, string? Reference = null, Guid? CardTerminalPublicId = null,
    string? TerminalBatch = null, string? ApprovalCode = null, string? VoucherNumber = null, Guid? MeansId = null);

public sealed record PorcionDeCajaDto(Guid ItemId, Guid MeansId, decimal Amount);

public sealed record OperacionDeCajaDto(Guid PublicId, int Status, Guid SessionPublicId, Guid PayerPersonPublicId, Guid? BeneficiaryPersonPublicId,
    Guid? DeliveredByPersonPublicId, DateOnly ValueDate, IReadOnlyList<ItemDeCajaDto> Items, IReadOnlyList<MedioDeCajaRequest> Means,
    IReadOnlyList<PorcionDeCajaDto> Imputation, bool ImputationFixed, decimal TotalItems, decimal TotalMeans, decimal Change, bool SourceOfFundsDeclared,
    Guid? ReceiptPublicId, string? ReceiptNumber);

public sealed record CrearOperacionRequest(Guid SessionPublicId, Guid PayerPersonPublicId, Guid? BeneficiaryPersonPublicId, Guid? DeliveredByPersonPublicId,
    DateOnly? ValueDate, string? BackdateReason, Guid? BackdateSupportAttachmentPublicId);

public sealed record AplicacionDelReciboDto(Guid PublicId, int Ordinal, int Target, string? ProductNumber, decimal Amount, int? LoanModality, int? AccountModality,
    int? Choice, bool AdvanceNoticeAccepted, string? MiscConceptCode, string? ModuleVoucherNumber, DateOnly? FinalityUntil, int Status);

public sealed record MedioDelReciboDto(Guid PublicId, string MeansCode, decimal Amount, string? Reference, int Status);

public sealed record ReciboDto(Guid PublicId, string Number, int Status, int Channel, DateOnly ValueDate, DateTime RegisteredAt, Guid PayerPersonPublicId,
    string PayerName, Guid? DeliveredByPersonPublicId, bool PayerIsThirdParty, bool Masked, decimal TotalApplied, decimal TotalMeans, decimal ChangeGiven,
    string VerificationCode, IReadOnlyList<AplicacionDelReciboDto> Applications, IReadOnlyList<MedioDelReciboDto> Means);

/// <summary>
/// Cliente tipado de la caja de Recaudo (feature 015, US3, T0120; contracts/api.md §4): lo que la persona puede pagar, la operación de caja y su
/// confirmación, y los recibos. La confirmación se repite con <b>la misma</b> <see cref="ClaveDeOperacion"/> tras <c>DistributionChanged</c> o tras una
/// aprobación presente (con <c>X-Approval-Request</c>); la pantalla renueva la clave sólo al empezar otra operación. (nuevo)
/// </summary>
public sealed class CajaClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/receipts";

    public Task<ResultadoDeInventario<LoQuePuedePagarDto>> LoQuePuedePagarAsync(Guid? persona, string? documento, bool incluirCodeudor = false, CancellationToken ct = default,
        string? numero = null) =>
        EnvioDeVentas.EnviarAsync<LoQuePuedePagarDto>(http, auth, HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/payables",
            EnvioDeVentas.Query(("person", EnvioDeVentas.Texto(persona)), ("document", documento), ("includeCoDebtor", EnvioDeVentas.Texto(incluirCodeudor)),
                ("number", numero))),
            null, null, ct);

    public Task<ResultadoDeInventario<OperacionDeCajaDto>> CrearAsync(CrearOperacionRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<OperacionDeCajaDto>(http, auth, HttpMethod.Post, $"{Base}/cash-operations", r, clave, ct);

    public Task<ResultadoDeInventario<OperacionDeCajaDto>> ItemsAsync(Guid operacion, IReadOnlyList<ItemPedidoRequest> items, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<OperacionDeCajaDto>(http, auth, HttpMethod.Put, $"{Base}/cash-operations/{operacion}/items", new { items }, clave, ct);

    public Task<ResultadoDeInventario<OperacionDeCajaDto>> MediosAsync(Guid operacion, IReadOnlyList<MedioDeCajaRequest> medios, IReadOnlyList<PorcionDeCajaDto>? fijada,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<OperacionDeCajaDto>(http, auth, HttpMethod.Put, $"{Base}/cash-operations/{operacion}/means",
            new { means = medios, fixedImputation = fijada }, clave, ct);

    public Task<ResultadoDeInventario<OperacionDeCajaDto>> DeclararAsync(Guid operacion, string origen, decimal efectivo, Guid soporte, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<OperacionDeCajaDto>(http, auth, HttpMethod.Post, $"{Base}/cash-operations/{operacion}/declaration",
            new { origin = origen, cashAmount = efectivo, supportAttachmentPublicId = soporte }, clave, ct);

    /// <summary>Quién efectúa el pago (decisión del dueño del 2026-10-24, T0115).</summary>
    public Task<ResultadoDeInventario<OperacionDeCajaDto>> QuienEfectuaAsync(Guid operacion, Guid? persona, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<OperacionDeCajaDto>(http, auth, HttpMethod.Put, $"{Base}/cash-operations/{operacion}/delivered-by",
            new { deliveredByPersonPublicId = persona }, clave, ct);

    public Task<ResultadoDeInventario<OperacionDeCajaDto>> AbandonarAsync(Guid operacion, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<OperacionDeCajaDto>(http, auth, HttpMethod.Post, $"{Base}/cash-operations/{operacion}/abandon", null, clave, ct);

    /// <summary>Confirma; con <paramref name="aprobacion"/> repite tras un 428 con la misma clave.</summary>
    public async Task<ResultadoDeInventario<ReciboDto>> ConfirmarAsync(Guid operacion, ClaveDeOperacion clave, Guid? aprobacion = null, CancellationToken ct = default)
    {
        if (aprobacion is null) return await EnvioDeVentas.EnviarAsync<ReciboDto>(http, auth, HttpMethod.Post, $"{Base}/cash-operations/{operacion}/confirm", null, clave, ct);
        if (auth.CurrentAccessToken is null) return ResultadoDeInventario<ReciboDto>.SinToken();
        try
        {
            var url = $"{Base}/cash-operations/{operacion}/confirm";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            clave.Aplicar(req, new { metodo = "POST", url, cuerpo = (object?)null });
            req.Headers.Add(SesionesClient.CabeceraDeAprobacion, aprobacion.Value.ToString());
            using var resp = await http.SendAsync(req, ct);
            var r = await ResultadoDeInventario<ReciboDto>.DesdeAsync(resp, ct);
            if (r.IsSuccess) clave.Exito();
            return r;
        }
        catch (HttpRequestException ex)
        {
            return ResultadoDeInventario<ReciboDto>.ErrorDeRed(ex.Message);
        }
    }

    public Task<ResultadoDeInventario<IReadOnlyList<ReciboDto>>> RecibosAsync(DateOnly? desde, DateOnly? hasta, string? numero, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<IReadOnlyList<ReciboDto>>(http, auth, HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/receipts",
            EnvioDeVentas.Query(("from", EnvioDeVentas.Texto(desde)), ("to", EnvioDeVentas.Texto(hasta)), ("number", numero))), null, null, ct);

    public Task<ResultadoDeInventario<ReciboDto>> ReciboAsync(Guid recibo, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<ReciboDto>(http, auth, HttpMethod.Get, $"{Base}/receipts/{recibo}", null, null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DocumentoAsync(Guid recibo, bool carta, bool copia, CancellationToken ct = default) =>
        EnvioDeVentas.DescargarAsync(http, auth, $"{Base}/receipts/{recibo}/document?format={(carta ? "letter" : "thermal")}&copy={(copia ? "true" : "false")}",
            "recibo.pdf", ct);

    public static string Destino(int destino) => destino switch { 1 => "Crédito", 2 => "Cuenta", 3 => "Concepto vario", _ => "—" };

    public static string Modalidad(int? modalidad) => modalidad switch
    {
        1 => "Cancelación total",
        2 => "Lo vencido",
        3 => "Cuota del ciclo",
        4 => "Anticipo",
        5 => "Abono extraordinario a capital",
        6 => "Cuota específica",
        7 => "Valor libre",
        _ => "—",
    };
}
