using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.RegulatoryProfile;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// El perfil regulatorio de la cooperativa (feature 013, FR-018, FR-108; T50; contracts/api.md §21.4): consulta a una fecha con
/// <c>Core.RegulatoryProfile.View</c> y registro de versiones (o correcciones) con <c>Core.RegulatoryProfile.Manage</c> e
/// <c>Idempotency-Key</c>. Con <c>recordedAsOf</c> (R) la consulta aplica la regla bitemporal (C83). Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class RegulatoryProfileEndpoints : ICarterModule
{
    public const string PermisoDeConsulta = "Core.RegulatoryProfile.View";
    public const string PermisoDeRegistro = "Core.RegulatoryProfile.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var perfil = app.MapGroup("/api/core/regulatory-profile")
            .WithTags("Core Perfil regulatorio")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        perfil.MapGet("/", async (DateOnly? date, DateTime? recordedAsOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetRegulatoryProfileQuery(date, recordedAsOf), ct))
            .WithName("Core_RegulatoryProfile_Get")
            .RequirePermission(PermisoDeConsulta);

        perfil.MapPost("/versions", async (VersionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new AddRegulatoryProfileVersionCommand(
                    body.OrganizationType ?? string.Empty, body.FinancialActivity, body.SupervisionLevel, body.Category, body.NiifGroup,
                    body.PayrollOperatorRegistration is not null, body.PayrollOperatorRegistration?.Number, body.PayrollOperatorRegistration?.Date,
                    body.ValidFrom, body.Source ?? string.Empty, body.SupportAttachmentPublicId, body.Reason ?? string.Empty, body.SupersedesPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created("/api/core/regulatory-profile", r.Value) : r;
            })
            .WithName("Core_RegulatoryProfile_AddVersion")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeRegistro);
    }

    /// <summary>La inscripción como operador de libranza (§21.4: <c>payrollOperatorRegistration?: { number, date }</c>).</summary>
    public sealed record RegistroDeOperadorRequest(string? Number, DateOnly? Date);

    /// <summary>
    /// El cuerpo del alta (§21.4) más <c>source</c> (columna obligatoria de data-model §4.5 que api.md no lista) y
    /// <c>supersedesPublicId</c> para una corrección.
    /// </summary>
    public sealed record VersionRequest(
        string? OrganizationType,
        bool FinancialActivity,
        byte SupervisionLevel,
        string? Category,
        byte? NiifGroup,
        RegistroDeOperadorRequest? PayrollOperatorRegistration,
        DateOnly ValidFrom,
        string? Source,
        Guid? SupportAttachmentPublicId,
        string? Reason,
        Guid? SupersedesPublicId);
}
