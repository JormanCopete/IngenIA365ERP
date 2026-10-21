using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes;
using IngenIA365ERP.Application.Lending.Disbursements.Tareas;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.ExternalChannels;
using IngenIA365ERP.ExternalChannels.Securities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Disbursements;

/// <summary>
/// Feature 013, US3, T0425 (DT §2.13, T7; data-model §2.8, §2.9, §7.4; adaptadores §3.8): las tres tareas programadas del desembolso. Cada una
/// corre por el ejecutor (con los servicios del ámbito de la cooperativa), escribe sólo por su comando y sale si Cartera no está activada:
/// <list type="bullet">
/// <item><c>cartera.giros-sin-resultado</c> alerta <c>Cartera.GiroSinResultado</c> por cada orden emitida que pasó su <c>ResultDueOn</c> (que ya
/// trae los días hábiles de <c>Giros.DiasSinResultado</c>);</item>
/// <item><c>cartera.pagares</c> consulta al depósito los pagarés desmaterializados creados o pendientes de firma y, pasados los días de vigencia de
/// la aprobación sin firma, alerta <c>Cartera.PagarePendienteDeFirma</c>;</item>
/// <item><c>cartera.garantias-por-vencer</c> alerta <c>Cartera.GarantiaPorVencer</c> por <c>ValidUntil</c> y por <c>PolicyExpiresOn</c>.</item>
/// </list>
/// Repetir no duplica: cada alerta lleva el <c>DedupKey</c> de su sujeto.
/// </summary>
public sealed class TareasDeDesembolsoTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private Loan _credito = null!;
    private readonly ICurrentTenantService _tenant = Substitute.For<ICurrentTenantService>();
    private readonly MemoriaDeDepositoSimulado _memoria = new();

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        _credito = _d.O.Credito(_d.O.Ana, 3_000_000m, "CR-000090");
        _tenant.TenantId.Returns(Guid.NewGuid().ToString());
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private List<AlertaALevantar> Levantadas => _d.O.L.Levantadas;

    private DepositoDeValoresSimulado Simulado() => new(Options.Create(new ExternalChannelsOptions()), _memoria);

    private DepositoDelPagare Deposito() => new(_d.Db, new DepositosDeValores([Simulado()], new CircuitoSiempreCerrado()),
        Substitute.For<ICredencialesDeCanalExterno>(), _tenant, Options.Create(_d.O.Opciones));

    private AlertDisbursementOrdersWithoutResultCommandHandler Giros() => new(_d.Db, _d.Alertas, _d.G.Reloj);
    private SyncPromissoryNotesCommandHandler Pagares() => new(_d.Db, Deposito(), _d.Parametros(), _d.Alertas, _d.G.Reloj);
    private AlertExpiringCollateralsCommandHandler Garantias() => new(_d.Db, _d.Alertas, _d.G.Reloj);

    private void Desactivar()
    {
        var setup = _d.Db.LendingSetups.Single(s => s.Id == LendingSetup.IdDeLaFilaUnica);
        setup.ActivationDate = null;
        _d.Db.SaveChanges();
    }

    private DisbursementOrder Orden(string numero, DateOnly vence, DisbursementOrderStatus estado = DisbursementOrderStatus.Issued)
    {
        var o = DisbursementOrder.Emitir(_credito.Id, 700 + _d.Db.DisbursementOrders.Count(), DisbursementForm.Transfer, _d.O.Ana.Id, "CC", "52000001",
            "Ana Solicitante", _d.Banco.Id, 1, "0123456789", 1_000_000m, 0m, DesembolsoDePrueba.Hoy.AddDays(-10), vence, false, _d.G.UtcNow, _d.Cajera.Id);
        o.Number = numero;
        o.Status = estado;
        _d.Db.DisbursementOrders.Add(o);
        _d.Db.SaveChanges();
        return o;
    }

    private async Task<PromissoryNote> PagareAsync(string documento, DateOnly emitido)
    {
        var contexto = new ContextoDeCanalExterno(Guid.Parse(_tenant.TenantId!), CodigosDeCanalExterno.Simulado, null, null, "x", true);
        var publicId = Guid.NewGuid();
        var creado = await Simulado().CrearPagareAsync(new SolicitudDePagare(publicId, "CR-000090", 1_000_000m, emitido,
            [new FirmanteDePagare(Guid.NewGuid(), FirmanteDePagare.Deudor, "CC", documento, "Ana", null)]), contexto, default);
        var p = new PromissoryNote
        {
            PublicId = publicId, Kind = PromissoryNoteKind.Dematerialized, LoanId = _credito.Id, IssuedOn = emitido, Status = PromissoryNoteStatus.PendingSignature,
            CustodianChannelCode = CodigosDeCanalExterno.Simulado, ExternalNoteId = creado.Valor!.IdExterno, Number = creado.Valor.IdExterno, IsSimulated = true,
        };
        _d.Db.PromissoryNotes.Add(p);
        _d.Db.SaveChanges();
        return p;
    }

    private Collateral Garantia(DateOnly? vence, DateOnly? poliza, CollateralStatus estado = CollateralStatus.Valid)
    {
        var g = new Collateral
        {
            Kind = CollateralKind.Pledge, OwnerPersonId = _d.O.Ana.Id, LoanId = _credito.Id, Description = "Vehículo", Value = 1m, AdmissibleValue = 1m,
            Status = estado, ConstitutedOn = DesembolsoDePrueba.Hoy.AddMonths(-6), ValidUntil = vence, PolicyNumber = poliza is null ? null : "POL-1",
            PolicyExpiresOn = poliza,
        };
        _d.Db.Collaterals.Add(g);
        _d.Db.SaveChanges();
        return g;
    }

    // ------------------------------------------------------------------------------------------------------------- giros --

    [Fact]
    public async Task Alerta_las_ordenes_emitidas_que_pasaron_su_plazo_de_resultado_una_sola_vez()
    {
        var vencida = Orden("OG-000101", DesembolsoDePrueba.Hoy.AddDays(-1));
        Orden("OG-000102", DesembolsoDePrueba.Hoy);
        Orden("OG-000103", DesembolsoDePrueba.Hoy.AddDays(-5), DisbursementOrderStatus.Disbursed);

        var primera = await Giros().Handle(new AlertDisbursementOrdersWithoutResultCommand(), default);
        var segunda = await Giros().Handle(new AlertDisbursementOrdersWithoutResultCommand(), default);

        (primera.Value, segunda.Value).Should().Be((1, 1));
        Levantadas.Should().HaveCount(2).And.OnlyContain(a => a.TypeCode == TiposDeAlerta.GiroSinResultado && a.EntityPublicId == vencida.PublicId);
        Levantadas.Select(a => a.DedupKey).Distinct().Should().ContainSingle().Which.Should().Be($"{TiposDeAlerta.GiroSinResultado}:{vencida.PublicId:D}");
        Levantadas[0].ScopeBranchPublicId.Should().Be(_d.O.Norte.PublicId);
    }

    [Fact]
    public async Task Sin_Cartera_activada_ninguna_tarea_hace_nada()
    {
        Orden("OG-000111", DesembolsoDePrueba.Hoy.AddDays(-1));
        Garantia(DesembolsoDePrueba.Hoy.AddDays(3), null);
        await PagareAsync("52000004", DesembolsoDePrueba.Hoy.AddDays(-60));
        Desactivar();

        (await Giros().Handle(new AlertDisbursementOrdersWithoutResultCommand(), default)).Value.Should().Be(0);
        (await Pagares().Handle(new SyncPromissoryNotesCommand(), default)).Value.Should().Be(0);
        (await Garantias().Handle(new AlertExpiringCollateralsCommand(), default)).Value.Should().Be(0);
        Levantadas.Should().BeEmpty();
        (await _d.Db.PromissoryNotes.AsNoTracking().SingleAsync()).LastSyncedAt.Should().BeNull();
    }

    // ----------------------------------------------------------------------------------------------------------- pagarés --

    [Fact]
    public async Task Consulta_los_pagares_pendientes_y_alerta_los_que_pasaron_la_vigencia_de_la_aprobacion_sin_firma()
    {
        var seFirma = await PagareAsync("52000004", DesembolsoDePrueba.Hoy.AddDays(-40));
        var rechazado = await PagareAsync("52000001", DesembolsoDePrueba.Hoy.AddDays(-40));
        var reciente = await PagareAsync("52000003", DesembolsoDePrueba.Hoy.AddDays(-2));
        _d.Db.ChangeTracker.Clear();

        var r = await Pagares().Handle(new SyncPromissoryNotesCommand(), default);
        await Pagares().Handle(new SyncPromissoryNotesCommand(), default);

        r.Value.Should().Be(3);
        var filas = await _d.Db.PromissoryNotes.AsNoTracking().ToDictionaryAsync(p => p.PublicId);
        filas[seFirma.PublicId].Status.Should().Be(PromissoryNoteStatus.InCustody);
        filas[rechazado.PublicId].Status.Should().Be(PromissoryNoteStatus.PendingSignature);
        filas[reciente.PublicId].Status.Should().Be(PromissoryNoteStatus.PendingSignature);
        filas.Values.Should().OnlyContain(p => p.LastSyncedAt == _d.G.UtcNow);
        Levantadas.Should().OnlyContain(a => a.TypeCode == TiposDeAlerta.PagarePendienteDeFirma && a.EntityPublicId == rechazado.PublicId);
        Levantadas.Select(a => a.DedupKey).Distinct().Should().Equal($"{TiposDeAlerta.PagarePendienteDeFirma}:{rechazado.PublicId:D}");
    }

    // --------------------------------------------------------------------------------------------------------- garantías --

    [Fact]
    public async Task Alerta_las_garantias_que_vencen_o_cuya_poliza_vence_dentro_de_la_anticipacion()
    {
        var porVencer = Garantia(DesembolsoDePrueba.Hoy.AddDays(10), null);
        var polizaPorVencer = Garantia(null, DesembolsoDePrueba.Hoy.AddDays(20));
        Garantia(DesembolsoDePrueba.Hoy.AddDays(60), DesembolsoDePrueba.Hoy.AddDays(90));
        Garantia(DesembolsoDePrueba.Hoy.AddDays(5), null, CollateralStatus.Released);

        var r = await Garantias().Handle(new AlertExpiringCollateralsCommand(), default);
        await Garantias().Handle(new AlertExpiringCollateralsCommand(), default);

        r.Value.Should().Be(2);
        Levantadas.Should().OnlyContain(a => a.TypeCode == TiposDeAlerta.GarantiaPorVencer);
        Levantadas.Select(a => a.EntityPublicId).Distinct().Should().BeEquivalentTo(new Guid?[] { porVencer.PublicId, polizaPorVencer.PublicId });
        Levantadas.Select(a => a.DedupKey).Distinct().Should().HaveCount(2).And.Contain($"{TiposDeAlerta.GarantiaPorVencer}:{porVencer.PublicId:D}");
    }

    // ------------------------------------------------------------------------------------------------------------ tareas --

    [Theory]
    [InlineData(typeof(TareaDeGirosSinResultado), "cartera.giros-sin-resultado", typeof(AlertDisbursementOrdersWithoutResultCommand))]
    [InlineData(typeof(TareaDePagares), "cartera.pagares", typeof(SyncPromissoryNotesCommand))]
    [InlineData(typeof(TareaDeGarantiasPorVencer), "cartera.garantias-por-vencer", typeof(AlertExpiringCollateralsCommand))]
    public async Task Cada_tarea_corre_su_comando_con_los_servicios_de_la_cooperativa(Type tipo, string nombre, Type comando)
    {
        var tarea = (Application.Common.Execution.ITareaProgramada)Activator.CreateInstance(tipo)!;
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<IRequest<Result<int>>>(), Arg.Any<CancellationToken>()).Returns(Result.Success(0));
        var servicios = new ServiceCollection().AddSingleton(sender).BuildServiceProvider();

        await tarea.EjecutarAsync(servicios, default);

        tarea.Nombre.Should().Be(nombre);
        tarea.DebeCorrer(new DateTimeOffset(2026, 12, 1, 7, 0, 0, TimeSpan.FromHours(-5)), null).Should().BeTrue();
        await sender.Received(1).Send(Arg.Is<IRequest<Result<int>>>(r => r.GetType() == comando), Arg.Any<CancellationToken>());
    }
}
