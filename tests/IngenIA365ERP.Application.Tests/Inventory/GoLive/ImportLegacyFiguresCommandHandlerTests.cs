using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Inventory.GoLive;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using Microsoft.EntityFrameworkCore;
using F = IngenIA365ERP.Application.Inventory.GoLive.PlantillaDeCifrasDeSolido;

namespace IngenIA365ERP.Application.Tests.Inventory.GoLive;

/// <summary>
/// Feature 012, T300 (contracts/plantillas.md §15; api.md §13.2; FR-091): la plantilla 15 —cifras de SOLIDO—. Llave fecha +
/// bodega + producto; la bodega debe existir; un producto que no está en el catálogo nuevo exige su grupo y queda sin resolver
/// con aviso; un grupo distinto del del producto avisa; la cantidad puede ser negativa; reimportar un par (fecha, bodega) deja
/// el lote anterior de baja; y nada de esto toca el kardex (FR-001).
/// </summary>
public class ImportLegacyFiguresCommandHandlerTests
{
    private static readonly string[] Encabezados = [F.Fecha, F.Bodega, F.Producto, F.Cantidad, F.Valor, F.GrupoContable];
    private const string Fecha = "2026-09-10";

    private static string?[] Fila(string bodega, string producto, string cantidad, string valor, string? grupo = null, string fecha = Fecha) =>
        [fecha, bodega, producto, cantidad, valor, grupo];

    [Fact]
    public async Task Bodega_y_producto_se_resuelven_o_avisan_y_la_cantidad_negativa_se_admite()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        p.Db.AccountingGroups.Add(new AccountingGroup { Code = "FERRE", Name = "Ferretería", IsActive = true });
        await p.Db.SaveChangesAsync();

        var r = await p.ImportarCifrasAsync(ModoDeImportacion.Review, Encabezados,
            Fila("B3", "P1", "10", "15000"),                   // 2: resuelto
            Fila("B9", "P1", "1", "10"),                       // 3: bodega inexistente
            Fila("B3", "OBSOL-77", "3", "45000"),              // 4: sin producto ni grupo: error
            Fila("B4", "OBSOL-77", "3", "45000", "ABARR"),     // 5: sin producto con grupo: aviso
            Fila("B4", "P2", "-2", "-3000", "FERRE"),          // 6: grupo distinto: aviso; negativa
            Fila("B3", "P1", "4", "100"));                     // 7: llave repetida

        var v = r.Value;
        v.Errors.Should().ContainSingle(e => e.Row == 3 && e.Column == F.Bodega && e.Code == ImportErrors.CellNotFound);
        v.Errors.Should().ContainSingle(e => e.Row == 4 && e.Column == F.GrupoContable && e.Code == ImportErrors.CellRequired);
        v.Errors.Should().ContainSingle(e => e.Row == 7 && e.Code == ImportErrors.RowDuplicate);
        v.Errors.Should().HaveCount(3);
        v.Warnings.Should().ContainSingle(w => w.Row == 5 && w.Code == GoLiveErrors.LegacyFiguresCodeUnresolvedCode);
        v.Warnings.Should().ContainSingle(w => w.Row == 6 && w.Code == GoLiveErrors.LegacyFiguresGroupMismatchCode);

        var porGrupo = (IReadOnlyList<CifraPorFechaBodegaGrupoDto>)v.Extra[ImportLegacyFiguresCommandHandler.ExtraPorFechaBodegaGrupo]!;
        porGrupo.Should().Contain(x => x.Bodega == "B4" && x.GrupoContable == "FERRE" && x.Cantidad == -2m && x.Valor == -3000m);
        porGrupo.Should().Contain(x => x.Bodega == "B4" && x.GrupoContable == "ABARR" && x.Cantidad == 3m);
    }

    [Fact]
    public async Task Aplicar_guarda_codigos_crudos_e_ids_y_reimportar_el_mismo_par_deja_el_lote_anterior_de_baja_sin_tocar_el_kardex()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();

        var primera = await p.ImportarCifrasAsync(ModoDeImportacion.Apply, Encabezados,
            Fila("B3", "P1", "10", "15000"), Fila("B3", "OBSOL-77", "3", "45000", "ABARR"), Fila("B4", "P2", "5", "5000"));
        primera.IsSuccess.Should().BeTrue(primera.IsFailure ? primera.Error.Message : string.Empty);
        var lote1 = (Guid)primera.Value.Extra[ImportLegacyFiguresCommandHandler.ExtraLote]!;
        ((IReadOnlyList<Guid>)primera.Value.Extra[ImportLegacyFiguresCommandHandler.ExtraReemplazados]!).Should().BeEmpty();

        var sinResolver = await p.Db.LegacyFigures.SingleAsync(x => x.ProductCodeRaw == "OBSOL-77");
        sinResolver.ProductId.Should().BeNull();
        sinResolver.AccountingGroupId.Should().Be(p.K.C.GrupoAbarrotes.Id);
        sinResolver.WarehouseCodeRaw.Should().Be("B3");
        sinResolver.ImportBatchPublicId.Should().Be(lote1);
        (await p.Db.LegacyFigures.SingleAsync(x => x.ProductCodeRaw == "P1")).ProductId.Should().Be(p.K.ProductoId(p.K.P1));

        var segunda = await p.ImportarCifrasAsync(ModoDeImportacion.Apply, Encabezados, Fila("B3", "P1", "11", "16500"));

        segunda.IsSuccess.Should().BeTrue();
        ((IReadOnlyList<Guid>)segunda.Value.Extra[ImportLegacyFiguresCommandHandler.ExtraReemplazados]!).Should().Equal(lote1);
        (await p.Db.LegacyFigures.CountAsync(x => x.WarehouseId == p.B3.Id)).Should().Be(1, "lo anterior de (fecha, B3) queda de baja lógica");
        (await p.Db.LegacyFigures.IgnoreQueryFilters().CountAsync(x => x.WarehouseId == p.B3.Id && x.IsDeleted)).Should().Be(2);
        (await p.Db.LegacyFigures.CountAsync(x => x.WarehouseId == p.B4.Id)).Should().Be(1, "otro par no se toca");

        (await p.Db.KardexEntries.CountAsync()).Should().Be(0, "las cifras de SOLIDO nunca mueven existencias (FR-001)");
        (await p.Db.StockBalances.CountAsync()).Should().Be(0);
        (await p.Db.CostStates.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Las_consultas_listan_los_lotes_por_fecha_y_bodega_y_las_filas_sin_resolver()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        await p.ImportarCifrasAsync(ModoDeImportacion.Apply, Encabezados, Fila("B3", "P1", "10", "15000"), Fila("B3", "OBSOL-77", "3", "45000", "ABARR"));
        await p.ImportarCifrasAsync(ModoDeImportacion.Apply, Encabezados, Fila("B3", "P1", "12", "18000"));

        var lotes = await new ListLegacyFigureBatchesQueryHandler(p.Db, p.K.Alcance).Handle(new ListLegacyFigureBatchesQuery(new DateOnly(2026, 9, 10), "b3"), default);

        lotes.Value.Should().HaveCount(2);
        var viejo = lotes.Value.Single(l => l.Superseded);
        viejo.Rows.Should().Be(2);
        viejo.UnresolvedRows.Should().Be(1);
        viejo.Value.Should().Be(60_000m);
        lotes.Value.Single(l => !l.Superseded).Quantity.Should().Be(12m);

        var filas = await new ListLegacyFigureRowsQueryHandler(p.Db, p.K.Alcance).Handle(new ListLegacyFigureRowsQuery(viejo.BatchPublicId, UnresolvedOnly: true), default);
        filas.Value.Items.Should().ContainSingle().Which.ProductCode.Should().Be("OBSOL-77");
        filas.Value.Items[0].ProductPublicId.Should().BeNull();
        filas.Value.Items[0].AccountingGroupCode.Should().Be("ABARR");
    }
}
