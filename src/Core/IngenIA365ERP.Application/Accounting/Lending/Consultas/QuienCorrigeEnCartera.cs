using IngenIA365ERP.Application.Common.Integration.Accounting;

namespace IngenIA365ERP.Application.Accounting.Lending.Consultas;

/// <summary>
/// Quién corrige cada hallazgo de un hecho de Cartera (feature 013, T0649; contracts/contabilidad.md §13.3): el módulo, la pantalla y el
/// permiso. Una fila por código; lo que no está en la tabla es un defecto del emisor y va a soporte. Lo usan la validación previa
/// (<c>EvaluateLendingPostingQuery</c>), la vista previa y el detalle de un lote. (nuevo)
/// </summary>
public static class QuienCorrigeEnCartera
{
    private const string Contabilidad = "Accounting";
    private const string Cartera = "Lending";
    private const string Core = "Core";

    public static QuienCorrigeDto De(string codigo) => codigo switch
    {
        "Accounting.LendingRule.Missing" => new(Contabilidad, "/contabilidad/cartera/matriz", "Accounting.LendingRules.Manage"),
        "Accounting.VoucherType.NotFound" or "Accounting.VoucherType.NotAllowedForModule" or "Accounting.Line.CrossDocumentTypeInvalid"
            => new(Contabilidad, "/contabilidad/cartera/tipos-de-comprobante", "Accounting.LendingRules.Manage"),
        "Accounting.Line.AccountNotMovement" or "Accounting.Line.AccountInactive" or "Accounting.Line.AccountNotEnabledForModule"
            or "Accounting.Line.CostCenterNotAllowed" or "Accounting.Line.TaxBaseRequired" or "Accounting.Line.AccountNotFound"
            => new(Contabilidad, "/contabilidad/plan-de-cuentas", "Accounting.Accounts.Manage"),
        "Accounting.Line.CostCenterRequired" or "Accounting.Line.CrossDocumentRequired" or "Accounting.Line.ThirdPartyRequired"
            => new(Contabilidad, "/contabilidad/cartera/matriz", "Accounting.LendingRules.Manage"),
        "Accounting.Line.ThirdPartyInvalid" => new(Core, "/maestros/personas", "Core.People.Update"),
        "Accounting.Period.Closed" => new(Contabilidad, "/contabilidad/periodos", "Accounting.Periods.Reopen"),
        "Accounting.Period.NotFound" => new(Contabilidad, "/contabilidad/periodos", "Accounting.Periods.CloseYear"),
        "Accounting.NotInitialized" => new(Contabilidad, "/contabilidad/configuracion", "Accounting.Setup.Manage"),
        _ => new(Cartera, null, null),
    };
}
