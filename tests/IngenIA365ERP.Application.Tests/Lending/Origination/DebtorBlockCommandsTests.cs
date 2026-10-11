using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0378 (api.md §7.2, FR-060): los bloqueos y embargos. A mano sólo <c>Manual</c> y <c>Garnishment</c>, con motivo; uno vigente de la
/// misma clase → <c>Lending.Block.Duplicate</c>; levantar uno automático → <c>Lending.Block.Automatic</c> con <c>{ kind, howItLifts }</c>; el
/// levantamiento se escribe una sola vez; un bloqueo vigente responde <c>Lending.Debtor.Blocked</c> al originar (lo lee el lector único).
/// </summary>
public sealed class DebtorBlockCommandsTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;

    public async Task InitializeAsync()
    {
        _o = await OriginacionDePrueba.CrearAsync();
        _o.G.ActuarComo(_o.Analista);
    }

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Result<DebtorBlockDto>> RegistrarAsync(DebtorBlockReason clase, string motivo = "Oficio 123 del juzgado 2 civil")
    {
        var r = await new RegisterDebtorBlockCommandHandler(_o.Db, _o.BloqueosReales(), _o.G.Actor, _o.G.Reloj)
            .Handle(new RegisterDebtorBlockCommand(_o.Ana.PublicId, clase, motivo) { OperationKey = Guid.NewGuid() }, default);
        _o.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<Result> LevantarAsync(Guid bloqueo, string motivo = "Levantamiento ordenado por el juzgado")
    {
        var r = await new LiftDebtorBlockCommandHandler(_o.Db, _o.G.Actor, _o.G.Reloj)
            .Handle(new LiftDebtorBlockCommand(bloqueo, motivo) { OperationKey = Guid.NewGuid() }, default);
        _o.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public void A_mano_solo_el_manual_y_el_embargo_y_con_motivo()
    {
        var validador = new RegisterDebtorBlockCommandValidator();

        validador.Validate(new RegisterDebtorBlockCommand(Guid.NewGuid(), DebtorBlockReason.Garnishment, "Oficio 77")).IsValid.Should().BeTrue();
        validador.Validate(new RegisterDebtorBlockCommand(Guid.NewGuid(), DebtorBlockReason.Manual, "Decisión de la gerencia")).IsValid.Should().BeTrue();
        validador.Validate(new RegisterDebtorBlockCommand(Guid.NewGuid(), DebtorBlockReason.Arrears, "Mora")).IsValid.Should().BeFalse("la mora la bloquea el sistema");
        validador.Validate(new RegisterDebtorBlockCommand(Guid.NewGuid(), DebtorBlockReason.Manual, " ")).IsValid.Should().BeFalse("sin motivo");
        new LiftDebtorBlockCommandValidator().Validate(new LiftDebtorBlockCommand(Guid.NewGuid(), "")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Registrar_un_embargo_lo_deja_vigente_con_su_motivo_y_su_propio_disparador()
    {
        var r = await RegistrarAsync(DebtorBlockReason.Garnishment);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Origin.Should().Be("Manual");
        r.Value.Reason.Should().Be("Oficio 123 del juzgado 2 civil");
        var fila = await _o.Db.DebtorBlocks.SingleAsync();
        fila.IsAutomatic.Should().BeFalse();
        fila.SourceKey.Should().Be(fila.PublicId.ToString());
        fila.CreatedByUserId.Should().Be(_o.Analista.Id);
        fila.StartedOn.Should().Be(OriginacionDePrueba.Hoy);
    }

    [Fact]
    public async Task Uno_vigente_de_la_misma_clase_es_Duplicate()
    {
        (await RegistrarAsync(DebtorBlockReason.Manual)).IsSuccess.Should().BeTrue();

        var r = await RegistrarAsync(DebtorBlockReason.Manual, "Otra vez");

        r.Error.Code.Should().Be("Lending.Block.Duplicate");
        OriginacionDePrueba.Datos(r.Error).GetProperty("kind").GetString().Should().Be("Manual");
        (await RegistrarAsync(DebtorBlockReason.Garnishment)).IsSuccess.Should().BeTrue("un embargo es otra clase");
    }

    [Fact]
    public async Task Un_bloqueo_automatico_no_se_levanta_a_mano()
    {
        var automatico = DebtorBlock.Nuevo(_o.Ana.Id, DebtorBlockReason.RestrictiveListMatch, Guid.NewGuid().ToString(), OriginacionDePrueba.Hoy, null, null);
        _o.Db.DebtorBlocks.Add(automatico);
        await _o.Db.SaveChangesAsync();

        var r = await LevantarAsync(automatico.PublicId);

        r.Error.Code.Should().Be("Lending.Block.Automatic");
        var datos = OriginacionDePrueba.Datos(r.Error);
        datos.GetProperty("kind").GetString().Should().Be("RestrictiveListMatch");
        datos.GetProperty("howItLifts").GetString().Should().Contain("oficial");
    }

    [Fact]
    public async Task El_levantamiento_se_escribe_una_sola_vez()
    {
        var bloqueo = (await RegistrarAsync(DebtorBlockReason.Garnishment)).Value;

        (await LevantarAsync(bloqueo.PublicId)).IsSuccess.Should().BeTrue();
        var levantado = await _o.Db.DebtorBlocks.SingleAsync();
        levantado.LiftedOn.Should().Be(OriginacionDePrueba.Hoy);
        levantado.LiftedByUserId.Should().Be(_o.Analista.Id);
        levantado.LiftReason.Should().Be("Levantamiento ordenado por el juzgado");

        (await LevantarAsync(bloqueo.PublicId, "Otra vez")).Error.Code.Should().Be("Lending.Block.AlreadyLifted");
        (await LevantarAsync(Guid.NewGuid())).Error.Code.Should().Be("Lending.Block.NotFound");
    }

    [Fact]
    public async Task La_lista_filtra_por_persona_clase_y_vigencia()
    {
        var embargo = (await RegistrarAsync(DebtorBlockReason.Garnishment)).Value;
        await RegistrarAsync(DebtorBlockReason.Manual);
        await LevantarAsync(embargo.PublicId);
        var listar = new ListDebtorBlocksQueryHandler(_o.Db);

        (await listar.Handle(new ListDebtorBlocksQuery(_o.Ana.PublicId), default)).Value.TotalCount.Should().Be(2);
        (await listar.Handle(new ListDebtorBlocksQuery(Active: true), default)).Value.Items.Should().ContainSingle().Which.Kind.Should().Be(DebtorBlockReason.Manual);
        var levantados = (await listar.Handle(new ListDebtorBlocksQuery(Kind: DebtorBlockReason.Garnishment, Active: false), default)).Value.Items;
        levantados.Should().ContainSingle().Which.LiftedBy!.UserPublicId.Should().Be(_o.Analista.PublicId);
        (await listar.Handle(new ListDebtorBlocksQuery(_o.Beto.PublicId), default)).Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_bloqueo_vigente_responde_Debtor_Blocked_al_originar()
    {
        var real = _o.BloqueosReales();
        _o.Bloqueos.VigentesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(ci => real.VigentesAsync(ci.Arg<IReadOnlyCollection<int>>(), ci.Arg<CancellationToken>()));
        (await RegistrarAsync(DebtorBlockReason.Manual, "Revisión de documentos falsos")).IsSuccess.Should().BeTrue();
        var dto = await _o.BorradorAsync();
        await _o.DocumentosAsync(dto);
        var s = await _o.Db.CreditApplications.SingleAsync(a => a.PublicId == dto.PublicId);
        var c = (await _o.Evaluacion().CargarAsync(s, default)).Value;

        var v = await _o.Validaciones().EvaluarAsync(new ContextoDeValidacion(s, c.Partes, c.Personas, c.Linea, c.Simulacion, CheckStage.AtSubmission,
            OriginacionDePrueba.Hoy), default);

        var bloqueo = v.Pendientes.Single(p => p.Codigo == "Lending.Debtor.Blocked");
        bloqueo.Resultado.Should().Be(CheckOutcome.Blocking);
        bloqueo.Exceptuable.Should().BeFalse();
    }
}
