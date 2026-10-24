using System.Globalization;
using System.Reflection;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Common.Layouts;
using IngenIA365ERP.Domain.Entities.Receipts.Compliance;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Cumplimiento;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Receipts.Compliance;

// Feature 015, R1, US14 (T0390; R-24; FR-096, FR-099, FR-204; archivos-y-plantillas.md §6; molde LND_RegulatoryFiles de la 013): el reporte de transacciones a la UIAF.
// La estructura es dato versionado (Layouts/uiaf-transacciones-*.json) sobre el motor genérico Domain/Common/Layouts, «sin cotejar» hasta que el oficial lo coteje (P7):
// cada generación lleva Recaudo.Sarlaft.LayoutSinCotejar como advertencia, nunca en el archivo. Las transacciones son las del registro único de efectivo: lo de Recaudo
// (recibos y egresos, sin los anulados) y lo del punto de venta por su puerto, sin copiar filas. Alcance, estado del período y ausencia, por los Sarlaft.Reporte.*.

/// <summary>El catálogo de layouts de Recaudo embebido (<c>Receipts/Compliance/Layouts/*.json</c>); el vigente a una fecha. (nuevo)</summary>
public sealed class CatalogoDeLayoutsDeRecaudo
{
    private const string Carpeta = ".Receipts.Compliance.Layouts.";
    private static readonly Lazy<CatalogoDeLayoutsDeRecaudo> DelEnsamblado = new(Cargar);

    public static CatalogoDeLayoutsDeRecaudo Embebido => DelEnsamblado.Value;

    public IReadOnlyList<LayoutDeArchivo> Todos { get; }

    private CatalogoDeLayoutsDeRecaudo(IReadOnlyList<LayoutDeArchivo> todos) => Todos = todos;

    public LayoutDeArchivo? Vigente(DateOnly fecha) => Todos.Where(l => l.IsValidAt(fecha)).OrderByDescending(l => l.ValidFrom).FirstOrDefault();

    private static CatalogoDeLayoutsDeRecaudo Cargar()
    {
        var ensamblado = typeof(CatalogoDeLayoutsDeRecaudo).Assembly;
        var lista = new List<LayoutDeArchivo>();
        foreach (var recurso in ensamblado.GetManifestResourceNames().Where(n => n.Contains(Carpeta, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)))
        {
            using var s = ensamblado.GetManifestResourceStream(recurso)!;
            using var r = new StreamReader(s);
            lista.Add(LectorDeLayout.Leer(r.ReadToEnd(), recurso));
        }
        return new CatalogoDeLayoutsDeRecaudo(lista.OrderBy(l => l.ValidFrom).ToList());
    }
}

/// <summary>Una inconsistencia o advertencia de una generación. (nuevo)</summary>
public sealed record InconsistenciaDelReporteDto(string Severity, string Code, string Message, int? Row, string? Field);

/// <summary>Una generación del reporte. (nuevo)</summary>
public sealed record CashTransactionReportDto(
    Guid PublicId,
    int Year,
    int Month,
    int Version,
    CashTransactionReportScope Scope,
    CashTransactionReportApplication ApplicationState,
    string LayoutCode,
    bool LayoutVerified,
    CashTransactionReportStatus Status,
    bool IsAbsenceReport,
    int Lines,
    decimal Total,
    int BlockingIssues,
    int Warnings,
    string? FileName,
    string? Sha256,
    string? UploadReference,
    DateTime? UploadedAt,
    DateTime GeneratedAt,
    IReadOnlyList<InconsistenciaDelReporteDto> Issues);

/// <summary>Los errores del reporte de transacciones. (nuevo)</summary>
public static class ErroresDelReporteDeTransacciones
{
    public const string CodigoYaCargado = "Receipts.CashTransactionReport.AlreadyUploaded";
    public const string CodigoSinLayout = "Receipts.CashTransactionReport.LayoutMissing";
    public const string CodigoNoGenerado = "Receipts.CashTransactionReport.NotGenerated";
    public const string CodigoLayoutSinCotejar = "Recaudo.Sarlaft.LayoutSinCotejar";
    public const string CodigoSuspendido = "Recaudo.Sarlaft.ReporteSuspendido";
    public const string CodigoSinTransacciones = "Recaudo.Sarlaft.SinTransacciones";

    public static Error YaCargado(int version) => new ErrorConDatos(CodigoYaCargado,
        $"El reporte del período ya se cargó (versión {version}): no se regenera.", new { version });

    public static Error SinLayout(int anio, int mes) => new ErrorConDatos(CodigoSinLayout,
        string.Create(CultureInfo.InvariantCulture, $"No hay una estructura vigente del reporte para {anio:0000}-{mes:00}."), new { year = anio, month = mes });

    public static Error NoGenerado(CashTransactionReportStatus estado) => new ErrorConDatos(CodigoNoGenerado,
        $"Sólo se marca cargado un reporte generado (está {estado}).", new { status = estado.ToString() });
}

/// <summary>Lo común del reporte de transacciones. (nuevo)</summary>
public static class ReportesDeTransacciones
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>El tipo de documento del reporte desde el <c>IdType</c> de la persona (CC, CE, NI, PA, TI); un código desconocido pasa tal cual y el layout lo señala.</summary>
    public static string TipoDeDocumento(string? idType) => idType?.Trim().ToUpperInvariant() switch
    {
        null or "" or "C" or "CC" => "CC",
        "E" or "CE" => "CE",
        "N" or "NI" or "NIT" => "NI",
        "P" or "PA" or "PP" => "PA",
        "T" or "TI" => "TI",
        var otro => otro,
    };

    public static CashTransactionReportDto Dto(CashTransactionReport r) => new(r.PublicId, r.PeriodYear, r.PeriodMonth, r.Version, r.Scope, r.ApplicationState, r.LayoutCode,
        r.LayoutVerified, r.Status, r.IsAbsenceReport, r.Lines, r.Total, r.BlockingIssues, r.Warnings, r.FileName, r.Sha256, r.UploadReference, r.UploadedAt, r.GeneratedAt,
        JsonSerializer.Deserialize<List<InconsistenciaDelReporteDto>>(r.IssuesJson, Json) ?? []);
}

/// <summary>
/// Arma el reporte de un mes (feature 015, R1, T0390): las transacciones del registro único, las líneas por <see cref="ReporteDeTransacciones"/>, las filas del layout y
/// la escritura con su validación. No escribe en la base. (nuevo)
/// </summary>
public sealed class PreparacionDelReporteDeTransacciones(IApplicationDbContext db, ParametrosDeCaja parametros, IEfectivoDelPuntoDeVenta? puntoDeVenta = null)
{
    public sealed record Preparado(LayoutDeArchivo Layout, CashTransactionReportScope Alcance, CashTransactionReportApplication Estado, bool Ausencia,
        IReadOnlyList<LineaDelReporte> Lineas, ResultadoDeEscritura? Escritura, IReadOnlyList<InconsistenciaDelReporteDto> Issues);

    public async Task<Result<Preparado>> PrepararAsync(int anio, int mes, CancellationToken ct)
    {
        var desde = new DateOnly(anio, mes, 1);
        var hasta = desde.AddMonths(1).AddDays(-1);
        var layout = CatalogoDeLayoutsDeRecaudo.Embebido.Vigente(hasta);
        if (layout is null) return Result.Failure<Preparado>(ErroresDelReporteDeTransacciones.SinLayout(anio, mes));
        var alcance = string.Equals(await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.SarlaftReporteAlcance, hasta, ct), "Todas", StringComparison.OrdinalIgnoreCase)
            ? CashTransactionReportScope.All
            : CashTransactionReportScope.CashOnly;
        var estado = string.Equals(await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.SarlaftReporteEstado, hasta, ct), "Suspendido", StringComparison.OrdinalIgnoreCase)
            ? CashTransactionReportApplication.Suspended
            : CashTransactionReportApplication.Active;
        var exigeAusencia = await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.SarlaftReporteExigeAusencia, hasta, ct);
        var individual = await parametros.DecimalAsync(ParametrosDeRecaudoYTesoreria.SarlaftUmbralTransaccionIndividual, hasta, ct) ?? 0m;
        var multiples = await parametros.DecimalAsync(ParametrosDeRecaudoYTesoreria.SarlaftUmbralTransaccionesMultiples, hasta, ct) ?? 0m;

        var transacciones = await TransaccionesAsync(desde, hasta, ct);
        var lineas = ReporteDeTransacciones.Armar(transacciones, alcance, individual, multiples, estado);
        var issues = new List<InconsistenciaDelReporteDto>();
        if (layout.HasUnverifiedFields)
            issues.Add(new InconsistenciaDelReporteDto("Warning", ErroresDelReporteDeTransacciones.CodigoLayoutSinCotejar,
                $"La estructura {layout.Code} tiene {layout.UnverifiedFields} campos sin cotejar con el anexo vigente: revísela antes de cargar.", null, null));
        if (estado == CashTransactionReportApplication.Suspended)
        {
            issues.Add(new InconsistenciaDelReporteDto("Warning", ErroresDelReporteDeTransacciones.CodigoSuspendido,
                "El reporte está suspendido para el período (Sarlaft.Reporte.Estado): no se genera archivo.", null, null));
            return Result.Success(new Preparado(layout, alcance, estado, false, lineas, null, issues));
        }
        var ausencia = lineas.Count == 0;
        if (ausencia && !exigeAusencia)
        {
            issues.Add(new InconsistenciaDelReporteDto("Warning", ErroresDelReporteDeTransacciones.CodigoSinTransacciones,
                "No hay transacciones que reportar en el período y la cooperativa no reporta la ausencia (Sarlaft.Reporte.ExigeAusencia).", null, null));
            return Result.Success(new Preparado(layout, alcance, estado, false, lineas, null, issues));
        }

        var empresa = await Sessions.DocumentosDeCajaDeRecaudo.EmpresaAsync(db, ct);
        var personas = lineas.Select(l => l.PersonaId).Distinct().ToList();
        var porPersona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var filas = new List<FilaDeLayout>
        {
            new("Header", null, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Encabezado.Periodo"] = anio * 100 + mes, ["Encabezado.Nit"] = empresa.Nit, ["Encabezado.TotalDeRegistros"] = lineas.Count,
                ["Encabezado.Ausencia"] = ausencia ? "S" : "N",
            }),
        };
        filas.AddRange(lineas.Select(l =>
        {
            var p = porPersona[l.PersonaId];
            return new FilaDeLayout("Detail", p.TaxId, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Linea.Clase"] = l.Clase == CashTransactionReportLineKind.Individual ? "I" : "M", ["Linea.Fecha"] = l.Fecha, ["Persona.TipoDeDocumento"] = ReportesDeTransacciones.TipoDeDocumento(p.IdType),
                ["Persona.NumeroDeDocumento"] = p.TaxId, ["Persona.Nombre"] = OperacionesDeCaja.Nombre(p), ["Linea.Valor"] = l.Valor, ["Linea.Cantidad"] = l.Cantidad,
                ["Linea.Documento"] = l.Documento, ["Linea.Oficina"] = null,
            });
        }));
        var escritura = EscritorPorLayout.Escribir(layout, filas);
        issues.AddRange(escritura.Inconsistencias.Select(i => new InconsistenciaDelReporteDto(i.Severidad.ToString(), i.Codigo, i.Mensaje, i.Fila, i.NombreDelCampo)));
        return Result.Success(new Preparado(layout, alcance, estado, ausencia, lineas, escritura, issues));
    }

    /// <summary>Las transacciones del registro único del período: Recaudo sin lo anulado (ni la fila de la anulación) y el punto de venta por su puerto.</summary>
    public async Task<IReadOnlyList<TransaccionReportable>> TransaccionesAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var filas = await db.ReceiptsCashTransactions.AsNoTracking()
            .Where(t => t.OccurredOn >= desde && t.OccurredOn <= hasta && t.DocumentKind != CashTransactionDocumentKind.Voiding).ToListAsync(ct);
        var recibos = filas.Where(f => f.DocumentKind == CashTransactionDocumentKind.Receipt).Select(f => f.DocumentPublicId).ToList();
        var recibosAnulados = (await db.IssuedReceipts.AsNoTracking().Where(r => recibos.Contains(r.PublicId) && r.Status == ReceiptState.Voided).Select(r => r.PublicId)
            .ToListAsync(ct)).ToHashSet();
        var egresos = filas.Where(f => f.DocumentKind == CashTransactionDocumentKind.CashDisbursement).Select(f => f.DocumentPublicId).ToList();
        var egresosAnulados = (await db.CashDisbursements.AsNoTracking().Where(e => egresos.Contains(e.PublicId) && e.Status == CashDisbursementStatus.Voided)
            .Select(e => e.PublicId).ToListAsync(ct)).ToHashSet();
        var lista = filas.Where(f => !recibosAnulados.Contains(f.DocumentPublicId) && !egresosAnulados.Contains(f.DocumentPublicId))
            .Select(f => new TransaccionReportable(f.PersonId, f.OccurredOn, Math.Abs(f.CashAmount), f.OtherMeansAmount, f.DocumentNumber, "Receipts")).ToList();
        if (puntoDeVenta is not null)
        {
            var pos = await puntoDeVenta.PorPeriodoAsync(desde, hasta, ct);
            var publicos = pos.Select(p => p.PersonPublicId).Distinct().ToList();
            var ids = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => publicos.Contains(p.PublicId)).ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
            lista.AddRange(pos.Where(p => ids.ContainsKey(p.PersonPublicId) && p.CashReceived + p.CashRefunded > 0m)
                .Select(p => new TransaccionReportable(ids[p.PersonPublicId], p.OperationDate, p.CashReceived + p.CashRefunded, 0m, p.DocumentNumber, "PointOfSale")));
        }
        return lista;
    }
}

/// <summary><c>GET /api/receipts/compliance/cash-transaction-reports?year=</c> (<c>Receipts.Sarlaft.Report</c>) (nuevo).</summary>
public sealed record ListCashTransactionReportsQuery(int? Year = null) : IRequest<Result<IReadOnlyList<CashTransactionReportDto>>>;

public sealed class ListCashTransactionReportsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListCashTransactionReportsQuery, Result<IReadOnlyList<CashTransactionReportDto>>>
{
    public async Task<Result<IReadOnlyList<CashTransactionReportDto>>> Handle(ListCashTransactionReportsQuery request, CancellationToken ct)
    {
        var consulta = db.CashTransactionReports.AsNoTracking();
        if (request.Year is { } a) consulta = consulta.Where(r => r.PeriodYear == a);
        var filas = await consulta.OrderByDescending(r => r.PeriodYear).ThenByDescending(r => r.PeriodMonth).ThenByDescending(r => r.Version).Take(200).ToListAsync(ct);
        return Result.Success<IReadOnlyList<CashTransactionReportDto>>(filas.Select(ReportesDeTransacciones.Dto).ToList());
    }
}

/// <summary>
/// <c>POST /api/receipts/compliance/cash-transaction-reports</c> (<c>Receipts.Sarlaft.Report</c>) (nuevo): genera el reporte de un mes. Con bloqueantes queda una versión
/// <c>Validated</c> sin archivo con sus inconsistencias; si no, <c>Generated</c> con el archivo (adjunto de módulo no borrable, subido antes de la fila). La anterior
/// vigente del período queda <c>Superseded</c>. Un período ya cargado no se regenera.
/// </summary>
public sealed record GenerateCashTransactionReportCommand(int Year, int Month) : IRequest<Result<CashTransactionReportDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class GenerateCashTransactionReportCommandValidator : AbstractValidator<GenerateCashTransactionReportCommand>
{
    public GenerateCashTransactionReportCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class GenerateCashTransactionReportCommandHandler(GeneracionDelReporteDeTransacciones generacion)
    : IRequestHandler<GenerateCashTransactionReportCommand, Result<CashTransactionReportDto>>
{
    public Task<Result<CashTransactionReportDto>> Handle(GenerateCashTransactionReportCommand request, CancellationToken ct) =>
        generacion.GenerarAsync(request.Year, request.Month, ct);
}

/// <summary><c>POST /api/receipts/compliance/cash-transaction-reports/{id}/regenerate</c> (<c>Receipts.Sarlaft.Report</c>): una versión nueva del mismo período. (nuevo)</summary>
public sealed record RegenerateCashTransactionReportCommand(Guid ReportPublicId) : IRequest<Result<CashTransactionReportDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegenerateCashTransactionReportCommandHandler(IApplicationDbContext db, GeneracionDelReporteDeTransacciones generacion)
    : IRequestHandler<RegenerateCashTransactionReportCommand, Result<CashTransactionReportDto>>
{
    public async Task<Result<CashTransactionReportDto>> Handle(RegenerateCashTransactionReportCommand request, CancellationToken ct)
    {
        var r = await db.CashTransactionReports.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.ReportPublicId, ct);
        if (r is null) return Result.Failure<CashTransactionReportDto>(ErroresDeRecaudo.NoEncontrado("El reporte"));
        return await generacion.GenerarAsync(r.PeriodYear, r.PeriodMonth, ct);
    }
}

/// <summary>La generación común de generar y regenerar. (nuevo)</summary>
public sealed class GeneracionDelReporteDeTransacciones(
    IApplicationDbContext db,
    IActorActual actorActual,
    PreparacionDelReporteDeTransacciones preparacion,
    ISender sender,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj)
{
    public async Task<Result<CashTransactionReportDto>> GenerarAsync(int anio, int mes, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashTransactionReportDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var delPeriodo = await db.CashTransactionReports.Where(r => r.PeriodYear == anio && r.PeriodMonth == mes).ToListAsync(ct);
        if (delPeriodo.FirstOrDefault(r => r.Status == CashTransactionReportStatus.Uploaded) is { } cargado)
            return Result.Failure<CashTransactionReportDto>(ErroresDelReporteDeTransacciones.YaCargado(cargado.Version));
        var prep = await preparacion.PrepararAsync(anio, mes, ct);
        if (prep.IsFailure) return Result.Failure<CashTransactionReportDto>(prep.Error);
        var p = prep.Value;
        var version = delPeriodo.Count == 0 ? 1 : delPeriodo.Max(r => r.Version) + 1;
        var reporte = new CashTransactionReport
        {
            PeriodYear = anio, PeriodMonth = mes, Version = version, Scope = p.Alcance, ApplicationState = p.Estado, LayoutCode = p.Layout.Code,
            LayoutVersion = p.Layout.Version, LayoutVerified = !p.Layout.HasUnverifiedFields, IsAbsenceReport = p.Ausencia, Lines = p.Lineas.Count,
            Total = p.Lineas.Sum(l => l.Valor), IssuesJson = JsonSerializer.Serialize(p.Issues, ReportesDeTransacciones.Json),
            BlockingIssues = p.Issues.Count(i => i.Severity == nameof(SeveridadDeInconsistencia.Blocking)),
            Warnings = p.Issues.Count(i => i.Severity == nameof(SeveridadDeInconsistencia.Warning)), GeneratedByUserId = usuario, GeneratedAt = reloj.UtcNow,
            Status = CashTransactionReportStatus.Validated,
        };
        if (p.Escritura is { Escrito: true, Contenido: { } bytes })
        {
            reporte.FileName = string.Create(CultureInfo.InvariantCulture, $"UIAF_{anio:0000}-{mes:00}_v{version}.txt");
            // El adjunto de módulo, antes de la fila: si el almacén falla no queda un reporte «generado» que nadie puede bajar.
            var subida = await sender.Send(new UploadAttachmentCommand(AdjuntosDeModulo.ReporteDeTransaccionesDeEfectivo, reporte.PublicId, reporte.FileName, "text/plain",
                bytes), ct);
            if (subida.IsFailure) return Result.Failure<CashTransactionReportDto>(subida.Error);
            (reporte.FileAttachmentPublicId, reporte.Sha256, reporte.Status) = (subida.Value, p.Escritura.Sha256, CashTransactionReportStatus.Generated);
        }
        var ahora = reloj.UtcNow;
        foreach (var vigente in delPeriodo.Where(r => r.Status is CashTransactionReportStatus.Validated or CashTransactionReportStatus.Generated))
            (vigente.Status, vigente.SupersededAt) = (CashTransactionReportStatus.Superseded, ahora);
        db.CashTransactionReports.Add(reporte);
        await db.SaveChangesAsync(ct);
        var n = 0;
        foreach (var l in p.Lineas)
            db.CashTransactionReportLines.Add(new CashTransactionReportLine
            {
                CashTransactionReportId = reporte.Id, LineNumber = ++n, Kind = l.Clase, PersonId = l.PersonaId, Amount = l.Valor, TransactionCount = l.Cantidad, OccurredOn = l.Fecha,
                DocumentNumber = l.Documento, DocumentsJson = JsonSerializer.Serialize(l.Documentos, ReportesDeTransacciones.Json), Source = l.Origen,
            });
        await auditoria.AgregarAsync("Receipts.Sarlaft.ReportGenerated", "CashTransactionReport", reporte.PublicId, null,
            new { period = string.Create(CultureInfo.InvariantCulture, $"{anio:0000}-{mes:00}"), version, status = reporte.Status.ToString(), scope = p.Alcance.ToString(),
                state = p.Estado.ToString(), layout = p.Layout.Code, verified = reporte.LayoutVerified, lines = reporte.Lines, blocking = reporte.BlockingIssues,
                absence = p.Ausencia, sha256 = reporte.Sha256, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(ReportesDeTransacciones.Dto(reporte));
    }
}

/// <summary>
/// <c>POST /api/receipts/compliance/cash-transaction-reports/{id}/uploaded</c> (<c>Receipts.Sarlaft.Report</c>) (nuevo): marca cargado en el portal un reporte generado, con
/// el radicado, su fecha y, si se adjuntó, el comprobante. Desde ahí el período no se regenera.
/// </summary>
public sealed record MarkCashTransactionReportUploadedCommand(Guid ReportPublicId, string Reference, DateTime? UploadedAt = null, Guid? SupportAttachmentPublicId = null)
    : IRequest<Result<CashTransactionReportDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class MarkCashTransactionReportUploadedCommandValidator : AbstractValidator<MarkCashTransactionReportUploadedCommand>
{
    public MarkCashTransactionReportUploadedCommandValidator()
    {
        RuleFor(x => x.ReportPublicId).NotEmpty();
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(60).WithMessage("Indique el radicado del cargue.");
    }
}

public sealed class MarkCashTransactionReportUploadedCommandHandler(IApplicationDbContext db, IActorActual actorActual, AuditoriaDeRecaudo auditoria, IDateTimeService reloj)
    : IRequestHandler<MarkCashTransactionReportUploadedCommand, Result<CashTransactionReportDto>>
{
    public async Task<Result<CashTransactionReportDto>> Handle(MarkCashTransactionReportUploadedCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashTransactionReportDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var r = await db.CashTransactionReports.FirstOrDefaultAsync(x => x.PublicId == request.ReportPublicId, ct);
        if (r is null) return Result.Failure<CashTransactionReportDto>(ErroresDeRecaudo.NoEncontrado("El reporte"));
        if (r.Status == CashTransactionReportStatus.Uploaded && r.UploadReference == request.Reference.Trim()) return Result.Success(ReportesDeTransacciones.Dto(r));
        if (r.Status != CashTransactionReportStatus.Generated) return Result.Failure<CashTransactionReportDto>(ErroresDelReporteDeTransacciones.NoGenerado(r.Status));
        (r.Status, r.UploadReference, r.UploadedAt, r.UploadedByUserId, r.UploadSupportAttachmentPublicId) =
            (CashTransactionReportStatus.Uploaded, request.Reference.Trim(), request.UploadedAt ?? reloj.UtcNow, usuario, request.SupportAttachmentPublicId);
        await auditoria.AgregarAsync("Receipts.Sarlaft.ReportUploaded", "CashTransactionReport", r.PublicId, null,
            new { reference = r.UploadReference, uploadedAt = r.UploadedAt, support = r.UploadSupportAttachmentPublicId, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(ReportesDeTransacciones.Dto(r));
    }
}

/// <summary>
/// <c>GET /api/receipts/compliance/cash-transaction-reports/{id}/file</c> (<c>Receipts.Sarlaft.Report</c>) (nuevo): el enlace firmado del archivo, con la lectura sensible
/// <c>Receipts.Sarlaft.ReportRead</c> (trae datos de personas).
/// </summary>
public sealed record GetCashTransactionReportFileQuery(Guid ReportPublicId) : IRequest<Result<EnlaceDeDescargaDto>>;

public sealed class GetCashTransactionReportFileQueryHandler(IApplicationDbContext db, AuditoriaDeRecaudo auditoria, IBlobStore store, IDateTimeService reloj,
    IOptions<LimitesDeAdjuntos> limites) : IRequestHandler<GetCashTransactionReportFileQuery, Result<EnlaceDeDescargaDto>>
{
    public async Task<Result<EnlaceDeDescargaDto>> Handle(GetCashTransactionReportFileQuery request, CancellationToken ct)
    {
        var r = await db.CashTransactionReports.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.ReportPublicId, ct);
        if (r?.FileAttachmentPublicId is not { } id) return Result.Failure<EnlaceDeDescargaDto>(ErroresDeRecaudo.NoEncontrado("El archivo del reporte"));
        var adjunto = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == id, ct);
        if (adjunto is null || adjunto.Status != EstadoDeAdjunto.Available) return Result.Failure<EnlaceDeDescargaDto>(ErroresDeRecaudo.NoEncontrado("El archivo del reporte"));
        await auditoria.ReporteDeEfectivoLeidoAsync(r.PublicId, r.PeriodYear, r.PeriodMonth, r.Version, r.Lines, ct);
        if (adjunto.Format == FormatoDeAdjunto.AppEncrypted) return Result.Success(EnlaceDeDescargaDto.PorLaApi);
        return Result.Success(await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct, nombre: r.FileName));
    }
}
