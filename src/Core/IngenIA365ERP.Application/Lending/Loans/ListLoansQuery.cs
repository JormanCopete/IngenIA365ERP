using IngenIA365ERP.Application.Common.Personas;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Loans;

/// <summary>
/// <c>GET /api/lending/loans</c> (feature 013, US4, T0504; api.md §8.1, <c>Lending.Loans.Read</c>): los créditos del alcance de oficina y línea por
/// número o deudor, estado, línea, oficina, persona (como deudor o codeudor), modalidad, mora mínima y origen. Los saldos y la mora salen de la
/// <b>posición diaria vigente</b> (la última fecha, la última versión), que nunca es la autoridad. (nuevo)
/// </summary>
public sealed record ListLoansQuery(
    string? Search = null,
    LoanStatus? Status = null,
    Guid? Line = null,
    Guid? Office = null,
    Guid? PersonPublicId = null,
    string? Role = null,
    string? Modality = null,
    int? MinDaysPastDue = null,
    LoanOrigin? Origin = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<LoanListItemDto>>>;

public sealed class ListLoansQueryValidator : AbstractValidator<ListLoansQuery>
{
    public ListLoansQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Origin).IsInEnum().When(x => x.Origin is not null);
        RuleFor(x => x.Role).Must(r => r is null or "Debtor" or "CoDebtor").WithMessage("El rol es Debtor o CoDebtor.");
        RuleFor(x => x.MinDaysPastDue).GreaterThanOrEqualTo(0).When(x => x.MinDaysPastDue is not null);
    }
}

public sealed class ListLoansQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListLoansQuery, Result<PagedResult<LoanListItemDto>>>
{
    public async Task<Result<PagedResult<LoanListItemDto>>> Handle(ListLoansQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.Loans.AsNoTracking().Where(l => !l.IsDeleted).PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        if (request.Status is { } estado) consulta = consulta.Where(l => l.Status == estado);
        if (request.Origin is { } origen) consulta = consulta.Where(l => l.Origin == origen);
        if (request.Line is { } linea) consulta = consulta.Where(l => db.CreditLines.Any(c => c.Id == l.CreditLineId && c.PublicId == linea));
        if (request.Office is { } oficina) consulta = consulta.Where(l => db.Branches.Any(b => b.Id == l.BranchId && b.PublicId == oficina));
        if (request.PersonPublicId is { } persona)
        {
            var personaId = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.PublicId == persona).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            consulta = request.Role == "CoDebtor"
                ? consulta.Where(l => db.LoanParties.Any(p => p.LoanId == l.Id && p.PersonId == personaId))
                : consulta.Where(l => l.DebtorPersonId == personaId);
        }
        if (!string.IsNullOrWhiteSpace(request.Modality))
        {
            var modalidad = request.Modality.Trim().ToUpperInvariant();
            consulta = consulta.Where(l => db.LoanTermsVersions.Any(t => t.LoanId == l.Id && t.VersionNumber == 1 && t.ModalityCode == modalidad));
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var texto = request.Search.Trim();
            var todas = db.People.AsNoTracking().IgnoreQueryFilters();
            // Feature 017: por documento con el resolutor (un número puede ser de dos personas y en una búsqueda salen las dos) o
            // por el usuario derivado («CE123456»); por nombre como siempre.
            var usuario = UsuarioDeDocumento.NormalizarIdentificador(texto);
            var porDocumento = ResolutorDePersonaPorDocumento.Filtrar(todas, null, texto).Select(p => p.Id);
            var personas = todas
                .Where(p => p.Username == usuario || p.FirstName.Contains(texto) || p.LastName.Contains(texto) || (p.BusinessName != null && p.BusinessName.Contains(texto)))
                .Select(p => p.Id);
            consulta = consulta.Where(l => l.Number.Contains(texto) || porDocumento.Contains(l.DebtorPersonId) || personas.Contains(l.DebtorPersonId));
        }

        // La posición vigente de cada crédito: la última fecha y, en ella, la última versión.
        var filas = await consulta.Select(l => new
        {
            Credito = l,
            Posicion = db.LoanDailyPositions.Where(p => p.LoanId == l.Id).OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version)
                .Select(p => new { p.PositionDate, p.Principal, p.DaysPastDue, p.IsSuspended, p.UsuryCapped }).FirstOrDefault(),
        }).ToListAsync(ct);
        if (request.MinDaysPastDue is { } mora) filas = filas.Where(f => (f.Posicion?.DaysPastDue ?? 0) >= mora).ToList();

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = filas.Count;
        var vista = filas.OrderByDescending(f => f.Credito.OpenedOn).ThenByDescending(f => f.Credito.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList();
        var creditos = vista.Select(f => f.Credito).ToList();
        var (personasDe, lineas, oficinas) = await ReferenciasAsync(db, creditos, ct);
        var desembolsado = await DesembolsadoAsync(db, creditos.Select(c => c.Id).ToList(), ct);

        var items = vista.Select(f =>
        {
            var marcas = new List<string>();
            if (f.Posicion?.IsSuspended == true) marcas.Add("Suspended");
            if (f.Posicion?.UsuryCapped == true) marcas.Add("UsuryCapped");
            if (f.Credito.ValidationFailureCode is not null) marcas.Add("ValidationFailed");
            return new LoanListItemDto(f.Credito.PublicId, f.Credito.Number, personasDe[f.Credito.DebtorPersonId], lineas[f.Credito.CreditLineId],
                oficinas[f.Credito.BranchId], f.Credito.Status.ToString(), f.Credito.Origin.ToString(), desembolsado.GetValueOrDefault(f.Credito.Id).Monto,
                f.Posicion?.Principal ?? 0m, f.Posicion?.DaysPastDue ?? 0, f.Posicion?.PositionDate, null, marcas);
        }).ToList();
        return Result.Success(new PagedResult<LoanListItemDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }

    /// <summary>Deudores, líneas y oficinas de unos créditos, por Id.</summary>
    public static async Task<(Dictionary<int, RefDto> Personas, Dictionary<int, LoanLineDto> Lineas, Dictionary<int, RefDto> Oficinas)> ReferenciasAsync(
        IApplicationDbContext db, IReadOnlyCollection<Loan> creditos, CancellationToken ct, IReadOnlyCollection<int>? otrasPersonas = null)
    {
        var personaIds = creditos.Select(c => c.DebtorPersonId).Concat(otrasPersonas ?? []).Distinct().ToList();
        var lineaIds = creditos.Select(c => c.CreditLineId).Distinct().ToList();
        var oficinaIds = creditos.Select(c => c.BranchId).Distinct().ToList();
        var personas = (await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personaIds.Contains(p.Id)).ToListAsync(ct))
            .ToDictionary(p => p.Id, p => new RefDto(p.PublicId, ReferenciasDeGobierno.Nombre(p)));
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => lineaIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new LoanLineDto(l.PublicId, l.Code, l.Name), ct);
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinaIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new RefDto(b.PublicId, b.Name), ct);
        foreach (var id in personaIds.Where(id => !personas.ContainsKey(id))) personas[id] = new RefDto(Guid.Empty, string.Empty);
        foreach (var id in lineaIds.Where(id => !lineas.ContainsKey(id))) lineas[id] = new LoanLineDto(Guid.Empty, string.Empty, string.Empty);
        foreach (var id in oficinaIds.Where(id => !oficinas.ContainsKey(id))) oficinas[id] = new RefDto(Guid.Empty, string.Empty);
        return (personas, lineas, oficinas);
    }

    /// <summary>Lo desembolsado (tramos vivos) y cuántos tramos, por crédito.</summary>
    public static async Task<Dictionary<int, (decimal Monto, int Tramos)>> DesembolsadoAsync(IApplicationDbContext db, IReadOnlyCollection<int> ids, CancellationToken ct) =>
        (await db.LoanOperations.AsNoTracking()
            .Where(o => ids.Contains(o.LoanId) && o.Kind == LoanOperationKind.Disbursement && o.ReversedByOperationId == null)
            .Select(o => new { o.LoanId, o.Amount }).ToListAsync(ct))
        .GroupBy(o => o.LoanId).ToDictionary(g => g.Key, g => (g.Sum(o => o.Amount), g.Count()));
}
