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

/// <summary>
/// Un concepto vario que se puede cobrar. 2026-11-16 (pedido del dueño): cómo funciona su tercero —1 quien paga, 2 el tercero fijo del concepto (p. ej.
/// la aseguradora del SOAT), 3 lo elige el cajero— con el tercero fijo, el valor sugerido y la cuenta contable a la que va (informativa, de la matriz).
/// </summary>
public sealed record ConceptoVarioPagableDto(Guid MiscConceptPublicId, string Code, string Name, bool RequiresThirdParty, bool RequiresCrossDocument,
    bool RequiresInvoice, decimal? MaxAmount, int ThirdPartyMode = 1, Guid? DefaultThirdPartyPublicId = null, string? DefaultThirdPartyName = null,
    decimal? SuggestedAmount = null);

public sealed record ObligacionPagableDto(Guid ObligationPublicId, string Number, string CreditLineName, int Role, int DaysPastDue, decimal DueToday,
    decimal CycleInstallment, decimal PastDue, decimal PayoffAmount, IReadOnlyList<int> AdmittedModalities, IReadOnlyList<string> BlockReasons,
    decimal Advance = 0m, decimal CreditBalance = 0m)
{
    /// <summary>2026-11-16: tiene algo causado o vencido por pagar (lo exigible a hoy).</summary>
    public bool ConExigible => DueToday > 0m;

    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 4): lo reconocido al inicio del ciclo y todavía no causado de la cuota del ciclo: la cuota del período se
    /// ofrece completa (la cuota del ciclo) y sin el aviso del anticipo.
    /// </summary>
    public decimal RecognizedPending { get; init; }

    /// <summary>La cuota del ciclo está reconocida al inicio de su ciclo.</summary>
    public bool CuotaReconocida => RecognizedPending > 0m;
}

/// <summary>
/// 2026-11-16 (pedido del dueño): una cuota de la obligación en el detalle que ve la caja antes de cobrar. <see cref="Status"/>: PastDue, DueToday,
/// Upcoming, Covered o Paid. <see cref="ValueToAnticipate"/> es lo que cuesta anticiparla hoy (capital, interés, seguro y otros, menos lo ya anticipado).
/// </summary>
public sealed record CuotaDeObligacionDto(int Number, DateOnly DueDate, string Status, decimal Principal, decimal Interest, decimal Insurance, decimal Other,
    decimal Total, decimal Owed, decimal AdvanceApplied, decimal ValueToAnticipate)
{
    /// <summary>2026-11-16 (pedido del dueño; feature 024, fase 2): la administración de la cuota, aparte del seguro y dentro del total.</summary>
    public decimal Administration { get; init; }

    /// <summary>2026-11-16 (pedido del dueño; feature 024, fase 4): lo reconocido al inicio del ciclo y todavía no causado de la cuota.</summary>
    public decimal Recognized { get; init; }
}

/// <summary>Un saldo de la obligación por concepto (capital, interés corriente, mora, seguro, cargos, anticipo…).</summary>
public sealed record SaldoPorConceptoDto(string Concept, string Name, decimal Amount);

/// <summary>2026-11-16: el detalle de una obligación para la caja (lo que el cajero quiere saber antes de cobrar).</summary>
public sealed record DetalleDeObligacionDto(Guid ObligationPublicId, string Number, string CreditLineName, string Status, int DaysPastDue, decimal DueToday,
    decimal PastDue, decimal PayoffAmount, decimal Advance, decimal CreditBalance, decimal? RegularInstallment, DateOnly? NextDueDate,
    IReadOnlyList<SaldoPorConceptoDto> Balances, IReadOnlyList<CuotaDeObligacionDto> Installments, IReadOnlyList<int> AdmittedModalities,
    bool AllowsSpecificInstallmentOutOfOrder, IReadOnlyList<string> Notices)
{
    /// <summary>2026-11-16 (pedido del dueño; feature 024, fase 4): lo reconocido al inicio del ciclo y todavía no causado de la cuota del ciclo.</summary>
    public decimal RecognizedPending { get; init; }
}

public sealed record ListadoPagableDto(IReadOnlyList<ObligacionPagableDto> Obligations, int LendingStatus = 2);

public sealed record CuentaPagableDto(Guid AccountPublicId, string Number, int Nature, string LineName, int Status, bool AcceptsDeposits, IReadOnlyList<string> BlockReasons);

public sealed record CuentasPagablesDto(IReadOnlyList<CuentaPagableDto> Accounts);

public sealed record ExigiblePagableDto(Guid AccountPublicId, string AccountNumber, Guid InstallmentPublicId, string Period, DateOnly DueDate, decimal Pending);

public sealed record ExigiblesPagablesDto(IReadOnlyList<ExigiblePagableDto> Installments, int DepositsStatus = 2, int ContributionsStatus = 2,
    IReadOnlyList<Guid>? MigratedNotActivated = null);

/// <summary>
/// Lo que la persona puede pagar. 2026-11-16 (pedido del dueño): con las reglas de la caja de la cooperativa —la prelación por clase de producto
/// (<c>Caja.PrelacionPorClase</c>: Aportes, Creditos, Ahorros, Facturas, Conceptos; vacía = libre), si con mora sólo se paga lo exigible
/// (<c>Caja.ConMoraSoloExigible</c>) y si lo no exigible exige al menos una cuota (<c>Caja.AnticipoMinimoUnaCuota</c>)— para que la pantalla las
/// aplique antes de que el servidor las rechace.
/// </summary>
public sealed record LoQuePuedePagarDto(Guid PersonPublicId, string PersonName, string? TaxId, bool IsAssociate, ListadoPagableDto? Obligations,
    CuentasPagablesDto? Accounts, ExigiblesPagablesDto? DueInstallments, IReadOnlyList<ConceptoVarioPagableDto> MiscConcepts, IReadOnlyList<string> Notices,
    IReadOnlyList<string>? PriorityClasses = null, bool OverdueOnlyDue = true, bool MinimumOneInstallment = true);

public sealed record ItemPedidoRequest(int Target, decimal Amount, Guid? ProductPublicId = null, Guid? BeneficiaryPersonPublicId = null, int? LoanModality = null,
    int? AccountModality = null, int? Choice = null, int? InstallmentNumber = null, IReadOnlyList<Guid>? InstallmentPublicIds = null,
    bool AdvanceNoticeAccepted = false, Guid? MiscConceptPublicId = null, string? CrossDocument = null, Guid? InvoicePublicId = null, Guid? ItemId = null,
    int? PendingTarget = null, Guid? LoadObligationPublicId = null, string? ProductReference = null);

public sealed record AlternativaDeAbonoDto(int? Choice, decimal AppliedAmount, JsonElement? NewPlan);

/// <summary>Feature 015, R1 (T0398): una cuenta por cobrar comercial pendiente (factura). (nuevo)</summary>
public sealed record CuentaPorCobrarComercialDto(Guid? AccountingDocumentPublicId, string InvoiceNumber, DateOnly Date, decimal AccountingPending, decimal AppliedByReceipts,
    decimal Pending);

public sealed record ItemDeCajaDto(Guid ItemId, int Target, decimal Amount, Guid? ProductPublicId, string? ProductNumber, Guid BeneficiaryPersonPublicId,
    int? LoanModality, int? AccountModality, int? Choice, int? InstallmentNumber, IReadOnlyList<Guid>? InstallmentPublicIds, bool AdvanceNoticeAccepted,
    Guid? MiscConceptPublicId, string? CrossDocument, Guid? InvoicePublicId, string? SimulationId, decimal? SimulatedApplied, decimal? SimulatedSurplus,
    bool AdvanceWithoutPrincipal, bool RequiresSourceOfFunds, IReadOnlyList<AlternativaDeAbonoDto> Alternatives, IReadOnlyList<string> Notices,
    int? PendingTarget = null, Guid? LoadObligationPublicId = null, string? ProductReference = null)
{
    /// <summary>2026-11-17: qué paga de cada rubro o período y cómo queda el producto, según la simulación del módulo.</summary>
    public DesgloseDelPagoDto? Breakdown { get; init; }
}

/// <summary>2026-11-17: lo que un pago abona a un rubro (capital, interés corriente, interés de mora, seguro…).</summary>
public sealed record RubroDelPagoDto(string Concept, string Name, decimal Amount);

/// <summary>2026-11-17: lo que un pago a una cuenta abona a la cuota de un período.</summary>
public sealed record PeriodoDelPagoDto(string Period, decimal Amount);

/// <summary>
/// 2026-11-17 (pedido del dueño): el desglose de un ítem de la caja —rubros, cuotas, anticipo, saldo a favor y cómo queda el crédito; o los períodos y el
/// saldo de la cuenta—.
/// </summary>
public sealed record DesgloseDelPagoDto(IReadOnlyList<RubroDelPagoDto> Concepts, IReadOnlyList<int> Installments, decimal Advance, decimal CreditBalance,
    decimal? PayoffAfter, decimal? PastDueAfter, int? DaysPastDueAfter, DateOnly? NextDueDate, decimal? NextInstallment, bool PaidOff,
    IReadOnlyList<PeriodoDelPagoDto> Periods, decimal? AccountBalanceAfter);

/// <summary>Feature 015, R1 (T0365): el resultado de enviar un aviso (recibo o egreso). (nuevo)</summary>
public sealed record AvisoDeRecaudoDto(string Outcome, string Channel, string? Destination, string Message);

/// <summary>Feature 015, R1 (T0365; FR-046): un recaudo pendiente de aplicar. (nuevo)</summary>
public sealed record PendienteDeAplicarDto(Guid PublicId, Guid ReceiptPublicId, string ReceiptNumber, Guid PersonPublicId, string PersonName, int TargetModule,
    string? ProductReference, decimal Amount, DateOnly ValueDate, int Status, Guid? AppliedToProductPublicId, string? ModuleVoucherNumber);

public sealed record MedioDeCajaRequest(Guid PaymentMeansPublicId, decimal Amount, string? Reference = null, Guid? CardTerminalPublicId = null,
    string? TerminalBatch = null, string? ApprovalCode = null, string? VoucherNumber = null, Guid? MeansId = null, ChequeDeCajaRequest? Check = null,
    Guid? OwnBankAccountPublicId = null, string? Last4 = null);

/// <summary>Segunda ronda del 2026-10-27 (D-f): una cuenta propia que el cajero puede elegir para un medio de su sesión.</summary>
public sealed record OpcionDeCuentaDto(Guid PaymentMeansPublicId, Guid OwnBankAccountPublicId, string AccountLabel);

/// <summary>Feature 015, US15 (T0219): los datos del cheque que se recibe como medio. (nuevo)</summary>
public sealed record ChequeDeCajaRequest(Guid DraweeBankPublicId, string CheckNumber, string AccountNumber, DateOnly IssueDate, bool SamePlace = true,
    Guid? DrawerPersonPublicId = null, string? DrawerName = null);

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

    /// <summary>Feature 015, R1 (T0398): las cuentas por cobrar comerciales pendientes de la persona, por factura.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<CuentaPorCobrarComercialDto>>> CuentasPorCobrarAsync(Guid persona, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<IReadOnlyList<CuentaPorCobrarComercialDto>>(http, auth, HttpMethod.Get,
            EnvioDeVentas.ConQuery($"{Base}/commercial-receivables", EnvioDeVentas.Query(("person", EnvioDeVentas.Texto(persona)))), null, null, ct);

    public Task<ResultadoDeInventario<LoQuePuedePagarDto>> LoQuePuedePagarAsync(Guid? persona, string? documento, bool incluirCodeudor = false, CancellationToken ct = default,
        string? numero = null, string? tipoDeDocumento = null) =>
        EnvioDeVentas.EnviarAsync<LoQuePuedePagarDto>(http, auth, HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/payables",
            EnvioDeVentas.Query(("person", EnvioDeVentas.Texto(persona)), ("document", documento), ("includeCoDebtor", EnvioDeVentas.Texto(incluirCodeudor)),
                ("number", numero), ("documentType", tipoDeDocumento))),
            null, null, ct);

    public Task<ResultadoDeInventario<OperacionDeCajaDto>> CrearAsync(CrearOperacionRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<OperacionDeCajaDto>(http, auth, HttpMethod.Post, $"{Base}/cash-operations", r, clave, ct);

    public Task<ResultadoDeInventario<OperacionDeCajaDto>> ItemsAsync(Guid operacion, IReadOnlyList<ItemPedidoRequest> items, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<OperacionDeCajaDto>(http, auth, HttpMethod.Put, $"{Base}/cash-operations/{operacion}/items", new { items }, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<OpcionDeCuentaDto>>> CuentasDeTransferenciaAsync(Guid sesion, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<IReadOnlyList<OpcionDeCuentaDto>>(http, auth, HttpMethod.Get, $"{Base}/sessions/{sesion}/transfer-accounts", null, null, ct);

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

    /// <summary>
    /// 2026-11-18 (pedido del dueño): el recibo por su número como lo dice la persona. El número se guarda con ceros a la izquierda («000005»), así que
    /// si no aparece tal cual y son sólo dígitos se busca también completado a seis. Nulo si no existe o no está al alcance.
    /// </summary>
    public async Task<ResultadoDeInventario<ReciboDto?>> ReciboPorNumeroAsync(string numero, CancellationToken ct = default)
    {
        var limpio = numero.Trim();
        var r = await RecibosAsync(null, null, limpio, ct);
        if (!r.IsSuccess) return Fallo(r);
        if (r.Value is { Count: > 0 } hallados) return Exito(hallados[0]);
        if (limpio.Length is > 0 and < 6 && limpio.All(char.IsDigit))
        {
            var conCeros = await RecibosAsync(null, null, limpio.PadLeft(6, '0'), ct);
            if (!conCeros.IsSuccess) return Fallo(conCeros);
            if (conCeros.Value is { Count: > 0 } c) return Exito(c[0]);
        }
        return Exito(null);
    }

    private static ResultadoDeInventario<ReciboDto?> Exito(ReciboDto? recibo) => new(true, recibo, null, null, 200, null);

    private static ResultadoDeInventario<ReciboDto?> Fallo<T>(ResultadoDeInventario<T> r) => new(false, null, r.ErrorCode, r.ErrorMessage, r.StatusCode, r.Data);

    public Task<ResultadoDeInventario<ReciboDto>> ReciboAsync(Guid recibo, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<ReciboDto>(http, auth, HttpMethod.Get, $"{Base}/receipts/{recibo}", null, null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DocumentoAsync(Guid recibo, bool carta, bool copia, CancellationToken ct = default) =>
        EnvioDeVentas.DescargarAsync(http, auth, $"{Base}/receipts/{recibo}/document?format={(carta ? "letter" : "thermal")}&copy={(copia ? "true" : "false")}",
            "recibo.pdf", ct);

    /// <summary>Feature 015, R1 (T0365; FR-187): F4, todo lo exigible en una acción, con sus simulaciones.</summary>
    /// <summary>2026-11-16: el detalle de una obligación (cuotas, saldos por concepto y lo que cuesta anticipar cada cuota) a una fecha.</summary>
    public Task<ResultadoDeInventario<DetalleDeObligacionDto>> DetalleDeObligacionAsync(Guid obligacion, DateOnly? fecha = null, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<DetalleDeObligacionDto>(http, auth, HttpMethod.Get,
            EnvioDeVentas.ConQuery($"{Base}/payables/obligations/{obligacion}", EnvioDeVentas.Query(("date", EnvioDeVentas.Texto(fecha)))), null, null, ct);

    public Task<ResultadoDeInventario<OperacionDeCajaDto>> PreseleccionarAsync(Guid operacion, bool incluirCodeudor, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<OperacionDeCajaDto>(http, auth, HttpMethod.Post,
            $"{Base}/cash-operations/{operacion}/preselect?includeCoDebtor={(incluirCodeudor ? "true" : "false")}", null, clave, ct);

    /// <summary>Feature 015, R1 (T0365; FR-020): el recibo por correo con la autorización del canal.</summary>
    public Task<ResultadoDeInventario<AvisoDeRecaudoDto>> AvisarAsync(Guid recibo, string? correo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<AvisoDeRecaudoDto>(http, auth, HttpMethod.Post, $"{Base}/receipts/{recibo}/notify", new { channel = 1, email = correo }, clave, ct);

    /// <summary>Feature 015, R1 (T0365; FR-046): los recaudos pendientes de aplicar.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<PendienteDeAplicarDto>>> PendientesAsync(int? estado, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<IReadOnlyList<PendienteDeAplicarDto>>(http, auth, HttpMethod.Get,
            EnvioDeVentas.ConQuery($"{Base}/pending-applications", EnvioDeVentas.Query(("state", estado?.ToString(System.Globalization.CultureInfo.InvariantCulture)))),
            null, null, ct);

    public Task<ResultadoDeInventario<PendienteDeAplicarDto>> AplicarPendienteAsync(Guid pendiente, Guid producto, int? modalidadDeCredito, int? modalidadDeCuenta,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<PendienteDeAplicarDto>(http, auth, HttpMethod.Post, $"{Base}/pending-applications/{pendiente}/apply",
            new { productPublicId = producto, loanModality = modalidadDeCredito, accountModality = modalidadDeCuenta }, clave, ct);

    public static string Destino(int destino) => destino switch
    {
        1 => "Crédito", 2 => "Cuenta", 3 => "Concepto vario", 4 => "Cuenta por cobrar comercial", 5 => "Pendiente de aplicar", _ => "—",
    };

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
