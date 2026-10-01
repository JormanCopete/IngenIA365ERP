using FluentAssertions;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using S = IngenIA365ERP.Domain.Enums.ElectronicInvoicing.ElectronicDocumentStatus;
using E = IngenIA365ERP.Domain.ElectronicInvoicing.EventoDelDocumentoElectronico;
using O = IngenIA365ERP.Domain.Enums.ElectronicInvoicing.ChannelOutcome;

namespace IngenIA365ERP.Domain.Tests.ElectronicInvoicing;

/// <summary>
/// Feature 012, I4, T673 (contracts/dian.md §5.1; data-model §18): la máquina de estados del documento electrónico,
/// pura. La tabla de <see cref="Permitidas"/> se escribe aquí, a mano, desde el contrato —no se lee de la
/// implementación—: cada fila se prueba con su destino y <b>toda</b> otra combinación (estado × evento × resultado del
/// canal, o × rechazo confirmado en los casos a, b y c) tiene que rechazarse con un código nombrado.
///
/// <para>
/// Además de las filas de §5.1, la tabla trae las que el contrato exige en otras secciones: la consulta desde
/// <c>Pending</c> (§6.3: si el proceso cae entre la llamada y el registro, el siguiente intento consulta antes de
/// reenviar; y «la DIAN que valida tarde un Pending lo deja validado», §5.1), la consulta que confirma un rechazo antes
/// de los casos b y c (§8), y el reintento de la transmisión de contingencia cuando el canal o la DIAN no responden
/// (§6.3: espera siguiente).
/// </para>
/// </summary>
public class TransicionesDelDocumentoElectronicoTests
{
    private static readonly S[] Estados = Enum.GetValues<S>();
    private static readonly O[] Resultados = Enum.GetValues<O>();
    private static readonly E[] EventosDelCanal = [E.Emitir, E.ConsultarEstado, E.TransmitirContingencia];
    private static readonly E[] Casos = [E.CorregirCasoA, E.ReemplazarCasoB, E.CancelarCasoC];

    /// <summary>Las transiciones de canal permitidas, con respuesta completa (código único y respuesta de validación).</summary>
    private static readonly (S Desde, E Evento, O Resultado, S Hacia)[] PermitidasDelCanal =
    [
        (S.Pending, E.Emitir, O.Validated, S.Validated),
        (S.Pending, E.Emitir, O.ValidatedWithNotices, S.ValidatedWithNotices),
        (S.Pending, E.Emitir, O.Rejected, S.Rejected),
        (S.Pending, E.Emitir, O.InvalidData, S.Rejected),
        (S.Pending, E.Emitir, O.InProcess, S.Sent),
        (S.Pending, E.Emitir, O.DianUnavailable, S.DianContingency),
        (S.Pending, E.Emitir, O.ChannelUnavailable, S.Pending),

        (S.Pending, E.ConsultarEstado, O.Validated, S.Validated),
        (S.Pending, E.ConsultarEstado, O.ValidatedWithNotices, S.ValidatedWithNotices),
        (S.Pending, E.ConsultarEstado, O.Rejected, S.Rejected),
        (S.Pending, E.ConsultarEstado, O.InvalidData, S.Rejected),
        (S.Pending, E.ConsultarEstado, O.InProcess, S.Sent),
        (S.Pending, E.ConsultarEstado, O.NotFound, S.Pending),
        (S.Pending, E.ConsultarEstado, O.ChannelUnavailable, S.Pending),
        (S.Pending, E.ConsultarEstado, O.DianUnavailable, S.Pending),

        (S.Sent, E.ConsultarEstado, O.Validated, S.Validated),
        (S.Sent, E.ConsultarEstado, O.ValidatedWithNotices, S.ValidatedWithNotices),
        (S.Sent, E.ConsultarEstado, O.Rejected, S.Rejected),
        (S.Sent, E.ConsultarEstado, O.InvalidData, S.Rejected),
        (S.Sent, E.ConsultarEstado, O.NotFound, S.Pending),
        (S.Sent, E.ConsultarEstado, O.InProcess, S.Sent),
        (S.Sent, E.ConsultarEstado, O.ChannelUnavailable, S.Sent),
        (S.Sent, E.ConsultarEstado, O.DianUnavailable, S.Sent),

        (S.IssuerContingency, E.TransmitirContingencia, O.Validated, S.Validated),
        (S.IssuerContingency, E.TransmitirContingencia, O.ValidatedWithNotices, S.ValidatedWithNotices),
        (S.IssuerContingency, E.TransmitirContingencia, O.Rejected, S.Rejected),
        (S.IssuerContingency, E.TransmitirContingencia, O.InvalidData, S.Rejected),
        (S.IssuerContingency, E.TransmitirContingencia, O.InProcess, S.Sent),
        (S.IssuerContingency, E.TransmitirContingencia, O.ChannelUnavailable, S.IssuerContingency),
        (S.IssuerContingency, E.TransmitirContingencia, O.DianUnavailable, S.IssuerContingency),

        (S.DianContingency, E.TransmitirContingencia, O.Validated, S.Validated),
        (S.DianContingency, E.TransmitirContingencia, O.ValidatedWithNotices, S.ValidatedWithNotices),
        (S.DianContingency, E.TransmitirContingencia, O.Rejected, S.Rejected),
        (S.DianContingency, E.TransmitirContingencia, O.InvalidData, S.Rejected),
        (S.DianContingency, E.TransmitirContingencia, O.InProcess, S.Sent),
        (S.DianContingency, E.TransmitirContingencia, O.ChannelUnavailable, S.DianContingency),
        (S.DianContingency, E.TransmitirContingencia, O.DianUnavailable, S.DianContingency),

        (S.Rejected, E.ConsultarEstado, O.Rejected, S.Rejected),
        (S.Rejected, E.ConsultarEstado, O.InvalidData, S.Rejected),
        (S.Rejected, E.ConsultarEstado, O.NotFound, S.Rejected),
        (S.Rejected, E.ConsultarEstado, O.InProcess, S.Rejected),
        (S.Rejected, E.ConsultarEstado, O.ChannelUnavailable, S.Rejected),
        (S.Rejected, E.ConsultarEstado, O.DianUnavailable, S.Rejected),
    ];

    /// <summary>Los casos a, b y c permitidos: (evento, rechazo confirmado, destino). Sólo desde <c>Rejected</c>.</summary>
    private static readonly (E Evento, bool Confirmado, S Hacia)[] PermitidosLosCasos =
    [
        (E.CorregirCasoA, false, S.Pending),
        (E.CorregirCasoA, true, S.Pending),
        (E.ReemplazarCasoB, true, S.Pending),
        (E.CancelarCasoC, true, S.CancelledWithoutReplacement),
    ];

    private static RespuestaDelCanal Completa(O resultado) => new(resultado, TieneCodigoUnico: true, TieneRespuestaDeValidacion: true);

    private static ResultadoDeTransicion Canal(S desde, E evento, O resultado) =>
        TransicionesDelDocumentoElectronico.Aplicar(desde, evento, Completa(resultado), eventoDeContingenciaCerrado: true);

    public static TheoryData<S, E, O, S> Permitidas()
    {
        var datos = new TheoryData<S, E, O, S>();
        foreach (var (desde, evento, resultado, hacia) in PermitidasDelCanal) datos.Add(desde, evento, resultado, hacia);
        return datos;
    }

    [Theory]
    [MemberData(nameof(Permitidas))]
    public void Cada_transicion_del_canal_permitida_llega_a_su_destino(S desde, E evento, O resultado, S hacia)
    {
        var r = Canal(desde, evento, resultado);

        r.Procede.Should().BeTrue($"{desde} + {evento} → {resultado} está en la tabla (código {r.Codigo})");
        r.Hacia.Should().Be(hacia);
    }

    public static TheoryData<E, bool, S> CasosPermitidos()
    {
        var datos = new TheoryData<E, bool, S>();
        foreach (var (evento, confirmado, hacia) in PermitidosLosCasos) datos.Add(evento, confirmado, hacia);
        return datos;
    }

    [Theory]
    [MemberData(nameof(CasosPermitidos))]
    public void Cada_caso_permitido_sobre_un_rechazado_llega_a_su_destino(E evento, bool confirmado, S hacia)
    {
        var r = TransicionesDelDocumentoElectronico.Aplicar(S.Rejected, evento, rechazoConfirmado: confirmado);

        r.Procede.Should().BeTrue();
        r.Hacia.Should().Be(hacia);
        r.NuevaVersion.Should().Be(evento != E.CancelarCasoC);
    }

    public static TheoryData<S, E, O> ProhibidasDelCanal()
    {
        var datos = new TheoryData<S, E, O>();
        foreach (var desde in Estados)
            foreach (var evento in EventosDelCanal)
                foreach (var resultado in Resultados)
                    if (!PermitidasDelCanal.Any(p => p.Desde == desde && p.Evento == evento && p.Resultado == resultado))
                        datos.Add(desde, evento, resultado);
        return datos;
    }

    [Theory]
    [MemberData(nameof(ProhibidasDelCanal))]
    public void Toda_combinacion_del_canal_no_listada_se_rechaza_con_un_codigo(S desde, E evento, O resultado)
    {
        var r = Canal(desde, evento, resultado);

        r.Procede.Should().BeFalse($"{desde} + {evento} → {resultado} no está en la tabla");
        r.Codigo.Should().StartWith("ElectronicInvoicing.");
    }

    public static TheoryData<S, E, bool> CasosProhibidos()
    {
        var datos = new TheoryData<S, E, bool>();
        foreach (var desde in Estados)
            foreach (var evento in Casos)
                foreach (var confirmado in new[] { false, true })
                    if (!(desde == S.Rejected && PermitidosLosCasos.Any(p => p.Evento == evento && p.Confirmado == confirmado)))
                        datos.Add(desde, evento, confirmado);
        return datos;
    }

    [Theory]
    [MemberData(nameof(CasosProhibidos))]
    public void Todo_caso_a_b_o_c_no_listado_se_rechaza_con_un_codigo(S desde, E evento, bool confirmado)
    {
        var r = TransicionesDelDocumentoElectronico.Aplicar(desde, evento, rechazoConfirmado: confirmado);

        r.Procede.Should().BeFalse();
        r.Codigo.Should().StartWith("ElectronicInvoicing.");
    }

    [Fact]
    public void Al_numerar_nace_pendiente_o_en_contingencia_03()
    {
        TransicionesDelDocumentoElectronico.AlNumerar(contingencia03Abierta: false).Should().Be((S.Pending, (ContingencyType?)null));
        TransicionesDelDocumentoElectronico.AlNumerar(contingencia03Abierta: true).Should().Be((S.IssuerContingency, ContingencyType.Issuer03));
    }

    [Theory]
    [InlineData(S.Pending, E.Emitir)]
    [InlineData(S.Pending, E.ConsultarEstado)]
    [InlineData(S.Sent, E.ConsultarEstado)]
    [InlineData(S.IssuerContingency, E.TransmitirContingencia)]
    [InlineData(S.DianContingency, E.TransmitirContingencia)]
    public void Nunca_queda_validado_sin_codigo_unico_y_respuesta_de_validacion(S desde, E evento)
    {
        foreach (var resultado in new[] { O.Validated, O.ValidatedWithNotices })
        {
            foreach (var (codigo, respuesta) in new[] { (false, true), (true, false), (false, false) })
            {
                var r = TransicionesDelDocumentoElectronico.Aplicar(desde, evento,
                    new RespuestaDelCanal(resultado, codigo, respuesta), eventoDeContingenciaCerrado: true);

                r.Procede.Should().BeTrue();
                r.Hacia.Should().Be(S.Sent, "sin código único o sin respuesta de validación el resultado es ambiguo: se consulta");
            }
        }
    }

    [Theory]
    [InlineData(E.CorregirCasoA)]
    [InlineData(E.ReemplazarCasoB)]
    [InlineData(E.CancelarCasoC)]
    public void Un_enviado_no_admite_los_casos_a_b_ni_c(E caso)
    {
        foreach (var confirmado in new[] { false, true })
        {
            var r = TransicionesDelDocumentoElectronico.Aplicar(S.Sent, caso, rechazoConfirmado: confirmado);
            r.Procede.Should().BeFalse();
            r.Codigo.Should().Be(TransicionesDelDocumentoElectronico.CodigoEsperaRespuesta);
        }
    }

    [Theory]
    [InlineData(S.Pending)]
    [InlineData(S.IssuerContingency)]
    [InlineData(S.DianContingency)]
    public void Los_casos_solo_aplican_a_un_rechazado(S desde)
    {
        TransicionesDelDocumentoElectronico.Aplicar(desde, E.CorregirCasoA).Codigo
            .Should().Be(TransicionesDelDocumentoElectronico.CodigoNoRechazado);
    }

    [Theory]
    [InlineData(E.ReemplazarCasoB)]
    [InlineData(E.CancelarCasoC)]
    public void Los_casos_b_y_c_exigen_el_rechazo_confirmado(E caso)
    {
        TransicionesDelDocumentoElectronico.Aplicar(S.Rejected, caso, rechazoConfirmado: false).Codigo
            .Should().Be(TransicionesDelDocumentoElectronico.CodigoRechazoSinConfirmar);
    }

    [Theory]
    [InlineData(S.Validated)]
    [InlineData(S.ValidatedWithNotices)]
    [InlineData(S.CancelledWithoutReplacement)]
    public void Nada_sale_de_un_estado_final(S final)
    {
        foreach (var evento in EventosDelCanal)
            foreach (var resultado in Resultados)
                Canal(final, evento, resultado).Codigo.Should().Be(TransicionesDelDocumentoElectronico.CodigoFinal);

        foreach (var caso in Casos)
            TransicionesDelDocumentoElectronico.Aplicar(final, caso, rechazoConfirmado: true).Codigo
                .Should().Be(TransicionesDelDocumentoElectronico.CodigoFinal);
    }

    [Fact]
    public void Del_rechazado_solo_se_sale_por_los_casos_a_b_o_c()
    {
        foreach (var evento in EventosDelCanal)
            foreach (var resultado in Resultados)
            {
                var r = Canal(S.Rejected, evento, resultado);
                if (r.Procede) r.Hacia.Should().Be(S.Rejected, $"{evento} → {resultado} no saca del rechazo");
            }
    }

    [Fact]
    public void La_contingencia_03_no_se_transmite_con_su_evento_abierto()
    {
        var r = TransicionesDelDocumentoElectronico.Aplicar(S.IssuerContingency, E.TransmitirContingencia, Completa(O.Validated),
            eventoDeContingenciaCerrado: false);

        r.Procede.Should().BeFalse();
        r.Codigo.Should().Be(TransicionesDelDocumentoElectronico.CodigoContingenciaAbierta);

        // La 04 se transmite al volver la DIAN, sin esperar el cierre manual (§7.1).
        TransicionesDelDocumentoElectronico.Aplicar(S.DianContingency, E.TransmitirContingencia, Completa(O.Validated),
            eventoDeContingenciaCerrado: false).Procede.Should().BeTrue();
    }

    [Fact]
    public void NotFound_desde_enviado_vuelve_a_pendiente_para_reenviar_la_misma_version()
    {
        var r = Canal(S.Sent, E.ConsultarEstado, O.NotFound);

        r.Hacia.Should().Be(S.Pending);
        r.ReenviarMismaVersion.Should().BeTrue();
        r.NuevaVersion.Should().BeFalse();
    }

    [Fact]
    public void Quien_rechaza_la_DIAN_o_el_canal_y_la_contingencia_04_la_declara_el_canal()
    {
        Canal(S.Pending, E.Emitir, O.Rejected).RechazadoPor.Should().Be(RejectedBy.Dian);
        Canal(S.Pending, E.Emitir, O.InvalidData).RechazadoPor.Should().Be(RejectedBy.Channel);
        Canal(S.Pending, E.Emitir, O.DianUnavailable).Contingencia.Should().Be(ContingencyType.Dian04);
        Canal(S.Pending, E.Emitir, O.ChannelUnavailable).CuentaComoFallaDelCanal.Should().BeTrue();
        Canal(S.Pending, E.Emitir, O.InProcess).CuentaComoFallaDelCanal.Should().BeFalse();
    }

    [Fact]
    public void Una_contingencia_04_sin_codigo_unico_no_se_supone_y_queda_por_consultar()
    {
        var r = TransicionesDelDocumentoElectronico.Aplicar(S.Pending, E.Emitir, new RespuestaDelCanal(O.DianUnavailable, false, false));

        r.Hacia.Should().Be(S.Sent);
        r.Contingencia.Should().BeNull();
    }

    [Fact]
    public void La_consulta_confirma_el_rechazo_solo_con_rechazo_o_no_encontrado()
    {
        Canal(S.Rejected, E.ConsultarEstado, O.Rejected).ConfirmaElRechazo.Should().BeTrue();
        Canal(S.Rejected, E.ConsultarEstado, O.Rejected).RechazadoPor.Should().Be(RejectedBy.Dian);
        Canal(S.Rejected, E.ConsultarEstado, O.NotFound).ConfirmaElRechazo.Should().BeTrue();
        Canal(S.Rejected, E.ConsultarEstado, O.NotFound).RechazadoPor.Should().Be(RejectedBy.Channel);
        Canal(S.Rejected, E.ConsultarEstado, O.InProcess).ConfirmaElRechazo.Should().BeFalse();
        Canal(S.Rejected, E.ConsultarEstado, O.ChannelUnavailable).ConfirmaElRechazo.Should().BeFalse();
    }

    [Fact]
    public void Un_evento_del_canal_sin_respuesta_es_un_error_de_programacion()
    {
        FluentActions.Invoking(() => TransicionesDelDocumentoElectronico.Aplicar(S.Pending, E.Emitir))
            .Should().Throw<ArgumentNullException>();
    }

    // ── El documento (T693) delega en la máquina ───────────────────────────────────────────────────────────

    private static readonly DateTime Ahora = new(2026, 12, 5, 15, 14, 22, DateTimeKind.Utc);

    [Fact]
    public void El_documento_conserva_el_tipo_de_contingencia_al_validarse()
    {
        var doc = new ElectronicDocument { Status = S.Pending };
        doc.AplicarEvento(E.Emitir, Completa(O.DianUnavailable), Ahora).Procede.Should().BeTrue();
        doc.Status.Should().Be(S.DianContingency);
        doc.ContingencyType.Should().Be(ContingencyType.Dian04);

        doc.AplicarEvento(E.TransmitirContingencia, Completa(O.Validated), Ahora.AddHours(3)).Procede.Should().BeTrue();

        doc.Status.Should().Be(S.Validated);
        doc.ContingencyType.Should().Be(ContingencyType.Dian04);
        doc.ValidatedAt.Should().Be(Ahora.AddHours(3));
        doc.LastOutcome.Should().Be(O.Validated);
    }

    [Fact]
    public void El_documento_no_cambia_cuando_la_maquina_rechaza()
    {
        var doc = new ElectronicDocument { Status = S.Validated, CurrentVersionId = 4 };

        var r = doc.AplicarEvento(E.CorregirCasoA, null, Ahora);

        r.Procede.Should().BeFalse();
        doc.Status.Should().Be(S.Validated);
        doc.CurrentVersionId.Should().Be(4);
    }

    [Fact]
    public void El_documento_que_vuelve_de_enviado_por_NotFound_conserva_su_version()
    {
        var doc = new ElectronicDocument { Status = S.Sent, CurrentVersionId = 7, SentAt = Ahora };

        doc.AplicarEvento(E.ConsultarEstado, Completa(O.NotFound), Ahora.AddMinutes(1));

        doc.Status.Should().Be(S.Pending);
        doc.CurrentVersionId.Should().Be(7);
    }

    [Fact]
    public void El_documento_rechazado_guarda_quien_rechazo_y_el_caso_c_su_motivo_y_responsable()
    {
        var doc = new ElectronicDocument { Status = S.Pending };
        doc.AplicarEvento(E.Emitir, Completa(O.InvalidData), Ahora);
        doc.Status.Should().Be(S.Rejected);
        doc.RejectedBy.Should().Be(RejectedBy.Channel);

        var r = doc.CancelarSinReemplazo("Venta mal registrada", 12, rechazoConfirmado: true);

        r.Procede.Should().BeTrue();
        doc.Status.Should().Be(S.CancelledWithoutReplacement);
        doc.RejectionReason.Should().Be("Venta mal registrada");
        doc.CancelledByUserId.Should().Be(12);
    }
}
