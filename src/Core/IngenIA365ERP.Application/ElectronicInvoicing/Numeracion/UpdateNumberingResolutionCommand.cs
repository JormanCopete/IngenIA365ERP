using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;

/// <summary>
/// Corregir una resolución (feature 012, I4, T709; <c>PUT /api/electronic-invoicing/resolutions/{id}</c>, api.md §24.2): los datos de
/// digitación se corrigen mientras no haya números emitidos (<c>LastIssuedNumber = RangeFrom − 1</c>), con las mismas reglas del alta. Con
/// números emitidos sólo se admite mover <see cref="ValidTo"/> hacia atrás para retirarla; cualquier otro cambio responde
/// <c>ElectronicInvoicing.Resolution.InUse</c>: se registra otra. (nuevo)
/// </summary>
public sealed record UpdateNumberingResolutionCommand(
    Guid ResolutionPublicId,
    ResolutionKind Kind,
    ResolutionKind? BacksUpKind,
    string ResolutionNumber,
    DateOnly ResolutionDate,
    string? Prefix,
    long RangeFrom,
    long RangeTo,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    DianEnvironment Environment,
    string Reason,
    string? Notes = null)
    : IRequest<Result<DianNumberingResolutionDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateNumberingResolutionCommandValidator : ValidadorConMotivo<UpdateNumberingResolutionCommand>
{
    public UpdateNumberingResolutionCommandValidator()
    {
        RuleFor(x => x.ResolutionPublicId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Environment).IsInEnum();
        RuleFor(x => x.ResolutionNumber).NotEmpty().WithMessage("Indique el número de la resolución.").MaximumLength(30);
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}

public sealed class UpdateNumberingResolutionCommandHandler(IApplicationDbContext db, IPrefijosDeModulos prefijos, IDateTimeService reloj)
    : IRequestHandler<UpdateNumberingResolutionCommand, Result<DianNumberingResolutionDto>>
{
    public async Task<Result<DianNumberingResolutionDto>> Handle(UpdateNumberingResolutionCommand request, CancellationToken ct)
    {
        var resolucion = await db.DianNumberingResolutions.Include(r => r.Channels).FirstOrDefaultAsync(r => r.PublicId == request.ResolutionPublicId, ct);
        if (resolucion is null) return Falla(ErroresDeNumeracionYConfiguracion.ResolutionNotFound());

        var datos = new DatosDeResolucion(request.Kind, request.BacksUpKind, request.ResolutionNumber, request.Prefix, request.RangeFrom,
            request.RangeTo, request.ValidFrom, request.ValidTo, request.Environment);
        var notas = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        if (resolucion.TieneNumerosEmitidos)
        {
            // Con números emitidos: sólo retirarla (fecha final hacia atrás) y las notas.
            var igual = resolucion.Kind == datos.Kind && resolucion.BacksUpKind == datos.Respaldo
                && resolucion.ResolutionNumber == datos.ResolutionNumber.Trim() && resolucion.ResolutionDate == request.ResolutionDate
                && resolucion.Prefix == datos.PrefijoNormalizado && resolucion.RangeFrom == datos.RangeFrom && resolucion.RangeTo == datos.RangeTo
                && resolucion.ValidFrom == datos.ValidFrom && resolucion.Environment == datos.Environment;
            if (!igual || request.ValidTo > resolucion.ValidTo || request.ValidTo < resolucion.ValidFrom)
                return Falla(ErroresDeNumeracionYConfiguracion.ResolutionInUse(resolucion.LastIssuedNumber));
            resolucion.ValidTo = request.ValidTo;
            resolucion.Notes = notas;
            await db.SaveChangesAsync(ct);
            return Result.Success(DianNumberingResolutionDto.De(resolucion, reloj.HoyLocal));
        }

        var revision = await ValidacionDeResolucion.RevisarAsync(db, prefijos, datos, resolucion.Id, ct);
        if (revision.IsFailure) return Falla(revision.Error);

        resolucion.Kind = datos.Kind;
        resolucion.BacksUpKind = datos.Respaldo;
        resolucion.ResolutionNumber = datos.ResolutionNumber.Trim();
        resolucion.ResolutionDate = request.ResolutionDate;
        resolucion.Prefix = datos.PrefijoNormalizado;
        resolucion.RangeFrom = datos.RangeFrom; // sin nada emitido arrastra LastIssuedNumber a RangeFrom − 1
        resolucion.RangeTo = datos.RangeTo;
        resolucion.ValidFrom = datos.ValidFrom;
        resolucion.ValidTo = datos.ValidTo;
        resolucion.Environment = datos.Environment;
        resolucion.Notes = notas;
        // La clave técnica es sólo de factura: si deja de serlo, las asociaciones la pierden.
        if (resolucion.Kind != ResolutionKind.Invoice)
            foreach (var asociacion in resolucion.Channels.Where(c => c.TechnicalKey is not null)) asociacion.TechnicalKey = null;

        await db.SaveChangesAsync(ct);
        return Result.Success(DianNumberingResolutionDto.De(resolucion, reloj.HoyLocal));
    }

    private static Result<DianNumberingResolutionDto> Falla(Error error) => Result.Failure<DianNumberingResolutionDto>(error);
}
