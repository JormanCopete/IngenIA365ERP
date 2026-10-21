using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Lending.Payments;

namespace IngenIA365ERP.Application.Lending.Payments;

/// <summary>
/// Los códigos y mensajes de los pagos de Cartera (feature 013, US5, bloque 9B; data-model §10.1; contracts/interfaces.md §3.16; api.md §10).
/// Un código por significado (C115): <c>Lending.Payment.PreferenceMissing</c> es sólo el rechazo de la forma interactiva; el aviso de un abono que
/// quedó como saldo a favor es <c>Lending.Payment.PreferenceAssumed</c>. Los mensajes van en español, dicen qué hacer y nunca muestran Id
/// internos (FR-013). Los nombres marcados <b>(nuevo)</b> se llevaron a data-model §10.1 por C111. (nuevo)
/// </summary>
public static class ErroresDePagos
{
    public const string CodigoNoEncontrado = "Generic.NotFound";
    public const string CodigoValidacion = "Validation.Invalid";
    public const string CodigoNoActivada = ContratoDeObligacionesSinActivar.CodigoNoActivada;
    public const string CodigoTransaccionRequerida = "Lending.Contract.TransactionRequired";
    public const string CodigoRecaudadorNoRegistrado = "Lending.Contract.CollectorNotRegistered";
    public const string CodigoVersionNoSoportada = "Integration.Participant.VersionNotSupported";
    public const string CodigoClaveReusada = "Lending.Contract.KeyReused";
    public const string CodigoAntesDelCorte = "Lending.Contract.BeforeCutoff";
    public const string CodigoClaseDeMedioDesconocida = "Lending.Contract.UnknownMeansClass";
    public const string CodigoClaseDeMedioNoAdmitida = "Lending.Payment.MeansClassNotAllowed";
    public const string CodigoNoPagable = "Lending.Payment.ObligationNotPayable";
    public const string CodigoFechaValorFueraDeRango = "Lending.Payment.ValueDateOutOfRange";
    public const string CodigoFechaValorAntesDelCierre = "Lending.Payment.ValueDateBeforeClosing";
    public const string CodigoPreferenciaFaltante = "Lending.Payment.PreferenceMissing";
    public const string CodigoDistribucionCambiada = "Lending.Payment.DistributionChanged";
    public const string CodigoCuotaEspecificaBloqueada = AplicadorDePagos.CodigoCuotaEspecificaBloqueada;
    public const string CodigoModalidadNoAdmitida = AplicadorDePagos.CodigoModalidadNoAdmitida;
    public const string CodigoReversoDeReverso = "Lending.Reversal.OfReversal";
    public const string CodigoDelRecaudador = "Lending.Reversal.OwnedByCollector";
    public const string CodigoVistaPreviaCambiada = "Lending.Reversal.PreviewChanged";

    /// <summary>(nuevo, C111) El pago ya tiene un reverso registrado con otra clave o desde otra puerta.</summary>
    public const string CodigoYaReversado = "Lending.Reversal.AlreadyReversed";

    public const string CodigoNoEsPago = "Lending.LoanOperation.NotPayment";
    public const string CodigoSaldoInsuficiente = "Lending.CreditBalance.Insufficient";
    public const string CodigoDestinoAjeno = "Lending.CreditBalance.TargetNotOwned";
    public const string CodigoCreditoNoActivo = "Lending.Loan.NotActive";

    /// <summary>Aviso del no descontado (interfaces §3.4): el código del éxito, el mensaje dice que la cuota sigue exigible.</summary>
    public const string MensajeNoDescontado = "No descontado registrado: la cuota sigue exigible.";

    public static Error NoEncontrado() => new(CodigoNoEncontrado, "El recurso no existe.");

    public static Error PagoInexistente() => new(CodigoNoEncontrado, "No hay un pago de este recaudador con esa clave.");

    public static Error ObligacionInexistente() => new(CodigoNoEncontrado, "La obligación no existe.");

    public static Error TransaccionRequerida() => new(CodigoTransaccionRequerida,
        "El contrato de obligaciones debe llamarse dentro de la transacción del recaudador.");

    public static Error RecaudadorNoRegistrado(string codigo) => new ErrorConDatos(CodigoRecaudadorNoRegistrado,
        $"El recaudador {codigo} no está habilitado en esta cooperativa para el contrato de obligaciones.", new { collectorCode = codigo });

    public static Error VersionNoSoportada(int version) => new ErrorConDatos(CodigoVersionNoSoportada,
        $"Cartera no acepta la versión {version} de {IngenIA365ERP.Application.Common.Integration.ContratosDeIntegracion.ProveedorDeObligaciones}.",
        new { contract = IngenIA365ERP.Application.Common.Integration.ContratosDeIntegracion.ProveedorDeObligaciones, version, supported = IngenIA365ERP.Application.Common.Integration.ContratosDeIntegracion.VersionesDelContratoDeObligaciones });

    public static Error ClaveReusada(string clave) => new ErrorConDatos(CodigoClaveReusada,
        "Esta clave ya se usó con otros valores. Use una clave nueva para una operación distinta.", new { idempotencyKey = clave });

    public static Error AntesDelCorte(DateOnly fecha, DateOnly corte) => new ErrorConDatos(CodigoAntesDelCorte,
        $"El pago tiene fecha valor {Fecha(fecha)}, igual o anterior al corte de la migración ({Fecha(corte)}): debía venir en la carga.",
        new { valueDate = fecha, cutoffDate = corte });

    public static Error ClaseDeMedioDesconocida(object clase) => new ErrorConDatos(CodigoClaseDeMedioDesconocida,
        $"Cartera no acepta la clase de medio {clase}.", new { meansClass = clase.ToString() });

    public static Error ClaseDeMedioDeCredito(ClaseDeMedioDeRecaudo clase) => new ErrorConDatos(CodigoClaseDeMedioNoAdmitida,
        "Un crédito no se paga con otro crédito: use la recogida al desembolsar o una novación.", new { meansClass = clase.ToString() });

    public static Error ClaseDeMedioNoAdmitida(ClaseDeMedioDeRecaudo clase, ClaseDeRecaudador recaudador) => new ErrorConDatos(CodigoClaseDeMedioNoAdmitida,
        $"El recaudador de clase {recaudador} no recauda con la clase de medio {clase}.", new { meansClass = clase.ToString(), collectorClass = recaudador.ToString() });

    public static Error NoPagable(string numero, string estado) => new ErrorConDatos(CodigoNoPagable,
        $"La obligación {numero} ya no recibe pagos (está {estado}). Aplique el pago a otra obligación; un saldo a favor se resuelve aparte.",
        new { obligationNumber = numero, status = estado });

    public static Error FechaValorFueraDeRango(DateOnly fecha, DateOnly minima, DateOnly maxima, int dias) => new ErrorConDatos(CodigoFechaValorFueraDeRango,
        $"La fecha valor {Fecha(fecha)} está fuera de lo permitido: hasta {dias} días antes de hoy y nunca futura.",
        new { valueDate = fecha, minDate = minima, maxDate = maxima });

    public static Error FechaValorAntesDelCierre(DateOnly fecha, DateOnly cierre) => new ErrorConDatos(CodigoFechaValorAntesDelCierre,
        $"La fecha valor cae antes del cierre de {cierre.ToString("MMMM 'de' yyyy", new CultureInfo("es-CO"))}: necesita la atribución para aplicar con fecha anterior a un cierre por este valor.",
        new { valueDate = fecha, closedThrough = cierre });

    public static Error PreferenciaFaltante() => new(CodigoPreferenciaFaltante,
        "El abono a capital necesita que el deudor elija si reduce el plazo o la cuota.");

    public static Error CreditoNoActivo(string numero, string estado) => new ErrorConDatos(CodigoCreditoNoActivo,
        $"El crédito {numero} no está activo (está {estado}): su preferencia de abono no se cambia.", new { status = estado });

    public static Error ReversoDeReverso() => new(CodigoReversoDeReverso, "Un reverso no se reversa. Para rehacer, registre una operación nueva.");

    public static Error YaReversado(string numero) => new ErrorConDatos(CodigoYaReversado,
        $"El pago {numero} ya fue reversado: un pago tiene a lo sumo un reverso.", new { number = numero });

    public static Error DelRecaudador(string clase) => new ErrorConDatos(CodigoDelRecaudador,
        clase == nameof(ClaseDeRecaudador.Payroll)
            ? "Este pago lo aplicó Nómina y sólo Nómina lo reversa, al reversar su nómina o su liquidación."
            : clase == nameof(ClaseDeRecaudador.OwnLoanPayoff)
                ? "Este pago es la recogida de un desembolso: se reversa al anular el crédito que lo recogió."
                : "Este pago lo aplicó Aportes y sólo Aportes lo reversa.",
        new { collectorClass = clase });

    public static Error VistaPreviaCambiada(string huella) => new ErrorConDatos(CodigoVistaPreviaCambiada,
        "El crédito cambió desde la vista previa del reverso. Revísela otra vez y confirme.", new { previewSha256 = huella });

    public static Error NoEsPago(string numero) => new ErrorConDatos(CodigoNoEsPago,
        $"La operación {numero} no es un pago: sólo los pagos se reversan por aquí.", new { number = numero });

    public static Error SaldoInsuficiente(decimal pedido, decimal disponible) => new ErrorConDatos(CodigoSaldoInsuficiente,
        $"Se pidió resolver {Pesos(pedido)} y hay {Pesos(disponible)}.", new { amount = pedido, available = disponible });

    public static Error DestinoAjeno() => new(CodigoDestinoAjeno,
        "El saldo a favor sólo se aplica a otra obligación del mismo deudor.");

    public static Error ResolucionNoAdmitida() => new ErrorConDatos(CodigoModalidadNoAdmitida,
        "Sólo un saldo a favor que nació de un abono sin preferencia se aplica como abono a capital.", new { form = nameof(FormaDeResolucion.ApplyAsExtraordinaryPrincipal) });

    /// <summary>Un error de forma de la solicitud (<c>data.errors[] { field, code }</c>).</summary>
    public static Error Validacion(IReadOnlyList<(string Campo, string Codigo, string Mensaje)> errores) => new ErrorConDatos(CodigoValidacion,
        errores.Count == 1 ? errores[0].Mensaje : "La solicitud tiene datos inválidos: " + string.Join(" ", errores.Select(e => e.Mensaje)),
        new { errors = errores.Select(e => new { field = e.Campo, code = e.Codigo, message = e.Mensaje }).ToList() });

    public static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Pesos(decimal valor) => valor.ToString("N2", new CultureInfo("es-CO"));
}
