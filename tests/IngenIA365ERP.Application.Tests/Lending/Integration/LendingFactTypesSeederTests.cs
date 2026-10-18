using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Persistence.Seeding;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, T0140 (C06, C110, C117; data-model §1.11; contracts/mensajes.md §2.1; contabilidad.md §2.1, §5): la semilla
/// <see cref="LendingFactTypesSeeder"/> (Order 90) deja un <c>LND_FactTypes</c> por cada tipo <c>CAR → Accounting</c> del catálogo de
/// mensajes de Cartera —35, con los tres contingentes—, con su código corto único de 3 a 4 letras, si es informativo, su comprobante,
/// su modo y disparador por defecto y su entrega; es idempotente, pone al día los nombres y nunca borra.
/// </summary>
public class LendingFactTypesSeederTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    [Fact]
    public void Corre_en_el_orden_90_por_cooperativa()
    {
        var seeder = new LendingFactTypesSeeder();
        seeder.Order.Should().Be(90);
        seeder.Scope.Should().Be(SeedScope.Tenant);
        seeder.Category.Should().Be(SeedCategory.Parametric);
    }

    [Fact]
    public async Task Siembra_los_37_hechos_a_contabilidad_con_su_codigo_corto()
    {
        // K6b (T1056): 35 hasta K6a, la corrección monetaria UVR y el reparto de un pago corregido por la UVR (decisión del dueño del 2026-10-17).
        (await LendingFactTypesSeeder.AplicarAsync(_db, NullLogger.Instance, CancellationToken.None)).Should().Be(37);

        var filas = await _db.LendingFactTypes.ToListAsync();
        filas.Should().HaveCount(37);
        filas.Select(f => f.Code).Should().BeEquivalentTo(CatalogoDeMensajesDeCarteraV1.HechosAContabilidad.Select(t => t.Type));
        CatalogoDeMensajesDeCarteraV1.Todos.Count.Should().Be(43, "los 6 a otros destinos no son hechos contables y no tienen fila");
        filas.Single(f => f.Code == "CorreccionMonetariaUvrCausada").Should().BeEquivalentTo(new
        {
            ShortCode = "CMU", VoucherTypeCode = "CA", DefaultMode = "PorLotes", DefaultBatchTrigger = "FinDeCausacion", DeliveredIn = EntregaDeCartera.K6b,
            IsInformational = false,
        });
        filas.Single(f => f.Code == "PagoRedistribuidoPorValorNacional").Should().BeEquivalentTo(new
        {
            ShortCode = "PRN", VoucherTypeCode = "CA", DefaultMode = "PorLotes", DefaultBatchTrigger = "FinDeCausacion", DeliveredIn = EntregaDeCartera.K6b,
            IsInformational = false,
        });
        filas.Select(f => f.ShortCode).Should().OnlyHaveUniqueItems()
            .And.OnlyContain(c => c.Length >= 3 && c.Length <= 4 && c.All(char.IsUpper));
        filas.Should().Contain(f => f.Code == "CreditoAprobadoSinDesembolsar" && f.ShortCode == "CAD" && f.DefaultMode == "NoPasa" && f.VoucherTypeCode == "CN");
        filas.Should().Contain(f => f.Code == "CupoRotativoOtorgado" && f.ShortCode == "CRO");
        filas.Should().Contain(f => f.Code == "GarantiaConstituida" && f.ShortCode == "GCT");
        filas.Should().OnlyContain(f => f.IsSeeded && !string.IsNullOrWhiteSpace(f.Name));
    }

    [Fact]
    public async Task Los_informativos_no_llevan_comprobante_y_los_de_negocio_si()
    {
        await LendingFactTypesSeeder.AplicarAsync(_db, NullLogger.Instance, CancellationToken.None);
        var filas = await _db.LendingFactTypes.ToListAsync();

        filas.Where(f => f.IsInformational).Select(f => f.Code).Should().BeEquivalentTo(
            ["CausacionSuspendida", "CausacionReanudada", "CarteraActivada", "CargaDeCarteraAplicada", "CierreDeCarteraRealizado", "CierreDeCarteraReabierto"]);
        filas.Where(f => f.IsInformational).Should().OnlyContain(f => f.VoucherTypeCode == null);
        filas.Where(f => !f.IsInformational).Should().OnlyContain(f => new[] { "DS", "RC", "CA", "PV", "CN" }.Contains(f.VoucherTypeCode));
        filas.Single(f => f.Code == "InteresCausado").Should().BeEquivalentTo(new { VoucherTypeCode = "CA", DefaultMode = "PorLotes", DefaultBatchTrigger = "FinDeCausacion", DeliveredIn = EntregaDeCartera.K1 });
        filas.Single(f => f.Code == "DeterioroCalculado").DefaultBatchTrigger.Should().Be("CierreDeCartera");
        filas.Single(f => f.Code == "CreditoCastigado").DeliveredIn.Should().Be(EntregaDeCartera.K3);
        filas.Single(f => f.Code == "GastoDeCobranzaCausado").DeliveredIn.Should().Be(EntregaDeCartera.K4);
    }

    [Fact]
    public async Task Es_idempotente_pone_al_dia_los_nombres_y_nunca_borra()
    {
        await LendingFactTypesSeeder.AplicarAsync(_db, NullLogger.Instance, CancellationToken.None);
        (await LendingFactTypesSeeder.AplicarAsync(_db, NullLogger.Instance, CancellationToken.None)).Should().Be(0);

        var pago = await _db.LendingFactTypes.SingleAsync(f => f.Code == "PagoAplicado");
        pago.Name = "Nombre viejo";
        var retirado = new IngenIA365ERP.Domain.Entities.Lending.Configuration.LendingFactType
        {
            Code = "TipoRetirado", ShortCode = "ZZZ", Name = "Ya no está", DefaultMode = "EnLinea",
        };
        _db.LendingFactTypes.Add(retirado);
        await _db.SaveChangesAsync();

        (await LendingFactTypesSeeder.AplicarAsync(_db, NullLogger.Instance, CancellationToken.None)).Should().Be(1);
        (await _db.LendingFactTypes.SingleAsync(f => f.Code == "PagoAplicado")).Name.Should().Be("Pago aplicado");
        (await _db.LendingFactTypes.AnyAsync(f => f.Code == "TipoRetirado")).Should().BeTrue("la semilla nunca borra");
    }

    [Fact]
    public void Cada_hecho_del_catalogo_tiene_sus_datos_y_un_tipo_sin_codigo_corto_haria_fallar_el_arranque()
    {
        LendingFactTypesSeeder.Datos.Keys.Should().BeEquivalentTo(CatalogoDeMensajesDeCarteraV1.HechosAContabilidad.Select(t => t.Type));
        CatalogoDeMensajesDeCarteraV1.HechosAContabilidad.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.CodigoCorto));
        CatalogoDeMensajesDeCarteraV1.HechosAContabilidad.Should().OnlyContain(t => t.Destination == IntegrationDestinations.Accounting);
        LendingFactTypesSeeder.Catalogo().Should().HaveCount(37);
    }
}
