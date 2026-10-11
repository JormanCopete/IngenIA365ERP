using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Disbursements.Collaterals;
using IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.ExternalChannels;
using IngenIA365ERP.ExternalChannels.Securities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Disbursements;

/// <summary>
/// Feature 013, US3, T0422 (FR-043, FR-044, FR-082; data-model §2.8–§2.9; api.md §6; adaptadores §3.6–§3.8): garantías y pagaré sobre el escenario
/// del desembolso (<see cref="DesembolsoDePrueba"/>), con el depósito <c>SIMULADO</c> real detrás de su registro:
/// <list type="bullet">
/// <item>registrar admite sólo las clases de la regla de la línea que casa (<c>Lending.Collateral.KindNotAdmitted</c>), no sobre un dueño ya
/// decidido (<c>.OwnerLocked</c>) y un constituyente distinto del deudor necesita su autorización (<c>Lending.Authorization.Missing</c>);</item>
/// <item>el estado sigue <c>Constituted → Valid → Extinguished | Executed</c>; liberar exige el crédito cancelado y en firme;</item>
/// <item>con A3, constituir, revaluar, extinguir o ejecutar emite <c>GarantiaConstituida</c> en <c>NoPasa</c>;</item>
/// <item>el pagaré desmaterializado sella canal, id externo y simulado, se actualiza por el depósito y en producción nunca perfecciona; el físico
/// se custodia con su documento; endosar y anular van por el adaptador y anular espera la cancelación en firme.</item>
/// </list>
/// </summary>
public sealed class GarantiasYPagareTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private readonly ICurrentTenantService _tenant = Substitute.For<ICurrentTenantService>();
    private readonly MemoriaDeDepositoSimulado _memoria = new();
    private ICircuitoDeCanalExterno _circuito = new CircuitoSiempreCerrado();

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync(OriginacionDePrueba.VersionDeLinea() with
        {
            CollateralRules = [new CollateralRuleInput(AmountFrom: 1m, AdmittedKinds: [CollateralKind.Pledge, CollateralKind.PromissoryNote])],
        });
        _tenant.TenantId.Returns(Guid.NewGuid().ToString());
        // El depósito simulado decide por el último dígito del documento del deudor (§3.7): 4 firma en la consulta siguiente.
        var ana = await _d.Db.People.SingleAsync(p => p.Id == _d.O.Ana.Id);
        ana.TaxId = "52000004";
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------------------------ piezas --

    private DepositoDelPagare Deposito() => new(_d.Db,
        new DepositosDeValores([new DepositoDeValoresSimulado(Options.Create(new ExternalChannelsOptions()), _memoria)], _circuito),
        Substitute.For<ICredencialesDeCanalExterno>(), _tenant, Options.Create(_d.O.Opciones));

    private RegisterCollateralCommandHandler Registrar() =>
        new(_d.Db, _d.O.Autorizaciones, _d.Emision(), _d.FechaContable(), _d.G.Alcance, _d.G.Auditoria, _d.G.Reloj);
    private UpdateCollateralCommandHandler Actualizar() => new(_d.Db, _d.Emision(), _d.FechaContable(), _d.G.Alcance, _d.G.Auditoria, _d.G.Reloj);
    private ChangeCollateralStatusCommandHandler CambiarEstado() =>
        new(_d.Db, _d.Emision(), _d.FechaContable(), _d.G.Alcance, _d.G.Auditoria, _d.G.Reloj);
    private ReleaseCollateralCommandHandler Liberar() =>
        new(_d.Db, _d.Emision(), _d.FechaContable(), _d.G.Alcance, _d.G.Auditoria, _d.G.Actor, _d.G.Reloj);
    private CreatePromissoryNoteCommandHandler CrearPagare() => new(_d.Db, Deposito(), _d.G.Alcance, _d.G.Auditoria, _d.G.Reloj);
    private RefreshPromissoryNoteStatusCommandHandler Actualizado() => new(_d.Db, Deposito(), _d.O.Guardia(), _d.G.Alcance, _d.G.Auditoria, _d.G.Reloj);
    private RegisterPhysicalPromissoryNoteCustodyCommandHandler Custodiar() => new(_d.Db, _d.G.Alcance, _d.G.Auditoria, _d.G.Reloj);
    private EndorsePromissoryNoteCommandHandler Endosar() => new(_d.Db, Deposito(), _d.O.Guardia(), _d.G.Alcance, _d.G.Auditoria, _d.G.Reloj);
    private VoidPromissoryNoteCommandHandler Anular() => new(_d.Db, Deposito(), _d.G.Alcance, _d.G.Auditoria, _d.G.Reloj);

    private void DepositoConfigurado(string canal = CodigosDeCanalExterno.Simulado)
    {
        _d.Db.ExternalChannelSettings.Add(new ExternalChannelSetting
        {
            Kind = ExternalChannelKind.SecuritiesDepository, SettingKey = "SecuritiesDepository", ChannelCode = canal, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba",
        });
        _d.Db.SaveChanges();
    }

    private LendingFactType ConA3()
    {
        var tipo = new LendingFactType
        {
            Code = GarantiaConstituidaV1.Type, ShortCode = "GCT", Name = "Garantía constituida", DefaultMode = ModoDePasoDeCartera.NoPasa,
        };
        _d.Db.LendingFactTypes.Add(tipo);
        _d.Db.SaveChanges();
        return tipo;
    }

    private CollateralRegistrationInput Prenda(Guid solicitud, decimal valor = 5_000_000m) => new()
    {
        OwnerKind = DuenosDeGarantia.Solicitud, OwnerPublicId = solicitud, Kind = CollateralKind.Pledge, Description = "Vehículo placa ABC123",
        Value = valor, AdmissibleValue = valor * 0.7m, ValidUntil = DesembolsoDePrueba.Hoy.AddDays(20),
    };

    private async Task<CollateralDto> RegistradaAsync(Guid solicitud, decimal valor = 5_000_000m)
    {
        var r = await Registrar().Handle(new RegisterCollateralCommand(Prenda(solicitud, valor)) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _d.Db.ChangeTracker.Clear();
        return r.Value;
    }

    private async Task<Domain.Entities.Lending.Origination.CreditApplication> SolicitudAsync(Guid publicId) =>
        await _d.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == publicId);

    private async Task<PromissoryNoteDto> PagareAsync(Guid solicitud, PromissoryNoteKind forma)
    {
        var r = await CrearPagare().Handle(new CreatePromissoryNoteCommand(DuenosDeGarantia.Solicitud, solicitud, forma, forma == PromissoryNoteKind.Physical ? "P-001" : null)
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _d.Db.ChangeTracker.Clear();
        return r.Value;
    }

    private static JsonElement Contenido(Domain.Entities.Integration.Transactions.IntegrationMessage m) => JsonDocument.Parse(m.PayloadJson).RootElement;

    // ------------------------------------------------------------------------------------------------------- garantías --

    [Fact]
    public async Task Registrar_admite_solo_las_clases_de_la_regla_que_casa()
    {
        var s = await _d.O.RadicadaAsync();

        var r = await Registrar().Handle(new RegisterCollateralCommand(Prenda(s.PublicId) with { Kind = CollateralKind.Mortgage }) { OperationKey = Guid.NewGuid() },
            default);

        r.Error.Code.Should().Be(ErroresDeGarantias.CodigoClaseNoAdmitida);
        DesembolsoDePrueba.Datos(r.Error).GetProperty("admitted").EnumerateArray().Select(e => e.GetString()).Should().Contain("Pledge");
        (await _d.Db.Collaterals.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Registrar_sobre_una_solicitud_decidida_responde_OwnerLocked()
    {
        var s = await _d.AprobadaAsync();

        var r = await Registrar().Handle(new RegisterCollateralCommand(Prenda(s.PublicId)) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ErroresDeGarantias.CodigoDuenoDecidido);
    }

    [Fact]
    public async Task Un_constituyente_distinto_del_deudor_sin_autorizacion_responde_Authorization_Missing()
    {
        var s = await _d.O.RadicadaAsync();
        _d.O.QuitarAutorizacion(_d.O.Beto, DataAuthorizationPurpose.CreditProcessing);

        var r = await Registrar().Handle(new RegisterCollateralCommand(Prenda(s.PublicId) with { ConstituentPersonPublicId = _d.O.Beto.PublicId })
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.Error.Code.Should().Be(ErroresDeOriginacion.CodigoSinAutorizacion);
    }

    [Fact]
    public async Task Registrar_deja_la_garantia_constituida_de_la_solicitud()
    {
        var s = await _d.O.RadicadaAsync();

        var g = await RegistradaAsync(s.PublicId);

        (g.Kind, g.Status, g.Owner.Kind, g.Owner.PublicId).Should().Be(("Pledge", "Constituted", DuenosDeGarantia.Solicitud, s.PublicId));
        g.OwnerPersonPublicId.Should().Be(_d.O.Ana.PublicId);
        g.AdmissibleValue.Should().Be(3_500_000m);
        var fila = await _d.Db.Collaterals.AsNoTracking().SingleAsync();
        (fila.ApplicationId, fila.LoanId, fila.ConstitutedOn).Should().Be(((await SolicitudAsync(s.PublicId)).Id, null, DesembolsoDePrueba.Hoy));
        (await _d.Db.IntegrationMessages.CountAsync()).Should().Be(0, "sin A3 no hay contingente");
    }

    [Fact]
    public async Task Actualizar_una_garantia_de_un_dueno_decidido_responde_OwnerLocked()
    {
        var s = await _d.O.RadicadaAsync();
        var g = await RegistradaAsync(s.PublicId);
        var fila = await _d.Db.CreditApplications.SingleAsync(a => a.PublicId == s.PublicId);
        fila.Status = CreditApplicationStatus.Approved;
        await _d.Db.SaveChangesAsync();

        var r = await Actualizar().Handle(new UpdateCollateralCommand(g.PublicId, Prenda(s.PublicId, 6_000_000m)) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ErroresDeGarantias.CodigoDuenoDecidido);
    }

    [Fact]
    public async Task El_estado_sigue_constituida_vigente_y_extinguida_o_ejecutada()
    {
        var s = await _d.O.RadicadaAsync();
        var g = await RegistradaAsync(s.PublicId);

        var vigente = await CambiarEstado().Handle(new ChangeCollateralStatusCommand(g.PublicId, CollateralStatus.Valid, "Registrada la prenda")
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _d.Db.ChangeTracker.Clear();
        var deVuelta = await CambiarEstado().Handle(new ChangeCollateralStatusCommand(g.PublicId, CollateralStatus.Constituted, "error")
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        var ejecutada = await CambiarEstado().Handle(new ChangeCollateralStatusCommand(g.PublicId, CollateralStatus.Executed, "Remate")
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        vigente.Value.Status.Should().Be("Valid");
        deVuelta.Error.Code.Should().Be(Collateral.CodigoTransicionInvalida);
        var datos = DesembolsoDePrueba.Datos(deVuelta.Error);
        (datos.GetProperty("from").GetString(), datos.GetProperty("to").GetString()).Should().Be(("Valid", "Constituted"));
        ejecutada.Value.Status.Should().Be("Executed");
        (await _d.Db.Collaterals.AsNoTracking().SingleAsync()).ExecutedOn.Should().Be(DesembolsoDePrueba.Hoy);
    }

    [Fact]
    public async Task Liberar_exige_el_credito_cancelado_y_la_firmeza_del_pago()
    {
        var s = await _d.O.RadicadaAsync();
        var g = await RegistradaAsync(s.PublicId);
        await CambiarEstado().Handle(new ChangeCollateralStatusCommand(g.PublicId, CollateralStatus.Valid, "Registrada") { OperationKey = Guid.NewGuid() }, default);
        var credito = _d.O.Credito(_d.O.Ana, 3_000_000m, "CR-LIB-1");
        var fila = await _d.Db.Collaterals.SingleAsync();
        fila.AsignarCredito(credito.Id);
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        var activo = await Liberar().Handle(new ReleaseCollateralCommand(g.PublicId, "Pagó") { OperationKey = Guid.NewGuid() }, default);
        activo.Error.Code.Should().Be(ErroresDeGarantias.CodigoCreditoNoCancelado);

        var c = await _d.Db.Loans.SingleAsync(l => l.Id == credito.Id);
        c.Status = LoanStatus.PaidOff;
        c.PaidOffOn = DesembolsoDePrueba.Hoy;
        var pago = new LoanOperation
        {
            LoanId = credito.Id, Kind = LoanOperationKind.Payment, Number = "PAG-000001", Sequence = 2, ValueDate = DesembolsoDePrueba.Hoy,
            AccountingDate = DesembolsoDePrueba.Hoy, RegisteredAt = _d.G.UtcNow, Amount = 3_000_000m, FinalityDate = DesembolsoDePrueba.Hoy.AddDays(3),
        };
        _d.Db.LoanOperations.Add(pago);
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        var sinFirmeza = await Liberar().Handle(new ReleaseCollateralCommand(g.PublicId, "Pagó") { OperationKey = Guid.NewGuid() }, default);
        sinFirmeza.Error.Code.Should().Be(ErroresDeGarantias.CodigoSinFirmeza);
        DesembolsoDePrueba.Datos(sinFirmeza.Error).GetProperty("finalityUntil").GetString().Should().Be("2026-12-04");

        _d.Manana();
        _d.G.HoyLocal = DesembolsoDePrueba.Hoy.AddDays(3);
        var liberada = await Liberar().Handle(new ReleaseCollateralCommand(g.PublicId, "Pagó y quedó en firme") { OperationKey = Guid.NewGuid() }, default);

        liberada.IsSuccess.Should().BeTrue(liberada.IsFailure ? liberada.Error.Message : string.Empty);
        liberada.Value.Status.Should().Be("Released");
        var final = await _d.Db.Collaterals.AsNoTracking().SingleAsync();
        (final.ReleasedOn, final.ReleaseOperationId).Should().Be((DesembolsoDePrueba.Hoy.AddDays(3), pago.Id));
    }

    [Fact]
    public async Task Con_A3_constituir_revaluar_y_extinguir_emiten_GarantiaConstituida_en_NoPasa()
    {
        ConA3();
        var s = await _d.O.RadicadaAsync();
        var g = await RegistradaAsync(s.PublicId);
        await Actualizar().Handle(new UpdateCollateralCommand(g.PublicId, Prenda(s.PublicId, 6_000_000m)) { OperationKey = Guid.NewGuid() }, default);
        _d.Db.ChangeTracker.Clear();
        await CambiarEstado().Handle(new ChangeCollateralStatusCommand(g.PublicId, CollateralStatus.Extinguished, "El garante no consintió")
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        var mensajes = await _d.Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == GarantiaConstituidaV1.Type).OrderBy(m => m.Id).ToListAsync();
        mensajes.Select(m => Contenido(m).GetProperty("movement").GetString()).Should().Equal("Constituted", "Revalued", "Extinguished");
        mensajes.Select(m => Contenido(m).GetProperty("amounts")[0].GetProperty("amount").GetDecimal()).Should().Equal(5_000_000m, 1_000_000m, -6_000_000m);
        mensajes.Select(m => Contenido(m).GetProperty("balanceAfter").GetDecimal()).Should().Equal(5_000_000m, 6_000_000m, 0m);
        mensajes.Should().OnlyContain(m => m.OriginPublicId == g.PublicId && m.OriginDocumentClass == EmisionDeCartera.ClaseGarantia);
        mensajes[0].OriginEventKey.Should().Be("Confirmation");
        mensajes.Select(m => m.OriginEventKey).Should().OnlyHaveUniqueItems();
        Contenido(mensajes[0]).GetProperty("subject").GetProperty("kind").GetString().Should().Be("Collateral");
        Contenido(mensajes[0]).GetProperty("debtorPersonPublicId").GetGuid().Should().Be(_d.O.Ana.PublicId);
        var ids = mensajes.Select(m => m.Id).ToList();
        (await _d.Db.IntegrationMessageDeliveries.AsNoTracking().Where(e => ids.Contains(e.MessageId)).Select(e => e.Mode).ToListAsync())
            .Should().OnlyContain(m => m == DeliveryMode.NotPosted);
    }

    // ---------------------------------------------------------------------------------------------------------- pagaré --

    [Fact]
    public async Task El_pagare_desmaterializado_sella_canal_id_externo_y_simulado_y_queda_pendiente_de_firma()
    {
        DepositoConfigurado();
        var s = await _d.AprobadaAsync();

        var p = await PagareAsync(s.PublicId, PromissoryNoteKind.Dematerialized);

        (p.Form, p.Status, p.ChannelCode, p.Simulated, p.Perfected).Should().Be(("Dematerialized", "PendingSignature", CodigosDeCanalExterno.Simulado, true, false));
        p.ExternalNoteId.Should().StartWith("SIM-");
        var fila = await _d.Db.PromissoryNotes.AsNoTracking().SingleAsync();
        (fila.CustodianChannelCode, fila.ExternalNoteId, fila.IsSimulated, fila.IssuedOn).Should()
            .Be((CodigosDeCanalExterno.Simulado, p.ExternalNoteId, true, DesembolsoDePrueba.Hoy));
    }

    [Fact]
    public async Task Un_segundo_pagare_vivo_del_mismo_dueno_responde_AlreadyExists()
    {
        DepositoConfigurado();
        var s = await _d.AprobadaAsync();
        await PagareAsync(s.PublicId, PromissoryNoteKind.Physical);

        var r = await CrearPagare().Handle(new CreatePromissoryNoteCommand(DuenosDeGarantia.Solicitud, s.PublicId, PromissoryNoteKind.Dematerialized)
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.Error.Code.Should().Be(ErroresDePagare.CodigoYaExiste);
    }

    [Fact]
    public async Task Sin_deposito_configurado_responde_NotConfigured()
    {
        var s = await _d.AprobadaAsync();

        var r = await CrearPagare().Handle(new CreatePromissoryNoteCommand(DuenosDeGarantia.Solicitud, s.PublicId, PromissoryNoteKind.Dematerialized)
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.Error.Code.Should().Be("Core.ExternalChannel.NotConfigured");
        (await _d.Db.PromissoryNotes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Con_el_circuito_abierto_responde_Adapter_Unavailable()
    {
        DepositoConfigurado();
        var s = await _d.AprobadaAsync();
        _circuito = Substitute.For<ICircuitoDeCanalExterno>();
        _circuito.PuedeLlamarAsync(Arg.Any<ExternalChannelKind>(), Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<ExternalOutcome>>>(),
            Arg.Any<CancellationToken>()).Returns(false);

        var r = await CrearPagare().Handle(new CreatePromissoryNoteCommand(DuenosDeGarantia.Solicitud, s.PublicId, PromissoryNoteKind.Dematerialized)
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.Error.Code.Should().Be(ErroresDeOriginacion.CodigoCanalNoDisponible);
        (await _d.Db.PromissoryNotes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Actualizar_lleva_el_estado_del_deposito_y_en_custodia_queda_perfeccionado()
    {
        DepositoConfigurado();
        var s = await _d.AprobadaAsync();
        var p = await PagareAsync(s.PublicId, PromissoryNoteKind.Dematerialized);

        var r = await Actualizado().Handle(new RefreshPromissoryNoteStatusCommand(p.PublicId), default);

        (r.Value.Status, r.Value.Perfected).Should().Be(("InCustody", true));
        var fila = await _d.Db.PromissoryNotes.AsNoTracking().SingleAsync();
        (fila.SignedAt, fila.InCustodyAt, fila.LastSyncedAt).Should().Be((_d.G.UtcNow, _d.G.UtcNow, _d.G.UtcNow));
    }

    [Fact]
    public async Task Un_Rejected_del_simulado_deja_la_firma_rechazada_sin_perfeccionar()
    {
        DepositoConfigurado();
        var ana = await _d.Db.People.SingleAsync(x => x.Id == _d.O.Ana.Id);
        ana.TaxId = "52000011";
        await _d.Db.SaveChangesAsync();
        var s = await _d.AprobadaAsync();
        var p = await PagareAsync(s.PublicId, PromissoryNoteKind.Dematerialized);

        var r = await Actualizado().Handle(new RefreshPromissoryNoteStatusCommand(p.PublicId), default);

        r.IsSuccess.Should().BeTrue();
        (r.Value.Status, r.Value.Perfected, r.Value.SignatureRejected).Should().Be(("PendingSignature", false, true));
    }

    [Fact]
    public async Task La_custodia_fisica_solo_se_registra_en_un_pagare_fisico_con_su_documento()
    {
        DepositoConfigurado();
        var s = await _d.AprobadaAsync();
        var desmaterializado = await PagareAsync(s.PublicId, PromissoryNoteKind.Dematerialized);
        var fila = await _d.Db.PromissoryNotes.SingleAsync();
        fila.Status = PromissoryNoteStatus.Voided;
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
        var fisico = await PagareAsync(s.PublicId, PromissoryNoteKind.Physical);
        var documento = Documento(fisico.PublicId);

        var noFisico = await Custodiar().Handle(new RegisterPhysicalPromissoryNoteCustodyCommand(desmaterializado.PublicId, _d.G.UtcNow, "Bóveda", documento)
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        var sinDocumento = await Custodiar().Handle(new RegisterPhysicalPromissoryNoteCustodyCommand(fisico.PublicId, _d.G.UtcNow, "Bóveda", Guid.NewGuid())
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        var custodiado = await Custodiar().Handle(new RegisterPhysicalPromissoryNoteCustodyCommand(fisico.PublicId, _d.G.UtcNow.AddHours(-2), "Bóveda", documento)
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        noFisico.Error.Code.Should().Be(ErroresDePagare.CodigoEstadoInvalido);
        sinDocumento.Error.Code.Should().Be(ErroresDePagare.CodigoSinDocumento);
        (custodiado.Value.Status, custodiado.Value.Perfected, custodiado.Value.Custody).Should().Be(("InCustody", true, "Bóveda"));
    }

    [Fact]
    public async Task Endosar_va_por_el_adaptador_y_en_produccion_un_simulado_no_se_endosa()
    {
        DepositoConfigurado();
        var s = await _d.AprobadaAsync();
        var p = await PagareAsync(s.PublicId, PromissoryNoteKind.Dematerialized);
        await Actualizado().Handle(new RefreshPromissoryNoteStatusCommand(p.PublicId), default);
        _d.Db.ChangeTracker.Clear();

        _d.O.Opciones.AmbienteDePruebas = false;
        var enProduccion = await Endosar().Handle(new EndorsePromissoryNoteCommand(p.PublicId, _d.O.Beto.PublicId, "Venta de cartera")
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _d.O.Opciones.AmbienteDePruebas = true;
        var endosado = await Endosar().Handle(new EndorsePromissoryNoteCommand(p.PublicId, _d.O.Beto.PublicId, "Venta de cartera")
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        enProduccion.Error.Code.Should().Be(ErroresDeCanalesExternos.CodigoSimuladoEnProduccion);
        endosado.Value.Status.Should().Be("Endorsed");
        var fila = await _d.Db.PromissoryNotes.AsNoTracking().SingleAsync();
        (fila.EndorseeName, fila.EndorsedAt).Should().Be(("Beto Codeudor", _d.G.UtcNow));
    }

    [Fact]
    public async Task Anular_espera_la_cancelacion_en_firme_y_va_por_el_adaptador()
    {
        DepositoConfigurado();
        var s = await _d.AprobadaAsync();
        var p = await PagareAsync(s.PublicId, PromissoryNoteKind.Dematerialized);
        var credito = _d.O.Credito(_d.O.Ana, 3_000_000m, "CR-PAG-1");
        var fila = await _d.Db.PromissoryNotes.SingleAsync();
        fila.AsignarCredito(credito.Id);
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        var vivo = await Anular().Handle(new VoidPromissoryNoteCommand(p.PublicId, "Pagó") { OperationKey = Guid.NewGuid() }, default);
        var c = await _d.Db.Loans.SingleAsync(l => l.Id == credito.Id);
        c.Status = LoanStatus.PaidOff;
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
        var anulado = await Anular().Handle(new VoidPromissoryNoteCommand(p.PublicId, "Cancelado en firme") { OperationKey = Guid.NewGuid() }, default);

        vivo.Error.Code.Should().Be(ErroresDeGarantias.CodigoCreditoNoCancelado);
        anulado.Value.Status.Should().Be("Voided");
        var final = await _d.Db.PromissoryNotes.AsNoTracking().SingleAsync();
        (final.VoidReason, final.ReleasedOn).Should().Be(("Cancelado en firme", DesembolsoDePrueba.Hoy));
        var enElDeposito = await new DepositoDeValoresSimulado(Options.Create(new ExternalChannelsOptions()), _memoria)
            .ConsultarPagareAsync(final.ExternalNoteId!, new ContextoDeCanalExterno(Guid.Parse(_tenant.TenantId!), CodigosDeCanalExterno.Simulado, null, null, "x", true), default);
        enElDeposito.Valor!.Estado.Should().Be(PromissoryNoteStatus.Voided);
    }

    [Fact]
    public async Task La_guardia_en_produccion_nunca_cuenta_un_simulado_como_perfeccionado()
    {
        var simulado = new PromissoryNote
        {
            Kind = PromissoryNoteKind.Dematerialized, Status = PromissoryNoteStatus.InCustody, CustodianChannelCode = CodigosDeCanalExterno.Simulado, IsSimulated = true,
        };
        var marcadoMal = new PromissoryNote
        {
            Kind = PromissoryNoteKind.Dematerialized, Status = PromissoryNoteStatus.InCustody, CustodianChannelCode = CodigosDeCanalExterno.Simulado, IsSimulated = false,
        };
        var real = new PromissoryNote { Kind = PromissoryNoteKind.Dematerialized, Status = PromissoryNoteStatus.InCustody, CustodianChannelCode = "DECEVAL" };
        var fisico = new PromissoryNote { Kind = PromissoryNoteKind.Physical, Status = PromissoryNoteStatus.Signed };
        var sinFirma = new PromissoryNote { Kind = PromissoryNoteKind.Dematerialized, Status = PromissoryNoteStatus.Signed, CustodianChannelCode = "DECEVAL" };

        _d.O.Opciones.AmbienteDePruebas = false;
        var guardia = _d.O.Guardia();

        guardia.PagarePerfeccionado(simulado).Should().BeFalse();
        guardia.PagarePerfeccionado(marcadoMal).Should().BeFalse();
        guardia.EvaluarPagare(simulado).ComoError()!.Code.Should().Be(ErroresDeCanalesExternos.CodigoSimuladoEnProduccion);
        guardia.PagarePerfeccionado(real).Should().BeTrue();
        guardia.PagarePerfeccionado(fisico).Should().BeTrue();
        guardia.PagarePerfeccionado(sinFirma).Should().BeFalse("el desmaterializado se perfecciona en custodia del depósito");
        _d.O.Opciones.AmbienteDePruebas = true;
        _d.O.Guardia().PagarePerfeccionado(simulado).Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------------------- adjuntos --

    private Guid Documento(Guid pagare)
    {
        var a = new Attachment
        {
            OwnerEntityType = Application.Attachments.Common.AdjuntosDeModulo.DocumentoDeGarantia, OwnerEntityPublicId = pagare, FileName = "pagare.pdf",
            ContentType = "application/pdf", SizeBytes = 10, Sha256Hex = new string('b', 64), StoragePath = "x",
            Status = Domain.Enums.Core.EstadoDeAdjunto.Available, CreatedBy = "test",
        };
        _d.Db.Attachments.Add(a);
        _d.Db.SaveChanges();
        return a.PublicId;
    }

    [Fact]
    public async Task Los_documentos_de_una_garantia_se_suben_con_su_permiso_y_se_bloquean_al_perfeccionarse()
    {
        var s = await _d.O.RadicadaAsync();
        var g = await RegistradaAsync(s.PublicId);
        var permisos = Substitute.For<IPermissionChecker>();
        permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<string>() == "Lending.Guarantees.Manage");
        const string tipo = Application.Attachments.Common.AdjuntosDeModulo.DocumentoDeGarantia;

        (await Application.Attachments.Common.AdjuntosDeModulo.PuedeSubirAsync(_d.Db, permisos, tipo, g.PublicId, default)).Should().BeNull();
        (await Application.Attachments.Common.AdjuntosDeModulo.PuedeSubirAsync(_d.Db, permisos, tipo, Guid.NewGuid(), default))!.Code.Should().Be("Generic.NotFound");
        (await Application.Attachments.Common.AdjuntosDeModulo.PuedeBorrarAsync(_d.Db, tipo, g.PublicId, default, permisos)).Should().BeNull();

        await CambiarEstado().Handle(new ChangeCollateralStatusCommand(g.PublicId, CollateralStatus.Valid, "Registrada") { OperationKey = Guid.NewGuid() }, default);

        (await Application.Attachments.Common.AdjuntosDeModulo.PuedeBorrarAsync(_d.Db, tipo, g.PublicId, default, permisos))!.Code
            .Should().Be(Application.Attachments.Common.AttachmentErrorCodes.OwnerLocked);
        permisos.HasPermissionAsync("Lending.Guarantees.Read", Arg.Any<CancellationToken>()).Returns(false);
        (await Application.Attachments.Common.AdjuntosDeModulo.PuedeLeerAsync(permisos, tipo, default)).Should().BeFalse();
    }
}
