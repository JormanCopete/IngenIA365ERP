using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Treasury;
using static IngenIA365ERP.Domain.Common.Parametros.CatalogoDeParametros;

namespace IngenIA365ERP.Domain.Receipts.Parameters;

/// <summary>
/// Catálogo cerrado de las claves de Recaudo (<c>RCD</c>) y de Tesorería (<c>TES</c>) (feature 015, T0042; data-model §23; FR-009, FR-010,
/// FR-173, FR-178, FR-202; D22, D25, D41). Cada clave con su tipo, valores, <b>defecto</b>, la entrega desde la que existe y, donde data-model
/// dice <b>P</b>, «pendiente de validar» con quién y con qué fuente. Las marcadas <b>sin valor</b> admiten vacío y su defecto es vacío: no se
/// siembran y no rigen hasta que la cooperativa registre una vigencia.
///
/// <para>
/// <b>Sin valores legales en el código</b> (<c>RecaudoYTesoreriaNoTienenValoresLegalesFijos</c>, que exime este archivo por ser donde viven
/// los defectos como dato): los umbrales SARLAFT, la tarifa del GMF, los plazos de cheques y de libranza y los años de conservación son texto
/// configurable con vigencia. Los <b>plazos regulatorios</b> (presentación de cheques, giro de libranza, días sin resultado, extractos sin
/// cargar) se informan y no bloquean salvo la bandera expresa de la cooperativa (<see cref="ChequesBloquearFueraDePlazo"/>); lo que la norma
/// impone —listas vinculantes y fondo de liquidez— <b>no tiene parámetro</b> para dejar de bloquear (FR-010).
/// </para>
///
/// <para>
/// Tres desviaciones de data-model §23, anotadas en decisiones-transversales §2.7: las claves «de plataforma» (<c>CuentasDestino.*</c>) y la de
/// Contabilidad (<c>Contabilidad.CierreExigeConciliaciones</c>, T1) viven en el módulo <c>TES</c>, que es quien las usa, en vez de inventar dos
/// módulos de parámetros sin ruta ni permiso; <c>Gmf.*</c> va sólo en <c>TES</c> en R0/T0 y Recaudo suma en R1 sus propias tarifa, hechos generadores y redondeo para los egresos de caja (la exención de desembolsos no le aplica); y la clave
/// <c>Contabilidad.ModoDePaso</c> va con el defecto de cada módulo (recibos de caja <c>PorLotes</c> en <c>RCD</c>, <c>EnLinea</c> en
/// <c>TES</c>) hasta que exista el ámbito por tipo de hecho.
/// </para>
/// </summary>
public static class ParametrosDeRecaudoYTesoreria
{
    public const string ModuloRecaudo = CatalogoDeRecaudo.Modulo;
    public const string ModuloTesoreria = CatalogoDeTesoreria.Modulo;

    private const string PendienteDueno = "Defecto de data-model §23 pendiente de validar por el dueño (feature 015).";
    private const string PendienteCumplimiento = "Defecto de data-model §23 pendiente de validar por el oficial de cumplimiento (P7).";
    private const string PendienteContadora = "Defecto de data-model §23 pendiente de validar por la contadora (P8, P12).";
    private const string PendienteTributarista = "Defecto de data-model §23 pendiente de validar por el tributarista (P10, P19).";

    // --------------------------------------------------------------------------------------------------------- Recaudo (RCD) --
    public const string RecaudoHoraDeCorte = "Recaudo.HoraDeCorte";
    public const string CajaArqueoCiego = "Caja.ArqueoCiego";
    public const string CajaBaseModo = "Caja.BaseModo";
    public const string CajaUnaSesionPorCajero = "Caja.UnaSesionPorCajero";
    public const string CajaDobleCustodia = "Caja.DobleCustodia";
    public const string CajaTratamientoFaltante = "Caja.TratamientoFaltante";
    public const string CajaSobrantesDiasAntesDeIngreso = "Caja.Sobrantes.DiasAntesDeIngreso";
    public const string CajaAdmiteCheque = "Caja.AdmiteCheque";
    public const string CajaAdmiteChequeDeTercero = "Caja.AdmiteChequeDeTercero";
    public const string CajaAdmitePosfechadoEnCustodia = "Caja.AdmitePosfechadoEnCustodia";
    public const string CajaCobroSoloDeSuOficina = "Caja.CobroSoloDeSuOficina";
    public const string CajaFechaValorAnteriorMaxDias = "Caja.FechaValorAnterior.MaxDias";
    /// <summary>Feature 015, R1 (fase 26; FR-055) (nuevo): los egresos varios por la caja, apagados por defecto.</summary>
    public const string CajaEgresosVarios = "Caja.EgresosVarios";
    /// <summary>Feature 015, R1 (fase 27; FR-067, FR-069; D11, D15) (nuevos): la aplicación al cargar lo de referencia exacta y la consignación reportada.</summary>
    public const string IdentificacionAplicarAlCargar = "Identificacion.AplicarAlCargar";
    public const string ConsignacionReportadaModo = "ConsignacionReportada.Modo";
    public const string ConsignacionReportadaDiasParaVerificar = "ConsignacionReportada.DiasParaVerificar";
    public const string ConsignacionReportadaDiasDeVentana = "ConsignacionReportada.DiasDeVentana";
    public const string ReciboOrdenDeImputacion = "Recibo.OrdenDeImputacion";
    public const string ChequesPlazoPresentacionMismaPlaza = "Cheques.PlazoPresentacionMismaPlaza";
    public const string ChequesPlazoPresentacionOtraPlaza = "Cheques.PlazoPresentacionOtraPlaza";
    public const string ChequesBloquearFueraDePlazo = "Cheques.BloquearFueraDePlazo";
    public const string ChequesCargoPorDevolucion = "Cheques.CargoPorDevolucion";
    public const string PartidasEscalonesDias = "Partidas.EscalonesDias";
    public const string PartidasDestinoVencido = "Partidas.DestinoVencido";
    public const string ExtractosDiasHabilesSinCarga = "Extractos.DiasHabilesSinCarga";
    public const string LibranzaDiasHabilesGiro = "Libranza.DiasHabilesGiro";
    public const string SarlaftUmbralDeclaracionOrigenFondos = "Sarlaft.UmbralDeclaracionOrigenFondos";
    public const string SarlaftUmbralTransaccionIndividual = "Sarlaft.UmbralTransaccionIndividual";
    public const string SarlaftUmbralTransaccionesMultiples = "Sarlaft.UmbralTransaccionesMultiples";
    public const string SarlaftFraccionamiento = "Sarlaft.Fraccionamiento";
    public const string SarlaftReporteEstado = "Sarlaft.Reporte.Estado";
    public const string SarlaftReporteAlcance = "Sarlaft.Reporte.Alcance";
    public const string SarlaftReporteExigeAusencia = "Sarlaft.Reporte.ExigeAusencia";
    public const string SarlaftPagadorTerceroFrecuenteTitulares = "Sarlaft.PagadorTerceroFrecuente.Titulares";
    public const string SarlaftConsignanteRepetidoPartidas = "Sarlaft.ConsignanteRepetido.Partidas";

    // ------------------------------------------------------------------------------------------------------ comunes (RCD, TES) --
    public const string SarlaftListasNoVinculantesEfecto = "Sarlaft.ListasNoVinculantes.Efecto";
    public const string ContabilidadFaltaDeRegla = "Contabilidad.FaltaDeRegla";
    public const string ContabilidadModoDePaso = "Contabilidad.ModoDePaso";
    public const string ConservacionAnios = "Conservacion.Anios";

    // ------------------------------------------------------------------------------------------------------- Tesorería (TES) --
    public const string GirosDiasSinResultado = "Giros.DiasSinResultado";
    public const string ChequesDiasSinCobrar = "Cheques.DiasSinCobrar";
    public const string ChequesDiasACuentaPorPagar = "Cheques.DiasACuentaPorPagar";
    public const string LotesTerceraPersonaParaResultado = "Lotes.TerceraPersonaParaResultado";
    public const string CuentasDestinoDiasDeEspera = "CuentasDestino.DiasDeEspera";
    public const string CuentasDestinoDiasDeMarca = "CuentasDestino.DiasDeMarca";
    public const string RemesasDiasParaConfirmar = "Remesas.DiasParaConfirmar";
    public const string ConciliacionDiasDeHolgura = "Conciliacion.DiasDeHolgura";
    public const string ConciliacionDiasPartidaAntigua = "Conciliacion.DiasPartidaAntigua";
    public const string ContabilidadCierreExigeConciliaciones = "Contabilidad.CierreExigeConciliaciones";
    public const string GmfTarifa = "Gmf.Tarifa";
    public const string GmfHechosGeneradoresDeEgresos = "Gmf.HechosGeneradoresDeEgresos";
    public const string GmfExencionDesembolsos = "Gmf.ExencionDesembolsos";
    public const string GmfRedondeo = "Gmf.Redondeo";

    // Feature 015, R2 (fase 38): la referencia de pago y la conciliación de los convenios.
    public const string ReferenciasDigitoDeVerificacion = "Referencias.DigitoDeVerificacion";
    public const string ConveniosToleranciaDeConciliacion = "Convenios.ToleranciaDeConciliacion";

    // Feature 015, T2 (fases 41 a 44): notas bancarias, liquidez y adquirentes.
    public const string NotasBancariasExigenAprobacion = "NotasBancarias.ExigenAprobacion";
    public const string LiquidezSemanasDeProyeccion = "Liquidez.SemanasDeProyeccion";
    public const string AdquirentesToleranciaDeConciliacion = "Adquirentes.ToleranciaDeConciliacion";

    /// <summary>El piso de <see cref="ConservacionAnios"/>: nunca menos de cinco años (Principio X; data-model §23).</summary>
    public const int PisoDeConservacion = 5;

    private static readonly EntregaDeModulo R0 = EntregaDeRecaudo.R0;
    private static readonly EntregaDeModulo R1 = EntregaDeRecaudo.R1;
    private static readonly EntregaDeModulo T0 = EntregaDeTesoreria.T0;
    private static readonly EntregaDeModulo T1 = EntregaDeTesoreria.T1;
    private static readonly EntregaDeModulo R2 = EntregaDeRecaudo.R2;
    private static readonly EntregaDeModulo T2 = EntregaDeTesoreria.T2;

    private static DefinicionDeParametro Pendiente(DefinicionDeParametro d, string quien) => d with { PendienteDeValidar = quien };

    private static DefinicionDeParametro SinValor(string modulo, string clave, string descripcion, EntregaDeModulo desde, string quien) =>
        Entero(modulo, clave, descripcion, string.Empty, desde) with { AdmiteVacio = true, PendienteDeValidar = quien };

    public static IReadOnlyList<DefinicionDeParametro> Definiciones { get; } = Construir();

    private static List<DefinicionDeParametro> Construir()
    {
        var lista = new List<DefinicionDeParametro>
        {
            // ---- caja y recibo (RCD) ----
            new()
            {
                Modulo = ModuloRecaudo, Clave = RecaudoHoraDeCorte, Tipo = TipoDeParametro.Time, DefectoSeguro = "18:00", DisponibleDesde = R0,
                Descripcion = "Hora de corte del día de Recaudo (FR-185): tiene que caer antes del ciclo diario de Cartera; se copia a la configuración al activar.",
            },
            SiNo(ModuloRecaudo, CajaArqueoCiego, "El cajero cuenta sin ver lo esperado (D6).", true, R0),
            Eleccion(ModuloRecaudo, CajaBaseModo, "Cómo se lleva la base de la caja: base diaria o fondo fijo (D7).", ["DailyBase", "FixedFund"], "DailyBase", R0),
            SiNo(ModuloRecaudo, CajaUnaSesionPorCajero, "Un cajero tiene a lo sumo una sesión abierta (D8).", true, R0),
            SiNo(ModuloRecaudo, CajaDobleCustodia, "La bóveda exige dos custodios (D8).", true, R0),
            Eleccion(ModuloRecaudo, CajaTratamientoFaltante, "Qué se hace con un faltante aprobado: cuenta por cobrar al cajero o gasto (D34).",
                ["CashierReceivable", "Expense"], "CashierReceivable", R0),
            SinValor(ModuloRecaudo, CajaSobrantesDiasAntesDeIngreso, "Días que un sobrante espera antes de pasar a ingreso; sin valor, no pasa solo (P8).", R0, PendienteContadora),
            SiNo(ModuloRecaudo, CajaAdmiteCheque, "La caja recibe cheques (D21, D37; P2).", false, R0),
            SiNo(ModuloRecaudo, CajaAdmiteChequeDeTercero, "La caja recibe cheques de terceros (D21).", true, R0),
            SiNo(ModuloRecaudo, CajaAdmitePosfechadoEnCustodia, "La caja recibe cheques posfechados en custodia (D37).", false, R0),
            SiNo(ModuloRecaudo, CajaCobroSoloDeSuOficina, "Un punto cobra sólo las obligaciones de su oficina (D36).", false, R0),
            Entero(ModuloRecaudo, CajaFechaValorAnteriorMaxDias, "Días hacia atrás que admite la fecha valor de un recibo de caja (FR-192).", "0", R0),
            Eleccion(ModuloRecaudo, ReciboOrdenDeImputacion, "Orden en que los medios se imputan a las aplicaciones: inmediatos primero y el cheque al final (D35).",
                ["InmediatosPrimeroChequeAlFinal", "ElegidoEnCaja"], "InmediatosPrimeroChequeAlFinal", R0),

            // ---- cheques, partidas, extractos, libranza (RCD) ----
            Pendiente(Entero(ModuloRecaudo, ChequesPlazoPresentacionMismaPlaza, "Días para presentar un cheque de la misma plaza (C. Co. art. 718); informativo.", "15", R0), PendienteDueno),
            Pendiente(Entero(ModuloRecaudo, ChequesPlazoPresentacionOtraPlaza, "Meses para presentar un cheque de otra plaza (C. Co. art. 718); informativo.", "1", R0), PendienteDueno),
            Pendiente(SiNo(ModuloRecaudo, ChequesBloquearFueraDePlazo, "Bloquear el cheque fuera de su plazo de presentación (sin esta bandera, sólo avisa).", false, R0), PendienteDueno),
            Pendiente(SiNo(ModuloRecaudo, ChequesCargoPorDevolucion, "Cobrar el cargo del cheque devuelto (C. Co. art. 731).", false, R0), PendienteDueno),
            new()
            {
                Modulo = ModuloRecaudo, Clave = PartidasEscalonesDias, Tipo = TipoDeParametro.Text, DefectoSeguro = "30,60,90", DisponibleDesde = R0,
                Patron = @"^\d{1,4}(,\d{1,4})*$", PendienteDeValidar = PendienteContadora,
                Descripcion = "Escalones de antigüedad de las partidas por identificar, en días separados por coma (D10).",
            },
            Pendiente(Eleccion(ModuloRecaudo, PartidasDestinoVencido, "Qué pasa con una partida vencida: sigue como pasivo o pasa a ingreso con aprobación (D10, P8).",
                ["SiguenComoPasivo", "Ingreso"], "SiguenComoPasivo", R0), PendienteContadora),
            Entero(ModuloRecaudo, ExtractosDiasHabilesSinCarga, "Días hábiles sin extracto cargado antes de avisar (FR-196); informativo.", "2", R0),
            Pendiente(Entero(ModuloRecaudo, LibranzaDiasHabilesGiro, "Días hábiles que tiene la pagaduría para girar (Ley 1527 art. 6); informativo.", "3", R1), PendienteDueno),

            // ---- egresos de caja y su GMF (RCD, R1, fase 26) ----
            SiNo(ModuloRecaudo, CajaEgresosVarios, "La caja entrega egresos varios por concepto (FR-055); apagado por defecto y, encendido, con aprobación y el tope del concepto.", false, R1),
            Pendiente(NumeroDecimal(ModuloRecaudo, GmfTarifa, "Tarifa del GMF de los egresos de caja como fracción (ET art. 871).", "0.004", R1) with { Maximo = 1m },
                PendienteTributarista),
            Pendiente(Eleccion(ModuloRecaudo, GmfHechosGeneradoresDeEgresos,
                "Qué egresos de caja causan GMF: las devoluciones de saldos a favor (DevolucionesYPartidas), también los varios (Todos) o ninguno; nunca lo que el origen ya liquidó (FR-205).",
                ["DevolucionesYPartidas", "Todos", "Ninguno"], "DevolucionesYPartidas", R1), PendienteTributarista),
            Pendiente(Eleccion(ModuloRecaudo, GmfRedondeo, "Redondeo del GMF de los egresos de caja.", ["AlPeso", "SinRedondeo"], "AlPeso", R1), PendienteTributarista),

            // ---- identificación por reglas y consignación reportada (RCD, R1, fase 27) ----
            SiNo(ModuloRecaudo, IdentificacionAplicarAlCargar,
                "Lo propuesto con referencia exacta por una regla marcada se aplica al cargar el extracto, a nombre de quien lo carga (D15); si no, sólo se propone.", false, R1),
            Eleccion(ModuloRecaudo, ConsignacionReportadaModo, "La consignación reportada en ventanilla queda por verificar hasta el extracto o se aplica de inmediato (D11).",
                ["PorVerificar", "AplicarDeInmediato"], "PorVerificar", R1),
            Pendiente(Entero(ModuloRecaudo, ConsignacionReportadaDiasParaVerificar,
                "Días sin encontrar la consignación reportada en el extracto antes de alertar (FR-069); informativo.", "3", R1), PendienteDueno),
            Entero(ModuloRecaudo, ConsignacionReportadaDiasDeVentana,
                "Días antes del movimiento del extracto en que se busca la consignación reportada que lo confirma (FR-067).", "5", R1),

            // ---- SARLAFT (RCD) ----
            Pendiente(NumeroDecimal(ModuloRecaudo, SarlaftUmbralDeclaracionOrigenFondos, "Valor por operación desde el que se exige la declaración de origen de fondos (P7).", "5000000", R0), PendienteCumplimiento),
            Pendiente(NumeroDecimal(ModuloRecaudo, SarlaftUmbralTransaccionIndividual, "Umbral de la transacción individual en efectivo (proyecto de circular de la Supersolidaria, dic-2024).", "5000000", R0), PendienteCumplimiento),
            Pendiente(NumeroDecimal(ModuloRecaudo, SarlaftUmbralTransaccionesMultiples, "Umbral de las transacciones múltiples en efectivo de una persona en el mes calendario.", "30000000", R0), PendienteCumplimiento),
            new()
            {
                Modulo = ModuloRecaudo, Clave = SarlaftFraccionamiento, Tipo = TipoDeParametro.Text, DefectoSeguro = "3;7", DisponibleDesde = R0,
                Patron = @"^\d{1,3};\d{1,3}$", PendienteDeValidar = PendienteCumplimiento,
                Descripcion = "Señal de fraccionamiento: «operaciones;días» bajo el umbral de declaración (3 en 7 días).",
            },
            Pendiente(Eleccion(ModuloRecaudo, SarlaftReporteEstado, "Estado del reporte de transacciones en efectivo por período (la suspensión de 2025 se registra como vigencia).",
                ["Vigente", "Suspendido"], "Vigente", R0), PendienteCumplimiento),
            Pendiente(Eleccion(ModuloRecaudo, SarlaftReporteAlcance, "Qué transacciones entran al reporte.", ["SoloEfectivo", "Todas"], "SoloEfectivo", R0), PendienteCumplimiento),
            Pendiente(SiNo(ModuloRecaudo, SarlaftReporteExigeAusencia, "Se reporta la ausencia de transacciones.", false, R0), PendienteCumplimiento),
            Pendiente(Entero(ModuloRecaudo, SarlaftPagadorTerceroFrecuenteTitulares,
                "Señal de pagador tercero frecuente: titulares distintos para los que una persona entregó efectivo en el mes calendario.", "3", R0) with { Minimo = 1 },
                PendienteCumplimiento),
            Pendiente(Entero(ModuloRecaudo, SarlaftConsignanteRepetidoPartidas,
                "Señal de consignante no identificado repetido: partidas por identificar abiertas del mismo consignante que la levantan.", "2", R0) with { Minimo = 1 },
                PendienteCumplimiento),
        };

        // ---- comunes: una definición por módulo ----
        foreach (var (modulo, desde, modoDePaso) in new[] { (ModuloRecaudo, R0, "PorLotes"), (ModuloTesoreria, T0, "EnLinea") })
        {
            lista.Add(Eleccion(modulo, SarlaftListasNoVinculantesEfecto, "Qué hace una coincidencia en una lista no vinculante: sigue con alerta o retiene (D22). Las vinculantes bloquean siempre.",
                ["Sigue", "Retiene"], "Sigue", desde));
            lista.Add(Eleccion(modulo, ContabilidadFaltaDeRegla, "Sin regla contable para un hecho: bloquear la operación o advertir y seguir (D25).",
                ["Bloquear", "Advertir"], "Bloquear", desde));
            lista.Add(Eleccion(modulo, ContabilidadModoDePaso, "Cómo pasan los hechos a Contabilidad: en línea, por lotes o no pasan (D25).",
                ["EnLinea", "PorLotes", "NoPasa"], modoDePaso, desde));
            lista.Add(Pendiente(Entero(modulo, ConservacionAnios, "Años de conservación de documentos y auditoría (C. Co. art. 60); nunca menos de cinco.", "10", desde)
                with { Minimo = PisoDeConservacion }, PendienteTributarista));
        }

        lista.AddRange(
        [
            // ---- giros, cheques, lotes, cuentas destino, remesas, conciliación (TES) ----
            Pendiente(Entero(ModuloTesoreria, GirosDiasSinResultado, "Días hábiles de una orden enviada sin resultado antes de avisar; informativo.", "3", T0), PendienteDueno),
            Pendiente(Entero(ModuloTesoreria, ChequesDiasSinCobrar, "Días de un cheque girado sin cobrar antes de avisar; informativo.", "30", T0), PendienteDueno),
            Pendiente(Entero(ModuloTesoreria, ChequesDiasACuentaPorPagar, "Días de un cheque sin cobrar antes de proponer pasarlo a cuenta por pagar.", "180", T0), PendienteDueno),
            SiNo(ModuloTesoreria, LotesTerceraPersonaParaResultado, "El resultado de un lote lo registra una tercera persona, distinta de quien lo preparó y quien lo aprobó (D18).", false, T0),
            SinValor(ModuloTesoreria, CuentasDestinoDiasDeEspera, "Días de espera desde la aprobación de una cuenta destino hasta poder girarle; sin valor, sin espera (D40, P4).", T0, PendienteDueno),
            SinValor(ModuloTesoreria, CuentasDestinoDiasDeMarca, "Días en que una cuenta destino recién cambiada lleva la marca de cambio; sin valor, sin marca (D40, P4).", T0, PendienteDueno),
            Entero(ModuloTesoreria, RemesasDiasParaConfirmar, "Días para confirmar en el banco una remesa de efectivo antes de avisar.", "2", T0),
            Entero(ModuloTesoreria, ConciliacionDiasDeHolgura, "Días de holgura en la fecha para el cruce automático de la conciliación (FR-216).", "3", T0),
            Pendiente(Entero(ModuloTesoreria, ConciliacionDiasPartidaAntigua,
                "Días de una partida conciliatoria abierta antes de alertar como antigua (Tesoreria.PartidaConciliatoriaAntigua); informativo.", "30", T0), PendienteDueno),
            SiNo(ModuloTesoreria, ContabilidadCierreExigeConciliaciones, "El cierre del mes contable exige las conciliaciones cerradas (D29); hasta entonces sólo avisa.", false, T1),

            // ---- GMF (TES) ----
            Pendiente(NumeroDecimal(ModuloTesoreria, GmfTarifa, "Tarifa del gravamen a los movimientos financieros como fracción (ET art. 871).", "0.004", T0) with { Maximo = 1m },
                PendienteTributarista),
            Pendiente(Eleccion(ModuloTesoreria, GmfHechosGeneradoresDeEgresos, "Qué egresos generan GMF: devoluciones de saldos a favor y partidas gravadas (DUR 1625 de 2016).",
                ["DevolucionesYPartidas", "Todos", "Ninguno"], "DevolucionesYPartidas", T0), PendienteTributarista),
            Pendiente(SiNo(ModuloTesoreria, GmfExencionDesembolsos, "El desembolso al deudor está exento (ET art. 879).", true, T0), PendienteTributarista),
            Pendiente(Eleccion(ModuloTesoreria, GmfRedondeo, "Redondeo del GMF.", ["AlPeso", "SinRedondeo"], "AlPeso", T0), PendienteTributarista),

            // ---- R2 (fase 38): referencias de pago y convenios (RCD) ----
            Pendiente(Eleccion(ModuloRecaudo, ReferenciasDigitoDeVerificacion,
                "Algoritmo del dígito de verificación de la referencia de pago estable de cada producto (FR-090): sin dígito, módulo 10 o módulo 11.",
                ["None", "Modulo10", "Modulo11"], "Modulo10", R2), PendienteDueno),
            Pendiente(NumeroDecimal(ModuloRecaudo, ConveniosToleranciaDeConciliacion,
                "Diferencia sin explicar (en pesos) que admite la conciliación de un convenio sin la aprobación de otra persona (FR-093); cero, toda diferencia sin explicar se aprueba.",
                "0", R2) with { Minimo = 0 }, PendienteDueno),

            // ---- T2 (fases 41 a 44): notas bancarias, liquidez y adquirentes (TES) ----
            Pendiente(SiNo(ModuloTesoreria, NotasBancariasExigenAprobacion, "Una nota bancaria (GMF, comisión, intereses, impuestos) exige la aprobación de otra persona antes de su hecho (FR-130).",
                true, T2), PendienteDueno),
            Pendiente(Entero(ModuloTesoreria, LiquidezSemanasDeProyeccion, "Semanas que proyecta el flujo de caja (FR-138).", "13", T2) with { Minimo = 1 }, PendienteDueno),
            Pendiente(NumeroDecimal(ModuloTesoreria, AdquirentesToleranciaDeConciliacion,
                "Diferencia sin explicar (en pesos) que admite la conciliación de un adquirente sin la aprobación de otra persona (FR-133).", "0", T2) with { Minimo = 0 },
                PendienteDueno),
        ]);

        return lista;
    }
}
