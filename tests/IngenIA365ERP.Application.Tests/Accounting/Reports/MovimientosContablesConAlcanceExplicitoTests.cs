using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Accounting.Reports;

/// <summary>
/// Feature 012, T492 (G5; contracts/contabilidad.md §7.2): la conciliación de Inventario compara el valorizado total
/// con el saldo contable de todas las sucursales, así que lee por <see cref="MovimientosContables"/> con el alcance
/// explícito <see cref="AlcanceDeSucursales.SinRestriccion"/> y no con el de quien consulta. El punto único de lectura
/// no cambia: la sobrecarga resuelve los mismos filtros y sólo recibe el alcance ya decidido.
/// </summary>
public class MovimientosContablesConAlcanceExplicitoTests
{
    [Fact]
    public async Task Con_alcance_explicito_lee_todas_las_sucursales_aunque_el_usuario_este_limitado()
    {
        var d = new ContabilidadTestData();
        var caja = d.Cuenta("110505");
        var ingreso = d.Cuenta("413505", AccountNature.Credit);
        var enPrincipal = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso), CancellationToken.None);
        enPrincipal.IsSuccess.Should().BeTrue(enPrincipal.Error.Message);
        await d.Db.SaveChangesAsync();
        d.RestringirA(d.Norte);
        var filtros = new FiltrosDeInforme { From = new DateOnly(2026, 3, 1), To = new DateOnly(2026, 3, 31) };

        var delUsuario = await MovimientosContables.PrepararAsync(d.Db, d.Alcance, d.Clock, filtros, CancellationToken.None);
        var explicito = await MovimientosContables.PrepararAsync(d.Db, AlcanceDeSucursales.SinRestriccion, d.Clock, filtros, CancellationToken.None);

        delUsuario.Value.Alcance.Restringido.Should().BeTrue();
        (await MovimientosContables.Base(d.Db, delUsuario.Value).CountAsync()).Should().Be(0, "el usuario sólo ve Norte");
        explicito.Value.Alcance.Restringido.Should().BeFalse();
        (await MovimientosContables.HastaInclusive(MovimientosContables.Base(d.Db, explicito.Value), new DateOnly(2026, 3, 31))
            .SumAsync(e => e.Debit - e.Credit)).Should().Be(0m);
        (await MovimientosContables.Base(d.Db, explicito.Value).CountAsync()).Should().Be(2);

        // Los mismos filtros se validan igual por las dos puertas.
        var alReves = filtros with { From = new DateOnly(2026, 4, 1) };
        (await MovimientosContables.PrepararAsync(d.Db, AlcanceDeSucursales.SinRestriccion, d.Clock, alReves, CancellationToken.None))
            .Error.Code.Should().Be(MovimientosContables.RangoInvalido.Code);
    }
}
