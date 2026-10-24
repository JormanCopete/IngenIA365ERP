using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.BankAccounts;

/// <summary>
/// Feature 015, T1, T0415 (FR-208): los firmantes y tokens de cada cuenta propia con su combinación por clase y monto y su vigencia; el alta y el retiro llevan acta y la
/// aprobación de otra persona (sujeto <c>BankAccountAuthorityChange</c>); un token vigente no se asigna dos veces; la vigilancia alerta cuando un firmante se retira de la
/// cooperativa. Prueba escrita, sin correr.
/// </summary>
public class FirmantesYTokensTests
{
    private readonly EscenarioDeTesoreria _t = new();

    private AuditoriaDeTesoreria Auditoria => new(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance);

    private RegisterBankAccountSignerCommandHandler Firmante() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Aprobacion, Auditoria, _t.E.Reloj);

    private RegisterBankAccountTokenCommandHandler Token() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Aprobacion, Auditoria, _t.E.Reloj);

    private RetireBankAccountAuthorityCommandHandler Retirar() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Aprobacion, Auditoria, _t.E.Reloj);

    private async Task<BankAccountAuthorityDto> ActivoAsync()
    {
        var r = await Firmante().Handle(new RegisterBankAccountSignerCommand(_t.Pagos.PublicId, _t.Beneficiaria.PublicId, "a", 10_000_000m, true, "Gerente", Guid.NewGuid())
            { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        (await new AprobableCambioDeFirmantes(_t.E.Db, _t.E.Reloj).AlAprobarAsync(new ApprovalRequest { SourcePublicId = r.Value.PublicId }, default)).IsSuccess.Should().BeTrue();
        await _t.E.Db.SaveChangesAsync();
        return r.Value;
    }

    [Fact]
    public async Task El_alta_queda_pendiente_de_la_aprobacion_de_otra_persona_y_al_aprobarse_rige()
    {
        var r = await Firmante().Handle(new RegisterBankAccountSignerCommand(_t.Pagos.PublicId, _t.Beneficiaria.PublicId, "A", null, true, null, Guid.NewGuid())
            { OperationKey = Guid.NewGuid() }, default);

        r.Value.Status.Should().Be(BankAccountAuthorityState.PendingApproval);
        r.Value.SignatureClass.Should().Be("A");
        await _t.Motor.Received(1).SolicitarAsync(Arg.Is<SolicitudDeAprobacion>(s => s.Subject == ApprovalSubjects.BankAccountAuthorityChange), Arg.Any<CancellationToken>());
        var activo = await ActivoAsync();
        activo.Should().NotBeNull();
    }

    [Fact]
    public async Task La_misma_persona_y_clase_no_se_registra_dos_veces()
    {
        await ActivoAsync();
        var otra = await Firmante().Handle(new RegisterBankAccountSignerCommand(_t.Pagos.PublicId, _t.Beneficiaria.PublicId, "A", null, true, null, Guid.NewGuid())
            { OperationKey = Guid.NewGuid() }, default);
        otra.Error.Code.Should().Be(ErroresDeTesoreria.CodigoFirmanteRepetido);
    }

    [Fact]
    public async Task El_retiro_exige_acta_y_aprobacion_y_cierra_la_vigencia()
    {
        var f = await ActivoAsync();

        var r = await Retirar().Handle(new RetireBankAccountAuthorityCommand(f.PublicId, "Renunció", Guid.NewGuid()) { OperationKey = Guid.NewGuid() }, default);
        r.Value.Status.Should().Be(BankAccountAuthorityState.PendingRetirement);
        (await new AprobableCambioDeFirmantes(_t.E.Db, _t.E.Reloj).AlAprobarAsync(new ApprovalRequest { SourcePublicId = f.PublicId }, default)).IsSuccess.Should().BeTrue();
        await _t.E.Db.SaveChangesAsync();

        var fila = await _t.E.Db.BankAccountSigners.SingleAsync();
        fila.Status.Should().Be(BankAccountAuthorityState.Retired);
        fila.ValidTo.Should().Be(EscenarioDeRecaudo.Hoy);
        new RetireBankAccountAuthorityCommandValidator().Validate(new RetireBankAccountAuthorityCommand(f.PublicId, "x", Guid.Empty)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Un_token_vigente_no_se_asigna_dos_veces()
    {
        var primero = await Token().Handle(new RegisterBankAccountTokenCommand(_t.Pagos.PublicId, _t.Beneficiaria.PublicId, "TK-1", "Aprobar", Guid.NewGuid())
            { OperationKey = Guid.NewGuid() }, default);
        var segundo = await Token().Handle(new RegisterBankAccountTokenCommand(_t.FondoDeLiquidez.PublicId, _t.Beneficiaria.PublicId, "TK-1", null, Guid.NewGuid())
            { OperationKey = Guid.NewGuid() }, default);

        primero.IsSuccess.Should().BeTrue();
        segundo.Error.Code.Should().Be(ErroresDeTesoreria.CodigoTokenRepetido);
    }

    [Fact]
    public async Task Alerta_cuando_un_firmante_se_retira_de_la_cooperativa()
    {
        await ActivoAsync();
        var fila = await _t.E.Db.BankAccountSigners.SingleAsync();
        var cajera = await _t.E.Db.Users.SingleAsync(u => u.Id == _t.E.Cajera.Id);
        cajera.IsActive = false;
        fila.UserId = cajera.Id;
        await _t.E.Db.SaveChangesAsync();

        var alertados = await new VigilanciaDeFirmantes(_t.E.Db, _t.E.Alertas, _t.E.Reloj).FirmantesRetiradosAsync(default);

        alertados.Should().Be(1);
        await _t.E.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.TesoreriaFirmanteRetirado), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Quien_pide_el_cambio_no_lo_aprueba()
    {
        var fuente = new FuenteDeAprobacionDeTesoreria(_t.E.Db, _t.E.Personas, [new AprobableCambioDeFirmantes(_t.E.Db, _t.E.Reloj)]);
        var solicitud = new ApprovalRequest { Subject = ApprovalSubjects.BankAccountAuthorityChange, RequestedByUserId = _t.E.Cajera.Id, CreatedByUserId = _t.E.Cajera.Id };

        var r = await fuente.AlAprobarElNivelAsync(solicitud, _t.E.Cajera.Id, default, esElUltimo: true, default);

        r.Error.Code.Should().Be(ErroresDeTesoreria.CodigoCambioDeFirmantesAutoaprobado);
    }
}
