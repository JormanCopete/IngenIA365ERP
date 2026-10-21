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

    /// <summary>
    /// Archivos exentos (rutas relativas a la raíz, con barras normales). Los de captaciones (<c>Contracts/Lending/Deposits</c>, feature 014) tienen su
    /// propia regla abajo: allí <c>Account</c> es la cuenta del asociado.
    /// </summary>
    private static readonly string[] Exentos = [];

    private static bool EsDeCaptaciones(string relativo) =>
        relativo.Contains("/Contracts/Lending/Deposits/", StringComparison.Ordinal)
        // Feature 014 (T055): la v2 de la orden de giro nombra la cuenta del asociado de origen del dinero (MemberAccountRefV1, mensajes.md §5.3).
        || relativo.EndsWith("/Contracts/Lending/OrdenDeGiroEmitidaV2.cs", StringComparison.Ordinal)
        // Feature 014 (T091, D36): la v2 del archivo de descuentos nombra la cuenta del asociado de cada cuota (MemberAccountRefV1, mensajes.md §5.4).
        || relativo.EndsWith("/Contracts/Lending/DescuentosPorPagaduriaV2.cs", StringComparison.Ordinal);

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
        var infracciones = ReglaSobreFuente.Infracciones(archivos, Prohibidos, relativo => Exentos.Contains(relativo) || EsDeCaptaciones(relativo));
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
            .Where(x => NombreDeCuenta.IsMatch(x.Propiedad.Name) && !SellosQueNoSonCuentas.Contains(x.Propiedad.Name)
                        && x.Propiedad.PropertyType != typeof(IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.MemberAccountRefV1))
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
                // En los hechos de captaciones (feature 014) la cuenta del asociado no es una cuenta del plan: la vigila su propia regla.
                // La v2 de la orden de giro (fuera de Deposits) también nombra la cuenta del asociado, por su bloque.
                var esLaCuentaDelAsociado =
                    propiedad.PropertyType == typeof(IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.MemberAccountRefV1)
                    || ((tipo.Namespace ?? string.Empty).StartsWith(EspacioDeCaptaciones, StringComparison.Ordinal) && CuentaDelAsociado.Contains(propiedad.Name));
                if ((propiedad.Name.StartsWith("Account", StringComparison.Ordinal) || propiedad.Name.StartsWith("Cuenta", StringComparison.Ordinal))
                    && !SellosQueNoSonCuentas.Contains(propiedad.Name) && !esLaCuentaDelAsociado)
                    infracciones.Add($"{tipo.Name}.{propiedad.Name}");
                foreach (var hijo in TiposDeBloque(propiedad.PropertyType)) pendientes.Enqueue(hijo);
            }
        }

        Assert.True(visitados.Count > raices.Count, "No se recorrió ningún bloque anidado: ¿cambió la forma de los contratos?");
        ReglaSobreFuente.SinInfracciones(infracciones, "Un bloque de un hecho de Cartera lleva una cuenta contable (FR-126)");
    }

    // ------------------------------------------------------------------------------------- feature 014 (T037; SC-009) --

    /// <summary>
    /// Feature 014 (T037; SC-009; mensajes.md §3): los hechos de captaciones y aportes viven en <c>Contracts/Lending/Deposits</c> y tampoco llevan cuentas
    /// contables. Allí <c>Account</c> nombra la <b>cuenta del asociado</b> (<c>MemberAccountRefV1</c>: su <c>PublicId</c> y su número, el documento
    /// cruce) y no una cuenta del plan: se admiten sólo la referencia a ese bloque, sus dos campos y los conteos de cuentas. Lo demás que suene a cuenta
    /// contable (<c>Puc*</c>, <c>Cuenta*</c>, <c>LedgerAccount</c>, <c>AccountingAccount</c>, <c>AccountCode</c>, <c>AccountId</c>) se rechaza igual que
    /// en los hechos de crédito. Los bloques nacen con T047; hasta entonces la regla no tiene tipos que violar.
    /// </summary>
    private const string EspacioDeCaptaciones = "IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits";

    /// <summary>Lo que en los hechos de captaciones es la cuenta del asociado o un conteo, no una cuenta contable.</summary>
    private static readonly string[] CuentaDelAsociado =
        ["AccountPublicId", "AccountNumber", "AccountCount", "AccountFlag", "TargetAccountPublicId", "SourceAccountPublicId"];

    private static readonly Regex CuentaContable = new(
        @"^(Puc\w*|Cuenta\w*|\w*(Ledger|Accounting)Account\w*|\w*AccountCode|\w*AccountId|Account(Code|Id)?)$", RegexOptions.Compiled);

    [Fact]
    public void Ningun_hecho_de_captaciones_lleva_una_cuenta_contable()
    {
        var tipos = typeof(LoanRefV1).Assembly.GetTypes()
            .Where(t => (t.Namespace ?? string.Empty).StartsWith(EspacioDeCaptaciones, StringComparison.Ordinal) && t.IsClass && !t.IsAbstract)
            .ToList();
        var infracciones = new List<string>();
        foreach (var tipo in tipos)
        {
            // «Cuenta» en el nombre de un hecho de captaciones es la del asociado (CargoDeCuentaCausado, OperacionDeCuentaReversada…).
            if (Regex.IsMatch(tipo.Name, "(Puc|CuentaContable|LedgerAccount|AccountingAccount)")) infracciones.Add($"{tipo.Name}: un bloque de cuenta contable");
            foreach (var propiedad in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var esLaCuentaDelAsociado = propiedad.PropertyType.Name == "MemberAccountRefV1" || CuentaDelAsociado.Contains(propiedad.Name);
                if (!esLaCuentaDelAsociado && CuentaContable.IsMatch(propiedad.Name)) infracciones.Add($"{tipo.Name}.{propiedad.Name}");
            }
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Un hecho de captaciones lleva una cuenta contable (FR-097, SC-009)");
    }

    [Fact]
    public void Los_contratos_de_captaciones_no_nombran_cuentas_contables_en_el_fuente()
    {
        var prohibidos = new (Regex Patron, string Que)[]
        {
            (new Regex(@"\b(Puc\w*|Cuenta(Contable)?|\w*(Ledger|Accounting)Account\w*|\w*AccountCode|AccountId)\b\s*[{;,)]", RegexOptions.Compiled),
                "una propiedad de cuenta contable en un hecho de captaciones"),
        };
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(["Core/IngenIA365ERP.Application/Common/Integration/Contracts/Lending/Deposits"]), prohibidos);
        ReglaSobreFuente.SinInfracciones(infracciones, "Un contrato de mensaje de captaciones lleva una cuenta contable (SC-009)");
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
