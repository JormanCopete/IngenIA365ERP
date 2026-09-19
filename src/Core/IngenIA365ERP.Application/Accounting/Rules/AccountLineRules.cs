using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Application.Accounting.Rules;

/// <summary>Lo que las reglas necesitan saber de la cuenta: sus banderas y la tarifa vigente a la fecha de la línea.</summary>
public sealed record CuentaParaReglas(
    int Id,
    string Code,
    bool IsMovement,
    bool IsActive,
    AccountingModules EnabledModules,
    bool RequiresThirdParty,
    bool RequiresCrossDocument,
    bool RequiresCostCenter,
    bool RequiresBranch,
    bool RequiresTaxBase,
    decimal? TarifaVigente)
{
    public static CuentaParaReglas De(ChartOfAccount cuenta, DateOnly fecha) => new(
        cuenta.Id, cuenta.Code, cuenta.IsMovement, cuenta.IsActive, cuenta.EnabledModules,
        cuenta.RequiresThirdParty, cuenta.RequiresCrossDocument, cuenta.RequiresCostCenter, cuenta.RequiresBranch,
        cuenta.RequiresTaxBase, TarifaVigenteDe(cuenta, fecha));

    /// <summary>La tarifa con la vigencia más reciente que no sea posterior a la fecha (FR-065).</summary>
    public static decimal? TarifaVigenteDe(ChartOfAccount cuenta, DateOnly fecha) => cuenta.TaxRates
        .Where(r => !r.IsDeleted && r.ValidFrom <= fecha)
        .OrderByDescending(r => r.ValidFrom)
        .Select(r => (decimal?)r.Rate)
        .FirstOrDefault();
}

/// <summary>Una línea ya resuelta: sucursal propuesta o explícita, referencias por Id y tipo de cruce en mayúsculas.</summary>
public sealed record LineaParaReglas(
    int LineNumber,
    string AccountRef,
    decimal Debit,
    decimal Credit,
    int? BranchId,
    bool BranchExplicita,
    int? PersonId,
    int? CostCenterId,
    string? CrossDocumentType,
    string? CrossDocumentNumber,
    decimal? TaxBase)
{
    public decimal Importe => Debit > 0 ? Debit : Credit;
}

/// <summary>
/// Lo que rodea a la línea: el módulo que la manda, la tolerancia de impuestos de la empresa, el
/// alcance de sucursales de quien digita y los conjuntos de referencias vigentes (sucursales,
/// terceros, centros de costo y tipos de documento cruce), ya consultados por quien llama.
/// </summary>
public sealed record ContextoDeReglas(
    string Module,
    decimal TaxTolerance,
    AlcanceDeSucursales Alcance,
    IReadOnlySet<int> SucursalesVigentes,
    IReadOnlySet<int> TercerosVigentes,
    IReadOnlySet<int> CentrosVigentes,
    IReadOnlySet<string> TiposDeCruceVigentes)
{
    public bool EsContabilidad => Module == ModuloContable.Contabilidad;
}

/// <summary>
/// El único conjunto de reglas de línea (feature 009, FR-014; contracts/contabilizacion.md §2,
/// reglas 4 a 10). Lo aplica el contrato de contabilización a toda línea, venga de la digitación o
/// de un módulo, y la validación campo a campo de la pantalla. Es puro: recibe la cuenta ya cargada
/// y los conjuntos de referencias vigentes, y devuelve las infracciones con su gravedad.
/// </summary>
public static class AccountLineRules
{
    public static IReadOnlyList<ErrorDeLinea> Evaluar(CuentaParaReglas? cuenta, LineaParaReglas linea, ContextoDeReglas contexto)
    {
        var errores = new List<ErrorDeLinea>();
        var n = linea.LineNumber;

        // 4. importe: exactamente uno mayor que cero, con dos decimales
        if (!ImporteValido(linea.Debit, linea.Credit))
            errores.Add(AccountingErrors.LineAmountInvalid(n));

        // 5. cuenta: existe, de movimiento, activa, habilitada para el módulo
        if (cuenta is null)
        {
            errores.Add(AccountingErrors.LineAccountNotFound(n, linea.AccountRef));
            return errores;
        }
        var code = cuenta.Code;
        if (!cuenta.IsMovement) errores.Add(AccountingErrors.LineAccountNotMovement(n, code));
        else if (!cuenta.IsActive) errores.Add(AccountingErrors.LineAccountInactive(n, code));
        else if (!ModuloContable.Habilitada(cuenta.EnabledModules, contexto.Module))
            errores.Add(AccountingErrors.LineAccountNotEnabledForModule(n, code, contexto.Module));

        // 6. sucursal: explícita si la cuenta lo exige; vigente; dentro del alcance sólo al digitar en Contabilidad
        if (cuenta.RequiresBranch && !linea.BranchExplicita)
            errores.Add(AccountingErrors.LineBranchRequired(n, code));
        else if (linea.BranchId is not { } sucursal || !contexto.SucursalesVigentes.Contains(sucursal))
            errores.Add(AccountingErrors.LineBranchInvalid(n));
        else if (contexto.EsContabilidad && !contexto.Alcance.Permite(sucursal))
            errores.Add(AccountingErrors.LineBranchOutOfScope(n));

        // 7. tercero
        if (cuenta.RequiresThirdParty && linea.PersonId is null)
            errores.Add(AccountingErrors.LineThirdPartyRequired(n, code));
        else if (linea.PersonId is { } tercero && !contexto.TercerosVigentes.Contains(tercero))
            errores.Add(AccountingErrors.LineThirdPartyInvalid(n));

        // 8. documento cruce
        var tipo = string.IsNullOrWhiteSpace(linea.CrossDocumentType) ? null : linea.CrossDocumentType.Trim();
        if (cuenta.RequiresCrossDocument && (tipo is null || string.IsNullOrWhiteSpace(linea.CrossDocumentNumber)))
            errores.Add(AccountingErrors.LineCrossDocumentRequired(n, code));
        else if (tipo is not null && !contexto.TiposDeCruceVigentes.Contains(tipo))
            errores.Add(AccountingErrors.LineCrossDocumentTypeInvalid(n, tipo));

        // 9. centro de costo: presente si lo exige, ausente si no lo maneja, vigente
        if (cuenta.RequiresCostCenter && linea.CostCenterId is null)
            errores.Add(AccountingErrors.LineCostCenterRequired(n, code));
        else if (!cuenta.RequiresCostCenter && linea.CostCenterId is not null)
            errores.Add(AccountingErrors.LineCostCenterNotAllowed(n, code));
        else if (linea.CostCenterId is { } centro && !contexto.CentrosVigentes.Contains(centro))
            errores.Add(AccountingErrors.LineCostCenterInvalid(n));

        // 10. base gravable: presente si la cuenta es de impuesto; aviso dentro de la tolerancia, error fuera
        if (cuenta.RequiresTaxBase)
        {
            if (linea.TaxBase is not { } baseGravable)
                errores.Add(AccountingErrors.LineTaxBaseRequired(n, code));
            else if (cuenta.TarifaVigente is not { } tarifa)
                errores.Add(AccountingErrors.LineTaxRateMissing(n, code));
            else
            {
                var esperado = decimal.Round(baseGravable * tarifa, 2, MidpointRounding.AwayFromZero);
                var diferencia = Math.Abs(linea.Importe - esperado);
                if (diferencia > contexto.TaxTolerance)
                    errores.Add(AccountingErrors.LineTaxAmountMismatch(n, code, esperado, contexto.TaxTolerance));
                else if (diferencia > 0m)
                    errores.Add(AccountingErrors.LineTaxAmountDiffers(n, code, esperado));
            }
        }

        return errores;
    }

    /// <summary>Regla 4: débito o crédito mayor que cero (nunca ambos, nunca negativos) con dos decimales.</summary>
    public static bool ImporteValido(decimal debit, decimal credit) =>
        debit >= 0m && credit >= 0m && (debit > 0m) != (credit > 0m) && DosDecimales(debit) && DosDecimales(credit);

    private static bool DosDecimales(decimal valor) => decimal.Round(valor, 2) == valor;
}
