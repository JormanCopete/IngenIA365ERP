using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Novelties.ImportNovelties;

public sealed record ImportErrorDto(int Row, string Column, string Message);

/// <summary>Con errores: <c>Applied = 0</c>, nada persistido y la lista completa por fila y columna (el endpoint responde 422).</summary>
public sealed record ImportNoveltiesResultDto(int Applied, Guid BatchId, IReadOnlyList<ImportErrorDto> Errors)
{
    public bool HasErrors => Errors.Count > 0;
    public string? Code => HasErrors ? "Payroll.ImportInvalid" : null;
}

/// <summary>
/// FR-006: importa las novedades de un período desde un archivo. Todo o nada: si una fila no
/// pasa las mismas reglas del registro manual, no entra ninguna y se devuelven todos los
/// errores con fila y columna. Las que entran llevan <c>Origin = Import</c> y el mismo
/// <see cref="PayrollNovelty.ImportBatchId"/>, y dejan el mismo rastro que una manual.
/// </summary>
public sealed record ImportNoveltiesCommand(Guid PeriodPublicId, Stream Content, string FileName, long Length) : IRequest<Result<ImportNoveltiesResultDto>>;

public sealed class ImportNoveltiesCommandValidator : AbstractValidator<ImportNoveltiesCommand>
{
    public const long MegaByte = 1024 * 1024;
    public const long MaxBytes = 5 * MegaByte;

    public ImportNoveltiesCommandValidator()
    {
        RuleFor(x => x.PeriodPublicId).NotEmpty();
        RuleFor(x => x.Content).NotNull();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(260);
        RuleFor(x => x.Length).GreaterThan(0).WithMessage("El archivo está vacío.");
    }
}

public sealed class ImportNoveltiesCommandHandler(
    IApplicationDbContext db,
    INoveltyFileParser parser,
    IDateTimeService clock,
    ICurrentUserService user,
    IPayrollRunStaleMarker staleMarker,
    CarryOverNoveltiesService carryOver)
    : IRequestHandler<ImportNoveltiesCommand, Result<ImportNoveltiesResultDto>>
{
    public async Task<Result<ImportNoveltiesResultDto>> Handle(ImportNoveltiesCommand request, CancellationToken ct)
    {
        if (request.Length > ImportNoveltiesCommandValidator.MaxBytes)
            return Result.Failure<ImportNoveltiesResultDto>(new Error("Payroll.ImportFileTooLarge",
                $"El archivo pesa {request.Length / (decimal)ImportNoveltiesCommandValidator.MegaByte:0.#} MB y el máximo es 5 MB. Divídalo en varios lotes."));

        var periodo = await NoveltyRules.ResolvePeriodAsync(db, request.PeriodPublicId, ct);
        if (periodo.IsFailure) return Result.Failure<ImportNoveltiesResultDto>(periodo.Error);
        var period = periodo.Value;
        var editable = await NoveltyRules.EnsureEditableAsync(db, period, ct);
        if (editable.IsFailure) return Result.Failure<ImportNoveltiesResultDto>(editable.Error);

        var batchId = Guid.NewGuid();
        var parseado = parser.Parse(request.Content);
        var errores = parseado.Errors.Select(e => new ImportErrorDto(e.Row, e.Column, e.Message)).ToList();
        if (errores.Count > 0)
            return Result.Success(new ImportNoveltiesResultDto(0, batchId, errores));
        if (parseado.Rows.Count == 0)
            return Result.Success(new ImportNoveltiesResultDto(0, batchId, [new ImportErrorDto(2, "documento", "El archivo no tiene filas de novedades.")]));

        // --- empleados del plan por documento ---
        var documentos = parseado.Rows.Select(r => r.EmployeeDocument).Distinct().ToList();
        var empleados = await (
            from e in db.Employees.AsNoTracking()
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where documentos.Contains(p.TaxId)
            select new { e, p.TaxId }).ToListAsync(ct);
        var porDocumento = empleados.GroupBy(x => x.TaxId).ToDictionary(g => g.Key, g => g.Select(x => x.e).ToList());

        var conceptos = new Dictionary<string, PayrollConceptDefinition>(StringComparer.OrdinalIgnoreCase);
        var nuevas = new List<PayrollNovelty>();
        var vistas = new HashSet<(int EmployeeId, string Code)>();
        var ahora = clock.UtcNow;

        foreach (var fila in parseado.Rows)
        {
            if (!porDocumento.TryGetValue(fila.EmployeeDocument, out var candidatos))
            {
                errores.Add(new ImportErrorDto(fila.Row, "documento", $"No hay empleado con documento {fila.EmployeeDocument}."));
                continue;
            }
            var employee = candidatos.FirstOrDefault(e => e.PayrollPlanId == period.PayrollPlanId) ?? candidatos[0];
            var vigente = NoveltyRules.EnsureEmployeeInPeriod(employee, period);
            if (vigente.IsFailure) { errores.Add(new ImportErrorDto(fila.Row, "documento", vigente.Error.Message)); continue; }

            if (!conceptos.TryGetValue(fila.ConceptCode, out var concept))
            {
                var conceptoRes = await NoveltyRules.ResolveConceptAsync(db, fila.ConceptCode, period.EndDate, employee.EmployeeClass, ct);
                if (conceptoRes.IsFailure) { errores.Add(new ImportErrorDto(fila.Row, "concepto", conceptoRes.Error.Message)); continue; }
                concept = conceptoRes.Value;
                conceptos[fila.ConceptCode] = concept;
            }
            else if (!concept.AppliesTo(employee.EmployeeClass))
            {
                errores.Add(new ImportErrorDto(fila.Row, "concepto", $"El concepto {concept.Name} no aplica a la clase de empleado de {fila.EmployeeDocument}."));
                continue;
            }

            var campos = NoveltyRules.ValidateFields(concept, period, fila.Quantity, fila.Amount, fila.StartDate, fila.EndDate);
            if (campos.IsFailure) { errores.Add(new ImportErrorDto(fila.Row, Columna(campos.Error.Code), campos.Error.Message)); continue; }

            if (!concept.AllowsRepeatInPeriod)
            {
                if (!vistas.Add((employee.Id, concept.Code)))
                {
                    errores.Add(new ImportErrorDto(fila.Row, "concepto", $"El concepto {concept.Name} aparece más de una vez para el documento {fila.EmployeeDocument} y no admite repetirse en el período."));
                    continue;
                }
                var duplicado = await NoveltyRules.EnsureNoDuplicateAsync(db, concept, period.Id, employee.Id, null, ct);
                if (duplicado.IsFailure) { errores.Add(new ImportErrorDto(fila.Row, "concepto", duplicado.Error.Message)); continue; }
            }

            nuevas.Add(new PayrollNovelty
            {
                PayPeriodId = period.Id,
                EmployeeId = employee.Id,
                ConceptDefinitionId = concept.Id,
                ConceptCode = concept.Code,
                Quantity = fila.Quantity,
                Amount = fila.Amount,
                StartDate = fila.StartDate,
                EndDate = fila.EndDate,
                DaysInPeriod = campos.Value.DaysInPeriod,
                CarryOverDays = campos.Value.CarryOverDays,
                Notes = fila.Notes,
                Status = NoveltyStatus.Active,
                Origin = NoveltyOrigin.Import,
                ImportBatchId = batchId,
                CreatedAt = ahora,
                CreatedBy = user.UserName,
            });
        }

        if (errores.Count > 0)
            return Result.Success(new ImportNoveltiesResultDto(0, batchId, errores.OrderBy(e => e.Row).ToList()));

        db.PayrollNovelties.AddRange(nuevas);
        await db.SaveChangesAsync(ct);
        foreach (var n in nuevas.Where(n => n.CarryOverDays > 0))
            await carryOver.CreateCarryOverAsync(n, period, ct);
        await staleMarker.MarkStaleAsync(period.Id, $"importación de {nuevas.Count} novedad(es) ({request.FileName})", ct);
        await db.SaveChangesAsync(ct);

        return Result.Success(new ImportNoveltiesResultDto(nuevas.Count, batchId, []));
    }

    private static string Columna(string errorCode) => errorCode switch
    {
        "Payroll.ConceptRequiresDates" => "desde",
        "Payroll.ConceptRequiresQuantity" => "cantidad",
        "Payroll.ConceptRequiresAmount" => "valor",
        "Payroll.NoveltyOverMax" => "cantidad",
        _ => "concepto",
    };
}
