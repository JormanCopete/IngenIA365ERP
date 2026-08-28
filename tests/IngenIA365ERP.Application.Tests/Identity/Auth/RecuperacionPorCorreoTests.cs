using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity.Auth.Recuperacion;
using IngenIA365ERP.Domain.Entities.Admin;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Auth;

/// <summary>
/// La recuperación por correo es la pieza más delicada de todo el segundo factor,
/// porque si el correo puede borrarlo, el segundo factor se degrada al primero:
/// quien tenga el buzón entra. Estas pruebas fijan las mitigaciones que lo hacen
/// defendible — y ninguna es opcional.
/// </summary>
public class RecuperacionPorCorreoTests
{
    private static ActiveMembershipInfo Coop(
        string nombre,
        bool exige = true,
        bool permiteRecuperacion = true,
        int horas = 24) =>
        new(Guid.NewGuid(), nombre, false, exige, ConversionDeMetodosMfa.Todos,
            permiteRecuperacion, horas);

    // ---------- Qué política manda con varias cooperativas ----------

    /// <summary>
    /// Manda la más estricta. Si bastara una permisiva, cualquiera podría entrar en
    /// la cooperativa exigente pidiendo la recuperación «por» la otra — y la
    /// política estricta dejaría de valer para nada en cuanto su gente perteneciera
    /// a una segunda cooperativa, que aquí es lo normal.
    /// </summary>
    [Fact]
    public void Basta_una_cooperativa_que_lo_prohiba_para_que_no_se_pueda()
    {
        var veredicto = GuardiaDeRecuperacionPorCorreo.Evaluar(
        [
            Coop("Permisiva", permiteRecuperacion: true),
            Coop("Estricta", permiteRecuperacion: false),
        ]);

        veredicto.Permitida.Should().BeFalse();
        veredicto.Motivo.Should().Contain("Estricta", "hay que decir cuál lo impide");
    }

    /// <summary>
    /// Y la demora es la más larga. Con una que espera 24 horas y otra que espera 1,
    /// esperar 1 haría que la política de 24 no valiera nada para quien pertenece a
    /// las dos.
    /// </summary>
    [Fact]
    public void La_demora_es_la_mas_larga_de_las_exigentes()
    {
        var veredicto = GuardiaDeRecuperacionPorCorreo.Evaluar(
        [
            Coop("Rapida", horas: 1),
            Coop("Lenta", horas: 72),
        ]);

        veredicto.Permitida.Should().BeTrue();
        veredicto.Demora.Should().Be(TimeSpan.FromHours(72));
    }

    [Fact]
    public void Las_que_no_exigen_MFA_no_opinan()
    {
        var veredicto = GuardiaDeRecuperacionPorCorreo.Evaluar(
        [
            Coop("Exigente", exige: true, permiteRecuperacion: true, horas: 24),
            Coop("Relajada", exige: false, permiteRecuperacion: false, horas: 1),
        ]);

        veredicto.Permitida.Should().BeTrue("la que no exige nada no puede prohibir la recuperación");
        veredicto.Demora.Should().Be(TimeSpan.FromHours(24));
    }

    /// <summary>
    /// Sin ninguna cooperativa que exija segundo factor, esta vía no se ofrece: la
    /// persona puede retirarlo desde su perfil, que es más corto y no depende del
    /// buzón. Y sin política explícita, esto queda apagado — la dirección segura.
    /// </summary>
    [Fact]
    public void Sin_cooperativas_exigentes_no_se_ofrece()
    {
        GuardiaDeRecuperacionPorCorreo.Evaluar([Coop("Relajada", exige: false)])
            .Permitida.Should().BeFalse();

        GuardiaDeRecuperacionPorCorreo.Evaluar([]).Permitida.Should().BeFalse();
    }

    /// <summary>
    /// Una columna a cero significaría «sin espera», que es el diseño rechazado. La
    /// entidad ya lo impide al escribir, pero esta lectura viene de una columna que
    /// también pudo escribir una migración o un UPDATE directo.
    /// </summary>
    [Fact]
    public void Una_demora_de_cero_horas_se_eleva_al_minimo()
    {
        var veredicto = GuardiaDeRecuperacionPorCorreo.Evaluar([Coop("Cero", horas: 0)]);

        veredicto.Permitida.Should().BeTrue();
        veredicto.Demora.Should().Be(
            TimeSpan.FromHours(TenantMfaPolicy.DemoraMinimaEnHoras),
            "cero horas es el diseño sin demora, no una demora corta");
    }

    // ---------- La solicitud ----------

    private static readonly DateTime Ahora = new(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc);

    private static MfaRecoveryRequest CrearSolicitud(TimeSpan? demora = null) =>
        MfaRecoveryRequest.Crear(
            centralUserId: Guid.NewGuid(),
            tokenHash: [1, 2, 3],
            cancelTokenHash: [4, 5, 6],
            ahora: Ahora,
            demora: demora ?? TimeSpan.FromHours(24),
            vigencia: TimeSpan.FromDays(7),
            ipSolicitante: "1.2.3.4");

    [Fact]
    public void Antes_de_la_espera_no_se_puede_ejecutar()
    {
        var solicitud = CrearSolicitud();

        solicitud.EstaViva(Ahora).Should().BeTrue();
        solicitud.SePuedeEjecutar(Ahora).Should().BeFalse();
        solicitud.SePuedeEjecutar(Ahora.AddHours(23)).Should().BeFalse();
        solicitud.SePuedeEjecutar(Ahora.AddHours(24)).Should().BeTrue();
    }

    [Fact]
    public void Ejecutar_antes_de_tiempo_lanza()
    {
        var solicitud = CrearSolicitud();

        var acto = () => solicitud.MarcarEjecutada(Ahora.AddHours(1));

        acto.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Cancelar es idempotente. El enlace no pide nada, así que se pulsa dos veces
    /// con facilidad —y el ingreso correcto también cancela, sin preguntar—; que la
    /// segunda vez lance convertiría un acierto en un error en la cara de quien hizo
    /// lo correcto.
    /// </summary>
    [Fact]
    public void Cancelar_dos_veces_no_lanza_y_conserva_el_primer_motivo()
    {
        var solicitud = CrearSolicitud();

        solicitud.Cancelar(Ahora.AddHours(1), MfaRecoveryRequest.Motivos.LaPersonaCancelo);
        solicitud.Cancelar(Ahora.AddHours(2), MfaRecoveryRequest.Motivos.EntroConNormalidad);

        solicitud.CanceladaEn.Should().Be(Ahora.AddHours(1));
        solicitud.MotivoDeCancelacion.Should().Be(MfaRecoveryRequest.Motivos.LaPersonaCancelo);
    }

    [Fact]
    public void Una_solicitud_cancelada_ya_no_se_ejecuta()
    {
        var solicitud = CrearSolicitud();
        solicitud.Cancelar(Ahora.AddHours(1), MfaRecoveryRequest.Motivos.LaPersonaCancelo);

        solicitud.SePuedeEjecutar(Ahora.AddHours(25)).Should().BeFalse();
        var acto = () => solicitud.MarcarEjecutada(Ahora.AddHours(25));
        acto.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Una_solicitud_ejecutada_no_se_ejecuta_dos_veces()
    {
        var solicitud = CrearSolicitud();
        solicitud.MarcarEjecutada(Ahora.AddHours(25));

        var acto = () => solicitud.MarcarEjecutada(Ahora.AddHours(26));
        acto.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Caduca()
    {
        var solicitud = CrearSolicitud();

        solicitud.EstaViva(Ahora.AddDays(8)).Should().BeFalse();
        solicitud.SePuedeEjecutar(Ahora.AddDays(8)).Should().BeFalse();
    }

    /// <summary>
    /// Una vigencia que no supere la demora produce una solicitud imposible de
    /// ejecutar SIEMPRE, y el fallo aparecería un día después de crearla, cuando
    /// nadie está mirando.
    /// </summary>
    [Fact]
    public void Una_vigencia_menor_que_la_demora_se_rechaza_al_crear()
    {
        var acto = () => MfaRecoveryRequest.Crear(
            centralUserId: Guid.NewGuid(),
            tokenHash: [1], cancelTokenHash: [2],
            ahora: Ahora,
            demora: TimeSpan.FromDays(2),
            vigencia: TimeSpan.FromDays(1),
            ipSolicitante: null);

        acto.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Los dos tokens tienen que ser distintos, y esto lo fija la firma: si
    /// compartieran token, quien tuviera el enlace de cancelar —que no pide
    /// contraseña— podría además ejecutar la recuperación pasada la espera.
    /// </summary>
    [Fact]
    public void Confirmar_y_cancelar_son_dos_tokens()
    {
        var solicitud = CrearSolicitud();

        solicitud.TokenHash.Should().NotBeEquivalentTo(solicitud.CancelTokenHash);
    }

    // ---------- La política de la cooperativa ----------

    [Fact]
    public void La_recuperacion_por_correo_nace_apagada()
    {
        var politica = TenantMfaPolicy.CreateForTenant(Guid.NewGuid());

        politica.AllowEmailRecovery.Should().BeFalse(
            "encenderla es aceptar que el buzón pueda retirar el segundo factor");
        politica.EmailRecoveryDelayHours.Should().Be(TenantMfaPolicy.DemoraPorDefectoEnHoras);
    }

    [Fact]
    public void No_se_puede_encender_con_demora_cero()
    {
        var politica = TenantMfaPolicy.CreateForTenant(Guid.NewGuid());

        var acto = () => politica.ConfigurarRecuperacionPorCorreo(permitir: true, demoraEnHoras: 0);

        acto.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Apagarla no reinicia la demora: volver a encenderla no puede perder en
    /// silencio un valor que alguien eligió.
    /// </summary>
    [Fact]
    public void Apagarla_conserva_la_demora_configurada()
    {
        var politica = TenantMfaPolicy.CreateForTenant(Guid.NewGuid());
        politica.ConfigurarRecuperacionPorCorreo(permitir: true, demoraEnHoras: 72);

        politica.ConfigurarRecuperacionPorCorreo(permitir: false, demoraEnHoras: 0);

        politica.AllowEmailRecovery.Should().BeFalse();
        politica.EmailRecoveryDelayHours.Should().Be(72);
    }
}
