using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Documents;

/// <summary>Un documento del lote: a nombre de quién, de qué cuenta (el extracto), el documento emitido o el motivo por el que no se emitió. (nuevo)</summary>
public sealed record DocumentBatchItemDto(Guid PersonPublicId, string PersonName, string? AccountNumber, Guid? DocumentPublicId, string? Number,
    bool AlreadyIssued, string? ErrorCode, string? Message);

/// <summary>El resultado del lote: cuántos se pidieron, cuántos se emitieron, cuántos ya estaban y cuántos no salieron, con el detalle. (nuevo)</summary>
public sealed record DocumentBatchResultDto(string Kind, DateOnly PeriodFrom, DateOnly PeriodTo, int Requested, int Issued, int AlreadyIssued, int NotIssued,
    IReadOnlyList<DocumentBatchItemDto> Items);

/// <summary>
/// <c>POST /api/lending/documents/batches</c> (feature 014, US13, T196; contracts/api.md §17, documentos.md §2.3; <c>Lending.Documents.Issue</c>, con clave): la
/// emisión masiva de documentos al titular de captaciones:
/// <list type="bullet">
/// <item><c>DepositTaxCertificate</c> del año terminado: uno por persona con productos de captación o aportes en el año (titular o cotitular);</item>
/// <item><c>WithholdingCertificate</c> del año (o del período pedido): uno por persona;</item>
/// <item><c>AccountStatement</c> (research E175, criterio del dueño): el extracto del último período terminado según
/// <c>Documentos.PeriodicidadDeExtractos</c> (mensual, trimestral, semestral o anual; informativa: el lote se corre a pedido) de cada cuenta viva, a nombre
/// de su titular.</item>
/// </list>
/// Cada documento sale por <see cref="IssueLendingDocumentCommand"/> —su número, su código, su alcance, la misma copia del extracto y su auditoría— en su
/// propia transacción: uno que no puede emitirse («nada que certificar», fuera del alcance) queda en el detalle y el lote sigue. Corre dentro de la
/// petición y responde el resumen (research E177: no abre una corrida de la Cartera, que impediría el ciclo diario mientras dura). <c>personPublicIds</c>
/// acota a esas personas. (nuevo)
/// </summary>
public sealed record IssueLendingDocumentBatchCommand(
    IssuedDocumentKind Kind,
    short? Year,
    PeriodoDeDocumento? Period,
    IReadOnlyList<Guid>? PersonPublicIds,
    DocumentDeliveryMode Delivery) : IRequest<Result<DocumentBatchResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }

    /// <summary>Las clases que admite el lote.</summary>
    public static bool Admite(IssuedDocumentKind clase) =>
        clase is IssuedDocumentKind.DepositTaxCertificate or IssuedDocumentKind.WithholdingCertificate or IssuedDocumentKind.AccountStatement;
}

public sealed class IssueLendingDocumentBatchCommandValidator : AbstractValidator<IssueLendingDocumentBatchCommand>
{
    public IssueLendingDocumentBatchCommandValidator()
    {
        RuleFor(x => x.Kind).Must(IssueLendingDocumentBatchCommand.Admite)
            .WithMessage("El lote emite certificados tributarios, certificados de retención o extractos de cuenta.");
        RuleFor(x => x.Delivery).IsInEnum();
        RuleFor(x => x.Year).InclusiveBetween((short)2000, (short)2100).When(x => x.Year is not null).WithMessage("El año no es válido.");
        RuleFor(x => x.Period).Must(p => p is null || p.From <= p.To).WithMessage("La fecha inicial del período no puede ser posterior a la final.");
    }
}

public sealed class IssueLendingDocumentBatchCommandHandler(
    IApplicationDbContext db,
    ISender sender,
    ReglasDeParametrosDeCartera parametros,
    IAlcanceDeCartera alcance,
    IDateTimeService reloj) : IRequestHandler<IssueLendingDocumentBatchCommand, Result<DocumentBatchResultDto>>
{
    public async Task<Result<DocumentBatchResultDto>> Handle(IssueLendingDocumentBatchCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        PeriodoDeDocumento periodo;
        short? año = null;
        if (request.Kind == IssuedDocumentKind.AccountStatement)
            periodo = request.Period ?? UltimoPeriodoTerminado(await PeriodicidadAsync(hoy, ct), hoy);
        else
        {
            año = request.Year ?? (short)(hoy.Year - 1);
            if (año >= hoy.Year) return Result.Failure<DocumentBatchResultDto>(DocumentErrors.AñoNoTerminado(año.Value));
            periodo = request.Kind == IssuedDocumentKind.WithholdingCertificate && request.Period is { } p
                ? p
                : new PeriodoDeDocumento(new DateOnly(año.Value, 1, 1), new DateOnly(año.Value, 12, 31));
        }

        var filtro = request.PersonPublicIds is { Count: > 0 } ids ? ids.ToHashSet() : null;
        // El lote sólo recorre las cuentas del alcance de quien lo corre; cada emisión vuelve a exigirlo (la de la persona, sobre todas sus cuentas).
        var vigente = await alcance.ObtenerAsync(ct);
        var pedidos = request.Kind == IssuedDocumentKind.AccountStatement
            ? await CuentasAsync(vigente, periodo, filtro, ct)
            : (await PersonasAsync(vigente, periodo, filtro, ct)).Select(x => (x.Persona, x.Nombre, (Guid?)null, (string?)null)).ToList();

        var items = new List<DocumentBatchItemDto>(pedidos.Count);
        foreach (var (persona, nombre, cuenta, numeroDeCuenta) in pedidos)
        {
            var comando = new IssueLendingDocumentCommand(request.Kind, persona, null, null,
                request.Kind == IssuedDocumentKind.DepositTaxCertificate ? null : periodo,
                request.Kind == IssuedDocumentKind.DepositTaxCertificate ? año : null, request.Delivery)
            {
                OperationKey = Guid.NewGuid(),
                AccountPublicId = cuenta,
            };
            var r = await sender.Send(comando, ct);
            items.Add(r.IsSuccess
                ? new DocumentBatchItemDto(persona, nombre, numeroDeCuenta, r.Value.DocumentPublicId, r.Value.Number, r.Value.AlreadyIssued, null, null)
                : new DocumentBatchItemDto(persona, nombre, numeroDeCuenta, null, null, false, r.Error.Code, r.Error.Message));
        }
        return Result.Success(new DocumentBatchResultDto(request.Kind.ToString(), periodo.From, periodo.To, items.Count,
            items.Count(i => i.DocumentPublicId is not null && !i.AlreadyIssued), items.Count(i => i.AlreadyIssued), items.Count(i => i.DocumentPublicId is null),
            items));
    }

    /// <summary>Las personas con productos de captación o aportes vivos en el período (titular o cotitular), en orden de nombre.</summary>
    private async Task<List<(Guid Persona, string Nombre)>> PersonasAsync(AlcanceDeCartera vigente, PeriodoDeDocumento periodo, IReadOnlySet<Guid>? filtro,
        CancellationToken ct)
    {
        var cuentas = db.MemberAccounts.AsNoTracking().Where(a => !a.IsDeleted && a.Status != MemberAccountStatus.Opening && a.OpenedOn <= periodo.To
                                                                   && (a.ClosedOn == null || a.ClosedOn >= periodo.From))
            .PorOficinaYLinea(vigente, a => a.BranchId, a => a.CreditLineId);
        var titulares = cuentas.Select(a => a.HolderPersonId);
        var cotitulares = db.MemberAccountParties.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Role == MemberAccountPartyRole.CoHolder && x.ValidFrom <= periodo.To && (x.ValidTo == null || x.ValidTo >= periodo.From)
                        && cuentas.Select(a => a.Id).Contains(x.MemberAccountId))
            .Select(x => x.PersonId);
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => titulares.Contains(p.Id) || cotitulares.Contains(p.Id)).ToListAsync(ct);
        return personas.Where(p => filtro is null || filtro.Contains(p.PublicId))
            .Select(p => (p.PublicId, ReferenciasDeGobierno.Nombre(p))).OrderBy(x => x.Item2, StringComparer.CurrentCulture).ToList();
    }

    /// <summary>Las cuentas vivas en el período con su titular (el extracto va a nombre del titular), en orden de número.</summary>
    private async Task<List<(Guid Persona, string Nombre, Guid? Cuenta, string? Numero)>> CuentasAsync(AlcanceDeCartera vigente, PeriodoDeDocumento periodo,
        IReadOnlySet<Guid>? filtro, CancellationToken ct)
    {
        var cuentas = await (from a in db.MemberAccounts.AsNoTracking().PorOficinaYLinea(vigente, a => a.BranchId, a => a.CreditLineId)
                             join p in db.People.AsNoTracking().IgnoreQueryFilters() on a.HolderPersonId equals p.Id
                             where !a.IsDeleted && a.Status != MemberAccountStatus.Opening && a.OpenedOn <= periodo.To
                                   && (a.ClosedOn == null || a.ClosedOn >= periodo.From)
                             orderby a.Number
                             select new { a.PublicId, a.Number, Persona = p }).ToListAsync(ct);
        return cuentas.Where(c => filtro is null || filtro.Contains(c.Persona.PublicId))
            .Select(c => (c.Persona.PublicId, ReferenciasDeGobierno.Nombre(c.Persona), (Guid?)c.PublicId, (string?)c.Number)).ToList();
    }

    private async Task<string> PeriodicidadAsync(DateOnly hoy, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.DocumentosPeriodicidadDeExtractos, hoy, ct: ct);
        return leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto.Trim() : "Mensual";
    }

    /// <summary>El último período terminado antes de <paramref name="hoy"/> según la periodicidad (meses calendario: 1, 3, 6 o 12).</summary>
    public static PeriodoDeDocumento UltimoPeriodoTerminado(string periodicidad, DateOnly hoy)
    {
        var meses = periodicidad switch
        {
            "Trimestral" => 3,
            "Semestral" => 6,
            "Anual" => 12,
            _ => 1,
        };
        var inicioDelActual = new DateOnly(hoy.Year, ((hoy.Month - 1) / meses * meses) + 1, 1);
        return new PeriodoDeDocumento(inicioDelActual.AddMonths(-meses), inicioDelActual.AddDays(-1));
    }
}
