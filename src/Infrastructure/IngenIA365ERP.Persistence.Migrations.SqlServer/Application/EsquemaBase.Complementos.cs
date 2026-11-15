using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application;

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
    /// Los 16 valores por defecto que el camino viejo dejó en columnas agregadas con <c>defaultValue</c> y que el
    /// modelo no declara. EF siempre manda esas columnas, pero un INSERT escrito a mano que las omita funciona en
    /// las bases existentes; sin esto fallaría en una cooperativa nueva. Salen de comparar catálogos.
    /// </summary>
    private static void RestituirValoresPorDefectoHeredados(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE [dbo].[ACC_FixedAssets] ADD DEFAULT (0.0) FOR [OpeningAccumulated];
            ALTER TABLE [dbo].[ACC_FixedAssets] ADD DEFAULT (0) FOR [OpeningElapsedMonths];
            ALTER TABLE [dbo].[COR_NotificationTemplates] ADD DEFAULT '0001-01-01' FOR [ValidFrom];
            ALTER TABLE [dbo].[COR_People] ADD DEFAULT N'' FOR [Username];
            ALTER TABLE [dbo].[LND_DepositLineVersions] ADD DEFAULT CONVERT([bit],(1)) FOR [GmfOnDeceasedAndUnclaimed];
            ALTER TABLE [dbo].[PAY_Employees] ADD DEFAULT (0) FOR [EmployeeClass];
            ALTER TABLE [dbo].[PAY_Employees] ADD DEFAULT (0) FOR [PayrollPlanId];
            ALTER TABLE [dbo].[PAY_LegalParameters] ADD DEFAULT CONVERT([bit],(0)) FOR [RangeIsMarginal];
            ALTER TABLE [dbo].[PAY_PayPeriods] ADD DEFAULT (0) FOR [PayrollPlanId];
            ALTER TABLE [dbo].[PAY_PayrollRunEmployees] ADD DEFAULT N'' FOR [BasesJson];
            ALTER TABLE [dbo].[PAY_PayrollRunEmployees] ADD DEFAULT N'' FOR [NotesJson];
            ALTER TABLE [dbo].[PAY_PensionProviders] ADD DEFAULT CONVERT([bit],(0)) FOR [IsAccai];
            ALTER TABLE [dbo].[PAY_RecurringNovelties] ADD DEFAULT (0) FOR [ApplyOn];
            ALTER TABLE [dbo].[PAY_WithholdingParameters] ADD DEFAULT (0) FOR [PayrollPlanId];
            ALTER TABLE [dbo].[RCD_ReceivedChecks] ADD DEFAULT CONVERT([bit],(0)) FOR [IsOpeningLoad];
            ALTER TABLE [dbo].[TES_CashRemittances] ADD DEFAULT CONVERT([bit],(0)) FOR [IsOpeningLoad];
            """);
    }

    /// <summary>Las filas fijas que sembraban las migraciones absorbidas (12 tablas, 72 filas).</summary>
    private static void SembrarLoQueSembrabanLasAbsorbidas(MigrationBuilder migrationBuilder)
    {
        // 20260905194555_NominaNovedadesYLiquidacion, llamada 1 de su Up().
        migrationBuilder.Sql("""
            IF NOT EXISTS (SELECT 1 FROM [dbo].[PAY_PayrollPlans] WHERE [IsDefault] = 1)
                INSERT INTO [dbo].[PAY_PayrollPlans]
                    ([Code], [Name], [Periodicity], [IsDefault], [IsActive],
                     [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
                VALUES ('DEFAULT', N'Nómina general', 30, 1, 1,
                        NEWID(), 0, SYSUTCDATETIME(), 'migracion:NominaNovedadesYLiquidacion');

            UPDATE [dbo].[PAY_PayPeriods]
               SET [PayrollPlanId] = (SELECT TOP 1 [Id] FROM [dbo].[PAY_PayrollPlans] WHERE [IsDefault] = 1)
             WHERE [PayrollPlanId] = 0;

            UPDATE [dbo].[PAY_Employees]
               SET [PayrollPlanId] = (SELECT TOP 1 [Id] FROM [dbo].[PAY_PayrollPlans] WHERE [IsDefault] = 1)
             WHERE [PayrollPlanId] = 0;
            """);

        // 20260925232439_PlataformaParaInventario, llamada 1 de su Up().
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[dbo].[COR_BackgroundLeases]') IS NOT NULL
            INSERT INTO [dbo].[COR_BackgroundLeases] ([Name], [Owner], [LeaseUntil], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT v.[Name], NULL, '2000-01-01T00:00:00', NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES (N'integration.dispatch'), (N'audit.forward'), (N'einvoicing.process'), (N'scheduled.tasks'), (N'email.dispatch')) AS v([Name])
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[COR_BackgroundLeases] l WHERE l.[Name] = v.[Name]);
            """);

        // 20261006075000_PlataformaParaCartera, llamada 2 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[COR_BackgroundLeases] ([Name], [Owner], [LeaseUntil], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT v.[Name], NULL, '2000-01-01T00:00:00', NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES (N'lending.process')) AS v([Name])
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[COR_BackgroundLeases] l WHERE l.[Name] = v.[Name]);
            """);

        // 20261006075000_PlataformaParaCartera, llamada 3 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[COR_IntegrationParticipants] ([Code], [Class], [Role], [Destination], [IsTestDouble], [IsEnabled], [ValidFrom], [ValidTo], [Reason], [RegisteredByKind], [RegisteredByUserId], [RegisteredByName], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT v.[Code], v.[Clase], v.[Rol], v.[Destino], 0, v.[Habilitado], '1900-01-01', NULL, v.[Motivo], 2, NULL, N'Migración PlataformaParaCartera', NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES
                (N'CNT', 6, 2, N'Accounting', 1, N'Contabilidad consume la bandeja desde la feature 012'),
                (N'INV', 8, 2, N'Inventory',  1, N'Comercial recibe las respuestas de Cartera a la venta a crédito'),
                (N'NOM', 2, 1, N'',           1, N'Nómina recauda obligaciones por descuento de nómina'),
                (N'CAR', 7, 2, N'Lending',    0, N'Destino de Cartera: inhabilitado hasta la activación')
            ) AS v([Code], [Clase], [Rol], [Destino], [Habilitado], [Motivo])
            WHERE NOT EXISTS (
                SELECT 1 FROM [dbo].[COR_IntegrationParticipants] p
                WHERE p.[Code] = v.[Code] AND p.[Role] = v.[Rol] AND p.[Destination] = v.[Destino] AND p.[IsDeleted] = 0);
            """);

        // 20261006075000_PlataformaParaCartera, llamada 4 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[COR_IntegrationParticipantAdoptions] ([ParticipantId], [ContractName], [Version], [ValidFrom], [ValidTo], [Reason], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT p.[Id], v.[Contrato], CAST(v.[Version] AS smallint), '1900-01-01', NULL, N'Migración PlataformaParaCartera', NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES
                (N'INV', 2, N'Inventory', N'ObligacionCreada', 1),
                (N'INV', 2, N'Inventory', N'ObligacionRechazada', 1),
                (N'NOM', 1, N'',          N'ProveedorDeObligaciones', 1),
                (N'CAR', 2, N'Lending',   N'VentaACreditoRegistrada', 1),
                (N'CAR', 2, N'Lending',   N'VentaACreditoRegistrada', 2),
                (N'CAR', 2, N'Lending',   N'AjusteDeVentaACredito', 1),
                (N'CAR', 2, N'Lending',   N'ResultadoDelGiro', 1)
            ) AS v([Code], [Rol], [Destino], [Contrato], [Version])
            JOIN [dbo].[COR_IntegrationParticipants] p
              ON p.[Code] = v.[Code] AND p.[Role] = v.[Rol] AND p.[Destination] = v.[Destino] AND p.[ValidTo] IS NULL AND p.[IsDeleted] = 0
            WHERE NOT EXISTS (
                SELECT 1 FROM [dbo].[COR_IntegrationParticipantAdoptions] a
                WHERE a.[ParticipantId] = p.[Id] AND a.[ContractName] = v.[Contrato] AND a.[Version] = v.[Version]);
            """);

        // 20261015040531_CarteraNucleo, llamada 1 de su Up().
        migrationBuilder.Sql("""
            SET IDENTITY_INSERT [dbo].[LND_Setup] ON;
            INSERT INTO [dbo].[LND_Setup] ([Id], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT 1, NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LND_Setup]);
            SET IDENTITY_INSERT [dbo].[LND_Setup] OFF;
            """);

        // 20261015140547_CarteraLibranza, llamada 1 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[COR_IntegrationParticipantAdoptions] ([ParticipantId], [ContractName], [Version], [ValidFrom], [ValidTo], [Reason], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT p.[Id], v.[Contrato], CAST(v.[Version] AS smallint), '1900-01-01', NULL, N'Migración CarteraLibranza', NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES
                (N'EnvioDeDescuentosConfirmado', 1),
                (N'ResultadoDeDescuentos', 1)
            ) AS v([Contrato], [Version])
            JOIN [dbo].[COR_IntegrationParticipants] p
              ON p.[Code] = N'CAR' AND p.[Role] = 2 AND p.[Destination] = N'Lending' AND p.[ValidTo] IS NULL AND p.[IsDeleted] = 0
            WHERE NOT EXISTS (
                SELECT 1 FROM [dbo].[COR_IntegrationParticipantAdoptions] a
                WHERE a.[ParticipantId] = p.[Id] AND a.[ContractName] = v.[Contrato] AND a.[Version] = v.[Version]);
            """);

        // 20261019104056_CaptacionesAportes, llamada 2 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[COR_IntegrationParticipantAdoptions] ([ParticipantId], [ContractName], [Version], [ValidFrom], [ValidTo], [Reason], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT p.[Id], N'ProveedorDeCuentas', CAST(1 AS smallint), '1900-01-01', NULL, N'Migración CaptacionesAportes', NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM [dbo].[COR_IntegrationParticipants] p
            WHERE p.[Code] = N'NOM' AND p.[Role] = 1 AND p.[Destination] = N'' AND p.[ValidTo] IS NULL AND p.[IsDeleted] = 0
              AND NOT EXISTS (
                SELECT 1 FROM [dbo].[COR_IntegrationParticipantAdoptions] a
                WHERE a.[ParticipantId] = p.[Id] AND a.[ContractName] = N'ProveedorDeCuentas' AND a.[Version] = 1);
            """);

        // 20261019104056_CaptacionesAportes, llamada 3 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[COR_IntegrationParticipants] ([Code], [Class], [Role], [Destination], [IsTestDouble], [IsEnabled], [ValidFrom], [ValidTo], [Reason], [RegisteredByKind], [RegisteredByUserId], [RegisteredByName], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT N'ASO', 9, 2, N'Associates', 0, 1, '1900-01-01', NULL, N'Asociados recibe el retiro del asociado aprobado en Cartera', 2, NULL, N'Migración CaptacionesAportes', NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
            WHERE NOT EXISTS (
                SELECT 1 FROM [dbo].[COR_IntegrationParticipants] p
                WHERE p.[Code] = N'ASO' AND p.[Role] = 2 AND p.[Destination] = N'Associates' AND p.[IsDeleted] = 0);

            INSERT INTO [dbo].[COR_IntegrationParticipantAdoptions] ([ParticipantId], [ContractName], [Version], [ValidFrom], [ValidTo], [Reason], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT p.[Id], N'RetiroDeAsociadoAprobado', CAST(1 AS smallint), '1900-01-01', NULL, N'Migración CaptacionesAportes', NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM [dbo].[COR_IntegrationParticipants] p
            WHERE p.[Code] = N'ASO' AND p.[Role] = 2 AND p.[Destination] = N'Associates' AND p.[ValidTo] IS NULL AND p.[IsDeleted] = 0
              AND NOT EXISTS (
                SELECT 1 FROM [dbo].[COR_IntegrationParticipantAdoptions] a
                WHERE a.[ParticipantId] = p.[Id] AND a.[ContractName] = N'RetiroDeAsociadoAprobado' AND a.[Version] = 1);
            """);

        // 20261024113421_RecaudoNucleo, llamada 1 de su Up().
        migrationBuilder.Sql("""
            SET IDENTITY_INSERT [dbo].[RCD_Setup] ON;
            INSERT INTO [dbo].[RCD_Setup] ([Id], [PublicId], [Status], [CutoffTime], [ContractVersionObligations], [ContractVersionAccounts], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT 1, NEWID(), 1, CAST('18:00' AS time), 1, 1, 0, SYSUTCDATETIME(), N'SYSTEM'
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_Setup]);
            SET IDENTITY_INSERT [dbo].[RCD_Setup] OFF;
            """);

        // 20261024113421_RecaudoNucleo, llamada 2 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[RCD_DocumentTypes] ([PublicId], [Code], [Name], [Kind], [UsesPointPrefix], [Status], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT NEWID(), v.code, v.name, v.kind, 0, 1, 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES (N'RC', N'Recibo de caja', 1), (N'RB', N'Recibo de banco', 2), (N'RP', N'Recibo de pagaduría', 4), (N'RT', N'Recibo de contingencia', 6), (N'AN', N'Anulación de recibo', 8), (N'MC', N'Movimiento de caja', 9), (N'AS', N'Ajuste de sesión de caja', 10)) AS v(code, name, kind)
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_DocumentTypes] t WHERE t.[Code] = v.code AND t.[IsDeleted] = 0)
              AND NOT EXISTS (SELECT 1 FROM [dbo].[RCD_DocumentTypes] t WHERE t.[Kind] = v.kind AND t.[Status] = 1 AND t.[IsDeleted] = 0);
            INSERT INTO [dbo].[RCD_MiscConcepts] ([PublicId], [Code], [Name], [Direction], [RequiresThirdParty], [RequiresCrossDocument], [RequiresInvoice], [RequiresApproval], [IsSystem], [Status], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT NEWID(), v.code, v.name, 1, 0, 0, 0, 0, 1, 1, 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES (N'GIRODEV', N'Giro devuelto'), (N'SOBRANTE', N'Sobrantes de caja'), (N'REINTEGRO', N'Reintegros'), (N'RENDIM', N'Rendimientos')) AS v(code, name)
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_MiscConcepts] c WHERE c.[Code] = v.code AND c.[IsDeleted] = 0);
            INSERT INTO [dbo].[RCD_ChannelModalityRules] ([PublicId], [Channel], [Target], [LoanModality], [AccountModality], [OnlyWhenInstallmentsDue], [Priority], [ValidFrom], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT NEWID(), v.channel, v.target, v.loan, v.account, v.due, v.priority, CAST('2026-01-01' AS date), 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES (2, 1, 3, NULL, 0, 1), (2, 2, NULL, 2, 1, 1), (2, 2, NULL, 1, 0, 2), (3, 1, 3, NULL, 0, 1), (3, 2, NULL, 2, 1, 1), (3, 2, NULL, 1, 0, 2), (6, 1, 3, NULL, 0, 1), (6, 2, NULL, 2, 1, 1), (6, 2, NULL, 1, 0, 2)) AS v(channel, target, loan, account, due, priority)
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_ChannelModalityRules] r WHERE r.[Channel] = v.channel AND r.[Target] = v.target AND r.[IsDeleted] = 0);
            -- Feature 015, US15 (T0216): las causales de devolución de un cheque, pendientes de validar con el dueño.
            INSERT INTO [dbo].[RCD_CheckReturnCauses] ([PublicId], [Code], [Description], [CountsForDrawerList], [IsActive], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT NEWID(), v.code, v.descr, v.cuenta, 1, 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES (N'01', N'Fondos insuficientes', 1), (N'02', N'Cuenta cancelada', 1), (N'03', N'Cuenta embargada', 1), (N'04', N'Firma no registrada o no coincide', 0), (N'05', N'Cheque mal girado o con enmendaduras', 0), (N'06', N'Fecha posterior a la de presentación', 0), (N'07', N'Orden de no pago del girador', 1), (N'08', N'Cheque alterado o con sello falso', 0), (N'09', N'Presentación fuera del plazo', 0)) AS v(code, descr, cuenta)
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_CheckReturnCauses] c WHERE c.[Code] = v.code);
            """);

        // 20261024113559_TesoreriaNucleo, llamada 1 de su Up().
        migrationBuilder.Sql("""
            SET IDENTITY_INSERT [dbo].[TES_Setup] ON;
            INSERT INTO [dbo].[TES_Setup] ([Id], [PublicId], [Status], [ResultWithThirdPerson], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT 1, NEWID(), 1, 0, 0, SYSUTCDATETIME(), N'SYSTEM'
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[TES_Setup]);
            SET IDENTITY_INSERT [dbo].[TES_Setup] OFF;
            """);

        // 20261024185110_RecaudoCompleto, llamada 1 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[RCD_DocumentTypes] ([PublicId], [Code], [Name], [Kind], [UsesPointPrefix], [Status], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT NEWID(), v.code, v.name, v.kind, 0, 1, 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES (N'EG', N'Egreso de caja', 7)) AS v(code, name, kind)
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_DocumentTypes] t WHERE t.[Code] = v.code AND t.[IsDeleted] = 0)
              AND NOT EXISTS (SELECT 1 FROM [dbo].[RCD_DocumentTypes] t WHERE t.[Kind] = v.kind AND t.[Status] = 1 AND t.[IsDeleted] = 0);
            """);

        // 20261026164807_RecaudoCanales, llamada 1 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[RCD_DocumentTypes] ([PublicId], [Code], [Name], [Kind], [UsesPointPrefix], [Status], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT NEWID(), v.code, v.name, v.kind, 0, 1, 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES (N'RV', N'Recibo de convenio', 3), (N'RL', N'Recibo en línea', 5)) AS v(code, name, kind)
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_DocumentTypes] t WHERE t.[Code] = v.code AND t.[IsDeleted] = 0)
              AND NOT EXISTS (SELECT 1 FROM [dbo].[RCD_DocumentTypes] t WHERE t.[Kind] = v.kind AND t.[Status] = 1 AND t.[IsDeleted] = 0);
            INSERT INTO [dbo].[RCD_ChannelModalityRules] ([PublicId], [Channel], [Target], [LoanModality], [AccountModality], [OnlyWhenInstallmentsDue], [Priority], [ValidFrom], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT NEWID(), v.channel, v.target, v.loan, v.account, v.due, v.priority, CAST('2026-01-01' AS date), 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES (4, 1, 3, NULL, 0, 1), (4, 2, NULL, 2, 1, 1), (4, 2, NULL, 1, 0, 2), (5, 1, 3, NULL, 0, 1), (5, 2, NULL, 2, 1, 1), (5, 2, NULL, 1, 0, 2)) AS v(channel, target, loan, account, due, priority)
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_ChannelModalityRules] r WHERE r.[Channel] = v.channel AND r.[Target] = v.target AND r.[IsDeleted] = 0);
            """);

        // 20261026185203_TesoreriaAmpliada, llamada 1 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[COR_IntegrationParticipantAdoptions] ([ParticipantId], [ContractName], [Version], [ValidFrom], [ValidTo], [Reason], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT p.[Id], v.[Contrato], CAST(v.[Version] AS smallint), '1900-01-01', NULL, N'Migración TesoreriaAmpliada', NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES
                (N'INV', N'Inventory', N'PagoDeFacturaDeProveedor', 1),
                (N'CAR', N'Lending', N'SaldoDelFondoDeLiquidez', 1),
                (N'TES', N'Treasury', N'DispersionDeNominaGenerada', 1),
                (N'TES', N'Treasury', N'FacturaDeProveedorPorPagar', 1),
                (N'TES', N'Treasury', N'AnulacionDeFacturaDeProveedor', 1)
            ) AS v([Code], [Destino], [Contrato], [Version])
            JOIN [dbo].[COR_IntegrationParticipants] p
              ON p.[Code] = v.[Code] AND p.[Role] = 2 AND p.[Destination] = v.[Destino] AND p.[ValidTo] IS NULL AND p.[IsDeleted] = 0 AND p.[IsTestDouble] = 0
            WHERE NOT EXISTS (
                SELECT 1 FROM [dbo].[COR_IntegrationParticipantAdoptions] a
                WHERE a.[ParticipantId] = p.[Id] AND a.[ContractName] = v.[Contrato] AND a.[Version] = v.[Version]);
            """);

        // 20261026185203_TesoreriaAmpliada, llamada 2 de su Up().
        migrationBuilder.Sql("""
            INSERT INTO [dbo].[TES_CashFlowCategories] ([Code], [Name], [Direction], [Source], [SortOrder], [IsActive], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT v.[Code], v.[Nombre], v.[Sentido], v.[Fuente], v.[Orden], 1, NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
            FROM (VALUES
                (N'CARTERA', N'Cuotas de Cartera por cobrar', 1, 2, 10),
                (N'CAPTAC', N'Cuotas de aportes y ahorros por cobrar', 1, 3, 20),
                (N'OTROSING', N'Otros ingresos', 1, 1, 30),
                (N'ORDENES', N'Órdenes de pago', 2, 4, 40),
                (N'NOMINA', N'Dispersiones de nómina', 2, 5, 50),
                (N'OTROSEGR', N'Otros egresos', 2, 1, 60)
            ) AS v([Code], [Nombre], [Sentido], [Fuente], [Orden])
            WHERE NOT EXISTS (SELECT 1 FROM [dbo].[TES_CashFlowCategories] c WHERE c.[Code] = v.[Code] AND c.[IsDeleted] = 0);
            """);
    }
}
