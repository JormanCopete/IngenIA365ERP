using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class NominaRecaudaPorElContrato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CarteraReversalPublicId",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObligationNumber",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ObligationPublicId",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LendingActivationAckAt",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LendingActivationAckBy",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObligationNumber",
                schema: "dbo",
                table: "PAY_Novelties",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ObligationPublicId",
                schema: "dbo",
                table: "PAY_Novelties",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PAY_PayrollLoanDeductions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollRunId = table.Column<int>(type: "int", nullable: false),
                    PayrollRunEmployeeId = table.Column<int>(type: "int", nullable: false),
                    PayrollRunLineId = table.Column<int>(type: "int", nullable: true),
                    ObligationPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObligationNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NotDeductedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NotDeductedReason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CarteraReceiptPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CarteraReversalPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResultCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ResultMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAY_PayrollLoanDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_PayrollLoanDeductions_PAY_PayrollRunEmployees_PayrollRunEmployeeId",
                        column: x => x.PayrollRunEmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRunEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_PayrollLoanDeductions_PAY_PayrollRunLines_PayrollRunLineId",
                        column: x => x.PayrollRunLineId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRunLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_PayrollLoanDeductions_PAY_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Obligation",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                columns: new[] { "TerminationId", "ObligationPublicId" },
                unique: true,
                filter: "[ObligationPublicId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Novelties_ObligationPublicId",
                schema: "dbo",
                table: "PAY_Novelties",
                column: "ObligationPublicId",
                filter: "[ObligationPublicId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollLoanDeductions_PayrollRunEmployeeId",
                schema: "dbo",
                table: "PAY_PayrollLoanDeductions",
                column: "PayrollRunEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollLoanDeductions_PayrollRunLineId",
                schema: "dbo",
                table: "PAY_PayrollLoanDeductions",
                column: "PayrollRunLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollLoanDeductions_PublicId",
                schema: "dbo",
                table: "PAY_PayrollLoanDeductions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PayrollLoanDeductions_Run_Employee_Obligation",
                schema: "dbo",
                table: "PAY_PayrollLoanDeductions",
                columns: new[] { "PayrollRunId", "PayrollRunEmployeeId", "ObligationPublicId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            // Feature 013 (T0178, C73, data-model parte 3 §7.2–§7.3): copia IDEMPOTENTE —transformación de datos existentes, no
            // semilla— de la obligación heredada a las columnas nuevas: ObligationPublicId = LND_LoanPortfolios.PublicId y
            // ObligationNumber = PortfolioNumber, donde LoanPortfolioId no es nulo y el destino sigue nulo. En PAY_Novelties no se
            // espera ninguna fila (nadie escribe hoy su LoanPortfolioId); se conserva por simetría. Si la copia no encuentra el
            // crédito heredado, el runbook lo declara como pérdida (Principio XII). RetiroDeLaCarteraHeredada suelta después
            // LoanPortfolioId, su FK y sus índices (CH37).
            migrationBuilder.Sql(@"
UPDATE d
   SET d.[ObligationPublicId] = l.[PublicId],
       d.[ObligationNumber] = LEFT(CAST(l.[PortfolioNumber] AS nvarchar(30)), 30)
  FROM [dbo].[PAY_SettlementDeductions] AS d
  JOIN [dbo].[LND_LoanPortfolios] AS l ON l.[Id] = d.[LoanPortfolioId]
 WHERE d.[LoanPortfolioId] IS NOT NULL
   AND d.[ObligationPublicId] IS NULL;

UPDATE n
   SET n.[ObligationPublicId] = l.[PublicId],
       n.[ObligationNumber] = LEFT(CAST(l.[PortfolioNumber] AS nvarchar(30)), 30)
  FROM [dbo].[PAY_Novelties] AS n
  JOIN [dbo].[LND_LoanPortfolios] AS l ON l.[Id] = n.[LoanPortfolioId]
 WHERE n.[LoanPortfolioId] IS NOT NULL
   AND n.[ObligationPublicId] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PAY_PayrollLoanDeductions",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Obligation",
                schema: "dbo",
                table: "PAY_SettlementDeductions");

            migrationBuilder.DropIndex(
                name: "IX_PAY_Novelties_ObligationPublicId",
                schema: "dbo",
                table: "PAY_Novelties");

            migrationBuilder.DropColumn(
                name: "CarteraReversalPublicId",
                schema: "dbo",
                table: "PAY_SettlementDeductions");

            migrationBuilder.DropColumn(
                name: "ObligationNumber",
                schema: "dbo",
                table: "PAY_SettlementDeductions");

            migrationBuilder.DropColumn(
                name: "ObligationPublicId",
                schema: "dbo",
                table: "PAY_SettlementDeductions");

            migrationBuilder.DropColumn(
                name: "LendingActivationAckAt",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "LendingActivationAckBy",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "ObligationNumber",
                schema: "dbo",
                table: "PAY_Novelties");

            migrationBuilder.DropColumn(
                name: "ObligationPublicId",
                schema: "dbo",
                table: "PAY_Novelties");
        }
    }
}
