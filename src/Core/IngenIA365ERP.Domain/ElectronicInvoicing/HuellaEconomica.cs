using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IngenIA365ERP.Domain.ElectronicInvoicing;

/// <summary>Un impuesto o una retención del documento: tributo DIAN, tarifa como fracción, base y valor. (nuevo)</summary>
public sealed record ImporteFiscal(string CodigoTributo, decimal Tarifa, decimal Base, decimal Valor);

/// <summary>
/// La contraparte tal como viaja en el canónico (la copia fiscal vigente). Identificación (tipo, número, DV) es
/// económica; lo demás no. (nuevo)
/// </summary>
public sealed record ContraparteFiscal(
    string TipoDeIdentificacion,
    string Identificacion,
    string? DigitoDeVerificacion,
    string Nombre,
    string? Direccion,
    string? CiudadDane,
    string? Correo,
    string? Telefono,
    IReadOnlyList<string> Responsabilidades);

/// <summary>Una línea del canónico: producto, cantidad, base (<c>lineExtension</c>), descuento, impuestos y retenciones. (nuevo)</summary>
public sealed record LineaFiscal(
    int Numero,
    string Producto,
    decimal Cantidad,
    decimal Base,
    decimal Descuento,
    IReadOnlyList<ImporteFiscal> Impuestos,
    IReadOnlyList<ImporteFiscal> Retenciones);

/// <summary>Los totales del canónico (contracts/dian.md §4.2, <c>totals</c>). (nuevo)</summary>
public sealed record TotalesFiscales(
    decimal LineExtension,
    decimal Allowances,
    decimal TaxExclusive,
    decimal Taxes,
    decimal TaxInclusive,
    decimal Charges,
    decimal Rounding,
    decimal Payable,
    decimal Withholdings,
    decimal AmountDue);

/// <summary>
/// Lo que <see cref="ReglaDeCorreccionFiscal"/> y <see cref="HuellaEconomica"/> leen del canónico
/// (<c>DocumentoElectronicoCanonico</c> v1, que vive en Application y lo arma <c>ConstructorDelCanonico</c>). Domain no
/// conoce el canónico: la aplicación le entrega esta vista. (nuevo)
/// </summary>
public sealed record DatosFiscalesDelDocumento(
    ContraparteFiscal Contraparte,
    IReadOnlyList<LineaFiscal> Lineas,
    IReadOnlyList<ImporteFiscal> Retenciones,
    TotalesFiscales Totales);

/// <summary>
/// La huella económica de un documento electrónico (feature 012, I4, T696; contracts/dian.md §8.2; data-model §18,
/// <c>COR_ElectronicDocumentVersions.EconomicFingerprint</c>): el SHA-256, en hexadecimal minúsculo de 64 caracteres, del
/// subconjunto económico del canónico —identificación de la contraparte (tipo, número y DV); por línea producto,
/// cantidad, base, descuento, impuestos y retenciones; las retenciones del documento y cada total—. Los nombres de los
/// campos son las rutas del canónico (<c>counterparty.taxId</c>, <c>lines[1].quantity</c>, <c>totals.payable</c>), las
/// mismas que <see cref="ReglaDeCorreccionFiscal"/> devuelve en <c>fields[]</c>: la huella y la regla leen la misma
/// lista, así no pueden discrepar. Los decimales se escriben sin ceros de escala (2,0 y 2,0000 son lo mismo). (nuevo)
/// </summary>
public static class HuellaEconomica
{
    public static string Calcular(DatosFiscalesDelDocumento datos)
    {
        var texto = new StringBuilder();
        foreach (var (campo, valor) in CamposEconomicos(datos))
            texto.Append(campo).Append('=').Append(valor).Append('\n');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
    }

    /// <summary>Los campos económicos, en orden estable, con su valor normalizado.</summary>
    internal static IReadOnlyList<(string Campo, string Valor)> CamposEconomicos(DatosFiscalesDelDocumento datos)
    {
        var c = datos.Contraparte;
        var campos = new List<(string, string)>
        {
            ("counterparty.idTypeCode", Texto(c.TipoDeIdentificacion)),
            ("counterparty.taxId", Texto(c.Identificacion)),
            ("counterparty.checkDigit", Texto(c.DigitoDeVerificacion)),
        };

        foreach (var linea in datos.Lineas.OrderBy(l => l.Numero))
        {
            var p = $"lines[{linea.Numero.ToString(CultureInfo.InvariantCulture)}]";
            campos.Add(($"{p}.productCode", Texto(linea.Producto)));
            campos.Add(($"{p}.quantity", Numero(linea.Cantidad)));
            campos.Add(($"{p}.base", Numero(linea.Base)));
            campos.Add(($"{p}.discount", Numero(linea.Descuento)));
            campos.Add(($"{p}.taxes", Importes(linea.Impuestos)));
            campos.Add(($"{p}.withholdings", Importes(linea.Retenciones)));
        }

        campos.Add(("withholdings", Importes(datos.Retenciones)));

        var t = datos.Totales;
        campos.Add(("totals.lineExtension", Numero(t.LineExtension)));
        campos.Add(("totals.allowances", Numero(t.Allowances)));
        campos.Add(("totals.taxExclusive", Numero(t.TaxExclusive)));
        campos.Add(("totals.taxes", Numero(t.Taxes)));
        campos.Add(("totals.taxInclusive", Numero(t.TaxInclusive)));
        campos.Add(("totals.charges", Numero(t.Charges)));
        campos.Add(("totals.rounding", Numero(t.Rounding)));
        campos.Add(("totals.payable", Numero(t.Payable)));
        campos.Add(("totals.withholdings", Numero(t.Withholdings)));
        campos.Add(("totals.amountDue", Numero(t.AmountDue)));
        return campos;
    }

    /// <summary>Los datos no económicos de la contraparte: su cambio es el caso a.</summary>
    internal static IReadOnlyList<(string Campo, string Valor)> CamposNoEconomicos(DatosFiscalesDelDocumento datos)
    {
        var c = datos.Contraparte;
        return
        [
            ("counterparty.name", Texto(c.Nombre)),
            ("counterparty.address", Texto(c.Direccion)),
            ("counterparty.cityDaneCode", Texto(c.CiudadDane)),
            ("counterparty.email", Texto(c.Correo)),
            ("counterparty.phone", Texto(c.Telefono)),
            ("counterparty.responsibilities", string.Join(";", c.Responsabilidades.Select(Texto).Order(StringComparer.Ordinal))),
        ];
    }

    private static string Texto(string? valor) => valor?.Trim() ?? string.Empty;

    /// <summary>El decimal sin ceros de escala y con punto (2,0000 → «2»).</summary>
    private static string Numero(decimal valor) => valor.ToString("0.############################", CultureInfo.InvariantCulture);

    private static string Importes(IReadOnlyList<ImporteFiscal> importes) =>
        string.Join(";", importes
            .Select(i => $"{Texto(i.CodigoTributo)}|{Numero(i.Tarifa)}|{Numero(i.Base)}|{Numero(i.Valor)}")
            .Order(StringComparer.Ordinal));
}
