using FluentValidation;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Channel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Service;

// Feature 021, US10 (T133; FR-012, FR-056; contracts/erp-api-de-servicio.md, «due-installments»): las cuotas que vencen dentro de N días para las
// personas habilitadas en el canal, para el aviso de cuota próxima que manda el canal una vez al día. Sólo de los conceptos publicados (créditos y
// cuentas con cuota), sin saldos ni datos personales: el canal sólo necesita a quién avisar, de qué y cuánto.

/// <summary>Una cuota próxima de una persona habilitada.</summary>
public sealed record DueInstallmentDto(Guid PersonPublicId, string ConceptKey, string Description, DateOnly DueDate, decimal Amount);

/// <summary><c>GET /tenants/{t}/due-installments?inDays=</c>: las cuotas con vencimiento exactamente hoy + <see cref="InDays"/> (hora de Colombia).</summary>
public sealed record ListDueInstallmentsQuery(int InDays) : IRequest<Result<IReadOnlyList<DueInstallmentDto>>>;

public sealed class ListDueInstallmentsQueryValidator : AbstractValidator<ListDueInstallmentsQuery>
{
    public ListDueInstallmentsQueryValidator() => RuleFor(x => x.InDays).InclusiveBetween(0, 15);
}

public sealed class ListDueInstallmentsQueryHandler(IApplicationDbContext db, IConsultaDeObligaciones consulta, IContratoDeCuentas cuentas, IDateTimeService reloj)
    : IRequestHandler<ListDueInstallmentsQuery, Result<IReadOnlyList<DueInstallmentDto>>>
{
    /// <summary>De a cuántas personas se pregunta a Cartera y Captaciones.</summary>
    public const int Tanda = 500;

    public async Task<Result<IReadOnlyList<DueInstallmentDto>>> Handle(ListDueInstallmentsQuery request, CancellationToken ct)
    {
        var dia = reloj.HoyLocal.AddDays(request.InDays);
        var publicados = await db.ChannelPublishedConcepts.AsNoTracking().Select(c => new { c.Kind, c.LineCode }).ToListAsync(ct);
        bool Publicado(ConceptKind clase, string linea) =>
            publicados.Any(p => p.Kind == clase && (p.LineCode == null || string.Equals(p.LineCode, linea, StringComparison.OrdinalIgnoreCase)));
        if (publicados.Count == 0) return Result.Success<IReadOnlyList<DueInstallmentDto>>([]);

        var personas = await (from e in db.ChannelEnablements.AsNoTracking()
                              join p in db.People.AsNoTracking() on e.PersonId equals p.Id
                              where e.Status == EnablementStatus.Enabled
                              select p.PublicId).ToListAsync(ct);
        var salida = new List<DueInstallmentDto>();
        foreach (var tanda in personas.Chunk(Tanda))
        {
            if (publicados.Any(p => p.Kind == ConceptKind.Loan))
            {
                var deCreditos = await consulta.CuotasDeNominaAsync(new ConsultaDeCuotasDeNomina(tanda, dia, dia, TodosLosCanales: true), ct);
                salida.AddRange(deCreditos.Installments
                    .Where(c => c.DueDate == dia && Publicado(ConceptKind.Loan, c.CreditLineCode))
                    .GroupBy(c => (c.PersonPublicId, c.ObligationPublicId))
                    .Select(g => new DueInstallmentDto(g.Key.PersonPublicId, ClaveDeConcepto.DeCredito(g.Key.ObligationPublicId),
                        $"Crédito {g.First().ObligationNumber}", dia, g.Sum(c => c.Amount))));
            }
            var deCuentas = await cuentas.ListarExigiblesAsync(new SolicitudDeExigibles(ClavesDeRecaudo.Recaudador, tanda, dia, dia, dia), ct);
            salida.AddRange((deCuentas.Value?.Installments ?? [])
                .Where(c => c.DueDate == dia && c.Pending > 0m && ClaveDeConcepto.ClaseDeCuenta(c.Nature, c.DepositClass) is { } clase && Publicado(clase, c.LineCode))
                .GroupBy(c => (c.PersonPublicId, c.AccountPublicId))
                .Select(g => new DueInstallmentDto(g.Key.PersonPublicId, ClaveDeConcepto.DeCuenta(g.Key.AccountPublicId),
                    $"{ClaveDeConcepto.Nombre(ClaveDeConcepto.ClaseDeCuenta(g.First().Nature, g.First().DepositClass)!.Value)} {g.First().AccountNumber}", dia,
                    g.Sum(c => c.Pending))));
        }
        return Result.Success<IReadOnlyList<DueInstallmentDto>>(salida);
    }
}
