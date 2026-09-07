using FluentValidation;
using IngenIA365ERP.Application.Admin.Branches.Common;
using IngenIA365ERP.Application.Admin.Branches.Common;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Admin.Branches.DeactivateBranch;

/// <summary>
/// Marca la sucursal como inactiva (no soft-delete — la sucursal puede tener
/// historial transaccional). La sucursal matriz no se puede desactivar
/// (FR-002 — cada tenant debe tener una matriz activa).
/// </summary>
public sealed record DeactivateBranchCommand(Guid BranchPublicId) : IRequest<Result>;

public sealed class DeactivateBranchCommandValidator
    : AbstractValidator<DeactivateBranchCommand>
{
    public DeactivateBranchCommandValidator() => RuleFor(x => x.BranchPublicId).NotEmpty();
}

public sealed class DeactivateBranchCommandHandler
    : IRequestHandler<DeactivateBranchCommand, Result>
{
    private readonly IAdminDbContext _db;
    private readonly ICurrentUserService _currentUser;

    private readonly ICurrentTenantService _cooperativaActual;
    private readonly ICurrentCentralUserContext _usuario;

    public DeactivateBranchCommandHandler(
        IAdminDbContext db,
        ICurrentUserService currentUser,
        ICurrentTenantService cooperativaActual,
        ICurrentCentralUserContext usuario)
    {
        _db = db;
        _cooperativaActual = cooperativaActual;
        _usuario = usuario;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeactivateBranchCommand request, CancellationToken ct)
    {
        var branch = await _db.TenantBranches
            .FirstOrDefaultAsync(b => b.PublicId == request.BranchPublicId, ct);

        // Pertenencia: localizarla por su identificador publico no basta.
        // Sin esto, conocer el identificador de una sucursal de otra cooperativa
        // era suficiente para modificarla. Se responde igual que si no existiera:
        // confirmar que existe pero es ajena ya seria decir de mas.
        var alcance = await AlcanceDeSucursales.ResolverAsync(
            _db, _cooperativaActual, _usuario, ct);
        if (!alcance.Permitido) return Result.Failure(alcance.Codigo!, alcance.Mensaje!);
        if (alcance.Cooperativa is { } propia && branch is not null && branch.TenantId != propia)
        {
            branch = null;
        }
        if (branch is null) return Result.Failure("Generic.NotFound", "Sucursal no encontrada.");

        if (branch.IsHeadquarters)
        {
            return Result.Failure(BranchErrorCodes.CannotDeactivateHeadquarters,
                "La sucursal matriz no se puede desactivar.");
        }

        if (!branch.IsActive)
        {
            return Result.Failure(BranchErrorCodes.AlreadyInactive,
                "La sucursal ya estaba inactiva.");
        }

        branch.IsActive = false;
        branch.UpdatedBy = _currentUser.UserName ?? "SYSTEM";

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
