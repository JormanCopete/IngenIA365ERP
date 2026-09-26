using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Persistence.Inventory;
using IngenIA365ERP.Persistence.Providers;

namespace IngenIA365ERP.Application.Tests.Inventory.Common;

/// <summary>
/// Feature 012, T110 (T15; data-model §0): el SQL que emite <see cref="SqlDelCerrojo"/> para cada motor. Sigue el orden
/// canónico (<c>INV_Setup</c> compartido → <c>INV_Warehouses</c> compartido por Id → documentos de origen exclusivos →
/// <c>INV_CostStates</c> → <c>INV_StockBalances</c> → <c>INV_StockDetails</c> exclusivos por Id → numeración al final),
/// ordena por <c>Id</c>, y el INSERT de las proyecciones que faltan escribe su propia auditoría y ceros. La prueba de
/// concurrencia real es <c>ConcurrenciaDeExistenciasTests</c> (US2).
/// </summary>
public class CerrojoDeInventarioSqlTests
{
    private static readonly DateTime Ahora = new(2026, 9, 25, 15, 30, 0, DateTimeKind.Utc);

    private static readonly PedidoDeCerrojo Pedido = new()
    {
        Bodegas = [7, 3, 7],
        DocumentosDeOrigen = [40, 12],
        EstadosDeCosto = [new(5, 0, CostMethod.WeightedAverage), new(2, 0, CostMethod.WeightedAverage)],
        Existencias = [new(5, 7), new(2, 3), new(5, 7)],
        Detalles = [new(5, 7, 71, null), new(2, 3, 31, 9)],
    };

    public static TheoryData<DatabaseProvider> Motores => new() { DatabaseProvider.PostgreSql, DatabaseProvider.SqlServer };

    [Theory]
    [MemberData(nameof(Motores))]
    public void Sigue_el_orden_canonico(DatabaseProvider motor)
    {
        var tablas = SqlDelCerrojo.Sentencias(motor, Pedido, "Ana Pérez", Ahora).Select(s => s.Tabla).ToList();

        tablas.Should().Equal(
            "INV_Setup",
            "INV_Warehouses",
            "INV_Documents",
            "INV_CostStates", "INV_CostStates",
            "INV_StockBalances", "INV_StockBalances",
            "INV_StockDetails", "INV_StockDetails");
        SqlDelCerrojo.Numeracion(motor, 4).Tabla.Should().Be("INV_DocumentSequences", "la fila de numeración va al final, aparte");
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void Lo_que_el_pedido_no_nombra_no_se_bloquea_salvo_el_setup(DatabaseProvider motor)
    {
        var sentencias = SqlDelCerrojo.Sentencias(motor, new PedidoDeCerrojo(), "Ana", Ahora);
        sentencias.Select(s => s.Tabla).Should().Equal("INV_Setup");
    }

    [Fact]
    public void PostgreSql_bloquea_compartido_con_FOR_SHARE_y_exclusivo_con_FOR_UPDATE_ordenando_por_Id()
    {
        var s = SqlDelCerrojo.Sentencias(DatabaseProvider.PostgreSql, Pedido, "Ana", Ahora);

        s[0].Sql.Should().Be("SELECT \"Id\" FROM \"dbo\".\"INV_Setup\" ORDER BY \"Id\" FOR SHARE;");
        s[1].Sql.Should().Be("SELECT \"Id\" FROM \"dbo\".\"INV_Warehouses\" WHERE \"Id\" IN (3, 7) ORDER BY \"Id\" FOR SHARE;",
            "las bodegas van ordenadas por Id y sin repetir");
        s[2].Sql.Should().Be("SELECT \"Id\" FROM \"dbo\".\"INV_Documents\" WHERE \"Id\" IN (12, 40) ORDER BY \"Id\" FOR UPDATE;");
        foreach (var bloqueo in new[] { s[4], s[6], s[8] })
        {
            bloqueo.Sql.Should().StartWith("SELECT t.\"Id\" FROM ").And.EndWith("ORDER BY t.\"Id\" FOR UPDATE OF t;");
        }
        SqlDelCerrojo.Numeracion(DatabaseProvider.PostgreSql, 4).Sql
            .Should().Be("SELECT \"Id\" FROM \"dbo\".\"INV_DocumentSequences\" WHERE \"Id\" IN (4) ORDER BY \"Id\" FOR UPDATE;");
    }

    [Fact]
    public void Abrir_un_conteo_toma_las_bodegas_en_exclusivo_en_los_dos_motores()
    {
        var pedido = new PedidoDeCerrojo { Bodegas = [7, 3], BodegasEnExclusivo = true };

        SqlDelCerrojo.Sentencias(DatabaseProvider.PostgreSql, pedido, "Ana", Ahora)[1].Sql
            .Should().Be("SELECT \"Id\" FROM \"dbo\".\"INV_Warehouses\" WHERE \"Id\" IN (3, 7) ORDER BY \"Id\" FOR UPDATE;");
        SqlDelCerrojo.Sentencias(DatabaseProvider.SqlServer, pedido, "Ana", Ahora)[1].Sql
            .Should().Be("SELECT [Id] FROM [dbo].[INV_Warehouses] WITH (XLOCK, ROWLOCK, HOLDLOCK) WHERE [Id] IN (3, 7) ORDER BY [Id];",
                "UPDLOCK es compatible con el compartido de las confirmaciones: no las esperaría");
    }

    [Fact]
    public void SqlServer_bloquea_con_HOLDLOCK_y_UPDLOCK_ordenando_por_Id()
    {
        var s = SqlDelCerrojo.Sentencias(DatabaseProvider.SqlServer, Pedido, "Ana", Ahora);

        s[0].Sql.Should().Be("SELECT [Id] FROM [dbo].[INV_Setup] WITH (ROWLOCK, HOLDLOCK) ORDER BY [Id];");
        s[1].Sql.Should().Be("SELECT [Id] FROM [dbo].[INV_Warehouses] WITH (ROWLOCK, HOLDLOCK) WHERE [Id] IN (3, 7) ORDER BY [Id];");
        s[2].Sql.Should().Be("SELECT [Id] FROM [dbo].[INV_Documents] WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE [Id] IN (12, 40) ORDER BY [Id];");
        foreach (var bloqueo in new[] { s[4], s[6], s[8] })
        {
            bloqueo.Sql.Should().Contain("WITH (UPDLOCK, ROWLOCK, HOLDLOCK)").And.EndWith("ORDER BY t.[Id];");
        }
        SqlDelCerrojo.Numeracion(DatabaseProvider.SqlServer, 4).Sql
            .Should().Be("SELECT [Id] FROM [dbo].[INV_DocumentSequences] WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE [Id] IN (4) ORDER BY [Id];");
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void El_setup_se_toma_exclusivo_al_cerrar_o_reabrir(DatabaseProvider motor)
    {
        var sql = SqlDelCerrojo.Sentencias(motor, new PedidoDeCerrojo { Setup = ModoDeBloqueoDelSetup.Exclusivo }, "Ana", Ahora)[0].Sql;
        sql.Should().Contain(motor == DatabaseProvider.PostgreSql ? "FOR UPDATE" : "UPDLOCK");
    }

    [Fact]
    public void PostgreSql_asegura_las_proyecciones_con_ON_CONFLICT_y_su_propia_auditoria()
    {
        var s = SqlDelCerrojo.Sentencias(DatabaseProvider.PostgreSql, Pedido, "Ana Pérez", Ahora);
        var existencias = s[5];

        existencias.Sql.Should().Be(
            "INSERT INTO \"dbo\".\"INV_StockBalances\" (\"PublicId\", \"ProductId\", \"WarehouseId\", \"Physical\", \"Reserved\", \"CreatedAt\", \"CreatedBy\", \"IsDeleted\") "
            + "SELECT gen_random_uuid(), v.\"ProductId\", v.\"WarehouseId\", 0, 0, {0}, {1}, FALSE "
            + "FROM (VALUES (2, 3), (5, 7)) AS v(\"ProductId\", \"WarehouseId\") ON CONFLICT DO NOTHING;");
        existencias.Parametros.Should().Equal(Ahora, "Ana Pérez");

        s[3].Sql.Should().Contain("\"Quantity\", \"Value\", \"AverageCost\", \"LastUnitCost\"")
            .And.Contain("v.\"Method\", 0, 0, 0, 0, {0}, {1}, FALSE")
            .And.Contain("(VALUES (2, 0, 1), (5, 0, 1))")
            .And.EndWith("ON CONFLICT DO NOTHING;");
        s[7].Sql.Should().Contain("(VALUES (2, 3, 31, 9), (5, 7, 71, CAST(NULL AS int)))", "el detalle sin lote va con un nulo tipado")
            .And.EndWith("ON CONFLICT DO NOTHING;");
    }

    [Fact]
    public void SqlServer_asegura_las_proyecciones_con_WHERE_NOT_EXISTS_UPDLOCK_HOLDLOCK()
    {
        var s = SqlDelCerrojo.Sentencias(DatabaseProvider.SqlServer, Pedido, "Ana Pérez", Ahora);
        var existencias = s[5];

        existencias.Sql.Should().Be(
            "INSERT INTO [dbo].[INV_StockBalances] ([PublicId], [ProductId], [WarehouseId], [Physical], [Reserved], [CreatedAt], [CreatedBy], [IsDeleted]) "
            + "SELECT NEWID(), v.[ProductId], v.[WarehouseId], 0, 0, {0}, {1}, 0 "
            + "FROM (VALUES (2, 3), (5, 7)) AS v([ProductId], [WarehouseId]) "
            + "WHERE NOT EXISTS (SELECT 1 FROM [dbo].[INV_StockBalances] t WITH (UPDLOCK, HOLDLOCK) WHERE t.[ProductId] = v.[ProductId] AND t.[WarehouseId] = v.[WarehouseId]);");
        existencias.Parametros.Should().Equal(Ahora, "Ana Pérez");

        s[7].Sql.Should().Contain("(t.[LotId] = v.[LotId] OR (t.[LotId] IS NULL AND v.[LotId] IS NULL))",
            "sin lote, dos nulos son la misma fila");
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void Cada_insert_escribe_PublicId_CreatedAt_UTC_CreatedBy_del_actor_IsDeleted_y_ceros(DatabaseProvider motor)
    {
        var inserts = SqlDelCerrojo.Sentencias(motor, Pedido, "Proceso de integración", Ahora).Where(x => x.Sql.StartsWith("INSERT")).ToList();

        inserts.Should().HaveCount(3);
        foreach (var insert in inserts)
        {
            insert.Sql.Should().Contain(motor == DatabaseProvider.PostgreSql ? "gen_random_uuid()" : "NEWID()");
            insert.Sql.Should().Contain("CreatedAt").And.Contain("CreatedBy").And.Contain("IsDeleted");
            insert.Sql.Should().Contain(motor == DatabaseProvider.PostgreSql ? "{0}, {1}, FALSE" : "{0}, {1}, 0");
            insert.Parametros[0].Should().BeOfType<DateTime>().Which.Kind.Should().Be(DateTimeKind.Utc);
            insert.Parametros[1].Should().Be("Proceso de integración");
        }
    }

    [Fact]
    public void El_instante_de_creacion_tiene_que_ser_UTC()
    {
        var local = () => SqlDelCerrojo.Sentencias(DatabaseProvider.PostgreSql, Pedido, "Ana", new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Local));
        local.Should().Throw<ArgumentException>();
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void El_nombre_del_actor_nunca_entra_al_SQL_como_texto(DatabaseProvider motor)
    {
        const string malicioso = "x'); DROP TABLE INV_Documents; --";
        SqlDelCerrojo.Sentencias(motor, Pedido, malicioso, Ahora).Should().OnlyContain(x => !x.Sql.Contains("DROP TABLE"));
    }
}
