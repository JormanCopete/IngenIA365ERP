using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Persistence.Inventory;
using IngenIA365ERP.Persistence.Providers;

namespace IngenIA365ERP.Application.Tests.Inventory.Common;

/// <summary>
/// Feature 012, I5 (T786, T834): el cerrojo de la Fundacional (T15) cubre las clases de compras de I5 y el costeo PEPS
/// <b>sin tocarlo</b>. La recepción contra orden y los costos adicionales bloquean su origen (la orden, la factura del flete)
/// en el paso de documentos de origen; las capas PEPS no tienen paso propio: las protege la fila exclusiva de
/// <c>INV_CostStates</c> de su ámbito; y un retroactivo bloquea los estados de costo y las existencias de todos los productos que
/// recalcula, en el orden canónico. La concurrencia real por HTTP es de <c>ConcurrenciaDeExistenciasTests</c>.
/// </summary>
public class CerrojoDeComprasYCosteoTests
{
    private static readonly DateTime Ahora = new(2026, 9, 28, 15, 30, 0, DateTimeKind.Utc);

    public static TheoryData<DatabaseProvider> Motores => new() { DatabaseProvider.PostgreSql, DatabaseProvider.SqlServer };

    [Theory]
    [MemberData(nameof(Motores))]
    public void Dos_recepciones_contra_la_misma_orden_chocan_en_la_fila_exclusiva_de_la_orden(DatabaseProvider motor)
    {
        // La orden 40 es el origen de las dos recepciones: la segunda espera a la primera y recalcula lo pendiente.
        var pedido = new PedidoDeCerrojo
        {
            Bodegas = [3],
            DocumentosDeOrigen = [40],
            EstadosDeCosto = [new(5, 0, CostMethod.WeightedAverage)],
            Existencias = [new(5, 3)],
        };

        var sentencias = SqlDelCerrojo.Sentencias(motor, pedido, "Ana", Ahora).ToList();

        var documentos = sentencias.Single(s => s.Tabla == SqlDelCerrojo.TablaDocumentos).Sql;
        documentos.Should().Contain("40");
        documentos.Should().Contain(motor == DatabaseProvider.PostgreSql ? "FOR UPDATE" : "UPDLOCK", "el origen se bloquea en exclusivo");
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void Los_costos_adicionales_bloquean_la_factura_del_flete_y_las_recepciones_en_orden_de_Id(DatabaseProvider motor)
    {
        // La factura del flete (90) y las dos recepciones (12, 55) son orígenes del LandedCost.
        var pedido = new PedidoDeCerrojo { DocumentosDeOrigen = [90, 55, 12] };

        var documentos = SqlDelCerrojo.Sentencias(motor, pedido, "Ana", Ahora).Single(s => s.Tabla == SqlDelCerrojo.TablaDocumentos).Sql;

        documentos.Should().Contain("IN (12, 55, 90)");
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void Las_capas_PEPS_no_agregan_un_paso_al_orden_canonico(DatabaseProvider motor)
    {
        var pedido = new PedidoDeCerrojo
        {
            Bodegas = [3],
            EstadosDeCosto = [new(5, 0, CostMethod.Fifo)],
            Existencias = [new(5, 3)],
        };

        var sentencias = SqlDelCerrojo.Sentencias(motor, pedido, "Ana", Ahora).ToList();

        sentencias.Select(s => s.Tabla).Should().Equal(
            "INV_Setup", "INV_Warehouses", "INV_CostStates", "INV_CostStates", "INV_StockBalances", "INV_StockBalances");
        sentencias.Should().NotContain(s => s.Sql.Contains("INV_CostLayers") || s.Sql.Contains("INV_LayerConsumptions"),
            "las capas y sus consumos quedan protegidos por la fila exclusiva de INV_CostStates del ámbito");
        sentencias.Single(s => s.Tabla == SqlDelCerrojo.TablaEstadosDeCosto && s.Parametros.Count == 0).Sql
            .Should().Contain(motor == DatabaseProvider.PostgreSql ? "FOR UPDATE" : "UPDLOCK");
    }

    [Theory]
    [MemberData(nameof(Motores))]
    public void Un_retroactivo_bloquea_los_estados_de_costo_y_las_existencias_de_todos_los_productos_que_recalcula(DatabaseProvider motor)
    {
        // El retroactivo recalcula los productos 9, 2 y 5 en dos bodegas (ámbito cooperativa): todos van, ordenados y sin repetir.
        var pedido = new PedidoDeCerrojo
        {
            Bodegas = [7, 3],
            EstadosDeCosto = [new(9, 0, CostMethod.WeightedAverage), new(2, 0, CostMethod.WeightedAverage), new(5, 0, CostMethod.WeightedAverage), new(2, 0, CostMethod.WeightedAverage)],
            Existencias = [new(9, 7), new(2, 3), new(5, 3), new(5, 7), new(2, 3)],
        };

        var sentencias = SqlDelCerrojo.Sentencias(motor, pedido, "Ana", Ahora).ToList();

        var costos = sentencias.Where(s => s.Tabla == SqlDelCerrojo.TablaEstadosDeCosto).Select(s => s.Sql).Last();
        var existencias = sentencias.Where(s => s.Tabla == SqlDelCerrojo.TablaExistencias).Select(s => s.Sql).Last();
        PosicionesDe(costos, "(2, 0)", "(5, 0)", "(9, 0)").Should().BeInAscendingOrder();
        PosicionesDe(existencias, "(2, 3)", "(5, 3)", "(5, 7)", "(9, 7)").Should().BeInAscendingOrder();
        sentencias.FindIndex(s => s.Tabla == SqlDelCerrojo.TablaEstadosDeCosto)
            .Should().BeLessThan(sentencias.FindIndex(s => s.Tabla == SqlDelCerrojo.TablaExistencias));
    }

    private static List<int> PosicionesDe(string sql, params string[] tuplas)
    {
        var posiciones = tuplas.Select(t => sql.IndexOf(t, StringComparison.Ordinal)).ToList();
        posiciones.Should().NotContain(-1, $"cada clave aparece en «{sql}»");
        return posiciones;
    }
}
