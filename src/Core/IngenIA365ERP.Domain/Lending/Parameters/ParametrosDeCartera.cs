using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;
using static IngenIA365ERP.Domain.Common.Parametros.CatalogoDeParametros;
using static IngenIA365ERP.Domain.Lending.EntregaDeCartera;

namespace IngenIA365ERP.Domain.Lending.Parameters;

/// <summary>
/// Catálogo cerrado de las claves <c>LND</c> de Cartera (feature 013, T43, R17; decisiones-transversales §2.20; data-model parte 3
/// §12.1). Las claves <b>no</b> llevan el prefijo «Cartera.»: el módulo ya lo dice (CH36). Cada una con su tipo, valores, defecto
/// seguro, ámbitos (<c>None</c>, <c>CreditLine</c>, <c>LendingFactType</c>), la entrega de Cartera desde la que existe
/// (<see cref="DefinicionDeParametro.DisponibleDesde"/>: una de K2 no se ofrece ni se acepta con
/// <see cref="CatalogoDeCartera.EntregaVigente"/> en K1), los límites legales que la acotan
/// (<see cref="DefinicionDeParametro.LimitesLegales"/>, §12.5) y si su defecto está «pendiente de validar».
///
/// <para>
/// <b>Sin valores legales en el código</b> (<c>LaCarteraNoTieneValoresLegalesFijos</c>): los defectos son texto, configurable con
/// vigencia; donde el defecto es «el límite nacional» la clave admite vacío y vacío significa «rige el límite» (lo resuelve
/// <c>ReglasDeParametrosDeCartera</c> al leer). Los únicos números del archivo son rangos de forma (un día del mes hasta 28, una
/// fracción hasta 1, segundos hasta 30), no normas.
/// </para>
///
/// <para>
/// Dos ajustes frente a data-model §12.1 para no cambiar las invariantes de la plataforma de la 012 (<c>LectorDeParametrosTests</c>):
/// toda clave admite el ámbito general —<c>Contabilidad.Granularidad</c>, <c>.DisparadorDeLote</c> y <c>.HoraDeLote</c>, que la tabla da
/// sólo por tipo de hecho, admiten además el general, que es la caída del lector— y ninguna exige <c>legalSource</c>: la fuente de
/// un límite vive en su valor nacional <c>LIMITE</c> y la comparación la hace <c>ReglasDeParametrosDeCartera</c>.
/// </para>
/// </summary>
public static class ParametrosDeCartera
{
    public const string Modulo = CatalogoDeCartera.Modulo;

    /// <summary>Permiso propio de <see cref="SegregacionIncompatibilidades"/> (api.md §3.4), además del de la ruta.</summary>
    public const string PermisoDeSegregacion = "Lending.Segregation.Manage";

    private const string PendienteContadora =
        "Defecto de data-model §12.1, sembrado pendiente de validar por la contadora (decisiones-transversales §7.7).";
    private const string PendienteLimite =
        "Sin valor propio: rige el límite legal vigente (valor nacional LIMITE, data-model §12.5), sembrado pendiente de validar.";

    // ---------------------------------------------------------------------------------------- pagos, mora y causación --
    public const string PagosDiasMaximosFechaValor = "Pagos.DiasMaximosFechaValor";
    public const string PagosFirmezaDiasPorClaseDeMedio = "Pagos.FirmezaDiasPorClaseDeMedio";
    public const string MoraToleranciaDeSaldoVencido = "Mora.ToleranciaDeSaldoVencido";
    public const string MoraSobreIntereses = "Mora.SobreIntereses";
    public const string CausacionContinuaTrasFallecimiento = "Causacion.ContinuaTrasFallecimiento";

    // ------------------------------------------------------------------------------------------------ riesgo y cierres --
    public const string RiesgoReglaMesesFaltantes = "Riesgo.ReglaMesesFaltantes";
    public const string RiesgoDiaLimiteCierre = "Riesgo.DiaLimiteCierre";
    public const string RiesgoCierreProgramado = "Riesgo.CierreProgramado";
    public const string RiesgoRedondeoDeterioro = "Riesgo.RedondeoDeterioro";
    public const string RiesgoCalificacionDiariaConArrastre = "Riesgo.CalificacionDiariaConArrastre";
    public const string RiesgoExcepcionAportesArrastre = "Riesgo.ExcepcionAportesArrastre";
    public const string ConcentracionLimiteIndividual = "Concentracion.LimiteIndividual";
    public const string ConcentracionLimiteIndividualConGarantia = "Concentracion.LimiteIndividualConGarantia";

    // ---------------------------------------------------------------------------------------------------- contabilidad --
    public const string ContabilidadModoDePaso = "Contabilidad.ModoDePaso";
    public const string ContabilidadGranularidad = "Contabilidad.Granularidad";
    public const string ContabilidadDisparadorDeLote = "Contabilidad.DisparadorDeLote";
    public const string ContabilidadHoraDeLote = "Contabilidad.HoraDeLote";
    public const string ContabilidadPoliticaSinRespuesta = "Contabilidad.PoliticaSinRespuesta";
    public const string ContabilidadFaltaDeRegla = "Contabilidad.FaltaDeRegla";
    public const string ContabilidadValidacionPreviaSegundos = "Contabilidad.ValidacionPreviaSegundos";
    public const string ContabilidadDisminucionDeDeterioro = "Contabilidad.DisminucionDeDeterioro";
    public const string ContabilidadIndicadoresVigenciaMeses = "Contabilidad.IndicadoresVigenciaMeses";
    public const string ContabilidadDiferirComisiones = "Contabilidad.DiferirComisiones";

    // -------------------------------------------------------------------- originación, cupos, reservas, giros, bloqueos --
    public const string OriginacionDiasVigenciaDeAprobacion = "Originacion.DiasVigenciaDeAprobacion";
    public const string ReservasDiasDeVigencia = "Reservas.DiasDeVigencia";
    public const string GirosDiasSinResultado = "Giros.DiasSinResultado";
    public const string CupoMoraMaximaParaVender = "Cupo.MoraMaximaParaVender";
    public const string BloqueosDiasDeMoraParaBloquear = "Bloqueos.DiasDeMoraParaBloquear";
    public const string SegregacionIncompatibilidades = "Segregacion.Incompatibilidades";

    // ---------------------------------------------------------------------------------------------------- libranza (K2) --
    public const string LibranzaCapacidadDeDescuento = "Libranza.CapacidadDeDescuento";
    public const string LibranzaVentanaDeGiroDias = "Libranza.VentanaDeGiroDias";
    public const string LibranzaUmbralDeMoraDePagaduria = "Libranza.UmbralDeMoraDePagaduria";

    // ------------------------------------------------------------------------------------------------- castigo (K3) --
    public const string CastigoExigeDeterioroTotal = "Castigo.ExigeDeterioroTotal";
    public const string CastigoDiasMinimosDeMora = "Castigo.DiasMinimosDeMora";
    public const string CastigoGestionesMinimas = "Castigo.GestionesMinimas";

    // -------------------------------------------------------------------- cobranza, aviso previo, mensajes y canales --
    public const string CobranzaHorarioLunesAViernes = "Cobranza.HorarioLunesAViernes";
    public const string CobranzaHorarioSabado = "Cobranza.HorarioSabado";
    public const string CobranzaContactosPorDia = "Cobranza.ContactosPorDia";

    /// <summary>K4 (US12, T0943; data-model §26.8): lo que se perdona, en pesos, para dar por cumplida una promesa de pago. (nuevo)</summary>
    public const string CobranzaToleranciaDePromesa = "Cobranza.ToleranciaDePromesa";
    public const string AvisoPrevioDiasMinimos = "AvisoPrevio.DiasMinimos";
    public const string AvisoPrevioUmbralDosComunicaciones = "AvisoPrevio.UmbralDosComunicaciones";
    public const string MensajesReintentos = "Mensajes.Reintentos";
    public const string CanalesUmbralDeFallas = "Canales.UmbralDeFallas";
    public const string CentralesDiasDeVigenciaDeConsulta = "Centrales.DiasDeVigenciaDeConsulta";
    public const string ListasUmbralDeCoincidenciaDeNombre = "Listas.UmbralDeCoincidenciaDeNombre";

    // ---------------------------------------------------------------------------------------------- documentos e informes --
    public const string DocumentosFirmanteNombre = "Documentos.Firmante.Nombre";
    public const string DocumentosFirmanteCargo = "Documentos.Firmante.Cargo";
    public const string DocumentosPazYSalvoIncluyeCodeudas = "Documentos.PazYSalvoIncluyeCodeudas";
    public const string InformesRangosDeEdad = "Informes.RangosDeEdad";
    public const string InformesPuntosAlertaUsura = "Informes.PuntosAlertaUsura";

    /// <summary>Forma de un horario de cobranza: <c>HH:mm-HH:mm</c>.</summary>
    public const string PatronDeHorario = @"^([01]\d|2[0-3]):[0-5]\d-([01]\d|2[0-3]):[0-5]\d$";

    private const ParameterScopeKind General = ParameterScopeKind.None;
    private const ParameterScopeKind Linea = ParameterScopeKind.CreditLine;
    private const ParameterScopeKind TipoDeHecho = ParameterScopeKind.LendingFactType;

    public static IReadOnlyList<DefinicionDeParametro> Definiciones { get; } =
    [
        // ------------------------------------------------------------------------------------ pagos, mora y causación --
        Entero(Modulo, PagosDiasMaximosFechaValor,
            "Días hacia atrás que admite la fecha valor de un pago registrado en pantalla. No rige para los pagos sin interacción cuya "
            + "fecha valor fija la norma: Nómina y, desde K2, la pagaduría con referencia (enmienda A9).",
            "5", K1, General),
        Texto(PagosFirmezaDiasPorClaseDeMedio,
            "Días hábiles de firmeza por clase de medio de recaudo, en JSON { clase: días } con «*» para las demás (D23).",
            "{\"*\": 10, \"InternalOffset\": 0}", K1, patron: @"(?s)^\s*\{.*\}\s*$") with { PendienteDeValidar = PendienteContadora },
        NumeroDecimal(Modulo, MoraToleranciaDeSaldoVencido,
            "Saldo vencido, en pesos, por debajo del cual el crédito no se considera en mora.", "0", K1, General, Linea),
        SiNo(Modulo, MoraSobreIntereses,
            "Se causa mora sobre los intereses vencidos, si la norma lo permite. Nunca en vivienda.", false, K1, General) with
        {
            LimitesLegales = [CatalogoDeLimitesLegales.MoraSobreInteresesPermitida],
        },
        SiNo(Modulo, CausacionContinuaTrasFallecimiento,
            "La causación sigue después del fallecimiento del deudor hasta que se resuelva el crédito.", true, K1, General, Linea),

        // -------------------------------------------------------------------------------------------- riesgo y cierres --
        Eleccion(Modulo, RiesgoReglaMesesFaltantes,
            "Cómo se completan los meses de historial que faltan para la pérdida esperada (se usa desde K6a).",
            ["MayorObservada", "DelCorte", "Fija"], "MayorObservada", K1, General) with { PendienteDeValidar = PendienteContadora },
        Entero(Modulo, RiesgoDiaLimiteCierre, "Día del mes siguiente hasta el que se puede cerrar el mes de cartera.", "10", K1, General)
            with { Minimo = 1, Maximo = 28 },
        SiNo(Modulo, RiesgoCierreProgramado, "El cierre mensual corre solo el día límite si nadie lo ordenó antes.", false, K1, General),
        Eleccion(Modulo, RiesgoRedondeoDeterioro, "Redondeo del deterioro calculado.", ["Centavo", "Peso"], "Centavo", K1, General),
        SiNo(Modulo, RiesgoCalificacionDiariaConArrastre,
            "La calificación diaria aplica el arrastre entre créditos del mismo deudor.", true, K1, General),
        SiNo(Modulo, RiesgoExcepcionAportesArrastre,
            "Los créditos cubiertos con aportes se exceptúan del arrastre. Forzado en «false» mientras rija D19.", false, K1, General),
        NumeroDecimal(Modulo, ConcentracionLimiteIndividual,
            "Límite de concentración individual, como fracción del patrimonio técnico. Vacío: rige el límite legal.", "", K1, General) with
        {
            AdmiteVacio = true, Maximo = 1m,
            LimitesLegales = [CatalogoDeLimitesLegales.ConcentracionIndividual],
            PendienteDeValidar = PendienteLimite,
        },
        NumeroDecimal(Modulo, ConcentracionLimiteIndividualConGarantia,
            "Límite de concentración individual con garantía admisible, como fracción del patrimonio técnico. Vacío: rige el límite legal.",
            "", K1, General) with
        {
            AdmiteVacio = true, Maximo = 1m,
            LimitesLegales = [CatalogoDeLimitesLegales.ConcentracionIndividualConGarantia],
            PendienteDeValidar = PendienteLimite,
        },

        // ------------------------------------------------------------------------------------------------- contabilidad --
        Eleccion(Modulo, ContabilidadModoDePaso,
            "Cómo pasan los hechos de Cartera a contabilidad. El defecto por tipo de hecho (D8, T56: por lotes los de causación, "
            + "deterioro, reclasificación y reverso de cierre; en línea el resto) lo propone la semilla de tipos de hecho; sin "
            + "contabilidad iniciada no pasa.",
            ["EnLinea", "PorLotes", "NoPasa"], "EnLinea", K1, General, TipoDeHecho) with { SelladoAlConfirmar = true },
        Eleccion(Modulo, ContabilidadGranularidad, "Cómo se resume el comprobante de un lote de hechos.",
            ["PorDia", "PorDiaYSucursal", "PorHecho"], "PorDia", K1, General, TipoDeHecho),
        Eleccion(Modulo, ContabilidadDisparadorDeLote,
            "Qué dispara el lote contable: una hora, el fin de la causación (los de causación) o el cierre de cartera (los del cierre).",
            ["Hora", "FinDeCausacion", "CierreDeCartera"], "FinDeCausacion", K1, General, TipoDeHecho),
        new DefinicionDeParametro
        {
            Modulo = Modulo, Clave = ContabilidadHoraDeLote,
            Descripcion = "Hora del lote contable, sólo con el disparador «Hora». Vacío: sin hora.",
            Tipo = TipoDeParametro.Time, DefectoSeguro = string.Empty, AdmiteVacio = true, DisponibleDesde = K1,
            AmbitosAdmitidos = [General, TipoDeHecho],
        },
        Eleccion(Modulo, ContabilidadPoliticaSinRespuesta,
            "Qué pasa si la validación previa contable no responde a tiempo. «Bloquear» sólo muerde en las operaciones de pantalla "
            + "de Cartera (desembolso, novedad, condonación, castigo); en el contrato, el consumidor y los procesos se confirma con "
            + "pendiente (A8 tocó sólo la falta de regla).",
            ["ConfirmarConPendiente", "Bloquear"], "ConfirmarConPendiente", K1, General),
        Eleccion(Modulo, ContabilidadFaltaDeRegla,
            "Qué pasa cuando la evaluación contable encuentra un hallazgo (falta la regla, la cuenta o un dato). «Bloquear» detiene "
            + "todo (A8, respuesta del dueño del 2026-10-14): la pantalla no confirma, el contrato rechaza el pago (Nómina no aprueba), "
            + "la venta de Comercial queda en reproceso y el proceso detiene su tanda y alerta. «Advertir» confirma y alerta. "
            + "Arranca en «Bloquear» (decisión del dueño del 2026-10-14).",
            ["Advertir", "Bloquear"], "Bloquear", K1, General, TipoDeHecho),
        Entero(Modulo, ContabilidadValidacionPreviaSegundos, "Segundos que espera la validación previa contable.", "3", K1, General)
            with { Minimo = 1m, Maximo = 30m },
        Eleccion(Modulo, ContabilidadDisminucionDeDeterioro,
            "Cómo se contabiliza una disminución del deterioro: primero contra el gasto del mismo ejercicio o toda a recuperación.",
            ["MismoEjercicioPrimero", "TodoARecuperacion"], "MismoEjercicioPrimero", K1, General) with { PendienteDeValidar = PendienteContadora },
        Entero(Modulo, ContabilidadIndicadoresVigenciaMeses, "Meses de vigencia de los indicadores regulatorios calculados.", "6", K1, General)
            with { Minimo = 1m },
        SiNo(Modulo, ContabilidadDiferirComisiones,
            "Diferir comisiones y costos de originación (FR-017). Ninguna entrega lo implementa todavía: sólo se admite «false».",
            false, K1, General) with { PendienteDeValidar = "Pregunta C10 a la contadora (decisiones-transversales §5); defecto FR-017." },

        // ---------------------------------------------------------------- originación, cupos, reservas, giros, bloqueos --
        Entero(Modulo, OriginacionDiasVigenciaDeAprobacion, "Días que dura una aprobación de crédito sin desembolsar.", "30", K1, General, Linea)
            with { Minimo = 1m, PendienteDeValidar = PendienteContadora },
        Entero(Modulo, ReservasDiasDeVigencia, "Días que dura una reserva de cupo para una venta a crédito.", "5", K1, General)
            with { Minimo = 1m, PendienteDeValidar = PendienteContadora },
        Entero(Modulo, GirosDiasSinResultado, "Días sin resultado del giro antes de alertar.", "3", K1, General)
            with { Minimo = 1m, PendienteDeValidar = PendienteContadora },
        Entero(Modulo, CupoMoraMaximaParaVender, "Días de mora con los que todavía se vende a crédito (0: en mora no se vende).", "0", K1, General)
            with { PendienteDeValidar = PendienteContadora },
        Entero(Modulo, BloqueosDiasDeMoraParaBloquear, "Días de mora desde los que se bloquea al deudor.", "30", K1, General)
            with { Minimo = 1m, PendienteDeValidar = PendienteContadora },
        Texto(SegregacionIncompatibilidades,
            "Pares de acciones que una misma persona no puede hacer sobre el mismo expediente, en JSON "
            + "[{ \"a\": acción, \"b\": acción, \"activo\": true|false }] (Radicar, Analizar, Decidir, Votar, Desembolsar, "
            + "AnularOrdenDeGiro, ArmarLoteDeCastigo, AplicarCastigo, CargarMigracion, AceptarDiferencias, Aplicar). Por defecto (F6, T0290) "
            + "Decidir × Desembolsar activo y sugeridos inactivos Analizar × Decidir, Desembolsar × AnularOrdenDeGiro, "
            + "ArmarLoteDeCastigo × AplicarCastigo y CargarMigracion × AceptarDiferencias.",
            "[{\"a\": \"Decidir\", \"b\": \"Desembolsar\", \"activo\": true}, {\"a\": \"Analizar\", \"b\": \"Decidir\", \"activo\": false}, {\"a\": \"Desembolsar\", \"b\": \"AnularOrdenDeGiro\", \"activo\": false}, {\"a\": \"ArmarLoteDeCastigo\", \"b\": \"AplicarCastigo\", \"activo\": false}, {\"a\": \"CargarMigracion\", \"b\": \"AceptarDiferencias\", \"activo\": false}]", K1, patron: @"(?s)^\s*\[.*\]\s*$")
            with { PermisoAdicional = PermisoDeSegregacion },

        // ------------------------------------------------------------------------------------------------ libranza (K2) --
        NumeroDecimal(Modulo, LibranzaCapacidadDeDescuento,
            "Fracción del salario o pensión, tras los descuentos de ley, que admite la libranza. Vacío: rige el límite legal.", "", K2, General) with
        {
            AdmiteVacio = true, Maximo = 1m,
            LimitesLegales = [CatalogoDeLimitesLegales.LibranzaCapacidadDescuento],
            PendienteDeValidar = PendienteLimite,
        },
        Entero(Modulo, LibranzaVentanaDeGiroDias, "Días que tiene la pagaduría para girar lo descontado.", "3", K2, General) with
        {
            Minimo = 1m,
            LimitesLegales = [CatalogoDeLimitesLegales.LibranzaVentanaGiroDias],
            PendienteDeValidar = PendienteContadora,
        },
        Entero(Modulo, LibranzaUmbralDeMoraDePagaduria,
            "Días de mora de la pagaduría desde los que se reporta. Sin valor hasta cotejar la Circular Básica Contable y Financiera "
            + "antes de K2 (C71): vacío, rige el límite legal cuando se registre.", "", K2, General) with
        {
            AdmiteVacio = true,
            LimitesLegales = [CatalogoDeLimitesLegales.LibranzaUmbralMoraPagaduria],
            PendienteDeValidar = "Sin valor hasta que el dueño coteje la Circular Básica Contable y Financiera antes de K2 (C71).",
        },

        // --------------------------------------------------------------------------------------------- castigo (K3) --
        // US14 (T0910; FR-095): la política de castigo de la cooperativa, que evalúa RequisitosDeCastigo junto con lo que exige la norma.
        SiNo(Modulo, CastigoExigeDeterioroTotal,
            "El castigo exige que el deterioro individual del último cierre cubra todo el saldo del crédito.", true, K3, General) with
        {
            PendienteDeValidar = "Defecto propuesto (exigir el deterioro total), sembrado pendiente de validar por el dueño y la contadora.",
        },
        Entero(Modulo, CastigoDiasMinimosDeMora,
            "Días de mora mínimos para castigar un crédito. Vacío: no se exige altura mínima.", "", K3, General) with
        {
            AdmiteVacio = true, Minimo = 0m,
            PendienteDeValidar = "Sin valor hasta que el dueño lo fije: mientras esté vacío, el castigo no exige altura mínima.",
        },
        Entero(Modulo, CastigoGestionesMinimas,
            "Gestiones de cobranza registradas mínimas para castigar. Mientras no exista el registro de gestiones (K4), las acredita el informe motivado.",
            "0", K3, General) with
        {
            Minimo = 0m,
            PendienteDeValidar = "Cero hasta K4: sin registro de gestiones, el informe motivado acredita que se agotaron.",
        },

        // ---------------------------------------------------------------- cobranza, aviso previo, mensajes y canales --
        Texto(CobranzaHorarioLunesAViernes,
            "Franja de contacto de cobranza de lunes a viernes (HH:mm-HH:mm), dentro del techo legal. Informativo en K1; se aplica en K4.",
            "07:00-19:00", K1, patron: PatronDeHorario) with
        {
            LimitesLegales = [CatalogoDeLimitesLegales.CobranzaHoraInicioLunesAViernes, CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes],
        },
        Texto(CobranzaHorarioSabado,
            "Franja de contacto de cobranza el sábado (HH:mm-HH:mm), dentro del techo legal. Informativo en K1; se aplica en K4.",
            "08:00-15:00", K1, patron: PatronDeHorario) with
        {
            LimitesLegales = [CatalogoDeLimitesLegales.CobranzaHoraInicioSabado, CatalogoDeLimitesLegales.CobranzaHoraFinSabado],
        },
        Entero(Modulo, CobranzaContactosPorDia,
            "Contactos de cobranza por día con el mismo deudor, dentro del techo legal. Informativo en K1; se aplica en K4.", "1", K1, General) with
        {
            Minimo = 1m,
            LimitesLegales = [CatalogoDeLimitesLegales.CobranzaContactosDia],
        },
        NumeroDecimal(Modulo, CobranzaToleranciaDePromesa,
            "Valor, en pesos, que se perdona para dar por cumplida una promesa de pago: la promesa se cumple si lo pagado llega a lo prometido menos "
            + "esta tolerancia. Se sella en la promesa al registrarla.", "0", K4, General) with
        {
            Minimo = 0m,
            PendienteDeValidar = "Defecto de data-model §26.8 (sin tolerancia), sembrado pendiente de validar por el dueño y la contadora (T0962).",
        },
        Entero(Modulo, AvisoPrevioDiasMinimos, "Días calendario mínimos entre el aviso previo y el reporte negativo.", "20", K4, General) with
        {
            LimitesLegales = [CatalogoDeLimitesLegales.AvisoPrevioDias],
        },
        NumeroDecimal(Modulo, AvisoPrevioUmbralDosComunicaciones,
            "Saldo de la obligación, en veces el SMMLV, hasta el cual (inferior o igual) el aviso previo exige dos comunicaciones en días distintos "
            + "(Ley 2157 de 2021); por encima basta una. Vacío: rige el límite legal.", "", K4, General) with
        {
            AdmiteVacio = true,
            LimitesLegales = [CatalogoDeLimitesLegales.AvisoPrevioUmbralSmmlv],
            PendienteDeValidar = PendienteLimite,
        },
        Entero(Modulo, MensajesReintentos, "Reintentos de un mensaje al deudor que no salió.", "3", K1, General),
        Entero(Modulo, CanalesUmbralDeFallas, "Fallas seguidas de un canal externo antes de abrir el circuito.", "5", K1, General)
            with { Minimo = 1m },
        Entero(Modulo, CentralesDiasDeVigenciaDeConsulta, "Días que vale una consulta a centrales de riesgo.", "30", K1, General, Linea)
            with { Minimo = 1m },
        NumeroDecimal(Modulo, ListasUmbralDeCoincidenciaDeNombre,
            "Similitud mínima (de 0 a 1) para que un nombre cuente como coincidencia en listas restrictivas.", "0.90", K1, General)
            with { Maximo = 1m },

        // ------------------------------------------------------------------------------------------ documentos e informes --
        Texto(DocumentosFirmanteNombre,
            "Nombre de quien firma los documentos de Cartera. Mientras falte, emitir responde Lending.Document.SignerMissing.",
            "", K1, admiteVacio: true),
        Texto(DocumentosFirmanteCargo,
            "Cargo de quien firma los documentos de Cartera. Mientras falte, emitir responde Lending.Document.SignerMissing.",
            "", K1, admiteVacio: true),
        SiNo(Modulo, DocumentosPazYSalvoIncluyeCodeudas, "El paz y salvo incluye los créditos donde la persona es codeudora.", false, K1, General),
        Texto(InformesRangosDeEdad,
            "Límites de los rangos de edad de la cartera, en días y en orden creciente, separados por coma.",
            "30,60,90,180,360", K1, patron: @"^\d{1,4}(,\d{1,4})*$"),
        NumeroDecimal(Modulo, InformesPuntosAlertaUsura,
            "Puntos de tasa efectiva anual bajo la usura desde los que se alerta.", "2.0", K1, General)
            with { PendienteDeValidar = PendienteContadora },
    ];

    private static DefinicionDeParametro Texto(string clave, string descripcion, string defecto, EntregaDeModulo desde,
        string? patron = null, bool admiteVacio = false) => new()
        {
            Modulo = Modulo, Clave = clave, Descripcion = descripcion, Tipo = TipoDeParametro.Text, DefectoSeguro = defecto,
            Patron = patron, AdmiteVacio = admiteVacio, DisponibleDesde = desde, AmbitosAdmitidos = [General],
        };
}
