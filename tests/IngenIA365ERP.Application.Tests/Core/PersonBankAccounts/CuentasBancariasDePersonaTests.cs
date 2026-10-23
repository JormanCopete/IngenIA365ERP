using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.PersonBankAccounts;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Core.PersonBankAccounts;

/// <summary>
/// Feature 015, T0012 (R-21; FR-210; data-model §16.1): el alta nace <c>PendingApproval</c> con la certificación obligatoria
/// (<c>Core.PersonBankAccount.CertificateRequired</c>); la aprueba otra persona (<c>.SelfApproval</c> compara <c>SEC_Users.Id</c>); queda
/// <c>Active</c> con <c>PayableFrom</c> = aprobación + <c>CuentasDestino.DiasDeEspera</c> (sin valor, sin espera); un cambio es una fila nueva que
/// deja la anterior <c>Retired</c> (<c>ReplacesId</c>); hay rechazo y retiro con motivo; leer es <c>Core.PersonBankAccount.Read</c> y escribir
/// <c>Core.PersonBankAccount.Changed</c>.
/// </summary>
public class CuentasBancariasDePersonaTests
{
    private readonly EscenarioDeCuentasBancarias _e = new();

    private Task<Result<PersonBankAccountDto>> PedirAsync(Guid? certificacion, Guid? reemplaza = null, string numero = "123-456 789") =>
        new RequestPersonBankAccountCommandHandler(_e.Db, _e.Actor, _e.Alertas, _e.Servicios).Handle(
            new RequestPersonBankAccountCommand(_e.Persona.PublicId, _e.Banco.PublicId, "Ahorros", numero, "Ana Pérez", "1010", certificacion, reemplaza),
            CancellationToken.None);

    private Task<Result<PersonBankAccountDto>> AprobarAsync(Guid cuenta) =>
        new ApprovePersonBankAccountCommandHandler(_e.Db, _e.Actor, _e.Parametros, _e.Reloj, _e.Servicios).Handle(
            new ApprovePersonBankAccountCommand(cuenta), CancellationToken.None);

    [Fact]
    public async Task Sin_certificacion_no_se_pide()
    {
        var r = await PedirAsync(null);
        r.Error.Code.Should().Be(ErroresDeCuentasBancariasDePersona.CodigoCertificacionRequerida);

        var deOtra = await PedirAsync(_e.CertificacionDe(_e.Otra));
        deOtra.Error.Code.Should().Be(ErroresDeCuentasBancariasDePersona.CodigoCertificacionRequerida, "la certificación es de la misma persona");
    }

    [Fact]
    public async Task El_alta_nace_pendiente_con_el_numero_normalizado_y_deja_su_evento()
    {
        var r = await PedirAsync(_e.Certificacion);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Status.Should().Be(PersonBankAccountState.PendingApproval);
        r.Value.AccountNumber.Should().Be("123456789");
        r.Value.RequestedByName.Should().Be("Paula Pide");
        r.Value.PayableFrom.Should().BeNull();
        await _e.Auditoria.Received().LogAsync(Arg.Is<AuditLogCommand>(c => c.Action == "Core.PersonBankAccount.Changed"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Quien_la_pidio_no_la_aprueba_ni_la_rechaza()
    {
        var pedida = (await PedirAsync(_e.Certificacion)).Value;

        (await AprobarAsync(pedida.PublicId)).Error.Code.Should().Be(ErroresDeCuentasBancariasDePersona.CodigoAutoaprobacion);
        var rechazo = await new RejectPersonBankAccountCommandHandler(_e.Db, _e.Actor, _e.Reloj, _e.Servicios)
            .Handle(new RejectPersonBankAccountCommand(pedida.PublicId, "no cuadra"), CancellationToken.None);
        rechazo.Error.Code.Should().Be(ErroresDeCuentasBancariasDePersona.CodigoAutoaprobacion);
    }

    [Fact]
    public async Task Otra_persona_la_aprueba_y_sin_espera_se_le_gira_desde_hoy()
    {
        var pedida = (await PedirAsync(_e.Certificacion)).Value;
        _e.ComoQuienAprueba();

        var r = await AprobarAsync(pedida.PublicId);

        r.Value.Status.Should().Be(PersonBankAccountState.Active);
        r.Value.DecidedByName.Should().Be("Andrés Aprueba");
        r.Value.PayableFrom.Should().Be(EscenarioDeCuentasBancarias.Hoy);
    }

    [Fact]
    public async Task Con_espera_se_le_gira_desde_la_aprobacion_mas_los_dias()
    {
        var pedida = (await PedirAsync(_e.Certificacion)).Value;
        _e.ComoQuienAprueba();
        _e.Espera(3);

        (await AprobarAsync(pedida.PublicId)).Value.PayableFrom.Should().Be(EscenarioDeCuentasBancarias.Hoy.AddDays(3));
    }

    [Fact]
    public async Task Un_cambio_es_una_fila_nueva_y_al_aprobarse_retira_la_anterior()
    {
        var primera = (await PedirAsync(_e.Certificacion)).Value;
        _e.ComoQuienAprueba();
        await AprobarAsync(primera.PublicId);

        _e.ComoQuienPide();
        var cambio = (await PedirAsync(_e.CertificacionDe(_e.Persona), primera.PublicId, "999")).Value;
        cambio.ReplacesPublicId.Should().Be(primera.PublicId);
        (await _e.Db.PersonBankAccounts.SingleAsync(c => c.PublicId == primera.PublicId)).Status.Should().Be(PersonBankAccountState.Active,
            "la anterior sigue activa hasta que otra persona apruebe el cambio");

        _e.ComoQuienAprueba();
        await AprobarAsync(cambio.PublicId);

        (await _e.Db.PersonBankAccounts.SingleAsync(c => c.PublicId == primera.PublicId)).Status.Should().Be(PersonBankAccountState.Retired);
        (await _e.Db.PersonBankAccounts.CountAsync()).Should().Be(2, "la historia de a qué cuenta se giró no se reescribe");
    }

    [Fact]
    public async Task Se_rechaza_con_motivo_y_una_activa_se_retira()
    {
        var pedida = (await PedirAsync(_e.Certificacion)).Value;
        _e.ComoQuienAprueba();
        var rechazada = await new RejectPersonBankAccountCommandHandler(_e.Db, _e.Actor, _e.Reloj, _e.Servicios)
            .Handle(new RejectPersonBankAccountCommand(pedida.PublicId, "La certificación es de otra cuenta"), CancellationToken.None);
        rechazada.Value.Status.Should().Be(PersonBankAccountState.Rejected);
        rechazada.Value.Reason.Should().Be("La certificación es de otra cuenta");

        _e.ComoQuienPide();
        var otra = (await PedirAsync(_e.CertificacionDe(_e.Persona), numero: "555")).Value;
        _e.ComoQuienAprueba();
        await AprobarAsync(otra.PublicId);
        var retirada = await new RetirePersonBankAccountCommandHandler(_e.Db, _e.Reloj, _e.Servicios)
            .Handle(new RetirePersonBankAccountCommand(otra.PublicId, "Cerró la cuenta"), CancellationToken.None);
        retirada.Value.Status.Should().Be(PersonBankAccountState.Retired);

        var otraVez = await new RetirePersonBankAccountCommandHandler(_e.Db, _e.Reloj, _e.Servicios)
            .Handle(new RetirePersonBankAccountCommand(otra.PublicId, "otra vez"), CancellationToken.None);
        otraVez.Error.Code.Should().Be(ErroresDeCuentasBancariasDePersona.CodigoNoActiva);
    }

    [Fact]
    public async Task La_cuenta_de_otra_persona_levanta_la_alerta_de_cuenta_compartida()
    {
        _e.Db.PersonBankAccounts.Add(new Domain.Entities.Core.PersonBankAccount
        {
            PersonId = _e.Otra.Id, BankId = _e.Banco.Id, AccountType = "Ahorros", AccountNumber = "123456789", HolderName = "Luis",
            HolderTaxId = "2020", Status = PersonBankAccountState.Active, RequestedByName = "x",
        });
        await _e.Db.SaveChangesAsync();

        await PedirAsync(_e.Certificacion);

        await _e.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.PlataformaCuentaDestinoCompartida),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leer_las_cuentas_es_una_lectura_sensible_auditada()
    {
        await PedirAsync(_e.Certificacion);

        var r = await new ListPersonBankAccountsQueryHandler(_e.Db, _e.Servicios)
            .Handle(new ListPersonBankAccountsQuery(_e.Persona.PublicId), CancellationToken.None);

        r.Value.Should().HaveCount(1);
        await _e.Auditoria.Received(1).LogAsync(Arg.Is<AuditLogCommand>(c => c.Action == "Core.PersonBankAccount.Read" && c.Module == "Treasury"),
            Arg.Any<CancellationToken>());
    }
}
