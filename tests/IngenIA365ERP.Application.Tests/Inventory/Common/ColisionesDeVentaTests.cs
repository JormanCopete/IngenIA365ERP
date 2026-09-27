using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Common;

/// <summary>
/// Feature 012, I3 (T589; T50; data-model §15 «Concurrencia» y §16; api.md §21): la base garantiza una sesión abierta por caja y por
/// cajero exclusivo, un cierre del día por punto y fecha, y que un bono de número único no se use en dos cajas; cuando dos personas
/// llegan a la vez, el segundo <c>SaveChanges</c> choca con el índice y <see cref="ColisionesDeVenta"/> lo traduce al mismo error
/// de negocio que habría dado la comprobación previa, nombrando lo que ganó. En PostgreSQL y en SQL Server.
/// </summary>
public class ColisionesDeVentaTests
{
    private static DbUpdateException DePostgreSql(string indice) =>
        new("error al guardar", new InvalidOperationException($"23505: duplicate key value violates unique constraint \"{indice}\""));

    private static DbUpdateException DeSqlServer(string indice) =>
        new("error al guardar", new InvalidOperationException($"Cannot insert duplicate key row in object 'dbo.X' with unique index '{indice}'."));

    public static TheoryData<string> Indices => new()
    {
        ColisionesDeVenta.IndiceDelBono,
        ColisionesDeVenta.IndiceDeLaCaja,
        ColisionesDeVenta.IndiceDelCajero,
        ColisionesDeVenta.IndiceDelCierreDelDia,
    };

    [Fact]
    public void Los_nombres_de_indice_son_los_de_las_configuraciones()
    {
        ColisionesDeVenta.IndiceDelBono.Should().Be("UK_INV_VoucherRedemptions_Means_Number_Active");
        ColisionesDeVenta.IndiceDeLaCaja.Should().Be("UK_INV_CashSessions_Register_Open");
        ColisionesDeVenta.IndiceDelCajero.Should().Be("UK_INV_CashSessions_Cashier_Open");
        ColisionesDeVenta.IndiceDelCierreDelDia.Should().Be("UK_INV_DayCloses_Point_Date_Closed");
    }

    [Theory]
    [MemberData(nameof(Indices))]
    public void Reconoce_el_indice_en_los_dos_motores(string indice)
    {
        ColisionesDeVenta.Indice(DePostgreSql(indice)).Should().Be(indice);
        ColisionesDeVenta.Indice(DeSqlServer(indice)).Should().Be(indice);
        ColisionesDeVenta.Es(DeSqlServer(indice)).Should().BeTrue();
    }

    [Fact]
    public async Task Otra_excepcion_no_se_traduce()
    {
        using var db = TestDbContextFactory.Create();
        var otra = DePostgreSql("UK_COR_People_TaxId");

        ColisionesDeVenta.Es(otra).Should().BeFalse();
        (await ColisionesDeVenta.TraducirAsync(db, otra, new DatosDeLaColision(), default)).Should().BeNull();
    }

    [Fact]
    public async Task El_bono_ya_usado_nombra_la_venta_que_lo_uso()
    {
        using var db = TestDbContextFactory.Create();
        var venta = new InventoryDocument { Class = DocumentClass.PosEquivalentDocument, Prefix = "POS", Number = 1234 };
        db.InventoryDocuments.Add(venta);
        await db.SaveChangesAsync();
        db.VoucherRedemptions.Add(new VoucherRedemption { PaymentMeansId = 7, NormalizedNumber = "NAV2026001", DocumentId = venta.Id, DocumentPaymentId = 1 });
        await db.SaveChangesAsync();

        var error = await ColisionesDeVenta.TraducirAsync(db, DePostgreSql(ColisionesDeVenta.IndiceDelBono),
            new DatosDeLaColision(Bonos: [(7, "OTRO"), (7, "NAV2026001")]), default);

        error!.Code.Should().Be("Payments.VoucherAlreadyUsed");
        error.Message.Should().Contain("POS1234");
        error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new
        {
            documentPublicId = venta.PublicId,
            documentClass = DocumentClass.PosEquivalentDocument.ToString(),
            prefix = "POS",
            number = 1234L,
        });
    }

    [Fact]
    public async Task Un_bono_liberado_no_cuenta_y_sin_ganador_visible_el_error_es_el_mismo()
    {
        using var db = TestDbContextFactory.Create();
        var liberado = new VoucherRedemption { PaymentMeansId = 7, NormalizedNumber = "NAV1", DocumentId = 99, DocumentPaymentId = 1 };
        liberado.Liberar(100, DateTime.UtcNow, "nota con reintegro");
        db.VoucherRedemptions.Add(liberado);
        await db.SaveChangesAsync();

        var error = await ColisionesDeVenta.TraducirAsync(db, DeSqlServer(ColisionesDeVenta.IndiceDelBono),
            new DatosDeLaColision(Bonos: [(7, "NAV1")]), default);

        error!.Code.Should().Be("Payments.VoucherAlreadyUsed");
    }

    [Fact]
    public async Task La_caja_ocupada_nombra_al_cajero_y_la_hora_de_apertura()
    {
        using var db = TestDbContextFactory.Create();
        var abierta = new DateTime(2026, 9, 27, 13, 0, 0, DateTimeKind.Utc);
        db.CashSessions.Add(new CashSession { CashRegisterId = 3, PointOfSaleId = 1, CashierUserId = 10, CashierName = "Ana Gómez", OpenedAt = abierta });
        await db.SaveChangesAsync();

        var error = await ColisionesDeVenta.TraducirAsync(db, DePostgreSql(ColisionesDeVenta.IndiceDeLaCaja),
            new DatosDeLaColision(CashRegisterId: 3, CashierUserId: 11), default);

        error!.Code.Should().Be("Inventory.CashSession.RegisterBusy");
        error.Message.Should().Contain("Ana Gómez");
        error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { cashierName = "Ana Gómez", openedAt = abierta });
    }

    [Fact]
    public async Task El_cajero_ocupado_nombra_la_caja_donde_ya_tiene_sesion()
    {
        using var db = TestDbContextFactory.Create();
        var caja = new CashRegister { Code = "CAJA2", Name = "Caja 2", PointOfSaleId = 1 };
        db.CashRegisters.Add(caja);
        await db.SaveChangesAsync();
        db.CashSessions.Add(new CashSession { CashRegisterId = caja.Id, PointOfSaleId = 1, CashierUserId = 10, CashierName = "Ana", ExclusiveCashier = true });
        await db.SaveChangesAsync();

        var error = await ColisionesDeVenta.TraducirAsync(db, DeSqlServer(ColisionesDeVenta.IndiceDelCajero),
            new DatosDeLaColision(CashRegisterId: 99, CashierUserId: 10), default);

        error!.Code.Should().Be("Inventory.CashSession.CashierBusy");
        error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { cashRegisterCode = "CAJA2" });
    }

    [Fact]
    public async Task Una_sesion_cerrada_no_ocupa_la_caja()
    {
        using var db = TestDbContextFactory.Create();
        var cerrada = new CashSession { CashRegisterId = 3, PointOfSaleId = 1, CashierUserId = 10, CashierName = "Ana" };
        cerrada.Cerrar(10, DateTime.UtcNow);
        db.CashSessions.Add(cerrada);
        await db.SaveChangesAsync();

        (await ColisionesDeVenta.CajaOcupadaAsync(db, 3, default)).Should().BeNull();
        (await ColisionesDeVenta.CajeroOcupadoAsync(db, 10, default)).Should().BeNull();
    }

    [Fact]
    public async Task El_dia_ya_cerrado_se_traduce_con_el_punto_y_la_fecha()
    {
        using var db = TestDbContextFactory.Create();
        var fecha = new DateOnly(2026, 9, 27);
        db.DayCloses.Add(new DayClose { PointOfSaleId = 5, OperatingDate = fecha, ClosedAt = DateTime.UtcNow, ClosedByUserId = 1 });
        await db.SaveChangesAsync();

        var error = await ColisionesDeVenta.TraducirAsync(db, DePostgreSql(ColisionesDeVenta.IndiceDelCierreDelDia),
            new DatosDeLaColision(PointOfSaleId: 5, OperatingDate: fecha), default);

        error!.Code.Should().Be("Inventory.DayClose.AlreadyClosed");
        error.Message.Should().Contain("2026-09-27");
        (await ColisionesDeVenta.DiaCerradoAsync(db, 5, fecha.AddDays(1), default)).Should().BeNull();
    }
}
