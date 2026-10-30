using FluentAssertions;
using IngenIA365ERP.Application.Core.PersonBankAccounts;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Core;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Core.PersonBankAccounts;

/// <summary>
/// Feature 015, T0013 (interfaces.md §5; FR-210, SC-026): <see cref="ICuentasBancariasDePersona.VerificarAsync"/> dice si la cuenta destino está
/// registrada y activa para la persona, si está en espera (<c>PayableFrom</c> futuro), si cambió hace pocos días (<c>CuentasDestino.DiasDeMarca</c>),
/// si la comparte con otro beneficiario o con un usuario interno y si es importada sin certificación.
/// </summary>
public class VerificacionDeCuentaDestinoTests
{
    private readonly EscenarioDeCuentasBancarias _e = new();

    private CuentasBancariasDePersona Servicio() => new(_e.Db, _e.Parametros, _e.Reloj);

    private PersonBankAccount Cuenta(Person persona, string numero, PersonBankAccountState estado = PersonBankAccountState.Active, DateOnly? pagableDesde = null,
        PersonBankAccountOrigin origen = PersonBankAccountOrigin.Registered, int? reemplaza = null, DateTime? aprobada = null, bool conCertificacion = true)
    {
        var c = new PersonBankAccount
        {
            PersonId = persona.Id, BankId = _e.Banco.Id, AccountType = "Ahorros", AccountNumber = numero, HolderName = "Titular", HolderTaxId = persona.TaxId,
            Status = estado, Origin = origen, PayableFrom = pagableDesde ?? EscenarioDeCuentasBancarias.Hoy.AddDays(-10), RequestedByName = "x",
            ReplacesId = reemplaza, DecidedAt = aprobada ?? new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
            CertificateAttachmentPublicId = conCertificacion ? Guid.NewGuid() : null,
        };
        _e.Db.PersonBankAccounts.Add(c);
        _e.Db.SaveChanges();
        return c;
    }

    [Fact]
    public async Task Registrada_y_activa_por_el_codigo_ACH_o_el_heredado()
    {
        var c = Cuenta(_e.Persona, "111");

        var r = await Servicio().VerificarAsync(_e.Persona.PublicId, "1001", "1-11", CancellationToken.None);
        r.Registered.Should().BeTrue();
        r.AccountPublicId.Should().Be(c.PublicId);
        r.Waiting.Should().BeFalse();
        (await Servicio().VerificarAsync(_e.Persona.PublicId, "01", "111", CancellationToken.None)).Registered.Should().BeTrue();
        (await Servicio().VerificarAsync(_e.Persona.PublicId, "1001", "222", CancellationToken.None)).Registered.Should().BeFalse();
    }

    [Fact]
    public async Task En_espera_mientras_PayableFrom_es_futuro()
    {
        Cuenta(_e.Persona, "111", pagableDesde: EscenarioDeCuentasBancarias.Hoy.AddDays(2));

        var r = await Servicio().VerificarAsync(_e.Persona.PublicId, "1001", "111", CancellationToken.None);
        r.Waiting.Should().BeTrue();
        r.PayableFrom.Should().Be(EscenarioDeCuentasBancarias.Hoy.AddDays(2));
    }

    [Fact]
    public async Task Cambiada_hace_pocos_dias_segun_la_marca()
    {
        var vieja = Cuenta(_e.Persona, "000", estado: PersonBankAccountState.Retired);
        Cuenta(_e.Persona, "111", reemplaza: vieja.Id, aprobada: new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc));

        (await Servicio().VerificarAsync(_e.Persona.PublicId, "1001", "111", CancellationToken.None)).ChangedRecently.Should().BeFalse("sin valor, sin marca");

        _e.Marca(5);
        var r = await Servicio().VerificarAsync(_e.Persona.PublicId, "1001", "111", CancellationToken.None);
        r.ChangedRecently.Should().BeTrue();
        r.DaysSinceChange.Should().Be(3);
    }

    [Fact]
    public async Task Compartida_con_otro_beneficiario_y_con_un_usuario_interno()
    {
        Cuenta(_e.Persona, "111");
        Cuenta(_e.Otra, "111");

        var r = await Servicio().VerificarAsync(_e.Persona.PublicId, "1001", "111", CancellationToken.None);
        r.SharedWithOtherBeneficiary.Should().BeTrue();
        r.SharedWithInternalUser.Should().BeFalse();

        _e.Db.Users.Add(new User { Username = "luis", PasswordHash = "x", PersonId = _e.Otra.Id });
        await _e.Db.SaveChangesAsync();
        (await Servicio().VerificarAsync(_e.Persona.PublicId, "1001", "111", CancellationToken.None)).SharedWithInternalUser.Should().BeTrue();
    }

    [Fact]
    public async Task Importada_sin_certificacion_queda_marcada()
    {
        Cuenta(_e.Persona, "111", origen: PersonBankAccountOrigin.Imported, conCertificacion: false);

        var r = await Servicio().VerificarAsync(_e.Persona.PublicId, "1001", "111", CancellationToken.None);
        r.Registered.Should().BeTrue();
        r.ImportedWithoutCertificate.Should().BeTrue();

        (await Servicio().ActivasAsync(_e.Persona.PublicId, CancellationToken.None)).Single().Imported.Should().BeTrue();
    }
}
