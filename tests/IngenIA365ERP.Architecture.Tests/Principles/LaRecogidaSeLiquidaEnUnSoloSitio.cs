using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 028 (T012; R3, FR-016, FR-024): <b>un solo sitio liquida una recogida</b>, <c>Application/Lending/Payoffs/LiquidacionDeRecogida</c>. La proyección
/// de la solicitud, la vista previa y el comando del desembolso tienen que decir lo mismo al peso, y lo mismo que aplica el contrato con el
/// <c>FullPayoff</c> de la recogida: si otra pieza del desembolso o de la recogida calculara lo que cuesta cancelar por su cuenta —con
/// <c>AplicadorDePagos.ValorParaCancelar</c>, con el valor para cancelar de la caja o con <c>MotorDeCreditoEnDesembolso.LiquidarAsync</c>, que es la
/// liquidación del cruce del retiro con el modo de la caja—, la orden de giro y lo aplicado se volverían a separar (H4). El motor del desembolso
/// (<c>IMotorDeCreditoEnDesembolso.cs</c>) define esa liquidación del retiro y queda exento; el cruce del retiro, que vive en Captaciones, la sigue usando.
/// </summary>
public class LaRecogidaSeLiquidaEnUnSoloSitio
{
    /// <summary>Carpetas vigiladas (relativas a <c>src/</c>): el desembolso y la recogida.</summary>
    private static readonly string[] Carpetas =
    [
        "Core/IngenIA365ERP.Application/Lending/Disbursements",
        "Core/IngenIA365ERP.Application/Lending/Payoffs",
    ];

    /// <summary>El único sitio que liquida una recogida (relativo a la raíz, con barras normales).</summary>
    private const string ElSitio = "src/Core/IngenIA365ERP.Application/Lending/Payoffs/LiquidacionDeRecogida.cs";

    /// <summary>Define <c>MotorDeCreditoEnDesembolso.LiquidarAsync</c>, la liquidación del cruce del retiro con el modo de la caja: no es una recogida.</summary>
    private const string ElMotorDelRetiro = "src/Core/IngenIA365ERP.Application/Lending/Disbursements/IMotorDeCreditoEnDesembolso.cs";

    private static readonly (Regex Patron, string Que)[] Prohibidos =
    [
        (new Regex(@"\bAplicadorDePagos\s*\.\s*ValorParaCancelar\b", RegexOptions.Compiled),
            "calcula lo que cuesta cancelar (AplicadorDePagos.ValorParaCancelar) fuera de LiquidacionDeRecogida"),
        (new Regex(@"\.\s*ValorParaCancelarAsync\s*\(", RegexOptions.Compiled),
            "pide el valor para cancelar con el modo de la caja (ValorParaCancelarAsync) en vez de liquidar la recogida"),
        (new Regex(@"\.\s*ValorParaRecogerAsync\s*\(", RegexOptions.Compiled),
            "calcula el valor para recoger (ValorParaRecogerAsync) fuera de LiquidacionDeRecogida"),
        (new Regex(@"\.\s*LiquidarAsync\s*\(", RegexOptions.Compiled),
            "liquida con el motor del desembolso (MotorDeCreditoEnDesembolso.LiquidarAsync, la del cruce del retiro) en vez de LiquidacionDeRecogida"),
    ];

    [Fact]
    public void En_el_desembolso_y_la_recogida_solo_LiquidacionDeRecogida_liquida()
    {
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos,
            relativo => relativo is ElSitio or ElMotorDelRetiro);

        ReglaSobreFuente.SinInfracciones(infracciones, "La recogida se liquida en un solo sitio, LiquidacionDeRecogida (R3)");
    }

    /// <summary>La regla de arriba no pasa por vacía: el sitio existe, cobra lo que cobra la cancelación con el modo de la recogida, y el desembolso lo usa.</summary>
    [Fact]
    public void El_sitio_cobra_lo_mismo_que_la_cancelacion_y_el_desembolso_lo_usa()
    {
        var raiz = RepoPath.FindRepoRoot();
        var sitio = Path.Combine(raiz, ElSitio.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(sitio), $"No existe {ElSitio} (T010).");
        var fuente = FuenteSinComentarios.Leer(sitio);
        Assert.Contains("AplicadorDePagos.ValorParaCancelar", fuente, StringComparison.Ordinal);
        Assert.Contains("ModoAlRecogerAsync", fuente, StringComparison.Ordinal);

        var requisitos = FuenteSinComentarios.Leer(Path.Combine(raiz, "src", "Core", "IngenIA365ERP.Application", "Lending", "Disbursements",
            "RequisitosDelDesembolso.cs"));
        Assert.Contains("LiquidarCreditoAsync", requisitos, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\bIMotorDeCreditoEnDesembolso\b", requisitos);
    }

    /// <summary>
    /// T050: el contrato aplica la recogida con el mismo modo que la liquidó (<c>ModoAlRecogerAsync</c>), y sólo la del desembolso: el cruce del retiro y el
    /// débito automático, que también pagan con <c>OwnLoanPayoff</c>, siguen con el modo de la caja.
    /// </summary>
    [Fact]
    public void El_contrato_aplica_la_recogida_con_el_modo_de_la_recogida_solo_desde_el_desembolso()
    {
        var aplicacion = FuenteSinComentarios.Leer(Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application", "Lending", "Payments",
            "AplicacionDePagos.cs"));
        Assert.Contains("ModoAlRecogerAsync", aplicacion, StringComparison.Ordinal);
        Assert.Matches(@"recogida:\s*EsRecogidaDelDesembolso\(", aplicacion);
        Assert.Contains("InteresesAlCancelar.DocumentoDelDesembolso", aplicacion, StringComparison.Ordinal);
    }
}
