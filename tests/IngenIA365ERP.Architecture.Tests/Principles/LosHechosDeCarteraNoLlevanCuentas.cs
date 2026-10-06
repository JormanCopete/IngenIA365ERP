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
}
