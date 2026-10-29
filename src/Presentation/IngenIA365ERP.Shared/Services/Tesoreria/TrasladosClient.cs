using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

/// <summary>Un traslado entre cuentas propias. Estado: 1 por aprobar, 2 aprobado, 3 enviado, 4 confirmado, 5 rechazado, 6 anulado. (nuevo)</summary>
public sealed record TrasladoDto(Guid PublicId, string Number, Guid FromBankAccountPublicId, string FromBankAccountCode, Guid ToBankAccountPublicId, string ToBankAccountCode,
    decimal Amount, string Reason, int Status, DateOnly RequestedOn, Guid? ApprovalRequestPublicId, DateOnly? SentOn, string? BankReference, DateOnly? ConfirmedOn,
    Guid? ConfirmedByStatementLinePublicId, string? DecisionReason, int DaysInTransit);

/// <summary>Un cheque que trató el cierre del mes: «Reclassified» o «MovedToPayable». (nuevo)</summary>
public sealed record LineaDelCierreDeChequesDto(Guid IssuedCheckPublicId, long CheckNumber, decimal Amount, string Treatment, DateOnly IssuedOn, DateOnly? DeliveredOn);

/// <summary>El cierre del mes de los cheques de una cuenta. (nuevo)</summary>
public sealed record CierreDeChequesDto(Guid PublicId, string Number, Guid BankAccountPublicId, string BankAccountCode, int Year, int Month, DateOnly CutoffDate,
    int DaysToPayable, int ReclassifiedCount, decimal ReclassifiedAmount, int MovedToPayableCount, decimal MovedToPayableAmount, DateTime RunAt, string RunBy,
    IReadOnlyList<LineaDelCierreDeChequesDto> Lines);

public sealed record PedirTrasladoRequest(Guid FromBankAccountPublicId, Guid ToBankAccountPublicId, decimal Amount, string Reason, Guid? SupportAttachmentPublicId);

public sealed record EnviarTrasladoRequest(DateOnly? SentOn, string? BankReference, Guid? LiquidityFundUsePublicId);

public sealed record ConfirmarTrasladoRequest(Guid? SupportAttachmentPublicId, Guid? StatementLinePublicId, DateOnly? ConfirmedOn);

public sealed record CorrerCierreDeChequesRequest(Guid BankAccountPublicId, int Year, int Month);

/// <summary>
/// Cliente tipado de los traslados entre cuentas propias y del cierre del mes de los cheques (feature 015, T1, fase 33, T0413; api.md §13). Las escrituras llevan
/// <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed class TrasladosClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/treasury";

    public Task<ResultadoDeInventario<IReadOnlyList<TrasladoDto>>> TrasladosAsync(int? estado = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<TrasladoDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/transfers", EnvioDeVentas.Query(("state", EnvioDeVentas.Texto(estado)))), null, null, ct);

    public Task<ResultadoDeInventario<TrasladoDto>> PedirAsync(PedirTrasladoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<TrasladoDto>(HttpMethod.Post, $"{Base}/transfers", r, clave, ct);

    public Task<ResultadoDeInventario<TrasladoDto>> AprobarAsync(Guid traslado, string? motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<TrasladoDto>(HttpMethod.Post, $"{Base}/transfers/{traslado}/approve", new MotivoDeTesoreriaRequest(motivo ?? string.Empty), clave, ct);

    public Task<ResultadoDeInventario<TrasladoDto>> RechazarAsync(Guid traslado, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<TrasladoDto>(HttpMethod.Post, $"{Base}/transfers/{traslado}/reject", new MotivoDeTesoreriaRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<TrasladoDto>> EnviarAsync(Guid traslado, EnviarTrasladoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<TrasladoDto>(HttpMethod.Post, $"{Base}/transfers/{traslado}/sent", r, clave, ct);

    public Task<ResultadoDeInventario<TrasladoDto>> ConfirmarAsync(Guid traslado, ConfirmarTrasladoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<TrasladoDto>(HttpMethod.Post, $"{Base}/transfers/{traslado}/confirm", r, clave, ct);

    public Task<ResultadoDeInventario<TrasladoDto>> AnularAsync(Guid traslado, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<TrasladoDto>(HttpMethod.Post, $"{Base}/transfers/{traslado}/void", new MotivoDeTesoreriaRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CierreDeChequesDto>>> CierresDeChequesAsync(Guid? cuenta = null, int? anio = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CierreDeChequesDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/month-end/checks",
            EnvioDeVentas.Query(("account", EnvioDeVentas.Texto(cuenta)), ("year", EnvioDeVentas.Texto(anio)))), null, null, ct);

    public Task<ResultadoDeInventario<CierreDeChequesDto>> CorrerCierreDeChequesAsync(CorrerCierreDeChequesRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CierreDeChequesDto>(HttpMethod.Post, $"{Base}/month-end/checks", r, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeRecaudo.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, null, ct);
}
