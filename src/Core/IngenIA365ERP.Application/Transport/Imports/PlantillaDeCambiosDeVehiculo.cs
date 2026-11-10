using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Imports;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Transport.Imports.PlantillaDeCambiosDeVehiculo;

namespace IngenIA365ERP.Application.Transport.Imports;

/// <summary>
/// La plantilla <c>transport.changes</c> (feature 020, R8, T063; FR-025, US3 escenario 4): activar, inactivar, retirar o cambiar de propietario
/// a varios vehículos, cada fila con su fecha y motivo, por <see cref="CambiosDeVehiculo"/>: las mismas reglas que una a una. Varias filas
/// de la misma placa se aplican en el orden del archivo.
/// </summary>
public static class PlantillaDeCambiosDeVehiculo
{
    public const string Clave = "transport.changes";

    public const string PlacaCol = "placa";
    public const string Accion = "accion";
    public const string Fecha = "fecha";
    public const string Motivo = "motivo";
    public const string TipoDocumentoNuevoPropietario = "tipoDocumentoNuevoPropietario";
    public const string DocumentoNuevoPropietario = "documentoNuevoPropietario";

    public static readonly IReadOnlyDictionary<string, string> Acciones = new Dictionary<string, string>
    {
        ["activar"] = "activar", ["inactivar"] = "inactivar", ["suspender"] = "inactivar", ["retirar"] = "retirar",
        ["desvincular"] = "retirar", ["propietario"] = "propietario", ["cambiarpropietario"] = "propietario",
    };

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Cambios de vehículos", ModuloDeAuditoria.Transport,
    [
        new HojaDePlantilla("Datos",
        [
            new(PlacaCol, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "placa de un vehículo vivo", Ejemplo: "AAA111"),
            new(Accion, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "activar, inactivar, retirar o propietario", Ejemplo: "inactivar"),
            new(Fecha, TipoDeValor.Fecha, Obligatoria: true, Reglas: "fecha efectiva; no anterior al último cambio del mismo tipo", Ejemplo: "2026-12-01"),
            new(Motivo, TipoDeValor.Texto, Obligatoria: true, Largo: 500, Ejemplo: "Suspensión por mantenimiento"),
            new(TipoDocumentoNuevoPropietario, TipoDeValor.Codigo, Largo: 3, Reglas: "con la acción «propietario»", Ejemplo: ""),
            new(DocumentoNuevoPropietario, TipoDeValor.Persona, Largo: 20,
                Reglas: "obligatorio con la acción «propietario»; la persona debe existir viva (esta plantilla no crea personas)", Ejemplo: ""),
        ]),
    ]);
}

public sealed record ImportVehicleChangesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string? Reason = null, SiYaExiste Existentes = SiYaExiste.Actualizar)
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportVehicleChangesCommandValidator : AbstractValidator<ImportVehicleChangesCommand>
{
    public ImportVehicleChangesCommandValidator() => RuleFor(x => x.File).NotNull();
}

public sealed class ImportVehicleChangesCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, CambiosDeVehiculo cambios)
    : IRequestHandler<ImportVehicleChangesCommand, Result<ImportResultDto>>
{
    public const string CodigoAccion = "Vehicle.ChangeActionInvalid";

    public Task<Result<ImportResultDto>> Handle(ImportVehicleChangesCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var filas = ctx.Datos.Filas;
        var placas = filas.Select(f => Placa.Normalizar(f.Texto(P.PlacaCol))).Where(p => p.Length > 0).Distinct().ToList();
        var vivos = placas.Count == 0
            ? new Dictionary<string, Vehicle>()
            : (await db.Vehicles.Where(v => placas.Contains(v.Plate) && v.Status != VehicleStatus.Retired).ToListAsync(ct))
                .ToDictionary(v => v.Plate);

        var documentos = new Dictionary<FilaDeImportacion, DocumentoValidado>();
        foreach (var fila in filas)
            if (!fila.EstaVacia(P.DocumentoNuevoPropietario)
                && DocumentoDePlantilla.Leer(fila, P.TipoDocumentoNuevoPropietario, P.DocumentoNuevoPropietario) is { } d)
                documentos[fila] = d;
        var usuarios = documentos.Values.Select(d => d.Usuario).Distinct().ToList();
        var personas = usuarios.Count == 0
            ? new Dictionary<string, Domain.Entities.Core.Person>()
            : (await db.People.Where(p => usuarios.Contains(p.Username)).ToListAsync(ct)).GroupBy(p => p.Username).ToDictionary(g => g.Key, g => g.First());

        var numero = 0;
        foreach (var fila in filas)
        {
            numero++;
            var placa = Placa.Normalizar(fila.Texto(P.PlacaCol));
            var accionCruda = fila.Texto(P.Accion);
            var fecha = fila.Fecha(P.Fecha);
            var motivo = fila.Texto(P.Motivo, 500);
            if (placa.Length == 0) fila.Requerido(P.PlacaCol);
            fila.Requerido(P.Accion);
            fila.Requerido(P.Fecha);
            fila.Requerido(P.Motivo);
            if (fila.TieneErrores || fecha is null || motivo is null || accionCruda is null) continue;

            if (!P.Acciones.TryGetValue(TablaLeida.Normalizar(accionCruda), out var accion))
            {
                fila.Error(P.Accion, CodigoAccion, $"«{accionCruda}» no es una acción. Admite: activar, inactivar, retirar o propietario.");
                continue;
            }
            if (!vivos.TryGetValue(placa, out var vehiculo))
            {
                fila.Error(P.PlacaCol, ImportErrors.CellNotFound, $"No hay un vehículo vivo con la placa {placa}.");
                continue;
            }

            Result r;
            switch (accion)
            {
                case "activar":
                    r = await cambios.CambiarEstadoAsync(vehiculo, VehicleStatus.Active, fecha.Value, motivo, ChangeSource.Import, ct);
                    break;
                case "inactivar":
                    r = await cambios.CambiarEstadoAsync(vehiculo, VehicleStatus.Inactive, fecha.Value, motivo, ChangeSource.Import, ct);
                    break;
                case "retirar":
                    r = await cambios.RetirarAsync(vehiculo, fecha.Value, motivo, ChangeSource.Import, ct);
                    break;
                default:
                    if (!documentos.TryGetValue(fila, out var documento))
                    {
                        fila.Requerido(P.DocumentoNuevoPropietario);
                        continue;
                    }
                    if (!personas.TryGetValue(documento.Usuario, out var nuevo))
                    {
                        fila.Error(P.DocumentoNuevoPropietario, ImportErrors.CellNotFound,
                            $"No hay una persona viva con documento «{documento.Numero}». Créela en Maestros › Personas: esta plantilla no crea personas.");
                        continue;
                    }
                    r = await cambios.CambiarPropietarioAsync(vehiculo, nuevo, fecha.Value, motivo, ChangeSource.Import, ct);
                    break;
            }

            if (r.IsFailure)
            {
                fila.Error(r.Error.Code is "Vehicle.EffectiveDateBeforeLastChange" ? P.Fecha : P.Accion, r.Error.Code, r.Error.Message);
                continue;
            }
            ctx.Registrar(fila, $"{placa}#{numero}", AccionDeImportacion.Update, [new CampoCambiadoDto(P.Accion, null, accion)]);
        }
    }
}
