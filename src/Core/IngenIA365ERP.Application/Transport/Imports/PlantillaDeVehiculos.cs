using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Imports;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Transport.Imports.PlantillaDeVehiculos;

namespace IngenIA365ERP.Application.Transport.Imports;

/// <summary>
/// La plantilla <c>transport.vehicles</c> (feature 020, R8, T062; FR-025, US3): la placa identifica la fila; el propietario va por tipo y
/// número de documento y debe existir (no crea personas); los catálogos por código o nombre. Crear o actualizar pasa por
/// <see cref="VehicleRegistrar"/>, el mismo camino de la pantalla. Actualizar cambia sólo las celdas con valor; el propietario y el estado
/// no cambian aquí: van en <c>transport.changes</c>.
/// </summary>
public static class PlantillaDeVehiculos
{
    public const string Clave = "transport.vehicles";

    public const string PlacaCol = "placa";
    public const string NumeroInterno = "numeroInterno";
    public const string RadioMovil = "radioMovil";
    public const string TipoDocumentoPropietario = "tipoDocumentoPropietario";
    public const string DocumentoPropietario = "documentoPropietario";
    public const string Marca = "marca";
    public const string Linea = "linea";
    public const string Modelo = "modelo";
    public const string Clase = "clase";
    public const string Carroceria = "carroceria";
    public const string Color = "color";
    public const string Cilindraje = "cilindraje";
    public const string Combustible = "combustible";
    public const string Capacidad = "capacidad";
    public const string Motor = "motor";
    public const string Chasis = "chasis";
    public const string Vin = "vin";
    public const string LicenciaTransito = "licenciaTransito";
    public const string OrganismoTransito = "organismoTransito";
    public const string FechaMatricula = "fechaMatricula";
    public const string ClaseServicio = "claseServicio";
    public const string Modalidad = "modalidad";
    public const string FechaIngreso = "fechaIngreso";
    public const string ContratoNumero = "contratoNumero";
    public const string ContratoDesde = "contratoDesde";
    public const string ContratoHasta = "contratoHasta";
    public const string TarjetaOperacion = "tarjetaOperacion";
    public const string GrupoRodamiento = "grupoRodamiento";
    public const string MinimoDiario = "minimoDiario";
    public const string MinimoMensual = "minimoMensual";
    public const string Oficina = "oficina";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Vehículos", ModuloDeAuditoria.Transport,
    [
        new HojaDePlantilla("Datos",
        [
            new(PlacaCol, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "llave; letras y números, sin espacios ni guiones (se quitan)", Ejemplo: "AAA111"),
            new(NumeroInterno, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "número interno de la cooperativa; único entre vivos", Ejemplo: "0001"),
            new(RadioMovil, TipoDeValor.Codigo, Largo: 20, Reglas: "código con que el sistema externo identifica al vehículo; único entre vivos", Ejemplo: "101"),
            new(TipoDocumentoPropietario, TipoDeValor.Codigo, Largo: 3, Reglas: "CC, CE, PPT, PEP, TI, PA o NIT", Ejemplo: "CC"),
            new(DocumentoPropietario, TipoDeValor.Persona, Largo: 20,
                Reglas: "obligatorio al crear; la persona debe existir viva en Maestros › Personas (esta plantilla no crea personas). Al actualizar se ignora: el propietario se cambia con la plantilla de cambios",
                Ejemplo: "1010101010"),
            new(Marca, TipoDeValor.Codigo, Largo: 120, Reglas: "código o nombre de una marca del catálogo de Transporte"),
            new(Linea, TipoDeValor.Texto, Largo: 60),
            new(Modelo, TipoDeValor.Entero, Reglas: "año, de 1950 al próximo", Ejemplo: "2022"),
            new(Clase, TipoDeValor.Codigo, Largo: 120, Reglas: "código o nombre de una clase de vehículo", Ejemplo: "AUTOMOVIL"),
            new(Carroceria, TipoDeValor.Codigo, Largo: 120, Reglas: "código o nombre de una carrocería", Ejemplo: "SEDAN"),
            new(Color, TipoDeValor.Texto, Largo: 40, Ejemplo: "Amarillo"),
            new(Cilindraje, TipoDeValor.Entero, Reglas: "en centímetros cúbicos", Ejemplo: "1400"),
            new(Combustible, TipoDeValor.Codigo, Largo: 120, Reglas: "código o nombre de un combustible", Ejemplo: "GASOLINA"),
            new(Capacidad, TipoDeValor.Entero, Reglas: "pasajeros", Ejemplo: "4"),
            new(Motor, TipoDeValor.Texto, Largo: 40),
            new(Chasis, TipoDeValor.Texto, Largo: 40),
            new(Vin, TipoDeValor.Texto, Largo: 40),
            new(LicenciaTransito, TipoDeValor.Texto, Largo: 30),
            new(OrganismoTransito, TipoDeValor.Codigo, Largo: 120, Reglas: "código o nombre de un organismo de tránsito"),
            new(FechaMatricula, TipoDeValor.Fecha, Reglas: "yyyy-MM-dd"),
            new(ClaseServicio, TipoDeValor.Codigo, Largo: 120, Reglas: "código o nombre de una clase de servicio", Ejemplo: "PUBLICO"),
            new(Modalidad, TipoDeValor.Codigo, Largo: 120, Reglas: "código o nombre de una modalidad", Ejemplo: "INDIVIDUAL"),
            new(FechaIngreso, TipoDeValor.Fecha, Reglas: "fecha de vinculación a la cooperativa; vacía al crear = hoy", Ejemplo: "2020-01-15"),
            new(ContratoNumero, TipoDeValor.Texto, Largo: 30),
            new(ContratoDesde, TipoDeValor.Fecha),
            new(ContratoHasta, TipoDeValor.Fecha, Reglas: "no anterior al inicio"),
            new(TarjetaOperacion, TipoDeValor.Texto, Largo: 30, Reglas: "número; su vencimiento va en los documentos"),
            new(GrupoRodamiento, TipoDeValor.Codigo, Largo: 120, Reglas: "código o nombre de un grupo de rodamiento"),
            new(MinimoDiario, TipoDeValor.Monto, Reglas: "dato informativo, sin control"),
            new(MinimoMensual, TipoDeValor.Monto, Reglas: "dato informativo, sin control"),
            new(Oficina, TipoDeValor.Sucursal, Largo: 80, Reglas: "código o nombre de la agencia"),
        ]),
    ]);
}

public sealed record ImportVehiclesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string? Reason = null, SiYaExiste Existentes = SiYaExiste.Actualizar)
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportVehiclesCommandValidator : AbstractValidator<ImportVehiclesCommand>
{
    public ImportVehiclesCommandValidator() => RuleFor(x => x.File).NotNull();
}

public sealed class ImportVehiclesCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, VehicleRegistrar registrar)
    : IRequestHandler<ImportVehiclesCommand, Result<ImportResultDto>>
{
    public const string CodigoInvalido = "Vehicle.Invalid";

    public Task<Result<ImportResultDto>> Handle(ImportVehiclesCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, (ctx, c) => ProcesarAsync(ctx, request.Existentes == SiYaExiste.Conservar, c), ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, bool conservar, CancellationToken ct)
    {
        var filas = ctx.Datos.Filas;
        var catalogos = await CatalogosDeTransporteCitados.LeerAsync(db, ct);
        var oficinas = CatalogoCitado<Guid>.Desde(
            await db.Branches.AsNoTracking().Select(b => new { b.PublicId, b.LegacyCode, b.Name, b.ShortName }).ToListAsync(ct),
            b => b.PublicId, b => b.LegacyCode, b => b.Name, b => b.ShortName);

        var placas = filas.Select(f => Placa.Normalizar(f.Texto(P.PlacaCol))).Where(p => p.Length > 0).Distinct().ToList();
        var vivos = placas.Count == 0
            ? new Dictionary<string, Vehicle>()
            : (await db.Vehicles.Where(v => placas.Contains(v.Plate) && v.Status != VehicleStatus.Retired).ToListAsync(ct))
                .ToDictionary(v => v.Plate);

        var documentos = new Dictionary<FilaDeImportacion, DocumentoValidado>();
        foreach (var fila in filas)
            if (!fila.EstaVacia(P.DocumentoPropietario) && DocumentoDePlantilla.Leer(fila, P.TipoDocumentoPropietario, P.DocumentoPropietario) is { } d)
                documentos[fila] = d;
        var usuarios = documentos.Values.Select(d => d.Usuario).Distinct().ToList();
        var personas = usuarios.Count == 0
            ? new Dictionary<string, Person>()
            : (await db.People.Where(p => usuarios.Contains(p.Username)).ToListAsync(ct)).GroupBy(p => p.Username).ToDictionary(g => g.Key, g => g.First());

        var validador = new VehicleInputValidator();
        foreach (var fila in filas)
        {
            var placa = Placa.Normalizar(fila.Texto(P.PlacaCol));
            if (placa.Length == 0) { fila.Requerido(P.PlacaCol); continue; }
            if (!ctx.Datos.LlaveUnica(fila, placa, P.PlacaCol)) continue;

            vivos.TryGetValue(placa, out var existente);
            if (existente is not null && conservar)
            {
                ctx.Registrar(fila, placa, AccionDeImportacion.Unchanged);
                continue;
            }

            var base_ = existente is null ? new VehicleInput { Plate = placa } : await registrar.ComoEntradaAsync(existente, ct);
            var entrada = Leer(fila, base_, placa, catalogos, oficinas);
            if (fila.TieneErrores) continue;

            foreach (var falla in validador.Validate(entrada).Errors)
                fila.Error(ColumnaDe(falla.PropertyName), CodigoInvalido, falla.ErrorMessage);
            if (fila.TieneErrores) continue;

            if (existente is null)
            {
                if (!documentos.TryGetValue(fila, out var documento))
                {
                    if (fila.EstaVacia(P.DocumentoPropietario)) fila.Requerido(P.DocumentoPropietario);
                    continue;
                }
                if (!personas.TryGetValue(documento.Usuario, out var propietario))
                {
                    fila.Error(P.DocumentoPropietario, ImportErrors.CellNotFound,
                        $"No hay una persona viva con documento «{documento.Numero}». Créela en Maestros › Personas: esta plantilla no crea personas.");
                    continue;
                }
                var alta = await registrar.RegistrarAsync(entrada, propietario, ChangeSource.Import, ct);
                if (alta.IsFailure) { fila.Error(ColumnaDelError(alta.Error.Code), alta.Error.Code, alta.Error.Message); continue; }
                ctx.Registrar(fila, placa, AccionDeImportacion.Create);
                continue;
            }

            if (documentos.TryGetValue(fila, out var otro) && personas.TryGetValue(otro.Usuario, out var citado) && citado.Id != existente.OwnerPersonId)
                fila.Aviso(P.DocumentoPropietario, "Vehicle.OwnerChangeIgnored",
                    "El propietario no cambia con esta plantilla: use la plantilla de cambios (acción «propietario»).");

            var cambios = Diferencias(base_, entrada);
            if (cambios.Count == 0) { ctx.Registrar(fila, placa, AccionDeImportacion.Unchanged); continue; }
            var r = await registrar.ActualizarAsync(existente, entrada, ct);
            if (r.IsFailure) { fila.Error(ColumnaDelError(r.Error.Code), r.Error.Code, r.Error.Message); continue; }
            ctx.Registrar(fila, placa, AccionDeImportacion.Update, cambios);
        }
    }

    /// <summary>La entrada de la fila: parte de lo que hay (o de nada) y pone sólo las celdas con valor.</summary>
    private static VehicleInput Leer(FilaDeImportacion f, VehicleInput b, string placa, CatalogosDeTransporteCitados c, CatalogoCitado<Guid> oficinas)
    {
        Guid? Cat(string col, TransportCatalogKind kind, string que) =>
            f.EstaVacia(col) ? null : f.Referencia(col, c.De(kind), que, "en Transporte › Catálogos") is { } g && g != Guid.Empty ? g : null;
        short? Corto(int? n) => n is { } x ? (short)Math.Clamp(x, short.MinValue, short.MaxValue) : null;

        return b with
        {
            Plate = placa,
            InternalNumber = f.Codigo(P.NumeroInterno, 10) ?? b.InternalNumber,
            RadioCode = f.Codigo(P.RadioMovil, 20) ?? b.RadioCode,
            BrandPublicId = Cat(P.Marca, TransportCatalogKind.Brand, "una marca") ?? b.BrandPublicId,
            Line = f.Texto(P.Linea, 60) ?? b.Line,
            ModelYear = Corto(f.Entero(P.Modelo)) ?? b.ModelYear,
            VehicleClassPublicId = Cat(P.Clase, TransportCatalogKind.VehicleClass, "una clase de vehículo") ?? b.VehicleClassPublicId,
            BodyTypePublicId = Cat(P.Carroceria, TransportCatalogKind.BodyType, "una carrocería") ?? b.BodyTypePublicId,
            Color = f.Texto(P.Color, 40) ?? b.Color,
            EngineDisplacementCc = f.Entero(P.Cilindraje) ?? b.EngineDisplacementCc,
            FuelPublicId = Cat(P.Combustible, TransportCatalogKind.Fuel, "un combustible") ?? b.FuelPublicId,
            PassengerCapacity = Corto(f.Entero(P.Capacidad)) ?? b.PassengerCapacity,
            EngineNumber = f.Texto(P.Motor, 40) ?? b.EngineNumber,
            ChassisNumber = f.Texto(P.Chasis, 40) ?? b.ChassisNumber,
            Vin = f.Texto(P.Vin, 40) ?? b.Vin,
            TransitLicenseNumber = f.Texto(P.LicenciaTransito, 30) ?? b.TransitLicenseNumber,
            TransitAuthorityPublicId = Cat(P.OrganismoTransito, TransportCatalogKind.TransitAuthority, "un organismo de tránsito") ?? b.TransitAuthorityPublicId,
            RegistrationDate = f.Fecha(P.FechaMatricula) ?? b.RegistrationDate,
            ServiceClassPublicId = Cat(P.ClaseServicio, TransportCatalogKind.ServiceClass, "una clase de servicio") ?? b.ServiceClassPublicId,
            ModalityPublicId = Cat(P.Modalidad, TransportCatalogKind.Modality, "una modalidad") ?? b.ModalityPublicId,
            JoinedOn = f.Fecha(P.FechaIngreso) ?? b.JoinedOn,
            AffiliationContractNumber = f.Texto(P.ContratoNumero, 30) ?? b.AffiliationContractNumber,
            AffiliationContractFrom = f.Fecha(P.ContratoDesde) ?? b.AffiliationContractFrom,
            AffiliationContractTo = f.Fecha(P.ContratoHasta) ?? b.AffiliationContractTo,
            OperationCardNumber = f.Texto(P.TarjetaOperacion, 30) ?? b.OperationCardNumber,
            RollingGroupPublicId = Cat(P.GrupoRodamiento, TransportCatalogKind.RollingGroup, "un grupo de rodamiento") ?? b.RollingGroupPublicId,
            MinDailyRolling = f.Monto(P.MinimoDiario) ?? b.MinDailyRolling,
            MinMonthlyRolling = f.Monto(P.MinimoMensual) ?? b.MinMonthlyRolling,
            BranchPublicId = Oficina(f, oficinas) ?? b.BranchPublicId,
        };
    }

    private static Guid? Oficina(FilaDeImportacion f, CatalogoCitado<Guid> oficinas)
    {
        if (f.EstaVacia(P.Oficina)) return null;
        var o = f.Referencia(P.Oficina, oficinas, "una agencia", "en Maestros › Agencias");
        return o == Guid.Empty ? null : o;
    }

    private static List<CampoCambiadoDto> Diferencias(VehicleInput antes, VehicleInput despues)
    {
        var cambios = new List<CampoCambiadoDto>();
        foreach (var prop in typeof(VehicleInput).GetProperties())
        {
            var a = prop.GetValue(antes);
            var d = prop.GetValue(despues);
            if (!Equals(a, d)) cambios.Add(new CampoCambiadoDto(ColumnaDe(prop.Name), a?.ToString(), d?.ToString()));
        }
        return cambios;
    }

    private static string ColumnaDe(string propiedad) => propiedad switch
    {
        nameof(VehicleInput.Plate) => P.PlacaCol,
        nameof(VehicleInput.InternalNumber) => P.NumeroInterno,
        nameof(VehicleInput.RadioCode) => P.RadioMovil,
        nameof(VehicleInput.Line) => P.Linea,
        nameof(VehicleInput.ModelYear) => P.Modelo,
        nameof(VehicleInput.Color) => P.Color,
        nameof(VehicleInput.EngineDisplacementCc) => P.Cilindraje,
        nameof(VehicleInput.PassengerCapacity) => P.Capacidad,
        nameof(VehicleInput.EngineNumber) => P.Motor,
        nameof(VehicleInput.ChassisNumber) => P.Chasis,
        nameof(VehicleInput.Vin) => P.Vin,
        nameof(VehicleInput.TransitLicenseNumber) => P.LicenciaTransito,
        nameof(VehicleInput.RegistrationDate) => P.FechaMatricula,
        nameof(VehicleInput.JoinedOn) => P.FechaIngreso,
        nameof(VehicleInput.AffiliationContractNumber) => P.ContratoNumero,
        nameof(VehicleInput.AffiliationContractFrom) => P.ContratoDesde,
        nameof(VehicleInput.AffiliationContractTo) => P.ContratoHasta,
        nameof(VehicleInput.OperationCardNumber) => P.TarjetaOperacion,
        nameof(VehicleInput.MinDailyRolling) => P.MinimoDiario,
        nameof(VehicleInput.MinMonthlyRolling) => P.MinimoMensual,
        nameof(VehicleInput.BrandPublicId) => P.Marca,
        nameof(VehicleInput.VehicleClassPublicId) => P.Clase,
        nameof(VehicleInput.BodyTypePublicId) => P.Carroceria,
        nameof(VehicleInput.FuelPublicId) => P.Combustible,
        nameof(VehicleInput.TransitAuthorityPublicId) => P.OrganismoTransito,
        nameof(VehicleInput.ServiceClassPublicId) => P.ClaseServicio,
        nameof(VehicleInput.ModalityPublicId) => P.Modalidad,
        nameof(VehicleInput.RollingGroupPublicId) => P.GrupoRodamiento,
        nameof(VehicleInput.BranchPublicId) => P.Oficina,
        _ => P.PlacaCol,
    };

    private static string ColumnaDelError(string codigo) => codigo switch
    {
        "Vehicle.PlateDuplicate" or "Vehicle.PlateInvalid" => P.PlacaCol,
        "Vehicle.InternalNumberDuplicate" => P.NumeroInterno,
        "Vehicle.RadioCodeDuplicate" => P.RadioMovil,
        "Vehicle.OwnerRequired" or "Vehicle.OwnerNotFound" => P.DocumentoPropietario,
        "Branch.NotFound" => P.Oficina,
        _ => P.PlacaCol,
    };
}

/// <summary>Los catálogos de Transporte citados por código o nombre, por tipo, para las plantillas.</summary>
public sealed class CatalogosDeTransporteCitados
{
    private readonly Dictionary<TransportCatalogKind, CatalogoCitado<Guid>> _porTipo;

    private CatalogosDeTransporteCitados(Dictionary<TransportCatalogKind, CatalogoCitado<Guid>> porTipo) => _porTipo = porTipo;

    public CatalogoCitado<Guid> De(TransportCatalogKind tipo) => _porTipo[tipo];

    public static async Task<CatalogosDeTransporteCitados> LeerAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var todos = await db.TransportCatalogItems.AsNoTracking().Where(c => c.IsActive)
            .Select(c => new { c.Kind, c.PublicId, c.Code, c.Name }).ToListAsync(ct);
        var porTipo = Enum.GetValues<TransportCatalogKind>().ToDictionary(
            k => k,
            k => CatalogoCitado<Guid>.Desde(todos.Where(c => c.Kind == k), c => c.PublicId, c => c.Code, c => c.Name));
        return new CatalogosDeTransporteCitados(porTipo);
    }
}
