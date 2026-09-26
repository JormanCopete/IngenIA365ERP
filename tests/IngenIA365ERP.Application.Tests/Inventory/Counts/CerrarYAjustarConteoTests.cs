using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Counts;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Counts;

/// <summary>
/// Feature 012, US11, T386 (US11-3, US11-4, FR-041; contracts/api.md §12):
/// <list type="bullet">
/// <item>una diferencia sobre la tolerancia sin segunda ronda es <c>Inventory.Count.RecountRequired</c> con <c>data.lines[]</c>; con la
/// ronda 2 cierra y manda la ronda 2;</item>
/// <item>cerrar numera el conteo al confirmar (sin huecos: un conteo descartado no consumió número) y no escribe kardex ni mensajes;</item>
/// <item>generar el ajuste crea hasta dos documentos (sobrantes y faltantes) con la causa «diferencia de conteo», enlazados
/// <c>CountAdjustmentOf</c>, en aprobación; sin diferencias no crea nada y el conteo queda <c>Adjusted</c>; un segundo intento es
/// <c>Inventory.Count.AdjustmentInProgress</c>;</item>
/// <item>la aprobación excluye a quien abrió y a todo contador (<c>Approvals.SelfApprovalForbidden</c>);</item>
/// <item>fecha: <c>Foto</c> → la de la foto; <c>Aprobacion</c> → la de la última aprobación; si ese mes cerró, el primer día abierto;
/// valor: el promedio vigente en esa fecha;</item>
/// <item>con <c>Costeo.RetroactivosPermitidos = false</c> y salidas posteriores del producto en otra bodega del ámbito, el ajuste se
/// confirma en su fecha sin cambiar el costo de esas salidas (caso dorado 15); un saldo intermedio negativo es
/// <c>Inventory.Stock.Insufficient</c>;</item>
/// <item><c>AjusteInventarioAprobado</c> v1 lleva <c>sourceDocument</c> = el conteo.</item>
/// </list>
/// </summary>
public class CerrarYAjustarConteoTests
{
    private static readonly DateOnly D05 = new(2026, 9, 5);
    private static readonly DateOnly D06 = new(2026, 9, 6);
    private static readonly DateOnly D10 = new(2026, 9, 10);
    private static readonly DateOnly D20 = new(2026, 9, 20);
    private static readonly DateOnly D25 = new(2026, 9, 25);

    [Fact]
    public async Task Sin_la_ronda_dos_una_diferencia_sobre_la_tolerancia_es_RecountRequired_y_con_ella_manda_la_ronda_dos()
    {
        var c = await ConteosDePrueba.CrearAsync();
        c.Parametro(ParametrosDeInventario.ConteoToleranciaReconteoUnidades, "1");
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        await c.EntradaAsync(c.K.P2, 5m, 500m);
        var conteo = await c.AbiertoAsync();
        c.ComoUsuario(ConteosDePrueba.ContadorA);
        await c.CapturarAsync(conteo, 1, c.Lectura(c.K.P1, 7m), c.Lectura(c.K.P2, 4m));

        c.ComoUsuario(ConteosDePrueba.Jefe);
        var r = await c.CerrarAsync(conteo);
        r.Error.Code.Should().Be("Inventory.Count.RecountRequired");
        var lineas = ((ErrorConDatos)r.Error).Data.GetType().GetProperty("lines")!.GetValue(((ErrorConDatos)r.Error).Data)
            as IReadOnlyList<ErroresDeConteos.LineaPorRecontar>;
        lineas.Should().ContainSingle().Which.Should().BeEquivalentTo(new { ProductCode = "P1", Theoretical = 10m, Counted = 7m, Difference = -3m },
            o => o.ExcludingMissingMembers(), "P2 (−1) queda dentro de la tolerancia de 1 unidad");
        c.Documento(conteo).Status.Should().Be(DocumentStatus.Draft);

        c.ComoUsuario(ConteosDePrueba.ContadorB);
        (await c.CapturarAsync(conteo, 2, c.Lectura(c.K.P1, 8m))).Value.Accepted.Should().Be(1);
        c.ComoUsuario(ConteosDePrueba.Jefe);
        var cerrado = await c.CerrarAsync(conteo);

        cerrado.IsSuccess.Should().BeTrue(cerrado.IsFailure ? cerrado.Error.Code : string.Empty);
        cerrado.Value.Should().BeEquivalentTo(new { State = EstadosDeConteo.Cerrado, Number = (long?)1, DisplayNumber = "CF1", Lines = 2, WithDifference = 2 },
            o => o.ExcludingMissingMembers());
        c.Lineas(conteo).Single(l => l.ProductId == c.K.ProductoId(c.K.P1)).Should().BeEquivalentTo(new
        {
            CountedQuantity = (decimal?)8m, Difference = (decimal?)-2m, LastRound = (byte)2, RecountRequired = false,
        }, o => o.ExcludingMissingMembers(), "después del reconteo manda la última ronda");
    }

    [Fact]
    public async Task Cerrar_numera_sin_huecos_y_no_escribe_kardex_ni_mensajes()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        await c.EntradaAsync(c.K.P2, 5m, 500m);
        var descartado = await c.AbiertoAsync(c.Definicion(CountScope.Selection, productos: [c.K.P2]));
        (await c.Descartar().Handle(new DiscardInventoryDraftCommand(descartado, DocumentClassGroup.Counts, "se reprograma"), default)).IsSuccess.Should().BeTrue();
        var kardex = await c.Db.KardexEntries.CountAsync();
        var mensajes = await c.Db.IntegrationMessages.CountAsync();

        var conteo = await c.CerradoAsync((c.K.P1, 10m), (c.K.P2, 5m));

        c.Documento(conteo).Should().BeEquivalentTo(new { Status = DocumentStatus.Confirmed, Prefix = "CF", Number = (long?)1 }, o => o.ExcludingMissingMembers(),
            "el descartado no consumió número");
        c.Documento(descartado).Number.Should().BeNull();
        (await c.Db.KardexEntries.CountAsync()).Should().Be(kardex);
        (await c.Db.IntegrationMessages.CountAsync()).Should().Be(mensajes);
        (await c.DetalleAsync(conteo)).Value.State.Should().Be(EstadosDeConteo.Ajustado, "sin diferencias no hay nada que ajustar");
        (await c.GenerarAjusteAsync(conteo)).Value.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task El_ajuste_nace_en_aprobacion_con_dos_documentos_y_la_causa_y_no_se_genera_dos_veces()
    {
        var c = await ConteosDePrueba.CrearAsync();
        c.Parametro(ParametrosDeInventario.ConteoToleranciaReconteoUnidades, "100");
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        await c.EntradaAsync(c.K.P2, 5m, 500m);
        var conteo = await c.CerradoAsync((c.K.P1, 12m), (c.K.P2, 4m));
        var fisico = c.Fisico(c.K.P1);

        var r = await c.GenerarAjusteAsync(conteo);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : string.Empty);
        r.Value.Documents.Select(d => (d.Class, d.Lines, d.Status)).Should().Equal(
            (DocumentClass.PositiveAdjustment, 1, DocumentStatus.PendingApproval),
            (DocumentClass.NegativeAdjustment, 1, DocumentStatus.PendingApproval));
        r.Value.Documents.Should().OnlyContain(d => d.ApprovalRequestPublicId != null);
        var positivo = c.Documento(r.Value.Documents[0].DocumentPublicId);
        c.K.Tipo("CONP").Id.Should().Be(positivo.DocumentTypeId, "el tipo de ajuste de conteo, con su política de Inventory.Counts.Approve");
        positivo.Lines.Single().Should().BeEquivalentTo(new { QuantityBase = 2m, LocationId = (int?)c.General.Id, UnitCost = (decimal?)null },
            o => o.ExcludingMissingMembers());
        positivo.Lines.Single().AdjustmentCauseId.Should().Be(c.Db.AdjustmentCauses.Single(x => x.Code == ReglaDelAjusteDeConteo.CausaDiferenciaDeConteo).Id);
        (await c.Db.DocumentLinks.CountAsync(l => l.SourceDocumentId == c.Documento(conteo).Id && l.Kind == DocumentLinkKind.CountAdjustmentOf)).Should().Be(2);
        c.Fisico(c.K.P1).Should().Be(fisico, "el ajuste no afecta la existencia hasta aprobarse");
        (await c.DetalleAsync(conteo)).Value.State.Should().Be(EstadosDeConteo.AjusteEnAprobacion);

        (await c.GenerarAjusteAsync(conteo)).Error.Code.Should().Be("Inventory.Count.AdjustmentInProgress");
    }

    [Fact]
    public async Task No_aprueban_quien_abrio_ni_quien_conto_y_otra_persona_lo_confirma_en_la_fecha_de_la_foto_con_el_conteo_como_origen()
    {
        var c = await ConteosDePrueba.CrearAsync();
        c.Parametro(ParametrosDeInventario.ConteoToleranciaReconteoUnidades, "100");
        c.Hoy(D05);
        await c.EntradaAsync(c.K.P1, 10m, 1_000m, fecha: D05);
        c.Hoy(D06);
        await c.EntradaAsync(c.K.P1, 10m, 1_300m, bodega: c.B2, fecha: D06);
        c.Hoy(D10);
        var conteo = await c.CerradoAsync((c.K.P1, 12m));
        c.Hoy(D20);
        var (salida, rs) = await c.SalidaAsync(c.K.P1, 5m, c.B2, D20);
        rs.IsSuccess.Should().BeTrue(rs.IsFailure ? rs.Error.Code : string.Empty);
        var costoDeLaSalida = c.Db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == c.Documento(salida).Id).Sum(k => k.TotalCost);

        c.ComoUsuario(ConteosDePrueba.Jefe);
        var ajuste = (await c.GenerarAjusteAsync(conteo)).Value.Documents.Single().DocumentPublicId;

        (await c.DecidirAsync(ajuste, ConteosDePrueba.Jefe)).Error.Code.Should().Be("Approvals.SelfApprovalForbidden", "abrió el conteo y generó el ajuste");
        (await c.DecidirAsync(ajuste, ConteosDePrueba.ContadorA)).Error.Code.Should().Be("Approvals.SelfApprovalForbidden", "capturó en el conteo");
        var aprobada = await c.DecidirAsync(ajuste, ConteosDePrueba.Aprobador);

        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? $"{aprobada.Error.Code}: {aprobada.Error.Message}" : string.Empty);
        var confirmado = c.Documento(ajuste);
        confirmado.Should().BeEquivalentTo(new { Status = DocumentStatus.Confirmed, OperationDate = D10, ConfirmedByUserId = (int?)ConteosDePrueba.Aprobador },
            o => o.ExcludingMissingMembers(), "Conteo.FechaDelAjuste = Foto (defecto): la fecha de la foto aunque haya salidas posteriores");
        var entrada = c.Db.KardexEntries.AsNoTracking().Single(k => k.DocumentId == confirmado.Id && k.Kind == KardexEntryKind.Entry);
        entrada.Should().BeEquivalentTo(new { OperationDate = D10, QuantityBase = 2m, UnitCost = 1_150m }, o => o.ExcludingMissingMembers(),
            "al promedio de la cooperativa a esa fecha: (10 × 1.000 + 10 × 1.300) / 20");
        c.Db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == c.Documento(salida).Id).Sum(k => k.TotalCost).Should().Be(costoDeLaSalida,
            "entra al promedio: el costo de la salida posterior no cambia (caso dorado 15)");
        c.Db.KardexEntries.AsNoTracking().Count(k => k.DocumentId == confirmado.Id && k.Reason == KardexReason.Retroactive).Should().Be(0);
        c.Fisico(c.K.P1).Should().Be(12m);

        var mensaje = await c.Db.IntegrationMessages.SingleAsync(m => m.OriginPublicId == ajuste && m.Type == AjusteInventarioAprobadoV1.Type);
        var origen = System.Text.Json.JsonDocument.Parse(mensaje.PayloadJson).RootElement.GetProperty("sourceDocument");
        origen.GetProperty("publicId").GetGuid().Should().Be(conteo);
        origen.GetProperty("number").GetString().Should().Be("CF1");
        (await c.DetalleAsync(conteo)).Value.State.Should().Be(EstadosDeConteo.Ajustado);
    }

    [Fact]
    public async Task Con_la_regla_Aprobacion_el_ajuste_va_en_la_fecha_de_la_ultima_aprobacion()
    {
        var c = await ConteosDePrueba.CrearAsync();
        c.Parametro(ParametrosDeInventario.ConteoToleranciaReconteoUnidades, "100");
        c.Parametro(ParametrosDeInventario.ConteoFechaDelAjuste, ReglasDeFechaDelAjuste.Aprobacion);
        c.Hoy(D05);
        await c.EntradaAsync(c.K.P1, 10m, 1_000m, fecha: D05);
        c.Hoy(D10);
        var conteo = await c.CerradoAsync((c.K.P1, 9m));
        c.Hoy(D20);
        var ajuste = (await c.GenerarAjusteAsync(conteo)).Value.Documents.Single().DocumentPublicId;
        c.Documento(ajuste).OperationDate.Should().Be(D20, "la propuesta es la de hoy");

        c.Hoy(D25);
        (await c.DecidirAsync(ajuste, ConteosDePrueba.Aprobador)).IsSuccess.Should().BeTrue();

        c.Documento(ajuste).OperationDate.Should().Be(D25, "la pone la última aprobación");
        c.Fisico(c.K.P1).Should().Be(9m);
    }

    [Fact]
    public async Task Si_el_mes_de_la_foto_cerro_el_ajuste_va_al_primer_dia_abierto()
    {
        var c = await ConteosDePrueba.CrearAsync();
        c.Parametro(ParametrosDeInventario.ConteoToleranciaReconteoUnidades, "100");
        var d20Ago = new DateOnly(2026, 8, 20);
        c.Hoy(new DateOnly(2026, 8, 1));
        await c.EntradaAsync(c.K.P1, 10m, 1_000m, fecha: new DateOnly(2026, 8, 1));
        c.Hoy(d20Ago);
        var conteo = await c.CerradoAsync((c.K.P1, 11m));
        c.Db.InventorySetups.Single().LastClosedDate = new DateOnly(2026, 8, 31);
        await c.Db.SaveChangesAsync();
        c.Hoy(D25);

        var previa = await c.VistaPreviaAsync(conteo);
        previa.Value.Should().BeEquivalentTo(new { AdjustmentDate = new DateOnly(2026, 9, 1), DateRule = ReglasDeFechaDelAjuste.Foto }, o => o.ExcludingMissingMembers());
        previa.Value.Lines.Single().Should().BeEquivalentTo(new { Difference = 1m, UnitCost = (decimal?)1_000m, Value = (decimal?)1_000m }, o => o.ExcludingMissingMembers());
        previa.Value.PositiveValue.Should().Be(1_000m);

        var ajuste = (await c.GenerarAjusteAsync(conteo)).Value.Documents.Single().DocumentPublicId;
        (await c.DecidirAsync(ajuste, ConteosDePrueba.Aprobador)).IsSuccess.Should().BeTrue();
        c.Documento(ajuste).OperationDate.Should().Be(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public async Task Un_ajuste_de_conteo_que_deja_un_saldo_intermedio_negativo_es_Stock_Insufficient()
    {
        var c = await ConteosDePrueba.CrearAsync();
        c.Parametro(ParametrosDeInventario.ConteoToleranciaReconteoUnidades, "100");
        c.Hoy(D05);
        await c.EntradaAsync(c.K.P1, 10m, 1_000m, fecha: D05);
        c.Hoy(D10);
        var conteo = await c.CerradoAsync((c.K.P1, 2m));
        c.Hoy(D20);
        (await c.SalidaAsync(c.K.P1, 5m, fecha: D20)).Confirmacion.IsSuccess.Should().BeTrue();

        var ajuste = (await c.GenerarAjusteAsync(conteo)).Value.Documents.Single().DocumentPublicId;
        var r = await c.DecidirAsync(ajuste, ConteosDePrueba.Aprobador);

        r.Error.Code.Should().Be("Inventory.Stock.Insufficient", "10 − 8 = 2 el día 10 y la salida del 20 lo dejaría en −3");
        c.Documento(ajuste).Status.Should().Be(DocumentStatus.PendingApproval);
    }

    [Fact]
    public async Task La_semilla_deja_los_tipos_de_ajuste_de_conteo_con_su_politica()
    {
        var k = await Kardex.KardexDePrueba.CrearAsync();
        await InventoryDocumentTypesSeeder.AplicarAsync(k.C.Db, default);

        foreach (var (clase, (codigo, _)) in InventoryDocumentTypesSeeder.AjustesDeConteo)
        {
            var tipo = await k.C.Db.InventoryDocumentTypes.Include(t => t.Sequences).SingleAsync(t => t.Code == codigo);
            tipo.Class.Should().Be(clase);
            tipo.Sequences.Should().ContainSingle();
            var politica = await k.C.Db.ApprovalPolicies.Include(p => p.Levels).SingleAsync(p => p.DocumentTypePublicId == tipo.PublicId);
            politica.Subject.Should().Be(ApprovalSubjects.DocumentConfirmation);
            politica.Levels.Should().ContainSingle().Which.Should().BeEquivalentTo(
                new { Order = (byte)1, Threshold = 0m, PermissionCode = "Inventory.Counts.Approve" }, o => o.ExcludingMissingMembers());
        }
        (await k.C.Db.InventoryDocumentTypes.CountAsync(t => t.Class == DocumentClass.PhysicalCount)).Should().BeGreaterThan(0);
        k.C.Db.AdjustmentCauses.Should().Contain(x => x.Code == ReglaDelAjusteDeConteo.CausaDiferenciaDeConteo && x.IsRequiredBySystem,
            "la causa «diferencia de conteo» la trae AdjustmentCausesSeeder");
        AdjustmentCausesSeeder.CodigoDiferenciaDeConteo.Should().Be(ReglaDelAjusteDeConteo.CausaDiferenciaDeConteo);

        var otraVez = await InventoryDocumentTypesSeeder.AplicarAsync(k.C.Db, default);
        otraVez.Should().Be(0, "idempotente");
    }
}
