using System.Globalization;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Columnas;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>Un cobro provisional válido: la fila del archivo, la factura, el valor, la fecha valor y la referencia contable. (nuevo)</summary>
public sealed record CobroProvisionalValidado(int Fila, string Factura, decimal Valor, DateOnly FechaValor, string ReferenciaContable);

/// <summary>Lo que dejó revisar el archivo de cobros provisionales: el resultado de la plantilla, los cobros válidos y la huella del archivo. (nuevo)</summary>
public sealed record CobrosProvisionalesRevisados(ImportResultDto Resultado, IReadOnlyList<CobroProvisionalValidado> Cobros, string Sha256)
{
    public bool HayErrores => Resultado.TotalErrors > 0;
}

/// <summary>
/// La validación de la plantilla <c>lending.provisional-collections</c> (feature 013, US8, T0748; plantillas.md §6; C62; data-model §29.5). La
/// usan la revisión previa (<see cref="ReviewProvisionalCollectionsCommand"/>) y la activación, que la vuelve a correr <b>dentro de su
/// transacción</b> sobre el adjunto revisado (todo o nada con la activación). Cada fila:
/// <list type="bullet">
/// <item>la factura es una venta a crédito con <c>VentaACreditoRegistrada</c> acumulado (sin consumir) <b>confirmada después del corte</b>; una venta
/// hasta el corte es de la migración y no va aquí (<c>Import.Cell.NotFound</c>);</item>
/// <item><c>fechaValor</c> entre la fecha de la venta y el registro de la activación (<c>Import.Cell.Format</c>);</item>
/// <item>llave <c>factura</c> + <c>referenciaContable</c> sin repetir (<c>Import.Row.Duplicate</c>);</item>
/// <item>la suma de los cobros de una factura no supera su valor a crédito: <c>Lending.ProvisionalCollection.ExceedsSale</c> en la columna
/// <c>valor</c> de la fila que la pasa.</item>
/// </list>
/// Lee las hojas con el mismo lector y las mismas reglas de celda que toda plantilla (<see cref="EjecutorDeImportacion"/>), pero no audita ni
/// guarda nada: la revisión audita en su comando y la activación en el suyo. (nuevo)
/// </summary>
public sealed class ValidacionDeCobrosProvisionales(
    IApplicationDbContext db,
    ITabularFileReader lector,
    ICurrentUserPermissions permisos,
    IMensajesEntrantes entrantes,
    IBlobStore? almacen = null)
{
    /// <summary>Los tipos de mensaje que se leen: sólo la venta (sus ajustes no cambian el valor a crédito revisado).</summary>
    private static readonly IReadOnlyList<string> Tipos = [VentaACreditoRegistradaV1.Type];

    /// <summary>Valida el archivo contra las ventas confirmadas después de <paramref name="corte"/>, con el registro de la activación en <paramref name="hoy"/>.</summary>
    public async Task<Result<CobrosProvisionalesRevisados>> ValidarAsync(ArchivoDeImportacion archivo, DateOnly corte, DateOnly hoy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(archivo);
        if (archivo.Contenido is not { Length: > 0 }) return Result.Failure<CobrosProvisionalesRevisados>(ArchivosTabulares.Vacio);
        if (archivo.Contenido.Length > EjecutorDeImportacion.MaximoDeBytes)
            return Result.Failure<CobrosProvisionalesRevisados>(ArchivosTabulares.Ilegible(
                string.Create(CultureInfo.InvariantCulture, $"el archivo pesa más de {EjecutorDeImportacion.MaximoDeBytes / (1024 * 1024)} MB.")));

        var concedidos = permisos.EsMaestroGlobal
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(await permisos.ListAsync(ct), StringComparer.OrdinalIgnoreCase);
        var preparado = await EjecutorDeImportacion.PrepararContextoAsync(lector, DefinicionesDeMigracion.CobrosProvisionales, ModoDeImportacion.Review,
            archivo, null, concedidos, permisos.EsMaestroGlobal, ct);
        if (preparado.IsFailure) return Result.Failure<CobrosProvisionalesRevisados>(preparado.Error);
        var ctx = preparado.Value;

        var ventas = await VentasDespuesDelCorteAsync(corte, ct);
        if (ventas.IsFailure) return Result.Failure<CobrosProvisionalesRevisados>(ventas.Error);

        var cobros = new List<CobroProvisionalValidado>();
        var sumas = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var fila in ctx.Datos.Filas)
        {
            fila.Requerido(C.Factura);
            fila.Requerido(C.Valor);
            fila.Requerido(C.FechaValor);
            fila.Requerido(C.ReferenciaContable);
            var factura = fila.Texto(C.Factura, 40);
            var valor = fila.Monto(C.Valor);
            var fecha = fila.Fecha(C.FechaValor);
            var referencia = fila.Texto(C.ReferenciaContable, 60);
            fila.Texto(C.Observaciones, 400);
            if (valor is <= 0m) fila.Error(C.Valor, ImportErrors.CellFormat, "El valor del cobro es mayor que cero.");

            var llave = $"{factura?.ToUpperInvariant()}|{referencia?.ToUpperInvariant()}";
            if (!ctx.Datos.LlaveUnica(fila, llave, C.ReferenciaContable) || factura is null || valor is null || fecha is null || referencia is null) continue;

            if (!ventas.Value.TryGetValue(factura, out var venta))
            {
                fila.Error(C.Factura, ImportErrors.CellNotFound,
                    $"La factura «{factura}» no es una venta a crédito confirmada después del corte que espere a Cartera. Las ventas hasta el corte van en la carga de migración.");
                continue;
            }
            if (fecha < venta.Fecha || fecha > hoy)
            {
                fila.Error(C.FechaValor, ImportErrors.CellFormat, string.Create(CultureInfo.InvariantCulture,
                    $"La fecha valor va entre la fecha de la venta ({venta.Fecha:yyyy-MM-dd}) y la del registro de la activación ({hoy:yyyy-MM-dd})."));
                continue;
            }
            if (fila.TieneErrores) continue;

            var suma = sumas.GetValueOrDefault(venta.Factura) + valor.Value;
            if (suma > venta.Valor)
            {
                fila.Error(C.Valor, ErroresDeMigracion.CodigoCobrosExcedenLaVenta, ErroresDeMigracion.CobrosExcedenLaVenta(venta.Factura, suma, venta.Valor).Message);
                continue;
            }
            sumas[venta.Factura] = suma;
            cobros.Add(new CobroProvisionalValidado(fila.Numero, venta.Factura, valor.Value, fecha.Value, referencia));
            ctx.Registrar(fila, llave, AccionDeImportacion.Create);
        }

        var resultado = ctx.Resultado(archivo, aplicado: false);
        return Result.Success(new CobrosProvisionalesRevisados(resultado, resultado.TotalErrors > 0 ? [] : cobros, archivo.Sha256));
    }

    /// <summary>
    /// El error de un archivo con errores: si todos son de cobros que superan la venta, <c>Lending.ProvisionalCollection.ExceedsSale</c> (el primero);
    /// si no, <c>Import.Invalid</c> con el resultado en <c>data</c>.
    /// </summary>
    public static Error Error(CobrosProvisionalesRevisados revisados)
    {
        ArgumentNullException.ThrowIfNull(revisados);
        var errores = revisados.Resultado.Errors;
        return errores.Count > 0 && errores.All(e => e.Code == ErroresDeMigracion.CodigoCobrosExcedenLaVenta)
            ? new ErrorConDatos(ErroresDeMigracion.CodigoCobrosExcedenLaVenta, errores[0].Message, revisados.Resultado)
            : ImportErrors.Invalid(revisados.Resultado);
    }

    /// <summary>
    /// El archivo revisado que se guardó como adjunto (<see cref="AdjuntosDeCobros"/>), con su nombre y su contenido. Falla si el adjunto no existe
    /// o no es de una revisión de cobros provisionales.
    /// </summary>
    public async Task<Result<ArchivoDeImportacion>> LeerAdjuntoAsync(Guid adjunto, CancellationToken ct)
    {
        var fila = await db.Attachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PublicId == adjunto && a.OwnerEntityType == AdjuntosDeCobros && !a.IsDeleted, ct);
        if (fila is null || almacen is null)
            return Result.Failure<ArchivoDeImportacion>(new ErrorConDatos(ImportErrors.CellNotFound,
                "El archivo de cobros provisionales revisado no existe. Vuelva a revisarlo y active con el adjunto que devuelve la revisión.",
                new { provisionalCollectionsAttachmentPublicId = adjunto }));
        await using var flujo = await almacen.GetAsync(new BlobReference(fila.StoragePath), ct);
        using var copia = new MemoryStream();
        await flujo.CopyToAsync(copia, ct);
        return Result.Success(new ArchivoDeImportacion(fila.FileName, copia.ToArray()));
    }

    /// <summary>El dueño del adjunto de una revisión de cobros provisionales (<c>Attachments.Common.AdjuntosDeModulo.CobrosProvisionales</c>).</summary>
    public const string AdjuntosDeCobros = Attachments.Common.AdjuntosDeModulo.CobrosProvisionales;

    private sealed record VentaPosterior(string Factura, DateOnly Fecha, decimal Valor);

    /// <summary>Las ventas a crédito acumuladas sin consumir con fecha posterior al corte, por factura (sin consumir ni intentar nada).</summary>
    private async Task<Result<Dictionary<string, VentaPosterior>>> VentasDespuesDelCorteAsync(DateOnly corte, CancellationToken ct)
    {
        var ventas = new Dictionary<string, VentaPosterior>(StringComparer.OrdinalIgnoreCase);
        var leidas = 0;
        for (var pagina = 1; ; pagina++)
        {
            var r = await entrantes.PendientesAsync(IntegrationDestinations.Lending, Tipos, DateOnly.MaxValue, new PageRequest(pagina, PageRequest.MaxPageSize), ct);
            if (r.IsFailure) return Result.Failure<Dictionary<string, VentaPosterior>>(r.Error);
            leidas += r.Value.Items.Count;
            foreach (var m in r.Value.Items)
                if (VentaRecibida.De(m) is { } venta && venta.Fecha > corte)
                    ventas.TryAdd(venta.NumeroDeFactura, new VentaPosterior(venta.NumeroDeFactura, venta.Fecha, venta.Pago.Amount));
            if (r.Value.Items.Count < PageRequest.MaxPageSize || leidas >= r.Value.TotalCount) break;
        }
        return Result.Success(ventas);
    }
}
