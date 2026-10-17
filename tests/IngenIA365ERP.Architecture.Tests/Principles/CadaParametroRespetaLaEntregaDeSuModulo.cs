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
    public void Con_K5_vigente_se_ofrecen_las_claves_hasta_K5_y_ninguna_posterior()
    {
        // T0849: K2 habilita las claves Libranza.*; T0889 sube a K3 y T0910 le suma las Castigo.*; T0955 sube a K4 (US12): AvisoPrevio.* y
        // Cobranza.ToleranciaDePromesa se ofrecen; T0969 sube a K5 (fase 19, sin claves nuevas). Hoy ninguna clave es de K6a en adelante: si una llega
        // antes de su entrega, esta prueba la frena.
        Assert.Equal(EntregaDeCartera.K5, CatalogoDeCartera.EntregaVigente);
        var vigente = CatalogoDeParametros.EntregaVigenteDe(EntregaDeModulo.LineaDeCartera);

        var ofrecidas = ParametrosDeCartera.Definiciones.Where(d => d.DisponibleEnLaEntrega()).ToList();
        Assert.All(ofrecidas, d => Assert.True(d.DisponibleDesde <= vigente, $"{d.Clave} se ofrece antes de su entrega ({d.DisponibleDesde})."));

        var posteriores = ParametrosDeCartera.Definiciones.Where(d => d.DisponibleDesde > vigente).ToList();
        Assert.All(posteriores, d => Assert.False(d.DisponibleEnLaEntrega(), $"{d.Clave} es de {d.DisponibleDesde} y se ofrece con K5."));
        Assert.All(ParametrosDeCartera.Definiciones.Where(d => d.Clave.StartsWith("Libranza.", StringComparison.Ordinal)),
            d => Assert.True(d.DisponibleEnLaEntrega(), $"{d.Clave} es de K2 y no se ofrece."));
        var castigo = ParametrosDeCartera.Definiciones.Where(d => d.Clave.StartsWith("Castigo.", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, castigo.Count);
        Assert.All(castigo, d => Assert.True(d.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K3 && d.DisponibleEnLaEntrega() && d.PendienteDeValidar is not null,
            $"{d.Clave} es de K3, se ofrece y está pendiente de validar."));
        var cobranza = ParametrosDeCartera.Definiciones.Where(d => d.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K4).Select(d => d.Clave).ToList();
        Assert.Contains(ParametrosDeCartera.CobranzaToleranciaDePromesa, cobranza);
        Assert.Contains(ParametrosDeCartera.AvisoPrevioDiasMinimos, cobranza);
        Assert.Contains(ParametrosDeCartera.AvisoPrevioUmbralDosComunicaciones, cobranza);
        Assert.All(ParametrosDeCartera.Definiciones.Where(d => d.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K4),
            d => Assert.True(d.DisponibleEnLaEntrega(), $"{d.Clave} es de K4 y no se ofrece."));
        Assert.NotNull(ParametrosDeCartera.Definiciones.Single(d => d.Clave == ParametrosDeCartera.CobranzaToleranciaDePromesa).PendienteDeValidar);
        // T0980 (fase 20): Informes.DiasCarteraVencida es la clave de K5 y se ofrece.
        var deK5 = ParametrosDeCartera.Definiciones.Single(d => d.Clave == ParametrosDeCartera.InformesDiasCarteraVencida);
        Assert.True(deK5.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K5 && deK5.DisponibleEnLaEntrega() && deK5.PendienteDeValidar is not null,
            "Informes.DiasCarteraVencida es de K5, se ofrece y está pendiente de validar.");
    }

    [Fact]
    public void Las_claves_del_comercio_siguen_en_su_linea()
    {
        var delComercio = CatalogoDeParametros.Todas.Where(d => d.Modulo != CatalogoDeCartera.Modulo).ToList();
        Assert.NotEmpty(delComercio);
        Assert.All(delComercio, d => Assert.Equal(EntregaDeModulo.LineaDelComercio, d.DisponibleDesde.Modulo));
    }
}
