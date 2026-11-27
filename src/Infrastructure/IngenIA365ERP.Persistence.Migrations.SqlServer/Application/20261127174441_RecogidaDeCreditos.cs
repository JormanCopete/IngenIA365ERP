using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 028 (R2, R18; T007): la recogida proyectada en la solicitud (<c>LND_CreditApplicationPayoffs</c>) y la aplicada en el desembolso
    /// (<c>LND_DisbursementPayoffs</c>). Aditiva. Escrito a mano al final de <c>Up</c>: cada <c>LND_CreditApplications.PayoffRequest</c> no vacío
    /// (el JSON <c>[{ "loanPublicId", "mode", "amount" }]</c> de <c>ConsultasDeSolicitudes.Serializar</c>) se copia a filas de créditos sin
    /// proyección —valor cero, desglose <c>{}</c>, huella vacía, D = la fecha esperada de desembolso o la de creación, R = la creación—; un crédito
    /// repetido en la misma solicitud entra una vez y uno que ya no existe no entra. La columna se conserva y deja de escribirse. (nuevo)
    /// </summary>
    public partial class RecogidaDeCreditos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LND_CreditApplicationPayoffs",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditApplicationId = table.Column<int>(type: "int", nullable: false),
                    ProductKind = table.Column<int>(type: "int", nullable: false),
                    LoanId = table.Column<int>(type: "int", nullable: true),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    CommercialReceivableReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ProductNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    InstallmentTreatment = table.Column<int>(type: "int", nullable: true),
                    InterestMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProjectedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedAsOf = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProjectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProjectedBreakdown = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentInstallmentNumber = table.Column<int>(type: "int", nullable: true),
                    CurrentInstallmentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SentInstallmentState = table.Column<int>(type: "int", nullable: false),
                    PayrollDeductionFileId = table.Column<int>(type: "int", nullable: true),
                    ProjectionSha256 = table.Column<string>(type: "nvarchar(71)", maxLength: 71, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_CreditApplicationPayoffs", x => x.Id);
                    table.CheckConstraint("CK_LND_CreditApplicationPayoffs_Product", "([ProductKind] = 1 AND [LoanId] IS NOT NULL AND [AccountId] IS NULL AND [CommercialReceivableReference] IS NULL) OR ([ProductKind] = 2 AND [LoanId] IS NULL AND [AccountId] IS NOT NULL AND [CommercialReceivableReference] IS NULL) OR ([ProductKind] = 3 AND [LoanId] IS NULL AND [AccountId] IS NULL AND [CommercialReceivableReference] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LND_CreditApplicationPayoffs_Account",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplicationPayoffs_Application",
                        column: x => x.CreditApplicationId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplicationPayoffs_Loan",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplicationPayoffs_PayrollDeductionFile",
                        column: x => x.PayrollDeductionFileId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_DisbursementPayoffs",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisbursementOperationId = table.Column<long>(type: "bigint", nullable: false),
                    CreditApplicationPayoffId = table.Column<int>(type: "int", nullable: true),
                    ProductKind = table.Column<int>(type: "int", nullable: false),
                    LoanId = table.Column<int>(type: "int", nullable: true),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    CommercialReceivableReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ProductNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    InstallmentTreatment = table.Column<int>(type: "int", nullable: true),
                    InterestMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProjectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DifferenceCause = table.Column<int>(type: "int", nullable: false),
                    DifferenceDetail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Omitted = table.Column<bool>(type: "bit", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PaymentOperationId = table.Column<long>(type: "bigint", nullable: true),
                    AccountOperationId = table.Column<long>(type: "bigint", nullable: true),
                    ReceiptPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversalKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_DisbursementPayoffs", x => x.Id);
                    table.CheckConstraint("CK_LND_DisbursementPayoffs_Product", "([ProductKind] = 1 AND [LoanId] IS NOT NULL AND [AccountId] IS NULL AND [CommercialReceivableReference] IS NULL) OR ([ProductKind] = 2 AND [LoanId] IS NULL AND [AccountId] IS NOT NULL AND [CommercialReceivableReference] IS NULL) OR ([ProductKind] = 3 AND [LoanId] IS NULL AND [AccountId] IS NULL AND [CommercialReceivableReference] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LND_DisbursementPayoffs_Account",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementPayoffs_AccountOperation",
                        column: x => x.AccountOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccountOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementPayoffs_ApplicationPayoff",
                        column: x => x.CreditApplicationPayoffId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditApplicationPayoffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementPayoffs_DisbursementOperation",
                        column: x => x.DisbursementOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementPayoffs_Loan",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementPayoffs_PaymentOperation",
                        column: x => x.PaymentOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationPayoffs_AccountId",
                schema: "dbo",
                table: "LND_CreditApplicationPayoffs",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationPayoffs_LoanId",
                schema: "dbo",
                table: "LND_CreditApplicationPayoffs",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationPayoffs_PayrollDeductionFileId",
                schema: "dbo",
                table: "LND_CreditApplicationPayoffs",
                column: "PayrollDeductionFileId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditApplicationPayoffs_Application_Account",
                schema: "dbo",
                table: "LND_CreditApplicationPayoffs",
                columns: new[] { "CreditApplicationId", "AccountId" },
                unique: true,
                filter: "[AccountId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditApplicationPayoffs_Application_Loan",
                schema: "dbo",
                table: "LND_CreditApplicationPayoffs",
                columns: new[] { "CreditApplicationId", "LoanId" },
                unique: true,
                filter: "[LoanId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditApplicationPayoffs_Application_Receivable",
                schema: "dbo",
                table: "LND_CreditApplicationPayoffs",
                columns: new[] { "CreditApplicationId", "CommercialReceivableReference" },
                unique: true,
                filter: "[CommercialReceivableReference] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditApplicationPayoffs_PublicId",
                schema: "dbo",
                table: "LND_CreditApplicationPayoffs",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementPayoffs_AccountId",
                schema: "dbo",
                table: "LND_DisbursementPayoffs",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementPayoffs_AccountOperationId",
                schema: "dbo",
                table: "LND_DisbursementPayoffs",
                column: "AccountOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementPayoffs_CreditApplicationPayoffId",
                schema: "dbo",
                table: "LND_DisbursementPayoffs",
                column: "CreditApplicationPayoffId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementPayoffs_DisbursementOperationId",
                schema: "dbo",
                table: "LND_DisbursementPayoffs",
                column: "DisbursementOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementPayoffs_LoanId",
                schema: "dbo",
                table: "LND_DisbursementPayoffs",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementPayoffs_PaymentOperationId",
                schema: "dbo",
                table: "LND_DisbursementPayoffs",
                column: "PaymentOperationId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DisbursementPayoffs_IdempotencyKey",
                schema: "dbo",
                table: "LND_DisbursementPayoffs",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DisbursementPayoffs_PublicId",
                schema: "dbo",
                table: "LND_DisbursementPayoffs",
                column: "PublicId",
                unique: true);

            // Feature 028 (T007): copia de PayoffRequest a filas de créditos sin proyección (a mano; ver el comentario de la clase).
            migrationBuilder.Sql("""
                INSERT INTO [dbo].[LND_CreditApplicationPayoffs]
                    ([CreditApplicationId], [ProductKind], [LoanId], [ProductNumber], [Mode], [RequestedAmount], [ProjectedDate], [RecordedAsOf],
                     [ProjectedAmount], [ProjectedBreakdown], [SentInstallmentState], [ProjectionSha256], [Order], [PublicId], [IsDeleted],
                     [CreatedAt], [CreatedBy])
                SELECT x.[CreditApplicationId], 1, x.[LoanId], x.[Number], x.[Mode], CASE WHEN x.[Mode] = 2 THEN x.[Amount] END,
                       x.[ProjectedDate], x.[RecordedAsOf], 0, N'{}', 0, N'', x.[Order], NEWID(), 0, SYSUTCDATETIME(),
                       N'migracion:RecogidaDeCreditos'
                FROM (
                    SELECT s.[Id] AS [CreditApplicationId], l.[Id] AS [LoanId], l.[Number],
                           CASE WHEN LOWER(LTRIM(RTRIM(COALESCE(JSON_VALUE(o.[item], '$.mode'), JSON_VALUE(o.[item], '$.Mode'), N'')))) = N'partial'
                                THEN 2 ELSE 1 END AS [Mode],
                           TRY_CAST(COALESCE(JSON_VALUE(o.[item], '$.amount'), JSON_VALUE(o.[item], '$.Amount')) AS decimal(18,2)) AS [Amount],
                           COALESCE(s.[ExpectedDisbursementDate], CAST(s.[CreatedAt] AS date)) AS [ProjectedDate],
                           s.[CreatedAt] AS [RecordedAsOf],
                           CAST(e.[key] AS int) + 1 AS [Order],
                           ROW_NUMBER() OVER (PARTITION BY s.[Id], l.[Id] ORDER BY CAST(e.[key] AS int)) AS [Vez]
                    FROM [dbo].[LND_CreditApplications] s
                    CROSS APPLY OPENJSON(CASE WHEN ISJSON(s.[PayoffRequest]) = 1 AND LEFT(LTRIM(s.[PayoffRequest]), 1) = N'['
                                              THEN s.[PayoffRequest] ELSE N'[]' END) e
                    CROSS APPLY (SELECT CASE WHEN e.[type] = 5 THEN e.[value] END AS [item]) o
                    JOIN [dbo].[LND_Loans] l
                      ON l.[PublicId] = TRY_CONVERT(uniqueidentifier, COALESCE(JSON_VALUE(o.[item], '$.loanPublicId'), JSON_VALUE(o.[item], '$.LoanPublicId')))
                    WHERE s.[PayoffRequest] IS NOT NULL AND s.[IsDeleted] = 0
                ) x
                WHERE x.[Vez] = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LND_DisbursementPayoffs",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditApplicationPayoffs",
                schema: "dbo");
        }
    }
}
