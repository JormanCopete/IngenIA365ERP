using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.NationalValues;

/// <summary>
/// T0024 (feature 013, T39; quickstart §1.5 «Valores nacionales»): el núcleo único de la réplica,
/// <see cref="ReplicacionDeValoresNacionales"/>, sobre la base de la cooperativa en memoria y una central de mentiras. Copia en
/// orden lo de <c>Sequence</c> mayor que la marca local, es idempotente por <c>SourcePublicId</c>, deja bitácora sólo si copió,
/// corre los efectos en el mismo guardado (y si uno falla no copia nada), no lanza si la central no responde y deja el evento
/// <c>NationalValues.Replicated</c>.
/// </summary>
public class ReplicacionDeValoresNacionalesTests
{
    private static readonly DateTime Ahora = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Enero = new(2026, 1, 1);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly CentralDePrueba _central = new();
    private readonly IAuditService _auditoria = Substitute.For<IAuditService>();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private readonly List<AlertaALevantar> _levantadas = [];

    public ReplicacionDeValoresNacionalesTests()
    {
        _alertas.LevantarAsync(Arg.Do<AlertaALevantar>(_levantadas.Add), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
    }

    private ReplicacionDeValoresNacionales Replicacion(params IEfectoDeReplicacion[] efectos)
    {
        var servicios = new ServiceCollection()
            .AddSingleton(_auditoria)
            .AddSingleton(_alertas)
            .AddSingleton(CooperativaDePrueba.Actual)
            .BuildServiceProvider();
        return new ReplicacionDeValoresNacionales(_db, _central, efectos, CopiasDePrueba.Reloj(Ahora), servicios: servicios);
    }

    private Task<ResultadoDeReplicacion> Replicar(params IEfectoDeReplicacion[] efectos) =>
        Replicacion(efectos).AplicarAsync(NationalValueReplicationOrigin.ScheduledTask, "Proceso de integración", CancellationToken.None);

    [Fact]
    public async Task Copia_solo_lo_de_Sequence_mayor_que_la_ultima_local_y_en_orden()
    {
        _central.Valor(CodigosDeValorNacional.Smmlv, "", Enero, 1_750_905m, "Pesos");
        _central.Valor(CodigosDeValorNacional.Uvt, "", Enero, 52_374m, "Pesos");

        var primera = await Replicar();
        _central.Valor(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.AvisoPrevioDias, Enero, 20m, "Dias");
        var segunda = await Replicar();

        _central.MarcasPedidas.Should().Equal(0L, 2L);
        (primera.Valores, primera.DesdeSequence, primera.HastaSequence).Should().Be((2, 1L, 2L));
        (segunda.Valores, segunda.DesdeSequence, segunda.HastaSequence).Should().Be((1, 3L, 3L));
        var copias = await _db.NationalValues.OrderBy(v => v.Id).ToListAsync();
        copias.Select(c => c.SourceSequence).Should().Equal(1L, 2L, 3L);
        copias.Should().OnlyContain(c => c.KnownAt == Ahora && c.CentralRegisteredAt == _central.RegistradoEn);
        copias.Select(c => c.SourcePublicId).Should().Equal(_central.Valores.Select(v => v.PublicId));
    }

    [Fact]
    public async Task Repetir_no_duplica_y_la_guarda_por_SourcePublicId_salta_lo_ya_copiado()
    {
        _central.Valor(CodigosDeValorNacional.Smmlv, "", Enero, 1_750_905m, "Pesos");
        await Replicar();

        _central.DevolverTodo = true;
        var otra = await Replicar();

        otra.Copio.Should().BeFalse();
        otra.ReplicacionPublicId.Should().BeNull();
        (await _db.NationalValues.CountAsync()).Should().Be(1);
        (await _db.NationalValueReplications.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Lo_que_confirmo_despues_lleva_numero_mayor_y_la_pasada_siguiente_lo_trae()
    {
        // El contador numera en la misma transacción del registro: lo que confirma después tiene Sequence mayor que todo lo
        // copiado, así que la marca de agua (MAX local) nunca lo deja atrás.
        var a = _central.Valor(CodigosDeValorNacional.Smmlv, "", Enero, 1_750_905m, "Pesos");
        await Replicar();
        var b = _central.Valor(CodigosDeValorNacional.Uvt, "", Enero, 52_374m, "Pesos");
        var c = _central.Valor(CodigosDeValorNacional.Smmlv, "", Enero, 1_750_906m, "Pesos", corrige: a.PublicId);

        await Replicar();

        (b.Sequence, c.Sequence).Should().Be((2L, 3L));
        (await _db.NationalValues.Select(v => v.SourcePublicId).ToListAsync()).Should().BeEquivalentTo([a.PublicId, b.PublicId, c.PublicId]);
        (await _db.NationalValues.SingleAsync(v => v.SourcePublicId == c.PublicId)).SupersedesSourcePublicId.Should().Be(a.PublicId);
    }

    [Fact]
    public async Task La_bitacora_se_escribe_solo_si_copio_algo()
    {
        var vacia = await Replicar();
        vacia.Should().Be(ResultadoDeReplicacion.SinNovedad);
        (await _db.NationalValueReplications.AnyAsync()).Should().BeFalse("una pasada vacía no deja fila: serían 1.440 por día");

        _central.Valor(CodigosDeValorNacional.Smmlv, "", Enero, 1_750_905m, "Pesos");
        _central.Documento(nameof(RegulatoryDocumentKind.TiposDeOrganizacion), "TIPOS-1", Enero, "{}", new string('a', 64));
        var r = await Replicar();

        var bitacora = await _db.NationalValueReplications.SingleAsync();
        (bitacora.FromSequence, bitacora.ToSequence, bitacora.ValueCount, bitacora.DocumentCount, bitacora.Origin, bitacora.ActorName)
            .Should().Be((1L, 2L, 1, 1, NationalValueReplicationOrigin.ScheduledTask, "Proceso de integración"));
        bitacora.PublicId.Should().Be(r.ReplicacionPublicId!.Value);
        (await _db.NationalValues.SingleAsync()).ReplicationId.Should().Be(bitacora.Id);
        (await _db.RegulatoryDocuments.SingleAsync()).ReplicationId.Should().Be(bitacora.Id);
    }

    [Fact]
    public async Task Los_efectos_corren_con_el_lote_en_el_mismo_guardado_y_su_resumen_queda_en_la_bitacora()
    {
        _central.Valor(CodigosDeValorNacional.Smmlv, "", Enero, 1_750_905m, "Pesos");
        var efecto = new EfectoQueEscribe();

        var r = await Replicar(efecto);

        efecto.Lote!.Valores.Should().ContainSingle(v => v.Code == CodigosDeValorNacional.Smmlv);
        efecto.Lote.Hoy.Should().Be(DateOnly.FromDateTime(Ahora));
        (await _db.Banks.SingleAsync()).Name.Should().Be("Escrito por el efecto", "lo que escribe el efecto se guarda con la réplica");
        var resumen = (await _db.NationalValueReplications.SingleAsync()).EffectsSummaryJson;
        resumen.Should().Contain("prueba").And.Contain("escritas").And.Contain("una nota del efecto");
        r.Efectos.Should().ContainKey("prueba");
        _levantadas.Should().ContainSingle(a => a.TypeCode == AlertasDeValoresNacionales.ValorNacionalDistinto,
            "las alertas que pide un efecto se levantan después de guardar");
    }

    [Fact]
    public async Task Si_un_efecto_falla_no_se_copia_nada()
    {
        _central.Valor(CodigosDeValorNacional.Smmlv, "", Enero, 1_750_905m, "Pesos");

        var r = await Replicar(new EfectoQueEscribe(), new EfectoQueFalla());

        r.Copio.Should().BeFalse();
        r.Motivo.Should().Contain("falla").And.Contain("se rompió");
        (await _db.NationalValues.AnyAsync()).Should().BeFalse();
        (await _db.NationalValueReplications.AnyAsync()).Should().BeFalse();
        (await _db.Banks.AnyAsync()).Should().BeFalse("ni lo que alcanzó a escribir el efecto anterior");
    }

    [Fact]
    public async Task Si_la_central_no_responde_devuelve_el_motivo_sin_lanzar()
    {
        _central.Falla = new TimeoutException("la base administrativa no contesta");

        var r = await Replicar();

        r.CentralDisponible.Should().BeFalse();
        r.Motivo.Should().Contain("no contesta");
        (await _db.NationalValueReplications.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Cada_replica_que_copio_deja_un_evento_NationalValues_Replicated_con_conteos_y_rango()
    {
        _central.Valor(CodigosDeValorNacional.Smmlv, "", Enero, 1_750_905m, "Pesos");
        _central.Valor(CodigosDeValorNacional.Uvt, "", Enero, 52_374m, "Pesos");

        await Replicar();
        await Replicar();

        var enMongo = _auditoria.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IAuditService.LogAsync))
            .Select(c => c.GetArguments()[0]).OfType<AuditLogCommand>()
            .Where(c => c.Action == ReplicacionDeValoresNacionales.EventoReplicada).ToList();
        var enCadena = await _db.AuditOutbox.Where(e => e.PayloadJson != null && e.PayloadJson.Contains(ReplicacionDeValoresNacionales.EventoReplicada)).CountAsync();
        (enMongo.Count + enCadena).Should().Be(1, "un evento por réplica que copió; la pasada vacía no deja evento");
        if (enMongo.Count == 1)
        {
            var evento = enMongo[0];
            evento.Module.Should().Be(ReplicacionDeValoresNacionales.ModuloDeAuditoria);
            System.Text.Json.JsonSerializer.Serialize(evento.NewValues).Should().Contain("\"fromSequence\":1").And.Contain("\"toSequence\":2").And.Contain("\"values\":2");
        }
    }

    // ----------------------------------------------------------------------------------------------- efectos de prueba --

    private sealed class EfectoQueEscribe : IEfectoDeReplicacion
    {
        public LoteReplicado? Lote { get; private set; }
        public string Nombre => "prueba";

        public Task<EfectoAplicado> AlReplicarAsync(IApplicationDbContext db, LoteReplicado lote, CancellationToken ct)
        {
            Lote = lote;
            db.Banks.Add(new Domain.Entities.Core.Bank { LegacyCode = "EFECTO", Name = "Escrito por el efecto", CreatedBy = "efecto" });
            return Task.FromResult(new EfectoAplicado(new Dictionary<string, int> { ["escritas"] = 1 }, ["una nota del efecto"],
                [new AlertaALevantar(AlertasDeValoresNacionales.ValorNacionalDistinto, "Distinto", "Cuerpo", DedupKey: "x")]));
        }
    }

    private sealed class EfectoQueFalla : IEfectoDeReplicacion
    {
        public string Nombre => "falla";

        public Task<EfectoAplicado> AlReplicarAsync(IApplicationDbContext db, LoteReplicado lote, CancellationToken ct) =>
            throw new InvalidOperationException("se rompió");
    }
}
