using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Partes;
using C = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Columnas;

namespace IngenIA365ERP.Application.Tests.Lending.Migration;

/// <summary>
/// Feature 013, US8, T0720 (plantillas.md §5.4; data-model §29; T83): aplicar la carga. Las partes obligatorias vacías (<c>PartMissing</c>; la carga
/// sin ninguna parte sigue valiendo, C107, ya probada en <c>ActivacionSinCarteraPreviaTests</c>), las validaciones entre partes con 422
/// <c>CrossPartErrors</c> y nada guardado; con todo, la carga <c>Applied</c> con su plazo, conteos, totales y resumen (y lo registrado a mano que
/// prevalece), la anterior <c>Replaced</c>, el cuadre <c>BalanceCheck</c> por dimensión contra el doble de Contabilidad con su huella, y
/// <c>CargaDeCarteraAplicada</c> en el mismo guardado; Cartera activada, <c>AlreadyActivated</c>. (nuevo)
/// </summary>
public sealed class ApplyMigrationLoadCommandHandlerTests : IAsyncLifetime
{
    private MigracionDePrueba _m = null!;

    public async Task InitializeAsync() => _m = await MigracionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _m.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Una_parte_obligatoria_vacia_responde_PartMissing_con_las_partes()
    {
        await _m.CargarAsync(P.Loans, MigracionDePrueba.Credito("CR-1"));

        var r = await _m.AplicarAsync();

        r.Error.Code.Should().Be(ErroresDeMigracion.CodigoParteFaltante);
        GobiernoDeDatos(r.Error).GetProperty("parts").EnumerateArray().Select(p => p.GetString()).Should().Equal(P.Installments, P.Balances, P.RiskData);
        (await _m.Db.MigrationLoads.AsNoTracking().SingleAsync()).Status.Should().Be(MigrationLoadStatus.Draft);
    }

    [Fact]
    public async Task Las_validaciones_entre_partes_responden_CrossPartErrors_con_parte_tanda_fila_y_columna_y_no_guardan_nada()
    {
        await _m.CreditoCompletoAsync("CR-1");
        await _m.CargarAsync(P.Loans, MigracionDePrueba.Credito("CR-2", "79000002", factura: "FE-7"));
        await _m.CargarAsync(P.Installments, MigracionDePrueba.Cuota("CR-9", 1, "2026-12-15", "1000"));
        await _m.CargarAsync(P.ChargedOff, MigracionDePrueba.Fila((C.Numero, "CR-1"), (C.Deudor, "52000001"), (C.Oficina, "NORTE"), (C.Linea, "CONLIB"),
            (C.FechaDeCastigo, "2025-12-31"), (C.CapitalCastigado, "500000"), (C.SoporteDelCastigo, "Acta 12")));
        await _m.CargarAsync(P.CreditLimits,
            MigracionDePrueba.Fila((C.Persona, "41000003"), (C.Tipo, "Consolidated"), (C.Valor, "1000000"), (C.VigenteDesde, "2026-01-01"), (C.Motivo, "Cupo")),
            MigracionDePrueba.Fila((C.Persona, "41000003"), (C.Tipo, "Consolidated"), (C.Valor, "2000000"), (C.VigenteDesde, "2026-06-01"), (C.Motivo, "Cupo")));
        var carla = await _m.Db.People.SingleAsync(p => p.Id == _m.Carla.Id);
        carla.IsDeleted = true;
        await _m.Db.SaveChangesAsync();
        _m.Db.ChangeTracker.Clear();

        var r = await _m.AplicarAsync();

        r.Error.Code.Should().Be(ErroresDeMigracion.CodigoErroresEntrePartes);
        var errores = MigracionDePrueba.Errores2(r.Error);
        errores.Should().Contain(e => e.Part == P.Installments && e.Batch == 2 && e.Row == 2 && e.Column == C.NumeroDeCredito, "CR-9 no está en loans");
        errores.Should().Contain(e => e.Part == P.Loans && e.Batch == 2 && e.Row == 2 && e.Message.Contains("cuotas"), "CR-2 sin cuotas");
        errores.Should().Contain(e => e.Part == P.Loans && e.Batch == 2 && e.Message.Contains("mora, calificación"), "CR-2 sin riesgo");
        errores.Should().Contain(e => e.Part == P.ChargedOff && e.Column == C.Numero, "CR-1 en loans y charged-off a la vez");
        errores.Should().Contain(e => e.Part == P.Loans && e.Column == C.Factura, "CR-2 nació de una venta que sales no trae como migrada");
        errores.Should().Contain(e => e.Part == P.CreditLimits && e.Row == 3 && e.Column == C.VigenteDesde, "cupos que se cruzan");
        errores.Should().Contain(e => e.Part == P.CreditLimits && e.Column == C.Persona && e.Message.Contains("baja"), "persona dada de baja después de importar");

        var carga = await _m.Db.MigrationLoads.AsNoTracking().SingleAsync();
        carga.Status.Should().Be(MigrationLoadStatus.Draft);
        (await _m.Db.MigrationLoadDifferences.AnyAsync()).Should().BeFalse();
        (await _m.Db.IntegrationMessages.AnyAsync()).Should().BeFalse("todo o nada");
    }

    [Fact]
    public async Task Aplicar_deja_la_carga_aplicada_con_su_cuadre_y_emite_CargaDeCarteraAplicada()
    {
        await _m.CreditoCompletoAsync("CR-1");
        _m.SaldoContable = (rol, concepto) => rol == "Capital" ? 1_000_000m : 0m; // el interés por cobrar no cuadra: 10.000 de diferencia

        var r = await _m.AplicarAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var carga = await _m.Db.MigrationLoads.AsNoTracking().SingleAsync();
        carga.Should().BeEquivalentTo(new
        {
            Status = MigrationLoadStatus.Applied, AppliedByUserId = (int?)_m.Jefa.Id, ActivationDeadline = (DateOnly?)MigracionDePrueba.Corte.AddDays(5),
            LoanCount = 1, ChargedOffCount = 0, PersonCount = 1, CurrentCheckVersion = 1, IsBalanced = false, SalesReconciled = true,
        }, o => o.ExcludingMissingMembers());
        carga.AppliedAt.Should().NotBeNull();
        JsonDocument.Parse(carga.TotalsJson!).RootElement.GetProperty("Principal").GetDecimal().Should().Be(1_000_000m);
        JsonDocument.Parse(carga.ValidationSummaryJson!).RootElement.GetProperty("manualPrevails").GetArrayLength().Should().Be(0);
        carga.BalanceCheckSha256.Should().HaveLength(64).And.NotBe(ApplyMigrationLoadCommandHandler.HuellaVacia);

        var diferencia = await _m.Db.MigrationLoadDifferences.AsNoTracking().SingleAsync();
        diferencia.Should().BeEquivalentTo(new
        {
            MigrationLoadId = carga.Id, CheckVersion = 1, Kind = MigrationDifferenceKind.BalanceCheck, ConceptFamily = "CurrentInterest", ModalityCode = "CONSUMO",
            RiskCategory = (RatingCategory?)RatingCategory.A, LendingAmount = 10_000m, AccountingAmount = 0m, Difference = 10_000m,
        }, o => o.ExcludingMissingMembers());
        diferencia.DimensionKey.Should().StartWith("InteresPorCobrar|");
        diferencia.AccountSetDisplay.Should().Contain("14430501", "Cartera muestra el conjunto que devolvió Contabilidad");
        r.Value.BalanceCheck.Should().Be(new BalanceCheckSummaryDto(1, false, 1));
        _m.Pedidas.Should().ContainSingle().Which.Should().Contain(c => c.Role == "Capital" && c.ModalityCode == "CONSUMO" && c.RiskCategory == "A"
            && c.BranchPublicId == _m.Norte.PublicId);

        var mensaje = await _m.Db.IntegrationMessages.AsNoTracking().SingleAsync();
        mensaje.Type.Should().Be(CargaDeCarteraAplicadaV1.Type);
        var contenido = JsonDocument.Parse(mensaje.PayloadJson).RootElement;
        (contenido.GetProperty("loanCount").GetInt32(), contenido.GetProperty("principalTotal").GetDecimal(), contenido.GetProperty("differencesCount").GetInt32())
            .Should().Be((1, 1_000_000m, 1));
    }

    [Fact]
    public async Task Una_carga_aplicada_nueva_deja_la_anterior_reemplazada_con_el_mismo_corte()
    {
        await _m.CreditoCompletoAsync("CR-1");
        (await _m.AplicarAsync()).IsSuccess.Should().BeTrue();
        var primera = await _m.Db.MigrationLoads.AsNoTracking().SingleAsync();
        var segunda = new MigrationLoad { Number = 2, CutoffDate = MigracionDePrueba.Corte, Status = MigrationLoadStatus.Draft, CreatedByUserId = _m.Jefa.Id };
        _m.Db.MigrationLoads.Add(segunda);
        await _m.Db.SaveChangesAsync();
        _m.Db.ChangeTracker.Clear();

        var r = await _m.AplicarAsync(segunda);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.ReplacedLoadPublicId.Should().Be(primera.PublicId);
        var anterior = await _m.Db.MigrationLoads.AsNoTracking().SingleAsync(l => l.Id == primera.Id);
        (anterior.Status, anterior.ReplacedByLoadId).Should().Be((MigrationLoadStatus.Replaced, (int?)segunda.Id));
        var mensajes = await _m.Db.IntegrationMessages.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
        JsonDocument.Parse(mensajes[^1].PayloadJson).RootElement.GetProperty("replacesLoadPublicId").GetGuid().Should().Be(primera.PublicId);
    }

    [Fact]
    public async Task Lo_registrado_a_mano_antes_prevalece_y_se_informa()
    {
        await _m.CreditoCompletoAsync("CR-1");
        _m.Db.CreditLimits.Add(new CreditLimit
        {
            PersonId = _m.Ana.Id, Kind = CreditLimitKind.Consolidated, Amount = 3_000_000m, Origin = CreditLimitOrigin.Assigned, ValidFrom = new DateOnly(2026, 11, 20),
            VersionNumber = 1, RegisteredAt = _m.G.UtcNow, Reason = "Cupo del comité",
        });
        _m.Db.RelatedParties.Add(new RelatedParty { PersonId = _m.Ana.Id, Basis = RelatedPartyBasis.BoardMember, ValidFrom = new DateOnly(2026, 1, 1) });
        await _m.Db.SaveChangesAsync();
        _m.Db.ChangeTracker.Clear();
        await _m.CargarAsync(P.CreditLimits,
            MigracionDePrueba.Fila((C.Persona, "52000001"), (C.Tipo, "Consolidated"), (C.Valor, "1000000"), (C.VigenteDesde, "2026-01-01"), (C.Motivo, "Cupo")));
        await _m.CargarAsync(P.RelatedParties,
            MigracionDePrueba.Fila(("persona", "52000001"), ("fundamento", "BoardMember"), ("vigenteDesde", "2026-01-01"), ("soporte", "Acta 3")));

        var r = await _m.AplicarAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.ManualPrevails.Should().BeEquivalentTo(new[]
        {
            new { PersonPublicId = _m.Ana.PublicId, Concept = "CreditLimit" },
            new { PersonPublicId = _m.Ana.PublicId, Concept = "RelatedParty" },
        }, o => o.ExcludingMissingMembers());
        var resumen = JsonDocument.Parse((await _m.Db.MigrationLoads.AsNoTracking().SingleAsync()).ValidationSummaryJson!).RootElement;
        resumen.GetProperty("manualPrevails").GetArrayLength().Should().Be(2);

        // El detalle de la carga (T0739): el resumen por parte y tanda, las diferencias y lo que prevalece.
        var detalle = (await new GetMigrationLoadQueryHandler(_m.Db).Handle(new GetMigrationLoadQuery(_m.Carga.PublicId), default)).Value;
        detalle.ManualPrevails.Should().HaveCount(2);
        detalle.Parts.Should().Contain(p => p.Part == P.Loans && p.Batch == 1 && p.Status == MigrationPartStatus.Imported && p.Rows == 1);
        detalle.Differences.Pending.Should().Be(2, "capital e interés no cuadran contra el doble de Contabilidad");
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_el_cuadre_no_se_puede_calcular()
    {
        await _m.CreditoCompletoAsync("CR-1");
        _m.ContabilidadIniciada = false;

        (await _m.AplicarAsync()).Error.Code.Should().Be(ErroresDeMigracion.CodigoSinContabilidad);
        (await _m.Db.MigrationLoads.AsNoTracking().SingleAsync()).Status.Should().Be(MigrationLoadStatus.Draft);
    }

    [Fact]
    public async Task Con_Cartera_activada_no_se_aplica()
    {
        var setup = await _m.Db.LendingSetups.SingleAsync();
        setup.ActivationDate = MigracionDePrueba.Hoy.AddDays(-10);
        await _m.Db.SaveChangesAsync();
        _m.Db.ChangeTracker.Clear();

        (await _m.AplicarAsync()).Error.Code.Should().Be(ErroresDeActivacion.CodigoYaActivada);
    }

    private static JsonElement GobiernoDeDatos(IngenIA365ERP.Application.Common.Models.Error e) => Sessions.GobiernoDePrueba.Datos(e);
}
