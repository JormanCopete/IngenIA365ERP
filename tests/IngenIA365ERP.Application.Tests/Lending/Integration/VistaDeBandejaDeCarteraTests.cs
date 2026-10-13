using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US7 (bloque B; pendiente de T0624; T0075, contabilidad.md §10.1): la bandeja vista desde Cartera. «Enviar lo que no pasaba»
/// (<see cref="SendNotApplicableMessagesCommand"/> con <c>Modulo = CAR</c>) se niega mientras el tipo siga en <c>NoPasa</c> y, cambiado el modo,
/// ordena el lote <c>SendNotApplicable</c> con los hechos de Cartera, que conservan su fecha contable original; la vista filtra por las oficinas
/// del alcance. Sobre el contingente de una aprobación (nace <c>NoPasa</c>).
/// </summary>
public class VistaDeBandejaDeCarteraTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private static readonly DateOnly Hoy = DesembolsoDePrueba.Hoy;

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        _d.Db.LendingFactTypes.Add(new LendingFactType
        {
            Code = CreditoAprobadoSinDesembolsarV1.Type, ShortCode = "CAD", Name = "Aprobado", DefaultMode = "NoPasa", CreatedBy = "test",
        });
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private VistaDeBandejaDeCartera Vista(AlcanceDeCartera alcance)
    {
        var porAlcance = Substitute.For<IAlcanceDeCartera>();
        porAlcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(alcance);
        return new VistaDeBandejaDeCartera(_d.Db, porAlcance, new ModoDePasoDeCartera(_d.Db, _d.Contabilidad, new LectorDeParametros(_d.Db), _d.G.Reloj));
    }

    private async Task<Domain.Entities.Integration.Transactions.IntegrationMessage> HechoNoPasaAsync()
    {
        var s = await _d.AprobadaAsync();
        var solicitud = await _d.Db.CreditApplications.SingleAsync(x => x.PublicId == s.PublicId);
        (await new MovimientoContingenteDeCartera(_d.Db, _d.Emision(), _d.FechaContable(), _d.G.Reloj).SolicitudAsync(solicitud, Hoy, default))
            .Value.Should().NotBeNull();
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
        return await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CreditoAprobadoSinDesembolsarV1.Type);
    }

    private SendNotApplicableMessagesCommandHandler Enviar() => new(_d.Db, _d.G.Actor, _d.G.Reloj, [Vista(AlcanceDeCartera.Total)]);

    private static SendNotApplicableMessagesCommand Pedido(Guid corte) =>
        new(Hoy.AddDays(-30), Hoy.AddDays(30), null, corte, "La contadora decidió que el contingente pase", ModulosDeOrigen.Cartera)
        { OperationKey = Guid.NewGuid() };

    [Fact]
    public async Task Mientras_el_tipo_siga_en_NoPasa_no_se_envia()
    {
        var hecho = await HechoNoPasaAsync();

        var r = await Enviar().Handle(Pedido(hecho.PublicId), default);

        r.Error.Code.Should().Be("Integration.SendNotApplicable.ModeStillNotPosted");
    }

    [Fact]
    public async Task Cambiado_el_modo_ordena_el_lote_con_la_fecha_contable_original()
    {
        var hecho = await HechoNoPasaAsync();
        _d.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.ContabilidadModoDePaso, Value = "EnLinea", ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "pasan", ScopeKind = ParameterScopeKind.LendingFactType, ScopeId = _d.Db.LendingFactTypes.Single().Id, CreatedBy = "test",
        });
        await _d.Db.SaveChangesAsync();

        var r = await Enviar().Handle(Pedido(hecho.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : string.Empty);
        (await _d.Db.IntegrationBatches.AsNoTracking().SingleAsync()).MessageCount.Should().Be(1);
        var lote = await _d.Db.IntegrationBatches.AsNoTracking().SingleAsync();
        lote.Trigger.Should().Be(BatchTrigger.SendNotApplicable);
        (await _d.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(x => x.MessageId == hecho.Id)).BatchId.Should().Be(lote.Id);
        (await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Id == hecho.Id)).OperationDate.Should().Be(hecho.OperationDate,
            "lo que no pasaba se envía con su fecha contable original");
    }

    [Fact]
    public async Task La_vista_filtra_por_las_oficinas_del_alcance()
    {
        var hecho = await HechoNoPasaAsync();

        (await (await Vista(AlcanceDeCartera.Total).VisiblesAsync(default)).AnyAsync(d => d.MessageId == hecho.Id)).Should().BeTrue();
        (await (await Vista(AlcanceDeCartera.Vacio).VisiblesAsync(default)).AnyAsync(d => d.MessageId == hecho.Id)).Should().BeFalse();
        Vista(AlcanceDeCartera.Total).CodigoDeTipo(null, CreditoAprobadoSinDesembolsarV1.Type).Should().Be(CreditoAprobadoSinDesembolsarV1.Type);
        Vista(AlcanceDeCartera.Total).RutaDelLoteResumido(Guid.Empty).Should().StartWith("/contabilidad/cartera/lotes/");
    }
}
