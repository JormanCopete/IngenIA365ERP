using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 014, T036 (research R06, R07, R29; data-model §0.7; FR-021, FR-022): dos fronteras de los motores de captaciones.
/// <list type="bullet">
/// <item><b><c>LaRetencionYElGmfSoloLosCalculanSusMotores</c></b>: la retención en la fuente sobre rendimientos y el GMF los calculan sólo
/// <c>RetencionSobreRendimientos</c> y <c>GravamenMovimientosFinancieros</c> (motores puros de <c>Domain/Lending/Deposits</c>); <c>RegistroDeCuentas</c>
/// sólo escribe lo que devuelven. Todo archivo que arma filas <c>WithholdingApplied</c> o <c>GmfCharged</c> nombra su motor, salvo el registro (que
/// valida la pareja) y el propio enum.</item>
/// <item><b><c>LasCaptacionesNoUsanLaUsura</c></b> (R01): ninguna ruta de captación ni de aportes llama al control de usura, a la usura certificada ni
/// al bloqueo por usura de la línea; la usura es de colocación (<c>CreditLine.Activar</c> y el bloqueo ya ignoran las demás naturalezas, T011).</item>
/// </list>
/// Los motores nacen en K8; hasta entonces la primera regla no tiene archivos que la violen (pasa porque no hay nada que violar, no por un retorno
/// temprano) y fija dónde vivirán.
/// </summary>
public class LasCaptacionesRespetanSusMotores
{
    /// <summary>Todo el código de producción que podría armar filas del libro de cuentas (relativo a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = ["Core/IngenIA365ERP.Application", "Core/IngenIA365ERP.Domain", "Infrastructure", "Presentation"];

    /// <summary>Las captaciones y aportes (relativas a <c>src/</c>).</summary>
    private static readonly string[] CarpetasDeCaptaciones =
    [
        "Core/IngenIA365ERP.Application/Lending/Deposits",
        "Core/IngenIA365ERP.Domain/Lending/Deposits",
        "Core/IngenIA365ERP.Domain/Entities/Lending/Deposits",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/Deposits",
        "Presentation/IngenIA365ERP.Shared/Pages/Cartera/Captaciones",
    ];

    private const string MotorDeRetencion = "RetencionSobreRendimientos";
    private const string MotorDeGmf = "GravamenMovimientosFinancieros";

    /// <summary>Quien escribe (el registro, que sólo valida la pareja) y quien declara las clases (el enum) no calculan.</summary>
    private static readonly string[] Exentos =
    [
        "src/Core/IngenIA365ERP.Application/Lending/Deposits/Ledger/RegistroDeCuentas.cs",
        "src/Core/IngenIA365ERP.Domain/Enums/Lending/DepositEnums.cs",
    ];

    [Fact]
    public void La_retencion_y_el_gmf_solo_los_calculan_sus_motores()
    {
        var root = RepoPath.FindRepoRoot();
        var infracciones = new List<string>();
        foreach (var archivo in ReglaSobreFuente.ArchivosEn(Carpetas))
        {
            var relativo = ReglaSobreFuente.Relativo(root, archivo);
            var nombre = Path.GetFileNameWithoutExtension(archivo);
            if (Exentos.Contains(relativo) || nombre is MotorDeRetencion or MotorDeGmf) continue;
            var fuente = FuenteSinComentarios.Leer(archivo);
            if (Regex.IsMatch(fuente, @"\bMemberAccountTransactionKind\.WithholdingApplied\b") && !fuente.Contains(MotorDeRetencion, StringComparison.Ordinal))
                infracciones.Add($"{relativo}: arma una retención sin RetencionSobreRendimientos");
            if (Regex.IsMatch(fuente, @"\bMemberAccountTransactionKind\.GmfCharged\b") && !fuente.Contains(MotorDeGmf, StringComparison.Ordinal))
                infracciones.Add($"{relativo}: arma un GMF sin GravamenMovimientosFinancieros");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "La retención o el GMF se calculan fuera de sus motores (R06, R07, data-model §0.7)");
    }

    /// <summary>Los dos motores, cuando existan, viven en <c>Domain/Lending/Deposits</c>, son estáticos y no tocan la base ni el reloj.</summary>
    [Fact]
    public void Los_motores_de_retencion_y_gmf_son_puros_y_viven_en_el_dominio_de_captaciones()
    {
        var root = RepoPath.FindRepoRoot();
        var motores = ReglaSobreFuente.ArchivosEn(Carpetas)
            .Where(a => Path.GetFileNameWithoutExtension(a) is MotorDeRetencion or MotorDeGmf)
            .ToList();
        var infracciones = new List<string>();
        foreach (var archivo in motores)
        {
            var relativo = ReglaSobreFuente.Relativo(root, archivo);
            if (!relativo.StartsWith("src/Core/IngenIA365ERP.Domain/Lending/Deposits/", StringComparison.Ordinal))
                infracciones.Add($"{relativo}: el motor vive en Domain/Lending/Deposits");
            var fuente = FuenteSinComentarios.Leer(archivo);
            if (!Regex.IsMatch(fuente, $@"\bstatic\s+(partial\s+)?class\s+{Path.GetFileNameWithoutExtension(archivo)}\b"))
                infracciones.Add($"{relativo}: el motor es una clase estática (pura)");
            if (Regex.IsMatch(fuente, @"\b(IApplicationDbContext|DbContext|IDateTimeService|TimeProvider|HoyLocal)\b|\bDateTime\.(Now|UtcNow|Today)\b"))
                infracciones.Add($"{relativo}: el motor toca la base o el reloj");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Los motores de retención y GMF no son puros (R06, R07)");
    }

    private static readonly (Regex Patron, string Que)[] Usura =
    [
        (new Regex(@"\b(ControlDeUsura|UsuraCertificada|ResultadoDeUsura|CargoDeUsura)\b", RegexOptions.Compiled), "usa el control de usura de la colocación"),
        (new Regex(@"\b(ControlarUsura|SimularConUsura|BloquearPorUsura|DesbloquearPorUsura)\(", RegexOptions.Compiled), "llama a la usura de la colocación"),
        (new Regex(@"\bCodigosDeValorNacional\.Usura\w*\b|""USURA""", RegexOptions.Compiled), "lee la usura certificada"),
    ];

    [Fact]
    public void Las_captaciones_no_usan_la_usura()
    {
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(CarpetasDeCaptaciones), Usura);
        ReglaSobreFuente.SinInfracciones(infracciones, "Una ruta de captación o de aportes usa la usura (R01, LasCaptacionesNoUsanLaUsura)");
    }

    /// <summary>La regla de la usura recorre las carpetas de captaciones que ya existen (no pasa en vacío por una carpeta mal escrita).</summary>
    [Fact]
    public void La_regla_de_la_usura_recorre_las_captaciones()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(CarpetasDeCaptaciones).ToList();
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Deposits", "ReconstruccionDeCuenta.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Ledger", "RegistroDeCuentas.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Accounts", "MemberAccount.cs"), StringComparison.Ordinal));
    }
}
