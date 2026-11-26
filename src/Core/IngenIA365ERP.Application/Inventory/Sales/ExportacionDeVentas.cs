using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.DocumentTypes;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// La lista de documentos de venta, filtrada, como tabla para exportar a Excel o PDF (2026-11-25, pedido del dueño: «la venta debe tener la
/// opción para exportar a Excel y demás para validar la información»). Recorre la misma consulta de la pantalla
/// (<see cref="ListSalesDocumentsQuery"/>, con su alcance y sus filtros) página por página, hasta <see cref="Tope"/> filas: lo que se ve y lo
/// que se descarga es lo mismo. La ruta exige exportar informes de Inventario y sus datos personales (trae al cliente).
/// </summary>
public sealed record ExportSalesDocumentsQuery(ListSalesDocumentsQuery Filtros) : IRequest<Result<TablaExportable>>;

public sealed class ExportSalesDocumentsQueryHandler(ISender sender) : IRequestHandler<ExportSalesDocumentsQuery, Result<TablaExportable>>
{
    /// <summary>Cuántas filas se exportan a lo sumo; si hay más, la nota lo dice y pide acotar los filtros.</summary>
    public const int Tope = 20_000;

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha", TipoDeColumna.Fecha), new("Clase"), new("Tipo"), new("Número"), new("Estado"), new("Cliente"), new("Vendedor"), new("Punto de venta"),
        new("Caja"), new("Total", TipoDeColumna.Moneda), new("Por pagar", TipoDeColumna.Moneda), new("Medios de pago"), new("Estado DIAN"), new("Creado por"),
    ];

    private static readonly IReadOnlyDictionary<DocumentClass, string> Clases =
        PlantillaDeTiposDeDocumento.EtiquetasDeClase.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);

    public async Task<Result<TablaExportable>> Handle(ExportSalesDocumentsQuery request, CancellationToken ct)
    {
        var filas = new List<SalesDocumentSummaryDto>();
        long total = 0;
        for (var pagina = 1; filas.Count < Tope; pagina++)
        {
            var r = await sender.Send(request.Filtros with { Page = pagina, PageSize = PageRequest.MaxPageSize }, ct);
            if (r.IsFailure) return Result.Failure<TablaExportable>(r.Error);
            total = r.Value.TotalCount;
            filas.AddRange(r.Value.Items);
            if (r.Value.Items.Count < PageRequest.MaxPageSize || filas.Count >= total) break;
        }

        var f = request.Filtros;
        var subtitulo = f.From is { } desde || f.To is not null
            ? $"Del {(f.From is { } d ? d.ToString("dd/MM/yyyy") : "inicio")} al {(f.To is { } h ? h.ToString("dd/MM/yyyy") : "hoy")}"
            : "Todas las fechas";
        var notas = new List<string> { $"{filas.Count} documento(s) con los filtros de la pantalla." };
        if (total > filas.Count) notas.Add($"Hay {total} documentos: se exportaron los primeros {filas.Count}. Acote los filtros para ver el resto.");
        var vivos = filas.Where(d => d.Status == DocumentStatus.Confirmed).ToList();

        return Result.Success(new TablaExportable("Documentos de venta", subtitulo, Columnas,
            filas.Select(d => new FilaExportable(
            [
                d.OperationDate, Clases.GetValueOrDefault(d.Class, d.Class.ToString()), d.DocumentTypeCode, d.Number is { } n ? $"{d.Prefix}{n}" : "Borrador",
                Estado(d.Status), d.CounterpartyName, d.SalespersonName, d.PointOfSaleCode, d.CashRegisterCode, d.Total, d.AmountDue,
                string.Join("; ", d.Payments.Select(p => $"{p.MeansCode} {p.Amount:N0}")), d.ElectronicStatus, d.CreatedByName,
            ])).ToList(),
            new FilaExportable(["Total confirmados", null, null, null, null, null, null, null, null, vivos.Sum(d => d.Total), vivos.Sum(d => d.AmountDue),
                null, null, null], Resaltada: true),
            notas));
    }

    private static string Estado(DocumentStatus estado) => estado switch
    {
        DocumentStatus.Draft => "Borrador",
        DocumentStatus.PendingApproval => "En aprobación",
        DocumentStatus.Confirmed => "Confirmado",
        DocumentStatus.Voided => "Anulado",
        DocumentStatus.Discarded => "Descartado",
        _ => estado.ToString(),
    };
}
