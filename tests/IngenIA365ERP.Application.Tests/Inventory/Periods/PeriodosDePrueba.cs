using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;

namespace IngenIA365ERP.Application.Tests.Inventory.Periods;

/// <summary>
/// La cooperativa de prueba de US3 (feature 012, T275–T277): la de <see cref="KardexDePrueba"/> con <c>INV_Setup</c> desde julio
/// de 2026 y los servicios reales de períodos, valorizado y reclasificación. Hoy es el 25 de septiembre de 2026, así que julio y
/// agosto ya terminaron y septiembre no. (nuevo)
/// </summary>
public sealed class PeriodosDePrueba
{
    public static readonly DateOnly Inicio = new(2026, 7, 1);

    public KardexDePrueba K { get; }

    public InventorySetup Setup { get; }

    private PeriodosDePrueba(KardexDePrueba k, InventorySetup setup)
    {
        K = k;
        Setup = setup;
    }

    public static async Task<PeriodosDePrueba> CrearAsync(DateOnly? ultimoCierre = null)
    {
        var k = await KardexDePrueba.CrearAsync();
        var setup = new InventorySetup { StartDate = Inicio, LastClosedDate = ultimoCierre, StartedAt = DateTime.UtcNow, StartedByUserId = KardexDePrueba.Usuario };
        k.C.Db.InventorySetups.Add(setup);
        await k.C.Db.SaveChangesAsync();
        return new PeriodosDePrueba(k, setup);
    }

    public ValorizadoALaFecha Valorizado() => new(K.C.Db, K.Lector());

    public EmisorDeMensajes Emisor() => new(K.C.Db, K.Actor, K.C.Reloj);

    public Task<Result<ClosePeriodResultDto>> CerrarAsync(int anio, int mes, bool reconocer = true) =>
        new CloseInventoryPeriodCommandHandler(K.C.Db, new RevisionDeCierre(K.C.Db, K.C.Reloj), Valorizado(), K.Cerrojo, Emisor(), K.Actor, K.Permisos, K.C.Reloj)
            .Handle(new CloseInventoryPeriodCommand(anio, mes, reconocer), default);

    public Task<Result<ReopenPeriodResultDto>> ReabrirAsync(int anio, int mes, string motivo = "Ajuste de costo tardío") =>
        new ReopenInventoryPeriodCommandHandler(K.C.Db, K.Cerrojo, Emisor(), K.Actor, K.C.Reloj)
            .Handle(new ReopenInventoryPeriodCommand(anio, mes, motivo), default);

    public Task<Result<PeriodCloseCheckDto>> RevisarAsync(int anio, int mes) =>
        new GetPeriodCloseCheckQueryHandler(new RevisionDeCierre(K.C.Db, K.C.Reloj)).Handle(new GetPeriodCloseCheckQuery(anio, mes), default);

    public ReclasificacionDeGrupo Reclasificacion() => new(K.C.Db, K.Cerrojo, Valorizado(), Emisor(), K.Lector(), K.C.Reloj);

    public Task<Result<AccountingGroupChangeResultDto>> ReclasificarAsync(Guid producto, Guid grupo, DateOnly? fecha = null, string motivo = "Cambio de línea") =>
        new ChangeProductAccountingGroupCommandHandler(K.C.Db, Reclasificacion(), K.Permisos)
            .Handle(new ChangeProductAccountingGroupCommand(producto, grupo, fecha, motivo), default);

    public Task<Result<Application.Common.Reports.TablaExportable>> ValorizadoAsync(DateOnly? fecha = null, Warehouse? bodega = null, bool conTransito = false,
        Guid? grupo = null) =>
        new ValuationReportQueryHandler(K.C.Db, K.Alcance, K.Permisos, Valorizado(), K.C.Reloj)
            .Handle(new ValuationReportQuery(new FiltrosDeInformeDeInventario { AsOf = fecha, Warehouse = bodega?.PublicId, AccountingGroup = grupo }, conTransito), default);

    /// <summary>Un ajuste confirmado con fecha; falla la prueba si no se confirma.</summary>
    public async Task<Guid> AjusteAsync(string tipo, DateOnly fecha, Guid producto, decimal cantidad, decimal? costo = null, Warehouse? bodega = null)
    {
        var (documento, r) = await K.AjusteAsync(K.Borrador(tipo, bodega, causa: tipo == "AJN" ? K.Causa() : null, fecha: fecha, lineas: [K.Linea(producto, cantidad, costo)]));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return documento;
    }
}
