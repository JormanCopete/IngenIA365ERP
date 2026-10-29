using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Receipts.Agreements;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Convenios;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Receipts.Agreements;

/// <summary>
/// Feature 015, R2, T0430 (FR-093, FR-218; US16-3): lo informado por el archivo contra lo abonado en la cuenta recaudadora, neto de comisión, IVA y cada retención con su
/// tercero; la diferencia explicada nace como <c>ComisionDeConvenioRegistrada</c>; lo sin explicar sobre la tolerancia (parámetro, nunca literal) exige la aprobación de otra
/// persona; el abono queda identificado como liquidación del convenio. Prueba escrita, sin correr. (nuevo)
/// </summary>
public class ConciliacionDelConvenioTests
{
    [Fact]
    public void La_diferencia_se_explica_con_comision_iva_y_retenciones()
    {
        var e = ConciliacionNeta.Explicar(10_000_000m, 9_985_000m, 10_000m, 1_900m, [new RetencionPracticada("WithholdingTax", 3_100m, null)], 0m);

        e.Diferencia.Should().Be(15_000m);
        e.Explicado.Should().Be(15_000m);
        e.SinExplicar.Should().Be(0m);
        e.ExigeAprobacion.Should().BeFalse();
    }

    [Fact]
    public void Lo_sin_explicar_sobre_la_tolerancia_exige_aprobacion()
    {
        ConciliacionNeta.Explicar(10_000_000m, 9_985_000m, 10_000m, 0m, [], 1_000m).ExigeAprobacion.Should().BeTrue();
        ConciliacionNeta.Explicar(10_000_000m, 9_985_000m, 10_000m, 0m, [], 5_000m).ExigeAprobacion.Should().BeFalse();
    }

    private static async Task<(ConveniosDePrueba P, CollectionFileDto Archivo)> ArchivoCargadoAsync()
    {
        var p = new ConveniosDePrueba();
        var hoy = EscenarioDeRecaudo.Hoy;
        var r = await p.Cargar().Handle(new LoadCollectionFileCommand(p.Convenio.PublicId, "CONVENIO.csv", ConveniosDePrueba.Archivo(
            $"{hoy:yyyy-MM-dd};600000.00;{ConveniosDePrueba.Referencia(ConveniosDePrueba.NumeroA)};S1;;",
            $"{hoy:yyyy-MM-dd};400000.00;{ConveniosDePrueba.Referencia(ConveniosDePrueba.NumeroB)};S2;;")) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        return (p, r.Value.File!);
    }

    [Fact]
    public async Task Explicada_nace_el_hecho_y_el_abono_queda_identificado()
    {
        var (p, archivo) = await ArchivoCargadoAsync();
        var abono = p.B.Abono(998_500m, diasAtras: 0, referencia: "CONVENIO");

        var r = await p.Conciliar().Handle(new ReconcileCollectionFileCommand(p.Convenio.PublicId, archivo.PublicId, abono.PublicId, 1_000m, 190m,
            [new RetencionPedida("WithholdingTax", 310m)]) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Status.Should().Be(AgreementReconciliationState.Explained);
        (await p.B.K.C.E.Db.BankItemIdentifications.SingleAsync(x => x.BankStatementLinePublicId == abono.PublicId)).Outcome
            .Should().Be(BankItemOutcome.AgreementSettlement);
        (await p.B.K.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == ComisionDeConvenioRegistradaV1.Type)).Should().Be(1);
    }

    [Fact]
    public async Task Sobre_la_tolerancia_la_aprueba_otra_persona_y_entonces_nace_el_hecho()
    {
        var (p, archivo) = await ArchivoCargadoAsync();
        p.B.K.C.E.Parametro(ParametrosDeRecaudoYTesoreria.ConveniosToleranciaDeConciliacion, "100");
        var abono = p.B.Abono(990_000m, diasAtras: 0, referencia: "CONVENIO");

        var r = await p.Conciliar().Handle(new ReconcileCollectionFileCommand(p.Convenio.PublicId, archivo.PublicId, abono.PublicId, 1_000m, 190m)
            { OperationKey = Guid.NewGuid() }, default);
        r.Value.Status.Should().Be(AgreementReconciliationState.PendingApproval);
        (await p.B.K.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == ComisionDeConvenioRegistradaV1.Type)).Should().Be(0);

        var propia = await p.Aprobar().Handle(new ApproveAgreementReconciliationCommand(r.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);
        propia.Error.Code.Should().Be("Receipts.AgreementReconciliation.SelfApproval");

        p.B.K.C.E.Como(p.B.K.C.E.Supervisor);
        var aprobada = await p.Aprobar().Handle(new ApproveAgreementReconciliationCommand(r.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);
        aprobada.Value.Status.Should().Be(AgreementReconciliationState.Approved);
        (await p.B.K.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == ComisionDeConvenioRegistradaV1.Type)).Should().Be(1);
    }

    [Fact]
    public async Task El_abono_de_otra_cuenta_no_concilia_el_convenio()
    {
        var (p, archivo) = await ArchivoCargadoAsync();

        var r = await p.Conciliar().Handle(new ReconcileCollectionFileCommand(p.Convenio.PublicId, archivo.PublicId, Guid.NewGuid(), 0m, 0m) { OperationKey = Guid.NewGuid() },
            default);

        r.Error.Code.Should().Be("Receipts.AgreementReconciliation.StatementLineMismatch");
    }
}
