using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Domain.Tests.Entities.Admin;

/// <summary>
/// Invariantes de una passkey. Las guardas de longitud no son formalismo: en
/// PostgreSQL las columnas binarias salen <c>bytea</c> SIN límite, así que el
/// único sitio donde el tope se respeta en los dos motores es la factory. Sin
/// ella, una clave larga se guardaría bien en desarrollo y reventaría sólo en una
/// instalación SQL Server.
/// </summary>
public class WebAuthnCredentialTests
{
    private static readonly Guid Persona = Guid.NewGuid();
    private static readonly DateTime Ahora = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    private static WebAuthnCredential Inscribir(
        byte[]? credentialId = null, byte[]? clave = null, long contador = 0) =>
        WebAuthnCredential.Inscribir(
            Persona,
            credentialId ?? [1, 2, 3, 4],
            clave ?? [9, 8, 7],
            contador,
            aaGuid: Guid.NewGuid(),
            transportsJson: """["internal","hybrid"]""",
            isBackupEligible: true,
            isBackedUp: true,
            attestationFormat: "none",
            label: "  Llave azul  ",
            Ahora,
            "ana@coop.co");

    [Fact]
    public void Inscribir_sella_la_auditoria_y_recorta_el_nombre()
    {
        var c = Inscribir();

        c.CentralUserId.Should().Be(Persona);
        c.ConfirmedAt.Should().Be(Ahora);
        c.CreatedAt.Should().Be(Ahora, "este contexto no tiene interceptores que lo rellenen solos");
        c.CreatedBy.Should().Be("ana@coop.co");
        c.Label.Should().Be("Llave azul");
        c.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Un_alta_no_deja_rastro_de_modificacion()
    {
        // Nace, no se edita. Si UpdatedAt viniera relleno desde el alta, la
        // auditoría diría que alguien la modificó el día que se creó.
        var c = Inscribir();

        c.UpdatedAt.Should().BeNull();
        c.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void Sin_credential_id_no_se_inscribe()
    {
        var accion = () => Inscribir(credentialId: []);

        accion.Should().Throw<ArgumentException>().WithMessage("*credential id*");
    }

    [Fact]
    public void Sin_clave_publica_no_se_inscribe()
    {
        var accion = () => Inscribir(clave: []);

        accion.Should().Throw<ArgumentException>().WithMessage("*clave pública*");
    }

    [Fact]
    public void Una_clave_mas_larga_que_la_columna_se_rechaza_en_vez_de_truncarse()
    {
        var demasiado = new byte[WebAuthnCredential.LongitudMaximaClavePublica + 1];

        var accion = () => Inscribir(clave: demasiado);

        accion.Should().Throw<ArgumentException>()
            .WithMessage("*truncaría*",
                "truncar la clave pública deja la llave inservible sin ningún error visible");
    }

    [Fact]
    public void El_contador_solo_avanza()
    {
        var c = Inscribir(contador: 10);

        // Un autenticador que reporta un contador MENOR es la señal de clonado.
        // La entidad no lo rechaza —eso lo hace la librería al verificar la
        // firma— pero tampoco lo retrocede.
        c.ActualizarContador(5, respaldada: true).Should().BeFalse();
        c.SignCount.Should().Be(10);

        c.ActualizarContador(11, respaldada: true).Should().BeTrue();
        c.SignCount.Should().Be(11);
    }

    [Fact]
    public void Sin_cambios_no_se_pide_escritura()
    {
        var c = Inscribir(contador: 7);

        // La mayoría de las passkeys de plataforma reportan siempre cero. Sin
        // esta guarda habría un UPDATE por cada ingreso de cada persona, para no
        // cambiar nada.
        c.ActualizarContador(7, respaldada: true).Should().BeFalse();
    }

    [Fact]
    public void Un_cambio_de_respaldo_si_merece_escritura()
    {
        var c = Inscribir(contador: 7);

        // Que la llave deje de estar sincronizada es un dato que cambia, aunque
        // el contador no se mueva.
        c.ActualizarContador(7, respaldada: false).Should().BeTrue();
        c.IsBackedUp.Should().BeFalse();
    }

    [Fact]
    public void Revocar_es_baja_logica_y_es_idempotente()
    {
        var c = Inscribir();

        c.Revocar(Ahora, "ana@coop.co");
        c.IsDeleted.Should().BeTrue();
        c.DeletedAt.Should().Be(Ahora);

        // Segunda vez: no cambia el autor ni la fecha originales.
        c.Revocar(Ahora.AddDays(1), "otro@coop.co");
        c.DeletedAt.Should().Be(Ahora);
        c.DeletedBy.Should().Be("ana@coop.co");
    }
}
