using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 013, T0620 (T0643; contracts/contabilidad.md §3.7; T91): la completitud de la matriz de Cartera. Las combinaciones en uso (por el
/// doble de <c>IDimensionesDeCartera</c>) y sus roles sin regla vigente; las clases de recaudador registradas sin <c>RecaudoPorAplicar</c> y
/// <c>OwnLoanPayoff</c> pedida siempre; las reglas cuya cuenta dejó de ser elegible; los hechos sin mapeo o mapeados a un tipo que ya no sirve; y
/// los avisos: puentes que exigen cruce, cuentas de la causación resumida que exigen tercero o cruce y naturaleza distinta.
/// </summary>
public class GetLendingRulesCompletenessQueryTests
{
    private static readonly DateOnly Fecha = new(2026, 11, 30);
    private readonly EscenarioContableDeCartera E = new();
    private readonly IRegistroDeIntegracion _registro = Substitute.For<IRegistroDeIntegracion>();

    public GetLendingRulesCompletenessQueryTests() => Recaudadores();

    private void Recaudadores(params (string Codigo, ClaseDeRecaudador Clase)[] recaudadores) =>
        _registro.QueCuentanAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ParticipanteVigente>>(recaudadores
            .Select(r => new ParticipanteVigente(Guid.NewGuid(), r.Codigo, (IntegrationParticipantClass)(int)r.Clase, IntegrationParticipantRole.ObligationsCollector,
                IntegrationDestinations.Lending, false, new DateOnly(2026, 1, 1), null, []))
            .ToList()));

    private async Task<CompletitudDeLaMatrizDeCarteraDto> CompletitudAsync()
    {
        var r = await new GetLendingRulesCompletenessQueryHandler(E.D.Db, E.Dimensiones, _registro, new TiposDeComprobanteDeCartera(E.D.Db))
            .Handle(new GetLendingRulesCompletenessQuery(Fecha), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        return r.Value;
    }

    private static CombinacionDeCarteraEnUsoDto Combinacion(string rol, string categoria, string? concepto = null, string linea = "LIB01") =>
        new(rol, linea, "CONSUMO", "ConLibranza", categoria, concepto, false, false, false, Guid.Parse("b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f"), Fecha);

    [Fact]
    public async Task Las_combinaciones_en_uso_sin_regla_salen_una_vez_con_sus_lineas()
    {
        E.Dimensiones.CombinacionesEnUsoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<CombinacionDeCarteraEnUsoDto>>(
        [
            Combinacion(R.Capital, "A"),
            Combinacion(R.Capital, "B"),
            Combinacion(R.Capital, "B", linea: "LIB02"),
            Combinacion(R.InteresPorCobrar, "A", R.CurrentInterest),
        ]));

        var c = await CompletitudAsync();

        var faltante = c.MissingRules.Should().ContainSingle().Subject;
        faltante.Role.Should().Be(R.Capital);
        faltante.Dimensions.RiskCategory.Should().Be("B");
        faltante.CreditLines.Should().Equal("LIB01", "LIB02");
        c.Summary.PorTipo["missingRules"].Should().Be(1);
    }

    [Fact]
    public async Task Una_clase_de_recaudador_registrada_sin_puente_se_pide_y_la_recogida_de_creditos_propios_siempre()
    {
        Recaudadores(("CAJA", ClaseDeRecaudador.Receipts), ("APORTES", ClaseDeRecaudador.Contributions));
        var propia = await E.D.Db.LendingPostingRules.SingleAsync(r => r.Role == R.RecaudoPorAplicar && r.CollectorClass == "OwnLoanPayoff");
        E.D.Db.LendingPostingRules.Remove(propia);
        await E.D.Db.SaveChangesAsync();

        var c = await CompletitudAsync();

        c.CollectorsWithoutBridge.Select(x => x.CollectorClass).Should().Equal("Contributions", "OwnLoanPayoff");
        c.CollectorsWithoutBridge.Single(x => x.CollectorClass == "OwnLoanPayoff").Collectors.Should().Equal(["CAR"], "es la propia Cartera (T91)");
    }

    [Fact]
    public async Task Una_regla_cuya_cuenta_dejo_de_servir_se_informa()
    {
        E.Cuenta("11059502").IsActive = false;
        await E.D.Db.SaveChangesAsync();

        var c = await CompletitudAsync();

        c.IneligibleRules.Should().ContainSingle().Which.AccountCode.Should().Be("11059502");
    }

    [Fact]
    public async Task Un_hecho_sin_mapeo_o_con_un_tipo_que_ya_no_sirve_se_informa()
    {
        var mapeo = await E.D.Db.LendingVoucherMappings.SingleAsync(m => m.MappingKey == "CreditoDesembolsado|*");
        E.D.Db.LendingVoucherMappings.Remove(mapeo);
        var cn = await E.D.Db.VoucherTypes.SingleAsync(v => v.Code == "CN");
        cn.IsActive = false;
        await E.D.Db.SaveChangesAsync();

        var c = await CompletitudAsync();

        c.UnmappedOperations.Should().Contain(x => x.Operation == "CreditoDesembolsado" && x.Code == "Accounting.VoucherType.NotFound");
        c.UnmappedOperations.Should().Contain(x => x.Operation == "DimensionContableCambiada" && x.Code == "Accounting.VoucherType.NotAllowedForModule");
    }

    [Fact]
    public async Task Avisa_los_puentes_con_cruce_la_causacion_por_credito_y_la_naturaleza_distinta()
    {
        E.Cuenta("24959501").RequiresCrossDocument = true;
        await E.D.Db.SaveChangesAsync();

        var c = await CompletitudAsync();

        c.Warnings.Should().Contain(w => w.Code == GetLendingRulesCompletenessQueryHandler.AvisoPuenteConCruce && w.AccountCode == "24959501");
        c.Warnings.Should().Contain(w => w.Code == GetLendingRulesCompletenessQueryHandler.AvisoCausacionPorCredito && w.AccountCode == "14430501",
            "la cuenta de interés exige tercero y cruce: la causación resumida lleva una línea por crédito (C5)");
        c.Warnings.Should().Contain(w => w.Code == ReglasDeLaMatrizDeCartera.AvisoNaturalezaDistinta && w.AccountCode == "25059501",
            "el puente de nómina es de naturaleza crédito y el rol de débito");
    }
}
