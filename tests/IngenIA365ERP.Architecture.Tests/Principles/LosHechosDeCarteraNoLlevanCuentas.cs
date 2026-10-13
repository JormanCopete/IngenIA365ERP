using System.Reflection;
using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-126 (DT §2.25, T13; T0037; contracts/mensajes.md §4, §9): el contenido de los mensajes de Cartera lleva dimensiones
/// por código y nunca una cuenta contable: ningún record de <c>Contracts/Lending</c> tiene propiedades <c>Account*</c> (salvo los dos
/// sellos de texto que no son cuentas: quién registró la cuenta por cobrar, <c>AccountsReceivableRecordedBy</c>, y la referencia del
/// registro que redujo la provisional, <c>AccountingReference</c>), <c>Puc*</c> ni códigos de cuenta. La cuenta bancaria del
/// beneficiario (<c>BankAccount*</c>) no es una cuenta contable. La matriz de Contabilidad decide las cuentas (FR-129).
/// </summary>
public class LosHechosDeCarteraNoLlevanCuentas
{
    /// <summary>Carpetas de contratos de mensaje de Cartera (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = ["Core/IngenIA365ERP.Application/Common/Integration/Contracts/Lending"];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = [];

    /// <summary>Propiedades que empiezan por <c>Account</c> y no son cuentas contables (los campos de mensajes.md §10.1, §10.3, §10.16).</summary>
    private static readonly string[] SellosQueNoSonCuentas = ["AccountsReceivableRecordedBy", "AccountingReference"];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\b(Account(Id|Code|PublicId|Number)?|Cuenta(Contable)?|Puc\w*)\b\s*[{;,)]", RegexOptions.Compiled), "una propiedad de cuenta en un contrato de mensaje"),
    ];

    private static readonly Regex NombreDeCuenta = new(
        @"^(Account\w*|Puc\w*|Cuenta\w*|\w*(Ledger|Accounting)Account\w*|\w*AccountCode|\w*AccountId)$", RegexOptions.Compiled);

    [Fact]
    public void Los_contratos_de_mensaje_no_llevan_cuentas()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        Assert.True(archivos.Count > 0, "No se encontraron los contratos de Cartera: si se movieron, actualizá Carpetas.");
        var infracciones = ReglaSobreFuente.Infracciones(archivos, Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Un contrato de mensaje de Cartera lleva una cuenta (FR-126)");
    }

    [Fact]
    public void Ningun_record_de_Contracts_Lending_tiene_propiedades_de_cuenta()
    {
        var records = typeof(LoanRefV1).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(LoanRefV1).Namespace && t.IsClass && !t.IsAbstract)
            .ToList();
        Assert.True(records.Count > 40, "No se encontraron los records de Contracts/Lending.");

        var infracciones = records
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => (Tipo: t, Propiedad: p)))
            .Where(x => NombreDeCuenta.IsMatch(x.Propiedad.Name) && !SellosQueNoSonCuentas.Contains(x.Propiedad.Name))
            .Select(x => $"{x.Tipo.Name}.{x.Propiedad.Name}")
            .Concat(records.Where(t => Regex.IsMatch(t.Name, "(Account|Cuenta|Puc)")).Select(t => $"{t.Name}: un bloque de cuenta"))
            .ToList();

        ReglaSobreFuente.SinInfracciones(infracciones, "Un record de mensaje de Cartera tiene una propiedad de cuenta contable (FR-126)");
    }

    /// <summary>
    /// Feature 013, US7 (T0606; contracts/contabilidad.md §1.2): partiendo de los tipos del catálogo de mensajes de Cartera
    /// (<see cref="CatalogoDeMensajesDeCarteraV1.Todos"/>) y de todo record de <c>Contracts/Lending</c>, recorre recursivamente sus bloques
    /// (<c>LoanFactDimensionsV1</c>, <c>ConceptAmountV1</c>, <c>LoanFactLineV1</c>, <c>GeneralImpairmentLineV1</c>…, también dentro de listas)
    /// y rechaza toda propiedad cuyo nombre empiece por <c>Account</c> o <c>Cuenta</c>, salvo los dos sellos de texto que no son cuentas.
    /// </summary>
    [Fact]
    public void Ningun_bloque_de_un_hecho_de_Cartera_lleva_una_cuenta()
    {
        var raices = CatalogoDeMensajesDeCarteraV1.Todos.Select(t => t.Record)
            .Concat(typeof(LoanRefV1).Assembly.GetTypes().Where(t => t.Namespace == typeof(LoanRefV1).Namespace && t.IsClass && !t.IsAbstract))
            .Distinct()
            .ToList();
        Assert.True(CatalogoDeMensajesDeCarteraV1.Todos.Count >= 35, "El catálogo de mensajes de Cartera perdió tipos.");

        var visitados = new HashSet<Type>();
        var pendientes = new Queue<Type>(raices);
        var infracciones = new List<string>();
        while (pendientes.Count > 0)
        {
            var tipo = pendientes.Dequeue();
            if (!visitados.Add(tipo)) continue;
            foreach (var propiedad in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if ((propiedad.Name.StartsWith("Account", StringComparison.Ordinal) || propiedad.Name.StartsWith("Cuenta", StringComparison.Ordinal))
                    && !SellosQueNoSonCuentas.Contains(propiedad.Name))
                    infracciones.Add($"{tipo.Name}.{propiedad.Name}");
                foreach (var hijo in TiposDeBloque(propiedad.PropertyType)) pendientes.Enqueue(hijo);
            }
        }

        Assert.True(visitados.Count > raices.Count, "No se recorrió ningún bloque anidado: ¿cambió la forma de los contratos?");
        ReglaSobreFuente.SinInfracciones(infracciones, "Un bloque de un hecho de Cartera lleva una cuenta contable (FR-126)");
    }

    /// <summary>Los bloques propios de los contratos que cuelgan de una propiedad (directo, en arreglo, lista o diccionario).</summary>
    private static IEnumerable<Type> TiposDeBloque(Type tipo)
    {
        if (tipo.IsArray) return TiposDeBloque(tipo.GetElementType()!);
        if (tipo.IsGenericType) return tipo.GetGenericArguments().SelectMany(TiposDeBloque);
        return tipo.Namespace is { } ns && ns.StartsWith("IngenIA365ERP.Application.Common.Integration.Contracts", StringComparison.Ordinal)
               && tipo.IsClass && !tipo.IsAbstract
            ? [tipo]
            : [];
    }
}
