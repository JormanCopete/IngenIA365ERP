using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Audit;

namespace IngenIA365ERP.Application.Lending.Deposits.Lines;

/// <summary>
/// Las plantillas de importación de líneas de captación y de aportes (feature 014, respuesta 67 B del dueño, E251; como la de líneas de crédito).
/// Cada fila de <c>Lineas</c> es una versión (llave <c>codigo</c> + <c>vigenteDesde</c>) con <b>las mismas reglas que el alta unitaria</b>; en la de
/// captación, la hoja <c>Tramos</c> cuelga de una versión y la reemplaza entera. Los cargos y la tasa variable se configuran en pantalla. (nuevo)
/// </summary>
public static class PlantillasDeLineasDeCaptacion
{
    public const string CaptacionClave = "lending.deposit-lines";
    public const string AportesClave = "lending.contribution-lines";
    public const string HojaLineas = "Lineas";
    public const string HojaTramos = "Tramos";

    public static class C
    {
        public const string Codigo = "codigo";
        public const string VigenteDesde = "vigenteDesde";
        public const string Nombre = "nombre";
        public const string Clase = "clase";
        public const string Activa = "activa";
        public const string Motivo = "motivo";
        public const string ExigeAsociado = "exigeAsociado";
        public const string AdmiteCotitulares = "admiteCotitulares";
        public const string AdmiteMenores = "admiteMenores";
        public const string AdmitePersonasJuridicas = "admitePersonasJuridicas";
        public const string AdmiteRetirosParciales = "admiteRetirosParciales";
        public const string TasaEA = "tasaEA";
        public const string ModoDeTramos = "modoDeTramos";
        public const string TasaPactada = "declaraTasaPactada";
        public const string AdmiteTasaNegociada = "admiteTasaNegociada";
        public const string AvisoDeCambioDeTasaDias = "avisoDeCambioDeTasaDias";
        public const string MetodoDeSaldo = "metodoDeSaldo";
        public const string MinimoApertura = "minimoApertura";
        public const string MinimoMantener = "minimoMantener";
        public const string MinimoParaInteres = "minimoParaInteres";
        public const string ConvencionDeDias = "convencionDeDias";
        public const string PeriodicidadDeLiquidacion = "periodicidadDeLiquidacion";
        public const string DiaDeLiquidacion = "diaDeLiquidacion";
        public const string AplicaGmf = "aplicaGmf";
        public const string ClaveDeRetencion = "claveDeRetencion";
        public const string RetiroMaximoPorOperacion = "retiroMaximoPorOperacion";
        public const string RetiroMaximoPorDia = "retiroMaximoPorDia";
        public const string DiasDeInactividad = "diasDeInactividad";
        public const string PlazoMinimoDias = "plazoMinimoDias";
        public const string PlazoMaximoDias = "plazoMaximoDias";
        public const string PeriodicidadesDeInteres = "periodicidadesDeInteres";
        public const string ProrrogaAutomatica = "prorrogaAutomatica";
        public const string MaximoDeProrrogas = "maximoDeProrrogas";
        public const string ReglaDeTasaEnProrroga = "reglaDeTasaEnProrroga";
        public const string DiasHabilesDeGracia = "diasHabilesDeGracia";
        public const string LineaALaVista = "lineaALaVista";
        public const string RedencionAnticipada = "redencionAnticipada";
        public const string ModoDePenalidad = "modoDePenalidad";
        public const string PenalidadEA = "penalidadEA";
        public const string DescuentoDePenalidad = "descuentoDePenalidad";
        public const string DiasMinimosParaRedimir = "diasMinimosParaRedimir";
        public const string PeriodicidadDeCuota = "periodicidadDeCuota";
        public const string CuotaMinima = "cuotaMinima";
        public const string PlazoMesesDesde = "plazoMesesDesde";
        public const string PlazoMesesHasta = "plazoMesesHasta";
        public const string TasaDeBonificacionEA = "tasaDeBonificacionEA";
        public const string TasaDeIncumplimientoEA = "tasaDeIncumplimientoEA";
        public const string CuotasDeTolerancia = "cuotasDeTolerancia";
        public const string EfectoDelIncumplimiento = "efectoDelIncumplimiento";
        public const string RetirableSoloAlRetiro = "retirableSoloAlRetiro";
        // Tramos.
        public const string SaldoDesde = "saldoDesde";
        public const string SaldoHasta = "saldoHasta";
        public const string PlazoDesdeDias = "plazoDesdeDias";
        public const string PlazoHastaDias = "plazoHastaDias";
        public const string MontoDesde = "montoDesde";
        public const string MontoHasta = "montoHasta";
        // Aportes.
        public const string MinimoModo = "minimoModo";
        public const string MinimoValor = "minimoValor";
        public const string CuotaModo = "cuotaModo";
        public const string CuotaValor = "cuotaValor";
        public const string CuotaRedondeo = "cuotaRedondeo";
        public const string CuotaPeriodicidad = "cuotaPeriodicidad";
        public const string CuotaDiaDeVencimiento = "cuotaDiaDeVencimiento";
        public const string MinimoPrimero = "minimoPrimero";
        public const string LineaDeAhorroPermanente = "lineaDeAhorroPermanente";
        public const string FraccionAAhorroPermanente = "fraccionAAhorroPermanente";
        public const string AdmiteExtraordinarios = "admiteExtraordinarios";
        public const string ParticipacionMaxima = "participacionMaxima";
        public const string AccionSobreExcedente = "accionSobreExcedente";
        public const string AdmiteTraspasoEntreAsociados = "admiteTraspasoEntreAsociados";
        public const string DiasDeDevolucion = "diasDeDevolucion";
        public const string AbsorbePerdidas = "absorbePerdidas";
    }

    private static ColumnaDePlantilla Col(string n, TipoDeValor t, string? reglas = null, string? ejemplo = null, bool obligatoria = false, int? largo = null) =>
        new(n, t, obligatoria, largo, Reglas: reglas, Ejemplo: ejemplo);

    private static ColumnaDePlantilla[] Comunes() =>
    [
        Col(C.Codigo, TipoDeValor.Codigo, "llave; la nomenclatura de la cooperativa, no cambia", "AHORRO", true, 10),
        Col(C.VigenteDesde, TipoDeValor.Fecha, "llave; desde cuándo rige la versión (yyyy-MM-dd); antes de activar las captaciones puede ser una fecha pasada", "2026-12-01", true),
        Col(C.Nombre, TipoDeValor.Texto, null, "Ahorro a la vista", true, 120),
        Col(C.Activa, TipoDeValor.SiNo, "«no» deja la línea sin activar; vacío = sí", "sí"),
        Col(C.Motivo, TipoDeValor.Texto, "motivo de la versión; vacío = el de la importación", "Tarifas 2027", false, 300),
    ];

    /// <summary>Plantilla de líneas de captación: <c>Lineas</c> (una versión por fila) y <c>Tramos</c> (de tasa, opcional).</summary>
    public static DefinicionDePlantilla Captacion { get; } = new(CaptacionClave, "Líneas de captación", ModuloDeAuditoria.Lending,
    [
        new HojaDePlantilla(HojaLineas,
        [
            .. Comunes(),
            Col(C.Clase, TipoDeValor.Enumeracion, "Demand (a la vista), Contractual, Permanent (permanente) o TermDeposit (CDAT); no cambia", "Demand", true),
            Col(C.ExigeAsociado, TipoDeValor.SiNo, "vacío = sí"),
            Col(C.AdmiteCotitulares, TipoDeValor.SiNo), Col(C.AdmiteMenores, TipoDeValor.SiNo), Col(C.AdmitePersonasJuridicas, TipoDeValor.SiNo),
            Col(C.AdmiteRetirosParciales, TipoDeValor.SiNo),
            Col(C.TasaEA, TipoDeValor.Porcentaje, "efectiva anual en puntos (2,5 = 2,5 % EA); vacía si la tasa va por tramos", "2,5"),
            Col(C.ModoDeTramos, TipoDeValor.Enumeracion, "WholeBalance o Stepped; con la hoja Tramos"),
            Col(C.TasaPactada, TipoDeValor.SiNo, "el CDAT declara la tasa pactada"),
            Col(C.AdmiteTasaNegociada, TipoDeValor.SiNo),
            Col(C.AvisoDeCambioDeTasaDias, TipoDeValor.Entero, "días de aviso a los titulares; vacío = 0"),
            Col(C.MetodoDeSaldo, TipoDeValor.Enumeracion, "DailyBalance, AverageBalance o MinimumBalance; vacío = DailyBalance"),
            Col(C.MinimoApertura, TipoDeValor.Monto), Col(C.MinimoMantener, TipoDeValor.Monto), Col(C.MinimoParaInteres, TipoDeValor.Monto),
            Col(C.ConvencionDeDias, TipoDeValor.Enumeracion, "Thirty360, Actual365 o Actual360; vacío = Actual365"),
            Col(C.PeriodicidadDeLiquidacion, TipoDeValor.Enumeracion, "Monthly, Quarterly, SemiAnnual, Annual…; vacío = Monthly"),
            Col(C.DiaDeLiquidacion, TipoDeValor.Entero),
            Col(C.AplicaGmf, TipoDeValor.SiNo, "vacío = sí"),
            Col(C.ClaveDeRetencion, TipoDeValor.Texto, "clave de la tarifa de retención; vacío = la de la clase", null, false, 60),
            Col(C.RetiroMaximoPorOperacion, TipoDeValor.Monto), Col(C.RetiroMaximoPorDia, TipoDeValor.Monto),
            Col(C.DiasDeInactividad, TipoDeValor.Entero, "vacío = 360"),
            Col(C.PlazoMinimoDias, TipoDeValor.Entero, "CDAT"), Col(C.PlazoMaximoDias, TipoDeValor.Entero, "CDAT"),
            Col(C.PeriodicidadesDeInteres, TipoDeValor.Texto, "CDAT: lista separada por coma (SinglePayment, Monthly…)", "SinglePayment"),
            Col(C.ProrrogaAutomatica, TipoDeValor.SiNo, "CDAT; vacío = sí"), Col(C.MaximoDeProrrogas, TipoDeValor.Entero, "CDAT"),
            Col(C.ReglaDeTasaEnProrroga, TipoDeValor.Enumeracion, "CDAT: SameRate, CurrentTableRate o Agreed; vacío = CurrentTableRate"),
            Col(C.DiasHabilesDeGracia, TipoDeValor.Entero, "CDAT; vacío = 3"),
            Col(C.LineaALaVista, TipoDeValor.Codigo, "CDAT: código de la línea a la vista a la que pasa al vencer (debe existir)", null, false, 10),
            Col(C.RedencionAnticipada, TipoDeValor.SiNo, "CDAT"),
            Col(C.ModoDePenalidad, TipoDeValor.Enumeracion, "CDAT: PenaltyRate o Discount"),
            Col(C.PenalidadEA, TipoDeValor.Porcentaje), Col(C.DescuentoDePenalidad, TipoDeValor.Porcentaje), Col(C.DiasMinimosParaRedimir, TipoDeValor.Entero),
            Col(C.PeriodicidadDeCuota, TipoDeValor.Enumeracion, "Contractual; vacío = Monthly"), Col(C.CuotaMinima, TipoDeValor.Monto, "Contractual"),
            Col(C.PlazoMesesDesde, TipoDeValor.Entero, "Contractual"), Col(C.PlazoMesesHasta, TipoDeValor.Entero, "Contractual"),
            Col(C.TasaDeBonificacionEA, TipoDeValor.Porcentaje), Col(C.TasaDeIncumplimientoEA, TipoDeValor.Porcentaje), Col(C.CuotasDeTolerancia, TipoDeValor.Entero),
            Col(C.EfectoDelIncumplimiento, TipoDeValor.Enumeracion, "Contractual: BreachRate, MoveToDemand o Cancel; vacío = MoveToDemand"),
            Col(C.RetirableSoloAlRetiro, TipoDeValor.SiNo, "Permanente; vacío = sí"),
        ]),
        new HojaDePlantilla(HojaTramos,
        [
            Col(C.Codigo, TipoDeValor.Codigo, "la línea de la versión", "AHORRO", true, 10),
            Col(C.VigenteDesde, TipoDeValor.Fecha, "la versión a la que pertenece", "2026-12-01", true),
            Col(C.SaldoDesde, TipoDeValor.Monto), Col(C.SaldoHasta, TipoDeValor.Monto),
            Col(C.PlazoDesdeDias, TipoDeValor.Entero), Col(C.PlazoHastaDias, TipoDeValor.Entero),
            Col(C.MontoDesde, TipoDeValor.Monto), Col(C.MontoHasta, TipoDeValor.Monto),
            Col(C.TasaEA, TipoDeValor.Porcentaje, "efectiva anual del tramo, en puntos", "2,5", true),
        ], Obligatoria: false),
    ]);

    /// <summary>Plantilla de la línea de aportes: <c>Lineas</c>, una versión por fila.</summary>
    public static DefinicionDePlantilla Aportes { get; } = new(AportesClave, "Línea de aportes", ModuloDeAuditoria.Lending,
    [
        new HojaDePlantilla(HojaLineas,
        [
            .. Comunes(),
            Col(C.MinimoModo, TipoDeValor.Enumeracion, "FixedAmount, IncomeFraction o MinimumWageFraction; vacío = FixedAmount"),
            Col(C.MinimoValor, TipoDeValor.Costo, "pesos, o fracción (0,05) según el modo; vacío = 0"),
            Col(C.CuotaModo, TipoDeValor.Enumeracion, "FixedAmount, IncomeFraction o MinimumWageFraction; vacío = FixedAmount"),
            Col(C.CuotaValor, TipoDeValor.Costo, "pesos, o fracción según el modo", "45000,5", true),
            Col(C.CuotaRedondeo, TipoDeValor.Monto, "unidad de redondeo; vacío = 1"),
            Col(C.CuotaPeriodicidad, TipoDeValor.Enumeracion, "vacío = Monthly"),
            Col(C.CuotaDiaDeVencimiento, TipoDeValor.Entero),
            Col(C.MinimoPrimero, TipoDeValor.SiNo, "vacío = sí"),
            Col(C.LineaDeAhorroPermanente, TipoDeValor.Codigo, "código de la línea de ahorro permanente que recibe parte de la cuota (debe existir)", null, false, 10),
            Col(C.FraccionAAhorroPermanente, TipoDeValor.Porcentaje, "en puntos, con la línea anterior"),
            Col(C.AdmiteExtraordinarios, TipoDeValor.SiNo),
            Col(C.ParticipacionMaxima, TipoDeValor.Porcentaje, "en puntos; vacío = sin límite propio"),
            Col(C.AccionSobreExcedente, TipoDeValor.Enumeracion, "Reject o RefundByDecision; vacío = Reject"),
            Col(C.AdmiteTraspasoEntreAsociados, TipoDeValor.SiNo),
            Col(C.DiasDeDevolucion, TipoDeValor.Entero, "vacío = 0"),
            Col(C.AbsorbePerdidas, TipoDeValor.SiNo),
        ]),
    ]);
}
