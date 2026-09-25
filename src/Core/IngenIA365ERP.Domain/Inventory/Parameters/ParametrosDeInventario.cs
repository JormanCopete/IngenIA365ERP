using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;
using static IngenIA365ERP.Domain.Common.Parametros.CatalogoDeParametros;
using static IngenIA365ERP.Domain.Common.Parametros.EntregaDelComercio;

namespace IngenIA365ERP.Domain.Inventory.Parameters;

/// <summary>
/// Catálogo cerrado de las claves <c>INV</c> (feature 012, T21, T068; data-model §4.2; decisiones-transversales
/// §2.8). Cada clave con sus valores, su <b>defecto seguro</b>, sus ámbitos, si se sella al confirmar y la entrega
/// desde la que tiene efecto. Los defectos son configurables con vigencia, no valores legales fijos: por eso el
/// archivo está exceptuado de <c>ElComercioNoTieneValoresLegalesFijos</c>.
/// </summary>
public static class ParametrosDeInventario
{
    public const string Modulo = "INV";

    /// <summary>Permiso de las claves <c>Costeo.*</c>, además de <c>Inventory.Parameters.Manage</c>.</summary>
    public const string PermisoDeCosteo = "Inventory.Costing.Manage";

    public const string CosteoMetodo = "Costeo.Metodo";
    public const string CosteoAmbito = "Costeo.Ambito";
    public const string CosteoRetroactivosPermitidos = "Costeo.RetroactivosPermitidos";
    public const string CosteoRetroactivosDiasMaximos = "Costeo.RetroactivosDiasMaximos";
    public const string ExistenciasStockNegativoPermitido = "Existencias.StockNegativoPermitido";
    public const string RedondeoMontos = "Redondeo.Montos";
    public const string RedondeoResiduo = "Redondeo.Residuo";
    public const string ConteoBloquearMovimientos = "Conteo.BloquearMovimientos";
    public const string ConteoFechaDelAjuste = "Conteo.FechaDelAjuste";
    public const string ConteoToleranciaReconteoPorcentaje = "Conteo.ToleranciaReconteoPorcentaje";
    public const string ConteoToleranciaReconteoUnidades = "Conteo.ToleranciaReconteoUnidades";
    public const string ComprasDiasAlertaEventosRadian = "Compras.DiasAlertaEventosRadian";
    public const string ComprasToleranciaCantidadPorcentaje = "Compras.ToleranciaCantidadPorcentaje";
    public const string ComprasToleranciaCantidadValor = "Compras.ToleranciaCantidadValor";
    public const string ComprasToleranciaPrecioPorcentaje = "Compras.ToleranciaPrecioPorcentaje";
    public const string ComprasToleranciaPrecioValor = "Compras.ToleranciaPrecioValor";
    public const string ComprasReglaDeTolerancia = "Compras.ReglaDeTolerancia";
    public const string ContabilidadModoDePaso = "Contabilidad.ModoDePaso";
    public const string ContabilidadGranularidad = "Contabilidad.Granularidad";
    public const string ContabilidadDisparadorDeLote = "Contabilidad.DisparadorDeLote";
    public const string ContabilidadHoraDeLote = "Contabilidad.HoraDeLote";
    public const string ContabilidadPoliticaSinRespuesta = "Contabilidad.PoliticaSinRespuesta";
    public const string ContabilidadValidacionPreviaSegundos = "Contabilidad.ValidacionPreviaSegundos";
    public const string VentasBajoCosto = "Ventas.BajoCosto";
    public const string VentasPersonaInactivaDeContado = "Ventas.PersonaInactivaDeContado";
    public const string VentasLoteVencido = "Ventas.LoteVencido";
    public const string VentasReservaDiasVencimiento = "Ventas.ReservaDiasVencimiento";
    public const string VentasRemisionDiasMaximosSinFacturar = "Ventas.RemisionDiasMaximosSinFacturar";
    public const string CajaBaseModo = "Caja.BaseModo";
    public const string CajaTratamientoFaltante = "Caja.TratamientoFaltante";
    public const string CajaArqueoCiego = "Caja.ArqueoCiego";
    public const string CajaUnaSesionPorCajero = "Caja.UnaSesionPorCajero";
    public const string CarteraCuentaPorCobrarRegistradaPor = "Cartera.CuentaPorCobrarRegistradaPor";
    public const string CarteraIntegracionHabilitadaDesde = "Cartera.IntegracionHabilitadaDesde";
    public const string CarteraPoliticaSinRespuesta = "Cartera.PoliticaSinRespuesta";
    public const string CarteraConsultaSegundos = "Cartera.ConsultaSegundos";
    public const string InformesDeterioroPorcentajeGastosVenta = "Informes.DeterioroPorcentajeGastosVenta";
    public const string InformesDiasSinMovimiento = "Informes.DiasSinMovimiento";
    public const string InformesDiasProximoAVencer = "Informes.DiasProximoAVencer";
    public const string InformesUmbralesAbc = "Informes.UmbralesAbc";
    public const string InformesTopeFaltantesPorcentaje = "Informes.TopeFaltantesPorcentaje";

    private const ParameterScopeKind General = ParameterScopeKind.None;
    private const ParameterScopeKind Bodega = ParameterScopeKind.Warehouse;
    private const ParameterScopeKind TipoDeDocumento = ParameterScopeKind.DocumentType;
    private const ParameterScopeKind Punto = ParameterScopeKind.PointOfSale;
    private const ParameterScopeKind Caja = ParameterScopeKind.CashRegister;
    private const ParameterScopeKind TipoDeTercero = ParameterScopeKind.ThirdPartyKind;

    public static IReadOnlyList<DefinicionDeParametro> Definiciones { get; } =
    [
        // ---------------------------------------------------------------------------------- costeo --
        Eleccion(Modulo, CosteoMetodo, "Método de costeo (sólo desde el primer día de un período abierto sin movimientos posteriores).",
            ["PromedioPonderado", "Peps"], "PromedioPonderado", I1) with
        {
            PermisoAdicional = PermisoDeCosteo,
            ValoresDesde = new Dictionary<string, EntregaDelComercio> { ["Peps"] = I5 },
        },
        Eleccion(Modulo, CosteoAmbito, "Ámbito del costo: uno por cooperativa o uno por bodega (misma regla que el método).",
            ["Cooperativa", "Bodega"], "Cooperativa", I1) with { PermisoAdicional = PermisoDeCosteo },
        SiNo(Modulo, CosteoRetroactivosPermitidos, "Se admiten movimientos con fecha anterior al último costeado (recalculan el costo).",
            false, I5) with { PermisoAdicional = PermisoDeCosteo },
        Entero(Modulo, CosteoRetroactivosDiasMaximos, "Días hacia atrás que admite un movimiento retroactivo.",
            "0", I5) with { PermisoAdicional = PermisoDeCosteo },

        // ------------------------------------------------------------------------------ existencias --
        SiNo(Modulo, ExistenciasStockNegativoPermitido, "Se permite dejar existencias negativas (general, con excepción por bodega).",
            false, I1, General, Bodega),

        // --------------------------------------------------------------------------------- redondeo --
        Eleccion(Modulo, RedondeoMontos, "Redondeo de los montos en pesos.", ["Centavo", "Peso"], "Centavo", I1),
        Eleccion(Modulo, RedondeoResiduo, "Dónde queda el residuo del redondeo.", ["MayorValor", "UltimaLinea"], "MayorValor", I1),

        // ----------------------------------------------------------------------------------- conteo --
        SiNo(Modulo, ConteoBloquearMovimientos, "El conteo físico bloquea los movimientos de la bodega mientras dura.",
            true, I1, General, Bodega),
        Eleccion(Modulo, ConteoFechaDelAjuste, "Fecha del ajuste de un conteo: la de la foto o la de la aprobación.",
            ["Foto", "Aprobacion"], "Foto", I1),
        NumeroDecimal(Modulo, ConteoToleranciaReconteoPorcentaje, "Diferencia porcentual desde la que se pide reconteo.",
            "0", I1, General, Bodega),
        NumeroDecimal(Modulo, ConteoToleranciaReconteoUnidades, "Diferencia en unidades desde la que se pide reconteo.",
            "0", I1, General, Bodega),

        // ---------------------------------------------------------------------------------- compras --
        Entero(Modulo, ComprasDiasAlertaEventosRadian, "Días antes del plazo en que se alerta por eventos RADIAN pendientes.", "3", I1),
        NumeroDecimal(Modulo, ComprasToleranciaCantidadPorcentaje, "Tolerancia porcentual de cantidad en el cruce a tres vías.", "0", I5),
        NumeroDecimal(Modulo, ComprasToleranciaCantidadValor, "Tolerancia de cantidad en el cruce a tres vías.", "0", I5),
        NumeroDecimal(Modulo, ComprasToleranciaPrecioPorcentaje, "Tolerancia porcentual de precio en el cruce a tres vías.", "0", I5),
        NumeroDecimal(Modulo, ComprasToleranciaPrecioValor, "Tolerancia de precio en pesos en el cruce a tres vías.", "0", I5),
        Eleccion(Modulo, ComprasReglaDeTolerancia, "Cómo se combinan las tolerancias de porcentaje y de valor.",
            ["AmbasCondiciones", "CualquieraDeLas"], "AmbasCondiciones", I5),

        // ----------------------------------------------------------------------------- contabilidad --
        Eleccion(Modulo, ContabilidadModoDePaso, "Cómo pasa el documento a contabilidad (por tipo, para toda su cadena).",
            ["EnLinea", "PorLotes", "NoPasa"], "EnLinea", I1, General, TipoDeDocumento) with { SelladoAlConfirmar = true },
        Eleccion(Modulo, ContabilidadGranularidad, "Un comprobante por documento o resumido por lote.",
            ["PorDocumento", "Resumido"], "PorDocumento", I2, General, TipoDeDocumento),
        Eleccion(Modulo, ContabilidadDisparadorDeLote, "Qué dispara el lote contable.",
            ["HoraDiaria", "CierreDeTurno", "CierreDePeriodo"], "HoraDiaria", I2, General, TipoDeDocumento),
        new DefinicionDeParametro
        {
            Modulo = Modulo, Clave = ContabilidadHoraDeLote, Descripcion = "Hora de Colombia del lote diario.",
            Tipo = TipoDeParametro.Time, DefectoSeguro = "23:00", DisponibleDesde = I2, AmbitosAdmitidos = [General, TipoDeDocumento],
        },
        Eleccion(Modulo, ContabilidadPoliticaSinRespuesta, "Qué hacer si la validación contable previa no responde.",
            ["ConfirmarConPendiente", "Bloquear"], "ConfirmarConPendiente", I2),
        Entero(Modulo, ContabilidadValidacionPreviaSegundos, "Segundos que espera la validación contable previa.", "3", I2),

        // ----------------------------------------------------------------------------------- ventas --
        Eleccion(Modulo, VentasBajoCosto, "Qué hacer con una venta por debajo del costo.", ["Alertar", "Bloquear"], "Alertar", I3),
        Eleccion(Modulo, VentasPersonaInactivaDeContado, "Venta de contado a una persona inactiva.", ["Permitir", "Bloquear"], "Permitir", I3),
        Eleccion(Modulo, VentasLoteVencido, "Venta de un lote vencido.", ["Bloquear", "Advertir"], "Bloquear", I6),
        Entero(Modulo, VentasReservaDiasVencimiento, "Días de vigencia de una reserva.", "15", I6),
        Entero(Modulo, VentasRemisionDiasMaximosSinFacturar, "Días máximos de una remisión sin facturar.", "30", I6),

        // ------------------------------------------------------------------------------------- caja --
        Eleccion(Modulo, CajaBaseModo, "Base de la caja: fondo fijo o base del día.", ["FondoFijo", "BaseDelDia"], "FondoFijo", I3, General, Caja),
        Eleccion(Modulo, CajaTratamientoFaltante, "Tratamiento del faltante de arqueo.", ["Gasto", "CargoAlCajero"], "Gasto", I3, General, Punto),
        SiNo(Modulo, CajaArqueoCiego, "El cajero cuenta sin ver el saldo esperado.", false, I3, General, Punto),
        SiNo(Modulo, CajaUnaSesionPorCajero, "Un cajero sólo puede tener una sesión de caja abierta.", true, I3),

        // ---------------------------------------------------------------------------------- cartera --
        Eleccion(Modulo, CarteraCuentaPorCobrarRegistradaPor, "Quién registra la cuenta por cobrar de una venta a crédito.",
            ["Contabilidad", "Cartera"], "Contabilidad", I3) with { SelladoAlConfirmar = true },
        new DefinicionDeParametro
        {
            Modulo = Modulo, Clave = CarteraIntegracionHabilitadaDesde,
            Descripcion = "Fecha desde la que la integración con Cartera está habilitada (vacío = pendiente).",
            Tipo = TipoDeParametro.Date, DefectoSeguro = string.Empty, AdmiteVacio = true, DisponibleDesde = IC,
        },
        Eleccion(Modulo, CarteraPoliticaSinRespuesta, "Qué hacer si Cartera no responde al evaluar un crédito.",
            ["Bloquear", "PermitirConAprobacion"], "Bloquear", IC, General, TipoDeTercero),
        Entero(Modulo, CarteraConsultaSegundos, "Segundos que espera la consulta a Cartera.", "5", IC),

        // --------------------------------------------------------------------------------- informes --
        NumeroDecimal(Modulo, InformesDeterioroPorcentajeGastosVenta, "Gastos de venta estimados para el deterioro, como fracción.",
            "0", I3) with { Maximo = 1 },
        Entero(Modulo, InformesDiasSinMovimiento, "Días sin movimiento para el informe de inventario quieto.", "90", I6),
        Entero(Modulo, InformesDiasProximoAVencer, "Días para considerar un lote próximo a vencer.", "30", I6),
        new DefinicionDeParametro
        {
            Modulo = Modulo, Clave = InformesUmbralesAbc, Descripcion = "Umbrales A/B/C del análisis ABC, en porcentaje.",
            Tipo = TipoDeParametro.Text, DefectoSeguro = "80/15/5", Patron = @"^\d{1,3}/\d{1,3}/\d{1,3}$", DisponibleDesde = I6,
        },
        NumeroDecimal(Modulo, InformesTopeFaltantesPorcentaje, "Tope de faltantes y mermas del informe, como fracción, con su norma o acta.",
            "0", I6) with { Maximo = 1, ExigeFuenteLegal = true },
    ];
}
