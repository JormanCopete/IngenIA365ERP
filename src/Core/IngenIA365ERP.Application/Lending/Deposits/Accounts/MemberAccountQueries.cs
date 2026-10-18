using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts.Parties;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

// Feature 014, US2 (T073, T078; api.md §4): las consultas de las cuentas de captación y de aportes. Todas filtran por el alcance de la Cartera —la
// oficina y la línea de la cuenta (PorOficinaYLinea)—; el detalle y las partes de una cuenta fuera de él responden el mismo 404 que una inexistente
// (LectorDeCuentas.CuentaEnAlcanceAsync). Los conteos no traen titulares (Accounts.View). (nuevo)

/// <summary>
/// Las cuentas (<c>GET /api/lending/accounts</c>, <c>Lending.Accounts.Read</c>): búsqueda por número o titular (documento o nombre), naturaleza, clase,
/// línea, oficina, estado, titular y número; la más reciente primero. El saldo es el del titular (Σ del libro, data-model §0.2); el disponible es el
/// mismo hasta que K8 traiga la posición diaria con sus retenciones. (nuevo)
/// </summary>
public sealed record ListMemberAccountsQuery(
    string? Search = null,
    LineNature? Nature = null,
    DepositClass? DepositClass = null,
    Guid? LinePublicId = null,
    Guid? OfficePublicId = null,
    MemberAccountStatus? Status = null,
    Guid? PersonPublicId = null,
    string? Number = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<MemberAccountListItemDto>>>;

public sealed class ListMemberAccountsQueryValidator : AbstractValidator<ListMemberAccountsQuery>
{
    public ListMemberAccountsQueryValidator()
    {
        RuleFor(x => x.Nature).Must(n => n is null or LineNature.Deposit or LineNature.Contribution)
            .WithMessage("La naturaleza de una cuenta es captación o aportes.");
        RuleFor(x => x.DepositClass).IsInEnum().When(x => x.DepositClass is not null);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Number).MaximumLength(20);
    }
}

public sealed class ListMemberAccountsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListMemberAccountsQuery, Result<PagedResult<MemberAccountListItemDto>>>
{
    public async Task<Result<PagedResult<MemberAccountListItemDto>>> Handle(ListMemberAccountsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.MemberAccounts.AsNoTracking().Where(a => !a.IsDeleted).PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId);
        if (request.Nature is { } naturaleza) consulta = consulta.Where(a => a.Nature == naturaleza);
        if (request.DepositClass is { } clase) consulta = consulta.Where(a => a.DepositClass == clase);
        if (request.Status is { } estado) consulta = consulta.Where(a => a.Status == estado);
        if (request.LinePublicId is { } linea) consulta = consulta.Where(a => db.CreditLines.Any(l => l.Id == a.CreditLineId && l.PublicId == linea));
        if (request.OfficePublicId is { } oficina) consulta = consulta.Where(a => db.Branches.Any(b => b.Id == a.BranchId && b.PublicId == oficina));
        if (request.PersonPublicId is { } persona) consulta = consulta.Where(a => db.People.Any(p => p.Id == a.HolderPersonId && p.PublicId == persona));
        if (!string.IsNullOrWhiteSpace(request.Number))
        {
            var numero = request.Number.Trim().ToUpperInvariant();
            consulta = consulta.Where(a => a.Number == numero || a.SourceAccountNumber == numero);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var texto = request.Search.Trim();
            consulta = consulta.Where(a => a.Number.Contains(texto) || db.People.Any(p => p.Id == a.HolderPersonId
                && (p.TaxId.Contains(texto) || p.FirstName.Contains(texto) || p.LastName.Contains(texto) || (p.BusinessName != null && p.BusinessName.Contains(texto)))));
        }

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(a => a.OpenedOn).ThenByDescending(a => a.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);

        var personas = await LectorDeCuentas.PersonasAsync(db, filas.Select(a => a.HolderPersonId), ct);
        var idsDeLinea = filas.Select(a => a.CreditLineId).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => idsDeLinea.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new LendingLineRefDto(l.PublicId, l.Code, l.Name), ct);
        var idsDeOficina = filas.Select(a => a.BranchId).Distinct().ToList();
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => idsDeOficina.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new MemberAccountOfficeDto(b.PublicId, b.Name), ct);
        var saldos = await LectorDeCuentas.SaldosAsync(db, filas.Select(a => a.Id).ToList(), ct);
        var idsDeCuenta = filas.Select(a => a.Id).ToList();
        var exentas = (await db.GmfExemptAccounts.AsNoTracking().Where(g => idsDeCuenta.Contains(g.MemberAccountId) && g.ValidTo == null && !g.IsDeleted)
            .Select(g => g.MemberAccountId).ToListAsync(ct)).ToHashSet();

        var items = filas.Select(a =>
        {
            var saldo = LectorDeCuentas.SaldoDelTitular(a.Nature, saldos.GetValueOrDefault(a.Id) ?? []);
            var marcas = new List<string>();
            if (a.RequiresDataUpdate) marcas.Add("RequiresDataUpdate");
            if (a.IsGarnished) marcas.Add("Garnished");
            if (exentas.Contains(a.Id)) marcas.Add("GmfExempt");
            if (a.TechnicalNegativeSince is not null) marcas.Add("OverdrawnTechnical");
            return new MemberAccountListItemDto(a.PublicId, a.Number, a.Nature, a.DepositClass, lineas[a.CreditLineId], oficinas.GetValueOrDefault(a.BranchId),
                personas[a.HolderPersonId], a.Status, saldo, saldo, marcas);
        }).ToList();
        return Result.Success(new PagedResult<MemberAccountListItemDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>
/// Los conteos de cuentas por estado y por línea, con su saldo y sin titulares (<c>GET /api/lending/accounts/counts</c>, <c>Lending.Accounts.View</c>),
/// sólo del alcance. (nuevo)
/// </summary>
public sealed record GetMemberAccountCountsQuery(LineNature? Nature = null, Guid? LinePublicId = null, Guid? OfficePublicId = null,
    MemberAccountStatus? Status = null) : IRequest<Result<MemberAccountCountsDto>>;

public sealed class GetMemberAccountCountsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<GetMemberAccountCountsQuery, Result<MemberAccountCountsDto>>
{
    public async Task<Result<MemberAccountCountsDto>> Handle(GetMemberAccountCountsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.MemberAccounts.AsNoTracking().Where(a => !a.IsDeleted).PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId);
        if (request.Nature is { } naturaleza) consulta = consulta.Where(a => a.Nature == naturaleza);
        if (request.Status is { } estado) consulta = consulta.Where(a => a.Status == estado);
        if (request.LinePublicId is { } linea) consulta = consulta.Where(a => db.CreditLines.Any(l => l.Id == a.CreditLineId && l.PublicId == linea));
        if (request.OfficePublicId is { } oficina) consulta = consulta.Where(a => db.Branches.Any(b => b.Id == a.BranchId && b.PublicId == oficina));
        var cuentas = await consulta.Select(a => new { a.Id, a.Nature, a.Status, a.CreditLineId }).ToListAsync(ct);
        var saldos = await LectorDeCuentas.SaldosAsync(db, cuentas.Select(a => a.Id).ToList(), ct);
        decimal Saldo(int id, LineNature n) => LectorDeCuentas.SaldoDelTitular(n, saldos.GetValueOrDefault(id) ?? []);
        var idsDeLinea = cuentas.Select(a => a.CreditLineId).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => idsDeLinea.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new LendingLineRefDto(l.PublicId, l.Code, l.Name), ct);

        var porEstado = cuentas.GroupBy(a => a.Status).OrderBy(g => g.Key)
            .Select(g => new MemberAccountCountByStatusDto(g.Key, g.Count(), g.Sum(a => Saldo(a.Id, a.Nature)))).ToList();
        var porLinea = cuentas.GroupBy(a => a.CreditLineId).OrderBy(g => lineas[g.Key].Code, StringComparer.Ordinal)
            .Select(g => new MemberAccountCountByLineDto(lineas[g.Key], g.Count(), g.Sum(a => Saldo(a.Id, a.Nature)))).ToList();
        return Result.Success(new MemberAccountCountsDto(porEstado, porLinea));
    }
}

/// <summary>Una cuenta con todo lo suyo (<c>GET /api/lending/accounts/{id}</c>, <c>Lending.Accounts.Read</c>; fuera del alcance, el 404). (nuevo)</summary>
public sealed record GetMemberAccountQuery(Guid AccountPublicId) : IRequest<Result<MemberAccountDto>>;

public sealed class GetMemberAccountQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetMemberAccountQuery, Result<MemberAccountDto>>
{
    public async Task<Result<MemberAccountDto>> Handle(GetMemberAccountQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        return leida.IsFailure
            ? Result.Failure<MemberAccountDto>(leida.Error)
            : Result.Success(await LectorDeCuentas.DtoAsync(db, leida.Value, reloj.HoyLocal, ct));
    }
}

/// <summary>
/// Las partes de la cuenta a una fecha (hoy si no viene) y, si se pide, toda su historia (<c>GET /api/lending/accounts/{id}/parties</c>,
/// <c>Lending.Accounts.Read</c>). (nuevo)
/// </summary>
public sealed record GetMemberAccountPartiesQuery(Guid AccountPublicId, DateOnly? Date = null, bool IncludeHistory = false)
    : IRequest<Result<MemberAccountPartiesDto>>;

public sealed class GetMemberAccountPartiesQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetMemberAccountPartiesQuery, Result<MemberAccountPartiesDto>>
{
    public async Task<Result<MemberAccountPartiesDto>> Handle(GetMemberAccountPartiesQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        return leida.IsFailure
            ? Result.Failure<MemberAccountPartiesDto>(leida.Error)
            : Result.Success(await ConsultasDePartes.DtoAsync(db, leida.Value, request.Date ?? reloj.HoyLocal, request.IncludeHistory, ct));
    }
}
