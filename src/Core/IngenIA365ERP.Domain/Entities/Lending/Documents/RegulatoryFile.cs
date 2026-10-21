using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Documents;

/// <summary>
/// <c>LND_RegulatoryFiles</c> (feature 013, K5, US16; data-model §28.1; adaptadores-y-archivos.md §7.5; FR-136, FR-137, SC-019, D28; T79): una
/// versión del formato de cartera del SICSES o del archivo de una central de riesgo para un período. Molde <c>PilaGeneration</c> de la 010.
/// Su <b>contenido es inmutable</b> (clase, central, período, versión, layout, advertencias, inconsistencias, excluidos, conteos, totales y el
/// archivo —nombre, huella y adjunto, escritos una vez al crearla—: sólo <c>init</c>); lo único que avanza es el estado:
/// <code>
/// Validated (con bloqueantes, sin archivo) ──regenerar──▶ Superseded (+ la versión siguiente)
/// Generated ──regenerar──▶ Superseded (+ la versión siguiente; conserva su archivo)
/// Generated ──marcar cargado con radicado──▶ Uploaded (el período de esa clase y central ya no se regenera)
/// </code>
/// La marca «modelo obligatorio no disponible» (<see cref="ObligatoryModelUnavailable"/>, D28) es una advertencia de la generación y
/// <b>nunca</b> va dentro del archivo. No es <see cref="IHechoInmutable"/> porque el estado cambia. (nuevo)
/// </summary>
public class RegulatoryFile : AuditableEntity
{
    /// <summary>Largo de la central (<c>nvarchar(20)</c>).</summary>
    public const int LargoDeLaCentral = 20;

    /// <summary>Largo del código del layout (<c>nvarchar(80)</c>).</summary>
    public const int LargoDelLayout = 80;

    /// <summary>Largo del nombre del archivo (<c>nvarchar(200)</c>).</summary>
    public const int LargoDelNombreDeArchivo = 200;

    /// <summary>Largo del radicado (<c>nvarchar(60)</c>).</summary>
    public const int LargoDelRadicado = 60;

    public RegulatoryFileKind Kind { get; init; }

    /// <summary>La central (<c>DATACREDITO</c>, <c>TRANSUNION</c>…); obligatoria en <c>CreditBureau</c>, nula en <c>Sicses</c>.</summary>
    public string? BureauCode { get; init; }

    /// <summary>Largo del código del formato (<c>nvarchar(40)</c>).</summary>
    public const int LargoDelFormato = 40;

    /// <summary>
    /// Feature 014, US17 (T220): el formato de captaciones (<c>sicses-captaciones</c>, <c>sicses-saldos-diarios</c>, <c>exogena-1019</c>…); obligatorio en
    /// <c>DepositsSicses</c> y <c>Exogenous</c>, nulo en el SICSES de cartera y en las centrales. En la exógena (anual) <see cref="PeriodMonth"/> es 12.
    /// </summary>
    public string? FormatCode { get; init; }

    public short PeriodYear { get; init; }

    public byte PeriodMonth { get; init; }

    /// <summary>Regenerar crea la siguiente.</summary>
    public int Version { get; init; } = 1;

    public RegulatoryFileStatus Status { get; private set; } = RegulatoryFileStatus.Validated;

    /// <summary>El layout embebido vigente con que se validó (<c>SICSES-CARTERA-v1</c>, <c>CENTRAL-DATACREDITO-v1</c>…).</summary>
    public string LayoutCode { get; init; } = string.Empty;

    /// <summary>El layout tenía campos sin cotejar: advertencia <c>Lending.Regulatory.LayoutSinCotejar</c> (§7.3).</summary>
    public bool LayoutHasUnverifiedFields { get; init; }

    /// <summary>SICSES: el cierre mensual vigente del período del que salieron las filas.</summary>
    public int? MonthlyCloseId { get; init; }

    /// <summary>El cierre llevaba la marca D28: advertencia <c>ModeloObligatorioNoDisponible</c>, nunca dentro del archivo.</summary>
    public bool ObligatoryModelUnavailable { get; init; }

    /// <summary>Las inconsistencias del validador (bloqueantes y advertencias, con registro, campo, crédito y ruta).</summary>
    public string IssuesJson { get; init; } = "[]";

    public int BlockingIssueCount { get; init; }

    public int WarningCount { get; init; }

    /// <summary>Centrales: los excluidos con su motivo (vista <c>bureau-exclusions</c>); nulo en el SICSES.</summary>
    public string? ExcludedJson { get; init; }

    public int ExcludedCount { get; init; }

    public int LineCount { get; init; }

    public string TotalsJson { get; init; } = "{}";

    /// <summary>Nulo en <c>Validated</c>.</summary>
    public string? FileName { get; init; }

    /// <summary>SHA-256 del archivo, hexadecimal; nulo en <c>Validated</c>.</summary>
    public string? FileSha256 { get; init; }

    /// <summary>Adjunto <c>LendingRegulatoryFile</c>, no borrable, subido antes de guardar la fila (sin FK); nulo en <c>Validated</c>.</summary>
    public Guid? FileAttachmentPublicId { get; init; }

    public int GeneratedByUserId { get; init; }

    public DateTime GeneratedAt { get; init; }

    [EscrituraUnica]
    public DateTime? SupersededAt { get; private set; }

    [EscrituraUnica]
    public DateTime? UploadedAt { get; private set; }

    [EscrituraUnica]
    public int? UploadedByUserId { get; private set; }

    /// <summary>El radicado del portal de la Superintendencia o de la central.</summary>
    [EscrituraUnica]
    public string? FilingNumber { get; private set; }

    [EscrituraUnica]
    public DateOnly? FilingDate { get; private set; }

    /// <summary>
    /// El comprobante del cargue en el portal (decisión del dueño del 2026-10-16): un adjunto <c>LendingRegulatoryFileReceipt</c> de esta generación, subido
    /// por la pantalla al marcarla cargada (sin FK, como el archivo). Nulo si no se adjuntó. (nuevo)
    /// </summary>
    [EscrituraUnica]
    public Guid? ReceiptAttachmentPublicId { get; private set; }

    /// <summary>¿Tiene archivo para descargar? Sólo lo generado (y lo que de ahí pasó a reemplazado o cargado).</summary>
    public bool TieneArchivo => FileAttachmentPublicId is not null;

    /// <summary>
    /// Regenerar (§7.5): éste queda <c>Superseded</c> —un <c>Validated</c> sin archivo o un <c>Generated</c> con el suyo, que conserva—. Uno cargado no
    /// se regenera (<c>Lending.Regulatory.AlreadyUploaded</c>, lo responde el comando).
    /// </summary>
    public void MarcarReemplazado(DateTime instante)
    {
        if (Status is not (RegulatoryFileStatus.Validated or RegulatoryFileStatus.Generated))
            throw new InvalidOperationException($"El archivo regulatorio {LayoutCode} {PeriodYear}-{PeriodMonth:00} v{Version} está {Status}: no se reemplaza.");
        Status = RegulatoryFileStatus.Superseded;
        SupersededAt = instante;
    }

    /// <summary>
    /// Marcar cargado con el radicado (<c>MarkRegulatoryFileUploadedCommand</c>, <c>Lending.RegulatoryFiles.MarkUploaded</c>): sólo desde
    /// <c>Generated</c> (<c>Lending.RegulatoryFile.NotGenerated</c>, lo responde el comando), con el comprobante del portal si se adjuntó.
    /// </summary>
    public void MarcarCargado(int usuarioId, DateTime instante, string radicado, DateOnly fechaDelRadicado, Guid? comprobante = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(radicado);
        if (Status != RegulatoryFileStatus.Generated)
            throw new InvalidOperationException($"El archivo regulatorio {LayoutCode} {PeriodYear}-{PeriodMonth:00} v{Version} está {Status}: sólo uno generado se marca cargado.");
        Status = RegulatoryFileStatus.Uploaded;
        UploadedAt = instante;
        UploadedByUserId = usuarioId;
        FilingNumber = radicado.Trim();
        FilingDate = fechaDelRadicado;
        ReceiptAttachmentPublicId = comprobante;
    }

    /// <summary>
    /// Crea la fila de una generación: con bloqueantes, <c>Validated</c> sin archivo; sin ellas, <c>Generated</c> con nombre, huella y adjunto.
    /// </summary>
    public static RegulatoryFile Nueva(RegulatoryFileKind clase, string? central, short anio, byte mes, int version, string layout, bool layoutSinCotejar,
        int? cierreMensualId, bool modeloObligatorioNoDisponible, string inconsistenciasJson, int bloqueantes, int advertencias, string? excluidosJson,
        int excluidos, int lineas, string totalesJson, string? nombreDeArchivo, string? huella, Guid? adjunto, int usuarioId, DateTime instante,
        string? formato = null)
    {
        var deCaptaciones = clase is RegulatoryFileKind.DepositsSicses or RegulatoryFileKind.Exogenous;
        if (deCaptaciones && (string.IsNullOrWhiteSpace(formato) || central is not null))
            throw new ArgumentException("Un formato de captaciones lleva su código de formato y no lleva central.", nameof(formato));
        if (!deCaptaciones && formato is not null)
            throw new ArgumentException("Sólo los formatos de captaciones llevan código de formato.", nameof(formato));
        if (clase == RegulatoryFileKind.CreditBureau && string.IsNullOrWhiteSpace(central))
            throw new ArgumentException("Un archivo de central necesita la central.", nameof(central));
        if (clase == RegulatoryFileKind.Sicses && central is not null)
            throw new ArgumentException("El archivo del SICSES no lleva central.", nameof(central));
        var conArchivo = adjunto is not null;
        if (conArchivo != (bloqueantes == 0))
            throw new ArgumentException("Con bloqueantes no hay archivo; sin ellas, el archivo es obligatorio.", nameof(adjunto));
        if (conArchivo && (string.IsNullOrWhiteSpace(nombreDeArchivo) || string.IsNullOrWhiteSpace(huella)))
            throw new ArgumentException("Un archivo generado lleva nombre y huella.", nameof(nombreDeArchivo));
        return new RegulatoryFile
        {
            Kind = clase,
            BureauCode = central,
            FormatCode = formato,
            PeriodYear = anio,
            PeriodMonth = mes,
            Version = version,
            Status = conArchivo ? RegulatoryFileStatus.Generated : RegulatoryFileStatus.Validated,
            LayoutCode = layout,
            LayoutHasUnverifiedFields = layoutSinCotejar,
            MonthlyCloseId = cierreMensualId,
            ObligatoryModelUnavailable = modeloObligatorioNoDisponible,
            IssuesJson = inconsistenciasJson,
            BlockingIssueCount = bloqueantes,
            WarningCount = advertencias,
            ExcludedJson = excluidosJson,
            ExcludedCount = excluidos,
            LineCount = lineas,
            TotalsJson = totalesJson,
            FileName = conArchivo ? nombreDeArchivo : null,
            FileSha256 = conArchivo ? huella : null,
            FileAttachmentPublicId = adjunto,
            GeneratedByUserId = usuarioId,
            GeneratedAt = instante,
        };
    }
}
