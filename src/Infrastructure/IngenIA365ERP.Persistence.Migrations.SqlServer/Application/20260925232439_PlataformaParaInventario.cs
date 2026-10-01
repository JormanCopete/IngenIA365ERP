using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 012 (T186, decisiones-transversales §2.15): par aditivo <c>PlataformaParaInventario</c>. Trae las tablas de
    /// plataforma de las fases 2 y 3 (parámetros con vigencia, claves de operación, arrendamientos, aprobaciones y montos por
    /// permiso, alertas, bandeja de salida de mensajes, auditoría encadenada, catálogo tributario de Core), las columnas del
    /// perfil tributario de <c>COR_People</c>, DIVIPOLA de <c>COR_Cities</c>/<c>COR_Branches</c> y
    /// <c>COR_Notifications.AlertPublicId</c>; siembra las cinco filas de <c>COR_BackgroundLeases</c>. Las tablas <c>INV_</c> del
    /// documento genérico NO van aquí: están excluidas del modelo de migraciones (<c>NucleoComercialSinMigracion</c>) hasta
    /// <c>InventarioComercialNucleo</c> (T440). Sin marcador destructivo: <c>Down()</c> quita sólo lo agregado.
    /// </summary>
    public partial class PlataformaParaInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsIncomeTaxFiler",
                schema: "dbo",
                table: "COR_People",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsObligatedToInvoice",
                schema: "dbo",
                table: "COR_People",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSelfWithholder",
                schema: "dbo",
                table: "COR_People",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSimpleTaxRegime",
                schema: "dbo",
                table: "COR_People",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVatResponsible",
                schema: "dbo",
                table: "COR_People",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVatWithholdingAgent",
                schema: "dbo",
                table: "COR_People",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "AlertPublicId",
                schema: "dbo",
                table: "COR_Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DaneCode",
                schema: "dbo",
                table: "COR_Cities",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MunicipalityDaneCode",
                schema: "dbo",
                table: "COR_Branches",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "COR_AlertTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecipientPermissions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Channels = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    ThresholdsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
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
                    table.PrimaryKey("PK_COR_AlertTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_ApprovalPolicies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Module = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DocumentTypePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
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
                    table.PrimaryKey("PK_COR_ApprovalPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_AuditAnchors",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Stream = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    Hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    AnchorDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Hmac = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    KeyVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AnchoredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_COR_AuditAnchors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_AuditChainHeads",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Stream = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    LastSeq = table.Column<long>(type: "bigint", nullable: false),
                    LastHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_COR_AuditChainHeads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_AuditOutbox",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stream = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Forwarded = table.Column<bool>(type: "bit", nullable: false),
                    ForwardedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ForwardAttempts = table.Column<int>(type: "int", nullable: false),
                    LastForwardError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Seq = table.Column<long>(type: "bigint", nullable: true),
                    PrevHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    Hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_COR_AuditOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_BackgroundLeases",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_COR_BackgroundLeases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_IntegrationMessages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Version = table.Column<short>(type: "smallint", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    OriginModule = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OriginKind = table.Column<int>(type: "int", nullable: false),
                    OriginPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginDocumentClass = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    OriginDocumentTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    OriginNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    OriginEventKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FiscalUniqueCode = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: true),
                    RelatedPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedDocumentClass = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RelatedNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ChainRootPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BranchPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostCenterPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PersonPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadSha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    PrevalidationOutcome = table.Column<int>(type: "int", nullable: true),
                    OriginUserCentralId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginUserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_COR_IntegrationMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_OperationKeys",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RequestSha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    CentralUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_COR_OperationKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_OperationKeys_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_ParameterVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Module = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ScopeKind = table.Column<int>(type: "int", nullable: false),
                    ScopeId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LegalSource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_COR_ParameterVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_TaxDefinitions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    CalculationForm = table.Column<int>(type: "int", nullable: false),
                    TaxedOnDefinitionId = table.Column<int>(type: "int", nullable: true),
                    IsWithholding = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DianTaxCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
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
                    table.PrimaryKey("PK_COR_TaxDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_TaxDefinitions_COR_TaxDefinitions_TaxedOnDefinitionId",
                        column: x => x.TaxedOnDefinitionId,
                        principalSchema: "dbo",
                        principalTable: "COR_TaxDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_WithholdingConcepts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
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
                    table.PrimaryKey("PK_COR_WithholdingConcepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SEC_PermissionAmountLimits",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
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
                    table.PrimaryKey("PK_SEC_PermissionAmountLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SEC_PermissionAmountLimits_SEC_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_Alerts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AlertTypeId = table.Column<int>(type: "int", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    EntityPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScopeWarehousePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScopePointOfSalePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DedupKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RaisedByKind = table.Column<int>(type: "int", nullable: false),
                    RaisedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OccurrenceCount = table.Column<int>(type: "int", nullable: false),
                    LastOccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttendedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttendedByKind = table.Column<int>(type: "int", nullable: true),
                    AttendedByUserId = table.Column<int>(type: "int", nullable: true),
                    AttendedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AttendNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecipientCount = table.Column<int>(type: "int", nullable: false),
                    WithoutRecipient = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_COR_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_Alerts_COR_AlertTypes_AlertTypeId",
                        column: x => x.AlertTypeId,
                        principalSchema: "dbo",
                        principalTable: "COR_AlertTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_Alerts_SEC_Users_AttendedByUserId",
                        column: x => x.AttendedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_ApprovalPolicyLevels",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PolicyId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<byte>(type: "tinyint", nullable: false),
                    Threshold = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PermissionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_COR_ApprovalPolicyLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_ApprovalPolicyLevels_COR_ApprovalPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalSchema: "dbo",
                        principalTable: "COR_ApprovalPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_ApprovalRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Module = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SourcePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceLabel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ScopeWarehousePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScopePointOfSalePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PolicyId = table.Column<int>(type: "int", nullable: true),
                    RequiredLevelsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    ExcludedUserIdsJson = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    ContentSha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_COR_ApprovalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_ApprovalRequests_COR_ApprovalPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalSchema: "dbo",
                        principalTable: "COR_ApprovalPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_ApprovalRequests_SEC_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_ApprovalRequests_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_IntegrationMessageDeliveries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScheduleKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    BatchScopeKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    BatchId = table.Column<int>(type: "int", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastErrorDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResultReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResultVoucherTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ResultVoucherNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
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
                    table.PrimaryKey("PK_COR_IntegrationMessageDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_IntegrationMessageDeliveries_COR_IntegrationMessages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "dbo",
                        principalTable: "COR_IntegrationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_IntegrationMessageDependencies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    DependsOnMessageId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_COR_IntegrationMessageDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_IntegrationMessageDependencies_COR_IntegrationMessages_DependsOnMessageId",
                        column: x => x.DependsOnMessageId,
                        principalSchema: "dbo",
                        principalTable: "COR_IntegrationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_IntegrationMessageDependencies_COR_IntegrationMessages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "dbo",
                        principalTable: "COR_IntegrationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_TaxRates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaxDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    AmountPerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    WithholdingConceptId = table.Column<int>(type: "int", nullable: true),
                    MunicipalityDaneCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    ActivityCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    MinimumBaseUvt = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MinimumBasePesos = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SubjectPersonType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    SubjectIsIncomeTaxFiler = table.Column<bool>(type: "bit", nullable: true),
                    SubjectIsVatResponsible = table.Column<bool>(type: "bit", nullable: true),
                    SubjectIsLargeContributor = table.Column<bool>(type: "bit", nullable: true),
                    SubjectIsSelfWithholder = table.Column<bool>(type: "bit", nullable: true),
                    SubjectIsSimpleTaxRegime = table.Column<bool>(type: "bit", nullable: true),
                    AgentIsLargeContributor = table.Column<bool>(type: "bit", nullable: true),
                    AgentIsVatWithholdingAgent = table.Column<bool>(type: "bit", nullable: true),
                    AppliesTo = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    LegalSource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewPending = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
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
                    table.PrimaryKey("PK_COR_TaxRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_TaxRates_COR_TaxDefinitions_TaxDefinitionId",
                        column: x => x.TaxDefinitionId,
                        principalSchema: "dbo",
                        principalTable: "COR_TaxDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_TaxRates_COR_WithholdingConcepts_WithholdingConceptId",
                        column: x => x.WithholdingConceptId,
                        principalSchema: "dbo",
                        principalTable: "COR_WithholdingConcepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_ApprovalDecisions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<byte>(type: "tinyint", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: false),
                    DecidedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    CredentialPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PermissionCodeUsed = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentSha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_COR_ApprovalDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_ApprovalDecisions_COR_ApprovalRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dbo",
                        principalTable: "COR_ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_ApprovalDecisions_SEC_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UK_COR_Cities_DaneCode",
                schema: "dbo",
                table: "COR_Cities",
                column: "DaneCode",
                unique: true,
                filter: "[DaneCode] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Alerts_AlertTypeId",
                schema: "dbo",
                table: "COR_Alerts",
                column: "AlertTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Alerts_AttendedByUserId",
                schema: "dbo",
                table: "COR_Alerts",
                column: "AttendedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Alerts_Status_TypeCode_RaisedAt",
                schema: "dbo",
                table: "COR_Alerts",
                columns: new[] { "Status", "TypeCode", "RaisedAt" });

            migrationBuilder.CreateIndex(
                name: "UK_COR_Alerts_DedupKey_Pending",
                schema: "dbo",
                table: "COR_Alerts",
                column: "DedupKey",
                unique: true,
                filter: "[Status] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_COR_Alerts_PublicId",
                schema: "dbo",
                table: "COR_Alerts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_AlertTypes_PublicId",
                schema: "dbo",
                table: "COR_AlertTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_AlertTypes_TypeCode_ValidFrom",
                schema: "dbo",
                table: "COR_AlertTypes",
                columns: new[] { "TypeCode", "ValidFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ApprovalDecisions_DecidedByUserId",
                schema: "dbo",
                table: "COR_ApprovalDecisions",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_ApprovalDecisions_PublicId",
                schema: "dbo",
                table: "COR_ApprovalDecisions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ApprovalDecisions_RequestId_Level_Approved",
                schema: "dbo",
                table: "COR_ApprovalDecisions",
                columns: new[] { "RequestId", "Level" },
                unique: true,
                filter: "[Decision] = 1");

            migrationBuilder.CreateIndex(
                name: "UK_COR_ApprovalPolicies_PolicyKey_ValidFrom",
                schema: "dbo",
                table: "COR_ApprovalPolicies",
                columns: new[] { "PolicyKey", "ValidFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_COR_ApprovalPolicies_PublicId",
                schema: "dbo",
                table: "COR_ApprovalPolicies",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ApprovalPolicyLevels_PolicyId_Order",
                schema: "dbo",
                table: "COR_ApprovalPolicyLevels",
                columns: new[] { "PolicyId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ApprovalPolicyLevels_PublicId",
                schema: "dbo",
                table: "COR_ApprovalPolicyLevels",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_ApprovalRequests_CreatedByUserId",
                schema: "dbo",
                table: "COR_ApprovalRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ApprovalRequests_PolicyId",
                schema: "dbo",
                table: "COR_ApprovalRequests",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ApprovalRequests_RequestedByUserId",
                schema: "dbo",
                table: "COR_ApprovalRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ApprovalRequests_Status_Module_CurrentLevel",
                schema: "dbo",
                table: "COR_ApprovalRequests",
                columns: new[] { "Status", "Module", "CurrentLevel" });

            migrationBuilder.CreateIndex(
                name: "UK_COR_ApprovalRequests_PublicId",
                schema: "dbo",
                table: "COR_ApprovalRequests",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ApprovalRequests_Source_Subject_Pending",
                schema: "dbo",
                table: "COR_ApprovalRequests",
                columns: new[] { "SourceType", "SourcePublicId", "Subject" },
                unique: true,
                filter: "[Status] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_COR_AuditAnchors_PublicId",
                schema: "dbo",
                table: "COR_AuditAnchors",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_AuditAnchors_Stream_AnchorDate",
                schema: "dbo",
                table: "COR_AuditAnchors",
                columns: new[] { "Stream", "AnchorDate" },
                unique: true,
                filter: "[Kind] = 3");

            migrationBuilder.CreateIndex(
                name: "UK_COR_AuditAnchors_Stream_Kind_Seq",
                schema: "dbo",
                table: "COR_AuditAnchors",
                columns: new[] { "Stream", "Kind", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_AuditChainHeads_PublicId",
                schema: "dbo",
                table: "COR_AuditChainHeads",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_AuditChainHeads_Stream",
                schema: "dbo",
                table: "COR_AuditChainHeads",
                column: "Stream",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_AuditOutbox_Pending",
                schema: "dbo",
                table: "COR_AuditOutbox",
                column: "Id",
                filter: "[Forwarded] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_COR_AuditOutbox_EventId",
                schema: "dbo",
                table: "COR_AuditOutbox",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_AuditOutbox_PublicId",
                schema: "dbo",
                table: "COR_AuditOutbox",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_AuditOutbox_Stream_Seq",
                schema: "dbo",
                table: "COR_AuditOutbox",
                columns: new[] { "Stream", "Seq" },
                unique: true,
                filter: "[Seq] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_COR_BackgroundLeases_Name",
                schema: "dbo",
                table: "COR_BackgroundLeases",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_BackgroundLeases_PublicId",
                schema: "dbo",
                table: "COR_BackgroundLeases",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationMessageDeliveries_Batch",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationMessageDeliveries_Eligible",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries",
                columns: new[] { "Destination", "NextAttemptAt", "MessageId" },
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationMessageDeliveries_InBatch",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries",
                columns: new[] { "ScheduleKey", "MessageId" },
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationMessageDeliveries_Message_Destination",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries",
                columns: new[] { "MessageId", "Destination" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationMessageDeliveries_PublicId",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationMessageDependencies_DependsOn",
                schema: "dbo",
                table: "COR_IntegrationMessageDependencies",
                column: "DependsOnMessageId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationMessageDependencies_Message_DependsOn",
                schema: "dbo",
                table: "COR_IntegrationMessageDependencies",
                columns: new[] { "MessageId", "DependsOnMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationMessageDependencies_PublicId",
                schema: "dbo",
                table: "COR_IntegrationMessageDependencies",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationMessages_ChainRoot",
                schema: "dbo",
                table: "COR_IntegrationMessages",
                columns: new[] { "ChainRootPublicId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationMessages_OperationDate",
                schema: "dbo",
                table: "COR_IntegrationMessages",
                column: "OperationDate");

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationMessages_Person",
                schema: "dbo",
                table: "COR_IntegrationMessages",
                column: "PersonPublicId",
                filter: "[PersonPublicId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationMessages_Type_OperationDate",
                schema: "dbo",
                table: "COR_IntegrationMessages",
                columns: new[] { "Type", "OperationDate" });

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationMessages_Origin_Type_EventKey",
                schema: "dbo",
                table: "COR_IntegrationMessages",
                columns: new[] { "OriginPublicId", "Type", "OriginEventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationMessages_PublicId",
                schema: "dbo",
                table: "COR_IntegrationMessages",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_OperationKeys_UserId",
                schema: "dbo",
                table: "COR_OperationKeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_OperationKeys_Key",
                schema: "dbo",
                table: "COR_OperationKeys",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_OperationKeys_PublicId",
                schema: "dbo",
                table: "COR_OperationKeys",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ParameterVersions_Module_Key_Scope_ValidFrom",
                schema: "dbo",
                table: "COR_ParameterVersions",
                columns: new[] { "Module", "Key", "ScopeKind", "ScopeId", "ValidFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_COR_ParameterVersions_PublicId",
                schema: "dbo",
                table: "COR_ParameterVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_TaxDefinitions_TaxedOnDefinitionId",
                schema: "dbo",
                table: "COR_TaxDefinitions",
                column: "TaxedOnDefinitionId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_TaxDefinitions_Code",
                schema: "dbo",
                table: "COR_TaxDefinitions",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_COR_TaxDefinitions_PublicId",
                schema: "dbo",
                table: "COR_TaxDefinitions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_TaxRates_MunicipalityDaneCode",
                schema: "dbo",
                table: "COR_TaxRates",
                column: "MunicipalityDaneCode");

            migrationBuilder.CreateIndex(
                name: "IX_COR_TaxRates_TaxDefinitionId",
                schema: "dbo",
                table: "COR_TaxRates",
                column: "TaxDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_TaxRates_WithholdingConceptId",
                schema: "dbo",
                table: "COR_TaxRates",
                column: "WithholdingConceptId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_TaxRates_Code_ValidFrom",
                schema: "dbo",
                table: "COR_TaxRates",
                columns: new[] { "Code", "ValidFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_COR_TaxRates_PublicId",
                schema: "dbo",
                table: "COR_TaxRates",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_WithholdingConcepts_Code",
                schema: "dbo",
                table: "COR_WithholdingConcepts",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_COR_WithholdingConcepts_PublicId",
                schema: "dbo",
                table: "COR_WithholdingConcepts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_SEC_PermissionAmountLimits_PublicId",
                schema: "dbo",
                table: "SEC_PermissionAmountLimits",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_SEC_PermissionAmountLimits_Role_Permission_ValidFrom",
                schema: "dbo",
                table: "SEC_PermissionAmountLimits",
                columns: new[] { "RoleId", "PermissionCode", "ValidFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            // Las cinco filas de arrendamiento, libres y vencidas (T186). Idempotente: sólo las que falten.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[COR_BackgroundLeases]') IS NOT NULL
INSERT INTO [dbo].[COR_BackgroundLeases] ([Name], [Owner], [LeaseUntil], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
SELECT v.[Name], NULL, '2000-01-01T00:00:00', NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
FROM (VALUES (N'integration.dispatch'), (N'audit.forward'), (N'einvoicing.process'), (N'scheduled.tasks'), (N'email.dispatch')) AS v([Name])
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[COR_BackgroundLeases] l WHERE l.[Name] = v.[Name]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COR_Alerts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_ApprovalDecisions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_ApprovalPolicyLevels",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_AuditAnchors",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_AuditChainHeads",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_AuditOutbox",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_BackgroundLeases",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_IntegrationMessageDeliveries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_IntegrationMessageDependencies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_OperationKeys",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_ParameterVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_TaxRates",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SEC_PermissionAmountLimits",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_AlertTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_ApprovalRequests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_IntegrationMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_TaxDefinitions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_WithholdingConcepts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_ApprovalPolicies",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "UK_COR_Cities_DaneCode",
                schema: "dbo",
                table: "COR_Cities");

            migrationBuilder.DropColumn(
                name: "IsIncomeTaxFiler",
                schema: "dbo",
                table: "COR_People");

            migrationBuilder.DropColumn(
                name: "IsObligatedToInvoice",
                schema: "dbo",
                table: "COR_People");

            migrationBuilder.DropColumn(
                name: "IsSelfWithholder",
                schema: "dbo",
                table: "COR_People");

            migrationBuilder.DropColumn(
                name: "IsSimpleTaxRegime",
                schema: "dbo",
                table: "COR_People");

            migrationBuilder.DropColumn(
                name: "IsVatResponsible",
                schema: "dbo",
                table: "COR_People");

            migrationBuilder.DropColumn(
                name: "IsVatWithholdingAgent",
                schema: "dbo",
                table: "COR_People");

            migrationBuilder.DropColumn(
                name: "AlertPublicId",
                schema: "dbo",
                table: "COR_Notifications");

            migrationBuilder.DropColumn(
                name: "DaneCode",
                schema: "dbo",
                table: "COR_Cities");

            migrationBuilder.DropColumn(
                name: "MunicipalityDaneCode",
                schema: "dbo",
                table: "COR_Branches");
        }
    }
}
