using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Application.Lending.Deposits.Contractual;

/// <summary>Los códigos de error del ahorro contractual (feature 014, US7, T182; api.md §4; prefijo de research C24 y E36). (nuevo)</summary>
public static class ErroresDeContractual
{
    /// <summary>api.md §4 (<c>PUT /{id}/maturity-instruction</c>): la cuenta no es un ahorro contractual.</summary>
    public const string CodigoNoEsContractual = "Lending.Savings.NotContractual";

    public const string CodigoCondicionesRequeridas = "Lending.Contractual.TermsRequired";
    public const string CodigoCondicionesNoAdmitidas = "Lending.Contractual.TermsNotAllowed";
    public const string CodigoRetiroNoAdmitido = "Lending.Contractual.WithdrawalNotAllowed";
    public const string CodigoConsignacionLibre = "Lending.Contractual.FreeDepositNotAllowed";
    public const string CodigoNoVigente = "Lending.Contractual.NotCurrent";
    public const string CodigoDestinoInvalido = "Lending.Contractual.DestinationInvalid";
    public const string CodigoSinCondiciones = "Lending.Contractual.TermsMissing";

    public static Error De(ProblemaDeCartera p) => ErroresDeCuentas.De(p);

    public static Error EntregaNoDisponible(string que) => ErroresDeAportes.EntregaNoDisponible(que, nameof(EntregaDeCartera.K9));

    public static Error NoEsContractual(string numero) => new ErrorConDatos(CodigoNoEsContractual,
        $"La cuenta {numero} no es un ahorro contractual.", new { accountNumber = numero });

    public static Error CondicionesRequeridas() => new ErrorConDatos(CodigoCondicionesRequeridas,
        "Un ahorro contractual se abre con su contrato: la cuota y el plazo en meses.", new { field = "contractual" });

    public static Error CondicionesNoAdmitidas() => new ErrorConDatos(CodigoCondicionesNoAdmitidas,
        "Las condiciones del contrato sólo van en una línea de ahorro contractual.", new { field = "contractual" });

    public static Error RetiroNoAdmitido(string numero) => new ErrorConDatos(CodigoRetiroNoAdmitido,
        $"El ahorro contractual {numero} no admite retiros mientras el contrato esté vigente: se retira cancelándolo (con la reliquidación del incumplimiento) "
        + "o al vencer.", new { accountNumber = numero });

    public static Error ConsignacionLibre(string numero) => new ErrorConDatos(CodigoConsignacionLibre,
        $"El ahorro contractual {numero} recibe sus cuotas: consigne por cuotas (las pendientes en orden de vencimiento o las nombradas).",
        new { accountNumber = numero });

    public static Error NoVigente(string numero, MemberAccountStatus estado, ContractualStatus? contrato) => new ErrorConDatos(CodigoNoVigente,
        $"El contrato de la cuenta {numero} no está vigente (cuenta «{estado}», contrato «{contrato}»): no admite la operación.",
        new { accountNumber = numero, status = estado.ToString(), contractualStatus = contrato?.ToString() });

    public static Error DestinoInvalido(string mensaje) => new ErrorConDatos(CodigoDestinoInvalido, mensaje, new { field = "maturityInstruction" });

    public static Error SinCondiciones(string numero) => new ErrorConDatos(CodigoSinCondiciones,
        $"El ahorro contractual {numero} no tiene condiciones selladas.", new { accountNumber = numero });

    /// <summary>Lo que el motor rechaza del contrato pedido (cuota bajo el mínimo, plazo fuera del rango, sin cuotas).</summary>
    public static Error DelContrato(ProblemaDeCartera p) => De(p);

    /// <summary>Los códigos del motor, para que la pantalla los nombre.</summary>
    public static IReadOnlyList<string> DelMotor { get; } =
        [AhorroContractual.CodigoCuotaBajoElMinimo, AhorroContractual.CodigoPlazoFueraDeRango, AhorroContractual.CodigoSinCuotas];
}
