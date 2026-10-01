using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Taxation;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Taxes;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Taxes;

/// <summary>
/// El único lector del catálogo tributario para el motor (feature 012, T22, T164; data-model §17): arma la
/// <see cref="TaxCatalogSnapshot"/> a una fecha con las definiciones, las tarifas vigentes y los conceptos, la UVT de
/// <see cref="IValorUvt"/> y los parámetros con vigencia de <see cref="ILectorDeParametros"/>: el perfil de la
/// cooperativa (<c>TAX</c>), el redondeo de la UVT a pesos (<c>Tributario.RedondeoUvtAPesos</c>) y el de los montos
/// (<c>Redondeo.Montos</c>). Sin UVT vigente falla visible (<c>Taxation.Uvt.Missing</c>): nunca un cero.
/// Scoped; memoriza cada fecha por petición.
/// </summary>
public sealed class LectorDeCatalogoTributario(IApplicationDbContext db, IValorUvt uvt, ILectorDeParametros parametros)
{
    private readonly Dictionary<DateOnly, Result<TaxCatalogSnapshot>> _memoria = [];

    public async Task<Result<TaxCatalogSnapshot>> FotoAsync(DateOnly fecha, CancellationToken ct = default)
    {
        if (_memoria.TryGetValue(fecha, out var memorizada)) return memorizada;
        var resultado = await ArmarAsync(fecha, ct);
        _memoria[fecha] = resultado;
        return resultado;
    }

    private async Task<Result<TaxCatalogSnapshot>> ArmarAsync(DateOnly fecha, CancellationToken ct)
    {
        var valorUvt = await uvt.LeerAsync(fecha, ct);
        if (valorUvt.IsFailure) return Result.Failure<TaxCatalogSnapshot>(valorUvt.Error);

        var responsable = await parametros.LeerComoAsync<bool>(ParametrosTributarios.Modulo, ParametrosTributarios.ResponsableIva, fecha, ct: ct);
        var granContribuyente = await parametros.LeerComoAsync<bool>(ParametrosTributarios.Modulo, ParametrosTributarios.GranContribuyente, fecha, ct: ct);
        var agenteIva = await parametros.LeerComoAsync<bool>(ParametrosTributarios.Modulo, ParametrosTributarios.AgenteRetencionIva, fecha, ct: ct);
        var autorretenedor = await parametros.LeerComoAsync<bool>(ParametrosTributarios.Modulo, ParametrosTributarios.Autorretenedor, fecha, ct: ct);
        var redondeoUvt = await parametros.LeerComoAsync<string>(ParametrosTributarios.Modulo, ParametrosTributarios.RedondeoUvtAPesos, fecha, ct: ct);
        var redondeoMontos = await parametros.LeerComoAsync<string>(ParametrosDeInventario.Modulo, ParametrosDeInventario.RedondeoMontos, fecha, ct: ct);
        foreach (var leido in new Result[] { responsable, granContribuyente, agenteIva, autorretenedor, redondeoUvt, redondeoMontos })
            if (leido.IsFailure) return Result.Failure<TaxCatalogSnapshot>(leido.Error);

        var cooperativa = new PerfilTributario
        {
            PersonType = PersonaJuridica,
            IsVatResponsible = responsable.Value,
            IsIncomeTaxFiler = true,
            IsLargeContributor = granContribuyente.Value,
            IsVatWithholdingAgent = agenteIva.Value,
            IsSelfWithholder = autorretenedor.Value,
            EsAgenteDeRetencion = true,
        };

        var impuestos = await db.TaxDefinitions.AsNoTracking().OrderBy(t => t.Id)
            .Select(t => new ImpuestoEnFoto(t.Id, t.Code, t.Name, t.Kind, t.CalculationForm, t.TaxedOnDefinitionId, t.IsWithholding, t.DianTaxCode, t.IsActive))
            .ToListAsync(ct);
        var conceptos = await db.WithholdingConcepts.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Id)
            .Select(c => new ConceptoEnFoto(c.Id, c.Code, c.Name))
            .ToListAsync(ct);
        var tarifas = (await db.TaxRates.AsNoTracking()
                .Where(r => r.ValidFrom <= fecha && (r.ValidTo == null || r.ValidTo >= fecha))
                .OrderBy(r => r.Id)
                .ToListAsync(ct))
            .Select(r => new TarifaEnFoto(r.Id, r.TaxDefinitionId, r.Code, r.Name, r.Rate, r.AmountPerUnit, r.WithholdingConceptId,
                r.MunicipalityDaneCode, r.ActivityCode, r.MinimumBaseUvt, r.MinimumBasePesos, ReglasDelCatalogoTributario.Condiciones(r),
                r.AppliesTo, r.Priority, r.ValidFrom, r.ValidTo, r.LegalSource))
            .ToList();

        return Result.Success(new TaxCatalogSnapshot(
            fecha,
            valorUvt.Value.Valor,
            ConversionUvt.Redondeo(redondeoUvt.Value),
            string.Equals(redondeoMontos.Value, "Peso", StringComparison.OrdinalIgnoreCase) ? 0 : DecimalesAlCentavo,
            cooperativa,
            impuestos,
            tarifas,
            conceptos));
    }

    /// <summary>La cooperativa es persona jurídica (<c>COR_People.PersonType</c> 02).</summary>
    private const string PersonaJuridica = "02";

    /// <summary><c>Redondeo.Montos = Centavo</c>: dos decimales.</summary>
    private const int DecimalesAlCentavo = 2;
}
