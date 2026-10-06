using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Admin
{
    /// <inheritdoc />
    public partial class ValoresNacionales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ADM_NationalValueCounters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NextValue = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_NationalValueCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_NationalValues",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ReferenceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Value = table.Column<decimal>(type: "decimal(20,10)", precision: 20, scale: 10, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegisteredBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RegisteredByCentralUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupersedesPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_NationalValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_RegulatoryDocuments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentSha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    SchemaVersion = table.Column<short>(type: "smallint", nullable: false),
                    Verified = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegisteredBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RegisteredByCentralUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupersedesPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_RegulatoryDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_ADM_NationalValueCounters_PublicId",
                schema: "dbo",
                table: "ADM_NationalValueCounters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_NationalValues_Code_Key_ValidFrom",
                schema: "dbo",
                table: "ADM_NationalValues",
                columns: new[] { "Code", "Key", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "UK_ADM_NationalValues_PublicId",
                schema: "dbo",
                table: "ADM_NationalValues",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ADM_NationalValues_Sequence",
                schema: "dbo",
                table: "ADM_NationalValues",
                column: "Sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ADM_NationalValues_Supersedes",
                schema: "dbo",
                table: "ADM_NationalValues",
                column: "SupersedesPublicId",
                unique: true,
                filter: "[SupersedesPublicId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_RegulatoryDocuments_Kind_ValidFrom",
                schema: "dbo",
                table: "ADM_RegulatoryDocuments",
                columns: new[] { "Kind", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "UK_ADM_RegulatoryDocuments_PublicId",
                schema: "dbo",
                table: "ADM_RegulatoryDocuments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ADM_RegulatoryDocuments_Sequence",
                schema: "dbo",
                table: "ADM_RegulatoryDocuments",
                column: "Sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ADM_RegulatoryDocuments_Supersedes",
                schema: "dbo",
                table: "ADM_RegulatoryDocuments",
                column: "SupersedesPublicId",
                unique: true,
                filter: "[SupersedesPublicId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ADM_NationalValueCounters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_NationalValues",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_RegulatoryDocuments",
                schema: "dbo");
        }
    }
}
