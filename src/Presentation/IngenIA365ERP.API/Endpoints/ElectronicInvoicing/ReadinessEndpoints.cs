using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.ElectronicInvoicing.Settings;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.ElectronicInvoicing;

/// <summary>
/// La preparación para emitir (feature 012, I4, T744; contracts/api.md §24.3): <c>GET /api/electronic-invoicing/readiness?asOf=&amp;documentType=&amp;cashRegister=</c>
/// responde el veredicto (<c>Electronic | NonElectronic | Blocked</c>), el canal vigente, las resoluciones y lo que falta con quién lo corrige
/// (<c>missing[] { code, message, whoFixes { page, permission } }</c>). Es la misma decisión de <c>GuardiaDeEmisionFiscal</c> que usan la
/// confirmación y la apertura de caja. Con <c>ElectronicInvoicing.Settings.View</c>. (nuevo)
/// </summary>
public class ReadinessEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/electronic-invoicing/readiness", async (DateOnly? asOf, Guid? documentType, Guid? cashRegister, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDianReadinessQuery(asOf, documentType, cashRegister), ct))
            .WithTags("Electronic Invoicing Settings")
            .WithName("ElectronicInvoicing_Readiness")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(SettingsEndpoints.Ver);
    }
}
