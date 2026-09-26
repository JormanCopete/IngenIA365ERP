using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.GoLive;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.GoLive;

/// <summary>
/// Feature 012, T301 (api.md §13.3; data-model §6.4; FR-090, US4-2, US4-3): la activación de una bodega tal como queda en I1,
/// antes de que US7 registre la consulta de saldos contables. La vista previa trae <c>sets: []</c> y el aviso
/// <c>Inventory.Activation.AccountingUnavailable</c>; en producción (<see cref="PuestaEnMarchaOptions.PermitirActivacionSinComparacion"/>
/// en falso) el POST responde 422 sin escribir nada; fuera de ella sólo activa aceptando la diferencia, con el permiso especial y
/// motivo, deja la fila de <c>INV_WarehouseActivations</c> «sin comparación contable» y activa el tránsito con la primera bodega
/// de la sucursal. Los bloqueos duros: saldo sin confirmar, corte distinto, ya activa, período cerrado.
/// </summary>
public class ActivateWarehouseCommandHandlerTests
{
    private const string Permiso = ActivateWarehouseCommandHandler.PermisoDeAceptarDiferencia;

    private static Task<Result<ActivationResultDto>> ActivarAsync(PuestaEnMarchaDePrueba p, Guid bodega, DateOnly? corte = null, bool aceptar = true,
        string motivo = "Ensayo de COOFLOPAL") =>
        p.Activar().Handle(new ActivateWarehouseCommand(bodega, corte ?? PuestaEnMarchaDePrueba.Corte, aceptar, motivo), default);

    [Fact]
    public async Task La_vista_previa_no_trae_conjuntos_y_avisa_que_contabilidad_no_responde()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        await p.SaldoConfirmadoAsync(p.B3, "P1", 10m, 1500m);

        var r = await p.VistaPrevia().Handle(new GetWarehouseActivationPreviewQuery(p.B3.PublicId, PuestaEnMarchaDePrueba.Corte), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Sets.Should().BeEmpty();
        r.Value.Blockers.Should().ContainSingle().Which.Code.Should().Be(GoLiveErrors.ActivationAccountingUnavailableCode);
        r.Value.OpeningBalance.Confirmed.Should().BeTrue();
        r.Value.OpeningBalance.Value.Should().Be(15_000m);
        r.Value.CanActivate.Should().BeTrue();
        r.Value.RequiresAcceptance.Should().BeTrue();
    }

    [Fact]
    public async Task En_produccion_sin_comparacion_responde_AccountingUnavailable_y_no_escribe_nada()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        await p.SaldoConfirmadoAsync(p.B3, "P1", 10m, 1500m);
        p.Opciones.PermitirActivacionSinComparacion = false;

        var r = await ActivarAsync(p, p.B3.PublicId);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(GoLiveErrors.ActivationAccountingUnavailableCode);
        (await p.Db.WarehouseActivations.CountAsync()).Should().Be(0);
        (await p.Db.Warehouses.AsNoTracking().SingleAsync(w => w.Id == p.B3.Id)).ActivationStatus.Should().Be(WarehouseActivationStatus.NotActivated);
    }

    [Fact]
    public async Task Fuera_de_produccion_exige_aceptar_la_diferencia_con_permiso_y_motivo()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        await p.SaldoConfirmadoAsync(p.B3, "P1", 10m, 1500m);

        var sinAceptar = await ActivarAsync(p, p.B3.PublicId, aceptar: false);
        sinAceptar.Error.Code.Should().Be(GoLiveErrors.ActivationDifferenceCode);

        p.K.Permisos.HasPermissionAsync(Permiso, Arg.Any<CancellationToken>()).Returns(false);
        var sinPermiso = await ActivarAsync(p, p.B3.PublicId);
        sinPermiso.Error.Code.Should().Be(GoLiveErrors.ActivationAcceptDifferenceNotAllowedCode);
        JsonSerializer.SerializeToElement(((ErrorConDatos)sinPermiso.Error).Data).GetProperty("permissionCode").GetString().Should().Be(Permiso);

        var validador = new ActivateWarehouseCommandValidator();
        validador.Validate(new ActivateWarehouseCommand(p.B3.PublicId, PuestaEnMarchaDePrueba.Corte, true, "")).IsValid.Should().BeFalse(
            "sin motivo, Validation.Invalid");
        validador.Validate(new ActivateWarehouseCommand(p.B3.PublicId, PuestaEnMarchaDePrueba.Corte, false, "")).IsValid.Should().BeTrue();
        (await p.Db.WarehouseActivations.CountAsync()).Should().Be(0, "sin aceptación no queda fila");
    }

    [Fact]
    public async Task Con_todo_activa_la_bodega_deja_la_fila_sin_comparacion_contable_y_activa_el_transito_con_la_primera()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        await p.SaldoConfirmadoAsync(p.B5, "P1", 10m, 1500m);

        var r = await ActivarAsync(p, p.B5.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.DifferenceAccepted.Should().BeTrue();
        var b5 = await p.Db.Warehouses.AsNoTracking().SingleAsync(w => w.Id == p.B5.Id);
        b5.ActivationStatus.Should().Be(WarehouseActivationStatus.Active);
        b5.CutoffDate.Should().Be(PuestaEnMarchaDePrueba.Corte);
        b5.ActivatedByUserId.Should().Be(KardexDePrueba.Usuario);
        b5.ActivatedAt.Should().NotBeNull();

        var fila = await p.Db.WarehouseActivations.AsNoTracking().SingleAsync();
        fila.WarehouseId.Should().Be(p.B5.Id);
        fila.IsBalanced.Should().BeFalse();
        fila.DifferenceAcceptedByUserId.Should().Be(KardexDePrueba.Usuario);
        fila.AcceptanceReason.Should().StartWith("Sin comparación contable").And.Contain("Ensayo de COOFLOPAL");
        JsonDocument.Parse(fila.ComparisonJson).RootElement.GetProperty("sets").GetArrayLength().Should().Be(0);

        (await p.Db.Warehouses.AsNoTracking().SingleAsync(w => w.Id == p.TR05.Id)).ActivationStatus.Should().Be(WarehouseActivationStatus.Active,
            "la primera bodega activa de la sucursal activa también su tránsito");

        var otraVez = await ActivarAsync(p, p.B5.PublicId);
        otraVez.Error.Code.Should().Be(GoLiveErrors.ActivationAlreadyActiveCode);
    }

    [Fact]
    public async Task Una_bodega_nueva_sin_saldo_inicial_tambien_se_activa()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();

        var r = await ActivarAsync(p, p.B4.PublicId, new DateOnly(2026, 9, 24));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
    }

    [Fact]
    public async Task Los_bloqueos_duros_responden_con_su_codigo()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        await p.BorradorAsync(p.B3, "P1", 10m, 1500m);
        await p.SaldoConfirmadoAsync(p.B4, "P1", 10m, 1500m);

        var sinConfirmar = await ActivarAsync(p, p.B3.PublicId);
        var otroCorte = await ActivarAsync(p, p.B4.PublicId, new DateOnly(2026, 9, 11));
        var yaActiva = await ActivarAsync(p, p.K.Principal.PublicId);

        sinConfirmar.Error.Code.Should().Be(GoLiveErrors.ActivationOpeningBalanceNotConfirmedCode);
        otroCorte.Error.Code.Should().Be(GoLiveErrors.ActivationCutoffMismatchCode);
        yaActiva.Error.Code.Should().Be(GoLiveErrors.ActivationAlreadyActiveCode);

        var setup = await p.Db.InventorySetups.SingleAsync();
        setup.LastClosedDate = new DateOnly(2026, 9, 30);
        await p.Db.SaveChangesAsync();
        p.Db.ChangeTracker.Clear();
        var cerrado = await ActivarAsync(p, p.B4.PublicId);
        cerrado.Error.Code.Should().Be("Inventory.Period.Closed");
        (await p.Db.WarehouseActivations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_bodega_fuera_del_alcance_es_la_inexistente()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        p.K.Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Application.Common.Interfaces.Security.AlcanceDeInventario.Vacio);

        var r = await ActivarAsync(p, p.B3.PublicId);

        r.Error.Code.Should().Be("Inventory.Warehouse.NotFound");
    }
}
