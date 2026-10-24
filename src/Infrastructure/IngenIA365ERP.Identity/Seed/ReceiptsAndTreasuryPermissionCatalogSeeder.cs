using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Feature 015 (T0044; decisiones-transversales §2.6; contracts/api.md §1.5; FR-011) — los permisos de Recaudo (<c>Receipts.*</c>), de la Tesorería
/// nueva (<c>Treasury.*</c>), de las piezas de plataforma de la 015 (<c>Core.PersonBankAccounts.*</c>, <c>Platform.GoLive.*</c>) y de las
/// matrices contables de los dos módulos (<c>Accounting.ReceiptsMatrix.*</c>, <c>Accounting.TreasuryMatrix.*</c>) para <c>SEC_Permissions</c>.
/// Hermano de <see cref="LendingPermissionCatalogSeeder"/>: misma convención <c>Resource.Action</c> y misma idempotencia (inserta los que faltan y
/// pone al día la descripción). Lo invoca <see cref="PhaseZeroSecuritySeeder"/> antes de <see cref="BuiltInRolesSeeder"/>, desde la API al
/// arrancar, nunca el DbMigrator.
///
/// <para>
/// Se siembra <b>entero en R0/T0</b>, también lo de R1 a T2 (cuyas rutas responden 404 hasta su entrega), para que las siete plantillas de rol
/// (<c>PerfilesSugeridos</c>) no cambien de forma entre entregas. Los que llevan monto están en <c>PermisosLimitables</c> y no se siembra ningún
/// valor en <c>SEC_PermissionAmountLimits</c> (P4: sin límite = sin atribución). Los permisos de la Tesorería heredada
/// (<c>Treasury.Checks</c>, <c>Treasury.Concepts</c>… del catálogo de dominio) no cambian: los nuevos tienen recursos propios.
/// </para>
/// </summary>
public static class ReceiptsAndTreasuryPermissionCatalogSeeder
{
    /// <summary>Los prefijos que este catálogo administra (para leer lo que ya existe).</summary>
    private static readonly string[] Prefijos = ["Receipts.", "Treasury.", "Core.PersonBankAccounts", "Platform.GoLive", "Accounting.ReceiptsMatrix", "Accounting.TreasuryMatrix"];

    internal static readonly (string Resource, string Action, string Description)[] Catalog =
    [
        // ------------------------------------------------------------------------------------------------- Recaudo · configuración --
        ("Receipts.Setup",              "View",               "Ver la configuración de Recaudo: puntos, cajas, cajeros, medios, tipos de documento y conceptos"),
        ("Receipts.Setup",              "Manage",             "Administrar la configuración de Recaudo"),
        ("Receipts.Scope",              "All",                "Alcance sobre todas las oficinas, puntos y cajas sin filas de asignación"),
        ("Receipts.Scope",              "Manage",             "Asignar oficinas, puntos, cajas y pagadurías a los usuarios"),
        // ------------------------------------------------------------------------------------------- Recaudo · sesión, arqueo, bóveda --
        ("Receipts.Session",            "Open",               "Abrir la sesión de caja"),
        ("Receipts.Session",            "View",               "Ver sesiones de caja"),
        ("Receipts.Session",            "ViewExpected",       "Ver lo esperado de una sesión antes del arqueo"),
        ("Receipts.Session",            "Move",               "Registrar movimientos de caja (base, abastecimiento, traslados)"),
        ("Receipts.Session",            "Reclassify",         "Reclasificar el medio de un recibo dentro de la sesión"),
        ("Receipts.Session",            "Close",              "Arquear y cerrar la sesión de caja"),
        ("Receipts.Session",            "CloseForgotten",     "Cerrar una sesión olvidada con arqueo diferido y testigo"),
        ("Receipts.Session",            "Adjust",             "Registrar el ajuste de una sesión cerrada"),
        ("Receipts.Session",            "SurpriseCount",      "Hacer un arqueo sorpresa"),
        ("Receipts.Count",              "ApproveDifference",  "Aprobar la diferencia de un arqueo (con monto)"),
        ("Receipts.Day",                "Close",              "Cerrar el día de un punto de recaudo"),
        ("Receipts.Vault",              "View",               "Ver la bóveda de la oficina"),
        ("Receipts.Vault",              "Move",               "Registrar movimientos de bóveda"),
        ("Receipts.Vault",              "Remit",              "Despachar remesas de efectivo al banco"),
        // ----------------------------------------------------------------------------------------------------- Recaudo · recibo --
        ("Receipts.Receipt",            "Register",           "Registrar recibos de caja"),
        ("Receipts.Receipt",            "RegisterContingency", "Digitar recibos de contingencia del talonario"),
        ("Receipts.Receipt",            "View",               "Ver recibos"),
        ("Receipts.Receipt",            "Print",              "Imprimir y reimprimir recibos"),
        ("Receipts.Receipt",            "Backdate",           "Registrar un recibo con fecha valor anterior (con monto)"),
        ("Receipts.Voiding",            "Request",            "Pedir la anulación de un recibo"),
        ("Receipts.Voiding",            "Approve",            "Aprobar la anulación de un recibo (con monto)"),
        ("Receipts.Reversals",          "View",               "Ver las aplicaciones reversadas por Cartera o Captaciones"),
        ("Receipts.Reversals",          "Resolve",            "Resolver el dinero de una aplicación reversada"),
        ("Receipts.CreditBalances",     "View",               "Ver saldos a favor de pagadores"),
        ("Receipts.CreditBalances",     "Resolve",            "Aplicar o devolver un saldo a favor"),
        ("Receipts.Checks",             "View",               "Ver cheques recibidos"),
        ("Receipts.Checks",             "Manage",             "Consignar y llevar el canje de los cheques recibidos"),
        ("Receipts.Checks",             "Return",             "Registrar la devolución de un cheque recibido"),
        // --------------------------------------------------------------------------------------- Recaudo · bancos, pagadurías --
        ("Receipts.BankItems",          "View",               "Ver los abonos del extracto por identificar"),
        ("Receipts.BankItems",          "Identify",           "Identificar y aplicar abonos bancarios"),
        ("Receipts.BankItems",          "Classify",           "Clasificar un abono como partida por identificar"),
        ("Receipts.UnidentifiedItems",  "View",               "Ver partidas por identificar"),
        ("Receipts.UnidentifiedItems",  "Resolve",            "Identificar, aplicar o devolver una partida"),
        ("Receipts.UnidentifiedItems",  "MoveToIncome",       "Pasar una partida vencida a ingreso con aprobación"),
        ("Receipts.Paymasters",         "View",               "Ver archivos, respuestas y giros de pagadurías"),
        ("Receipts.Paymasters",         "ConfirmSent",        "Confirmar el envío de un archivo de descuentos"),
        ("Receipts.Paymasters",         "LoadResponse",       "Cargar la respuesta de una pagaduría"),
        ("Receipts.Paymasters",         "RegisterRemittance", "Registrar el giro de una pagaduría"),
        ("Receipts.Paymasters",         "ApplyRemittance",    "Aplicar el giro de una pagaduría"),
        // -------------------------------------------------------------------------------------------- Recaudo · cumplimiento --
        ("Receipts.Compliance",         "View",               "Ver señales de efectivo y coincidencias en listas"),
        ("Receipts.Compliance",         "Release",            "Liberar una operación retenida por cumplimiento"),
        ("Receipts.Sarlaft",            "Report",             "Generar y leer el reporte de transacciones en efectivo"),
        // ------------------------------------------------------------------------------ Recaudo · contabilidad, activación, informes --
        ("Receipts.Accounting",         "PolicyChange",       "Cambiar la política contable de Recaudo (bloquear o advertir)"),
        ("Receipts.Activation",         "View",               "Ver los requisitos de la activación de Recaudo"),
        ("Receipts.Activation",         "Activate",           "Activar Recaudo"),
        ("Receipts.Activation",         "Square",             "Registrar el cuadre definitivo de la puesta en marcha"),
        ("Receipts.Activation",         "ProvisionalSquare",  "Registrar el cuadre provisional de la puesta en marcha"),
        ("Receipts.Imports",            "Run",                "Cargar las plantillas de puesta en marcha de Recaudo"),
        ("Receipts.Reports",            "View",               "Ver los informes de Recaudo"),
        ("Receipts.Reports",            "Export",             "Exportar los informes de Recaudo"),
        // ------------------------------------------------------------------------------------------------- Recaudo · R1 y R2 --
        ("Receipts.PendingApplications", "View",              "Ver el recaudo pendiente de aplicar (R1)"),
        ("Receipts.PendingApplications", "Apply",             "Aplicar el recaudo pendiente en el módulo ya activado (R1)"),
        ("Receipts.Disbursements",      "View",               "Ver egresos de caja (R1)"),
        ("Receipts.Disbursements",      "Register",           "Registrar egresos de caja (R1)"),
        ("Receipts.Disbursements",      "Approve",            "Aprobar egresos de caja (R1)"),
        ("Receipts.Agreements",         "View",               "Ver convenios de recaudo (R2)"),
        ("Receipts.Agreements",         "Manage",             "Administrar convenios de recaudo (R2)"),
        ("Receipts.Agreements",         "LoadFile",           "Cargar el archivo de un convenio de recaudo (R2)"),

        // --------------------------------------------------------------------------------------- Tesorería · cuentas y fondo --
        ("Treasury.BankAccounts",       "View",               "Ver las cuentas bancarias propias"),
        ("Treasury.BankAccounts",       "Manage",             "Administrar las cuentas bancarias propias"),
        ("Treasury.BankBalances",       "View",               "Ver saldos bancarios (lectura auditada)"),
        // Decisión del dueño del 2026-10-23 (DT §2.7): el alcance total de Tesorería, como Receipts.Scope.All; el administrador lo trae.
        ("Treasury.Scope",              "All",                "Alcance sobre todas las cuentas bancarias propias sin filas de asignación"),
        ("Treasury.Scope",              "Manage",             "Asignar cuentas bancarias a los usuarios"),
        ("Treasury.LiquidityFund",      "Request",            "Pedir el uso del fondo de liquidez"),
        ("Treasury.LiquidityFund",      "Approve",            "Aprobar el uso del fondo de liquidez (con monto)"),
        // ------------------------------------------------------------------------------------- Tesorería · órdenes, lotes, archivos --
        ("Treasury.Orders",             "View",               "Ver la bandeja de órdenes de pago"),
        ("Treasury.Orders",             "Manage",             "Preparar, retener y anular órdenes de pago"),
        ("Treasury.Batches",            "Prepare",            "Armar lotes de pago"),
        ("Treasury.Batches",            "Approve",            "Aprobar lotes de pago (con monto)"),
        ("Treasury.PaymentFiles",       "Generate",           "Generar el archivo de pago de un lote"),
        ("Treasury.PaymentFiles",       "Download",           "Descargar el archivo de pago"),
        ("Treasury.PaymentFiles",       "MarkSent",           "Marcar enviado el archivo de pago"),
        ("Treasury.PaymentFiles",       "Void",               "Anular un archivo de pago sin enviar"),
        ("Treasury.Results",            "Register",           "Registrar los resultados de los giros"),
        // ---------------------------------------------------------------------------------------- Tesorería · chequeras, cheques --
        ("Treasury.Checkbooks",         "View",               "Ver chequeras"),
        ("Treasury.Checkbooks",         "Manage",             "Administrar chequeras"),
        ("Treasury.Checks",             "View",               "Ver cheques girados"),
        ("Treasury.Checks",             "Deliver",            "Entregar cheques girados"),
        ("Treasury.Checks",             "Void",               "Anular cheques girados"),
        ("Treasury.Checks",             "Print",              "Imprimir cheques"),
        // ------------------------------------------------------------------------- Tesorería · remesas, extractos, conciliación --
        ("Treasury.Remittances",        "View",               "Ver remesas de efectivo"),
        ("Treasury.Remittances",        "Confirm",            "Confirmar remesas en el banco"),
        ("Treasury.Statements",         "Load",               "Cargar extractos bancarios"),
        ("Treasury.Statements",         "View",               "Ver extractos bancarios"),
        ("Treasury.Reconciliation",     "View",               "Ver conciliaciones bancarias"),
        ("Treasury.Reconciliation",     "Work",               "Cruzar y registrar partidas en la conciliación"),
        ("Treasury.Reconciliation",     "Close",              "Cerrar una conciliación"),
        ("Treasury.Reconciliation",     "Reopen",             "Reabrir una conciliación cerrada"),
        // ------------------------------------------------------------------------ Tesorería · contabilidad, activación, informes --
        ("Treasury.Accounting",         "PolicyChange",       "Cambiar la política contable de Tesorería (bloquear o advertir)"),
        ("Treasury.Activation",         "View",               "Ver los requisitos de la activación de Tesorería"),
        ("Treasury.Activation",         "Activate",           "Activar Tesorería"),
        ("Treasury.Activation",         "Square",             "Registrar el cuadre definitivo de la puesta en marcha"),
        ("Treasury.Activation",         "ProvisionalSquare",  "Registrar el cuadre provisional de la puesta en marcha"),
        ("Treasury.Imports",            "Run",                "Cargar las plantillas de puesta en marcha de Tesorería"),
        ("Treasury.Reports",            "View",               "Ver los informes de Tesorería"),
        ("Treasury.Reports",            "Export",             "Exportar los informes de Tesorería"),
        // ------------------------------------------------------------------------------------------------ Tesorería · T1 y T2 --
        ("Treasury.ManualOrders",       "Create",             "Crear órdenes de pago manuales (T1)"),
        ("Treasury.ManualOrders",       "Approve",            "Aprobar órdenes de pago manuales (T1)"),
        ("Treasury.Transfers",          "Request",            "Pedir traslados entre cuentas propias (T1)"),
        ("Treasury.Transfers",          "Approve",            "Aprobar traslados entre cuentas propias (T1, con monto)"),
        ("Treasury.ManualOrders",       "ManageConcepts",     "Administrar el catálogo cerrado de conceptos de órdenes manuales (T1)"),
        ("Treasury.Signers",            "Manage",             "Administrar firmantes y tokens de las cuentas (T1)"),
        ("Treasury.Signers",            "Approve",            "Aprobar el alta o el retiro de firmantes y tokens (T1)"),
        ("Treasury.BankNotes",          "Register",           "Registrar notas bancarias (T2)"),
        ("Treasury.Payables",           "View",               "Ver cuentas por pagar (T2)"),
        ("Treasury.Payables",           "Manage",             "Administrar cuentas por pagar (T2)"),
        ("Treasury.Payables",           "Pay",                "Programar el pago de cuentas por pagar (T2)"),

        // ------------------------------------------------------------------------------------------------- plataforma (E-06) --
        ("Core.PersonBankAccounts",     "View",               "Ver las cuentas bancarias de una persona (lectura auditada)"),
        ("Core.PersonBankAccounts",     "Request",            "Pedir el alta o el cambio de una cuenta bancaria de una persona"),
        ("Core.PersonBankAccounts",     "Approve",            "Aprobar, rechazar o retirar una cuenta bancaria de una persona (otra persona)"),
        ("Platform.GoLive",             "View",               "Ver las listas de go/no-go"),
        ("Platform.GoLive",             "Manage",             "Crear listas de go/no-go, verificar sus condiciones y decidir"),
        ("Platform.GoLive",             "Override",           "Aprobar seguir con una condición en rojo, con motivo"),
        // ------------------------------------------------------------------------------------------------ Contabilidad (E-04) --
        ("Accounting.ReceiptsMatrix",   "View",               "Ver la matriz contable de Recaudo"),
        ("Accounting.ReceiptsMatrix",   "Manage",             "Administrar la matriz contable de Recaudo"),
        ("Accounting.TreasuryMatrix",   "View",               "Ver la matriz contable de Tesorería"),
        ("Accounting.TreasuryMatrix",   "Manage",             "Administrar la matriz contable de Tesorería"),
    ];

    /// <summary>
    /// Los «View» que son lecturas sensibles (R-32): <see cref="BuiltInRolesSeeder"/> los excluye del <c>*.View</c> de Operator, ReadOnly y
    /// Auditor; se dan con las plantillas de rol de cada módulo.
    /// </summary>
    public static readonly string[] LecturasSensibles = ["Core.PersonBankAccounts.View", "Treasury.BankBalances.View", "Receipts.Session.ViewExpected"];

    public static async Task SeedAsync(IApplicationDbContext db, ILogger logger)
    {
        var claves = Catalog.Select(p => $"{p.Resource}.{p.Action}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Los recursos son pocos: se leen los de los seis prefijos (uno por consulta, que EF traduce) y se filtran por clave en memoria.
        var existentes = new List<Permission>();
        foreach (var prefijo in Prefijos)
            existentes.AddRange(await db.Permissions.IgnoreQueryFilters().Where(p => p.Resource.StartsWith(prefijo)).ToListAsync());
        existentes = existentes.DistinctBy(p => p.Id).Where(p => claves.Contains($"{p.Resource}.{p.Action}")).ToList();

        var yaEstan = existentes.Select(p => $"{p.Resource}.{p.Action}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var descripciones = Catalog.ToDictionary(p => $"{p.Resource}.{p.Action}", p => p.Description, StringComparer.OrdinalIgnoreCase);
        var actualizadas = 0;
        foreach (var p in existentes)
        {
            if (descripciones.TryGetValue($"{p.Resource}.{p.Action}", out var descripcion) && p.Description != descripcion)
            {
                p.Description = descripcion;
                p.UpdatedBy = "Seed";
                actualizadas++;
            }
        }

        var nuevos = Catalog
            .Where(p => !yaEstan.Contains($"{p.Resource}.{p.Action}"))
            .Select(p => new Permission
            {
                Resource = p.Resource,
                Action = p.Action,
                Description = p.Description,
                CreatedBy = "Seed",
                UpdatedBy = "Seed",
            })
            .ToList();

        if (nuevos.Count == 0 && actualizadas == 0)
        {
            logger.LogDebug("Permisos de Recaudo y Tesorería ya presentes ({Count}).", Catalog.Length);
            return;
        }

        db.Permissions.AddRange(nuevos);
        await db.SaveChangesAsync(default);
        logger.LogInformation("Permisos de Recaudo y Tesorería: {Inserted} nuevos de {Total}, {Updated} descripciones puestas al día.",
            nuevos.Count, Catalog.Length, actualizadas);
    }
}
