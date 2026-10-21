using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Documents;

namespace IngenIA365ERP.Domain.Tests.Lending.Documents;

/// <summary>
/// Feature 013, US13, T0764 (T80; adaptadores-y-archivos.md §8.3, §8.8): un reverso anula todo documento que cubra el crédito con
/// <c>InformationCutoff</c> igual o posterior al registro de lo reversado y deja los anteriores; un documento ya anulado no se anula dos veces; el
/// estado calculado (<c>Voided</c> con evento de anulación; <c>SubjectToFinality</c> mientras hoy &lt; <c>FinalityUntil</c>; <c>Valid</c> en otro caso;
/// el paso a vigente lo da la fecha). Y los eventos nacen con lo que su clase exige (T0776).
/// </summary>
public class VigenciaDeDocumentosTests
{
    private const int Credito = 7;
    private static readonly DateTime RegistroDelPago = new(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Un_reverso_anula_lo_emitido_desde_el_registro_de_lo_reversado_y_deja_lo_anterior()
    {
        DocumentoQueCubre[] documentos =
        [
            new(1, [Credito], RegistroDelPago.AddMinutes(-1), false),
            new(2, [Credito], RegistroDelPago, false),
            new(3, [Credito, 9], RegistroDelPago.AddDays(2), false),
        ];

        VigenciaDeDocumentos.AAnular(documentos, Credito, RegistroDelPago).Should().Equal(2, 3);
    }

    [Fact]
    public void Solo_anula_los_documentos_que_cubren_el_credito_reversado()
    {
        DocumentoQueCubre[] documentos = [new(1, [9], RegistroDelPago.AddDays(1), false), new(2, [], RegistroDelPago.AddDays(1), false)];

        VigenciaDeDocumentos.AAnular(documentos, Credito, RegistroDelPago).Should().BeEmpty();
    }

    /// <summary>
    /// K5 (T0965; T80, FR-132): el reverso de un pago con fecha valor dentro del año certificado y registrado antes de la emisión anula el certificado
    /// tributario y el extracto del período que lo contenía (los dos lo vieron: su <c>InformationCutoff</c> es posterior al registro); el extracto de
    /// un período anterior emitido antes del registro del pago no lo conocía y sigue vigente.
    /// </summary>
    [Fact]
    public void El_reverso_anula_el_certificado_tributario_y_el_extracto_que_vieron_el_pago_y_no_el_extracto_anterior()
    {
        const int certificadoTributario = 10;
        const int extractoDelPeriodo = 11;
        const int extractoAnterior = 12;
        DocumentoQueCubre[] documentos =
        [
            new(certificadoTributario, [Credito, 9], RegistroDelPago.AddDays(45), false),
            new(extractoDelPeriodo, [Credito], RegistroDelPago.AddDays(5), false),
            new(extractoAnterior, [Credito], RegistroDelPago.AddDays(-20), false),
        ];

        VigenciaDeDocumentos.AAnular(documentos, Credito, RegistroDelPago).Should().Equal(certificadoTributario, extractoDelPeriodo);
    }

    [Fact]
    public void Un_documento_ya_anulado_no_se_anula_dos_veces()
    {
        DocumentoQueCubre[] documentos = [new(1, [Credito], RegistroDelPago.AddDays(1), true), new(2, [Credito], RegistroDelPago.AddDays(1), false)];

        VigenciaDeDocumentos.AAnular(documentos, Credito, RegistroDelPago).Should().Equal(2);
    }

    [Fact]
    public void El_estado_es_anulado_con_evento_de_anulacion_aunque_siga_en_firmeza()
    {
        EstadoDeDocumentoEmitido.Calcular(true, new DateOnly(2026, 12, 15), new DateOnly(2026, 12, 2)).Should().Be(IssuedDocumentStatus.Voided);
        EstadoDeDocumentoEmitido.Calcular(true, null, new DateOnly(2026, 12, 2)).Should().Be(IssuedDocumentStatus.Voided);
    }

    [Theory]
    [InlineData(2026, 12, 14, IssuedDocumentStatus.SubjectToFinality)]
    [InlineData(2026, 12, 15, IssuedDocumentStatus.Valid)]
    [InlineData(2026, 12, 16, IssuedDocumentStatus.Valid)]
    public void Sujeto_a_firmeza_mientras_hoy_es_anterior_al_fin_de_la_firmeza_y_vigente_despues_sin_que_nadie_lo_marque(int a, int m, int d,
        IssuedDocumentStatus esperado)
    {
        var documento = new IssuedLendingDocument { Kind = IssuedDocumentKind.LoanClearance, FinalityUntil = new DateOnly(2026, 12, 15) };

        documento.EstadoA(false, new DateOnly(a, m, d)).Should().Be(esperado);
    }

    [Fact]
    public void Sin_firmeza_es_vigente()
    {
        EstadoDeDocumentoEmitido.Calcular(false, null, new DateOnly(2026, 12, 1)).Should().Be(IssuedDocumentStatus.Valid);
    }

    [Fact]
    public void La_anulacion_lleva_motivo_y_la_entrega_su_mensaje()
    {
        var anular = () => IssuedDocumentEvent.Anulado(1, " ", RegistroDelPago, ActorKind.Person, 4);
        anular.Should().Throw<ArgumentException>();
        var entregar = () => IssuedDocumentEvent.Entregado(1, 0, RegistroDelPago, ActorKind.Process, null);
        entregar.Should().Throw<ArgumentOutOfRangeException>();

        var porReverso = IssuedDocumentEvent.Anulado(1, "Se reversó el pago PAG-000004.", RegistroDelPago, ActorKind.Process, null, 88);
        porReverso.Kind.Should().Be(IssuedDocumentEventKind.Voided);
        porReverso.CausingOperationId.Should().Be(88);
        IssuedDocumentEvent.Anulado(1, new string('x', 600), RegistroDelPago, ActorKind.Person, 4).Reason.Should().HaveLength(IssuedDocumentEvent.LargoDelMotivo);

        var entrega = IssuedDocumentEvent.Entregado(1, 31, RegistroDelPago, ActorKind.Process, null);
        entrega.Kind.Should().Be(IssuedDocumentEventKind.Delivered);
        entrega.DebtorMessageId.Should().Be(31);
    }

    [Fact]
    public void Una_persona_registra_con_su_usuario_y_el_proceso_sin_usuario()
    {
        var sinUsuario = () => IssuedDocumentEvent.Anulado(1, "Error", RegistroDelPago, ActorKind.Person, null);
        sinUsuario.Should().Throw<ArgumentException>();
        var procesoConUsuario = () => IssuedDocumentEvent.Entregado(1, 3, RegistroDelPago, ActorKind.Process, 4);
        procesoConUsuario.Should().Throw<ArgumentException>();
    }
}
