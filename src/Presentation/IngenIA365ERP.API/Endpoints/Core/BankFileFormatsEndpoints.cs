using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Core.BankFiles;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// Feature 010 (N4, D-42): formatos de archivo plano de los bancos, en Core y ligados al banco,
/// para que nómina, tesorería y contabilidad generen sus archivos con el mismo motor. Sólo
/// reenvía al <c>ISender</c>.
/// </summary>
public sealed class BankFileFormatsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/bank-file-formats")
            .WithTags("BankFileFormats")
            .RequireAuthorization();

        group.MapGet("/", async (string? scope, Guid? bankId, bool? onlyActive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListBankFileFormatsQuery(scope, bankId, onlyActive ?? false), ct))
            .WithName("Core_BankFileFormats_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Core.BankFileFormats.View");

        group.MapGet("/sources", async (ISender sender, CancellationToken ct) =>
                await sender.Send(new ListBankFieldSourcesQuery(), ct))
            .WithName("Core_BankFileFormats_Sources")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Core.BankFileFormats.View");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetBankFileFormatQuery(id), ct))
            .WithName("Core_BankFileFormats_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Core.BankFileFormats.View");

        group.MapPost("/", async (BankFileFormatDefinition definition, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateBankFileFormatCommand(definition), ct);
                return result.IsSuccess
                    ? Results.Created($"/api/core/bank-file-formats/{result.Value}", new { formatPublicId = result.Value })
                    : (object)result;
            })
            .WithName("Core_BankFileFormats_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Core.BankFileFormats.Manage");

        group.MapPut("/{id:guid}", async (Guid id, BankFileFormatDefinition definition, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateBankFileFormatCommand(id, definition), ct))
            .WithName("Core_BankFileFormats_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Core.BankFileFormats.Manage");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeleteBankFileFormatCommand(id), ct))
            .WithName("Core_BankFileFormats_Delete")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Core.BankFileFormats.Manage");
    }
}
