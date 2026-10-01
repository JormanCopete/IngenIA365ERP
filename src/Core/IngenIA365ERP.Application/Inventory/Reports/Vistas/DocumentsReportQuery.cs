using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.DocumentTypes;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports.Vistas;

/// <summary>
/// La vista <c>documents</c> de <c>/api/reports/inventory</c> (feature 012, US17, T955; FR-086, FR-087; contracts/api.md §27;
/// decisiones-transversales §2.12): un documento de inventario por fila —fecha, clase, tipo, número, estado, bodega, bodega destino,
/// contraparte (la copia fiscal de <c>INV_DocumentPartySnapshots</c>; en un borrador, la persona), total, modo de paso, estado del
/// mensaje (de <c>COR_IntegrationMessageDeliveries</c>), creado por y confirmado por— con la columna oculta <c>_documento</c> para
/// abrirlo. Filtros comunes <c>from</c>/<c>to</c> (fecha de operación), <c>warehouse</c> (origen <b>o</b> destino),
/// <c>documentType</c> y <c>person</c>, y los propios <c>class</c> y <c>status</c>. Alcance: bodega de origen o de destino (sin bodega,
/// por sus orígenes; <see cref="FiltroDeAlcance.DocumentosVisibles"/>) y sólo de los grupos cuyo <c>View</c> tiene quien pregunta,
/// como la lista de documentos. El total de un documento valorado al costo (sin precio: ajustes, traslados, saldo inicial) sólo sale
/// con <c>Inventory.Costs.Read</c>, y la nota lo dice. Trae la contraparte, así que exportarla con alguna exige además
/// <c>Inventory.Reports.ExportPersonalData</c> (<see cref="ColumnaDeDatosPersonales"/>, lo decide la ruta). (nuevo)
/// </summary>
public sealed record DocumentsReportQuery(FiltrosDeInformeDeInventario Filtros, DocumentClass? Class = null, DocumentStatus? Status = null)
    : IRequest<Result<TablaExportable>>;

public sealed class DocumentsReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    VistaDeDocumentos vista,
    IDateTimeService reloj)
    : IRequestHandler<DocumentsReportQuery, Result<TablaExportable>>
{
    /// <summary>La columna con datos de personas: con algún valor, exportar exige <c>Inventory.Reports.ExportPersonalData</c>.</summary>
    public const string ColumnaDeDatosPersonales = "Contraparte";

    /// <summary>
    /// Lo que la vista declara al publicarse (T957): filtros comunes <c>from</c>, <c>to</c>, <c>warehouse</c>, <c>documentType</c> y
    /// <c>person</c>; propios <c>class</c> y <c>status</c> (por nombre o número); datos personales según la columna Contraparte.
    /// </summary>
    public static readonly VistaDeInformeDeInventario Vista = new(
        "documents", "Documentos", "Los documentos de inventario con su estado, bodegas, contraparte, total, modo de paso y estado del mensaje.",
        "documentos", ["from", "to", "warehouse", "documentType", "person"], ["class", "status"],
        PersonalDataColumn: ColumnaDeDatosPersonales);

    public const string NotaSinCostos = "Sin el permiso Inventory.Costs.Read el total de los documentos valorados al costo (sin precio) sale vacío.";

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha", TipoDeColumna.Fecha),
        new("Clase", TipoDeColumna.Texto),
        new("Tipo", TipoDeColumna.Texto),
        new("Número", TipoDeColumna.Texto),
        new("Estado", TipoDeColumna.Texto),
        new("Bodega", TipoDeColumna.Texto),
        new("Bodega destino", TipoDeColumna.Texto),
        new(ColumnaDeDatosPersonales, TipoDeColumna.Texto),
        new("Total", TipoDeColumna.Moneda),
        new("Modo de paso", TipoDeColumna.Texto),
        new("Estado del mensaje", TipoDeColumna.Texto),
        new("Creado por", TipoDeColumna.Texto),
        new("Confirmado por", TipoDeColumna.Texto),
        new("Documento", TipoDeColumna.Texto, "_documento"),
    ];

    /// <summary>La etiqueta en español de cada clase (la misma que admite la plantilla de tipos de documento).</summary>
    private static readonly IReadOnlyDictionary<DocumentClass, string> Clases =
        PlantillaDeTiposDeDocumento.EtiquetasDeClase.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);

    public async Task<Result<TablaExportable>> Handle(DocumentsReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var rango = f.ValidarRango(hoy);
        if (rango.IsFailure) return Result.Failure<TablaExportable>(rango.Error);
        var desde = f.Desde(hoy);
        var hasta = f.Hasta(hoy);

        // Las clases que puede ver: las de los grupos cuyo View tiene (y las anulaciones de ésas), como la lista (§9.2).
        var clases = new List<DocumentClass>();
        foreach (var grupo in Enum.GetValues<DocumentClassGroup>())
            if (await vista.VeGrupoAsync(grupo, ct)) clases.AddRange(ClasesDeDocumento.DelGrupo(grupo));
        var subtitulo = $"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";
        if (clases.Count == 0) return Result.Success(TablaExportable.Vacia("Documentos de inventario", subtitulo, Columnas));

        var visibles = clases.ToArray();
        var consulta = db.InventoryDocuments.AsNoTracking().Where(d =>
            visibles.Contains(d.Class)
            || (d.Class == DocumentClass.Voiding && db.InventoryDocuments.Any(o => o.Id == d.VoidsDocumentId && visibles.Contains(o.Class))));
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        consulta = consulta.DocumentosVisibles(alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking());

        consulta = consulta.Where(d => d.OperationDate >= desde && d.OperationDate <= hasta);
        if (request.Class is { } clase) consulta = consulta.Where(d => d.Class == clase);
        if (request.Status is { } estado) consulta = consulta.Where(d => d.Status == estado);
        if (f.DocumentType is { } tipo) consulta = consulta.Where(d => db.InventoryDocumentTypes.Any(t => t.Id == d.DocumentTypeId && t.PublicId == tipo));
        if (f.Person is { } persona) consulta = consulta.Where(d => db.People.Any(p => p.Id == d.CounterpartyPersonId && p.PublicId == persona));
        if (f.Warehouse is { } bodegaPedida)
        {
            var bodega = await db.Warehouses.AsNoTracking().Where(w => w.PublicId == bodegaPedida).Select(w => (int?)w.Id).FirstOrDefaultAsync(ct);
            if (bodega is null || !alcance.IncluyeBodega(bodega.Value)) return Result.Failure<TablaExportable>(ErroresDeAlcance.BodegaInexistente());
            consulta = consulta.Where(d => d.WarehouseId == bodega || d.DestinationWarehouseId == bodega);
            subtitulo += " · filtrado por bodega";
        }

        var documentos = await consulta
            .OrderBy(d => d.OperationDate).ThenBy(d => d.Prefix).ThenBy(d => d.Number).ThenBy(d => d.Id)
            .Select(d => new
            {
                d.Id, d.PublicId, d.Class, d.DocumentTypeId, d.Prefix, d.Number, d.Status, d.OperationDate,
                d.WarehouseId, d.DestinationWarehouseId, d.CounterpartyPersonId, d.Total, d.CostTotal, d.PostingMode,
                d.CreatedByUserId, d.ConfirmedByUserId,
            })
            .ToListAsync(ct);

        var costos = await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct);
        var ids = documentos.Select(d => d.Id).ToList();
        var publicos = documentos.Select(d => d.PublicId).ToList();
        var tipos = await db.InventoryDocumentTypes.AsNoTracking().IgnoreQueryFilters()
            .Where(t => documentos.Select(d => d.DocumentTypeId).Distinct().Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Code, ct);
        var bodegaIds = documentos.SelectMany(d => new[] { d.WarehouseId, d.DestinationWarehouseId }).OfType<int>().Distinct().ToList();
        var bodegas = await db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => bodegaIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.Code, ct);
        var fotos = (await db.DocumentPartySnapshots.AsNoTracking().Where(s => ids.Contains(s.DocumentId))
                .Select(s => new { s.DocumentId, s.Version, s.LegalName, s.TaxId }).ToListAsync(ct))
            .GroupBy(s => s.DocumentId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Version).First());
        var personaIds = documentos.Select(d => d.CounterpartyPersonId).OfType<int>().Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personaIds.Contains(p.Id))
            .Select(p => new { p.Id, Nombre = p.BusinessName ?? (p.FirstName + " " + p.LastName), p.TaxId })
            .ToDictionaryAsync(p => p.Id, ct);
        var usuarioIds = documentos.SelectMany(d => new[] { (int?)d.CreatedByUserId, d.ConfirmedByUserId }).OfType<int>().Distinct().ToList();
        var usuarios = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => usuarioIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username, ct);
        var entregas = (await db.IntegrationMessages.AsNoTracking()
                .Where(m => publicos.Contains(m.OriginPublicId))
                .Join(db.IntegrationMessageDeliveries.AsNoTracking(), m => m.Id, e => e.MessageId, (m, e) => new { m.OriginPublicId, e.Status })
                .ToListAsync(ct))
            .GroupBy(x => x.OriginPublicId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Status).Distinct().OrderBy(s => s).Select(EstadoDeEntrega)));

        var filas = documentos.Select(d =>
        {
            decimal? total = d.Total != 0m ? d.Total : costos ? d.CostTotal : null;
            string? contraparte = fotos.TryGetValue(d.Id, out var foto)
                ? $"{foto.LegalName} ({foto.TaxId})"
                : d.CounterpartyPersonId is int pid && personas.TryGetValue(pid, out var p) ? $"{p.Nombre.Trim()} ({p.TaxId})" : null;
            return new FilaExportable(
            [
                d.OperationDate,
                Clases.TryGetValue(d.Class, out var etiqueta) ? etiqueta : d.Class.ToString(),
                tipos.GetValueOrDefault(d.DocumentTypeId),
                VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number),
                Estado(d.Status),
                d.WarehouseId is int o ? bodegas.GetValueOrDefault(o) : null,
                d.DestinationWarehouseId is int x ? bodegas.GetValueOrDefault(x) : null,
                contraparte,
                total,
                d.PostingMode is { } modo ? ModoDePaso(modo) : null,
                entregas.GetValueOrDefault(d.PublicId),
                usuarios.GetValueOrDefault(d.CreatedByUserId),
                d.ConfirmedByUserId is int c ? usuarios.GetValueOrDefault(c) : null,
                d.PublicId.ToString(),
            ]);
        }).ToList();

        var notas = new List<string> { "Fecha de operación del documento; el número lo asigna la confirmación (un borrador no tiene)." };
        if (!costos) notas.Add(NotaSinCostos);
        return Result.Success(new TablaExportable("Documentos de inventario", subtitulo, Columnas, filas, null, notas));
    }

    public static string Estado(DocumentStatus estado) => estado switch
    {
        DocumentStatus.Draft => "Borrador",
        DocumentStatus.PendingApproval => "En aprobación",
        DocumentStatus.Confirmed => "Confirmado",
        DocumentStatus.Voided => "Anulado",
        DocumentStatus.Discarded => "Descartado",
        _ => estado.ToString(),
    };

    public static string ModoDePaso(PostingMode modo) => modo switch
    {
        PostingMode.Online => "En línea",
        PostingMode.Batch => "Por lotes",
        PostingMode.NotPosted => "No pasa",
        _ => modo.ToString(),
    };

    public static string EstadoDeEntrega(DeliveryStatus estado) => estado switch
    {
        DeliveryStatus.Pending => "Pendiente",
        DeliveryStatus.InBatch => "En lote",
        DeliveryStatus.Processed => "Procesado",
        DeliveryStatus.Rejected => "Rechazado",
        DeliveryStatus.NotApplicable => "No aplica",
        DeliveryStatus.ValidationFailed => "Validación fallida",
        _ => estado.ToString(),
    };
}
