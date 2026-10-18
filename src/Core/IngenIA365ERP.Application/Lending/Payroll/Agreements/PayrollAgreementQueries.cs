using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payroll.Agreements;

// Feature 013, K2 (US10, bloque B; T0833; api.md §13.1; C84): las consultas de pagadurías y convenios. Los convenios son un catálogo de la
// cooperativa, sin oficina (los pendientes de giro, que sí son de créditos, están en PendingPayrollTransfersQuery.cs con el alcance). Bajo
// PayrollAgreements.View los contactos llevan nombre, papel y contacto institucional, nunca documento (C84). (nuevo)

/// <summary>La pagaduría de un convenio: persona jurídica, su razón social y su NIT (no es dato personal). (nuevo)</summary>
public sealed record PayrollAgreementPayerDto(Guid PersonPublicId, string LegalName, string TaxId);

/// <summary>Lo que rige hoy del convenio (api.md §13.1, <c>currentVersion</c>). (nuevo)</summary>
public sealed record PayrollAgreementCurrentVersionDto(Guid VersionPublicId, DateOnly ValidFrom, int CutoffDay, int? SendDay, int PayDay,
    int? TransferWindowDays);

/// <summary>Un convenio en la lista (api.md §13.1). (nuevo)</summary>
public sealed record PayrollAgreementSummaryDto(
    Guid PublicId,
    string Code,
    string Name,
    PayrollAgreementPayerDto Payer,
    bool IsOwnPayroll,
    PayrollAgreementCurrentVersionDto? CurrentVersion,
    int ActiveLoans,
    bool IsActive,
    string Status);

/// <summary>Un código de respuesta de la pagaduría. (nuevo)</summary>
public sealed record PayrollResultCodeDto(string Code, string Description, string Outcome, string? Reason);

/// <summary>Una versión del convenio con sus formatos y códigos de respuesta. (nuevo)</summary>
public sealed record PayrollAgreementVersionDto(
    Guid PublicId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Periodicity,
    int CutoffDay,
    int? SendDay,
    int PayDay,
    int SendLeadDays,
    Guid OutboundFormatPublicId,
    string OutboundFormatCode,
    Guid ResultFormatPublicId,
    string ResultFormatCode,
    decimal? DeductionCap,
    bool IncludesPastDue,
    IReadOnlyList<string> NoveltyRules,
    int? TransferWindowDays,
    string NotAppliedAction,
    string AlternateRepaymentChannel,
    string? Notes,
    IReadOnlyList<PayrollResultCodeDto> ResultCodes);

/// <summary>Un contacto vigente: nombre, papel y contacto institucional (correo y teléfono del maestro), sin documento (C84). (nuevo)</summary>
public sealed record PayrollAgreementContactDto(Guid PublicId, Guid PersonPublicId, string Name, string Role, bool IsPrimary, string? Email, string? Phone,
    DateOnly ValidFrom);

/// <summary>El convenio completo (api.md §13.1, <c>GET /{id}</c>). (nuevo)</summary>
public sealed record PayrollAgreementDto(
    Guid PublicId,
    string Code,
    string Name,
    PayrollAgreementPayerDto Payer,
    bool IsOwnPayroll,
    string Status,
    bool IsActive,
    PayrollAgreementCurrentVersionDto? CurrentVersion,
    int ActiveLoans,
    IReadOnlyList<PayrollAgreementVersionDto> Versions,
    IReadOnlyList<PayrollAgreementContactDto> Contacts);

/// <summary>Lo armado de los convenios para las respuestas. (nuevo)</summary>
public static class ConsultasDeConvenios
{
    public static async Task<PayrollAgreementDto> DtoAsync(IApplicationDbContext db, int convenioId, DateOnly hoy, CancellationToken ct)
    {
        var c = await db.PayrollAgreements.AsNoTracking().FirstAsync(a => a.Id == convenioId, ct);
        var resumen = (await ResumenesAsync(db, [c], hoy, ct))[0];
        var versiones = await db.PayrollAgreementVersions.AsNoTracking().Where(v => v.PayrollAgreementId == convenioId && !v.IsDeleted)
            .OrderBy(v => v.ValidFrom).ToListAsync(ct);
        var ids = versiones.Select(v => v.Id).ToList();
        var codigos = (await db.PayrollAgreementResultCodes.AsNoTracking().Where(r => ids.Contains(r.PayrollAgreementVersionId) && !r.IsDeleted)
            .OrderBy(r => r.PaymasterCode).ToListAsync(ct)).ToLookup(r => r.PayrollAgreementVersionId);
        var formatos = versiones.SelectMany(v => new[] { v.OutboundFormatId, v.ResultFormatId }).Distinct().ToList();
        var deFormato = await db.BankFileFormats.AsNoTracking().IgnoreQueryFilters().Where(f => formatos.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => (f.PublicId, f.Code), ct);
        var dtos = versiones.Select(v =>
        {
            var reglas = ReglasDelConvenio.Leer(v.NoveltyRulesJson);
            var ida = deFormato.GetValueOrDefault(v.OutboundFormatId);
            var vuelta = deFormato.GetValueOrDefault(v.ResultFormatId);
            return new PayrollAgreementVersionDto(v.PublicId, v.ValidFrom, v.ValidTo, v.Periodicity.ToString(), v.CutoffDay, reglas.SendDay, v.PaymentDay,
                v.SendLeadDays, ida.PublicId, ida.Code ?? string.Empty, vuelta.PublicId, vuelta.Code ?? string.Empty, v.DeductionCapFraction, v.IncludesPastDue,
                reglas.Reports ?? ReglasDelConvenio.TodasLasNovedades, v.TransferWindowDays, v.NonDeductionAction.ToString(),
                v.AlternateRepaymentChannel.ToString(), v.Notes,
                codigos[v.Id].Select(r => new PayrollResultCodeDto(r.PaymasterCode, r.Description, r.Outcome.ToString(), r.NonDeductionReason?.ToString())).ToList());
        }).ToList();
        return new PayrollAgreementDto(c.PublicId, c.Code, resumen.Name, resumen.Payer, c.IsOwnPayroll, c.Status.ToString(), resumen.IsActive,
            resumen.CurrentVersion, resumen.ActiveLoans, dtos, await ContactosAsync(db, convenioId, hoy, ct));
    }

    public static async Task<IReadOnlyList<PayrollAgreementSummaryDto>> ResumenesAsync(IApplicationDbContext db, IReadOnlyList<PayrollAgreement> convenios,
        DateOnly hoy, CancellationToken ct)
    {
        var ids = convenios.Select(c => c.Id).ToList();
        var pagadurias = convenios.Select(c => c.PaymasterPersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => pagadurias.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var versiones = (await db.PayrollAgreementVersions.AsNoTracking().Where(v => ids.Contains(v.PayrollAgreementId) && !v.IsDeleted).ToListAsync(ct))
            .ToLookup(v => v.PayrollAgreementId);
        var vinculos = (await db.LoanPayrollLinks.AsNoTracking()
                .Where(v => v.PayrollAgreementId != null && ids.Contains(v.PayrollAgreementId.Value) && !v.IsDeleted && v.ValidTo == null)
                .Join(db.Loans.AsNoTracking(), v => v.LoanId, l => l.Id, (v, l) => new { v.PayrollAgreementId, l.Status })
                .Where(x => x.Status == LoanStatus.Active || x.Status == LoanStatus.WrittenOff)
                .ToListAsync(ct))
            .GroupBy(x => x.PayrollAgreementId!.Value).ToDictionary(g => g.Key, g => g.Count());
        return convenios.Select(c =>
        {
            var p = personas.GetValueOrDefault(c.PaymasterPersonId);
            var pagador = new PayrollAgreementPayerDto(p?.PublicId ?? Guid.Empty, p is null ? string.Empty : ReferenciasDeGobierno.Nombre(p), p?.TaxId ?? string.Empty);
            var vigente = PayrollAgreementVersion.Vigente(versiones[c.Id], hoy);
            return new PayrollAgreementSummaryDto(c.PublicId, c.Code, c.Description ?? c.Code, pagador, c.IsOwnPayroll, Actual(vigente),
                vinculos.GetValueOrDefault(c.Id), c.Operable, c.Status.ToString());
        }).ToList();
    }

    public static PayrollAgreementCurrentVersionDto? Actual(PayrollAgreementVersion? v) =>
        v is null ? null : new(v.PublicId, v.ValidFrom, v.CutoffDay, ReglasDelConvenio.Leer(v.NoveltyRulesJson).SendDay, v.PaymentDay, v.TransferWindowDays);

    public static async Task<IReadOnlyList<PayrollAgreementContactDto>> ContactosAsync(IApplicationDbContext db, int convenioId, DateOnly hoy, CancellationToken ct)
    {
        var contactos = (await db.PayrollAgreementContacts.AsNoTracking().Where(c => c.PayrollAgreementId == convenioId && !c.IsDeleted).ToListAsync(ct))
            .Where(c => c.VigenteEn(hoy)).ToList();
        var ids = contactos.Select(c => c.PersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        return contactos.OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Role, StringComparer.Ordinal).Select(c =>
        {
            var p = personas[c.PersonId];
            return new PayrollAgreementContactDto(c.PublicId, p.PublicId, ReferenciasDeGobierno.Nombre(p), c.Role, c.IsPrimary, p.Email,
                p.Phone1 ?? p.Mobile, c.ValidFrom);
        }).ToList();
    }
}

/// <summary>Los convenios (api.md §13.1, <c>GET /api/lending/payroll-agreements?includeInactive=</c>, <c>PayrollAgreements.View</c>). (nuevo)</summary>
public sealed record ListPayrollAgreementsQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<PayrollAgreementSummaryDto>>>;

public sealed class ListPayrollAgreementsQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListPayrollAgreementsQuery, Result<IReadOnlyList<PayrollAgreementSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<PayrollAgreementSummaryDto>>> Handle(ListPayrollAgreementsQuery request, CancellationToken ct)
    {
        var consulta = db.PayrollAgreements.AsNoTracking().Where(a => !a.IsDeleted);
        if (!request.IncludeInactive) consulta = consulta.Where(a => a.Status == PayrollAgreementStatus.Active);
        var convenios = await consulta.OrderBy(a => a.Code).ToListAsync(ct);
        return Result.Success(await ConsultasDeConvenios.ResumenesAsync(db, convenios, reloj.HoyLocal, ct));
    }
}

/// <summary>Un convenio con versiones, contactos y códigos de respuesta (api.md §13.1, <c>GET /{id}</c>, <c>PayrollAgreements.View</c>). (nuevo)</summary>
public sealed record GetPayrollAgreementQuery(Guid AgreementPublicId) : IRequest<Result<PayrollAgreementDto>>;

public sealed class GetPayrollAgreementQueryValidator : AbstractValidator<GetPayrollAgreementQuery>
{
    public GetPayrollAgreementQueryValidator() => RuleFor(x => x.AgreementPublicId).NotEmpty();
}

public sealed class GetPayrollAgreementQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<GetPayrollAgreementQuery, Result<PayrollAgreementDto>>
{
    public async Task<Result<PayrollAgreementDto>> Handle(GetPayrollAgreementQuery request, CancellationToken ct)
    {
        var id = await db.PayrollAgreements.AsNoTracking().Where(a => a.PublicId == request.AgreementPublicId && !a.IsDeleted).Select(a => (int?)a.Id)
            .FirstOrDefaultAsync(ct);
        return id is null
            ? Result.Failure<PayrollAgreementDto>(ErroresDeLibranza.ConvenioInexistente())
            : Result.Success(await ConsultasDeConvenios.DtoAsync(db, id.Value, reloj.HoyLocal, ct));
    }
}
