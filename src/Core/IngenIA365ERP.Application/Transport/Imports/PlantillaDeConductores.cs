using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Imports;
using IngenIA365ERP.Application.Transport.Drivers;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Transport.Imports.PlantillaDeConductores;

namespace IngenIA365ERP.Application.Transport.Imports;

/// <summary>
/// La plantilla <c>transport.drivers</c> (feature 020, R8, T088; FR-025): registra conductores y sus asignaciones. La llave es el documento
/// del conductor y la placa; la persona debe existir (no crea personas). Pasa por <see cref="RolDeConductor"/>, el mismo camino de la
/// pantalla. Una fila sin placa sólo registra el rol.
/// </summary>
public static class PlantillaDeConductores
{
    public const string Clave = "transport.drivers";

    public const string TipoDocumento = "tipoDocumento";
    public const string Documento = "documento";
    public const string PlacaCol = "placa";
    public const string Desde = "desde";
    public const string Titular = "titular";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Conductores", ModuloDeAuditoria.Transport,
    [
        new HojaDePlantilla("Datos",
        [
            new(TipoDocumento, TipoDeValor.Codigo, Obligatoria: true, Largo: 3, Reglas: "CC, CE, PPT, PEP, TI o PA", Ejemplo: "CC"),
            new(Documento, TipoDeValor.Persona, Obligatoria: true, Largo: 20,
                Reglas: "llave; la persona debe existir viva en Maestros › Personas (esta plantilla no crea personas)", Ejemplo: "2020202020"),
            new(PlacaCol, TipoDeValor.Codigo, Largo: 10, Reglas: "llave; placa de un vehículo vivo. Vacía = sólo registra al conductor", Ejemplo: "AAA111"),
            new(Desde, TipoDeValor.Fecha, Reglas: "inicio de la asignación; vacía = hoy", Ejemplo: "2026-12-01"),
            new(Titular, TipoDeValor.SiNo, Reglas: "sí si es el conductor titular del vehículo (a lo sumo uno vigente)", Ejemplo: "si"),
        ]),
    ]);
}

public sealed record ImportDriversCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string? Reason = null, SiYaExiste Existentes = SiYaExiste.Actualizar)
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportDriversCommandValidator : AbstractValidator<ImportDriversCommand>
{
    public ImportDriversCommandValidator() => RuleFor(x => x.File).NotNull();
}

public sealed class ImportDriversCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, RolDeConductor rol, IDateTimeService reloj)
    : IRequestHandler<ImportDriversCommand, Result<ImportResultDto>>
{
    public Task<Result<ImportResultDto>> Handle(ImportDriversCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var filas = ctx.Datos.Filas;
        var documentos = new Dictionary<FilaDeImportacion, DocumentoValidado>();
        foreach (var fila in filas)
            if (DocumentoDePlantilla.Leer(fila, P.TipoDocumento, P.Documento) is { } d)
                documentos[fila] = d;
        var usuarios = documentos.Values.Select(d => d.Usuario).Distinct().ToList();
        var personas = usuarios.Count == 0
            ? new Dictionary<string, Person>()
            : (await db.People.Where(p => usuarios.Contains(p.Username)).ToListAsync(ct)).GroupBy(p => p.Username).ToDictionary(g => g.Key, g => g.First());
        var placas = filas.Select(f => Placa.Normalizar(f.Texto(P.PlacaCol))).Where(p => p.Length > 0).Distinct().ToList();
        var vehiculos = placas.Count == 0
            ? new Dictionary<string, Vehicle>()
            : (await db.Vehicles.Where(v => placas.Contains(v.Plate) && v.Status != VehicleStatus.Retired).ToListAsync(ct)).ToDictionary(v => v.Plate);
        var conductores = new Dictionary<int, Driver>();

        foreach (var fila in filas)
        {
            if (!documentos.TryGetValue(fila, out var documento)) continue;
            var placa = Placa.Normalizar(fila.Texto(P.PlacaCol));
            if (!ctx.Datos.LlaveUnica(fila, $"{documento.Usuario}|{placa}", P.Documento)) continue;
            if (!personas.TryGetValue(documento.Usuario, out var persona))
            {
                fila.Error(P.Documento, ImportErrors.CellNotFound,
                    $"No hay una persona viva con documento «{documento.Numero}». Créela en Maestros › Personas: esta plantilla no crea personas.");
                continue;
            }

            var desde = fila.Fecha(P.Desde) ?? reloj.HoyLocal;
            var titular = fila.SiNo(P.Titular);
            if (fila.TieneErrores) continue;

            var accion = AccionDeImportacion.Unchanged;
            if (!conductores.TryGetValue(persona.Id, out var conductor))
            {
                var existente = await rol.FilaDeAsync(persona.Id, ct);
                if (existente is { IsDeleted: false, Status: DriverStatus.Active })
                {
                    conductor = existente;
                }
                else
                {
                    var alta = await rol.AsignarAsync(persona, desde, ct);
                    if (alta.IsFailure) { fila.Error(P.Documento, alta.Error.Code, alta.Error.Message); continue; }
                    conductor = alta.Value;
                    accion = AccionDeImportacion.Create;
                }
                conductores[persona.Id] = conductor;
            }

            if (placa.Length > 0)
            {
                if (!vehiculos.TryGetValue(placa, out var vehiculo))
                {
                    fila.Error(P.PlacaCol, ImportErrors.CellNotFound, $"No hay un vehículo vivo con la placa {placa}.");
                    continue;
                }
                var yaAsignado = conductor.Id != 0 && await db.DriverAssignments.AnyAsync(a => a.DriverId == conductor.Id && a.VehicleId == vehiculo.Id
                                                                                            && (a.To == null || a.To >= desde), ct);
                if (!yaAsignado)
                {
                    var asignacion = await rol.AsignarVehiculoAsync(conductor, vehiculo, desde, titular, ct);
                    if (asignacion.IsFailure) { fila.Error(asignacion.Error.Code == "Driver.PrimaryAlreadyExists" ? P.Titular : P.PlacaCol, asignacion.Error.Code, asignacion.Error.Message); continue; }
                    accion = accion == AccionDeImportacion.Create ? accion : AccionDeImportacion.Update;
                }
            }
            ctx.Registrar(fila, $"{documento.Usuario}|{placa}", accion);
        }
    }
}
