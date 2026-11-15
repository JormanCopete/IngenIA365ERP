using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Transport.Services;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Transport.Imports.PlantillaDeSuscripciones;

namespace IngenIA365ERP.Application.Transport.Imports;

/// <summary>
/// La plantilla <c>transport.subscriptions</c> (feature 020, R8, T107; FR-025): suscribe los vehículos del parque a los servicios, con su
/// vigencia y, si se pactó, su precio. La llave es placa y código de producto; pasa por <see cref="SuscripcionDeServicio"/>, el mismo
/// camino de la pantalla. Una suscripción vigente que ya existe se conserva o, con «actualizar», se le pacta el precio de la fila.
/// </summary>
public static class PlantillaDeSuscripciones
{
    public const string Clave = "transport.subscriptions";

    public const string PlacaCol = "placa";
    public const string Producto = "producto";
    public const string Desde = "desde";
    public const string Hasta = "hasta";
    public const string Precio = "precioPactado";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Suscripciones a servicios", ModuloDeAuditoria.Transport,
    [
        new HojaDePlantilla("Datos",
        [
            new(PlacaCol, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "llave; placa de un vehículo vivo", Ejemplo: "AAA111"),
            new(Producto, TipoDeValor.Codigo, Obligatoria: true, Largo: 30, Reglas: "llave; código del producto de Comercial configurado como servicio", Ejemplo: "RADIO"),
            new(Desde, TipoDeValor.Fecha, Obligatoria: true, Ejemplo: "2026-12-01"),
            new(Hasta, TipoDeValor.Fecha, Reglas: "vacía = sin fin"),
            new(Precio, TipoDeValor.Monto, Reglas: "precio mensual pactado, impuestos incluidos; vacío = el de la lista de Comercial"),
        ]),
    ]);
}

public sealed record ImportSubscriptionsCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string? Reason = null, SiYaExiste Existentes = SiYaExiste.Actualizar)
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportSubscriptionsCommandValidator : AbstractValidator<ImportSubscriptionsCommand>
{
    public ImportSubscriptionsCommandValidator() => RuleFor(x => x.File).NotNull();
}

public sealed class ImportSubscriptionsCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, SuscripcionDeServicio suscripciones)
    : IRequestHandler<ImportSubscriptionsCommand, Result<ImportResultDto>>
{
    public Task<Result<ImportResultDto>> Handle(ImportSubscriptionsCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, (ctx, c) => ProcesarAsync(ctx, request.Existentes == SiYaExiste.Conservar, c), ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, bool conservar, CancellationToken ct)
    {
        var filas = ctx.Datos.Filas;
        var placas = filas.Select(f => Placa.Normalizar(f.Texto(P.PlacaCol))).Where(p => p.Length > 0).Distinct().ToList();
        var vehiculos = placas.Count == 0
            ? new Dictionary<string, Vehicle>()
            : (await db.Vehicles.Where(v => placas.Contains(v.Plate) && v.Status != VehicleStatus.Retired).ToListAsync(ct)).ToDictionary(v => v.Plate);
        var servicios = (await db.TransportServices.Where(s => s.IsActive).ToListAsync(ct)).ToDictionary(s => s.ProductCode, StringComparer.OrdinalIgnoreCase);
        var ids = vehiculos.Values.Select(v => v.Id).ToList();
        var existentes = ids.Count == 0 ? [] : await db.VehicleSubscriptions.Include(s => s.Prices).Where(s => ids.Contains(s.VehicleId)).ToListAsync(ct);

        foreach (var fila in filas)
        {
            var placa = Placa.Normalizar(fila.Texto(P.PlacaCol));
            var producto = fila.Codigo(P.Producto, 30);
            var desde = fila.Fecha(P.Desde);
            var hasta = fila.Fecha(P.Hasta);
            var precio = fila.Monto(P.Precio);
            if (placa.Length == 0) fila.Requerido(P.PlacaCol);
            fila.Requerido(P.Producto);
            fila.Requerido(P.Desde);
            if (fila.TieneErrores || producto is null || desde is null) continue;
            if (!ctx.Datos.LlaveUnica(fila, $"{placa}|{producto}", P.Producto)) continue;

            if (!vehiculos.TryGetValue(placa, out var v)) { fila.Error(P.PlacaCol, ImportErrors.CellNotFound, $"No hay un vehículo vivo con la placa {placa}."); continue; }
            if (!servicios.TryGetValue(producto, out var s))
            {
                fila.Error(P.Producto, ImportErrors.CellNotFound, $"El producto {producto} no está configurado como servicio de Transporte activo.");
                continue;
            }

            var vigente = existentes.FirstOrDefault(x => x.VehicleId == v.Id && x.ServiceId == s.Id && !x.IsDeleted && x.SeCruzaCon(desde.Value, hasta));
            if (vigente is not null)
            {
                if (conservar || precio is null) { ctx.Registrar(fila, $"{placa}|{producto}", AccionDeImportacion.Unchanged); continue; }
                var actual = vigente.Prices.Where(p => !p.IsDeleted && p.VigenteEn(desde.Value)).Select(p => (decimal?)p.Price).FirstOrDefault();
                if (actual == precio) { ctx.Registrar(fila, $"{placa}|{producto}", AccionDeImportacion.Unchanged); continue; }
                var desdeDelPrecio = desde.Value < vigente.From ? vigente.From : desde.Value;
                var r = suscripciones.PonerPrecio(vigente, precio.Value, desdeDelPrecio);
                if (r.IsFailure) { fila.Error(P.Precio, r.Error.Code, r.Error.Message); continue; }
                ctx.Registrar(fila, $"{placa}|{producto}", AccionDeImportacion.Update, [new CampoCambiadoDto(P.Precio, actual?.ToString(), precio.Value.ToString())]);
                continue;
            }

            var alta = await suscripciones.SuscribirAsync(v, s, desde.Value, hasta, precio, null, ct);
            if (alta.IsFailure) { fila.Error(P.Desde, alta.Error.Code, alta.Error.Message); continue; }
            existentes.Add(alta.Value);
            ctx.Registrar(fila, $"{placa}|{producto}", AccionDeImportacion.Create);
        }
    }
}
