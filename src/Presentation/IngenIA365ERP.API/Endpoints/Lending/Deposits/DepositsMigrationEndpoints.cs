using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// Migración y activación de aportes y captaciones (feature 014, T055; contracts/api.md §16 de la 014), sobre <see cref="LendingGroup"/>. En la fase
/// fundacional sólo la activación <b>sin carga</b> (FR-117): <c>POST /api/lending/deposits/activation</c> con <c>nature</c> (<c>Deposit</c> o
/// <c>Contribution</c>), <c>Lending.Deposits.Activate</c> e <c>Idempotency-Key</c>; la migración y el estado de la activación llegan con US9. Cada ruta
/// sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class DepositsMigrationEndpoints : ICarterModule
{
    public const string PermisoDeActivacion = "Lending.Deposits.Activate";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var captaciones = LendingGroup.Grupo(app, "/deposits", "Cartera Captaciones: migración y activación");

        captaciones.MapPost("/activation", ActivarAsync)
            .WithName("Lending_Deposits_Activate")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeActivacion);
    }

    /// <summary>Reenvía al comando de la naturaleza: captaciones (<c>Deposit</c>) o aportes (<c>Contribution</c>).</summary>
    private static async Task<object> ActivarAsync(ActivarRequest body, HttpContext http, ISender sender, CancellationToken ct)
    {
        Result<DepositsActivationDto> r = body.Nature switch
        {
            LineNature.Deposit => await sender.Send(new ActivateDepositsCommand(body.ActivationDate, body.Reason ?? string.Empty,
                body.MigrationLoadPublicId, body.SessionItemPublicId, body.AcknowledgeWarnings) { OperationKey = http.ClaveDeOperacion() }, ct),
            LineNature.Contribution => await sender.Send(new ActivateContributionsCommand(body.ActivationDate, body.Reason ?? string.Empty,
                body.MigrationLoadPublicId, body.SessionItemPublicId, body.AcknowledgeWarnings) { OperationKey = http.ClaveDeOperacion() }, ct),
            _ => Result.Failure<DepositsActivationDto>(new ErrorConDatos("Validation.Invalid",
                "Indique qué naturaleza activa: Deposit (captaciones) o Contribution (aportes).", new { field = "nature" })),
        };
        return r.IsSuccess ? Results.Created("/api/lending/deposits/activation", r.Value) : r;
    }

    /// <summary>El cuerpo de la activación (api.md §16): <c>{ nature, migrationLoadPublicId?, activationDate, sessionItemPublicId?, acknowledgeWarnings, reason }</c>.</summary>
    public sealed record ActivarRequest(
        LineNature Nature,
        Guid? MigrationLoadPublicId,
        DateOnly ActivationDate,
        Guid? SessionItemPublicId,
        bool AcknowledgeWarnings,
        string? Reason);
}
