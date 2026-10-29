using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.Results;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// El comprobante de pago numerado por pago (feature 015, T1, fase 35, T0421; contracts/api.md §13; FR-117): la lista del rango (<c>GET /</c>, nuevo), su documento en PDF
/// con código de verificación y su envío al beneficiario por correo con autorización. (nuevo)
/// </summary>
public class PaymentVouchersEndpoints : ICarterModule
{
    public const string Ver = "Treasury.Orders.View";
    public const string Enviar = "Treasury.Results.Register";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TreasuryGroup.Grupo(app, "/payment-vouchers", "Tesorería Comprobantes de pago");
        g.MapGet("/", async (DateOnly? from, DateOnly? to, ISender s, CancellationToken ct) => await s.Send(new ListPaymentVouchersQuery(from, to), ct))
            .WithName("Treasury_PaymentVouchers_List").RequirePermission(new AlgunPermiso(Ver, Enviar));
        g.MapGet("/{id:guid}/document", async (Guid id, ISender s, IComprobanteDePagoEnPdf pdf, CancellationToken ct) =>
            {
                var r = await s.Send(new GetPaymentVoucherDocumentQuery(id), ct);
                return r.IsSuccess ? Results.File(pdf.ComprobanteDePago(r.Value), "application/pdf", $"comprobante-{r.Value.Number}.pdf") : (object)r;
            })
            .WithName("Treasury_PaymentVouchers_Document").RequirePermission(new AlgunPermiso(Ver, Enviar));
        g.MapPost("/{id:guid}/send", async (Guid id, EnvioRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SendPaymentVoucherCommand(id, body?.Channel ?? MessageChannel.Email, body?.Email) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentVouchers_Send").ConClaveDeOperacion().RequirePermission(Enviar);
    }

    public sealed record EnvioRequest(MessageChannel? Channel, string? Email);
}
