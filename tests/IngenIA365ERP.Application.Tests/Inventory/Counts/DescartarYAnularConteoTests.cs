using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Counts;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Counts;

/// <summary>
/// Feature 012, US11, T387 (data-model §8; contracts/api.md §12; FR-005, FR-006): descartar un conteo en borrador o abierto con motivo
/// lo deja <c>Discarded</c>, sin número y con quién, cuándo y por qué; anular uno abierto es <c>Inventory.Document.NotConfirmed</c>;
/// anular uno cerrado crea un <c>Voiding</c> con <c>Voids</c> sin kardex ni mensajes; con ajustes en curso o vigentes es
/// <c>Inventory.Document.HasDependents</c> nombrándolos.
/// </summary>
public class DescartarYAnularConteoTests
{
    private static Task<Result> DescartarAsync(ConteosDePrueba c, Guid conteo, string motivo = "se reprograma") =>
        c.Descartar().Handle(new DiscardInventoryDraftCommand(conteo, DocumentClassGroup.Counts, motivo), default);

    private static Task<Result<VoidResultDto>> AnularAsync(ConteosDePrueba c, Guid conteo) =>
        c.Anular().Handle(new VoidInventoryDocumentCommand(conteo, DocumentClassGroup.Counts, "el conteo se hizo sobre la bodega equivocada"), default);

    [Fact]
    public async Task Descartar_un_conteo_en_borrador_o_abierto_lo_deja_Discarded_sin_numero_y_con_su_motivo()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        var borrador = (await c.DefinirAsync(c.Definicion(CountScope.All))).Value.PublicId;
        var abierto = await c.AbiertoAsync();

        (await DescartarAsync(c, borrador)).IsSuccess.Should().BeTrue();
        (await DescartarAsync(c, abierto)).IsSuccess.Should().BeTrue();

        foreach (var id in new[] { borrador, abierto })
        {
            c.Documento(id).Should().BeEquivalentTo(new
            {
                Status = DocumentStatus.Discarded, Number = (long?)null, DiscardReason = "se reprograma", DiscardedByUserId = (int?)ConteosDePrueba.Jefe,
            }, o => o.ExcludingMissingMembers());
            (await c.DetalleAsync(id)).Value.State.Should().Be(EstadosDeConteo.Descartado);
        }
        c.Lineas(abierto).Should().NotBeEmpty("la foto se conserva: el descarte no borra nada");
    }

    [Fact]
    public async Task Anular_un_conteo_abierto_es_NotConfirmed()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        var abierto = await c.AbiertoAsync();

        (await AnularAsync(c, abierto)).Error.Code.Should().Be("Inventory.Document.NotConfirmed", "uno abierto se descarta, no se anula");
    }

    [Fact]
    public async Task Anular_un_conteo_cerrado_crea_el_Voiding_sin_kardex_ni_mensajes()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        var conteo = await c.CerradoAsync((c.K.P1, 10m));
        var kardex = await c.Db.KardexEntries.CountAsync();
        var mensajes = await c.Db.IntegrationMessages.CountAsync();

        var r = await AnularAsync(c, conteo);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(DocumentStatus.Confirmed);
        var anulacion = c.Documento(r.Value.VoidingDocumentPublicId);
        anulacion.Class.Should().Be(DocumentClass.Voiding);
        anulacion.VoidsDocumentId.Should().Be(c.Documento(conteo).Id);
        c.Documento(conteo).Status.Should().Be(DocumentStatus.Voided);
        (await c.Db.DocumentLinks.AnyAsync(l => l.TargetDocumentId == anulacion.Id && l.Kind == DocumentLinkKind.Voids)).Should().BeTrue();
        (await c.Db.KardexEntries.CountAsync()).Should().Be(kardex);
        (await c.Db.IntegrationMessages.CountAsync()).Should().Be(mensajes);
        (await c.DetalleAsync(conteo)).Value.State.Should().Be(EstadosDeConteo.Anulado);
    }

    [Fact]
    public async Task Con_ajustes_en_curso_o_vigentes_la_anulacion_los_nombra()
    {
        var c = await ConteosDePrueba.CrearAsync();
        c.Parametro(ParametrosDeInventario.ConteoToleranciaReconteoUnidades, "100");
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        var conteo = await c.CerradoAsync((c.K.P1, 8m));
        var ajuste = (await c.GenerarAjusteAsync(conteo)).Value.Documents.Single().DocumentPublicId;

        var enCurso = await AnularAsync(c, conteo);
        enCurso.Error.Code.Should().Be("Inventory.Document.HasDependents");
        var dependientes = ((ErrorConDatos)enCurso.Error).Data.GetType().GetProperty("dependents")!.GetValue(((ErrorConDatos)enCurso.Error).Data)
            as IReadOnlyList<InventoryErrors.Dependiente>;
        dependientes.Should().ContainSingle().Which.PublicId.Should().Be(ajuste);

        (await c.DecidirAsync(ajuste, ConteosDePrueba.Aprobador)).IsSuccess.Should().BeTrue();
        c.ComoUsuario(ConteosDePrueba.Jefe);
        (await AnularAsync(c, conteo)).Error.Code.Should().Be("Inventory.Document.HasDependents", "primero se anulan los ajustes");
    }
}
