using FluentValidation;
using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Accounting.Lending.Consultas;

/// <summary>Una combinación en uso sin regla vigente (§3.7). (nuevo)</summary>
public sealed record ReglaDeCarteraFaltanteDto(
    string Operation,
    string Role,
    DimensionesDeReglaDeCarteraDto Dimensions,
    IReadOnlyList<string> CreditLines,
    DateOnly? LastUsedAt,
    string? Nature = null)
{
    /// <summary>Feature 014 (T144): la cuenta CUIF sugerida de una combinación exigida de captaciones (informativa, pendiente de la contadora).</summary>
    public string? SuggestedAccount { get; init; }
}

/// <summary>Una clase de recaudador registrada sin regla <c>RecaudoPorAplicar</c> vigente (§3.7; <c>OwnLoanPayoff</c> siempre, T91). (nuevo)</summary>
public sealed record RecaudadorSinPuenteDto(string CollectorClass, IReadOnlyList<string> Collectors);

/// <summary>Una regla vigente o futura cuya cuenta dejó de ser elegible para CAR. (nuevo)</summary>
public sealed record ReglaDeCarteraNoElegibleDto(Guid RulePublicId, string Operation, string Role, string AccountCode, string Reason);

/// <summary>Un hecho sin tipo de comprobante mapeado, o mapeado a uno que ya no sirve. (nuevo)</summary>
public sealed record HechoSinMapeoDto(string Operation, string? CollectorClass, string Code, string Message);

/// <summary>Un aviso de la completitud: no estorba contabilizar, pero conviene revisarlo. (nuevo)</summary>
public sealed record AvisoDeCompletitudDeCarteraDto(string Code, string Message, string? AccountCode, Guid? RulePublicId);

public sealed record ResumenDeCompletitudDeCarteraDto(int Total, IReadOnlyDictionary<string, int> PorTipo);

/// <summary><c>GET /api/accounting/lending/completeness?date=</c> (api.md §23.3). (nuevo)</summary>
public sealed record CompletitudDeLaMatrizDeCarteraDto(
    DateOnly Date,
    IReadOnlyList<ReglaDeCarteraFaltanteDto> MissingRules,
    IReadOnlyList<RecaudadorSinPuenteDto> CollectorsWithoutBridge,
    IReadOnlyList<ReglaDeCarteraNoElegibleDto> IneligibleRules,
    IReadOnlyList<HechoSinMapeoDto> UnmappedOperations,
    ResumenDeCompletitudDeCarteraDto Summary,
    IReadOnlyList<AvisoDeCompletitudDeCarteraDto> Warnings);

/// <summary>
/// La completitud de la matriz de Cartera a una fecha (feature 013, T0643; contracts/contabilidad.md §3.7; api.md §23.3). Recibe de Cartera,
/// por <see cref="IDimensionesDeCartera"/>, las combinaciones en uso (Contabilidad no lee <c>LND_</c>) y lee de la plataforma los recaudadores
/// registrados (<see cref="IRegistroDeIntegracion"/>). Devuelve:
/// <list type="bullet">
///   <item>las combinaciones en uso y sus roles sin regla vigente que case (con cualquier hecho: la del hecho fijado también la cubre);</item>
///   <item>las clases de recaudador registradas sin <c>RecaudoPorAplicar</c> vigente; <c>OwnLoanPayoff</c> se pide siempre (es la propia
///         Cartera y no está en el registro, T91);</item>
///   <item>las reglas vigentes o futuras cuya cuenta dejó de ser elegible (inactiva, de agrupación o sin CAR habilitado);</item>
///   <item>los hechos de negocio sin tipo de comprobante mapeado o mapeados a uno que ya no es de Cartera o está inactivo;</item>
///   <item>avisos: puentes cuya cuenta exige documento cruce (no saldarán contra el documento del otro lado, §2.2); cuentas de la causación
///         resumida que exigen tercero o cruce (una línea por crédito y por día, C5); naturaleza de la cuenta distinta de la del rol.</item>
/// </list>
/// Los conjuntos de cuentas y los saldos por combinación son de <c>GetLendingAccountBalancesQuery</c> (T0649). (nuevo)
/// </summary>
public sealed record GetLendingRulesCompletenessQuery(DateOnly Date) : IRequest<Result<CompletitudDeLaMatrizDeCarteraDto>>;

public sealed class GetLendingRulesCompletenessQueryValidator : AbstractValidator<GetLendingRulesCompletenessQuery>
{
    public GetLendingRulesCompletenessQueryValidator() =>
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Indique la fecha.");
}

public sealed class GetLendingRulesCompletenessQueryHandler(
    IApplicationDbContext db, IDimensionesDeCartera dimensiones, IRegistroDeIntegracion registro, TiposDeComprobanteDeCartera tipos)
    : IRequestHandler<GetLendingRulesCompletenessQuery, Result<CompletitudDeLaMatrizDeCarteraDto>>
{
    public const string AvisoPuenteConCruce = "Accounting.LendingRule.BridgeRequiresCrossDocument";
    public const string AvisoCausacionPorCredito = "Accounting.LendingRule.SummarizedAccrualPerLoan";

    /// <summary>Feature 014 (T144): la causación resumida de captaciones llevará una línea por cuenta en una cuenta que exige tercero o cruce.</summary>
    public const string AvisoCausacionPorCuenta = "Accounting.LendingRule.SummarizedAccrualPerAccount";

    /// <summary>Feature 014 (T144; contabilidad.md §4.3): la marca de la cuenta (exenta o embargada) va a otra cuenta que la estándar (informativo).</summary>
    public const string AvisoMarcaEnOtraCuenta = "Accounting.LendingRule.FlagInOtherAccount";

    /// <summary>Feature 014 (T144): el puente de Tesorería (recaudador <c>Treasury</c>) sin regla, con captaciones o aportes.</summary>
    public const string AvisoSinPuenteDeTesoreria = "Accounting.LendingRule.TreasuryBridgeMissing";

    /// <summary>Los roles de la causación de captaciones (sale resumida por día, D24).</summary>
    private static readonly HashSet<string> RolesDeLaCausacionDeCaptaciones = new(
        new[] { Common.Integration.Contracts.Lending.Deposits.InteresDeCaptacionCausadoV1.Type, Common.Integration.Contracts.Lending.Deposits.AjusteDeCausacionDeCaptacionV1.Type }
            .SelectMany(t => OperacionesDeCartera.Buscar(t)!.Roles),
        StringComparer.Ordinal);

    /// <summary>Los roles que mueve la causación diaria, que sale resumida (C5).</summary>
    private static readonly HashSet<string> RolesDeLaCausacion = new(
        new[] { InteresCausado, MoraCausada, SeguroCausado, AjusteDeCausacion, CorreccionMonetaria, RepartoCorregido }
            .SelectMany(t => OperacionesDeCartera.Buscar(t)!.Roles),
        StringComparer.Ordinal);

    private const string InteresCausado = Common.Integration.Contracts.Lending.InteresCausadoV1.Type;
    private const string MoraCausada = Common.Integration.Contracts.Lending.MoraCausadaV1.Type;
    private const string SeguroCausado = Common.Integration.Contracts.Lending.SeguroCausadoV1.Type;
    private const string AjusteDeCausacion = Common.Integration.Contracts.Lending.AjusteDeCausacionPorFechaValorV1.Type;

    /// <summary>K6b (T1056): la corrección monetaria UVR sale resumida con la causación del día.</summary>
    private const string CorreccionMonetaria = Common.Integration.Contracts.Lending.CorreccionMonetariaUvrCausadaV1.Type;

    /// <summary>K6b (decisión del dueño del 2026-10-17): el reparto de un pago corregido por la UVR sale resumido con el ajuste de la corrida.</summary>
    private const string RepartoCorregido = Common.Integration.Contracts.Lending.PagoRedistribuidoPorValorNacionalV1.Type;

    public async Task<Result<CompletitudDeLaMatrizDeCarteraDto>> Handle(GetLendingRulesCompletenessQuery request, CancellationToken ct)
    {
        var fecha = request.Date;
        // Vigentes a la fecha y las que empiezan después: una cuenta no elegible en una regla futura también estorba.
        var reglas = await db.LendingPostingRules.AsNoTracking()
            .Where(r => !r.IsDeleted && (r.ValidTo == null || r.ValidTo >= fecha)).ToListAsync(ct);
        var vigentes = reglas.Where(r => r.VigenteEn(fecha)).ToList();
        var idsDeCuenta = reglas.Select(r => r.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(a => idsDeCuenta.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var sucursales = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).ToDictionaryAsync(b => b.PublicId, b => b.Id, ct);
        var combinaciones = await dimensiones.CombinacionesEnUsoAsync(fecha, ct);

        var faltantes = Faltantes(combinaciones, vigentes, sucursales);
        // Feature 014, US8 (T144; contabilidad.md §4.3): por cada naturaleza de captaciones con líneas, las combinaciones exigidas sin regla.
        var catalogo = await dimensiones.CatalogoAsync(ct);
        faltantes.AddRange(FaltantesDeCaptaciones(catalogo.CreditLines, vigentes));
        var recaudadores = await RecaudadoresSinPuenteAsync(vigentes, fecha, ct);
        var noElegibles = reglas
            .Select(r => (Regla: r, Reparo: AccountEligibility.Reparo(cuentas.GetValueOrDefault(r.AccountId), ModuloContable.Cartera)))
            .Where(x => x.Reparo is not null)
            .Select(x => new ReglaDeCarteraNoElegibleDto(x.Regla.PublicId, x.Regla.Operation, x.Regla.Role,
                cuentas.GetValueOrDefault(x.Regla.AccountId)?.Code ?? string.Empty, x.Reparo!))
            .ToList();
        var sinMapeo = await SinMapeoAsync(ct);
        var avisos = Avisos(reglas, cuentas).Concat(AvisosDeCaptaciones(catalogo.CreditLines, vigentes, cuentas)).ToList();

        var porTipo = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["missingRules"] = faltantes.Count,
            ["collectorsWithoutBridge"] = recaudadores.Count,
            ["ineligibleRules"] = noElegibles.Count,
            ["unmappedOperations"] = sinMapeo.Count,
        };
        return Result.Success(new CompletitudDeLaMatrizDeCarteraDto(fecha, faltantes, recaudadores, noElegibles, sinMapeo,
            new ResumenDeCompletitudDeCarteraDto(porTipo.Values.Sum(), porTipo), avisos));
    }

    /// <summary>
    /// Las combinaciones en uso sin regla vigente que case. Una combinación sin categoría o sin concepto busca la regla que no los fija. Se
    /// agrupan por rol y dimensiones: la misma combinación en varias líneas sale una vez, con sus líneas.
    /// </summary>
    public static List<ReglaDeCarteraFaltanteDto> Faltantes(
        IReadOnlyList<CombinacionDeCarteraEnUsoDto> combinaciones, IReadOnlyList<LendingPostingRule> vigentes, IReadOnlyDictionary<Guid, int> sucursales)
    {
        var faltantes = new Dictionary<string, (ReglaDeCarteraFaltanteDto Dto, SortedSet<string> Lineas)>(StringComparer.Ordinal);
        foreach (var c in combinaciones)
        {
            if (R.Buscar(c.Role) is null) continue;
            int? sucursal = sucursales.TryGetValue(c.BranchPublicId, out var id) ? id : null;
            var valores = new ValoresDeCartera(c.CreditLineCode, c.ModalityCode, c.SubsegmentCode, c.RiskCategory, c.ConceptCode, c.IsEmployee,
                c.InGracePeriod, null, c.Restructured, null, null, c.BranchPublicId);
            if (vigentes.Any(r => r.Role == c.Role && MatrizVigenteDeCartera.Coincide(r, valores, sucursal))) continue;

            var clave = string.Join('|', c.Role, c.ModalityCode, c.SubsegmentCode, c.RiskCategory, c.ConceptCode, c.IsEmployee, c.InGracePeriod,
                c.Restructured, c.BranchPublicId);
            if (!faltantes.TryGetValue(clave, out var f))
            {
                f = (new ReglaDeCarteraFaltanteDto(LendingPostingRule.Cualquiera, c.Role,
                        new DimensionesDeReglaDeCarteraDto(null, c.ModalityCode, c.SubsegmentCode, c.RiskCategory, c.ConceptCode, c.IsEmployee,
                            c.InGracePeriod, null, c.Restructured, BranchPublicId: c.BranchPublicId),
                        [], c.LastUsedAt),
                    new SortedSet<string>(StringComparer.Ordinal));
                faltantes[clave] = f;
            }
            if (c.CreditLineCode is { } linea) f.Lineas.Add(linea);
            if (f.Dto.LastUsedAt is null || c.LastUsedAt > f.Dto.LastUsedAt)
                faltantes[clave] = f = (f.Dto with { LastUsedAt = c.LastUsedAt }, f.Lineas);
        }
        return faltantes.Values.Select(f => f.Dto with { CreditLines = f.Lineas.ToList() }).ToList();
    }

    /// <summary>
    /// Las combinaciones exigidas de captaciones y aportes sin regla vigente que case (feature 014, T144): las filas sugeridas exigidas
    /// (<see cref="Plantilla.SugerenciasDeCaptaciones"/>) de las naturalezas y clases con líneas, buscadas con el código de cada línea (una regla por línea
    /// también la cubre); la combinación sale una vez con las líneas que la tienen sin regla.
    /// </summary>
    public static IReadOnlyList<ReglaDeCarteraFaltanteDto> FaltantesDeCaptaciones(IReadOnlyList<LineaDeCarteraDto> lineas, IReadOnlyList<LendingPostingRule> vigentes)
    {
        var resultado = new List<ReglaDeCarteraFaltanteDto>();
        foreach (var s in Plantilla.SugerenciasDeCaptaciones.De(lineas).Where(s => s.Exigida))
        {
            var naturaleza = s.NaturalezaDeLaFila;
            var deLaFila = lineas.Where(l => l.LineNature == naturaleza
                    && (s.Clase is null || string.Equals(l.DepositClass ?? nameof(Domain.Enums.Lending.DepositClass.Demand), s.Clase, StringComparison.Ordinal)))
                .Select(l => l.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            var sinRegla = deLaFila
                .Where(codigo => !vigentes.Any(r => r.Role == s.Rol && MatrizVigenteDeCartera.Coincide(r, s.Valores with { CreditLineCode = codigo }, null)))
                .ToList();
            if (sinRegla.Count == 0) continue;
            resultado.Add(new ReglaDeCarteraFaltanteDto(LendingPostingRule.Cualquiera, s.Rol,
                new DimensionesDeReglaDeCarteraDto(CollectorClass: s.ClaseDeRecaudador, ReasonCode: s.Motivo, LineNature: s.Naturaleza, DepositClass: s.Clase,
                    IssueTermBand: s.Plazo, ContributionClass: s.ClaseDeAporte, AccountFlag: s.Marca),
                sinRegla, null, naturaleza) { SuggestedAccount = s.CuentaSugerida });
        }
        return resultado;
    }

    /// <summary>
    /// Los avisos de captaciones (contabilidad.md §4.3): la devolución o el embargo por pagar que exigen documento cruce (el pago de Tesorería no saldará
    /// contra el número de la cuenta), la causación resumida que irá una línea por cuenta, la marca exenta o embargada en otra cuenta que la estándar
    /// (informativo) y el puente de Tesorería sin regla.
    /// </summary>
    private static IEnumerable<AvisoDeCompletitudDeCarteraDto> AvisosDeCaptaciones(IReadOnlyList<LineaDeCarteraDto> lineas, IReadOnlyList<LendingPostingRule> vigentes,
        IReadOnlyDictionary<int, ChartOfAccount> cuentas)
    {
        var conCaptaciones = lineas.Any(l => l.LineNature is nameof(Domain.Enums.Lending.LineNature.Deposit) or nameof(Domain.Enums.Lending.LineNature.Contribution));
        foreach (var r in vigentes)
        {
            if (!cuentas.TryGetValue(r.AccountId, out var cuenta) || R.Buscar(r.Role) is not { } rol || !R.EsDeCaptaciones(rol.Codigo)) continue;
            if (rol.Codigo is R.DevolucionPorPagar or R.EmbargosPorConsignar && cuenta.RequiresCrossDocument)
                yield return new AvisoDeCompletitudDeCarteraDto(AvisoPuenteConCruce,
                    $"La cuenta {cuenta.Code} del puente {rol.Codigo} exige documento cruce: Tesorería paga con su propio documento y no saldará contra el número de la cuenta.",
                    cuenta.Code, r.PublicId);
            if (RolesDeLaCausacionDeCaptaciones.Contains(rol.Codigo) && (cuenta.RequiresThirdParty || cuenta.RequiresCrossDocument))
                yield return new AvisoDeCompletitudDeCarteraDto(AvisoCausacionPorCuenta,
                    $"La cuenta {cuenta.Code} ({rol.Codigo}) exige tercero o documento cruce: la causación resumida de captaciones llevará una línea por cuenta y por día en ella.",
                    cuenta.Code, r.PublicId);
            if (rol.Codigo == R.CapitalDeCaptacion && r.AccountFlag is { } marca && marca != nameof(Domain.Enums.Lending.AccountFlag.Standard)
                && vigentes.FirstOrDefault(e => e.Role == R.CapitalDeCaptacion && e.AccountFlag == nameof(Domain.Enums.Lending.AccountFlag.Standard)
                                                && e.DepositClass == r.DepositClass && e.IssueTermBand == r.IssueTermBand && e.CreditLineCode == r.CreditLineCode
                                                && e.BranchId == r.BranchId) is { } estandar
                && estandar.AccountId != r.AccountId)
                yield return new AvisoDeCompletitudDeCarteraDto(AvisoMarcaEnOtraCuenta,
                    $"El capital con la marca {marca} va a {cuenta.Code}, otra cuenta que el estándar: al marcar o desmarcar una cuenta su saldo se reclasifica entre las dos.",
                    cuenta.Code, r.PublicId);
        }
        if (conCaptaciones && !vigentes.Any(r => r.Role == R.RecaudoPorAplicar && r.CollectorClass == nameof(ClaseDeRecaudador.Treasury)))
            yield return new AvisoDeCompletitudDeCarteraDto(AvisoSinPuenteDeTesoreria,
                "No hay regla para el puente de Tesorería (RecaudoPorAplicar con la clase Treasury): las transferencias que Tesorería consigne en cuentas no se contabilizarán.",
                null, null);
    }

    private async Task<List<RecaudadorSinPuenteDto>> RecaudadoresSinPuenteAsync(IReadOnlyList<LendingPostingRule> vigentes, DateOnly fecha, CancellationToken ct)
    {
        var participantes = await registro.QueCuentanAsync(fecha, ct);
        var porClase = participantes
            .Where(p => p.Role == IntegrationParticipantRole.ObligationsCollector && Enum.IsDefined(typeof(ClaseDeRecaudador), (int)p.Class))
            .GroupBy(p => ((ClaseDeRecaudador)(int)p.Class).ToString(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Code).Order(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        // T91: la recogida de créditos propios es la propia Cartera; no está en el registro y siempre pide su puente.
        porClase.TryAdd(nameof(ClaseDeRecaudador.OwnLoanPayoff), [ModulosDeOrigen.Cartera]);

        return porClase
            .Where(x => !vigentes.Any(r => r.Role == R.RecaudoPorAplicar && string.Equals(r.CollectorClass, x.Key, StringComparison.Ordinal)))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new RecaudadorSinPuenteDto(x.Key, x.Value))
            .ToList();
    }

    private async Task<List<HechoSinMapeoDto>> SinMapeoAsync(CancellationToken ct)
    {
        var mapeos = await tipos.CargarAsync(ct);
        return OperacionesDeCartera.DeNegocio
            .Select(o => (Operacion: o.Codigo, Resultado: mapeos.Resolver(o.Codigo, null)))
            .Where(x => x.Resultado.IsFailure)
            .Select(x => new HechoSinMapeoDto(x.Operacion, null, x.Resultado.Error.Code, x.Resultado.Error.Message))
            .ToList();
    }

    private static IEnumerable<AvisoDeCompletitudDeCarteraDto> Avisos(IReadOnlyList<LendingPostingRule> reglas, IReadOnlyDictionary<int, ChartOfAccount> cuentas)
    {
        foreach (var r in reglas)
        {
            if (!cuentas.TryGetValue(r.AccountId, out var cuenta) || R.Buscar(r.Role) is not { } rol) continue;
            if (rol.Codigo is R.RecaudoPorAplicar or R.DesembolsoPorGirar && cuenta.RequiresCrossDocument)
                yield return new AvisoDeCompletitudDeCarteraDto(AvisoPuenteConCruce,
                    $"La cuenta {cuenta.Code} del puente {rol.Codigo} exige documento cruce: el otro lado (Recaudo, Nómina, Tesorería) contabiliza con su propio documento y no saldará contra el número del crédito.",
                    cuenta.Code, r.PublicId);
            if (RolesDeLaCausacion.Contains(rol.Codigo) && (cuenta.RequiresThirdParty || cuenta.RequiresCrossDocument))
                yield return new AvisoDeCompletitudDeCarteraDto(AvisoCausacionPorCredito,
                    $"La cuenta {cuenta.Code} ({rol.Codigo}) exige tercero o documento cruce: la causación resumida del día llevará una línea por crédito en ella.",
                    cuenta.Code, r.PublicId);
            if (cuenta.Nature != rol.Naturaleza)
                yield return new AvisoDeCompletitudDeCarteraDto(ReglasDeLaMatrizDeCartera.AvisoNaturalezaDistinta,
                    $"La cuenta {cuenta.Code} no tiene la naturaleza del rol {rol.Codigo}.", cuenta.Code, r.PublicId);
        }
    }
}
