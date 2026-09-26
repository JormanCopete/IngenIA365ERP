using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Catalogos;

/// <summary>Lo que la pantalla necesita saber al salir del campo «Código»: si ya existe y cuál es.</summary>
public sealed record CodigoDeCatalogoEncontrado(bool Existe, Guid? PublicId, string? Nombre);

/// <summary>
/// ¿Existe ya un registro con este código en el catálogo dado? Un solo endpoint para
/// todos los catálogos (<c>GET /api/catalogos/{catalogo}/codigo/{codigo}</c>): la
/// pantalla lo consulta al salir del campo, antes de que la persona escriba el resto,
/// y le ofrece editar el existente. Los nombres de catálogo son los de la URL de cada
/// pantalla, para que quien lea la petición sepa de qué habla.
/// </summary>
public sealed record BuscarCodigoDeCatalogoQuery(string Catalogo, string Codigo) : IRequest<Result<CodigoDeCatalogoEncontrado>>;

public sealed class BuscarCodigoDeCatalogoQueryHandler(IApplicationDbContext db)
    : IRequestHandler<BuscarCodigoDeCatalogoQuery, Result<CodigoDeCatalogoEncontrado>>
{
    public async Task<Result<CodigoDeCatalogoEncontrado>> Handle(BuscarCodigoDeCatalogoQuery request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Codigo);
        if (codigo is null)
            return Result.Success(new CodigoDeCatalogoEncontrado(false, null, null));

        var consulta = Consulta(request.Catalogo, codigo);
        if (consulta is null)
            return Result.Failure<CodigoDeCatalogoEncontrado>(
                new Error("Catalogo.Desconocido", $"No existe el catálogo «{request.Catalogo}»."));

        var hallado = await consulta.FirstOrDefaultAsync(ct);
        return Result.Success(hallado is null
            ? new CodigoDeCatalogoEncontrado(false, null, null)
            : new CodigoDeCatalogoEncontrado(true, hallado.PublicId, hallado.Nombre));
    }

    private sealed record Hallazgo(Guid PublicId, string Nombre);

    private IQueryable<Hallazgo>? Consulta(string catalogo, string codigo) => catalogo.ToLowerInvariant() switch
    {
        // Nómina: el código es obligatorio y único.
        "eps" => db.HealthInsuranceProviders.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "arl" => db.WorkRiskProviders.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "pensiones" => db.PensionProviders.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "cesantias" => db.SeveranceProviders.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "cajas-compensacion" => db.FamilyCompensationFunds.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "causas-retencion" => db.WithholdingCauses.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        // Contabilidad (feature 009): el código de la cuenta es la nomenclatura del plan.
        "cuentas" => db.ChartOfAccounts.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "tipos-comprobante" => db.VoucherTypes.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "documentos-cruce" => db.CrossDocumentTypes.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        // Core: el código es opcional y vive en LegacyCode.
        "agencias" => db.Branches.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "bancos" => db.Banks.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "empresas" => db.Companies.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "centros-costo" => db.CostCenters.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "secciones" => db.Sections.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "profesiones" => db.Professions.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "cargos" => db.Positions.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "parentescos" => db.Relationships.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "motivos-retiro" => db.WithdrawalReasons.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "enfermedades" => db.Diseases.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "entidades" => db.ExternalEntities.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "comites" => db.Committees.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "deportes" => db.Sports.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "actividades-culturales" => db.CulturalActivities.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "convenios" => db.Agreements.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        // Inventario (feature 012, T150): el código del tipo de documento es inmutable y único entre vivos.
        "tipos-de-documento" => db.InventoryDocumentTypes.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        // Inventario (feature 012, T213; contracts/api.md §17.1): catálogo y bodegas. El código del producto tiene 20.
        "unidades" => db.UnitsOfMeasure.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "categorias" => db.ProductCategories.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "marcas" => db.Brands.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "grupos-contables" => db.AccountingGroups.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "productos" => db.Products.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "tipos-de-bodega" => db.WarehouseTypes.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "bodegas" => db.Warehouses.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "causas-de-ajuste" => db.AdjustmentCauses.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "canales-de-venta" => db.SalesChannels.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        // Catálogo tributario de Core (feature 012, T165): el código de la tarifa es el mismo en todas sus vigencias.
        "impuestos" => db.TaxDefinitions.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "tarifas" => db.TaxRates.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).OrderByDescending(e => e.ValidFrom).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "conceptos-de-retencion" => db.WithholdingConcepts.AsNoTracking().Where(e => !e.IsDeleted && e.Code == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        "ciudades" => db.Cities.AsNoTracking().Where(e => !e.IsDeleted && e.LegacyCode == codigo).Select(e => new Hallazgo(e.PublicId, e.Name)),
        _ => null,
    };
}
