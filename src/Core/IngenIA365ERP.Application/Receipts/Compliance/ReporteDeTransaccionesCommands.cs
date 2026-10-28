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
// La estructura es dato versionado (Layouts/uiaf-trso-*.json) sobre el motor genérico Domain/Common/Layouts. Desde la segunda ronda del 2026-10-27 es el Anexo 2 TRSO de
// la Supersolidaria: ancho fijo de 645 posiciones en ANSI CP1252, encabezado, una línea por transacción y cola, cotejado con el PDF (verified); si una versión nueva
// llega sin cotejar, cada generación lleva Recaudo.Sarlaft.LayoutSinCotejar como advertencia, nunca en el archivo. Las transacciones: el efectivo del registro único (lo
// de Recaudo sin lo anulado y lo del punto de venta por su puerto, sin copiar filas), los cheques en ventanilla y la plataforma virtual de los recibos. Alcance, estado
// del período, código de la organización, usuario SIREL, códigos de producto y el recordatorio de la ausencia (que se presenta en SIREL), por los Sarlaft.Reporte.*.

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

    /// <summary>La moneda del reporte (ISO 4217): el ERP lleva todo en pesos.</summary>
    public const string Moneda = "COP";

    public const string CodigoSinCodigoDeOrganizacion = "Recaudo.Sarlaft.CodigoDeOrganizacionFaltante";
    public const string CodigoSinUsuarioSirel = "Recaudo.Sarlaft.UsuarioSirelFaltante";
    public const string CodigoAusenciaEnSirel = "Recaudo.Sarlaft.AusenciaEnSirel";

    /// <summary>El alcance del parámetro <c>Sarlaft.Reporte.Alcance</c> («Todas» es el nombre anterior de los tres medios).</summary>
    public static CashTransactionReportScope Alcance(string? valor) => valor?.Trim() switch
    {
        "SoloEfectivo" => CashTransactionReportScope.CashOnly,
        "EfectivoYCheque" => CashTransactionReportScope.CashAndCheck,
        _ => CashTransactionReportScope.All,
    };

    public static CashTransactionReportDto Dto(CashTransactionReport r) => new(r.PublicId, r.PeriodYear, r.PeriodMonth, r.Version, r.Scope, r.ApplicationState, r.LayoutCode,
        r.LayoutVerified, r.Status, r.IsAbsenceReport, r.Lines, r.Total, r.BlockingIssues, r.Warnings, r.FileName, r.Sha256, r.UploadReference, r.UploadedAt, r.GeneratedAt,
        JsonSerializer.Deserialize<List<InconsistenciaDelReporteDto>>(r.IssuesJson, Json) ?? []);

    /// <summary>La persona como la pide el anexo.</summary>
    public static PersonaParaLaUiaf Persona(Domain.Entities.Core.Person p) => DatosParaLaUiaf.Persona(p.IdType, p.TaxId, p.TaxIdCheckDigit, p.PersonType, p.FirstName,
        p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName);

    /// <summary>El detalle de una línea que se guarda en <c>DocumentsJson</c> (el archivo no dice por qué entró ni de dónde vino).</summary>
    public sealed record DetalleDeLinea(string Document, int Means, int Direction, string? ProductKey, string? ProductNumber, string Source);
}

/// <summary>
/// Arma el reporte de un mes (feature 015, R1, T0390; reescrito al Anexo 2 TRSO en la segunda ronda del 2026-10-27): las transacciones del período, las líneas por
/// <see cref="ReporteDeTransacciones"/>, las filas del layout (encabezado, una por transacción y cola) y la escritura con su validación. No escribe en la base. (nuevo)
/// </summary>
public sealed class PreparacionDelReporteDeTransacciones(IApplicationDbContext db, ParametrosDeCaja parametros, IEfectivoDelPuntoDeVenta? puntoDeVenta = null)
{
    public sealed record Preparado(LayoutDeArchivo Layout, CashTransactionReportScope Alcance, CashTransactionReportApplication Estado, bool Ausencia,
        IReadOnlyList<LineaDelReporte> Lineas, ResultadoDeEscritura? Escritura, IReadOnlyList<InconsistenciaDelReporteDto> Issues, string? NombreDelArchivo = null);

    public async Task<Result<Preparado>> PrepararAsync(int anio, int mes, CancellationToken ct)
    {
        var desde = new DateOnly(anio, mes, 1);
        var hasta = desde.AddMonths(1).AddDays(-1);
        var layout = CatalogoDeLayoutsDeRecaudo.Embebido.Vigente(hasta);
        if (layout is null) return Result.Failure<Preparado>(ErroresDelReporteDeTransacciones.SinLayout(anio, mes));
        var alcance = ReportesDeTransacciones.Alcance(await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.SarlaftReporteAlcance, hasta, ct));
        var estado = string.Equals(await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.SarlaftReporteEstado, hasta, ct), "Suspendido", StringComparison.OrdinalIgnoreCase)
            ? CashTransactionReportApplication.Suspended
            : CashTransactionReportApplication.Active;
        var recordarAusencia = await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.SarlaftReporteExigeAusencia, hasta, ct);
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
        if (lineas.Count == 0)
        {
            // El reporte de ausencia no es un archivo: se presenta en SIREL (Anexo 2 TRSO §5.1). Aquí sólo se recuerda, si la cooperativa lo pide.
            issues.Add(recordarAusencia
                ? new InconsistenciaDelReporteDto("Warning", ReportesDeTransacciones.CodigoAusenciaEnSirel, string.Create(CultureInfo.InvariantCulture,
                    $"No hay transacciones que reportar en {anio:0000}-{mes:00}: presente el reporte de ausencia en SIREL dentro de los 20 días calendario siguientes al corte y registre aquí su radicado."),
                    null, null)
                : new InconsistenciaDelReporteDto("Warning", ErroresDelReporteDeTransacciones.CodigoSinTransacciones,
                    "No hay transacciones que reportar en el período.", null, null));
            return Result.Success(new Preparado(layout, alcance, estado, recordarAusencia, lineas, null, issues));
        }

        var codigo = (await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.SarlaftReporteCodigoDeOrganizacion, hasta, ct)).Trim();
        if (codigo.Length == 0)
            issues.Add(new InconsistenciaDelReporteDto(nameof(SeveridadDeInconsistencia.Blocking), ReportesDeTransacciones.CodigoSinCodigoDeOrganizacion,
                "Falta el código de la organización ante la UIAF (SSTTTCCCCC, el que asigna la Supersolidaria): regístrelo en el parámetro Sarlaft.Reporte.CodigoDeOrganizacion de Recaudo.",
                null, null));
        if ((await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.SarlaftReporteUsuarioSirel, hasta, ct)).Trim().Length == 0)
            issues.Add(new InconsistenciaDelReporteDto(nameof(SeveridadDeInconsistencia.Blocking), ReportesDeTransacciones.CodigoSinUsuarioSirel,
                "Falta el usuario SIREL con que la cooperativa carga el reporte: regístrelo en el parámetro Sarlaft.Reporte.UsuarioSirel de Recaudo.", null, null));
        var mapa = await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.SarlaftReporteTiposDeProducto, hasta, ct);

        var personas = lineas.SelectMany(l => new[] { l.PersonaId, l.Transaccion.Realiza }).Distinct().ToList();
        var porPersona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var sucursales = lineas.Select(l => l.Transaccion.SucursalId).OfType<int>().Distinct().ToList();
        var porSucursal = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => sucursales.Contains(b.Id)).ToDictionaryAsync(b => b.Id, ct);
        var encabezado = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Encabezado.CodigoDeOrganizacion"] = codigo, ["Encabezado.FechaDeCorte"] = hasta, ["Encabezado.TotalDeRegistros"] = lineas.Count,
        };
        var filas = new List<FilaDeLayout> { new("Header", null, encabezado) };
        var n = 0;
        foreach (var l in lineas)
        {
            var t = l.Transaccion;
            var titular = ReportesDeTransacciones.Persona(porPersona[t.PersonaId]);
            var realiza = ReportesDeTransacciones.Persona(porPersona[t.Realiza]);
            var sucursal = t.SucursalId is { } s ? porSucursal.GetValueOrDefault(s) : null;
            var municipio = DatosParaLaUiaf.SoloAlfanumerico(sucursal?.MunicipalityDaneCode);
            var oficina = sucursal is null ? null : DatosParaLaUiaf.SoloAlfanumerico(sucursal.LegacyCode) is { Length: > 0 } propio
                ? propio : sucursal.Id.ToString(CultureInfo.InvariantCulture);
            filas.Add(new FilaDeLayout("Detail", porPersona[t.PersonaId].TaxId, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Linea.Consecutivo"] = ++n, ["Linea.Fecha"] = t.Fecha, ["Linea.Valor"] = decimal.Round(t.Valor, 0, MidpointRounding.AwayFromZero),
                ["Linea.Moneda"] = ReportesDeTransacciones.Moneda, ["Linea.Oficina"] = oficina,
                ["Linea.TipoDeProducto"] = DatosParaLaUiaf.TipoDeProducto(mapa, t.ClaveDeProducto, t.NumeroDeProducto),
                ["Linea.TipoDeTransaccion"] = t.Sentido == CashTransactionDirection.Out ? 1 : 2, ["Linea.Medio"] = (int)t.Medio,
                ["Linea.NumeroDeProducto"] = DatosParaLaUiaf.SoloAlfanumerico(t.NumeroDeProducto),
                ["Linea.Municipio"] = municipio.Length == 0 ? null : municipio,
                ["Titular.TipoDeIdentificacion"] = titular.TipoDeIdentificacion, ["Titular.NumeroDeIdentificacion"] = titular.NumeroDeIdentificacion,
                ["Titular.DigitoDeVerificacion"] = titular.DigitoDeVerificacion, ["Titular.Nombres"] = titular.Nombres, ["Titular.Apellidos"] = titular.Apellidos,
                ["Titular.RazonSocial"] = titular.RazonSocial,
                ["Realiza.TipoDeIdentificacion"] = realiza.TipoDeIdentificacion, ["Realiza.NumeroDeIdentificacion"] = realiza.NumeroDeIdentificacion,
                ["Realiza.DigitoDeVerificacion"] = realiza.DigitoDeVerificacion, ["Realiza.Nombres"] = realiza.Nombres, ["Realiza.Apellidos"] = realiza.Apellidos,
                ["Realiza.RazonSocial"] = realiza.RazonSocial,
            }));
        }
        filas.Add(new FilaDeLayout("Trailer", null, encabezado));
        var escritura = EscritorPorLayout.Escribir(layout, filas);
        issues.AddRange(escritura.Inconsistencias.Select(i => new InconsistenciaDelReporteDto(i.Severidad.ToString(), i.Codigo, i.Mensaje, i.Fila, i.NombreDelCampo)));
        return Result.Success(new Preparado(layout, alcance, estado, false, lineas, escritura, issues,
            codigo.Length == 0 ? null : DatosParaLaUiaf.NombreDelArchivo(codigo, anio, mes)));
    }

    /// <summary>
    /// Las transacciones del período (segunda ronda del 2026-10-27): el efectivo del registro único —recibos y egresos de caja sin lo anulado (ni la fila de la anulación) y
    /// el punto de venta por su puerto, lo recibido como depósito y lo devuelto como retiro—, los cheques recibidos en ventanilla y lo recibido por plataforma virtual
    /// (canal en línea). Cada una con su titular, quien la realiza, su sentido, el producto principal que la vincula (la aplicación de mayor valor del recibo) y su oficina.
    /// </summary>
    public async Task<IReadOnlyList<TransaccionReportable>> TransaccionesAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var filas = await db.ReceiptsCashTransactions.AsNoTracking()
            .Where(t => t.OccurredOn >= desde && t.OccurredOn <= hasta && t.DocumentKind != CashTransactionDocumentKind.Voiding).ToListAsync(ct);
        var recibosDelEfectivo = filas.Where(f => f.DocumentKind == CashTransactionDocumentKind.Receipt).Select(f => f.DocumentPublicId).ToList();
        var recibos = await db.IssuedReceipts.AsNoTracking()
            .Where(r => recibosDelEfectivo.Contains(r.PublicId) || r.ValueDate >= desde && r.ValueDate <= hasta && r.Status != ReceiptState.Voided).ToListAsync(ct);
        var anulados = recibos.Where(r => r.Status == ReceiptState.Voided).Select(r => r.PublicId).ToHashSet();
        var vivos = recibos.Where(r => r.Status != ReceiptState.Voided).ToList();
        var idsDeRecibos = vivos.Select(r => r.Id).ToList();
        var aplicaciones = (await db.ReceiptApplications.AsNoTracking().Where(a => idsDeRecibos.Contains(a.ReceiptId)).ToListAsync(ct))
            .GroupBy(a => a.ReceiptId).ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Amount).ThenBy(a => a.Ordinal).First());
        var medios = (await db.ReceiptMeans.AsNoTracking().Where(m => idsDeRecibos.Contains(m.ReceiptId) && m.Status == ReceiptMeansState.Active).ToListAsync(ct))
            .GroupBy(m => m.ReceiptId).ToDictionary(g => g.Key, g => g.ToList());
        var porPublico = vivos.ToDictionary(r => r.PublicId);

        var egresosIds = filas.Where(f => f.DocumentKind == CashTransactionDocumentKind.CashDisbursement).Select(f => f.DocumentPublicId).ToList();
        var egresos = await db.CashDisbursements.AsNoTracking().Where(e => egresosIds.Contains(e.PublicId)).ToDictionaryAsync(e => e.PublicId, ct);

        var lista = new List<TransaccionReportable>();
        foreach (var f in filas)
        {
            if (f.DocumentKind == CashTransactionDocumentKind.Receipt)
            {
                if (anulados.Contains(f.DocumentPublicId)) continue;
                var recibo = porPublico.GetValueOrDefault(f.DocumentPublicId);
                if (recibo?.Channel == ReceiptChannel.Online) continue;
                var principal = recibo is null ? null : aplicaciones.GetValueOrDefault(recibo.Id);
                lista.Add(new TransaccionReportable(f.PersonId, f.OccurredOn, Math.Abs(f.CashAmount), MedioDeTransaccionReportable.Efectivo, CashTransactionDirection.In,
                    f.DocumentNumber, "Receipts", f.DeliveredByPersonId ?? f.PayerPersonId, principal?.Target.ToString(), principal?.ProductNumber ?? recibo?.Number ?? f.DocumentNumber,
                    f.BranchId));
            }
            else if (f.DocumentKind == CashTransactionDocumentKind.CashDisbursement)
            {
                var egreso = egresos.GetValueOrDefault(f.DocumentPublicId);
                if (egreso?.Status == CashDisbursementStatus.Voided) continue;
                var clave = egreso?.ProductNature switch
                {
                    "Loan" => nameof(ApplicationTarget.Loan),
                    "Deposit" => nameof(ApplicationTarget.Account),
                    _ => nameof(ApplicationTarget.MiscConcept),
                };
                lista.Add(new TransaccionReportable(f.PersonId, f.OccurredOn, Math.Abs(f.CashAmount), MedioDeTransaccionReportable.Efectivo, CashTransactionDirection.Out,
                    f.DocumentNumber, "Receipts", f.ReceivedByPersonId ?? f.PayerPersonId, clave, egreso?.ProductNumber ?? egreso?.Number ?? f.DocumentNumber, f.BranchId));
            }
        }

        // Los cheques en ventanilla y la plataforma virtual salen del recibo mismo: el registro único es sólo del efectivo.
        foreach (var recibo in vivos.Where(r => r.ValueDate >= desde && r.ValueDate <= hasta))
        {
            var principal = aplicaciones.GetValueOrDefault(recibo.Id);
            var titular = principal?.BeneficiaryPersonId ?? recibo.PayerPersonId;
            var realiza = recibo.DeliveredByPersonId ?? recibo.PayerPersonId;
            var delRecibo = medios.GetValueOrDefault(recibo.Id) ?? [];
            if (recibo.Channel == ReceiptChannel.Online)
            {
                var valor = recibo.TotalMeans - recibo.ChangeGiven;
                if (valor > 0m)
                    lista.Add(new TransaccionReportable(titular, recibo.ValueDate, valor, MedioDeTransaccionReportable.PlataformaVirtual, CashTransactionDirection.In,
                        Corto(recibo.Number), "Receipts", realiza, principal?.Target.ToString(), principal?.ProductNumber ?? recibo.Number, recibo.BranchId));
                continue;
            }
            var cheques = delRecibo.Where(m => m.MeansClass == PaymentMeansClass.Check).Sum(m => m.Amount);
            if (cheques > 0m)
                lista.Add(new TransaccionReportable(titular, recibo.ValueDate, cheques, MedioDeTransaccionReportable.Cheque, CashTransactionDirection.In, Corto(recibo.Number),
                    "Receipts", realiza, principal?.Target.ToString(), principal?.ProductNumber ?? recibo.Number, recibo.BranchId));
        }

        if (puntoDeVenta is not null)
        {
            var pos = await puntoDeVenta.PorPeriodoAsync(desde, hasta, ct);
            var publicos = pos.Select(p => p.PersonPublicId).Distinct().ToList();
            var ids = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => publicos.Contains(p.PublicId)).ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
            var oficinas = pos.Select(p => p.BranchPublicId).OfType<Guid>().Distinct().ToList();
            var porOficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinas.Contains(b.PublicId)).ToDictionaryAsync(b => b.PublicId, b => b.Id, ct);
            foreach (var p in pos.Where(p => ids.ContainsKey(p.PersonPublicId)))
            {
                int? oficina = p.BranchPublicId is { } b && porOficina.TryGetValue(b, out var id) ? id : null;
                if (p.CashReceived > 0m)
                    lista.Add(new TransaccionReportable(ids[p.PersonPublicId], p.OperationDate, p.CashReceived, MedioDeTransaccionReportable.Efectivo, CashTransactionDirection.In,
                        p.DocumentNumber, "PointOfSale", null, "PointOfSale", p.DocumentNumber, oficina));
                if (p.CashRefunded > 0m)
                    lista.Add(new TransaccionReportable(ids[p.PersonPublicId], p.OperationDate, p.CashRefunded, MedioDeTransaccionReportable.Efectivo, CashTransactionDirection.Out,
                        p.DocumentNumber, "PointOfSale", null, "PointOfSale", p.DocumentNumber, oficina));
            }
        }
        return lista;
    }

    private static string Corto(string numero) => numero.Length <= 30 ? numero : numero[..30];
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
        if (p.Escritura is { Escrito: true, Contenido: { } bytes } && reporte.BlockingIssues == 0 && p.NombreDelArchivo is { } nombre)
        {
            // El nombre del anexo, TRSOSSTTTCCCCCMMAA.txt: el mismo en cada versión; lo que las distingue es la generación dueña del adjunto.
            reporte.FileName = nombre;
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
                CashTransactionReportId = reporte.Id, LineNumber = ++n, Kind = l.Clase, PersonId = l.PersonaId, Amount = l.Valor, TransactionCount = 1,
                OccurredOn = l.Transaccion.Fecha, DocumentNumber = l.Transaccion.Documento,
                DocumentsJson = JsonSerializer.Serialize(new[]
                {
                    new ReportesDeTransacciones.DetalleDeLinea(l.Transaccion.Documento, (int)l.Transaccion.Medio, l.Transaccion.Sentido == CashTransactionDirection.Out ? 1 : 2,
                        l.Transaccion.ClaveDeProducto, l.Transaccion.NumeroDeProducto, l.Transaccion.Origen),
                }, ReportesDeTransacciones.Json),
                Source = l.Transaccion.Origen,
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
        // La ausencia no tiene archivo (se presenta en SIREL): su generación sin bloqueantes recibe el radicado igual que un archivo generado.
        var ausenciaPorRadicar = r is { IsAbsenceReport: true, Status: CashTransactionReportStatus.Validated, BlockingIssues: 0 };
        if (r.Status != CashTransactionReportStatus.Generated && !ausenciaPorRadicar)
            return Result.Failure<CashTransactionReportDto>(ErroresDelReporteDeTransacciones.NoGenerado(r.Status));
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
