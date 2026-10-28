using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.PersonBankAccounts.Imports;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Imports;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using T = IngenIA365ERP.Application.Treasury.Imports.PlantillasDePuestaEnMarchaDeTesoreria;

namespace IngenIA365ERP.Application.Tests.Treasury.Imports;

/// <summary>
/// Feature 015, US13, T0283 (archivos-y-plantillas.md §5; FR-157, FR-210): las plantillas de Tesorería —cuentas bancarias con su vínculo contable por código (E-04),
/// el fondo de liquidez exclusivo y el saldo al corte; chequeras; cheques en tránsito sólo de una chequera registrada antes; remesas en tránsito— y la de cuentas
/// bancarias de personas, que entran <c>PendingApproval</c>. Ninguna emite hechos. Prueba escrita, sin correr.
/// </summary>
public class PlantillasDePuestaEnMarchaDeTesoreriaTests
{
    private readonly EscenarioDeRecaudo _e = new();
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly IContabilidadParaTesoreria _contabilidad = Substitute.For<IContabilidadParaTesoreria>();
    private readonly Bank _banco;

    public PlantillasDePuestaEnMarchaDeTesoreriaTests()
    {
        _permisos.EsMaestroGlobal.Returns(true);
        _banco = new Bank { Name = "Banco Uno", TransferCode = "1007" };
        _e.Db.Banks.Add(_banco);
        _e.Db.SaveChanges();
        _contabilidad.CuentasBancariasDelPlanAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns([new CuentaContableBancariaDto(Guid.NewGuid(), "11100501", "Banco Uno", null, null)]);
        _contabilidad.VincularCuentaBancariaAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
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
        var archivo = new ArchivoDeImportacion("carga.xlsx", [1, 2, 3]);
        var r = codigo == PlantillaDeCuentasBancariasDePersonas.Clave
            ? await new ImportPersonBankAccountsCommandHandler(_e.Db, ejecutor, _e.Actor).Handle(new ImportPersonBankAccountsCommand(modo, archivo), default)
            : await new ImportTreasuryGoLiveTemplateCommandHandler(_e.Db, ejecutor, _contabilidad, _e.Actor, _e.Reloj)
                .Handle(new ImportTreasuryGoLiveTemplateCommand(codigo, modo, archivo), default);
        _e.Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>
    /// Segunda ronda del 2026-10-27 (D-l, T0500): los parámetros TES se cargan por plantilla con las reglas del alta de una vigencia: clave del catálogo, valor admitido;
    /// la vigencia nueva cierra la anterior la víspera. Una clave que no es del módulo no se carga y nada se guarda.
    /// </summary>
    [Fact]
    public async Task La_plantilla_de_parametros_carga_vigencias_con_las_reglas_del_alta()
    {
        var P = IngenIA365ERP.Application.Common.Parameters.PlantillaDeParametrosDeModulo.Clave;
        var V = IngenIA365ERP.Application.Common.Parameters.PlantillaDeParametrosDeModulo.Valor;
        var D = IngenIA365ERP.Application.Common.Parameters.PlantillaDeParametrosDeModulo.VigenteDesde;
        var M = IngenIA365ERP.Application.Common.Parameters.PlantillaDeParametrosDeModulo.Motivo;
        Archivo([P, V, D, M], ["Cheques.DiasSinCobrar", "45", "2026-12-01", "Política de la cooperativa"], ["Caja.EgresosVarios", "true", "2026-12-01", "No es de TES"]);
        (await ImportarAsync(T.Parametros, ModoDeImportacion.Apply)).IsFailure.Should().BeTrue("la segunda clave no es de Tesorería: todo o nada");
        (await _e.Db.ParameterVersions.CountAsync(v => v.Key == "Cheques.DiasSinCobrar" && v.Value == "45")).Should().Be(0);

        Archivo([P, V, D, M], ["Cheques.DiasSinCobrar", "45", "2026-12-01", "Política de la cooperativa"]);
        var r = await ImportarAsync(T.Parametros, ModoDeImportacion.Apply);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code + r.Error.Message : null);
        (await _e.Db.ParameterVersions.SingleAsync(v => v.Key == "Cheques.DiasSinCobrar" && v.Value == "45")).ValidFrom.Should().Be(new DateOnly(2026, 12, 1));
    }

    private OwnBankAccount Cuenta(string codigo = "BCO01")
    {
        var c = new OwnBankAccount { Code = codigo, Name = codigo, BankId = _banco.Id, AccountNumber = "123" + codigo, BranchId = _e.OficinaA.Id, Purposes = BankAccountPurpose.Payments,
            AccountType = OwnBankAccountType.Checking, ValidFrom = EscenarioDeRecaudo.Hoy.AddMonths(-1) };
        _e.Db.OwnBankAccounts.Add(c);
        _e.Db.SaveChanges();
        return c;
    }

    [Fact]
    public async Task La_cuenta_bancaria_se_vincula_por_codigo_y_guarda_su_saldo_al_corte()
    {
        Archivo([T.Codigo, T.Nombre, T.Banco, T.Numero, T.Tipo, T.Oficina, T.Propositos, T.CuentaContable, T.SaldoAlCorte, T.FechaDeCorte],
            ["BCO01", "Banco uno pagos", "Banco Uno", "123456", "Checking", "Oficina A", "Recaudo,Pagos", "11100501", "8500000", "2026-11-30"]);

        var r = await ImportarAsync(T.CuentasBancarias, ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        var cuenta = await _e.Db.OwnBankAccounts.SingleAsync();
        cuenta.OpeningBalance.Should().Be(8_500_000m);
        cuenta.OpeningBalanceOn.Should().Be(new DateOnly(2026, 11, 30));
        cuenta.Purposes.Should().Be(BankAccountPurpose.Collection | BankAccountPurpose.Payments);
        await _contabilidad.Received(1).VincularCuentaBancariaAsync(cuenta.PublicId, Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_fondo_de_liquidez_va_solo()
    {
        Archivo([T.Codigo, T.Nombre, T.Banco, T.Numero, T.Tipo, T.Oficina, T.Propositos],
            ["BCO02", "Fondo", "Banco Uno", "999", "Savings", "Oficina A", "FondoDeLiquidez,Pagos"]);

        (await ImportarAsync(T.CuentasBancarias, ModoDeImportacion.Apply)).IsFailure.Should().BeTrue("el fondo de liquidez es exclusivo (FR-207)");
    }

    [Fact]
    public async Task El_cheque_en_transito_exige_una_chequera_registrada_antes()
    {
        Cuenta();
        Archivo([T.Cuenta, T.Numero, T.Beneficiario, T.Valor, T.Fecha], ["BCO01", "501", "1001", "350000", "2026-11-20"]);

        var sinChequera = await ImportarAsync(T.ChequesEnTransito, ModoDeImportacion.Apply);
        sinChequera.IsFailure.Should().BeTrue();

        Archivo([T.Cuenta, T.Desde, T.Hasta, T.Siguiente], ["BCO01", "500", "550", "502"]);
        (await ImportarAsync(T.Chequeras, ModoDeImportacion.Apply)).IsSuccess.Should().BeTrue();
        Archivo([T.Cuenta, T.Numero, T.Beneficiario, T.Valor, T.Fecha, T.Entregado], ["BCO01", "501", "1001", "350000", "2026-11-20", "S"]);
        var conChequera = await ImportarAsync(T.ChequesEnTransito, ModoDeImportacion.Apply);
        conChequera.IsSuccess.Should().BeTrue(conChequera.Error?.Message);
        var cheque = await _e.Db.IssuedChecks.SingleAsync();
        cheque.IsOpeningLoad.Should().BeTrue();
        cheque.Status.Should().Be(IssuedCheckState.Delivered);
        (await _e.Db.IntegrationMessages.CountAsync()).Should().Be(0, "sin hechos (FR-158)");
    }

    [Fact]
    public async Task La_remesa_en_transito_queda_marcada_de_la_puesta_en_marcha()
    {
        Cuenta();
        Archivo([T.Cuenta, T.Oficina, T.Valor, T.Fecha, T.Tipo], ["BCO01", "Oficina A", "1200000", "2026-11-30", "Remesa"]);

        var r = await ImportarAsync(T.RemesasEnTransito, ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        var remesa = await _e.Db.CashRemittances.SingleAsync();
        remesa.IsOpeningLoad.Should().BeTrue();
        remesa.Status.Should().Be(RemittanceState.InTransit);
    }

    [Fact]
    public async Task Las_cuentas_de_personas_entran_pendientes_de_aprobacion()
    {
        Archivo([PlantillaDeCuentasBancariasDePersonas.Documento, PlantillaDeCuentasBancariasDePersonas.Banco, PlantillaDeCuentasBancariasDePersonas.Tipo,
                PlantillaDeCuentasBancariasDePersonas.Numero, PlantillaDeCuentasBancariasDePersonas.Titular, PlantillaDeCuentasBancariasDePersonas.DocumentoTitular],
            ["1001", "Banco Uno", "Ahorros", "445566", "Carla Caja", "1001"]);

        var r = await ImportarAsync(PlantillaDeCuentasBancariasDePersonas.Clave, ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        var cuenta = await _e.Db.PersonBankAccounts.SingleAsync();
        cuenta.Status.Should().Be(PersonBankAccountState.PendingApproval, "las aprueba otra persona (FR-210)");
        cuenta.Origin.Should().Be(PersonBankAccountOrigin.Imported);
        r.Value.Warnings.Should().Contain(w => w.Code == ImportPersonBankAccountsCommandHandler.CodigoSinCertificacion);
    }
}
