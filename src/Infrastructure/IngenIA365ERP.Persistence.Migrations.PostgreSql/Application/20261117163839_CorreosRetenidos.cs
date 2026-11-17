using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CorreosRetenidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "COR_HeldEmails",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OriginalTo = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    RedirectedTo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: true),
                    BodyText = table.Column<string>(type: "text", nullable: true),
                    FromOverride = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    AttachmentCount = table.Column<int>(type: "integer", nullable: false),
                    ResentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResentBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
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
                    table.PrimaryKey("PK_COR_HeldEmails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_HeldEmailAttachments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HeldEmailId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
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
                    table.PrimaryKey("PK_COR_HeldEmailAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_HeldEmailAttachments_COR_HeldEmails_HeldEmailId",
                        column: x => x.HeldEmailId,
                        principalSchema: "dbo",
                        principalTable: "COR_HeldEmails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_COR_HeldEmailAttachments_HeldEmailId",
                schema: "dbo",
                table: "COR_HeldEmailAttachments",
                column: "HeldEmailId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_HeldEmailAttachments_PublicId",
                schema: "dbo",
                table: "COR_HeldEmailAttachments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_HeldEmails_Status_CreatedAt",
                schema: "dbo",
                table: "COR_HeldEmails",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UK_COR_HeldEmails_PublicId",
                schema: "dbo",
                table: "COR_HeldEmails",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COR_HeldEmailAttachments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_HeldEmails",
                schema: "dbo");
        }
    }
}
