namespace IngenIA365ERP.Domain.Common.NationalValues;

/// <summary>Qué lado es el estricto de un límite legal: un valor mayor (<see cref="Mayor"/>) o uno menor (<see cref="Menor"/>).</summary>
public enum DireccionDeLoEstricto
{
    /// <summary>Un valor mayor es más estricto (p. ej. más días de aviso previo).</summary>
    Mayor = 1,

    /// <summary>Un valor menor es más estricto (p. ej. una fracción menor del salario descontable).</summary>
    Menor = 2,
}

/// <summary>
/// Un límite legal del catálogo: su código (la clave del valor nacional <c>LIMITE</c>), la unidad, la dirección de lo
/// estricto, qué acota (<see cref="Acota"/>; <see cref="ClaveDeParametro"/> cuando es un parámetro de la cooperativa) y la
/// norma de donde sale. El <b>valor</b> no está aquí: vive en <c>ADM_NationalValues</c> con vigencia y fuente.
/// </summary>
public sealed record LimiteLegal(
    string Codigo,
    UnidadDeValorNacional Unidad,
    DireccionDeLoEstricto Estricto,
    string Acota,
    string? ClaveDeParametro,
    string Fuente);

/// <summary>
/// Catálogo cerrado de los límites legales (feature 013, T42, R8, C71; data-model parte 3 §12.5). Fija por código la
/// unidad, la dirección de lo estricto y qué acota; los valores (y su vigencia y fuente) son valores nacionales
/// <c>LIMITE</c>. La cooperativa sólo puede ser más estricta: un parámetro menos estricto se rechaza
/// (<see cref="EsMenosEstricto"/>, <c>Lending.Parameter.BelowLegalFloor</c>) y al leer rige <see cref="MasEstricto"/>.
/// Un límite nuevo es una entrada nueva aquí y en data-model §12.5.
/// </summary>
public static class CatalogoDeLimitesLegales
{
    public const string AvisoPrevioDias = "AVISO_PREVIO_DIAS";
    public const string CobranzaHoraFinLunesAViernes = "COBRANZA_HORA_FIN_LV";
    public const string LibranzaCapacidadDescuento = "LIBRANZA_CAPACIDAD_DESCUENTO";
    public const string PrepagoTopeSmmlv = "PREPAGO_TOPE_SMMLV";
    public const string ConcentracionIndividual = "CONCENTRACION_INDIVIDUAL";
    public const string DeterioroGeneralMinimo = "DETERIORO_GENERAL_MINIMO";
    public const string ConcentracionIndividualConGarantia = "CONCENTRACION_INDIVIDUAL_CON_GARANTIA";
    public const string CobranzaHoraInicioLunesAViernes = "COBRANZA_HORA_INICIO_LV";
    public const string CobranzaHoraInicioSabado = "COBRANZA_HORA_INICIO_SAB";
    public const string CobranzaHoraFinSabado = "COBRANZA_HORA_FIN_SAB";
    public const string CobranzaContactosDia = "COBRANZA_CONTACTOS_DIA";
    public const string AvisoPrevioUmbralSmmlv = "AVISO_PREVIO_UMBRAL_SMMLV";
    public const string MoraSobreInteresesPermitida = "MORA_SOBRE_INTERESES_PERMITIDA";
    public const string LibranzaVentanaGiroDias = "LIBRANZA_VENTANA_GIRO_DIAS";
    public const string LibranzaUmbralMoraPagaduria = "LIBRANZA_UMBRAL_MORA_PAGADURIA";
    public const string VinculadosFraccionCalificada = "VINCULADOS_FRACCION_CALIFICADA";
    public const string TitularConsultaDias = "TITULAR_CONSULTA_DIAS";
    public const string TitularConsultaProrrogaDias = "TITULAR_CONSULTA_PRORROGA_DIAS";
    public const string TitularReclamoDias = "TITULAR_RECLAMO_DIAS";
    public const string TitularReclamoProrrogaDias = "TITULAR_RECLAMO_PRORROGA_DIAS";
    public const string TitularLeyendaReclamoDias = "TITULAR_LEYENDA_RECLAMO_DIAS";
    public const string PermanenciaNegativoVecesMora = "PERMANENCIA_NEGATIVO_VECES_MORA";
    public const string PermanenciaNegativoMesesMaximos = "PERMANENCIA_NEGATIVO_MESES_MAXIMOS";

    // Feature 014 (T017; research C02; data-model §9.1): los pisos y techos legales de los aportes. El valor (y su fuente) es un valor nacional
    // LIMITE; el parámetro de la cooperativa sólo puede ser más estricto.

    /// <summary>Techo de la cuota obligatoria de los fondos de empleados, como fracción del ingreso.</summary>
    public const string AportesTopeCuotaFondoEmpleados = "APORTES_TOPE_CUOTA_FONDO_EMPLEADOS";

    /// <summary>Piso de la fracción de la cuota periódica que va a aportes sociales.</summary>
    public const string AportesFraccionMinima = "APORTES_FRACCION_MINIMA";

    /// <summary>Techo de los aportes de una persona natural frente al total de aportes.</summary>
    public const string AportesLimiteParticipacionPersonaNatural = "APORTES_LIMITE_PARTICIPACION_PN";

    /// <summary>Techo de los aportes de una persona jurídica frente al total de aportes.</summary>
    public const string AportesLimiteParticipacionPersonaJuridica = "APORTES_LIMITE_PARTICIPACION_PJ";

    /// <summary>
    /// El grado máximo de parentesco (consanguinidad o afinidad) con el que un pariente es persona vinculada (feature 013, US17, bloque D;
    /// Ley 454 de 1998, art. 61; data-model §3.12, §12.5): un vínculo <c>Relative</c> con un grado mayor no se registra. No acota un
    /// parámetro de la cooperativa. (nuevo)
    /// </summary>
    public const string VinculadosGradoParentesco = "VINCULADOS_GRADO_PARENTESCO";

    // K6b (T1051; FR-024, D18; data-model §12.5): los límites de la Ley 546 de 1999 para los créditos de vivienda. Los valores semilla van «pendientes
    // de validar» con su fuente en la semilla central; la versión de línea y la solicitud de vivienda los cumplen (ReglasDeVivienda). (nuevo)

    /// <summary>Plazo mínimo de un crédito de vivienda, en meses (Ley 546 de 1999, art. 17).</summary>
    public const string ViviendaPlazoMinimoMeses = "VIVIENDA_PLAZO_MINIMO_MESES";

    /// <summary>Plazo máximo de un crédito de vivienda, en meses (Ley 546 de 1999, art. 17).</summary>
    public const string ViviendaPlazoMaximoMeses = "VIVIENDA_PLAZO_MAXIMO_MESES";

    /// <summary>Fracción máxima del valor del inmueble que se financia en vivienda que no es de interés social.</summary>
    public const string ViviendaFinanciable = "VIVIENDA_FINANCIABLE";

    /// <summary>Fracción máxima del valor del inmueble que se financia en vivienda de interés social (VIS).</summary>
    public const string ViviendaFinanciableVis = "VIVIENDA_FINANCIABLE_VIS";

    /// <summary>Fracción máxima de los ingresos familiares que puede representar la primera cuota (relación cuota-ingreso).</summary>
    public const string ViviendaCuotaIngreso = "VIVIENDA_CUOTA_INGRESO";

    /// <summary>
    /// Feature 014, US11 (T188): veces el SMMLV que protege el límite inembargable en el cobro coactivo de la DIAN y demás entidades públicas, sobre la
    /// cuenta de ahorros más antigua de la persona natural (ET art. 837-1, modificado por la Ley 1066 de 2006, art. 9).
    /// </summary>
    public const string EmbargoCoactivoSmmlv = "EMBARGO_COACTIVO_SMMLV";

    // Feature 014, US15 (T216; research §24): los pisos del fondo de liquidez y el IRL mínimo. La cooperativa puede exigir más, nunca menos.

    /// <summary>Porcentaje mínimo del fondo de liquidez sobre los depósitos (Decreto 1068 de 2015 art. 2.11.7.2.1.1; Decreto 976 de 2026).</summary>
    public const string FondoLiquidezDepositos = "FONDO_LIQUIDEZ_DEPOSITOS";

    /// <summary>Porcentaje mínimo sobre el ahorro permanente que sólo se retira al desvincularse (régimen general).</summary>
    public const string FondoLiquidezPermanenteSoloAlRetiro = "FONDO_LIQUIDEZ_PERMANENTE_SOLO_RETIRO";

    /// <summary>Porcentaje mínimo de la banda media (31 a 540 días) en la categoría plena (Decreto 976 de 2026, art. 2.11.7.2.2.3).</summary>
    public const string FondoLiquidezPlenaBandaMedia = "FONDO_LIQUIDEZ_PLENA_BANDA_MEDIA";

    /// <summary>Porcentaje mínimo sobre el ahorro permanente que sólo se retira al desvincularse en la categoría plena.</summary>
    public const string FondoLiquidezPlenaPermanenteSoloAlRetiro = "FONDO_LIQUIDEZ_PLENA_PERMANENTE_SOLO_RETIRO";

    /// <summary>IRL acumulado mínimo (veces) de la Circular Básica Contable y Financiera, Título IV, Capítulo III, Anexo 1.</summary>
    public const string IrlMinimo = "IRL_MINIMO";

    /// <summary>
    /// Feature 014, US17 (T220): veces la UVT del año desde las que una persona entra al formato 1019 de la exógena (movimientos crédito mensuales o saldo del
    /// período), Resolución DIAN 000162 de 2023, art. 3. La entidad puede reportar desde menos, nunca desde más.
    /// </summary>
    public const string Exogena1019UmbralUvt = "EXOGENA_1019_UMBRAL_UVT";

    private static readonly IReadOnlyList<LimiteLegal> Lista =
    [
        new(AvisoPrevioDias, UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Mayor, "AvisoPrevio.DiasMinimos", "AvisoPrevio.DiasMinimos", "Ley 1266 de 2008; Ley 2157 de 2021"),
        new(CobranzaHoraFinLunesAViernes, UnidadDeValorNacional.Horas, DireccionDeLoEstricto.Menor, "Cobranza.HorarioLunesAViernes", "Cobranza.HorarioLunesAViernes", "Ley 2300 de 2023"),
        new(LibranzaCapacidadDescuento, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "Libranza.CapacidadDeDescuento", "Libranza.CapacidadDeDescuento", "Ley 1527 de 2012"),
        new(PrepagoTopeSmmlv, UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Mayor, "Prepago sin penalidad de la versión de la línea de crédito", null, "Ley 1555 de 2012"),
        new(ConcentracionIndividual, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "Concentracion.LimiteIndividual", "Concentracion.LimiteIndividual", "Decreto 961 de 2018"),
        new(DeterioroGeneralMinimo, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Mayor, "Deterioro general de la política SARC (LND_RiskPolicyVersions)", null, "Circular Básica Contable y Financiera, Anexo 1"),
        new(ConcentracionIndividualConGarantia, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "Concentracion.LimiteIndividualConGarantia", "Concentracion.LimiteIndividualConGarantia", "Decreto 961 de 2018"),
        new(CobranzaHoraInicioLunesAViernes, UnidadDeValorNacional.Horas, DireccionDeLoEstricto.Mayor, "Cobranza.HorarioLunesAViernes", "Cobranza.HorarioLunesAViernes", "Ley 2300 de 2023"),
        new(CobranzaHoraInicioSabado, UnidadDeValorNacional.Horas, DireccionDeLoEstricto.Mayor, "Cobranza.HorarioSabado", "Cobranza.HorarioSabado", "Ley 2300 de 2023"),
        new(CobranzaHoraFinSabado, UnidadDeValorNacional.Horas, DireccionDeLoEstricto.Menor, "Cobranza.HorarioSabado", "Cobranza.HorarioSabado", "Ley 2300 de 2023"),
        new(CobranzaContactosDia, UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Menor, "Cobranza.ContactosPorDia", "Cobranza.ContactosPorDia", "Ley 2300 de 2023"),
        new(AvisoPrevioUmbralSmmlv, UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Mayor, "AvisoPrevio.UmbralDosComunicaciones", "AvisoPrevio.UmbralDosComunicaciones", "Ley 2157 de 2021"),
        new(MoraSobreInteresesPermitida, UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Menor, "Mora.SobreIntereses (nunca en vivienda)", "Mora.SobreIntereses", "Código de Comercio"),
        new(LibranzaVentanaGiroDias, UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "Libranza.VentanaDeGiroDias", "Libranza.VentanaDeGiroDias", "Ley 1527 de 2012, art. 6"),
        new(LibranzaUmbralMoraPagaduria, UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "Libranza.UmbralDeMoraDePagaduria", "Libranza.UmbralDeMoraDePagaduria", "Circular Básica Contable y Financiera"),
        new(VinculadosFraccionCalificada, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Mayor, "Mayoría calificada de cada órgano para vinculados (LND_GoverningBodies)", null, "Ley 454 de 1998, art. 61"),
        new(TitularConsultaDias, UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "PlazosDelTitular (consulta)", null, "Ley 1581 de 2012; Ley 1266 de 2008"),
        new(TitularConsultaProrrogaDias, UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "PlazosDelTitular (prórroga de la consulta)", null, "Ley 1581 de 2012; Ley 1266 de 2008"),
        new(TitularReclamoDias, UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "PlazosDelTitular (reclamo); LND_CreditReportClaims", null, "Ley 1581 de 2012; Ley 1266 de 2008"),
        new(TitularReclamoProrrogaDias, UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "PlazosDelTitular (prórroga del reclamo); LND_CreditReportClaims", null, "Ley 1581 de 2012; Ley 1266 de 2008"),
        new(TitularLeyendaReclamoDias, UnidadDeValorNacional.Dias, DireccionDeLoEstricto.Menor, "Marca «reclamo en trámite»", null, "Ley 1266 de 2008"),
        new(PermanenciaNegativoVecesMora, UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Menor, "ProyeccionCentralDeRiesgo; ReglaDeRevocacion", null, "Ley 2157 de 2021"),
        new(PermanenciaNegativoMesesMaximos, UnidadDeValorNacional.Meses, DireccionDeLoEstricto.Menor, "ProyeccionCentralDeRiesgo; ReglaDeRevocacion", null, "Ley 2157 de 2021"),
        new(VinculadosGradoParentesco, UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Menor, "Grado de parentesco de un vinculado pariente (LND_RelatedParties)", null, "Ley 454 de 1998, art. 61"),
        new(ViviendaPlazoMinimoMeses, UnidadDeValorNacional.Meses, DireccionDeLoEstricto.Mayor, "Plazo mínimo de la versión de línea y de la solicitud de vivienda (ReglasDeVivienda)", null, "Ley 546 de 1999, art. 17"),
        new(ViviendaPlazoMaximoMeses, UnidadDeValorNacional.Meses, DireccionDeLoEstricto.Menor, "Plazo máximo de la versión de línea y de la solicitud de vivienda (ReglasDeVivienda)", null, "Ley 546 de 1999, art. 17"),
        new(ViviendaFinanciable, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "Monto de la solicitud de vivienda frente al valor del inmueble (ReglasDeVivienda)", null, "Ley 546 de 1999, art. 17; Decreto 145 de 2000 (compilado en el Decreto 2555 de 2010)"),
        new(ViviendaFinanciableVis, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "Monto de la solicitud de vivienda de interés social frente al valor del inmueble (ReglasDeVivienda)", null, "Ley 546 de 1999, art. 17; Decreto 145 de 2000 (compilado en el Decreto 2555 de 2010)"),
        new(ViviendaCuotaIngreso, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "Primera cuota de la solicitud de vivienda frente a los ingresos familiares (ReglasDeVivienda)", null, "Ley 546 de 1999, art. 17; Decreto 145 de 2000 (compilado en el Decreto 2555 de 2010)"),
        // Feature 014 (T017): los de aportes (data-model §9.1).
        new(AportesTopeCuotaFondoEmpleados, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "Aportes.TopeCuotaFondoEmpleados", "Aportes.TopeCuotaFondoEmpleados", "Ley 2496 de 2025 (fondos de empleados; investigación financiera de la feature 014 §3.5)"),
        new(AportesFraccionMinima, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Mayor, "Aportes.FraccionMinima", "Aportes.FraccionMinima", "Ley 2496 de 2025 (fondos de empleados; investigación financiera de la feature 014 §3.5)"),
        new(AportesLimiteParticipacionPersonaNatural, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "Aportes.LimiteParticipacionPersonaNatural", "Aportes.LimiteParticipacionPersonaNatural", "Ley 79 de 1988 (a validar, pregunta P12)"),
        // Feature 014, US11 (T188): el límite inembargable del cobro coactivo (lo usa LimiteInembargable; sin parámetro de la cooperativa).
        new(EmbargoCoactivoSmmlv, UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Mayor, "Límite inembargable del embargo coactivo (RetencionDeEmbargos)", null,
            "Estatuto Tributario art. 837-1 (Ley 1066 de 2006, art. 9)"),
        new(AportesLimiteParticipacionPersonaJuridica, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Menor, "Aportes.LimiteParticipacionPersonaJuridica", "Aportes.LimiteParticipacionPersonaJuridica", "Ley 79 de 1988 (a validar, pregunta P12)"),
        // Feature 014, US15 (T216): los pisos del fondo de liquidez (los lee FondoDeLiquidezDeCartera sobre la tabla JSON del parámetro) y el IRL mínimo.
        new(FondoLiquidezDepositos, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Mayor, "FondoDeLiquidez.PorcentajesPorCategoria (depósitos y primera banda)", null,
            "Decreto 1068 de 2015 art. 2.11.7.2.1.1 y 2.11.7.2.2.3 (Decreto 976 de 2026)"),
        new(FondoLiquidezPermanenteSoloAlRetiro, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Mayor,
            "FondoDeLiquidez.PorcentajesPorCategoria (permanente sólo al retiro, régimen general)", null, "Decreto 1068 de 2015 art. 2.11.7.2.1.1, parágrafo (Decreto 976 de 2026)"),
        new(FondoLiquidezPlenaBandaMedia, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Mayor, "FondoDeLiquidez.PorcentajesPorCategoria (banda media, categoría plena)",
            null, "Decreto 1068 de 2015 art. 2.11.7.2.2.3 (Decreto 976 de 2026)"),
        new(FondoLiquidezPlenaPermanenteSoloAlRetiro, UnidadDeValorNacional.Fraccion, DireccionDeLoEstricto.Mayor,
            "FondoDeLiquidez.PorcentajesPorCategoria (permanente sólo al retiro, categoría plena)", null, "Decreto 1068 de 2015 art. 2.11.7.2.2.3 (Decreto 976 de 2026)"),
        new(IrlMinimo, UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Mayor, "RiesgoDeLiquidez.UmbralIrl", "RiesgoDeLiquidez.UmbralIrl",
            "Circular Básica Contable y Financiera, Título IV, Capítulo III, Anexo 1"),
        // Feature 014, US17 (T220): el umbral de la exógena 1019 (lo lee ProyeccionDeCaptaciones; sin parámetro de la cooperativa).
        new(Exogena1019UmbralUvt, UnidadDeValorNacional.Veces, DireccionDeLoEstricto.Menor, "Umbral de la exógena 1019 (ProyeccionDeCaptaciones)", null,
            "Resolución DIAN 000162 de 2023, art. 3 (Resolución 227 de 2025, art. 1.3.3.1.1)"),
    ];

    private static readonly Dictionary<string, LimiteLegal> PorCodigo = Lista.ToDictionary(l => l.Codigo, StringComparer.Ordinal);

    public static IReadOnlyList<LimiteLegal> Todos => Lista;

    public static LimiteLegal? Buscar(string? codigo) =>
        codigo is not null && PorCodigo.TryGetValue(codigo, out var l) ? l : null;

    /// <summary>Lo más estricto entre el valor de la cooperativa y el límite vigente, según la dirección del código.</summary>
    public static decimal MasEstricto(string codigo, decimal valorCooperativa, decimal limite) =>
        Requerido(codigo).Estricto == DireccionDeLoEstricto.Mayor
            ? Math.Max(valorCooperativa, limite)
            : Math.Min(valorCooperativa, limite);

    /// <summary>Verdadero si el valor de la cooperativa es menos estricto que el límite (se rechaza al guardarlo).</summary>
    public static bool EsMenosEstricto(string codigo, decimal valorCooperativa, decimal limite) =>
        Requerido(codigo).Estricto == DireccionDeLoEstricto.Mayor
            ? valorCooperativa < limite
            : valorCooperativa > limite;

    private static LimiteLegal Requerido(string codigo) =>
        Buscar(codigo) ?? throw new ArgumentException($"«{codigo}» no es un límite legal del catálogo.", nameof(codigo));
}
