using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using ClaseDeDocumento = IngenIA365ERP.Domain.Enums.Inventory.DocumentClass;
using EstadoDelDocumentoDeInventario = IngenIA365ERP.Domain.Enums.Inventory.DocumentStatus;

namespace IngenIA365ERP.Application.Attachments.Common;

/// <summary>
/// Quién puede leer, subir y borrar los adjuntos de cada tipo de dueño. Es el único sitio que lo decide.
///
/// <para>
/// <b>Adjuntos que genera un módulo</b> (feature 010, D-36): el PDF para firma de la liquidación
/// definitiva, el archivo de dispersión, la planilla PILA. Los gobierna ese módulo, no el permiso
/// genérico <c>Attachments.*</c>: leerlos exige el permiso del módulo y la ruta genérica no los borra.
/// Antes, quien tuviera <c>Attachments.Download</c> descargaba la definitiva sin
/// <c>Payroll.Settlements.View</c>, y el Operador la borraba con <c>Attachments.Delete</c>.
/// </para>
///
/// <para>
/// <b>Soportes de comprobantes</b> (feature 011, R9): hasta el 2026-09-23 no tenían regla, así que
/// (a) se leían sin permiso de contabilidad, sólo con el genérico de adjuntos (FR-025), (b) se podía
/// colgar un archivo de cualquier documento —incluida la PILA de otro, que después nadie podía borrar—
/// porque subir no miraba el dueño (FR-019), y (c) el soporte de un comprobante contabilizado se podía
/// borrar por la API, porque sólo la pantalla escondía el botón (FR-004). Las tres cosas se deciden ahora
/// aquí, contra el comprobante mismo.
/// </para>
///
/// <para>
/// <b>Destinos de subida habilitados</b>: el comprobante contable y las imágenes del producto de inventario
/// (<see cref="ProductoDeInventario"/>, feature 012, T221: leer con <c>Inventory.Catalog.View</c>, subir y borrar con
/// <c>Inventory.Catalog.Manage</c>, sólo JPEG, PNG y WebP, el producto tiene que existir) y los soportes de un ajuste de
/// inventario (<see cref="SoporteDeAjuste"/>, feature 012, T255: actas de destrucción, denuncias; subir con
/// <c>Inventory.Adjustments.Create</c> mientras el ajuste está en borrador o en aprobación, leer con
/// <c>Inventory.Adjustments.View</c>, y confirmado no se borran: <c>Attachments.OwnerLocked</c>), y el soporte del concepto tributario del
/// interés de una línea de crédito (<see cref="SoporteTributarioDeLinea"/>, feature 013, T0361) y los documentos de una solicitud de crédito
/// mientras no esté decidida (<see cref="DocumentoDeSolicitud"/>, feature 013, T0397). Los tipos de módulo nunca, y
/// cualquier otro tipo tampoco hasta que tenga su pantalla y su regla (FR-019).
/// </para>
/// </summary>
public static class AdjuntosDeModulo
{
    /// <summary>Qué permiso exige leer el adjunto y si la ruta genérica puede borrarlo.</summary>
    public sealed record Regla(string PermisoDeLectura, bool Borrable, string Descripcion);

    /// <summary>El tipo de dueño de los soportes de un comprobante contable.</summary>
    public const string Comprobante = "AccountingDocument";

    /// <summary>El permiso genérico de borrar adjuntos; además, la regla del dueño (<see cref="PuedeBorrarAsync"/>).</summary>
    public const string PermisoDeBorrar = "Attachments.Delete";

    /// <summary>El tipo de dueño de las imágenes de un producto de inventario (feature 012, T41, T221; FR-029).</summary>
    public const string ProductoDeInventario = "InventoryProduct";

    /// <summary>Los únicos tipos de archivo que admite la imagen de un producto.</summary>
    public static readonly IReadOnlySet<string> TiposDeImagenDeProducto =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    /// <summary>El tipo de dueño de los soportes de un ajuste de inventario (feature 012, T41, T255; FR-037; contracts/api.md §10).</summary>
    public const string SoporteDeAjuste = "InventoryAdjustmentSupport";

    private const string VerAjustes = "Inventory.Adjustments.View";
    private const string CrearAjustes = "Inventory.Adjustments.Create";

    /// <summary>Las clases de un ajuste de I1 cuyo documento admite soportes (el grupo <c>Adjustments</c>).</summary>
    private static readonly ClaseDeDocumento[] ClasesDeAjuste =
    [
        ClaseDeDocumento.PositiveAdjustment, ClaseDeDocumento.NegativeAdjustment, ClaseDeDocumento.InternalConsumption,
        ClaseDeDocumento.WriteOff, ClaseDeDocumento.Assembly, ClaseDeDocumento.LocationMove,
    ];

    private const string VerCatalogo = "Inventory.Catalog.View";
    private const string AdministrarCatalogo = "Inventory.Catalog.Manage";

    /// <summary>
    /// Feature 012, I4 (T717, T41; contracts/dian.md §12; FR-068): los artefactos de factura, notas, DEE y su nota (canónico, XML firmado,
    /// <c>AttachedDocument</c>, <c>ApplicationResponse</c>, PDF). Los genera la facturación electrónica: se leen con <c>Inventory.Sales.View</c>,
    /// no se borran ni reciben subidas. El <c>OwnerEntityPublicId</c> es el del documento electrónico.
    /// </summary>
    public const string DocumentoElectronicoDeVenta = "ElectronicSalesDocument";

    /// <summary>Los del documento soporte y su nota (y los eventos RADIAN de I5): <c>Inventory.Purchases.View</c>, igual que los de venta.</summary>
    public const string DocumentoElectronicoDeCompra = "ElectronicPurchaseDocument";

    /// <summary>
    /// Las constancias y evidencias de un evento de contingencia 03/04: se leen con <c>ElectronicInvoicing.Contingencies.View</c>, <b>admiten</b>
    /// subidas de personas con <c>ElectronicInvoicing.Contingencies.Declare</c> sobre un evento que exista, y no se borran (son la evidencia
    /// ante la DIAN).
    /// </summary>
    public const string EventoDeContingencia = "DianContingencyEvent";

    private const string VerContingencias = "ElectronicInvoicing.Contingencies.View";
    private const string DeclararContingencias = "ElectronicInvoicing.Contingencies.Declare";

    /// <summary>
    /// Feature 013 (T81, T0086; data-model parte 3 §4.10): la evidencia de una autorización de tratamiento de datos (el formato
    /// firmado, la grabación). El dueño es la <b>persona</b> (<c>OwnerEntityPublicId</c> = su <c>PublicId</c>), porque se sube antes de
    /// registrar la autorización, que la cita en <c>EvidenceAttachmentPublicId</c>. Se lee con <c>Compliance.HabeasData.ViewHistory</c>,
    /// se sube con <c>Compliance.HabeasData.RecordConsent</c> sobre una persona que exista y no se borra: es la prueba ante el titular y
    /// la Superintendencia (Ley 1581).
    /// </summary>
    public const string AutorizacionDeDatos = "DataAuthorization";

    /// <summary>
    /// Feature 013 (T0088; data-model parte 3 §4.9): el escrito del titular o el soporte de la respuesta de una solicitud del titular.
    /// Dueño: la persona (se sube antes de radicar). Se lee con <c>Compliance.DataSubjectRequests.Read</c>, se sube con
    /// <c>.Register</c> o <c>.Respond</c> sobre una persona que exista y no se borra.
    /// </summary>
    public const string SolicitudDelTitular = "DataSubjectRequest";

    /// <summary>
    /// Feature 013 (T0297, T64; data-model §3.8): el PDF sellado del acta de una sesión de un órgano de Cartera. Lo genera el cierre de la
    /// sesión (<c>OwnerEntityPublicId</c> = el <c>PublicId</c> de la sesión): se lee con <c>Lending.Sessions.Read</c>, no recibe subidas y no se
    /// borra (un acta cerrada se corrige con otra sesión).
    /// </summary>
    public const string ActaDeSesionDeCartera = "LendingSessionMinutes";

    /// <summary>
    /// Feature 013, US1 (T0361; D20, T44): el soporte del concepto tributario del interés de una línea de crédito (el concepto de la
    /// contadora). El dueño es la <b>línea</b> (<c>OwnerEntityPublicId</c> = su <c>PublicId</c>): se sube antes de registrar la versión que lo
    /// cita en <c>InterestTaxSupportAttachmentPublicId</c>. Se lee con <c>Lending.CreditLines.View</c>, se sube con
    /// <c>Lending.CreditLines.Manage</c> sobre una línea que exista y no se borra: es lo que respalda una versión sellada.
    /// </summary>
    public const string SoporteTributarioDeLinea = "CreditLineTaxSupport";

    /// <summary>
    /// Feature 013, US2 (T0397, T81; DT §2.24): un documento soporte de una solicitud de crédito (cédula, certificación laboral, avalúo). El
    /// dueño es la <b>solicitud</b> (<c>OwnerEntityPublicId</c> = su <c>PublicId</c>). Se lee con <c>Lending.Applications.Read</c>; se sube con
    /// <c>Lending.Applications.Create</c> mientras la solicitud no esté decidida, y decidida queda bloqueado (<c>Attachments.OwnerLocked</c>):
    /// es el soporte de lo que se decidió. Antes de decidir lo retira <c>RemoveCreditApplicationDocumentCommand</c>.
    /// </summary>
    public const string DocumentoDeSolicitud = "LoanApplicationDocument";

    /// <summary>
    /// Feature 013, US2 (T0397, T73; DT §2.24): el informe crudo de una consulta a centrales de riesgo. Lo guarda la consulta
    /// (<c>OwnerEntityPublicId</c> = el <c>PublicId</c> de la consulta): se lee con <c>Lending.CreditBureau.ReadReports</c> (lectura auditada por la
    /// consulta que lo entrega), no recibe subidas y no se borra.
    /// </summary>
    public const string InformeDeCentrales = "LendingBureauReport";

    /// <summary>
    /// Feature 013, US2 (T0390, C87): el PDF de una simulación enviada a la persona simulada. Lo guarda el envío (<c>OwnerEntityPublicId</c> = el
    /// <c>PublicId</c> de la persona, porque todavía no hay crédito ni solicitud): se lee con <c>Lending.DebtorMessages.Read</c>, como el mensaje que
    /// lo lleva, no recibe subidas y no se borra.
    /// </summary>
    public const string SimulacionEnviada = "LendingSimulation";

    /// <summary>
    /// Feature 013, US3 (T0450, T81; DT §2.24; adaptadores §9): un documento soporte de una garantía o de un pagaré (avalúo, póliza, registro de la
    /// prenda, el pagaré físico firmado). El dueño es la <b>garantía</b> o el <b>pagaré</b> (<c>OwnerEntityPublicId</c> = su <c>PublicId</c>). Se lee
    /// con <c>Lending.Guarantees.Read</c>; se sube con <c>Lending.Guarantees.Manage</c> mientras la garantía no esté liberada, extinguida ni
    /// ejecutada (el pagaré, mientras no esté anulado); y se borra sólo mientras la garantía sigue constituida en una solicitud sin decidir (el
    /// pagaré, sin firmar y sin crédito): perfeccionada o decidida, es soporte de lo que se decidió (<c>Attachments.OwnerLocked</c>).
    /// </summary>
    public const string DocumentoDeGarantia = "LendingGuaranteeDocument";

    /// <summary>
    /// Feature 013, US3 (T0454, T78; DT §2.24): el archivo plano de transferencias de desembolso tal como se generó (SHA-256 en
    /// <c>LND_DisbursementFiles</c>). Lo genera Cartera: se lee con <c>Lending.DisbursementFiles.Generate</c>, no recibe subidas y no se borra.
    /// </summary>
    public const string ArchivoDeDesembolsos = "LendingDisbursementFile";

    /// <summary>
    /// Feature 013, K2 (T0837, T78; adaptadores-y-archivos.md §9): el archivo de descuentos de una pagaduría tal como se generó (SHA-256 en
    /// <c>LND_PayrollDeductionFiles</c>; viaja a Recaudo por su <c>PublicId</c> en <c>DescuentosPorPagaduria</c>). Lo genera Cartera: se lee con
    /// <c>Lending.PayrollDeductionFiles.Read</c>, no recibe subidas y no se borra.
    /// </summary>
    public const string ArchivoDeDescuentos = "LendingPayrollDeductionFile";

    /// <summary>
    /// Feature 013, K2 (T0837; FR-109; adaptadores-y-archivos.md §9): la certificación laboral y de descuentos de la pagaduría. El dueño es la
    /// <b>solicitud</b> que se radica por libranza —se lee con <c>Lending.Applications.Read</c>, se sube con <c>Lending.Applications.Create</c> mientras
    /// la solicitud no esté decidida y decidida queda bloqueada (<c>Attachments.OwnerLocked</c>)— o el <b>crédito</b> que se traslada a otra pagaduría
    /// (FR-116: se sube con <c>Lending.PayrollTransfers.Manage</c> y no se borra: respalda el vínculo nuevo).
    /// </summary>
    public const string CertificacionDeLibranza = "LendingPayrollCertification";

    /// <summary>
    /// Feature 013, US8 (T0748, C62; plantillas.md §6): el archivo de cobros provisionales tal como se revisó antes de activar Cartera. El dueño es la
    /// carga aplicada (<c>OwnerEntityPublicId</c> = su <c>PublicId</c>); la activación lo vuelve a leer y lo valida en su transacción. Lo genera Cartera:
    /// se lee con <c>Lending.Activation.Execute</c>, no recibe subidas y no se borra.
    /// </summary>
    public const string CobrosProvisionales = "LendingProvisionalCollections";

    /// <summary>
    /// Feature 013, US7 (T0638, T59; contracts/contabilidad.md §4.4; data-model parte 3 §5.4): el soporte del patrimonio técnico o de la relación de
    /// solvencia que registra el contador. Se sube <b>antes</b> de registrar el indicador (<c>RegisterRegulatoryIndicatorCommand</c> lo cita en
    /// <c>SupportAttachmentPublicId</c>), así que el dueño es el <c>PublicId</c> que la pantalla reserva para el registro. Se lee con
    /// <c>Accounting.RegulatoryIndicators.View</c>; se sube y se borra con <c>.Manage</c>; una vez registrado el indicador que lo cita (o el que lleva
    /// ese <c>PublicId</c>) queda bloqueado (<c>Attachments.OwnerLocked</c>): es el soporte de un hecho de sólo inserción.
    /// </summary>
    public const string SoporteDeIndicadorRegulatorio = "RegulatoryIndicatorSupport";

    /// <summary>
    /// Feature 013, US13 (T0780, T81; adaptadores-y-archivos.md §8.10, §9): el PDF de un documento emitido de Cartera (plan de pagos, paz y salvo,
    /// certificado de saldo) tal como se emitió, con su SHA-256 en <c>LND_IssuedDocuments.ContentSha256</c>. El dueño es el documento
    /// (<c>OwnerEntityPublicId</c> = su <c>PublicId</c>). Lo genera Cartera: se lee con <c>Lending.Documents.Read</c>, no recibe subidas, no se borra
    /// —reimprimir es descargar este mismo archivo—, se guarda <c>Direct</c> y <c>Available</c> desde el servidor y, como nunca se borra, nunca pasa por
    /// la papelera de 90 días.
    /// </summary>
    public const string DocumentoEmitidoDeCartera = "LendingIssuedDocument";

    /// <summary>
    /// Feature 013, US13 (T0781; pregunta I6 de DT §5, rige la propuesta por defecto; C90, C101): el logotipo de la cooperativa en los documentos de
    /// Cartera. El dueño es la configuración de Cartera (<c>OwnerEntityPublicId</c> = el <c>PublicId</c> de la fila única de <c>LND_Setup</c>). Se lee
    /// con <c>Lending.Documents.Read</c>; se sube y se reemplaza con <c>Lending.Parameters.Manage</c> (como el firmante, que es parámetro), sólo JPEG o
    /// PNG; se borra sólo cuando ya lo reemplazó otro, y un documento emitido conserva su PDF con el logotipo que tenía.
    /// </summary>
    public const string LogotipoDeCartera = "LendingLogo";

    /// <summary>Feature 013, US13 (T0781, I6): la imagen de la firma de los documentos de Cartera; las mismas reglas que <see cref="LogotipoDeCartera"/>.</summary>
    public const string ImagenDeFirmaDeCartera = "LendingSignatureImage";

    /// <summary>Los tipos de archivo de la imagen del logotipo y de la firma.</summary>
    public static readonly IReadOnlySet<string> TiposDeImagenDeCartera =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png" };

    /// <summary>
    /// Feature 013, K3 (T0880; transversales §2.24): la aceptación firmada (o la constancia del rechazo) de un codeudor, avalista o propietario de una
    /// garantía a una novedad, con dueño la <b>novedad</b> (<c>OwnerEntityPublicId</c> = su <c>PublicId</c>). Se lee con <c>Lending.Novelties.Read</c>; se
    /// sube y se borra con <c>Lending.Novelties.Request</c> mientras la novedad no esté decidida (borrador o radicada); decidida, queda bloqueado
    /// (<c>Attachments.OwnerLocked</c>): es lo que respalda la decisión.
    /// </summary>
    public const string ConsentimientoDeNovedad = "LendingNoveltyConsent";

    /// <summary>
    /// Feature 013, K3 (bloque C, T0890, T0892): el soporte de un evento del deudor (acta de defunción, constancia de retiro, oficio de embargo), del
    /// proceso de insolvencia (auto de admisión, constancia del fracaso) o del cambio de estado de una reclamación al seguro, con dueño la
    /// <b>persona</b> (<c>OwnerEntityPublicId</c> = su <c>PublicId</c>). Se lee con <c>Lending.Loans.Read</c>; lo sube quien tiene
    /// <c>Lending.DebtorEvents.Manage</c> o <c>Lending.Insolvency.Manage</c>; no se borra: es la evidencia de lo que suspendió el cobro (Principio XI).
    /// </summary>
    public const string SoporteDeEventoDelDeudor = "LendingDebtorEventSupport";

    /// <summary>
    /// Feature 013, K3 (US14, T0906): el informe motivado, el concepto de irrecuperabilidad o el soporte del cruce de aportes de un crédito que se
    /// propone castigar, con dueño el <b>crédito</b> (<c>OwnerEntityPublicId</c> = su <c>PublicId</c>: el lote todavía no existe al subirlos). Se lee con
    /// <c>Lending.Loans.Read</c>; lo sube quien tiene <c>Lending.WriteOffs.Prepare</c> sobre un crédito que exista; no se borra: respalda el castigo.
    /// </summary>
    public const string SoporteDeCastigo = "LendingWriteOffSupport";

    /// <summary>
    /// Feature 013, K3 (US14, T0909): el soporte del reporte de los castigos al supervisor, con dueño el <b>lote</b> (<c>OwnerEntityPublicId</c> = su
    /// <c>PublicId</c>). Se lee con <c>Lending.Loans.Read</c>; lo sube quien tiene <c>Lending.WriteOffs.Apply</c> mientras el lote esté aplicado, deba
    /// reportarse y el reporte no se haya registrado; no se borra.
    /// </summary>
    public const string ReporteDeCastigos = "LendingWriteOffSupervisorReport";

    /// <summary>
    /// Feature 013, K4 (US12, T0947): un documento del cobro jurídico —demanda, auto, embargo, sentencia, cuenta de honorarios—, con dueño el
    /// <b>proceso</b> (<c>OwnerEntityPublicId</c> = su <c>PublicId</c>). Se lee con <c>Lending.Collections.Read</c>; lo sube quien tiene
    /// <c>Lending.LegalProcesses.Manage</c> mientras el proceso esté abierto; no se borra: respalda la actuación.
    /// </summary>
    public const string DocumentoDeProcesoJuridico = "LendingLegalProcessDocument";

    /// <summary>
    /// Feature 013, K4 (US12, T0950): la guía de envío de la carta de un aviso previo al reporte negativo, con dueño el <b>aviso</b>
    /// (<c>OwnerEntityPublicId</c> = su <c>PublicId</c>). Se lee con <c>Lending.PriorNotices.Read</c>; lo sube quien tiene
    /// <c>Lending.PriorNotices.RegisterEvidence</c> sobre un aviso que exista; no se borra: es la constancia (Leyes 1266 y 2157).
    /// </summary>
    public const string ConstanciaDeAvisoPrevio = "LendingPriorNoticeEvidence";

    /// <summary>
    /// Feature 013, K4 (US12, T0951): un soporte de un reclamo o una suplantación sobre el reporte a centrales —lo que trae el titular y la
    /// respuesta—, con dueño la <b>persona</b> (<c>OwnerEntityPublicId</c> = su <c>PublicId</c>: el reclamo todavía no existe al subirlo). Se lee con
    /// <c>Lending.CreditReportClaims.Read</c>; lo sube quien tiene <c>Lending.CreditReportClaims.Manage</c>; no se borra: es evidencia del titular.
    /// </summary>
    public const string SoporteDeReclamoSobreElReporte = "LendingCreditReportClaimSupport";

    private const string LeerCobranza = "Lending.Collections.Read";
    private const string GestionarCobroJuridico = "Lending.LegalProcesses.Manage";
    private const string LeerAvisosPrevios = "Lending.PriorNotices.Read";
    private const string RegistrarConstanciaDeAviso = "Lending.PriorNotices.RegisterEvidence";
    private const string LeerReclamos = "Lending.CreditReportClaims.Read";
    private const string TramitarReclamos = "Lending.CreditReportClaims.Manage";

    private const string ArmarLotesDeCastigo = "Lending.WriteOffs.Prepare";
    private const string AplicarLotesDeCastigo = "Lending.WriteOffs.Apply";

    private const string LeerCreditos = "Lending.Loans.Read";
    private const string GestionarEventosDelDeudor = "Lending.DebtorEvents.Manage";
    private const string GestionarInsolvencia = "Lending.Insolvency.Manage";

    private const string LeerNovedades = "Lending.Novelties.Read";
    private const string PedirNovedades = "Lending.Novelties.Request";

    private const string LeerDocumentosDeCartera = "Lending.Documents.Read";
    private const string AdministrarParametrosDeCartera = "Lending.Parameters.Manage";

    private const string VerIndicadores = "Accounting.RegulatoryIndicators.View";
    private const string AdministrarIndicadores = "Accounting.RegulatoryIndicators.Manage";

    private const string LeerGarantias = "Lending.Guarantees.Read";
    private const string AdministrarGarantias = "Lending.Guarantees.Manage";

    private const string LeerSolicitudesDeCredito = "Lending.Applications.Read";
    private const string TrasladarLibranzas = "Lending.PayrollTransfers.Manage";
    private const string CrearSolicitudesDeCredito = "Lending.Applications.Create";

    private const string VerLineas = "Lending.CreditLines.View";
    private const string AdministrarLineas = "Lending.CreditLines.Manage";

    private const string VerHabeasData = "Compliance.HabeasData.ViewHistory";
    private const string RegistrarAutorizacion = "Compliance.HabeasData.RecordConsent";
    private const string LeerSolicitudes = "Compliance.DataSubjectRequests.Read";
    private const string RadicarSolicitud = "Compliance.DataSubjectRequests.Register";
    private const string ResponderSolicitud = "Compliance.DataSubjectRequests.Respond";

    private const string LeerComprobantes = "Accounting.Vouchers.View";
    private const string EscribirComprobantes = "Accounting.Vouchers.Create";

    /// <summary>Los tipos que genera un módulo: inmutables y nunca destino de una subida de personas.</summary>
    private static readonly IReadOnlyDictionary<string, Regla> DeModulo = new Dictionary<string, Regla>(StringComparer.Ordinal)
    {
        ["EmploymentTermination"] = new("Payroll.Settlements.View", Borrable: false, "el documento para firma de la liquidación definitiva"),
        ["BankDisbursementFile"] = new("Payroll.Disbursement.View", Borrable: false, "el archivo de dispersión bancaria que se entregó al banco"),
        ["PilaGeneration"] = new("Payroll.Pila.View", Borrable: false, "la planilla PILA tal como se generó y se cargó en el operador"),
        [DocumentoElectronicoDeVenta] = new("Inventory.Sales.View", Borrable: false, "un archivo de un documento electrónico de venta ante la DIAN"),
        [DocumentoElectronicoDeCompra] = new("Inventory.Purchases.View", Borrable: false, "un archivo de un documento electrónico de compra ante la DIAN"),
        [ActaDeSesionDeCartera] = new("Lending.Sessions.Read", Borrable: false, "el acta sellada de una sesión de un órgano de Cartera"),
        [InformeDeCentrales] = new("Lending.CreditBureau.ReadReports", Borrable: false, "el informe de una consulta a centrales de riesgo"),
        [SimulacionEnviada] = new("Lending.DebtorMessages.Read", Borrable: false, "la simulación enviada a la persona"),
        [ArchivoDeDesembolsos] = new("Lending.DisbursementFiles.Generate", Borrable: false, "el archivo de desembolsos de crédito que se entregó al banco"),
        [ArchivoDeDescuentos] = new("Lending.PayrollDeductionFiles.Read", Borrable: false, "el archivo de descuentos que se entregó a una pagaduría"),
        [CobrosProvisionales] = new("Lending.Activation.Execute", Borrable: false, "el archivo de cobros provisionales revisado para la activación de Cartera"),
        [DocumentoEmitidoDeCartera] = new(LeerDocumentosDeCartera, Borrable: false, "un documento emitido de Cartera (plan de pagos, paz y salvo o certificado)"),
    };

    /// <summary>La regla de un tipo que genera un módulo, o nula si no lo es.</summary>
    public static Regla? De(string? ownerEntityType) =>
        ownerEntityType is not null && DeModulo.TryGetValue(ownerEntityType, out var regla) ? regla : null;

    /// <summary>
    /// Verdadero si quien pide puede leer un adjunto de ese dueño. Los tipos de módulo exigen el permiso
    /// del módulo; los soportes de comprobantes, el de consultar comprobantes (FR-025); el resto, sólo el
    /// genérico, que la ruta ya exigió. Sin permiso la respuesta es la misma que si no existiera.
    /// </summary>
    public static async Task<bool> PuedeLeerAsync(IPermissionChecker permisos, string? ownerEntityType, CancellationToken ct)
    {
        var permiso = ownerEntityType switch
        {
            Comprobante => LeerComprobantes,
            ProductoDeInventario => VerCatalogo,
            SoporteDeAjuste => VerAjustes,
            EventoDeContingencia => VerContingencias,
            AutorizacionDeDatos => VerHabeasData,
            SolicitudDelTitular => LeerSolicitudes,
            SoporteTributarioDeLinea => VerLineas,
            DocumentoDeSolicitud or CertificacionDeLibranza => LeerSolicitudesDeCredito,
            DocumentoDeGarantia => LeerGarantias,
            ConsentimientoDeNovedad => LeerNovedades,
            SoporteDeEventoDelDeudor or SoporteDeCastigo or ReporteDeCastigos => LeerCreditos,
            SoporteDeIndicadorRegulatorio => VerIndicadores,
            LogotipoDeCartera or ImagenDeFirmaDeCartera => LeerDocumentosDeCartera,
            DocumentoDeProcesoJuridico => LeerCobranza,
            ConstanciaDeAvisoPrevio => LeerAvisosPrevios,
            SoporteDeReclamoSobreElReporte => LeerReclamos,
            _ => De(ownerEntityType)?.PermisoDeLectura,
        };
        return permiso is null || await permisos.HasPermissionAsync(permiso, ct);
    }

    /// <summary>
    /// Nulo si quien pide puede subir un adjunto a ese dueño; si no, el error (FR-019):
    /// <c>OwnerNotAllowed</c> (422) si el tipo no admite subidas de personas, y <c>Generic.NotFound</c>
    /// (404) si el comprobante no existe en la cooperativa <b>o</b> falta el permiso de escribir
    /// comprobantes —la misma respuesta, para no revelar qué comprobantes existen—. Con una base por
    /// cooperativa, un comprobante de otra simplemente no está.
    /// </summary>
    public static async Task<Error?> PuedeSubirAsync(
        IApplicationDbContext db, IPermissionChecker permisos, string? ownerEntityType, Guid ownerEntityPublicId, CancellationToken ct,
        string? contentType = null)
    {
        if (De(ownerEntityType) is { } deModulo)
            return new Error(AttachmentErrorCodes.OwnerNotAllowed,
                $"No se pueden subir archivos a {deModulo.Descripcion}: lo genera el programa.");
        if (ownerEntityType == ProductoDeInventario)
        {
            if (contentType is not null && !TiposDeImagenDeProducto.Contains(contentType))
                return new Error(AttachmentErrorCodes.Validation_MimeTypeNotAllowed, "La imagen del producto va en JPEG, PNG o WebP.");
            if (!await permisos.HasPermissionAsync(AdministrarCatalogo, ct)
                || !await db.Products.AnyAsync(p => p.PublicId == ownerEntityPublicId, ct))
                return new Error("Generic.NotFound", "Producto no encontrado.");
            return null;
        }
        if (ownerEntityType == SoporteDeAjuste)
        {
            var ajuste = await db.InventoryDocuments.AsNoTracking()
                .Where(d => d.PublicId == ownerEntityPublicId && ClasesDeAjuste.Contains(d.Class))
                .Select(d => (EstadoDelDocumentoDeInventario?)d.Status)
                .FirstOrDefaultAsync(ct);
            if (ajuste is null || !await permisos.HasPermissionAsync(CrearAjustes, ct))
                return new Error("Generic.NotFound", "Ajuste no encontrado.");
            return ajuste is EstadoDelDocumentoDeInventario.Draft or EstadoDelDocumentoDeInventario.PendingApproval
                ? null
                : AjusteBloqueado();
        }
        if (ownerEntityType == EventoDeContingencia)
        {
            if (!await permisos.HasPermissionAsync(DeclararContingencias, ct)
                || !await db.DianContingencyEvents.AnyAsync(e => e.PublicId == ownerEntityPublicId, ct))
                return new Error("Generic.NotFound", "Evento de contingencia no encontrado.");
            return null;
        }
        if (ownerEntityType is AutorizacionDeDatos or SolicitudDelTitular)
        {
            var puede = ownerEntityType == AutorizacionDeDatos
                ? await permisos.HasPermissionAsync(RegistrarAutorizacion, ct)
                : await permisos.HasPermissionAsync(RadicarSolicitud, ct) || await permisos.HasPermissionAsync(ResponderSolicitud, ct);
            if (!puede || !await db.People.AnyAsync(p => p.PublicId == ownerEntityPublicId, ct))
                return new Error("Generic.NotFound", "Persona no encontrada.");
            return null;
        }
        if (ownerEntityType == SoporteTributarioDeLinea)
        {
            if (!await permisos.HasPermissionAsync(AdministrarLineas, ct)
                || !await db.CreditLines.AnyAsync(l => l.PublicId == ownerEntityPublicId && !l.IsDeleted, ct))
                return new Error("Generic.NotFound", "Línea de crédito no encontrada.");
            return null;
        }
        if (ownerEntityType == DocumentoDeSolicitud)
        {
            var solicitud = await db.CreditApplications.AsNoTracking()
                .Where(a => a.PublicId == ownerEntityPublicId && !a.IsDeleted)
                .Select(a => (Domain.Enums.Lending.CreditApplicationStatus?)a.Status)
                .FirstOrDefaultAsync(ct);
            if (solicitud is null || !await permisos.HasPermissionAsync(CrearSolicitudesDeCredito, ct))
                return new Error("Generic.NotFound", "Solicitud de crédito no encontrada.");
            return SolicitudDecidida(solicitud.Value) ? SolicitudBloqueada() : null;
        }
        if (ownerEntityType == CertificacionDeLibranza)
        {
            // Feature 013, K2 (T0837): la certificación de una solicitud por libranza (como sus demás soportes) o la de un traslado (el crédito).
            var solicitudDeLibranza = await db.CreditApplications.AsNoTracking()
                .Where(a => a.PublicId == ownerEntityPublicId && !a.IsDeleted)
                .Select(a => (Domain.Enums.Lending.CreditApplicationStatus?)a.Status)
                .FirstOrDefaultAsync(ct);
            if (solicitudDeLibranza is { } estadoDeLibranza)
            {
                if (!await permisos.HasPermissionAsync(CrearSolicitudesDeCredito, ct)) return new Error("Generic.NotFound", "Solicitud de crédito no encontrada.");
                return SolicitudDecidida(estadoDeLibranza) ? SolicitudBloqueada() : null;
            }
            if (!await permisos.HasPermissionAsync(TrasladarLibranzas, ct)
                || !await db.Loans.AnyAsync(l => l.PublicId == ownerEntityPublicId && !l.IsDeleted, ct))
                return new Error("Generic.NotFound", "Crédito no encontrado.");
            return null;
        }
        if (ownerEntityType == DocumentoDeGarantia)
        {
            if (!await permisos.HasPermissionAsync(AdministrarGarantias, ct)) return new Error("Generic.NotFound", "Garantía no encontrada.");
            var garantia = await db.Collaterals.AsNoTracking().Where(c => c.PublicId == ownerEntityPublicId && !c.IsDeleted)
                .Select(c => (Domain.Enums.Lending.CollateralStatus?)c.Status).FirstOrDefaultAsync(ct);
            if (garantia is { } estadoDeLaGarantia)
                return estadoDeLaGarantia is Domain.Enums.Lending.CollateralStatus.Constituted or Domain.Enums.Lending.CollateralStatus.Valid
                    ? null
                    : GarantiaBloqueada();
            var pagare = await db.PromissoryNotes.AsNoTracking().Where(p => p.PublicId == ownerEntityPublicId && !p.IsDeleted)
                .Select(p => (Domain.Enums.Lending.PromissoryNoteStatus?)p.Status).FirstOrDefaultAsync(ct);
            if (pagare is null) return new Error("Generic.NotFound", "Garantía no encontrada.");
            return pagare == Domain.Enums.Lending.PromissoryNoteStatus.Voided ? GarantiaBloqueada() : null;
        }
        if (ownerEntityType == SoporteDeIndicadorRegulatorio)
        {
            if (!await permisos.HasPermissionAsync(AdministrarIndicadores, ct)) return new Error("Generic.NotFound", "Indicador no encontrado.");
            return await IndicadorRegistradoAsync(db, ownerEntityPublicId, ct) ? IndicadorBloqueado() : null;
        }
        if (ownerEntityType == ConsentimientoDeNovedad)
        {
            // Feature 013, K3 (T0880): el consentimiento de una novedad, mientras no se decida.
            var novedad = await EstadoDeNovedadAsync(db, ownerEntityPublicId, ct);
            if (novedad is null || !await permisos.HasPermissionAsync(PedirNovedades, ct)) return new Error("Generic.NotFound", "Novedad no encontrada.");
            return NovedadDecidida(novedad.Value) ? NovedadBloqueada() : null;
        }
        if (ownerEntityType == SoporteDeEventoDelDeudor)
        {
            // Feature 013, K3 (bloque C): el soporte de un evento del deudor, de su insolvencia o de su reclamación.
            var puede = await permisos.HasPermissionAsync(GestionarEventosDelDeudor, ct) || await permisos.HasPermissionAsync(GestionarInsolvencia, ct);
            if (!puede || !await db.People.AnyAsync(p => p.PublicId == ownerEntityPublicId, ct))
                return new Error("Generic.NotFound", "Persona no encontrada.");
            return null;
        }
        if (ownerEntityType == SoporteDeCastigo)
        {
            // Feature 013, K3 (US14): los soportes del castigo de un crédito, de quien arma el lote.
            if (!await permisos.HasPermissionAsync(ArmarLotesDeCastigo, ct)
                || !await db.Loans.AnyAsync(l => l.PublicId == ownerEntityPublicId && !l.IsDeleted, ct))
                return new Error("Generic.NotFound", "Crédito no encontrado.");
            return null;
        }
        if (ownerEntityType == ReporteDeCastigos)
        {
            // Feature 013, K3 (US14): el soporte del reporte al supervisor de un lote aplicado que debe reportarse y todavía no se reportó.
            var lote = await db.WriteOffBatches.AsNoTracking().Where(b => b.PublicId == ownerEntityPublicId && !b.IsDeleted)
                .Select(b => new { b.Status, b.SupervisorReportRequired, b.SupervisorReportedAt }).FirstOrDefaultAsync(ct);
            if (lote is null || !await permisos.HasPermissionAsync(AplicarLotesDeCastigo, ct))
                return new Error("Generic.NotFound", "Lote de castigo no encontrado.");
            return lote.Status == Domain.Enums.Lending.WriteOffBatchStatus.Applied && lote.SupervisorReportRequired && lote.SupervisorReportedAt is null
                ? null
                : new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
                    "El lote no espera el soporte del reporte al supervisor: no está aplicado, no debe reportarse o el reporte ya se registró.",
                    new { ownerEntityType });
        }
        if (ownerEntityType == DocumentoDeProcesoJuridico)
        {
            // Feature 013, K4 (US12): los documentos de un proceso de cobro jurídico abierto.
            var proceso = await db.LegalProcesses.AsNoTracking().Where(p => p.PublicId == ownerEntityPublicId && !p.IsDeleted)
                .Select(p => (Domain.Enums.Lending.LegalProcessStatus?)p.Status).FirstOrDefaultAsync(ct);
            if (proceso is null || !await permisos.HasPermissionAsync(GestionarCobroJuridico, ct))
                return new Error("Generic.NotFound", "Proceso de cobro jurídico no encontrado.");
            return proceso is Domain.Enums.Lending.LegalProcessStatus.Concluded or Domain.Enums.Lending.LegalProcessStatus.Withdrawn
                ? new ErrorConDatos(AttachmentErrorCodes.OwnerLocked, "El proceso de cobro jurídico ya terminó: no recibe más documentos.",
                    new { ownerEntityType })
                : null;
        }
        if (ownerEntityType == ConstanciaDeAvisoPrevio)
        {
            // Feature 013, K4 (US12): la guía de la carta de un aviso previo.
            if (!await permisos.HasPermissionAsync(RegistrarConstanciaDeAviso, ct)
                || !await db.PriorNotices.AnyAsync(n => n.PublicId == ownerEntityPublicId && !n.IsDeleted, ct))
                return new Error("Generic.NotFound", "Aviso previo no encontrado.");
            return null;
        }
        if (ownerEntityType == SoporteDeReclamoSobreElReporte)
        {
            // Feature 013, K4 (US12): los soportes de un reclamo sobre el reporte a centrales, de la persona que reclama.
            if (!await permisos.HasPermissionAsync(TramitarReclamos, ct) || !await db.People.AnyAsync(p => p.PublicId == ownerEntityPublicId, ct))
                return new Error("Generic.NotFound", "Persona no encontrada.");
            return null;
        }
        if (ownerEntityType is LogotipoDeCartera or ImagenDeFirmaDeCartera)
        {
            if (contentType is not null && !TiposDeImagenDeCartera.Contains(contentType))
                return new Error(AttachmentErrorCodes.Validation_MimeTypeNotAllowed, "El logotipo y la imagen de la firma van en JPEG o PNG.");
            if (!await permisos.HasPermissionAsync(AdministrarParametrosDeCartera, ct)
                || !await db.LendingSetups.AnyAsync(x => x.PublicId == ownerEntityPublicId, ct))
                return new Error("Generic.NotFound", "Configuración de Cartera no encontrada.");
            return null;
        }
        if (ownerEntityType != Comprobante)
            return new Error(AttachmentErrorCodes.OwnerNotAllowed,
                "Este tipo de documento todavía no admite soportes.");
        if (!await permisos.HasPermissionAsync(EscribirComprobantes, ct)
            || !await db.AccountingDocuments.AnyAsync(d => d.PublicId == ownerEntityPublicId, ct))
            return new Error("Generic.NotFound", "Comprobante no encontrado.");
        return null;
    }

    /// <summary>
    /// Nulo si el adjunto se puede borrar; si no, el error. Los de módulo nunca (<c>OwnedByModule</c>).
    /// Los soportes de un comprobante sólo mientras esté en borrador (<c>OwnerLocked</c>, FR-004): uno
    /// contabilizado o reversado conserva sus soportes, como conserva sus movimientos (Principio XI). Si
    /// el comprobante ya no existe —un borrador descartado antes de esta regla—, no hay nada que proteger.
    /// </summary>
    public static async Task<Error?> PuedeBorrarAsync(
        IApplicationDbContext db, string? ownerEntityType, Guid ownerEntityPublicId, CancellationToken ct, IPermissionChecker? permisos = null,
        Guid? attachmentPublicId = null)
    {
        if (De(ownerEntityType) is { Borrable: false } regla)
            return NoBorrable(regla);
        // Las constancias de una contingencia son la evidencia ante la DIAN: se conservan con el evento (T717).
        if (ownerEntityType == EventoDeContingencia)
            return new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
                "Es evidencia de una contingencia ante la DIAN: se conserva con el evento y no se puede borrar.",
                new { ownerEntityType });
        // Feature 013 (T0086, T0088): la evidencia del titular se conserva (Ley 1581).
        if (ownerEntityType is AutorizacionDeDatos or SolicitudDelTitular)
            return new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
                "Es evidencia del titular de los datos (autorización o solicitud): se conserva y no se puede borrar.",
                new { ownerEntityType });
        // Feature 013, K3 (bloque C): el soporte de un evento del deudor es la evidencia de lo que suspendió el cobro.
        if (ownerEntityType == SoporteDeEventoDelDeudor)
            return new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
                "Es el soporte de un evento del deudor (fallecimiento, retiro, embargo o insolvencia): se conserva y no se puede borrar.",
                new { ownerEntityType });
        // Feature 013, K3 (US14): los soportes del castigo y del reporte al supervisor respaldan lo que se castigó.
        if (ownerEntityType is SoporteDeCastigo or ReporteDeCastigos)
            return new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
                "Es un soporte del castigo de cartera (informe, concepto, cruce de aportes o reporte al supervisor): se conserva y no se puede borrar.",
                new { ownerEntityType });
        // Feature 013, K4 (US12): los documentos del cobro jurídico, la guía de la carta del aviso previo y los soportes de un reclamo sobre el reporte
        // son evidencia: se conservan.
        if (ownerEntityType is DocumentoDeProcesoJuridico or ConstanciaDeAvisoPrevio or SoporteDeReclamoSobreElReporte)
            return new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
                "Es evidencia de la cobranza (documento del cobro jurídico, constancia del aviso previo o soporte de un reclamo): se conserva y no se puede borrar.",
                new { ownerEntityType });
        // Feature 013 (T0361): el soporte del concepto tributario respalda una versión sellada de la línea.
        if (ownerEntityType == SoporteTributarioDeLinea)
            return new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
                "Es el soporte del concepto tributario de una línea de crédito: respalda sus versiones y no se puede borrar.",
                new { ownerEntityType });
        // Feature 013 (T0397): el soporte de una solicitud se borra sólo antes de decidirla, con el permiso de armarla.
        if (ownerEntityType == DocumentoDeSolicitud)
        {
            var solicitud = await db.CreditApplications.AsNoTracking()
                .Where(a => a.PublicId == ownerEntityPublicId)
                .Select(a => (Domain.Enums.Lending.CreditApplicationStatus?)a.Status)
                .FirstOrDefaultAsync(ct);
            if (permisos is not null && !await permisos.HasPermissionAsync(CrearSolicitudesDeCredito, ct))
                return new Error("Generic.NotFound", "Adjunto no encontrado.");
            return solicitud is { } estadoDeLaSolicitud && SolicitudDecidida(estadoDeLaSolicitud) ? SolicitudBloqueada() : null;
        }
        // Feature 013, K2 (T0837): la certificación de libranza se borra como un soporte de la solicitud; la de un traslado respalda el vínculo nuevo.
        if (ownerEntityType == CertificacionDeLibranza)
        {
            var solicitudDeLibranza = await db.CreditApplications.AsNoTracking()
                .Where(a => a.PublicId == ownerEntityPublicId)
                .Select(a => (Domain.Enums.Lending.CreditApplicationStatus?)a.Status)
                .FirstOrDefaultAsync(ct);
            if (solicitudDeLibranza is null)
                return new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
                    "Es la certificación que respalda el traslado de una libranza: se conserva y no se puede borrar.", new { ownerEntityType });
            if (permisos is not null && !await permisos.HasPermissionAsync(CrearSolicitudesDeCredito, ct))
                return new Error("Generic.NotFound", "Adjunto no encontrado.");
            return SolicitudDecidida(solicitudDeLibranza.Value) ? SolicitudBloqueada() : null;
        }
        // Feature 013 (T0450, T81): el soporte de una garantía o de un pagaré se borra sólo antes de perfeccionarse y de decidirse su dueño.
        if (ownerEntityType == DocumentoDeGarantia)
        {
            if (permisos is not null && !await permisos.HasPermissionAsync(AdministrarGarantias, ct))
                return new Error("Generic.NotFound", "Adjunto no encontrado.");
            return await GarantiaBorrableAsync(db, ownerEntityPublicId, ct) ? null : GarantiaBloqueada();
        }
        // Feature 013, K3 (T0880): el consentimiento de una novedad se borra sólo antes de decidirla, con el permiso de pedirla.
        if (ownerEntityType == ConsentimientoDeNovedad)
        {
            if (permisos is not null && !await permisos.HasPermissionAsync(PedirNovedades, ct))
                return new Error("Generic.NotFound", "Adjunto no encontrado.");
            return await EstadoDeNovedadAsync(db, ownerEntityPublicId, ct) is { } estadoDeLaNovedad && NovedadDecidida(estadoDeLaNovedad)
                ? NovedadBloqueada()
                : null;
        }
        // Feature 013 (T0638): el soporte de un indicador regulatorio se borra sólo antes de registrar el indicador que lo cita.
        if (ownerEntityType == SoporteDeIndicadorRegulatorio)
        {
            if (permisos is not null && !await permisos.HasPermissionAsync(AdministrarIndicadores, ct))
                return new Error("Generic.NotFound", "Adjunto no encontrado.");
            return await IndicadorRegistradoAsync(db, ownerEntityPublicId, ct) ? IndicadorBloqueado() : null;
        }
        // Feature 013, US13 (T0781, I6): el logotipo y la imagen de la firma se borran sólo cuando ya los reemplazó otro, y con el permiso de los
        // parámetros de Cartera. Un documento emitido no se toca: su PDF ya los lleva.
        if (ownerEntityType is LogotipoDeCartera or ImagenDeFirmaDeCartera)
        {
            if (permisos is not null && !await permisos.HasPermissionAsync(AdministrarParametrosDeCartera, ct))
                return new Error("Generic.NotFound", "Adjunto no encontrado.");
            return attachmentPublicId is { } adjunto && await ImagenReemplazadaAsync(db, ownerEntityType, adjunto, ct) ? null : ImagenVigente(ownerEntityType);
        }
        // La imagen de un producto la borra quien administra el catálogo (T221); el borrado queda auditado por el comando.
        if (ownerEntityType == ProductoDeInventario)
            return permisos is not null && await permisos.HasPermissionAsync(AdministrarCatalogo, ct)
                ? null
                : new Error("Generic.NotFound", "Adjunto no encontrado.");
        // Los soportes de un ajuste se borran sólo antes de confirmarlo (T255): confirmado o anulado, se conservan con él.
        if (ownerEntityType == SoporteDeAjuste)
        {
            var ajuste = await db.InventoryDocuments.AsNoTracking()
                .Where(d => d.PublicId == ownerEntityPublicId)
                .Select(d => (EstadoDelDocumentoDeInventario?)d.Status)
                .FirstOrDefaultAsync(ct);
            if (permisos is not null && !await permisos.HasPermissionAsync(CrearAjustes, ct))
                return new Error("Generic.NotFound", "Adjunto no encontrado.");
            return ajuste is EstadoDelDocumentoDeInventario.Confirmed or EstadoDelDocumentoDeInventario.Voided ? AjusteBloqueado() : null;
        }
        if (ownerEntityType != Comprobante)
            return null;

        var estado = await db.AccountingDocuments
            .Where(d => d.PublicId == ownerEntityPublicId)
            .Select(d => (DocumentStatus?)d.Status)
            .FirstOrDefaultAsync(ct);
        return estado is DocumentStatus.Posted or DocumentStatus.Reversed
            ? new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
                "Es soporte de un comprobante contabilizado: se conserva con el comprobante y no se puede borrar.",
                new { ownerEntityType })
            : null;
    }

    /// <summary>K3: el estado de una novedad por su <c>PublicId</c>, o nulo si no existe.</summary>
    private static Task<Domain.Enums.Lending.LoanNoveltyStatus?> EstadoDeNovedadAsync(IApplicationDbContext db, Guid publicId, CancellationToken ct) =>
        db.LoanNovelties.AsNoTracking().Where(n => n.PublicId == publicId && !n.IsDeleted)
            .Select(n => (Domain.Enums.Lending.LoanNoveltyStatus?)n.Status).FirstOrDefaultAsync(ct);

    /// <summary>K3: decidida (aprobada, rechazada, aplicada o anulada): sus consentimientos se conservan con ella.</summary>
    public static bool NovedadDecidida(Domain.Enums.Lending.LoanNoveltyStatus estado) =>
        estado is not (Domain.Enums.Lending.LoanNoveltyStatus.Draft or Domain.Enums.Lending.LoanNoveltyStatus.Submitted);

    public static Error NovedadBloqueada() => new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
        "La novedad ya se decidió: sus consentimientos respaldan la decisión y no se cambian.", new { ownerEntityType = ConsentimientoDeNovedad });

    /// <summary>Decidida (o terminada): sus soportes se conservan con ella (T81).</summary>
    public static bool SolicitudDecidida(Domain.Enums.Lending.CreditApplicationStatus estado) =>
        estado is Domain.Enums.Lending.CreditApplicationStatus.Approved or Domain.Enums.Lending.CreditApplicationStatus.Denied
            or Domain.Enums.Lending.CreditApplicationStatus.Disbursed or Domain.Enums.Lending.CreditApplicationStatus.Granted
            or Domain.Enums.Lending.CreditApplicationStatus.Cancelled;

    /// <summary>
    /// Feature 013 (T0450): ¿se puede retirar un soporte de la garantía o del pagaré? La garantía, sólo constituida, sin crédito y con su solicitud
    /// sin decidir; el pagaré, sin firmar y sin crédito. Un dueño que ya no existe no tiene nada que proteger.
    /// </summary>
    private static async Task<bool> GarantiaBorrableAsync(IApplicationDbContext db, Guid dueno, CancellationToken ct)
    {
        var garantia = await db.Collaterals.AsNoTracking().Where(c => c.PublicId == dueno)
            .Select(c => new { c.Status, c.LoanId, c.ApplicationId }).FirstOrDefaultAsync(ct);
        if (garantia is not null)
        {
            if (garantia.Status != Domain.Enums.Lending.CollateralStatus.Constituted || garantia.LoanId is not null) return false;
            var solicitud = garantia.ApplicationId is { } id
                ? await db.CreditApplications.AsNoTracking().Where(a => a.Id == id).Select(a => (Domain.Enums.Lending.CreditApplicationStatus?)a.Status)
                    .FirstOrDefaultAsync(ct)
                : null;
            return solicitud is not { } estado || !SolicitudDecidida(estado);
        }
        var pagare = await db.PromissoryNotes.AsNoTracking().Where(p => p.PublicId == dueno).Select(p => new { p.Status, p.LoanId }).FirstOrDefaultAsync(ct);
        return pagare is null
            || pagare.LoanId is null && pagare.Status is Domain.Enums.Lending.PromissoryNoteStatus.Created or Domain.Enums.Lending.PromissoryNoteStatus.PendingSignature;
    }

    /// <summary>
    /// Feature 013 (T0638): ¿ya hay un indicador regulatorio registrado con ese dueño —el que lleva ese <c>PublicId</c> o el que cita como soporte un
    /// adjunto de ese dueño—? Registrado, el soporte se conserva con él.
    /// </summary>
    private static async Task<bool> IndicadorRegistradoAsync(IApplicationDbContext db, Guid dueno, CancellationToken ct)
    {
        if (await db.RegulatoryIndicators.AsNoTracking().AnyAsync(i => i.PublicId == dueno, ct)) return true;
        var adjuntos = await db.Attachments.AsNoTracking()
            .Where(a => a.OwnerEntityType == SoporteDeIndicadorRegulatorio && a.OwnerEntityPublicId == dueno)
            .Select(a => a.PublicId).ToListAsync(ct);
        return adjuntos.Count > 0
            && await db.RegulatoryIndicators.AsNoTracking().AnyAsync(i => i.SupportAttachmentPublicId != null && adjuntos.Contains(i.SupportAttachmentPublicId.Value), ct);
    }

    /// <summary>
    /// Feature 013, US13 (T0781): ¿<paramref name="adjunto"/> ya fue reemplazado? La imagen vigente de un tipo es la última disponible (la de mayor
    /// Id); la que se quiere borrar tiene que ser anterior. Una imagen que sigue vigente no se borra: se reemplaza subiendo otra. Sin decir cuál
    /// adjunto (la lista por dueño), la respuesta es la conservadora: no se borra.
    /// </summary>
    private static async Task<bool> ImagenReemplazadaAsync(IApplicationDbContext db, string tipo, Guid adjunto, CancellationToken ct)
    {
        var vigente = await db.Attachments.AsNoTracking()
            .Where(a => a.OwnerEntityType == tipo && a.Status == Domain.Enums.Core.EstadoDeAdjunto.Available && !a.IsDeleted)
            .OrderByDescending(a => a.Id).Select(a => (long?)a.Id).FirstOrDefaultAsync(ct);
        var este = await db.Attachments.AsNoTracking().Where(a => a.PublicId == adjunto && a.OwnerEntityType == tipo)
            .Select(a => (long?)a.Id).FirstOrDefaultAsync(ct);
        return vigente is { } idVigente && este is { } id && id < idVigente;
    }

    /// <summary>La imagen vigente del logotipo o de la firma no se borra: se reemplaza subiendo otra (T0781).</summary>
    public static Error ImagenVigente(string tipo) => new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
        "Es la imagen vigente de los documentos de Cartera: no se borra, se reemplaza subiendo otra.", new { ownerEntityType = tipo });

    /// <summary>El soporte de un indicador regulatorio ya registrado no se cambia ni se borra (T0638).</summary>
    public static Error IndicadorBloqueado() => new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
        "Es el soporte de un indicador regulatorio ya registrado: se conserva con el indicador y no se puede cambiar. Para corregirlo registre otro del mismo corte.",
        new { ownerEntityType = SoporteDeIndicadorRegulatorio });

    /// <summary>El soporte de una garantía perfeccionada o decidida (o de un pagaré firmado) no se cambia ni se borra (T81).</summary>
    public static Error GarantiaBloqueada() => new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
        "Es soporte de una garantía o de un pagaré ya perfeccionado o decidido: se conserva y no se puede cambiar.",
        new { ownerEntityType = DocumentoDeGarantia });

    /// <summary>El soporte de una solicitud decidida no se cambia ni se borra (T81).</summary>
    public static Error SolicitudBloqueada() => new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
        "Es soporte de una solicitud de crédito ya decidida: se conserva con la solicitud y no se puede cambiar.",
        new { ownerEntityType = DocumentoDeSolicitud });

    private static Error AjusteBloqueado() => new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
        "Es soporte de un ajuste confirmado: se conserva con el ajuste y no se puede cambiar.",
        new { ownerEntityType = SoporteDeAjuste });

    /// <summary>El error de borrar por la ruta genérica un adjunto que su módulo declara inmutable.</summary>
    public static Error NoBorrable(Regla regla) =>
        new(AttachmentErrorCodes.OwnedByModule,
            $"Este adjunto es {regla.Descripcion}: lo generó el programa y no se borra; se conserva con la operación que lo produjo (reverse la operación si hace falta).");
}
