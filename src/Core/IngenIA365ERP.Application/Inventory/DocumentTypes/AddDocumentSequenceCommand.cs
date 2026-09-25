using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.DocumentTypes;

/// <summary>
/// Cambiar de prefijo o de número (feature 012, T150; contracts/api.md §8, <c>POST /{id}/sequences</c>; FR-038, T16;
/// data-model §5.9): una fila nueva de <c>INV_DocumentSequences</c> que cierra la vigente la víspera de
/// <see cref="ValidFrom"/>. Volver a un prefijo ya usado reabre su fila (continúa su consecutivo: el número es único por
/// tipo y prefijo); el mismo prefijo vigente sólo mueve su siguiente número. El siguiente número nunca queda en o por
/// debajo de uno ya emitido con ese prefijo (<c>Inventory.Sequence.NumberAlreadyIssued</c>) y dos vigencias del mismo
/// tipo no se cruzan (<c>Inventory.Sequence.Overlaps</c>). (nuevo)
/// </summary>
public sealed record AddDocumentSequenceCommand(Guid DocumentTypePublicId, string? Prefix, long NextValue, DateOnly ValidFrom, string Reason)
    : IRequest<Result<DocumentTypeDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class AddDocumentSequenceCommandValidator : ValidadorConMotivo<AddDocumentSequenceCommand>
{
    public AddDocumentSequenceCommandValidator()
    {
        RuleFor(x => x.DocumentTypePublicId).NotEmpty();
        RuleFor(x => x.Prefix).Matches(ReglasDeTipoDeDocumento.PatronDePrefijo).WithMessage(ReglasDeTipoDeDocumento.MensajeDePrefijo);
        RuleFor(x => x.NextValue).GreaterThanOrEqualTo(1).WithMessage("El siguiente número es 1 o mayor.");
    }
}

public sealed class AddDocumentSequenceCommandHandler(IApplicationDbContext db, VistaDeTiposDeDocumento vista)
    : IRequestHandler<AddDocumentSequenceCommand, Result<DocumentTypeDto>>
{
    public async Task<Result<DocumentTypeDto>> Handle(AddDocumentSequenceCommand request, CancellationToken ct)
    {
        var tipo = await db.InventoryDocumentTypes.Include(t => t.Sequences).FirstOrDefaultAsync(t => t.PublicId == request.DocumentTypePublicId, ct);
        if (tipo is null) return Falla(InventoryErrors.DocumentTypeNotFound());
        if (ClasesDeDocumento.De(tipo.Class).NumberedBy == NumberedBy.DianResolution) return Falla(InventoryErrors.NumberedByResolution(tipo.Class));

        var prefijo = ReglasDeTipoDeDocumento.Prefijo(request.Prefix);
        var secuencias = tipo.Sequences.Where(s => !s.IsDeleted).ToList();

        // Lo ya emitido con ese prefijo (también en documentos anulados: el número es único por tipo y prefijo).
        var ultimoEmitido = await db.InventoryDocuments.IgnoreQueryFilters()
            .Where(d => d.DocumentTypeId == tipo.Id && d.Prefix == prefijo && d.Number != null)
            .MaxAsync(d => d.Number, ct);
        if (ultimoEmitido is long emitido && request.NextValue <= emitido) return Falla(InventoryErrors.SequenceNumberAlreadyIssued(emitido));

        var mismaVigente = secuencias.FirstOrDefault(s => s.Prefix == prefijo && s.ValidTo is null);
        if (mismaVigente is not null)
        {
            // El prefijo vigente: sólo cambia su siguiente número (la vigencia sigue igual).
            Numerador.AjustarSiguiente(mismaVigente, request.NextValue);
        }
        else
        {
            // Una vigencia que empieza en o después de la nueva, o una cerrada que la cubre, se cruza con ella.
            if (secuencias.Any(s => s.ValidFrom >= request.ValidFrom || (s.ValidTo is { } hasta && hasta >= request.ValidFrom)))
                return Falla(InventoryErrors.SequenceOverlaps());

            var abierta = secuencias.FirstOrDefault(s => s.ValidTo is null);
            if (abierta is not null) abierta.ValidTo = request.ValidFrom.AddDays(-1);

            var anterior = secuencias.FirstOrDefault(s => s.Prefix == prefijo);
            if (anterior is not null)
            {
                anterior.ValidFrom = request.ValidFrom;
                anterior.ValidTo = null;
                Numerador.AjustarSiguiente(anterior, request.NextValue);
            }
            else
            {
                tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = prefijo, NextValue = request.NextValue, ValidFrom = request.ValidFrom });
            }
        }

        await db.SaveChangesAsync(ct);
        return Result.Success((await vista.ArmarAsync([tipo], conHistorial: true, ct))[0]);
    }

    private static Result<DocumentTypeDto> Falla(Error error) => Result.Failure<DocumentTypeDto>(error);
}
