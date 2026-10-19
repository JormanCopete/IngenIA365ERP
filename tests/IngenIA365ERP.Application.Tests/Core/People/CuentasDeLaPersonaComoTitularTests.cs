using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.People.Queries;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>
/// Feature 014 (decisión del dueño del 2026-10-18, research §13 D20): la ficha de la persona cuenta y suma sólo las cuentas donde es <b>titular</b>; las de
/// cotitular se cuentan aparte (<c>CoHeldAccounts</c>) y se listan en su ventana (<see cref="GetPersonCoHeldAccountsQuery"/>).
/// </summary>
public class CuentasDeLaPersonaComoTitularTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);
    private readonly Guid _persona = Guid.NewGuid();
    private readonly IConsultasDeCartera _consultas = Substitute.For<IConsultasDeCartera>();

    public CuentasDeLaPersonaComoTitularTests()
    {
        ProductoDeCaptacionDto Producto(string numero, NaturalezaDeCuenta naturaleza, RolEnLaCuenta rol, decimal saldo, EstadoDeCuenta estado = EstadoDeCuenta.Active) =>
            new(Guid.NewGuid(), numero, naturaleza, naturaleza == NaturalezaDeCuenta.Deposit ? ClaseDeCaptacion.Demand : null, "AH", "Ahorro", rol, estado,
                saldo, saldo, null, null);
        _consultas.ProductosDeCaptacionAsync(Arg.Any<ConsultaDeProductosDeCaptacion>(), Arg.Any<CancellationToken>()).Returns(new ProductosDeCaptacionDto(
            EstadoDeCartera.Active, EstadoDeCartera.Active,
            [
                new ProductosDePersonaDto(_persona, true, false,
                [
                    Producto("AH-1", NaturalezaDeCuenta.Deposit, RolEnLaCuenta.Holder, 100m),
                    Producto("AH-2", NaturalezaDeCuenta.Deposit, RolEnLaCuenta.CoHolder, 900m),
                    Producto("AH-3", NaturalezaDeCuenta.Deposit, RolEnLaCuenta.CoHolder, 50m, EstadoDeCuenta.Closed),
                    Producto("AP-1", NaturalezaDeCuenta.Contribution, RolEnLaCuenta.Holder, 300m),
                    Producto("AH-4", NaturalezaDeCuenta.Deposit, RolEnLaCuenta.Beneficiary, 70m),
                ]),
            ]));
    }

    [Fact]
    public async Task El_resumen_cuenta_solo_las_cuentas_como_titular_y_aparte_las_de_cotitular()
    {
        var resumen = await ResumenDeCaptacionesDePersona.ArmarAsync(_consultas, _persona, Hoy, default);

        resumen.Cuentas.Should().Be(1);
        resumen.Ahorros.Should().Be(100m, "el saldo de la cuenta como cotitular no suma a la ficha");
        resumen.Aportes.Should().Be(300m);
        resumen.ComoCotitular.Should().Be(1, "la cerrada no cuenta");
    }

    [Fact]
    public async Task La_ventana_de_cotitular_lista_sus_cuentas_vivas()
    {
        var db = TestDbContextFactory.Create();
        db.People.Add(new Person { PublicId = _persona, FirstName = "Ana", LastName = "Ruiz", TaxId = "900", IdType = "CC" });
        await db.SaveChangesAsync();
        var reloj = Substitute.For<IDateTimeService>();
        reloj.HoyLocal.Returns(Hoy);

        var r = await new GetPersonCoHeldAccountsQueryHandler(db, _consultas, reloj).Handle(new GetPersonCoHeldAccountsQuery(_persona), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Available.Should().BeTrue();
        r.Value.Accounts.Should().ContainSingle().Which.Number.Should().Be("AH-2");
        (await new GetPersonCoHeldAccountsQueryHandler(db, _consultas, reloj).Handle(new GetPersonCoHeldAccountsQuery(Guid.NewGuid()), default))
            .Error.Code.Should().Be("Person.NotFound");
    }
}
