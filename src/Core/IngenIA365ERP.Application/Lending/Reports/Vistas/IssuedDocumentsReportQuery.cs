using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>issued-documents</c> de <c>/api/reports/lending</c> (feature 013, US15, K5, T0981; FR-131, FR-133; contracts/api.md §17): los
/// documentos al deudor emitidos en el rango (<c>from</c>–<c>to</c>, por la fecha local de emisión), con su estado <b>calculado</b> a hoy
/// (<see cref="EstadoDeDocumentoEmitido"/>: vigente, sujeto a firmeza o anulado), filtrables por clase (<c>kind</c>) y estado (<c>status</c>). Trae
/// datos del deudor (el titular; PD, C84). El alcance es el de la lista de documentos (<see cref="DocumentosEmitidos.Visibles"/>: un documento se ve
/// si ninguno de los créditos que cubre está fuera del alcance). Columna oculta <c>_documento</c>. (nuevo)
/// </summary>
public sealed record IssuedDocumentsReportQuery(FiltrosDeInformeDeCartera Filtros, string? Kind = null, string? Status = null)
    : IRequest<Result<TablaExportable>>;

public sealed class IssuedDocumentsReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<IssuedDocumentsReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "issued-documents", "Documentos emitidos",
        "Los documentos al deudor emitidos en el rango: clase, número, titular, estado a hoy, código de verificación y quién lo emitió.",
        "documentos-emitidos", ["from", "to"], ["kind", "status"], EntregaDeCartera.K5);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha", TipoDeColumna.Fecha),
        new("Clase"),
        new("Número"),
        new("Titular"),
        new("Estado"),
        new("Código"),
        new("Emitido por"),
        new("_documento", Clave: "_documento"),
    ];

    public async Task<Result<TablaExportable>> Handle(IssuedDocumentsReportQuery request, CancellationToken ct)
    {
        IssuedDocumentKind? clase = null;
        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            if (!Enum.TryParse<IssuedDocumentKind>(request.Kind.Trim(), true, out var k) || !Enum.IsDefined(k) || int.TryParse(request.Kind, out _))
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("kind", request.Kind, Enum.GetNames<IssuedDocumentKind>()));
            clase = k;
        }
        IssuedDocumentStatus? estado = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<IssuedDocumentStatus>(request.Status.Trim(), true, out var e) || !Enum.IsDefined(e) || int.TryParse(request.Status, out _))
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("status", request.Status, Enum.GetNames<IssuedDocumentStatus>()));
            estado = e;
        }

        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var (desde, hasta) = (f.Desde(hoy), f.Hasta(hoy));
        var desfase = reloj.AhoraLocal.Offset;
        var desdeUtc = DateTime.SpecifyKind(desde.ToDateTime(TimeOnly.MinValue) - desfase, DateTimeKind.Utc);
        var hastaUtc = DateTime.SpecifyKind(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue) - desfase, DateTimeKind.Utc);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        var consulta = DocumentosEmitidos.Visibles(db, alcance).Where(d => d.IssuedAt >= desdeUtc && d.IssuedAt < hastaUtc);
        if (clase is { } c) consulta = consulta.Where(d => d.Kind == c);
        var documentos = await consulta.OrderBy(d => d.IssuedAt).ThenBy(d => d.Id).ToListAsync(ct);
        var dtos = await DocumentosEmitidos.DtosAsync(db, documentos, hoy, ct);

        var filas = dtos
            .Where(d => estado is not { } e || d.Status == e.ToString())
            .Select(d => new FilaExportable(
            [
                DateOnly.FromDateTime(DateTime.SpecifyKind(d.IssuedAt, DateTimeKind.Utc) + desfase), Clase(d.Kind), d.Number, d.Holder.FullName, Estado(d.Status),
                d.VerificationCode, d.IssuedBy, d.PublicId.ToString(),
            ])).ToList();
        return Result.Success(new TablaExportable("Documentos emitidos", $"Del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd} · {filas.Count} documentos", Columnas,
            filas, null,
            ["Estado a hoy, calculado: vigente, sujeto a firmeza hasta la fecha que dice el documento o anulado (a mano o por el reverso de lo que respaldaba)."]));
    }

    /// <summary>El nombre en español de la clase del documento.</summary>
    public static string Clase(string clase) => Enum.TryParse<IssuedDocumentKind>(clase, out var k) ? k switch
    {
        IssuedDocumentKind.PaymentPlan => "Plan de pagos",
        IssuedDocumentKind.Statement => "Extracto",
        IssuedDocumentKind.LoanClearance => "Paz y salvo de la obligación",
        IssuedDocumentKind.GeneralClearance => "Paz y salvo general",
        IssuedDocumentKind.BalanceCertificate => "Certificado de saldo",
        IssuedDocumentKind.TaxCertificate => "Certificado tributario",
        _ => clase,
    } : clase;

    /// <summary>El nombre en español del estado calculado.</summary>
    public static string Estado(string estado) => Enum.TryParse<IssuedDocumentStatus>(estado, out var e) ? e switch
    {
        IssuedDocumentStatus.Valid => "Vigente",
        IssuedDocumentStatus.SubjectToFinality => "Sujeto a firmeza",
        IssuedDocumentStatus.Voided => "Anulado",
        _ => estado,
    } : estado;
}
