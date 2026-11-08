using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Enums.Channel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Settings;

// Feature 021 (T039; FR-007, FR-020, FR-034, FR-057): los textos legales del canal. Una versión publicada no se edita: se publica
// otra, y la aceptación del asociado guarda la versión que aceptó.

public sealed record ChannelLegalTextDto(Guid PublicId, LegalTextKind Kind, int Version, string Content, DateTime PublishedAt, string? PublishedBy);

/// <summary>La versión vigente de cada tipo y, si se pide un tipo, su historia completa.</summary>
public sealed record ListChannelLegalTextsQuery(LegalTextKind? Kind = null) : IRequest<Result<IReadOnlyList<ChannelLegalTextDto>>>;

public sealed class ListChannelLegalTextsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListChannelLegalTextsQuery, Result<IReadOnlyList<ChannelLegalTextDto>>>
{
    public async Task<Result<IReadOnlyList<ChannelLegalTextDto>>> Handle(ListChannelLegalTextsQuery request, CancellationToken ct)
    {
        var consulta = db.ChannelLegalTexts.AsNoTracking();
        if (request.Kind is { } tipo) consulta = consulta.Where(t => t.Kind == tipo);
        var textos = await consulta.OrderBy(t => t.Kind).ThenByDescending(t => t.Version).ToListAsync(ct);
        var lista = request.Kind is null
            ? textos.GroupBy(t => t.Kind).Select(g => g.First()).ToList()
            : textos;
        return Result.Success<IReadOnlyList<ChannelLegalTextDto>>(lista
            .Select(t => new ChannelLegalTextDto(t.PublicId, t.Kind, t.Version, t.Content, t.PublishedAt, t.CreatedBy))
            .ToList());
    }
}

/// <summary>Publica una versión nueva de un texto legal.</summary>
public sealed record PublishChannelLegalTextCommand(LegalTextKind Kind, string Content) : IRequest<Result<ChannelLegalTextDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class PublishChannelLegalTextCommandValidator : AbstractValidator<PublishChannelLegalTextCommand>
{
    public const int LargoMaximo = 60_000;

    public PublishChannelLegalTextCommandValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().WithMessage("El tipo de texto no es válido.");
        RuleFor(x => x.Content).NotEmpty().WithMessage("El texto no puede ir vacío.")
            .MaximumLength(LargoMaximo).WithMessage($"El texto admite hasta {LargoMaximo:N0} caracteres.");
    }
}

public sealed class PublishChannelLegalTextCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<PublishChannelLegalTextCommand, Result<ChannelLegalTextDto>>
{
    public async Task<Result<ChannelLegalTextDto>> Handle(PublishChannelLegalTextCommand r, CancellationToken ct)
    {
        var ultima = await db.ChannelLegalTexts.Where(t => t.Kind == r.Kind).MaxAsync(t => (int?)t.Version, ct) ?? 0;
        var texto = new ChannelLegalText { Kind = r.Kind, Version = ultima + 1, Content = r.Content.Trim(), PublishedAt = reloj.UtcNow };
        db.ChannelLegalTexts.Add(texto);
        await db.SaveChangesAsync(ct);
        return Result.Success(new ChannelLegalTextDto(texto.PublicId, texto.Kind, texto.Version, texto.Content, texto.PublishedAt, texto.CreatedBy));
    }
}
