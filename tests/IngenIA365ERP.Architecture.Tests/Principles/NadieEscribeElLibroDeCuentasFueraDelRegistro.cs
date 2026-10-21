using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 014, T035 (data-model §0.7; research R03, R29; FR-001): el libro de cuentas y todo lo que lo acompaña lo escribe sólo
/// <c>RegistroDeCuentas</c> —las operaciones, las transacciones, las posiciones diarias, la fila de control y sus marcas de agua, y desde K8 y K9 las
/// liquidaciones, la retención acumulada, el GMF del mes, las condiciones del CDAT, su plan y las cuotas exigibles—. Recorre todo Application,
/// Infrastructure y Presentation (no sólo las carpetas de captaciones, para que ningún módulo escriba el libro por un costado) y exime sólo el registro.
/// Molde: <see cref="NadieEscribeElLibroDeCarteraFueraDelRegistro"/>. Las tablas de entregas posteriores ya están en la lista: si alguien las escribe
/// antes, falla igual.
/// </summary>
public class NadieEscribeElLibroDeCuentasFueraDelRegistro
{
    /// <summary>Carpetas recorridas (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = ["Core/IngenIA365ERP.Application", "Infrastructure", "Presentation"];

    /// <summary>El único escritor (ruta relativa a la raíz).</summary>
    private const string Registro = "src/Core/IngenIA365ERP.Application/Lending/Deposits/Ledger/RegistroDeCuentas.cs";

    /// <summary>Los conjuntos de data-model §0.7, también los de K8 y K9 que todavía no existen.</summary>
    private const string Conjuntos =
        "MemberAccountOperations|MemberAccountTransactions|MemberAccountDailyPositions|MemberAccountControls|InterestSettlements|WithholdingAccumulations"
        + "|GmfMonthlyUsage|GmfMonthlyUsages|MemberAccountTermsVersions|TermDepositSchedules|TermDepositScheduleItems|AccountInstallments";

    /// <summary>Las entidades de esos conjuntos.</summary>
    private const string Entidades =
        "MemberAccountOperation|MemberAccountTransaction|MemberAccountDailyPosition|MemberAccountControl|InterestSettlement|WithholdingAccumulation"
        + "|GmfMonthlyUsage|MemberAccountTermsVersion|TermDepositScheduleItem|AccountInstallment";

    private static readonly (Regex Patron, string Que)[] Prohibidos =
    [
        (new Regex($@"\b({Conjuntos})\.(Add|AddRange|Update|UpdateRange|Attach|AttachRange)\(", RegexOptions.Compiled),
            "escribe el libro de cuentas, sus proyecciones, sus versiones o la fila de control"),
        (new Regex($@"\bSet<\s*({Entidades})\s*>\(\)\s*\.\s*(Add|AddRange|Update|UpdateRange|Attach)\(", RegexOptions.Compiled),
            "escribe el libro de cuentas por Set<>"),
        // Las marcas propias de la cuenta (las compartidas con el crédito, LastAccruedDate y compañía, las vigila también la regla de la 013).
        (new Regex(@"\.(LastSettledThrough|LastHolderActivityDate|LastInstallmentPeriod)\s*(=(?!=)|\+=)", RegexOptions.Compiled),
            "mueve la marca de agua de la cuenta"),
    ];

    /// <summary>
    /// Armar una fila del libro o una posición fuera del registro se admite sólo para entregársela (<c>RegistrarTransacciones</c>,
    /// <c>RegistrarPosicion</c>, <c>PosicionVersionada</c>, <c>CausarEnBloque</c>); un archivo que la arma y no se la entrega la estaría escribiendo por
    /// su cuenta.
    /// </summary>
    private static readonly Regex ArmaUnaFila = new(@"\bnew\s+(MemberAccountTransaction|MemberAccountDailyPosition|MemberAccountOperation)\s*[({]", RegexOptions.Compiled);

    private static readonly Regex SeLaEntrega =
        new(@"\b(RegistrarTransacciones|RegistrarPosicion|PosicionVersionada|CausarEnBloque|RegistrarOperacion)\(", RegexOptions.Compiled);

    [Fact]
    public void Solo_el_registro_escribe_el_libro_de_cuentas()
    {
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos,
            relativo => relativo == Registro);
        ReglaSobreFuente.SinInfracciones(infracciones, "Alguien escribe el libro de cuentas fuera de RegistroDeCuentas (FR-001, data-model §0.7)");
    }

    [Fact]
    public void Quien_arma_una_fila_del_libro_de_cuentas_se_la_entrega_al_registro()
    {
        var root = RepoPath.FindRepoRoot();
        var infracciones = ReglaSobreFuente.ArchivosEn(Carpetas)
            .Where(a => ReglaSobreFuente.Relativo(root, a) != Registro)
            .Select(a => (Archivo: a, Texto: FuenteSinComentarios.Leer(a)))
            .Where(x => ArmaUnaFila.IsMatch(x.Texto) && !SeLaEntrega.IsMatch(x.Texto))
            .Select(x => $"{ReglaSobreFuente.Relativo(root, x.Archivo)}: arma una fila del libro de cuentas y no se la entrega a RegistroDeCuentas")
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones, "Alguien arma una fila del libro de cuentas y la escribe fuera de RegistroDeCuentas (FR-001)");
    }

    /// <summary>
    /// El registro existe donde lo dice data-model §0.7, es el único que nombra los conjuntos para escribir y la regla de la 013 lo reconoce como el escritor
    /// de las marcas que comparte con el crédito.
    /// </summary>
    [Fact]
    public void El_registro_de_cuentas_es_el_escritor_y_esta_en_su_sitio()
    {
        var root = RepoPath.FindRepoRoot();
        var registro = Path.Combine(root, Registro.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(registro), "RegistroDeCuentas vive en Application/Lending/Deposits/Ledger (data-model §0.7).");
        var fuente = FuenteSinComentarios.Leer(registro);
        foreach (var conjunto in new[] { "MemberAccountOperations", "MemberAccountTransactions", "MemberAccountDailyPositions", "MemberAccountControls" })
            Assert.Matches($@"\b{conjunto}\.(Add|AddRange)\(", fuente);
        Assert.Equal("IngenIA365ERP.Application.Lending.Deposits.Ledger", typeof(IngenIA365ERP.Application.Lending.Deposits.Ledger.RegistroDeCuentas).Namespace);
    }
}
