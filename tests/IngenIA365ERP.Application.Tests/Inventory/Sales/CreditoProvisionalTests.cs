using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 012, I3, T647 (contracts/api.md §23.2; T32, T33; FR-061, FR-062): el pago a crédito al confirmar. Cuotas y plazo dentro de los
/// máximos del medio; primer y último vencimiento, y el vencimiento del documento = el mayor; la solicitud <c>ProvisionalCredit</c> sobre el
/// pago (<c>DocumentPayment</c>) por lo financiado, sin el cajero ni el creador; nadie con límite suficiente → <c>AmountExceedsLimit</c> con
/// <c>data.maxAmount</c>; la última aprobación deja el pago «pendiente de validar», de origen provisional y con la cuenta por cobrar sellada en
/// Contabilidad aunque el parámetro diga Cartera (IC pendiente); el pago mixto crédito + efectivo suma <c>AmountDue</c>.
/// </summary>
public class CreditoProvisionalTests
{
    private static string Codigo<T>(Result<T> r) => r.IsFailure ? r.Error.Code : "OK";

    /// <summary>Una venta de 120.000 (60 × P1 a 2.000) al asociado X, toda a crédito con CREDASOC.</summary>
    private static Task<Guid> VentaACreditoAsync(CreditoDePrueba c, PaymentCreditInput? condiciones = null) =>
        c.VentaAsync(c.AsociadoX, total => [c.Credito(c.CredAsoc, total, condiciones)], c.V.Linea(c.V.P1, 60m));

    [Fact]
    public async Task La_venta_a_credito_queda_en_aprobacion_sin_numero_con_la_solicitud_del_pago()
    {
        var c = await CreditoDePrueba.CrearAsync();
        var venta = await VentaACreditoAsync(c, new PaymentCreditInput(3, 90, 30));

        var r = await c.ConfirmarAsync(venta);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Status.Should().Be(DocumentStatus.PendingApproval);
        r.Value.Number.Should().BeNull();
        var documento = c.V.Documento(venta);
        documento.Number.Should().BeNull();
        var pago = c.Db.DocumentPayments.AsNoTracking().Single(p => p.DocumentId == documento.Id && !p.IsDeleted);
        var solicitud = c.SolicitudDeCredito(venta);
        solicitud.Subject.Should().Be(ApprovalSubjects.ProvisionalCredit);
        solicitud.SourceType.Should().Be(ApprovalSourceTypes.DocumentPayment);
        solicitud.SourcePublicId.Should().Be(pago.PublicId);
        solicitud.Amount.Should().Be(120_000m);
        solicitud.Status.Should().Be(ApprovalRequestStatus.Pending);
        solicitud.Excluidos().Should().Contain(CreditoDePrueba.Cajera, "la cajera creó la venta y cobra");
        solicitud.NivelesRequeridos().Should().ContainSingle().Which.PermissionCode.Should().Be("Inventory.Sales.SellOnCredit");
        r.Value.Approval!.RequestPublicId.Should().Be(solicitud.PublicId);
    }

    [Fact]
    public async Task Los_vencimientos_salen_de_las_condiciones_y_el_documento_vence_con_el_mayor()
    {
        var c = await CreditoDePrueba.CrearAsync();
        var venta = await VentaACreditoAsync(c, new PaymentCreditInput(3, 90, 30));

        (await c.ConfirmarAsync(venta)).IsSuccess.Should().BeTrue();

        var documento = c.V.Documento(venta);
        var pago = c.Db.DocumentPayments.AsNoTracking().Single(p => p.DocumentId == documento.Id && !p.IsDeleted);
        var hoy = documento.OperationDate;
        (pago.InstallmentCount, pago.CreditTermDays, pago.InstallmentPeriodDays).Should().Be(((short)3, (short)90, (short)30));
        pago.FirstDueDate.Should().Be(hoy.AddDays(30));
        pago.FinalDueDate.Should().Be(hoy.AddDays(90));
        documento.DueDate.Should().Be(hoy.AddDays(90));
    }

    [Fact]
    public async Task Sin_condiciones_se_usan_las_propuestas_del_medio()
    {
        var c = await CreditoDePrueba.CrearAsync();
        var venta = await VentaACreditoAsync(c);

        (await c.ConfirmarAsync(venta)).IsSuccess.Should().BeTrue();

        var documento = c.V.Documento(venta);
        var pago = c.Db.DocumentPayments.AsNoTracking().Single(p => p.DocumentId == documento.Id && !p.IsDeleted);
        (pago.InstallmentCount, pago.CreditTermDays).Should().Be(((short)1, (short)30));
        pago.FirstDueDate.Should().Be(pago.FinalDueDate);
        pago.SuggestedCreditLineCode.Should().Be("CONSUMO");
    }

    [Theory]
    [InlineData(12, 90)]
    [InlineData(3, 120)]
    public async Task Cuotas_o_plazo_por_encima_del_maximo_del_medio_se_rechazan(short cuotas, short plazo)
    {
        var c = await CreditoDePrueba.CrearAsync();
        var venta = await VentaACreditoAsync(c, new PaymentCreditInput(cuotas, plazo, 30));

        var r = await c.ConfirmarAsync(venta);

        Codigo(r).Should().Be(ErroresDeCredito.TermsOutOfRangeCode);
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new { maxInstallments = (short?)6, maxTermDays = (short?)90 }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Al_asociado_retirado_no_se_le_vende_a_credito()
    {
        var c = await CreditoDePrueba.CrearAsync();
        var venta = await c.VentaAsync(c.AsociadoRetiradoZ, total => [c.Credito(c.CredAsoc, total)], c.V.Linea(c.V.P1, 10m));

        var r = await c.ConfirmarAsync(venta);

        Codigo(r).Should().Be(ErroresDeCredito.NotAssociateCode);
    }

    [Fact]
    public async Task Si_nadie_tiene_limite_suficiente_es_AmountExceedsLimit_con_el_maximo()
    {
        var c = await CreditoDePrueba.CrearAsync();
        foreach (var l in c.Db.PermissionAmountLimits.Where(x => x.MaxAmount == 1_000_000m)) l.MaxAmount = 50_000m;
        await c.Db.SaveChangesAsync();
        var venta = await VentaACreditoAsync(c);

        var r = await c.ConfirmarAsync(venta);

        Codigo(r).Should().Be("Inventory.Approval.AmountExceedsLimit");
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new { maxAmount = 100_000m }, o => o.ExcludingMissingMembers());
        c.V.Documento(venta).Status.Should().Be(DocumentStatus.Draft);
    }

    [Fact]
    public async Task Ni_la_cajera_ni_un_cajero_con_limite_menor_aprueban_y_el_supervisor_si()
    {
        var c = await CreditoDePrueba.CrearAsync();
        c.V.Parametro(ParametrosDeInventario.CarteraCuentaPorCobrarRegistradaPor, "Cartera");
        var venta = await VentaACreditoAsync(c, new PaymentCreditInput(3, 90, 30));
        (await c.ConfirmarAsync(venta)).IsSuccess.Should().BeTrue();
        var solicitud = c.SolicitudDeCredito(venta);

        var propia = await c.AprobarAsync(solicitud, CreditoDePrueba.Cajera);
        var corta = await c.AprobarAsync(solicitud, CreditoDePrueba.Cajero2);
        var supervisor = await c.AprobarAsync(solicitud, CreditoDePrueba.Supervisor);

        propia.IsFailure.Should().BeTrue("quien crea y cobra nunca aprueba su crédito");
        Codigo(corta).Should().Be("Inventory.Approval.AmountExceedsLimit");
        supervisor.IsSuccess.Should().BeTrue(supervisor.IsFailure ? supervisor.Error.Message : string.Empty);
        c.Db.ChangeTracker.Clear();
        var documento = c.V.Documento(venta);
        documento.Status.Should().Be(DocumentStatus.Confirmed);
        documento.Number.Should().NotBeNull();
        var pago = c.Db.DocumentPayments.AsNoTracking().Single(p => p.DocumentId == documento.Id && !p.IsDeleted);
        pago.PendingValidation.Should().BeTrue();
        pago.CreditOrigin.Should().Be(CreditOrigin.ProvisionalCredit);
        pago.AccountsReceivableRecordedBy.Should().Be(CreditoEnLaVenta.Contabilidad, "mientras Cartera.IntegracionHabilitadaDesde esté vacío se fuerza Contabilidad");
        pago.ApprovalRequestId.Should().Be(solicitud.Id);
    }

    [Fact]
    public async Task El_pago_mixto_credito_mas_efectivo_suma_el_valor_a_pagar()
    {
        var c = await CreditoDePrueba.CrearAsync();
        var mal = await c.VentaAsync(c.AsociadoX, total => [c.Credito(c.CredAsoc, 50_000m), c.V.Pago(c.V.Efectivo, total - 60_000m)], c.V.Linea(c.V.P1, 60m));
        var bien = await c.VentaAsync(c.AsociadoX, total => [c.Credito(c.CredAsoc, 50_000m), c.V.Pago(c.V.Efectivo, total - 50_000m)], c.V.Linea(c.V.P1, 60m));

        var descuadrada = await c.ConfirmarAsync(mal);
        var cuadrada = await c.ConfirmarAsync(bien);

        Codigo(descuadrada).Should().Be("Payments.TotalMismatch");
        cuadrada.Value.Status.Should().Be(DocumentStatus.PendingApproval);
        c.SolicitudDeCredito(bien).Amount.Should().Be(50_000m, "se aprueba sólo lo financiado");
        var aprobada = await c.AprobarAsync(c.SolicitudDeCredito(bien), CreditoDePrueba.Supervisor);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? aprobada.Error.Message : string.Empty);
        c.Db.ChangeTracker.Clear();
        c.V.Documento(bien).Status.Should().Be(DocumentStatus.Confirmed);
    }
}
