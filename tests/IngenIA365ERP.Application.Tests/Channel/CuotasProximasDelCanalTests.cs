using FluentAssertions;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Enums.Channel;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Channel;

/// <summary>
/// Feature 021, US10 (T133; FR-012): las cuotas que vencen en la anticipación de la cooperativa, sólo para personas habilitadas y sólo de los conceptos
/// publicados, sumadas por producto.
/// </summary>
public sealed class CuotasProximasDelCanalTests
{
    private readonly CajaConContratos _k = new();
    private readonly IConsultaDeObligaciones _consulta = Substitute.For<IConsultaDeObligaciones>();
    private readonly DateOnly _dia = EscenarioDeRecaudo.Hoy.AddDays(3);

    public CuotasProximasDelCanalTests()
    {
        var db = _k.C.E.Db;
        db.ChannelEnablements.Add(new ChannelEnablement { PersonId = _k.Asociado.Id, Status = EnablementStatus.Enabled, EnabledAt = DateTime.UtcNow });
        db.ChannelPublishedConcepts.Add(new ChannelPublishedConcept { Kind = ConceptKind.Loan, LineCode = "CONSUMO" });
        db.SaveChanges();
        _consulta.CuotasDeNominaAsync(Arg.Any<ConsultaDeCuotasDeNomina>(), Arg.Any<CancellationToken>()).Returns(ci => new CuotasDeNominaDto(default,
        [
            new CuotaDeNominaDto(_k.Asociado.PublicId, _k.CreditoA, "CR-1", "CONSUMO", 4, _dia, 300_000m, 0m, 1),
            new CuotaDeNominaDto(_k.Asociado.PublicId, _k.CreditoA, "CR-1", "CONSUMO", 4, _dia, 32_500m, 0m, 2),
            new CuotaDeNominaDto(_k.Asociado.PublicId, _k.CreditoB, "CR-2", "VIVIENDA", 9, _dia, 900_000m, 0m, 3),
        ], []));
        _k.Cuentas.ListarExigiblesAsync(Arg.Any<SolicitudDeExigibles>(), Arg.Any<CancellationToken>())
            .Returns(new RespuestaDelContratoDto<ExigiblesDeCuentasDto>(ResultadoDelContrato.Applied, "OK", "", new ExigiblesDeCuentasDto(default, default, [], [])));
    }

    [Fact]
    public async Task Solo_lo_publicado_de_las_personas_habilitadas_sumado_por_producto()
    {
        var r = await new ListDueInstallmentsQueryHandler(_k.C.E.Db, _consulta, _k.Cuentas, _k.C.E.Reloj).Handle(new ListDueInstallmentsQuery(3), default);

        r.IsSuccess.Should().BeTrue();
        var cuota = r.Value.Should().ContainSingle("la línea VIVIENDA no está publicada").Subject;
        cuota.PersonPublicId.Should().Be(_k.Asociado.PublicId);
        cuota.ConceptKey.Should().Be(ClaveDeConcepto.DeCredito(_k.CreditoA));
        cuota.Amount.Should().Be(332_500m);
        cuota.DueDate.Should().Be(_dia);
        await _consulta.Received(1).CuotasDeNominaAsync(Arg.Is<ConsultaDeCuotasDeNomina>(c => c.PeriodStart == _dia && c.PeriodEnd == _dia && c.TodosLosCanales),
            Arg.Any<CancellationToken>());
    }
}
