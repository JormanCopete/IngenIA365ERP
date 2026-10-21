using FluentAssertions;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0368 (api.md §4.2, data-model §2.1–§2.3): el borrador de la solicitud. Crear, editar, adjuntar y quitar documentos con las reglas
/// de la originación: solicitante no asociado sin que la línea y el perfil lo admitan (<c>Lending.Application.NotAssociate</c>), parte repetida
/// (<c>.DuplicateParty</c>), línea cerrada, nómina propia sin ser empleado, pagaduría externa (K2), edición fuera de <c>Draft</c>/<c>Returned</c>
/// (<c>.NotEditable</c>), versión leída vieja (<c>Concurrency.StaleRowVersion</c>), las autorizaciones del cuerpo por
/// <c>IAutorizacionDeDatos.AgregarAsync</c> sin repetir las vigentes, requisito desconocido, documento de una solicitud decidida
/// (<c>Attachments.OwnerLocked</c>) y Cartera sin activar (<c>Lending.NotActivated</c>).
/// </summary>
public sealed class CreditApplicationDraftCommandsTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;

    public async Task InitializeAsync() => _o = await OriginacionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private async Task<IngenIA365ERP.Application.Common.Models.Error> FallaAlCrear(CreditApplicationInput e)
    {
        var r = await _o.Crear().Handle(new CreateCreditApplicationCommand(e) { OperationKey = Guid.NewGuid() }, default);
        r.IsFailure.Should().BeTrue("la solicitud no debía crearse");
        (await _o.Db.CreditApplications.CountAsync()).Should().Be(0, "no se guarda nada");
        return r.Error;
    }

    [Fact]
    public async Task Crea_el_borrador_sin_numero_con_sus_partes_y_lo_simulado()
    {
        var s = await _o.BorradorAsync(_o.Solicitud(3_000_000m, 24, null, _o.Beto));

        s.Status.Should().Be(CreditApplicationStatus.Draft);
        s.Number.Should().BeNull("un borrador no consume número");
        s.Applicant.PersonPublicId.Should().Be(_o.Ana.PublicId);
        s.DebtorKind.Should().Be(DebtorKind.Associate);
        s.Office!.PublicId.Should().Be(_o.Norte.PublicId);
        s.Line.Code.Should().Be("CONLIB");
        s.Line.VersionPublicId.Should().NotBeNull();
        s.Installments.Should().Be(24);
        s.TermMonths.Should().Be(24);
        s.PrepaymentPreference.Should().Be(PrepaymentChoice.ReduceTerm);
        s.SimulationSha256.Should().MatchRegex("^[0-9a-f]{64}$");
        s.Parties.Should().HaveCount(2);
        s.Parties[0].Role.Should().Be(CreditPartyRole.Applicant);
        s.Parties[0].MonthlyIncome.Should().Be(5_000_000m);
        s.Parties[1].Person.PersonPublicId.Should().Be(_o.Beto.PublicId);
        s.Requirements.Should().ContainSingle(r => r.Code == "CEDULA" && r.Mandatory);
    }

    [Fact]
    public async Task Un_no_asociado_necesita_una_linea_y_un_perfil_que_lo_admitan()
    {
        var error = await FallaAlCrear(_o.Solicitud(solicitante: _o.Carla));
        error.Code.Should().Be("Lending.Application.NotAssociate");
        OriginacionDePrueba.Datos(error).GetProperty("personPublicId").GetGuid().Should().Be(_o.Carla.PublicId);

        _o.G.ConActividadFinanciera(false);
        var abierta = await _o.L.LineaAsync("NOASOC", OriginacionDePrueba.VersionDeLinea() with { AllowsNonAssociates = true });
        var s = await _o.BorradorAsync(_o.Solicitud(solicitante: _o.Carla) with { CreditLinePublicId = abierta.PublicId });
        s.DebtorKind.Should().Be(DebtorKind.Customer);
    }

    [Fact]
    public async Task Una_persona_dos_veces_es_DuplicateParty()
    {
        (await FallaAlCrear(_o.Solicitud(3_000_000m, 24, null, _o.Beto, _o.Beto))).Code.Should().Be("Lending.Application.DuplicateParty");
        (await FallaAlCrear(_o.Solicitud(3_000_000m, 24, null, _o.Ana))).Code.Should().Be("Lending.Application.DuplicateParty");
    }

    [Fact]
    public async Task Una_linea_cerrada_no_admite_solicitudes()
    {
        await _o.L.Cerrar().Handle(new CloseCreditLineCommand(_o.Linea.PublicId, "Cierre") { OperationKey = Guid.NewGuid() }, default);
        _o.Db.ChangeTracker.Clear();

        (await FallaAlCrear(_o.Solicitud())).Code.Should().Be("Lending.CreditLine.Closed");
    }

    [Fact]
    public async Task Lo_que_se_sale_de_la_linea_no_se_radica()
    {
        var error = await FallaAlCrear(_o.Solicitud(45_000_000m));
        error.Code.Should().Be("Lending.Schedule.OutOfLineRange");
        OriginacionDePrueba.Datos(error).GetProperty("field").GetString().Should().Be("amount");
    }

    [Fact]
    public async Task La_nomina_propia_exige_que_el_solicitante_sea_empleado()
    {
        var error = await FallaAlCrear(_o.Solicitud(solicitante: _o.Beto) with { PaymentForm = RepaymentChannel.OwnPayroll });
        error.Code.Should().Be("Lending.Application.PaymentFormNotAllowed");
        OriginacionDePrueba.Datos(error).GetProperty("reason").GetString().Should().Be("NotEmployee");

        var s = await _o.BorradorAsync(_o.Solicitud() with { PaymentForm = RepaymentChannel.OwnPayroll });
        s.PaymentForm.Should().Be(RepaymentChannel.OwnPayroll);
    }

    [Fact]
    public async Task Una_forma_de_pago_que_la_linea_no_ofrece_no_se_admite()
    {
        var error = await FallaAlCrear(_o.Solicitud() with { PaymentForm = RepaymentChannel.DirectDebit });
        OriginacionDePrueba.Datos(error).GetProperty("reason").GetString().Should().Be("NotInLine");
    }

    [Fact]
    public async Task Un_convenio_de_pagaduria_sin_la_forma_de_pago_libranza_no_se_admite()
    {
        // K2 (T0842): el convenio sólo va con la forma de pago «libranza con pagaduría»; la libranza completa la prueban RadicacionPorLibranzaTests.
        var error = await FallaAlCrear(_o.Solicitud() with { Payroll = new PayrollInput(PayrollAgreementPublicId: Guid.NewGuid()) });
        error.Code.Should().Be("Lending.PayrollAgreement.NotAvailable");
    }

    [Fact]
    public async Task Se_edita_solo_en_borrador_o_devuelta_y_con_la_version_vigente()
    {
        var s = await _o.BorradorAsync();
        var editada = await _o.Actualizar().Handle(new UpdateCreditApplicationCommand(s.PublicId, _o.Solicitud(4_000_000m, 12, null, _o.Beto))
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        editada.IsSuccess.Should().BeTrue(editada.IsFailure ? editada.Error.Message : string.Empty);
        editada.Value.Amount.Should().Be(4_000_000m);
        editada.Value.Parties.Should().HaveCount(2);
        _o.Db.ChangeTracker.Clear();

        // Sin Beto: sale de la solicitud (baja lógica).
        var sinBeto = await _o.Actualizar().Handle(new UpdateCreditApplicationCommand(s.PublicId, _o.Solicitud(4_000_000m, 12))
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        sinBeto.Value.Parties.Should().ContainSingle();
        _o.Db.ChangeTracker.Clear();

        // InMemory no guarda RowVersion: la fila seguida lleva la «vigente» y el handler la lee de la misma unidad de trabajo.
        var fila = await _o.Db.CreditApplications.SingleAsync(a => a.PublicId == s.PublicId);
        fila.RowVersion = [1, 2, 3];
        var vieja = await _o.Actualizar().Handle(new UpdateCreditApplicationCommand(s.PublicId, _o.Solicitud(), [9, 9, 9])
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        vieja.Error.Code.Should().Be("Concurrency.StaleRowVersion");
        _o.Db.ChangeTracker.Clear();

        fila = await _o.Db.CreditApplications.SingleAsync(a => a.PublicId == s.PublicId);
        fila.Status = CreditApplicationStatus.PendingDecision;
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();
        var noEditable = await _o.Actualizar().Handle(new UpdateCreditApplicationCommand(s.PublicId, _o.Solicitud()) { OperationKey = Guid.NewGuid() },
            default);
        noEditable.Error.Code.Should().Be("Lending.Application.NotEditable");
        OriginacionDePrueba.Datos(noEditable.Error).GetProperty("status").GetString().Should().Be("PendingDecision");
    }

    [Fact]
    public async Task Las_autorizaciones_del_cuerpo_se_agregan_sin_repetir_las_vigentes()
    {
        _o.QuitarAutorizacion(_o.Beto, DataAuthorizationPurpose.CreditProcessing);
        var evidencia = Guid.NewGuid();

        await _o.BorradorAsync(_o.Solicitud(3_000_000m, 24, null, _o.Beto) with
        {
            Authorizations =
            [
                new AuthorizationInput(_o.Ana.PublicId, DataAuthorizationPurpose.CreditProcessing, EvidenceAttachmentPublicId: Guid.NewGuid()),
                new AuthorizationInput(_o.Beto.PublicId, DataAuthorizationPurpose.CreditProcessing, EvidenceAttachmentPublicId: evidencia),
            ],
        });

        var agregada = _o.Agregadas.Should().ContainSingle("la de Ana ya está vigente").Subject;
        agregada.PersonPublicId.Should().Be(_o.Beto.PublicId);
        agregada.Purpose.Should().Be(DataAuthorizationPurpose.CreditProcessing);
        agregada.EvidenceAttachmentPublicId.Should().Be(evidencia);
        agregada.Origin.Should().Be(DataAuthorizationOrigin.CreditApplication);
    }

    [Fact]
    public async Task Un_requisito_que_la_linea_no_pide_es_RequirementUnknown()
    {
        var s = await _o.BorradorAsync();
        var adjunto = _o.Adjunto(s.PublicId);

        var r = await _o.Adjuntar().Handle(new AttachCreditApplicationDocumentCommand(s.PublicId, "PASAPORTE", adjunto.PublicId)
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.Error.Code.Should().Be("Lending.Application.RequirementUnknown");
        OriginacionDePrueba.Datos(r.Error).GetProperty("requirementCode").GetString().Should().Be("PASAPORTE");
    }

    [Fact]
    public async Task Un_adjunto_que_no_es_de_la_solicitud_no_se_acepta()
    {
        var s = await _o.BorradorAsync();
        var deOtra = _o.Adjunto(Guid.NewGuid());

        var r = await _o.Adjuntar().Handle(new AttachCreditApplicationDocumentCommand(s.PublicId, "CEDULA", deOtra.PublicId)
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.Error.Code.Should().Be("Attachments.NotAvailable");
    }

    [Fact]
    public async Task Un_documento_se_quita_antes_de_decidir_y_despues_queda_bloqueado()
    {
        var s = await _o.BorradorAsync();
        await _o.DocumentosAsync(s);
        var documento = await _o.Db.CreditApplicationDocuments.AsNoTracking().SingleAsync();

        (await _o.Quitar().Handle(new RemoveCreditApplicationDocumentCommand(s.PublicId, documento.PublicId) { OperationKey = Guid.NewGuid() }, default))
            .IsSuccess.Should().BeTrue();
        (await _o.Db.CreditApplicationDocuments.AsNoTracking().IgnoreQueryFilters().SingleAsync()).IsDeleted.Should().BeTrue();

        await _o.DocumentosAsync(s);
        var otro = await _o.Db.CreditApplicationDocuments.AsNoTracking().SingleAsync();
        var fila = await _o.Db.CreditApplications.SingleAsync(a => a.PublicId == s.PublicId);
        fila.Status = CreditApplicationStatus.Denied;
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();

        var r = await _o.Quitar().Handle(new RemoveCreditApplicationDocumentCommand(s.PublicId, otro.PublicId) { OperationKey = Guid.NewGuid() }, default);
        r.Error.Code.Should().Be("Attachments.OwnerLocked");
        var adjuntar = await _o.Adjuntar().Handle(new AttachCreditApplicationDocumentCommand(s.PublicId, "CEDULA", _o.Adjunto(s.PublicId).PublicId)
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        adjuntar.Error.Code.Should().Be("Lending.Application.NotEditable");
    }

    [Fact]
    public async Task Fuera_del_alcance_la_solicitud_no_existe()
    {
        var s = await _o.BorradorAsync();
        _o.G.AlcanceActual = IngenIA365ERP.Application.Common.Interfaces.Security.AlcanceDeCartera.Vacio;

        var r = await _o.Actualizar().Handle(new UpdateCreditApplicationCommand(s.PublicId, _o.Solicitud()) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Lending.Application.NotFound");
        (await _o.G.EventosAsync()).Should().Contain("Lending.Access.OutOfScope");
    }

    [Fact]
    public async Task Sin_persona_no_se_arma_una_solicitud()
    {
        _o.G.ActuarComo(_o.SinPersona);

        (await FallaAlCrear(_o.Solicitud())).Code.Should().Be("Lending.Security.UserWithoutPerson");
    }

    [Fact]
    public async Task Con_Cartera_sin_activar_no_se_radica_nada()
    {
        using var o = await OriginacionDePrueba.CrearAsync(activada: false);

        var r = await o.Crear().Handle(new CreateCreditApplicationCommand(o.Solicitud()) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Lending.NotActivated");
    }
}
