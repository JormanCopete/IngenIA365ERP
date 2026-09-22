using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Audit.QueryAuditLog;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Accounts;

// ------------------------------------------------------------------------------ árbol --

/// <summary>Hijos de un nodo (la raíz son las clases) para el árbol con carga por demanda.</summary>
public sealed record GetAccountTreeQuery(Guid? ParentPublicId = null, bool OnlyActive = false, bool All = false) : IRequest<Result<IReadOnlyList<CuentaNodoDto>>>;

public sealed class GetAccountTreeQueryValidator : AbstractValidator<GetAccountTreeQuery>;

public sealed class GetAccountTreeQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAccountTreeQuery, Result<IReadOnlyList<CuentaNodoDto>>>
{
    public async Task<Result<IReadOnlyList<CuentaNodoDto>>> Handle(GetAccountTreeQuery request, CancellationToken ct)
    {
        int? padreId = null;
        if (request.ParentPublicId is { } p)
        {
            padreId = await db.ChartOfAccounts.AsNoTracking().Where(a => a.PublicId == p && !a.IsDeleted).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);
            if (padreId is null) return Result.Failure<IReadOnlyList<CuentaNodoDto>>(AccountingErrors.AccountNotFound(p.ToString()));
        }

        // All: el plan completo de una vez (unas 2 000 filas) para el arbol en pantalla; si no, los hijos del nodo.
        var consulta = request.All ? db.ChartOfAccounts.AsNoTracking().Where(a => !a.IsDeleted) : db.ChartOfAccounts.AsNoTracking().Where(a => !a.IsDeleted && a.ParentId == padreId);
        if (request.OnlyActive) consulta = consulta.Where(a => a.IsActive);

        var lista = await consulta.OrderBy(a => a.Code)
            .Select(a => new CuentaNodoDto(a.PublicId, a.Code, a.Name, a.Level, a.Nature.ToString(), a.IsMovement, a.IsActive,
                db.ChartOfAccounts.Any(h => h.ParentId == a.Id && !h.IsDeleted), a.Origin.ToString(), a.Parent != null ? a.Parent.PublicId : null))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<CuentaNodoDto>>(lista);
    }
}

// -------------------------------------------------------------------------- buscador --

/// <summary>FR-015: el buscador sólo ofrece cuentas de movimiento, activas y habilitadas para el módulo que pregunta.</summary>
public sealed record SearchAccountsQuery(string Q, string? Module = null, bool OnlyMovement = true, int Take = 30) : IRequest<Result<IReadOnlyList<CuentaBuscadaDto>>>;

public sealed class SearchAccountsQueryValidator : AbstractValidator<SearchAccountsQuery>
{
    public SearchAccountsQueryValidator()
    {
        RuleFor(x => x.Q).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Module).Must(m => m is null || ModuloContable.EsValido(m)).WithMessage("Módulo desconocido.");
        RuleFor(x => x.Take).InclusiveBetween(1, 200);
    }
}

public sealed class SearchAccountsQueryHandler(IApplicationDbContext db) : IRequestHandler<SearchAccountsQuery, Result<IReadOnlyList<CuentaBuscadaDto>>>
{
    public async Task<Result<IReadOnlyList<CuentaBuscadaDto>>> Handle(SearchAccountsQuery request, CancellationToken ct)
    {
        var termino = request.Q.Trim();
        var consulta = db.ChartOfAccounts.AsNoTracking().Where(a => !a.IsDeleted);
        if (request.OnlyMovement) consulta = consulta.Where(a => a.IsMovement && a.IsActive);
        if (request.Module is { } modulo && ModuloContable.Bandera(modulo) is { } bandera)
            consulta = consulta.Where(a => (a.EnabledModules & bandera) == bandera);
        consulta = termino.All(char.IsDigit)
            ? consulta.Where(a => a.Code.StartsWith(termino))
            : consulta.Where(a => a.Name.Contains(termino) || a.Code.StartsWith(termino));

        var lista = await consulta.OrderBy(a => a.Code).Take(request.Take)
            .Select(a => new CuentaBuscadaDto(a.PublicId, a.Code, a.Name, a.Nature.ToString(), a.RequiresThirdParty, a.RequiresCrossDocument, a.RequiresCostCenter, a.RequiresBranch, a.RequiresTaxBase))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<CuentaBuscadaDto>>(lista);
    }
}

// ------------------------------------------------------------------------------ ficha --

public sealed record GetAccountByPublicIdQuery(Guid PublicId) : IRequest<Result<CuentaDto>>;

public sealed class GetAccountByPublicIdQueryValidator : AbstractValidator<GetAccountByPublicIdQuery>
{
    public GetAccountByPublicIdQueryValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class GetAccountByPublicIdQueryHandler(IApplicationDbContext db, IAccountReferenceFinder referencias) : IRequestHandler<GetAccountByPublicIdQuery, Result<CuentaDto>>
{
    public async Task<Result<CuentaDto>> Handle(GetAccountByPublicIdQuery request, CancellationToken ct)
    {
        var a = await db.ChartOfAccounts.AsNoTracking().Include(x => x.Parent).Include(x => x.Bank).Include(x => x.TaxRates)
            .FirstOrDefaultAsync(x => x.PublicId == request.PublicId && !x.IsDeleted, ct);
        if (a is null) return Result.Failure<CuentaDto>(AccountingErrors.AccountNotFound(request.PublicId.ToString()));

        var refs = await referencias.BuscarAsync(a.Id, a.Code, ct);
        return Result.Success(ADto(a, refs));
    }

    public static CuentaDto ADto(ChartOfAccount a, IReadOnlyList<ReferenciaDeCuenta> refs) => new(
        a.PublicId, a.Code, a.Name, a.Level, a.Nature.ToString(), a.Parent?.PublicId, a.Parent?.Code, a.NiifItemCode, a.Origin.ToString(),
        a.IsMovement, a.IsActive, a.FirstMovementAt, a.FirstMovementAt is not null, ModuloContable.Lista(a.EnabledModules),
        a.RequiresThirdParty, a.RequiresCrossDocument, a.RequiresCostCenter, a.RequiresBranch,
        a.BankId is { } ? new CuentaBancariaDto(a.Bank?.PublicId ?? Guid.Empty, a.Bank?.Name, a.BankAccountNumber) : null,
        a.TaxKind == TaxKind.None ? null : new CuentaDeImpuestoDto(a.TaxKind.ToString(), a.TaxConceptCode, a.RequiresTaxBase,
            a.TaxRates.Where(t => !t.IsDeleted).OrderBy(t => t.ValidFrom).Select(t => new TarifaDto(t.ValidFrom, t.Rate)).ToList()),
        refs);
}

// -------------------------------------------------------------------------- historial --

/// <summary>FR-018: los cambios de la cuenta (reglas, estado, nombre) con quién y cuándo, desde la auditoría.</summary>
public sealed record GetAccountHistoryQuery(Guid PublicId) : IRequest<Result<IReadOnlyList<EventoDeCuentaDto>>>;

public sealed class GetAccountHistoryQueryValidator : AbstractValidator<GetAccountHistoryQuery>
{
    public GetAccountHistoryQueryValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class GetAccountHistoryQueryHandler(ISender sender) : IRequestHandler<GetAccountHistoryQuery, Result<IReadOnlyList<EventoDeCuentaDto>>>
{
    public async Task<Result<IReadOnlyList<EventoDeCuentaDto>>> Handle(GetAccountHistoryQuery request, CancellationToken ct)
    {
        var eventos = await sender.Send(new QueryAuditLogQuery(null, nameof(ChartOfAccount), request.PublicId.ToString(), null, null, null, null,
            new PageRequest(1, 100)), ct);
        if (eventos.IsFailure) return Result.Failure<IReadOnlyList<EventoDeCuentaDto>>(eventos.Error);

        var lista = eventos.Value.Items
            .OrderByDescending(e => e.Timestamp)
            .Select(e => new EventoDeCuentaDto(e.Timestamp, e.Action, e.UserName, e.NewValuesJson ?? e.OldValuesJson))
            .ToList();
        return Result.Success<IReadOnlyList<EventoDeCuentaDto>>(lista);
    }
}

// ---------------------------------------------------------- parametrizaciones inválidas --

/// <summary>
/// FR-017 y FR-088: cuentas parametrizadas en otros módulos que ya no cumplen (agrupación,
/// inactivas, no habilitadas) y entidades institucionales sin persona vinculada cuando alguna
/// cuenta de nómina exige tercero. En E1 mira nómina; las demás tablas entran en E3.
/// </summary>
public sealed record ListInvalidParameterizationsQuery : IRequest<Result<IReadOnlyList<ParametrizacionInvalidaDto>>>;

public sealed class ListInvalidParameterizationsQueryValidator : AbstractValidator<ListInvalidParameterizationsQuery>;

public sealed class ListInvalidParameterizationsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListInvalidParameterizationsQuery, Result<IReadOnlyList<ParametrizacionInvalidaDto>>>
{
    public async Task<Result<IReadOnlyList<ParametrizacionInvalidaDto>>> Handle(ListInvalidParameterizationsQuery request, CancellationToken ct)
    {
        var lista = new List<ParametrizacionInvalidaDto>();
        var nomina = ModuloContable.Nombre(ModuloContable.Nomina);

        var porConcepto = await db.PayrollConceptDefinitionAccounts.AsNoTracking().Where(a => !a.IsDeleted)
            .Select(a => new { a.ConceptCode, Debito = a.DebitAccountId, Credito = a.CreditAccountId })
            .ToListAsync(ct);
        var ids = porConcepto.SelectMany(a => new[] { a.Debito, a.Credito }).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var conceptos = porConcepto.Select(a => a.ConceptCode).Distinct().ToList();
        var naturalezas = await db.PayrollConceptDefinitions.AsNoTracking().Where(c => conceptos.Contains(c.Code)).ToDictionaryAsync(c => c.Code, c => c.Nature, ct);
        var entidadesConTercero = new HashSet<Payroll.Services.EntidadInstitucional>();
        foreach (var fila in porConcepto)
        {
            foreach (var (id, lado) in new[] { (fila.Debito, "débito"), (fila.Credito, "crédito") })
            {
                var cuenta = cuentas.GetValueOrDefault(id);
                if (cuenta is { IsDeleted: false, RequiresThirdParty: true } && naturalezas.TryGetValue(fila.ConceptCode, out var naturaleza))
                {
                    var entidad = Payroll.Services.TercerosDeNomina.EntidadDe(fila.ConceptCode, naturaleza);
                    if (entidad != Payroll.Services.EntidadInstitucional.Ninguna) entidadesConTercero.Add(entidad);
                }
                var reparo = cuenta is null || cuenta.IsDeleted ? "la cuenta no existe" : AccountEligibility.Reparo(cuenta, ModuloContable.Nomina);
                if (reparo is not null)
                    lista.Add(new ParametrizacionInvalidaDto(nomina, "Cuentas por concepto", $"{fila.ConceptCode} ({lado})", cuenta?.Code, $"La cuenta {reparo}."));
            }
        }

        // FR-088: las entidades sin persona vinculada sólo estorban cuando un concepto de aporte o provisión las contabiliza con tercero.
        foreach (var entidad in entidadesConTercero.OrderBy(e => e))
        {
            foreach (var nombre in await Payroll.Services.VinculosInstitucionales.TodasSinPersonaAsync(db, entidad, ct))
                lista.Add(new ParametrizacionInvalidaDto(nomina, Payroll.Services.TercerosDeNomina.Pantalla(entidad).Replace("Nómina › ", string.Empty), nombre, null,
                    "Sin persona vinculada como tercero: la aprobación de nómina fallará en los aportes a esta entidad."));
        }

        return Result.Success<IReadOnlyList<ParametrizacionInvalidaDto>>(lista);
    }
}

// ------------------------------------------------------------------ cuentas bancarias --

/// <summary>Una cuenta del plan con banco: lo que la dispersión de nómina (feature 010, US8) y tesorería ofrecen como cuenta origen.</summary>
public sealed record CuentaBancariaDelPlanDto(Guid AccountPublicId, string Code, string Name, Guid BankPublicId, string BankName, string? BankAccountNumber, string? BankTransferCode, bool IsActive);

/// <summary>Las cuentas bancarias activas del plan, ordenadas por banco y código.</summary>
public sealed record ListBankAccountsQuery(Guid? BankPublicId = null) : IRequest<Result<IReadOnlyList<CuentaBancariaDelPlanDto>>>;

public sealed class ListBankAccountsQueryValidator : AbstractValidator<ListBankAccountsQuery>;

public sealed class ListBankAccountsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListBankAccountsQuery, Result<IReadOnlyList<CuentaBancariaDelPlanDto>>>
{
    public async Task<Result<IReadOnlyList<CuentaBancariaDelPlanDto>>> Handle(ListBankAccountsQuery request, CancellationToken ct)
    {
        var q = db.ChartOfAccounts.AsNoTracking().Include(a => a.Bank).Where(a => a.BankId != null && a.IsActive && !a.IsDeleted);
        if (request.BankPublicId is { } bankId) q = q.Where(a => a.Bank!.PublicId == bankId);
        var cuentas = await q.OrderBy(a => a.Bank!.Name).ThenBy(a => a.Code).ToListAsync(ct);
        return Result.Success<IReadOnlyList<CuentaBancariaDelPlanDto>>(cuentas.Select(a => new CuentaBancariaDelPlanDto(
            a.PublicId, a.Code, a.Name, a.Bank!.PublicId, a.Bank.Name, a.BankAccountNumber, a.Bank.TransferCode, a.IsActive)).ToList());
    }
}
