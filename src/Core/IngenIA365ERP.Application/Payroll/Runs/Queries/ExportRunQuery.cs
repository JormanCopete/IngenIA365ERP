using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Runs.Queries;

public sealed record ExportedFileDto(string FileName, string ContentType, byte[] Content);

/// <summary>
/// US4: exportación de la corrida (CSV UTF-8 con <c>;</c>): empleado, documento, concepto,
/// cantidad, base, factor, parámetro y vigencia, novedad, valor y la explicación en
/// texto. Deja evento explícito de auditoría: sale información de personas.
/// </summary>
public sealed record ExportRunQuery(Guid RunPublicId) : IRequest<Result<ExportedFileDto>>;

public sealed class ExportRunQueryValidator : AbstractValidator<ExportRunQuery>
{
    public ExportRunQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class ExportRunQueryHandler(IApplicationDbContext db, PayrollAuditEmitter audit)
    : IRequestHandler<ExportRunQuery, Result<ExportedFileDto>>
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<Result<ExportedFileDto>> Handle(ExportRunQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().Include(r => r.PayPeriod).FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<ExportedFileDto>(new Error("Payroll.RunNotFound", "No existe la corrida indicada."));

        var filas = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            join l in db.PayrollRunLines.AsNoTracking() on re.Id equals l.PayrollRunEmployeeId
            where re.PayrollRunId == run.Id
            orderby p.LastName, p.FirstName, l.Order
            select new { p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.TaxId, re.DaysWorked, l }).ToListAsync(ct);

        var noveltyIds = filas.Where(f => f.l.NoveltyId != null).Select(f => f.l.NoveltyId!.Value).Distinct().ToList();
        var novedades = noveltyIds.Count == 0
            ? new Dictionary<int, Guid>()
            : await db.PayrollNovelties.AsNoTracking().Where(n => noveltyIds.Contains(n.Id)).ToDictionaryAsync(n => n.Id, n => n.PublicId, ct);

        var sb = new StringBuilder();
        sb.Append('﻿'); // BOM: Excel en Windows abre UTF-8 con acentos sin preguntar
        sb.AppendLine("Empleado;Documento;Días;Concepto;Nombre;Tipo;Cantidad;Base;Factor;Parámetro;Vigencia del parámetro;Novedad;Valor;Afecta contabilidad;Explicación");
        foreach (var f in filas)
        {
            var exp = Explicar(f.l.ExplanationJson, out var parametro, out var vigencia);
            sb.Append(Campo(NombreDePersona.Completo(f.FirstName, f.OtherNames, f.LastName, f.SecondLastName))).Append(';')
              .Append(Campo(f.TaxId)).Append(';')
              .Append(f.DaysWorked).Append(';')
              .Append(Campo(f.l.ConceptCode)).Append(';')
              .Append(Campo(f.l.ConceptName)).Append(';')
              .Append(f.l.Nature).Append(';')
              .Append(Num(f.l.Quantity)).Append(';')
              .Append(Num(f.l.BaseAmount)).Append(';')
              .Append(Num(f.l.Factor)).Append(';')
              .Append(Campo(parametro)).Append(';')
              .Append(Campo(vigencia)).Append(';')
              .Append(f.l.NoveltyId is { } nid && novedades.TryGetValue(nid, out var np) ? np.ToString() : string.Empty).Append(';')
              .Append(f.l.Amount.ToString("0.##", Inv)).Append(';')
              .Append(f.l.AffectsAccounting ? "Sí" : "No").Append(';')
              .Append(Campo(exp))
              .AppendLine();
        }

        // Feature 010: una liquidación especial no tiene período; el archivo se nombra por tipo y corte.
        var nombre = run.PayPeriod is { } periodo
            ? $"nomina_{periodo.StartDate:yyyyMMdd}_{periodo.EndDate:yyyyMMdd}_v{run.Version}.csv"
            : $"{run.Kind.ToString().ToLowerInvariant()}_{run.CutoffDate:yyyyMMdd}_v{run.Version}.csv";
        await audit.EmitAsync(AuditEventTypes.PayrollRunExported, "PayrollRun", run.PublicId, null,
            new { file = nombre, rows = filas.Count, employees = filas.Select(f => f.TaxId).Distinct().Count() }, ct);

        return Result.Success(new ExportedFileDto(nombre, "text/csv; charset=utf-8", Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static string Num(decimal? v) => v is { } d ? d.ToString("0.####", Inv) : string.Empty;

    private static string Campo(string? v)
    {
        if (string.IsNullOrEmpty(v)) return string.Empty;
        var limpio = v.Replace("\r", " ").Replace("\n", " ");
        return limpio.Contains(';') || limpio.Contains('"') ? "\"" + limpio.Replace("\"", "\"\"") + "\"" : limpio;
    }

    /// <summary>La explicación en una línea de texto: forma, resumen y pasos «etiqueta = valor».</summary>
    private static string Explicar(string json, out string? parametro, out string? vigencia)
    {
        parametro = null; vigencia = null;
        try
        {
            var exp = JsonSerializer.Deserialize<Explanation>(json, RunJson.Options);
            if (exp is null) return string.Empty;
            if (exp.Parameter is { } p)
            {
                parametro = p.Code;
                vigencia = p.ValidFrom.ToString("yyyy-MM-dd", Inv);
            }
            var pasos = exp.Steps.Select(s => s.Value is { } v ? $"{s.Label} = {v.ToString("0.##", Inv)}" : $"{s.Label}: {s.Text}");
            return $"[{exp.Form}] {exp.Summary} | {string.Join(" | ", pasos)}";
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
