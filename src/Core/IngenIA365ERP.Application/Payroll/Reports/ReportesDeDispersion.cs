using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Dispersion;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.Reports;

/// <summary>
/// Vista <c>dispersion</c> del centro de reportes (feature 010, US8; contracts/api.md §11): las
/// líneas de un archivo de dispersión y, debajo, los pendientes con su motivo. Sirve para
/// entregar al banco o a la revisoría lo mismo que fue en el archivo, en Excel, PDF o Word.
/// </summary>
public sealed record DispersionReportQuery(Guid FilePublicId, bool Exportacion = false) : IRequest<Result<TablaExportable>>;

public sealed class DispersionReportQueryValidator : AbstractValidator<DispersionReportQuery>
{
    public DispersionReportQueryValidator() => RuleFor(x => x.FilePublicId).NotEmpty().WithMessage("El archivo es obligatorio.");
}

public sealed class DispersionReportQueryHandler(
    ISender sender,
    ICurrentTenantService tenant,
    ICurrentUserService user,
    IDateTimeService clock,
    PayrollAuditEmitter audit)
    : IRequestHandler<DispersionReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(DispersionReportQuery request, CancellationToken ct)
    {
        var detalle = await sender.Send(new GetDisbursementFileQuery(request.FilePublicId), ct);
        if (detalle.IsFailure) return Result.Failure<TablaExportable>(detalle.Error);
        var d = detalle.Value;
        var a = d.Summary;

        var columnas = new List<ColumnaExportable>
        {
            new("#", TipoDeColumna.Entero), new("Empleado"), new("Documento"), new("Banco destino"), new("Tipo de cuenta"), new("Cuenta"),
            new("Valor", TipoDeColumna.Moneda), new("Estado"), new("Motivo"),
        };
        var filas = new List<FilaExportable>(d.Lines.Count + d.Excluded.Count + 1);
        foreach (var l in d.Lines)
            filas.Add(new FilaExportable([l.LineNumber, l.Name, l.Document, l.BankName, l.AccountType, l.AccountNumber, l.Amount, l.Paid ? "Pagado" : "En el archivo", null]));
        if (d.Excluded.Count > 0)
        {
            filas.Add(new FilaExportable([null, "Pendientes (no fueron al banco)", null, null, null, null, d.Excluded.Sum(e => e.NetPay), null, null], Resaltada: true));
            foreach (var e in d.Excluded)
                filas.Add(new FilaExportable([null, e.Name, null, null, null, null, e.NetPay, "Pendiente", e.Reason]));
        }
        var totales = new FilaExportable([null, "Total del archivo", $"{a.LineCount} línea(s)", null, null, null, a.TotalAmount, a.Status, null], Resaltada: true);
        var notas = new List<string>
        {
            $"Archivo {a.FileName} · formato {a.FormatCode} ({a.FormatName}) · banco {a.BankName ?? "genérico"} · cuenta origen {d.SourceAccountNumber ?? "—"} · fecha de pago {a.PaymentDate:dd/MM/yyyy} · referencia {a.Reference ?? "—"} · SHA-256 {a.FileSha256}",
            $"Generado por {a.GeneratedBy} el {a.GeneratedAt:dd/MM/yyyy HH:mm}"
                + (a.SentAt is { } s ? $" · enviado por {a.SentBy} el {s:dd/MM/yyyy HH:mm}, referencia del banco {a.BankReference ?? "—"}" : string.Empty)
                + (a.VoidedAt is { } v ? $" · anulado por {a.VoidedBy} el {v:dd/MM/yyyy HH:mm}: {a.VoidReason}" : string.Empty),
        };
        var tabla = new TablaExportable(
            $"Dispersión bancaria · {a.RunLabel}",
            $"{tenant.TenantName ?? "Cooperativa"} · {a.FileName} · {a.LineCount} línea(s), {a.ExcludedCount} pendiente(s) · generado por {user.UserName ?? "sistema"} el {clock.UtcNow:dd/MM/yyyy HH:mm} UTC",
            columnas, filas, totales, notas);
        if (request.Exportacion)
            await audit.EmitAsync(AuditEventTypes.PayrollRunExported, nameof(BankDisbursementFile), a.FilePublicId, null,
                new { report = "dispersion", runPublicId = a.RunPublicId, lines = a.LineCount, excluded = a.ExcludedCount }, ct);
        return Result.Success(tabla);
    }
}
