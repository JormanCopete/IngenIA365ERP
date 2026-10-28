using IngenIA365ERP.Application.Common.Personas;
using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Governance;
using MediatR;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Imports.CatalogoDePlantillasDeCartera.ColumnasDeVinculados;

namespace IngenIA365ERP.Application.Lending.Imports;

/// <summary>
/// Plantilla 2 — personas vinculadas (feature 013, T0302; plantillas.md §2; <c>POST /api/lending/related-parties/import?mode=review|apply</c>,
/// <c>Lending.RelatedParties.Manage</c>). (nuevo)
/// </summary>
public sealed record ImportRelatedPartiesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportRelatedPartiesCommandValidator : AbstractValidator<ImportRelatedPartiesCommand>
{
    public ImportRelatedPartiesCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportRelatedPartiesCommand>.LargoMaximo);
    }
}

/// <summary>
/// Cada fila registra un vínculo con las <b>mismas reglas que el alta unitaria</b> (<see cref="AltaDeVinculados"/>): la persona y la
/// persona relacionada se citan por documento y deben existir vivas en el maestro (si no, <c>Import.Cell.NotFound</c> en su columna: la
/// plantilla no crea personas); los errores de negocio llevan el mismo código del alta (<c>Lending.RelatedParty.LinkRequired</c>,
/// <c>.Duplicate</c>, <c>.KinshipDegreeExceeded</c>) en su columna. Una fila cuya llave ya está registrada viva queda «sin cambio». Lo
/// importado <b>prevalece sobre lo migrado</b> para la misma persona y fundamento. Todo o nada (<see cref="EjecutorDeImportacion"/>); un
/// vínculo nuevo es una vigencia, así que aplicar pide motivo.
/// </summary>
public sealed class ImportRelatedPartiesCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, AltaDeVinculados alta)
    : IRequestHandler<ImportRelatedPartiesCommand, Result<ImportResultDto>>
{
    private static readonly IReadOnlyDictionary<string, RelatedPartyBasis> EtiquetasDeFundamento = new Dictionary<string, RelatedPartyBasis>
    {
        ["consejero"] = RelatedPartyBasis.BoardMember,
        ["junta de vigilancia"] = RelatedPartyBasis.OversightBoardMember,
        ["representante legal"] = RelatedPartyBasis.LegalRepresentative,
        ["aportante significativo"] = RelatedPartyBasis.SignificantContributor,
        ["conyuge"] = RelatedPartyBasis.Spouse,
        ["pariente"] = RelatedPartyBasis.Relative,
    };

    private static readonly IReadOnlyDictionary<string, KinshipKind> EtiquetasDeParentesco = new Dictionary<string, KinshipKind>
    {
        ["consanguinidad"] = KinshipKind.Consanguinity,
        ["afinidad"] = KinshipKind.Affinity,
    };

    public Task<Result<ImportResultDto>> Handle(ImportRelatedPartiesCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(CatalogoDePlantillasDeCartera.Vinculados, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        // Los documentos citados, en bloque: nunca una consulta por fila.
        var documentos = ctx.Datos.Filas.SelectMany(f => new[] { f.Crudo(C.Persona), f.Crudo(C.PersonaRelacionada) }).OfType<string>()
            .Select(d => d.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // Feature 017: el número solo puede ser de dos personas (CC y CE); la plantilla no trae el tipo y la fila ambigua se rechaza.
        documentos = documentos.Concat(documentos.Select(d => UsuarioDeDocumento.NormalizarNumero(d))).Distinct().ToList();
        var personas = ResolutorDePersonaPorDocumento.Indexar(
            await db.People.AsNoTracking().Where(p => documentos.Contains(p.TaxId) && !p.IsDeleted)
                .Select(p => new { p.Id, p.TaxId, p.Username }).ToListAsync(ct),
            p => p.Username, p => p.TaxId);

        // La persona viva de un documento; si no la hay o es ambigua, el error en la columna y nulo.
        int? Persona(FilaDeImportacion fila, string columna, string documento)
        {
            if (personas.TryGet(null, documento, out var encontrada, out var ambigua)) return encontrada.Id;
            fila.Error(columna, ambigua ? ResolutorDePersonaPorDocumento.CodigoAmbiguo : ImportErrors.CellNotFound, ambigua
                ? $"Hay varias personas con el documento «{documento}» y esta plantilla no trae el tipo de documento: no se puede saber a cuál corresponde."
                : $"No hay una persona viva con documento «{documento}». Créela en Maestros › Personas: esta plantilla no crea personas.");
            return null;
        }

        foreach (var fila in ctx.Datos.Filas)
        {
            var documento = fila.Texto(C.Persona, 20);
            var fundamento = fila.Enumeracion(C.Fundamento, EtiquetasDeFundamento);
            var documentoRelacionado = fila.Texto(C.PersonaRelacionada, 20);
            var clase = fila.Enumeracion(C.ClaseDeParentesco, EtiquetasDeParentesco);
            var grado = fila.Entero(C.Grado);
            var porcentaje = fila.Porcentaje(C.PorcentajeDeAportes);
            var desde = fila.Fecha(C.VigenteDesde);
            var hasta = fila.Fecha(C.VigenteHasta);
            var soporte = fila.Texto(C.Soporte, 200);
            fila.Requerido(C.Soporte);

            var llave = string.Join('|', documento, fundamento?.ToString(), documentoRelacionado,
                desde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (!ctx.Datos.LlaveUnica(fila, llave, C.Persona) || fila.TieneErrores || documento is null || fundamento is null || desde is null) continue;

            if (grado is < 1 or > byte.MaxValue)
            {
                fila.Error(C.Grado, ImportErrors.CellFormat, "El grado es un entero desde 1.");
                continue;
            }
            if (porcentaje is <= 0m or > 1m)
            {
                fila.Error(C.PorcentajeDeAportes, ImportErrors.CellFormat, "El porcentaje de aportes va en puntos, mayor que cero y hasta 100.");
                continue;
            }
            if (hasta < desde)
            {
                fila.Error(C.VigenteHasta, ImportErrors.CellFormat, "La vigencia no puede terminar antes de empezar.");
                continue;
            }
            if (Persona(fila, C.Persona, documento) is not { } persona) continue;
            int? relacionada = null;
            if (documentoRelacionado is not null)
            {
                if (Persona(fila, C.PersonaRelacionada, documentoRelacionado) is not { } otra) continue;
                relacionada = otra;
            }

            var yaEsta = await db.RelatedParties.AsNoTracking().AnyAsync(v => !v.IsDeleted && v.PersonId == persona && v.Basis == fundamento
                && v.RelatedThroughPersonId == relacionada && v.ValidFrom == desde, ct);
            if (yaEsta)
            {
                ctx.Registrar(fila, llave, AccionDeImportacion.Unchanged);
                continue;
            }

            var registro = await alta.RegistrarAsync(new BorradorDeVinculado(persona, fundamento.Value, desde.Value, RelatedPartySource.Import)
            {
                RelatedThroughPersonId = relacionada,
                KinshipKind = clase,
                KinshipDegree = grado is { } g ? (byte)g : null,
                ContributionPercentage = porcentaje,
                ValidTo = hasta,
                Notes = soporte,
            }, ct);
            if (registro.IsFailure)
            {
                fila.Error(ColumnaDelError(registro.Error.Code), registro.Error.Code, registro.Error.Message);
                continue;
            }

            ctx.PedirMotivo();
            ctx.Registrar(fila, llave, AccionDeImportacion.Create,
            [
                new(C.Fundamento, null, fundamento.Value.ToString()),
                new(C.VigenteDesde, null, desde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ]);
        }
    }

    /// <summary>La columna donde cae cada error del alta unitaria.</summary>
    private static string ColumnaDelError(string codigo) => codigo switch
    {
        RelatedParty.CodigoVinculoRequerido => C.PersonaRelacionada,
        RelatedParty.CodigoDuplicado => C.VigenteDesde,
        ErroresDeGobierno.CodigoGradoFueraDelLimite => C.Grado,
        _ => C.Persona,
    };
}
