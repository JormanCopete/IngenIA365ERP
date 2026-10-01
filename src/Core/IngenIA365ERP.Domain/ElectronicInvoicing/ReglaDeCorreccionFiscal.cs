namespace IngenIA365ERP.Domain.ElectronicInvoicing;

/// <summary>
/// Decide si un documento rechazado se corrige por el caso a o necesita el caso b (feature 012, I4, T696;
/// contracts/dian.md §8.2, FR-066), pura. Quien opera no elige: lo decide la huella económica. Compara el canónico
/// vigente con el nuevo campo por campo sobre la misma lista que firma <see cref="HuellaEconomica"/>:
/// <list type="bullet">
/// <item>si algún campo económico cambió → <see cref="CasoB"/> con esos campos (<c>fields[]</c> del error
/// <c>ElectronicInvoicing.Document.EconomicFootprintChanged</c>);</item>
/// <item>si no → <see cref="CasoA"/>, con los datos no económicos de la contraparte que cambiaron (nombre, dirección,
/// ciudad, correo, teléfono, responsabilidades), que son los que van a la versión nueva de la copia fiscal.</item>
/// </list>
/// (nuevo)
/// </summary>
public static class ReglaDeCorreccionFiscal
{
    /// <summary>El veredicto de la regla.</summary>
    public abstract record Decision;

    /// <summary>Caso a: la huella no cambió. <see cref="CamposCambiados"/> son los no económicos que sí cambiaron.</summary>
    public sealed record CasoA(IReadOnlyList<string> CamposCambiados) : Decision;

    /// <summary>Caso b: cambió al menos un campo económico; <see cref="Fields"/> los nombra con su ruta del canónico.</summary>
    public sealed record CasoB(IReadOnlyList<string> Fields) : Decision;

    public static Decision Decidir(DatosFiscalesDelDocumento vigente, DatosFiscalesDelDocumento nuevo)
    {
        ArgumentNullException.ThrowIfNull(vigente);
        ArgumentNullException.ThrowIfNull(nuevo);

        var economicos = Diferencias(HuellaEconomica.CamposEconomicos(vigente), HuellaEconomica.CamposEconomicos(nuevo));
        if (economicos.Count > 0) return new CasoB(economicos);

        return new CasoA(Diferencias(HuellaEconomica.CamposNoEconomicos(vigente), HuellaEconomica.CamposNoEconomicos(nuevo)));
    }

    /// <summary>Los campos cuyo valor difiere, o que están en uno solo (una línea de más o de menos), en orden de aparición.</summary>
    private static List<string> Diferencias(IReadOnlyList<(string Campo, string Valor)> antes, IReadOnlyList<(string Campo, string Valor)> despues)
    {
        var mapaAntes = antes.ToDictionary(c => c.Campo, c => c.Valor, StringComparer.Ordinal);
        var mapaDespues = despues.ToDictionary(c => c.Campo, c => c.Valor, StringComparer.Ordinal);

        return antes.Select(c => c.Campo)
            .Concat(despues.Select(c => c.Campo).Where(c => !mapaAntes.ContainsKey(c)))
            .Where(campo => !mapaAntes.TryGetValue(campo, out var a) || !mapaDespues.TryGetValue(campo, out var d)
                || !string.Equals(a, d, StringComparison.Ordinal))
            .ToList();
    }
}
