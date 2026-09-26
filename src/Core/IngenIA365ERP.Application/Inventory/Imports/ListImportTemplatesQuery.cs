using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Imports;

/// <summary>
/// Las dieciséis plantillas de la parametrización en su orden de carga, con lo que quien pregunta puede hacer con cada una
/// (feature 012, T160; contracts/api.md §3.9, contracts/plantillas.md §0.1). Es la <b>única</b> consulta de la lista de
/// plantillas: la sirve <c>GET /api/inventory/templates</c> y la pinta <c>/inventario/plantillas</c>. (nuevo)
/// </summary>
public sealed record ListImportTemplatesQuery : IRequest<Result<IReadOnlyList<ImportTemplateDto>>>;

/// <summary>
/// Una plantilla (nuevo). <see cref="CanDownload"/> y <see cref="CanImport"/> combinan el permiso de quien pregunta con la
/// entrega en que existe cada ruta; <see cref="Note"/> dice «se importa con I3» (o «se descarga con I2») mientras la ruta
/// no exista.
/// </summary>
public sealed record ImportTemplateDto(
    int Number,
    string Key,
    string Name,
    IReadOnlyList<string> Sheets,
    string BaseRoute,
    string Command,
    string ImportsFrom,
    string DownloadsFrom,
    bool CanDownload,
    bool CanImport,
    string DownloadPermission,
    string ImportPermission,
    string? AdditionalPermissions,
    string? Note);

public sealed class ListImportTemplatesQueryHandler(ICurrentUserPermissions permisos)
    : IRequestHandler<ListImportTemplatesQuery, Result<IReadOnlyList<ImportTemplateDto>>>
{
    public async Task<Result<IReadOnlyList<ImportTemplateDto>>> Handle(ListImportTemplatesQuery request, CancellationToken ct)
    {
        var todos = permisos.EsMaestroGlobal;
        var concedidos = todos
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(await permisos.ListAsync(ct), StringComparer.OrdinalIgnoreCase);
        bool Tiene(string permiso) => todos || concedidos.Contains(permiso);

        IReadOnlyList<ImportTemplateDto> lista = CatalogoDePlantillas.Todas
            .OrderBy(p => p.Numero)
            .Select(p => new ImportTemplateDto(
                p.Numero,
                p.Clave,
                p.Nombre,
                p.Definicion.Hojas.Select(h => h.Nombre).ToList(),
                p.RutaBase,
                p.Comando,
                p.ImportaDesde.ToString(),
                p.DescargaDesde.ToString(),
                CanDownload: p.SeDescargaYa && Tiene(p.PermisoDeDescarga),
                CanImport: p.SeImportaYa && Tiene(p.PermisoDeImportacion),
                p.PermisoDeDescarga,
                p.PermisoDeImportacion,
                p.PermisosAdicionales,
                Nota(p)))
            .ToList();
        return Result.Success(lista);
    }

    private static string? Nota(PlantillaDeParametrizacion p) =>
        !p.SeDescargaYa ? $"Se descarga e importa con {p.DescargaDesde}."
        : !p.SeImportaYa ? $"Se importa con {p.ImportaDesde}."
        : null;
}
