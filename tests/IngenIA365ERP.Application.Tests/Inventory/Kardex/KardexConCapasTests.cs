using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Inventory.Parameters;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Kardex;

/// <summary>
/// Feature 012, I5, US16, T849 (data-model §3.5; FR-042): la vista <c>kardex</c> dice, en PEPS, qué capas consumió cada salida —la
/// cantidad de cada capa con la fecha en que entró y, con <c>Inventory.Costs.Read</c>, su costo unitario—; en promedio ponderado la
/// columna va vacía. La columna es visible y va antes de las ocultas: sus índices no cambian. (nuevo)
/// </summary>
public class KardexConCapasTests
{
    private static async Task<KardexDePrueba> PepsAsync()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.Entrega = EntregaDelComercio.I5;
        k.Parametro(ParametrosDeInventario.CosteoMetodo, "Peps");
        await Confirmado(k, "AJP", new DateOnly(2026, 9, 5), 10m, 1000m);
        await Confirmado(k, "AJP", new DateOnly(2026, 9, 10), 10m, 1300m);
        await Confirmado(k, "AJN", new DateOnly(2026, 9, 15), 15m);
        k.C.Db.ChangeTracker.Clear();
        return k;
    }

    private static async Task Confirmado(KardexDePrueba k, string tipo, DateOnly fecha, decimal cantidad, decimal? costo = null)
    {
        var (_, r) = await k.AjusteAsync(k.Borrador(tipo, causa: tipo == "AJN" ? k.Causa() : null, fecha: fecha, lineas: [k.Linea(k.P1, cantidad, costo)]));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
    }

    private static Task<IngenIA365ERP.Application.Common.Models.Result<IngenIA365ERP.Application.Common.Reports.TablaExportable>> KardexAsync(KardexDePrueba k) =>
        new KardexReportQueryHandler(k.C.Db, k.Alcance, k.Permisos, k.C.Reloj).Handle(
            new KardexReportQuery(new FiltrosDeInformeDeInventario { Product = k.P1, From = new DateOnly(2026, 9, 1), To = new DateOnly(2026, 9, 25) }), default);

    [Fact]
    public async Task En_PEPS_cada_salida_muestra_las_capas_que_consumio()
    {
        var k = await PepsAsync();

        var r = await KardexAsync(k);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var indice = r.Value.Columnas.ToList().FindIndex(c => c.Nombre == "Capas consumidas");
        indice.Should().Be(15, "va tras «Costo promedio» y antes de las columnas ocultas");
        r.Value.Columnas.Where(c => c.EsOculta).Select(c => c.Clave).Should().Equal("_documento", "_producto", "_bodega");
        var salida = r.Value.Filas.Single(f => f.Valores[8] is decimal s && s == 15m);
        salida.Valores[indice].Should().Be("10 × 1.000,00 (capa del 05/09/2026); 5 × 1.300,00 (capa del 10/09/2026)");
        r.Value.Filas.Where(f => f != salida).Should().OnlyContain(f => f.Valores[indice] == null, "las entradas no consumen capas");
    }

    [Fact]
    public async Task Sin_permiso_de_costos_las_capas_van_sin_costo()
    {
        var k = await PepsAsync();
        k.Permisos.HasPermissionAsync(PermisosDeGrupo.LeerCostos, Arg.Any<CancellationToken>()).Returns(false);

        var r = await KardexAsync(k);

        var indice = r.Value.Columnas.ToList().FindIndex(c => c.Nombre == "Capas consumidas");
        r.Value.Filas.Single(f => f.Valores[8] is decimal s && s == 15m).Valores[indice]
            .Should().Be("10 (capa del 05/09/2026); 5 (capa del 10/09/2026)");
    }
}
