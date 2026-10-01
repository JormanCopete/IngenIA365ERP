using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Tests.Inventory.GoLive;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IngenIA365ERP.Application.Tests.Inventory.Integration;

/// <summary>
/// Feature 012, T468 (FR-074; T30; api.md §9.3; contracts/contabilidad.md §4): la validación previa contable dentro de
/// <see cref="ConfirmacionDeDocumento"/>, con un doble de <see cref="IContabilidadParaInventario"/>. No contabilizable → 422
/// <c>Inventory.Prevalidation.NotPostable</c> con <c>data.errors[]</c>, sin número ni mensajes y en borrador; sin respuesta con
/// <c>ConfirmarConPendiente</c> → confirma con el aviso y sella <c>NoResponse</c>; con <c>Bloquear</c> → 422; <c>NotPosted</c> o
/// sólo informativos → <c>NotApplicable</c> sin preguntar; y la pregunta corre antes del cerrojo.
/// </summary>
public class ValidacionPreviaEnLaConfirmacionTests
{
    private readonly IContabilidadParaInventario _contabilidad = Substitute.For<IContabilidadParaInventario>();
    private readonly List<IReadOnlyList<MensajeContableDto>> _preguntas = [];
    private int _bloqueosAlPreguntar = -1;

    private ConfirmacionDeDocumento Confirmacion(KardexDePrueba k) => new(
        k.C.Db, k.Maestros(), k.Actor, k.C.Reloj, k.Efectos(), k.Motor, k.Cerrojo, new Numerador(k.C.Db, k.Cerrojo),
        new EmisorDeMensajes(k.C.Db, k.Actor, k.C.Reloj), k.Lector(), k.Vista(), [],
        [new ValidacionPreviaContable(_contabilidad, k.Lector(), k.C.Reloj)]);

    private void Responde(KardexDePrueba k, Func<Result<ResultadoDeContabilizacionDto>> respuesta) =>
        _contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>()).Returns(c =>
        {
            _preguntas.Add(c.Arg<IReadOnlyList<MensajeContableDto>>());
            _bloqueosAlPreguntar = k.Bloqueos.Count;
            return Task.FromResult(respuesta());
        });

    private static readonly HallazgoContableDto FaltaTercero = new(AjusteInventarioAprobadoV1.Type, [1], "51059501",
        "Accounting.Line.ThirdPartyRequired", "La cuenta 51059501 exige tercero.",
        new QuienCorrigeDto("Contabilidad", "/contabilidad/inventario/matriz", "Accounting.InventoryRules.Manage"));

    private static async Task<Guid> BorradorAsync(KardexDePrueba k, string tipo = "AJP")
    {
        var r = await k.GuardarAsync(k.Borrador(tipo, causa: tipo == "AJP" ? null : k.Causa(), lineas: [k.Linea(k.P1, 10m, 1000m)]));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value.PublicId;
    }

    private Task<Result<ConfirmationResultDto>> ConfirmarAsync(KardexDePrueba k, Guid documento) =>
        Confirmacion(k).ConfirmarAsync(new PedidoDeConfirmacion(documento, DocumentClassGroup.Adjustments), default);

    [Fact]
    public async Task No_contabilizable_responde_NotPostable_sin_numero_ni_mensajes_y_en_borrador()
    {
        var k = await KardexDePrueba.CrearAsync();
        Responde(k, () => Result.Success(new ResultadoDeContabilizacionDto(false, [FaltaTercero], [])));
        var documento = await BorradorAsync(k);

        var r = await ConfirmarAsync(k, documento);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Inventory.Prevalidation.NotPostable");
        var error = JsonSerializer.SerializeToElement(((ErrorConDatos)r.Error).Data, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("errors")[0];
        error.GetProperty("lineNumber").GetInt32().Should().Be(1);
        error.GetProperty("account").GetString().Should().Be("51059501");
        error.GetProperty("rule").GetString().Should().Be("Accounting.Line.ThirdPartyRequired");
        error.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        error.GetProperty("whoFixes").GetProperty("page").GetString().Should().Be("/contabilidad/inventario/matriz");

        k.C.Db.ChangeTracker.Clear();
        var guardado = await k.C.Db.InventoryDocuments.AsNoTracking().SingleAsync(d => d.PublicId == documento);
        guardado.Status.Should().Be(DocumentStatus.Draft);
        guardado.Number.Should().BeNull();
        (await k.C.Db.IntegrationMessages.CountAsync()).Should().Be(0);
        _bloqueosAlPreguntar.Should().Be(0, "la validación previa corre fuera del cerrojo (T30)");
        k.Bloqueos.Should().BeEmpty();
    }

    [Fact]
    public async Task Contabilizable_confirma_sella_Postable_y_pregunta_con_los_sobres_que_emite()
    {
        var k = await KardexDePrueba.CrearAsync();
        Responde(k, () => Result.Success(new ResultadoDeContabilizacionDto(true, [], [])));
        var documento = await BorradorAsync(k);

        var r = await ConfirmarAsync(k, documento);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Prevalidation!.Outcome.Should().Be(PrevalidationOutcome.Postable);
        var mensaje = await k.C.Db.IntegrationMessages.AsNoTracking().SingleAsync();
        mensaje.PrevalidationOutcome.Should().Be(PrevalidationOutcome.Postable);

        var sobre = _preguntas.Should().ContainSingle().Which.Should().ContainSingle().Which;
        sobre.Envelope.Type.Should().Be(mensaje.Type);
        sobre.Envelope.OriginEventKey.Should().Be(mensaje.OriginEventKey);
        sobre.Envelope.Origin.PublicId.Should().Be(documento);
        sobre.Envelope.BranchPublicId.Should().Be(mensaje.BranchPublicId);
        sobre.Envelope.WarehouseCode.Should().Be("PRIN");
        var provisional = sobre.Payload.Should().BeOfType<AjusteInventarioAprobadoV1>().Subject;
        provisional.Operation.Should().Be("AjustePositivo");
        provisional.Lines.Should().ContainSingle().Which.Cost.Should().Be(10_000m, "el costo digitado de la entrada");
        _bloqueosAlPreguntar.Should().Be(0);
    }

    [Fact]
    public async Task Sin_respuesta_y_ConfirmarConPendiente_confirma_con_aviso_y_sella_NoResponse()
    {
        var k = await KardexDePrueba.CrearAsync();
        _contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Contabilidad caída"));
        var documento = await BorradorAsync(k);

        var r = await ConfirmarAsync(k, documento);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Prevalidation!.Outcome.Should().Be(PrevalidationOutcome.NoResponse);
        r.Value.Warnings.Select(w => w.Code).Should().Contain(ValidacionPreviaContable.AvisoSinRespuesta);
        r.Value.Number.Should().NotBeNull();
        (await k.C.Db.IntegrationMessages.AsNoTracking().SingleAsync()).PrevalidationOutcome.Should().Be(PrevalidationOutcome.NoResponse);
    }

    [Fact]
    public async Task El_tiempo_agotado_es_no_responde()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.Parametro(ParametrosDeInventario.ContabilidadValidacionPreviaSegundos, "1");
        _contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>()).Returns(async c =>
        {
            await Task.Delay(Timeout.Infinite, c.Arg<CancellationToken>());
            return Result.Success(new ResultadoDeContabilizacionDto(true, [], []));
        });
        var documento = await BorradorAsync(k);

        var r = await ConfirmarAsync(k, documento);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Prevalidation!.Outcome.Should().Be(PrevalidationOutcome.NoResponse);
    }

    [Fact]
    public async Task Sin_respuesta_y_Bloquear_responde_NoResponse_y_no_confirma()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.Parametro(ParametrosDeInventario.ContabilidadPoliticaSinRespuesta, ValidacionPreviaContable.Bloquear);
        _contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ResultadoDeContabilizacionDto>(new Error("Accounting.Unavailable", "no")));
        var documento = await BorradorAsync(k);

        var r = await ConfirmarAsync(k, documento);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Inventory.Prevalidation.NoResponse");
        k.C.Db.ChangeTracker.Clear();
        (await k.C.Db.InventoryDocuments.AsNoTracking().SingleAsync(d => d.PublicId == documento)).Status.Should().Be(DocumentStatus.Draft);
    }

    [Fact]
    public async Task En_modo_NotPosted_no_pregunta_y_queda_NotApplicable()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.Parametro(ParametrosDeInventario.ContabilidadModoDePaso, "NoPasa", ParameterScopeKind.DocumentType, k.Tipo("AJP").Id);
        Responde(k, () => Result.Success(new ResultadoDeContabilizacionDto(false, [FaltaTercero], [])));
        var documento = await BorradorAsync(k);

        var r = await ConfirmarAsync(k, documento);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Prevalidation!.Outcome.Should().Be(PrevalidationOutcome.NotApplicable);
        _preguntas.Should().BeEmpty();
        await _contabilidad.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
    }

    [Fact]
    public async Task Un_documento_que_solo_emite_informativos_no_pregunta()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        p.ValidacionesPrevias = [new ValidacionPreviaContable(_contabilidad, p.K.Lector(), p.K.C.Reloj)];
        var saldo = await p.BorradorAsync(p.B3, "P1", 10m, 1500m);

        var r = await p.ConfirmarAsync(saldo);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Prevalidation!.Outcome.Should().Be(PrevalidationOutcome.NotApplicable);
        await _contabilidad.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
    }
}
