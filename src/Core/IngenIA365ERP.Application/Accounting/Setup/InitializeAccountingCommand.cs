using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Setup;

/// <summary>
/// Inicia la contabilidad de la empresa (feature 009, US1, FR-003): una sola vez, con el catálogo
/// elegido, el nivel de movimiento, las longitudes de las auxiliares, el grupo NIIF, el primer
/// ejercicio (con sus doce períodos abiertos), la sucursal principal y cuatro ojos. El plan queda
/// con todas las cuentas del catálogo hasta nivel 4, ninguna de movimiento: las auxiliares las
/// crea la empresa (US2). Todo en un <c>SaveChangesAsync</c>.
/// </summary>
public sealed record InitializeAccountingCommand(
    string CatalogCode,
    byte MovementLevel,
    byte Level5Length,
    byte Level6Length,
    byte NiifGroup,
    int FirstFiscalYear,
    Guid MainBranchPublicId,
    bool FourEyes) : IRequest<Result<InicializacionDto>>;

public sealed class InitializeAccountingCommandValidator : AbstractValidator<InitializeAccountingCommand>
{
    public InitializeAccountingCommandValidator()
    {
        RuleFor(x => x.CatalogCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.MovementLevel).Must(l => l is 5 or 6).WithMessage("El nivel de movimiento es 5 o 6.");
        RuleFor(x => x.NiifGroup).InclusiveBetween((byte)1, (byte)3).WithMessage("El grupo NIIF es 1, 2 o 3.");
        RuleFor(x => x.FirstFiscalYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.MainBranchPublicId).NotEmpty();
        RuleFor(x => x).Must(x => CopiaDelCatalogo.ReparoDeLongitudes(x.MovementLevel, x.Level5Length, x.Level6Length) is null)
            .WithMessage(x => CopiaDelCatalogo.ReparoDeLongitudes(x.MovementLevel, x.Level5Length, x.Level6Length) ?? string.Empty);
    }
}

public sealed class InitializeAccountingCommandHandler(
    IApplicationDbContext db,
    IDateTimeService clock,
    ICurrentUserService user,
    AccountingAuditEmitter audit)
    : IRequestHandler<InitializeAccountingCommand, Result<InicializacionDto>>
{
    public async Task<Result<InicializacionDto>> Handle(InitializeAccountingCommand request, CancellationToken ct)
    {
        if (await db.AccountingSetups.AnyAsync(s => !s.IsDeleted, ct))
            return Result.Failure<InicializacionDto>(AccountingErrors.SetupAlreadyInitialized);

        if (CopiaDelCatalogo.ReparoDeLongitudes(request.MovementLevel, request.Level5Length, request.Level6Length) is { } reparo)
            return Result.Failure<InicializacionDto>(AccountingErrors.SetupLengthsInvalid(reparo));

        var codigoCatalogo = request.CatalogCode.Trim().ToUpperInvariant();
        var catalogo = await db.AccountCatalogs.Include(c => c.Entries).FirstOrDefaultAsync(c => c.Code == codigoCatalogo && !c.IsDeleted, ct);
        if (catalogo is null) return Result.Failure<InicializacionDto>(AccountingErrors.SetupCatalogNotFound(codigoCatalogo));

        var sucursal = await db.Branches.FirstOrDefaultAsync(b => b.PublicId == request.MainBranchPublicId && !b.IsDeleted, ct);
        if (sucursal is null) return Result.Failure<InicializacionDto>(AccountingErrors.SetupBranchNotFound);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";

        var cuentas = CopiaDelCatalogo.Copiar(catalogo.Entries.Where(e => !e.IsDeleted), new Dictionary<string, ChartOfAccount>(), quien, ahora);
        foreach (var c in cuentas) db.ChartOfAccounts.Add(c);

        var ejercicio = CopiaDelCatalogo.Ejercicio(request.FirstFiscalYear, quien, ahora);
        db.FiscalYears.Add(ejercicio);

        var setup = new AccountingSetup
        {
            CatalogId = catalogo.Id,
            MovementLevel = request.MovementLevel,
            Level5Length = request.Level5Length,
            Level6Length = request.MovementLevel == 6 ? request.Level6Length : (byte)0,
            NiifGroup = request.NiifGroup,
            FirstFiscalYear = request.FirstFiscalYear,
            MainBranchId = sucursal.Id,
            FourEyes = request.FourEyes,
            ReconciliationDayTolerance = 3,
            TaxTolerance = 1m,
            InitializedAt = ahora,
            InitializedBy = quien,
            CreatedAt = ahora,
            CreatedBy = quien,
        };
        db.AccountingSetups.Add(setup);

        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.AccountingSetupInitialized, nameof(AccountingSetup), setup.PublicId, null,
            new
            {
                catalog = catalogo.Code, catalogVersion = catalogo.Version, movementLevel = request.MovementLevel,
                level5Length = request.Level5Length, level6Length = setup.Level6Length, niifGroup = request.NiifGroup,
                firstFiscalYear = request.FirstFiscalYear, mainBranch = sucursal.Name, fourEyes = request.FourEyes,
                accounts = cuentas.Count, periods = ejercicio.Periods.Count,
            }, ct);

        return Result.Success(new InicializacionDto(cuentas.Count, ejercicio.Periods.Count));
    }
}
