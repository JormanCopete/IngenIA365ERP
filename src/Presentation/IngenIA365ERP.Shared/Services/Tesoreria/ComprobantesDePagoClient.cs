using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

/// <summary>Un comprobante de pago. Forma: 1 transferencia, 2 cheque, 3 pago a tercero. (nuevo)</summary>
public sealed record ComprobanteDePagoDto(Guid PublicId, string Number, Guid PaymentOrderPublicId, string OrderNumber, string OrderOrigin, Guid BeneficiaryPersonPublicId,
    string BeneficiaryName, int Form, decimal GrossAmount, decimal WithholdingAmount, decimal NetAmount, DateOnly PaidOn, string TreasuryReference, string? CheckNumber,
    string? BankAccountCode, string VerificationCode, DateTime? SentAt, string? SentTo, int SendCount);

/// <summary>El resultado del envío: «Sent», «NotAuthorized», «NoDestination» o «Simulated». (nuevo)</summary>
public sealed record EnvioDeComprobanteDto(string Outcome, string Channel, string? Destination, string Message);

public sealed record EnviarComprobanteRequest(int Channel, string? Email);

/// <summary>Cliente tipado del comprobante de pago (feature 015, T1, fase 35, T0421; api.md §13): la lista, el PDF y el envío al beneficiario. (nuevo)</summary>
public sealed class ComprobantesDePagoClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/treasury/payment-vouchers";

    public Task<ResultadoDeInventario<IReadOnlyList<ComprobanteDePagoDto>>> ListarAsync(DateOnly? desde = null, DateOnly? hasta = null, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<IReadOnlyList<ComprobanteDePagoDto>>(http, auth, HttpMethod.Get, EnvioDeVentas.ConQuery(Base,
            EnvioDeVentas.Query(("from", desde?.ToString("yyyy-MM-dd")), ("to", hasta?.ToString("yyyy-MM-dd")))), null, null, null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DocumentoAsync(Guid comprobante, CancellationToken ct = default) =>
        EnvioDeVentas.DescargarAsync(http, auth, $"{Base}/{comprobante}/document", "comprobante-de-pago.pdf", ct);

    public Task<ResultadoDeInventario<EnvioDeComprobanteDto>> EnviarAsync(Guid comprobante, string? correo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<EnvioDeComprobanteDto>(http, auth, HttpMethod.Post, $"{Base}/{comprobante}/send", new EnviarComprobanteRequest(1, correo), clave, null, ct);
}
