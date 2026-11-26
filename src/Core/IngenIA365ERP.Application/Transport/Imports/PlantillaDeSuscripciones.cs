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
/// camino de la pantalla. Una suscripción que ya existe (la que se cruza con las fechas de la fila: <c>desde</c> sólo la identifica) se
/// conserva o, con «actualizar», se le pacta el precio de la fila y, desde el 2026-11-25, <b>se termina</b> en el <c>hasta</c> de la fila si
/// no tenía fin o tenía otro (<see cref="SuscripcionDeServicio.Terminar"/>). Un <c>hasta</c> vacío no le quita el fin a una que lo tiene.
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
            new(Desde, TipoDeValor.Fecha, Obligatoria: true,
                Reglas: "en una suscripción nueva, desde cuándo; si ya existe, sólo la identifica (cualquier fecha de su vigencia)", Ejemplo: "2026-12-01"),
            new(Hasta, TipoDeValor.Fecha,
                Reglas: "vacía = sin fin; en una suscripción que ya existe, la termina en esa fecha (vacía no le quita el fin que tenga)"),
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
        ejecutor.EjecutarAsync(P.Definicion, request, (ctx, c) => ProcesarAsync(ctx, request.Existentes == SiYaExiste.Conservar, request.Reason, c), ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, bool conservar, string? motivo, CancellationToken ct)
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
                if (conservar) { ctx.Registrar(fila, $"{placa}|{producto}", AccionDeImportacion.Unchanged); continue; }
                var cambios = new List<CampoCambiadoDto>();

                // El fin primero: un precio pactado en la misma fila queda cerrado en el fin nuevo.
                if (hasta is { } fin && vigente.To != fin)
                {
                    var anterior = vigente.To;
                    var t = suscripciones.Terminar(vigente, fin, string.IsNullOrWhiteSpace(motivo) ? "Terminada por plantilla" : $"Terminada por plantilla: {motivo.Trim()}");
                    if (t.IsFailure) { fila.Error(P.Hasta, t.Error.Code, t.Error.Message); continue; }
                    cambios.Add(new CampoCambiadoDto(P.Hasta, anterior?.ToString("yyyy-MM-dd"), fin.ToString("yyyy-MM-dd")));
                }

                if (precio is { } nuevo)
                {
                    var desdeDelPrecio = desde.Value < vigente.From ? vigente.From : desde.Value;
                    var actual = vigente.Prices.Where(p => !p.IsDeleted && p.VigenteEn(desdeDelPrecio)).Select(p => (decimal?)p.Price).FirstOrDefault();
                    if (actual != nuevo)
                    {
                        var r = suscripciones.PonerPrecio(vigente, nuevo, desdeDelPrecio);
                        if (r.IsFailure) { fila.Error(P.Precio, r.Error.Code, r.Error.Message); continue; }
                        cambios.Add(new CampoCambiadoDto(P.Precio, actual?.ToString(), nuevo.ToString()));
                    }
                }

                ctx.Registrar(fila, $"{placa}|{producto}", cambios.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update,
                    cambios.Count == 0 ? null : cambios);
                continue;
            }

            var alta = await suscripciones.SuscribirAsync(v, s, desde.Value, hasta, precio, null, ct);
            if (alta.IsFailure) { fila.Error(P.Desde, alta.Error.Code, alta.Error.Message); continue; }
            existentes.Add(alta.Value);
            ctx.Registrar(fila, $"{placa}|{producto}", AccionDeImportacion.Create);
        }
    }
}
