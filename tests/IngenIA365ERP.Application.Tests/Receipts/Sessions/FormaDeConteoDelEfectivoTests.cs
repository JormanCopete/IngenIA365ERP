using FluentAssertions;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Sessions;

/// <summary>
/// 2026-11-23 (pedido del dueño): <c>Caja.FormaDeConteoDelEfectivo</c>. Por defecto (<c>AElegir</c>) la base se abre con un solo valor; con
/// <c>Denominaciones</c> el servidor exige los billetes al abrir y al arquear (<c>Receipts.Session.CountByDenominations</c>). La clave derivada de la caja
/// única es estable por paso y distinta entre pasos.
/// </summary>
public class FormaDeConteoDelEfectivoTests
{
    private readonly CajaDePrueba _c = new();

    [Fact]
    public async Task Por_defecto_la_base_se_abre_con_un_solo_valor()
    {
        _c.E.Como(_c.E.Cajera);
        var r = await _c.Abrir().Handle(new OpenCashSessionCommand(_c.C1.PublicId, [], 350_000m), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.OpeningBase.Should().Be(350_000);
        (await _c.E.Db.CashMovements.SingleAsync()).Amount.Should().Be(350_000);
        (await _c.E.Db.CashMovementDenominations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Con_denominaciones_no_admite_la_base_con_un_solo_valor()
    {
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaFormaDeConteoDelEfectivo, ParametrosDeRecaudoYTesoreria.ConteoPorDenominaciones);
        _c.E.Como(_c.E.Cajera);
        var r = await _c.Abrir().Handle(new OpenCashSessionCommand(_c.C1.PublicId, [], 350_000m), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoConteoPorDenominaciones);
    }

    [Fact]
    public async Task Con_denominaciones_los_billetes_siguen_valiendo()
    {
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaFormaDeConteoDelEfectivo, ParametrosDeRecaudoYTesoreria.ConteoPorDenominaciones);
        _c.E.Como(_c.E.Cajera);
        var r = await _c.Abrir().Handle(new OpenCashSessionCommand(_c.C1.PublicId, CajaDePrueba.Billetes((_c.B50, 2))), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Status.Should().Be(CashSessionState.Open);
    }

    [Fact]
    public void La_clave_derivada_de_la_caja_unica_es_estable_por_paso()
    {
        var clave = Guid.NewGuid();
        CajaUnica.Derivada(clave, "cerrar").Should().Be(CajaUnica.Derivada(clave, "cerrar"));
        CajaUnica.Derivada(clave, "cerrar").Should().NotBe(CajaUnica.Derivada(clave, "entrar"));
        CajaUnica.Derivada(clave, "cerrar").Should().NotBe(clave);
    }
}
