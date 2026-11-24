using FluentAssertions;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Reports;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Reports;

/// <summary>
/// 2026-11-23 (pedido del dueño, el «solidario»): lo recibido con un concepto de recaudo para terceros se resume por tercero, concepto y documento cruce —lo que se
/// le liquida— con quién pagó; los conceptos varios comunes no entran.
/// </summary>
public sealed class RecaudoParaTercerosTests
{
    private readonly CajaConContratos _k = new();
    private readonly Person _fondo;
    private readonly MiscConcept _solidario;

    public RecaudoParaTercerosTests()
    {
        _fondo = new Person { BusinessName = "Fondo Solidario", TaxId = "900300400" };
        _k.C.E.Db.People.Add(_fondo);
        _k.C.E.Db.SaveChanges();
        _solidario = new MiscConcept
        {
            Code = "SOLID", Name = "Solidario", Direction = MiscConceptDirection.Income, ThirdPartyMode = MiscConceptThirdPartyMode.FixedThirdParty,
            DefaultThirdPartyPersonId = _fondo.Id, RequiresCrossDocument = true, CollectedForThirdParty = true,
        };
        _k.C.E.Db.MiscConcepts.Add(_solidario);
        _k.C.E.Db.SaveChanges();
    }

    private async Task CobrarAsync(MiscConcept c, decimal valor, string? cruce)
    {
        var op = await _k.PreparadaAsync([new ItemPedidoDto(ApplicationTarget.MiscConcept, valor, MiscConceptPublicId: c.PublicId, CrossDocument: cruce)],
            [_k.Efectivo(valor)]);
        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
    }

    [Fact]
    public async Task Se_resume_por_tercero_concepto_y_documento_cruce_con_quien_pago()
    {
        await _k.AbrirAsync();
        await CobrarAsync(_solidario, 20_000m, "SOLID-202611");
        await CobrarAsync(_solidario, 30_000m, "SOLID-202611");
        await CobrarAsync(_solidario, 10_000m, "SOLID-202612");
        await CobrarAsync(_k.Concepto, 5_000m, null);

        var r = await new ListThirdPartyCollectionsQueryHandler(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Reloj)
            .Handle(new ListThirdPartyCollectionsQuery(EscenarioDeRecaudo.Hoy.AddDays(-1), EscenarioDeRecaudo.Hoy), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Summary.Should().HaveCount(2, "los conceptos varios comunes no son recaudo para terceros");
        var noviembre = r.Value.Summary.Single(x => x.CrossDocument == "SOLID-202611");
        (noviembre.ThirdPartyPublicId, noviembre.Receipts, noviembre.Collected).Should().Be((_fondo.PublicId, 2, 50_000m));
        r.Value.Payments.Should().HaveCount(3).And.OnlyContain(p => p.PayerPublicId == _k.Asociado.PublicId && p.ThirdPartyName == "Fondo Solidario");
    }
}
