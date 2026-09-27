using IngenIA365ERP.Shared.Services.Core;

namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// Los medios que ofrece el panel de cobro en una venta de oficina (feature 012, I3, T642): el POS los recibe ya filtrados en la venta
/// (<c>availablePaymentMeans</c>); la oficina los arma del catálogo de Core —activos y vigentes a la fecha, con los datáfonos de su
/// adquirente y los valores del crédito—. Dónde se ofrece cada uno y si el cliente puede pagar a crédito lo vuelve a decidir el servidor
/// al guardar (<c>Payments.MeansNotAvailable</c>). (nuevo)
/// </summary>
public static class MediosDeOficina
{
    public static IReadOnlyList<MedioDelPosDto> Desde(IEnumerable<MedioDePagoDto> medios, IEnumerable<DatafonoDto> datafonos, DateOnly fecha)
    {
        var activos = datafonos.Where(d => d.IsActive).ToList();
        return medios
            .Where(m => m.IsActive && m.ValidFrom <= fecha && (m.ValidTo is null || m.ValidTo >= fecha))
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Code, StringComparer.Ordinal)
            .Select(m => new MedioDelPosDto(
                m.PaymentMeansPublicId, m.Code, m.Name, m.Class, m.QuickKey, m.RequiresReference, m.ReferenceKind, m.AllowsChange, m.AllowsPartial, m.CountMethod,
                TextosDeVentas.EsTarjeta(m.Class)
                    ? activos.Where(d => m.CardAcquirerPublicId is null || d.CardAcquirerPublicId == m.CardAcquirerPublicId).Select(d => new DatafonoDelPosDto(d.CardTerminalPublicId, d.Code)).ToList()
                    : [],
                null,
                m.CreditDefaults is { } c ? new CreditoPropuestoDto(c.TermDays, c.DefaultInstallments ?? c.MaxInstallments, c.PeriodicityDays, c.SuggestedLineCode) : null))
            .ToList();
    }
}
