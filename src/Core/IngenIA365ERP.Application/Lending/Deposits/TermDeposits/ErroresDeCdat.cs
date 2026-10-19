using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits;

/// <summary>Los códigos de error del CDAT y de la tasa negociada (feature 014, US6, T170, T171; api.md §5, §6; prefijos de research C24). (nuevo)</summary>
public static class ErroresDeCdat
{
    public const string CodigoNoEncontrado = "Lending.TermDeposit.NotFound";
    public const string CodigoNoEsCdat = "Lending.TermDeposit.LineIsNotTermDeposit";
    public const string CodigoMontoFueraDeRango = "Lending.TermDeposit.AmountOutOfRange";
    public const string CodigoSinTasa = "Lending.TermDeposit.NoRateForTerm";
    public const string CodigoDestinoInvalido = "Lending.TermDeposit.DestinationInvalid";
    public const string CodigoFondeoInvalido = "Lending.TermDeposit.FundingInvalid";
    public const string CodigoMontoDistinto = "Lending.TermDeposit.FundingAmountMismatch";
    public const string CodigoNegociacionPendiente = "Lending.TermDeposit.PendingNegotiation";
    public const string CodigoYaFondeado = "Lending.TermDeposit.AlreadyFunded";
    public const string CodigoSinConsignaciones = "Lending.TermDeposit.NoAdditionalDeposits";
    public const string CodigoFechaDeInicio = "Lending.TermDeposit.StartDateInvalid";
    public const string CodigoVistaPreviaCambio = "Lending.Savings.PreviewChanged";

    public const string CodigoLineaNoNegocia = "Lending.NegotiatedRate.LineDoesNotAllow";
    public const string CodigoReglaRetroactiva = "Lending.NegotiatedRate.Retroactive";
    public const string CodigoReglaInexistente = "Lending.NegotiatedRate.RuleNotFound";
    public const string CodigoReglaSeCruza = "Lending.NegotiatedRate.RuleOverlaps";
    public const string CodigoSolicitudInexistente = "Lending.NegotiatedRate.NotFound";
    public const string CodigoYaDecidida = "Lending.NegotiatedRate.AlreadyDecided";
    public const string CodigoSoloQuienPidio = "Lending.NegotiatedRate.OnlyRequester";

    public static Error NoEncontrado() => new(CodigoNoEncontrado, "El CDAT no existe.");

    public static Error De(ProblemaDeCartera p) => ErroresDeCuentas.De(p);

    public static Error EntregaNoDisponible(string que) => ErroresDeAportes.EntregaNoDisponible(que, nameof(EntregaDeCartera.K9));

    public static Error MontoFueraDeRango(decimal monto, decimal minimo) => new ErrorConDatos(CodigoMontoFueraDeRango,
        $"El CDAT se constituye desde {minimo:N0}; se pidió {monto:N0}.", new { amount = monto, minAmount = minimo });

    public static Error SinTasa(int plazo, decimal monto) => new ErrorConDatos(CodigoSinTasa,
        $"La línea no tiene tasa vigente para {plazo} días y {monto:N0}.", new { termDays = plazo, amount = monto });

    public static Error DestinoInvalido(string mensaje) => new ErrorConDatos(CodigoDestinoInvalido, mensaje, new { field = "paymentDestination" });

    public static Error FondeoInvalido(string mensaje) => new ErrorConDatos(CodigoFondeoInvalido, mensaje, new { field = "funding" });

    public static Error MontoDistinto(decimal esperado, decimal recibido) => new ErrorConDatos(CodigoMontoDistinto,
        $"El CDAT se fondea con exactamente {esperado:N2}; llegaron {recibido:N2}.", new { expected = esperado, received = recibido });

    public static Error NegociacionPendiente(string numero) => new ErrorConDatos(CodigoNegociacionPendiente,
        $"La tasa negociada del CDAT {numero} espera decisión: no se fondea hasta decidirla o retirarla.", new { accountNumber = numero });

    public static Error YaFondeado(string numero) => new ErrorConDatos(CodigoYaFondeado, $"El CDAT {numero} ya está constituido.", new { accountNumber = numero });

    public static Error SinConsignaciones(string numero) => new ErrorConDatos(CodigoSinConsignaciones,
        $"El CDAT {numero} no recibe consignaciones: el monto se cambia al renovarlo.", new { accountNumber = numero });

    public static Error VistaPreviaCambio(object vistaPrevia) => new ErrorConDatos(CodigoVistaPreviaCambio,
        "La simulación cambió desde que se mostró: revísela y vuelva a confirmar.", new { preview = vistaPrevia });

    public static Error FechaDeInicio(DateOnly hoy) => new ErrorConDatos(CodigoFechaDeInicio,
        $"El CDAT empieza hoy ({ErroresDeCuentas.Fecha(hoy)}) o, si se fondea después, el día del fondeo.", new { today = ErroresDeCuentas.Fecha(hoy) });

    public static Error LineaNoNegocia(string linea) => new ErrorConDatos(CodigoLineaNoNegocia,
        $"La línea {linea} no admite tasa negociada.", new { lineCode = linea });

    public static Error SolicitudInexistente() => new(CodigoSolicitudInexistente, "La solicitud de tasa negociada no existe.");

    public static Error YaDecidida(string estado) => new ErrorConDatos(CodigoYaDecidida,
        $"La solicitud ya está «{estado}»: sólo se decide o se retira una en espera.", new { status = estado });
}
