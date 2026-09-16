using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Setup;

/// <summary>
/// Cambia la configuración contable (feature 009, FR-004). Catálogo, nivel de movimiento y
/// longitudes sólo mientras no exista ninguna auxiliar ni ningún movimiento; cambiar de catálogo
/// retira (soft-delete) las cuentas <c>Origin = Catalog</c> y copia las del nuevo. Cuatro ojos,
/// cuenta de resultado, sucursal principal, grupo NIIF y tolerancias cambian siempre. Auditado
/// con antes y después.
/// </summary>
public sealed record UpdateAccountingSetupCommand(
    string? CatalogCode,
    byte? MovementLevel,
    byte? Level5Length,
    byte? Level6Length,
    byte? NiifGroup,
    Guid? ResultAccountPublicId,
    Guid? MainBranchPublicId,
    bool FourEyes,
    int ReconciliationDayTolerance,
    decimal TaxTolerance) : IRequest<Result>;

public sealed class UpdateAccountingSetupCommandValidator : AbstractValidator<UpdateAccountingSetupCommand>
{
    public UpdateAccountingSetupCommandValidator()
    {
        RuleFor(x => x.CatalogCode).MaximumLength(20);
        RuleFor(x => x.MovementLevel).Must(l => l is null or 5 or 6).WithMessage("El nivel de movimiento es 5 o 6.");
        RuleFor(x => x.NiifGroup).Must(g => g is null or >= 1 and <= 3).WithMessage("El grupo NIIF es 1, 2 o 3.");
        RuleFor(x => x.ReconciliationDayTolerance).InclusiveBetween(0, 30);
        RuleFor(x => x.TaxTolerance).InclusiveBetween(0m, 1_000_000m);
    }
}

public sealed class UpdateAccountingSetupCommandHandler(
    IApplicationDbContext db,
    IDateTimeService clock,
    ICurrentUserService user,
    AccountingAuditEmitter audit)
    : IRequestHandler<UpdateAccountingSetupCommand, Result>
{
    public async Task<Result> Handle(UpdateAccountingSetupCommand request, CancellationToken ct)
    {
        var setup = await db.AccountingSetups.Include(s => s.Catalog).FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (setup is null) return Result.Failure(AccountingErrors.NotInitialized);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var antes = new
        {
            catalog = setup.Catalog?.Code, setup.MovementLevel, setup.Level5Length, setup.Level6Length, setup.NiifGroup,
            setup.ResultAccountId, setup.MainBranchId, setup.FourEyes, setup.ReconciliationDayTolerance, setup.TaxTolerance,
        };

        // --- lo estructural: catálogo, nivel y longitudes (FR-004) ---
        var codigoCatalogo = string.IsNullOrWhiteSpace(request.CatalogCode) ? null : request.CatalogCode.Trim().ToUpperInvariant();
        var cambiaCatalogo = codigoCatalogo is not null && !string.Equals(codigoCatalogo, setup.Catalog?.Code, StringComparison.OrdinalIgnoreCase);
        var nivel = request.MovementLevel ?? setup.MovementLevel;
        var largo5 = request.Level5Length ?? setup.Level5Length;
        var largo6 = request.Level6Length ?? setup.Level6Length;
        var cambiaEstructura = cambiaCatalogo || nivel != setup.MovementLevel || largo5 != setup.Level5Length || (nivel == 6 && largo6 != setup.Level6Length);

        if (cambiaEstructura)
        {
            var auxiliares = await db.ChartOfAccounts.CountAsync(a => !a.IsDeleted && a.Origin == AccountOrigin.Company, ct);
            var primerMovimiento = await db.ChartOfAccounts.Where(a => !a.IsDeleted && a.FirstMovementAt != null).MinAsync(a => a.FirstMovementAt, ct);
            if (auxiliares > 0 || primerMovimiento is not null)
                return Result.Failure(AccountingErrors.SetupLocked(auxiliares, primerMovimiento));

            if (CopiaDelCatalogo.ReparoDeLongitudes(nivel, largo5, largo6) is { } reparo)
                return Result.Failure(AccountingErrors.SetupLengthsInvalid(reparo));

            if (cambiaCatalogo)
            {
                var catalogo = await db.AccountCatalogs.Include(c => c.Entries).FirstOrDefaultAsync(c => c.Code == codigoCatalogo && !c.IsDeleted, ct);
                if (catalogo is null) return Result.Failure(AccountingErrors.SetupCatalogNotFound(codigoCatalogo!));

                // Retirar el plan anterior (queda en la base por Principio VII) y copiar el nuevo.
                foreach (var cuenta in await db.ChartOfAccounts.Where(a => !a.IsDeleted).ToListAsync(ct))
                {
                    cuenta.IsDeleted = true;
                    cuenta.DeletedAt = ahora;
                    cuenta.DeletedBy = quien;
                }
                foreach (var c in CopiaDelCatalogo.Copiar(catalogo.Entries.Where(e => !e.IsDeleted), new Dictionary<string, ChartOfAccount>(), quien, ahora))
                    db.ChartOfAccounts.Add(c);
                setup.CatalogId = catalogo.Id;
                setup.Catalog = catalogo;
                setup.ResultAccountId = null;
            }

            setup.MovementLevel = nivel;
            setup.Level5Length = largo5;
            setup.Level6Length = nivel == 6 ? largo6 : (byte)0;
        }

        // --- lo que cambia siempre ---
        if (request.NiifGroup is { } grupo) setup.NiifGroup = grupo;
        if (request.MainBranchPublicId is { } sucursalId)
        {
            var sucursal = await db.Branches.FirstOrDefaultAsync(b => b.PublicId == sucursalId && !b.IsDeleted, ct);
            if (sucursal is null) return Result.Failure(AccountingErrors.SetupBranchNotFound);
            setup.MainBranchId = sucursal.Id;
        }
        if (request.ResultAccountPublicId is { } resultadoId)
        {
            var cuenta = await db.ChartOfAccounts.FirstOrDefaultAsync(a => a.PublicId == resultadoId && !a.IsDeleted, ct);
            if (cuenta is null || !cuenta.IsMovement || !cuenta.IsActive) return Result.Failure(AccountingErrors.SetupResultAccountNotMovement);
            setup.ResultAccountId = cuenta.Id;
        }
        else if (!cambiaCatalogo)
        {
            setup.ResultAccountId = null;
        }
        setup.FourEyes = request.FourEyes;
        setup.ReconciliationDayTolerance = request.ReconciliationDayTolerance;
        setup.TaxTolerance = request.TaxTolerance;
        setup.UpdatedAt = ahora;
        setup.UpdatedBy = quien;

        await db.SaveChangesAsync(ct);

        await audit.EmitAsync("Accounting.Setup.Updated", nameof(AccountingSetup), setup.PublicId, antes, new
        {
            catalog = setup.Catalog?.Code, setup.MovementLevel, setup.Level5Length, setup.Level6Length, setup.NiifGroup,
            setup.ResultAccountId, setup.MainBranchId, setup.FourEyes, setup.ReconciliationDayTolerance, setup.TaxTolerance,
        }, ct);

        return Result.Success();
    }
}
