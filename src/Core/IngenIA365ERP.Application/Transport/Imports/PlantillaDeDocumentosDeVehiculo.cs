using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Transport.Documents;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Transport.Imports.PlantillaDeDocumentosDeVehiculo;

namespace IngenIA365ERP.Application.Transport.Imports;

/// <summary>
/// La plantilla <c>transport.documents</c> (feature 020, R8, T075; FR-025, US3 escenario 3): las renovaciones de SOAT, técnico-mecánica,
/// pólizas y demás de todo el parque. La llave es placa, tipo y número: un número nuevo registra el documento y deja el anterior en la
/// historia (<see cref="RegistrarDocumento"/>); uno que ya existe se corrige con las celdas que traigan valor o se conserva.
/// </summary>
public static class PlantillaDeDocumentosDeVehiculo
{
    public const string Clave = "transport.documents";

    public const string PlacaCol = "placa";
    public const string Tipo = "tipoDocumento";
    public const string Numero = "numero";
    public const string Emisor = "emisor";
    public const string Expedicion = "expedicion";
    public const string Desde = "vigenteDesde";
    public const string Vence = "vence";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Documentos de vehículos", ModuloDeAuditoria.Transport,
    [
        new HojaDePlantilla("Datos",
        [
            new(PlacaCol, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "llave; placa de un vehículo vivo", Ejemplo: "AAA111"),
            new(Tipo, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "llave; código del tipo de documento (SOAT, RTM, TOP, RCC, RCE…)", Ejemplo: "SOAT"),
            new(Numero, TipoDeValor.Texto, Obligatoria: true, Largo: 40, Reglas: "llave; número de la póliza, del certificado o de la tarjeta", Ejemplo: "123456789"),
            new(Emisor, TipoDeValor.Texto, Largo: 120, Reglas: "aseguradora, CDA o entidad que lo expide"),
            new(Expedicion, TipoDeValor.Fecha, Reglas: "yyyy-MM-dd"),
            new(Desde, TipoDeValor.Fecha, Reglas: "inicio de la vigencia"),
            new(Vence, TipoDeValor.Fecha, Obligatoria: true, Reglas: "no anterior al inicio de la vigencia", Ejemplo: "2027-11-30"),
        ]),
    ]);
}

public sealed record ImportVehicleDocumentsCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string? Reason = null, SiYaExiste Existentes = SiYaExiste.Actualizar)
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportVehicleDocumentsCommandValidator : AbstractValidator<ImportVehicleDocumentsCommand>
{
    public ImportVehicleDocumentsCommandValidator() => RuleFor(x => x.File).NotNull();
}

public sealed class ImportVehicleDocumentsCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, RegistrarDocumento registrar)
    : IRequestHandler<ImportVehicleDocumentsCommand, Result<ImportResultDto>>
{
    public Task<Result<ImportResultDto>> Handle(ImportVehicleDocumentsCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, (ctx, c) => ProcesarAsync(ctx, request.Existentes == SiYaExiste.Conservar, c), ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, bool conservar, CancellationToken ct)
    {
        var filas = ctx.Datos.Filas;
        var placas = filas.Select(f => Placa.Normalizar(f.Texto(P.PlacaCol))).Where(p => p.Length > 0).Distinct().ToList();
        var vehiculos = placas.Count == 0
            ? new Dictionary<string, Vehicle>()
            : (await db.Vehicles.Where(v => placas.Contains(v.Plate) && v.Status != VehicleStatus.Retired).ToListAsync(ct)).ToDictionary(v => v.Plate);
        var tipos = (await db.TransportDocumentTypes.AsNoTracking().Where(t => t.IsActive).ToListAsync(ct)).ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
        var ids = vehiculos.Values.Select(v => v.Id).ToList();
        var existentes = ids.Count == 0 ? [] : await db.VehicleDocuments.Where(d => ids.Contains(d.VehicleId)).ToListAsync(ct);
        var validador = new VehicleDocumentInputValidator();

        foreach (var fila in filas)
        {
            var placa = Placa.Normalizar(fila.Texto(P.PlacaCol));
            var codigo = fila.Codigo(P.Tipo, 10);
            var numero = fila.Texto(P.Numero, 40)?.ToUpperInvariant();
            var vence = fila.Fecha(P.Vence);
            if (placa.Length == 0) fila.Requerido(P.PlacaCol);
            fila.Requerido(P.Tipo);
            fila.Requerido(P.Numero);
            if (fila.TieneErrores || codigo is null || numero is null) continue;
            if (!ctx.Datos.LlaveUnica(fila, $"{placa}|{codigo}|{numero}", P.Numero)) continue;

            if (!vehiculos.TryGetValue(placa, out var vehiculo))
            {
                fila.Error(P.PlacaCol, ImportErrors.CellNotFound, $"No hay un vehículo vivo con la placa {placa}.");
                continue;
            }
            if (!tipos.TryGetValue(codigo, out var tipo))
            {
                fila.Error(P.Tipo, ImportErrors.CellNotFound, $"No hay un tipo de documento activo con el código {codigo}. Créelo en Transporte › Documentos.");
                continue;
            }

            var existente = existentes.FirstOrDefault(d => d.VehicleId == vehiculo.Id && d.DocumentTypeId == tipo.Id && d.Number == numero);
            if (existente is not null && conservar) { ctx.Registrar(fila, $"{placa}|{codigo}|{numero}", AccionDeImportacion.Unchanged); continue; }

            var entrada = new VehicleDocumentInput(tipo.PublicId, numero, fila.Texto(P.Emisor, 120) ?? existente?.Issuer, fila.Fecha(P.Expedicion) ?? existente?.IssuedOn,
                fila.Fecha(P.Desde) ?? existente?.ValidFrom, vence ?? existente?.ExpiresOn ?? default);
            foreach (var falla in validador.Validate(entrada).Errors)
                fila.Error(falla.PropertyName == nameof(VehicleDocumentInput.ExpiresOn) ? P.Vence : P.Numero, "Transport.DocumentInvalid", falla.ErrorMessage);
            if (fila.TieneErrores) continue;

            if (existente is null)
            {
                var r = await registrar.RegistrarAsync(vehiculo, entrada, ct);
                if (r.IsFailure) { fila.Error(P.Tipo, r.Error.Code, r.Error.Message); continue; }
                ctx.Registrar(fila, $"{placa}|{codigo}|{numero}", AccionDeImportacion.Create);
                continue;
            }

            var cambios = new List<CampoCambiadoDto>();
            if (entrada.Issuer != existente.Issuer) { cambios.Add(new(P.Emisor, existente.Issuer, entrada.Issuer)); existente.Issuer = entrada.Issuer; }
            if (entrada.IssuedOn != existente.IssuedOn) { cambios.Add(new(P.Expedicion, existente.IssuedOn?.ToString("yyyy-MM-dd"), entrada.IssuedOn?.ToString("yyyy-MM-dd"))); existente.IssuedOn = entrada.IssuedOn; }
            if (entrada.ValidFrom != existente.ValidFrom) { cambios.Add(new(P.Desde, existente.ValidFrom?.ToString("yyyy-MM-dd"), entrada.ValidFrom?.ToString("yyyy-MM-dd"))); existente.ValidFrom = entrada.ValidFrom; }
            if (entrada.ExpiresOn != existente.ExpiresOn) { cambios.Add(new(P.Vence, existente.ExpiresOn.ToString("yyyy-MM-dd"), entrada.ExpiresOn.ToString("yyyy-MM-dd"))); existente.ExpiresOn = entrada.ExpiresOn; }
            ctx.Registrar(fila, $"{placa}|{codigo}|{numero}", cambios.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, cambios);
        }
    }
}
