using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Inventory.Imports.PlantillaDePuntosDeVenta;

namespace IngenIA365ERP.Application.Inventory.Imports;

/// <summary>
/// Plantilla 10 — puntos de venta y cajas (feature 012, I3, T595; contracts/plantillas.md §10). Escribe <c>INV_PointsOfSale</c>,
/// <c>INV_CashRegisters</c> e <c>INV_CashRegisterDocumentTypes</c>. Reemplaza la definición que I1 publicaba sólo para descargar
/// (la que retiró T600). Las seis columnas de tipo de la hoja Cajas son los seis roles de
/// <see cref="CashRegisterDocumentRole"/>. (nuevo)
/// </summary>
public static class PlantillaDePuntosDeVenta
{
    public const string Clave = CatalogoDePlantillas.PuntosDeVentaClave;
    public const string HojaPuntos = "PuntosDeVenta";
    public const string HojaCajas = "Cajas";

    public const string Codigo = "codigo";
    public const string Nombre = "nombre";
    public const string Sucursal = "sucursal";
    public const string Canal = "canal";
    public const string PosHabilitado = "posHabilitado";
    public const string BodegaPorDefecto = "bodegaPorDefecto";
    public const string Activo = "activo";

    public const string Punto = "punto";
    public const string Bodega = "bodega";
    public const string TipoVentaPos = "tipoVentaPos";
    public const string TipoFactura = "tipoFactura";
    public const string TipoNotaVentaPos = "tipoNotaVentaPos";
    public const string TipoNotaCreditoFactura = "tipoNotaCreditoFactura";
    public const string TipoContingenciaVentaPos = "tipoContingenciaVentaPos";
    public const string TipoContingenciaFactura = "tipoContingenciaFactura";
    public const string Impresion = "impresion";
    public const string Activa = "activa";

    /// <summary>Cada columna de tipo con su rol (contracts/plantillas.md §10).</summary>
    public static IReadOnlyList<(string Columna, CashRegisterDocumentRole Rol)> ColumnasDeTipo { get; } =
    [
        (TipoVentaPos, CashRegisterDocumentRole.PosSale),
        (TipoFactura, CashRegisterDocumentRole.InvoiceOnRequest),
        (TipoNotaVentaPos, CashRegisterDocumentRole.PosAdjustmentNote),
        (TipoNotaCreditoFactura, CashRegisterDocumentRole.InvoiceCreditNote),
        (TipoContingenciaVentaPos, CashRegisterDocumentRole.PosSaleContingency),
        (TipoContingenciaFactura, CashRegisterDocumentRole.InvoiceContingency),
    ];

    /// <summary>Las etiquetas en español de la columna <c>impresion</c>.</summary>
    public static IReadOnlyDictionary<string, CashRegisterPrintFormat> EtiquetasDeImpresion { get; } = new Dictionary<string, CashRegisterPrintFormat>
    {
        ["Tirilla80"] = CashRegisterPrintFormat.Ticket80,
        ["Tirilla58"] = CashRegisterPrintFormat.Ticket58,
        ["Carta"] = CashRegisterPrintFormat.Letter,
    };

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Puntos de venta y cajas", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla(HojaPuntos,
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "llave; no cambia nunca (la matriz contable lo usa)", Ejemplo: "PV01"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: ReglasDePuntoDeVenta.LargoDeNombreDePunto, Ejemplo: "ALMACÉN FLORIDA"),
            new(Sucursal, TipoDeValor.Sucursal, Obligatoria: true, Reglas: "código o nombre de la sucursal contable; no cambia", Ejemplo: "01"),
            new(Canal, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "el punto fija el canal", Ejemplo: "MOSTRADOR"),
            new(PosHabilitado, TipoDeValor.SiNo, Reglas: "vacío = sí; el POS es opcional por punto", Ejemplo: "sí"),
            new(BodegaPorDefecto, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "operativa y de la misma sucursal", Ejemplo: "PV01"),
            new(Activo, TipoDeValor.SiNo, Reglas: "vacío = sí; un punto con sesiones abiertas no se inactiva", Ejemplo: "sí"),
        ]),
        new HojaDePlantilla(HojaCajas,
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "llave, única en la cooperativa", Ejemplo: "CJ01"),
            new(Punto, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "no cambia si la caja tuvo sesiones", Ejemplo: "PV01"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: ReglasDePuntoDeVenta.LargoDeNombreDeCaja, Ejemplo: "CAJA 1"),
            new(Bodega, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "operativa, de la sucursal del punto", Ejemplo: "PV01"),
            new(TipoVentaPos, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto,
                Reglas: "PosEquivalentDocument si la cooperativa está obligada a facturar electrónicamente; NonElectronicSalesReceipt si no", Ejemplo: "POS"),
            new(TipoFactura, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "SalesInvoice, para cuando el comprador pide factura", Ejemplo: "FV"),
            new(TipoNotaVentaPos, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "PosAdjustmentNote (obligada) o NonElectronicSalesNote (no obligada)", Ejemplo: "NAPOS"),
            new(TipoNotaCreditoFactura, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "CreditNote; obligatoria con tipoFactura", Ejemplo: "NC"),
            new(TipoContingenciaVentaPos, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "PosEquivalentDocument con resolución de contingencia", Ejemplo: "POSC"),
            new(TipoContingenciaFactura, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "SalesInvoice con resolución de contingencia", Ejemplo: "FVC"),
            new(Impresion, TipoDeValor.Enumeracion, Reglas: "Tirilla80, Tirilla58, Carta; vacío = Tirilla80", Ejemplo: "Tirilla80"),
            new(Activa, TipoDeValor.SiNo, Reglas: "vacío = sí", Ejemplo: "sí"),
        ], Obligatoria: false),
    ]);
}

/// <summary>
/// La plantilla 10 (feature 012, I3, T595; contracts/plantillas.md §10, §0.5; <c>POST /api/inventory/points-of-sale/import</c>):
/// puntos y cajas con <b>las mismas reglas que el alta</b> (<see cref="ReglasDePuntoDeVenta"/>), todo o nada. El código es la
/// llave: si existe, actualiza. Un punto no cambia de sucursal; una caja no cambia de punto si tuvo sesiones. (nuevo)
/// </summary>
public sealed record ImportPointsOfSaleCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportPointsOfSaleCommandValidator : AbstractValidator<ImportPointsOfSaleCommand>
{
    public ImportPointsOfSaleCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportPointsOfSaleCommand>.LargoMaximo);
    }
}

public sealed class ImportPointsOfSaleCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, ILectorDeParametros parametros, IDateTimeService reloj)
    : IRequestHandler<ImportPointsOfSaleCommand, Result<ImportResultDto>>
{
    public Task<Result<ImportResultDto>> Handle(ImportPointsOfSaleCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var sucursales = CatalogoCitado<Branch>.Desde(await db.Branches.Where(b => !b.IsDeleted).ToListAsync(ct), b => b, b => b.LegacyCode, b => b.Name);
        var canales = CatalogoCitado<SalesChannel>.Desde(await db.SalesChannels.Where(c => !c.IsDeleted).ToListAsync(ct), c => c, c => c.Code);
        var bodegas = CatalogoCitado<Warehouse>.Desde(await db.Warehouses.Where(w => !w.IsDeleted).ToListAsync(ct), w => w, w => w.Code);
        var puntos = (await db.PointsOfSale.Where(p => !p.IsDeleted).ToListAsync(ct)).ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

        // ---------------------------------------------------------------------------------------- PuntosDeVenta --
        var hoja = ctx.Hoja(P.HojaPuntos);
        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(P.Codigo);
            var nombre = fila.Texto(P.Nombre);
            var sucursal = fila.Referencia(P.Sucursal, sucursales, "una sucursal", "en Maestros → Agencias");
            var canal = fila.Referencia(P.Canal, canales, "un canal de venta", "en Ventas → Canales");
            var pos = fila.SiNo(P.PosHabilitado, porDefecto: true);
            var bodega = fila.Referencia(P.BodegaPorDefecto, bodegas, "una bodega", "en la plantilla de bodegas o en Inventario → Bodegas");
            var activo = fila.SiNo(P.Activo, porDefecto: true);
            if (!hoja.LlaveUnica(fila, codigo, P.Codigo) || codigo is null || nombre is null || sucursal is null || canal is null || bodega is null || fila.TieneErrores) continue;

            var datos = new DatosDePunto(codigo, nombre, sucursal, canal, pos, bodega, null, activo);
            if (!puntos.TryGetValue(codigo, out var punto))
            {
                var alta = await ReglasDePuntoDeVenta.AltaDePuntoAsync(db, datos, ct);
                if (alta.IsFailure)
                {
                    Error(fila, alta.Error);
                    continue;
                }
                puntos[codigo] = alta.Value;
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create,
                    [new(P.Nombre, null, nombre), new(P.Sucursal, null, sucursal.LegacyCode ?? sucursal.Name), new(P.Canal, null, canal.Code), new(P.BodegaPorDefecto, null, bodega.Code)]);
                continue;
            }

            if (punto.BranchId != sucursal.Id)
            {
                fila.Error(P.Sucursal, ImportErrors.CellFormat, $"El punto {punto.Code} es de otra sucursal: la sucursal de un punto no cambia.");
                continue;
            }
            var campos = new List<CampoCambiadoDto>();
            Diferencia(campos, P.Nombre, punto.Name, nombre);
            Diferencia(campos, P.Canal, canales.Buscar(punto.SalesChannelId, db), canal.Code);
            Diferencia(campos, P.PosHabilitado, SiNo(punto.PosEnabled), SiNo(pos));
            Diferencia(campos, P.BodegaPorDefecto, bodegas.Buscar(punto.DefaultWarehouseId, db), bodega.Code);
            Diferencia(campos, P.Activo, SiNo(punto.IsActive), SiNo(activo));
            var r = await ReglasDePuntoDeVenta.ActualizarPuntoAsync(db, punto, datos with { Address = punto.Address }, ct);
            if (r.IsFailure)
            {
                Error(fila, r.Error);
                continue;
            }
            ctx.Registrar(fila, codigo, campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
        }

        await CajasAsync(ctx, puntos, bodegas, ct);
    }

    private async Task CajasAsync(ContextoDeImportacion ctx, Dictionary<string, PointOfSale> puntos, CatalogoCitado<Warehouse> bodegas, CancellationToken ct)
    {
        var hoja = ctx.Hoja(P.HojaCajas);
        if (hoja.Filas.Count == 0) return;

        var tipos = CatalogoCitado<InventoryDocumentType>.Desde(
            await db.InventoryDocumentTypes.Include(t => t.Warehouses).Where(t => !t.IsDeleted).ToListAsync(ct), t => t, t => t.Code);
        var cajas = (await db.CashRegisters.Where(c => !c.IsDeleted).ToListAsync(ct)).ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var codigosDeTipo = (await db.CashRegisterDocumentTypes.AsNoTracking().Where(t => !t.IsDeleted)
                .Join(db.InventoryDocumentTypes.AsNoTracking(), x => x.DocumentTypeId, t => t.Id, (x, t) => new { x.CashRegisterId, x.Role, t.Code })
                .ToListAsync(ct))
            .ToDictionary(x => (x.CashRegisterId, x.Role), x => x.Code);
        var obligada = await ReglasDePuntoDeVenta.ObligadaAFacturarAsync(parametros, reloj.HoyLocal, ct);
        var ahora = reloj.UtcNow;

        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(P.Codigo);
            var codigoPunto = fila.Codigo(P.Punto);
            var nombre = fila.Texto(P.Nombre);
            var bodega = fila.Referencia(P.Bodega, bodegas, "una bodega", "en la plantilla de bodegas o en Inventario → Bodegas");
            var pedidos = new List<(CashRegisterDocumentRole, InventoryDocumentType)>();
            foreach (var (columna, rol) in P.ColumnasDeTipo)
            {
                var tipo = fila.Referencia(columna, tipos, "un tipo de documento", "en la plantilla de tipos de documento o en Inventario → Tipos de documento");
                if (tipo is not null) pedidos.Add((rol, tipo));
            }
            var formato = fila.Enumeracion(P.Impresion, P.EtiquetasDeImpresion) ?? CashRegisterPrintFormat.Ticket80;
            var activa = fila.SiNo(P.Activa, porDefecto: true);
            if (!hoja.LlaveUnica(fila, codigo, P.Codigo) || codigo is null || codigoPunto is null || nombre is null || bodega is null || fila.TieneErrores) continue;
            if (!puntos.TryGetValue(codigoPunto, out var punto))
            {
                fila.Error(P.Punto, ImportErrors.CellNotFound, $"No hay un punto de venta «{codigoPunto}». Créelo en la hoja PuntosDeVenta de este archivo o en Ventas → Puntos de venta.");
                continue;
            }

            cajas.TryGetValue(codigo, out var existente);
            if (existente is not null && existente.PointOfSaleId != punto.Id)
            {
                var sesiones = await db.CashSessions.CountAsync(s => s.CashRegisterId == existente.Id && !s.IsDeleted, ct);
                if (sesiones > 0)
                {
                    fila.Error(P.Punto, ImportErrors.CellFormat, $"La caja {existente.Code} ya tuvo sesiones: no cambia de punto.");
                    continue;
                }
            }

            var antes = existente is null ? null : new
            {
                existente.Name,
                Punto = puntos.Values.FirstOrDefault(p => p.Id == existente.PointOfSaleId)?.Code,
                Bodega = bodegas.Buscar(existente.WarehouseId, db),
                Formato = ReglasDePuntoDeVenta.FormatoDe(existente.ReceiptWidthMm),
                existente.IsActive,
            };
            // La plantilla 10 no trae el datáfono (lo pone la 11 con cajaPorDefecto): se conserva el que la caja tenga.
            var datafono = existente?.DefaultCardTerminalId is { } dt
                ? await db.CardTerminals.FirstOrDefaultAsync(t => t.Id == dt, ct)
                : null;
            var r = await ReglasDePuntoDeVenta.AplicarCajaAsync(db, punto, existente,
                new DatosDeCaja(codigo, nombre, bodega, datafono, formato, pedidos, existente?.DianCashRegisterPlate, null, activa, existente?.DianCashRegisterTypeCode), obligada, ahora, ct);
            if (r.IsFailure)
            {
                Error(fila, r.Error);
                continue;
            }
            cajas[codigo] = r.Value;

            if (antes is null)
            {
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create,
                    [new(P.Punto, null, punto.Code), new(P.Nombre, null, nombre), new(P.Bodega, null, bodega.Code)]);
                continue;
            }
            var campos = new List<CampoCambiadoDto>();
            Diferencia(campos, P.Nombre, antes.Name, nombre);
            Diferencia(campos, P.Punto, antes.Punto, punto.Code);
            Diferencia(campos, P.Bodega, antes.Bodega, bodega.Code);
            foreach (var (columna, rol) in P.ColumnasDeTipo)
                Diferencia(campos, columna, codigosDeTipo.GetValueOrDefault((existente!.Id, rol)), pedidos.FirstOrDefault(p => p.Item1 == rol).Item2?.Code);
            Diferencia(campos, P.Impresion, antes.Formato.ToString(), formato.ToString());
            Diferencia(campos, P.Activa, SiNo(antes.IsActive), SiNo(activa));
            ctx.Registrar(fila, codigo, campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
        }
    }

    /// <summary>La columna de un error de regla: el mismo código que el alta una a una, en la columna que lo causó.</summary>
    private static void Error(FilaDeImportacion fila, Error error)
    {
        var columna = error.Code switch
        {
            "Catalogo.CodigoDuplicado" => P.Codigo,
            ErroresDePuntoDeVenta.PointWarehouseBranchMismatchCode => P.BodegaPorDefecto,
            ErroresDePuntoDeVenta.HasOpenSessionsCode => P.Activo,
            ErroresDePuntoDeVenta.TransitWarehouseCode => fila.Hoja.Nombre == P.HojaPuntos ? P.BodegaPorDefecto : P.Bodega,
            ErroresDePuntoDeVenta.WarehouseBranchMismatchCode => P.Bodega,
            ErroresDePuntoDeVenta.RoleClassMismatchCode or ErroresDePuntoDeVenta.RoleDuplicateCode or ErroresDePuntoDeVenta.RoleRequiredCode
                => ColumnaDelRol(error),
            "Inventory.Document.WarehouseNotAllowedForType" => P.Bodega,
            _ => P.Codigo,
        };
        fila.Error(columna, error.Code, error.Message);
    }

    private static string ColumnaDelRol(Error error)
    {
        var rol = error is ErrorConDatos { Data: { } data } ? data.GetType().GetProperty("role")?.GetValue(data) as CashRegisterDocumentRole? : null;
        return P.ColumnasDeTipo.FirstOrDefault(c => c.Rol == rol).Columna ?? P.TipoVentaPos;
    }

    private static void Diferencia(List<CampoCambiadoDto> campos, string columna, string? antes, string? despues)
    {
        if (!string.Equals(antes ?? string.Empty, despues ?? string.Empty, StringComparison.Ordinal)) campos.Add(new(columna, antes, despues));
    }

    private static string SiNo(bool valor) => valor ? "sí" : "no";
}

/// <summary>Busca el código de un elemento ya cargado por su Id (para el diff de la revisión). (nuevo)</summary>
internal static class BusquedaPorId
{
    public static string? Buscar(this CatalogoCitado<SalesChannel> _, int id, IApplicationDbContext db) =>
        db.SalesChannels.Local.FirstOrDefault(c => c.Id == id)?.Code;

    public static string? Buscar(this CatalogoCitado<Warehouse> _, int id, IApplicationDbContext db) =>
        db.Warehouses.Local.FirstOrDefault(w => w.Id == id)?.Code;
}
