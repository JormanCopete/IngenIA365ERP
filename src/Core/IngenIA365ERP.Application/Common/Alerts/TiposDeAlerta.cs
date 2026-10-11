using IngenIA365ERP.Domain.Enums.Alerts;

namespace IngenIA365ERP.Application.Common.Alerts;

/// <summary>
/// La definición fija de un tipo de alerta del catálogo cerrado (feature 012, T39; decisiones-transversales §2.13;
/// data-model §22): su módulo, qué la dispara y el defecto con que la siembra <c>AlertTypesSeeder</c>. La cooperativa
/// cambia destinatarios, canales y umbrales por <c>SaveAlertTypeCommand</c>; no inventa tipos. (nuevo)
/// </summary>
/// <param name="UsaDestinatarios">
/// Falso para <c>Aprobaciones.Pendiente</c>: la reciben los titulares del permiso del nivel pendiente, no una lista fija.
/// </param>
/// <param name="Umbrales">Las claves de umbral que admite (vacío: no tiene umbrales propios).</param>
/// <param name="DisponibleDesde">La entrega desde la que la levanta algún proceso (I1…I6, IC; K1…K6b en Cartera).</param>
/// <param name="UmbralesPorDefecto">Feature 013: los valores con que la semilla llena <c>ThresholdsJson</c> (nulo: sin umbrales).</param>
public sealed record DefinicionDeTipoDeAlerta(
    string TypeCode,
    string Module,
    string Description,
    IReadOnlyList<string> Destinatarios,
    AlertChannels Canales,
    AlertSeverity Severidad,
    string DisponibleDesde,
    bool UsaDestinatarios = true,
    IReadOnlyList<string>? Umbrales = null,
    IReadOnlyDictionary<string, decimal>? UmbralesPorDefecto = null)
{
    public IReadOnlyList<string> UmbralesAdmitidos => Umbrales ?? [];
}

/// <summary>El catálogo cerrado de tipos de alerta (decisiones-transversales §2.13). (nuevo)</summary>
public static class TiposDeAlerta
{
    public const string Reorden = "Inventario.Reorden";
    public const string Quiebre = "Inventario.Quiebre";
    public const string IncidenteDeIntegridad = "Inventario.IncidenteDeIntegridad";
    public const string EventosRadianFaltantes = "Compras.EventosRadianFaltantes";
    public const string AprobacionPendiente = "Aprobaciones.Pendiente";
    public const string MensajeSinEntregar = "Integracion.MensajeSinEntregar";
    public const string MensajeRechazado = "Integracion.MensajeRechazado";
    public const string LoteNoCorrio = "Integracion.LoteNoCorrio";
    public const string VentaBajoCosto = "Inventario.VentaBajoCosto";
    public const string SinPoliticaDeDatos = "Personas.SinPoliticaDeDatos";
    public const string ValidacionFallida = "Integracion.ValidacionFallida";
    public const string DocumentoSinValidar = "Dian.DocumentoSinValidar";
    public const string DocumentoRechazado = "Dian.DocumentoRechazado";
    public const string PlazoDeContingencia = "Dian.PlazoDeContingencia";
    public const string ContingenciaAbierta = "Dian.ContingenciaAbierta";
    public const string ResolucionPorAgotar = "Dian.ResolucionPorAgotar";
    public const string ResolucionPorVencer = "Dian.ResolucionPorVencer";
    public const string ProximoAVencer = "Inventario.ProximoAVencer";
    public const string RemisionSinFacturar = "Inventario.RemisionSinFacturar";

    // ---- Feature 013 (T0091; data-model parte 3 §11; decisiones-transversales §2.18; C99, C123) ----

    public const string ProcesoDiarioNoCorrio = "Cartera.ProcesoDiarioNoCorrio";
    public const string CreditoSinCausar = "Cartera.CreditoSinCausar";
    public const string ReservaVencida = "Cartera.ReservaVencida";
    public const string GiroSinResultado = "Cartera.GiroSinResultado";
    public const string CierreProgramadoFallido = "Cartera.CierreProgramadoFallido";
    public const string ValorNacionalFaltante = "Cartera.ValorNacionalFaltante";
    public const string ReplicaDeValoresNacionalesAtrasada = "Cartera.ReplicaDeValoresNacionalesAtrasada";
    public const string IndiceDesactualizado = "Cartera.IndiceDesactualizado";
    public const string NormativaPendienteDeValidar = "Cartera.NormativaPendienteDeValidar";
    public const string LimiteLegalMasEstricto = "Cartera.LimiteLegalMasEstricto";
    public const string ObligacionSobrevenida = "Cartera.ObligacionSobrevenida";
    public const string ModeloObligatorioNoDisponible = "Cartera.ModeloObligatorioNoDisponible";
    public const string HistorialDeMoraIncompleto = "Cartera.HistorialDeMoraIncompleto";
    public const string CierreMensualPendiente = "Cartera.CierreMensualPendiente";
    public const string CanalExternoNoDisponible = "Cartera.CanalExternoNoDisponible";
    public const string MensajeAlDeudorFallido = "Cartera.MensajeAlDeudorFallido";
    public const string CoincidenciaEnListas = "Cartera.CoincidenciaEnListas";
    public const string PagarePendienteDeFirma = "Cartera.PagarePendienteDeFirma";
    public const string HechoNoContabilizable = "Cartera.HechoNoContabilizable";
    public const string VentaNoIncorporada = "Cartera.VentaNoIncorporada";
    public const string DescuadreContable = "Cartera.DescuadreContable";
    public const string GarantiaPorVencer = "Cartera.GarantiaPorVencer";
    public const string UsuraTopada = "Cartera.UsuraTopada";
    public const string LineaCerradaPorUsura = "Cartera.LineaCerradaPorUsura";
    public const string CuotaNoDescontadaPorNomina = "Cartera.CuotaNoDescontadaPorNomina";
    public const string CupoPorRevalidar = "Cartera.CupoPorRevalidar";
    public const string PlazoRegulatorioPorVencer = "Cartera.PlazoRegulatorioPorVencer";
    public const string PatrimonioTecnicoNoDisponible = "Cartera.PatrimonioTecnicoNoDisponible";
    public const string TareaDiariaNoCorrio = "Cartera.TareaDiariaNoCorrio";

    /// <summary>
    /// US4 (bloque 8C): la verificación del libro (<c>POST /api/lending/integrity/verify</c> o la muestra del paso 8 del ciclo) encontró que el
    /// libro de un crédito no cuadra con su reconstrucción. Hasta el 2026-10-11 se levantaba como <see cref="CreditoSinCausar"/> con la clave
    /// <c>…:libro</c>: no es un crédito sin causar sino un libro descuadrado, y lo revisan cartera y la contadora.
    /// </summary>
    public const string LibroDescuadrado = "Cartera.LibroDescuadrado";
    public const string NoAplicadoDePagaduria = "Cartera.NoAplicadoDePagaduria";
    public const string GiroDePagaduriaAtrasado = "Cartera.GiroDePagaduriaAtrasado";
    public const string ArchivoDeDescuentosPendiente = "Cartera.ArchivoDeDescuentosPendiente";
    public const string AvisoPrevioSinConstancia = "Cartera.AvisoPrevioSinConstancia";
    public const string PeriodoInexistente = "Contabilidad.PeriodoInexistente";
    public const string GrupoNiifDistinto = "Contabilidad.GrupoNiifDistinto";
    public const string ValorNacionalDistinto = "Nomina.ValorNacionalDistinto";
    public const string PlazoDelTitularPorVencer = "Cumplimiento.PlazoDelTitularPorVencer";

    /// <summary>El umbral de <see cref="GarantiaPorVencer"/>: con cuántos días de anticipación avisa (data-model §11).</summary>
    public const string DiasDeAnticipacion = "diasDeAnticipacion";

    /// <summary>
    /// Feature 013 (C68; data-model parte 3 §4.14): lo que Cartera suma a los tres tipos reutilizados de la 012. Una cooperativa que
    /// ya tiene versión recibe una versión nueva desde el despliegue con la unión (<c>AlertTypesSeeder</c>); una nueva ya la siembra
    /// con la unión, porque la definición la trae.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DestinatariosQueSumaCartera =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [ValidacionFallida] = ["Lending.ValidationFailures.Confirm"],
            [MensajeRechazado] = ["Lending.Messages.Reprocess", "Accounting.LendingRules.Manage"],
            [MensajeSinEntregar] = ["Lending.Messages.Reprocess", "Accounting.LendingBatches.Run"],
        };

    private const string Cartera = "Lending";
    private const AlertChannels SoloApp = AlertChannels.InApp;
    private const AlertChannels AppYCorreo = AlertChannels.InApp | AlertChannels.Email;

    public static readonly IReadOnlyList<DefinicionDeTipoDeAlerta> Todos =
    [
        new(Reorden, "Inventory", "La posición de un producto en una bodega quedó en o bajo su punto de reorden (FR-035).",
            ["Inventory.Purchases.Create"], SoloApp, AlertSeverity.Info, "I1"),
        new(Quiebre, "Inventory", "El disponible de un producto en una bodega quedó bajo su mínimo (FR-035).",
            ["Inventory.Purchases.Create", "Inventory.Warehouses.Manage"], SoloApp, AlertSeverity.Warning, "I1"),
        new(IncidenteDeIntegridad, "Inventory", "La verificación del kardex encontró una diferencia con las existencias (FR-003).",
            ["Inventory.Integrity.Rebuild"], AppYCorreo, AlertSeverity.Critical, "I1"),
        new(EventosRadianFaltantes, "Inventory", "Una factura de proveedor a crédito sigue sin los eventos RADIAN 030 y 032.",
            ["Inventory.Purchases.RegisterRadianEvent"], AppYCorreo, AlertSeverity.Warning, "I1"),
        new(AprobacionPendiente, "Approvals", "Una solicitud de aprobación espera un nivel: la reciben los titulares del permiso de ese nivel.",
            [], SoloApp, AlertSeverity.Info, "I1", UsaDestinatarios: false),
        new(MensajeSinEntregar, "Integration", "Un mensaje de integración lleva varios intentos o minutos sin entregarse.",
            ["Inventory.Messages.Reprocess", "Accounting.InventoryBatches.Run", "Lending.Messages.Reprocess", "Accounting.LendingBatches.Run"],
            AppYCorreo, AlertSeverity.Warning, "I2"),
        new(MensajeRechazado, "Integration", "Un destino rechazó un mensaje de integración (FR-080).",
            ["Inventory.Messages.Reprocess", "Accounting.InventoryRules.Manage", "Lending.Messages.Reprocess", "Accounting.LendingRules.Manage"],
            AppYCorreo, AlertSeverity.Critical, "I2"),
        new(LoteNoCorrio, "Integration", "Un lote programado no corrió a su hora (FR-080).",
            ["Accounting.InventoryBatches.Run", "Inventory.Messages.Reprocess"], AppYCorreo, AlertSeverity.Warning, "I2"),
        new(VentaBajoCosto, "Inventory", "Se vendió por debajo del costo con la política «Alertar».",
            ["Inventory.Prices.Manage"], SoloApp, AlertSeverity.Warning, "I3"),
        new(SinPoliticaDeDatos, "Core", "Se dio de alta una persona sin política de tratamiento de datos publicada (FR-011).",
            ["Compliance.HabeasData.RecordConsent"], AppYCorreo, AlertSeverity.Warning, "I3"),
        new(ValidacionFallida, "Integration", "Cartera informó una validación negativa de un crédito (FR-061).",
            ["Inventory.Sales.Approve", "Lending.ValidationFailures.Confirm"], AppYCorreo, AlertSeverity.Warning, "IC"),
        new(DocumentoSinValidar, "ElectronicInvoicing", "Un documento electrónico sigue sin validación de la DIAN.",
            ["ElectronicInvoicing.Documents.Transmit"], SoloApp, AlertSeverity.Warning, "I4"),
        new(DocumentoRechazado, "ElectronicInvoicing", "La DIAN rechazó un documento electrónico.",
            ["ElectronicInvoicing.Documents.Correct"], AppYCorreo, AlertSeverity.Critical, "I4"),
        new(PlazoDeContingencia, "ElectronicInvoicing", "Se acerca o venció el plazo para transmitir lo emitido en contingencia.",
            ["ElectronicInvoicing.Contingencies.Declare", "ElectronicInvoicing.Documents.Transmit"], AppYCorreo, AlertSeverity.Critical, "I4"),
        new(ContingenciaAbierta, "ElectronicInvoicing", "Hay una contingencia DIAN abierta.",
            ["ElectronicInvoicing.Contingencies.Declare"], AppYCorreo, AlertSeverity.Warning, "I4"),
        new(ResolucionPorAgotar, "ElectronicInvoicing", "Una resolución de numeración está por agotarse.",
            ["ElectronicInvoicing.Resolutions.Manage"], AppYCorreo, AlertSeverity.Warning, "I4"),
        new(ResolucionPorVencer, "ElectronicInvoicing", "Una resolución de numeración está por vencer.",
            ["ElectronicInvoicing.Resolutions.Manage"], AppYCorreo, AlertSeverity.Warning, "I4"),
        new(ProximoAVencer, "Inventory", "Un lote con existencia vence pronto (FR-026).",
            ["Inventory.Warehouses.Manage"], SoloApp, AlertSeverity.Info, "I6"),
        new(RemisionSinFacturar, "Inventory", "Una remisión lleva demasiados días sin facturar (FR-052).",
            ["Inventory.Sales.Confirm"], SoloApp, AlertSeverity.Info, "I6"),
        // ---- Feature 013 (T0091; data-model parte 3 §11) ----
        new(ProcesoDiarioNoCorrio, Cartera, "El ciclo diario de Cartera del día anterior no terminó pasada la tolerancia.",
            ["Lending.Processes.Run"], AppYCorreo, AlertSeverity.Critical, "K1"),
        new(CreditoSinCausar, Cartera, "Un crédito quedó con error en el proceso diario y su causación no avanzó.",
            ["Lending.Processes.Run"], SoloApp, AlertSeverity.Warning, "K1"),
        new(ReservaVencida, Cartera, "Venció una reserva de número de crédito (o una versión tomó un número vencido).",
            ["Lending.CreditLimits.Assign"], SoloApp, AlertSeverity.Info, "K1"),
        new(GiroSinResultado, Cartera, "Una orden de giro lleva más días de los admitidos sin resultado.",
            ["Lending.Disbursements.Execute", "Lending.DisbursementOrders.Void"], AppYCorreo, AlertSeverity.Warning, "K1"),
        new(CierreProgramadoFallido, Cartera, "Falló el cierre mensual programado de Cartera.",
            ["Lending.MonthlyClose.Execute"], AppYCorreo, AlertSeverity.Critical, "K1"),
        new(ValorNacionalFaltante, Cartera, "Falta un valor nacional que necesita la causación, el cierre o la originación (NationalValues.Missing).",
            ["Lending.Processes.Run", "Core.NationalValues.Review"], AppYCorreo, AlertSeverity.Critical, "K1"),
        new(ReplicaDeValoresNacionalesAtrasada, Cartera, "La réplica de valores nacionales no corre o la cooperativa tiene migraciones pendientes.",
            ["Core.NationalValues.Review", "Lending.Processes.Run"], AppYCorreo, AlertSeverity.Warning, "K1"),
        new(IndiceDesactualizado, Cartera, "El último índice publicado (IBR, IPC) es más viejo de lo admitido.",
            ["Core.NationalValues.Review"], SoloApp, AlertSeverity.Warning, "K1"),
        new(NormativaPendienteDeValidar, Cartera, "Se calculó con un valor nacional o un documento normativo sin revisar.",
            ["Core.NationalValues.Review"], SoloApp, AlertSeverity.Info, "K1"),
        new(LimiteLegalMasEstricto, Cartera, "La réplica trajo un límite legal más estricto que un parámetro de la cooperativa.",
            ["Lending.Parameters.Manage"], AppYCorreo, AlertSeverity.Warning, "K1"),
        new(ObligacionSobrevenida, Cartera, "Un perfil regulatorio o una regla de pérdida esperada nuevos obligan después de activar.",
            ["Lending.ImpairmentModel.Manage", "Core.RegulatoryProfile.Manage"], AppYCorreo, AlertSeverity.Critical, "K1"),
        new(ModeloObligatorioNoDisponible, Cartera, "Un cierre se hizo sin el modelo de deterioro obligatorio.",
            ["Lending.MonthlyClose.Execute", "Lending.ImpairmentModel.Manage"], AppYCorreo, AlertSeverity.Critical, "K1"),
        new(HistorialDeMoraIncompleto, Cartera, "Hay créditos de segmentos cubiertos sin historial de mora completo.",
            ["Lending.Migration.Load", "Lending.ArrearsHistory.Import"], SoloApp, AlertSeverity.Warning, "K1"),
        new(CierreMensualPendiente, Cartera, "Pasó el día límite sin cierre de Cartera del mes anterior.",
            ["Lending.MonthlyClose.Execute"], AppYCorreo, AlertSeverity.Warning, "K1"),
        new(CanalExternoNoDisponible, Cartera, "Se abrió el circuito de un canal externo (mensajería, listas, centrales).",
            ["Core.ExternalChannels.Manage"], AppYCorreo, AlertSeverity.Critical, "K1"),
        new(MensajeAlDeudorFallido, Cartera, "Un mensaje al deudor falló en definitiva.",
            ["Lending.DebtorMessages.Send"], SoloApp, AlertSeverity.Warning, "K1"),
        new(CoincidenciaEnListas, Cartera, "Una consulta de listas restrictivas (radicación, venta, cupo o re-escaneo) coincidió.",
            ["Compliance.RestrictiveLists.Release"], AppYCorreo, AlertSeverity.Critical, "K1"),
        new(PagarePendienteDeFirma, Cartera, "Un pagaré desmaterializado sigue sin firma.",
            ["Lending.PromissoryNotes.Manage"], SoloApp, AlertSeverity.Warning, "K1"),
        new(HechoNoContabilizable, Cartera, "La validación previa selló un hecho de Cartera como no contabilizable en una operación que no se bloquea.",
            ["Accounting.LendingRules.Manage"], SoloApp, AlertSeverity.Warning, "K1"),
        new(VentaNoIncorporada, Cartera, "Una venta a crédito fechada hasta el corte llegó después de activar y no se incorporó (C63, C99).",
            ["Lending.Messages.Reprocess", "Inventory.Messages.Reprocess", "Lending.Activation.Execute"], AppYCorreo, AlertSeverity.Warning, "K1"),
        new(DescuadreContable, Cartera, "La conciliación de Cartera con Contabilidad encontró una diferencia sin explicar (FR-130).",
            ["Accounting.LendingRules.Manage", "Lending.Messages.Reprocess"], AppYCorreo, AlertSeverity.Critical, "K1"),
        new(GarantiaPorVencer, Cartera, "Una garantía (póliza, avalúo) vence pronto (FR-043).",
            ["Lending.Guarantees.Manage"], SoloApp, AlertSeverity.Info, "K1",
            Umbrales: [DiasDeAnticipacion], UmbralesPorDefecto: new Dictionary<string, decimal> { [DiasDeAnticipacion] = 30m }),
        new(UsuraTopada, Cartera, "La causación topó la tasa del día en la usura (FR-021).",
            ["Lending.CreditLines.Manage"], SoloApp, AlertSeverity.Warning, "K1"),
        new(LineaCerradaPorUsura, Cartera, "Una usura nueva cerró una línea a créditos nuevos (FR-021).",
            ["Lending.CreditLines.Manage"], AppYCorreo, AlertSeverity.Critical, "K1"),
        new(CuotaNoDescontadaPorNomina, Cartera, "Una cuota no se descontó por nómina, o Cartera estaba sin activar al liquidar (FR-112).",
            ["Lending.Blocks.Manage"], SoloApp, AlertSeverity.Warning, "K1"),
        new(CupoPorRevalidar, Cartera, "Al activar o reemplazar la carga, un cupo otorgado a mano excede un límite (FR-142).",
            ["Lending.CreditLimits.Assign"], AppYCorreo, AlertSeverity.Warning, "K1"),
        new(PlazoRegulatorioPorVencer, Cartera, "Vence un plazo regulatorio: reporte de castigos, SICSES o reclamo sobre el reporte a centrales.",
            ["Lending.RegulatoryReports.Generate", "Lending.WriteOffs.Apply", "Lending.CreditReportClaims.Manage"], AppYCorreo, AlertSeverity.Warning, "K1"),
        new(PatrimonioTecnicoNoDisponible, Cartera, "Los indicadores de la entidad no estaban disponibles al validar la concentración (FR-041).",
            ["Accounting.RegulatoryIndicators.Manage"], AppYCorreo, AlertSeverity.Critical, "K1"),
        new(TareaDiariaNoCorrio, Cartera, "Una tarea diaria de Cartera no corrió en su día (C123, SC-020).",
            ["Lending.Processes.Run"], AppYCorreo, AlertSeverity.Warning, "K1"),
        new(LibroDescuadrado, Cartera, "El libro de un crédito no cuadra con su reconstrucción (verificación de integridad, T33).",
            ["Lending.Processes.Run", "Accounting.LendingRules.Manage"], AppYCorreo, AlertSeverity.Critical, "K1"),
        new(NoAplicadoDePagaduria, Cartera, "El resultado de descuentos de una pagaduría trajo no aplicados.",
            ["Lending.PayrollNotApplied.Manage"], SoloApp, AlertSeverity.Warning, "K2"),
        new(GiroDePagaduriaAtrasado, Cartera, "Venció la ventana de giro de una pagaduría.",
            ["Lending.PayrollTransfers.Manage"], AppYCorreo, AlertSeverity.Warning, "K2"),
        new(ArchivoDeDescuentosPendiente, Cartera, "Hay un archivo de descuentos por generar según el convenio.",
            ["Lending.PayrollDeductionFiles.Generate"], SoloApp, AlertSeverity.Warning, "K2"),
        new(AvisoPrevioSinConstancia, Cartera, "Un episodio de mora sigue sin constancia del aviso previo.",
            ["Lending.PriorNotices.RegisterEvidence"], SoloApp, AlertSeverity.Warning, "K4"),
        new(PeriodoInexistente, "Accounting", "Llegó un hecho con fecha contable en un período que no existe.",
            ["Accounting.Periods.CloseYear"], AppYCorreo, AlertSeverity.Critical, "K1"),
        new(GrupoNiifDistinto, "Accounting", "El perfil regulatorio y el grupo NIIF de la configuración contable difieren.",
            ["Core.RegulatoryProfile.Manage"], SoloApp, AlertSeverity.Warning, "K1"),
        new(ValorNacionalDistinto, "Payroll", "La réplica trajo un SMMLV, UVT o festivo distinto del que Nómina tenía (T93).",
            ["Payroll.LegalParameters.Manage"], AppYCorreo, AlertSeverity.Warning, "K1"),
        new(PlazoDelTitularPorVencer, "Compliance", "Una solicitud del titular se acerca a su vencimiento sin respuesta.",
            ["Compliance.DataSubjectRequests.Respond"], AppYCorreo, AlertSeverity.Warning, "K1",
            Umbrales: [DiasDeAnticipacion], UmbralesPorDefecto: new Dictionary<string, decimal> { [DiasDeAnticipacion] = 3m }),
    ];

    private static readonly Dictionary<string, DefinicionDeTipoDeAlerta> PorCodigo = Todos.ToDictionary(t => t.TypeCode, StringComparer.Ordinal);

    /// <summary>La definición del tipo, o nula si no es del catálogo.</summary>
    public static DefinicionDeTipoDeAlerta? Buscar(string? typeCode) =>
        typeCode is not null && PorCodigo.TryGetValue(typeCode, out var def) ? def : null;
}
