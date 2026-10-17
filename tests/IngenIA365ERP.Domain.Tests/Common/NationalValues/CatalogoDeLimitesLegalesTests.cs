using FluentAssertions;
using IngenIA365ERP.Domain.Common.NationalValues;

namespace IngenIA365ERP.Domain.Tests.Common.NationalValues;

/// <summary>
/// Feature 013, T0021 (T42, R8; data-model parte 3 §12.3, §12.5): el catálogo cerrado de límites legales (valor nacional
/// <c>LIMITE</c>) y el de códigos de valor nacional. El catálogo fija por código la unidad, la dirección de lo estricto
/// y qué acota; el <b>valor</b> nunca está en el código, vive en <c>ADM_NationalValues</c> con vigencia y fuente. La
/// cooperativa sólo puede ser más estricta: al leer rige <c>MasEstricto</c>.
/// </summary>
public class CatalogoDeLimitesLegalesTests
{
    public static TheoryData<string, UnidadDeValorNacional, DireccionDeLoEstricto, string> Fijados => new()
    {
        { "AVISO_PREVIO_DIAS", UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Mayor, "AvisoPrevio.DiasMinimos" },
        { "COBRANZA_HORA_FIN_LV", UnidadDeValorNacional.Horas, DireccionDeLoEstricto.Menor, "Cobranza.HorarioLunesAViernes" },
        { "LIBRANZA_CAPACIDAD_DESCUENTO", UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "Libranza.CapacidadDeDescuento" },
        { "PREPAGO_TOPE_SMMLV", UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Mayor, "" },
        { "CONCENTRACION_INDIVIDUAL", UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "Concentracion.LimiteIndividual" },
        { "DETERIORO_GENERAL_MINIMO", UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Mayor, "" },
        { "CONCENTRACION_INDIVIDUAL_CON_GARANTIA", UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "Concentracion.LimiteIndividualConGarantia" },
        { "COBRANZA_HORA_INICIO_LV", UnidadDeValorNacional.Horas, DireccionDeLoEstricto.Mayor, "Cobranza.HorarioLunesAViernes" },
        { "COBRANZA_HORA_INICIO_SAB", UnidadDeValorNacional.Horas, DireccionDeLoEstricto.Mayor, "Cobranza.HorarioSabado" },
        { "COBRANZA_HORA_FIN_SAB", UnidadDeValorNacional.Horas, DireccionDeLoEstricto.Menor, "Cobranza.HorarioSabado" },
        { "COBRANZA_CONTACTOS_DIA", UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Menor, "Cobranza.ContactosPorDia" },
        { "AVISO_PREVIO_UMBRAL_SMMLV", UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Mayor, "AvisoPrevio.UmbralDosComunicaciones" },
        { "MORA_SOBRE_INTERESES_PERMITIDA", UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Menor, "Mora.SobreIntereses" },
        { "LIBRANZA_VENTANA_GIRO_DIAS", UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "Libranza.VentanaDeGiroDias" },
        { "LIBRANZA_UMBRAL_MORA_PAGADURIA", UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "Libranza.UmbralDeMoraDePagaduria" },
        { "VINCULADOS_FRACCION_CALIFICADA", UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Mayor, "" },
        { "TITULAR_CONSULTA_DIAS", UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "" },
        { "TITULAR_CONSULTA_PRORROGA_DIAS", UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "" },
        { "TITULAR_RECLAMO_DIAS", UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "" },
        { "TITULAR_RECLAMO_PRORROGA_DIAS", UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "" },
        { "TITULAR_LEYENDA_RECLAMO_DIAS", UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "" },
        { "PERMANENCIA_NEGATIVO_VECES_MORA", UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Menor, "" },
        { "PERMANENCIA_NEGATIVO_MESES_MAXIMOS", UnidadDeValorNacional.Meses, DireccionDeLoEstricto.Menor, "" },
        // Feature 013, US17 bloque D (data-model §3.12, §12.5): el grado de parentesco de un vinculado.
        { "VINCULADOS_GRADO_PARENTESCO", UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Menor, "" },
        // K6b (T1051; data-model §12.5): los límites de vivienda de la Ley 546 de 1999.
        { "VIVIENDA_PLAZO_MINIMO_MESES", UnidadDeValorNacional.Meses, DireccionDeLoEstricto.Mayor, "" },
        { "VIVIENDA_PLAZO_MAXIMO_MESES", UnidadDeValorNacional.Meses, DireccionDeLoEstricto.Menor, "" },
        { "VIVIENDA_FINANCIABLE", UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "" },
        { "VIVIENDA_FINANCIABLE_VIS", UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "" },
        { "VIVIENDA_CUOTA_INGRESO", UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "" },
    };

    [Fact]
    public void Son_los_veintinueve_codigos_fijados_sin_repetir()
    {
        // K6b (T1051) suma los cinco de vivienda a los veinticuatro de K1–K6a.
        CatalogoDeLimitesLegales.Todos.Should().HaveCount(29);
        CatalogoDeLimitesLegales.Todos.Select(l => l.Codigo).Should().OnlyHaveUniqueItems();
        CatalogoDeLimitesLegales.Todos.Select(l => l.Codigo).Should().BeEquivalentTo(Fijados.Select(f => (string)f[0]));
    }

    [Theory]
    [MemberData(nameof(Fijados))]
    public void Cada_limite_lleva_su_unidad_su_direccion_y_lo_que_acota(string codigo, UnidadDeValorNacional unidad, DireccionDeLoEstricto estricto, string clave)
    {
        var limite = CatalogoDeLimitesLegales.Buscar(codigo)!;
        limite.Unidad.Should().Be(unidad);
        limite.Estricto.Should().Be(estricto);
        limite.Acota.Should().NotBeNullOrWhiteSpace();
        if (clave.Length > 0) limite.ClaveDeParametro.Should().Be(clave);
        limite.Fuente.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Un_codigo_desconocido_no_esta()
    {
        CatalogoDeLimitesLegales.Buscar("NO_EXISTE").Should().BeNull();
    }

    [Fact]
    public void MasEstricto_cuando_mayor_es_mas_estricto()
    {
        // AVISO_PREVIO_DIAS: más días de aviso es más estricto.
        CatalogoDeLimitesLegales.MasEstricto("AVISO_PREVIO_DIAS", valorCooperativa: 15m, limite: 20m).Should().Be(20m);
        CatalogoDeLimitesLegales.MasEstricto("AVISO_PREVIO_DIAS", valorCooperativa: 30m, limite: 20m).Should().Be(30m);
        CatalogoDeLimitesLegales.EsMenosEstricto("AVISO_PREVIO_DIAS", valorCooperativa: 15m, limite: 20m).Should().BeTrue();
        CatalogoDeLimitesLegales.EsMenosEstricto("AVISO_PREVIO_DIAS", valorCooperativa: 20m, limite: 20m).Should().BeFalse();
    }

    [Fact]
    public void MasEstricto_cuando_menor_es_mas_estricto()
    {
        // LIBRANZA_CAPACIDAD_DESCUENTO: descontar una fracción menor del salario es más estricto.
        CatalogoDeLimitesLegales.MasEstricto("LIBRANZA_CAPACIDAD_DESCUENTO", valorCooperativa: 0.6m, limite: 0.5m).Should().Be(0.5m);
        CatalogoDeLimitesLegales.MasEstricto("LIBRANZA_CAPACIDAD_DESCUENTO", valorCooperativa: 0.4m, limite: 0.5m).Should().Be(0.4m);
        CatalogoDeLimitesLegales.EsMenosEstricto("LIBRANZA_CAPACIDAD_DESCUENTO", valorCooperativa: 0.6m, limite: 0.5m).Should().BeTrue();
        CatalogoDeLimitesLegales.EsMenosEstricto("LIBRANZA_CAPACIDAD_DESCUENTO", valorCooperativa: 0.4m, limite: 0.5m).Should().BeFalse();
    }

    [Fact]
    public void MasEstricto_con_un_codigo_desconocido_se_rechaza()
    {
        var acto = () => CatalogoDeLimitesLegales.MasEstricto("NO_EXISTE", 1m, 2m);
        acto.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Un_LIMITE_con_unidad_distinta_de_la_del_catalogo_se_rechaza()
    {
        CodigosDeValorNacional.Validar("LIMITE", "AVISO_PREVIO_DIAS", UnidadDeValorNacional.Dias, 20m).Should().BeEmpty();

        CodigosDeValorNacional.Validar("LIMITE", "AVISO_PREVIO_DIAS", UnidadDeValorNacional.Horas, 20m)
            .Should().ContainSingle(r => r.Motivo == MotivoDeRechazoDeValorNacional.UnidadInvalida);
    }

    [Fact]
    public void Un_LIMITE_con_una_clave_que_no_esta_en_el_catalogo_se_rechaza()
    {
        CodigosDeValorNacional.Validar("LIMITE", "INVENTADO", UnidadDeValorNacional.Dias, 1m)
            .Should().ContainSingle(r => r.Motivo == MotivoDeRechazoDeValorNacional.ClaveInvalida);
    }

    // ---------- Códigos de valor nacional (§12.3) ----------

    [Fact]
    public void Son_los_nueve_codigos_de_valor_nacional()
    {
        CodigosDeValorNacional.Todos.Select(c => c.Codigo).Should().BeEquivalentTo(
            "USURA", "IBR", "IPC", "TOPE_TASA_VIVIENDA", "UVR", "SMMLV", "UVT", "FESTIVO", "LIMITE");
    }

    [Fact]
    public void Las_unidades_incluyen_veces()
    {
        Enum.GetNames<UnidadDeValorNacional>().Should().BeEquivalentTo("EA", "NominalMV", "Pesos", "Fraccion", "Dias", "Meses", "Horas", "Veces");
    }

    [Fact]
    public void UVR_y_tope_de_vivienda_no_estan_disponibles_en_K1_y_si_desde_K6b()
    {
        CodigosDeValorNacional.Buscar("UVR")!.DisponibleEnK1.Should().BeFalse();
        CodigosDeValorNacional.Buscar("TOPE_TASA_VIVIENDA")!.DisponibleEnK1.Should().BeFalse();
        CodigosDeValorNacional.Buscar("USURA")!.DisponibleEnK1.Should().BeTrue();
        // Antes de K6b (la guarda de entrega, explícita) se rechazan; con K6b vigente (T1050, T1058) se admiten.
        CodigosDeValorNacional.Validar("UVR", "", UnidadDeValorNacional.Pesos, 380m, IngenIA365ERP.Domain.Lending.EntregaDeCartera.K6a)
            .Should().ContainSingle(r => r.Motivo == MotivoDeRechazoDeValorNacional.CodigoNoDisponible);
        CodigosDeValorNacional.Validar("TOPE_TASA_VIVIENDA", "", UnidadDeValorNacional.EA, 0.12m, IngenIA365ERP.Domain.Lending.EntregaDeCartera.K6a)
            .Should().ContainSingle(r => r.Motivo == MotivoDeRechazoDeValorNacional.CodigoNoDisponible);
        CodigosDeValorNacional.Validar("UVR", "", UnidadDeValorNacional.Pesos, 380m).Should().BeEmpty();
        CodigosDeValorNacional.Validar("TOPE_TASA_VIVIENDA", "", UnidadDeValorNacional.EA, 0.12m).Should().BeEmpty();
        CodigosDeValorNacional.Validar("UVR", "CLAVE", UnidadDeValorNacional.Pesos, 380m)
            .Should().ContainSingle(r => r.Motivo == MotivoDeRechazoDeValorNacional.ClaveInvalida, "la UVR no lleva clave");
    }

    [Fact]
    public void Solo_IBR_e_IPC_declaran_antiguedad_maxima()
    {
        CodigosDeValorNacional.Todos.Where(c => c.AntiguedadMaximaDias is not null).Select(c => c.Codigo)
            .Should().BeEquivalentTo("IBR", "IPC");
        CodigosDeValorNacional.Todos.Where(c => c.AntiguedadMaximaDias is not null).Should().OnlyContain(c => c.AntiguedadMaximaDias > 0);
    }

    [Fact]
    public void Un_codigo_desconocido_se_rechaza()
    {
        CodigosDeValorNacional.Validar("DOLAR", "", UnidadDeValorNacional.Pesos, 1m)
            .Should().ContainSingle(r => r.Motivo == MotivoDeRechazoDeValorNacional.CodigoDesconocido);
    }

    [Fact]
    public void La_clave_depende_del_codigo()
    {
        CodigosDeValorNacional.Validar("SMMLV", "", UnidadDeValorNacional.Pesos, 1m).Should().BeEmpty();
        CodigosDeValorNacional.Validar("SMMLV", "2026", UnidadDeValorNacional.Pesos, 1m)
            .Should().ContainSingle(r => r.Motivo == MotivoDeRechazoDeValorNacional.ClaveInvalida, "SMMLV no lleva clave");
        CodigosDeValorNacional.Validar("IBR", "", UnidadDeValorNacional.NominalMV, 0.09m)
            .Should().ContainSingle(r => r.Motivo == MotivoDeRechazoDeValorNacional.ClaveInvalida, "el IBR lleva plazo");
        CodigosDeValorNacional.Validar("FESTIVO", "2026-11-02", null, null).Should().BeEmpty();
        CodigosDeValorNacional.Validar("FESTIVO", "2 de noviembre", null, null)
            .Should().ContainSingle(r => r.Motivo == MotivoDeRechazoDeValorNacional.ClaveInvalida, "la clave del festivo es la fecha ISO");
    }

    [Fact]
    public void La_unidad_y_el_valor_dependen_del_codigo()
    {
        CodigosDeValorNacional.Validar("USURA", "CONSUMO_Y_ORDINARIO", UnidadDeValorNacional.Pesos, 0.25m)
            .Should().ContainSingle(r => r.Motivo == MotivoDeRechazoDeValorNacional.UnidadInvalida);
        CodigosDeValorNacional.Validar("USURA", "CONSUMO_Y_ORDINARIO", UnidadDeValorNacional.EA, null)
            .Should().ContainSingle(r => r.Motivo == MotivoDeRechazoDeValorNacional.ValorInvalido);
        CodigosDeValorNacional.Validar("FESTIVO", "2026-11-02", UnidadDeValorNacional.Dias, 1m)
            .Should().HaveCount(2, "el festivo no lleva unidad ni valor");
    }
}
