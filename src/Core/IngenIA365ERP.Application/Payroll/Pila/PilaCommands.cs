using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll.Pila;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Pila;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Pila;

// ------------------------------------------------------- datos del aportante --

/// <summary>Guarda (o crea) la fila única de datos del aportante (FR-024; auditado por el comportamiento genérico de comandos).</summary>
public sealed record UpdatePilaSettingsCommand(PilaSettingsInput Input) : IRequest<Result<PilaSettingsDto>>;

public sealed class UpdatePilaSettingsCommandValidator : AbstractValidator<UpdatePilaSettingsCommand>
{
    public UpdatePilaSettingsCommandValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.ContributorType).NotEmpty().MaximumLength(2).Matches("^[0-9]+$").WithMessage("El tipo de aportante es numérico (1 = empleador).");
        RuleFor(x => x.Input.ContributorClass).NotEmpty().Must(c => c is "A" or "B" or "C" or "D" or "I").WithMessage("La clase de aportante es A, B, C, D o I.");
        RuleFor(x => x.Input.PresentationForm).NotEmpty().Must(f => f is "U" or "S").WithMessage("La forma de presentación es U (única) o S (sucursal).");
        RuleFor(x => x.Input.BranchCode).MaximumLength(10);
        RuleFor(x => x.Input.BranchName).MaximumLength(40);
        RuleFor(x => x.Input.ArlPilaCode).MaximumLength(6);
        RuleFor(x => x.Input.EconomicActivityCode).MaximumLength(7);
        RuleFor(x => x.Input.DivipolaDepartment).Matches("^[0-9]{2}$").When(x => !string.IsNullOrEmpty(x.Input.DivipolaDepartment)).WithMessage("El departamento DIVIPOLA tiene dos dígitos.");
        RuleFor(x => x.Input.DivipolaMunicipality).Matches("^[0-9]{3}$").When(x => !string.IsNullOrEmpty(x.Input.DivipolaMunicipality)).WithMessage("El municipio DIVIPOLA tiene tres dígitos.");
        RuleFor(x => x.Input.OperatorCode).MaximumLength(2);
        RuleFor(x => x.Input.OperatorName).MaximumLength(40);
    }
}

public sealed class UpdatePilaSettingsCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<UpdatePilaSettingsCommand, Result<PilaSettingsDto>>
{
    public async Task<Result<PilaSettingsDto>> Handle(UpdatePilaSettingsCommand request, CancellationToken ct)
    {
        var i = request.Input;
        var s = await db.PilaSettings.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        var ahora = clock.UtcNow;
        if (s is null)
        {
            s = new PilaSettings { CreatedAt = ahora, CreatedBy = user.UserName };
            db.PilaSettings.Add(s);
        }
        s.ContributorType = i.ContributorType.Trim();
        s.ContributorClass = i.ContributorClass.Trim().ToUpperInvariant();
        s.PresentationForm = i.PresentationForm.Trim().ToUpperInvariant();
        s.BranchCode = Vacio(i.BranchCode);
        s.BranchName = Vacio(i.BranchName);
        s.ArlPilaCode = Vacio(i.ArlPilaCode)?.ToUpperInvariant();
        s.EconomicActivityCode = Vacio(i.EconomicActivityCode);
        s.MunicipalityDaneCode = string.IsNullOrWhiteSpace(i.DivipolaDepartment) || string.IsNullOrWhiteSpace(i.DivipolaMunicipality) ? null : i.DivipolaDepartment.Trim() + i.DivipolaMunicipality.Trim();
        s.OperatorCode = Vacio(i.OperatorCode);
        s.OperatorName = Vacio(i.OperatorName);
        s.UpdatedAt = ahora; s.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct);

        var empresa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).Select(c => c.TaxId).FirstOrDefaultAsync(ct);
        return Result.Success(PilaSettingsMapper.ToDto(s, empresa));
    }

    private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public static class PilaSettingsMapper
{
    public static PilaSettingsDto ToDto(PilaSettings? s, string? nit)
    {
        var faltan = new List<string>();
        if (s is null) faltan.Add("datos del aportante");
        else
        {
            if (string.IsNullOrWhiteSpace(s.ArlPilaCode)) faltan.Add("código PILA de la ARL del aportante");
            if (string.IsNullOrWhiteSpace(s.OperatorCode)) faltan.Add("código del operador de información");
            if (s.PresentationForm == "S" && string.IsNullOrWhiteSpace(s.BranchCode)) faltan.Add("código de la sucursal");
        }
        if (string.IsNullOrWhiteSpace(nit)) faltan.Add("NIT de la empresa (Maestros › Empresas)");
        var (dep, mun) = Domain_Divipola(s?.MunicipalityDaneCode);
        return new PilaSettingsDto(
            s?.ContributorType ?? "1", s?.ContributorClass ?? "B", s?.PresentationForm ?? "U", s?.BranchCode, s?.BranchName,
            s?.ArlPilaCode, s?.EconomicActivityCode, dep, mun, s?.OperatorCode, s?.OperatorName, s?.PlanillaType ?? "E",
            PreparacionDePila.Digitos(nit), faltan.Count == 0, faltan);
    }

    private static (string? Dep, string? Mun) Domain_Divipola(string? dane) =>
        string.IsNullOrWhiteSpace(dane) || dane.Length < 5 ? (null, null) : (dane[..2], dane[2..5]);
}

// ------------------------------------------------------------------- generar --

/// <summary>
/// Genera la planilla del período (FR-024..FR-027): con bloqueantes deja una generación
/// <c>Validated</c> sin archivo; con alertas exige reconocerlas; regenerar crea la versión
/// siguiente y deja la anterior <c>Superseded</c>; la vigente cargada en el operador no se
/// regenera (<c>AlreadyUploaded</c>). El <c>.txt</c> se guarda en <c>COR_Attachments</c> antes
/// de la fila, como la dispersión.
/// </summary>
public sealed record GeneratePilaCommand(short Year, byte Month, bool AcknowledgeWarnings = false) : IRequest<Result<PilaGeneratedDto>>;

public sealed class GeneratePilaCommandValidator : AbstractValidator<GeneratePilaCommand>
{
    public GeneratePilaCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween((short)2000, (short)2100);
        RuleFor(x => x.Month).InclusiveBetween((byte)1, (byte)12);
    }
}

public sealed class GeneratePilaCommandHandler(
    IApplicationDbContext db, PreparacionDePila preparacion, ISender sender, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<GeneratePilaCommand, Result<PilaGeneratedDto>>
{
    public const string OwnerEntityType = nameof(PilaGeneration);
    internal static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    public async Task<Result<PilaGeneratedDto>> Handle(GeneratePilaCommand request, CancellationToken ct)
    {
        var vigentes = await db.PilaGenerations.Where(g => g.Year == request.Year && g.Month == request.Month && g.Status != PilaGenerationStatus.Superseded).ToListAsync(ct);
        var cargada = vigentes.FirstOrDefault(g => g.Status == PilaGenerationStatus.Uploaded);
        if (cargada is not null) return Result.Failure<PilaGeneratedDto>(PilaErrors.AlreadyUploaded(cargada.PublicId));

        var prep = await preparacion.PrepararAsync(request.Year, request.Month, ct);
        if (prep.IsFailure) return Result.Failure<PilaGeneratedDto>(prep.Error);
        var p = prep.Value;
        var faltantes = p.Load.Input.Parameters.Missing(PilaParameterCodes.Required);
        if (faltantes.Count > 0) return Result.Failure<PilaGeneratedDto>(PilaErrors.ParametersMissing(faltantes));
        var ajustes = PilaSettingsMapper.ToDto(p.Load.Settings, p.Load.CompanyNit);
        if (!ajustes.Complete) return Result.Failure<PilaGeneratedDto>(PilaErrors.SettingsIncomplete(ajustes.Missing));
        if (!p.HasBlocking && p.Warnings > 0 && !request.AcknowledgeWarnings)
            return Result.Failure<PilaGeneratedDto>(PilaErrors.WarningsNotAcknowledged);

        var ahora = clock.UtcNow;
        var version = (await db.PilaGenerations.Where(g => g.Year == request.Year && g.Month == request.Month).MaxAsync(g => (int?)g.Version, ct) ?? 0) + 1;
        var r = p.Result;
        var entidad = new PilaGeneration
        {
            Year = request.Year, Month = request.Month, Version = version,
            Status = p.HasBlocking ? PilaGenerationStatus.Validated : PilaGenerationStatus.Generated,
            LayoutVersion = PilaLayoutCatalog.VersionLabel(p.Load.Input.Layout),
            GeneratedAt = ahora, GeneratedBy = user.UserName ?? "sistema",
            ExemptionApplied = p.Load.Input.Policies.Exonerada114_1,
            ContributorCount = r.ContributorCount, LineCount = r.LineCount,
            TotalIbcHealth = r.TotalIbcHealth, TotalIbcPension = r.TotalIbcPension, TotalIbcWorkRisk = r.TotalIbcWorkRisk, TotalIbcFamilyCompensation = r.TotalIbcFamilyCompensation,
            TotalHealth = r.TotalHealth, TotalPension = r.TotalPension, TotalSolidarityFund = r.TotalSolidarityFund, TotalWorkRisk = r.TotalWorkRisk,
            TotalFamilyCompensation = r.TotalFamilyCompensation, TotalSena = r.TotalSena, TotalIcbf = r.TotalIcbf, TotalContributions = r.TotalContributions,
            ReconciliationJson = JsonSerializer.Serialize(p.Reconciliation, JsonWeb), Balanced = p.Reconciliation.Balanced,
            SourceRunsJson = JsonSerializer.Serialize(p.Load.Sources, JsonWeb),
            BlockingIssueCount = p.Blocking, WarningCount = p.Warnings,
            ProposedPaymentDueDate = p.DueDate,
            CreatedAt = ahora, CreatedBy = user.UserName,
        };
        foreach (var i in r.Issues)
            entidad.Issues.Add(new PilaIssue { Severity = i.Severity, Code = i.Code, FieldNumber = i.Field, EmployeeId = i.EmployeeId, Message = Recorta(i.Message, 500), LinkRoute = i.LinkRoute, CreatedAt = ahora, CreatedBy = user.UserName });

        string? fileName = null;
        if (!p.HasBlocking)
        {
            var salida = PilaWriter.Write(p.Load.Input.Layout, r);
            fileName = $"PILA_{p.Load.CompanyNit}_{request.Year}-{request.Month:00}_v{version}.txt";
            var sha = Convert.ToHexString(SHA256.HashData(salida.Content)).ToLowerInvariant();
            entidad.FileName = fileName; entidad.FileSha256 = sha;
            for (var n = 0; n < r.Lines.Count; n++)
            {
                var l = r.Lines[n];
                entidad.Lines.Add(new PilaGenerationLine
                {
                    LineNumber = l.LineNumber, EmployeeId = l.Contributor.EmployeeId,
                    ContributorType = l.ContributorType, ContributorSubType = l.ContributorSubType, NoveltyFlags = string.Join(",", l.Flags),
                    DaysHealth = (byte)l.DaysHealth, DaysPension = (byte)l.DaysPension, DaysWorkRisk = (byte)l.DaysWorkRisk, DaysFamilyCompensation = (byte)l.DaysFamilyCompensation,
                    Salary = l.Salary, IbcHealth = l.IbcHealth, IbcPension = l.IbcPension, IbcWorkRisk = l.IbcWorkRisk, IbcFamilyCompensation = l.IbcFamilyCompensation,
                    HealthRate = l.HealthRate, PensionRate = l.PensionRate, WorkRiskRate = l.WorkRiskRate, FamilyCompensationRate = l.FamilyCompensationRate, SenaRate = l.SenaRate, IcbfRate = l.IcbfRate,
                    Health = l.Health, Pension = l.PensionTotal, SolidarityFund = l.SolidarityFund, SubsistenceFund = l.SubsistenceFund, WorkRisk = l.WorkRisk,
                    FamilyCompensation = l.FamilyCompensation, Sena = l.Sena, Icbf = l.Icbf, Exempt = l.Exempt,
                    FieldsJson = JsonSerializer.Serialize(salida.LineFields[n].ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)),
                    RecordText = salida.LineTexts[n],
                    ExplanationJson = JsonSerializer.Serialize(l.Explanations, JsonWeb),
                    CreatedAt = ahora, CreatedBy = user.UserName,
                });
            }
            var subida = await sender.Send(new UploadAttachmentCommand(OwnerEntityType, entidad.PublicId, fileName, "text/plain", salida.Content), ct);
            if (subida.IsFailure) return Result.Failure<PilaGeneratedDto>(subida.Error);
            entidad.FileAttachmentPublicId = subida.Value;
        }

        foreach (var anterior in vigentes)
        {
            anterior.Status = PilaGenerationStatus.Superseded;
            anterior.UpdatedAt = ahora; anterior.UpdatedBy = user.UserName;
        }
        db.PilaGenerations.Add(entidad);
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollPilaGenerated, OwnerEntityType, entidad.PublicId, null, new
        {
            period = $"{request.Year}-{request.Month:00}", version, status = entidad.Status.ToString(), layout = entidad.LayoutVersion,
            contributors = r.ContributorCount, lines = r.LineCount, total = r.TotalContributions, balanced = p.Reconciliation.Balanced,
            blocking = p.Blocking, warnings = p.Warnings, fileName, sha256 = entidad.FileSha256, sources = p.Load.Sources.Select(s => s.RunPublicId).ToList(),
        }, ct);

        return Result.Success(new PilaGeneratedDto(entidad.PublicId, version, entidad.Status, fileName, r.ContributorCount, r.LineCount, p.Totals, p.Reconciliation, p.Issues, p.DueDate));
    }

    private static string Recorta(string s, int max) => s.Length <= max ? s : s[..max];
}

// ------------------------------------------------------------ marcar cargada --

/// <summary>La planilla se radicó en el operador: número y fecha digitados; sólo una <c>Generated</c>.</summary>
public sealed record MarkPilaUploadedCommand(Guid GenerationPublicId, DateTime UploadedAt, string OperatorReference, DateOnly? OperatorFilingDate = null, DateOnly? PaidAt = null) : IRequest<Result<PilaUploadedDto>>;

public sealed class MarkPilaUploadedCommandValidator : AbstractValidator<MarkPilaUploadedCommand>
{
    public MarkPilaUploadedCommandValidator()
    {
        RuleFor(x => x.GenerationPublicId).NotEmpty();
        RuleFor(x => x.UploadedAt).NotEmpty();
        RuleFor(x => x.OperatorReference).NotEmpty().WithMessage("Indique el número de radicación o de planilla que asignó el operador.").MaximumLength(40);
    }
}

public sealed class MarkPilaUploadedCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<MarkPilaUploadedCommand, Result<PilaUploadedDto>>
{
    public async Task<Result<PilaUploadedDto>> Handle(MarkPilaUploadedCommand request, CancellationToken ct)
    {
        var g = await db.PilaGenerations.FirstOrDefaultAsync(x => x.PublicId == request.GenerationPublicId, ct);
        if (g is null) return Result.Failure<PilaUploadedDto>(PilaErrors.GenerationNotFound);
        if (g.Status != PilaGenerationStatus.Generated) return Result.Failure<PilaUploadedDto>(PilaErrors.NotGenerated(g.Status.ToString()));

        g.Status = PilaGenerationStatus.Uploaded;
        g.UploadedAt = request.UploadedAt; g.UploadedBy = user.UserName ?? "sistema";
        g.OperatorFilingNumber = request.OperatorReference.Trim();
        g.OperatorFilingDate = request.OperatorFilingDate ?? DateOnly.FromDateTime(request.UploadedAt);
        g.PaidAt = request.PaidAt;
        g.UpdatedAt = clock.UtcNow; g.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollPilaUploaded, nameof(PilaGeneration), g.PublicId, null,
            new { period = $"{g.Year}-{g.Month:00}", version = g.Version, uploadedAt = request.UploadedAt, reference = g.OperatorFilingNumber, paidAt = request.PaidAt }, ct);
        return Result.Success(new PilaUploadedDto(g.PublicId, g.Status, request.UploadedAt, g.OperatorFilingNumber));
    }
}
