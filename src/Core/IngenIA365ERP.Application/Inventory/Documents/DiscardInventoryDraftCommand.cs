using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>
/// Descarta un borrador (feature 012, T145; contracts/api.md §9.3, <c>POST /{id}/discard</c>; FR-005): queda
/// <c>Discarded</c> con quién, cuándo y por qué, se conserva y se audita; sus vínculos se dan de baja lógica (suelta los
/// orígenes). No consume número. (nuevo)
/// </summary>
public sealed record DiscardInventoryDraftCommand(Guid DocumentPublicId, DocumentClassGroup ExpectedGroup, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DiscardInventoryDraftCommandValidator : ValidadorConMotivo<DiscardInventoryDraftCommand>
{
    public DiscardInventoryDraftCommandValidator()
    {
        RuleFor(x => x.DocumentPublicId).NotEmpty();
    }
}

public sealed class DiscardInventoryDraftCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    VistaDeDocumentos vista)
    : IRequestHandler<DiscardInventoryDraftCommand, Result>
{
    public async Task<Result> Handle(DiscardInventoryDraftCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure(ErroresDelDocumento.SinUsuario());

        var documento = await vista.BuscarAsync(request.DocumentPublicId, request.ExpectedGroup, seguir: true, ct);
        if (documento is null) return Result.Failure(InventoryErrors.DocumentNotFound());
        if (documento.Status != DocumentStatus.Draft) return Result.Failure(InventoryErrors.NotDraft(documento.Status));

        var ahora = reloj.UtcNow;
        documento.Descartar(usuario, ahora, request.Reason);

        // Suelta sus orígenes: los vínculos donde es el destino (y los de sus líneas) quedan de baja.
        var vinculos = await db.DocumentLinks.Include(l => l.LineLinks).Where(l => l.TargetDocumentId == documento.Id).ToListAsync(ct);
        foreach (var vinculo in vinculos)
        {
            vinculo.IsDeleted = true;
            vinculo.DeletedAt = ahora;
            vinculo.DeletedBy = actor.Name;
            foreach (var deLinea in vinculo.LineLinks.Where(x => !x.IsDeleted))
            {
                deLinea.IsDeleted = true;
                deLinea.DeletedAt = ahora;
                deLinea.DeletedBy = actor.Name;
            }
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
