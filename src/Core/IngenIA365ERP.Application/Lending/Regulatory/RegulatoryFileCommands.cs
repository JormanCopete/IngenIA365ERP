using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.Layouts;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Lending.Regulatory;

// Feature 013, K5 (US16, T1004; api.md §18; adaptadores-y-archivos.md §7.5; data-model §28; FR-136, FR-137, SC-012, SC-019, D28, C46): validar,
// generar, regenerar, marcar cargado, listar, leer y descargar el formato de cartera del SICSES y los archivos de las centrales de riesgo. Molde: la
// PILA de la 010 (generaciones Validated/Generated/Superseded/Uploaded) y el archivo de descuentos de K2 (numerador, adjunto antes de la fila, evento
// resumen). (nuevo)

// ------------------------------------------------------------------------------------------------------------------------------ DTOs --

/// <summary>Una inconsistencia o advertencia (api.md §18 <c>issues[]</c>): severidad, código, registro, campo, crédito, ruta, fila y mensaje. (nuevo)</summary>
public sealed record RegulatoryIssueDto(
    string Severity,
    string Code,
    string? Record,
    int? Field,
    string? FieldName,
    string? Loan,
    string? Path,
    int? Row,
    string Message);

/// <summary>Una advertencia de la generación (<c>LayoutSinCotejar</c>, <c>ModeloObligatorioNoDisponible</c>…); nunca va dentro del archivo. (nuevo)</summary>
public sealed record RegulatoryWarningDto(string Code, string Message);

/// <summary>Un titular excluido del archivo de una central, con su motivo y la gestión sugerida (vista <c>bureau-exclusions</c>). (nuevo)</summary>
public sealed record RegulatoryExcludedDto(string Loan, Guid LoanPublicId, Guid PersonPublicId, bool IsDebtor, string Reason, string SuggestedAction);

/// <summary>Lo que responde validar (api.md §18): el layout, las filas, las inconsistencias, las advertencias, los excluidos y el plazo. (nuevo)</summary>
public sealed record RegulatoryValidationDto(
    string Layout,
    int Rows,
    int Blocking,
    IReadOnlyList<RegulatoryIssueDto> Issues,
    IReadOnlyList<RegulatoryWarningDto> Warnings,
    IReadOnlyList<RegulatoryExcludedDto> Excluded,
    DateOnly? DueDate);

public sealed record RegulatoryFileCountsDto(int Rows, int Blocking, int Warnings, int Excluded);

public sealed record RegulatoryFileUploadDto(string FilingNumber, DateOnly? FilingDate, DateTime? At, string? By);

/// <summary>Una generación regulatoria (api.md §18 <c>RegulatoryFileDto</c>). (nuevo)</summary>
public sealed record RegulatoryFileDto(
    Guid PublicId,
    RegulatoryFileKind Kind,
    string? BureauCode,
    int Year,
    int Month,
    int Version,
    RegulatoryFileStatus Status,
    string LayoutCode,
    IReadOnlyList<RegulatoryWarningDto> Warnings,
    RegulatoryFileCountsDto Counts,
    string? FileName,
    string? Sha256,
    bool DownloadAvailable,
    string GeneratedBy,
    DateTime GeneratedAt,
    RegulatoryFileUploadDto? Uploaded);

/// <summary>Una generación con sus inconsistencias, sus excluidos y sus totales (<c>GET /files/{id}</c>, y lo que responde generar). (nuevo)</summary>
public sealed record RegulatoryFileDetailDto(
    RegulatoryFileDto File,
    IReadOnlyList<RegulatoryIssueDto> Issues,
    IReadOnlyList<RegulatoryExcludedDto> Excluded,
    JsonElement Totals);

// ---------------------------------------------------------------------------------------------------------------------------- errores --

/// <summary>Los errores de los archivos regulatorios de Cartera (api.md §18). Mensajes en español y sin datos del deudor. (nuevo)</summary>
public static class ErroresDeArchivosRegulatorios
{
    public const string CodigoSinCierre = "Lending.MonthlyClose.NotFound";
    public const string CodigoCentralDesconocida = "Lending.Regulatory.BureauUnknown";
    public const string CodigoSinLayout = "Lending.Regulatory.LayoutUnavailable";
    public const string CodigoYaCargado = "Lending.Regulatory.AlreadyUploaded";
    public const string CodigoBloqueantes = "Lending.Regulatory.BlockingIssues";
    public const string CodigoInexistente = "Lending.RegulatoryFile.NotFound";
    public const string CodigoSinArchivo = "Lending.RegulatoryFile.NoFile";
    public const string CodigoNoGenerado = "Lending.RegulatoryFile.NotGenerated";

    private static string Periodo(int anio, int mes) => string.Create(CultureInfo.InvariantCulture, $"{anio:0000}-{mes:00}");

    public static Error SinCierre(int anio, int mes) => new ErrorConDatos(CodigoSinCierre,
        $"Cartera no tiene cierre mensual vigente de {Periodo(anio, mes)}: el formato del SICSES sale del cierre del mes. Cierre el mes (o vuelva a cerrarlo si se reabrió).",
        new { period = Periodo(anio, mes) });

    public static Error CentralDesconocida(string central, IReadOnlyList<string> conocidas) => new ErrorConDatos(CodigoCentralDesconocida,
        $"No hay layout de la central «{central}». Centrales con layout: {string.Join(", ", conocidas)}.", new { bureauCode = central, known = conocidas });

    public static Error SinLayout(RegulatoryFileKind clase, string? central, DateOnly fin) => new ErrorConDatos(CodigoSinLayout,
        $"No hay layout {(clase == RegulatoryFileKind.Sicses ? "del SICSES" : $"de {central}")} vigente al {fin:yyyy-MM-dd}.",
        new { kind = clase.ToString(), bureauCode = central, periodEnd = fin });

    public static Error YaCargado(RegulatoryFile cargado) => new ErrorConDatos(CodigoYaCargado,
        $"El archivo de {Periodo(cargado.PeriodYear, cargado.PeriodMonth)} (versión {cargado.Version}) ya se marcó cargado con el radicado {cargado.FilingNumber}: "
        + "el período no se regenera; la corrección va en el período siguiente.",
        new { regulatoryFilePublicId = cargado.PublicId, period = Periodo(cargado.PeriodYear, cargado.PeriodMonth), version = cargado.Version, filingNumber = cargado.FilingNumber });

    /// <summary>
    /// La respuesta 422 de una generación con bloqueantes (api.md §18): la generación <c>Validated</c> sin archivo <b>queda guardada</b> —por eso el
    /// comando responde con éxito y la ruta traduce un <see cref="RegulatoryFileStatus.Validated"/> a este error—.
    /// </summary>
    public static Error Bloqueantes(RegulatoryFileDetailDto generacion) => new ErrorConDatos(CodigoBloqueantes,
        $"El archivo tiene {generacion.File.Counts.Blocking} inconsistencias que impiden generarlo: corríjalas y vuelva a generar.",
        new
        {
            regulatoryFilePublicId = generacion.File.PublicId, version = generacion.File.Version,
            issues = generacion.Issues.Where(i => i.Severity == nameof(SeveridadDeInconsistencia.Blocking)).ToList(),
        });

    /// <summary>Generar sin reconocer las advertencias (api.md §1.9): el código es el de la primera (casi siempre <c>LayoutSinCotejar</c>).</summary>
    public static Error AdvertenciasSinReconocer(IReadOnlyList<RegulatoryWarningDto> advertencias) => new ErrorConDatos(advertencias[0].Code,
        string.Join(" ", advertencias.Select(a => a.Message)) + " Reconózcalo para generar.", new { warnings = advertencias });

    public static Error Inexistente() => new(CodigoInexistente, "El archivo regulatorio no existe.");

    public static Error SinArchivo(Guid id) => new ErrorConDatos(CodigoSinArchivo,
        "Esta generación quedó validada con inconsistencias y no tiene archivo para descargar.", new { regulatoryFilePublicId = id });

    public static Error NoGenerado(RegulatoryFileStatus estado) => new ErrorConDatos(CodigoNoGenerado,
        $"Sólo un archivo generado se marca cargado; éste está «{estado}».", new { status = estado.ToString() });
}

// -------------------------------------------------------------------------------------------------------------------- preparación --

/// <summary>Un archivo regulatorio preparado: el layout, las filas, lo que escribiría, las advertencias y lo que lo originó. (nuevo)</summary>
public sealed record ArchivoRegulatorioPreparado(
    RegulatoryFileKind Clase,
    string? Central,
    int Anio,
    int Mes,
    LayoutDeCartera Layout,
    IReadOnlyList<FilaDeLayout> Filas,
    ResultadoDeEscritura Escritura,
    IReadOnlyList<AdvertenciaRegulatoria> Advertencias,
    ProyeccionDelSicses? Sicses,
    ProyeccionDeCentral? DeCentral)
{
    public int Bloqueantes => Escritura.Bloqueantes;

    public bool HayBloqueantes => Bloqueantes > 0;

    /// <summary>Las del validador (bloqueantes y advertencias) seguidas de las advertencias de la generación.</summary>
    public IReadOnlyList<RegulatoryIssueDto> Inconsistencias =>
        Escritura.Inconsistencias.Select(i => new RegulatoryIssueDto(i.Severidad.ToString(), i.Codigo, i.Registro, i.Campo, i.NombreDelCampo, i.Credito, i.Ruta,
                i.Fila, i.Mensaje))
            .Concat(Advertencias.Select(a => new RegulatoryIssueDto(nameof(SeveridadDeInconsistencia.Warning), a.Code, null, null, null, null, null, null, a.Message)))
            .ToList();

    /// <summary>Todo lo que hay que reconocer para generar: las advertencias de la generación y las del validador.</summary>
    public IReadOnlyList<RegulatoryWarningDto> AReconocer =>
        Advertencias.Select(a => new RegulatoryWarningDto(a.Code, a.Message))
            .Concat(Escritura.Inconsistencias.Where(i => i.Severidad == SeveridadDeInconsistencia.Warning).Select(i => new RegulatoryWarningDto(i.Codigo, i.Mensaje)))
            .ToList();

    public int Lineas => DeCentral is null ? Filas.Count : Filas.Count(f => f.Registro == FilasDeCentral.Detalle);

    public IReadOnlyList<RegulatoryExcludedDto> Excluidos => DeCentral is null
        ? []
        : DeCentral.Excluidos.Select(x => new RegulatoryExcludedDto(x.NumeroDeObligacion, x.LoanPublicId, x.PersonPublicId, x.EsDeudor, x.MotivoDeExclusion!,
            ArchivosRegulatorios.GestionSugerida(x.MotivoDeExclusion!))).ToList();

    public DateOnly? Plazo => Sicses?.Plazo?.Limite;
}

/// <summary>
/// La preparación común de validar y generar (K5, T1004): el layout vigente por clase, central y fin del período (<see cref="CatalogoDeLayoutsDeCartera"/>;
/// sin él, <c>Lending.Regulatory.LayoutUnavailable</c>; central sin layout, <c>Lending.Regulatory.BureauUnknown</c>), la proyección, las filas y lo que
/// escribiría el motor con sus inconsistencias, más la advertencia <c>LayoutSinCotejar</c> si algún campo no está cotejado. Sólo lee. (nuevo)
/// </summary>
public sealed class PreparacionDeArchivoRegulatorio(
    CatalogoDeLayoutsDeCartera catalogo,
    ProyeccionSicsesCartera sicses,
    ProyeccionCentralDeRiesgo centrales)
{
    public async Task<Result<ArchivoRegulatorioPreparado>> SicsesAsync(int anio, int mes, CancellationToken ct)
    {
        var fin = MonthlyClose.UltimoDiaDe(anio, mes);
        var layout = catalogo.Vigente(RegulatoryFileKind.Sicses, null, fin);
        if (layout is null) return Falla(ErroresDeArchivosRegulatorios.SinLayout(RegulatoryFileKind.Sicses, null, fin));
        var p = await sicses.ProyectarAsync(anio, mes, ct);
        if (p.IsFailure) return Falla(p.Error);
        return Result.Success(Armar(RegulatoryFileKind.Sicses, null, anio, mes, layout, p.Value.Filas, p.Value.Advertencias, p.Value, null));
    }

    public async Task<Result<ArchivoRegulatorioPreparado>> CentralAsync(string central, int anio, int mes, CancellationToken ct)
    {
        central = central.Trim().ToUpperInvariant();
        if (!catalogo.ConoceLaCentral(central)) return Falla(ErroresDeArchivosRegulatorios.CentralDesconocida(central, catalogo.Centrales));
        var fin = MonthlyClose.UltimoDiaDe(anio, mes);
        var layout = catalogo.Vigente(RegulatoryFileKind.CreditBureau, central, fin);
        if (layout is null) return Falla(ErroresDeArchivosRegulatorios.SinLayout(RegulatoryFileKind.CreditBureau, central, fin));
        var p = await centrales.ProyectarAsync(central, anio, mes, ct);
        if (p.IsFailure) return Falla(p.Error);
        var filas = FilasDeCentral.Construir(layout.Layout, p.Value.Archivo, p.Value.Titulares);
        return Result.Success(Armar(RegulatoryFileKind.CreditBureau, central, anio, mes, layout, filas, [], null, p.Value));
    }

    private static ArchivoRegulatorioPreparado Armar(RegulatoryFileKind clase, string? central, int anio, int mes, LayoutDeCartera layout,
        IReadOnlyList<FilaDeLayout> filas, IReadOnlyList<AdvertenciaRegulatoria> deLaProyeccion, ProyeccionDelSicses? s, ProyeccionDeCentral? c)
    {
        var advertencias = new List<AdvertenciaRegulatoria>();
        if (layout.Layout.HasUnverifiedFields)
            advertencias.Add(AdvertenciasRegulatorias.SinCotejar(layout.Code, layout.Layout.UnverifiedFields, layout.Layout.TotalFields));
        advertencias.AddRange(deLaProyeccion);
        return new ArchivoRegulatorioPreparado(clase, central, anio, mes, layout, filas, EscritorPorLayout.Escribir(layout.Layout, filas), advertencias, s, c);
    }

    private static Result<ArchivoRegulatorioPreparado> Falla(Error e) => Result.Failure<ArchivoRegulatorioPreparado>(e);
}

/// <summary>Lo común de leer y proyectar las generaciones regulatorias. (nuevo)</summary>
public static class ArchivosRegulatorios
{
    /// <summary>El prefijo del numerador de archivos regulatorios (el número encabeza el nombre del archivo).</summary>
    public const string Prefijo = "ARG-";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>La gestión que corrige cada motivo de exclusión (vista <c>bureau-exclusions</c>).</summary>
    public static string GestionSugerida(string motivo) => motivo switch
    {
        MotivosDeExclusionDeCentral.NoReportAuthorization => "Obtener la autorización de reporte a centrales con su evidencia.",
        MotivosDeExclusionDeCentral.NoPriorNoticeEvidence => "Enviar el aviso previo o registrar su constancia; se reporta desde su fecha más temprana.",
        MotivosDeExclusionDeCentral.DeceasedClaimInProcess => "Esperar la respuesta de la aseguradora a la reclamación del seguro de vida deudores.",
        MotivosDeExclusionDeCentral.Insolvency => "No reportar mientras el proceso de insolvencia esté en negociación.",
        MotivosDeExclusionDeCentral.PaymasterArrearsWithinThreshold => "Gestionar el giro de lo descontado con la pagaduría.",
        _ => motivo,
    };

    /// <summary>El nombre en español de un motivo de exclusión.</summary>
    public static string Motivo(string motivo) => motivo switch
    {
        MotivosDeExclusionDeCentral.NoReportAuthorization => "Sin autorización de reporte",
        MotivosDeExclusionDeCentral.NoPriorNoticeEvidence => "Sin aviso previo con constancia",
        MotivosDeExclusionDeCentral.DeceasedClaimInProcess => "Reclamación al seguro de vida en trámite",
        MotivosDeExclusionDeCentral.Insolvency => "Insolvencia en negociación",
        MotivosDeExclusionDeCentral.PaymasterArrearsWithinThreshold => "Pagaduría en mora dentro del umbral",
        _ => motivo,
    };

    /// <summary>Las advertencias de una generación guardada: las de la generación (código <c>Lending.Regulatory.*</c>) entre sus inconsistencias.</summary>
    public static IReadOnlyList<RegulatoryIssueDto> Inconsistencias(RegulatoryFile f) =>
        JsonSerializer.Deserialize<List<RegulatoryIssueDto>>(f.IssuesJson, Json) ?? [];

    public static IReadOnlyList<RegulatoryWarningDto> Advertencias(RegulatoryFile f) =>
        Inconsistencias(f).Where(i => i.Code.StartsWith("Lending.Regulatory.", StringComparison.Ordinal)).Select(i => new RegulatoryWarningDto(i.Code, i.Message)).ToList();

    public static IReadOnlyList<RegulatoryExcludedDto> Excluidos(RegulatoryFile f) =>
        f.ExcludedJson is null ? [] : JsonSerializer.Deserialize<List<RegulatoryExcludedDto>>(f.ExcludedJson, Json) ?? [];

    public static async Task<IReadOnlyList<RegulatoryFileDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<RegulatoryFile> archivos, CancellationToken ct)
    {
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db,
            archivos.Select(a => a.GeneratedByUserId).Concat(archivos.Where(a => a.UploadedByUserId is not null).Select(a => a.UploadedByUserId!.Value)), ct);
        string Nombre(int id) => usuarios.TryGetValue(id, out var u) ? u.Name : id.ToString(CultureInfo.InvariantCulture);
        return archivos.Select(a => new RegulatoryFileDto(a.PublicId, a.Kind, a.BureauCode, a.PeriodYear, a.PeriodMonth, a.Version, a.Status, a.LayoutCode,
            Advertencias(a), new RegulatoryFileCountsDto(a.LineCount, a.BlockingIssueCount, a.WarningCount, a.ExcludedCount), a.FileName, a.FileSha256,
            a.TieneArchivo, Nombre(a.GeneratedByUserId), a.GeneratedAt,
            a.FilingNumber is null ? null : new RegulatoryFileUploadDto(a.FilingNumber, a.FilingDate, a.UploadedAt,
                a.UploadedByUserId is { } u ? Nombre(u) : null))).ToList();
    }

    public static async Task<RegulatoryFileDetailDto> DetalleAsync(IApplicationDbContext db, RegulatoryFile f, CancellationToken ct)
    {
        var dto = (await DtosAsync(db, [f], ct))[0];
        using var totales = JsonDocument.Parse(string.IsNullOrWhiteSpace(f.TotalsJson) ? "{}" : f.TotalsJson);
        return new RegulatoryFileDetailDto(dto, Inconsistencias(f), Excluidos(f), totales.RootElement.Clone());
    }

    public static RegulatoryValidationDto Validacion(ArchivoRegulatorioPreparado p) =>
        new(p.Layout.Code, p.Lineas, p.Bloqueantes, p.Inconsistencias, p.AReconocer, p.Excluidos, p.Plazo);
}

// ------------------------------------------------------------------------------------------------------------------------- validar --

/// <summary>
/// Valida el formato del SICSES de un período (api.md §18, <c>POST /sicses/{year}/{month}/validate</c>, <c>Lending.RegulatoryReports.Generate</c>, sin
/// clave): corre la proyección y el validador y <b>no guarda nada</b>. Sin cierre vigente del mes, <c>Lending.MonthlyClose.NotFound</c>. (nuevo)
/// </summary>
public sealed record ValidateSicsesFileQuery(int Year, int Month) : IRequest<Result<RegulatoryValidationDto>>;

public sealed class ValidateSicsesFileQueryValidator : AbstractValidator<ValidateSicsesFileQuery>
{
    public ValidateSicsesFileQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2999).WithMessage("Indique el año del período.");
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithMessage("El mes del período va de 1 a 12.");
    }
}

public sealed class ValidateSicsesFileQueryHandler(PreparacionDeArchivoRegulatorio preparacion)
    : IRequestHandler<ValidateSicsesFileQuery, Result<RegulatoryValidationDto>>
{
    public async Task<Result<RegulatoryValidationDto>> Handle(ValidateSicsesFileQuery request, CancellationToken ct)
    {
        var p = await preparacion.SicsesAsync(request.Year, request.Month, ct);
        return p.IsFailure ? Result.Failure<RegulatoryValidationDto>(p.Error) : Result.Success(ArchivosRegulatorios.Validacion(p.Value));
    }
}

/// <summary>
/// Valida el archivo de una central para un período (api.md §18, <c>POST /bureaus/{bureauCode}/{year}/{month}/validate</c>,
/// <c>Lending.CreditBureauFiles.Generate</c>, sin clave): la proyección con sus excluidos y el validador; <b>no guarda nada</b> ni levanta alertas.
/// (nuevo)
/// </summary>
public sealed record ValidateCreditBureauFileQuery(string BureauCode, int Year, int Month) : IRequest<Result<RegulatoryValidationDto>>;

public sealed class ValidateCreditBureauFileQueryValidator : AbstractValidator<ValidateCreditBureauFileQuery>
{
    public ValidateCreditBureauFileQueryValidator()
    {
        RuleFor(x => x.BureauCode).NotEmpty().WithMessage("Indique la central.").MaximumLength(RegulatoryFile.LargoDeLaCentral);
        RuleFor(x => x.Year).InclusiveBetween(2000, 2999).WithMessage("Indique el año del período.");
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithMessage("El mes del período va de 1 a 12.");
    }
}

public sealed class ValidateCreditBureauFileQueryHandler(PreparacionDeArchivoRegulatorio preparacion)
    : IRequestHandler<ValidateCreditBureauFileQuery, Result<RegulatoryValidationDto>>
{
    public async Task<Result<RegulatoryValidationDto>> Handle(ValidateCreditBureauFileQuery request, CancellationToken ct)
    {
        var p = await preparacion.CentralAsync(request.BureauCode, request.Year, request.Month, ct);
        return p.IsFailure ? Result.Failure<RegulatoryValidationDto>(p.Error) : Result.Success(ArchivosRegulatorios.Validacion(p.Value));
    }
}

// ------------------------------------------------------------------------------------------------------------------------- generar --

/// <summary>
/// Genera el formato del SICSES de un período (api.md §18, <c>POST /sicses/{year}/{month}/generate</c>, <c>Lending.RegulatoryReports.Generate</c>, con
/// clave). Ver <see cref="GeneracionDeArchivoRegulatorio"/>. (nuevo)
/// </summary>
public sealed record GenerateSicsesFileCommand(int Year, int Month, bool AcknowledgeWarnings = false)
    : IRequest<Result<RegulatoryFileDetailDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class GenerateSicsesFileCommandValidator : AbstractValidator<GenerateSicsesFileCommand>
{
    public GenerateSicsesFileCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2999).WithMessage("Indique el año del período.");
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithMessage("El mes del período va de 1 a 12.");
    }
}

public sealed class GenerateSicsesFileCommandHandler(GeneracionDeArchivoRegulatorio generacion)
    : IRequestHandler<GenerateSicsesFileCommand, Result<RegulatoryFileDetailDto>>
{
    public Task<Result<RegulatoryFileDetailDto>> Handle(GenerateSicsesFileCommand request, CancellationToken ct) =>
        generacion.GenerarAsync(RegulatoryFileKind.Sicses, null, request.Year, request.Month, request.AcknowledgeWarnings, ct);
}

/// <summary>
/// Genera el archivo de una central para un período (api.md §18, <c>POST /bureaus/{bureauCode}/{year}/{month}/generate</c>,
/// <c>Lending.CreditBureauFiles.Generate</c>, con clave): sólo quienes autorizaron el reporte y, si el dato es negativo, con aviso previo con constancia
/// del episodio. Ver <see cref="GeneracionDeArchivoRegulatorio"/>. (nuevo)
/// </summary>
public sealed record GenerateCreditBureauFileCommand(string BureauCode, int Year, int Month, bool AcknowledgeWarnings = false)
    : IRequest<Result<RegulatoryFileDetailDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class GenerateCreditBureauFileCommandValidator : AbstractValidator<GenerateCreditBureauFileCommand>
{
    public GenerateCreditBureauFileCommandValidator()
    {
        RuleFor(x => x.BureauCode).NotEmpty().WithMessage("Indique la central.").MaximumLength(RegulatoryFile.LargoDeLaCentral);
        RuleFor(x => x.Year).InclusiveBetween(2000, 2999).WithMessage("Indique el año del período.");
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithMessage("El mes del período va de 1 a 12.");
    }
}

public sealed class GenerateCreditBureauFileCommandHandler(GeneracionDeArchivoRegulatorio generacion)
    : IRequestHandler<GenerateCreditBureauFileCommand, Result<RegulatoryFileDetailDto>>
{
    public Task<Result<RegulatoryFileDetailDto>> Handle(GenerateCreditBureauFileCommand request, CancellationToken ct) =>
        generacion.GenerarAsync(RegulatoryFileKind.CreditBureau, request.BureauCode.Trim().ToUpperInvariant(), request.Year, request.Month,
            request.AcknowledgeWarnings, ct);
}

/// <summary>
/// La generación común del SICSES y de las centrales (K5, T1004; adaptadores-y-archivos.md §7.5), en una transacción:
/// <list type="number">
/// <item>el período de esa clase (y central) ya cargado no se regenera: <c>Lending.Regulatory.AlreadyUploaded</c>; en una central tampoco uno anterior
/// al último cargado (las novedades se cuentan desde él);</item>
/// <item>la preparación (<see cref="PreparacionDeArchivoRegulatorio"/>); sin bloqueantes y con advertencias, exige <c>acknowledgeWarnings</c> (el aviso
/// es el código de la primera, casi siempre <c>Lending.Regulatory.LayoutSinCotejar</c>);</item>
/// <item>con bloqueantes, una fila <c>Validated</c> <b>sin archivo</b> con sus inconsistencias; sin ellas, el número al final por
/// <see cref="NumeradorDeCartera"/> (clase <c>RegulatoryFile</c>, que encabeza el nombre del archivo) y el archivo con su SHA-256 subido como adjunto
/// <c>LendingRegulatoryFile</c> <b>antes</b> de guardar la fila <c>Generated</c>;</item>
/// <item>la versión siguiente: las vigentes del período quedan <c>Superseded</c> (la generada conserva su archivo);</item>
/// <item>en una central, una <c>LND_RegulatoryFileLines</c> por obligación y titular, incluidos y excluidos con su motivo —la base del archivo
/// siguiente—, y las alertas <c>Cartera.AvisoPrevioSinConstancia</c> de la proyección;</item>
/// <item>el evento resumen del archivo (las líneas van con <c>[SinDiffDeAuditoria]</c>, C46).</item>
/// </list>
/// Con bloqueantes la respuesta es <b>éxito</b> con la generación <c>Validated</c>: la fila tiene que quedar y un fallo revertiría la transacción (y la
/// clave de la operación). La ruta la traduce a 422 <c>Lending.Regulatory.BlockingIssues</c> con <see cref="ErroresDeArchivosRegulatorios.Bloqueantes"/>.
/// (nuevo)
/// </summary>
public sealed class GeneracionDeArchivoRegulatorio(
    IApplicationDbContext db,
    PreparacionDeArchivoRegulatorio preparacion,
    NumeradorDeCartera numerador,
    ISender sender,
    IAlertas alertas,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    public async Task<Result<RegulatoryFileDetailDto>> GenerarAsync(RegulatoryFileKind clase, string? central, int anio, int mes, bool reconocerAdvertencias,
        CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var periodo = anio * 12 + mes;
            var deLaClase = db.RegulatoryFiles.Where(f => !f.IsDeleted && f.Kind == clase && f.BureauCode == central);
            var cargado = await deLaClase.Where(f => f.Status == RegulatoryFileStatus.Uploaded
                                                     && (f.PeriodYear * 12 + f.PeriodMonth == periodo
                                                         || (clase == RegulatoryFileKind.CreditBureau && f.PeriodYear * 12 + f.PeriodMonth > periodo)))
                .OrderByDescending(f => f.PeriodYear).ThenByDescending(f => f.PeriodMonth).FirstOrDefaultAsync(ct);
            if (cargado is not null) return Falla(ErroresDeArchivosRegulatorios.YaCargado(cargado));

            var prep = clase == RegulatoryFileKind.Sicses
                ? await preparacion.SicsesAsync(anio, mes, ct)
                : await preparacion.CentralAsync(central!, anio, mes, ct);
            if (prep.IsFailure) return Falla(prep.Error);
            var p = prep.Value;
            var aReconocer = p.AReconocer;
            if (!p.HayBloqueantes && aReconocer.Count > 0 && !reconocerAdvertencias) return Falla(ErroresDeArchivosRegulatorios.AdvertenciasSinReconocer(aReconocer));

            var delPeriodo = await deLaClase.Where(f => f.PeriodYear == anio && f.PeriodMonth == mes).ToListAsync(ct);
            var version = delPeriodo.Count == 0 ? 1 : delPeriodo.Max(f => f.Version) + 1;
            var publicId = Guid.NewGuid();
            string? nombre = null;
            Guid? adjunto = null;
            if (!p.HayBloqueantes)
            {
                // El número al final (T34): encabeza el nombre del archivo.
                var numero = await numerador.AsignarAsync(LendingNumberKind.RegulatoryFile, string.Empty, ArchivosRegulatorios.Prefijo, ct);
                nombre = string.Create(CultureInfo.InvariantCulture,
                    $"{numero}_{(clase == RegulatoryFileKind.Sicses ? "SICSES" : central)}_{anio:0000}-{mes:00}_v{version}.txt");
                // El adjunto de módulo, antes de la fila: si el almacén falla no queda un archivo «generado» que nadie puede bajar.
                var subida = await sender.Send(new UploadAttachmentCommand(AdjuntosDeModulo.ArchivoRegulatorio, publicId, nombre, "text/plain",
                    p.Escritura.Contenido!), ct);
                if (subida.IsFailure) return Falla(subida.Error);
                adjunto = subida.Value;
            }

            var ahora = reloj.UtcNow;
            foreach (var vigente in delPeriodo.Where(f => f.Status is RegulatoryFileStatus.Validated or RegulatoryFileStatus.Generated))
                vigente.MarcarReemplazado(ahora);

            var inconsistencias = p.Inconsistencias;
            var excluidos = p.Excluidos;
            var archivo = RegulatoryFile.Nueva(clase, central, (short)anio, (byte)mes, version, p.Layout.Code, p.Layout.Layout.HasUnverifiedFields,
                p.Sicses?.Cierre.Id, p.Sicses?.Cierre.ObligatoryModelUnavailable ?? false,
                JsonSerializer.Serialize(inconsistencias, ArchivosRegulatorios.Json), p.Bloqueantes,
                inconsistencias.Count(i => i.Severity == nameof(SeveridadDeInconsistencia.Warning)),
                clase == RegulatoryFileKind.CreditBureau ? JsonSerializer.Serialize(excluidos, ArchivosRegulatorios.Json) : null, excluidos.Count, p.Lineas,
                JsonSerializer.Serialize(Totales(p), ArchivosRegulatorios.Json), nombre, p.Escritura.Sha256, adjunto, usuario, ahora);
            archivo.PublicId = publicId;
            db.RegulatoryFiles.Add(archivo);
            await db.SaveChangesAsync(ct);

            if (p.DeCentral is { } c)
            {
                var traduccion = TraduccionesRegulatorias.DeLaCentral(c.Central);
                var n = 0;
                foreach (var t in c.Titulares.OrderBy(t => t.NumeroDeObligacion, StringComparer.Ordinal).ThenBy(t => t.EsDeudor ? 0 : 1).ThenBy(t => t.PersonId))
                    db.RegulatoryFileLines.Add(new RegulatoryFileLine
                    {
                        RegulatoryFileId = archivo.Id,
                        LineNumber = ++n,
                        LoanId = t.LoanId,
                        PersonId = t.PersonId,
                        Included = t.Incluido,
                        ExclusionReason = t.MotivoDeExclusion,
                        ReportedStatusCode = traduccion.Estado(t.Estado),
                        DaysPastDue = t.DiasDeMora,
                        BalanceReported = t.SaldoTotal,
                        IsNegative = t.Incluido && t.EsNegativo,
                        EpisodeStartDate = t.InicioDelEpisodio,
                        PriorNoticeId = t.AvisoPrevioId,
                        HasClaimInProcess = t.ReclamoEnTramite,
                        IsWithdrawalOfUnnoticedReport = t.RetiroSinAviso,
                    });
                await db.SaveChangesAsync(ct);
                if (!p.HayBloqueantes)
                    foreach (var alerta in c.Alertas.DistinctBy(a => a.DedupKey)) await alertas.LevantarAsync(alerta, ct);
            }

            await auditoria.EmitirArchivoRegulatorioGeneradoAsync(archivo.PublicId, new
            {
                kind = clase.ToString(), bureau = central, period = string.Create(CultureInfo.InvariantCulture, $"{anio:0000}-{mes:00}"), version,
                status = archivo.Status.ToString(), layout = p.Layout.Code, monthlyClose = p.Sicses?.Cierre.PublicId, rows = p.Lineas,
                blocking = p.Bloqueantes, warnings = aReconocer.Select(a => a.Code).Distinct().ToList(), acknowledged = reconocerAdvertencias,
                excluded = excluidos.Count, fileName = nombre, sha256 = archivo.FileSha256,
                supersedes = delPeriodo.Where(f => f.Status == RegulatoryFileStatus.Superseded && f.SupersededAt == ahora).Select(f => f.Version).ToList(),
            }, ct);
            return Result.Success(await ArchivosRegulatorios.DetalleAsync(db, archivo, ct));
        }, ct);
    }

    private static Dictionary<string, object?> Totales(ArchivoRegulatorioPreparado p)
    {
        if (p.Sicses is { } s) return new Dictionary<string, object?>(s.Totales, StringComparer.Ordinal) { ["Plazo.Limite"] = s.Plazo?.Limite };
        var c = p.DeCentral!;
        var incluidos = c.Titulares.Where(t => t.Incluido).ToList();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Totales.Registros"] = incluidos.Count,
            ["Totales.SaldoTotal"] = incluidos.Sum(t => t.SaldoTotal),
            ["Totales.Negativos"] = incluidos.Count(t => t.EsNegativo),
            ["Totales.Retiros"] = incluidos.Count(t => t.RetiroSinAviso || t.AnuladoDesdeElOrigen),
            ["Totales.ConReclamo"] = incluidos.Count(t => t.ReclamoEnTramite),
            ["Totales.Excluidos"] = c.Titulares.Count - incluidos.Count,
            ["Archivo.UltimoCargado"] = c.UltimoCargado?.PublicId,
            ["Archivo.CodigoDeSuscriptor"] = c.CodigoDeSuscriptor,
        };
    }

    private static Result<RegulatoryFileDetailDto> Falla(Error e) => Result.Failure<RegulatoryFileDetailDto>(e);
}

// --------------------------------------------------------------------------------------------------------------------- marcar cargado --

/// <summary>
/// Marca cargado en el portal un archivo generado (api.md §18, <c>POST /files/{id}/mark-uploaded</c>, <c>Lending.RegulatoryFiles.MarkUploaded</c>, con
/// clave): sólo desde <c>Generated</c> (<c>Lending.RegulatoryFile.NotGenerated</c>), con el radicado y su fecha (la del cargue si no se da). Después el
/// período de esa clase (y central) ya no se regenera. El comprobante del portal, si se adjuntó, queda en el evento. (nuevo)
/// </summary>
public sealed record MarkRegulatoryFileUploadedCommand(
    Guid RegulatoryFilePublicId,
    string FilingNumber,
    DateTime UploadedAt,
    DateOnly? FilingDate = null,
    Guid? ReceiptAttachmentPublicId = null) : IRequest<Result<RegulatoryFileDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class MarkRegulatoryFileUploadedCommandValidator : AbstractValidator<MarkRegulatoryFileUploadedCommand>
{
    public MarkRegulatoryFileUploadedCommandValidator()
    {
        RuleFor(x => x.RegulatoryFilePublicId).NotEmpty();
        RuleFor(x => x.FilingNumber).NotEmpty().WithMessage("Indique el radicado que dio el portal.").MaximumLength(RegulatoryFile.LargoDelRadicado);
        RuleFor(x => x.UploadedAt).NotEmpty().WithMessage("Indique cuándo se cargó.");
    }
}

public sealed class MarkRegulatoryFileUploadedCommandHandler(
    IApplicationDbContext db,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<MarkRegulatoryFileUploadedCommand, Result<RegulatoryFileDto>>
{
    public async Task<Result<RegulatoryFileDto>> Handle(MarkRegulatoryFileUploadedCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<RegulatoryFileDto>(ErroresDeGobierno.SinUsuario());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var archivo = await db.RegulatoryFiles.FirstOrDefaultAsync(f => f.PublicId == request.RegulatoryFilePublicId && !f.IsDeleted, ct);
            if (archivo is null) return Result.Failure<RegulatoryFileDto>(ErroresDeArchivosRegulatorios.Inexistente());
            if (archivo.Status != RegulatoryFileStatus.Generated) return Result.Failure<RegulatoryFileDto>(ErroresDeArchivosRegulatorios.NoGenerado(archivo.Status));

            var instante = request.UploadedAt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(request.UploadedAt, DateTimeKind.Utc) : request.UploadedAt.ToUniversalTime();
            var fecha = request.FilingDate ?? DateOnly.FromDateTime(instante + reloj.AhoraLocal.Offset);
            archivo.MarcarCargado(usuario, instante, request.FilingNumber, fecha);
            await db.SaveChangesAsync(ct);

            await auditoria.EmitirArchivoRegulatorioCargadoAsync(archivo.PublicId, new
            {
                kind = archivo.Kind.ToString(), bureau = archivo.BureauCode,
                period = string.Create(CultureInfo.InvariantCulture, $"{archivo.PeriodYear:0000}-{archivo.PeriodMonth:00}"), version = archivo.Version,
                filingNumber = archivo.FilingNumber, filingDate = fecha, uploadedAt = instante, receipt = request.ReceiptAttachmentPublicId, sha256 = archivo.FileSha256,
            }, ct);
            return Result.Success((await ArchivosRegulatorios.DtosAsync(db, [archivo], ct))[0]);
        }, ct);
    }
}

// --------------------------------------------------------------------------------------------------------------------- listar y leer --

/// <summary>
/// Las generaciones regulatorias (api.md §18, <c>GET /files</c>, <c>Lending.RegulatoryFiles.Read</c>), de la más reciente a la más antigua, filtrables por
/// clase, central, año, mes y estado. Son de toda la cooperativa (el SICSES y la central llevan todas las oficinas). (nuevo)
/// </summary>
public sealed record ListRegulatoryFilesQuery(
    RegulatoryFileKind? Kind = null,
    string? Bureau = null,
    int? Year = null,
    int? Month = null,
    RegulatoryFileStatus? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<RegulatoryFileDto>>>;

public sealed class ListRegulatoryFilesQueryValidator : AbstractValidator<ListRegulatoryFilesQuery>
{
    public ListRegulatoryFilesQueryValidator()
    {
        RuleFor(x => x.Month).InclusiveBetween(1, 12).When(x => x.Month is not null);
        RuleFor(x => x.Bureau).MaximumLength(RegulatoryFile.LargoDeLaCentral);
    }
}

public sealed class ListRegulatoryFilesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListRegulatoryFilesQuery, Result<PagedResult<RegulatoryFileDto>>>
{
    public async Task<Result<PagedResult<RegulatoryFileDto>>> Handle(ListRegulatoryFilesQuery request, CancellationToken ct)
    {
        var consulta = db.RegulatoryFiles.AsNoTracking().Where(f => !f.IsDeleted);
        if (request.Kind is { } k) consulta = consulta.Where(f => f.Kind == k);
        if (!string.IsNullOrWhiteSpace(request.Bureau))
        {
            var central = request.Bureau.Trim().ToUpperInvariant();
            consulta = consulta.Where(f => f.BureauCode == central);
        }
        if (request.Year is { } y) consulta = consulta.Where(f => f.PeriodYear == y);
        if (request.Month is { } m) consulta = consulta.Where(f => f.PeriodMonth == m);
        if (request.Status is { } s) consulta = consulta.Where(f => f.Status == s);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderByDescending(f => f.PeriodYear).ThenByDescending(f => f.PeriodMonth).ThenBy(f => f.Kind).ThenBy(f => f.BureauCode)
            .ThenByDescending(f => f.Version).Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<RegulatoryFileDto>(await ArchivosRegulatorios.DtosAsync(db, filas, ct), pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>Una generación con sus inconsistencias, sus excluidos y sus totales (api.md §18, <c>GET /files/{id}</c>). (nuevo)</summary>
public sealed record GetRegulatoryFileQuery(Guid PublicId) : IRequest<Result<RegulatoryFileDetailDto>>;

public sealed class GetRegulatoryFileQueryHandler(IApplicationDbContext db) : IRequestHandler<GetRegulatoryFileQuery, Result<RegulatoryFileDetailDto>>
{
    public async Task<Result<RegulatoryFileDetailDto>> Handle(GetRegulatoryFileQuery request, CancellationToken ct)
    {
        var archivo = await db.RegulatoryFiles.AsNoTracking().FirstOrDefaultAsync(f => f.PublicId == request.PublicId && !f.IsDeleted, ct);
        return archivo is null
            ? Result.Failure<RegulatoryFileDetailDto>(ErroresDeArchivosRegulatorios.Inexistente())
            : Result.Success(await ArchivosRegulatorios.DetalleAsync(db, archivo, ct));
    }
}

// -------------------------------------------------------------------------------------------------------------------------- descargar --

/// <summary>
/// El enlace firmado de 60 s al archivo de una generación (api.md §18, <c>POST /files/{id}/download-link</c>, <c>Lending.RegulatoryFiles.Read</c>, sin
/// clave: repetirlo emite otro), auditado con su huella (<c>Lending.RegulatoryFile.Downloaded</c>). Una generación <c>Validated</c> no tiene archivo:
/// <c>Lending.RegulatoryFile.NoFile</c>. (nuevo)
/// </summary>
public sealed record IssueRegulatoryFileDownloadLinkCommand(Guid PublicId) : IRequest<Result<EnlaceDeDescargaDto>>;

public sealed class IssueRegulatoryFileDownloadLinkCommandValidator : AbstractValidator<IssueRegulatoryFileDownloadLinkCommand>
{
    public IssueRegulatoryFileDownloadLinkCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class IssueRegulatoryFileDownloadLinkCommandHandler(
    IApplicationDbContext db,
    IBlobStore store,
    IOptions<LimitesDeAdjuntos> limites,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<IssueRegulatoryFileDownloadLinkCommand, Result<EnlaceDeDescargaDto>>
{
    public async Task<Result<EnlaceDeDescargaDto>> Handle(IssueRegulatoryFileDownloadLinkCommand request, CancellationToken ct)
    {
        var archivo = await db.RegulatoryFiles.AsNoTracking().FirstOrDefaultAsync(f => f.PublicId == request.PublicId && !f.IsDeleted, ct);
        if (archivo is null) return Result.Failure<EnlaceDeDescargaDto>(ErroresDeArchivosRegulatorios.Inexistente());
        if (archivo.FileAttachmentPublicId is not { } id) return Result.Failure<EnlaceDeDescargaDto>(ErroresDeArchivosRegulatorios.SinArchivo(archivo.PublicId));
        var adjunto = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == id, ct);
        if (adjunto is null || adjunto.Status != EstadoDeAdjunto.Available)
            return Result.Failure<EnlaceDeDescargaDto>(ErroresDeArchivosRegulatorios.SinArchivo(archivo.PublicId));

        await auditoria.EmitirArchivoRegulatorioDescargadoAsync(archivo.PublicId, archivo.FileName ?? string.Empty, archivo.FileSha256, ct);
        if (adjunto.Format == FormatoDeAdjunto.AppEncrypted) return Result.Success(EnlaceDeDescargaDto.PorLaApi);
        return Result.Success(await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct));
    }
}
