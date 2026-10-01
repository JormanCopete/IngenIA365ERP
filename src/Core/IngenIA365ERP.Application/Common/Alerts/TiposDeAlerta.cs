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
/// <param name="DisponibleDesde">La entrega desde la que la levanta algún proceso (I1…I6, IC).</param>
public sealed record DefinicionDeTipoDeAlerta(
    string TypeCode,
    string Module,
    string Description,
    IReadOnlyList<string> Destinatarios,
    AlertChannels Canales,
    AlertSeverity Severidad,
    string DisponibleDesde,
    bool UsaDestinatarios = true,
    IReadOnlyList<string>? Umbrales = null)
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
            ["Inventory.Messages.Reprocess", "Accounting.InventoryBatches.Run"], AppYCorreo, AlertSeverity.Warning, "I2"),
        new(MensajeRechazado, "Integration", "Un destino rechazó un mensaje de integración (FR-080).",
            ["Inventory.Messages.Reprocess", "Accounting.InventoryRules.Manage"], AppYCorreo, AlertSeverity.Critical, "I2"),
        new(LoteNoCorrio, "Integration", "Un lote programado no corrió a su hora (FR-080).",
            ["Accounting.InventoryBatches.Run", "Inventory.Messages.Reprocess"], AppYCorreo, AlertSeverity.Warning, "I2"),
        new(VentaBajoCosto, "Inventory", "Se vendió por debajo del costo con la política «Alertar».",
            ["Inventory.Prices.Manage"], SoloApp, AlertSeverity.Warning, "I3"),
        new(SinPoliticaDeDatos, "Core", "Se dio de alta una persona sin política de tratamiento de datos publicada (FR-011).",
            ["Compliance.HabeasData.RecordConsent"], AppYCorreo, AlertSeverity.Warning, "I3"),
        new(ValidacionFallida, "Integration", "Cartera informó una validación negativa de un crédito (FR-061).",
            ["Inventory.Sales.Approve"], AppYCorreo, AlertSeverity.Warning, "IC"),
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
    ];

    private static readonly Dictionary<string, DefinicionDeTipoDeAlerta> PorCodigo = Todos.ToDictionary(t => t.TypeCode, StringComparer.Ordinal);

    /// <summary>La definición del tipo, o nula si no es del catálogo.</summary>
    public static DefinicionDeTipoDeAlerta? Buscar(string? typeCode) =>
        typeCode is not null && PorCodigo.TryGetValue(typeCode, out var def) ? def : null;
}
