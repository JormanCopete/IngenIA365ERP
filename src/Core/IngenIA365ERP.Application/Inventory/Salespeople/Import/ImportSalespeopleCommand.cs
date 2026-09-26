using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Salespeople.Import;

/// <summary>
/// Plantilla 9 — vendedores (feature 012, T426; contracts/plantillas.md §9). Escribe <c>INV_Salespeople</c> y la marca
/// <c>IsSalesperson</c> de la persona sólo por <see cref="RolDeVendedor"/>, la misma operación de
/// <c>CreateSalespersonCommand</c> y <c>DeleteSalespersonCommand</c>. (nuevo)
/// </summary>
public static class PlantillaDeVendedores
{
    public const string Clave = CatalogoDePlantillas.VendedoresClave;
    public const string Documento = "documento";
    public const string Nombre = "nombre";
    public const string TipoDeVendedor = "tipoDeVendedor";
    public const string AplicaComision = "aplicaComision";
    public const string Activo = "activo";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Vendedores", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla("Datos",
        [
            new(Documento, TipoDeValor.Persona, Obligatoria: true, Largo: 20,
                Reglas: "llave; número sin puntos ni dígito de verificación; la persona debe existir y estar viva en Maestros → Personas (aquí no se crean ni se modifican personas)"),
            new(Nombre, TipoDeValor.Texto, Largo: 160, Reglas: "informativa: si no coincide con el maestro sale un aviso", Ejemplo: "MARÍA PÉREZ"),
            new(TipoDeVendedor, TipoDeValor.Entero, Reglas: "SalespersonType", Ejemplo: "1"),
            new(AplicaComision, TipoDeValor.SiNo, Reglas: "vacío = no", Ejemplo: "no"),
            new(Activo, TipoDeValor.SiNo, Reglas: "vacío = sí; «no» retira el rol, «sí» sobre un rol retirado lo restaura", Ejemplo: "sí"),
        ]),
    ]);
}

/// <summary>Plantilla 9 (§9; <c>POST /api/inventory/salespeople/import?mode=review|apply</c>). (nuevo)</summary>
public sealed record ImportSalespeopleCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportSalespeopleCommandValidator : AbstractValidator<ImportSalespeopleCommand>
{
    public ImportSalespeopleCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportSalespeopleCommand>.LargoMaximo);
    }
}

/// <summary>
/// Cada fila cita a una persona por su documento: si no existe, <c>Import.Cell.NotFound</c>; si fue eliminada,
/// <c>Core.Person.NotFound</c> con la indicación de restaurarla en Personas. <c>activo = sí</c> da el rol (o lo restaura) y
/// actualiza tipo y comisión; <c>activo = no</c> lo retira y pide motivo. Nunca crea ni modifica personas: sólo su marca
/// <c>IsSalesperson</c>, y eso lo hace <see cref="RolDeVendedor"/>. Todo o nada (<see cref="EjecutorDeImportacion"/>).
/// </summary>
public sealed class ImportSalespeopleCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, RolDeVendedor rol)
    : IRequestHandler<ImportSalespeopleCommand, Result<ImportResultDto>>
{
    private const string V = "sí";
    private const string F = "no";

    public Task<Result<ImportResultDto>> Handle(ImportSalespeopleCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(PlantillaDeVendedores.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var documentos = ctx.Datos.Filas.Select(f => f.Crudo(PlantillaDeVendedores.Documento)).OfType<string>().Distinct().ToList();
        var personas = (await db.People.IgnoreQueryFilters().Where(p => documentos.Contains(p.TaxId)).ToListAsync(ct))
            .GroupBy(p => p.TaxId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.IsDeleted).First(), StringComparer.OrdinalIgnoreCase);
        var ids = personas.Values.Select(p => p.Id).ToList();
        var filas = (await db.Salespeople.IgnoreQueryFilters().Where(s => ids.Contains(s.PersonId)).ToListAsync(ct))
            .GroupBy(s => s.PersonId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.IsDeleted).ThenByDescending(s => s.Id).First());

        foreach (var fila in ctx.Datos.Filas)
        {
            var documento = fila.Texto(PlantillaDeVendedores.Documento);
            var nombre = fila.Texto(PlantillaDeVendedores.Nombre);
            var tipo = fila.Entero(PlantillaDeVendedores.TipoDeVendedor);
            var comision = fila.SiNo(PlantillaDeVendedores.AplicaComision);
            var activo = fila.SiNo(PlantillaDeVendedores.Activo, porDefecto: true);
            if (!ctx.Datos.LlaveUnica(fila, documento, PlantillaDeVendedores.Documento) || documento is null || fila.TieneErrores) continue;

            if (tipo is < 0)
            {
                fila.Error(PlantillaDeVendedores.TipoDeVendedor, ImportErrors.CellFormat, "El tipo de vendedor no puede ser negativo.");
                continue;
            }
            if (!personas.TryGetValue(documento, out var persona))
            {
                fila.Error(PlantillaDeVendedores.Documento, ImportErrors.CellNotFound,
                    $"No hay una persona con documento «{documento}». Créela en Maestros → Personas: esta plantilla no crea personas.");
                continue;
            }
            if (persona.IsDeleted)
            {
                var error = ErroresDeVendedores.PersonaEliminada(documento);
                fila.Error(PlantillaDeVendedores.Documento, error.Code, error.Message);
                continue;
            }
            if (nombre is not null && !Coincide(nombre, persona))
                fila.Aviso(PlantillaDeVendedores.Nombre, ImportErrors.CellFormat,
                    $"El nombre «{nombre}» no coincide con el del maestro («{ErroresDeVendedores.Nombre(persona)}»). ¿El documento está bien digitado?");

            filas.TryGetValue(persona.Id, out var existente);
            var vivo = existente is { IsDeleted: false };
            var campos = new List<CampoCambiadoDto>();

            if (!activo)
            {
                if (!vivo)
                {
                    ctx.Registrar(fila, documento, AccionDeImportacion.Unchanged);
                    continue;
                }
                campos.Add(new(PlantillaDeVendedores.Activo, V, F));
                await rol.RetirarAsync(existente!, persona, ct);
                ctx.PedirMotivo();
                ctx.Registrar(fila, documento, AccionDeImportacion.Update, campos);
                continue;
            }

            if (vivo)
            {
                Diferencia(campos, PlantillaDeVendedores.TipoDeVendedor, existente!.SalespersonType?.ToString(), tipo?.ToString());
                Diferencia(campos, PlantillaDeVendedores.AplicaComision, existente.AppliesCommission ? V : F, comision ? V : F);
                existente.SalespersonType = tipo;
                existente.AppliesCommission = comision;
                ctx.Registrar(fila, documento, campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
                continue;
            }

            var antes = existente;
            var asignacion = rol.Asignar(persona, existente, tipo, comision);
            if (asignacion.IsFailure)
            {
                fila.Error(PlantillaDeVendedores.Documento, asignacion.Error.Code, asignacion.Error.Message);
                continue;
            }
            filas[persona.Id] = asignacion.Value.Fila;
            if (antes is not null)
            {
                campos.Add(new(PlantillaDeVendedores.Activo, F, V));
                ctx.Registrar(fila, documento, AccionDeImportacion.Update, campos);
            }
            else
            {
                campos.Add(new(PlantillaDeVendedores.Activo, null, V));
                ctx.Registrar(fila, documento, AccionDeImportacion.Create, campos);
            }
        }
    }

    private static bool Coincide(string nombre, Person persona)
    {
        static string N(string s) => string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        return N(nombre) == N(ErroresDeVendedores.Nombre(persona));
    }

    private static void Diferencia(List<CampoCambiadoDto> campos, string columna, string? antes, string? despues)
    {
        if (!string.Equals(antes ?? string.Empty, despues ?? string.Empty, StringComparison.Ordinal)) campos.Add(new(columna, antes, despues));
    }
}

/// <summary>La plantilla 9 llena (§0.6; <c>GET /api/inventory/salespeople/template.xlsx?withData=true</c>, con datos personales). (nuevo)</summary>
public sealed record GetSalespeopleTemplateDataQuery : IRequest<Result<DatosDePlantilla>>;

public sealed class GetSalespeopleTemplateDataQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSalespeopleTemplateDataQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetSalespeopleTemplateDataQuery request, CancellationToken ct)
    {
        var filas = await (from s in db.Salespeople.IgnoreQueryFilters().AsNoTracking()
                           join p in db.People.AsNoTracking() on s.PersonId equals p.Id
                           orderby p.TaxId
                           select new { p.TaxId, p.FirstName, p.LastName, s.SalespersonType, s.AppliesCommission, s.IsDeleted })
            .ToListAsync(ct);
        var datos = filas
            .GroupBy(f => f.TaxId)
            .Select(g => g.OrderBy(f => f.IsDeleted).First())
            .Select(f => (IReadOnlyList<object?>)[f.TaxId, $"{f.FirstName} {f.LastName}".Trim(), f.SalespersonType, f.AppliesCommission, !f.IsDeleted])
            .ToList();
        return Result.Success(new DatosDePlantilla(
            new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>(StringComparer.OrdinalIgnoreCase) { ["Datos"] = datos }));
    }
}
