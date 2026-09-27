using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Sales.Payments;

/// <summary>
/// Un medio con lo que decide dónde se ofrece: estado, vigencia, las tres marcas «todos» del medio de Core y los tres conjuntos
/// explícitos del módulo (<c>INV_PaymentMeansPointsOfSale</c>, <c>…Channels</c>, <c>…DocumentTypes</c>). (nuevo)
/// </summary>
public sealed record MedioOfrecible(
    int PaymentMeansId,
    string Code,
    string Name,
    PaymentMeansClass Class,
    short DisplayOrder,
    string? QuickKey,
    bool IsActive,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool OfferedAtAllPointsOfSale,
    IReadOnlyCollection<int> PointOfSaleIds,
    bool OfferedInAllChannels,
    IReadOnlyCollection<int> SalesChannelIds,
    bool OfferedForAllDocumentTypes,
    IReadOnlyCollection<int> DocumentTypeIds);

/// <summary>
/// Dónde y a quién se cobra: fecha, punto (nulo en la factura de oficina), canal (nulo si el documento no lo lleva), tipo de
/// documento, si el cliente es el consumidor final y si el usuario tiene <c>Inventory.Sales.SellOnCredit</c>. (nuevo)
/// </summary>
public sealed record CasoDeCobro(DateOnly Date, int? PointOfSaleId, int? SalesChannelId, int DocumentTypeId, bool IsFinalConsumer, bool CanSellOnCredit);

/// <summary>
/// La regla pura que decide si un medio se ofrece (feature 012, I3, T579; T25; data-model §15 «Disponibilidad de medios»;
/// contracts/api.md §22.3). La usan la pantalla (qué medios mostrar), el POS y el servidor al cobrar (un medio que no se ofrece
/// es <see cref="MeansNotAvailable"/>).
/// <list type="bullet">
/// <item>activo y vigente a la fecha (los dos extremos incluidos);</item>
/// <item>en cada dimensión, la marca «todos» o estar en el conjunto; un conjunto vacío <b>no</b> es «todos»; una dimensión que
/// el caso no trae (punto o canal nulos) no restringe;</item>
/// <item>los créditos no se ofrecen al consumidor final ni a quien no puede vender a crédito.</item>
/// </list>
/// (nuevo)
/// </summary>
public static class DisponibilidadDeMedio
{
    public const string MeansNotAvailable = "Payments.MeansNotAvailable";

    public static bool SeOfrece(MedioOfrecible medio, CasoDeCobro caso)
    {
        ArgumentNullException.ThrowIfNull(medio);
        ArgumentNullException.ThrowIfNull(caso);

        if (!medio.IsActive || medio.ValidFrom > caso.Date || (medio.ValidTo is { } hasta && hasta < caso.Date)) return false;
        if (caso.PointOfSaleId is { } punto && !medio.OfferedAtAllPointsOfSale && !medio.PointOfSaleIds.Contains(punto)) return false;
        if (caso.SalesChannelId is { } canal && !medio.OfferedInAllChannels && !medio.SalesChannelIds.Contains(canal)) return false;
        if (!medio.OfferedForAllDocumentTypes && !medio.DocumentTypeIds.Contains(caso.DocumentTypeId)) return false;
        if (ClasesDeMedio.EsCredito(medio.Class) && (caso.IsFinalConsumer || !caso.CanSellOnCredit)) return false;
        return true;
    }

    /// <summary>Los medios que se ofrecen en el caso, por <c>DisplayOrder</c> y luego por código.</summary>
    public static IReadOnlyList<MedioOfrecible> Ofrecidos(IEnumerable<MedioOfrecible> medios, CasoDeCobro caso)
    {
        ArgumentNullException.ThrowIfNull(medios);
        return medios.Where(m => SeOfrece(m, caso))
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Code, StringComparer.Ordinal)
            .ToList();
    }
}
