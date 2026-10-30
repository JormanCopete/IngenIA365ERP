using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Admin
{
    /// <inheritdoc />
    public partial class IdentidadConDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                schema: "dbo",
                table: "ADM_TenantMemberships",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewRequestedAt",
                schema: "dbo",
                table: "ADM_TenantMemberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CentralUserId",
                schema: "dbo",
                table: "ADM_Invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentAttempts",
                schema: "dbo",
                table: "ADM_Invitations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                schema: "dbo",
                table: "ADM_Invitations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                schema: "dbo",
                table: "ADM_Invitations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "dbo",
                table: "ADM_Invitations",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                schema: "dbo",
                table: "ADM_Invitations",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxDocumentAttempts",
                schema: "dbo",
                table: "ADM_Invitations",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "MobilePhone",
                schema: "dbo",
                table: "ADM_Invitations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherNames",
                schema: "dbo",
                table: "ADM_Invitations",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PositionPublicId",
                schema: "dbo",
                table: "ADM_Invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RolePublicId",
                schema: "dbo",
                table: "ADM_Invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondLastName",
                schema: "dbo",
                table: "ADM_Invitations",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypedEmail",
                schema: "dbo",
                table: "ADM_Invitations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                schema: "dbo",
                table: "ADM_CentralUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                schema: "dbo",
                table: "ADM_CentralUsers",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentUsername",
                schema: "dbo",
                table: "ADM_CentralUsers",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifiedAt",
                schema: "dbo",
                table: "ADM_CentralUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "dbo",
                table: "ADM_CentralUsers",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                schema: "dbo",
                table: "ADM_CentralUsers",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherNames",
                schema: "dbo",
                table: "ADM_CentralUsers",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondLastName",
                schema: "dbo",
                table: "ADM_CentralUsers",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identifier",
                schema: "dbo",
                table: "ADM_CentralUserLoginAttempts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdentifierKind",
                schema: "dbo",
                table: "ADM_CentralUserLoginAttempts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ADM_DocumentHistory",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CentralUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldType = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    OldNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OldUsername = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    NewType = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NewNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NewUsername = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupportReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupportTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorCentralUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_DocumentHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_EmailChangeRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CentralUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NewEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NewNormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CodeHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedFromIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    ByMaster = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_EmailChangeRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_IdentityCases",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CentralUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OtherCentralUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    OpenedByCentralUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedByCentralUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_IdentityCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_TenantIdentityPolicies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedDocumentTypes = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "CC,CE,PPT,PEP,TI,PA"),
                    DocumentRequiredFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    MaxActivationAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_TenantIdentityPolicies", x => x.Id);
                });

            // Feature 017 (research R4). Antes de los índices únicos:
            // (a) correos con espacios alrededor: se recortan y se recalcula su forma normalizada. Sólo
            //     esas filas: a las demás no se les toca nada, así su búsqueda sigue idéntica.
            migrationBuilder.Sql("""
                UPDATE dbo."ADM_CentralUsers"
                   SET "Email" = btrim("Email"), "NormalizedEmail" = upper(btrim("Email")),
                       "UserName" = btrim("UserName"), "NormalizedUserName" = upper(btrim("UserName"))
                 WHERE "Email" <> btrim("Email") OR "UserName" <> btrim("UserName");
                """);

            // (b) Guarda: el índice único de correo no se crea si hay repetidos (diagnóstico A1). El
            //     mensaje da la cantidad, nunca los correos: va a los registros del despliegue.
            migrationBuilder.Sql("""
                DO $$
                DECLARE n integer;
                BEGIN
                    SELECT count(*) INTO n FROM (
                        SELECT "NormalizedEmail" FROM dbo."ADM_CentralUsers"
                         WHERE NOT "IsDeleted" AND "NormalizedEmail" IS NOT NULL
                         GROUP BY "NormalizedEmail" HAVING count(*) > 1) d;
                    IF n > 0 THEN
                        RAISE EXCEPTION 'IdentidadConDocumento: % correo(s) repetidos en ADM_CentralUsers. Revise el diagnostico A1 de specs/017-login-documento-invitacion/diagnostico-de-datos.md y resuelvalos con el dueno antes de continuar.', n;
                    END IF;
                END $$;
                """);

            // (c) Regla de oro (D-03): toda cuenta viva, activa y con clave queda con el correo verificado,
            //     para que «sólo correo verificado» no deje a nadie por fuera. Sin pasar por UserManager:
            //     no se toca SecurityStamp ni ConcurrencyStamp, y las sesiones vivas siguen valiendo.
            migrationBuilder.Sql("""
                UPDATE dbo."ADM_CentralUsers"
                   SET "EmailConfirmed" = true, "EmailVerifiedAt" = now()
                 WHERE NOT "IsDeleted" AND "Status" = 0 AND "PasswordHash" IS NOT NULL AND NOT "EmailConfirmed";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Invitations_CentralUser_Tenant_Status",
                schema: "dbo",
                table: "ADM_Invitations",
                columns: new[] { "CentralUserId", "TenantId", "Status" },
                filter: "\"IsDeleted\" = FALSE AND \"CentralUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ADM_CentralUsers_Document",
                schema: "dbo",
                table: "ADM_CentralUsers",
                columns: new[] { "DocumentType", "DocumentNumber" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"DocumentNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ADM_CentralUsers_DocumentUsername",
                schema: "dbo",
                table: "ADM_CentralUsers",
                column: "DocumentUsername",
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"DocumentUsername\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ADM_CentralUsers_NormalizedEmail",
                schema: "dbo",
                table: "ADM_CentralUsers",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"NormalizedEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_DocumentHistory_CentralUser_OccurredAt",
                schema: "dbo",
                table: "ADM_DocumentHistory",
                columns: new[] { "CentralUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ADM_DocumentHistory_PublicId",
                schema: "dbo",
                table: "ADM_DocumentHistory",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_EmailChangeRequests_CentralUser_Status",
                schema: "dbo",
                table: "ADM_EmailChangeRequests",
                columns: new[] { "CentralUserId", "Status" },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_EmailChangeRequests_PublicId",
                schema: "dbo",
                table: "ADM_EmailChangeRequests",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_IdentityCases_CentralUser",
                schema: "dbo",
                table: "ADM_IdentityCases",
                column: "CentralUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_IdentityCases_PublicId",
                schema: "dbo",
                table: "ADM_IdentityCases",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_IdentityCases_Status_Kind",
                schema: "dbo",
                table: "ADM_IdentityCases",
                columns: new[] { "Status", "Kind" },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_TenantIdentityPolicies_PublicId",
                schema: "dbo",
                table: "ADM_TenantIdentityPolicies",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ADM_TenantIdentityPolicies_TenantId",
                schema: "dbo",
                table: "ADM_TenantIdentityPolicies",
                column: "TenantId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ADM_DocumentHistory",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_EmailChangeRequests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_IdentityCases",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_TenantIdentityPolicies",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_ADM_Invitations_CentralUser_Tenant_Status",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropIndex(
                name: "UX_ADM_CentralUsers_Document",
                schema: "dbo",
                table: "ADM_CentralUsers");

            migrationBuilder.DropIndex(
                name: "UX_ADM_CentralUsers_DocumentUsername",
                schema: "dbo",
                table: "ADM_CentralUsers");

            migrationBuilder.DropIndex(
                name: "UX_ADM_CentralUsers_NormalizedEmail",
                schema: "dbo",
                table: "ADM_CentralUsers");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                schema: "dbo",
                table: "ADM_TenantMemberships");

            migrationBuilder.DropColumn(
                name: "ReviewRequestedAt",
                schema: "dbo",
                table: "ADM_TenantMemberships");

            migrationBuilder.DropColumn(
                name: "CentralUserId",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropColumn(
                name: "DocumentAttempts",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropColumn(
                name: "DocumentNumber",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropColumn(
                name: "FirstName",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropColumn(
                name: "LastName",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropColumn(
                name: "MaxDocumentAttempts",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropColumn(
                name: "MobilePhone",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropColumn(
                name: "OtherNames",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropColumn(
                name: "PositionPublicId",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropColumn(
                name: "RolePublicId",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropColumn(
                name: "SecondLastName",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropColumn(
                name: "TypedEmail",
                schema: "dbo",
                table: "ADM_Invitations");

            migrationBuilder.DropColumn(
                name: "DocumentNumber",
                schema: "dbo",
                table: "ADM_CentralUsers");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                schema: "dbo",
                table: "ADM_CentralUsers");

            migrationBuilder.DropColumn(
                name: "DocumentUsername",
                schema: "dbo",
                table: "ADM_CentralUsers");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAt",
                schema: "dbo",
                table: "ADM_CentralUsers");

            migrationBuilder.DropColumn(
                name: "FirstName",
                schema: "dbo",
                table: "ADM_CentralUsers");

            migrationBuilder.DropColumn(
                name: "LastName",
                schema: "dbo",
                table: "ADM_CentralUsers");

            migrationBuilder.DropColumn(
                name: "OtherNames",
                schema: "dbo",
                table: "ADM_CentralUsers");

            migrationBuilder.DropColumn(
                name: "SecondLastName",
                schema: "dbo",
                table: "ADM_CentralUsers");

            migrationBuilder.DropColumn(
                name: "Identifier",
                schema: "dbo",
                table: "ADM_CentralUserLoginAttempts");

            migrationBuilder.DropColumn(
                name: "IdentifierKind",
                schema: "dbo",
                table: "ADM_CentralUserLoginAttempts");
        }
    }
}
