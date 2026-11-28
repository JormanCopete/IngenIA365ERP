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
    decimal? SuggestedAmount = null, bool CollectedForThirdParty = false, bool IsAdvance = false);

/// <summary>2026-11-23: un anticipo de concepto disponible de la persona, con el medio con que la caja lo cruza.</summary>
public sealed record AnticipoDisponibleDto(Guid MiscConceptPublicId, string ConceptCode, string ConceptName, decimal Balance, Guid? MeansPublicId, string? MeansCode,
    string? MeansName);

public sealed record ObligacionPagableDto(Guid ObligationPublicId, string Number, string CreditLineName, int Role, int DaysPastDue, decimal DueToday,
    decimal CycleInstallment, decimal PastDue, decimal PayoffAmount, IReadOnlyList<int> AdmittedModalities, IReadOnlyList<string> BlockReasons,
    decimal Advance = 0m, decimal CreditBalance = 0m)
{
    /// <summary>2026-11-16: tiene algo causado o vencido por pagar (lo exigible a hoy).</summary>
    public bool ConExigible => DueToday > 0m;

    /// <summary>
    /// 2026-11-26 (pedido del dueño): lo exigible sin el interés ya causado de la cuota del período calendario en curso: la mora para las reglas de la caja.
    /// Nulo (una API anterior) se lee como <see cref="DueToday"/>.
    /// </summary>
    public decimal? DueWithoutPeriodInProgress { get; init; }

    /// <summary>2026-11-26: lo vencido (o que vence hoy): la mora para las reglas de la caja.</summary>
    public decimal Vencido => DueWithoutPeriodInProgress ?? DueToday;

    /// <summary>2026-11-26: tiene cuotas vencidas o que vencen hoy (el interés causado del mes en curso no cuenta).</summary>
    public bool ConVencido => Vencido > 0m;

    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 4): lo reconocido al inicio del ciclo y todavía no causado de la cuota del ciclo: la cuota del período se
    /// ofrece completa (la cuota del ciclo) y sin el aviso del anticipo.
    /// </summary>
    public decimal RecognizedPending { get; init; }

    /// <summary>La cuota del ciclo está reconocida al inicio de su ciclo.</summary>
    public bool CuotaReconocida => RecognizedPending > 0m;

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T071; FR-023): la cuota del período que ya va en un archivo de la pagaduría o en la nómina propia —en qué va, la cuota, el
    /// archivo y la frase que lo dice—; nula si no hay nada enviado.
    /// </summary>
    public CuotaEnviadaDeLaCajaDto? SentInstallment { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T071; D4): lo que dice <c>Pagos.CuotaEnviadaAlCancelar</c> para la línea del crédito (<c>Incluir</c>, <c>Excluir</c>, <c>Preguntar</c>).</summary>
    public string? SentInstallmentAtPayoff { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T071): lo que cuesta cancelar todo menos la cuota enviada; nulo si no se puede dejar.</summary>
    public decimal? PayoffExceptSentInstallmentAmount { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T071): lo que la cuota enviada queda debiendo a su canal si se deja.</summary>
    public decimal? SentInstallmentLeftAmount { get; init; }
}

/// <summary>
/// 2026-11-27 (feature 028, E2, T071; FR-021, FR-023): la cuota del período de un crédito que ya salió a su canal, como la dice el contrato de obligaciones:
/// en qué va (<see cref="State"/>: 1 enviada sin respuesta, 2 descontada pendiente de giro, 3 no descontada, 4 a 6 en la nómina propia), la cuota, la
/// pagaduría, el archivo, el período, los valores de la línea y la frase que lo explica.
/// </summary>
public sealed record CuotaEnviadaDeLaCajaDto(int State, int? InstallmentNumber, DateOnly? DueDate, string? Paymaster, string? Agreement, string? File,
    string? Period, decimal? CurrentAmount, decimal? PastDueAmount, decimal? TotalAmount, Guid? PayrollRunPublicId, string Explanation)
{
    /// <summary>Va en la nómina propia (novedad generada, corrida calculada o aprobada); si no, en un archivo de la pagaduría.</summary>
    public bool EnNominaPropia => State is 4 or 5 or 6;
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

    /// <summary>
    /// 2026-11-26 (pedido del dueño): lo que de esta cuota por vencer ya es exigible (lo causado del período calendario en curso). Si el recibo paga antes lo
    /// exigible, anticiparla cuesta <see cref="ValueToAnticipate"/> menos esto.
    /// </summary>
    public decimal DueInPeriod { get; init; }

    /// <summary>2026-11-26 (pedido del dueño): es la cuota del período en curso (su período ya empezó: no lleva descuento por pago anticipado).</summary>
    public bool InCurrentPeriod { get; init; }

    /// <summary>
    /// 2026-11-26 (pedido del dueño): la cuota admite el descuento por pago anticipado si se anticipa completa: por vencer, de un período que no ha empezado y
    /// sin anticipo previo (lo mismo que exige el servidor).
    /// </summary>
    public bool AdmiteDescuentoPorAnticipo => Status == "Upcoming" && !InCurrentPeriod && AdvanceApplied == 0m && ValueToAnticipate > 0m;
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

    /// <summary>2026-11-26 (pedido del dueño): lo exigible sin el interés ya causado del período calendario en curso; nulo se lee como <see cref="DueToday"/>.</summary>
    public decimal? DueWithoutPeriodInProgress { get; init; }

    /// <summary>2026-11-26: lo vencido (o que vence hoy): la mora para las reglas de la caja.</summary>
    public decimal Vencido => DueWithoutPeriodInProgress ?? DueToday;

    /// <summary>2026-11-26 (pedido del dueño): la fracción del interés de una cuota anticipada que la cooperativa descuenta en caja; cero sin descuento.</summary>
    public decimal AdvanceDiscountFraction { get; init; }
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
    IReadOnlyList<string>? PriorityClasses = null, bool OverdueOnlyDue = true, bool MinimumOneInstallment = true, bool PreselectDueOnSelect = false,
    IReadOnlyList<AnticipoDisponibleDto>? Advances = null, bool OverdueAllowsAdvance = false);

public sealed record ItemPedidoRequest(int Target, decimal Amount, Guid? ProductPublicId = null, Guid? BeneficiaryPersonPublicId = null, int? LoanModality = null,
    int? AccountModality = null, int? Choice = null, int? InstallmentNumber = null, IReadOnlyList<Guid>? InstallmentPublicIds = null,
    bool AdvanceNoticeAccepted = false, Guid? MiscConceptPublicId = null, string? CrossDocument = null, Guid? InvoicePublicId = null, Guid? ItemId = null,
    int? PendingTarget = null, Guid? LoadObligationPublicId = null, string? ProductReference = null, bool ApplyAdvanceDiscount = false);

/// <summary>
/// 2026-11-26 (pedido del dueño): las cuotas que el asociado quiere anticipar, elegidas en el detalle de la obligación, y si el cajero aplica el descuento por
/// pago anticipado a las que lo admiten.
/// </summary>
public sealed record AnticipoElegido(IReadOnlyList<CuotaDeObligacionDto> Cuotas, bool ConDescuento);

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

    /// <summary>2026-11-26 (pedido del dueño): el descuento por pago anticipado que asume la cooperativa en este ítem; el asociado paga el valor menos esto.</summary>
    public decimal AdvanceDiscount { get; init; }

    /// <summary>2026-11-26: la fracción del interés con que se calculó el descuento.</summary>
    public decimal? AdvanceDiscountFraction { get; init; }
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
    int? Choice, bool AdvanceNoticeAccepted, string? MiscConceptCode, string? ModuleVoucherNumber, DateOnly? FinalityUntil, int Status)
{
    /// <summary>2026-11-26 (pedido del dueño): el descuento por pago anticipado que asumió la cooperativa; nulo sin descuento.</summary>
    public decimal? AdvanceDiscount { get; init; }
}

public sealed record MedioDelReciboDto(Guid PublicId, string MeansCode, decimal Amount, string? Reference, int Status);

public sealed record ReciboDto(Guid PublicId, string Number, int Status, int Channel, DateOnly ValueDate, DateTime RegisteredAt, Guid PayerPersonPublicId,
    string PayerName, Guid? DeliveredByPersonPublicId, bool PayerIsThirdParty, bool Masked, decimal TotalApplied, decimal TotalMeans, decimal ChangeGiven,
    string VerificationCode, IReadOnlyList<AplicacionDelReciboDto> Applications, IReadOnlyList<MedioDelReciboDto> Means)
{
    /// <summary>2026-11-26 (pedido del dueño): el descuento por pago anticipado del recibo; lo pagado es lo aplicado menos esto.</summary>
    public decimal TotalDiscount => Applications.Sum(a => a.AdvanceDiscount ?? 0m);
}

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

    /// <remarks>2026-11-24 (pedido del dueño): <paramref name="pagador"/> filtra por la persona que pagó (la del recibo).</remarks>
    public Task<ResultadoDeInventario<IReadOnlyList<ReciboDto>>> RecibosAsync(DateOnly? desde, DateOnly? hasta, string? numero, CancellationToken ct = default,
        Guid? pagador = null) =>
        EnvioDeVentas.EnviarAsync<IReadOnlyList<ReciboDto>>(http, auth, HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/receipts",
            EnvioDeVentas.Query(("from", EnvioDeVentas.Texto(desde)), ("to", EnvioDeVentas.Texto(hasta)), ("number", numero),
                ("person", EnvioDeVentas.Texto(pagador)))), null, null, ct);

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

    /// <summary>2026-11-24: la tirilla del recibo como imagen PNG de 80 mm, para imprimirla a tamaño real desde el navegador.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> TirillaComoImagenAsync(Guid recibo, bool copia, CancellationToken ct = default) =>
        EnvioDeVentas.DescargarAsync(http, auth, $"{Base}/receipts/{recibo}/document?format=thermal-image&copy={(copia ? "true" : "false")}",
            "recibo.png", ct);

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
        8 => "Todo menos la cuota enviada",
        _ => "—",
    };

    /// <summary>
    /// 2026-11-24 (pedido del dueño): cómo funciona cada modalidad y cuándo usarla, para el tooltip de la lista y la ayuda F1 de la Caja (antes sólo se veía
    /// el nombre y, al simular, códigos como «Lending.Payment.AdvanceDoesNotReduceInterest»).
    /// </summary>
    public static string AyudaDeModalidad(int? modalidad) => modalidad switch
    {
        1 => "Cancelación total: paga todo lo que debe el crédito a hoy —capital, intereses, seguros y cargos— y lo deja cancelado. Usa primero el anticipo "
             + "que tenga. Úsela cuando el asociado quiere saldar el crédito: el valor se propone solo; lo que sobre se devuelve.",
        2 => "Lo vencido: paga sólo las cuotas ya vencidas, con su mora. Úsela para poner al día un crédito atrasado; lo que pase de lo vencido no se aplica.",
        3 => "Cuota del ciclo: paga lo vencido y la cuota del período. Si esa cuota todavía no vence, lo que le toca queda como anticipo de esa cuota (no "
             + "reduce intereses). Es la de todos los meses.",
        4 => "Anticipo: paga lo vencido y el resto queda guardado como anticipo; cada cuota se paga sola con él cuando vence. No reduce intereses ni "
             + "abona a capital antes de tiempo. Úsela cuando el asociado deja plata para las próximas cuotas.",
        5 => "Abono extraordinario a capital: paga lo vencido y el resto baja el capital; el deudor elige si se reduce el plazo o la cuota. Sí reduce "
             + "intereses. Si alcanza todo el capital, se aplica como cancelación.",
        6 => "Cuota específica: paga sólo la cuota que indique (el número), si las anteriores están pagas o la línea lo permite. Si todavía no vence, "
             + "queda como anticipo de esa cuota.",
        7 => "Valor libre: paga lo vencido por la prelación y el resto queda como anticipo general (como «Anticipo»). Úsela cuando el asociado trae un "
             + "valor que no corresponde a una cuota.",
        8 => "Todo menos la cuota enviada: cancela todo lo que debe el crédito salvo la cuota del período que ya va en el archivo de la pagaduría o en la "
             + "nómina; esa cuota queda en el crédito tal como se envió y la paga su canal. Úsela para no cobrarle al asociado una cuota que ya le van a descontar.",
        _ => "Cómo se aplica el pago al crédito: elija la modalidad y pase el cursor sobre ella para ver cómo funciona.",
    };
}
