using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.GoLive;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using S = IngenIA365ERP.Application.Inventory.GoLive.PlantillaDeSaldoInicial;

namespace IngenIA365ERP.Application.Tests.Inventory.GoLive;

/// <summary>
/// Feature 012, T297 (contracts/plantillas.md §14; api.md §13.1; FR-089, US4-1): la plantilla 14 —saldo inicial— todo o nada,
/// con las reglas de la bodega (no activa, no de tránsito, al alcance, sin saldo confirmado, una sola fecha de corte), del
/// producto y de la cantidad; los borradores por bodega partidos en tramos de 4.000 líneas; volver a importar reemplaza las
/// líneas del mismo borrador; y el primer corte crea <c>INV_Setup</c>.
/// </summary>
public class ImportOpeningBalanceCommandHandlerTests
{
    private static string Errores(ImportResultDto r) => string.Join(" | ", r.Errors.Select(e => $"{e.Row} {e.Column} {e.Code}: {e.Message}"));

    private static void Bien(Result<ImportResultDto> r)
    {
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Errors.Should().BeEmpty(Errores(r.Value));
    }

    private static IReadOnlyList<ResumenDeSaldoInicialDto> PorBodega(ImportResultDto r) =>
        (IReadOnlyList<ResumenDeSaldoInicialDto>)r.Extra[ImportOpeningBalanceCommandHandler.ExtraPorBodega]!;

    private static IReadOnlyList<DocumentoDeSaldoInicialDto> Documentos(ImportResultDto r) =>
        (IReadOnlyList<DocumentoDeSaldoInicialDto>)r.Extra[ImportOpeningBalanceCommandHandler.ExtraDocumentos]!;

    // ------------------------------------------------------------------------------------------ revisión --

    [Fact]
    public async Task La_revision_corre_las_mismas_reglas_no_guarda_nada_y_resume_por_bodega()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();

        var r = await p.ImportarSaldoAsync(ModoDeImportacion.Review,
            PuestaEnMarchaDePrueba.Fila("B3", "P1", "10", "1500"),
            PuestaEnMarchaDePrueba.Fila("B3", "P2", "4", "2500,5", "A-01"),
            PuestaEnMarchaDePrueba.Fila("B4", "P1", "3", "1000"));

        Bien(r);
        r.Value.Applied.Should().BeFalse();
        (await p.Db.InventoryDocuments.CountAsync()).Should().Be(0, "la revisión no guarda nada");
        (await p.Db.InventorySetups.CountAsync()).Should().Be(0);
        (await p.Db.Warehouses.SingleAsync(w => w.Code == "B3")).CutoffDate.Should().BeNull();

        var b3 = PorBodega(r.Value).Single(b => b.Bodega == "B3");
        b3.FechaDeCorte.Should().Be(PuestaEnMarchaDePrueba.Corte);
        b3.Lineas.Should().Be(2);
        b3.Documentos.Should().Be(1);
        b3.CantidadTotal.Should().Be(14m);
        b3.ValorTotal.Should().Be(15000m + 10002m);
        b3.ValorPorGrupo.Should().ContainSingle().Which.Should().Be(new ValorPorGrupoDto("ABARR", 25002m));
        PorBodega(r.Value).Select(b => b.Bodega).Should().Equal("B3", "B4");
    }

    // ----------------------------------------------------------------------------------------- todo o nada --

    [Fact]
    public async Task Veinte_mil_filas_con_un_producto_y_una_bodega_inexistentes_no_guardan_nada_y_listan_los_dos_errores()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        var codigos = await p.ProductosEnBloqueAsync(10_000);
        var filas = new List<string?[]>(20_000);
        foreach (var bodega in new[] { "B3", "B4" })
            foreach (var codigo in codigos)
                filas.Add(PuestaEnMarchaDePrueba.Fila(bodega, codigo, "2", "100"));
        filas[7_345] = PuestaEnMarchaDePrueba.Fila("B3", "NOEXISTE", "2", "100");
        filas[15_010] = PuestaEnMarchaDePrueba.Fila("B9", "Q05011", "2", "100");
        p.Datos(PuestaEnMarchaDePrueba.Encabezados, filas);

        var revision = await p.ImportarSaldoAsync(ModoDeImportacion.Review);
        var aplicacion = await p.ImportarSaldoAsync(ModoDeImportacion.Apply);

        revision.Value.Valid.Should().BeFalse();
        revision.Value.Errors.Should().HaveCount(2, Errores(revision.Value));
        revision.Value.Errors.Should().ContainSingle(e => e.Row == 7_347 && e.Column == S.Producto && e.Code == ImportErrors.CellNotFound);
        revision.Value.Errors.Should().ContainSingle(e => e.Row == 15_012 && e.Column == S.Bodega && e.Code == ImportErrors.CellNotFound);
        aplicacion.IsFailure.Should().BeTrue();
        aplicacion.Error.Code.Should().Be(ImportErrors.InvalidCode);
        (await p.Db.InventoryDocuments.CountAsync()).Should().Be(0);
        (await p.Db.InventoryDocumentLines.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Llave_repetida_cantidades_costos_y_productos_responden_con_los_codigos_del_alta()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        await p.K.C.ProductoAsync(p.K.C.Alta("SERV", "Flete", clase: ProductKind.Service));
        var bloqueado = await p.K.C.ProductoAsync(p.K.C.Alta("BLOQ", "Bloqueado"));
        (await p.Db.Products.SingleAsync(x => x.PublicId == bloqueado.PublicId)).Status = ProductStatus.Blocked;
        await p.Db.SaveChangesAsync();
        p.Db.ChangeTracker.Clear();

        p.Datos([.. PuestaEnMarchaDePrueba.Encabezados, S.Lote],
        [
            [.. PuestaEnMarchaDePrueba.Fila("B3", "P1", "10", "1000"), null],
            [.. PuestaEnMarchaDePrueba.Fila("B3", "P1", "5", "1000"), null],          // 3: llave repetida
            [.. PuestaEnMarchaDePrueba.Fila("B3", "P2", "0", "1000"), null],          // 4: cantidad cero
            [.. PuestaEnMarchaDePrueba.Fila("B4", "P2", "2", "-1"), null],            // 5: costo negativo
            [.. PuestaEnMarchaDePrueba.Fila("B4", "P1", "2,5", "1000"), null],        // 6: más decimales que la unidad
            [.. PuestaEnMarchaDePrueba.Fila("B3", "P2", "1", "0", "A-01"), null],     // 7: costo cero, aviso
            [.. PuestaEnMarchaDePrueba.Fila("B3", "SERV", "1", "10"), null],          // 8: no inventariable
            [.. PuestaEnMarchaDePrueba.Fila("B4", "BLOQ", "1", "10"), null],          // 9: bloqueado
            [.. PuestaEnMarchaDePrueba.Fila("B5", "P1", "1", "10"), "L-001"],         // 10: lote hasta I6
        ]);

        var r = await p.ImportarSaldoAsync(ModoDeImportacion.Review);

        var e = r.Value.Errors;
        e.Should().ContainSingle(x => x.Row == 3 && x.Code == ImportErrors.RowDuplicate);
        e.Should().ContainSingle(x => x.Row == 4 && x.Column == S.Cantidad && x.Code == ImportErrors.CellFormat);
        e.Should().ContainSingle(x => x.Row == 5 && x.Column == S.CostoUnitario && x.Code == ImportErrors.CellFormat);
        e.Should().ContainSingle(x => x.Row == 6 && x.Column == S.Cantidad && x.Code == "Inventory.Unit.DecimalsNotAllowed");
        e.Should().NotContain(x => x.Row == 7);
        r.Value.Warnings.Should().ContainSingle(x => x.Row == 7 && x.Code == GoLiveErrors.OpeningBalanceZeroCostCode);
        e.Should().ContainSingle(x => x.Row == 8 && x.Code == "Inventory.Product.NotInventoriable");
        e.Should().ContainSingle(x => x.Row == 9 && x.Code == "Inventory.Product.Blocked");
        e.Should().ContainSingle(x => x.Row == 10 && x.Column == S.Lote && x.Code == ImportErrors.CellNotYetAvailable);
        e.Should().NotContain(x => x.Row == 2);
    }

    // ------------------------------------------------------------------------------------------ la bodega --

    [Fact]
    public async Task La_bodega_debe_ser_no_activa_operativa_y_del_alcance_con_una_sola_fecha_de_corte_valida()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        var fuera = await p.Db.Warehouses.SingleAsync(w => w.Code == "B5");
        p.K.Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Application.Common.Interfaces.Security.AlcanceDeInventario(
            false, p.Db.Warehouses.Where(w => w.Id != fuera.Id).Select(w => w.Id).ToHashSet(), null, true, new HashSet<int>(), null));
        p.Db.InventorySetups.Add(new InventorySetup { StartDate = new DateOnly(2026, 7, 1), LastClosedDate = new DateOnly(2026, 7, 31) });
        (await p.Db.Warehouses.SingleAsync(w => w.Code == "B4")).FijarFechaDeCorte(new DateOnly(2026, 9, 5));
        await p.Db.SaveChangesAsync();
        p.Db.ChangeTracker.Clear();

        var r = await p.ImportarSaldoAsync(ModoDeImportacion.Review,
            PuestaEnMarchaDePrueba.Fila("PRIN", "P1", "1", "10"),                                    // 2: activa
            PuestaEnMarchaDePrueba.Fila("TR01", "P1", "1", "10"),                                    // 3: tránsito
            PuestaEnMarchaDePrueba.Fila("B5", "P1", "1", "10"),                                      // 4: fuera del alcance
            PuestaEnMarchaDePrueba.Fila("B3", "P1", "1", "10"),                                      // 5
            PuestaEnMarchaDePrueba.Fila("B3", "P2", "1", "10", corte: new DateOnly(2026, 9, 11)),    // 6: otra fecha en la misma bodega
            PuestaEnMarchaDePrueba.Fila("B4", "P1", "1", "10"),                                      // 7: distinta de la fijada
            PuestaEnMarchaDePrueba.Fila("B3", "P1", "1", "10", "A-01", new DateOnly(2026, 10, 1)),   // 8: futura
            PuestaEnMarchaDePrueba.Fila("B4", "P2", "1", "10", corte: new DateOnly(2026, 7, 20)));   // 9: período cerrado

        var e = r.Value.Errors;
        e.Should().ContainSingle(x => x.Row == 2 && x.Code == GoLiveErrors.OpeningBalanceWarehouseActiveCode);
        e.Should().ContainSingle(x => x.Row == 3 && x.Code == GoLiveErrors.OpeningBalanceTransitNotAllowedCode);
        e.Should().ContainSingle(x => x.Row == 4 && x.Column == S.Bodega && x.Code == ImportErrors.CellNotFound);
        e.Should().NotContain(x => x.Row == 5);
        e.Should().ContainSingle(x => x.Row == 6 && x.Column == S.FechaDeCorte && x.Code == ImportErrors.CellFormat);
        e.Should().ContainSingle(x => x.Row == 7 && x.Column == S.FechaDeCorte && x.Code == ImportErrors.CellFormat);
        e.Should().ContainSingle(x => x.Row == 8 && x.Code == "Inventory.Document.DateInFuture");
        e.Should().ContainSingle(x => x.Row == 9 && x.Code == "Inventory.Period.Closed");
    }

    [Fact]
    public async Task Una_bodega_con_saldo_inicial_confirmado_vigente_lo_rechaza_nombrando_los_documentos()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        var confirmado = await p.SaldoConfirmadoAsync(p.B3, "P1", 5m, 1000m);

        var r = await p.ImportarSaldoAsync(ModoDeImportacion.Review, PuestaEnMarchaDePrueba.Fila("B3", "P2", "1", "10"));

        var error = r.Value.Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be(GoLiveErrors.OpeningBalanceAlreadyConfirmedCode);
        error.Column.Should().Be(S.Bodega);
        error.Message.Should().Contain("anúlelo primero");
        (await p.Db.InventoryDocuments.SingleAsync(d => d.PublicId == confirmado)).Status.Should().Be(DocumentStatus.Confirmed);
        GoLiveErrors.OpeningBalanceAlreadyConfirmed("B3", [new(confirmado, "1", PuestaEnMarchaDePrueba.Corte)])
            .Should().BeOfType<ErrorConDatos>().Which.Data.ToString().Should().Contain("documents");
    }

    // ----------------------------------------------------------------------------------------- los borradores --

    [Fact]
    public async Task Nueve_mil_quinientas_lineas_de_una_bodega_quedan_en_tres_borradores_del_mismo_corte()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        var codigos = await p.ProductosEnBloqueAsync(9_500);
        p.Datos(PuestaEnMarchaDePrueba.Encabezados, codigos.Select(c => PuestaEnMarchaDePrueba.Fila("B3", c, "1", "10")));

        var r = await p.ImportarSaldoAsync(ModoDeImportacion.Apply);

        Bien(r);
        Documentos(r.Value).Select(d => d.Lines).Should().Equal(4_000, 4_000, 1_500);
        var documentos = await p.Db.InventoryDocuments.Include(d => d.Lines).OrderBy(d => d.Id).ToListAsync();
        documentos.Should().HaveCount(3).And.OnlyContain(d => d.Class == DocumentClass.OpeningBalance && d.Status == DocumentStatus.Draft
            && d.OperationDate == PuestaEnMarchaDePrueba.Corte && d.WarehouseId == p.B3.Id);
        documentos.Select(d => d.Lines.Count).Should().Equal(4_000, 4_000, 1_500);
        documentos[0].CostTotal.Should().Be(40_000m);
        (await p.Db.Warehouses.SingleAsync(w => w.Id == p.B3.Id)).CutoffDate.Should().Be(PuestaEnMarchaDePrueba.Corte);
    }

    [Fact]
    public async Task Volver_a_importar_reemplaza_las_lineas_del_mismo_borrador()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        var primera = await p.ImportarSaldoAsync(ModoDeImportacion.Apply,
            PuestaEnMarchaDePrueba.Fila("B3", "P1", "10", "1000"), PuestaEnMarchaDePrueba.Fila("B3", "P2", "2", "500"));
        Bien(primera);
        var borrador = Documentos(primera.Value).Single();
        borrador.Replaced.Should().BeFalse();

        var segunda = await p.ImportarSaldoAsync(ModoDeImportacion.Apply, PuestaEnMarchaDePrueba.Fila("B3", "P1", "12", "1100"));

        Bien(segunda);
        var reemplazo = Documentos(segunda.Value).Single();
        reemplazo.DocumentPublicId.Should().Be(borrador.DocumentPublicId);
        reemplazo.Replaced.Should().BeTrue();
        var documento = await p.Db.InventoryDocuments.Include(d => d.Lines).SingleAsync();
        documento.Lines.Count(l => !l.IsDeleted).Should().Be(1);
        documento.Lines.Count(l => l.IsDeleted).Should().Be(2, "las líneas viejas quedan de baja lógica (Principio XI)");
        documento.Lines.Single(l => !l.IsDeleted).Should().Match<Domain.Entities.Inventory.Documents.InventoryDocumentLine>(l =>
            l.Quantity == 12m && l.UnitCost == 1100m && l.TotalCost == 13_200m);
        documento.CostTotal.Should().Be(13_200m);
    }

    // ------------------------------------------------------------------------------------------- INV_Setup --

    [Fact]
    public async Task El_primer_apply_crea_INV_Setup_desde_el_mes_del_corte_mas_antiguo_y_despues_un_corte_anterior_es_error()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();

        var r = await p.ImportarSaldoAsync(ModoDeImportacion.Apply,
            PuestaEnMarchaDePrueba.Fila("B3", "P1", "10", "1000", corte: new DateOnly(2026, 9, 10)),
            PuestaEnMarchaDePrueba.Fila("B4", "P1", "10", "1000", corte: new DateOnly(2026, 8, 20)));

        Bien(r);
        var setup = await p.Db.InventorySetups.SingleAsync();
        setup.StartDate.Should().Be(new DateOnly(2026, 8, 1));
        setup.StartedByUserId.Should().Be(Kardex.KardexDePrueba.Usuario);

        var anterior = await p.ImportarSaldoAsync(ModoDeImportacion.Review,
            PuestaEnMarchaDePrueba.Fila("B5", "P1", "1", "10", corte: new DateOnly(2026, 7, 31)));
        anterior.Value.Errors.Should().ContainSingle(x => x.Column == S.FechaDeCorte && x.Code == "Inventory.Document.DateBeforeCutoff");
    }
}
