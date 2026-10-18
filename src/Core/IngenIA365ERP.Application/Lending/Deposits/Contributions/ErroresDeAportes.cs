using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Deposits.Contributions;

/// <summary>
/// Los errores de los aportes y del contrato de cuentas (feature 014, US3, T083, T086; contrato-de-cuentas §3.14; api.md §1.8, §7). Un rechazo del
/// contrato es <c>Rejected</c> con este código, nunca una excepción; en las rutas, 422 con <c>data</c> (404 los inexistentes). Mensajes en español que
/// dicen qué hacer (FR-013). (nuevo)
/// </summary>
public static class ErroresDeAportes
{
    public const string CodigoOperacionInexistente = "Lending.Savings.OperationNotFound";
    public const string CodigoNoOperable = "Lending.Savings.AccountNotOperable";
    public const string CodigoClaseDeMedio = "Lending.Savings.MeansClassNotAllowed";
    public const string CodigoAntesDelCorte = "Lending.Savings.BeforeCutoff";
    public const string CodigoFechaValor = "Lending.Savings.ValueDateOutOfRange";
    public const string CodigoOrigenDeFondos = "Lending.Savings.SourceOfFundsRequired";
    public const string CodigoEntregaNoDisponible = "Lending.Savings.DeliveryNotAvailable";
    public const string CodigoCuotaAjena = "Lending.Contributions.InstallmentNotOfAccount";
    public const string CodigoCuotaPagada = "Lending.Contributions.InstallmentNotPending";
    public const string CodigoExcedenteNoAdmitido = "Lending.Contributions.OverpaymentNotAllowed";
    public const string CodigoModalidadNoAdmitida = "Lending.Contributions.ModalityNotAllowed";
    public const string CodigoLimiteDeParticipacion = "Lending.Contributions.ParticipationLimitExceeded";
    public const string CodigoNoRetirable = "Lending.Contributions.NotWithdrawable";
    public const string CodigoMinimo = "Lending.Contributions.MinimumBreached";
    public const string CodigoConCreditosVivos = "Lending.Contributions.HasLiveLoans";
    public const string CodigoMinimoDeLaEntidad = "Lending.Contributions.EntityMinimumBreached";
    public const string CodigoCesionNoAdmitida = "Lending.Contributions.AssignmentNotAllowed";
    public const string CodigoSolicitudInexistente = "Lending.Contributions.RequestNotFound";
    public const string CodigoSolicitudDecidida = "Lending.Contributions.RequestAlreadyDecided";
    public const string CodigoExcesoInexistente = "Lending.Contributions.NoParticipationExcess";
    public const string CodigoSinCuentaDeAportes = "Lending.Contributions.AccountNotFound";
    public const string CodigoReversoDeOtro = "Lending.Savings.ReversalOwnedByCollector";

    public static Error OperacionInexistente() => new(CodigoOperacionInexistente, "No hay una operación de este recaudador con esa clave.");

    public static Error NoOperable(string numero, MemberAccountStatus estado, string? motivo = null) => new ErrorConDatos(CodigoNoOperable,
        motivo ?? $"La cuenta {numero} está «{estado}»: no recibe esta operación.", new { status = estado.ToString(), holds = Array.Empty<string>() });

    public static Error ClaseDeMedio(ClaseDeMedioDeRecaudo clase, ClaseDeRecaudador recaudador) => new ErrorConDatos(CodigoClaseDeMedio,
        $"El medio {clase} no se admite para {recaudador} en las cuentas.", new { meansClass = clase.ToString(), collectorClass = recaudador.ToString() });

    public static Error AntesDelCorte(DateOnly fecha, DateOnly activacion) => new ErrorConDatos(CodigoAntesDelCorte,
        $"La fecha valor {Fecha(fecha)} es anterior a la activación ({Fecha(activacion)}): lo de antes del corte va en la carga de la migración.",
        new { valueDate = Fecha(fecha), activationDate = Fecha(activacion) });

    public static Error FechaValor(DateOnly fecha, DateOnly minima, DateOnly maxima) => new ErrorConDatos(CodigoFechaValor,
        $"La fecha valor {Fecha(fecha)} está fuera del rango admitido ({Fecha(minima)} a {Fecha(maxima)}).",
        new { valueDate = Fecha(fecha), min = Fecha(minima), max = Fecha(maxima) });

    public static Error OrigenDeFondos(decimal umbral, decimal monto) => new ErrorConDatos(CodigoOrigenDeFondos,
        $"La consignación en efectivo de {monto:N0} alcanza el umbral ({umbral:N0}): adjunte la declaración de origen de fondos.",
        new { threshold = umbral, amount = monto });

    public static Error EntregaNoDisponible(string que, string desde) => new ErrorConDatos(CodigoEntregaNoDisponible,
        $"{que} llega con la entrega {desde} de captaciones: todavía no está disponible.", new { nature = nameof(LineNature.Deposit), availableFrom = desde });

    public static Error CuotaAjena(Guid cuota) => new ErrorConDatos(CodigoCuotaAjena,
        "La cuota nombrada no es de esta cuenta.", new { installmentPublicId = cuota });

    public static Error CuotaPagada(string periodo) => new ErrorConDatos(CodigoCuotaPagada,
        $"La cuota {periodo} ya está pagada o anulada: no recibe más.", new { period = periodo });

    public static Error ExcedenteNoAdmitido(decimal excedente) => new ErrorConDatos(CodigoExcedenteNoAdmitido,
        $"La consignación supera lo exigible en {excedente:N2} y la línea no admite aportes extraordinarios: consigne lo exigible.",
        new { surplus = excedente });

    public static Error ModalidadNoAdmitida(ModalidadDeConsignacion modalidad) => new ErrorConDatos(CodigoModalidadNoAdmitida,
        $"La modalidad {modalidad} no aplica a esta cuenta.", new { modality = modalidad.ToString() });

    public static Error LimiteDeParticipacion(decimal limite, decimal actual, decimal pedido, string fuente) => new ErrorConDatos(CodigoLimiteDeParticipacion,
        $"El aporte supera el límite individual de participación ({limite:P2} del capital social): con él quedaría en {(actual + pedido):N2}.",
        new { limit = limite, current = actual, requested = pedido, source = fuente });

    public static Error NoRetirable() => new(CodigoNoRetirable,
        "Los aportes no se retiran por el contrato: se devuelven con el retiro del asociado o con una devolución aprobada.");

    public static Error Minimo(decimal minimo, decimal saldo, decimal pedido) => new ErrorConDatos(CodigoMinimo,
        $"La devolución dejaría los aportes ({saldo:N2}) por debajo del mínimo no reducible ({minimo:N2}).",
        new { minimum = minimo, balance = saldo, requested = pedido });

    public static Error ConCreditosVivos(IReadOnlyList<string> creditos) => new ErrorConDatos(CodigoConCreditosVivos,
        $"Los aportes respaldan créditos vivos ({string.Join(", ", creditos)}): no se devuelven ni se ceden mientras los tenga.",
        new { loans = creditos });

    public static Error MinimoDeLaEntidad(decimal minimo, decimal capital) => new ErrorConDatos(CodigoMinimoDeLaEntidad,
        $"La devolución dejaría el capital social ({capital:N2}) por debajo del mínimo irreducible de la entidad ({minimo:N2}): queda pendiente.",
        new { entityMinimum = minimo, capital });

    public static Error CesionNoAdmitida() => new(CodigoCesionNoAdmitida,
        "Los estatutos (la línea de aportes) no permiten ceder aportes entre asociados.");

    public static Error SolicitudInexistente() => new(CodigoSolicitudInexistente, "La solicitud de devolución o de cesión no existe.");

    public static Error SolicitudDecidida(string estado) => new ErrorConDatos(CodigoSolicitudDecidida,
        $"La solicitud ya está «{estado}».", new { status = estado });

    public static Error SinCuentaDeAportes() => new(CodigoSinCuentaDeAportes, "La persona no tiene una cuenta de aportes viva.");

    public static Error ReversoDeOtro() => new(CodigoReversoDeOtro,
        "Esta operación la aplicó un recaudador (o Nómina): la reversa él con su clave, no la Cartera.");

    public static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
