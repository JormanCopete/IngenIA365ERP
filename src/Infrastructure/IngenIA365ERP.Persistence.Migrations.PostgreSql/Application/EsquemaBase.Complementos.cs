using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application;

/// <summary>
/// Lo que las migraciones absorbidas por <see cref="EsquemaBase"/> dejaban en una base NUEVA y que el modelo no
/// conoce, copiado tal cual y en el mismo orden (consolidación de migraciones, 2026-11-15). Se generó con
/// <c>tools/scripts/consolidar-migraciones/generar_datos.py</c> desde git; no se edita a mano.
///
/// <para>Quedó fuera, a propósito, todo lo que sobre una base vacía no hace nada: los rellenos de columnas nuevas
/// (<c>UPDATE</c> sobre filas que en una base nueva no existen), las copias desde tablas que nacen vacías y las guardas
/// de las migraciones destructivas. La prueba de equivalencia compara las filas de una base creada por el camino viejo
/// con las de una creada por éste.</para>
/// </summary>
public partial class EsquemaBase
{
    /// <summary>
    /// La extensión que las búsquedas por trigramas necesitan antes de crear sus índices GIN
    /// (<c>gin_trgm_ops</c>). La creaba a mano <c>InventarioComercialNucleo</c>: el modelo no la declara.
    /// </summary>
    private static void CrearExtensiones(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
    }

    /// <summary>
    /// Los 16 valores por defecto que el camino viejo dejó en columnas agregadas con <c>defaultValue</c> y que el
    /// modelo no declara. EF siempre manda esas columnas, pero un INSERT escrito a mano que las omita funciona en
    /// las bases existentes; sin esto fallaría en una cooperativa nueva. Salen de comparar catálogos.
    /// </summary>
    private static void RestituirValoresPorDefectoHeredados(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE dbo."ACC_FixedAssets" ALTER COLUMN "OpeningAccumulated" SET DEFAULT 0.0;
            ALTER TABLE dbo."ACC_FixedAssets" ALTER COLUMN "OpeningElapsedMonths" SET DEFAULT 0;
            ALTER TABLE dbo."COR_NotificationTemplates" ALTER COLUMN "ValidFrom" SET DEFAULT '-infinity'::date;
            ALTER TABLE dbo."COR_People" ALTER COLUMN "Username" SET DEFAULT ''::character varying;
            ALTER TABLE dbo."LND_DepositLineVersions" ALTER COLUMN "GmfOnDeceasedAndUnclaimed" SET DEFAULT true;
            ALTER TABLE dbo."PAY_Employees" ALTER COLUMN "EmployeeClass" SET DEFAULT 0;
            ALTER TABLE dbo."PAY_Employees" ALTER COLUMN "PayrollPlanId" SET DEFAULT 0;
            ALTER TABLE dbo."PAY_LegalParameters" ALTER COLUMN "RangeIsMarginal" SET DEFAULT false;
            ALTER TABLE dbo."PAY_PayPeriods" ALTER COLUMN "PayrollPlanId" SET DEFAULT 0;
            ALTER TABLE dbo."PAY_PayrollRunEmployees" ALTER COLUMN "BasesJson" SET DEFAULT ''::text;
            ALTER TABLE dbo."PAY_PayrollRunEmployees" ALTER COLUMN "NotesJson" SET DEFAULT ''::text;
            ALTER TABLE dbo."PAY_PensionProviders" ALTER COLUMN "IsAccai" SET DEFAULT false;
            ALTER TABLE dbo."PAY_RecurringNovelties" ALTER COLUMN "ApplyOn" SET DEFAULT 0;
            ALTER TABLE dbo."PAY_WithholdingParameters" ALTER COLUMN "PayrollPlanId" SET DEFAULT 0;
            ALTER TABLE dbo."RCD_ReceivedChecks" ALTER COLUMN "IsOpeningLoad" SET DEFAULT false;
            ALTER TABLE dbo."TES_CashRemittances" ALTER COLUMN "IsOpeningLoad" SET DEFAULT false;
            """);
    }

    /// <summary>Las filas fijas que sembraban las migraciones absorbidas (12 tablas, 72 filas).</summary>
    private static void SembrarLoQueSembrabanLasAbsorbidas(MigrationBuilder migrationBuilder)
    {
        // 20260905194621_NominaNovedadesYLiquidacion, llamada 1 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO "dbo"."PAY_PayrollPlans"
                ("Code", "Name", "Periodicity", "IsDefault", "IsActive",
                 "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT 'DEFAULT', 'Nómina general', 30, TRUE, TRUE,
                   gen_random_uuid(), FALSE, now() at time zone 'utc', 'migracion:NominaNovedadesYLiquidacion'
            WHERE NOT EXISTS (SELECT 1 FROM "dbo"."PAY_PayrollPlans" WHERE "IsDefault" = TRUE);

            UPDATE "dbo"."PAY_PayPeriods"
               SET "PayrollPlanId" = (SELECT "Id" FROM "dbo"."PAY_PayrollPlans" WHERE "IsDefault" = TRUE LIMIT 1)
             WHERE "PayrollPlanId" = 0;

            UPDATE "dbo"."PAY_Employees"
               SET "PayrollPlanId" = (SELECT "Id" FROM "dbo"."PAY_PayrollPlans" WHERE "IsDefault" = TRUE LIMIT 1)
             WHERE "PayrollPlanId" = 0;
            """);

        // 20260925232507_PlataformaParaInventario, llamada 1 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."COR_BackgroundLeases" ("Name", "Owner", "LeaseUntil", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT v.name, NULL, TIMESTAMPTZ '2000-01-01 00:00:00+00', gen_random_uuid(), FALSE, now(), 'SYSTEM'
            FROM (VALUES ('integration.dispatch'), ('audit.forward'), ('einvoicing.process'), ('scheduled.tasks'), ('email.dispatch')) AS v(name)
            WHERE NOT EXISTS (SELECT 1 FROM dbo."COR_BackgroundLeases" l WHERE l."Name" = v.name);
            """);

        // 20261006075037_PlataformaParaCartera, llamada 2 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."COR_BackgroundLeases" ("Name", "Owner", "LeaseUntil", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT v.name, NULL, TIMESTAMPTZ '2000-01-01 00:00:00+00', gen_random_uuid(), FALSE, now(), 'SYSTEM'
            FROM (VALUES ('lending.process')) AS v(name)
            WHERE NOT EXISTS (SELECT 1 FROM dbo."COR_BackgroundLeases" l WHERE l."Name" = v.name);
            """);

        // 20261006075037_PlataformaParaCartera, llamada 3 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."COR_IntegrationParticipants" ("Code", "Class", "Role", "Destination", "IsTestDouble", "IsEnabled", "ValidFrom", "ValidTo", "Reason", "RegisteredByKind", "RegisteredByUserId", "RegisteredByName", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT v.code, v.clase, v.rol, v.destino, FALSE, v.habilitado, DATE '1900-01-01', NULL, v.motivo, 2, NULL, 'Migración PlataformaParaCartera', gen_random_uuid(), FALSE, now(), 'SYSTEM'
            FROM (VALUES
                ('CNT', 6, 2, 'Accounting', TRUE,  'Contabilidad consume la bandeja desde la feature 012'),
                ('INV', 8, 2, 'Inventory',  TRUE,  'Comercial recibe las respuestas de Cartera a la venta a crédito'),
                ('NOM', 2, 1, '',           TRUE,  'Nómina recauda obligaciones por descuento de nómina'),
                ('CAR', 7, 2, 'Lending',    FALSE, 'Destino de Cartera: inhabilitado hasta la activación')
            ) AS v(code, clase, rol, destino, habilitado, motivo)
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo."COR_IntegrationParticipants" p
                WHERE p."Code" = v.code AND p."Role" = v.rol AND p."Destination" = v.destino AND p."IsDeleted" = FALSE);
            """);

        // 20261006075037_PlataformaParaCartera, llamada 4 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."COR_IntegrationParticipantAdoptions" ("ParticipantId", "ContractName", "Version", "ValidFrom", "ValidTo", "Reason", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT p."Id", v.contrato, CAST(v.version AS smallint), DATE '1900-01-01', NULL, 'Migración PlataformaParaCartera', gen_random_uuid(), FALSE, now(), 'SYSTEM'
            FROM (VALUES
                ('INV', 2, 'Inventory', 'ObligacionCreada', 1),
                ('INV', 2, 'Inventory', 'ObligacionRechazada', 1),
                ('NOM', 1, '',          'ProveedorDeObligaciones', 1),
                ('CAR', 2, 'Lending',   'VentaACreditoRegistrada', 1),
                ('CAR', 2, 'Lending',   'VentaACreditoRegistrada', 2),
                ('CAR', 2, 'Lending',   'AjusteDeVentaACredito', 1),
                ('CAR', 2, 'Lending',   'ResultadoDelGiro', 1)
            ) AS v(code, rol, destino, contrato, version)
            JOIN dbo."COR_IntegrationParticipants" p
              ON p."Code" = v.code AND p."Role" = v.rol AND p."Destination" = v.destino AND p."ValidTo" IS NULL AND p."IsDeleted" = FALSE
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo."COR_IntegrationParticipantAdoptions" a
                WHERE a."ParticipantId" = p."Id" AND a."ContractName" = v.contrato AND a."Version" = v.version);
            """);

        // 20261015040557_CarteraNucleo, llamada 1 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."LND_Setup" ("Id", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
            OVERRIDING SYSTEM VALUE
            SELECT 1, gen_random_uuid(), FALSE, now(), 'SYSTEM'
            WHERE NOT EXISTS (SELECT 1 FROM dbo."LND_Setup");
            SELECT setval(pg_get_serial_sequence('dbo."LND_Setup"', 'Id'), GREATEST((SELECT MAX("Id") FROM dbo."LND_Setup"), 1));
            """);

        // 20261015140615_CarteraLibranza, llamada 1 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."COR_IntegrationParticipantAdoptions" ("ParticipantId", "ContractName", "Version", "ValidFrom", "ValidTo", "Reason", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT p."Id", v.contrato, CAST(v.version AS smallint), DATE '1900-01-01', NULL, 'Migración CarteraLibranza', gen_random_uuid(), FALSE, now(), 'SYSTEM'
            FROM (VALUES
                ('EnvioDeDescuentosConfirmado', 1),
                ('ResultadoDeDescuentos', 1)
            ) AS v(contrato, version)
            JOIN dbo."COR_IntegrationParticipants" p
              ON p."Code" = 'CAR' AND p."Role" = 2 AND p."Destination" = 'Lending' AND p."ValidTo" IS NULL AND p."IsDeleted" = FALSE
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo."COR_IntegrationParticipantAdoptions" a
                WHERE a."ParticipantId" = p."Id" AND a."ContractName" = v.contrato AND a."Version" = v.version);
            """);

        // 20261019104128_CaptacionesAportes, llamada 2 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."COR_IntegrationParticipantAdoptions" ("ParticipantId", "ContractName", "Version", "ValidFrom", "ValidTo", "Reason", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT p."Id", 'ProveedorDeCuentas', CAST(1 AS smallint), DATE '1900-01-01', NULL, 'Migración CaptacionesAportes', gen_random_uuid(), FALSE, now(), 'SYSTEM'
            FROM dbo."COR_IntegrationParticipants" p
            WHERE p."Code" = 'NOM' AND p."Role" = 1 AND p."Destination" = '' AND p."ValidTo" IS NULL AND p."IsDeleted" = FALSE
              AND NOT EXISTS (
                SELECT 1 FROM dbo."COR_IntegrationParticipantAdoptions" a
                WHERE a."ParticipantId" = p."Id" AND a."ContractName" = 'ProveedorDeCuentas' AND a."Version" = 1);
            """);

        // 20261019104128_CaptacionesAportes, llamada 3 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."COR_IntegrationParticipants" ("Code", "Class", "Role", "Destination", "IsTestDouble", "IsEnabled", "ValidFrom", "ValidTo", "Reason", "RegisteredByKind", "RegisteredByUserId", "RegisteredByName", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT 'ASO', 9, 2, 'Associates', FALSE, TRUE, DATE '1900-01-01', NULL, 'Asociados recibe el retiro del asociado aprobado en Cartera', 2, NULL, 'Migración CaptacionesAportes', gen_random_uuid(), FALSE, now(), 'SYSTEM'
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo."COR_IntegrationParticipants" p
                WHERE p."Code" = 'ASO' AND p."Role" = 2 AND p."Destination" = 'Associates' AND p."IsDeleted" = FALSE);

            INSERT INTO dbo."COR_IntegrationParticipantAdoptions" ("ParticipantId", "ContractName", "Version", "ValidFrom", "ValidTo", "Reason", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT p."Id", 'RetiroDeAsociadoAprobado', CAST(1 AS smallint), DATE '1900-01-01', NULL, 'Migración CaptacionesAportes', gen_random_uuid(), FALSE, now(), 'SYSTEM'
            FROM dbo."COR_IntegrationParticipants" p
            WHERE p."Code" = 'ASO' AND p."Role" = 2 AND p."Destination" = 'Associates' AND p."ValidTo" IS NULL AND p."IsDeleted" = FALSE
              AND NOT EXISTS (
                SELECT 1 FROM dbo."COR_IntegrationParticipantAdoptions" a
                WHERE a."ParticipantId" = p."Id" AND a."ContractName" = 'RetiroDeAsociadoAprobado' AND a."Version" = 1);
            """);

        // 20261024113511_RecaudoNucleo, llamada 1 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."RCD_Setup" ("Id", "PublicId", "Status", "CutoffTime", "ContractVersionObligations", "ContractVersionAccounts", "IsDeleted", "CreatedAt", "CreatedBy")
            OVERRIDING SYSTEM VALUE
            SELECT 1, gen_random_uuid(), 1, TIME '18:00', 1, 1, FALSE, now(), 'SYSTEM'
            WHERE NOT EXISTS (SELECT 1 FROM dbo."RCD_Setup");
            SELECT setval(pg_get_serial_sequence('dbo."RCD_Setup"', 'Id'), GREATEST((SELECT MAX("Id") FROM dbo."RCD_Setup"), 1));
            """);

        // 20261024113511_RecaudoNucleo, llamada 2 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."RCD_DocumentTypes" ("PublicId", "Code", "Name", "Kind", "UsesPointPrefix", "Status", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT gen_random_uuid(), v.code, v.name, v.kind, FALSE, 1, FALSE, now(), 'SYSTEM'
            FROM (VALUES ('RC', 'Recibo de caja', 1), ('RB', 'Recibo de banco', 2), ('RP', 'Recibo de pagaduría', 4), ('RT', 'Recibo de contingencia', 6), ('AN', 'Anulación de recibo', 8), ('MC', 'Movimiento de caja', 9), ('AS', 'Ajuste de sesión de caja', 10)) AS v(code, name, kind)
            WHERE NOT EXISTS (SELECT 1 FROM dbo."RCD_DocumentTypes" t WHERE t."Code" = v.code AND t."IsDeleted" = FALSE)
              AND NOT EXISTS (SELECT 1 FROM dbo."RCD_DocumentTypes" t WHERE t."Kind" = v.kind AND t."Status" = 1 AND t."IsDeleted" = FALSE);
            INSERT INTO dbo."RCD_MiscConcepts" ("PublicId", "Code", "Name", "Direction", "RequiresThirdParty", "RequiresCrossDocument", "RequiresInvoice", "RequiresApproval", "IsSystem", "Status", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT gen_random_uuid(), v.code, v.name, 1, FALSE, FALSE, FALSE, FALSE, TRUE, 1, FALSE, now(), 'SYSTEM'
            FROM (VALUES ('GIRODEV', 'Giro devuelto'), ('SOBRANTE', 'Sobrantes de caja'), ('REINTEGRO', 'Reintegros'), ('RENDIM', 'Rendimientos')) AS v(code, name)
            WHERE NOT EXISTS (SELECT 1 FROM dbo."RCD_MiscConcepts" c WHERE c."Code" = v.code AND c."IsDeleted" = FALSE);
            INSERT INTO dbo."RCD_ChannelModalityRules" ("PublicId", "Channel", "Target", "LoanModality", "AccountModality", "OnlyWhenInstallmentsDue", "Priority", "ValidFrom", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT gen_random_uuid(), v.channel, v.target, v.loan, v.account, v.due, v.priority, DATE '2026-01-01', FALSE, now(), 'SYSTEM'
            FROM (VALUES (2, 1, 3, NULL, FALSE, 1), (2, 2, NULL, 2, TRUE, 1), (2, 2, NULL, 1, FALSE, 2), (3, 1, 3, NULL, FALSE, 1), (3, 2, NULL, 2, TRUE, 1), (3, 2, NULL, 1, FALSE, 2), (6, 1, 3, NULL, FALSE, 1), (6, 2, NULL, 2, TRUE, 1), (6, 2, NULL, 1, FALSE, 2)) AS v(channel, target, loan, account, due, priority)
            WHERE NOT EXISTS (SELECT 1 FROM dbo."RCD_ChannelModalityRules" r WHERE r."Channel" = v.channel AND r."Target" = v.target AND r."IsDeleted" = FALSE);
            -- Feature 015, US15 (T0216): las causales de devolución de un cheque, pendientes de validar con el dueño.
            INSERT INTO dbo."RCD_CheckReturnCauses" ("PublicId", "Code", "Description", "CountsForDrawerList", "IsActive", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT gen_random_uuid(), v.code, v.descr, v.cuenta, TRUE, FALSE, now(), 'SYSTEM'
            FROM (VALUES ('01', 'Fondos insuficientes', TRUE), ('02', 'Cuenta cancelada', TRUE), ('03', 'Cuenta embargada', TRUE), ('04', 'Firma no registrada o no coincide', FALSE), ('05', 'Cheque mal girado o con enmendaduras', FALSE), ('06', 'Fecha posterior a la de presentación', FALSE), ('07', 'Orden de no pago del girador', TRUE), ('08', 'Cheque alterado o con sello falso', FALSE), ('09', 'Presentación fuera del plazo', FALSE)) AS v(code, descr, cuenta)
            WHERE NOT EXISTS (SELECT 1 FROM dbo."RCD_CheckReturnCauses" c WHERE c."Code" = v.code);
            """);

        // 20261024113647_TesoreriaNucleo, llamada 1 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."TES_Setup" ("Id", "PublicId", "Status", "ResultWithThirdPerson", "IsDeleted", "CreatedAt", "CreatedBy")
            OVERRIDING SYSTEM VALUE
            SELECT 1, gen_random_uuid(), 1, FALSE, FALSE, now(), 'SYSTEM'
            WHERE NOT EXISTS (SELECT 1 FROM dbo."TES_Setup");
            SELECT setval(pg_get_serial_sequence('dbo."TES_Setup"', 'Id'), GREATEST((SELECT MAX("Id") FROM dbo."TES_Setup"), 1));
            """);

        // 20261024185201_RecaudoCompleto, llamada 1 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."RCD_DocumentTypes" ("PublicId", "Code", "Name", "Kind", "UsesPointPrefix", "Status", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT gen_random_uuid(), v.code, v.name, v.kind, FALSE, 1, FALSE, now(), 'SYSTEM'
            FROM (VALUES ('EG', 'Egreso de caja', 7)) AS v(code, name, kind)
            WHERE NOT EXISTS (SELECT 1 FROM dbo."RCD_DocumentTypes" t WHERE t."Code" = v.code AND t."IsDeleted" = FALSE)
              AND NOT EXISTS (SELECT 1 FROM dbo."RCD_DocumentTypes" t WHERE t."Kind" = v.kind AND t."Status" = 1 AND t."IsDeleted" = FALSE);
            """);

        // 20261026164905_RecaudoCanales, llamada 1 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."RCD_DocumentTypes" ("PublicId", "Code", "Name", "Kind", "UsesPointPrefix", "Status", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT gen_random_uuid(), v.code, v.name, v.kind, FALSE, 1, FALSE, now(), 'SYSTEM'
            FROM (VALUES ('RV', 'Recibo de convenio', 3), ('RL', 'Recibo en línea', 5)) AS v(code, name, kind)
            WHERE NOT EXISTS (SELECT 1 FROM dbo."RCD_DocumentTypes" t WHERE t."Code" = v.code AND t."IsDeleted" = FALSE)
              AND NOT EXISTS (SELECT 1 FROM dbo."RCD_DocumentTypes" t WHERE t."Kind" = v.kind AND t."Status" = 1 AND t."IsDeleted" = FALSE);
            INSERT INTO dbo."RCD_ChannelModalityRules" ("PublicId", "Channel", "Target", "LoanModality", "AccountModality", "OnlyWhenInstallmentsDue", "Priority", "ValidFrom", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT gen_random_uuid(), v.channel, v.target, v.loan, v.account, v.due, v.priority, DATE '2026-01-01', FALSE, now(), 'SYSTEM'
            FROM (VALUES (4, 1, 3, NULL, FALSE, 1), (4, 2, NULL, 2, TRUE, 1), (4, 2, NULL, 1, FALSE, 2), (5, 1, 3, NULL, FALSE, 1), (5, 2, NULL, 2, TRUE, 1), (5, 2, NULL, 1, FALSE, 2)) AS v(channel, target, loan, account, due, priority)
            WHERE NOT EXISTS (SELECT 1 FROM dbo."RCD_ChannelModalityRules" r WHERE r."Channel" = v.channel AND r."Target" = v.target AND r."IsDeleted" = FALSE);
            """);

        // 20261026185302_TesoreriaAmpliada, llamada 1 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."COR_IntegrationParticipantAdoptions" ("ParticipantId", "ContractName", "Version", "ValidFrom", "ValidTo", "Reason", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT p."Id", v.contrato, CAST(v.version AS smallint), DATE '1900-01-01', NULL, 'Migración TesoreriaAmpliada', gen_random_uuid(), FALSE, now(), 'SYSTEM'
            FROM (VALUES
                ('INV', 'Inventory', 'PagoDeFacturaDeProveedor', 1),
                ('CAR', 'Lending', 'SaldoDelFondoDeLiquidez', 1),
                ('TES', 'Treasury', 'DispersionDeNominaGenerada', 1),
                ('TES', 'Treasury', 'FacturaDeProveedorPorPagar', 1),
                ('TES', 'Treasury', 'AnulacionDeFacturaDeProveedor', 1)
            ) AS v(code, destino, contrato, version)
            JOIN dbo."COR_IntegrationParticipants" p
              ON p."Code" = v.code AND p."Role" = 2 AND p."Destination" = v.destino AND p."ValidTo" IS NULL AND p."IsDeleted" = FALSE AND p."IsTestDouble" = FALSE
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo."COR_IntegrationParticipantAdoptions" a
                WHERE a."ParticipantId" = p."Id" AND a."ContractName" = v.contrato AND a."Version" = v.version);
            """);

        // 20261026185302_TesoreriaAmpliada, llamada 2 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO dbo."TES_CashFlowCategories" ("Code", "Name", "Direction", "Source", "SortOrder", "IsActive", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
            SELECT v.code, v.nombre, v.sentido, v.fuente, v.orden, TRUE, gen_random_uuid(), FALSE, now(), 'SYSTEM'
            FROM (VALUES
                ('CARTERA', 'Cuotas de Cartera por cobrar', 1, 2, 10),
                ('CAPTAC', 'Cuotas de aportes y ahorros por cobrar', 1, 3, 20),
                ('OTROSING', 'Otros ingresos', 1, 1, 30),
                ('ORDENES', 'Órdenes de pago', 2, 4, 40),
                ('NOMINA', 'Dispersiones de nómina', 2, 5, 50),
                ('OTROSEGR', 'Otros egresos', 2, 1, 60)
            ) AS v(code, nombre, sentido, fuente, orden)
            WHERE NOT EXISTS (SELECT 1 FROM dbo."TES_CashFlowCategories" c WHERE c."Code" = v.code AND c."IsDeleted" = FALSE);
            """);
    }
}
