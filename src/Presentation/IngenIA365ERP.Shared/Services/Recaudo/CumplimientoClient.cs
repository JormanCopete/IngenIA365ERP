using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Recaudo;

/// <summary>Una transacción en efectivo del registro. Clase: 1 recibo, 2 desembolso en efectivo, 3 anulación. (nuevo)</summary>
public sealed record TransaccionDeEfectivoDto(Guid DocumentPublicId, string DocumentNumber, int DocumentKind, Guid PersonPublicId, string PersonName, Guid PayerPersonPublicId,
    decimal CashAmount, decimal OtherMeansAmount, DateOnly OccurredOn, int Month, IReadOnlyList<string> Alerts);

/// <summary>Una señal. Señal: 1 acumulado, 2 fraccionamiento, 3 pagador tercero frecuente, 4 consignante repetido, 5 lista vinculante, 6 lista no vinculante. (nuevo)</summary>
public sealed record SenalDeCumplimientoDto(Guid PublicId, Guid? PersonPublicId, string? PersonName, string? DepositorKey, int Signal, DateOnly WindowFrom, DateOnly WindowTo,
    int Count, decimal Total, string? ParameterVersion, Guid? AlertPublicId, DateTime EvaluatedAt);

public sealed record OperacionRetenidaDto(Guid OperationPublicId, Guid SessionPublicId, Guid PayerPersonPublicId, string PayerName, decimal CashAmount, DateTime WithheldAt);

public sealed record LiberarRetenidaRequest(Guid SupportAttachmentPublicId, string Reason);

public sealed record DevolverRetenidaRequest(Guid SupportAttachmentPublicId, Guid? PersonBankAccountPublicId, string Form);

public sealed record ReciboDeContingenciaRequest(Guid ContingencyBookPublicId, int ContingencyNumber, Guid PayerPersonPublicId, DateOnly ValueDate,
    IReadOnlyList<ParteDeAbonoRequest> Parts, Guid ImageAttachmentPublicId, int? MeansClass, Guid? DeliveredByPersonPublicId);

public sealed record EstadoDeTalonarioDto(Guid PublicId, Guid CollectionPointPublicId, int FromNumber, int ToNumber, IReadOnlyList<int> Typed, IReadOnlyList<int> Pending,
    int Unused, int Status);

/// <summary>Cliente tipado del cumplimiento en la caja y de la contingencia (feature 015, US14 y US2, T0245). Las escrituras llevan <see cref="ClaveDeOperacion"/>. (nuevo)</summary>
public sealed class CumplimientoClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/receipts";

    public Task<ResultadoDeInventario<IReadOnlyList<TransaccionDeEfectivoDto>>> TransaccionesAsync(Guid? persona = null, int? mes = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<TransaccionDeEfectivoDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/compliance/cash-transactions",
            EnvioDeVentas.Query(("person", EnvioDeVentas.Texto(persona)), ("month", EnvioDeVentas.Texto(mes)))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<SenalDeCumplimientoDto>>> SenalesAsync(Guid? persona = null, DateOnly? desde = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<SenalDeCumplimientoDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/compliance/signals",
            EnvioDeVentas.Query(("person", EnvioDeVentas.Texto(persona)), ("from", EnvioDeVentas.Texto(desde)))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<OperacionRetenidaDto>>> RetenidasAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<OperacionRetenidaDto>>(HttpMethod.Get, $"{Base}/compliance/withheld", null, null, ct);

    public Task<ResultadoDeInventario<OperacionDeCajaDto>> RetenerAsync(Guid operacion, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<OperacionDeCajaDto>(HttpMethod.Post, $"{Base}/cash-operations/{operacion}/withhold", null, clave, ct);

    public Task<ResultadoDeInventario<OperacionDeCajaDto>> LiberarAsync(Guid operacion, LiberarRetenidaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<OperacionDeCajaDto>(HttpMethod.Post, $"{Base}/cash-operations/{operacion}/release", r, clave, ct);

    public Task<ResultadoDeInventario<OperacionDeCajaDto>> DevolverAsync(Guid operacion, DevolverRetenidaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<OperacionDeCajaDto>(HttpMethod.Post, $"{Base}/cash-operations/{operacion}/refund-by-order", r, clave, ct);

    public Task<ResultadoDeInventario<ReciboDto>> ContingenciaAsync(ReciboDeContingenciaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ReciboDto>(HttpMethod.Post, $"{Base}/contingency-receipts", r, clave, ct);

    public Task<ResultadoDeInventario<EstadoDeTalonarioDto>> TalonarioAsync(Guid talonario, CancellationToken ct = default) =>
        Enviar<EstadoDeTalonarioDto>(HttpMethod.Get, $"{Base}/contingency-books/{talonario}/status", null, null, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeRecaudo.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, null, ct);
}
