using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class ComercioConCartera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConsultationEvidence",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreditLineCode",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CreditWithoutLending",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationPublicId",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservedLoanNumber",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CreditWithoutLending",
                schema: "dbo",
                table: "COR_PaymentMeans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "INV_LendingObligationResponses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentPaymentId = table.Column<int>(type: "integer", nullable: false),
                    MessagePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MessageVersion = table.Column<short>(type: "smallint", nullable: false),
                    OriginalMessagePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ObligationPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObligationNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ValidationStatus = table.Column<int>(type: "integer", nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Retryable = table.Column<bool>(type: "boolean", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_LendingObligationResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_LendingObligationResponses_INV_DocumentPayments_Documen~",
                        column: x => x.DocumentPaymentId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INV_LendingObligationResponses_Payment",
                schema: "dbo",
                table: "INV_LendingObligationResponses",
                columns: new[] { "DocumentPaymentId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "UK_INV_LendingObligationResponses_Message",
                schema: "dbo",
                table: "INV_LendingObligationResponses",
                column: "MessagePublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_LendingObligationResponses_PublicId",
                schema: "dbo",
                table: "INV_LendingObligationResponses",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INV_LendingObligationResponses",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "ConsultationEvidence",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "CreditLineCode",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "CreditWithoutLending",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "ReservationPublicId",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "ReservedLoanNumber",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "CreditWithoutLending",
                schema: "dbo",
                table: "COR_PaymentMeans");
        }
    }
}
