using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Compliance;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Compliance;

/// <summary>
/// Feature 015, segunda ronda del 2026-10-27 (D-e, T0491; FR-230): el falso positivo de lista vinculante en la ventanilla. La operación bloqueada no la libera el cajero
/// de la sesión ni sin soporte de la operación; el oficial la libera con motivo y soporte, queda una fila por persona en <c>RCD_ScreeningReleases</c> y la confirmación ya
/// no se detiene por esa persona (pero sí por otra que coincida).
/// </summary>
public class LiberacionDeListaVinculanteTests
{
    private readonly CajaConContratos _k = new();

    private AuditoriaDeRecaudo Auditoria => new(_k.C.E.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance);

    private ReleaseBindingMatchCommandHandler Liberar() => new(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.E.Segregacion, Auditoria, _k.C.E.Reloj);

    private async Task<CashOperationDto> BloqueadaAsync()
    {
        _k.C.E.Db.SaveChanges();
        var pagador = _k.Asociado.Id;
        // La lista coincide sólo si la consulta trae al pagador.
        _k.Listas.EvaluarAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(c => ((IReadOnlyCollection<int>)c[0]).Contains(pagador) ? ScreeningOutcomeR.BindingMatch : ScreeningOutcomeR.Clear);
        await _k.AbrirAsync();
        return (await new CreateCashOperationCommandHandler(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Reloj, _k.Listas)
            .Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value;
    }

    private Guid Soporte(Guid operacion, string tipo = AdjuntosDeModulo.SoporteDeLiberacionDeLista)
    {
        var a = new Attachment { OwnerEntityType = tipo, OwnerEntityPublicId = operacion, FileName = "soporte.pdf", ContentType = "application/pdf", SizeBytes = 10 };
        _k.C.E.Db.Attachments.Add(a);
        _k.C.E.Db.SaveChanges();
        return a.PublicId;
    }

    [Fact]
    public async Task El_cajero_de_la_sesion_no_libera_y_sin_soporte_de_la_operacion_tampoco()
    {
        var op = await BloqueadaAsync();
        op.Status.Should().Be(CashOperationState.Blocked);
        var soporte = Soporte(op.PublicId);

        var cajero = await Liberar().Handle(new ReleaseBindingMatchCommand(op.PublicId, soporte, "Homónimo") { OperationKey = Guid.NewGuid() }, default);
        cajero.Error.Code.Should().Be(LiberacionesDeLista.PorElCajero().Code);

        _k.C.E.Como(_k.C.E.Supervisor);
        var ajeno = Soporte(Guid.NewGuid());
        var sinSoporte = await Liberar().Handle(new ReleaseBindingMatchCommand(op.PublicId, ajeno, "Homónimo") { OperationKey = Guid.NewGuid() }, default);
        sinSoporte.Error.Code.Should().Be(LiberacionesDeLista.SoporteInvalido().Code, "el soporte tiene que ser de la operación y de su tipo");
        (await _k.C.E.Db.ScreeningReleases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_oficial_la_libera_y_la_confirmacion_sigue_sin_volver_a_detenerse_por_esa_persona()
    {
        var op = await BloqueadaAsync();
        var soporte = Soporte(op.PublicId);
        _k.C.E.Como(_k.C.E.Supervisor);

        var r = await Liberar().Handle(new ReleaseBindingMatchCommand(op.PublicId, soporte, "Homónimo: otro documento") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Status.Should().Be(CashOperationState.Preparing);
        var fila = await _k.C.E.Db.ScreeningReleases.SingleAsync();
        fila.PersonId.Should().Be(_k.Asociado.Id);
        fila.SupportAttachmentPublicId.Should().Be(soporte);
        var otraVez = await Liberar().Handle(new ReleaseBindingMatchCommand(op.PublicId, soporte, "Otra vez") { OperationKey = Guid.NewGuid() }, default);
        otraVez.Error.Code.Should().Be(LiberacionesDeLista.NoBloqueada().Code);

        _k.C.E.Como(_k.C.E.Cajera);
        await _k.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, [_k.Cuota(_k.CreditoA, 100_000m)]), default);
        var medios = await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, [_k.Efectivo(100_000m)]), default);
        medios.IsSuccess.Should().BeTrue(medios.IsFailure ? medios.Error.Code : null);
        var confirmada = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);
        confirmada.IsSuccess.Should().BeTrue(confirmada.IsFailure ? confirmada.Error.Code : null);
    }
}
