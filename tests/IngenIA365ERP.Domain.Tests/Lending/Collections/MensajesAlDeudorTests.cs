using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Domain.Tests.Lending.Collections;

/// <summary>
/// Feature 013, T0305 (data-model §26.1–§26.2; adaptadores-y-archivos.md §4.2; C57): el mensaje al deudor nace sellado y no cambia; su
/// historia es de sólo inserción con las transiciones del contrato, motivos en <c>Deferred</c>/<c>Excluded</c>, instante de salida en
/// <c>Queued</c>/<c>Deferred</c> y la dirección copiada en <c>Sent</c>/<c>Simulated</c>.
/// </summary>
public class MensajesAlDeudorTests
{
    private static readonly DateTime Ahora = new(2026, 10, 8, 15, 0, 0, DateTimeKind.Utc);

    private static BorradorDeMensajeAlDeudor Mensaje(ContactRole destinatario = ContactRole.Debtor) =>
        new(70, destinatario, 500, DebtorMessagePurpose.Informative, MessageChannel.Email, "SIMULADO",
            "Se aplicó su abono de $ 100.000 al crédito 000123.", "PaymentApplied:op-1:Informative:2026-10-08", "PaymentApplied",
            Ahora, ActorKind.Person, 7)
        { TemplateId = 3, TemplateVersion = 2, Subject = "Abono aplicado" };

    [Fact]
    public void Nace_sellado_con_la_huella_de_su_cuerpo()
    {
        var m = DebtorMessage.Nacer(Mensaje());

        m.BodySha256.Should().Be(HuellaDeCartera.DeTexto(m.RenderedBody)).And.HaveLength(64);
        m.ChannelCode.Should().Be("SIMULADO");
        m.TemplateVersion.Should().Be(2);
        m.CuerpoIntacto().Should().BeTrue();
    }

    [Fact]
    public void Solo_va_al_deudor_o_a_un_codeudor_obligado()
    {
        DebtorMessage.Validar(Mensaje(ContactRole.CoDebtor)).Should().BeNull();
        DebtorMessage.Validar(Mensaje(ContactRole.Paymaster))!.Codigo.Should().Be("Lending.DebtorMessage.RecipientNotObligated");
        ((Action)(() => DebtorMessage.Nacer(Mensaje(ContactRole.Paymaster)))).Should().Throw<ReglaDeCarteraException>();
    }

    [Fact]
    public void La_forma_del_sello_se_exige()
    {
        ((Action)(() => DebtorMessage.Nacer(Mensaje() with { LoanId = null }))).Should().Throw<ArgumentException>("sólo la entrega de un documento general va sin crédito");
        DebtorMessage.Validar(Mensaje() with { LoanId = null, Purpose = DebtorMessagePurpose.DocumentDelivery }).Should().BeNull();
        ((Action)(() => DebtorMessage.Nacer(Mensaje() with { OriginKind = "Telepatia" }))).Should().Throw<ArgumentException>();
        ((Action)(() => DebtorMessage.Nacer(Mensaje() with { RequestedByKind = ActorKind.Process }))).Should().Throw<ArgumentException>();
        ((Action)(() => DebtorMessage.Nacer(Mensaje() with { IdempotencyKey = new string('k', 201) }))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Nace_diferido_con_motivos_e_instante_y_despues_sale_con_la_direccion_copiada()
    {
        var diferido = DebtorMessageEvent.Registrar(9, null,
            new EventoDeMensaje(DebtorMessageEventKind.Deferred, Ahora, ActorKind.Person, 7)
            {
                DueAt = Ahora.AddHours(15),
                Motivos = [ContactWindowReason.Sunday, ContactWindowReason.OutsideHours, ContactWindowReason.Sunday],
            }, enProduccion: true);

        diferido.Sequence.Should().Be(1);
        diferido.ReasonCode.Should().Be("Sunday,OutsideHours");
        diferido.Motivos().Should().Equal(ContactWindowReason.Sunday, ContactWindowReason.OutsideHours);
        diferido.DueAt.Should().Be(Ahora.AddHours(15));

        var enviado = DebtorMessageEvent.Registrar(9, diferido,
            new EventoDeMensaje(DebtorMessageEventKind.Simulated, Ahora.AddHours(15), ActorKind.Process)
            { DestinationAddress = "deudor@correo.co", AttemptNumber = 1 }, enProduccion: true);

        enviado.Sequence.Should().Be(2);
        enviado.DestinationAddress.Should().Be("deudor@correo.co");
        enviado.AttemptNumber.Should().Be(1);
    }

    [Theory]
    [InlineData(null, DebtorMessageEventKind.Sent, false)]
    [InlineData(null, DebtorMessageEventKind.Queued, true)]
    [InlineData(DebtorMessageEventKind.Queued, DebtorMessageEventKind.Cancelled, true)]
    [InlineData(DebtorMessageEventKind.Deferred, DebtorMessageEventKind.Deferred, true)]
    [InlineData(DebtorMessageEventKind.Queued, DebtorMessageEventKind.Delivered, false)]
    [InlineData(DebtorMessageEventKind.Sent, DebtorMessageEventKind.Delivered, true)]
    [InlineData(DebtorMessageEventKind.Sent, DebtorMessageEventKind.Cancelled, false)]
    [InlineData(DebtorMessageEventKind.Excluded, DebtorMessageEventKind.Queued, false)]
    [InlineData(DebtorMessageEventKind.Failed, DebtorMessageEventKind.Sent, false)]
    [InlineData(DebtorMessageEventKind.Delivered, DebtorMessageEventKind.Failed, false)]
    public void Las_transiciones_son_las_del_contrato(DebtorMessageEventKind? anterior, DebtorMessageEventKind siguiente, bool admitida)
    {
        DebtorMessageEvent.PuedeSeguir(anterior, siguiente, enProduccion: true).Should().Be(admitida);
    }

    [Fact]
    public void Simulado_es_final_en_produccion_y_no_fuera()
    {
        DebtorMessageEvent.PuedeSeguir(DebtorMessageEventKind.Simulated, DebtorMessageEventKind.Delivered, enProduccion: true).Should().BeFalse();
        DebtorMessageEvent.PuedeSeguir(DebtorMessageEventKind.Simulated, DebtorMessageEventKind.Delivered, enProduccion: false).Should().BeTrue();
    }

    [Fact]
    public void Cada_clase_lleva_lo_suyo()
    {
        var sinMotivos = () => DebtorMessageEvent.Registrar(9, null, new EventoDeMensaje(DebtorMessageEventKind.Excluded, Ahora, ActorKind.Process), true);
        sinMotivos.Should().Throw<ArgumentException>();
        var encoladoSinInstante = () => DebtorMessageEvent.Registrar(9, null, new EventoDeMensaje(DebtorMessageEventKind.Queued, Ahora, ActorKind.Process), true);
        encoladoSinInstante.Should().Throw<ArgumentException>();

        var encolado = DebtorMessageEvent.Registrar(9, null, new EventoDeMensaje(DebtorMessageEventKind.Queued, Ahora, ActorKind.Process) { DueAt = Ahora }, true);
        var enviadoSinDireccion = () => DebtorMessageEvent.Registrar(9, encolado, new EventoDeMensaje(DebtorMessageEventKind.Sent, Ahora, ActorKind.Process), true);
        enviadoSinDireccion.Should().Throw<ArgumentException>("C57: la dirección usada queda en el evento");
        var canceladoConDireccion = () => DebtorMessageEvent.Registrar(9, encolado,
            new EventoDeMensaje(DebtorMessageEventKind.Cancelled, Ahora, ActorKind.Person, 7) { DestinationAddress = "x@y.co" }, true);
        canceladoConDireccion.Should().Throw<ArgumentException>();
        var deOtroMensaje = () => DebtorMessageEvent.Registrar(10, encolado,
            new EventoDeMensaje(DebtorMessageEventKind.Cancelled, Ahora, ActorKind.Person, 7), true);
        deOtroMensaje.Should().Throw<ArgumentException>();
        var desdeFinal = () => DebtorMessageEvent.Registrar(9,
            DebtorMessageEvent.Registrar(9, encolado, new EventoDeMensaje(DebtorMessageEventKind.Cancelled, Ahora, ActorKind.Person, 7), true),
            new EventoDeMensaje(DebtorMessageEventKind.Queued, Ahora, ActorKind.Process) { DueAt = Ahora }, true);
        desdeFinal.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Los_motivos_que_no_caben_por_nombre_van_por_numero()
    {
        var todos = Enum.GetValues<ContactWindowReason>();
        var codigo = DebtorMessageEvent.CodigoDeMotivos(todos)!;

        codigo.Length.Should().BeLessThanOrEqualTo(DebtorMessageEvent.LargoDelMotivo);
        codigo.Should().StartWith("1,2,3");
        new DebtorMessageEvent { ReasonCode = codigo }.Motivos().Should().Equal(todos);
    }
}
