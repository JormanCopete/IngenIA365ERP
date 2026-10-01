using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.ElectronicInvoicing;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// La cooperativa de ventas de prueba (<see cref="VentasDePrueba"/>) <b>obligada</b> a facturar y lista para emitir con el canal simulado
/// (feature 012, I4, T734–T739, T743): configuración vigente del canal <c>SIMULADO</c> en pruebas con la credencial verificada, la resolución de
/// factura <c>SETP</c> 1..100 asociada al canal, el tipo FV con ese prefijo, las unidades y los tributos con su código DIAN, y el flujo canónico
/// real con el paso fiscal (<see cref="EmisionFiscalDeLaConfirmacion"/>) y la entrega I4 en la guardia. (nuevo)
/// </summary>
public sealed class VentaElectronicaDePrueba
{
    public VentasDePrueba V { get; }
    public IngenIA365ERP.Application.Tests.Common.TestApplicationDbContext Db => V.Db;
    public TareasTrasElCommit Tareas { get; } = new();
    public RechazoFiscalEnCurso Rechazo { get; } = new();
    public TrasladoDeVentaEnCurso Traslado { get; } = new();
    public CanalesDePrueba Canales { get; } = new(new CanalDePrueba(GuardiaDeEmisionFiscal.CanalSimulado, CanalDePrueba.Completas()));
    public ElectronicEmissionSetting Configuracion { get; private set; } = null!;
    public DianNumberingResolution Resolucion { get; private set; } = null!;
    public static DateOnly Hoy => Catalog.CatalogoDePrueba.Hoy;

    private VentaElectronicaDePrueba(VentasDePrueba v) => V = v;

    public static async Task<VentaElectronicaDePrueba> CrearAsync() => await SobreAsync(await VentasDePrueba.CrearAsync());

    /// <summary>La misma preparación sobre una cooperativa de ventas ya creada (I6: el ciclo comercial la combina con el crédito).</summary>
    public static async Task<VentaElectronicaDePrueba> SobreAsync(VentasDePrueba v)
    {
        var e = new VentaElectronicaDePrueba(v);
        var db = e.Db;
        foreach (var p in db.ParameterVersions.Where(p => p.Key == ParametrosDeFacturacionElectronica.ObligadaAFacturar)) p.Value = "true";
        foreach (var u in db.UnitsOfMeasure) u.DianUnitCode ??= "94";
        foreach (var t in db.TaxDefinitions) t.DianTaxCode ??= "01";
        db.InventoryDocumentTypes.Single(t => t.Code == "FV").FiscalPrefix = "SETP";
        e.Configuracion = new ElectronicEmissionSetting
        {
            Mode = EmissionMode.TechnologyProvider, ChannelCode = GuardiaDeEmisionFiscal.CanalSimulado, Environment = DianEnvironment.Testing,
            CredentialKey = "k.SIMULADO.json", CredentialVerifiedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            IssuerTaxId = "900123456", IssuerCheckDigit = "7", IssuerBusinessName = "Cooperativa de prueba", IssuerAddress = "Calle 1",
            IssuerMunicipalityDaneCode = "76001", IssuerEmail = "facturas@coop.co", IsEnabled = true, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
        };
        db.ElectronicEmissionSettings.Add(e.Configuracion);
        e.Resolucion = e.NuevaResolucion(ResolutionKind.Invoice, "SETP");
        foreach (var persona in db.People) persona.IdType ??= "CC";
        await db.SaveChangesAsync();
        return e;
    }

    public DianNumberingResolution NuevaResolucion(ResolutionKind tipo, string prefijo, ResolutionKind? respalda = null, long desde = 1, long hasta = 100)
    {
        var r = new DianNumberingResolution
        {
            Kind = tipo, BacksUpKind = respalda, ResolutionNumber = $"18764{prefijo}{(int)tipo}", ResolutionDate = new DateOnly(2025, 12, 20),
            Prefix = prefijo, RangeTo = hasta, ValidFrom = new DateOnly(2026, 1, 1), ValidTo = new DateOnly(2026, 12, 31),
            Environment = DianEnvironment.Testing, IsActive = true,
        };
        r.RangeFrom = desde;
        r.Channels.Add(new DianResolutionChannel { Resolution = r, ChannelCode = GuardiaDeEmisionFiscal.CanalSimulado, ValidFrom = new DateOnly(2026, 1, 1) });
        Db.DianNumberingResolutions.Add(r);
        Db.SaveChanges();
        return r;
    }

    /// <summary>Una contingencia 03 abierta en el canal simulado.</summary>
    public DianContingencyEvent Contingencia03()
    {
        var e = new DianContingencyEvent
        {
            Type = ContingencyType.Issuer03, ChannelCode = GuardiaDeEmisionFiscal.CanalSimulado, StartedAt = new DateTime(2026, 9, 25, 13, 0, 0, DateTimeKind.Utc),
            DetectedByName = "Ana", Reason = "sin internet", Status = ContingencyEventStatus.Open,
        };
        Db.DianContingencyEvents.Add(e);
        Db.SaveChanges();
        return e;
    }

    // ------------------------------------------------------------------------------------------- servicios --

    public GuardiaDeEmisionFiscal Guardia() => new(V.K.Lector(), Db, Canales, EntregaDelComercio.I4);

    public ReglasDeConfirmacionDeVenta Reglas() => new(Db, V.K.Actor, V.Compras.C.Reloj, V.K.Lector(), V.K.Permisos, Guardia(), V.Calculo(),
        V.Aprobaciones(), V.Toque, traslado: Traslado);

    public FuenteDeEmisionDeInventario Fuente() => new(Db, V.K.Actor, V.Compras.C.Reloj, V.K.Lector(), V.Confirmacion(), Rechazo);

    public EmisionFiscalDeLaConfirmacion Fiscal { get; private set; } = null!;

    public EmisionFiscalDeLaConfirmacion NuevoPasoFiscal()
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton<IFuenteDeDocumentoElectronico>(_ => Fuente());
        Fiscal = new EmisionFiscalDeLaConfirmacion(Db, Guardia(), new NumeradorFiscal(Db, V.K.Cerrojo), new RegistroDeDocumentoElectronico(Db, V.Compras.C.Reloj),
            new ConstructorDelCanonico(V.K.Lector()), servicios.BuildServiceProvider(), Rechazo, Tareas);
        return Fiscal;
    }

    public ConfirmacionDeDocumento Confirmacion()
    {
        var reglas = Reglas();
        return new ConfirmacionDeDocumento(Db, V.K.Maestros(), V.K.Actor, V.Compras.C.Reloj, V.Efectos(reglas), V.K.Motor, V.K.Cerrojo,
            new Numerador(Db, V.K.Cerrojo), new EmisorDeMensajes(Db, V.K.Actor, V.Compras.C.Reloj), V.K.Lector(), V.K.Vista(), [NuevoPasoFiscal()], []);
    }

    public async Task<Result<ConfirmationResultDto>> ConfirmarAsync(Guid documento) =>
        await new ConfirmInventoryDocumentCommandHandler(Db, Confirmacion()).Handle(new ConfirmInventoryDocumentCommand(documento, DocumentClassGroup.Sales), default);

    /// <summary>Guarda una factura FV a Ana pagada en efectivo y la confirma por el flujo con el paso fiscal.</summary>
    public async Task<(Guid Venta, Result<ConfirmationResultDto> Confirmacion)> FacturaAsync(string tipo = "FV", decimal cantidad = 2m)
    {
        var borrador = await V.GuardarAsync(V.Venta(tipo, V.Ana, lineas: V.Linea(V.P1, cantidad)));
        if (borrador.IsFailure) throw new InvalidOperationException($"{borrador.Error.Code}: {borrador.Error.Message}");
        var total = V.Documento(borrador.Value.PublicId).AmountDue;
        var conPagos = await V.GuardarAsync(V.Venta(tipo, V.Ana, pagos: [V.Pago(V.Efectivo, total)], lineas: V.Linea(V.P1, cantidad)), borrador.Value.PublicId);
        if (conPagos.IsFailure) throw new InvalidOperationException($"{conPagos.Error.Code}: {conPagos.Error.Message}");
        return (borrador.Value.PublicId, await ConfirmarAsync(borrador.Value.PublicId));
    }

    /// <summary>El documento electrónico del comercial.</summary>
    public ElectronicDocument Electronico(Guid comercial) =>
        Db.ElectronicDocuments.Include(e => e.Versions).Single(e => e.SourceDocumentPublicId == comercial);

    /// <summary>Pone el documento electrónico del comercial en un estado (como si la emisión ya hubiera pasado).</summary>
    public void Estado(Guid comercial, ElectronicDocumentStatus estado, string? codigo = null)
    {
        var e = Electronico(comercial);
        e.Status = estado;
        e.UniqueCode = codigo;
        if (codigo is not null) e.UniqueCodeKind = UniqueCodeKind.Cufe;
        Db.SaveChanges();
    }
}
