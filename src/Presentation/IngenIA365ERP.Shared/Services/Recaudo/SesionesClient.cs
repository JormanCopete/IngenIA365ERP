using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Recaudo;

// Feature 015, US2 (T0098; contracts/api.md §3): lo que las pantallas de la caja leen y mandan. Los enums viajan como número (la API los devuelve así). (nuevo)

public sealed record DenominacionContadaRequest(Guid DenominationPublicId, int Quantity);

public sealed record MedioDeSesionDto(Guid PaymentMeansPublicId, string Code, string Name, int Class, int CountMethod, decimal Tolerance);

public sealed record SesionDeCajaDto(Guid PublicId, Guid CashBoxPublicId, string CashBoxCode, Guid CollectionPointPublicId, string CollectionPointCode,
    Guid CashierUserPublicId, string CashierName, DateOnly BusinessDate, int Status, DateTime OpenedAt, DateTime? ClosedAt, decimal OpeningBase,
    bool IsForgotten, bool BlindCount, bool DoubleCustody, decimal? CashWarnCap, decimal? CashBlockCap, IReadOnlyList<MedioDeSesionDto> Means);

public sealed record EsperadoDeMedioDto(Guid PaymentMeansPublicId, string Code, decimal Base, decimal Receipts, decimal Voided, decimal In, decimal Out, decimal Expected);

public sealed record EsperadoDeSesionDto(Guid SessionPublicId, bool Hidden, IReadOnlyList<EsperadoDeMedioDto> Means, decimal? Total, bool CashWarnCapReached);

public sealed record MovimientoDeCajaDto(Guid PublicId, string Number, int Kind, decimal Amount, Guid? FromMeansPublicId, Guid? ToMeansPublicId,
    Guid? CounterpartSessionPublicId, Guid? VaultPublicId, string Reason, DateTime MovedAt);

public sealed record LineaDeArqueoDto(Guid PublicId, Guid PaymentMeansPublicId, string MeansCode, int Method, decimal Expected, decimal Counted, decimal Difference,
    decimal Tolerance, bool WithinTolerance, int Treatment, string? Reason, bool PendingApproval, DateTime? ApprovedAt);

public sealed record ArqueoDto(Guid PublicId, int Kind, DateTime CountedAt, decimal TotalExpected, decimal TotalCounted, decimal TotalDifference,
    decimal AmountToApprove, int? SessionStatus, IReadOnlyList<LineaDeArqueoDto> Lines);

public sealed record LoteContadoRequest(Guid CardTerminalPublicId, string BatchNumber, decimal Amount);

public sealed record ReferenciaContadaRequest(string Reference, decimal Amount, bool Confirmed);

public sealed record ConteoDeMedioRequest(Guid PaymentMeansPublicId, decimal? Total = null, IReadOnlyList<DenominacionContadaRequest>? Denominations = null,
    IReadOnlyList<LoteContadoRequest>? TerminalBatches = null, IReadOnlyList<ReferenciaContadaRequest>? References = null, string? Reason = null);

public sealed record AbrirSesionRequest(Guid CashBoxPublicId, IReadOnlyList<DenominacionContadaRequest> OpeningBase);

public sealed record MovimientoRequest(int Kind, IReadOnlyList<DenominacionContadaRequest> Denominations, string Reason, Guid? CounterpartSessionPublicId,
    Guid? SecondCustodianUserPublicId, string? BagSeal);

public sealed record ReclasificacionRequest(Guid FromMeansPublicId, Guid ToMeansPublicId, decimal Amount, string Reason, Guid? SupportAttachmentPublicId);

public sealed record ConteoRequest(IReadOnlyList<ConteoDeMedioRequest> Means);

public sealed record AprobarDiferenciaRequest(string Reason, int? Treatment);

public sealed record CerrarOlvidadaRequest(Guid WitnessUserPublicId, IReadOnlyList<ConteoDeMedioRequest> Means);

public sealed record AjusteRequest(int Kind, Guid PaymentMeansPublicId, decimal Amount, string Reason, Guid? SupportAttachmentPublicId);

public sealed record SorpresivoRequest(Guid SessionPublicId, Guid MinutesAttachmentPublicId, IReadOnlyList<ConteoDeMedioRequest> Means, Guid? WitnessUserPublicId);

public sealed record AjusteDeSesionDto(Guid PublicId, string Number, int Kind, Guid PaymentMeansPublicId, decimal Amount, string Reason, DateTime AdjustedAt);

public sealed record LineaDelCierreDelDiaDto(Guid PaymentMeansPublicId, string MeansCode, decimal Expected, decimal Counted, decimal Difference);

public sealed record CierreDelDiaDto(Guid PublicId, Guid CollectionPointPublicId, DateOnly BusinessDate, int SessionCount, int ReceiptCount, int VoidingCount,
    decimal Expected, decimal Counted, decimal Difference, DateTime ClosedAt, string VerificationCode, bool ByDelegate, IReadOnlyList<LineaDelCierreDelDiaDto> Lines);

public sealed record MovimientoDeBovedaDto(Guid PublicId, string Number, int Kind, decimal Amount, string? BagSeal, string? Reason, DateTime MovedAt);

public sealed record SaldoDeBovedaDto(Guid DenominationPublicId, decimal Value, int Quantity, decimal Amount);

public sealed record DetalleDeBovedaDto(Guid PublicId, Guid BranchPublicId, string BranchName, int Mode, int Status, decimal Balance,
    IReadOnlyList<SaldoDeBovedaDto> Denominations, IReadOnlyList<MovimientoDeBovedaDto> Movements);

public sealed record MovimientoDeBovedaRequest(int Kind, IReadOnlyList<DenominacionContadaRequest> Denominations, string Reason, Guid? SecondCustodianUserPublicId,
    string? BagSeal, Guid? MinutesAttachmentPublicId, Guid? RemittancePublicId);

public sealed record CuadreDiarioDto(Guid PublicId, Guid BranchPublicId, string BranchName, DateOnly BusinessDate, decimal CashInBoxes, decimal CashInVault,
    decimal CashInTransit, decimal? LedgerBalance, decimal EmittedPendingBatch, decimal Difference, int Status, bool VaultBalanceMatches, string? Explanation);

public sealed record FaltanteDeCajeroDto(Guid PublicId, Guid CashierPersonPublicId, decimal Amount, decimal Balance, bool HasPayrollAuthorization, int Status);

public sealed record SobranteDto(Guid PublicId, decimal Amount, DateOnly? ToIncomeFrom, int Status);

public sealed record DenominacionDeCajaDto(Guid CashDenominationPublicId, string Currency, int Kind, decimal Value, short DisplayOrder, bool IsActive, DateOnly ValidFrom, DateOnly? ValidTo);

/// <summary>
/// Cliente tipado de la sesión de caja de Recaudo (feature 015, US2, T0098; contracts/api.md §3): sesión, lo esperado, movimientos, reclasificación, lo contado,
/// diferencias, sesión olvidada, ajustes, arqueo sorpresivo, cierre del día, bóveda, faltantes, sobrantes, cuadre diario y documentos. Sin
/// <c>Authorization</c> a mano; las escrituras llevan <see cref="ClaveDeOperacion"/> y las que piden aprobador presente se repiten con
/// <c>X-Approval-Request</c>. (nuevo)
/// </summary>
public sealed class SesionesClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/receipts";
    public const string CabeceraDeAprobacion = "X-Approval-Request";

    public Task<ResultadoDeInventario<SesionDeCajaDto?>> ActualAsync(CancellationToken ct = default) => Get<SesionDeCajaDto?>($"{Base}/sessions/current", ct);

    public Task<ResultadoDeInventario<IReadOnlyList<SesionDeCajaDto>>> SesionesAsync(int? estado = null, Guid? punto = null, DateOnly? dia = null,
        CancellationToken ct = default) =>
        Get<IReadOnlyList<SesionDeCajaDto>>(EnvioDeVentas.ConQuery($"{Base}/sessions",
            EnvioDeVentas.Query(("state", EnvioDeVentas.Texto(estado)), ("point", EnvioDeVentas.Texto(punto)), ("date", EnvioDeVentas.Texto(dia)))), ct);

    public Task<ResultadoDeInventario<SesionDeCajaDto>> AbrirAsync(AbrirSesionRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<SesionDeCajaDto>(HttpMethod.Post, $"{Base}/sessions", r, clave, null, ct);

    public Task<ResultadoDeInventario<EsperadoDeSesionDto>> EsperadoAsync(Guid sesion, CancellationToken ct = default) =>
        Get<EsperadoDeSesionDto>($"{Base}/sessions/{sesion}/expected", ct);

    public Task<ResultadoDeInventario<MovimientoDeCajaDto>> MoverAsync(Guid sesion, MovimientoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<MovimientoDeCajaDto>(HttpMethod.Post, $"{Base}/sessions/{sesion}/movements", r, clave, null, ct);

    public Task<ResultadoDeInventario<MovimientoDeCajaDto>> ReclasificarAsync(Guid sesion, ReclasificacionRequest r, ClaveDeOperacion clave, Guid? aprobacion,
        CancellationToken ct = default) =>
        Enviar<MovimientoDeCajaDto>(HttpMethod.Post, $"{Base}/sessions/{sesion}/reclassifications", r, clave, aprobacion, ct);

    public Task<ResultadoDeInventario<ArqueoDto>> ContarAsync(Guid sesion, ConteoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ArqueoDto>(HttpMethod.Post, $"{Base}/sessions/{sesion}/count", r, clave, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ArqueoDto>>> ArqueosAsync(Guid sesion, CancellationToken ct = default) =>
        Get<IReadOnlyList<ArqueoDto>>($"{Base}/sessions/{sesion}/counts", ct);

    public Task<ResultadoDeInventario<ArqueoDto>> AprobarDiferenciaAsync(Guid sesion, Guid linea, AprobarDiferenciaRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ArqueoDto>(HttpMethod.Post, $"{Base}/sessions/{sesion}/differences/{linea}/approve", r, clave, null, ct);

    public Task<ResultadoDeInventario<ArqueoDto>> CerrarOlvidadaAsync(Guid sesion, CerrarOlvidadaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ArqueoDto>(HttpMethod.Post, $"{Base}/sessions/{sesion}/close-forgotten", r, clave, null, ct);

    public Task<ResultadoDeInventario<AjusteDeSesionDto>> AjustarAsync(Guid sesion, AjusteRequest r, ClaveDeOperacion clave, Guid? aprobacion,
        CancellationToken ct = default) =>
        Enviar<AjusteDeSesionDto>(HttpMethod.Post, $"{Base}/sessions/{sesion}/adjustments", r, clave, aprobacion, ct);

    public Task<ResultadoDeInventario<ArqueoDto>> ArqueoSorpresivoAsync(SorpresivoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ArqueoDto>(HttpMethod.Post, $"{Base}/surprise-counts", r, clave, null, ct);

    public Task<ResultadoDeInventario<CierreDelDiaDto>> CerrarDiaAsync(Guid punto, DateOnly dia, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CierreDelDiaDto>(HttpMethod.Post, $"{Base}/collection-points/{punto}/days/{dia:yyyy-MM-dd}/close", null, clave, null, ct);

    public Task<ResultadoDeInventario<DetalleDeBovedaDto>> BovedaAsync(Guid boveda, CancellationToken ct = default) =>
        Get<DetalleDeBovedaDto>($"{Base}/vaults/{boveda}", ct);

    public Task<ResultadoDeInventario<MovimientoDeBovedaDto>> MoverBovedaAsync(Guid boveda, MovimientoDeBovedaRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<MovimientoDeBovedaDto>(HttpMethod.Post, $"{Base}/vaults/{boveda}/movements", r, clave, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CuadreDiarioDto>>> CuadresAsync(Guid? oficina = null, DateOnly? dia = null, CancellationToken ct = default) =>
        Get<IReadOnlyList<CuadreDiarioDto>>(EnvioDeVentas.ConQuery($"{Base}/daily-squares",
            EnvioDeVentas.Query(("branch", EnvioDeVentas.Texto(oficina)), ("date", EnvioDeVentas.Texto(dia)))), ct);

    public Task<ResultadoDeInventario<CuadreDiarioDto>> ExplicarCuadreAsync(Guid cuadre, string explicacion, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CuadreDiarioDto>(HttpMethod.Post, $"{Base}/daily-squares/{cuadre}/explain", new { explanation = explicacion }, clave, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<FaltanteDeCajeroDto>>> FaltantesAsync(CancellationToken ct = default) =>
        Get<IReadOnlyList<FaltanteDeCajeroDto>>($"{Base}/cashier-receivables", ct);

    public Task<ResultadoDeInventario<IReadOnlyList<SobranteDto>>> SobrantesAsync(CancellationToken ct = default) =>
        Get<IReadOnlyList<SobranteDto>>($"{Base}/cash-surpluses", ct);

    public Task<ResultadoDeInventario<SobranteDto>> SobranteAIngresoAsync(Guid sobrante, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<SobranteDto>(HttpMethod.Post, $"{Base}/cash-surpluses/{sobrante}/to-income", null, clave, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<DenominacionDeCajaDto>>> DenominacionesAsync(CancellationToken ct = default) =>
        Get<IReadOnlyList<DenominacionDeCajaDto>>("/api/core/cash-denominations", ct);

    public Task<InvitationApiResult<ArchivoDescargado>> InformeDeCierreAsync(Guid sesion, CancellationToken ct = default) =>
        EnvioDeVentas.DescargarAsync(http, auth, $"{Base}/sessions/{sesion}/close-report", "cierre-de-sesion.pdf", ct);

    public Task<InvitationApiResult<ArchivoDescargado>> InformeDelDiaAsync(Guid punto, DateOnly dia, CancellationToken ct = default) =>
        EnvioDeVentas.DescargarAsync(http, auth, $"{Base}/collection-points/{punto}/days/{dia:yyyy-MM-dd}/report", "cierre-del-dia.pdf", ct);

    /// <summary>El desafío para que el aprobador presente decida en el equipo del cajero (api.md §1.4).</summary>
    public Task<ResultadoDeInventario<DesafioDePresenciaDto>> DesafioDePresenciaAsync(Guid solicitud, string correo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<DesafioDePresenciaDto>(HttpMethod.Post, $"{Base}/approvals/{solicitud}/presence-challenge", new DesafioDePresenciaRequest(correo), clave, null, ct);

    /// <summary>La decisión del aprobador presente con su llave o su código.</summary>
    public Task<ResultadoDeInventario<ResultadoDeDecisionDto>> DecidirEnPersonaAsync(Guid solicitud, DecisionEnPersonaRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResultadoDeDecisionDto>(HttpMethod.Post, $"{Base}/approvals/{solicitud}/decide", r, clave, null, ct);

    /// <summary>El estado de una sesión en palabras.</summary>
    public static string Estado(int estado) => estado switch
    {
        1 => "Abierta",
        2 => "Cerrándose",
        3 => "Cerrada con diferencia por aprobar",
        4 => "Cerrada",
        5 => "Olvidada",
        _ => "—",
    };

    /// <summary>El tratamiento de una diferencia en palabras.</summary>
    public static string Tratamiento(int tratamiento) => tratamiento switch
    {
        1 => "A cargo del cajero",
        2 => "Gasto",
        3 => "Sobrante",
        _ => "—",
    };

    private Task<ResultadoDeInventario<T>> Get<T>(string url, CancellationToken ct) => EnvioDeVentas.EnviarAsync<T>(http, auth, HttpMethod.Get, url, null, null, ct);

    private async Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion clave, Guid? aprobacion,
        CancellationToken ct)
    {
        if (aprobacion is null) return await EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
        if (auth.CurrentAccessToken is null) return ResultadoDeInventario<T>.SinToken();
        try
        {
            using var req = new HttpRequestMessage(metodo, url);
            if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo, cuerpo.GetType());
            clave.Aplicar(req, new { metodo = metodo.Method, url, cuerpo });
            req.Headers.Add(CabeceraDeAprobacion, aprobacion.Value.ToString());
            using var resp = await http.SendAsync(req, ct);
            var resultado = await ResultadoDeInventario<T>.DesdeAsync(resp, ct);
            if (resultado.IsSuccess) clave.Exito();
            return resultado;
        }
        catch (HttpRequestException ex)
        {
            return ResultadoDeInventario<T>.ErrorDeRed(ex.Message);
        }
    }
}
