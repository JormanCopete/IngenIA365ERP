using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.GoLive;
using IngenIA365ERP.Domain.Entities.Inventory.GoLive;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Contable = IngenIA365ERP.Application.Common.Integration.Accounting;

namespace IngenIA365ERP.Application.Tests.Inventory.GoLive;

/// <summary>
/// Feature 012, T470 (FR-090, US4-2, US4-3, SC-018; api.md §13.3; data-model §6.4): la activación con el cuadre contable de I2,
/// con un doble de <c>IContabilidadParaInventario.SaldosDeCuentasMapeadasAsync</c>. El valorizado del conjunto suma la bodega que se
/// activa (B3, saldo inicial 10 × 1.500), las activas que usan las cuentas (PRIN, 4 × 1.000 de P2) y las no activas con sus cifras de
/// SOLIDO al corte (B4, 3.000); los bloqueos <c>.LegacyFiguresMissing</c>, <c>.RulesMissing</c>, <c>.AccountingUnavailable</c>,
/// <c>.AlreadyActive</c>, <c>.OpeningBalanceNotConfirmed</c> y <c>.CutoffMismatch</c>; la diferencia exige aceptarla con permiso; y
/// el POST vuelve a calcular.
/// </summary>
public class ActivacionConCuadreTests
{
    private static readonly DateOnly Corte = PuestaEnMarchaDePrueba.Corte;
    private const string Permiso = ActivateWarehouseCommandHandler.PermisoDeAceptarDiferencia;

    private readonly Contable.IContabilidadParaInventario _contabilidad = Substitute.For<Contable.IContabilidadParaInventario>();

    /// <summary>B3 con su saldo inicial confirmado, PRIN con 4.000 antes del corte y las cifras de B4 al corte.</summary>
    private async Task<PuestaEnMarchaDePrueba> EscenarioAsync(bool cifrasDeB4 = true)
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        p.Contabilidad = _contabilidad;
        var (_, entrada) = await p.K.AjusteAsync(p.K.Borrador("AJP", p.K.Principal, fecha: new DateOnly(2026, 9, 5),
            lineas: [p.K.Linea(p.K.P2, 4m, 1000m)]));
        entrada.IsSuccess.Should().BeTrue(entrada.IsFailure ? $"{entrada.Error.Code}: {entrada.Error.Message}" : string.Empty);
        await p.SaldoConfirmadoAsync(p.B3, "P1", 10m, 1500m);
        if (cifrasDeB4)
        {
            p.Db.LegacyFigures.Add(new LegacyFigure
            {
                ImportBatchPublicId = Guid.NewGuid(), AsOfDate = Corte, ProductCodeRaw = "P1", WarehouseCodeRaw = "B4",
                ProductId = p.K.ProductoId(p.K.P1), WarehouseId = p.B4.Id, AccountingGroupId = p.K.C.GrupoAbarrotes.Id,
                Quantity = 2m, Value = 3000m, SourceFileName = "cifras.xlsx",
            });
            await p.Db.SaveChangesAsync();
            p.Db.ChangeTracker.Clear();
        }
        return p;
    }

    /// <summary>Un conjunto ABARR con las bodegas indicadas y ese saldo contable.</summary>
    private void Saldo(decimal saldo, params string[] bodegas) =>
        _contabilidad.SaldosDeCuentasMapeadasAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Result.Success<IReadOnlyList<Contable.ConjuntoDeCuentasDto>>(
        [
            new Contable.ConjuntoDeCuentasDto(["ABARR"], bodegas.Select(b => new Contable.ParGrupoBodegaDto("ABARR", b)).ToList(),
                [new Contable.CuentaDelConjuntoDto("14350501", "Mercancías", "Inventario", [new Contable.SaldoPorSucursalDto(Guid.NewGuid(), saldo)])],
                saldo),
        ]));

    private Task<Result<ActivationPreviewDto>> VistaAsync(PuestaEnMarchaDePrueba p, Guid? bodega = null) =>
        p.VistaPrevia().Handle(new GetWarehouseActivationPreviewQuery(bodega ?? p.B3.PublicId, Corte), default);

    private static Task<Result<ActivationResultDto>> ActivarAsync(PuestaEnMarchaDePrueba p, Guid? bodega = null, bool aceptar = false, string motivo = "") =>
        p.Activar().Handle(new ActivateWarehouseCommand(bodega ?? p.B3.PublicId, Corte, aceptar, motivo), default);

    [Fact]
    public async Task El_conjunto_suma_la_bodega_las_activas_y_las_no_activas_con_sus_cifras_y_cuadra()
    {
        var p = await EscenarioAsync();
        Saldo(22_000m, "B3", "PRIN", "B2", "B4");

        var r = await VistaAsync(p);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var conjunto = r.Value.Sets.Should().ContainSingle().Which;
        conjunto.Valuation.ThisWarehouse.Should().Be(15_000m);
        conjunto.Valuation.ActiveWarehouses.Select(a => (a.Code, a.Value)).Should().Equal(("PRIN", 4_000m));
        conjunto.Valuation.LegacyWarehouses.Select(l => (l.WarehouseCode, l.Value, l.FiguresAsOf)).Should().Equal(("B4", 3_000m, Corte));
        conjunto.Valuation.Total.Should().Be(22_000m);
        conjunto.LedgerBalance.Should().Be(22_000m);
        conjunto.Difference.Should().Be(0m);
        conjunto.Accounts.Should().ContainSingle().Which.Role.Should().Be("Inventario");
        r.Value.Blockers.Should().BeEmpty();
        r.Value.CanActivate.Should().BeTrue();
        r.Value.RequiresAcceptance.Should().BeFalse();

        var activada = await ActivarAsync(p);

        activada.IsSuccess.Should().BeTrue(activada.IsFailure ? $"{activada.Error.Code}: {activada.Error.Message}" : string.Empty);
        activada.Value.DifferenceAccepted.Should().BeFalse();
        var fila = await p.Db.WarehouseActivations.AsNoTracking().SingleAsync();
        fila.IsBalanced.Should().BeTrue();
        fila.TotalDifference.Should().Be(0m);
        JsonDocument.Parse(fila.ComparisonJson).RootElement.GetProperty("sets").GetArrayLength().Should().Be(1);
        (await p.Db.Warehouses.AsNoTracking().SingleAsync(w => w.Id == p.B3.Id)).ActivationStatus.Should().Be(WarehouseActivationStatus.Active);
    }

    [Fact]
    public async Task Con_diferencia_exige_aceptarla_con_el_permiso_especial()
    {
        var p = await EscenarioAsync();
        Saldo(21_000m, "B3", "PRIN", "B4");

        var vista = await VistaAsync(p);
        vista.Value.TotalDifference.Should().Be(1_000m);
        vista.Value.RequiresAcceptance.Should().BeTrue();

        var sinAceptar = await ActivarAsync(p);
        sinAceptar.Error.Code.Should().Be(GoLiveErrors.ActivationDifferenceCode);

        p.K.Permisos.HasPermissionAsync(Permiso, Arg.Any<CancellationToken>()).Returns(false);
        var sinPermiso = await ActivarAsync(p, aceptar: true, motivo: "Diferencia de SOLIDO conocida");
        sinPermiso.Error.Code.Should().Be(GoLiveErrors.ActivationAcceptDifferenceNotAllowedCode);

        p.K.Permisos.HasPermissionAsync(Permiso, Arg.Any<CancellationToken>()).Returns(true);
        var aceptada = await ActivarAsync(p, aceptar: true, motivo: "Diferencia de SOLIDO conocida");
        aceptada.IsSuccess.Should().BeTrue(aceptada.IsFailure ? $"{aceptada.Error.Code}: {aceptada.Error.Message}" : string.Empty);
        aceptada.Value.DifferenceAccepted.Should().BeTrue();
        aceptada.Value.Reason.Should().Be("Diferencia de SOLIDO conocida", "con la comparación hecha, el motivo no lleva el prefijo «sin comparación»");
    }

    [Fact]
    public async Task Una_bodega_no_activa_que_comparte_cuentas_sin_cifras_bloquea()
    {
        var p = await EscenarioAsync(cifrasDeB4: false);
        Saldo(19_000m, "B3", "PRIN", "B4");

        var vista = await VistaAsync(p);
        var bloqueo = vista.Value.Blockers.Should().ContainSingle().Which;
        bloqueo.Code.Should().Be(GoLiveErrors.ActivationLegacyFiguresMissingCode);
        JsonSerializer.SerializeToElement(bloqueo.Data).GetProperty("warehouseCodes")[0].GetString().Should().Be("B4");
        vista.Value.CanActivate.Should().BeFalse();

        var r = await ActivarAsync(p, aceptar: true, motivo: "igual");
        r.Error.Code.Should().Be(GoLiveErrors.ActivationLegacyFiguresMissingCode);
        (await p.Db.WarehouseActivations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_grupo_de_la_bodega_sin_regla_de_inventario_bloquea()
    {
        var p = await EscenarioAsync();
        Saldo(4_000m, "PRIN");

        var vista = await VistaAsync(p);
        var bloqueo = vista.Value.Blockers.Should().ContainSingle().Which;
        bloqueo.Code.Should().Be(GoLiveErrors.ActivationRulesMissingCode);
        JsonSerializer.SerializeToElement(bloqueo.Data).GetProperty("accountingGroups")[0].GetString().Should().Be("ABARR");

        (await ActivarAsync(p, aceptar: true, motivo: "igual")).Error.Code.Should().Be(GoLiveErrors.ActivationRulesMissingCode);
    }

    [Fact]
    public async Task Si_contabilidad_no_responde_avisa_y_en_produccion_no_activa()
    {
        var p = await EscenarioAsync();
        _contabilidad.SaldosDeCuentasMapeadasAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("caída"));
        p.Opciones.PermitirActivacionSinComparacion = false;

        var vista = await VistaAsync(p);
        vista.Value.Blockers.Should().ContainSingle().Which.Code.Should().Be(GoLiveErrors.ActivationAccountingUnavailableCode);
        vista.Value.Sets.Should().BeEmpty();

        (await ActivarAsync(p, aceptar: true, motivo: "igual")).Error.Code.Should().Be(GoLiveErrors.ActivationAccountingUnavailableCode);
    }

    [Fact]
    public async Task Los_bloqueos_de_I1_siguen_con_la_comparacion()
    {
        var p = await EscenarioAsync();
        Saldo(22_000m, "*");
        await p.BorradorAsync(p.B5, "P1", 1m, 100m);

        (await ActivarAsync(p, p.K.Principal.PublicId)).Error.Code.Should().Be(GoLiveErrors.ActivationAlreadyActiveCode);
        (await ActivarAsync(p, p.B5.PublicId)).Error.Code.Should().Be(GoLiveErrors.ActivationOpeningBalanceNotConfirmedCode);
        (await p.Activar().Handle(new ActivateWarehouseCommand(p.B3.PublicId, Corte.AddDays(1)), default)).Error.Code
            .Should().Be(GoLiveErrors.ActivationCutoffMismatchCode);
    }

    [Fact]
    public async Task El_POST_vuelve_a_calcular_y_no_confia_en_la_vista_previa()
    {
        var p = await EscenarioAsync();
        Saldo(22_000m, "B3", "PRIN", "B4");
        (await VistaAsync(p)).Value.TotalDifference.Should().Be(0m);

        Saldo(20_000m, "B3", "PRIN", "B4");
        var r = await ActivarAsync(p);

        r.Error.Code.Should().Be(GoLiveErrors.ActivationDifferenceCode);
        JsonSerializer.SerializeToElement(((ErrorConDatos)r.Error).Data).GetProperty("totalDifference").GetDecimal().Should().Be(2_000m);
    }
}
