using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Exceptions;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T121 (T17, T18; Principio XI), en el motor de <c>DB_PROVIDER</c>: sobre el <c>ApplicationDbContext</c> real de una
/// cooperativa aislada, modificar o borrar un <c>DocumentPartySnapshot</c> o un <c>DocumentTaxLine</c> lanza
/// <see cref="ImmutableEntityModifiedException"/>; en un <c>InventoryDocument</c> confirmado sólo se admite cambiar
/// <c>Status</c> a <c>Voided</c>, <c>VoidedByDocumentId</c>, <c>FiscalNumberReleased</c> y la auditoría; sus líneas no
/// cambian. La prueba sin contenedores de la misma guarda es <c>GuardaDeInmutabilidadTests</c>.
///
/// <para>
/// Escribe a la base por debajo a propósito: lo que se prueba es la guarda del contexto, no la API. Las líneas y la foto
/// tributaria referencian el catálogo (producto, unidad, impuesto, tarifa): los toma de la semilla tributaria (T168) y del
/// catálogo que deja <see cref="EscenarioDeInventario"/>.
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class InmutabilidadDeDocumentosTests(CentralIdentityApiFixture fx)
{
    private static bool EsSqlServer => CentralIdentityApiFixture.ProviderKey == "SqlServer";

    [Fact]
    public async Task Un_hecho_no_se_modifica_ni_se_borra_y_un_confirmado_solo_se_anula()
    {
        // El catálogo (producto y unidad) lo deja el escenario de ensayo; las bodegas no hacen falta activas.
        var coop = (await EscenarioDeInventario.PrepararAsync(fx, "inmutables", activar: false)).Coop;
        using (var http = fx.CreateClient()) await Accounting.ContabilidadE2E.CrearPersonaAsync(http, coop.TokenAdmin, "Inmutable");
        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == coop.TenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        var db = (ApplicationDbContext)ambito.Db;

        var sucursal = await db.Branches.Select(b => b.Id).FirstAsync();
        var usuario = await db.Users.Select(u => u.Id).FirstAsync();
        var persona = await db.People.Select(p => p.Id).FirstOrDefaultAsync();
        var producto = await PrimerIdAsync(db, "INV_Products");
        var unidad = await PrimerIdAsync(db, "INV_UnitsOfMeasure");
        var impuesto = await PrimerIdAsync(db, "COR_TaxDefinitions");
        var tarifa = await PrimerIdAsync(db, "COR_TaxRates");
        producto.Should().NotBeNull("la cooperativa necesita un producto del catálogo de US1 para la línea");
        unidad.Should().NotBeNull("la cooperativa necesita una unidad de medida");
        impuesto.Should().NotBeNull("la semilla tributaria (T168) deja definiciones");
        tarifa.Should().NotBeNull("la semilla tributaria (T168) deja tarifas");

        var tipo = await db.InventoryDocumentTypes.FirstAsync(t => t.Class == DocumentClass.PositiveAdjustment);
        var documento = new InventoryDocument
        {
            Class = DocumentClass.PositiveAdjustment, DocumentTypeId = tipo.Id, OperationDate = DateOnly.FromDateTime(DateTime.UtcNow),
            BranchId = sucursal, CreatedByUserId = usuario, CounterpartyPersonId = persona == 0 ? null : persona,
            Prefix = "IN", Number = 900_001,
        };
        documento.Lines.Add(new InventoryDocumentLine
        {
            Document = documento, LineNumber = 1, ProductId = producto!.Value, UnitId = unidad!.Value, Quantity = 2, QuantityBase = 2, UnitCost = 10, TotalCost = 20,
        });
        documento.Confirmar(usuario, DateTime.UtcNow);
        db.InventoryDocuments.Add(documento);
        await db.SaveChangesAsync();

        var foto = new DocumentPartySnapshot
        {
            DocumentId = documento.Id, Version = 1, PersonId = persona == 0 ? await db.People.Select(p => p.Id).FirstAsync() : persona,
            DianOrganizationType = "2", DianIdTypeCode = "13", TaxId = "900", LegalName = "Tercero",
        };
        var renglon = new DocumentTaxLine
        {
            DocumentId = documento.Id, TaxDefinitionId = impuesto!.Value, TaxRateId = tarifa!.Value, TaxRateCode = "IVA19",
            Kind = TaxKind.Iva, Treatment = TaxTreatment.Deductible, Base = 20, Amount = 3.8m,
        };
        db.DocumentPartySnapshots.Add(foto);
        db.DocumentTaxLines.Add(renglon);
        await db.SaveChangesAsync();

        // (1) Los hechos: ni se modifican ni se borran.
        db.Entry(foto).Property(f => f.LegalName).CurrentValue = "Otro nombre";
        (await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.EntityType.Should().Be(nameof(DocumentPartySnapshot));
        db.ChangeTracker.Clear();

        db.DocumentTaxLines.Remove(await db.DocumentTaxLines.SingleAsync(t => t.Id == renglon.Id));
        (await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.EntityType.Should().Be(nameof(DocumentTaxLine));
        db.ChangeTracker.Clear();

        // (2) El confirmado: sus líneas y sus columnas no cambian…
        var linea = await db.InventoryDocumentLines.SingleAsync(l => l.DocumentId == documento.Id);
        linea.Quantity = 3;
        (await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.Property.Should().Be(nameof(InventoryDocumentLine.Quantity));
        db.ChangeTracker.Clear();

        var confirmado = await db.InventoryDocuments.SingleAsync(d => d.Id == documento.Id);
        confirmado.Notes = "una nota tardía";
        (await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.Property.Should().Be(nameof(InventoryDocument.Notes));
        db.ChangeTracker.Clear();

        // …salvo pasar a anulado con la referencia a su anulación y liberar el número (FR-066 caso b).
        var anulacion = new InventoryDocument
        {
            Class = DocumentClass.Voiding, DocumentTypeId = (await db.InventoryDocumentTypes.FirstAsync(t => t.Class == DocumentClass.Voiding)).Id,
            OperationDate = DateOnly.FromDateTime(DateTime.UtcNow), BranchId = sucursal, CreatedByUserId = usuario, Reason = "prueba",
            VoidsDocumentId = documento.Id,
        };
        db.InventoryDocuments.Add(anulacion);
        await db.SaveChangesAsync();

        var aAnular = await db.InventoryDocuments.SingleAsync(d => d.Id == documento.Id);
        aAnular.MarcarAnulado(anulacion.Id);
        aAnular.FiscalNumberReleased = true;
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var anulado = await db.InventoryDocuments.AsNoTracking().SingleAsync(d => d.Id == documento.Id);
        anulado.Status.Should().Be(DocumentStatus.Voided);
        anulado.VoidedByDocumentId.Should().Be(anulacion.Id);
        (await db.InventoryDocumentLines.AsNoTracking().SingleAsync(l => l.DocumentId == documento.Id)).Quantity.Should().Be(2);
    }

    /// <summary>El menor Id de una tabla de la cooperativa, o nulo si está vacía.</summary>
    private static async Task<int?> PrimerIdAsync(ApplicationDbContext db, string tabla)
    {
        var conexion = db.Database.GetDbConnection();
        var abierta = conexion.State == System.Data.ConnectionState.Open;
        if (!abierta) await conexion.OpenAsync();
        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = EsSqlServer
                ? $"SELECT MIN([Id]) FROM [dbo].[{tabla}] WHERE [IsDeleted] = 0"
                : $"SELECT MIN(\"Id\") FROM dbo.\"{tabla}\" WHERE \"IsDeleted\" = false";
            var valor = await comando.ExecuteScalarAsync();
            return valor is null or DBNull ? null : Convert.ToInt32(valor);
        }
        finally
        {
            if (!abierta) await conexion.CloseAsync();
        }
    }
}
