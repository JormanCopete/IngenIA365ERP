using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.ElectronicInvoicing.Settings;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.ElectronicInvoicing;

/// <summary>
/// La configuración de emisión electrónica (feature 012, I4, T744; contracts/api.md §24.1; contracts/dian.md §10): la vigencia actual y su
/// historial con los canales registrados y el estado de la credencial (<c>GET /settings</c>), una vigencia nueva que cierra la anterior la
/// víspera (<c>POST /settings</c> → 201) y la verificación de la credencial del canal (<c>POST /settings/verify-credential</c>), que sólo
/// sella <c>CredentialVerifiedAt</c>. <c>ElectronicInvoicing.Settings.View|Manage</c>; toda escritura con <c>Idempotency-Key</c>. La
/// credencial nunca viaja: la clave del Secret se deriva de la cooperativa y el canal. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class SettingsEndpoints : ICarterModule
{
    public const string Ruta = "/api/electronic-invoicing/settings";
    public const string Ver = "ElectronicInvoicing.Settings.View";
    public const string Administrar = "ElectronicInvoicing.Settings.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Ruta)
            .WithTags("Electronic Invoicing Settings")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
                await sender.Send(new GetEmissionSettingsQuery(), ct))
            .WithName("ElectronicInvoicing_Settings_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        group.MapPost("/", async (ConfigureEmissionCommand body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess ? (object)Results.Created(Ruta, result.Value) : result;
            })
            .WithName("ElectronicInvoicing_Settings_Configure")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        group.MapPost("/verify-credential", async (VerificarCredencialRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new VerifyChannelCredentialCommand(body?.ChannelCode) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("ElectronicInvoicing_Settings_VerifyCredential")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary><c>POST /settings/verify-credential</c>: el canal a verificar (nulo = el de la vigencia actual).</summary>
    public sealed record VerificarCredencialRequest(string? ChannelCode);
}
