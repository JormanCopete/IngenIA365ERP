using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// 2026-11-16 (decisión del dueño: «se debe poder parametrizar cómo funciona cada concepto: si tiene cuenta contable y tercero por defecto, o si el tercero es el
/// mismo cliente que paga»): el concepto vario con su modo de tercero.
/// <list type="bullet">
/// <item>quien paga (defecto): el beneficiario del ítem es el titular, como siempre;</item>
/// <item>tercero fijo (el SOAT a nombre de la aseguradora): se fuerza aunque el ítem traiga otro, y no hace al pagador «tercero» ni pide quién efectúa;</item>
/// <item>lo elige el cajero: el ítem lo trae o se rechaza con <c>Receipts.Receipt.ThirdPartyRequired</c>;</item>
/// <item>la configuración exige el tercero fijo en su modo, que exista, y el valor sugerido entre cero y el máximo; la lista dice el nombre del tercero y la
/// cuenta de la matriz de Recaudo (por el puerto de Contabilidad).</item>
/// </list>
/// </summary>
public sealed class ConceptosVariosConTerceroTests
{
    private readonly CajaConContratos _k = new();
    private readonly Person _aseguradora;
    private readonly Person _otro;
    private readonly MiscConcept _soat;

    public ConceptosVariosConTerceroTests()
    {
        _aseguradora = new Person { BusinessName = "Aseguradora Andina S.A.", TaxId = "900100200" };
        _otro = new Person { FirstName = "Olga", LastName = "Otra", TaxId = "4001" };
        _k.C.E.Db.People.AddRange(_aseguradora, _otro);
        _k.C.E.Db.SaveChanges();
        _soat = new MiscConcept
        {
            Code = "SOAT", Name = "SOAT", Direction = MiscConceptDirection.Income, ThirdPartyMode = MiscConceptThirdPartyMode.FixedThirdParty,
            DefaultThirdPartyPersonId = _aseguradora.Id, SuggestedAmount = 450_000m,
        };
        _k.C.E.Db.MiscConcepts.Add(_soat);
        _k.C.E.Db.SaveChanges();
    }

    private async Task<Guid> OperacionAsync()
    {
        await _k.AbrirAsync();
        return (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value.PublicId;
    }

    private static ItemPedidoDto Concepto(MiscConcept c, decimal valor, Guid? tercero = null) =>
        new(ApplicationTarget.MiscConcept, valor, BeneficiaryPersonPublicId: tercero, MiscConceptPublicId: c.PublicId);

    [Fact]
    public async Task Con_tercero_fijo_el_item_va_a_nombre_del_tercero_del_concepto_aunque_traiga_otro()
    {
        var op = await OperacionAsync();

        var r = await _k.Items().Handle(new UpdateCashOperationItemsCommand(op, [Concepto(_soat, 450_000m, _otro.PublicId)]), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Items.Single().BeneficiaryPersonPublicId.Should().Be(_aseguradora.PublicId);
    }

    [Fact]
    public async Task El_tercero_fijo_es_el_tercero_contable_no_el_titular_no_pide_quien_efectua_ni_enmascara()
    {
        await _k.AbrirAsync();
        var op = await _k.PreparadaAsync([Concepto(_soat, 450_000m)], [_k.Efectivo(450_000m)]);

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.PayerIsThirdParty.Should().BeFalse("quien paga su SOAT no es un tercero que paga lo de otro");
        r.Value.Masked.Should().BeFalse();
        (await _k.C.E.Db.ReceiptApplications.SingleAsync()).BeneficiaryPersonId.Should().Be(_aseguradora.Id, "el tercero contable sale del beneficiario");
    }

    [Fact]
    public async Task Elegido_por_el_cajero_el_tercero_es_obligatorio_y_tiene_que_existir()
    {
        _soat.ThirdPartyMode = MiscConceptThirdPartyMode.ChosenByCashier;
        _soat.DefaultThirdPartyPersonId = null;
        await _k.C.E.Db.SaveChangesAsync();
        var op = await OperacionAsync();

        var sinTercero = await _k.Items().Handle(new UpdateCashOperationItemsCommand(op, [Concepto(_soat, 450_000m)]), default);
        sinTercero.Error.Code.Should().Be(ErroresDeRecaudo.CodigoItemRechazado);
        JsonSerializer.Serialize(((ErrorConDatos)sinTercero.Error).Data).Should().Contain(ErroresDeRecaudo.CodigoTerceroRequerido);
        (await _k.Items().Handle(new UpdateCashOperationItemsCommand(op, [Concepto(_soat, 450_000m, Guid.NewGuid())]), default)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoItemRechazado, "la persona elegida no existe");

        var conTercero = await _k.Items().Handle(new UpdateCashOperationItemsCommand(op, [Concepto(_soat, 450_000m, _otro.PublicId)]), default);
        conTercero.Value.Items.Single().BeneficiaryPersonPublicId.Should().Be(_otro.PublicId);
    }

    [Fact]
    public async Task Con_quien_paga_el_item_va_a_nombre_del_titular_como_siempre()
    {
        var op = await OperacionAsync();

        var r = await _k.Items().Handle(new UpdateCashOperationItemsCommand(op, [Concepto(_k.Concepto, 10_000m)]), default);

        r.Value.Items.Single().BeneficiaryPersonPublicId.Should().Be(_k.Asociado.PublicId);
    }

    [Fact]
    public async Task Lo_que_puede_pagar_dice_como_funciona_el_tercero_de_cada_concepto()
    {
        var r = await _k.Pagables().Handle(new GetPayablesQuery(_k.Asociado.PublicId), default);

        var soat = r.Value.MiscConcepts.Single(c => c.Code == "SOAT");
        soat.ThirdPartyMode.Should().Be(MiscConceptThirdPartyMode.FixedThirdParty);
        soat.DefaultThirdPartyPublicId.Should().Be(_aseguradora.PublicId);
        soat.DefaultThirdPartyName.Should().Be("Aseguradora Andina S.A.");
        soat.SuggestedAmount.Should().Be(450_000m);
        r.Value.MiscConcepts.Single(c => c.Code == "CERT").ThirdPartyMode.Should().Be(MiscConceptThirdPartyMode.Payer);
    }

    // ------------------------------------------------------------------------------------------- configuración --

    [Fact]
    public void El_tercero_fijo_es_obligatorio_en_su_modo_y_el_valor_sugerido_va_de_cero_al_maximo()
    {
        var crear = new CreateMiscConceptCommandValidator();
        crear.Validate(new CreateMiscConceptCommand("RUNT", "Trámite RUNT", MiscConceptDirection.Income, false, false, false, false, null,
            MiscConceptThirdPartyMode.FixedThirdParty)).IsValid.Should().BeFalse("sin el tercero fijo");
        crear.Validate(new CreateMiscConceptCommand("RUNT", "Trámite RUNT", MiscConceptDirection.Income, false, false, false, false, 100_000m,
            MiscConceptThirdPartyMode.ChosenByCashier, SuggestedAmount: 120_000m)).IsValid.Should().BeFalse("el sugerido pasa del máximo");
        crear.Validate(new CreateMiscConceptCommand("RUNT", "Trámite RUNT", MiscConceptDirection.Income, false, false, false, false, null,
            SuggestedAmount: -1m)).IsValid.Should().BeFalse();
        crear.Validate(new CreateMiscConceptCommand("RUNT", "Trámite RUNT", MiscConceptDirection.Income, false, false, false, false, 100_000m,
            MiscConceptThirdPartyMode.FixedThirdParty, Guid.NewGuid(), 80_000m)).IsValid.Should().BeTrue();
        new UpdateMiscConceptCommandValidator().Validate(new UpdateMiscConceptCommand(_soat.PublicId, "SOAT", false, false, false, false, null,
            MiscConceptThirdPartyMode.FixedThirdParty)).IsValid.Should().BeFalse("al editar, igual");
    }

    [Fact]
    public async Task Crear_y_editar_guardan_el_tercero_fijo_que_existe_y_lo_sueltan_al_cambiar_de_modo()
    {
        var crear = new CreateMiscConceptCommandHandler(_k.C.E.Db);
        (await crear.Handle(new CreateMiscConceptCommand("RUNT", "Trámite RUNT", MiscConceptDirection.Income, false, false, false, false, null,
            MiscConceptThirdPartyMode.FixedThirdParty, Guid.NewGuid()), default)).Error.Code.Should().Be(ErroresDeRecaudo.CodigoPersonaNoEncontrada);

        var creado = await crear.Handle(new CreateMiscConceptCommand("RUNT", "Trámite RUNT", MiscConceptDirection.Income, false, false, false, false, null,
            MiscConceptThirdPartyMode.FixedThirdParty, _aseguradora.PublicId, 25_000m), default);
        creado.Value.DefaultThirdPartyPublicId.Should().Be(_aseguradora.PublicId);
        creado.Value.DefaultThirdPartyName.Should().Be("Aseguradora Andina S.A.");
        creado.Value.SuggestedAmount.Should().Be(25_000m);

        var editado = await new UpdateMiscConceptCommandHandler(_k.C.E.Db).Handle(new UpdateMiscConceptCommand(creado.Value.PublicId, "Trámite RUNT", false, false,
            false, false, null, MiscConceptThirdPartyMode.Payer, _aseguradora.PublicId), default);
        editado.Value.ThirdPartyMode.Should().Be(MiscConceptThirdPartyMode.Payer);
        editado.Value.DefaultThirdPartyPublicId.Should().BeNull("sin tercero fijo, no lo guarda");
        (await _k.C.E.Db.MiscConcepts.AsNoTracking().SingleAsync(c => c.Code == "RUNT")).DefaultThirdPartyPersonId.Should().BeNull();
    }

    [Fact]
    public async Task La_lista_dice_la_cuenta_de_la_matriz_por_el_puerto_de_Contabilidad()
    {
        var contabilidad = Substitute.For<IContabilidadParaRecaudo>();
        contabilidad.CuentasDeConceptosVariosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, CuentaDeConceptoVarioDto> { ["SOAT"] = new("SOAT", "24459501", "SOAT por pagar a aseguradoras") });

        var r = await new ListMiscConceptsQueryHandler(_k.C.E.Db, contabilidad, _k.C.E.Reloj).Handle(new ListMiscConceptsQuery(), default);

        var soat = r.Value.Single(c => c.Code == "SOAT");
        soat.AccountCode.Should().Be("24459501");
        soat.AccountName.Should().Be("SOAT por pagar a aseguradoras");
        soat.DefaultThirdPartyName.Should().Be("Aseguradora Andina S.A.");
        r.Value.Single(c => c.Code == "CERT").AccountCode.Should().BeNull("sin regla en la matriz");
        await contabilidad.Received(1).CuentasDeConceptosVariosAsync(Arg.Is<IReadOnlyCollection<string>>(c => c.Contains("SOAT") && c.Contains("CERT")),
            Arg.Is(EscenarioDeRecaudo.Hoy), Arg.Any<CancellationToken>());
    }
}
