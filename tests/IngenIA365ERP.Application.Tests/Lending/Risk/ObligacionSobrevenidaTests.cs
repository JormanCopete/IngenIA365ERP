using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9, T0683 (T50, D28; FR-105; api.md §12.5): con Cartera activada, una <c>ReglaDePerdidaEsperada</c> que llega por la réplica y obliga
/// a la cooperativa por su perfil vigente devuelve <c>Cartera.ObligacionSobrevenida</c> (con los créditos sin historial completo) y, sin K6a,
/// <c>Cartera.ModeloObligatorioNoDisponible</c>; si no la obliga o no hay regla, nada. La consulta del modelo por segmento lo refleja: los segmentos
/// cubiertos van <c>Obligated</c> con el Anexo 1 y la marca <c>ObligatoryModelUnavailable</c>, el resto <c>NotObligated</c>.
/// </summary>
public class ObligacionSobrevenidaTests : IAsyncLifetime
{
    private static readonly DateOnly Desde = new(2027, 1, 1);
    private CierreDePrueba _c = null!;

    public async Task InitializeAsync() => _c = await CierreDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private async Task PerfilAsync(string tipo, string? categoria = null)
    {
        _c.Db.RegulatoryProfileVersions.Add(new RegulatoryProfileVersion
        {
            OrganizationTypeCode = tipo, EmployeeFundCategory = categoria, FinancialActivityAuthorized = true, SupervisionLevel = 1, ValidFrom = new DateOnly(2026, 1, 1),
            Source = "Certificado", Reason = "Perfil inicial", RegisteredByUserId = _c.Jefe.Id, RegisteredByName = "Jefe", CreatedBy = "test",
        });
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();
    }

    private static RegulatoryDocument Regla()
    {
        var contenido = PlatformNationalValuesSeeder.Contenido("regla-de-perdida-esperada.json");
        return new RegulatoryDocument
        {
            Kind = nameof(RegulatoryDocumentKind.ReglaDePerdidaEsperada), Code = "CE100-2026", ValidFrom = Desde, Content = contenido,
            ContentSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contenido))), SchemaVersion = 1, Verified = false,
            Source = "Superintendencia de la Economía Solidaria", SourcePublicId = Guid.NewGuid(), SourceSequence = 90_100,
            CentralRegisteredAt = CierreDePrueba.Hoy.ToDateTime(TimeOnly.MinValue), KnownAt = CierreDePrueba.Hoy.ToDateTime(TimeOnly.MinValue),
        };
    }

    private async Task ReplicarReglaAsync()
    {
        var replica = new NationalValueReplication
        {
            FromSequence = 2, ToSequence = 2, DocumentCount = 1, Origin = NationalValueReplicationOrigin.ScheduledTask,
            StartedAt = _c.G.UtcNow, FinishedAt = _c.G.UtcNow, ActorName = "prueba",
        };
        replica.Documents.Add(Regla());
        _c.Db.NationalValueReplications.Add(replica);
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();
    }

    private Task<EfectoAplicado> ReplicarAsync() =>
        new EfectoDeReplicacionObligacionSobrevenida().AlReplicarAsync(_c.Db, new LoteReplicado([], [Regla()], _c.G.UtcNow, CierreDePrueba.Hoy), default);

    [Fact]
    public async Task Una_regla_que_obliga_a_la_cooperativa_activada_alerta_la_obligacion_sin_la_marca_con_K6a()
    {
        await PerfilAsync("COOP_AHORRO_CREDITO");

        var efecto = await ReplicarAsync();

        // Antes de K6a se sumaba Cartera.ModeloObligatorioNoDisponible; con K6a (T1024) la pérdida esperada está desplegada (T1021, bloque 2, suma
        // Cartera.HistorialDeMoraIncompleto).
        efecto.Alertas.Select(a => a.TypeCode).Should().Equal(TiposDeAlerta.ObligacionSobrevenida);
        efecto.Alertas[0].DedupKey.Should().Be($"{TiposDeAlerta.ObligacionSobrevenida}:2027-01");
        efecto.Alertas[0].Body.Should().Contain("36 meses");
        efecto.Notas.Should().ContainSingle().Which.Should().Contain("2027-01-01");
    }

    [Fact]
    public async Task Una_cooperativa_que_la_regla_no_obliga_no_alerta()
    {
        await PerfilAsync("COOP_MULTIACTIVA");

        (await ReplicarAsync()).Alertas.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_lote_sin_reglas_no_hace_nada()
    {
        await PerfilAsync("COOP_AHORRO_CREDITO");

        (await new EfectoDeReplicacionObligacionSobrevenida().AlReplicarAsync(_c.Db, new LoteReplicado([], [], _c.G.UtcNow, CierreDePrueba.Hoy), default))
            .Should().Be(EfectoAplicado.Nada);
    }

    [Fact]
    public async Task El_modelo_por_segmento_lleva_los_cubiertos_obligados_a_la_perdida_esperada_con_K6a()
    {
        await PerfilAsync("COOP_AHORRO_CREDITO");
        await ReplicarReglaAsync();

        var r = await new GetImpairmentModelBySegmentQueryHandler(new LectorDeDocumentoNormativo(_c.Db), new LectorDePerfilRegulatorio(_c.Db), _c.G.Reloj)
            .Handle(new GetImpairmentModelBySegmentQuery(Desde), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Select(s => s.Segment).Should().BeEquivalentTo("CONSUMO_LIBRANZA", "CONSUMO_SIN_LIBRANZA", "COMERCIAL_PN");
        // Sin K6a quedaban en el Anexo 1 con la marca «ObligatoryModelUnavailable» (D28); con K6a (T1024) van a la pérdida esperada, sin marca.
        r.Value.Should().OnlyContain(s => s.Basis == nameof(ModelBasis.Obligated) && s.Model == nameof(ImpairmentModel.ExpectedLossAnnex2)
            && s.Mark == null && s.Since == Desde);

        var antes = await new GetImpairmentModelBySegmentQueryHandler(new LectorDeDocumentoNormativo(_c.Db), new LectorDePerfilRegulatorio(_c.Db), _c.G.Reloj)
            .Handle(new GetImpairmentModelBySegmentQuery(CierreDePrueba.Hoy), default);
        antes.Value.Should().OnlyContain(s => s.Basis == nameof(ModelBasis.NotObligated), "antes de la vigencia de la regla nadie está obligado");
    }
}
