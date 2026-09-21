using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.HealthInsuranceProviders.Commands.UpdateHealthInsuranceProvider;

public record UpdateHealthInsuranceProviderCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public int CheckDigit { get; init; }

    /// <summary>Feature 010 (US5): código de la administradora en el listado del operador de la PILA (6 posiciones).</summary>
    public string? PilaCode { get; init; }
    /// <summary>Feature 009 (FR-088): persona de Personas que la representa como tercero; null = sin vínculo.</summary>
    public Guid? PersonPublicId { get; init; }
}

public class UpdateHealthInsuranceProviderCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateHealthInsuranceProviderCommand, Result>
{
    public async Task<Result> Handle(
        UpdateHealthInsuranceProviderCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.HealthInsuranceProviders
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var repetido = await context.HealthInsuranceProviders.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Code == codigo && e.Id != entity.Id, cancellationToken);
        if (repetido is not null)
            return Result.Failure(CodigoDeCatalogo.Duplicado("una EPS", codigo, repetido.Name));

        entity.Code = codigo;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName ?? string.Empty;
        entity.TaxId = request.TaxId;
        entity.CheckDigit = request.CheckDigit;
        entity.PilaCode = string.IsNullOrWhiteSpace(request.PilaCode) ? null : request.PilaCode.Trim().ToUpperInvariant();
        var persona = await PersonaVinculada.ResolverAsync(context, request.PersonPublicId, cancellationToken);
        if (persona.IsFailure) return Result.Failure(persona.Error);
        entity.PersonId = persona.Value;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
