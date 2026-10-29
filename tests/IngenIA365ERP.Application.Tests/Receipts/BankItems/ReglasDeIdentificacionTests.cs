using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Identificacion;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.BankItems;

/// <summary>
/// Feature 015, R1, T0376 (FR-067; D15; SC-008): las reglas de identificación.
/// <list type="bullet">
/// <item>los criterios <c>PaymentReference</c>, <c>DepositorTaxId</c>, <c>ReportedDeposit</c> y <c>ExactDueAmount</c> se recorren por prioridad y cada uno propone sólo con
/// un único candidato (la ambigüedad pasa a la siguiente);</item>
/// <item>la propuesta la confirma una persona: proponer no escribe nada;</item>
/// <item>con «aplicar al cargar» (regla marcada y parámetro encendido) y referencia exacta, lo propuesto se aplica al cargar el extracto a nombre de quien carga; con el
/// parámetro apagado, nada se aplica;</item>
/// <item>ninguna tarea programada aplica (la de antigüedad sólo avisa).</item>
/// </list>
/// Prueba escrita, sin correr.
/// </summary>
public sealed class ReglasDeIdentificacionTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private static ReglaDeIdentificacion R(int id, IdentificationCriterion c, int prioridad, bool alCargar = false) => new(id, c, prioridad, alCargar);

    private static AbonoAIdentificar Abono(decimal valor = 100_000m, string? referencia = "CR-1", IReadOnlyList<CandidatoPorReferencia>? porReferencia = null,
        IReadOnlyList<Guid>? consignantes = null, IReadOnlyList<CandidatoReportado>? reportadas = null, IReadOnlyList<CandidatoExigible>? exigibles = null) =>
        new(valor, Hoy, referencia, porReferencia ?? [], consignantes ?? [], reportadas ?? [], exigibles ?? [], 5);

    [Fact]
    public void Se_recorren_por_prioridad_y_la_ambiguedad_pasa_a_la_siguiente()
    {
        var persona = Guid.NewGuid();
        var credito = Guid.NewGuid();
        var reglas = new[] { R(1, IdentificationCriterion.DepositorTaxId, 2), R(2, IdentificationCriterion.PaymentReference, 1) };

        var porReferencia = MotorDeIdentificacion.Proponer(Abono(porReferencia: [new CandidatoPorReferencia(persona, credito, ApplicationTarget.Loan, true)],
            consignantes: [Guid.NewGuid()]), reglas, false);
        porReferencia!.Criterio.Should().Be(IdentificationCriterion.PaymentReference, "la de menor prioridad va primero");
        porReferencia.ProductoPublicId.Should().Be(credito);

        var ambiguo = MotorDeIdentificacion.Proponer(Abono(porReferencia:
        [
            new CandidatoPorReferencia(persona, credito, ApplicationTarget.Loan, false), new CandidatoPorReferencia(Guid.NewGuid(), Guid.NewGuid(), ApplicationTarget.Account, false),
        ], consignantes: [persona]), reglas, false);
        ambiguo!.Criterio.Should().Be(IdentificationCriterion.DepositorTaxId);
        ambiguo.ProductoPublicId.Should().BeNull("el documento propone la persona; el producto lo elige quien confirma");
    }

    [Fact]
    public void Solo_aplica_al_cargar_la_referencia_exacta_con_la_regla_marcada_y_el_parametro()
    {
        var c = new CandidatoPorReferencia(Guid.NewGuid(), Guid.NewGuid(), ApplicationTarget.Loan, Exacta: true);
        MotorDeIdentificacion.Proponer(Abono(porReferencia: [c]), [R(1, IdentificationCriterion.PaymentReference, 1, alCargar: true)], true)!.AplicarAlCargar.Should().BeTrue();
        MotorDeIdentificacion.Proponer(Abono(porReferencia: [c]), [R(1, IdentificationCriterion.PaymentReference, 1, alCargar: true)], false)!.AplicarAlCargar.Should().BeFalse();
        MotorDeIdentificacion.Proponer(Abono(porReferencia: [c]), [R(1, IdentificationCriterion.PaymentReference, 1)], true)!.AplicarAlCargar.Should().BeFalse();
        MotorDeIdentificacion.Proponer(Abono(porReferencia: [c with { Exacta = false }]), [R(1, IdentificationCriterion.PaymentReference, 1, alCargar: true)], true)!
            .AplicarAlCargar.Should().BeFalse();
    }

    [Fact]
    public void La_consignacion_reportada_y_el_valor_exacto_proponen_con_un_unico_candidato()
    {
        var persona = Guid.NewGuid();
        var reportada = new CandidatoReportado(7, persona, Guid.NewGuid(), ApplicationTarget.Account, 100_000m, Hoy.AddDays(-2), null);
        MotorDeIdentificacion.Proponer(Abono(reportadas: [reportada]), [R(1, IdentificationCriterion.ReportedDeposit, 1)], true)!.ReportadaId.Should().Be(7);
        MotorDeIdentificacion.Proponer(Abono(reportadas: [reportada with { Fecha = Hoy.AddDays(-9) }]), [R(1, IdentificationCriterion.ReportedDeposit, 1)], true)
            .Should().BeNull("fuera de la ventana de días");
        MotorDeIdentificacion.Proponer(Abono(reportadas: [reportada with { Valor = 99_000m }]), [R(1, IdentificationCriterion.ReportedDeposit, 1)], true).Should().BeNull();

        var exigible = new CandidatoExigible(persona, Guid.NewGuid(), ApplicationTarget.Loan, 100_000m);
        MotorDeIdentificacion.Proponer(Abono(exigibles: [exigible]), [R(1, IdentificationCriterion.ExactDueAmount, 1)], true)!.ProductoPublicId.Should().Be(exigible.ProductoPublicId);
        MotorDeIdentificacion.Proponer(Abono(exigibles: [exigible, exigible with { ProductoPublicId = Guid.NewGuid() }]), [R(1, IdentificationCriterion.ExactDueAmount, 1)], true)
            .Should().BeNull("dos exigibles del mismo valor: decide una persona");
    }

    [Fact]
    public async Task Proponer_no_escribe_y_al_cargar_aplica_solo_con_el_parametro_encendido()
    {
        var b = new BancosDePrueba();
        var busqueda = Substitute.For<IBusquedaDeObligaciones>();
        busqueda.PorNumeroAsync(Arg.Any<BusquedaPorNumero>(), Arg.Any<CancellationToken>()).Returns(ci =>
            ci.Arg<BusquedaPorNumero>().Number == "CR-0001"
                ? [new ObligacionEncontradaDto(ClaseDeObligacionEncontrada.Loan, b.K.CreditoA, "CR-0001", b.K.Asociado.PublicId, "Ana Asociada", "2001", "Active")]
                : (IReadOnlyList<ObligacionEncontradaDto>)[]);
        b.K.C.E.Db.IdentificationRules.Add(new IdentificationRule
        {
            Code = "REF", Name = "Por referencia", Criterion = IdentificationCriterion.PaymentReference, Priority = 1, ApplyOnLoad = true,
        });
        await b.K.C.E.Db.SaveChangesAsync();
        var abono = b.Abono(150_000m, referencia: "CR-0001");
        var propuestas = new PropuestasDeIdentificacion(b.K.C.E.Db, busqueda, b.K.Obligaciones, b.K.C.E.Parametros);
        b.K.C.E.Como(b.K.C.E.Supervisor);

        var p = await new GetBankItemProposalQueryHandler(b.K.C.E.Db, b.K.C.E.Alcance, b.Movimientos, propuestas, b.K.C.E.Reloj)
            .Handle(new GetBankItemProposalQuery(abono.PublicId), default);
        p.Value!.ProductPublicId.Should().Be(b.K.CreditoA);
        p.Value.ApplyOnLoad.Should().BeFalse("el parámetro Identificacion.AplicarAlCargar está apagado por defecto");
        (await b.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(0, "proponer no aplica");

        // Un pedido nuevo, piezas nuevas: ParametrosDeCaja recuerda lo leído durante el pedido (en la API es de ámbito), así que no se reusa la de la propuesta.
        ApplyIdentificationRulesOnLoadCommandHandler AlCargar() => new(b.K.C.E.Db, b.K.C.E.Alcance, b.K.C.E.Actor, b.Movimientos,
            new PropuestasDeIdentificacion(b.K.C.E.Db, busqueda, b.K.Obligaciones, b.K.C.E.Parametros), b.K.C.E.Parametros,
            b.Aplicacion, b.Partidas, b.Auditoria, b.K.C.E.Reloj);
        var apagado = await AlCargar().Handle(new ApplyIdentificationRulesOnLoadCommand(b.Cuenta.PublicId, b.Extracto.Number), default);
        apagado.Value.Enabled.Should().BeFalse();
        (await b.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(0);

        b.K.C.E.Parametro(ParametrosDeRecaudoYTesoreria.IdentificacionAplicarAlCargar, "true");
        var encendido = await AlCargar().Handle(new ApplyIdentificationRulesOnLoadCommand(b.Cuenta.PublicId, b.Extracto.Number), default);
        encendido.Value.Applied.Should().Be(1);
        var identificacion = await b.K.C.E.Db.BankItemIdentifications.SingleAsync();
        identificacion.IdentifiedByUserId.Should().Be(b.K.C.E.Supervisor.Id, "a nombre de quien carga");
        identificacion.RuleId.Should().NotBeNull();
    }
}
