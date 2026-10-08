using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Domain.Tests.Lending.Collections;

/// <summary>
/// Feature 013, T0269 (T74, Ley 2300; <c>adaptadores-y-archivos.md</c> §4.5; quickstart §1.4; SC-011): <see cref="VentanaLegalDeContacto"/>
/// coincide con las decisiones hechas a mano de <c>Casos/</c>: las once reglas en su orden (primero las exclusiones, después la
/// exención de la entrega de documentos, después el aplazamiento), el aplazamiento día por día hasta 14 días, la semana de lunes a
/// domingo, el techo nacional ∩ la restricción de la cooperativa y los festivos nacionales (no los «Manual» de Nómina). Los casos de
/// contacto directo (<c>WeeklyChannelLocked</c> por gestiones) los suma K4.
/// </summary>
public class CasosDoradosDeVentanaLegalTests
{
    public static IEnumerable<object[]> Casos() =>
        CasoDoradoDeVentanaLegal.Archivos().Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_quince_casos_de_K1()
    {
        CasoDoradoDeVentanaLegal.Archivos().Select(Path.GetFileNameWithoutExtension).Should().BeEquivalentTo(
        [
            "01-destinatario-no-obligado", "02-fallecido-con-reclamacion", "03-insolvencia-sin-cobranza", "04-canal-no-autorizado",
            "05-carta-sin-autorizacion-de-canal", "06-cobranza-sin-evidencia", "07-sin-destino", "08-entrega-de-documento-exenta",
            "09-domingo-difiere-al-lunes", "10-festivo-nacional-difiere", "11-fuera-de-horario", "12-tope-diario-cuenta-informativos",
            "13-informativo-no-fija-canal-semanal", "14-sin-ventana-en-14-dias", "15-horario-de-la-cooperativa-mas-estricto",
        ]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_caso_coincide_con_la_decision_a_mano(string archivo)
    {
        var caso = CasoDoradoDeVentanaLegal.Cargar(Path.Combine(CasoDoradoDeVentanaLegal.DirectorioDeCasos, archivo));

        foreach (var escenario in caso.EscenariosACorrer())
        {
            var decision = VentanaLegalDeContacto.Evaluar(escenario.Solicitud.DelMotor(), caso.ReglasDelMotor(escenario),
                caso.HistorialDelMotor(escenario), caso.TitularDelMotor(escenario));
            var e = escenario.Esperado;

            using (new AssertionScope($"{caso.Caso} · {escenario.Nombre}"))
            {
                switch (e.Decision)
                {
                    case "Permitido":
                        decision.Should().BeOfType<Permitido>()
                            .Which.En.Should().Be(CasoDoradoDeVentanaLegal.Instante(e.En ?? escenario.Solicitud.Pedido));
                        break;
                    case "Diferido":
                        var diferido = decision.Should().BeOfType<Diferido>().Subject;
                        diferido.Siguiente.Should().Be(CasoDoradoDeVentanaLegal.Instante(e.Siguiente!));
                        diferido.Motivos.Should().Equal(e.Motivos);
                        break;
                    case "Excluido":
                        decision.Should().BeOfType<Excluido>().Which.Motivo.Should().Be(e.Motivo!.Value);
                        break;
                    default:
                        throw new InvalidOperationException($"Decisión esperada desconocida «{e.Decision}» en {archivo}.");
                }
            }
        }
    }

    [Fact]
    public void El_ejemplo_del_contrato_un_contacto_directo_por_telefono_bloquea_whatsapp_esa_semana()
    {
        // adaptadores-y-archivos.md §4.5, US12-2 (lo suma K4 a los casos; el motor ya lo cumple).
        var reglas = new ReglasDeContacto(new FranjaHoraria(new TimeOnly(7, 0), new TimeOnly(19, 0)),
            new FranjaHoraria(new TimeOnly(8, 0), new TimeOnly(15, 0)), 1, [new DateOnly(2026, 11, 16)]);
        var titular = new EstadoDelTitular(true, ContactRole.Debtor)
        {
            Autorizaciones = [new(MessageChannel.Sms, true), new(MessageChannel.WhatsApp, true), new(MessageChannel.Email, true)],
            Destinos = [MessageChannel.Sms, MessageChannel.WhatsApp, MessageChannel.Email],
            AutorizacionGeneral = true,
            AutorizacionGeneralConEvidencia = true,
        };
        var historial = new HistorialDeContactos([ContactoRegistrado.DeGestion(new DateTime(2026, 11, 9, 10, 15, 0), CollectionActionChannel.Phone, true)]);
        var solicitud = new SolicitudDeContacto(new DateTime(2026, 11, 11, 9, 0, 0), MessageChannel.WhatsApp, DebtorMessagePurpose.Collection, Inmediato: true);

        var decision = VentanaLegalDeContacto.Evaluar(solicitud, reglas, historial, titular);

        var diferido = decision.Should().BeOfType<Diferido>().Subject;
        diferido.Siguiente.Should().Be(new DateTime(2026, 11, 17, 7, 0, 0));
        diferido.Motivos.Should().Equal(ContactWindowReason.WeeklyChannelLocked);
    }

    [Fact]
    public void Un_contacto_sin_contacto_directo_no_fija_el_canal()
    {
        var reglas = new ReglasDeContacto(new FranjaHoraria(new TimeOnly(7, 0), new TimeOnly(19, 0)), null, 1, []);
        var titular = new EstadoDelTitular(true, ContactRole.Debtor)
        {
            Autorizaciones = [new(MessageChannel.WhatsApp, true)], Destinos = [MessageChannel.WhatsApp],
        };
        var historial = new HistorialDeContactos([ContactoRegistrado.DeGestion(new DateTime(2026, 11, 9, 10, 15, 0), CollectionActionChannel.Phone, false)]);
        var solicitud = new SolicitudDeContacto(new DateTime(2026, 11, 11, 9, 0, 0), MessageChannel.WhatsApp, DebtorMessagePurpose.Collection, false);

        VentanaLegalDeContacto.Evaluar(solicitud, reglas, historial, titular).Should().Be(new Permitido(new DateTime(2026, 11, 11, 9, 0, 0)));
    }

    [Fact]
    public void Lo_pedido_en_el_pasado_se_permite_desde_ahora()
    {
        var reglas = new ReglasDeContacto(new FranjaHoraria(new TimeOnly(7, 0), new TimeOnly(19, 0)), null, 1, []);
        var titular = new EstadoDelTitular(true, ContactRole.Debtor) { Autorizaciones = [new(MessageChannel.Sms, true)], Destinos = [MessageChannel.Sms] };
        var solicitud = new SolicitudDeContacto(new DateTime(2026, 11, 10, 9, 0, 0), MessageChannel.Sms, DebtorMessagePurpose.Informative, false)
        {
            Ahora = new DateTime(2026, 11, 11, 10, 30, 0),
        };

        VentanaLegalDeContacto.Evaluar(solicitud, reglas, HistorialDeContactos.Vacio, titular)
            .Should().Be(new Permitido(new DateTime(2026, 11, 11, 10, 30, 0)));
    }

    [Fact]
    public void La_combinacion_toma_lo_mas_estricto_de_cada_lado()
    {
        var techo = new ReglasDeContacto(new FranjaHoraria(new TimeOnly(7, 0), new TimeOnly(19, 0)),
            new FranjaHoraria(new TimeOnly(8, 0), new TimeOnly(15, 0)), 1, [new DateOnly(2026, 11, 16)]);
        var cooperativa = new ReglasDeContacto(new FranjaHoraria(new TimeOnly(6, 0), new TimeOnly(18, 0)), null, 3, []);

        var efectivas = ReglasDeContacto.Combinar(techo, cooperativa);

        efectivas.LunesAViernes.Should().Be(new FranjaHoraria(new TimeOnly(7, 0), new TimeOnly(18, 0)));
        efectivas.Sabado.Should().BeNull("la cooperativa no contacta los sábados");
        efectivas.ContactosPorDia.Should().Be(1);
        efectivas.NoHabiles.Should().Equal(new DateOnly(2026, 11, 16));
    }

    [Fact]
    public void Una_franja_vacia_o_un_tope_negativo_se_rechazan()
    {
        var franja = () => new FranjaHoraria(new TimeOnly(19, 0), new TimeOnly(7, 0));
        franja.Should().Throw<ArgumentException>();
        var tope = () => new ReglasDeContacto(null, null, -1, []);
        tope.Should().Throw<ArgumentException>();
    }
}
