using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Branches.Commands.UpdateBranch;

public record UpdateBranchCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }

    /// <summary>Oficina administrativa vinculada (ver <c>CreateBranchCommand.TenantBranchPublicId</c>); nulo quita el vínculo.</summary>
    public Guid? TenantBranchPublicId { get; init; }

    /// <summary>
    /// Municipio DIVIPOLA (feature 012, T178; ver <c>CreateBranchCommand.MunicipalityDaneCode</c>). Nulo = no cambia (un
    /// cliente que no lo conoce no lo borra); vacío = lo quita.
    /// </summary>
    public string? MunicipalityDaneCode { get; init; }
}

public class UpdateBranchCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateBranchCommand, Result>
{
    public async Task<Result> Handle(
        UpdateBranchCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Branches
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        var codigo = CodigoDeCatalogo.Normalizar(request.Code);
        if (codigo is not null)
        {
            var repetido = await context.Branches.AsNoTracking()
                .FirstOrDefaultAsync(e => e.LegacyCode == codigo && e.Id != entity.Id && !e.IsDeleted, cancellationToken);
            if (repetido is not null)
                return Result.Failure(CodigoDeCatalogo.Duplicado("una agencia", codigo, repetido.Name));
        }
        entity.LegacyCode = codigo;

        var vinculo = await CreateBranch.VinculoConOficina.ValidarAsync(context, request.TenantBranchPublicId, excluirId: entity.Id, cancellationToken);
        if (vinculo.IsFailure) return Result.Failure(vinculo.Error);

        if (request.MunicipalityDaneCode is not null)
        {
            var municipio = await CreateBranch.MunicipioDeSucursal.ValidarAsync(context, request.MunicipalityDaneCode, cancellationToken);
            if (municipio.IsFailure) return Result.Failure(municipio.Error);
            entity.MunicipalityDaneCode = municipio.Value;
        }

        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.TenantBranchPublicId = request.TenantBranchPublicId == Guid.Empty ? null : request.TenantBranchPublicId;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
