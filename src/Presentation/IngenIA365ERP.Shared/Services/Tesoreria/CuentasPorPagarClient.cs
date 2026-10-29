using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

/// <summary>Una cuenta por pagar: «SupplierInvoice» (orden de una factura de Comercial) o «AccountingPayable» (cuenta por pagar contable). (nuevo)</summary>
public sealed record CuentaPorPagarDto(string Source, Guid? PaymentOrderPublicId, string? OrderNumber, Guid? DocumentPublicId, string Document, Guid PersonPublicId, string PersonName,
    DateOnly DueDate, decimal Pending, decimal InOrders, int? OrderStatus);

public sealed record DiaDeCalendarioDto(DateOnly Date, int Orders, decimal Total, IReadOnlyList<string> OrderNumbers);

public sealed record ConversionDeCuentaPorPagarRequest(Guid PersonPublicId, string CrossDocument, decimal Amount, DateOnly? PayDate, string? Concept);

/// <summary>Cliente tipado de las cuentas por pagar de T2 (feature 015, fase 42; api.md §13). (nuevo)</summary>
public sealed class CuentasPorPagarClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/treasury/payables";

    public Task<ResultadoDeInventario<IReadOnlyList<CuentaPorPagarDto>>> CuentasAsync(Guid? persona, DateOnly? hasta, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<IReadOnlyList<CuentaPorPagarDto>>(http, auth, HttpMethod.Get, EnvioDeVentas.ConQuery(Base,
            EnvioDeVentas.Query(("person", EnvioDeVentas.Texto(persona)), ("dueUntil", EnvioDeVentas.Texto(hasta)))), null, null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<DiaDeCalendarioDto>>> CalendarioAsync(DateOnly? desde, DateOnly? hasta, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<IReadOnlyList<DiaDeCalendarioDto>>(http, auth, HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/calendar",
            EnvioDeVentas.Query(("from", EnvioDeVentas.Texto(desde)), ("to", EnvioDeVentas.Texto(hasta)))), null, null, null, ct);

    public Task<ResultadoDeInventario<CuentaPorPagarDto>> ConvertirAsync(ConversionDeCuentaPorPagarRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<CuentaPorPagarDto>(http, auth, HttpMethod.Post, $"{Base}/orders", r, clave, null, ct);
}
