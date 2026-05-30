using FluentValidation;
using IngenIA365ERP.Application.Admin.Branches.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Admin.Branches.UpdateBranch;

/// <summary>
/// Actualiza datos de una sucursal. El <c>Code</c> NO es editable (identidad
/// de máquina). Cambiar <see cref="IsHeadquarters"/> de false→true requiere
/// que no haya otra matriz activa.
/// </summary>
public sealed record UpdateBranchCommand(
    Guid BranchPublicId,
    string Name,
    string? Address,
    string? Phone,
    string? Email,
    bool IsHeadquarters) : IRequest<Result>;

public sealed class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.BranchPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(200)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand, Result>
{
    private readonly IAdminDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateBranchCommandHandler(IAdminDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateBranchCommand request, CancellationToken ct)
    {
        var branch = await _db.TenantBranches
            .FirstOrDefaultAsync(b => b.PublicId == request.BranchPublicId, ct);
        if (branch is null) return Result.Failure("Generic.NotFound", "Sucursal no encontrada.");

        if (request.IsHeadquarters && !branch.IsHeadquarters
            && await _db.TenantBranches.AnyAsync(b => b.TenantId == branch.TenantId
                                                  && b.IsHeadquarters
                                                  && b.Id != branch.Id, ct))
        {
            return Result.Failure(BranchErrorCodes.HeadquartersExists,
                "La cooperativa ya tiene otra sucursal matriz activa.");
        }

        branch.Name = request.Name;
        branch.Address = request.Address;
        branch.Phone = request.Phone;
        branch.Email = request.Email;
        branch.IsHeadquarters = request.IsHeadquarters;
        branch.UpdatedBy = _currentUser.UserName ?? "SYSTEM";

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
