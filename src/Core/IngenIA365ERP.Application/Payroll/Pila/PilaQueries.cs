using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.DownloadAttachment;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Pila;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.Extensions.Options;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Pila;

// ------------------------------------------------------------ ajustes y layouts --

public sealed record GetPilaSettingsQuery : IRequest<Result<PilaSettingsDto>>;

public sealed class GetPilaSettingsQueryValidator : AbstractValidator<GetPilaSettingsQuery>;

public sealed class GetPilaSettingsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPilaSettingsQuery, Result<PilaSettingsDto>>
{
    public async Task<Result<PilaSettingsDto>> Handle(GetPilaSettingsQuery request, CancellationToken ct)
    {
        var s = await db.PilaSettings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        var nit = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).Select(c => c.TaxId).FirstOrDefaultAsync(ct);
        return Result.Success(PilaSettingsMapper.ToDto(s, nit));
    }
}

public sealed record ListPilaLayoutsQuery : IRequest<Result<IReadOnlyList<PilaLayoutDto>>>;

public sealed class ListPilaLayoutsQueryValidator : AbstractValidator<ListPilaLayoutsQuery>;

public sealed class ListPilaLayoutsQueryHandler : IRequestHandler<ListPilaLayoutsQuery, Result<IReadOnlyList<PilaLayoutDto>>>
{
    public Task<Result<IReadOnlyList<PilaLayoutDto>>> Handle(ListPilaLayoutsQuery request, CancellationToken ct) =>
        Task.FromResult(Result.Success<IReadOnlyList<PilaLayoutDto>>(PilaLayoutCatalog.All.OrderByDescending(l => l.ValidFrom).Select(l =>
            new PilaLayoutDto(l.Code, l.Version, l.ValidFrom, l.ValidTo, l.Type1.Fields.Count, l.Type1.Length, l.Type2.Fields.Count, l.Type2.Length, l.Source, l.IsVerified,
                l.Records.Sum(r => r.Fields.Count(f => !f.Verified)))).ToList()));
}

public sealed record GetPilaDueDateQuery(short Year, byte Month) : IRequest<Result<PilaDueDateDto>>;

public sealed class GetPilaDueDateQueryValidator : AbstractValidator<GetPilaDueDateQuery>
{
    public GetPilaDueDateQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween((short)2000, (short)2100);
        RuleFor(x => x.Month).InclusiveBetween((byte)1, (byte)12);
    }
}

public sealed class GetPilaDueDateQueryHandler(IApplicationDbContext db, PreparacionDePila preparacion) : IRequestHandler<GetPilaDueDateQuery, Result<PilaDueDateDto>>
{
    public async Task<Result<PilaDueDateDto>> Handle(GetPilaDueDateQuery request, CancellationToken ct)
    {
        var inicio = new DateTime(request.Year, request.Month, 1);
        var parametros = await db.PayrollLegalParameters.AsNoTracking().Include(p => p.Ranges)
            .Where(p => p.Code == PilaParameterCodes.PaymentDeadlineByNitTable && p.ValidFrom <= inicio && (p.ValidTo == null || p.ValidTo >= inicio)).ToListAsync(ct);
        var nit = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).Select(c => c.TaxId).FirstOrDefaultAsync(ct);
        var dto = await preparacion.FechaLimiteAsync(request.Year, request.Month, nit, new ParameterSet(parametros, inicio), ct);
        return Result.Success(dto!);
    }
}

// --------------------------------------------------------------------- validar --

/// <summary>FR-025: la validación previa sin guardar nada.</summary>
public sealed record ValidatePilaQuery(short Year, byte Month) : IRequest<Result<PilaValidationDto>>;

public sealed class ValidatePilaQueryValidator : AbstractValidator<ValidatePilaQuery>
{
    public ValidatePilaQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween((short)2000, (short)2100);
        RuleFor(x => x.Month).InclusiveBetween((byte)1, (byte)12);
    }
}

public sealed class ValidatePilaQueryHandler(PreparacionDePila preparacion) : IRequestHandler<ValidatePilaQuery, Result<PilaValidationDto>>
{
    public async Task<Result<PilaValidationDto>> Handle(ValidatePilaQuery request, CancellationToken ct)
    {
        var prep = await preparacion.PrepararAsync(request.Year, request.Month, ct);
        if (prep.IsFailure) return Result.Failure<PilaValidationDto>(prep.Error);
        var p = prep.Value;
        var ajustes = PilaSettingsMapper.ToDto(p.Load.Settings, p.Load.CompanyNit);
        var issues = p.Issues.ToList();
        if (!ajustes.Complete)
            issues.Insert(0, new PilaIssueDto(PilaIssueSeverity.Blocking, "Pila.AportanteIncompleto", null, $"Faltan datos del aportante: {string.Join(", ", ajustes.Missing)}.", null, null, "/nomina/pila?ajustes=1"));
        var bloqueantes = issues.Count(i => i.Severity == PilaIssueSeverity.Blocking);
        return Result.Success(new PilaValidationDto(bloqueantes == 0, bloqueantes, issues.Count - bloqueantes, issues, p.Load.Sources, p.Result.ContributorCount, p.Result.LineCount));
    }
}

// ------------------------------------------------------------------- consultas --

public sealed record ListPilaGenerationsQuery(short? Year = null, byte? Month = null) : IRequest<Result<IReadOnlyList<PilaGenerationSummaryDto>>>;

public sealed class ListPilaGenerationsQueryValidator : AbstractValidator<ListPilaGenerationsQuery>;

public sealed class ListPilaGenerationsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListPilaGenerationsQuery, Result<IReadOnlyList<PilaGenerationSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<PilaGenerationSummaryDto>>> Handle(ListPilaGenerationsQuery request, CancellationToken ct)
    {
        var q = db.PilaGenerations.AsNoTracking();
        if (request.Year is { } y) q = q.Where(g => g.Year == y);
        if (request.Month is { } m) q = q.Where(g => g.Month == m);
        var lista = await q.OrderByDescending(g => g.Year).ThenByDescending(g => g.Month).ThenByDescending(g => g.Version).Take(200).ToListAsync(ct);
        return Result.Success<IReadOnlyList<PilaGenerationSummaryDto>>(lista.Select(PilaMappers.Resumen).ToList());
    }
}

public sealed record GetPilaGenerationQuery(Guid GenerationPublicId) : IRequest<Result<PilaGenerationDetailDto>>;

public sealed class GetPilaGenerationQueryValidator : AbstractValidator<GetPilaGenerationQuery>
{
    public GetPilaGenerationQueryValidator() => RuleFor(x => x.GenerationPublicId).NotEmpty();
}

public sealed class GetPilaGenerationQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPilaGenerationQuery, Result<PilaGenerationDetailDto>>
{
    public async Task<Result<PilaGenerationDetailDto>> Handle(GetPilaGenerationQuery request, CancellationToken ct)
    {
        var g = await db.PilaGenerations.AsNoTracking().Include(x => x.Issues).FirstOrDefaultAsync(x => x.PublicId == request.GenerationPublicId, ct);
        if (g is null) return Result.Failure<PilaGenerationDetailDto>(PilaErrors.GenerationNotFound);
        var lineas = await (
            from l in db.PilaGenerationLines.AsNoTracking()
            join e in db.Employees.AsNoTracking() on l.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where l.GenerationId == g.Id
            orderby l.LineNumber
            select new { Line = l, e.PublicId, p.FirstName, p.LastName, p.SecondLastName, p.TaxId }).ToListAsync(ct);
        var empleados = await (
            from e in db.Employees.AsNoTracking() join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where g.Issues.Select(i => i.EmployeeId).Contains(e.Id)
            select new { e.Id, e.PublicId, Nombre = p.FirstName + " " + p.LastName }).ToDictionaryAsync(x => x.Id, ct);

        var dtoLineas = lineas.Select(x => new PilaLineDto(
            x.Line.LineNumber, x.PublicId, $"{x.FirstName} {x.LastName} {x.SecondLastName}".Trim(), x.TaxId, x.Line.ContributorType, x.Line.ContributorSubType,
            x.Line.NoveltyFlags.Split(',', StringSplitOptions.RemoveEmptyEntries),
            x.Line.DaysPension, x.Line.DaysHealth, x.Line.DaysWorkRisk, x.Line.DaysFamilyCompensation, x.Line.Salary,
            x.Line.IbcPension, x.Line.IbcHealth, x.Line.IbcWorkRisk, x.Line.IbcFamilyCompensation,
            x.Line.Pension, x.Line.SolidarityFund + x.Line.SubsistenceFund, x.Line.Health, x.Line.WorkRisk, x.Line.FamilyCompensation, x.Line.Sena, x.Line.Icbf,
            x.Line.Pension + x.Line.SolidarityFund + x.Line.SubsistenceFund + x.Line.Health + x.Line.WorkRisk + x.Line.FamilyCompensation + x.Line.Sena + x.Line.Icbf,
            x.Line.Exempt, JsonSerializer.Deserialize<Dictionary<string, string>>(x.Line.FieldsJson) ?? [])).ToList();
        var issues = g.Issues.OrderBy(i => i.Severity).Select(i => new PilaIssueDto(i.Severity, i.Code, i.FieldNumber, i.Message,
            i.EmployeeId is { } eid && empleados.TryGetValue(eid, out var emp) ? emp.PublicId : null,
            i.EmployeeId is { } eid2 && empleados.TryGetValue(eid2, out var emp2) ? emp2.Nombre : null, i.LinkRoute)).ToList();
        var cuadre = JsonSerializer.Deserialize<PilaReconciliationDto>(g.ReconciliationJson, GeneratePilaCommandHandler.JsonWeb) ?? new PilaReconciliationDto([], g.Balanced, null);
        var fuentes = JsonSerializer.Deserialize<List<PilaSourceRunDto>>(g.SourceRunsJson, GeneratePilaCommandHandler.JsonWeb) ?? [];
        return Result.Success(new PilaGenerationDetailDto(PilaMappers.Resumen(g), dtoLineas, issues, cuadre, fuentes, g.ExemptionApplied));
    }
}

public sealed record GetPilaLineExplanationQuery(Guid GenerationPublicId, int LineNumber) : IRequest<Result<PilaLineExplanationDto>>;

public sealed class GetPilaLineExplanationQueryValidator : AbstractValidator<GetPilaLineExplanationQuery>
{
    public GetPilaLineExplanationQueryValidator()
    {
        RuleFor(x => x.GenerationPublicId).NotEmpty();
        RuleFor(x => x.LineNumber).GreaterThan(0);
    }
}

public sealed class GetPilaLineExplanationQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPilaLineExplanationQuery, Result<PilaLineExplanationDto>>
{
    public async Task<Result<PilaLineExplanationDto>> Handle(GetPilaLineExplanationQuery request, CancellationToken ct)
    {
        var g = await db.PilaGenerations.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.GenerationPublicId, ct);
        if (g is null) return Result.Failure<PilaLineExplanationDto>(PilaErrors.GenerationNotFound);
        var x = await (
            from l in db.PilaGenerationLines.AsNoTracking()
            join e in db.Employees.AsNoTracking() on l.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where l.GenerationId == g.Id && l.LineNumber == request.LineNumber
            select new { Line = l, e.PublicId, p.FirstName, p.LastName }).FirstOrDefaultAsync(ct);
        if (x is null) return Result.Failure<PilaLineExplanationDto>(PilaErrors.LineNotFound);

        var layout = PilaLayoutCatalog.ByCode(g.LayoutVersion) ?? PilaLayoutCatalog.ForPeriod(new DateOnly(g.Year, g.Month, 1));
        var campos = JsonSerializer.Deserialize<Dictionary<string, string>>(x.Line.FieldsJson) ?? [];
        var explicaciones = JsonSerializer.Deserialize<List<PilaFieldExplanation>>(x.Line.ExplanationJson, GeneratePilaCommandHandler.JsonWeb) ?? [];
        var porCampo = explicaciones.ToLookup(e => e.Field);
        var lista = new List<PilaFieldExplanationDto>();
        foreach (var f in (layout?.Type2.Fields ?? []).OrderBy(f => f.Number))
        {
            var valor = campos.GetValueOrDefault(f.Number.ToString()) ?? string.Empty;
            var ex = porCampo[f.Number].ToList();
            var detalle = ex.Count == 0 ? (f.Rules.Count > 0 ? string.Join("; ", f.Rules) : null) : string.Join(" → ", ex.Select(e => e.Detail));
            lista.Add(new PilaFieldExplanationDto(f.Number, f.Name, valor, ex.Count > 0 ? ex[^1].Source : f.Source, detalle, ex.Count > 0 ? ex[^1].Value : null));
        }
        return Result.Success(new PilaLineExplanationDto(x.Line.LineNumber, x.PublicId, $"{x.FirstName} {x.LastName}", x.Line.RecordText, lista));
    }
}

public sealed record DownloadPilaFileQuery(Guid GenerationPublicId, bool AcknowledgeDifference = false) : IRequest<Result<PilaDownloadDto>>;

public sealed class DownloadPilaFileQueryValidator : AbstractValidator<DownloadPilaFileQuery>
{
    public DownloadPilaFileQueryValidator() => RuleFor(x => x.GenerationPublicId).NotEmpty();
}

public sealed class DownloadPilaFileQueryHandler(IApplicationDbContext db, ISender sender) : IRequestHandler<DownloadPilaFileQuery, Result<PilaDownloadDto>>
{
    public async Task<Result<PilaDownloadDto>> Handle(DownloadPilaFileQuery request, CancellationToken ct)
    {
        var g = await db.PilaGenerations.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.GenerationPublicId, ct);
        if (g is null) return Result.Failure<PilaDownloadDto>(PilaErrors.GenerationNotFound);
        if (g.FileAttachmentPublicId is not { } adjunto || g.FileName is null) return Result.Failure<PilaDownloadDto>(PilaErrors.NotGenerated(g.Status.ToString()));
        // FR-027: la diferencia con los comprobantes se muestra ANTES de descargar; se descarga sólo reconociéndola.
        if (!g.Balanced && !request.AcknowledgeDifference)
        {
            var cuadre = JsonSerializer.Deserialize<PilaReconciliationDto>(g.ReconciliationJson, GeneratePilaCommandHandler.JsonWeb);
            return Result.Failure<PilaDownloadDto>(PilaErrors.Unreconciled(cuadre ?? new PilaReconciliationDto([], false, null)));
        }
        var descarga = await sender.Send(new DownloadAttachmentQuery(adjunto), ct);
        if (descarga.IsFailure) return Result.Failure<PilaDownloadDto>(descarga.Error);
        if (!string.Equals(descarga.Value.Sha256Hex, g.FileSha256, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<PilaDownloadDto>(new Error("Payroll.Pila.FileTampered", "El archivo guardado no coincide con la huella registrada al generarlo."));
        var layout = PilaLayoutCatalog.ByCode(g.LayoutVersion);
        return Result.Success(new PilaDownloadDto(g.FileName, "text/plain", descarga.Value.Content, layout?.Encoding ?? "us-ascii"));
    }
}

/// <summary>
/// Feature 011 (US4, contracts/api.md §9): la planilla se baja con un enlace firmado, sin pasar por la
/// memoria del servidor. Aplica las mismas reglas que la descarga de siempre —la generación tiene
/// archivo, y con descuadre hay que reconocerlo (FR-027)— y firma con el <c>charset</c> del layout, que
/// el operador exige. Una planilla guardada con el formato anterior responde <c>direct: false</c> y se
/// baja por <c>GET /{id}/file</c>. Es un comando porque cada enlace queda en la auditoría.
/// </summary>
public sealed record EmitirEnlaceDePilaCommand(Guid GenerationPublicId, bool AcknowledgeDifference = false) : IRequest<Result<EnlaceDeDescargaDto>>;

public sealed class EmitirEnlaceDePilaCommandValidator : AbstractValidator<EmitirEnlaceDePilaCommand>
{
    public EmitirEnlaceDePilaCommandValidator() => RuleFor(x => x.GenerationPublicId).NotEmpty();
}

public sealed class EmitirEnlaceDePilaCommandHandler(IApplicationDbContext db, IBlobStore store, IDateTimeService reloj, IOptions<LimitesDeAdjuntos> limites)
    : IRequestHandler<EmitirEnlaceDePilaCommand, Result<EnlaceDeDescargaDto>>
{
    public async Task<Result<EnlaceDeDescargaDto>> Handle(EmitirEnlaceDePilaCommand request, CancellationToken ct)
    {
        var g = await db.PilaGenerations.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.GenerationPublicId, ct);
        if (g is null) return Result.Failure<EnlaceDeDescargaDto>(PilaErrors.GenerationNotFound);
        if (g.FileAttachmentPublicId is not { } adjuntoId || g.FileName is null) return Result.Failure<EnlaceDeDescargaDto>(PilaErrors.NotGenerated(g.Status.ToString()));
        if (!g.Balanced && !request.AcknowledgeDifference)
        {
            var cuadre = JsonSerializer.Deserialize<PilaReconciliationDto>(g.ReconciliationJson, GeneratePilaCommandHandler.JsonWeb);
            return Result.Failure<EnlaceDeDescargaDto>(PilaErrors.Unreconciled(cuadre ?? new PilaReconciliationDto([], false, null)));
        }
        var adjunto = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == adjuntoId, ct);
        if (adjunto is null) return Result.Failure<EnlaceDeDescargaDto>("Generic.NotFound", "El archivo de la planilla no está guardado.");
        if (!string.Equals(adjunto.Sha256Hex, g.FileSha256, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<EnlaceDeDescargaDto>(new Error("Payroll.Pila.FileTampered", "El archivo guardado no coincide con la huella registrada al generarlo."));
        if (adjunto.Format == FormatoDeAdjunto.AppEncrypted) return Result.Success(EnlaceDeDescargaDto.PorLaApi);

        var codificacion = PilaLayoutCatalog.ByCode(g.LayoutVersion)?.Encoding ?? "us-ascii";
        return Result.Success(await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct,
            contentType: $"text/plain; charset={codificacion}", nombre: g.FileName));
    }
}

internal static class PilaMappers
{
    public static PilaGenerationSummaryDto Resumen(PilaGeneration g) => new(
        g.PublicId, $"{g.Year}-{g.Month:00}", g.Year, g.Month, g.Version, g.Status, g.LayoutVersion, g.GeneratedAt, g.GeneratedBy,
        new PilaTotalsDto(g.TotalPension, g.TotalHealth, g.TotalWorkRisk, g.TotalFamilyCompensation, g.TotalSena, g.TotalIcbf, g.TotalSolidarityFund, g.TotalContributions),
        g.Balanced, g.ContributorCount, g.LineCount, g.BlockingIssueCount, g.WarningCount, g.FileName, g.ProposedPaymentDueDate,
        g.UploadedAt, g.UploadedBy, g.OperatorFilingNumber, g.OperatorFilingDate, g.PaidAt);
}
