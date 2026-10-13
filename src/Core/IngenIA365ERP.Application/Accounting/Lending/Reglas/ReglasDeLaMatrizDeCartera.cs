using System.Globalization;
using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using Microsoft.EntityFrameworkCore;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Accounting.Lending.Reglas;

/// <summary>
/// Una regla de Cartera tal como se quiere guardar, con la sucursal y el centro ya traducidos a <c>Id</c> y los códigos como llegaron
/// (vacío o <c>*</c> = cualquiera). La construyen los comandos de alta y versión y la importación. (nuevo)
/// </summary>
public sealed record ReglaPropuestaDeCartera(
    string Operation,
    string Role,
    int AccountId,
    DateOnly ValidFrom,
    DateOnly? ValidTo = null,
    string? CreditLineCode = null,
    string? ModalityCode = null,
    string? SubsegmentCode = null,
    string? RiskCategory = null,
    string? ConceptCode = null,
    bool? IsEmployee = null,
    bool? InGracePeriod = null,
    bool? OnBehalfOfAssociate = null,
    bool? Restructured = null,
    string? CollectorClass = null,
    string? ReasonCode = null,
    int? BranchId = null,
    int? CostCenterId = null)
{
    /// <summary>La <c>DimensionKey</c> que tendría (la de la entidad).</summary>
    public string Clave => LendingPostingRule.ClaveDe(Operation, Role, CreditLineCode, ModalityCode, SubsegmentCode, RiskCategory, ConceptCode,
        IsEmployee, InGracePeriod, OnBehalfOfAssociate, Restructured, CollectorClass, ReasonCode, BranchId);

    /// <summary>Las dimensiones que fija (para comprobar las exigidas y las que el rol no admite).</summary>
    public IReadOnlySet<DimensionDeCartera> Presentes => ReglasDeLaMatrizDeCartera.Presentes(
        Operation, CreditLineCode, ModalityCode, SubsegmentCode, RiskCategory, ConceptCode, IsEmployee, InGracePeriod, OnBehalfOfAssociate,
        Restructured, CollectorClass, ReasonCode, BranchId is not null);

    public LendingPostingRule ComoRegla(string? notas) => new(
        Operation, Role, AccountId, ValidFrom, CreditLineCode, ModalityCode, SubsegmentCode, RiskCategory, ConceptCode, IsEmployee, InGracePeriod,
        OnBehalfOfAssociate, Restructured, CollectorClass, ReasonCode, BranchId, CostCenterId, ValidTo, notas);

    /// <summary>Vacío o <c>*</c> = no fija valor.</summary>
    internal static string? Valor(string? valor) =>
        string.IsNullOrWhiteSpace(valor) || valor.Trim() == LendingPostingRule.Cualquiera ? null : valor.Trim();
}

/// <summary>Un aviso al guardar una regla de Cartera: no impide guardarla (la naturaleza de la cuenta no es la del rol, §3.6). (nuevo)</summary>
public sealed record AvisoDeReglaDeCarteraDto(string Code, string Message);

/// <summary>
/// El resultado de <see cref="ReglasDeLaMatrizDeCartera.ValidarAsync"/>: los errores (la regla no se guarda), los avisos y las versiones
/// vivas de la clave que hay que cerrar la víspera de la nueva (<see cref="ACerrar"/>). (nuevo)
/// </summary>
public sealed record ValidacionDeReglaDeCartera(
    IReadOnlyList<Error> Errores, IReadOnlyList<AvisoDeReglaDeCarteraDto> Avisos, IReadOnlyList<LendingPostingRule> ACerrar)
{
    public bool EsValida => Errores.Count == 0;
}

/// <summary>
/// Lo que la validación de una regla lee una sola vez: los códigos de Cartera (por <see cref="IDimensionesDeCartera"/>). La importación lo
/// carga una vez para todo el archivo (plantillas.md §0.3: «nunca una consulta por fila»). (nuevo)
/// </summary>
public sealed class CatalogosDeLaMatrizDeCartera
{
    internal CatalogosDeLaMatrizDeCartera(CatalogoDeDimensionesDeCarteraDto dimensiones)
    {
        Dimensiones = dimensiones;
        Lineas = new HashSet<string>(dimensiones.CreditLines.Select(l => l.Code.Trim()), StringComparer.OrdinalIgnoreCase);
        Modalidades = dimensiones.Modalities.ToDictionary(m => m.Code.Trim(), m => (IReadOnlySet<string>)new HashSet<string>(m.SubsegmentCodes, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    public CatalogoDeDimensionesDeCarteraDto Dimensiones { get; }

    internal IReadOnlySet<string> Lineas { get; }

    /// <summary>Modalidad → sus sub-segmentos.</summary>
    internal IReadOnlyDictionary<string, IReadOnlySet<string>> Modalidades { get; }
}

/// <summary>
/// El punto único de las reglas de la matriz de Cartera (feature 013, T0642; contracts/contabilidad.md §3.5 y §3.6): lo usan el alta, la
/// versión, la desactivación y la importación, así la plantilla responde lo mismo que el alta una a una. Comprueba la forma
/// (<see cref="Forma"/>: hecho y rol de los catálogos, hecho en <c>*</c> salvo en los roles de resultado, exigidas presentes y no admitidas
/// ausentes), los códigos (línea, modalidad y sub-segmento por <see cref="IDimensionesDeCartera"/>; categoría, concepto, clase de recaudador
/// y motivo en sus catálogos fijos), la cuenta (<see cref="AccountEligibility.Verificar"/> para CAR), los cruces de vigencia de la misma
/// clave (<c>Overlaps</c>) y la retroactividad sobre lo contabilizado (<c>RetroactiveOverPosted</c>, con la excepción de la clave nueva que no
/// casa con nada contabilizado). Avisa —y guarda— si la naturaleza de la cuenta no es la del rol. Molde: <c>ReglasDeLaMatriz</c> de la 012. (nuevo)
/// </summary>
public sealed class ReglasDeLaMatrizDeCartera(IApplicationDbContext db, IDimensionesDeCartera dimensiones, ResolutorDeReglasDeCartera resolutor)
{
    /// <summary>El aviso de §3.6: la naturaleza de la cuenta no es la del rol (p. ej. un deterioro en una cuenta débito).</summary>
    public const string AvisoNaturalezaDistinta = "Accounting.LendingRule.NatureMismatch";

    /// <summary>Lo que las validaciones leen una vez.</summary>
    public async Task<CatalogosDeLaMatrizDeCartera> CargarAsync(CancellationToken ct) => new(await dimensiones.CatalogoAsync(ct));

    /// <summary>Las dimensiones fijadas, a partir de sus valores (vacío o <c>*</c> = no fija).</summary>
    public static IReadOnlySet<DimensionDeCartera> Presentes(
        string? operacion, string? linea, string? modalidad, string? subsegmento, string? categoria, string? concepto, bool? empleado, bool? gracia,
        bool? porCuentaDelAsociado, bool? reestructurado, string? clase, string? motivo, bool sucursal)
    {
        var presentes = new HashSet<DimensionDeCartera>();
        if (ReglaPropuestaDeCartera.Valor(operacion) is not null) presentes.Add(DimensionDeCartera.Operation);
        if (ReglaPropuestaDeCartera.Valor(linea) is not null) presentes.Add(DimensionDeCartera.CreditLine);
        if (ReglaPropuestaDeCartera.Valor(modalidad) is not null) presentes.Add(DimensionDeCartera.Modality);
        if (ReglaPropuestaDeCartera.Valor(subsegmento) is not null) presentes.Add(DimensionDeCartera.Subsegment);
        if (ReglaPropuestaDeCartera.Valor(categoria) is not null) presentes.Add(DimensionDeCartera.RiskCategory);
        if (ReglaPropuestaDeCartera.Valor(concepto) is not null) presentes.Add(DimensionDeCartera.Concept);
        if (empleado is not null) presentes.Add(DimensionDeCartera.IsEmployee);
        if (gracia is not null) presentes.Add(DimensionDeCartera.InGracePeriod);
        if (porCuentaDelAsociado is not null) presentes.Add(DimensionDeCartera.OnBehalfOfAssociate);
        if (reestructurado is not null) presentes.Add(DimensionDeCartera.Restructured);
        if (ReglaPropuestaDeCartera.Valor(clase) is not null) presentes.Add(DimensionDeCartera.CollectorClass);
        if (ReglaPropuestaDeCartera.Valor(motivo) is not null) presentes.Add(DimensionDeCartera.Reason);
        if (sucursal) presentes.Add(DimensionDeCartera.Branch);
        return presentes;
    }

    /// <summary>
    /// La forma, sin leer nada: el hecho es <c>*</c> o uno de negocio de <see cref="OperacionesDeCartera"/>; el rol existe y, con el hecho
    /// fijado, es de ese hecho y de resultado (en los demás el hecho va en <c>*</c>, C77); las exigidas están (según el motivo, en el gasto y
    /// la recuperación del deterioro); las que el rol no admite, no. La usan los validadores (400) y el handler (422).
    /// </summary>
    public static IReadOnlyList<Error> Forma(string? operacion, string? rol, string? motivo, IReadOnlySet<DimensionDeCartera> presentes)
    {
        var errores = new List<Error>();
        var hecho = ReglaPropuestaDeCartera.Valor(operacion);
        OperacionDeCartera? op = null;
        if (hecho is not null)
        {
            op = OperacionesDeCartera.Buscar(hecho);
            if (op is null || op.EsInformativo)
            {
                errores.Add(AccountingErrors.LendingRuleOperationUnknown(hecho));
                return errores;
            }
        }
        var definicion = R.Buscar(rol);
        if (definicion is null)
        {
            errores.Add(AccountingErrors.LendingRuleRoleNotInOperation(hecho ?? LendingPostingRule.Cualquiera, rol ?? string.Empty, []));
            return errores;
        }
        if (op is not null && !op.TieneRol(definicion.Codigo))
        {
            errores.Add(AccountingErrors.LendingRuleRoleNotInOperation(op.Codigo, definicion.Codigo, op.Roles));
            return errores;
        }

        var nombreDelHecho = hecho ?? LendingPostingRule.Cualquiera;
        var (exigidas, opcionales) = R.DimensionesDe(definicion, ReglaPropuestaDeCartera.Valor(motivo));
        foreach (var exigida in exigidas.Where(d => !presentes.Contains(d)))
            errores.Add(AccountingErrors.LendingRuleDimensionRequired(nombreDelHecho, definicion.Codigo, R.NombreDe(exigida)));
        foreach (var presente in presentes.Where(d => !exigidas.Contains(d) && !opcionales.Contains(d)).OrderBy(d => d))
            errores.Add(AccountingErrors.LendingRuleDimensionNotAllowed(nombreDelHecho, definicion.Codigo, R.NombreDe(presente)));
        return errores;
    }

    /// <summary>
    /// Todas las reglas de §3.5 y §3.6 sobre <paramref name="propuesta"/>. <paramref name="versionesDeLaClave"/> son las reglas vivas con la
    /// misma <c>DimensionKey</c> (seguidas, porque las que haya que cerrar se cierran sobre ellas; la importación agrega las que ya creó el
    /// mismo archivo). Con <paramref name="esVersion"/> la vigente que empieza antes se cierra la víspera; sin él, cualquier cruce es
    /// <c>Overlaps</c>.
    /// </summary>
    public async Task<ValidacionDeReglaDeCartera> ValidarAsync(
        ReglaPropuestaDeCartera propuesta,
        CatalogosDeLaMatrizDeCartera catalogos,
        IReadOnlyList<LendingPostingRule> versionesDeLaClave,
        bool esVersion,
        CancellationToken ct)
    {
        var errores = new List<Error>(Forma(propuesta.Operation, propuesta.Role, propuesta.ReasonCode, propuesta.Presentes));
        var avisos = new List<AvisoDeReglaDeCarteraDto>();
        if (errores.Count > 0) return new ValidacionDeReglaDeCartera(errores, avisos, []);

        var rol = R.Buscar(propuesta.Role)!;
        if (propuesta.ValidTo is { } hasta && hasta < propuesta.ValidFrom.AddDays(-1))
            errores.Add(AccountingErrors.LendingRuleValidToInvalid(propuesta.ValidFrom, hasta));

        Codigos(propuesta, rol, catalogos, errores);

        // La cuenta: de movimiento, activa y habilitada para CAR (§3.6). La naturaleza distinta de la del rol sólo avisa.
        var cuenta = await db.ChartOfAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == propuesta.AccountId && !a.IsDeleted, ct);
        var elegible = AccountEligibility.Verificar(cuenta, propuesta.AccountId.ToString(CultureInfo.InvariantCulture), ModuloContable.Cartera);
        if (elegible.IsFailure) errores.Add(elegible.Error);
        else if (cuenta!.Nature != rol.Naturaleza)
            avisos.Add(new AvisoDeReglaDeCarteraDto(AvisoNaturalezaDistinta,
                $"La cuenta {cuenta.Code} es de naturaleza {Naturaleza(cuenta.Nature)} y el rol {rol.Codigo} es de naturaleza {Naturaleza(rol.Naturaleza)}. "
                + "Se guarda, pero revise que sea la cuenta que quiere: la completitud lo seguirá mostrando."));

        var aCerrar = new List<LendingPostingRule>();
        Vigencias(propuesta, versionesDeLaClave, esVersion, errores, aCerrar);

        if (errores.Count == 0 && await RetroactividadAsync(propuesta, ct) is { } retro) errores.Add(retro);
        return new ValidacionDeReglaDeCartera(errores, avisos, errores.Count == 0 ? aCerrar : []);
    }

    /// <summary>
    /// La retroactividad al desactivar: cerrar una regla antes de la fecha contable de un hecho ya contabilizado que la usó dejaría lo
    /// contabilizado sin la regla que lo explica (§3.5). Se mira desde el día siguiente al cierre hasta su fin de vigencia actual.
    /// </summary>
    public async Task<Error?> RetroactividadDelCierreAsync(LendingPostingRule regla, DateOnly hasta, CancellationToken ct)
    {
        if (regla.ValidTo is { } fin && hasta >= fin) return null;
        var desde = hasta.AddDays(1) < regla.ValidFrom ? regla.ValidFrom : hasta.AddDays(1);
        var hecho = await resolutor.UltimoQueCoincideAsync(regla, desde, regla.ValidTo, ct);
        return hecho is null ? null : AccountingErrors.LendingRuleRetroactiveOverPosted(hecho.OperationDate, hecho.Documento);
    }

    private static void Codigos(ReglaPropuestaDeCartera p, RolDeCuentaDeCartera rol, CatalogosDeLaMatrizDeCartera catalogos, List<Error> errores)
    {
        void Desconocido(DimensionDeCartera dimension, string codigo) =>
            errores.Add(AccountingErrors.LendingRuleDimensionCodeUnknown(R.NombreDe(dimension), codigo));

        if (ReglaPropuestaDeCartera.Valor(p.CreditLineCode) is { } linea && !catalogos.Lineas.Contains(linea))
            Desconocido(DimensionDeCartera.CreditLine, linea);

        var modalidad = ReglaPropuestaDeCartera.Valor(p.ModalityCode);
        IReadOnlySet<string>? subsegmentosDeLaModalidad = null;
        if (modalidad is not null && !catalogos.Modalidades.TryGetValue(modalidad, out subsegmentosDeLaModalidad))
            Desconocido(DimensionDeCartera.Modality, modalidad);

        if (ReglaPropuestaDeCartera.Valor(p.SubsegmentCode) is { } subsegmento)
        {
            // El sub-segmento tiene que ser de la modalidad (§3.2); sin modalidad, de alguna.
            var existe = subsegmentosDeLaModalidad?.Contains(subsegmento)
                ?? (modalidad is null && catalogos.Modalidades.Values.Any(s => s.Contains(subsegmento)));
            if (!existe) Desconocido(DimensionDeCartera.Subsegment, modalidad is null ? subsegmento : $"{subsegmento} (de {modalidad})");
        }

        if (ReglaPropuestaDeCartera.Valor(p.RiskCategory) is { } categoria
            && (!R.Categorias.Contains(LendingPostingRule.Categoria(categoria)!, StringComparer.Ordinal) || categoria.Length > 2))
            Desconocido(DimensionDeCartera.RiskCategory, categoria);

        if (ReglaPropuestaDeCartera.Valor(p.ConceptCode) is { } concepto && !R.AdmiteConcepto(rol, concepto))
            Desconocido(DimensionDeCartera.Concept, concepto);

        if (ReglaPropuestaDeCartera.Valor(p.CollectorClass) is { } clase && !R.ClasesDeRecaudador.Contains(clase, StringComparer.Ordinal))
            Desconocido(DimensionDeCartera.CollectorClass, clase);

        if (ReglaPropuestaDeCartera.Valor(p.ReasonCode) is { } motivo && !R.AdmiteMotivo(rol, motivo))
            Desconocido(DimensionDeCartera.Reason, motivo);
    }

    private static void Vigencias(
        ReglaPropuestaDeCartera p, IReadOnlyList<LendingPostingRule> versiones, bool esVersion, List<Error> errores, List<LendingPostingRule> aCerrar)
    {
        var hasta = p.ValidTo ?? DateOnly.MaxValue;
        foreach (var v in versiones.Where(v => !v.IsDeleted))
        {
            var seCruza = v.ValidFrom <= hasta && (v.ValidTo ?? DateOnly.MaxValue) >= p.ValidFrom;
            if (!seCruza) continue;
            if (esVersion && v.ValidFrom < p.ValidFrom)
            {
                aCerrar.Add(v);
                continue;
            }
            errores.Add(AccountingErrors.LendingRuleOverlaps(new
            {
                rulePublicId = v.PublicId, validFrom = v.ValidFrom, validTo = v.ValidTo, dimensionKey = v.DimensionKey,
            }));
            return;
        }
    }

    /// <summary>
    /// §3.5: una regla (clave nueva o versión) no puede empezar en o antes de la fecha contable de un hecho ya contabilizado que casa con ella.
    /// Una clave nueva sin hechos contabilizados que casen desde su <c>ValidFrom</c> sí puede empezar antes: así se rescatan los rechazados por
    /// regla faltante sin cambiar el resultado de nada contabilizado.
    /// </summary>
    private async Task<Error?> RetroactividadAsync(ReglaPropuestaDeCartera p, CancellationToken ct)
    {
        var hecho = await resolutor.UltimoQueCoincideAsync(p.ComoRegla(null), p.ValidFrom, p.ValidTo, ct);
        return hecho is null ? null : AccountingErrors.LendingRuleRetroactiveOverPosted(hecho.OperationDate, hecho.Documento);
    }

    private static string Naturaleza(Domain.Enums.Accounting.AccountNature n) =>
        n == Domain.Enums.Accounting.AccountNature.Debit ? "débito" : "crédito";
}
