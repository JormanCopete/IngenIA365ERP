using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Novelties;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Novelties;

/// <summary>
/// El consentimiento de codeudores, avalistas y propietarios de garantías (feature 013, K3, T0862; FR-092, US11-2): se crea pendiente al pedir la
/// novedad; sólo lo registra una parte del crédito y aceptar exige el documento firmado entre los soportes de la novedad; aplicar con uno pendiente no
/// se puede, y quien aplica decide no aplicar o aplicar extinguiendo la garantía si lo que queda cumple la línea. (nuevo)
/// </summary>
public sealed class ConsentimientoDeGarantesTests : IAsyncLifetime
{
    private NovedadesDePrueba _n = null!;

    public async Task InitializeAsync() => _n = await NovedadesDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _n.Dispose();
        return Task.CompletedTask;
    }

    private static NewTermsInput Prorroga => new(Installments: 30);

    /// <summary>Un crédito de Ana con Beto de codeudor, desembolsado por el flujo real.</summary>
    private async Task<Loan> ConCodeudorAsync()
    {
        _n.G.ActuarComo(_n.D.O.Asesora);
        var credito = await _n.P.C.DesembolsarAsync(_n.D.O.Solicitud(2_000_000m, 24, _n.D.O.Ana, _n.D.O.Beto));
        _n.Db.ChangeTracker.Clear();
        (await _n.Db.LoanParties.AnyAsync(p => p.LoanId == credito.Id && p.PersonId == _n.D.O.Beto.Id)).Should().BeTrue();
        return credito;
    }

    private async Task<Attachment> SoporteAsync(Guid novedad)
    {
        var a = new Attachment
        {
            OwnerEntityType = AdjuntosDeModulo.ConsentimientoDeNovedad, OwnerEntityPublicId = novedad, FileName = "otrosi.pdf", ContentType = "application/pdf",
            SizeBytes = 10, Sha256Hex = new string('b', 64), StoragePath = "x", Status = Domain.Enums.Core.EstadoDeAdjunto.Available, CreatedBy = "test",
        };
        _n.Db.Attachments.Add(a);
        await _n.Db.SaveChangesAsync();
        _n.Db.ChangeTracker.Clear();
        return a;
    }

    private async Task<Application.Common.Models.Result<NoveltyConsentDto>> ConsentirAsync(Guid novedad, Guid persona, bool acepta, Guid? adjunto)
    {
        _n.G.ActuarComo(_n.D.O.Asesora);
        var r = await _n.Consentir().Handle(new RegisterNoveltyConsentCommand(novedad, persona, acepta, adjunto) { OperationKey = Guid.NewGuid() }, default);
        _n.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task Pedirla_crea_el_consentimiento_pendiente_de_cada_codeudor()
    {
        var credito = await ConCodeudorAsync();

        var r = await _n.PedirAsync(NoveltyMechanism.Extension, Prorroga, NoveltyRequestMotive.Hardship, credito);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Consents.Should().ContainSingle();
        (r.Value.Consents[0].Party.PersonPublicId, r.Value.Consents[0].Role, r.Value.Consents[0].Status)
            .Should().Be((_n.D.O.Beto.PublicId, ConsentPartyRole.CoDebtor, ConsentStatus.Pending));
    }

    [Fact]
    public async Task Una_persona_ajena_no_consiente_y_aceptar_exige_el_documento_firmado()
    {
        var credito = await ConCodeudorAsync();
        var pedida = (await _n.PedirAsync(NoveltyMechanism.Extension, Prorroga, NoveltyRequestMotive.Hardship, credito)).Value;

        var ajena = await ConsentirAsync(pedida.PublicId, _n.D.O.Carla.PublicId, true, null);
        ajena.Error.Code.Should().Be(ErroresDeNovedades.CodigoParteDesconocida);

        var sinDocumento = await ConsentirAsync(pedida.PublicId, _n.D.O.Beto.PublicId, true, null);
        sinDocumento.Error.Code.Should().Be(LoanNoveltyConsent.CodigoSinAdjunto);

        var ajeno = await ConsentirAsync(pedida.PublicId, _n.D.O.Beto.PublicId, true, Guid.NewGuid());
        ajeno.Error.Code.Should().Be(LoanNoveltyConsent.CodigoSinAdjunto, "el documento tiene que estar entre los soportes de la novedad");

        var soporte = await SoporteAsync(pedida.PublicId);
        var aceptado = await ConsentirAsync(pedida.PublicId, _n.D.O.Beto.PublicId, true, soporte.PublicId);
        aceptado.IsSuccess.Should().BeTrue(aceptado.IsFailure ? aceptado.Error.Message : string.Empty);
        (aceptado.Value.Status, aceptado.Value.AttachmentPublicId).Should().Be((ConsentStatus.Accepted, (Guid?)soporte.PublicId));
    }

    [Fact]
    public async Task Con_un_consentimiento_pendiente_no_se_aplica_y_dice_quien_falta()
    {
        var credito = await ConCodeudorAsync();
        var aprobada = await _n.AprobadaAsync(NoveltyMechanism.Extension, Prorroga, NoveltyRequestMotive.Hardship, credito);

        var r = await _n.AplicarAsync(aprobada.PublicId);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeNovedades.CodigoConsentimientoFaltante);
        NovedadesDePrueba.Datos(r.Error).GetProperty("parties")[0].GetProperty("personPublicId").GetGuid().Should().Be(_n.D.O.Beto.PublicId);
    }

    [Fact]
    public async Task Sin_consentimiento_y_decidiendo_no_aplicar_la_novedad_queda_anulada_sin_tocar_el_credito()
    {
        var credito = await ConCodeudorAsync();
        var aprobada = await _n.AprobadaAsync(NoveltyMechanism.Extension, Prorroga, NoveltyRequestMotive.Hardship, credito);

        var r = await _n.AplicarAsync(aprobada.PublicId, [new WithoutConsentInput(_n.D.O.Beto.PublicId, ConsentRefusalResolution.DoNotApply, "El codeudor no firma")]);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.OperationPublicId.Should().BeNull();
        var novedad = await _n.Db.LoanNovelties.AsNoTracking().SingleAsync();
        novedad.Status.Should().Be(LoanNoveltyStatus.Cancelled);
        var consentimiento = await _n.Db.LoanNoveltyConsents.AsNoTracking().SingleAsync();
        (consentimiento.Status, consentimiento.RefusalResolution).Should().Be((ConsentStatus.Refused, (ConsentRefusalResolution?)ConsentRefusalResolution.DoNotApply));
        (await _n.Db.LoanOperations.CountAsync(o => o.Kind == LoanOperationKind.TermsChange)).Should().Be(0);
    }

    [Fact]
    public async Task Aplicar_extinguiendo_al_codeudor_lo_saca_del_credito_si_la_linea_no_lo_exige()
    {
        var credito = await ConCodeudorAsync();
        var aprobada = await _n.AprobadaAsync(NoveltyMechanism.Extension, Prorroga, NoveltyRequestMotive.Hardship, credito);

        var r = await _n.AplicarAsync(aprobada.PublicId,
            [new WithoutConsentInput(_n.D.O.Beto.PublicId, ConsentRefusalResolution.ApplyWithGuaranteeExtinguished, "El codeudor no firma")]);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.OperationPublicId.Should().NotBeNull();
        (await _n.Db.LoanParties.AsNoTracking().SingleAsync(p => p.LoanId == credito.Id && p.PersonId == _n.D.O.Beto.Id)).ExtinguishedOn.Should().Be(_n.G.HoyLocal);
        (await _n.Db.LoanNoveltyConsents.AsNoTracking().SingleAsync()).RefusalResolution.Should().Be(ConsentRefusalResolution.ApplyWithGuaranteeExtinguished);
    }

    [Fact]
    public async Task Si_la_linea_exige_el_codeudor_extinguirlo_no_alcanza_la_cobertura()
    {
        await _n.D.VersionDesdeMananaAsync(new CollateralRuleInput(AmountFrom: 1m, AmountTo: 30_000_000m, TermFrom: 1, CoDebtors: 1));
        var credito = await ConCodeudorAsync();
        var aprobada = await _n.AprobadaAsync(NoveltyMechanism.Extension, Prorroga, NoveltyRequestMotive.Hardship, credito);

        var r = await _n.AplicarAsync(aprobada.PublicId,
            [new WithoutConsentInput(_n.D.O.Beto.PublicId, ConsentRefusalResolution.ApplyWithGuaranteeExtinguished, "El codeudor no firma")]);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeNovedades.CodigoCoberturaInsuficiente);
        var datos = NovedadesDePrueba.Datos(r.Error);
        (datos.GetProperty("required").GetDecimal(), datos.GetProperty("remaining").GetDecimal()).Should().Be((1m, 0m));
    }
}
