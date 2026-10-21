using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Tests.Common.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Configuration;

/// <summary>
/// Feature 013, T0339 (US1-5; FR-021; data-model §1.2; T44): <see cref="EfectoDeReplicacionUsuraEnLineas"/> corrido por el núcleo de la
/// réplica, en su misma transacción. Una <c>USURA</c> nueva que la versión vigente (o alguna banda) supera bloquea la línea desde el día de
/// la usura (<c>UsuryBlockedSince</c>, <c>UsuryBlockedByValuePublicId</c>) y alerta <c>Cartera.LineaCerradaPorUsura</c> con <c>DedupKey</c>
/// por línea; una línea ya bloqueada no se vuelve a alertar; el efecto corre con Cartera sin activar; una modalidad que sale del catálogo
/// cierra la línea con <c>ModalityRetired</c> y alerta; los conteos quedan en <c>EffectsSummaryJson</c>.
/// </summary>
public sealed class EfectoDeReplicacionUsuraEnLineasTests : IDisposable
{
    private readonly LineasDePrueba _l = new();
    private readonly CentralDePrueba _central = new() { DevolverTodo = true };

    public EfectoDeReplicacionUsuraEnLineasTests() => _l.NormativaAsync(0.25m).GetAwaiter().GetResult();

    public void Dispose() => _l.Dispose();

    private async Task<ResultadoDeReplicacion> Replicar()
    {
        var r = await new ReplicacionDeValoresNacionales(_l.Db, _central, [new EfectoDeReplicacionUsuraEnLineas()], CopiasDePrueba.Reloj(LineasDePrueba.Ahora))
            .AplicarAsync(NationalValueReplicationOrigin.ScheduledTask, "Tarea de réplica", CancellationToken.None);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    private EfectoAplicado Efecto(ResultadoDeReplicacion r) => r.Efectos[EfectoDeReplicacionUsuraEnLineas.NombreDelEfecto];

    [Fact]
    public async Task Una_usura_nueva_menor_que_la_tasa_bloquea_la_linea_y_alerta_en_la_misma_replica()
    {
        await _l.LineaAsync("CONLIB", LineasDePrueba.Version(0.22m));
        await _l.LineaAsync("BAJA", LineasDePrueba.Version(0.18m));
        var nueva = _central.Valor(CodigosDeValorNacional.Usura, LineasDePrueba.ClaveConsumo, new DateOnly(2026, 12, 15), 0.20m, "EA");

        var r = await Replicar();

        r.Motivo.Should().BeNull();
        r.Copio.Should().BeTrue();
        var efecto = Efecto(r);
        efecto.Conteos[EfectoDeReplicacionUsuraEnLineas.ConteoBloqueadas].Should().Be(1);
        var alerta = efecto.Alertas.Should().ContainSingle().Which;
        alerta.TypeCode.Should().Be(TiposDeAlerta.LineaCerradaPorUsura);
        alerta.DedupKey.Should().Be("Cartera.LineaCerradaPorUsura:CONLIB");
        alerta.Body.Should().Contain("CONLIB").And.NotContain("Lending.");

        var copia = await _l.Db.NationalValues.SingleAsync(v => v.SourcePublicId == nueva.PublicId);
        var conlib = await _l.Db.CreditLines.SingleAsync(l => l.Code == "CONLIB");
        conlib.UsuryBlockedSince.Should().Be(new DateOnly(2026, 12, 15));
        conlib.UsuryBlockedByValuePublicId.Should().Be(copia.PublicId, "queda el valor nacional que la bloqueó (la copia de la cooperativa)");
        conlib.Status.Should().Be(CreditLineStatus.Active, "el bloqueo por usura no es un cierre");
        conlib.AdmiteCreditosNuevos.Should().BeFalse();
        (await _l.Db.CreditLines.SingleAsync(l => l.Code == "BAJA")).UsuryBlockedSince.Should().BeNull();
        (await _l.Db.NationalValueReplications.OrderBy(x => x.Id).LastAsync()).EffectsSummaryJson.Should().Contain(EfectoDeReplicacionUsuraEnLineas.ConteoBloqueadas);
        (await _l.Db.LendingSetups.AnyAsync(s => s.ActivationDate != null)).Should().BeFalse("el efecto corre con Cartera sin activar");
    }

    [Fact]
    public async Task Una_linea_ya_bloqueada_no_se_vuelve_a_alertar()
    {
        await _l.LineaAsync("CONLIB", LineasDePrueba.Version(0.22m));
        _central.Valor(CodigosDeValorNacional.Usura, LineasDePrueba.ClaveConsumo, new DateOnly(2026, 12, 15), 0.20m, "EA");
        await Replicar();
        _central.Valor(CodigosDeValorNacional.Usura, LineasDePrueba.ClaveConsumo, new DateOnly(2027, 1, 1), 0.19m, "EA");

        var r = await Replicar();

        Efecto(r).Alertas.Should().BeEmpty();
        Efecto(r).Conteos[EfectoDeReplicacionUsuraEnLineas.ConteoBloqueadas].Should().Be(0);
        (await _l.Db.CreditLines.SingleAsync()).UsuryBlockedSince.Should().Be(new DateOnly(2026, 12, 15), "conserva el primer día");
    }

    [Fact]
    public async Task Una_banda_que_supera_la_usura_nueva_tambien_bloquea()
    {
        await _l.LineaAsync("CONLIB", LineasDePrueba.Version(0.18m) with
        {
            Rates = LineasDePrueba.Version().Rates with { Bands = [new RateBandInput(0m, 2_000_000m, EffectiveAnnual: 0.24m)] },
        });
        _central.Valor(CodigosDeValorNacional.Usura, LineasDePrueba.ClaveConsumo, LineasDePrueba.Hoy.AddDays(1), 0.22m, "EA");

        var r = await Replicar();

        Efecto(r).Conteos[EfectoDeReplicacionUsuraEnLineas.ConteoBloqueadas].Should().Be(1);
    }

    [Fact]
    public async Task Una_version_nueva_bajo_la_usura_limpia_el_bloqueo()
    {
        var linea = await _l.LineaAsync("CONLIB", LineasDePrueba.Version(0.22m));
        _central.Valor(CodigosDeValorNacional.Usura, LineasDePrueba.ClaveConsumo, new DateOnly(2026, 12, 15), 0.20m, "EA");
        await Replicar();

        var r = await _l.Versionar().Handle(new AddCreditLineVersionCommand(linea.PublicId, LineasDePrueba.Version(0.18m, new DateOnly(2026, 12, 15)))
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        (await _l.Db.CreditLines.AsNoTracking().SingleAsync()).UsuryBlockedSince.Should().BeNull();
    }

    [Fact]
    public async Task Una_modalidad_retirada_del_catalogo_cierra_la_linea_con_ModalityRetired_y_alerta()
    {
        await _l.LineaAsync("MICRO", LineasDePrueba.Version(0.30m, modalidad: "MICROCREDITO", subsegmento: null));
        await _l.LineaAsync("CONLIB");
        var sinMicro = LineasDePrueba.Catalogo.Replace(
            """{"code":"MICROCREDITO","name":"Microcrédito","combinations":[{"sarcSegment":"MICROCREDITO","usuryKey":"MICROCREDITO"}]},""", string.Empty);
        _central.Documento(nameof(RegulatoryDocumentKind.CatalogoDeModalidades), "CATMOD-2", LineasDePrueba.Hoy, sinMicro,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sinMicro))));

        var r = await Replicar();

        var efecto = Efecto(r);
        efecto.Conteos[EfectoDeReplicacionUsuraEnLineas.ConteoCerradas].Should().Be(1);
        efecto.Alertas.Should().ContainSingle(a => a.DedupKey == "Cartera.LineaCerradaPorUsura:MICRO");
        var micro = await _l.Db.CreditLines.SingleAsync(l => l.Code == "MICRO");
        micro.Status.Should().Be(CreditLineStatus.Closed);
        micro.ClosedReason.Should().Be(CreditLineClosedReason.ModalityRetired);
        micro.ClosedByUserId.Should().BeNull("la cierra la réplica, no una persona");
        (await _l.Db.CreditLines.SingleAsync(l => l.Code == "CONLIB")).Status.Should().Be(CreditLineStatus.Active);
    }

    [Fact]
    public async Task Sin_usura_ni_catalogo_en_el_lote_no_hace_nada()
    {
        await _l.LineaAsync("CONLIB");
        _central.Valor(CodigosDeValorNacional.Ibr, "3M", LineasDePrueba.Hoy, 0.09m, "NominalMV");

        var r = await Replicar();

        r.Efectos[EfectoDeReplicacionUsuraEnLineas.NombreDelEfecto].Should().BeSameAs(EfectoAplicado.Nada);
    }
}
