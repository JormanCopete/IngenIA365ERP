using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Branches.Commands.CreateBranch;

public record CreateBranchCommand : IRequest<Result<Guid>>
{
    /// <summary>Código alfanumérico de la cooperativa (hasta 10); se guarda en LegacyCode.</summary>
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }

    /// <summary>
    /// Oficina de <c>ADM_Branches</c> (su PublicId) a la que corresponde esta sucursal contable
    /// (feature 009, R7): por ese vínculo el alcance de sucursal de un usuario
    /// (<c>SEC_UserBranchAssignments</c>) se traduce a las sucursales de <c>COR_Branches</c>. Nulo =
    /// sin vínculo. Hasta el 2026-09-20 la columna existía pero ningún comando la escribía: el
    /// alcance de sucursal (FR-035) no se podía configurar desde la API ni desde la pantalla.
    /// </summary>
    public Guid? TenantBranchPublicId { get; init; }

    /// <summary>
    /// Municipio DIVIPOLA de la sucursal (feature 012, T24; T178), validado contra <c>COR_Cities.DaneCode</c>:
    /// si no existe, <c>Branch.MunicipalityUnknown</c> (422). Nulo o vacío = sin municipio.
    /// </summary>
    public string? MunicipalityDaneCode { get; init; }
}

public class CreateBranchCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateBranchCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateBranchCommand request,
        CancellationToken cancellationToken)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code);
        if (codigo is not null)
        {
            var repetido = await context.Branches.AsNoTracking()
                .FirstOrDefaultAsync(e => e.LegacyCode == codigo && !e.IsDeleted, cancellationToken);
            if (repetido is not null)
                return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("una agencia", codigo, repetido.Name));
        }

        var vinculo = await VinculoConOficina.ValidarAsync(context, request.TenantBranchPublicId, excluirId: null, cancellationToken);
        if (vinculo.IsFailure) return Result.Failure<Guid>(vinculo.Error);

        var municipio = await MunicipioDeSucursal.ValidarAsync(context, request.MunicipalityDaneCode, cancellationToken);
        if (municipio.IsFailure) return Result.Failure<Guid>(municipio.Error);

        var entity = new Branch
        {
            LegacyCode = codigo,
            Name = request.Name,
            ShortName = request.ShortName,
            TenantBranchPublicId = request.TenantBranchPublicId == Guid.Empty ? null : request.TenantBranchPublicId,
            MunicipalityDaneCode = municipio.Value,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Branches.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage($"El código no puede superar {CodigoDeCatalogo.LargoCorto} caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron)
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(80).WithMessage("Name must not exceed 80 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(40).WithMessage("Short name must not exceed 40 characters.");

        RuleFor(x => x.MunicipalityDaneCode)
            .Matches(MunicipioDeSucursal.Patron).When(x => !string.IsNullOrWhiteSpace(x.MunicipalityDaneCode))
            .WithMessage(MunicipioDeSucursal.MensajeDePatron);
    }
}

/// <summary>
/// El municipio DIVIPOLA de una sucursal (feature 012, T24; T178): cinco dígitos que existan en <c>COR_Cities.DaneCode</c>.
/// Sin FK: el código es el dato. (nuevo)
/// </summary>
public static class MunicipioDeSucursal
{
    public const string Patron = "^[0-9]{5}$";
    public const string MensajeDePatron = "El municipio es el código DANE (DIVIPOLA) de cinco dígitos.";

    public static Error Desconocido(string codigo) =>
        new("Branch.MunicipalityUnknown", $"El municipio {codigo} no está en las ciudades (código DIVIPOLA). Cárguelo en Maestros › Ciudades o revise el código.");

    /// <summary>El código normalizado (nulo si viene vacío) o <c>Branch.MunicipalityUnknown</c>.</summary>
    public static async Task<Result<string?>> ValidarAsync(IApplicationDbContext context, string? codigo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return Result.Success<string?>(null);
        var limpio = codigo.Trim();
        var existe = await context.Cities.AsNoTracking().AnyAsync(c => c.DaneCode == limpio && !c.IsDeleted, ct);
        return existe ? Result.Success<string?>(limpio) : Result.Failure<string?>(Desconocido(limpio));
    }
}

/// <summary>
/// El vínculo sucursal contable → oficina administrativa es uno a uno (índice único filtrado sobre
/// <c>COR_Branches.TenantBranchPublicId</c>): dos sucursales sobre la misma oficina harían que el
/// alcance de un usuario abarcara las dos sin que nadie lo hubiera decidido. Se comprueba aquí
/// para responder con un error legible en vez del choque del índice.
/// </summary>
public static class VinculoConOficina
{
    public static Error OficinaYaVinculada(string sucursal) =>
        new("Branch.OfficeAlreadyLinked", $"Esa oficina ya está vinculada a la sucursal «{sucursal}»: cada oficina corresponde a una sola sucursal contable.");

    public static async Task<Result> ValidarAsync(IApplicationDbContext context, Guid? tenantBranchPublicId, int? excluirId, CancellationToken ct)
    {
        if (tenantBranchPublicId is null || tenantBranchPublicId == Guid.Empty) return Result.Success();
        var otra = await context.Branches.AsNoTracking()
            .Where(b => b.TenantBranchPublicId == tenantBranchPublicId && !b.IsDeleted && (excluirId == null || b.Id != excluirId))
            .Select(b => b.Name)
            .FirstOrDefaultAsync(ct);
        return otra is null ? Result.Success() : Result.Failure(OficinaYaVinculada(otra));
    }
}
