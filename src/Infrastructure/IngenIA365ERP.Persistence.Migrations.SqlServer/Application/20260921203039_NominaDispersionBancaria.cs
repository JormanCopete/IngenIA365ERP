using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class NominaDispersionBancaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PAY_PayrollRuns_VacationMovementId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropIndex(
                name: "UK_PAY_PayrollRuns_Vacation_Employee_Cutoff_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.AddColumn<int>(
                name: "BankDisbursementFileId",
                schema: "dbo",
                table: "PAY_PayrollPayments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "COR_BankFileFormats",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankId = table.Column<int>(type: "int", nullable: true),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Delimiter = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    QuoteText = table.Column<bool>(type: "bit", nullable: false),
                    Encoding = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LineEnding = table.Column<int>(type: "int", nullable: false),
                    Uppercase = table.Column<bool>(type: "bit", nullable: false),
                    StripAccents = table.Column<bool>(type: "bit", nullable: false),
                    AmountFormat = table.Column<int>(type: "int", nullable: false),
                    FileNamePattern = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    HasHeader = table.Column<bool>(type: "bit", nullable: false),
                    HasTrailer = table.Column<bool>(type: "bit", nullable: false),
                    AgreementCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsSeeded = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_COR_BankFileFormats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_BankFileFormats_COR_Banks_BankId",
                        column: x => x.BankId,
                        principalSchema: "dbo",
                        principalTable: "COR_Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_BankFileFormatFields",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormatId = table.Column<int>(type: "int", nullable: false),
                    Record = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ConstantValue = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Length = table.Column<int>(type: "int", nullable: true),
                    Alignment = table.Column<int>(type: "int", nullable: false),
                    PadChar = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    DataType = table.Column<int>(type: "int", nullable: false),
                    ValueFormat = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ValueMapJson = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Required = table.Column<bool>(type: "bit", nullable: false),
                    Truncate = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_COR_BankFileFormatFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_BankFileFormatFields_COR_BankFileFormats_FormatId",
                        column: x => x.FormatId,
                        principalSchema: "dbo",
                        principalTable: "COR_BankFileFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PAY_BankDisbursementFiles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollRunId = table.Column<int>(type: "int", nullable: false),
                    BankId = table.Column<int>(type: "int", nullable: true),
                    FormatId = table.Column<int>(type: "int", nullable: false),
                    FormatCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SourceAccountId = table.Column<int>(type: "int", nullable: true),
                    SourceAccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExcludedCount = table.Column<int>(type: "int", nullable: false),
                    ExcludedJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SentNotes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_PAY_BankDisbursementFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_BankDisbursementFiles_COR_BankFileFormats_FormatId",
                        column: x => x.FormatId,
                        principalSchema: "dbo",
                        principalTable: "COR_BankFileFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_BankDisbursementFiles_COR_Banks_BankId",
                        column: x => x.BankId,
                        principalSchema: "dbo",
                        principalTable: "COR_Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_BankDisbursementFiles_PAY_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_BankDisbursementFileLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    PayrollRunEmployeeId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    DestinationBankId = table.Column<int>(type: "int", nullable: true),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RecordText = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    PayrollPaymentId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PAY_BankDisbursementFileLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_BankDisbursementFileLines_PAY_BankDisbursementFiles_FileId",
                        column: x => x.FileId,
                        principalSchema: "dbo",
                        principalTable: "PAY_BankDisbursementFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PAY_BankDisbursementFileLines_PAY_PayrollPayments_PayrollPaymentId",
                        column: x => x.PayrollPaymentId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_BankDisbursementFileLines_PAY_PayrollRunEmployees_PayrollRunEmployeeId",
                        column: x => x.PayrollRunEmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRunEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PayrollRuns_Vacation_Movement_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                columns: new[] { "VacationMovementId", "Version" },
                unique: true,
                filter: "[Kind] = 3");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollPayments_BankDisbursementFileId",
                schema: "dbo",
                table: "PAY_PayrollPayments",
                column: "BankDisbursementFileId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_BankFileFormatFields_Format_Record_Order",
                schema: "dbo",
                table: "COR_BankFileFormatFields",
                columns: new[] { "FormatId", "Record", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_BankFileFormatFields_PublicId",
                schema: "dbo",
                table: "COR_BankFileFormatFields",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_BankFileFormats_Bank_Scope_ValidFrom",
                schema: "dbo",
                table: "COR_BankFileFormats",
                columns: new[] { "BankId", "Scope", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "UK_COR_BankFileFormats_Code",
                schema: "dbo",
                table: "COR_BankFileFormats",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_COR_BankFileFormats_PublicId",
                schema: "dbo",
                table: "COR_BankFileFormats",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_BankDisbursementFileLines_PayrollPaymentId",
                schema: "dbo",
                table: "PAY_BankDisbursementFileLines",
                column: "PayrollPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_BankDisbursementFileLines_RunEmployee",
                schema: "dbo",
                table: "PAY_BankDisbursementFileLines",
                column: "PayrollRunEmployeeId");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_BankDisbursementFileLines_File_Line",
                schema: "dbo",
                table: "PAY_BankDisbursementFileLines",
                columns: new[] { "FileId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_BankDisbursementFileLines_PublicId",
                schema: "dbo",
                table: "PAY_BankDisbursementFileLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_BankDisbursementFiles_BankId",
                schema: "dbo",
                table: "PAY_BankDisbursementFiles",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_BankDisbursementFiles_FormatId",
                schema: "dbo",
                table: "PAY_BankDisbursementFiles",
                column: "FormatId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_BankDisbursementFiles_PaymentDate_Sequence",
                schema: "dbo",
                table: "PAY_BankDisbursementFiles",
                columns: new[] { "PaymentDate", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_BankDisbursementFiles_Run_Status",
                schema: "dbo",
                table: "PAY_BankDisbursementFiles",
                columns: new[] { "PayrollRunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UK_PAY_BankDisbursementFiles_PublicId",
                schema: "dbo",
                table: "PAY_BankDisbursementFiles",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_PayrollPayments_PAY_BankDisbursementFiles_BankDisbursementFileId",
                schema: "dbo",
                table: "PAY_PayrollPayments",
                column: "BankDisbursementFileId",
                principalSchema: "dbo",
                principalTable: "PAY_BankDisbursementFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PayrollPayments_PAY_BankDisbursementFiles_BankDisbursementFileId",
                schema: "dbo",
                table: "PAY_PayrollPayments");

            migrationBuilder.DropTable(
                name: "COR_BankFileFormatFields",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_BankDisbursementFileLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_BankDisbursementFiles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_BankFileFormats",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "UK_PAY_PayrollRuns_Vacation_Movement_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropIndex(
                name: "IX_PAY_PayrollPayments_BankDisbursementFileId",
                schema: "dbo",
                table: "PAY_PayrollPayments");

            migrationBuilder.DropColumn(
                name: "BankDisbursementFileId",
                schema: "dbo",
                table: "PAY_PayrollPayments");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRuns_VacationMovementId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                column: "VacationMovementId");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PayrollRuns_Vacation_Employee_Cutoff_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                columns: new[] { "EmployeeId", "CutoffDate", "Version" },
                unique: true,
                filter: "[Kind] = 3");
        }
    }
}
