using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.PaymentMeans;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Core.PaymentMeans;

/// <summary>
/// Feature 013, T0119 (A6 «propuesta por defecto: sí», D29; data-model parte 3 §8.2): <c>COR_PaymentMeans.CreditWithoutLending</c>. Sólo en
/// las clases <c>AssociateCredit</c> y <c>CustomerCredit</c> (otra clase → <c>Core.PaymentMeans.Invalid</c> de
/// <see cref="ReglasDeMedioDePago"/>, regla <c>CreditWithoutLendingOnlyForCredit</c>); como la clase, no cambia una vez el medio tiene
/// pagos (<c>Core.PaymentMeans.InUse</c>); y se copia a <c>DocumentPayment.CreditWithoutLending</c> al confirmar.
/// </summary>
public class CreditoSinCarteraTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 7);
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public CreditoSinCarteraTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _reloj.UtcNow.Returns(new DateTime(2026, 10, 7, 15, 0, 0, DateTimeKind.Utc));
    }

    private static PaymentMeansInput CreditoDeServicios(string codigo = "CREDSERV") => new()
    {
        Code = codigo, Name = "Crédito de servicios", Class = PaymentMeansClass.AssociateCredit, DianPaymentMeansCode = "1", ValidFrom = Hoy,
        CreditDefaults = new CreditDefaultsInput(30, 6, 30, "SERVICIOS"), CreditWithoutLending = true,
    };

    private Task<Result<Guid>> CrearAsync(PaymentMeansInput input) =>
        new CreatePaymentMeansCommandHandler(_db, _reloj).Handle(new CreatePaymentMeansCommand(input), default);

    private Task<Result<PaymentMeansDto>> EditarAsync(Guid id, PaymentMeansInput input) =>
        new UpdatePaymentMeansCommandHandler(_db, _reloj).Handle(new UpdatePaymentMeansCommand(id, input, "ajuste del medio"), default);

    private static (string Field, string Rule) CampoYRegla(Error e)
    {
        var data = e.Should().BeOfType<ErrorConDatos>().Subject.Data;
        return ((string)data.GetType().GetProperty("field")!.GetValue(data)!, (string)data.GetType().GetProperty("rule")!.GetValue(data)!);
    }

    [Theory]
    [InlineData(PaymentMeansClass.AssociateCredit)]
    [InlineData(PaymentMeansClass.CustomerCredit)]
    public async Task Un_medio_de_credito_admite_credito_sin_Cartera(PaymentMeansClass clase)
    {
        var r = await CrearAsync(CreditoDeServicios() with { Class = clase });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        (await _db.PaymentMeans.SingleAsync(m => m.PublicId == r.Value)).CreditWithoutLending.Should().BeTrue();
        var leido = await new GetPaymentMeansQueryHandler(_db).Handle(new GetPaymentMeansQuery(r.Value), default);
        leido.Value.CreditWithoutLending.Should().BeTrue();
    }

    [Fact]
    public async Task Otra_clase_no_admite_credito_sin_Cartera()
    {
        var r = await CrearAsync(new PaymentMeansInput
        {
            Code = "EFECTIVO", Name = "Efectivo", Class = PaymentMeansClass.Cash, AllowsChange = true, DianPaymentMeansCode = "10", ValidFrom = Hoy,
            CreditWithoutLending = true,
        });

        r.Error.Code.Should().Be(PaymentMeansErrors.InvalidCode);
        CampoYRegla(r.Error).Should().Be(("creditWithoutLending", ReglasDeMedioDePago.CreditoSinCarteraSoloEnCredito));
    }

    [Fact]
    public async Task Con_pagos_no_cambia_y_sin_pagos_si()
    {
        var id = (await CrearAsync(CreditoDeServicios())).Value;
        var sinPagos = await EditarAsync(id, CreditoDeServicios() with { CreditWithoutLending = false });
        sinPagos.IsSuccess.Should().BeTrue(sinPagos.IsFailure ? sinPagos.Error.Message : string.Empty);

        var medio = await _db.PaymentMeans.SingleAsync(m => m.PublicId == id);
        _db.DocumentPayments.Add(new DocumentPayment
        {
            DocumentId = 900, LineNumber = 1, PaymentMeansId = medio.Id, Amount = 10_000m, MeansCode = medio.Code, MeansName = medio.Name,
            MeansClass = PaymentMeansClass.AssociateCredit, DianPaymentMeansCode = "1",
        });
        await _db.SaveChangesAsync();
        var conPagos = await EditarAsync(id, CreditoDeServicios() with { CreditWithoutLending = true });

        conPagos.Error.Code.Should().Be(PaymentMeansErrors.InUseCode, "como la clase, no cambia una vez el medio tiene pagos");
        (await _db.PaymentMeans.AsNoTracking().SingleAsync(m => m.PublicId == id)).CreditWithoutLending.Should().BeFalse();
    }

    [Fact]
    public void El_pago_lo_copia_del_medio_solo_si_es_de_credito()
    {
        var credito = new Domain.Entities.Core.Payments.PaymentMeans { Code = "CREDSERV", Name = "Servicios", Class = PaymentMeansClass.AssociateCredit, CreditWithoutLending = true };
        var otro = new Domain.Entities.Core.Payments.PaymentMeans { Code = "EFECTIVO", Name = "Efectivo", Class = PaymentMeansClass.Cash, CreditWithoutLending = true };

        var deCredito = new DocumentPayment();
        deCredito.CopiarDelMedio(credito);
        var deContado = new DocumentPayment();
        deContado.CopiarDelMedio(otro);

        deCredito.CreditWithoutLending.Should().BeTrue();
        deCredito.PasaACartera.Should().BeFalse();
        deContado.CreditWithoutLending.Should().BeFalse();
    }

    [Fact]
    public async Task Al_confirmar_el_pago_queda_con_la_copia_del_medio()
    {
        var c = await CreditoDePrueba.CrearAsync();
        c.Db.PaymentMeans.Single(m => m.Id == c.CredAsoc.Id).CreditWithoutLending = true;
        await c.Db.SaveChangesAsync();
        var venta = await c.VentaAsync(c.AsociadoX, total => [c.Credito(c.CredAsoc, total)], c.V.Linea(c.V.P1, 10m));

        (await c.ConfirmarAsync(venta)).IsSuccess.Should().BeTrue();

        c.Db.ChangeTracker.Clear();
        var id = c.V.Documento(venta).Id;
        c.Db.DocumentPayments.AsNoTracking().Single(p => p.DocumentId == id && !p.IsDeleted).CreditWithoutLending.Should().BeTrue();
    }
}
