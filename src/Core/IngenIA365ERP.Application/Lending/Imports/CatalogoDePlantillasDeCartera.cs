using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Imports;

/// <summary>
/// Una plantilla de Cartera en el registro (plantillas.md §0.1, §0.7; api.md §3.9): clave, nombre, hojas, ruta base, el permiso para
/// descargarla vacía y para importarla, la entrega desde la que se importa y si trae datos personales. <see cref="Definicion"/> es la de
/// <see cref="EjecutorDeImportacion"/> cuando la plantilla ya está construida; nula, todavía no se descarga. (nuevo)
/// </summary>
public sealed record PlantillaDeCartera(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> Sheets,
    string Route,
    string DownloadPermission,
    string ImportPermission,
    EntregaDeCartera AvailableFrom,
    bool PersonalData,
    DefinicionDePlantilla? Definicion = null);

/// <summary>
/// El registro de plantillas de importación de Cartera (feature 013, T0302, T82; plantillas.md §0.1, §0.7, §0.8): lo que
/// <c>GET /api/lending/templates</c> devuelve y las <see cref="DefinicionDePlantilla"/> de las claves <c>lending.*</c> (módulo de auditoría
/// <c>Lending</c>). US17 construye <c>lending.related-parties</c> (§2); US1 y US2 suman <c>lending.credit-lines</c> y
/// <c>lending.contribution-balances</c>, US8 las partes de la migración y los cobros provisionales. Las plantillas de plataforma
/// (listas restrictivas, valores nacionales) no están: viven en sus pantallas. <b>Ninguna plantilla crea personas.</b> (nuevo)
/// </summary>
public static class CatalogoDePlantillasDeCartera
{
    public const string VinculadosClave = "lending.related-parties";

    public static class ColumnasDeVinculados
    {
        public const string Persona = "persona";
        public const string Fundamento = "fundamento";
        public const string PersonaRelacionada = "personaRelacionada";
        public const string ClaseDeParentesco = "claseDeParentesco";
        public const string Grado = "grado";
        public const string PorcentajeDeAportes = "porcentajeDeAportes";
        public const string VigenteDesde = "vigenteDesde";
        public const string VigenteHasta = "vigenteHasta";
        public const string Soporte = "soporte";
    }

    /// <summary>Plantilla 2 — personas vinculadas (plantillas.md §2). Llave: persona + fundamento + persona relacionada + vigente desde.</summary>
    public static DefinicionDePlantilla Vinculados { get; } = new(VinculadosClave, "Personas vinculadas", ModuloDeAuditoria.Lending,
    [
        new HojaDePlantilla("Datos",
        [
            new(ColumnasDeVinculados.Persona, TipoDeValor.Persona, Obligatoria: true, Largo: 20,
                Reglas: "llave; documento de la persona vinculada sin puntos ni dígito de verificación; debe existir y estar viva en Maestros › Personas (esta plantilla no crea personas)"),
            new(ColumnasDeVinculados.Fundamento, TipoDeValor.Enumeracion, Obligatoria: true,
                Reglas: "llave; BoardMember (consejero), OversightBoardMember (junta de vigilancia), LegalRepresentative (representante legal), SignificantContributor (aportante significativo), Spouse (cónyuge), Relative (pariente)",
                Ejemplo: "BoardMember"),
            new(ColumnasDeVinculados.PersonaRelacionada, TipoDeValor.Persona, Largo: 20,
                Reglas: "llave; obligatoria con Spouse y Relative: el miembro, representante o asociado del que se deriva el vínculo", Ejemplo: ""),
            new(ColumnasDeVinculados.ClaseDeParentesco, TipoDeValor.Enumeracion,
                Reglas: "obligatoria con Relative: Consanguinity (consanguinidad), Affinity (afinidad) o Civil", Ejemplo: ""),
            new(ColumnasDeVinculados.Grado, TipoDeValor.Entero, Reglas: "obligatorio con Relative; dentro de los grados legales (límite nacional)", Ejemplo: ""),
            new(ColumnasDeVinculados.PorcentajeDeAportes, TipoDeValor.Porcentaje, Reglas: "con SignificantContributor; en puntos (5 = 5 %)", Ejemplo: ""),
            new(ColumnasDeVinculados.VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "llave; yyyy-MM-dd", Ejemplo: "2026-03-15"),
            new(ColumnasDeVinculados.VigenteHasta, TipoDeValor.Fecha, Reglas: "vacía = vigente", Ejemplo: ""),
            new(ColumnasDeVinculados.Soporte, TipoDeValor.Texto, Obligatoria: true, Largo: 200,
                Reglas: "acta de elección, certificado o registro civil", Ejemplo: "Acta de asamblea 45"),
        ]),
    ]);

    /// <summary>El registro de plantillas.md §0.1 que aparece en <c>/cartera/plantillas</c> (sin las de plataforma), en su orden de carga.</summary>
    public static IReadOnlyList<PlantillaDeCartera> Todas { get; } =
    [
        new("lending.credit-lines", "Líneas de crédito", "Líneas con sus tasas, documentos, seguros, garantías y equivalencias de plazos de Comercial.",
            ["Lineas", "Tasas", "Documentos", "Seguros", "Garantias", "Equivalencias"], "/api/lending/credit-lines",
            "Lending.CreditLines.View", "Lending.CreditLines.Manage", EntregaDeCartera.K1, PersonalData: false),
        new(VinculadosClave, "Personas vinculadas", "Consejeros, junta de vigilancia, representantes, aportantes significativos, cónyuges y parientes (Ley 454, art. 61).",
            ["Datos"], "/api/lending/related-parties", "Lending.Templates.View", "Lending.RelatedParties.Manage", EntregaDeCartera.K1, PersonalData: true,
            Vinculados),
        new("lending.contribution-balances", "Saldos de aportes y ahorro permanente", "Los saldos que alimentan el cupo por múltiplo de aportes.",
            ["Datos"], "/api/lending/contribution-balances", "Lending.Templates.View", "Lending.CreditLimits.Assign", EntregaDeCartera.K1, PersonalData: true),
        new("lending.migration", "Migración por partes", "La cartera vigente al corte, parte por parte, para revisar, aplicar y activar.",
            ["Datos"], "/api/lending/migration/loads", "Lending.Templates.View", "Lending.Migration.Load", EntregaDeCartera.K1, PersonalData: true),
        new("lending.provisional-collections", "Cobros provisionales", "Lo cobrado por fuera de Cartera entre el corte y la activación, por factura.",
            ["Datos"], "/api/lending/activation/provisional-collections", "Lending.Templates.View", "Lending.Activation.Execute", EntregaDeCartera.K1,
            PersonalData: false),
        new("lending.arrears-history", "Historial de mora complementario", "Los meses de mora que faltan para la pérdida esperada.",
            ["Datos"], "/api/lending/risk/arrears-history", "Lending.Templates.View", "Lending.ArrearsHistory.Import", EntregaDeCartera.K6a, PersonalData: true),
        new("lending.payroll-agreements", "Convenios de pagaduría", "Pagadurías, convenios, contactos y códigos de respuesta.",
            ["Convenios", "Contactos", "CodigosDeRespuesta"], "/api/lending/payroll-agreements", "Lending.PayrollAgreements.View",
            "Lending.PayrollAgreements.Manage", EntregaDeCartera.K2, PersonalData: false),
    ];

    /// <summary>La plantilla por clave (sin distinguir mayúsculas), o nula.</summary>
    public static PlantillaDeCartera? Buscar(string? clave) =>
        string.IsNullOrWhiteSpace(clave) ? null : Todas.FirstOrDefault(p => string.Equals(p.Key, clave.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Si la entrega de la plantilla ya está desplegada (<see cref="CatalogoDeCartera.EntregaVigente"/>).</summary>
    public static bool Disponible(PlantillaDeCartera p) => p.AvailableFrom <= CatalogoDeCartera.EntregaVigente;
}

/// <summary>Una plantilla en el registro, con lo que puede hacer quien pregunta (plantillas.md §0.1, api.md §3.9). (nuevo)</summary>
public sealed record LendingTemplateDto(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> Sheets,
    string Route,
    string ImportPermission,
    EntregaDeCartera AvailableFrom,
    bool PersonalData,
    bool CanDownload,
    bool CanImport);

/// <summary>
/// El registro de plantillas de Cartera (api.md §3.9, <c>GET /api/lending/templates</c>, <c>Lending.Templates.View</c>): cada una con
/// <c>canDownload</c> (tiene el permiso de descargarla, su entrega está desplegada y ya está construida) y <c>canImport</c> (además, el
/// permiso de importar). Catálogo sin oficina. (nuevo)
/// </summary>
public sealed record ListLendingTemplatesQuery : IRequest<Result<IReadOnlyList<LendingTemplateDto>>>;

public sealed class ListLendingTemplatesQueryValidator : AbstractValidator<ListLendingTemplatesQuery>;

public sealed class ListLendingTemplatesQueryHandler(ICurrentUserPermissions permisos)
    : IRequestHandler<ListLendingTemplatesQuery, Result<IReadOnlyList<LendingTemplateDto>>>
{
    public async Task<Result<IReadOnlyList<LendingTemplateDto>>> Handle(ListLendingTemplatesQuery request, CancellationToken ct)
    {
        var todos = permisos.EsMaestroGlobal;
        var concedidos = new HashSet<string>(todos ? Array.Empty<string>() : await permisos.ListAsync(ct), StringComparer.OrdinalIgnoreCase);
        bool Tiene(string p) => todos || concedidos.Contains(p);

        IReadOnlyList<LendingTemplateDto> lista = CatalogoDePlantillasDeCartera.Todas.Select(p =>
        {
            var descarga = CatalogoDePlantillasDeCartera.Disponible(p) && p.Definicion is not null && Tiene(p.DownloadPermission);
            return new LendingTemplateDto(p.Key, p.Name, p.Description, p.Sheets, p.Route, p.ImportPermission, p.AvailableFrom, p.PersonalData,
                descarga, descarga && Tiene(p.ImportPermission));
        }).ToList();
        return Result.Success(lista);
    }
}

/// <summary>
/// La definición de una plantilla para armar el libro vacío (api.md §3.9, <c>GET /api/lending/templates/{templateKey}/template.xlsx</c>;
/// el libro lo arma la API con <c>PlantillaDeImportacion</c>). Clave inexistente, de una entrega no desplegada o todavía sin construir →
/// <c>Lending.Template.NotFound</c>. (nuevo)
/// </summary>
public sealed record GetLendingTemplateQuery(string TemplateKey) : IRequest<Result<DefinicionDePlantilla>>;

public sealed class GetLendingTemplateQueryValidator : AbstractValidator<GetLendingTemplateQuery>
{
    public GetLendingTemplateQueryValidator() => RuleFor(x => x.TemplateKey).NotEmpty().MaximumLength(60);
}

public sealed class GetLendingTemplateQueryHandler : IRequestHandler<GetLendingTemplateQuery, Result<DefinicionDePlantilla>>
{
    public Task<Result<DefinicionDePlantilla>> Handle(GetLendingTemplateQuery request, CancellationToken ct)
    {
        var plantilla = CatalogoDePlantillasDeCartera.Buscar(request.TemplateKey);
        return Task.FromResult(plantilla is { Definicion: { } definicion } && CatalogoDePlantillasDeCartera.Disponible(plantilla)
            ? Result.Success(definicion)
            : Result.Failure<DefinicionDePlantilla>(ErroresDeGobierno.PlantillaInexistente()));
    }
}
