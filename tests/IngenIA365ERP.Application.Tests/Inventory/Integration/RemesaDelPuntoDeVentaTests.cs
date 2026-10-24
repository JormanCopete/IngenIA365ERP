using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Inventory.Cash;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IngenIA365ERP.Application.Tests.Inventory.Integration;

/// <summary>
/// Feature 015, T1, T0409 (E-03; FR-166; contratos-v2.md §4): el cierre del día del punto de venta pide la remesa de su efectivo a Tesorería por
/// <see cref="IRemesasDeTesoreria"/> en su misma transacción, con la clave de la operación, la oficina del punto y el punto como origen; no remesa más que lo contado en
/// efectivo; sin Tesorería o con su rechazo, el cierre no queda. Sin remesa, el cierre es el de siempre. Prueba escrita, sin correr.
/// </summary>
public class RemesaDelPuntoDeVentaTests
{
    private VentasDePrueba V = null!;
    private KardexDePrueba K => V.K;
    private IngenIA365ERP.Application.Tests.Common.TestApplicationDbContext Db => V.Db;
    private readonly ICerrojoPorClave _cerrojo = Substitute.For<ICerrojoPorClave>();
    private readonly IAuditoriaDelPuntoDeVenta _auditoria = Substitute.For<IAuditoriaDelPuntoDeVenta>();
    private readonly IRemesasDeTesoreria _remesas = Substitute.For<IRemesasDeTesoreria>();

    private static async Task<RemesaDelPuntoDeVentaTests> CrearAsync()
    {
        var t = new RemesaDelPuntoDeVentaTests { V = await VentasDePrueba.CrearAsync() };
        foreach (var (codigo, clase) in new[] { ("CJ", DocumentClass.CashMovement), ("AQ", DocumentClass.CashCountDifference) })
        {
            var tipo = new InventoryDocumentType { Code = codigo, Name = codigo, Class = clase, IsActive = true, AllWarehouses = true };
            tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = codigo, NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
            t.Db.InventoryDocumentTypes.Add(tipo);
        }
        await t.Db.SaveChangesAsync();
        var emision = new EmisionDeInventario(t.Db);
        t.V.EfectosAdicionales.Add(new EfectoMovimientoDeCaja(t.Db, emision, t.Reglas(), t.V.Toque, t.V.Compras.C.Reloj));
        t.V.EfectosAdicionales.Add(new EfectoDiferenciaDeArqueo(t.Db, emision));
        t._remesas.RegistrarAsync(Arg.Any<SolicitudDeRemesa>(), Arg.Any<CancellationToken>()).Returns(new RemesaRegistradaDto(Guid.NewGuid(), "RM-0001"));
        return t;
    }

    private SesionesDeCaja Sesiones() => new(Db, K.Actor, K.Alcance, K.Permisos);

    private ReglasDeMovimientoDeCaja Reglas() => new(Db, Sesiones());

    private ArqueoDeLaSesion Arqueos() => new(Db, V.Compras.C.Reloj, Sesiones(), V.Confirmacion());

    private CloseCashSessionCommandHandler Cerrar() => new(Db, K.Actor, V.Compras.C.Reloj, Sesiones(), Arqueos(), V.Toque, V.Confirmacion(), _auditoria);

    private ExecuteDayCloseCommandHandler CerrarDia(IRemesasDeTesoreria? remesas) => new(Db, K.Actor, V.Compras.C.Reloj, K.Alcance, _cerrojo, _auditoria, remesas);

    private async Task<decimal> DiaConEfectivoAsync()
    {
        var venta = await V.VentaConfirmadaAsync(total => [new DocumentPaymentInput(V.Efectivo.PublicId, total, CashSessionPublicId: V.Sesion.PublicId)],
            lineas: V.Linea(V.P1, 1m));
        (await Cerrar().Handle(new CloseCashSessionCommand(V.Sesion.PublicId, [new CashCountInput(V.Efectivo.PublicId, CountedTotal: venta.AmountDue)]), default))
            .IsSuccess.Should().BeTrue();
        return venta.AmountDue;
    }

    [Fact]
    public async Task El_cierre_del_dia_registra_la_remesa_por_Tesoreria_en_su_transaccion()
    {
        var t = await CrearAsync();
        var efectivo = await t.DiaConEfectivoAsync();
        var cuenta = Guid.NewGuid();
        var clave = Guid.NewGuid();

        var r = await t.CerrarDia(t._remesas).Handle(new ExecuteDayCloseCommand(t.V.Punto.PublicId, Catalog.CatalogoDePrueba.Hoy)
        {
            OperationKey = clave, Remittance = new RemesaDelCierreDelDia(cuenta, efectivo, "SELLO-9"),
        }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        await t._remesas.Received(1).RegistrarAsync(Arg.Is<SolicitudDeRemesa>(s => s.IdempotencyKey == clave && s.OwnBankAccountPublicId == cuenta && s.Amount == efectivo
            && s.BagSeal == "SELLO-9" && s.SourceDocumentPublicId == r.Value.DayClosePublicId && s.VaultPublicId == t.V.Punto.PublicId), Arg.Any<CancellationToken>());
        t.Db.DayCloses.Single().RemittancePublicId.Should().NotBeNull();
    }

    [Fact]
    public async Task No_remesa_mas_que_lo_contado_en_efectivo()
    {
        var t = await CrearAsync();
        var efectivo = await t.DiaConEfectivoAsync();

        var r = await t.CerrarDia(t._remesas).Handle(new ExecuteDayCloseCommand(t.V.Punto.PublicId, Catalog.CatalogoDePrueba.Hoy)
        {
            OperationKey = Guid.NewGuid(), Remittance = new RemesaDelCierreDelDia(Guid.NewGuid(), efectivo + 1m),
        }, default);

        r.Error.Code.Should().Be(ErroresDeCaja.DayCloseRemittanceOverCashCode);
        await t._remesas.DidNotReceive().RegistrarAsync(Arg.Any<SolicitudDeRemesa>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_Tesoreria_o_con_su_rechazo_el_cierre_no_queda()
    {
        var t = await CrearAsync();
        var efectivo = await t.DiaConEfectivoAsync();
        var pedido = new RemesaDelCierreDelDia(Guid.NewGuid(), efectivo);

        var sinTesoreria = await t.CerrarDia(null).Handle(new ExecuteDayCloseCommand(t.V.Punto.PublicId, Catalog.CatalogoDePrueba.Hoy)
            { OperationKey = Guid.NewGuid(), Remittance = pedido }, default);
        t._remesas.RegistrarAsync(Arg.Any<SolicitudDeRemesa>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("La cuenta no está activa."));
        var rechazada = await t.CerrarDia(t._remesas).Handle(new ExecuteDayCloseCommand(t.V.Punto.PublicId, Catalog.CatalogoDePrueba.Hoy)
            { OperationKey = Guid.NewGuid(), Remittance = pedido }, default);

        sinTesoreria.Error.Code.Should().Be(ErroresDeCaja.DayCloseRemittanceUnavailableCode);
        rechazada.Error.Code.Should().Be(ErroresDeCaja.DayCloseRemittanceRejectedCode);
        t.Db.DayCloses.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_remesa_el_cierre_es_el_de_siempre()
    {
        var t = await CrearAsync();
        await t.DiaConEfectivoAsync();

        var r = await t.CerrarDia(t._remesas).Handle(new ExecuteDayCloseCommand(t.V.Punto.PublicId, Catalog.CatalogoDePrueba.Hoy) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue();
        await t._remesas.DidNotReceive().RegistrarAsync(Arg.Any<SolicitudDeRemesa>(), Arg.Any<CancellationToken>());
        t.Db.DayCloses.Single().RemittancePublicId.Should().BeNull();
    }
}
