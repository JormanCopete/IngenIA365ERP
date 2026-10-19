using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Documents;
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

    /// <summary>
    /// K6a (T1027; data-model §12.1): los días de mora con que se completan los meses de historial que faltan cuando
    /// <see cref="RiesgoReglaMesesFaltantes"/> es «Fija» (<c>HistorialDeMora.Completar</c>). Sin valor; con la regla «Fija» es obligatoria. (nuevo)
    /// </summary>
    public const string RiesgoMesesFaltantesDiasFijos = "Riesgo.MesesFaltantesDiasFijos";
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

    /// <summary>
    /// K4, bloque D (decisión del dueño del 2026-10-16): el tope del gasto de cobranza como fracción de la base del gasto (lo vencido o el capital
    /// vencido, la base de la regla de la etapa). Vacío = sin tope. (nuevo)
    /// </summary>
    public const string CobranzaTopeGastoPorcentaje = "Cobranza.TopeGasto.Porcentaje";

    /// <summary>K4, bloque D: el tope del gasto de cobranza en pesos. Vacío = sin máximo. Con los dos, rige el menor. (nuevo)</summary>
    public const string CobranzaTopeGastoValorMaximo = "Cobranza.TopeGasto.ValorMaximo";
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

    /// <summary>
    /// K5 (US15, T0980; data-model §12.1): desde qué días de mora un crédito cuenta como cartera <b>vencida</b> en los indicadores de calidad
    /// (<c>quality</c>) y en el tablero: vencida = saldo de capital con días de mora mayores que este valor. Pendiente de validar por la contadora. (nuevo)
    /// </summary>
    public const string InformesDiasCarteraVencida = "Informes.DiasCarteraVencida";

    /// <summary>
    /// K5 (US13; decisión del dueño del 2026-10-16): qué conceptos suma el saldo al 31 de diciembre del certificado tributario, como nombres de
    /// <c>ConceptosDelSaldoCertificado</c> separados por coma (<c>Capital</c>, <c>InteresCorriente</c>, <c>InteresDeMora</c>, <c>OtrosCargos</c> y
    /// <c>MenosAnticipo</c>, que resta el anticipo). El defecto los lleva todos: el saldo de antes del parámetro. Se lee vigente el día de la emisión. (nuevo)
    /// </summary>
    public const string DocumentosCertificadoTributarioSaldoIncluye = "Documentos.CertificadoTributario.SaldoIncluye";

    /// <summary>Feature 014, US13 (T196; documentos.md §2.3, P10): el certificado tributario de captaciones incluye el saldo y la revalorización de aportes.</summary>
    public const string DocumentosCertificadoTributarioIncluyeAportes = "Documentos.CertificadoTributarioIncluyeAportes";

    /// <summary>Feature 014, US13 (T196; documentos.md §2.4): la leyenda del certificado de retención (expedición sin firma autógrafa).</summary>
    public const string DocumentosLeyendaCertificadoRetencion = "Documentos.LeyendaCertificadoRetencion";

    /// <summary>
    /// Feature 014, US13 (T196; research E175): el texto propio de la entidad que se suma a cada clase de documento de captaciones, en JSON
    /// <c>{ clase: texto }</c> (criterio del dueño: textos parametrizables por entidad).
    /// </summary>
    public const string DocumentosTextosDeCaptaciones = "Documentos.TextosDeCaptaciones";

    /// <summary>Feature 014, US13 (T196; research E175): la periodicidad de los extractos de cuenta del lote (Mensual, Trimestral, Semestral, Anual).</summary>
    public const string DocumentosPeriodicidadDeExtractos = "Documentos.PeriodicidadDeExtractos";

    /// <summary>
    /// K5 (US16, T1003; adaptadores-y-archivos.md §7.4): el código de suscriptor (fuente) de la cooperativa en cada central de riesgo, como
    /// <c>CENTRAL=código</c> separados por punto y coma (<c>DATACREDITO=123456;TRANSUNION=ABC123</c>). Va en el archivo de la central
    /// (<c>Archivo.CodigoDeSuscriptor</c>); sin el de una central, el validador lo señala como obligatorio vacío y su archivo no se genera. (nuevo)
    /// </summary>
    public const string CentralesCodigosDeSuscriptor = "Centrales.CodigosDeSuscriptor";

    /// <summary>
    /// K5 (US16; decisión del dueño del 2026-10-16): los rangos de mora del reporte a centrales por tipo de cartera (modalidad) y desde qué rango el
    /// dato es negativo, como <c>TIPO=límite,límite,…|rango</c> separados por punto y coma (<see cref="Regulatory.RangosDeMoraParaCentrales"/>). El
    /// defecto (rangos de 30 días, negativo desde el primero) reproduce el comportamiento de antes: toda mora es dato negativo. Se lee vigente al corte
    /// del archivo. (nuevo)
    /// </summary>
    public const string CentralesRangosDeMora = "Centrales.RangosDeMora";

    /// <summary>
    /// K5 (decisión del dueño del 2026-10-16): el aviso previo al reporte negativo se abre y se envía también a cada codeudor vigente del crédito, con su
    /// propia constancia, y la central reporta al codeudor con dato negativo sólo si <b>su</b> aviso está completo. En <c>false</c> (el defecto), los
    /// codeudores no reciben aviso y no se reportan con dato negativo (quedan fuera con <c>NoPriorNoticeEvidence</c>). (nuevo)
    /// </summary>
    public const string AvisoPrevioIncluirCodeudores = "AvisoPrevio.IncluirCodeudores";

    // ----------------------------------------------------------------------- feature 014: captaciones y aportes (T017) --
    // research R26, C02, C09, C26 y E10; data-model §9.1. Cada clave desde la entrega que la usa: no se ofrece ni se acepta hasta que esa
    // entrega suba CatalogoDeCartera.EntregaVigente. Los defectos son los de la spec o la investigación, sembrados pendientes de validar.

    /// <summary>Tarifa de retención sobre rendimientos de ahorro a la vista (fracción).</summary>
    public const string RetencionTarifaAhorro = "Retencion.TarifaAhorro";

    /// <summary>Tarifa de retención sobre rendimientos de CDAT (fracción).</summary>
    public const string RetencionTarifaCdat = "Retencion.TarifaCdat";

    /// <summary>Tarifa de retención del ahorro contractual (fracción). Sin valor: rige la del ahorro.</summary>
    public const string RetencionTarifaContractual = "Retencion.TarifaContractual";

    /// <summary>Tarifa de retención del ahorro permanente (fracción). Sin valor: rige la del ahorro.</summary>
    public const string RetencionTarifaPermanente = "Retencion.TarifaPermanente";

    /// <summary>Umbral diario de la retención, en UVT (R06).</summary>
    public const string RetencionUmbralDiarioUvt = "Retencion.UmbralDiarioUvt";

    /// <summary>A qué clases aplica el umbral diario (<c>WithholdingThresholdScope</c>; D10).</summary>
    public const string RetencionAlcanceDelUmbral = "Retencion.AlcanceDelUmbral";

    /// <summary>Si se descuenta el componente inflacionario al retener a la persona natural no obligada a llevar contabilidad (P27).</summary>
    public const string RetencionComponenteInflacionarioAplica = "Retencion.ComponenteInflacionarioAplica";

    /// <summary>Feature 014, US5 (T128; research E94): el redondeo de la retención y del componente inflacionario: al peso (defecto) o al centavo.</summary>
    public const string RetencionRedondeo = "Retencion.Redondeo";

    /// <summary>Tarifa del GMF (fracción; admite cero desde su eliminación).</summary>
    public const string GmfTarifa = "Gmf.Tarifa";

    /// <summary>Tope mensual exento de GMF por persona, en UVT.</summary>
    public const string GmfTopeExencionUvt = "Gmf.TopeExencionUvt";

    /// <summary>Política de exención del GMF (<c>GmfExemptionPolicy</c>; P25).</summary>
    public const string GmfPoliticaDeExencion = "Gmf.PoliticaDeExencion";

    /// <summary>Qué operaciones generan GMF (JSON; D32).</summary>
    public const string GmfHechosGeneradores = "Gmf.HechosGeneradores";

    /// <summary>Redondeo del GMF: <c>Peso</c> (defecto) o <c>Centavo</c> (feature 014, decisión del dueño del 2026-10-18, D54).</summary>
    public const string GmfRedondeo = "Gmf.Redondeo";

    /// <summary>Porcentajes del fondo de liquidez por categoría, con la tabla del Decreto 976 de 2026 y su transición (JSON).</summary>
    public const string FondoDeLiquidezPorcentajesPorCategoria = "FondoDeLiquidez.PorcentajesPorCategoria";

    /// <summary>Techo de la cuota obligatoria de los fondos de empleados (fracción del ingreso). Vacío: rige el límite legal.</summary>
    public const string AportesTopeCuotaFondoEmpleados = "Aportes.TopeCuotaFondoEmpleados";

    /// <summary>Piso de la fracción de la cuota que va a aportes. Vacío: rige el límite legal.</summary>
    public const string AportesFraccionMinima = "Aportes.FraccionMinima";

    /// <summary>Techo de participación de una persona natural en el total de aportes. Vacío: rige el límite legal.</summary>
    public const string AportesLimiteParticipacionPersonaNatural = "Aportes.LimiteParticipacionPersonaNatural";

    /// <summary>Techo de participación de una persona jurídica en el total de aportes. Vacío: rige el límite legal.</summary>
    public const string AportesLimiteParticipacionPersonaJuridica = "Aportes.LimiteParticipacionPersonaJuridica";

    /// <summary>Mínimo irreducible de aportes de la entidad (estatutos), en pesos (FR-080).</summary>
    public const string AportesMinimoIrreducibleEntidad = "Aportes.MinimoIrreducibleEntidad";

    /// <summary>Edad desde la que el titular menor pasa a manejar su cuenta (R22).</summary>
    public const string CaptacionesMayoriaDeEdadAnios = "Captaciones.MayoriaDeEdadAnios";

    /// <summary>Orden en que se embargan las cuentas por clase (JSON; R23).</summary>
    public const string EmbargosOrdenDeCuentas = "Embargos.OrdenDeCuentas";

    /// <summary>US11 (T188): las clases de captación que protege el límite inembargable (JSON; research E156).</summary>
    public const string EmbargosClasesProtegidas = "Embargos.ClasesProtegidas";

    /// <summary>US11 (T188): las clases de captación que no se embargan por estatuto o ley (JSON; research E156).</summary>
    public const string EmbargosClasesInembargables = "Embargos.ClasesInembargables";

    /// <summary>US11 (T188): cómo se aplica el límite por autoridad (JSON; research E156).</summary>
    public const string EmbargosAplicacionDelLimite = "Embargos.AplicacionDelLimite";

    /// <summary>US11 (T188): el límite sólo protege a las personas naturales (research E156).</summary>
    public const string EmbargosLimiteSoloPersonasNaturales = "Embargos.LimiteSoloPersonasNaturales";

    /// <summary>US11 (T189): días hábiles para responder el oficio (informativo; research E157).</summary>
    public const string EmbargosDiasHabilesParaResponder = "Embargos.DiasHabilesParaResponder";

    /// <summary>US11 (T189): las plantillas de las respuestas al juzgado (JSON por clase de respuesta; research E158).</summary>
    public const string EmbargosPlantillasDeRespuesta = "Embargos.PlantillasDeRespuesta";

    /// <summary>Días hacia atrás que admite la fecha valor de una operación de cuenta (D11).</summary>
    public const string CaptacionesDiasMaximosFechaValor = "Captaciones.DiasMaximosFechaValor";

    /// <summary>Días hábiles de canje por clase de medio de recaudo (JSON; C14, FR-050).</summary>
    public const string CaptacionesDiasDeCanjePorClaseDeMedio = "Captaciones.DiasDeCanjePorClaseDeMedio";

    /// <summary>Umbral en pesos desde el que una consignación en efectivo exige la declaración de origen de fondos (FR-059).</summary>
    public const string EfectivoUmbralDeclaracion = "Efectivo.UmbralDeclaracion";

    /// <summary>Si los aportes descuentan del deterioro (FR-094, R20); junto a <see cref="RiesgoExcepcionAportesArrastre"/>.</summary>
    public const string RiesgoDescuentoAportesDeterioro = "Riesgo.DescuentoAportesDeterioro";

    /// <summary>Soportes de la entrega sin juicio de sucesión (JSON; FR-082).</summary>
    public const string FallecimientoSoportesDeEntregaSinSucesion = "Fallecimiento.SoportesDeEntregaSinSucesion";

    /// <summary>Si los saldos de cuentas abandonadas se trasladan (FR-084; confirmación de la contadora, P7).</summary>
    public const string CuentasAbandonadasTrasladoHabilitado = "CuentasAbandonadas.TrasladoHabilitado";

    /// <summary>Días de anticipación del aviso de cambio de tasa de una línea a la vista (FR-038).</summary>
    public const string CaptacionesAnticipacionAvisoCambioDeTasaDias = "Captaciones.AnticipacionAvisoCambioDeTasaDias";

    /// <summary>Plazo estatutario, en días, para devolver los aportes del asociado retirado (FR-079).</summary>
    public const string CaptacionesDiasPlazoDevolucionAportes = "Captaciones.DiasPlazoDevolucionAportes";

    /// <summary>Días sin cobrar desde los que lo devuelto pasa a no reclamados (FR-081).</summary>
    public const string CaptacionesDiasParaNoReclamados = "Captaciones.DiasParaNoReclamados";

    /// <summary>
    /// Feature 014, US7 (T182; FR-074; research §19): los días calendario que una cuota del ahorro contractual puede llegar después de su vencimiento sin
    /// contar como atrasada para la tolerancia de la línea (el descuento de Nómina o de la pagaduría se aplica unos días después del fin del período).
    /// </summary>
    public const string CaptacionesDiasDeGraciaDeCuotaContractual = "Captaciones.DiasDeGraciaDeCuotaContractual";

    /// <summary>
    /// Feature 014 (decisión del dueño del 2026-10-18, D41): de qué fondos sale primero el cruce al retiro del asociado —<c>AportesPrimero</c> (defecto) o
    /// <c>AhorrosPrimero</c>—, según los estatutos.
    /// </summary>
    public const string CaptacionesOrdenDeFondosAlRetiro = "Captaciones.OrdenDeFondosAlRetiro";

    /// <summary>
    /// Feature 014 (D46): la causa del retiro de la Cartera → el código del catálogo de motivos de retiro de Asociados (<c>COR_WithdrawalReasons.LegacyCode</c>),
    /// en JSON <c>{ "Voluntary": "VOL", "Exclusion": "EXC", "Death": "FAL", "Other": "OTR" }</c>. Lo que no se mapea deja el motivo vacío en Asociados.
    /// </summary>
    public const string CaptacionesMotivosDeRetiroDeAsociados = "Captaciones.MotivosDeRetiroDeAsociados";

    /// <summary>
    /// Feature 014 (D44): los correos adicionales que reciben el aviso del reverso tardío de un pago del cruce al retiro (separados por coma o punto y coma),
    /// además de la gerencia y la revisoría fiscal.
    /// </summary>
    public const string CaptacionesDestinatariosReversoTardio = "Captaciones.DestinatariosReversoTardio";

    /// <summary>
    /// Decisión del dueño del 2026-10-18: la periodicidad del formato de cartera del SICSES es <b>informativa</b> y la decide la entidad según su
    /// calendario. Los meses de corte en que la cooperativa reporta, separados por coma (<c>3,6,9,12</c> si reporta trimestral); vacío, los del
    /// documento <c>TiposDeOrganizacion</c> para su tipo y nivel. Sólo alimenta el aviso del plazo y la advertencia de mes fuera de la periodicidad:
    /// nada se bloquea por ella. (nuevo)
    /// </summary>
    public const string SicsesMesesDeCorte = "Sicses.MesesDeCorte";

    /// <summary>
    /// Decisión del dueño del 2026-10-18: los días calendario de plazo para entregar el SICSES desde el fin del mes cortado, propios de la entidad;
    /// vacío, los del documento <c>TiposDeOrganizacion</c> (con sus plazos especiales). Con valor, rige para todo corte y los plazos especiales del
    /// documento no se aplican. Informativo, como <see cref="SicsesMesesDeCorte"/>. (nuevo)
    /// </summary>
    public const string SicsesDiasDePlazo = "Sicses.DiasDePlazo";

    /// <summary>Forma de <see cref="SicsesMesesDeCorte"/>: meses del 1 al 12 separados por coma.</summary>
    public const string PatronDeMesesDeCorte = @"^(1[0-2]|[1-9])(,(1[0-2]|[1-9]))*$";

    /// <summary>Forma de <see cref="CentralesCodigosDeSuscriptor"/>.</summary>
    public const string PatronDeCodigosDeSuscriptor = @"^[A-Z0-9_]{1,20}=[A-Za-z0-9-]{1,20}(;[A-Z0-9_]{1,20}=[A-Za-z0-9-]{1,20})*$";

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
        Entero(Modulo, RiesgoMesesFaltantesDiasFijos,
            "Días de mora con que se completan los meses de historial que faltan si la regla de meses faltantes es «Fija». Sin valor; "
            + "obligatoria con esa regla.", "", K6a, General) with
        {
            AdmiteVacio = true, Minimo = 0m,
            PendienteDeValidar = "Sin valor: la cooperativa lo fija si elige la regla «Fija» (pregunta E9 de la contadora).",
        },
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
        NumeroDecimal(Modulo, CobranzaTopeGastoPorcentaje,
            "Tope del gasto de cobranza como fracción (de 0 a 1) de la base del gasto: lo vencido o el capital vencido, según la regla de "
            + "la etapa. Ninguna regla de etapa puede tener una tarifa mayor y el gasto propuesto se recorta a él. Vacío: sin tope.", "", K4, General) with
        {
            AdmiteVacio = true, Minimo = 0m, Maximo = 1m,
            PendienteDeValidar = "Sin valor (sin tope) hasta que el dueño y la contadora lo fijen (decisión del dueño del 2026-10-16, K4 bloque D).",
        },
        NumeroDecimal(Modulo, CobranzaTopeGastoValorMaximo,
            "Tope del gasto de cobranza en pesos por gasto propuesto. Con el porcentaje, rige el menor. Vacío: sin valor máximo.", "", K4, General) with
        {
            AdmiteVacio = true, Minimo = 0m,
            PendienteDeValidar = "Sin valor (sin máximo) hasta que el dueño y la contadora lo fijen (decisión del dueño del 2026-10-16, K4 bloque D).",
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
        Entero(Modulo, InformesDiasCarteraVencida,
            "Días de mora desde los que un crédito cuenta como cartera vencida en los indicadores de calidad y el tablero (más de este número).",
            "30", K5, General) with { Minimo = 0m, PendienteDeValidar = PendienteContadora },
        Texto(DocumentosCertificadoTributarioSaldoIncluye,
            "Conceptos que suma el saldo al 31 de diciembre del certificado tributario, separados por coma: Capital, InteresCorriente, InteresDeMora, "
            + "OtrosCargos (seguros, gastos de cobranza y otros cargos) y MenosAnticipo (resta el anticipo). El defecto los lleva todos.",
            SaldoDelCertificadoTributario.Defecto, K5, patron: SaldoDelCertificadoTributario.Patron),
        // Feature 014, US13 (T196; documentos.md §2.3, §2.4; research E175): los documentos al titular de captaciones.
        SiNo(Modulo, DocumentosCertificadoTributarioIncluyeAportes,
            "El certificado tributario de captaciones incluye el saldo de aportes al 31 de diciembre y la revalorización abonada en el año.", false, K8, General) with
        {
            PendienteDeValidar = "P10: la contadora decide si los aportes van en el certificado tributario de captaciones.",
        },
        Texto(DocumentosLeyendaCertificadoRetencion,
            "Leyenda del certificado de retención en la fuente sobre rendimientos financieros.",
            "Certificado expedido por computador, sin firma autógrafa, según el artículo 1.6.1.12.12 del Decreto 1625 de 2016 (DUR tributario). "
            + "Se expide conforme al artículo 381 del Estatuto Tributario.", K8) with
        {
            PendienteDeValidar = "Texto estándar con la norma citada (research §21): la contadora lo ajusta.",
        },
        Texto(DocumentosTextosDeCaptaciones,
            "Texto propio de la cooperativa que se suma, como leyenda, a cada clase de documento de captaciones y aportes, en JSON { clase: texto } con "
            + "las clases AccountStatement, AccountBalanceCertificate, DepositTaxCertificate, WithholdingCertificate, TermDepositTitle, "
            + "ContributionCertificate, ContributionClearance, MembershipWithdrawalSettlement y GarnishmentResponse.",
            "{}", K8, patron: @"(?s)^\s*\{.*\}\s*$"),
        Texto(DocumentosPeriodicidadDeExtractos,
            "Periodicidad de los extractos de cuenta que emite el lote: Mensual, Trimestral, Semestral o Anual. Informativa: el lote se corre a pedido.",
            "Mensual", K8, patron: "^(Mensual|Trimestral|Semestral|Anual)$") with
        {
            PendienteDeValidar = "Defecto mensual: cada cooperativa la fija según su reglamento de ahorro.",
        },
        Texto(CentralesCodigosDeSuscriptor,
            "Código de suscriptor de la cooperativa en cada central de riesgo, como CENTRAL=código separados por punto y coma "
            + "(DATACREDITO=123456;TRANSUNION=ABC123). Sin el de una central, su archivo no se genera.",
            string.Empty, K5, patron: PatronDeCodigosDeSuscriptor, admiteVacio: true),
        Texto(CentralesRangosDeMora,
            "Rangos de mora del reporte a centrales por tipo de cartera (modalidad) y desde qué rango el dato es negativo: TIPO=límite,límite,…|rango "
            + "separados por punto y coma, con * para los tipos que no se nombran (*=30,60,90,120,150,180|1;VIVIENDA=60,120,180|1). El rango 1 va de 1 "
            + "al primer límite, el último queda abierto; por debajo del rango negativo la obligación se reporta en mora con sus días reales, sin marca "
            + "negativa. La altura de mora reportada sale del rango.",
            Regulatory.RangosDeMoraParaCentrales.Defecto, K5, patron: Regulatory.RangosDeMoraParaCentrales.Patron) with
        {
            PendienteDeValidar = "Defecto propuesto (rangos de 30 días, negativo desde el primero: toda mora es dato negativo, como antes del parámetro), "
                                 + "pendiente de validar por el dueño y la contadora (decisión del dueño del 2026-10-16).",
        },
        Texto(SicsesMesesDeCorte,
            "Meses de corte en que la cooperativa reporta el formato de cartera del SICSES, separados por coma (3,6,9,12 si reporta trimestral). "
            + "Vacío: los del documento de tipos de organización para su tipo y nivel. Es informativo: sólo avisa el plazo; no bloquea nada.",
            string.Empty, K5, patron: PatronDeMesesDeCorte, admiteVacio: true),
        Entero(Modulo, SicsesDiasDePlazo,
            "Días calendario de plazo para entregar el SICSES desde el fin del mes cortado, propios de la cooperativa. Vacío: los del documento de "
            + "tipos de organización (con sus plazos especiales). Es informativo: sólo avisa el plazo; no bloquea nada.", "", K5, General) with
        {
            AdmiteVacio = true, Minimo = 1m,
        },
        SiNo(Modulo, AvisoPrevioIncluirCodeudores,
            "El aviso previo al reporte negativo se abre y se envía también a cada codeudor del crédito, con su propia constancia; la central reporta al "
            + "codeudor con dato negativo sólo si su aviso está completo. En «false», los codeudores no se reportan con dato negativo.", false, K5, General),

        // --------------------------------------------------------------- feature 014: captaciones y aportes (T017) --
        // Retención en la fuente sobre rendimientos (R06).
        NumeroDecimal(Modulo, RetencionTarifaAhorro,
            "Tarifa de retención en la fuente sobre los rendimientos del ahorro a la vista, como fracción (0,07 = 7 %).", "0.07", K8, General, Linea) with
        {
            Maximo = 1m, PendienteDeValidar = "Defecto de data-model §9.1 (DUR 1625 de 2016, tarifa residual), pendiente de validar por la contadora.",
        },
        NumeroDecimal(Modulo, RetencionTarifaCdat,
            "Tarifa de retención en la fuente sobre los rendimientos del CDAT, como fracción (0,04 = 4 %).", "0.04", K9, General, Linea) with
        {
            Maximo = 1m,
            PendienteDeValidar = "Defecto de data-model §9.1 (DUR 1625 de 2016 art. 1.2.4.2.83; Decreto 572 de 2025), pendiente de validar por la contadora.",
        },
        NumeroDecimal(Modulo, RetencionTarifaContractual,
            "Tarifa de retención del ahorro contractual, como fracción. Vacío: rige la del ahorro a la vista.", "", K9, General, Linea) with
        {
            AdmiteVacio = true, Maximo = 1m, PendienteDeValidar = "Sin valor (DUR 1625 de 2016): lo fija la contadora.",
        },
        NumeroDecimal(Modulo, RetencionTarifaPermanente,
            "Tarifa de retención del ahorro permanente, como fracción. Vacío: rige la del ahorro a la vista.", "", K8, General, Linea) with
        {
            AdmiteVacio = true, Maximo = 1m, PendienteDeValidar = "Sin valor (DUR 1625 de 2016): lo fija la contadora.",
        },
        NumeroDecimal(Modulo, RetencionUmbralDiarioUvt,
            "Umbral diario de la retención, en UVT: si ningún día del período lo alcanza no se retiene; si alguno lo alcanza, la base es todo el período.",
            "0.055", K8, General) with
        {
            PendienteDeValidar = "Defecto de data-model §9.1 (DUR 1625 de 2016 art. 1.2.4.2.6), pendiente de validar por la contadora.",
        },
        Eleccion(Modulo, RetencionAlcanceDelUmbral,
            "A qué clases de captación aplica el umbral diario de la retención.",
            ["AllDepositClasses", "ExceptTermDeposit", "None"], "ExceptTermDeposit", K8, General) with
        {
            PendienteDeValidar = "Defecto D10 de la spec (el CDAT retiene sin umbral, pregunta P2), pendiente de validar por la contadora.",
        },
        SiNo(Modulo, RetencionComponenteInflacionarioAplica,
            "Al retener a una persona natural no obligada a llevar contabilidad se descuenta el componente inflacionario vigente.", true, K8, General) with
        {
            PendienteDeValidar = "Defecto P27 de la spec (Estatuto Tributario; Decreto 898 de 2026), pendiente de validar por la contadora.",
        },
        // Fase 8 de la feature 014 (US5, research E94): el redondeo de la retención es un defecto configurable por cooperativa, como el del GMF (D54).
        Eleccion(Modulo, RetencionRedondeo, "Redondeo de la retención en la fuente sobre rendimientos y del componente inflacionario: al peso o al centavo.",
            ["Peso", "Centavo"], "Peso", K8, General) with
        {
            PendienteDeValidar = "Defecto al peso, mitad hacia arriba (investigación §3.6: 8.702 sobre 488.088), pendiente de validar por la contadora.",
        },

        // Gravamen a los movimientos financieros (R07).
        NumeroDecimal(Modulo, GmfTarifa,
            "Tarifa del GMF como fracción (0,004 = 4 por mil). Admite cero: desde esa vigencia no se calcula.", "0.004", K8, General) with
        {
            Maximo = 1m, PendienteDeValidar = "Defecto de data-model §9.1 (Estatuto Tributario art. 872), pendiente de validar por la contadora.",
        },
        NumeroDecimal(Modulo, GmfTopeExencionUvt,
            "Tope mensual exento del GMF por persona, en UVT, en la cuenta marcada.", "350", K8, General) with
        {
            PendienteDeValidar = "Defecto de data-model §9.1 (Estatuto Tributario art. 879 num. 1), pendiente de validar por la contadora.",
        },
        Eleccion(Modulo, GmfPoliticaDeExencion,
            "Cómo se aplica la exención del GMF: una cuenta marcada por persona, un tope por titular o no aplica.",
            ["SingleMarkedAccount", "PerHolderLimit", "NotApplicable"], "SingleMarkedAccount", K8, General) with
        {
            PendienteDeValidar = "Defecto P25 de la spec (Estatuto Tributario art. 881-1), pendiente de validar por la contadora.",
        },
        Texto(GmfHechosGeneradores,
            "Qué operaciones generan GMF, en JSON { \"gravados\": [...], \"exentos\": [...] } con las operaciones de la Cartera (D32).",
            "{\"gravados\": [\"Withdrawal\", \"TransferToOtherHolder\", \"TransferBetweenDemandAndTermDeposit\", \"TermDepositRedemptionToAccount\", \"Cancellation\", \"DirectDebit\"], \"exentos\": [\"TransferBetweenSameHolderSavings\"]}",
            K8, patron: @"(?s)^\s*\{.*\}\s*$") with
        {
            PendienteDeValidar = "Defecto D32 de la spec (Estatuto Tributario arts. 871 y 879 num. 14; Oficio DIAN 2787 de 2019) y el débito automático gravado "
                + "como un retiro (D54, 2026-10-18), pendientes de validar por la contadora.",
        },
        // Decisión del dueño del 2026-10-18 (feature 014, D54): el redondeo al peso es un defecto configurable por cooperativa.
        Eleccion(Modulo, GmfRedondeo, "Redondeo del GMF calculado: al peso o al centavo.", ["Peso", "Centavo"], "Peso", K8, General) with
        {
            PendienteDeValidar = "Defecto al peso (E81 de la feature 014), pendiente de validar por la contadora.",
        },

        // Fondo de liquidez (R25).
        Texto(FondoDeLiquidezPorcentajesPorCategoria,
            "Porcentajes del fondo de liquidez por categoría de la entidad y clase de depósito, con la transición, en JSON. Sin valor hasta cargar la tabla.",
            string.Empty, K11, patron: @"(?s)^\s*\{.*\}\s*$", admiteVacio: true) with
        {
            PendienteDeValidar = "Sin valor: la contadora carga la tabla del Decreto 976 de 2026 y su transición.",
        },

        // Aportes (FR-040 a FR-049, FR-080, FR-094).
        // Decisión del dueño del 2026-10-18 (feature 014, D29): 10 % por defecto, configurable por cada entidad con su vigencia; el LIMITE nacional
        // APORTES_TOPE_CUOTA_FONDO_EMPLEADOS (Ley) lo acota y no lo edita la entidad: es de la central.
        NumeroDecimal(Modulo, AportesTopeCuotaFondoEmpleados,
            "Techo de la cuota obligatoria de los fondos de empleados, como fracción del ingreso. Por defecto 10 %; cada entidad lo ajusta con su "
            + "vigencia sin pasar el límite legal. Vacío: rige el límite legal.", "0.10", K7, General) with
        {
            AdmiteVacio = true, Maximo = 1m,
            LimitesLegales = [CatalogoDeLimitesLegales.AportesTopeCuotaFondoEmpleados],
            PendienteDeValidar = PendienteLimite,
        },
        NumeroDecimal(Modulo, AportesFraccionMinima,
            "Fracción mínima de la cuota periódica que va a aportes sociales. Vacío: rige el límite legal.", "", K7, General) with
        {
            AdmiteVacio = true, Maximo = 1m,
            LimitesLegales = [CatalogoDeLimitesLegales.AportesFraccionMinima],
            PendienteDeValidar = PendienteLimite,
        },
        NumeroDecimal(Modulo, AportesLimiteParticipacionPersonaNatural,
            "Fracción máxima del total de aportes que puede tener una persona natural. Vacío: rige el límite legal.", "", K7, General) with
        {
            AdmiteVacio = true, Maximo = 1m,
            LimitesLegales = [CatalogoDeLimitesLegales.AportesLimiteParticipacionPersonaNatural],
            PendienteDeValidar = "Sin valor (pregunta P12, Ley 79 de 1988 a validar): rige el límite legal cuando se registre.",
        },
        NumeroDecimal(Modulo, AportesLimiteParticipacionPersonaJuridica,
            "Fracción máxima del total de aportes que puede tener una persona jurídica. Vacío: rige el límite legal.", "", K7, General) with
        {
            AdmiteVacio = true, Maximo = 1m,
            LimitesLegales = [CatalogoDeLimitesLegales.AportesLimiteParticipacionPersonaJuridica],
            PendienteDeValidar = "Sin valor (pregunta P12, Ley 79 de 1988 a validar): rige el límite legal cuando se registre.",
        },
        NumeroDecimal(Modulo, AportesMinimoIrreducibleEntidad,
            "Mínimo irreducible de aportes de la entidad, en pesos: una devolución que lo perforaría queda pendiente y avisa al gerente. Vacío: sin mínimo.",
            "", K7, General) with
        {
            AdmiteVacio = true, PendienteDeValidar = "Sin valor: lo fijan los estatutos de cada cooperativa (FR-080).",
        },
        SiNo(Modulo, RiesgoDescuentoAportesDeterioro,
            "Los aportes del deudor descuentan de la base del deterioro individual y del valor expuesto del activo de la pérdida esperada cuando la norma "
            + "lo permite (Anexo 1, num. 5.1; Anexo 2, num. 4.2): la entidad sin pérdidas acumuladas y con solvencia. Forzado en «false» mientras rija D19.",
            false, K7, General),

        // Cuentas: titulares, fecha valor, canje, efectivo, embargos (R22, R23, C14, D11, FR-059).
        Entero(Modulo, CaptacionesMayoriaDeEdadAnios,
            "Edad desde la que el titular menor maneja su cuenta: el proceso cierra la vigencia del representante y avisa.", "18", K7, General) with
        {
            Minimo = 1m, PendienteDeValidar = "Defecto de data-model §9.1 (Código Civil), pendiente de validar.",
        },
        Entero(Modulo, CaptacionesDiasMaximosFechaValor,
            "Días hacia atrás que admite la fecha valor de una operación de cuenta.", "5", K7, General) with
        {
            PendienteDeValidar = "Defecto D11 de la spec (reglamento), pendiente de validar.",
        },
        Texto(CaptacionesDiasDeCanjePorClaseDeMedio,
            "Días hábiles de canje por clase de medio de recaudo, en JSON { clase: días } con «*» para las demás: lo consignado suma al saldo pero no al "
            + "disponible hasta cumplirlos.",
            "{\"Check\": 3, \"*\": 0}", K8, patron: @"(?s)^\s*\{.*\}\s*$") with
        {
            PendienteDeValidar = "Defecto de contracts/contrato-de-cuentas.md (C14), pendiente de validar.",
        },
        NumeroDecimal(Modulo, EfectivoUmbralDeclaracion,
            "Valor en pesos desde el que una consignación en efectivo exige la declaración de origen de fondos. Vacío: no se exige.", "", K7, General) with
        {
            AdmiteVacio = true, PendienteDeValidar = "Sin valor (pregunta P4): lo fija el oficial de cumplimiento con la norma SARLAFT vigente.",
        },
        Texto(EmbargosOrdenDeCuentas,
            "Orden en que se embargan las cuentas de la persona, en JSON [clase, …] con las clases de captación. Sin valor: el orden de apertura.",
            string.Empty, K8, patron: @"(?s)^\s*\[.*\]\s*$", admiteVacio: true) with
        {
            PendienteDeValidar = "Sin valor (pregunta P5): lo fija la cooperativa.",
        },
        // US11 (T188, T189; research §20): el límite inembargable y la respuesta al juzgado, todo por entidad y «pendiente de validar» por el abogado.
        Texto(EmbargosClasesProtegidas,
            "Clases de captación que protege el límite inembargable de los depósitos de ahorro, en JSON [clase, …]. El CDAT no es depósito de ahorro.",
            "[\"Demand\", \"Contractual\", \"Permanent\"]", K8, patron: @"(?s)^\s*\[.*\]\s*$") with
        {
            PendienteDeValidar = "CGP art. 594 num. 2 (depósitos de ahorro): el abogado confirma qué productos de la cooperativa son de ahorro.",
        },
        Texto(EmbargosClasesInembargables,
            "Clases de captación que no se embargan nunca por estatuto o ley, en JSON [clase, …] (en un fondo de empleados, el ahorro permanente). "
            + "Los aportes no se embargan siempre (Ley 79 de 1988, art. 49).",
            "[]", K8, patron: @"(?s)^\s*\[.*\]\s*$") with
        {
            PendienteDeValidar = "Vacío: en un fondo de empleados el abogado debe poner [\"Permanent\"] (Decreto Ley 1481 de 1989, art. 16, modificado por la Ley 2496 de 2025).",
        },
        Texto(EmbargosAplicacionDelLimite,
            "Cómo protege el límite cada autoridad, en JSON { autoridad: AllProtectedAccounts | OldestAccount } con Court, TaxAuthority y OtherPublicEntity.",
            "{\"Court\": \"AllProtectedAccounts\", \"TaxAuthority\": \"OldestAccount\", \"OtherPublicEntity\": \"OldestAccount\"}", K8,
            patron: @"(?s)^\s*\{.*\}\s*$") with
        {
            PendienteDeValidar = "Judicial sobre la suma de las cuentas (D20); coactivo sobre la cuenta más antigua (ET art. 837-1): a validar por el abogado.",
        },
        SiNo(Modulo, EmbargosLimiteSoloPersonasNaturales,
            "El límite inembargable sólo protege a las personas naturales: una persona jurídica se embarga sin límite.", true, K8, General) with
        {
            PendienteDeValidar = "D20 de la spec y ET art. 837-1 (personas naturales): a validar por el abogado para el embargo judicial.",
        },
        Entero(Modulo, EmbargosDiasHabilesParaResponder,
            "Días hábiles desde el recibo del oficio para responder y poner a disposición lo retenido. Informativo: avisa, no bloquea.", "3", K8, General) with
        {
            Minimo = 0m, PendienteDeValidar = "CGP art. 593 num. 10 (tres días): a validar por el abogado.",
        },
        Texto(EmbargosPlantillasDeRespuesta,
            "Texto de cada respuesta al juzgado, en JSON { clase: texto } con las clases Acknowledgement, BalanceReport, ContributionsNotGarnishable, "
            + "ConsignmentNotice y ReleaseAcknowledgement; variables {Oficio}, {Proceso}, {Autoridad}, {Embargado}, {Documento}, {Demandante}, {Valor}, "
            + "{Retenido}, {Consignado}, {Fecha}, {Cuentas}. Sin texto para una clase rige el estándar.",
            "{}", K8, patron: @"(?s)^\s*\{.*\}\s*$") with
        {
            PendienteDeValidar = "Vacío: rigen los textos estándar; el abogado de la cooperativa los ajusta.",
        },

        // Retiro, fallecimiento, no reclamados y abandonadas (FR-038, FR-079, FR-081, FR-082, FR-084; C26).
        Entero(Modulo, CaptacionesAnticipacionAvisoCambioDeTasaDias,
            "Días de anticipación con que se avisa a los titulares un cambio de tasa de una línea a la vista. Vacío: se avisa el día del cambio.", "", K8, General) with
        {
            AdmiteVacio = true, PendienteDeValidar = "Sin valor: lo fija el reglamento de ahorro de la cooperativa (FR-038).",
        },
        Entero(Modulo, CaptacionesDiasPlazoDevolucionAportes,
            "Días del plazo estatutario para devolver los aportes del asociado retirado. Vacío: sin plazo de alerta.", "", K7, General) with
        {
            AdmiteVacio = true, PendienteDeValidar = "Sin valor: lo fijan los estatutos (FR-079).",
        },
        // Decisiones del dueño del 2026-10-18 (D41, D44, D46): lo de estatutos y de cada entidad del retiro del asociado.
        Eleccion(Modulo, CaptacionesOrdenDeFondosAlRetiro,
            "De qué fondos sale primero el cruce al retiro del asociado contra sus créditos: los aportes o los ahorros (lo fijan los estatutos).",
            ["AportesPrimero", "AhorrosPrimero"], "AportesPrimero", K7, General) with
        {
            PendienteDeValidar = "Defecto aportes → ahorros (D41, 2026-10-18): cada cooperativa lo ajusta a sus estatutos.",
        },
        Texto(CaptacionesMotivosDeRetiroDeAsociados,
            "La causa del retiro (Voluntary, Exclusion, Death, Other) y el código del motivo de retiro de Asociados que le corresponde, en JSON "
            + "{ \"Voluntary\": \"código\", … }. Sin código, Asociados registra el retiro sin motivo.",
            "{}", K7, patron: @"(?s)^\s*\{.*\}\s*$") with
        {
            PendienteDeValidar = "Sin mapeo: cada cooperativa lo arma con su catálogo de motivos de retiro (D46, 2026-10-18).",
        },
        Texto(CaptacionesDestinatariosReversoTardio,
            "Correos adicionales, separados por coma o punto y coma, que reciben el aviso del reverso tardío de un pago del cruce al retiro (además de "
            + "la gerencia y la revisoría fiscal).",
            string.Empty, K7, admiteVacio: true) with
        {
            PendienteDeValidar = "Sin valor: cada cooperativa pone los suyos (D44, 2026-10-18).",
        },
        // US7 (T182; FR-074): la gracia de una cuota del contractual antes de contar como atrasada, por cooperativa o por línea.
        Entero(Modulo, CaptacionesDiasDeGraciaDeCuotaContractual,
            "Días calendario que una cuota del ahorro contractual puede pagarse después de su vencimiento sin contar como atrasada para la tolerancia de la "
            + "línea (el descuento por nómina o por pagaduría llega unos días después del fin del período).", "5", K9, General, Linea) with
        {
            Minimo = 0m, PendienteDeValidar = "Defecto de 5 días (research §19): cada cooperativa lo ajusta a su reglamento de ahorro contractual.",
        },
        Entero(Modulo, CaptacionesDiasParaNoReclamados,
            "Días sin cobrar desde los que lo devuelto pasa a no reclamados. Vacío: no se traslada solo.", "", K10, General) with
        {
            AdmiteVacio = true, PendienteDeValidar = "Sin valor: lo fija el reglamento (FR-081).",
        },
        Texto(FallecimientoSoportesDeEntregaSinSucesion,
            "Soportes que exige la entrega de depósitos sin juicio de sucesión, en JSON [soporte, …].",
            string.Empty, K10, patron: @"(?s)^\s*\[.*\]\s*$", admiteVacio: true) with
        {
            PendienteDeValidar = "Sin valor (pregunta P6): lo fija el reglamento (EOSF art. 127, alcance a validar).",
        },
        SiNo(Modulo, CuentasAbandonadasTrasladoHabilitado,
            "Los saldos de cuentas abandonadas se trasladan según la norma. En «false» no se trasladan.", false, K10, General) with
        {
            PendienteDeValidar = "Defecto P7 de la spec (Ley 1777 de 2016 a validar): la contadora confirma si aplica a la cooperativa.",
        },
    ];

    private static DefinicionDeParametro Texto(string clave, string descripcion, string defecto, EntregaDeModulo desde,
        string? patron = null, bool admiteVacio = false) => new()
        {
            Modulo = Modulo, Clave = clave, Descripcion = descripcion, Tipo = TipoDeParametro.Text, DefectoSeguro = defecto,
            Patron = patron, AdmiteVacio = admiteVacio, DisponibleDesde = desde, AmbitosAdmitidos = [General],
        };
}
