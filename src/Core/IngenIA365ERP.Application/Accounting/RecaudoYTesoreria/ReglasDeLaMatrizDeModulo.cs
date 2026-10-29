using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;

// Feature 015, US8 (T0257, T0265; FR-143, FR-227; contracts/contabilidad.md §6, §8; api.md §16): las reglas de las matrices de Recaudo y Tesorería con vigencia,
// su plantilla de filas sugeridas sin cuenta, su completitud y el tipo de comprobante de cada hecho. Un mismo juego para los dos módulos (Module = REC | TES); los
// permisos los pone la ruta (Accounting.ReceiptsMatrix.*, Accounting.TreasuryMatrix.*). Nada se borra: una versión nueva cierra la anterior la víspera.

/// <summary>Las dimensiones de una regla, por código; nula = cualquiera. (nuevo)</summary>
public sealed record DimensionesDeReglaDeModuloDto(
    string? BranchCode = null,
    string? CollectionPointCode = null,
    string? CashBoxCode = null,
    string? MeansCode = null,
    int? MeansClass = null,
    string? MiscConceptCode = null,
    string? TargetModule = null,
    string? TargetNature = null,
    string? CollectorClass = null,
    string? OwnBankAccountCode = null,
    string? OrderOrigin = null,
    string? OrderKind = null,
    string? Cause = null)
{
    public static DimensionesDeReglaDeModuloDto De(ReglaContableDeModulo r) => new(r.BranchCode, r.CollectionPointCode, r.CashBoxCode, r.MeansCode, r.MeansClass,
        r.MiscConceptCode, r.TargetModule, r.TargetNature, r.CollectorClass, r.OwnBankAccountCode, r.OrderOrigin, r.OrderKind, r.Cause);

    /// <summary>Como diccionario con los nombres de las dimensiones del hecho (sólo las fijadas).</summary>
    public IReadOnlyDictionary<string, string> ComoDiccionario()
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        void Poner(string clave, string? valor) { if (!string.IsNullOrWhiteSpace(valor) && valor.Trim() != ReglaContableDeModulo.Cualquiera) d[clave] = valor.Trim(); }
        Poner(MatricesDeRecaudoYTesoreria.Oficina, BranchCode);
        Poner(MatricesDeRecaudoYTesoreria.Punto, CollectionPointCode);
        Poner(MatricesDeRecaudoYTesoreria.Caja, CashBoxCode);
        Poner(MatricesDeRecaudoYTesoreria.Medio, MeansCode);
        Poner(MatricesDeRecaudoYTesoreria.ClaseDeMedio, MeansClass?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Poner(MatricesDeRecaudoYTesoreria.Concepto, MiscConceptCode);
        Poner(MatricesDeRecaudoYTesoreria.Modulo, TargetModule);
        Poner(MatricesDeRecaudoYTesoreria.Naturaleza, TargetNature);
        Poner(MatricesDeRecaudoYTesoreria.ClaseDeRecaudador, CollectorClass);
        Poner(MatricesDeRecaudoYTesoreria.CuentaPropia, OwnBankAccountCode);
        Poner(MatricesDeRecaudoYTesoreria.OrigenDeLaOrden, OrderOrigin);
        Poner(MatricesDeRecaudoYTesoreria.ClaseDeOrden, OrderKind);
        Poner(MatricesDeRecaudoYTesoreria.Causa, Cause);
        return d;
    }
}

public sealed record ReglaDeModuloDto(
    Guid PublicId,
    string Module,
    string FactType,
    string Role,
    DimensionesDeReglaDeModuloDto Dimensions,
    Guid AccountPublicId,
    string AccountCode,
    string AccountName,
    Guid? CostCenterPublicId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    int SpecificityWeight,
    string Notes);

/// <summary>Una fila de la plantilla: hecho (o <c>*</c>), rol y combinación esperada por la configuración real, sin cuenta. (nuevo)</summary>
public sealed record FilaDePlantillaDeModuloDto(string FactType, string Role, string RoleName, DimensionesDeReglaDeModuloDto Dimensions, string Description,
    IReadOnlyList<string> Facts);

/// <summary>Una fila de la completitud: <c>Complete</c>, <c>Missing</c>, <c>Ambiguous</c> o <c>ByLink</c> (lo resuelve el vínculo). (nuevo)</summary>
public sealed record FilaDeCompletitudDeModuloDto(string FactType, string FactName, string Role, string RoleName, DimensionesDeReglaDeModuloDto Dimensions,
    string Status, string? AccountCode, string? Detail);

public sealed record CompletitudDeModuloDto(string Module, DateOnly AsOf, bool NotPosted, decimal Percent, int Complete, int Total,
    IReadOnlyList<FilaDeCompletitudDeModuloDto> Rows);

public sealed record MapeoDeModuloDto(string FactType, string FactName, string SuggestedVoucherType, string? VoucherTypeCode, Guid? VoucherTypePublicId,
    string? CrossDocumentTypeCode, bool IsSeeded, DateTime? ValidatedAt);

/// <summary>Lo que el validador de cada comando comparte: el módulo tiene que ser <c>REC</c> o <c>TES</c>.</summary>
internal static class ValidacionDeModulo
{
    public static IRuleBuilderOptions<T, string> EsModuloDeLaMatriz<T>(this IRuleBuilder<T, string> regla) =>
        regla.Must(m => MatricesDeRecaudoYTesoreria.De(m) is not null).WithMessage("El módulo es REC (Recaudo) o TES (Tesorería).");
}

// ---------------------------------------------------------------------------------------------------------- crear una regla --

public sealed record CreateModulePostingRuleCommand(
    string Module,
    string? FactType,
    string Role,
    DimensionesDeReglaDeModuloDto? Dimensions,
    Guid AccountPublicId,
    Guid? CostCenterPublicId,
    DateOnly ValidFrom,
    string? Notes,
    string Reason) : IRequest<Result<ReglaDeModuloDto>>, IConMotivo;

public sealed class CreateModulePostingRuleCommandValidator : ValidadorConMotivo<CreateModulePostingRuleCommand>
{
    public CreateModulePostingRuleCommandValidator()
    {
        RuleFor(x => x.Module).NotEmpty().EsModuloDeLaMatriz();
        RuleFor(x => x.Role).NotEmpty().MaximumLength(40);
        RuleFor(x => x.FactType).MaximumLength(60);
        RuleFor(x => x.AccountPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly));
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}

public sealed class CreateModulePostingRuleCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<CreateModulePostingRuleCommand, Result<ReglaDeModuloDto>>
{
    public async Task<Result<ReglaDeModuloDto>> Handle(CreateModulePostingRuleCommand request, CancellationToken ct)
    {
        var modulo = MatricesDeRecaudoYTesoreria.De(request.Module)!;
        var rol = modulo.Rol(request.Role.Trim());
        if (rol is null) return Result.Failure<ReglaDeModuloDto>(ErroresDeLaMatriz.RolDesconocido(modulo, request.FactType ?? "*", request.Role));
        if (rol.PorVinculo) return Result.Failure<ReglaDeModuloDto>(ErroresDeLaMatriz.RolPorVinculo(modulo, rol.Codigo));
        var tipo = string.IsNullOrWhiteSpace(request.FactType) || request.FactType.Trim() == ReglaContableDeModulo.Cualquiera ? ReglaContableDeModulo.Cualquiera : request.FactType.Trim();
        if (tipo != ReglaContableDeModulo.Cualquiera)
        {
            if (modulo.Hecho(tipo) is null) return Result.Failure<ReglaDeModuloDto>(ErroresDeLaMatriz.HechoDesconocido(modulo, tipo));
            if (!rol.DeResultado) return Result.Failure<ReglaDeModuloDto>(ErroresDeLaMatriz.HechoSoloEnResultado(modulo, rol.Codigo));
        }

        var cuenta = await db.ChartOfAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == request.AccountPublicId, ct);
        if (cuenta is null || !cuenta.IsMovement || !cuenta.IsActive || !ModuloContable.Habilitada(cuenta.EnabledModules, modulo.Codigo))
            return Result.Failure<ReglaDeModuloDto>(ErroresDeLaMatriz.CuentaNoElegible(modulo, cuenta?.Code ?? request.AccountPublicId.ToString()));
        int? centro = null;
        if (request.CostCenterPublicId is { } cc)
        {
            centro = await db.CostCenters.AsNoTracking().Where(c => c.PublicId == cc).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
            if (centro is null) return Result.Failure<ReglaDeModuloDto>(new Error("Accounting.CostCenter.NotFound", "No existe ese centro de costo."));
        }

        var d = request.Dimensions ?? new DimensionesDeReglaDeModuloDto();
        ReglaContableDeModulo regla = modulo.Codigo == ModuloContable.Recaudo ? new ReceiptsPostingRule() : new TreasuryPostingRule();
        regla.FactType = tipo;
        regla.Role = rol.Codigo;
        regla.BranchCode = d.BranchCode;
        regla.CollectionPointCode = d.CollectionPointCode;
        regla.CashBoxCode = d.CashBoxCode;
        regla.MeansCode = d.MeansCode;
        regla.MeansClass = d.MeansClass;
        regla.MiscConceptCode = d.MiscConceptCode;
        regla.TargetModule = d.TargetModule;
        regla.TargetNature = d.TargetNature;
        regla.CollectorClass = d.CollectorClass;
        regla.OwnBankAccountCode = d.OwnBankAccountCode;
        regla.OrderOrigin = d.OrderOrigin;
        regla.OrderKind = d.OrderKind;
        regla.Cause = d.Cause;
        regla.AccountId = cuenta.Id;
        regla.CostCenterId = centro;
        regla.ValidFrom = request.ValidFrom;
        regla.Notes = request.Notes ?? string.Empty;
        regla.CreatedAt = reloj.UtcNow;
        regla.Recalcular(rol.DeResultado);

        var mismas = await ConsultasDeReglas.PorClaveAsync(db, modulo, regla.DimensionKey, ct);
        if (mismas.Any(r => r.ValidFrom >= regla.ValidFrom)) return Result.Failure<ReglaDeModuloDto>(ErroresDeLaMatriz.Duplicada(modulo));
        if (await UltimoContabilizadoQueCoincideAsync(db, modulo, regla, ct) is { } ultimo && ultimo >= regla.ValidFrom)
            return Result.Failure<ReglaDeModuloDto>(ErroresDeLaMatriz.Retroactiva(modulo, ultimo));
        foreach (var abierta in mismas.Where(r => r.ValidTo is null || r.ValidTo >= regla.ValidFrom)) abierta.CerrarVigencia(regla.ValidFrom.AddDays(-1));

        if (regla is ReceiptsPostingRule rr) db.ReceiptsPostingRules.Add(rr);
        else db.TreasuryPostingRules.Add((TreasuryPostingRule)regla);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ReglasDeModuloQueries.DtoAsync(db, modulo, regla, ct));
    }


    /// <summary>
    /// La fecha contable más reciente de un hecho ya contabilizado (con comprobante) que tiene una línea que casaría con la regla; nula si ninguno. Conservadora:
    /// mira los últimos mil recibos del módulo con un tipo que puede mover el rol. Así una regla nueva nunca cambia lo contabilizado (contabilidad.md §6).
    /// </summary>
    public static async Task<DateOnly?> UltimoContabilizadoQueCoincideAsync(IApplicationDbContext db, ModuloDeLaMatriz modulo, ReglaContableDeModulo regla, CancellationToken ct)
    {
        var tipos = modulo.Hechos.Where(h => (regla.FactType == ReglaContableDeModulo.Cualquiera || h.Tipo == regla.FactType) && h.RolesPosibles.Contains(regla.Role))
            .Select(h => h.Tipo).ToList();
        if (tipos.Count == 0) return null;
        var recibos = modulo.Codigo == ModuloContable.Recaudo
            ? (from p in db.ReceiptsPostings.AsNoTracking()
               join m in db.IntegrationMessages.AsNoTracking() on p.MessagePublicId equals m.PublicId
               where p.AccountingDocumentId != null && p.OperationDate >= regla.ValidFrom && tipos.Contains(p.MessageType)
               orderby p.OperationDate descending, p.Id descending
               select new { p.OperationDate, m.PayloadJson })
            : (from p in db.TreasuryPostings.AsNoTracking()
               join m in db.IntegrationMessages.AsNoTracking() on p.MessagePublicId equals m.PublicId
               where p.AccountingDocumentId != null && p.OperationDate >= regla.ValidFrom && tipos.Contains(p.MessageType)
               orderby p.OperationDate descending, p.Id descending
               select new { p.OperationDate, m.PayloadJson });
        foreach (var x in await recibos.Take(1000).ToListAsync(ct))
            if (HechoContableDeModulo.Leer(x.PayloadJson).Any(l => l.Rol == regla.Role && MatrizVigente.Coincide(regla, l.Dimensiones)))
                return x.OperationDate;
        return null;
    }
}

// --------------------------------------------------------------------------------------------------------- cerrar una regla --

public sealed record DeactivateModulePostingRuleCommand(string Module, Guid RulePublicId, DateOnly ValidTo, string Reason) : IRequest<Result>, IConMotivo;

public sealed class DeactivateModulePostingRuleCommandValidator : ValidadorConMotivo<DeactivateModulePostingRuleCommand>
{
    public DeactivateModulePostingRuleCommandValidator()
    {
        RuleFor(x => x.Module).NotEmpty().EsModuloDeLaMatriz();
        RuleFor(x => x.RulePublicId).NotEqual(Guid.Empty);
    }
}

public sealed class DeactivateModulePostingRuleCommandHandler(IApplicationDbContext db) : IRequestHandler<DeactivateModulePostingRuleCommand, Result>
{
    public async Task<Result> Handle(DeactivateModulePostingRuleCommand request, CancellationToken ct)
    {
        var modulo = MatricesDeRecaudoYTesoreria.De(request.Module)!;
        var regla = await ConsultasDeReglas.PorIdAsync(db, modulo, request.RulePublicId, ct);
        if (regla is null) return Result.Failure(ErroresDeLaMatriz.NoExiste(modulo));
        if (request.ValidTo < regla.ValidFrom.AddDays(-1))
            return Result.Failure(new Error($"{modulo.PrefijoDeErrores}.InvalidValidTo", "La vigencia no puede cerrarse antes de la víspera de su inicio."));
        // Cerrarla antes de un hecho contabilizado que la usó cambiaría lo contabilizado: no se admite.
        var probe = (ReglaContableDeModulo)(modulo.Codigo == ModuloContable.Recaudo ? new ReceiptsPostingRule() : new TreasuryPostingRule());
        CopiarDimensiones(regla, probe, request.ValidTo.AddDays(1));
        if (await CreateModulePostingRuleCommandHandler.UltimoContabilizadoQueCoincideAsync(db, modulo, probe, ct) is { } ultimo && regla.VigenteEn(ultimo))
            return Result.Failure(ErroresDeLaMatriz.Retroactiva(modulo, ultimo));
        regla.CerrarVigencia(request.ValidTo);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static void CopiarDimensiones(ReglaContableDeModulo de, ReglaContableDeModulo a, DateOnly desde)
    {
        a.FactType = de.FactType; a.Role = de.Role; a.BranchCode = de.BranchCode; a.CollectionPointCode = de.CollectionPointCode; a.CashBoxCode = de.CashBoxCode;
        a.MeansCode = de.MeansCode; a.MeansClass = de.MeansClass; a.MiscConceptCode = de.MiscConceptCode; a.TargetModule = de.TargetModule;
        a.TargetNature = de.TargetNature; a.CollectorClass = de.CollectorClass; a.OwnBankAccountCode = de.OwnBankAccountCode; a.OrderOrigin = de.OrderOrigin;
        a.OrderKind = de.OrderKind; a.Cause = de.Cause; a.ValidFrom = desde;
    }
}

// ---------------------------------------------------------------------------------------------------------------- consultas --

public sealed record ListModulePostingRulesQuery(string Module, DateOnly? AsOf = null, string? FactType = null, string? Role = null)
    : IRequest<Result<IReadOnlyList<ReglaDeModuloDto>>>;

public sealed class ListModulePostingRulesQueryValidator : AbstractValidator<ListModulePostingRulesQuery>
{
    public ListModulePostingRulesQueryValidator() => RuleFor(x => x.Module).NotEmpty().EsModuloDeLaMatriz();
}

public sealed class ListModulePostingRulesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListModulePostingRulesQuery, Result<IReadOnlyList<ReglaDeModuloDto>>>
{
    public async Task<Result<IReadOnlyList<ReglaDeModuloDto>>> Handle(ListModulePostingRulesQuery request, CancellationToken ct)
    {
        var modulo = MatricesDeRecaudoYTesoreria.De(request.Module)!;
        var reglas = await ConsultasDeReglas.ListarAsync(db, modulo, request.AsOf, request.FactType, request.Role, ct);
        return Result.Success(await ReglasDeModuloQueries.DtosAsync(db, modulo, reglas, ct));
    }
}

/// <summary>Las consultas de reglas sobre la tabla de cada módulo, genéricas para que EF las traduzca sobre la entidad concreta. (nuevo)</summary>
public static class ConsultasDeReglas
{
    public static Task<List<ReglaContableDeModulo>> PorClaveAsync(IApplicationDbContext db, ModuloDeLaMatriz modulo, string clave, CancellationToken ct) =>
        modulo.Codigo == ModuloContable.Recaudo ? PorClave(db.ReceiptsPostingRules, clave, ct) : PorClave(db.TreasuryPostingRules, clave, ct);

    public static async Task<ReglaContableDeModulo?> PorIdAsync(IApplicationDbContext db, ModuloDeLaMatriz modulo, Guid id, CancellationToken ct) =>
        modulo.Codigo == ModuloContable.Recaudo
            ? await db.ReceiptsPostingRules.FirstOrDefaultAsync(r => r.PublicId == id, ct)
            : await db.TreasuryPostingRules.FirstOrDefaultAsync(r => r.PublicId == id, ct);

    public static Task<List<ReglaContableDeModulo>> ListarAsync(IApplicationDbContext db, ModuloDeLaMatriz modulo, DateOnly? fecha, string? tipo, string? rol,
        CancellationToken ct) =>
        modulo.Codigo == ModuloContable.Recaudo ? Listar(db.ReceiptsPostingRules, fecha, tipo, rol, ct) : Listar(db.TreasuryPostingRules, fecha, tipo, rol, ct);

    private static async Task<List<ReglaContableDeModulo>> PorClave<T>(IQueryable<T> reglas, string clave, CancellationToken ct) where T : ReglaContableDeModulo =>
        (await reglas.Where(r => r.DimensionKey == clave).ToListAsync(ct)).Cast<ReglaContableDeModulo>().ToList();

    private static async Task<List<ReglaContableDeModulo>> Listar<T>(IQueryable<T> reglas, DateOnly? fecha, string? tipo, string? rol, CancellationToken ct)
        where T : ReglaContableDeModulo
    {
        var consulta = reglas.AsNoTracking();
        if (fecha is { } f) consulta = consulta.Where(r => r.ValidFrom <= f && (r.ValidTo == null || r.ValidTo >= f));
        if (!string.IsNullOrWhiteSpace(tipo)) consulta = consulta.Where(r => r.FactType == tipo);
        if (!string.IsNullOrWhiteSpace(rol)) consulta = consulta.Where(r => r.Role == rol);
        var lista = await consulta.OrderBy(r => r.Role).ThenBy(r => r.FactType).ThenByDescending(r => r.SpecificityWeight).ThenBy(r => r.ValidFrom).ToListAsync(ct);
        return lista.Cast<ReglaContableDeModulo>().ToList();
    }
}

public static class ReglasDeModuloQueries
{
    public static async Task<ReglaDeModuloDto> DtoAsync(IApplicationDbContext db, ModuloDeLaMatriz modulo, ReglaContableDeModulo regla, CancellationToken ct) =>
        (await DtosAsync(db, modulo, [regla], ct))[0];

    public static async Task<IReadOnlyList<ReglaDeModuloDto>> DtosAsync(IApplicationDbContext db, ModuloDeLaMatriz modulo, IReadOnlyList<ReglaContableDeModulo> reglas,
        CancellationToken ct)
    {
        var ids = reglas.Select(r => r.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => new { a.PublicId, a.Code, a.Name }, ct);
        var centrosIds = reglas.Where(r => r.CostCenterId != null).Select(r => r.CostCenterId!.Value).Distinct().ToList();
        var centros = await db.CostCenters.AsNoTracking().Where(c => centrosIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.PublicId, ct);
        return reglas.Select(r =>
        {
            var c = cuentas[r.AccountId];
            return new ReglaDeModuloDto(r.PublicId, modulo.Codigo, r.FactType, r.Role, DimensionesDeReglaDeModuloDto.De(r), c.PublicId, c.Code, c.Name,
                r.CostCenterId is { } cc ? centros.GetValueOrDefault(cc) : null, r.ValidFrom, r.ValidTo, r.SpecificityWeight, r.Notes);
        }).ToList();
    }
}

// ---------------------------------------------------------------------------------------------------------------- plantilla --

/// <summary>
/// La plantilla de la matriz (contabilidad.md §6; FR-143): las filas sugeridas <b>sin cuenta</b> según la configuración real de la cooperativa —una por caja y por
/// medio de cada punto en <c>Caja</c>, por naturaleza en el puente del módulo, por concepto, por cuenta propia, por clase de orden—; en los roles de resultado, una
/// por hecho. No se siembra ninguna regla con cuenta (P12). (nuevo)
/// </summary>
public sealed record GetModulePostingTemplateQuery(string Module) : IRequest<Result<IReadOnlyList<FilaDePlantillaDeModuloDto>>>;

public sealed class GetModulePostingTemplateQueryValidator : AbstractValidator<GetModulePostingTemplateQuery>
{
    public GetModulePostingTemplateQueryValidator() => RuleFor(x => x.Module).NotEmpty().EsModuloDeLaMatriz();
}

public sealed class GetModulePostingTemplateQueryHandler(IDimensionesDeRecaudo? recaudo = null, IDimensionesDeTesoreria? tesoreria = null)
    : IRequestHandler<GetModulePostingTemplateQuery, Result<IReadOnlyList<FilaDePlantillaDeModuloDto>>>
{
    public async Task<Result<IReadOnlyList<FilaDePlantillaDeModuloDto>>> Handle(GetModulePostingTemplateQuery request, CancellationToken ct)
    {
        var modulo = MatricesDeRecaudoYTesoreria.De(request.Module)!;
        IReadOnlyList<DimensionDeMatrizDto> valores = modulo.Codigo == ModuloContable.Recaudo
            ? recaudo is null ? [] : await recaudo.DimensionesAsync(ct)
            : tesoreria is null ? [] : await tesoreria.DimensionesAsync(ct);
        return Result.Success(PlantillaDeLaMatriz.Filas(modulo, valores));
    }
}

/// <summary>Las filas de la plantilla, puras: el mismo cálculo para la pantalla, el archivo y la completitud. (nuevo)</summary>
public static class PlantillaDeLaMatriz
{
    /// <summary>Las naturalezas del puente del módulo que la plantilla sugiere (contabilidad.md §2).</summary>
    public static readonly IReadOnlyList<string> Naturalezas = ["Loan", "Deposit", "Contribution"];

    public static IReadOnlyList<FilaDePlantillaDeModuloDto> Filas(ModuloDeLaMatriz modulo, IReadOnlyList<DimensionDeMatrizDto> valores)
    {
        var filas = new List<FilaDePlantillaDeModuloDto>();
        foreach (var rol in modulo.Roles.Where(r => !r.PorVinculo))
        {
            var hechos = modulo.Hechos.Where(h => h.RolesPosibles.Contains(rol.Codigo)).Select(h => h.Tipo).ToList();
            if (hechos.Count == 0) continue;
            var combinaciones = Combinaciones(rol, valores);
            if (rol.DeResultado)
            {
                foreach (var h in hechos)
                    foreach (var (dim, texto) in combinaciones)
                        filas.Add(new FilaDePlantillaDeModuloDto(h, rol.Codigo, rol.Nombre, dim, $"{rol.Nombre} de «{modulo.Hecho(h)!.Nombre}»{texto}", [h]));
                continue;
            }
            foreach (var (dim, texto) in combinaciones)
                filas.Add(new FilaDePlantillaDeModuloDto(ReglaContableDeModulo.Cualquiera, rol.Codigo, rol.Nombre, dim, rol.Nombre + texto, hechos));
        }
        return filas;
    }

    private static IReadOnlyList<(DimensionesDeReglaDeModuloDto Dim, string Texto)> Combinaciones(RolDeLaMatriz rol, IReadOnlyList<DimensionDeMatrizDto> valores)
    {
        List<DimensionDeMatrizDto> De(string dimension) => valores.Where(v => v.Dimension == dimension).GroupBy(v => v.Code).Select(g => g.First()).ToList();
        if (rol.Codigo == Common.Integration.Contracts.Receipts.RolesDeRecaudo.PuenteDelModulo)
            return Naturalezas.Select(n => (new DimensionesDeReglaDeModuloDto(TargetModule: ModulosDeOrigen.Cartera, TargetNature: n), $" · Cartera · {n}")).ToList();
        foreach (var dimension in rol.Dimensiones)
        {
            var lista = De(dimension);
            if (lista.Count == 0) continue;
            return lista.Select(v => (dimension switch
            {
                MatricesDeRecaudoYTesoreria.Caja => new DimensionesDeReglaDeModuloDto(CashBoxCode: v.Code),
                MatricesDeRecaudoYTesoreria.Punto => new DimensionesDeReglaDeModuloDto(CollectionPointCode: v.Code),
                MatricesDeRecaudoYTesoreria.Oficina => new DimensionesDeReglaDeModuloDto(BranchCode: v.Code),
                MatricesDeRecaudoYTesoreria.Medio => new DimensionesDeReglaDeModuloDto(MeansCode: v.Code),
                MatricesDeRecaudoYTesoreria.Concepto => new DimensionesDeReglaDeModuloDto(MiscConceptCode: v.Code),
                MatricesDeRecaudoYTesoreria.CuentaPropia => new DimensionesDeReglaDeModuloDto(OwnBankAccountCode: v.Code),
                MatricesDeRecaudoYTesoreria.OrigenDeLaOrden => new DimensionesDeReglaDeModuloDto(OrderOrigin: v.Code),
                MatricesDeRecaudoYTesoreria.ClaseDeOrden => new DimensionesDeReglaDeModuloDto(OrderKind: v.Code),
                _ => new DimensionesDeReglaDeModuloDto(),
            }, $" · {v.Name} ({v.Code})")).ToList();
        }
        return [(new DimensionesDeReglaDeModuloDto(), string.Empty)];
    }

    /// <summary>La plantilla como tabla para descargarla (Excel): una fila por combinación con la columna de cuenta vacía.</summary>
    public static TablaExportable Tabla(ModuloDeLaMatriz modulo, IReadOnlyList<FilaDePlantillaDeModuloDto> filas)
    {
        var columnas = new List<ColumnaExportable> { new("Hecho"), new("Rol"), new("Combinación"), new("Hechos que la usan"), new("Cuenta"), new("Centro de costo"), new("Notas") };
        var datos = filas.Select(f => new FilaExportable(new object?[]
        {
            f.FactType, f.Role, string.Join(" · ", f.Dimensions.ComoDiccionario().Select(d => $"{d.Key}={d.Value}")), string.Join(", ", f.Facts), null, null, f.Description,
        })).ToList();
        return new TablaExportable($"Plantilla de la matriz contable de {modulo.Nombre}", "Filas sugeridas sin cuenta: la contadora completa la cuenta de cada una.",
            columnas, datos, null, ["No se siembra ninguna regla con cuenta (P12)."]);
    }
}

// --------------------------------------------------------------------------------------------------------------- completitud --

/// <summary>
/// La completitud de la matriz (FR-227; condición del go/no-go al 100 %): cada fila de la plantilla se resuelve con la matriz vigente a la fecha —completa, falta
/// o empate— y los roles por vínculo se informan aparte. Con el modo de paso en «no pasa» lo dice y no cuenta. (nuevo)
/// </summary>
public sealed record GetModuleCompletenessQuery(string Module, DateOnly? AsOf = null) : IRequest<Result<CompletitudDeModuloDto>>;

public sealed class GetModuleCompletenessQueryValidator : AbstractValidator<GetModuleCompletenessQuery>
{
    public GetModuleCompletenessQueryValidator() => RuleFor(x => x.Module).NotEmpty().EsModuloDeLaMatriz();
}

public sealed class GetModuleCompletenessQueryHandler(ISender sender, ResolutorDeLaMatriz resolutor, ILectorDeParametros lector, IDateTimeService reloj)
    : IRequestHandler<GetModuleCompletenessQuery, Result<CompletitudDeModuloDto>>
{
    public async Task<Result<CompletitudDeModuloDto>> Handle(GetModuleCompletenessQuery request, CancellationToken ct)
    {
        var modulo = MatricesDeRecaudoYTesoreria.De(request.Module)!;
        var fecha = request.AsOf ?? reloj.TodayUtc;
        var modo = await lector.LeerAsync(modulo.ModuloDeParametros, ParametrosDeRecaudoYTesoreria.ContabilidadModoDePaso, fecha, ct: ct);
        var noPasa = modo.IsSuccess && modo.Value.Texto == EvaluacionPreviaDeModulo.NoPasa;
        var plantilla = await sender.Send(new GetModulePostingTemplateQuery(modulo.Codigo), ct);
        if (plantilla.IsFailure) return Result.Failure<CompletitudDeModuloDto>(plantilla.Error);
        var matriz = await resolutor.CargarAsync(modulo, fecha, fecha, ct);
        var filas = new List<FilaDeCompletitudDeModuloDto>();
        foreach (var f in plantilla.Value)
        {
            foreach (var tipo in f.Facts)
            {
                var hecho = modulo.Hecho(tipo)!;
                var linea = new LineaDeHecho(f.Role, true, 1m, null, null, f.Dimensions.ComoDiccionario(), null);
                var r = matriz.Resolver(tipo, linea, fecha);
                var estado = r.IsSuccess ? "Complete" : r.Error.Code.EndsWith(".Ambiguous", StringComparison.Ordinal) ? "Ambiguous" : "Missing";
                filas.Add(new FilaDeCompletitudDeModuloDto(tipo, hecho.Nombre, f.Role, f.RoleName, f.Dimensions, estado, r.IsSuccess ? r.Value!.Cuenta.Code : null,
                    r.IsSuccess ? null : r.Error.Message));
            }
        }
        foreach (var rol in modulo.Roles.Where(r => r.PorVinculo))
            foreach (var h in modulo.Hechos.Where(h => h.RolesPosibles.Contains(rol.Codigo)))
                filas.Add(new FilaDeCompletitudDeModuloDto(h.Tipo, h.Nombre, rol.Codigo, rol.Nombre, new DimensionesDeReglaDeModuloDto(), "ByLink", null,
                    "Lo resuelve el vínculo de cada cuenta bancaria propia con su cuenta contable (Tesorería, cuentas propias)."));
        var contables = filas.Where(x => x.Status != "ByLink").ToList();
        var completas = contables.Count(x => x.Status == "Complete");
        var porcentaje = contables.Count == 0 ? 100m : Math.Round(100m * completas / contables.Count, 2);
        return Result.Success(new CompletitudDeModuloDto(modulo.Codigo, fecha, noPasa, porcentaje, completas, contables.Count, filas));
    }
}

// ------------------------------------------------------------------------------------------------------ tipos de comprobante --

public sealed record ListModuleVoucherMappingsQuery(string Module) : IRequest<Result<IReadOnlyList<MapeoDeModuloDto>>>;

public sealed class ListModuleVoucherMappingsQueryValidator : AbstractValidator<ListModuleVoucherMappingsQuery>
{
    public ListModuleVoucherMappingsQueryValidator() => RuleFor(x => x.Module).NotEmpty().EsModuloDeLaMatriz();
}

public sealed class ListModuleVoucherMappingsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListModuleVoucherMappingsQuery, Result<IReadOnlyList<MapeoDeModuloDto>>>
{
    public async Task<Result<IReadOnlyList<MapeoDeModuloDto>>> Handle(ListModuleVoucherMappingsQuery request, CancellationToken ct)
    {
        var modulo = MatricesDeRecaudoYTesoreria.De(request.Module)!;
        var filas = modulo.Codigo == ModuloContable.Recaudo
            ? await db.ReceiptsVoucherMappings.AsNoTracking().Select(m => new
                { m.FactType, Codigo = m.VoucherType!.Code, Id = m.VoucherType.PublicId, Cruce = m.CrossDocumentType == null ? null : m.CrossDocumentType.Code, m.IsSeeded, m.ValidatedAt })
                .ToListAsync(ct)
            : await db.TreasuryVoucherMappings.AsNoTracking().Select(m => new
                { m.FactType, Codigo = m.VoucherType!.Code, Id = m.VoucherType.PublicId, Cruce = m.CrossDocumentType == null ? null : m.CrossDocumentType.Code, m.IsSeeded, m.ValidatedAt })
                .ToListAsync(ct);
        var porHecho = filas.ToDictionary(f => f.FactType, StringComparer.Ordinal);
        return Result.Success<IReadOnlyList<MapeoDeModuloDto>>(modulo.Hechos.Select(h => porHecho.TryGetValue(h.Tipo, out var f)
            ? new MapeoDeModuloDto(h.Tipo, h.Nombre, h.ComprobanteSugerido, f.Codigo, f.Id, f.Cruce, f.IsSeeded, f.ValidatedAt)
            : new MapeoDeModuloDto(h.Tipo, h.Nombre, h.ComprobanteSugerido, null, null, null, false, null)).ToList());
    }
}

public sealed record SetModuleVoucherMappingCommand(string Module, string FactType, Guid VoucherTypePublicId, Guid? CrossDocumentTypePublicId, string Reason)
    : IRequest<Result<MapeoDeModuloDto>>, IConMotivo;

public sealed class SetModuleVoucherMappingCommandValidator : ValidadorConMotivo<SetModuleVoucherMappingCommand>
{
    public SetModuleVoucherMappingCommandValidator()
    {
        RuleFor(x => x.Module).NotEmpty().EsModuloDeLaMatriz();
        RuleFor(x => x.FactType).NotEmpty().MaximumLength(60);
        RuleFor(x => x.VoucherTypePublicId).NotEqual(Guid.Empty);
    }
}

public sealed class SetModuleVoucherMappingCommandHandler(IApplicationDbContext db, IDateTimeService reloj, Common.Interfaces.Security.IActorActual actorActual)
    : IRequestHandler<SetModuleVoucherMappingCommand, Result<MapeoDeModuloDto>>
{
    public async Task<Result<MapeoDeModuloDto>> Handle(SetModuleVoucherMappingCommand request, CancellationToken ct)
    {
        var modulo = MatricesDeRecaudoYTesoreria.De(request.Module)!;
        var hecho = modulo.Hecho(request.FactType.Trim());
        if (hecho is null) return Result.Failure<MapeoDeModuloDto>(ErroresDeLaMatriz.HechoDesconocido(modulo, request.FactType));
        var tipo = await db.VoucherTypes.AsNoTracking().FirstOrDefaultAsync(v => v.PublicId == request.VoucherTypePublicId, ct);
        if (tipo is null || tipo.Usage != VoucherUsage.Module || !string.Equals(tipo.ModuleCode, modulo.Codigo, StringComparison.OrdinalIgnoreCase) || !tipo.IsActive)
            return Result.Failure<MapeoDeModuloDto>(ErroresDeLaMatriz.ComprobanteNoElegible(modulo, tipo?.Code ?? request.VoucherTypePublicId.ToString()));
        int? cruce = null;
        string? codigoDeCruce = null;
        if (request.CrossDocumentTypePublicId is { } c)
        {
            var fila = await db.CrossDocumentTypes.AsNoTracking().Where(x => x.PublicId == c).Select(x => new { x.Id, x.Code }).FirstOrDefaultAsync(ct);
            if (fila is null) return Result.Failure<MapeoDeModuloDto>(AccountingErrors.CrossDocumentTypeNotFound);
            cruce = fila.Id;
            codigoDeCruce = fila.Code;
        }
        var actor = await actorActual.ObtenerAsync(ct);
        MapeoDeComprobanteDeModulo? mapeo = modulo.Codigo == ModuloContable.Recaudo
            ? await db.ReceiptsVoucherMappings.FirstOrDefaultAsync(m => m.FactType == hecho.Tipo, ct)
            : await db.TreasuryVoucherMappings.FirstOrDefaultAsync(m => m.FactType == hecho.Tipo, ct);
        if (mapeo is null)
        {
            mapeo = modulo.Codigo == ModuloContable.Recaudo ? new ReceiptsVoucherMapping { FactType = hecho.Tipo } : new TreasuryVoucherMapping { FactType = hecho.Tipo };
            mapeo.CreatedAt = reloj.UtcNow;
            if (mapeo is ReceiptsVoucherMapping r) db.ReceiptsVoucherMappings.Add(r);
            else db.TreasuryVoucherMappings.Add((TreasuryVoucherMapping)mapeo);
        }
        mapeo.VoucherTypeId = tipo.Id;
        mapeo.CrossDocumentTypeId = cruce;
        mapeo.ValidatedAt = reloj.UtcNow;
        mapeo.ValidatedBy = actor.Name;
        await db.SaveChangesAsync(ct);
        return Result.Success(new MapeoDeModuloDto(hecho.Tipo, hecho.Nombre, hecho.ComprobanteSugerido, tipo.Code, tipo.PublicId, codigoDeCruce, mapeo.IsSeeded, mapeo.ValidatedAt));
    }
}
