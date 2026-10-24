using System.Globalization;
using IngenIA365ERP.Application.Accounting.Rules;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;

/// <summary>La cuenta que resolvió un rol de un hecho: por una regla de la matriz o por el vínculo de la cuenta propia. (nuevo)</summary>
public sealed record CuentaResuelta(ChartOfAccount Cuenta, CuentaParaReglas Reglas, int? CostCenterId, Guid? ReglaPublicId);

/// <summary>
/// Las reglas que rigen en un rango, con sus cuentas y los vínculos de las cuentas propias, cargadas una vez (feature 015, T0257; contabilidad.md §6). Responde, sin
/// más consultas, qué cuenta gana para un rol de un hecho: el mismo código en el consumo, la validación previa y la completitud. Los roles por vínculo
/// (<c>Banco</c>, <c>BancoRecaudo</c>) no admiten regla: los resuelve <c>ACC_OwnBankAccountLinks</c> vigente a la fecha. (nuevo)
/// </summary>
public sealed class MatrizVigente(
    ModuloDeLaMatriz modulo,
    IReadOnlyList<ReglaContableDeModulo> reglas,
    IReadOnlyDictionary<int, ChartOfAccount> cuentas,
    IReadOnlyList<OwnBankAccountLink> vinculos)
{
    public ModuloDeLaMatriz Modulo => modulo;

    public IReadOnlyList<ReglaContableDeModulo> Reglas => reglas;

    /// <summary>Las reglas que podrían casar con la línea (mismo rol, hecho o <c>*</c>, vigentes, y cada dimensión fijada igual a la del hecho).</summary>
    public IReadOnlyList<ReglaContableDeModulo> Candidatas(string tipo, string rol, IReadOnlyDictionary<string, string> dimensiones, DateOnly fecha) =>
        reglas.Where(r => r.Role == rol && (r.FactType == ReglaContableDeModulo.Cualquiera || r.FactType == tipo) && r.VigenteEn(fecha)
                          && Coincide(r, dimensiones)).ToList();

    public Result<CuentaResuelta> Resolver(string tipo, LineaDeHecho linea, DateOnly fecha)
    {
        var rol = modulo.Rol(linea.Rol);
        if (rol is null) return Result.Failure<CuentaResuelta>(ErroresDeLaMatriz.RolDesconocido(modulo, tipo, linea.Rol));
        if (rol.PorVinculo)
        {
            var vinculo = linea.CuentaPropia is { } propia
                ? vinculos.Where(v => v.OwnBankAccountPublicId == propia && v.ValidFrom <= fecha && (v.ValidTo is null || v.ValidTo >= fecha))
                    .OrderByDescending(v => v.ValidFrom).FirstOrDefault()
                : null;
            if (vinculo is null || !cuentas.TryGetValue(vinculo.ChartOfAccountId, out var bancaria))
                return Result.Failure<CuentaResuelta>(ErroresDeLaMatriz.SinVinculo(modulo, tipo, linea.Rol, linea.Dimensiones.GetValueOrDefault(MatricesDeRecaudoYTesoreria.CuentaPropia)));
            return Result.Success(new CuentaResuelta(bancaria, CuentaParaReglas.De(bancaria, fecha), null, null));
        }

        var candidatas = Candidatas(tipo, linea.Rol, linea.Dimensiones, fecha);
        if (candidatas.Count == 0) return Result.Failure<CuentaResuelta>(ErroresDeLaMatriz.SinRegla(modulo, tipo, linea.Rol, linea.Combinacion));
        var mayor = candidatas.Max(c => c.SpecificityWeight);
        var ganadoras = candidatas.Where(c => c.SpecificityWeight == mayor).ToList();
        if (ganadoras.Count > 1) return Result.Failure<CuentaResuelta>(ErroresDeLaMatriz.Empate(modulo, tipo, linea.Rol, linea.Combinacion, ganadoras.Count));
        var regla = ganadoras[0];
        if (!cuentas.TryGetValue(regla.AccountId, out var cuenta))
            return Result.Failure<CuentaResuelta>(ErroresDeLaMatriz.SinRegla(modulo, tipo, linea.Rol, linea.Combinacion));
        return Result.Success(new CuentaResuelta(cuenta, CuentaParaReglas.De(cuenta, fecha), regla.CostCenterId, regla.PublicId));
    }

    /// <summary>
    /// ¿Hay alguna regla vigente que podría casar con el rol del hecho? Para la validación previa, que pregunta con las dimensiones que conoce: una dimensión que la
    /// pregunta no trae no descarta la regla.
    /// </summary>
    public bool HayReglaCompatible(string tipo, string rol, IReadOnlyDictionary<string, string> conocidas, DateOnly fecha) =>
        reglas.Any(r => r.Role == rol && (r.FactType == ReglaContableDeModulo.Cualquiera || r.FactType == tipo) && r.VigenteEn(fecha)
                        && Coincide(r, conocidas, soloLasConocidas: true));

    /// <summary>¿Hay vínculo vigente de la cuenta propia en la fecha?</summary>
    public bool TieneVinculo(Guid cuentaPropia, DateOnly fecha) =>
        vinculos.Any(v => v.OwnBankAccountPublicId == cuentaPropia && v.ValidFrom <= fecha && (v.ValidTo is null || v.ValidTo >= fecha));

    /// <summary>¿Cada dimensión fijada en la regla es igual a la del hecho? Una que el hecho no trae no casa.</summary>
    public static bool Coincide(ReglaContableDeModulo r, IReadOnlyDictionary<string, string> d, bool soloLasConocidas = false)
    {
        bool Igual(string? fijada, IReadOnlyDictionary<string, string> d, string clave) =>
            fijada is null || (d.TryGetValue(clave, out var valor) ? string.Equals(valor, fijada, StringComparison.OrdinalIgnoreCase) : soloLasConocidas);

        return Igual(r.BranchCode, d, MatricesDeRecaudoYTesoreria.Oficina)
               && Igual(r.CollectionPointCode, d, MatricesDeRecaudoYTesoreria.Punto)
               && Igual(r.CashBoxCode, d, MatricesDeRecaudoYTesoreria.Caja)
               && Igual(r.MeansCode, d, MatricesDeRecaudoYTesoreria.Medio)
               && Igual(r.MeansClass?.ToString(CultureInfo.InvariantCulture), d, MatricesDeRecaudoYTesoreria.ClaseDeMedio)
               && Igual(r.MiscConceptCode, d, MatricesDeRecaudoYTesoreria.Concepto)
               && Igual(r.TargetModule, d, MatricesDeRecaudoYTesoreria.Modulo)
               && Igual(r.TargetNature, d, MatricesDeRecaudoYTesoreria.Naturaleza)
               && Igual(r.CollectorClass, d, MatricesDeRecaudoYTesoreria.ClaseDeRecaudador)
               && Igual(r.OwnBankAccountCode, d, MatricesDeRecaudoYTesoreria.CuentaPropia)
               && Igual(r.OrderOrigin, d, MatricesDeRecaudoYTesoreria.OrigenDeLaOrden)
               && Igual(r.OrderKind, d, MatricesDeRecaudoYTesoreria.ClaseDeOrden)
               && Igual(r.Cause, d, MatricesDeRecaudoYTesoreria.Causa);
    }
}

/// <summary>Carga la matriz vigente de un módulo en un rango, sin seguimiento. (nuevo)</summary>
public sealed class ResolutorDeLaMatriz(IApplicationDbContext db)
{
    public async Task<MatrizVigente> CargarAsync(ModuloDeLaMatriz modulo, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (hasta < desde) (desde, hasta) = (hasta, desde);
        var reglas = modulo.Codigo == MatricesDeRecaudoYTesoreria.Recaudo.Codigo
            ? (await db.ReceiptsPostingRules.AsNoTracking().Where(r => r.ValidFrom <= hasta && (r.ValidTo == null || r.ValidTo >= desde)).ToListAsync(ct))
                .Cast<ReglaContableDeModulo>().ToList()
            : (await db.TreasuryPostingRules.AsNoTracking().Where(r => r.ValidFrom <= hasta && (r.ValidTo == null || r.ValidTo >= desde)).ToListAsync(ct))
                .Cast<ReglaContableDeModulo>().ToList();
        var vinculos = await db.OwnBankAccountLinks.AsNoTracking()
            .Where(v => v.ValidFrom <= hasta && (v.ValidTo == null || v.ValidTo >= desde)).ToListAsync(ct);
        var ids = reglas.Select(r => r.AccountId).Concat(vinculos.Select(v => v.ChartOfAccountId)).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Include(a => a.TaxRates).Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        return new MatrizVigente(modulo, reglas, cuentas, vinculos);
    }
}

/// <summary>Los errores de las matrices, con el prefijo del módulo (<c>Accounting.ReceiptsRule.*</c>, <c>Accounting.TreasuryRule.*</c>). (nuevo)</summary>
public static class ErroresDeLaMatriz
{
    public static Error SinRegla(ModuloDeLaMatriz m, string tipo, string rol, string combinacion) =>
        new ErrorConDatos($"{m.PrefijoDeErrores}.Missing",
            $"La matriz contable de {m.Nombre} no tiene regla para el rol {rol} del hecho {tipo} ({(combinacion.Length == 0 ? "sin dimensiones" : combinacion)}). La completa la contadora en Contabilidad.",
            new { factType = tipo, role = rol, combination = combinacion, whoFixes = m.PermisoDeLaMatriz + ".Manage" });

    public static Error Empate(ModuloDeLaMatriz m, string tipo, string rol, string combinacion, int cuantas) =>
        new ErrorConDatos($"{m.PrefijoDeErrores}.Ambiguous",
            $"{cuantas} reglas de la matriz de {m.Nombre} igual de específicas casan con el rol {rol} del hecho {tipo} ({combinacion}): es un error de configuración; deje una.",
            new { factType = tipo, role = rol, combination = combinacion, whoFixes = m.PermisoDeLaMatriz + ".Manage" });

    public static Error SinVinculo(ModuloDeLaMatriz m, string tipo, string rol, string? cuentaPropia) =>
        new ErrorConDatos("Accounting.OwnBankAccount.NotLinked",
            $"La cuenta bancaria propia {cuentaPropia ?? "(sin código)"} del hecho {tipo} no está vinculada a una cuenta contable bancaria: vincúlela en Tesorería.",
            new { factType = tipo, role = rol, ownBankAccount = cuentaPropia, whoFixes = "Treasury.BankAccounts.Manage" });

    public static Error RolDesconocido(ModuloDeLaMatriz m, string tipo, string rol) =>
        new ErrorConDatos($"{m.PrefijoDeErrores}.UnknownRole", $"El hecho {tipo} trae el rol {rol}, que no es de la matriz de {m.Nombre}.", new { factType = tipo, role = rol });

    public static Error RolPorVinculo(ModuloDeLaMatriz m, string rol) =>
        new($"{m.PrefijoDeErrores}.RoleResolvedByLink",
            $"El rol {rol} no admite regla: lo resuelve el vínculo de la cuenta bancaria propia con su cuenta contable (Tesorería, cuentas propias).");

    public static Error HechoSoloEnResultado(ModuloDeLaMatriz m, string rol) =>
        new($"{m.PrefijoDeErrores}.FactOnlyInResultRoles", $"El hecho sólo se fija en los roles de resultado (gastos e ingresos); {rol} va con el hecho en «*».");

    public static Error HechoDesconocido(ModuloDeLaMatriz m, string tipo) =>
        new($"{m.PrefijoDeErrores}.UnknownFact", $"{tipo} no es un hecho de {m.Nombre} que pase a Contabilidad.");

    public static Error CuentaNoElegible(ModuloDeLaMatriz m, string codigo) =>
        new($"{m.PrefijoDeErrores}.AccountNotEligible",
            $"La cuenta {codigo} no es de movimiento, no está activa o no está habilitada para {m.Nombre}: elija otra o habilítela en el plan de cuentas.");

    public static Error Duplicada(ModuloDeLaMatriz m) =>
        new($"{m.PrefijoDeErrores}.Duplicate", "Ya existe una regla con las mismas dimensiones que empieza ese día: cierre la anterior o cambie la fecha.");

    public static Error Retroactiva(ModuloDeLaMatriz m, DateOnly fecha) =>
        new($"{m.PrefijoDeErrores}.Retroactive",
            string.Create(CultureInfo.InvariantCulture, $"Ya hay hechos contabilizados con esa combinación el {fecha:yyyy-MM-dd} o después: la regla nueva no puede regir antes; nunca es retroactiva sobre lo contabilizado."));

    public static Error NoExiste(ModuloDeLaMatriz m) => new($"{m.PrefijoDeErrores}.NotFound", "No existe esa regla.");

    public static Error SinComprobante(ModuloDeLaMatriz m, string tipo) =>
        new ErrorConDatos($"{m.PrefijoDeErrores}.VoucherTypeMissing",
            $"El hecho {tipo} no tiene tipo de comprobante en {m.Nombre}: asígnelo en Contabilidad.", new { factType = tipo, whoFixes = m.PermisoDeLaMatriz + ".Manage" });

    public static Error ComprobanteNoElegible(ModuloDeLaMatriz m, string codigo) =>
        new($"{m.PrefijoDeErrores}.VoucherTypeNotAllowed", $"El tipo de comprobante {codigo} no es de uso «módulo» de {m.Nombre} o está inactivo.");
}
