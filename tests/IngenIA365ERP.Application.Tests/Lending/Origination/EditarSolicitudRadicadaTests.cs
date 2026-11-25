using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Pedido del dueño del 2026-11-25: una solicitud radicada (en decisión) se edita sin devolverla, si <c>Originacion.EditarRadicadaSinDevolver</c>
/// lo deja (de fábrica, sí), por su ruta y con motivo, en lo que deja <c>Originacion.GruposEditablesAlDevolver</c>. Queda en su historial
/// (<c>LND_CreditApplicationEdits</c>) y como intervención, vuelve a correr las validaciones y el enrutamiento, y cambia la huella: quien decide con
/// la versión anterior a la vista recibe <c>Lending.Decision.ContentChanged</c>.
/// </summary>
public sealed class EditarSolicitudRadicadaTests : IAsyncLifetime
{
    private const string Motivo = "El asociado pidió un monto menor";

    private OriginacionDePrueba _o = null!;

    public async Task InitializeAsync() => _o = await OriginacionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Result<CreditApplicationDto>> EditarAsync(CreditApplicationDto s, CreditApplicationInput entrada, string? motivo = Motivo,
        bool porSuRuta = true)
    {
        var r = await _o.Actualizar().Handle(new UpdateCreditApplicationCommand(s.PublicId, entrada)
        {
            OperationKey = Guid.NewGuid(),
            EditReason = motivo,
            EdicionRadicada = porSuRuta,
        }, default);
        _o.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<CreditApplicationDto> LeerAsync(Guid id) => (await _o.Leer().Handle(new GetCreditApplicationQuery(id), default)).Value;

    private async Task ParametroAsync(string clave, string valor)
    {
        _o.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba", CreatedBy = "test",
        });
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task La_radicada_se_edita_con_motivo_queda_en_su_historial_y_se_reenruta()
    {
        var s = await _o.RadicadaAsync();
        s.Status.Should().Be(CreditApplicationStatus.PendingDecision);
        (await LeerAsync(s.PublicId)).EditableGroups.Should().HaveCount(Domain.Lending.Origination.GruposEditablesAlDevolver.Nombres.Count,
            "de fábrica la radicada se edita y todo se puede cambiar");
        var huellaAntes = s.RoutingSha256;

        var r = await EditarAsync(s, _o.Solicitud(2_500_000m) with { Purpose = "Compra de vehículo" });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(CreditApplicationStatus.PendingDecision, "no se devuelve: sigue esperando decisión");
        r.Value.Amount.Should().Be(2_500_000m);
        r.Value.RoutingSha256.Should().NotBe(huellaAntes, "la edición cambia la huella del enrutamiento");
        var leida = await LeerAsync(s.PublicId);
        var edicion = leida.SubmittedEdits.Should().ContainSingle().Subject;
        edicion.Reason.Should().Be(Motivo);
        edicion.StatusAtEdit.Should().Be(CreditApplicationStatus.PendingDecision);
        edicion.Changes.Select(c => (c.Field, c.Before, c.After)).Should().Contain(
            [("Monto", "3000000", "2500000"), ("Destino", "Libre inversión", "Compra de vehículo")]);
        edicion.LevelAfter.Should().NotBeNull();
        (await _o.Db.CreditApplications.SingleAsync(a => a.PublicId == s.PublicId)).EditsAfterSubmission.Should().Be(1);
        (await _o.Db.CaseParticipations.AnyAsync(p => p.CaseRootPublicId == s.PublicId && p.Action == AccionesDeSegregacion.EditarRadicada))
            .Should().BeTrue("la edición queda como intervención en el expediente");
    }

    [Fact]
    public async Task Quien_decide_con_la_version_anterior_a_la_vista_recibe_que_cambio()
    {
        var s = await _o.RadicadaAsync();
        _o.G.ActuarComo(_o.Analista);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Analyst");
        var vista = await _o.Enrutado().Handle(new GetCreditApplicationRoutingQuery(s.PublicId), default);
        _o.G.ActuarComo(_o.Asesora);

        (await EditarAsync(s, _o.Solicitud(2_500_000m))).IsSuccess.Should().BeTrue();

        _o.G.ActuarComo(_o.Analista);
        var decision = await _o.Decidir().Handle(new RecordCreditDecisionCommand(LendingDecisionSubjects.CreditApplication, s.PublicId,
            CreditDecisionOutcome.Return, "Revisar", null, vista.Value.ContentSha256) { OperationKey = Guid.NewGuid() }, default);
        decision.IsFailure.Should().BeTrue();
        decision.Error.Code.Should().Be(ErroresDeGobierno.CodigoContenidoCambiado);
    }

    [Fact]
    public async Task Sin_motivo_no_se_edita()
    {
        var s = await _o.RadicadaAsync();

        var r = await EditarAsync(s, _o.Solicitud(2_500_000m), motivo: "  ");

        r.Error.Code.Should().Be(ErroresDeOriginacion.CodigoMotivoDeEdicion);
    }

    [Fact]
    public async Task Por_la_ruta_del_borrador_la_radicada_no_se_edita()
    {
        var s = await _o.RadicadaAsync();

        var r = await EditarAsync(s, _o.Solicitud(2_500_000m), porSuRuta: false);

        r.Error.Code.Should().Be(ErroresDeOriginacion.CodigoEdicionRadicadaPorSuRuta);
    }

    [Fact]
    public async Task Con_el_parametro_en_No_la_radicada_no_se_edita()
    {
        var s = await _o.RadicadaAsync();
        await ParametroAsync(ParametrosDeCartera.OriginacionEditarRadicadaSinDevolver, "false");

        (await LeerAsync(s.PublicId)).EditableGroups.Should().BeEmpty("la pantalla no ofrece editarla");
        var r = await EditarAsync(s, _o.Solicitud(2_500_000m));

        r.Error.Code.Should().Be(ErroresDeOriginacion.CodigoRadicadaNoEditable);
    }

    [Fact]
    public async Task Lo_que_el_parametro_deja_fijo_tampoco_cambia_en_la_radicada()
    {
        var s = await _o.RadicadaAsync();
        await ParametroAsync(ParametrosDeCartera.OriginacionGruposEditablesAlDevolver, "Destino");

        var monto = await EditarAsync(s, _o.Solicitud(2_500_000m));
        monto.Error.Code.Should().Be(ErroresDeOriginacion.CodigoNoCorregible);
        OriginacionDePrueba.Datos(monto.Error).GetProperty("groups")[0].GetString().Should().Be("Condiciones");

        var destino = await EditarAsync(s, _o.Solicitud() with { Purpose = "Educación" });
        destino.IsSuccess.Should().BeTrue(destino.IsFailure ? $"{destino.Error.Code}: {destino.Error.Message}" : string.Empty);
        destino.Value.Purpose.Should().Be("Educación");
    }

    [Fact]
    public async Task Sin_nada_distinto_no_queda_historial()
    {
        var s = await _o.RadicadaAsync();

        var r = await EditarAsync(s, _o.Solicitud());

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        (await LeerAsync(s.PublicId)).SubmittedEdits.Should().BeEmpty();
        (await _o.Db.CreditApplications.SingleAsync(a => a.PublicId == s.PublicId)).EditsAfterSubmission.Should().Be(0);
    }

    [Fact]
    public async Task Una_decidida_no_se_edita()
    {
        var s = await _o.RadicadaAsync();
        var fila = await _o.Db.CreditApplications.SingleAsync(a => a.PublicId == s.PublicId);
        fila.Status = CreditApplicationStatus.Denied;
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();

        var r = await EditarAsync(s, _o.Solicitud(2_500_000m));

        r.IsFailure.Should().BeTrue();
        (await LeerAsync(s.PublicId)).EditableGroups.Should().BeEmpty();
    }
}
