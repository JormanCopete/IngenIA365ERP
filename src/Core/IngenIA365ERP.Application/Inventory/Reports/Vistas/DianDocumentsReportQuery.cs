using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports.Vistas;

/// <summary>
/// La vista <c>dian-documents</c> de <c>/api/reports/inventory</c> (feature 012, I4, T751; contracts/api.md §27; decisiones-transversales
/// §2.12): un documento electrónico por fila —tipo, prefijo, número, fecha de expedición, contraparte (la de la copia fiscal, tal como se
/// numeró), total, estado, contingencia, código único (CUFE/CUDE/CUDS), intentos, plazo de transmisión, canal sellado y el último mensaje
/// traducido— con la columna oculta <c>_documento</c> (el <c>PublicId</c> del documento electrónico) para abrirlo en la bandeja. Filtros
/// comunes <c>from</c>/<c>to</c> sobre la fecha de expedición y los propios <c>status</c>, <c>kind</c> (por nombre o número) y
/// <c>contingency</c>. El alcance es el de la bandeja: cada módulo fuente decide qué documentos ve quien consulta
/// (<see cref="IConsultaDeFuenteElectronica"/>). Trae la contraparte, así que exportarla con alguna exige además
/// <c>Inventory.Reports.ExportPersonalData</c> (<see cref="ColumnaDeDatosPersonales"/>, lo decide la ruta). (nuevo)
/// </summary>
public sealed record DianDocumentsReportQuery(
    FiltrosDeInformeDeInventario Filtros,
    ElectronicDocumentStatus? Status = null,
    ElectronicDocumentKind? Kind = null,
    bool? Contingency = null) : IRequest<Result<TablaExportable>>;

public sealed class DianDocumentsReportQueryHandler(
    IApplicationDbContext db,
    IEnumerable<IConsultaDeFuenteElectronica> fuentes,
    IDateTimeService reloj)
    : IRequestHandler<DianDocumentsReportQuery, Result<TablaExportable>>
{
    /// <summary>La columna con datos de personas: con algún valor, exportar exige <c>Inventory.Reports.ExportPersonalData</c>.</summary>
    public const string ColumnaDeDatosPersonales = "Contraparte";

    /// <summary>Lo que la vista declara al publicarse (T751): <c>from</c>/<c>to</c> comunes; <c>status</c>, <c>kind</c> y <c>contingency</c> propios.</summary>
    public static readonly VistaDeInformeDeInventario Vista = new(
        "dian-documents", "Documentos electrónicos DIAN",
        "Facturas, documentos equivalentes, notas y documentos soporte ante la DIAN con su estado, contingencia, código único, intentos y plazo.",
        "documentos-electronicos-dian", ["from", "to"], ["status", "kind", "contingency"],
        PersonalDataColumn: ColumnaDeDatosPersonales);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Tipo", TipoDeColumna.Texto),
        new("Prefijo", TipoDeColumna.Texto),
        new("Número", TipoDeColumna.Texto),
        new("Fecha", TipoDeColumna.Fecha),
        new(ColumnaDeDatosPersonales, TipoDeColumna.Texto),
        new("Total", TipoDeColumna.Moneda),
        new("Estado", TipoDeColumna.Texto),
        new("Contingencia", TipoDeColumna.Texto),
        new("Código único", TipoDeColumna.Texto),
        new("Intentos", TipoDeColumna.Entero),
        new("Plazo", TipoDeColumna.Texto),
        new("Canal", TipoDeColumna.Texto),
        new("Último mensaje", TipoDeColumna.Texto),
        new("Documento", TipoDeColumna.Texto, "_documento"),
    ];

    public async Task<Result<TablaExportable>> Handle(DianDocumentsReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var rango = f.ValidarRango(hoy);
        if (rango.IsFailure) return Result.Failure<TablaExportable>(rango.Error);
        var desde = f.Desde(hoy);
        var hasta = f.Hasta(hoy);

        var q = await ConsultaDeDocumentosElectronicos.ConAlcanceAsync(db.ElectronicDocuments.AsNoTracking(), fuentes, ct);
        q = q.Where(d => d.IssueDate >= desde && d.IssueDate <= hasta);
        if (request.Status is { } estado) q = q.Where(d => d.Status == estado);
        if (request.Kind is { } tipo) q = q.Where(d => d.Kind == tipo);
        if (request.Contingency is { } conContingencia)
            q = conContingencia ? q.Where(d => d.ContingencyType != null) : q.Where(d => d.ContingencyType == null);

        var documentos = await q.OrderBy(d => d.IssueDate).ThenBy(d => d.Prefix).ThenBy(d => d.Consecutive).ThenBy(d => d.Id)
            .Select(d => new
            {
                d.PublicId, d.Kind, d.Prefix, d.Number, d.IssueDate, d.CounterpartyName, d.CounterpartyTaxId, d.TotalAmount, d.Status,
                d.ContingencyType, d.UniqueCode, d.AttemptCount, d.TransmissionDeadline, d.ChannelCode, d.LastMessagesJson,
            })
            .ToListAsync(ct);

        var filas = documentos.Select(d => new FilaExportable(
        [
            LeyendasDeRepresentacion.Nombre(d.Kind),
            d.Prefix,
            d.Number,
            d.IssueDate,
            Contraparte(d.CounterpartyName, d.CounterpartyTaxId),
            d.TotalAmount,
            Estado(d.Status),
            d.ContingencyType is { } c ? Contingencia(c) : null,
            d.UniqueCode,
            d.AttemptCount,
            d.TransmissionDeadline is { } plazo ? plazo.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " UTC" : null,
            d.ChannelCode,
            ConsultaDeDocumentosElectronicos.Mensajes(d.LastMessagesJson).Select(m => m.Translation ?? m.Text).FirstOrDefault(),
            d.PublicId.ToString(),
        ])).ToList();

        var subtitulo = $"Expedidos del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";
        var notas = new List<string>
        {
            "La contraparte es la de la copia fiscal con que se numeró el documento, no la del maestro de hoy.",
            "El plazo es el de transmisión de un documento expedido en contingencia; vacío si no aplica.",
        };
        return Result.Success(new TablaExportable("Documentos electrónicos DIAN", subtitulo, Columnas, filas, null, notas));
    }

    private static string? Contraparte(string? nombre, string? identificacion) =>
        string.IsNullOrWhiteSpace(nombre) ? identificacion
        : string.IsNullOrWhiteSpace(identificacion) ? nombre.Trim()
        : $"{nombre.Trim()} ({identificacion})";

    /// <summary>El estado del documento electrónico en palabras.</summary>
    public static string Estado(ElectronicDocumentStatus estado) => estado switch
    {
        ElectronicDocumentStatus.Pending => "Pendiente",
        ElectronicDocumentStatus.Sent => "Enviado",
        ElectronicDocumentStatus.Validated => "Validado",
        ElectronicDocumentStatus.ValidatedWithNotices => "Validado con notificaciones",
        ElectronicDocumentStatus.Rejected => "Rechazado",
        ElectronicDocumentStatus.IssuerContingency => "Contingencia del facturador",
        ElectronicDocumentStatus.DianContingency => "Contingencia de la DIAN",
        ElectronicDocumentStatus.CancelledWithoutReplacement => "Anulado sin reemplazo",
        _ => estado.ToString(),
    };

    /// <summary>El tipo de contingencia en palabras (el número es el del catálogo: 03 del facturador, 04 de la DIAN).</summary>
    public static string Contingencia(ContingencyType tipo) => tipo switch
    {
        ContingencyType.Issuer03 => $"Tipo {(int)tipo:00} (facturador)",
        ContingencyType.Dian04 => $"Tipo {(int)tipo:00} (DIAN)",
        _ => tipo.ToString(),
    };
}
