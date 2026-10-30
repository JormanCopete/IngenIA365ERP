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
        // Feature 015 (T0042): las claves RCD y TES.
        "Core/IngenIA365ERP.Domain/Receipts/Parameters/ParametrosDeRecaudoYTesoreria.cs",
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
    public void Con_K11_vigente_se_ofrecen_las_claves_hasta_K11_y_ninguna_posterior()
    {
        // T0849: K2 habilita las claves Libranza.*; T0889 sube a K3 y T0910 le suma las Castigo.*; T0955 sube a K4 (US12): AvisoPrevio.* y
        // Cobranza.ToleranciaDePromesa se ofrecen; T0969 sube a K5 (fase 19, sin claves nuevas). Hoy ninguna clave es de K6a en adelante: si una llega
        // antes de su entrega, esta prueba la frena. T1024 (fase 22) sube a K6a: Riesgo.MesesFaltantesDiasFijos (T1027) se ofrece. T1058 (fase 23) sube
        // a K6b, la última entrega: la vivienda no trae claves nuevas (sus límites son valores nacionales LIMITE, T1051).
        // La 014 (T225) sube de una vez a K11, la última entrega: las claves de captaciones (K7 a K11) se ofrecen.
        Assert.Equal(EntregaDeCartera.K11, CatalogoDeCartera.EntregaVigente);
        Assert.Empty(ParametrosDeCartera.Definiciones.Where(d => d.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K6b));
        var vigente = CatalogoDeParametros.EntregaVigenteDe(EntregaDeModulo.LineaDeCartera);

        var ofrecidas = ParametrosDeCartera.Definiciones.Where(d => d.DisponibleEnLaEntrega()).ToList();
        Assert.All(ofrecidas, d => Assert.True(d.DisponibleDesde <= vigente, $"{d.Clave} se ofrece antes de su entrega ({d.DisponibleDesde})."));

        var posteriores = ParametrosDeCartera.Definiciones.Where(d => d.DisponibleDesde > vigente).ToList();
        Assert.All(posteriores, d => Assert.False(d.DisponibleEnLaEntrega(), $"{d.Clave} es de {d.DisponibleDesde} y se ofrece antes de su entrega."));
        var deK6a = ParametrosDeCartera.Definiciones.Where(d => d.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K6a).ToList();
        Assert.Equal([ParametrosDeCartera.RiesgoMesesFaltantesDiasFijos], deK6a.Select(d => d.Clave));
        Assert.All(deK6a, d => Assert.True(d.DisponibleEnLaEntrega() && d.PendienteDeValidar is not null, $"{d.Clave} es de K6a, se ofrece y está pendiente de validar."));
        Assert.True(ParametrosDeCartera.Definiciones.Single(d => d.Clave == ParametrosDeCartera.RiesgoReglaMesesFaltantes).DisponibleDesde
            < (EntregaDeModulo)EntregaDeCartera.K6a, "la regla de meses faltantes existe desde K1 (se usa desde K6a)");
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
        // 2026-10-16 (decisión del dueño): el saldo del certificado tributario también es de K5 y se ofrece.
        var saldo = ParametrosDeCartera.Definiciones.Single(d => d.Clave == ParametrosDeCartera.DocumentosCertificadoTributarioSaldoIncluye);
        Assert.True(saldo.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K5 && saldo.DisponibleEnLaEntrega(),
            "Documentos.CertificadoTributario.SaldoIncluye es de K5 y se ofrece.");
        // T1003 (fase 21): los códigos de suscriptor de las centrales son de K5 y se ofrecen.
        var suscriptor = ParametrosDeCartera.Definiciones.Single(d => d.Clave == ParametrosDeCartera.CentralesCodigosDeSuscriptor);
        Assert.True(suscriptor.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K5 && suscriptor.DisponibleEnLaEntrega(),
            "Centrales.CodigosDeSuscriptor es de K5 y se ofrece.");
        // Decisión del dueño del 2026-10-16: los rangos de mora de las centrales y el aviso previo a los codeudores son de K5 y se ofrecen.
        foreach (var clave in new[] { ParametrosDeCartera.CentralesRangosDeMora, ParametrosDeCartera.AvisoPrevioIncluirCodeudores })
        {
            var d = ParametrosDeCartera.Definiciones.Single(x => x.Clave == clave);
            Assert.True(d.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K5 && d.DisponibleEnLaEntrega(), $"{clave} es de K5 y se ofrece.");
        }
    }

    [Fact]
    public void Las_claves_del_comercio_siguen_en_su_linea()
    {
        // Feature 015 (T0042): las de Recaudo y Tesorería tienen su propia línea (Las_claves_de_Recaudo_y_Tesoreria_siguen_en_su_linea).
        var delComercio = CatalogoDeParametros.Todas.Where(d => d.Modulo != CatalogoDeCartera.Modulo && d.Modulo is not ("RCD" or "TES")).ToList();
        Assert.NotEmpty(delComercio);
        Assert.All(delComercio, d => Assert.Equal(EntregaDeModulo.LineaDelComercio, d.DisponibleDesde.Modulo));
    }

    /// <summary>Feature 015 (T0042): cada clave RCD declara una entrega de Recaudo y cada clave TES una de Tesorería.</summary>
    [Fact]
    public void Las_claves_de_Recaudo_y_Tesoreria_siguen_en_su_linea()
    {
        var deRecaudo = CatalogoDeParametros.Todas.Where(d => d.Modulo == "RCD").ToList();
        var deTesoreria = CatalogoDeParametros.Todas.Where(d => d.Modulo == "TES").ToList();
        Assert.NotEmpty(deRecaudo);
        Assert.NotEmpty(deTesoreria);
        Assert.All(deRecaudo, d => Assert.Equal(EntregaDeModulo.LineaDeRecaudo, d.DisponibleDesde.Modulo));
        Assert.All(deTesoreria, d => Assert.Equal(EntregaDeModulo.LineaDeTesoreria, d.DisponibleDesde.Modulo));
    }
}
