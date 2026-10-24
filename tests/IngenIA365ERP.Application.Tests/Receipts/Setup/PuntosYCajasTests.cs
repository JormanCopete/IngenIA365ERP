using FluentAssertions;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Setup;

/// <summary>
/// Feature 015, T0065 (US1; FR-021, FR-022, FR-028, FR-180, FR-181; data-model §1.2–§1.4): el código es la nomenclatura de la cooperativa (mayúsculas,
/// único entre vivos; <c>Catalogo.CodigoDuplicado</c> con el nombre del existente); la caja lleva base, modo (<c>DailyBase</c> por defecto), tope de aviso y
/// tope de bloqueo (nulo no bloquea) y a lo sumo una caja principal por oficina; un cambio se guarda en la caja y la sesión abierta conserva lo sellado
/// (lo prueba la sesión, US2); el cajero tiene persona y sus vigencias no se cruzan.
/// </summary>
public class PuntosYCajasTests
{
    private readonly EscenarioDeRecaudo _e = new();

    private CreateCollectionPointCommandHandler CrearPunto() => new(_e.Db, _e.Alcance, _e.Reloj);

    private CreateCashBoxCommandHandler CrearCaja() => new(_e.Db, _e.Alcance, _e.Reloj);

    [Fact]
    public async Task El_codigo_se_normaliza_y_un_duplicado_dice_cual_existe()
    {
        var primero = await CrearPunto().Handle(new CreateCollectionPointCommand(_e.OficinaA.PublicId, "norte", "Punto norte"), CancellationToken.None);
        var segundo = await CrearPunto().Handle(new CreateCollectionPointCommand(_e.OficinaB.PublicId, "NORTE", "Otro"), CancellationToken.None);

        primero.Value.Code.Should().Be("NORTE");
        primero.Value.ValidFrom.Should().Be(EscenarioDeRecaudo.Hoy);
        segundo.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        segundo.Error.Message.Should().Contain("Punto norte");
    }

    [Fact]
    public void El_validador_exige_un_codigo_de_catalogo()
    {
        var v = new CreateCollectionPointCommandValidator();
        v.Validate(new CreateCollectionPointCommand(_e.OficinaA.PublicId, "CON ESPACIO", "x")).IsValid.Should().BeFalse();
        v.Validate(new CreateCollectionPointCommand(_e.OficinaA.PublicId, "ABCDEFGHIJK", "x")).IsValid.Should().BeFalse("admite 10 caracteres");
        v.Validate(new CreateCollectionPointCommand(_e.OficinaA.PublicId, "PTO-1", "x")).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task La_caja_nace_con_base_del_dia_y_sus_topes()
    {
        var punto = _e.Punto("NORTE");

        var r = await CrearCaja().Handle(new CreateCashBoxCommand(punto.PublicId, "c1", "Caja 1", CashBoxBaseMode.DailyBase, 500_000, 3_000_000, null, null,
            false), CancellationToken.None);

        r.Value.Code.Should().Be("C1");
        r.Value.BaseMode.Should().Be(CashBoxBaseMode.DailyBase);
        r.Value.CashBlockCap.Should().BeNull("sin tope de bloqueo, no bloquea (FR-181)");
        r.Value.CashWarnCap.Should().Be(3_000_000);
    }

    [Fact]
    public async Task El_tope_de_bloqueo_no_es_menor_que_el_de_aviso()
    {
        var punto = _e.Punto("NORTE");
        var r = await CrearCaja().Handle(new CreateCashBoxCommand(punto.PublicId, "C1", "Caja", CashBoxBaseMode.DailyBase, 0, 5_000_000, 4_000_000, null,
            false), CancellationToken.None);
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoTopesInvalidos);
    }

    [Fact]
    public async Task La_caja_es_unica_por_punto_y_hay_una_sola_caja_principal_por_oficina()
    {
        var norte = _e.Punto("NORTE");
        var sur = _e.Punto("SUR");
        await CrearCaja().Handle(new CreateCashBoxCommand(norte.PublicId, "C1", "Caja", CashBoxBaseMode.DailyBase, 0, null, null, null, true), CancellationToken.None);

        var otraEnSur = await CrearCaja().Handle(new CreateCashBoxCommand(sur.PublicId, "C1", "Caja sur", CashBoxBaseMode.FixedFund, 0, null, null, null, false),
            CancellationToken.None);
        var repetida = await CrearCaja().Handle(new CreateCashBoxCommand(norte.PublicId, "C1", "Otra", CashBoxBaseMode.DailyBase, 0, null, null, null, false),
            CancellationToken.None);
        var segundaPrincipal = await CrearCaja().Handle(new CreateCashBoxCommand(sur.PublicId, "C2", "Principal 2", CashBoxBaseMode.DailyBase, 0, null, null,
            null, true), CancellationToken.None);

        otraEnSur.IsSuccess.Should().BeTrue("el código es único por punto");
        repetida.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        segundaPrincipal.Error.Code.Should().Be(ErroresDeRecaudo.CodigoCajaPrincipalRepetida);
    }

    [Fact]
    public async Task Cambiar_la_base_queda_en_la_caja_para_la_proxima_sesion()
    {
        var caja = _e.Caja(_e.Punto("NORTE"), "C1");

        var r = await new UpdateCashBoxCommandHandler(_e.Db, _e.Alcance, _e.Reloj).Handle(new UpdateCashBoxCommand(caja.PublicId, "Caja 1",
            CashBoxBaseMode.FixedFund, 800_000, null, null, null, false), CancellationToken.None);

        r.Value.BaseAmount.Should().Be(800_000);
        r.Value.BaseMode.Should().Be(CashBoxBaseMode.FixedFund);
    }

    [Fact]
    public async Task Inactivar_no_borra()
    {
        var caja = _e.Caja(_e.Punto("NORTE"), "C1");
        await new InactivateCashBoxCommandHandler(_e.Db, _e.Alcance, _e.Reloj).Handle(new InactivateCashBoxCommand(caja.PublicId), CancellationToken.None);

        (await _e.Db.CashBoxes.SingleAsync()).Status.Should().Be(ReceiptsCatalogStatus.Inactive);
        var activas = await new ListCashBoxesQueryHandler(_e.Db, _e.Alcance, _e.Reloj).Handle(new ListCashBoxesQuery(), CancellationToken.None);
        activas.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task El_cajero_tiene_persona_y_sus_vigencias_no_se_cruzan()
    {
        var caja = _e.Caja(_e.Punto("NORTE"), "C1");
        var asignar = new AssignCashierCommandHandler(_e.Db, _e.Alcance, _e.Reloj);

        (await asignar.Handle(new AssignCashierCommand(caja.PublicId, _e.SinPersona.PublicId), CancellationToken.None)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoCajeroSinPersona);
        var ok = await asignar.Handle(new AssignCashierCommand(caja.PublicId, _e.Cajera.PublicId), CancellationToken.None);
        ok.Value.Should().ContainSingle(c => c.PersonName == "Carla Caja");
        (await asignar.Handle(new AssignCashierCommand(caja.PublicId, _e.Cajera.PublicId, EscenarioDeRecaudo.Hoy.AddDays(3)), CancellationToken.None)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoCajeroSeCruza);

        var retiro = await new RetireCashierCommandHandler(_e.Db, _e.Alcance, _e.Reloj)
            .Handle(new RetireCashierCommand(caja.PublicId, _e.Cajera.PublicId, EscenarioDeRecaudo.Hoy.AddDays(10)), CancellationToken.None);
        retiro.Value.Single().ValidTo.Should().Be(EscenarioDeRecaudo.Hoy.AddDays(10));
        (await asignar.Handle(new AssignCashierCommand(caja.PublicId, _e.Cajera.PublicId, EscenarioDeRecaudo.Hoy.AddDays(11)), CancellationToken.None)).IsSuccess
            .Should().BeTrue("después de cerrar la vigencia puede volver");
    }
}
