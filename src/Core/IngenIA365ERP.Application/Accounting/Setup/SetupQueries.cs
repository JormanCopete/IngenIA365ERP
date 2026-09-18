using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Setup;

// ------------------------------------------------------------------ configuración --

/// <summary><c>GET /api/accounting/setup</c>: sin iniciar devuelve los valores propuestos y <c>Initialized = false</c>.</summary>
public sealed record GetAccountingSetupQuery : IRequest<Result<ConfiguracionContableDto>>;

public sealed class GetAccountingSetupQueryValidator : AbstractValidator<GetAccountingSetupQuery>;

public sealed class GetAccountingSetupQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAccountingSetupQuery, Result<ConfiguracionContableDto>>
{
    public async Task<Result<ConfiguracionContableDto>> Handle(GetAccountingSetupQuery request, CancellationToken ct)
    {
        var setup = await db.AccountingSetups.AsNoTracking()
            .Include(s => s.Catalog).Include(s => s.MainBranch).Include(s => s.ResultAccount).Include(s => s.OpeningDocument)
            .FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (setup is null)
        {
            return Result.Success(new ConfiguracionContableDto(false, null, null, 6, 2, DateTime.UtcNow.Year, null, null, null, null,
                false, 3, 1m, false, null, null, null, null));
        }

        var auxiliares = await db.ChartOfAccounts.CountAsync(a => !a.IsDeleted && a.Origin == AccountOrigin.Company, ct);
        var primerMovimiento = await db.ChartOfAccounts.Where(a => !a.IsDeleted && a.FirstMovementAt != null).MinAsync(a => a.FirstMovementAt, ct);
        var bloqueada = auxiliares > 0 || primerMovimiento is not null;
        var motivo = !bloqueada ? null
            : primerMovimiento is null ? $"Ya existen {auxiliares} cuenta(s) auxiliar(es)."
            : $"Ya existen {auxiliares} auxiliar(es) y hay movimientos desde el {primerMovimiento:yyyy-MM-dd}.";

        return Result.Success(new ConfiguracionContableDto(
            true, setup.Catalog?.Code, setup.Catalog?.Name, setup.MovementLevel, setup.NiifGroup,
            setup.FirstFiscalYear, setup.ResultAccount?.PublicId, setup.ResultAccount?.Code, setup.MainBranch?.PublicId, setup.MainBranch?.Name,
            setup.FourEyes, setup.ReconciliationDayTolerance, setup.TaxTolerance, bloqueada, motivo, setup.OpeningDocument?.PublicId,
            setup.InitializedAt, setup.InitializedBy));
    }
}

// ----------------------------------------------------------------------- catálogos --

public sealed record ListAccountCatalogsQuery : IRequest<Result<IReadOnlyList<CatalogoContableDto>>>;

public sealed class ListAccountCatalogsQueryValidator : AbstractValidator<ListAccountCatalogsQuery>;

public sealed class ListAccountCatalogsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListAccountCatalogsQuery, Result<IReadOnlyList<CatalogoContableDto>>>
{
    public async Task<Result<IReadOnlyList<CatalogoContableDto>>> Handle(ListAccountCatalogsQuery request, CancellationToken ct)
    {
        var lista = await db.AccountCatalogs.AsNoTracking().Where(c => !c.IsDeleted)
            .OrderBy(c => c.Source).ThenBy(c => c.Code)
            .Select(c => new CatalogoContableDto(c.Code, c.Name, c.Version, c.Source.ToString(), c.EntryCount, c.ValidatedAt, c.ValidatedBy))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<CatalogoContableDto>>(lista);
    }
}

public sealed record GetCatalogEntriesQuery(string Code, byte? Level = null) : IRequest<Result<IReadOnlyList<EntradaDeCatalogoDto>>>;

public sealed class GetCatalogEntriesQueryValidator : AbstractValidator<GetCatalogEntriesQuery>
{
    public GetCatalogEntriesQueryValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Level).Must(l => l is null or >= 1 and <= 4).WithMessage("El nivel del catálogo va de 1 a 4.");
    }
}

public sealed class GetCatalogEntriesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCatalogEntriesQuery, Result<IReadOnlyList<EntradaDeCatalogoDto>>>
{
    public async Task<Result<IReadOnlyList<EntradaDeCatalogoDto>>> Handle(GetCatalogEntriesQuery request, CancellationToken ct)
    {
        var codigo = request.Code.Trim().ToUpperInvariant();
        var catalogo = await db.AccountCatalogs.AsNoTracking().FirstOrDefaultAsync(c => c.Code == codigo && !c.IsDeleted, ct);
        if (catalogo is null) return Result.Failure<IReadOnlyList<EntradaDeCatalogoDto>>(AccountingErrors.CatalogNotFound);

        var entradas = db.AccountCatalogEntries.AsNoTracking().Where(e => e.CatalogId == catalogo.Id && !e.IsDeleted);
        if (request.Level is { } nivel) entradas = entradas.Where(e => e.Level <= nivel);
        var lista = await entradas.OrderBy(e => e.Code)
            .Select(e => new EntradaDeCatalogoDto(e.Code, e.Name, e.Level, e.Nature.ToString(), e.NiifItemCode, e.ParentCode))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<EntradaDeCatalogoDto>>(lista);
    }
}

/// <summary>FR-006: cuentas del catálogo de la empresa que todavía no están en su plan (una versión nueva del catálogo, o cuentas retiradas).</summary>
public sealed record ListCatalogUpdatesQuery : IRequest<Result<IReadOnlyList<CuentaNuevaDeCatalogoDto>>>;

public sealed class ListCatalogUpdatesQueryValidator : AbstractValidator<ListCatalogUpdatesQuery>;

public sealed class ListCatalogUpdatesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListCatalogUpdatesQuery, Result<IReadOnlyList<CuentaNuevaDeCatalogoDto>>>
{
    public async Task<Result<IReadOnlyList<CuentaNuevaDeCatalogoDto>>> Handle(ListCatalogUpdatesQuery request, CancellationToken ct)
    {
        var setup = await db.AccountingSetups.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (setup is null) return Result.Failure<IReadOnlyList<CuentaNuevaDeCatalogoDto>>(AccountingErrors.NotInitialized);

        var enLaEmpresa = (await db.ChartOfAccounts.AsNoTracking().Where(a => !a.IsDeleted).Select(a => a.Code).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var nuevas = await db.AccountCatalogEntries.AsNoTracking()
            .Where(e => e.CatalogId == setup.CatalogId && !e.IsDeleted)
            .OrderBy(e => e.Code)
            .Select(e => new { e.Code, e.Name, e.Level, e.ParentCode })
            .ToListAsync(ct);
        var lista = nuevas.Where(e => !enLaEmpresa.Contains(e.Code))
            .Select(e => new CuentaNuevaDeCatalogoDto(e.Code, e.Name, e.Level, e.ParentCode, false))
            .ToList();
        return Result.Success<IReadOnlyList<CuentaNuevaDeCatalogoDto>>(lista);
    }
}
