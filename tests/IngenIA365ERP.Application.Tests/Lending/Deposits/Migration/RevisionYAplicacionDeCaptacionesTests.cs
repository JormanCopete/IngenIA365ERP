using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Migration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Columnas;
using P = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Migration;

/// <summary>
/// Feature 014, US9 (T157; plantillas-y-archivos.md §1.1, §4; api.md §16): la carga de migración de aportes y captaciones —crear por naturaleza, importar
/// cada parte con revisión previa todo o nada y errores por fila y columna, aplicar con la validación entre partes, la carga reemplazable, el cuadre contra
/// Contabilidad y las cifras de referencia—. (nuevo)
/// </summary>
public sealed class RevisionYAplicacionDeCaptacionesTests : IAsyncLifetime
{
    private MigracionDeCaptacionesDePrueba _m = null!;

    public async Task InitializeAsync() => _m = await MigracionDeCaptacionesDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _m.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Un_titular_inexistente_lista_fila_columna_y_motivo_y_no_guarda_nada_US9_1()
    {
        var carga = await _m.CargaAsync(LineNature.Deposit);
        var filas = Enumerable.Range(1, 40).Select(i => MigracionDeCaptacionesDePrueba.Ahorro($"AH-{i:000}")).ToArray();
        filas[35] = MigracionDeCaptacionesDePrueba.Ahorro("AH-036", titular: "99999999");
        _m.Archivo(P.MemberAccounts, filas);

        var revisada = await _m.ImportarAsync(carga, P.MemberAccounts, ModoDeImportacion.Review);
        var aplicada = await _m.ImportarAsync(carga, P.MemberAccounts);

        MigracionDeCaptacionesDePrueba.Errores(revisada).Should().ContainSingle(e => e.Row == 37 && e.Column == C.Titular && e.Code == ImportErrors.CellNotFound);
        aplicada.Error.Code.Should().Be(ImportErrors.InvalidCode);
        MigracionDeCaptacionesDePrueba.Errores(aplicada).Should().ContainSingle(e => e.Row == 37 && e.Column == C.Titular);
        (await _m.Db.MigrationLoadParts.AnyAsync()).Should().BeFalse("todo o nada: no se guarda ninguna cuenta");
        (await _m.Db.MigrationAccountRows.AnyAsync()).Should().BeFalse();

        filas[35] = MigracionDeCaptacionesDePrueba.Ahorro("AH-036");
        _m.Archivo(P.MemberAccounts, filas);
        var corregida = await _m.ImportarAsync(carga, P.MemberAccounts);
        corregida.IsSuccess.Should().BeTrue();
        (await _m.Db.MigrationAccountRows.CountAsync()).Should().Be(40);
        var fila = await _m.Db.MigrationAccountRows.AsNoTracking().SingleAsync(r => r.SourceAccountNumber == "AH-001");
        fila.PersonId.Should().Be(_m.Cu.Ana.Id);
        fila.DepositClass.Should().Be(DepositClass.Demand);
        fila.Principal.Should().Be(1_000_000m);
    }

    [Fact]
    public async Task Una_parte_de_otra_naturaleza_o_que_llega_despues_no_se_importa()
    {
        var aportes = await _m.CargaAsync(LineNature.Contribution);
        _m.Archivo(P.MemberAccounts, MigracionDeCaptacionesDePrueba.Ahorro("AH-1"));
        (await _m.ImportarAsync(aportes, P.MemberAccounts)).Error.Code.Should().Be(ErroresDeMigracionDeCaptaciones.CodigoParteDeOtraNaturaleza);

        var captaciones = await _m.CargaAsync(LineNature.Deposit);
        (await _m.ImportarAsync(captaciones, P.HoldsAndGarnishments)).Error.Code.Should().Be(ErroresDeMigracionDeCaptaciones.CodigoParteNoDisponible);

        _m.Entrega = EntregaDeCartera.K7;
        (await _m.ImportarAsync(captaciones, P.MemberAccounts)).Error.Code.Should().Be(ErroresDeMigracionDeCaptaciones.CodigoParteNoDisponible,
            "las cuentas de ahorro llegan con K8");
        // US6: el CDAT nace con su entrega (K9); con K8 desplegada todavía no se importa (MigracionDeCdatTests prueba la parte).
        _m.Entrega = EntregaDeCartera.K8;
        (await _m.ImportarAsync(captaciones, P.TermDeposits)).Error.Code.Should().Be(ErroresDeMigracionDeCaptaciones.CodigoParteNoDisponible,
            "el CDAT llega con K9");
    }

    [Fact]
    public async Task La_linea_se_resuelve_por_el_codigo_heredado_con_su_equivalencia_y_el_contractual_espera_su_entrega()
    {
        var carga = await _m.CargaAsync(LineNature.Deposit);
        _m.Archivo(P.MemberAccounts, MigracionDeCaptacionesDePrueba.Ahorro("AH-1", heredado: "AH01"));
        MigracionDeCaptacionesDePrueba.Errores(await _m.ImportarAsync(carga, P.MemberAccounts)).Should()
            .ContainSingle(e => e.Column == C.CodigoHeredado && e.Code == ErroresDeMigracionDeCaptaciones.CodigoSinEquivalencia);

        await _m.CargarAsync(carga, P.LineEquivalences, MigracionDeCaptacionesDePrueba.Fila((C.CodigoHeredado, "ah01"), (C.Linea, "AHVISTA"), (C.Clase, "Demand")));
        await _m.CargarAsync(carga, P.MemberAccounts, MigracionDeCaptacionesDePrueba.Ahorro("AH-1", heredado: "AH01"));
        var fila = await _m.Db.MigrationAccountRows.AsNoTracking().SingleAsync();
        fila.CreditLineCode.Should().Be("AHVISTA");
        fila.SourceProductCode.Should().Be("AH01");

        _m.Archivo(P.MemberAccounts, MigracionDeCaptacionesDePrueba.Ahorro("AH-2", clase: "Contractual"));
        MigracionDeCaptacionesDePrueba.Errores(await _m.ImportarAsync(carga, P.MemberAccounts)).Should()
            .Contain(e => e.Column == C.Clase && e.Code == ImportErrors.CellNotYetAvailable);
    }

    [Fact]
    public async Task Los_aportes_exigen_asociado_vigente_y_un_numero_que_no_tenga_otra_cuenta()
    {
        var carga = await _m.CargaAsync(LineNature.Contribution);
        _m.Archivo(P.ContributionAccounts,
            MigracionDeCaptacionesDePrueba.Aporte("1001", "AP-1"),
            MigracionDeCaptacionesDePrueba.Aporte("1005", "AP-2"),
            MigracionDeCaptacionesDePrueba.Aporte("1002", "AP-1"));

        var r = await _m.ImportarAsync(carga, P.ContributionAccounts);

        MigracionDeCaptacionesDePrueba.Errores(r).Select(e => (e.Row, e.Column, e.Code)).Should().BeEquivalentTo(new[]
        {
            (3, C.Asociado, "Lending.Savings.MembersOnly"),
            (4, C.Numero, ImportErrors.RowDuplicate),
        });
    }

    [Fact]
    public async Task Aplicar_valida_entre_partes_todo_o_nada_con_parte_fila_y_columna()
    {
        var carga = await _m.CargaAsync(LineNature.Deposit);
        await _m.CargarAsync(carga, P.MemberAccounts, MigracionDeCaptacionesDePrueba.Ahorro("AH-1"), MigracionDeCaptacionesDePrueba.Ahorro("AH-2", titular: "1003"));
        await _m.CargarAsync(carga, P.MemberAccountParties,
            MigracionDeCaptacionesDePrueba.Parte("AH-1", "1003", "Beneficiary", beneficiario: "60"),
            MigracionDeCaptacionesDePrueba.Parte("AH-9", "1002", "CoHolder"));

        var r = await _m.AplicarAsync(carga);

        r.Error.Code.Should().Be(ErroresDeMigracion.CodigoErroresEntrePartes);
        var errores = MigracionDeCaptacionesDePrueba.ErroresEntrePartes(r.Error);
        errores.Should().Contain(e => e.Part == P.MemberAccountParties && e.Row == 3 && e.Code == ErroresDeMigracionDeCaptaciones.CodigoCuentaNoEnLaCarga);
        errores.Should().Contain(e => e.Part == P.MemberAccountParties && e.Row == 2 && e.Code == "Lending.Savings.BeneficiariesMustSum100");
        errores.Should().Contain(e => e.Part == P.MemberAccounts && e.Row == 3 && e.Code == ErroresDeMigracionDeCaptaciones.CodigoMenorSinRepresentante,
            "Carla nació en 2010: un menor titular lleva su representante legal");
        (await _m.RecargarAsync(carga)).Status.Should().Be(MigrationLoadStatus.Draft);
    }

    [Fact]
    public async Task Aplicar_deja_la_carga_aplicada_con_plazo_totales_cuadre_y_su_informativo()
    {
        _m.SaldoContable = rol => rol == "CapitalDeCaptacion" ? 2_999_000m : 0m;
        var carga = await _m.CargaAsync(LineNature.Deposit);
        await _m.CargarAsync(carga, P.MemberAccounts,
            MigracionDeCaptacionesDePrueba.Ahorro("AH-1", capital: "1000000"),
            MigracionDeCaptacionesDePrueba.Ahorro("AH-2", titular: "1002", capital: "2000000", interes: "1234,567891", liquidadoHasta: "2026-10-31"));

        var r = await _m.AplicarAsync(carga);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(MigrationLoadStatus.Applied);
        r.Value.ActivationDeadline.Should().Be(MigracionDeCaptacionesDePrueba.Corte.AddDays(5), "corte + Captaciones.DiasMaximosFechaValor (5)");
        r.Value.Accounts.Should().Be(2);
        r.Value.Totals.Should().BeEquivalentTo(new[]
        {
            new TotalDeCargaDto("AHVISTA", "Principal", 3_000_000m, 2),
            new TotalDeCargaDto("AHVISTA", "InterestPayable", 1234.567891m, 1),
        });
        r.Value.BalanceCheck.IsBalanced.Should().BeFalse("el capital difiere en 1.000 y el interés por pagar no está en Contabilidad");
        var diferencias = await _m.Db.MigrationLoadDifferences.AsNoTracking().Where(d => d.MigrationLoadId == carga.Id).ToListAsync();
        diferencias.Should().Contain(d => d.ConceptFamily == "CapitalDeCaptacion" && d.Difference == 1_000m);
        (await _m.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CargaDeCaptacionesAplicadaV1.Type)).PayloadJson
            .Should().Contain("\"nature\":\"Deposit\"");
    }

    [Fact]
    public async Task Una_carga_nueva_aplicada_reemplaza_la_anterior_sin_borrarla_US9_3()
    {
        var primera = await _m.CargaAsync(LineNature.Contribution);
        await _m.CargarAsync(primera, P.ContributionAccounts, MigracionDeCaptacionesDePrueba.Aporte("1001", "AP-1"));
        (await _m.AplicarAsync(primera)).IsSuccess.Should().BeTrue();

        var segunda = await _m.CargaAsync(LineNature.Contribution, MigracionDeCaptacionesDePrueba.Corte.AddDays(-1));
        await _m.CargarAsync(segunda, P.ContributionAccounts, MigracionDeCaptacionesDePrueba.Aporte("1001", "AP-1", ordinarios: "2100000"));
        var r = await _m.AplicarAsync(segunda);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.ReplacedLoadPublicId.Should().Be(primera.PublicId);
        (await _m.RecargarAsync(primera)).Status.Should().Be(MigrationLoadStatus.Replaced, "queda como ensayo");
        (await _m.Db.MigrationContributionAccountRows.CountAsync()).Should().Be(2, "nada se borra");

        var captaciones = await _m.CargaAsync(LineNature.Deposit);
        (await _m.RecargarAsync(segunda)).Status.Should().Be(MigrationLoadStatus.Applied, "cada naturaleza tiene su carga aplicada");
        captaciones.Status.Should().Be(MigrationLoadStatus.Draft);
    }

    [Fact]
    public async Task Las_cifras_de_referencia_se_cotejan_por_linea_y_concepto_sin_nombrar_al_origen()
    {
        var carga = await _m.CargaAsync(LineNature.Contribution);
        await _m.CargarAsync(carga, P.ContributionAccounts, MigracionDeCaptacionesDePrueba.Aporte("1001", "AP-1"), MigracionDeCaptacionesDePrueba.Aporte("1002", "AP-2"));

        var invalidas = await new SetDepositReferenceFiguresCommandHandler(_m.Db).Handle(new SetDepositReferenceFiguresCommand(carga.PublicId,
            [new CifraDeReferenciaDto("AHVISTA", "Principal", 1m, null)]) { OperationKey = Guid.NewGuid() }, default);
        invalidas.Error.Code.Should().Be(CifrasDeReferencia.CodigoInvalidas, "una línea y un concepto que no son de aportes");

        var r = await new SetDepositReferenceFiguresCommandHandler(_m.Db).Handle(new SetDepositReferenceFiguresCommand(carga.PublicId,
        [
            new CifraDeReferenciaDto("aportes", "ContributionOrdinary", 4_000_000m, "Balance de prueba de noviembre"),
            new CifraDeReferenciaDto("APORTES", "ContributionMinimum", 900_000m, "Balance de prueba de noviembre"),
        ]) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Comparison.Should().BeEquivalentTo(new[]
        {
            new CotejoDeCifraDto("APORTES", "ContributionMinimum", 1_000_000m, 2, 900_000m, 100_000m, "Balance de prueba de noviembre"),
            new CotejoDeCifraDto("APORTES", "ContributionOrdinary", 4_000_000m, 2, 4_000_000m, 0m, "Balance de prueba de noviembre"),
        });
        r.Value.TotalLoaded.Should().Be(5_000_000m);
    }

    [Fact]
    public async Task Las_cargas_de_creditos_y_las_de_captaciones_no_se_ven_entre_si()
    {
        var captaciones = await _m.CargaAsync(LineNature.Deposit);
        (await new GetMigrationLoadQueryHandler(_m.Db).Handle(new GetMigrationLoadQuery(captaciones.PublicId), default)).Error.Code
            .Should().Be(ErroresDeActivacion.CodigoCargaNoEncontrada);
        (await new ListMigrationLoadsQueryHandler(_m.Db).Handle(new ListMigrationLoadsQuery(), default)).Value.Should().BeEmpty();

        var creditos = new Domain.Entities.Lending.Migration.MigrationLoad
        {
            Number = 99, CutoffDate = MigracionDeCaptacionesDePrueba.Corte, Status = MigrationLoadStatus.Draft, CreatedByUserId = _m.Jefe.Id,
        };
        _m.Db.MigrationLoads.Add(creditos);
        await _m.Db.SaveChangesAsync();
        (await new GetDepositMigrationLoadQueryHandler(_m.Db, _m.Requisitos()).Handle(new GetDepositMigrationLoadQuery(creditos.PublicId), default)).Error.Code
            .Should().Be(ErroresDeActivacion.CodigoCargaNoEncontrada);
        (await new ListDepositMigrationLoadsQueryHandler(_m.Db, _m.Requisitos()).Handle(new ListDepositMigrationLoadsQuery(), default)).Value
            .Should().ContainSingle(l => l.PublicId == captaciones.PublicId);
    }
}
