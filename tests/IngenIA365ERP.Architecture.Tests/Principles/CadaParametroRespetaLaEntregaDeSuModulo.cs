using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T43): cada definición de parámetro declara desde qué entrega de su módulo está
/// disponible (<c>EntregaDeModulo</c>).
///
/// <para>
/// Llenada en la fase 2 (T0036): los cinco catálogos de <c>CatalogoDeParametros.Todas</c> nombran <c>DisponibleDesde</c>; toda clave de
/// <c>ParametrosDeCartera</c> declara una entrega de la línea <c>LND</c>; y con la entrega vigente de Cartera (K1) ninguna clave de una
/// entrega posterior se ofrece. Las del comercio siguen gobernadas por su línea (<c>INV</c>), sin cambio.
/// </para>
/// </summary>
public class CadaParametroRespetaLaEntregaDeSuModulo
{
    /// <summary>
    /// Catálogos de parámetros que declaran su entrega en el fuente (relativos a <c>src/</c>). Los tributarios y los de facturación
    /// electrónica toman el defecto de <c>DefinicionDeParametro.DisponibleDesde</c> (I1 del comercio): los cubre
    /// <see cref="Las_claves_del_comercio_siguen_en_su_linea"/> por reflexión.
    /// </summary>
    private static readonly string[] Archivos =
    [
        "Core/IngenIA365ERP.Domain/Inventory/Parameters/ParametrosDeInventario.cs",
        "Core/IngenIA365ERP.Domain/Lending/Parameters/ParametrosDeCartera.cs",
        "Core/IngenIA365ERP.Domain/Common/Parametros/DefinicionDeParametro.cs",
    ];

    private static readonly Regex Requerido = new(@"DisponibleDesde|EntregaDe(Cartera|lComercio)\.", RegexOptions.Compiled);

    [Fact]
    public void Cada_definicion_declara_su_entrega()
    {
        var faltantes = ReglaSobreFuente.Faltantes(ReglaSobreFuente.ArchivosNombrados(Archivos), Requerido);
        ReglaSobreFuente.SinInfracciones(faltantes, "Un catálogo de parámetros no declara la entrega de su módulo (T43)");
    }

    [Fact]
    public void Las_claves_de_Cartera_son_de_la_linea_de_Cartera()
    {
        Assert.NotEmpty(ParametrosDeCartera.Definiciones);
        var ajenas = ParametrosDeCartera.Definiciones
            .Where(d => d.Modulo != CatalogoDeCartera.Modulo || d.DisponibleDesde.Modulo != EntregaDeModulo.LineaDeCartera)
            .Select(d => $"{d.Modulo}.{d.Clave}: {d.DisponibleDesde.Modulo}/{d.DisponibleDesde.Nombre}")
            .ToList();
        Assert.True(ajenas.Count == 0, "Claves de Cartera sin entrega de la línea LND:\n  " + string.Join("\n  ", ajenas));
    }

    [Fact]
    public void Con_K3_vigente_no_se_ofrece_ninguna_clave_posterior()
    {
        // T0849: K2 habilita las claves Libranza.*; T0889 sube a K3 y T0910 le suma las Castigo.*; las de K4 en adelante siguen sin ofrecerse.
        Assert.Equal(EntregaDeCartera.K3, CatalogoDeCartera.EntregaVigente);
        var vigente = CatalogoDeParametros.EntregaVigenteDe(EntregaDeModulo.LineaDeCartera);

        var ofrecidas = ParametrosDeCartera.Definiciones.Where(d => d.DisponibleEnLaEntrega()).ToList();
        Assert.All(ofrecidas, d => Assert.True(d.DisponibleDesde <= vigente, $"{d.Clave} se ofrece antes de su entrega ({d.DisponibleDesde})."));

        var posteriores = ParametrosDeCartera.Definiciones.Where(d => d.DisponibleDesde > vigente).ToList();
        Assert.NotEmpty(posteriores);
        Assert.All(posteriores, d => Assert.False(d.DisponibleEnLaEntrega(), $"{d.Clave} es de {d.DisponibleDesde} y se ofrece con K3."));
        Assert.All(ParametrosDeCartera.Definiciones.Where(d => d.Clave.StartsWith("Libranza.", StringComparison.Ordinal)),
            d => Assert.True(d.DisponibleEnLaEntrega(), $"{d.Clave} es de K2 y no se ofrece."));
        var castigo = ParametrosDeCartera.Definiciones.Where(d => d.Clave.StartsWith("Castigo.", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, castigo.Count);
        Assert.All(castigo, d => Assert.True(d.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K3 && d.DisponibleEnLaEntrega() && d.PendienteDeValidar is not null,
            $"{d.Clave} es de K3, se ofrece y está pendiente de validar."));
    }

    [Fact]
    public void Las_claves_del_comercio_siguen_en_su_linea()
    {
        var delComercio = CatalogoDeParametros.Todas.Where(d => d.Modulo != CatalogoDeCartera.Modulo).ToList();
        Assert.NotEmpty(delComercio);
        Assert.All(delComercio, d => Assert.Equal(EntregaDeModulo.LineaDelComercio, d.DisponibleDesde.Modulo));
    }
}
