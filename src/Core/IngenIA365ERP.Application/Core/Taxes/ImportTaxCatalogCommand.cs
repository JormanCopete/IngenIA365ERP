using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Core.Taxes.PlantillaDeImpuestos;

namespace IngenIA365ERP.Application.Core.Taxes;

/// <summary>
/// La plantilla 1 (feature 012, T166; contracts/plantillas.md §1; contracts/api.md §30,
/// <c>POST /api/core/taxes/import?mode=review|apply</c>): conceptos, impuestos y tarifas en un libro, sobre
/// <see cref="EjecutorDeImportacion"/> y con <b>las mismas reglas que el alta unitaria</b>
/// (<see cref="ReglasDelCatalogoTributario"/>): el código es la llave (en tarifas, código + vigente desde), lo que existe
/// se actualiza, lo que queda igual es «sin cambio», nada se borra. Una tarifa en vigencia sólo admite cambiar nombre,
/// notas y fin de vigencia (<c>Core.TaxRate.InEffect</c>); crear o cerrar una vigencia exige motivo
/// (<c>requiresReason</c>). La clase y la forma de cálculo de un impuesto existente no cambian (<c>Core.Tax.Immutable</c>).
/// Lo que se crea por plantilla no queda «pendiente de validar». (nuevo)
/// </summary>
public sealed record ImportTaxCatalogCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportTaxCatalogCommandValidator : AbstractValidator<ImportTaxCatalogCommand>
{
    public ImportTaxCatalogCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportTaxCatalogCommand>.LargoMaximo);
    }
}

public sealed class ImportTaxCatalogCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, IDateTimeService reloj)
    : IRequestHandler<ImportTaxCatalogCommand, Result<ImportResultDto>>
{
    public Task<Result<ImportResultDto>> Handle(ImportTaxCatalogCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        // Todo en bloque, con seguimiento: la plantilla modifica en el contexto y el ejecutor guarda o deshace.
        var conceptos = await db.WithholdingConcepts.ToListAsync(ct);
        var impuestos = await db.TaxDefinitions.ToListAsync(ct);
        var tarifas = await db.TaxRates.ToListAsync(ct);

        var porCodigoDeConcepto = conceptos.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var porCodigoDeImpuesto = impuestos.ToDictionary(i => i.Code, StringComparer.OrdinalIgnoreCase);

        Conceptos(ctx, porCodigoDeConcepto, tarifas, hoy);
        Impuestos(ctx, porCodigoDeImpuesto);
        Tarifas(ctx, porCodigoDeImpuesto, porCodigoDeConcepto, tarifas, hoy);
    }

    // ---------------------------------------------------------------------------------------------- Conceptos --

    private void Conceptos(ContextoDeImportacion ctx, Dictionary<string, WithholdingConcept> porCodigo, List<TaxRate> tarifas, DateOnly hoy)
    {
        var hoja = ctx.Hoja(P.HojaConceptos);
        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(P.Codigo);
            var nombre = fila.Texto(P.Nombre);
            var activo = fila.SiNo(P.Activo, porDefecto: true);
            if (!hoja.LlaveUnica(fila, codigo, P.Codigo) || fila.TieneErrores || codigo is null || nombre is null) continue;

            if (porCodigo.TryGetValue(codigo, out var concepto))
            {
                var campos = new List<CampoCambiadoDto>();
                Diferencia(campos, P.Nombre, concepto.Name, nombre);
                Diferencia(campos, P.Activo, SiNo(concepto.IsActive), SiNo(activo));
                if (concepto.IsActive && !activo)
                {
                    var enUso = tarifas.Where(t => !t.IsDeleted && (ReferenceEquals(t.WithholdingConcept, concepto) || (concepto.Id != 0 && t.WithholdingConceptId == concepto.Id))
                            && (t.ValidTo is null || t.ValidTo >= hoy))
                        .Select(t => t.Code).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToList();
                    if (enUso.Count > 0)
                    {
                        var error = TaxErrors.ConceptInUse(enUso);
                        fila.Error(P.Activo, error.Code, error.Message);
                        continue;
                    }
                }
                concepto.Name = nombre;
                concepto.IsActive = activo;
                ctx.Registrar(fila, codigo, campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
            }
            else
            {
                concepto = new WithholdingConcept { Code = codigo, Name = nombre, IsActive = activo };
                db.WithholdingConcepts.Add(concepto);
                porCodigo[codigo] = concepto;
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create, [new(P.Nombre, null, nombre)]);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------- Impuestos --

    private void Impuestos(ContextoDeImportacion ctx, Dictionary<string, TaxDefinition> porCodigo)
    {
        var hoja = ctx.Hoja(P.HojaImpuestos);
        var leidas = new List<(FilaDeImportacion Fila, TaxDefinition Impuesto, string? Base, bool Nuevo, List<CampoCambiadoDto> Campos)>();

        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(P.Codigo);
            var nombre = fila.Texto(P.Nombre);
            var clase = fila.Enumeracion(P.Clase, P.EtiquetasDeClase);
            var forma = fila.Enumeracion(P.FormaDeCalculo, P.EtiquetasDeForma);
            var calculadoSobre = fila.Codigo(P.CalculadoSobre);
            var esRetencion = fila.SiNo(P.EsRetencion);
            var codigoDian = fila.Texto(P.CodigoDian)?.ToUpperInvariant();
            var activo = fila.EstaVacia(P.Activo) || fila.SiNo(P.Activo, porDefecto: true);
            if (codigoDian is not null && !ReglasDelCatalogoTributario.EsCodigoDianValido(codigoDian))
                fila.Error(P.CodigoDian, ImportErrors.CellFormat, $"«{codigoDian}» no es un código de tributo DIAN (2 a 4 letras o dígitos: 01, 04, 22, ZZ…).");
            if (!hoja.LlaveUnica(fila, codigo, P.Codigo) || fila.TieneErrores || codigo is null || nombre is null || clase is null || forma is null) continue;

            var retencion = ReglasDelCatalogoTributario.EsRetencion(clase.Value, esRetencion);
            var campos = new List<CampoCambiadoDto>();
            if (porCodigo.TryGetValue(codigo, out var impuesto))
            {
                if (impuesto.Kind != clase.Value) { Error(fila, P.Clase, TaxErrors.Immutable(codigo, "clase")); continue; }
                if (impuesto.CalculationForm != forma.Value) { Error(fila, P.FormaDeCalculo, TaxErrors.Immutable(codigo, "forma de cálculo")); continue; }
                Diferencia(campos, P.Nombre, impuesto.Name, nombre);
                Diferencia(campos, P.CodigoDian, impuesto.DianTaxCode, codigoDian);
                Diferencia(campos, P.Activo, SiNo(impuesto.IsActive), SiNo(activo));
                impuesto.Name = nombre;
                impuesto.DianTaxCode = codigoDian;
                impuesto.IsActive = activo;
                leidas.Add((fila, impuesto, calculadoSobre, false, campos));
            }
            else
            {
                impuesto = new TaxDefinition
                {
                    Code = codigo, Name = nombre, Kind = clase.Value, CalculationForm = forma.Value,
                    IsWithholding = retencion, DianTaxCode = codigoDian, IsActive = activo,
                };
                db.TaxDefinitions.Add(impuesto);
                porCodigo[codigo] = impuesto;
                campos.Add(new(P.Nombre, null, nombre));
                leidas.Add((fila, impuesto, calculadoSobre, true, campos));
            }
        }

        // El impuesto base puede venir en el mismo archivo: se resuelve cuando todas las filas están leídas.
        foreach (var (fila, impuesto, codigoBase, nuevo, campos) in leidas)
        {
            TaxDefinition? base_ = null;
            if (codigoBase is not null && !porCodigo.TryGetValue(codigoBase, out base_))
            {
                fila.Error(P.CalculadoSobre, ImportErrors.CellNotFound, $"No hay un impuesto «{codigoBase}». Créelo en la hoja Impuestos.");
                continue;
            }
            if (!nuevo)
            {
                var actual = impuesto.TaxedOnDefinition?.Code;
                if (!string.Equals(actual, base_?.Code, StringComparison.OrdinalIgnoreCase))
                {
                    Error(fila, P.CalculadoSobre, TaxErrors.Immutable(impuesto.Code, "impuesto base"));
                    continue;
                }
            }
            else
            {
                if (ReglasDelCatalogoTributario.Definicion(impuesto.Code, impuesto.CalculationForm, base_) is { } error)
                {
                    Error(fila, P.CalculadoSobre, error);
                    continue;
                }
                impuesto.TaxedOnDefinition = base_;
            }
            ctx.Registrar(fila, impuesto.Code, nuevo ? AccionDeImportacion.Create : campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
        }
    }

    // ----------------------------------------------------------------------------------------------- Tarifas --

    private void Tarifas(ContextoDeImportacion ctx, Dictionary<string, TaxDefinition> impuestos, Dictionary<string, WithholdingConcept> conceptos,
        List<TaxRate> tarifas, DateOnly hoy)
    {
        var hoja = ctx.Hoja(P.HojaTarifas);
        var impuestosCitados = CatalogoCitado<TaxDefinition>.Desde(impuestos.Values, i => i, i => i.Code);
        var conceptosCitados = CatalogoCitado<WithholdingConcept>.Desde(conceptos.Values, c => c, c => c.Code);
        var revisar = new List<(FilaDeImportacion Fila, TaxRate Tarifa)>();

        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(P.Codigo);
            var impuesto = fila.Referencia(P.Impuesto, impuestosCitados, "un impuesto", "en la hoja Impuestos o en Maestros › Impuestos");
            var nombre = fila.Texto(P.Nombre);
            var porcentaje = fila.Porcentaje(P.TarifaPorcentaje);
            var porUnidad = fila.Monto(P.ValorPorUnidad);
            var concepto = fila.Referencia(P.ConceptoRetencion, conceptosCitados, "un concepto de retención", "en la hoja Conceptos");
            var municipio = fila.Texto(P.Municipio);
            var actividad = fila.Texto(P.Actividad);
            var minimoUvt = fila.Cantidad(P.BaseMinimaUvt);
            var minimoPesos = fila.Monto(P.BaseMinimaPesos);
            var aplicaA = fila.Enumeracion(P.AplicaA, P.EtiquetasDeAplicaA);
            var prioridad = fila.Entero(P.Prioridad) ?? 0;
            var tipoPersona = LeerTipoDePersona(fila);
            var condiciones = new TaxRateConditionsDto(tipoPersona,
                fila.SiNoIndiferente(P.SujetoDeclarante), fila.SiNoIndiferente(P.SujetoResponsableIva),
                fila.SiNoIndiferente(P.SujetoGranContribuyente), fila.SiNoIndiferente(P.SujetoAutorretenedor),
                fila.SiNoIndiferente(P.SujetoRegimenSimple), fila.SiNoIndiferente(P.AgenteGranContribuyente),
                fila.SiNoIndiferente(P.AgenteRetenedorIva));
            var desde = fila.Fecha(P.VigenteDesde);
            var hasta = fila.Fecha(P.VigenteHasta);
            var norma = fila.Texto(P.Norma);
            var notas = fila.Texto(P.Notas);

            var llave = codigo is null || desde is null ? null : $"{codigo}|{desde:yyyy-MM-dd}";
            if (!hoja.LlaveUnica(fila, llave, P.VigenteDesde) || fila.TieneErrores
                || codigo is null || impuesto is null || nombre is null || aplicaA is null || desde is null || norma is null) continue;

            var datos = new DatosDeFila(codigo, nombre, porcentaje, porUnidad, municipio, actividad, minimoUvt, minimoPesos, condiciones,
                aplicaA.Value, (short)Math.Clamp(prioridad, 0, short.MaxValue), desde.Value, hasta, norma, notas);
            if (!Forma(fila, datos, prioridad)) continue;

            var propuesta = new TaxRate
            {
                TaxDefinition = impuesto, TaxDefinitionId = impuesto.Id, Code = codigo,
                WithholdingConcept = concepto, WithholdingConceptId = concepto?.Id,
            };
            ReglasDelCatalogoTributario.Copiar(datos, propuesta);
            if (ReglasDelCatalogoTributario.TarifaConCampo(impuesto, propuesta, concepto) is { } regla) { Error(fila, Columna(regla.Campo), regla.Error); continue; }

            var otroImpuesto = tarifas.FirstOrDefault(t => !t.IsDeleted && string.Equals(t.Code, codigo, StringComparison.OrdinalIgnoreCase) && !MismoImpuesto(t, impuesto));
            if (otroImpuesto is not null)
            {
                var nombreOtro = otroImpuesto.TaxDefinition?.Name ?? otroImpuesto.Code;
                Error(fila, P.Codigo, CodigoDeCatalogo.Duplicado($"una tarifa de otro impuesto ({nombreOtro})", codigo, nombreOtro));
                continue;
            }

            var existente = tarifas.FirstOrDefault(t => !t.IsDeleted && string.Equals(t.Code, codigo, StringComparison.OrdinalIgnoreCase) && t.ValidFrom == desde.Value);
            if (existente is not null)
            {
                var campos = Cambios(existente, propuesta);
                if (existente.ValidFrom <= hoy && ReglasDelCatalogoTributario.CambiaLoFijo(existente, propuesta))
                {
                    Error(fila, P.TarifaPorcentaje, TaxErrors.InEffect(existente));
                    continue;
                }
                if (existente.ValidTo != propuesta.ValidTo) ctx.PedirMotivo();
                if (existente.ValidFrom <= hoy)
                {
                    existente.Name = propuesta.Name;
                    existente.Notes = propuesta.Notes;
                    existente.ValidTo = propuesta.ValidTo;
                }
                else
                {
                    ReglasDelCatalogoTributario.Copiar(datos, existente);
                    existente.TaxDefinition = impuesto;
                    existente.WithholdingConcept = concepto;
                    existente.WithholdingConceptId = concepto?.Id;
                }
                revisar.Add((fila, existente));
                ctx.Registrar(fila, llave!, campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
            }
            else
            {
                db.TaxRates.Add(propuesta);
                tarifas.Add(propuesta);
                ctx.PedirMotivo();
                revisar.Add((fila, propuesta));
                ctx.Registrar(fila, llave!, AccionDeImportacion.Create, [new(P.Nombre, null, propuesta.Name)]);
            }
        }

        // Con todas las filas aplicadas: vigencias cruzadas y empates contra el catálogo como quedaría.
        foreach (var (fila, tarifa) in revisar)
        {
            if (ReglasDelCatalogoTributario.Cruce(tarifas, tarifa) is { } cruce) { Error(fila, P.VigenteDesde, TaxErrors.Overlaps(cruce)); continue; }
            if (ReglasDelCatalogoTributario.Empate(tarifas, tarifa.TaxDefinition!, tarifa) is { } empate)
                Error(fila, P.Prioridad, TaxErrors.Ambiguous(tarifa.Code, empate.Code));
        }
    }

    /// <summary>Las reglas de forma del validador del alta unitaria, fila por fila.</summary>
    private static bool Forma(FilaDeImportacion fila, DatosDeFila d, int prioridad)
    {
        if (d.Rate is { } r && (r < 0 || r > 1)) fila.Error(P.TarifaPorcentaje, ImportErrors.CellFormat, "La tarifa va de 0 a 100 (en puntos).");
        if (d.Rate is not null && d.AmountPerUnit is not null) fila.Error(P.ValorPorUnidad, ImportErrors.CellFormat, "Una tarifa es porcentual o por unidad, no las dos.");
        if (d.AmountPerUnit is <= 0) fila.Error(P.ValorPorUnidad, ImportErrors.CellFormat, "El valor por unidad es mayor que cero.");
        if (d.MinimumBaseUvt is < 0) fila.Error(P.BaseMinimaUvt, ImportErrors.CellFormat, "La base mínima es cero o más.");
        if (d.MinimumBasePesos is < 0) fila.Error(P.BaseMinimaPesos, ImportErrors.CellFormat, "La base mínima es cero o más.");
        if (d.MinimumBaseUvt is not null && d.MinimumBasePesos is not null) fila.Error(P.BaseMinimaPesos, ImportErrors.CellFormat, "La base mínima va en UVT o en pesos, no en las dos.");
        if (d.MunicipalityDaneCode is { } m && !System.Text.RegularExpressions.Regex.IsMatch(m, ReglasDelCatalogoTributario.PatronDeMunicipio))
            fila.Error(P.Municipio, ImportErrors.CellFormat, "El municipio es el código DANE (DIVIPOLA) de cinco dígitos.");
        if (d.ActivityCode is { } a)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(a, ReglasDelCatalogoTributario.PatronDeActividad))
                fila.Error(P.Actividad, ImportErrors.CellFormat, "La actividad es el código CIIU (4 a 6 dígitos) o *.");
            else if (a != Domain.Taxes.MotorTributario.ActividadGeneral && d.MunicipalityDaneCode is null)
                fila.Error(P.Actividad, ImportErrors.CellRequired, "Una actividad distinta de * exige el municipio.");
        }
        if (prioridad < 0) fila.Error(P.Prioridad, ImportErrors.CellFormat, "La prioridad es cero o más.");
        if (d.ValidTo is { } h && h < d.ValidFrom) fila.Error(P.VigenteHasta, ImportErrors.CellFormat, "La vigencia no puede terminar antes de empezar.");
        return !fila.TieneErrores;
    }

    private static string? LeerTipoDePersona(FilaDeImportacion fila)
    {
        var crudo = fila.Crudo(P.SujetoTipoPersona);
        if (crudo is null) return null;
        var tipo = P.TipoDePersona(crudo);
        if (tipo is null) fila.Error(P.SujetoTipoPersona, ImportErrors.CellFormat, $"«{crudo}» no es válido en «{P.SujetoTipoPersona}». Admite: Natural, Juridica o vacío.");
        return tipo;
    }

    /// <summary>La columna de la plantilla que corresponde al dato de la tarifa que incumple la regla.</summary>
    private static string Columna(CampoDeTarifa campo) => campo switch
    {
        CampoDeTarifa.ValorPorUnidad => P.ValorPorUnidad,
        CampoDeTarifa.Concepto => P.ConceptoRetencion,
        CampoDeTarifa.Municipio => P.Municipio,
        CampoDeTarifa.Actividad => P.Actividad,
        CampoDeTarifa.Condiciones => P.SujetoTipoPersona,
        _ => P.TarifaPorcentaje,
    };

    private static List<CampoCambiadoDto> Cambios(TaxRate antes, TaxRate despues)
    {
        var campos = new List<CampoCambiadoDto>();
        Diferencia(campos, P.Nombre, antes.Name, despues.Name);
        Diferencia(campos, P.TarifaPorcentaje, Numero(antes.Rate * 100), Numero(despues.Rate * 100));
        Diferencia(campos, P.ValorPorUnidad, Numero(antes.AmountPerUnit), Numero(despues.AmountPerUnit));
        Diferencia(campos, P.ConceptoRetencion, CodigoDeConcepto(antes), CodigoDeConcepto(despues));
        Diferencia(campos, P.Municipio, antes.MunicipalityDaneCode, despues.MunicipalityDaneCode);
        Diferencia(campos, P.Actividad, antes.ActivityCode, despues.ActivityCode);
        Diferencia(campos, P.BaseMinimaUvt, Numero(antes.MinimumBaseUvt), Numero(despues.MinimumBaseUvt));
        Diferencia(campos, P.BaseMinimaPesos, Numero(antes.MinimumBasePesos), Numero(despues.MinimumBasePesos));
        Diferencia(campos, P.AplicaA, antes.AppliesTo.ToString(), despues.AppliesTo.ToString());
        Diferencia(campos, P.Prioridad, antes.Priority.ToString(CultureInfo.InvariantCulture), despues.Priority.ToString(CultureInfo.InvariantCulture));
        Diferencia(campos, P.SujetoTipoPersona, P.EtiquetaDeTipoDePersona(antes.SubjectPersonType), P.EtiquetaDeTipoDePersona(despues.SubjectPersonType));
        Diferencia(campos, P.SujetoDeclarante, SiNo(antes.SubjectIsIncomeTaxFiler), SiNo(despues.SubjectIsIncomeTaxFiler));
        Diferencia(campos, P.SujetoResponsableIva, SiNo(antes.SubjectIsVatResponsible), SiNo(despues.SubjectIsVatResponsible));
        Diferencia(campos, P.SujetoGranContribuyente, SiNo(antes.SubjectIsLargeContributor), SiNo(despues.SubjectIsLargeContributor));
        Diferencia(campos, P.SujetoAutorretenedor, SiNo(antes.SubjectIsSelfWithholder), SiNo(despues.SubjectIsSelfWithholder));
        Diferencia(campos, P.SujetoRegimenSimple, SiNo(antes.SubjectIsSimpleTaxRegime), SiNo(despues.SubjectIsSimpleTaxRegime));
        Diferencia(campos, P.AgenteGranContribuyente, SiNo(antes.AgentIsLargeContributor), SiNo(despues.AgentIsLargeContributor));
        Diferencia(campos, P.AgenteRetenedorIva, SiNo(antes.AgentIsVatWithholdingAgent), SiNo(despues.AgentIsVatWithholdingAgent));
        Diferencia(campos, P.VigenteHasta, antes.ValidTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), despues.ValidTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Diferencia(campos, P.Norma, antes.LegalSource, despues.LegalSource);
        Diferencia(campos, P.Notas, antes.Notes, despues.Notes);
        return campos;
    }

    private static string? CodigoDeConcepto(TaxRate t) => t.WithholdingConcept?.Code;

    private static bool MismoImpuesto(TaxRate t, TaxDefinition impuesto) =>
        ReferenceEquals(t.TaxDefinition, impuesto) || (impuesto.Id != 0 && t.TaxDefinitionId == impuesto.Id);

    private static void Diferencia(List<CampoCambiadoDto> campos, string columna, string? antes, string? despues)
    {
        var a = string.IsNullOrEmpty(antes) ? null : antes;
        var d = string.IsNullOrEmpty(despues) ? null : despues;
        if (!string.Equals(a, d, StringComparison.Ordinal)) campos.Add(new(columna, a, d));
    }

    private static string? Numero(decimal? valor) => valor?.ToString("0.######", CultureInfo.InvariantCulture);

    private static string? SiNo(bool? valor) => valor switch { true => "sí", false => "no", null => null };

    private static void Error(FilaDeImportacion fila, string columna, Error error) => fila.Error(columna, error.Code, error.Message);

    /// <summary>Una fila de la hoja Tarifas con la forma del alta unitaria.</summary>
    private sealed record DatosDeFila(
        string? Code, string Name, decimal? Rate, decimal? AmountPerUnit, string? MunicipalityDaneCode, string? ActivityCode,
        decimal? MinimumBaseUvt, decimal? MinimumBasePesos, TaxRateConditionsDto? Conditions, TaxAppliesTo AppliesTo, short Priority,
        DateOnly ValidFrom, DateOnly? ValidTo, string LegalSource, string? Notes) : IDatosDeTarifa;
}

/// <summary>
/// Lo que hoy tiene la cooperativa para la plantilla con datos (T166; contracts/plantillas.md §0.6,
/// <c>GET /api/core/taxes/template.xlsx?withData=true</c>): conceptos, impuestos y tarifas con los mismos valores que
/// la importación lee, así el mismo libro se revisa «sin cambio». (nuevo)
/// </summary>
public sealed record GetTaxCatalogTemplateDataQuery : IRequest<Result<DatosDePlantilla>>;

public sealed class GetTaxCatalogTemplateDataQueryValidator : AbstractValidator<GetTaxCatalogTemplateDataQuery>
{
    public GetTaxCatalogTemplateDataQueryValidator() => RuleFor(x => x).NotNull();
}

public sealed class GetTaxCatalogTemplateDataQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetTaxCatalogTemplateDataQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetTaxCatalogTemplateDataQuery request, CancellationToken ct)
    {
        var conceptos = await db.WithholdingConcepts.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
        var impuestos = await db.TaxDefinitions.AsNoTracking().Include(i => i.TaxedOnDefinition).OrderBy(i => i.Code).ToListAsync(ct);
        var tarifas = await db.TaxRates.AsNoTracking().Include(r => r.TaxDefinition).Include(r => r.WithholdingConcept)
            .OrderBy(r => r.Code).ThenBy(r => r.ValidFrom).ToListAsync(ct);

        var filas = new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [P.HojaConceptos] = conceptos.Select(c => (IReadOnlyList<object?>)[c.Code, c.Name, c.IsActive]).ToList(),
            [P.HojaImpuestos] = impuestos.Select(i => (IReadOnlyList<object?>)
                [i.Code, i.Name, i.Kind.ToString(), i.CalculationForm.ToString(), i.TaxedOnDefinition?.Code,
                 i.Kind == TaxKind.Other ? i.IsWithholding : null, i.DianTaxCode, i.IsActive]).ToList(),
            [P.HojaTarifas] = tarifas.Select(r => (IReadOnlyList<object?>)
                [r.Code, r.TaxDefinition?.Code, r.Name, r.Rate, r.AmountPerUnit, r.WithholdingConcept?.Code, r.MunicipalityDaneCode, r.ActivityCode,
                 r.MinimumBaseUvt, r.MinimumBasePesos, r.AppliesTo.ToString(), (int)r.Priority, P.EtiquetaDeTipoDePersona(r.SubjectPersonType),
                 r.SubjectIsIncomeTaxFiler, r.SubjectIsVatResponsible, r.SubjectIsLargeContributor, r.SubjectIsSelfWithholder,
                 r.SubjectIsSimpleTaxRegime, r.AgentIsLargeContributor, r.AgentIsVatWithholdingAgent, r.ValidFrom, r.ValidTo,
                 r.LegalSource, r.Notes]).ToList(),
        };
        return Result.Success(new DatosDePlantilla(filas));
    }
}
