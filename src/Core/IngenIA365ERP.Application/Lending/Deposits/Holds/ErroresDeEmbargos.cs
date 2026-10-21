using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;

namespace IngenIA365ERP.Application.Lending.Deposits.Holds;

/// <summary>
/// Los códigos de los bloqueos y embargos (feature 014, US11, T189; api.md §8; research C24, E161): <c>Lending.Garnishment.*</c> y <c>Lending.Holds.*</c>.
/// En las rutas: 404 lo inexistente, 422 lo demás; los avisos (<c>ContributionsNotGarnishable</c>, <c>NothingGarnishable</c>,
/// <c>ResponseOverdue</c>) no son errores: viajan en <c>warnings</c> de la respuesta. (nuevo)
/// </summary>
public static class ErroresDeEmbargos
{
    public const string CodigoNoEncontrado = Garnishment.CodigoNoEncontrado;
    public const string CodigoYaLevantado = Garnishment.CodigoYaLevantado;
    public const string CodigoAportesInembargables = "Lending.Garnishment.ContributionsNotGarnishable";
    public const string CodigoNadaEmbargable = "Lending.Garnishment.NothingGarnishable";
    public const string CodigoExcedeRetenido = "Lending.Garnishment.ExceedsRetained";
    public const string CodigoRespuestaVencida = "Lending.Garnishment.ResponseOverdue";
    public const string CodigoCuentaAjena = "Lending.Garnishment.AccountNotOfPerson";
    public const string CodigoSinCuentaDestino = "Lending.Garnishment.DestinationMissing";
    public const string CodigoNoActivo = "Lending.Garnishment.NotActive";
    public const string CodigoDesembargoParcialInvalido = "Lending.Garnishment.PartialReleaseInvalid";
    public const string CodigoBloqueoNoEncontrado = "Lending.Holds.NotFound";
    public const string CodigoBloqueoDeProceso = "Lending.Holds.OwnedByProcess";
    public const string CodigoGarantiaVigente = "Lending.Holds.LoanStillActive";
    public const string CodigoBloqueoInvalido = "Lending.Holds.Invalid";
    public const string CodigoDisponibleInsuficiente = "Lending.Holds.InsufficientAvailable";

    public static Error NoEncontrado() => new(CodigoNoEncontrado, "El embargo no existe.");

    public static Error YaLevantado(string oficio) => new ErrorConDatos(CodigoYaLevantado, $"El embargo del oficio {oficio} ya está levantado.", new { officeReference = oficio });

    public static Error NoActivo(string oficio, string estado) => new ErrorConDatos(CodigoNoActivo,
        $"El embargo del oficio {oficio} está «{estado}»: sólo uno activo se consigna o se levanta.", new { officeReference = oficio, status = estado });

    public static Error ExcedeRetenido(decimal retenido, decimal pedido) => new ErrorConDatos(CodigoExcedeRetenido,
        string.Create(CultureInfo.InvariantCulture, $"La consignación de {pedido:N2} supera lo retenido por el embargo ({retenido:N2})."),
        new { retained = retenido, requested = pedido });

    public static Error CuentaAjena() => new(CodigoCuentaAjena, "Alguna de las cuentas del oficio no es una cuenta de la persona embargada.");

    public static Error SinCuentaDestino() => new(CodigoSinCuentaDestino,
        "Falta la cuenta de depósitos judiciales del juzgado (banco y número) a la que se consigna.");

    public static Error DesembargoParcialInvalido(decimal? valor, decimal pedido) => new ErrorConDatos(CodigoDesembargoParcialInvalido,
        "Un desembargo parcial baja el valor de una medida con valor sin agotarlo; para levantarlo todo, no indique valor.",
        new { amount = valor, requested = pedido });

    public static Error BloqueoNoEncontrado() => new(CodigoBloqueoNoEncontrado, "El bloqueo no existe.");

    public static Error BloqueoDeProceso(string motivo) => new ErrorConDatos(CodigoBloqueoDeProceso,
        $"El bloqueo por «{motivo}» lo pone y lo levanta su proceso (el embargo, el fallecimiento o las listas), no esta pantalla.", new { reason = motivo });

    public static Error GarantiaVigente(string credito) => new ErrorConDatos(CodigoGarantiaVigente,
        $"La cuenta garantiza el crédito {credito}, que sigue vigente: el bloqueo se levanta al pagarlo o al liberar la garantía.", new { loanNumber = credito });

    public static Error BloqueoInvalido(string mensaje, string campo) => new ErrorConDatos(CodigoBloqueoInvalido, mensaje, new { field = campo });

    public static Error DisponibleInsuficiente(decimal disponible, decimal pedido) => new ErrorConDatos(CodigoDisponibleInsuficiente,
        string.Create(CultureInfo.InvariantCulture, $"El bloqueo de {pedido:N2} supera el disponible de la cuenta ({disponible:N2})."),
        new { available = disponible, requested = pedido });
}
