using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Imports;
using IngenIA365ERP.Domain.Entities.Receipts.Vaults;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using P = IngenIA365ERP.Application.Receipts.Imports.PlantillasDePuestaEnMarchaDeRecaudo;

namespace IngenIA365ERP.Application.Tests.Receipts.Imports;

/// <summary>
/// Feature 015, US13, T0282 (archivos-y-plantillas.md §5; FR-157, FR-158): las plantillas de puesta en marcha de Recaudo sobre <c>EjecutorDeImportacion</c> —una hoja
/// «Datos» con encabezados en la fila 1, todo o nada, revisión previa que no guarda, error por fila y columna—; las cargas quedan con su marca de puesta en marcha y
/// ningún hecho contable sale a la bandeja; la fila de partida sin fecha se rechaza; la suma de la bóveda tiene que dar el saldo declarado; el cajero sin segundo
/// factor avisa. Prueba escrita, sin correr.
/// </summary>
public class PlantillasDePuestaEnMarchaDeRecaudoTests
{
    private readonly EscenarioDeRecaudo _e = new();
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly ICuentasBancariasPropias _cuentas = Substitute.For<ICuentasBancariasPropias>();
    private static readonly Guid CuentaDeRecaudo = Guid.NewGuid();

    public PlantillasDePuestaEnMarchaDeRecaudoTests()
    {
        _permisos.EsMaestroGlobal.Returns(true);
        _cuentas.DeRecaudoAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(
            [new CuentaBancariaPropiaDto(CuentaDeRecaudo, "BCO01", "Banco", "1007", "Savings", "****1234", null, true, true)]);
    }

    private void Archivo(string[] columnas, params string?[][] filas)
    {
        var tabla = new TablaLeida(columnas, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(tabla)));
    }

    private async Task<Result<ImportResultDto>> ImportarAsync(string codigo, ModoDeImportacion modo)
    {
        var ejecutor = new EjecutorDeImportacion(_e.Db, _lector, _permisos, _e.Servicios);
        var r = await new ImportReceiptsGoLiveTemplateCommandHandler(_e.Db, ejecutor, _e.Parametros, _cuentas, _e.Actor, _e.Reloj)
            .Handle(new ImportReceiptsGoLiveTemplateCommand(codigo, modo, new ArchivoDeImportacion("carga.xlsx", [1, 2, 3])), default);
        _e.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public void Cada_plantilla_tiene_una_hoja_de_datos_y_su_codigo()
    {
        P.Todas.Keys.Should().BeEquivalentTo(
            [P.PuntosYCajas, P.CajerosYAlcances, P.MediosPorPunto, P.BovedasAlCorte, P.PartidasAlCorte, P.ChequesEnCanje, P.CifrasDeReferencia]);
        P.Todas.Values.Should().OnlyContain(d => d.EsDeUnaHoja && d.Hojas[0].Nombre == "Datos");
        P.Por(P.PartidasAlCorte)!.Hojas[0].Columna(P.Fecha)!.Obligatoria.Should().BeTrue("una partida sin fecha se rechaza (Edge Cases)");
    }

    [Fact]
    public async Task Una_partida_sin_fecha_rechaza_el_archivo_entero_y_dice_fila_y_columna()
    {
        Archivo([P.CuentaPropia, P.Oficina, P.Fecha, P.Valor, P.Referencia],
            ["BCO01", "Oficina A", "2026-11-28", "150000", "REF1"],
            ["BCO01", "Oficina A", null, "90000", "REF2"]);

        var r = await ImportarAsync(P.PartidasAlCorte, ModoDeImportacion.Apply);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        (await _e.Db.UnidentifiedItems.CountAsync()).Should().Be(0, "todo o nada");
    }

    [Fact]
    public async Task Review_no_guarda_y_apply_carga_las_partidas_al_corte_sin_hechos()
    {
        Archivo([P.CuentaPropia, P.Oficina, P.Fecha, P.Valor, P.Referencia, P.Consignante],
            ["BCO01", "Oficina A", "2026-11-28", "150000", "REF1", "Pedro"],
            ["BCO01", "Oficina B", "2026-11-29", "90000", "REF2", null]);

        var revision = await ImportarAsync(P.PartidasAlCorte, ModoDeImportacion.Review);
        revision.IsSuccess.Should().BeTrue(revision.Error?.Message);
        revision.Value.Valid.Should().BeTrue();
        (await _e.Db.UnidentifiedItems.CountAsync()).Should().Be(0, "la revisión no guarda nada");

        var aplicacion = await ImportarAsync(P.PartidasAlCorte, ModoDeImportacion.Apply);
        aplicacion.IsSuccess.Should().BeTrue(aplicacion.Error?.Message);
        var partidas = await _e.Db.UnidentifiedItems.ToListAsync();
        partidas.Should().HaveCount(2).And.OnlyContain(p => p.OriginKind == UnidentifiedItemOrigin.OpeningLoad && p.Status == UnidentifiedItemState.Open
                                                            && p.OwnBankAccountPublicId == CuentaDeRecaudo);
        (await _e.Db.IntegrationMessages.CountAsync()).Should().Be(0, "la puesta en marcha no emite hechos contables (FR-158)");
    }

    [Fact]
    public async Task La_boveda_tiene_que_sumar_el_saldo_declarado()
    {
        _e.Db.Vaults.Add(new Vault { BranchId = _e.OficinaA.Id });
        _e.Db.SaveChanges();
        _e.Denominacion(50_000m);
        _e.Denominacion(20_000m);
        Archivo([P.Oficina, P.FechaDeCorte, P.Denominacion, P.Cantidad, P.SaldoDeclarado],
            ["Oficina A", "2026-11-30", "50000", "10", "800000"],
            ["Oficina A", "2026-11-30", "20000", "10", "800000"]);

        var r = await ImportarAsync(P.BovedasAlCorte, ModoDeImportacion.Apply);

        r.IsFailure.Should().BeTrue("50.000 × 10 + 20.000 × 10 = 700.000, no 800.000");
        (await _e.Db.VaultMovements.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task La_boveda_cuadrada_queda_con_su_movimiento_de_puesta_en_marcha_y_su_saldo_por_denominacion()
    {
        _e.Db.Vaults.Add(new Vault { BranchId = _e.OficinaA.Id });
        _e.Db.SaveChanges();
        _e.Denominacion(50_000m);
        _e.Denominacion(20_000m);
        Archivo([P.Oficina, P.FechaDeCorte, P.Denominacion, P.Cantidad, P.SaldoDeclarado],
            ["Oficina A", "2026-11-30", "50000", "10", "700000"],
            ["Oficina A", "2026-11-30", "20000", "10", "700000"]);

        var r = await ImportarAsync(P.BovedasAlCorte, ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        var movimiento = await _e.Db.VaultMovements.SingleAsync();
        movimiento.Kind.Should().Be(VaultMovementKind.OpeningBalance);
        movimiento.Amount.Should().Be(700_000m);
        (await _e.Db.VaultMovementDenominations.CountAsync()).Should().Be(2);
        (await _e.Db.VaultDenominationBalances.SumAsync(s => s.Amount)).Should().Be(700_000m);
        (await _e.Db.IntegrationMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Puntos_y_cajas_crea_el_punto_y_sus_cajas_y_rechaza_la_caja_repetida()
    {
        Archivo([P.Oficina, P.Punto, P.NombrePunto, P.Caja, P.NombreCaja, P.Base],
            ["Oficina A", "PTO01", "Principal", "CAJA01", "Caja 1", "500000"],
            ["Oficina A", "PTO01", "Principal", "CAJA02", "Caja 2", "500000"]);

        var r = await ImportarAsync(P.PuntosYCajas, ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        var punto = await _e.Db.CollectionPoints.SingleAsync(p => p.Code == "PTO01");
        (await _e.Db.CashBoxes.Where(c => c.CollectionPointId == punto.Id).CountAsync()).Should().Be(2, "las cajas de un punto nuevo entran tras el primer guardado");

        Archivo([P.Oficina, P.Punto, P.NombrePunto, P.Caja, P.NombreCaja],
            ["Oficina A", "PTO01", "Principal", "CAJA01", "Caja 1"]);
        (await ImportarAsync(P.PuntosYCajas, ModoDeImportacion.Apply)).IsFailure.Should().BeTrue("la caja ya existe");
    }

    [Fact]
    public async Task El_cajero_sin_segundo_factor_avisa_y_queda_asignado()
    {
        var punto = _e.Punto("PTO01");
        _e.Caja(punto, "CAJA01");
        Archivo([P.Documento, P.Rol, P.Caja], ["1001", "Cajero", "CAJA01"]);

        var r = await ImportarAsync(P.CajerosYAlcances, ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Warnings.Should().Contain(w => w.Code == ImportReceiptsGoLiveTemplateCommandHandler.CodigoSinSegundoFactor);
        (await _e.Db.CashBoxCashiers.CountAsync(c => c.UserId == _e.Cajera.Id)).Should().Be(1);
        (await _e.Db.UserCollectionPointScopes.CountAsync(s => s.UserId == _e.Cajera.Id && s.CollectionPointId == punto.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Las_cifras_de_referencia_reemplazan_la_carga_anterior_del_mismo_corte()
    {
        Archivo([P.FechaDeCorte, P.Concepto, P.Dimension, P.Valor], ["2026-11-30", "Boveda", "A", "700000"]);
        (await ImportarAsync(P.CifrasDeReferencia, ModoDeImportacion.Apply)).IsSuccess.Should().BeTrue();
        Archivo([P.FechaDeCorte, P.Concepto, P.Dimension, P.Valor], ["2026-11-30", "Boveda", "A", "720000"]);
        (await ImportarAsync(P.CifrasDeReferencia, ModoDeImportacion.Apply)).IsSuccess.Should().BeTrue();

        var vigentes = await _e.Db.ReceiptsReferenceFigures.ToListAsync();
        vigentes.Should().ContainSingle().Which.Amount.Should().Be(720_000m);
    }
}
