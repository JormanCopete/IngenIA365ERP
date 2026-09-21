using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.ServiceBonus;

/// <summary>Una fila de la lista de primas (contracts/api.md §3.1 <c>GET /service-bonus</c>): todas las versiones, de la más reciente a la más vieja.</summary>
public sealed record ServiceBonusRunListItemDto(
    Guid RunPublicId,
    int Year,
    int Semester,
    int Version,
    string Status,
    DateOnly CutoffDate,
    DateOnly? PayDate,
    int Employees,
    decimal Total,
    DateTime CalculatedAt,
    string CalculatedBy,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    DateTime? ReversedAt,
    DateTime? DiscardedAt,
    int PaidCount,
    Guid? PostedDocumentPublicId,
    string? PostedDocumentNumber);

/// <summary>Lista las corridas de prima de servicios, opcionalmente de un año o en un estado (<c>Draft</c>, <c>Approved</c>, <c>Reversed</c>, <c>Superseded</c>).</summary>
public sealed record ListServiceBonusRunsQuery(int? Year = null, string? Status = null) : IRequest<Result<IReadOnlyList<ServiceBonusRunListItemDto>>>;

public sealed class ListServiceBonusRunsQueryValidator : AbstractValidator<ListServiceBonusRunsQuery>
{
    public ListServiceBonusRunsQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100).When(x => x.Year is not null).WithMessage("El año no es válido.");
        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse<PayrollRunStatus>(s, ignoreCase: true, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("El estado no existe. Admite: " + string.Join(", ", Enum.GetNames<PayrollRunStatus>()) + ".");
    }
}

public sealed class ListServiceBonusRunsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListServiceBonusRunsQuery, Result<IReadOnlyList<ServiceBonusRunListItemDto>>>
{
    public async Task<Result<IReadOnlyList<ServiceBonusRunListItemDto>>> Handle(ListServiceBonusRunsQuery request, CancellationToken ct)
    {
        var query = db.PayrollRuns.AsNoTracking().Where(r => r.Kind == PayrollRunKind.ServiceBonus);
        if (request.Year is { } year) query = query.Where(r => r.Year == year);
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<PayrollRunStatus>(request.Status, ignoreCase: true, out var status))
            query = query.Where(r => r.Status == status);

        var runs = await query
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Semester).ThenByDescending(r => r.Version)
            .ToListAsync(ct);
        if (runs.Count == 0) return Result.Success<IReadOnlyList<ServiceBonusRunListItemDto>>([]);

        var ids = runs.Select(r => r.Id).ToList();
        var pagados = await (
            from p in db.PayrollPayments.AsNoTracking()
            join re in db.PayrollRunEmployees.AsNoTracking() on p.PayrollRunEmployeeId equals re.Id
            where ids.Contains(re.PayrollRunId) && !p.IsReverted
            group p by re.PayrollRunId into g
            select new { RunId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RunId, x => x.Count, ct);

        var idsDocumento = runs.Where(r => r.AccountingDocumentId != null).Select(r => r.AccountingDocumentId!.Value).Distinct().ToList();
        var documentos = idsDocumento.Count == 0
            ? new Dictionary<long, (Guid PublicId, string Numero)>()
            : await db.AccountingDocuments.AsNoTracking()
                .Where(d => idsDocumento.Contains(d.Id))
                .Select(d => new { d.Id, d.PublicId, Codigo = d.VoucherType!.Code, d.Number })
                .ToDictionaryAsync(d => d.Id, d => (PublicId: d.PublicId, Numero: $"{d.Codigo}-{d.Number}"), ct);

        var filas = runs.Select(r =>
        {
            var documento = r.AccountingDocumentId is { } docId && documentos.TryGetValue(docId, out var d) ? d : default((Guid PublicId, string Numero)?);
            return new ServiceBonusRunListItemDto(
                r.PublicId, r.Year ?? 0, r.Semester ?? 0, r.Version, r.Status.ToString(),
                r.CutoffDate ?? default, r.PayDate, r.EmployeeCount, r.TotalNet, r.CalculatedAt, r.CalculatedBy,
                r.ApprovedAt, r.ApprovedBy, r.ReversedAt, r.DiscardedAt,
                pagados.GetValueOrDefault(r.Id), documento?.PublicId, documento?.Numero);
        }).ToList();

        return Result.Success<IReadOnlyList<ServiceBonusRunListItemDto>>(filas);
    }
}
