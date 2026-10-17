using FluentAssertions;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Application.Tests.Lending.Common;

/// <summary>
/// Feature 013, T0139 (T43, R17, CH36; data-model parte 3 §12.1): el catálogo <see cref="ParametrosDeCartera"/> es del módulo
/// <c>LND</c>, sus claves no llevan el prefijo «Cartera.», cada una tiene el defecto, los ámbitos y la entrega de §12.1, y con
/// <see cref="CatalogoDeCartera.EntregaVigente"/> en K1 no se ofrece ninguna clave de K2 en adelante.
/// </summary>
public class ParametrosDeCarteraTests
{
    private const ParameterScopeKind N = ParameterScopeKind.None;
    private const ParameterScopeKind L = ParameterScopeKind.CreditLine;
    private const ParameterScopeKind F = ParameterScopeKind.LendingFactType;

    /// <summary>Clave → (defecto, ámbitos, entrega), de data-model §12.1.</summary>
    public static TheoryData<string, string, ParameterScopeKind[], EntregaDeCartera> Claves => new()
    {
        { "Pagos.DiasMaximosFechaValor", "5", [N], EntregaDeCartera.K1 },
        { "Pagos.FirmezaDiasPorClaseDeMedio", "{\"*\": 10, \"InternalOffset\": 0}", [N], EntregaDeCartera.K1 },
        { "Mora.ToleranciaDeSaldoVencido", "0", [N, L], EntregaDeCartera.K1 },
        { "Mora.SobreIntereses", "false", [N], EntregaDeCartera.K1 },
        { "Causacion.ContinuaTrasFallecimiento", "true", [N, L], EntregaDeCartera.K1 },
        { "Riesgo.ReglaMesesFaltantes", "MayorObservada", [N], EntregaDeCartera.K1 },
        // K6a (T1027): los días fijos de la regla «Fija», sin valor.
        { "Riesgo.MesesFaltantesDiasFijos", "", [N], EntregaDeCartera.K6a },
        { "Riesgo.DiaLimiteCierre", "10", [N], EntregaDeCartera.K1 },
        { "Riesgo.CierreProgramado", "false", [N], EntregaDeCartera.K1 },
        { "Riesgo.RedondeoDeterioro", "Centavo", [N], EntregaDeCartera.K1 },
        { "Riesgo.CalificacionDiariaConArrastre", "true", [N], EntregaDeCartera.K1 },
        { "Riesgo.ExcepcionAportesArrastre", "false", [N], EntregaDeCartera.K1 },
        { "Concentracion.LimiteIndividual", "", [N], EntregaDeCartera.K1 },
        { "Concentracion.LimiteIndividualConGarantia", "", [N], EntregaDeCartera.K1 },
        { "Contabilidad.ModoDePaso", "EnLinea", [N, F], EntregaDeCartera.K1 },
        { "Contabilidad.Granularidad", "PorDia", [N, F], EntregaDeCartera.K1 },
        { "Contabilidad.DisparadorDeLote", "FinDeCausacion", [N, F], EntregaDeCartera.K1 },
        { "Contabilidad.HoraDeLote", "", [N, F], EntregaDeCartera.K1 },
        { "Contabilidad.PoliticaSinRespuesta", "ConfirmarConPendiente", [N], EntregaDeCartera.K1 },
        { "Contabilidad.FaltaDeRegla", "Bloquear", [N, F], EntregaDeCartera.K1 },
        { "Contabilidad.ValidacionPreviaSegundos", "3", [N], EntregaDeCartera.K1 },
        { "Contabilidad.DisminucionDeDeterioro", "MismoEjercicioPrimero", [N], EntregaDeCartera.K1 },
        { "Contabilidad.IndicadoresVigenciaMeses", "6", [N], EntregaDeCartera.K1 },
        { "Contabilidad.DiferirComisiones", "false", [N], EntregaDeCartera.K1 },
        { "Originacion.DiasVigenciaDeAprobacion", "30", [N, L], EntregaDeCartera.K1 },
        { "Reservas.DiasDeVigencia", "5", [N], EntregaDeCartera.K1 },
        { "Giros.DiasSinResultado", "3", [N], EntregaDeCartera.K1 },
        { "Cupo.MoraMaximaParaVender", "0", [N], EntregaDeCartera.K1 },
        { "Bloqueos.DiasDeMoraParaBloquear", "30", [N], EntregaDeCartera.K1 },
        { "Segregacion.Incompatibilidades", "[{\"a\": \"Decidir\", \"b\": \"Desembolsar\", \"activo\": true}, {\"a\": \"Analizar\", \"b\": \"Decidir\", \"activo\": false}, {\"a\": \"Desembolsar\", \"b\": \"AnularOrdenDeGiro\", \"activo\": false}, {\"a\": \"ArmarLoteDeCastigo\", \"b\": \"AplicarCastigo\", \"activo\": false}, {\"a\": \"CargarMigracion\", \"b\": \"AceptarDiferencias\", \"activo\": false}]", [N], EntregaDeCartera.K1 },
        { "Libranza.CapacidadDeDescuento", "", [N], EntregaDeCartera.K2 },
        { "Libranza.VentanaDeGiroDias", "3", [N], EntregaDeCartera.K2 },
        { "Libranza.UmbralDeMoraDePagaduria", "", [N], EntregaDeCartera.K2 },
        { "Castigo.ExigeDeterioroTotal", "true", [N], EntregaDeCartera.K3 },
        { "Castigo.DiasMinimosDeMora", "", [N], EntregaDeCartera.K3 },
        { "Castigo.GestionesMinimas", "0", [N], EntregaDeCartera.K3 },
        { "Cobranza.ToleranciaDePromesa", "0", [N], EntregaDeCartera.K4 },
        // K4, bloque D (decisión del dueño del 2026-10-16): el tope del gasto de cobranza, sin valor sembrado (vacío = sin tope).
        { "Cobranza.TopeGasto.Porcentaje", "", [N], EntregaDeCartera.K4 },
        { "Cobranza.TopeGasto.ValorMaximo", "", [N], EntregaDeCartera.K4 },
        { "Cobranza.HorarioLunesAViernes", "07:00-19:00", [N], EntregaDeCartera.K1 },
        { "Cobranza.HorarioSabado", "08:00-15:00", [N], EntregaDeCartera.K1 },
        { "Cobranza.ContactosPorDia", "1", [N], EntregaDeCartera.K1 },
        { "AvisoPrevio.DiasMinimos", "20", [N], EntregaDeCartera.K4 },
        { "AvisoPrevio.UmbralDosComunicaciones", "", [N], EntregaDeCartera.K4 },
        { "Mensajes.Reintentos", "3", [N], EntregaDeCartera.K1 },
        { "Canales.UmbralDeFallas", "5", [N], EntregaDeCartera.K1 },
        { "Centrales.DiasDeVigenciaDeConsulta", "30", [N, L], EntregaDeCartera.K1 },
        { "Listas.UmbralDeCoincidenciaDeNombre", "0.90", [N], EntregaDeCartera.K1 },
        { "Documentos.Firmante.Nombre", "", [N], EntregaDeCartera.K1 },
        { "Documentos.Firmante.Cargo", "", [N], EntregaDeCartera.K1 },
        { "Documentos.PazYSalvoIncluyeCodeudas", "false", [N], EntregaDeCartera.K1 },
        { "Informes.RangosDeEdad", "30,60,90,180,360", [N], EntregaDeCartera.K1 },
        { "Informes.PuntosAlertaUsura", "2.0", [N], EntregaDeCartera.K1 },
        { "Informes.DiasCarteraVencida", "30", [N], EntregaDeCartera.K5 },
        { "Documentos.CertificadoTributario.SaldoIncluye", "Capital,InteresCorriente,InteresDeMora,OtrosCargos,MenosAnticipo", [N], EntregaDeCartera.K5 },
        { "Centrales.CodigosDeSuscriptor", "", [N], EntregaDeCartera.K5 },
        // Decisión del dueño del 2026-10-16: los rangos de mora de las centrales y el aviso previo a los codeudores.
        { "Centrales.RangosDeMora", "*=30,60,90,120,150,180|1", [N], EntregaDeCartera.K5 },
        { "AvisoPrevio.IncluirCodeudores", "false", [N], EntregaDeCartera.K5 },
    };

    [Fact]
    public void El_modulo_es_LND_y_las_claves_no_llevan_el_prefijo_Cartera()
    {
        ParametrosDeCartera.Modulo.Should().Be("LND");
        ParametrosDeCartera.Definiciones.Should().OnlyContain(d => d.Modulo == "LND");
        ParametrosDeCartera.Definiciones.Should().NotContain(d => d.Clave.StartsWith("Cartera.", StringComparison.Ordinal), "CH36");
        ParametrosDeCartera.Definiciones.Select(d => d.Clave).Should().OnlyHaveUniqueItems()
            .And.BeEquivalentTo(Claves.Select(c => (string)c[0]));
    }

    [Theory]
    [MemberData(nameof(Claves))]
    public void Cada_clave_lleva_su_defecto_sus_ambitos_y_su_entrega(string clave, string defecto, ParameterScopeKind[] ambitos, EntregaDeCartera entrega)
    {
        var d = ParametrosDeCartera.Definiciones.Single(x => x.Clave == clave);

        d.DefectoSeguro.Should().Be(defecto);
        d.AmbitosAdmitidos.Should().BeEquivalentTo(ambitos);
        d.DisponibleDesde.Should().Be((EntregaDeModulo)entrega);
    }

    [Fact]
    public void Con_K6b_vigente_se_ofrecen_todas_las_claves_hasta_K6b()
    {
        // T0849: K2 habilita las claves Libranza.*; T0889 sube a K3 y T0910 le suma las Castigo.*; T0955 sube a K4 y T0969 a K5 (fase 19: el
        // extracto y el certificado tributario no traen claves nuevas); T1024 (fase 22) sube a K6a, con Riesgo.MesesFaltantesDiasFijos (T1027); T1058
        // (fase 23) sube a K6b, la última entrega, sin claves nuevas (los límites de vivienda son valores nacionales LIMITE, T1051).
        CatalogoDeCartera.EntregaVigente.Should().Be(EntregaDeCartera.K6b);
        var posteriores = ParametrosDeCartera.Definiciones.Where(d => d.DisponibleDesde > (EntregaDeModulo)EntregaDeCartera.K6b).ToList();

        posteriores.Should().BeEmpty("K6b es la última entrega de Cartera");
        ParametrosDeCartera.Definiciones.Where(d => d.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K6b).Should().BeEmpty("la vivienda no trae claves");
        ParametrosDeCartera.Definiciones.Where(d => d.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K6a).Select(d => d.Clave).Should()
            .Equal(ParametrosDeCartera.RiesgoMesesFaltantesDiasFijos);
        ParametrosDeCartera.Definiciones.Single(d => d.Clave == ParametrosDeCartera.RiesgoMesesFaltantesDiasFijos).Should()
            .Match<IngenIA365ERP.Domain.Common.Parametros.DefinicionDeParametro>(d => d.DisponibleEnLaEntrega() && d.AdmiteVacio && d.PendienteDeValidar != null);
        // T0980 (fase 20): Informes.DiasCarteraVencida, pendiente de validar por la contadora; 2026-10-16: el saldo del certificado tributario y
        // (T1003, fase 21) los códigos de suscriptor de las centrales.
        ParametrosDeCartera.Definiciones.Where(d => d.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K5).Select(d => d.Clave).Should()
            .Equal(ParametrosDeCartera.InformesDiasCarteraVencida, ParametrosDeCartera.DocumentosCertificadoTributarioSaldoIncluye,
                ParametrosDeCartera.CentralesCodigosDeSuscriptor, ParametrosDeCartera.CentralesRangosDeMora, ParametrosDeCartera.AvisoPrevioIncluirCodeudores);
        ParametrosDeCartera.Definiciones.Single(d => d.Clave == ParametrosDeCartera.CentralesRangosDeMora).PendienteDeValidar.Should().NotBeNull();
        ParametrosDeCartera.Definiciones.Single(d => d.Clave == ParametrosDeCartera.InformesDiasCarteraVencida).Should()
            .Match<IngenIA365ERP.Domain.Common.Parametros.DefinicionDeParametro>(d => d.DisponibleEnLaEntrega() && d.PendienteDeValidar != null);
        ParametrosDeCartera.Definiciones.Where(d => d.Clave.StartsWith("Libranza.", StringComparison.Ordinal)).Should().OnlyContain(d => d.DisponibleEnLaEntrega());
        ParametrosDeCartera.Definiciones.Where(d => d.Clave.StartsWith("Castigo.", StringComparison.Ordinal)).Should().HaveCount(3)
            .And.OnlyContain(d => d.DisponibleEnLaEntrega() && d.DisponibleDesde == (EntregaDeModulo)EntregaDeCartera.K3 && d.PendienteDeValidar != null);
        posteriores.Should().OnlyContain(d => !d.DisponibleEnLaEntrega());
        ParametrosDeCartera.Definiciones.Except(posteriores).Should().OnlyContain(d => d.DisponibleEnLaEntrega());
    }

    [Fact]
    public void El_catalogo_de_parametros_de_la_plataforma_incluye_el_de_Cartera()
    {
        CatalogoDeParametros.Todas.Where(d => d.Modulo == "LND").Should().HaveCount(ParametrosDeCartera.Definiciones.Count);
    }
}
