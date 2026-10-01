using FluentAssertions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Exceptions;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Documents;

/// <summary>
/// Feature 012, T137 (T17, T18; Principio XI): la guarda que <c>ApplicationDbContext.SaveChangesAsync</c> corre antes de
/// escribir, probada sobre el seguimiento de cambios de un contexto en memoria. Un hecho no se modifica ni se borra; un
/// documento confirmado sólo cambia su estado a anulado, su anulación, la liberación del número y la auditoría; sus
/// líneas no cambian. La e2e sobre el contexto real es <c>InmutabilidadDeDocumentosTests</c> (T121).
/// </summary>
public class GuardaDeInmutabilidadTests
{
    private static readonly DateTime Ahora = new(2026, 9, 25, 15, 0, 0, DateTimeKind.Utc);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private InventoryDocument Guardado(bool confirmado)
    {
        var doc = new InventoryDocument { Class = DocumentClass.PositiveAdjustment, OperationDate = new DateOnly(2026, 9, 25) };
        doc.Lines.Add(new InventoryDocumentLine { LineNumber = 1, ProductId = 1, UnitId = 1, Quantity = 2, QuantityBase = 2 });
        if (confirmado) doc.Confirmar(7, Ahora);
        _db.InventoryDocuments.Add(doc);
        _db.SaveChanges();
        return doc;
    }

    private Task Verificar() => GuardaDeInmutabilidad.VerificarAsync(_db);

    [Fact]
    public async Task Un_hecho_se_inserta_pero_no_se_modifica_ni_se_borra()
    {
        var doc = Guardado(confirmado: true);
        var foto = new DocumentTaxLine { DocumentId = doc.Id, TaxDefinitionId = 1, TaxRateId = 1, TaxRateCode = "IVA19", Kind = TaxKind.Iva, Treatment = TaxTreatment.Deductible, Base = 100, Amount = 19 };
        _db.DocumentTaxLines.Add(foto);
        await Verificar();
        await _db.SaveChangesAsync();

        _db.Entry(foto).Property(f => f.Amount).CurrentValue = 20;
        (await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.Property.Should().Be(nameof(DocumentTaxLine.Amount));

        _db.Entry(foto).State = EntityState.Deleted;
        (await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.EntityType.Should().Be(nameof(DocumentTaxLine));
    }

    [Fact]
    public async Task Un_borrador_se_edita_libremente()
    {
        var doc = Guardado(confirmado: false);
        doc.Notes = "otra nota";
        doc.Lines.Single().Quantity = 5;
        await Verificar();
    }

    [Fact]
    public async Task Confirmado_no_cambia_sus_columnas()
    {
        var doc = Guardado(confirmado: true);
        doc.Notes = "cambio tardío";

        var e = await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>();
        e.Which.EntityType.Should().Be(nameof(InventoryDocument));
        e.Which.Property.Should().Be(nameof(InventoryDocument.Notes));
    }

    [Fact]
    public async Task Confirmado_admite_anularse_y_liberar_su_numero()
    {
        var doc = Guardado(confirmado: true);
        doc.MarcarAnulado(99);
        doc.FiscalNumberReleased = true;
        doc.UpdatedBy = "Ana";
        await Verificar();
    }

    [Fact]
    public async Task Confirmado_no_se_borra_ni_se_da_de_baja()
    {
        var doc = Guardado(confirmado: true);
        doc.IsDeleted = true;
        (await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.Property.Should().Be(nameof(InventoryDocument.IsDeleted));

        doc.IsDeleted = false;
        _db.Entry(doc).State = EntityState.Deleted;
        await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>();
    }

    [Fact]
    public async Task Las_lineas_de_un_confirmado_no_cambian()
    {
        var doc = Guardado(confirmado: true);
        doc.Lines.Single().Quantity = 9;

        (await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.EntityType.Should().Be(nameof(InventoryDocumentLine));
    }

    [Fact]
    public async Task Las_lineas_de_un_confirmado_tampoco_si_la_cabecera_no_esta_cargada()
    {
        var doc = Guardado(confirmado: true);
        var lineaId = doc.Lines.Single().Id;
        _db.ChangeTracker.Clear();

        var linea = await _db.InventoryDocumentLines.SingleAsync(l => l.Id == lineaId);
        linea.UnitCost = 10;

        await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>();
    }

    [Fact]
    public async Task La_confirmacion_escribe_el_costo_de_sus_lineas_en_el_mismo_guardado()
    {
        var doc = Guardado(confirmado: false);
        doc.Lines.Single().UnitCost = 1200;
        doc.Lines.Single().TotalCost = 2400;
        doc.Confirmar(7, Ahora);

        await Verificar();
    }
}
