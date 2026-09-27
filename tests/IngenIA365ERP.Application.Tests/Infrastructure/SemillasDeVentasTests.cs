using FluentAssertions;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Feature 012, I3 (T587, T588; decisiones-transversales §2.14; data-model §16 y §25): las semillas de ventas —billetes y monedas
/// (Order 85), el medio <c>EFECTIVO</c> (86) y la persona «Consumidor final» (87)— y los tipos por defecto de caja (<c>MC</c>,
/// <c>DA</c>) en la semilla de tipos de documento. Todas idempotentes y sin pisar lo que la cooperativa cambió.
/// </summary>
public class SemillasDeVentasTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 27);

    // ------------------------------------------------------------------------------------------ denominaciones --

    [Fact]
    public async Task Las_denominaciones_son_los_billetes_y_monedas_COP_vigentes_e_idempotentes()
    {
        using var db = TestDbContextFactory.Create();

        var insertadas = await CashDenominationsSeeder.AplicarAsync(db, default);

        var filas = await db.CashDenominations.ToListAsync();
        insertadas.Should().Be(filas.Count);
        filas.Should().OnlyContain(d => d.Currency == "COP" && d.IsActive && d.ValidTo == null);
        filas.Where(d => d.Kind == CashDenominationKind.Bill).Select(d => d.Value)
            .Should().BeEquivalentTo([2_000m, 5_000m, 10_000m, 20_000m, 50_000m, 100_000m]);
        filas.Where(d => d.Kind == CashDenominationKind.Coin).Select(d => d.Value)
            .Should().BeEquivalentTo([50m, 100m, 200m, 500m, 1_000m]);
        filas.Select(d => d.DisplayOrder).Should().OnlyHaveUniqueItems();
        filas.OrderBy(d => d.DisplayOrder).First().Value.Should().Be(100_000m, "el arqueo lista de la denominación mayor a la menor");

        (await CashDenominationsSeeder.AplicarAsync(db, default)).Should().Be(0, "idempotente por (moneda, clase, valor)");
    }

    [Fact]
    public async Task Una_denominacion_que_la_cooperativa_dio_de_baja_no_vuelve()
    {
        using var db = TestDbContextFactory.Create();
        db.CashDenominations.Add(new CashDenomination { Currency = "COP", Kind = CashDenominationKind.Coin, Value = 50m, IsDeleted = true });
        await db.SaveChangesAsync();

        await CashDenominationsSeeder.AplicarAsync(db, default);

        (await db.CashDenominations.IgnoreQueryFilters().CountAsync(d => d.Kind == CashDenominationKind.Coin && d.Value == 50m)).Should().Be(1);
    }

    // ------------------------------------------------------------------------------------------ efectivo --

    [Fact]
    public async Task El_unico_medio_sembrado_es_EFECTIVO_de_clase_efectivo_con_arqueo_fisico_y_vueltas()
    {
        using var db = TestDbContextFactory.Create();

        (await DefaultPaymentMeansSeeder.AplicarAsync(db, default)).Should().Be(1);

        var medio = await db.PaymentMeans.SingleAsync();
        medio.Should().BeEquivalentTo(new
        {
            Code = "EFECTIVO",
            Class = PaymentMeansClass.Cash,
            CountMethod = CashCountMethod.PhysicalCount,
            AllowsChange = true,
            AllowsPartial = true,
            RequiresReference = false,
            UniqueReference = false,
            DianPaymentMeansCode = "10",
            OfferedAtAllPointsOfSale = true,
            OfferedInAllChannels = true,
            OfferedForAllDocumentTypes = true,
            IsActive = true,
            ToleranceAmount = 0m,
        }, o => o.ExcludingMissingMembers());
        medio.Notes.Should().Contain("pendiente de validar por la contadora");
        CatalogoDian.Embebido.MedioDePago(medio.DianPaymentMeansCode, Hoy).Should().NotBeNull("el código sugerido existe en el catálogo DIAN");
    }

    [Fact]
    public async Task La_semilla_del_efectivo_no_pisa_lo_que_la_cooperativa_cambio()
    {
        using var db = TestDbContextFactory.Create();
        await DefaultPaymentMeansSeeder.AplicarAsync(db, default);
        var medio = await db.PaymentMeans.SingleAsync();
        medio.Name = "Efectivo pesos";
        medio.ToleranceAmount = 500m;
        await db.SaveChangesAsync();

        (await DefaultPaymentMeansSeeder.AplicarAsync(db, default)).Should().Be(0);

        var despues = await db.PaymentMeans.SingleAsync();
        despues.Name.Should().Be("Efectivo pesos");
        despues.ToleranceAmount.Should().Be(500m);
    }

    // ------------------------------------------------------------------------------------------ consumidor final --

    [Fact]
    public async Task El_consumidor_final_es_una_persona_del_maestro_con_la_identificacion_del_catalogo_DIAN()
    {
        using var db = TestDbContextFactory.Create();
        var esperado = CatalogoDian.Embebido.ConsumidorFinal(Hoy)!;

        (await ConsumidorFinalSeeder.AplicarAsync(db, NullLogger.Instance, Hoy, default)).Should().Be(1);

        var persona = await db.People.SingleAsync();
        persona.TaxId.Should().Be(esperado.Numero);
        CatalogoDian.Embebido.TipoDeIdentificacionDe(persona.IdType, Hoy).Should().Be(esperado.TipoDeIdentificacion,
            "el tipo guardado en COR_People se traduce al del catálogo");
        ConsumidorFinalSeeder.NombreVisible(persona).Should().Be(esperado.Nombre);
        persona.IsCustomer.Should().BeTrue();
        persona.Status.Should().Be("A");
        persona.CreatedBy.Should().Be("system:seed");

        (await ConsumidorFinalSeeder.AplicarAsync(db, NullLogger.Instance, Hoy, default)).Should().Be(0, "idempotente por documento");
        (await db.People.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Si_el_documento_ya_existe_aunque_este_eliminado_no_se_crea_otra_persona()
    {
        using var db = TestDbContextFactory.Create();
        var numero = CatalogoDian.Embebido.ConsumidorFinal(Hoy)!.Numero;
        db.People.Add(new Person { TaxId = numero, FirstName = "Cliente", LastName = "Mostrador", IsDeleted = true });
        await db.SaveChangesAsync();

        (await ConsumidorFinalSeeder.AplicarAsync(db, NullLogger.Instance, Hoy, default)).Should().Be(0);
        (await db.People.IgnoreQueryFilters().CountAsync(p => p.TaxId == numero)).Should().Be(1, "UK_COR_People_TaxId no distingue eliminadas");
    }

    [Fact]
    public async Task Sin_catalogo_vigente_no_se_inventa_una_identificacion()
    {
        using var db = TestDbContextFactory.Create();

        (await ConsumidorFinalSeeder.AplicarAsync(db, NullLogger.Instance, new DateOnly(2000, 1, 1), default)).Should().Be(0);
        (await db.People.AnyAsync()).Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------------ tipos de caja --

    [Fact]
    public void Los_tipos_de_caja_tienen_codigo_propuesto()
    {
        InventoryDocumentTypesSeeder.Sembrados[DocumentClass.CashMovement].Codigo.Should().Be("MC");
        InventoryDocumentTypesSeeder.Sembrados[DocumentClass.CashCountDifference].Codigo.Should().Be("DA");
    }

    [Fact]
    public async Task Con_la_entrega_I3_vigente_la_semilla_deja_MC_y_DA_con_su_consecutivo_sin_intervencion()
    {
        using var db = TestDbContextFactory.Create();

        await InventoryDocumentTypesSeeder.AplicarAsync(db, EntregaDelComercio.I3, default);

        var tipos = await db.InventoryDocumentTypes.Include(t => t.Sequences)
            .Where(t => t.Class == DocumentClass.CashMovement || t.Class == DocumentClass.CashCountDifference).ToListAsync();
        tipos.Select(t => t.Code).Should().BeEquivalentTo(["MC", "DA"]);
        tipos.Should().OnlyContain(t => t.IsSeeded && t.IsActive && t.Sequences.Count == 1
            && t.Sequences.Single().Prefix == string.Empty && t.Sequences.Single().NextValue == 1
            && t.Sequences.Single().ValidFrom == InventoryDocumentTypesSeeder.VigenciaDeLaSemilla);
        tipos.Single(t => t.Code == "MC").RequiresReason.Should().BeTrue("todo movimiento de caja lleva motivo");

        (await InventoryDocumentTypesSeeder.AplicarAsync(db, EntregaDelComercio.I3, default)).Should().Be(0, "idempotente por código");
    }

    [Fact]
    public async Task Mientras_la_entrega_vigente_sea_anterior_a_I3_no_se_siembran_los_tipos_de_caja()
    {
        using var db = TestDbContextFactory.Create();

        await InventoryDocumentTypesSeeder.AplicarAsync(db, default);

        if (CatalogoDeParametros.EntregaVigente < EntregaDelComercio.I3)
            (await db.InventoryDocumentTypes.AnyAsync(t => t.Class == DocumentClass.CashMovement)).Should().BeFalse();
        else
            (await db.InventoryDocumentTypes.AnyAsync(t => t.Code == "MC")).Should().BeTrue();
    }
}
