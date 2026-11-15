namespace IngenIA365ERP.Application.Transport;

/// <summary>
/// Los permisos de Transporte y de los conectores de la cooperativa (feature 020, R19; FR-053). Los siembra la API al arrancar
/// (<c>TransportPermissionCatalogSeeder</c>); con el módulo apagado, ningún <c>Transport.*</c> llega a la sesión (R1, capa 2).
/// </summary>
public static class TransportPermissions
{
    public const string VehiclesView = "Transport.Vehicles.View";
    public const string VehiclesExport = "Transport.Vehicles.Export";
    public const string VehiclesCreate = "Transport.Vehicles.Create";
    public const string VehiclesUpdate = "Transport.Vehicles.Update";
    public const string VehiclesChangeStatus = "Transport.Vehicles.ChangeStatus";
    public const string VehiclesChangeOwner = "Transport.Vehicles.ChangeOwner";
    public const string VehiclesRetire = "Transport.Vehicles.Retire";
    public const string VehiclesImport = "Transport.Vehicles.Import";

    public const string DocumentsView = "Transport.Documents.View";
    public const string DocumentsManage = "Transport.Documents.Manage";

    public const string DriversView = "Transport.Drivers.View";
    public const string DriversManage = "Transport.Drivers.Manage";

    public const string CatalogsView = "Transport.Catalogs.View";
    public const string CatalogsManage = "Transport.Catalogs.Manage";

    /// <summary>Ver servicios y suscripciones.</summary>
    public const string ServicesView = "Transport.Services.View";

    /// <summary>Suscribir vehículos a servicios y pactar su precio.</summary>
    public const string ServicesManage = "Transport.Services.Manage";

    /// <summary>Configurar el servicio: producto, prioridad, línea de Cartera, medios de pago, conector y políticas de cobro.</summary>
    public const string ServiceSetupManage = "Transport.ServiceSetup.Manage";

    public const string BillingView = "Transport.Billing.View";
    public const string BillingRun = "Transport.Billing.Run";

    public const string NotificationsView = "Transport.Notifications.View";
    public const string NotificationsManage = "Transport.Notifications.Manage";

    /// <summary>Ver los conectores externos habilitados para la cooperativa (sin credenciales).</summary>
    public const string ConnectorsView = "Core.Connectors.View";

    /// <summary>Poner o reemplazar las credenciales de un conector y probar la conexión.</summary>
    public const string ConnectorsConfigure = "Core.Connectors.Configure";
}
