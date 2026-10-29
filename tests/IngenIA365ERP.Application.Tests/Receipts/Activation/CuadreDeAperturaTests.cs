using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Cuadres;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Receipts.Activation;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Common.Cuadres;
using IngenIA365ERP.Domain.Entities.Receipts.Activation;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Activation;

/// <summary>
/// Feature 015, US13, T0284 (FR-158, FR-160, FR-228; decisión del dueño): el cuadre de apertura de Recaudo por concepto y oficina contra <c>SaldoContableAsync</c> a la
/// fecha de corte; el definitivo con diferencia cero (<c>Squared</c>) o con acta (<c>AcceptedWithMinutes</c>), y sin acta no se registra; el provisional con motivo y
/// fecha límite posterior al corte, aprobado por quien lo registra; el definitivo queda enlazado al provisional, que pasa a reemplazado; las cifras de referencia se
/// cotejan contra lo cargado y lo contable. Prueba escrita, sin correr.
/// </summary>
public class CuadreDeAperturaTests
{
    private static readonly DateOnly Corte = new(2026, 11, 30);
    private readonly EscenarioDeRecaudo _e = new();
    private readonly IContabilidadParaRecaudo _contabilidad = Substitute.For<IContabilidadParaRecaudo>();

    public CuadreDeAperturaTests()
    {
        _contabilidad.SaldoContableAsync(Arg.Any<SolicitudDeSaldo>(), Arg.Any<CancellationToken>()).Returns([]);
        _e.Db.UnidentifiedItems.Add(new UnidentifiedItem
        {
            Number = "PI-1", OwnBankAccountPublicId = Guid.NewGuid(), BranchId = _e.OficinaA.Id, MovementDate = Corte, Amount = 150_000m, Balance = 150_000m,
            Status = UnidentifiedItemState.Open, OriginKind = UnidentifiedItemOrigin.OpeningLoad, OpenedOn = Corte,
        });
        _e.Db.SaveChanges();
    }

    private void Contable(string concepto, decimal saldo) =>
        _contabilidad.SaldoContableAsync(Arg.Is<SolicitudDeSaldo>(s => s.Concept == concepto && s.OfficePublicId == _e.OficinaA.PublicId), Arg.Any<CancellationToken>())
            .Returns([new SaldoContableDto(concepto, _e.OficinaA.PublicId, null, saldo)]);

    private LineasDelCuadreDeRecaudo Lineas => new(_e.Db, _contabilidad);

    private AuditoriaDeRecaudo Auditoria => new(_e.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance);

    [Fact]
    public async Task Las_partidas_se_comparan_con_el_saldo_contable_de_su_rol_con_el_signo_del_pasivo()
    {
        Contable("PartidasPorIdentificar", -150_000m);

        var calculo = await Lineas.ProponerAsync(Corte, default);

        var linea = calculo.Lineas.Single(l => l.Concepto == "PartidasPorIdentificar");
        linea.Cargado.Should().Be(150_000m);
        linea.Contable.Should().Be(150_000m, "el pasivo tiene saldo crédito y se compara con el signo cambiado");
        linea.Cuadra.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_dato_contable_no_cuadra_y_el_definitivo_sin_acta_no_se_registra()
    {
        var r = await new RegisterDefinitiveOpeningSquareCommandHandler(_e.Db, Lineas, _e.Actor, Auditoria, _e.Reloj)
            .Handle(new RegisterDefinitiveOpeningSquareCommand(Corte), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDelCuadre.CodigoConDiferencia("Receipts"));
        (await _e.Db.ReceiptsOpeningSquares.CountAsync()).Should().Be(0);
    }

    /// <summary>Segunda ronda del 2026-10-27 (D-g): el acta como adjunto de su tipo de la configuración de Recaudo.</summary>
    private Guid Acta(string tipo = AdjuntosDeModulo.ActaDelCuadreDeApertura, Guid? dueno = null)
    {
        var setup = _e.Db.ReceiptsSetups.FirstOrDefault();
        if (setup is null)
        {
            setup = new Domain.Entities.Receipts.Configuration.ReceiptsSetup();
            _e.Db.ReceiptsSetups.Add(setup);
            _e.Db.SaveChanges();
        }
        var a = new Domain.Entities.Core.Attachment
        {
            OwnerEntityType = tipo, OwnerEntityPublicId = dueno ?? setup.PublicId, FileName = "acta.pdf", ContentType = "application/pdf", SizeBytes = 10,
        };
        _e.Db.Attachments.Add(a);
        _e.Db.SaveChanges();
        return a.PublicId;
    }

    [Fact]
    public async Task El_acta_tiene_que_ser_un_adjunto_de_su_tipo_de_la_configuracion_y_citada_no_se_borra()
    {
        var handler = new RegisterDefinitiveOpeningSquareCommandHandler(_e.Db, Lineas, _e.Actor, Auditoria, _e.Reloj);
        foreach (var invalida in new[] { Guid.NewGuid(), Acta(tipo: "ReceiptsCashSupport"), Acta(dueno: Guid.NewGuid()) })
            (await handler.Handle(new RegisterDefinitiveOpeningSquareCommand(Corte, invalida), default)).Error.Code
                .Should().Be(ErroresDelCuadre.CodigoActaInvalida("Receipts"));
        var acta = Acta();
        (await AdjuntosDeModulo.PuedeBorrarAsync(_e.Db, AdjuntosDeModulo.ActaDelCuadreDeApertura, Guid.Empty, default,
            attachmentPublicId: acta)).Should().BeNull("sin cuadre que la cite se puede borrar");

        (await handler.Handle(new RegisterDefinitiveOpeningSquareCommand(Corte, acta), default)).IsSuccess.Should().BeTrue();

        (await AdjuntosDeModulo.PuedeBorrarAsync(_e.Db, AdjuntosDeModulo.ActaDelCuadreDeApertura, Guid.Empty, default,
            attachmentPublicId: acta))!.Code.Should().Be(AttachmentErrorCodes.OwnerLocked, "citada por un cuadre, se conserva");
    }

    [Fact]
    public async Task Con_acta_el_definitivo_queda_aceptado_con_su_diferencia()
    {
        var r = await new RegisterDefinitiveOpeningSquareCommandHandler(_e.Db, Lineas, _e.Actor, Auditoria, _e.Reloj)
            .Handle(new RegisterDefinitiveOpeningSquareCommand(Corte, Acta()), default);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Status.Should().Be(nameof(OpeningSquareState.AcceptedWithMinutes));
        r.Value.Lines.Should().Contain(l => l.Concept == "PartidasPorIdentificar" && !l.Squares);
    }

    [Fact]
    public async Task El_provisional_exige_fecha_limite_despues_del_corte()
    {
        var handler = new RegisterProvisionalOpeningSquareCommandHandler(_e.Db, Lineas, _e.Actor, Auditoria, _e.Reloj);

        var antes = await handler.Handle(new RegisterProvisionalOpeningSquareCommand("Falta la contabilidad", Corte, Corte), default);
        antes.IsFailure.Should().BeTrue();
        antes.Error.Code.Should().Be(ErroresDelCuadre.CodigoFechaLimite("Receipts"));

        var bien = await handler.Handle(new RegisterProvisionalOpeningSquareCommand("Falta la contabilidad", new DateOnly(2026, 12, 4), Corte), default);
        bien.IsSuccess.Should().BeTrue(bien.Error?.Message);
        bien.Value.Status.Should().Be(nameof(OpeningSquareState.ProvisionalApproved));
        bien.Value.ApprovedBy.Should().Be(_e.Cajera.Username, "quien lo registra con el permiso es quien lo aprueba");
        bien.Value.DeadlineOn.Should().Be(new DateOnly(2026, 12, 4));
    }

    [Fact]
    public async Task El_definitivo_queda_enlazado_al_provisional_que_reemplaza()
    {
        await new RegisterProvisionalOpeningSquareCommandHandler(_e.Db, Lineas, _e.Actor, Auditoria, _e.Reloj)
            .Handle(new RegisterProvisionalOpeningSquareCommand("Falta la contabilidad", new DateOnly(2026, 12, 4), Corte), default);
        Contable("PartidasPorIdentificar", -150_000m);

        var definitivo = await new RegisterDefinitiveOpeningSquareCommandHandler(_e.Db, Lineas, _e.Actor, Auditoria, _e.Reloj)
            .Handle(new RegisterDefinitiveOpeningSquareCommand(Corte), default);

        definitivo.IsSuccess.Should().BeTrue(definitivo.Error?.Message);
        definitivo.Value.Status.Should().Be(nameof(OpeningSquareState.Squared));
        var provisional = await _e.Db.ReceiptsOpeningSquares.SingleAsync(c => c.Kind == OpeningSquareKind.Provisional);
        provisional.Status.Should().Be(OpeningSquareState.Superseded);
        provisional.DefinitiveSquareId.Should().NotBeNull();

        var actual = await new GetReceiptsOpeningSquareQueryHandler(_e.Db, Lineas, _e.Reloj).Handle(new GetReceiptsOpeningSquareQuery(Corte), default);
        actual.Value.PendingProvisional.Should().BeNull();
        actual.Value.Current!.Kind.Should().Be(nameof(OpeningSquareKind.Definitive));
    }

    [Fact]
    public async Task Las_cifras_de_referencia_se_cotejan_contra_lo_cargado_y_lo_contable()
    {
        _e.Db.ReceiptsReferenceFigures.Add(new ReceiptsReferenceFigure
        {
            CutoffDate = Corte, Concept = "PartidasPorIdentificar", Dimension = _e.OficinaA.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), Amount = 160_000m,
            LoadedByUserId = _e.Cajera.Id, LoadedAt = DateTime.UtcNow, ImportBatchPublicId = Guid.NewGuid(),
        });
        _e.Db.SaveChanges();

        var r = await new ListReceiptsReferenceFiguresQueryHandler(_e.Db, Lineas).Handle(new ListReceiptsReferenceFiguresQuery(Corte), default);

        r.Value.Should().ContainSingle().Which.DifferenceLoadedVsReference.Should().Be(-10_000m);
    }
}
