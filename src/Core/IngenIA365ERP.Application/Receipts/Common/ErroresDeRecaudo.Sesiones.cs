using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Receipts.Common;

// Feature 015, US2 (T0092–T0095; data-model §2, §4.4, §21; DT §2.7): los errores de la sesión de caja, el arqueo, la bóveda y el cierre del día. Los de
// data-model §21 conservan su código; los demás nacen aquí (nuevo) y quedan registrados en DT §2.7.
public static partial class ErroresDeRecaudo
{
    // data-model §21
    public const string CodigoSesionRequerida = "Receipts.Session.Required";
    public const string CodigoSesionYaAbierta = "Receipts.Session.AlreadyOpen";
    public const string CodigoSesionNoAbierta = "Receipts.Session.NotOpen";
    public const string CodigoSesionOlvidada = "Receipts.Session.Forgotten";
    public const string CodigoCajeroExclusivo = "Receipts.Session.CashierExclusive";
    public const string CodigoTopeDeBloqueo = "Receipts.Session.CashBlockCapReached";
    public const string CodigoDiferenciaPorAprobar = "Receipts.Count.DifferenceNeedsApproval";
    public const string CodigoAutoaprobacionDeArqueo = "Receipts.Count.SelfApproval";
    public const string CodigoDiaConSesionesAbiertas = "Receipts.Day.SessionsOpen";
    public const string CodigoDiaConDiferencias = "Receipts.Day.DifferencesPending";

    // (nuevo, DT §2.7)
    public const string CodigoNoEsCajeroDeLaCaja = "Receipts.Session.NotCashierOfBox";
    public const string CodigoCajaNoVigente = "Receipts.Session.CashBoxInactive";
    public const string CodigoSinMediosEnElPunto = "Receipts.Session.NoMeansOffered";
    public const string CodigoBaseDistintaDelFondo = "Receipts.Session.BaseMismatch";
    public const string CodigoSesionNoCerrada = "Receipts.Session.NotClosed";
    public const string CodigoNoEsOlvidada = "Receipts.Session.NotForgotten";
    public const string CodigoTestigoRequerido = "Receipts.Session.WitnessRequired";
    public const string CodigoSinMotivo = "Receipts.Count.ReasonRequired";
    public const string CodigoLineaSinDiferenciaPendiente = "Receipts.Count.NotPending";
    public const string CodigoAtribucionDeArqueoInsuficiente = "Receipts.Count.ApproverLimitTooLow";
    public const string CodigoTratamientoInvalido = "Receipts.Count.TreatmentInvalid";
    public const string CodigoDiaCerrado = "Receipts.Day.AlreadyClosed";
    public const string CodigoDobleCustodia = "Receipts.Movement.DoubleCustodyRequired";
    public const string CodigoEfectivoInsuficiente = "Receipts.Movement.InsufficientCash";
    public const string CodigoMovimientoInvalido = "Receipts.Movement.Invalid";
    public const string CodigoSoporteRequerido = "Receipts.Movement.SupportRequired";
    public const string CodigoSaldoDeBovedaInsuficiente = "Receipts.Vault.InsufficientBalance";
    public const string CodigoActaRequerida = "Receipts.Vault.MinutesRequired";
    public const string CodigoBovedaInactiva = "Receipts.Vault.Inactive";
    public const string CodigoAutorizacionDeDescuento = "Receipts.Receivable.AuthorizationRequired";
    public const string CodigoSobranteNoVencido = "Receipts.Surplus.NotDue";
    public const string CodigoSobranteNoEsPasivo = "Receipts.Surplus.NotLiability";

    public static Error SesionRequerida() => new(CodigoSesionRequerida,
        "No tiene una sesión de caja abierta: abra la sesión con la base contada antes de registrar operaciones.");

    public static Error SesionYaAbierta(string quien, Guid sesion) => new ErrorConDatos(CodigoSesionYaAbierta,
        $"La caja ya tiene una sesión abierta por {quien}: ciérrela antes de abrir otra.", new { holder = quien, sessionPublicId = sesion });

    public static Error SesionNoAbierta() => new(CodigoSesionNoAbierta, "La sesión no está abierta: ya se está cerrando o se cerró.");

    public static Error SesionOlvidada() => new(CodigoSesionOlvidada,
        "La sesión quedó abierta después de la hora de corte: no recibe operaciones. El supervisor la cierra con arqueo diferido y testigo.");

    public static Error CajeroExclusivo(string caja) => new ErrorConDatos(CodigoCajeroExclusivo,
        $"Ya tiene una sesión abierta en la caja {caja}: un cajero tiene a lo sumo una sesión abierta.", new { cashBox = caja });

    public static Error TopeDeBloqueo(decimal tope) => new ErrorConDatos(CodigoTopeDeBloqueo,
        $"El efectivo de la caja llegó al tope de bloqueo ({tope:N0}): traslade a la bóveda antes de recibir más efectivo.", new { cap = tope });

    public static Error DiferenciaPorAprobar(decimal monto) => new ErrorConDatos(CodigoDiferenciaPorAprobar,
        $"El arqueo tiene diferencias sobre la tolerancia por {monto:N0}: otra persona tiene que aprobarlas con motivo.", new { amount = monto });

    public static Error AutoaprobacionDeArqueo() => new(CodigoAutoaprobacionDeArqueo,
        "La diferencia de un arqueo no la aprueba el cajero de la sesión ni quien contó: tiene que hacerlo otra persona.");

    public static Error DiaConSesionesAbiertas(int cuantas) => new ErrorConDatos(CodigoDiaConSesionesAbiertas,
        $"El día del punto tiene {cuantas} sesión(es) sin cerrar: ciérrelas antes de cerrar el día.", new { sessions = cuantas });

    public static Error DiaConDiferencias(int cuantas) => new ErrorConDatos(CodigoDiaConDiferencias,
        $"El día del punto tiene {cuantas} diferencia(s) de arqueo por aprobar: apruébelas antes de cerrar el día.", new { differences = cuantas });

    public static Error NoEsCajeroDeLaCaja() => new(CodigoNoEsCajeroDeLaCaja,
        "No es cajero vigente de esta caja: pida que lo asignen antes de abrirla.");

    public static Error CajaNoVigente() => new(CodigoCajaNoVigente, "La caja o su punto no están activos y vigentes hoy.");

    public static Error SinMediosEnElPunto() => new(CodigoSinMediosEnElPunto,
        "El punto no ofrece ningún medio de pago vigente: configure los medios del punto antes de abrir la caja.");

    public static Error BaseDistintaDelFondo(decimal fondo, decimal contado) => new ErrorConDatos(CodigoBaseDistintaDelFondo,
        $"La caja trabaja con fondo fijo de {fondo:N0} y se contaron {contado:N0}: la base tiene que ser el fondo.", new { fund = fondo, counted = contado });

    public static Error SesionNoCerrada() => new(CodigoSesionNoCerrada, "Sólo una sesión cerrada admite un ajuste.");

    public static Error NoEsOlvidada() => new(CodigoNoEsOlvidada,
        "La sesión no está olvidada: la cierra su cajero con el arqueo de cierre.");

    public static Error TestigoRequerido() => new(CodigoTestigoRequerido,
        "El arqueo diferido lleva un testigo distinto del supervisor y del cajero de la sesión.");

    public static Error SinMotivo(IReadOnlyList<string> medios) => new ErrorConDatos(CodigoSinMotivo,
        $"Escriba el motivo de la diferencia de: {string.Join(", ", medios)}.", new { means = medios });

    public static Error LineaSinDiferenciaPendiente() => new(CodigoLineaSinDiferenciaPendiente,
        "Esa línea del arqueo no tiene una diferencia por aprobar.");

    public static Error AtribucionDeArqueoInsuficiente(decimal maximo, decimal monto) => new ErrorConDatos(CodigoAtribucionDeArqueoInsuficiente,
        $"Su atribución para aprobar diferencias de arqueo llega a {maximo:N0} y la diferencia es de {monto:N0}.", new { limit = maximo, amount = monto });

    public static Error TratamientoInvalido() => new(CodigoTratamientoInvalido,
        "Un faltante va a cuenta por cobrar al cajero o a gasto; un sobrante, a sobrantes.");

    public static Error DiaCerrado() => new(CodigoDiaCerrado, "El día del punto ya está cerrado.");

    public static Error DobleCustodia() => new(CodigoDobleCustodia,
        "Con doble custodia, el movimiento lleva un segundo custodio vigente de la bóveda, distinto de quien lo registra.");

    public static Error EfectivoInsuficiente(decimal disponible) => new ErrorConDatos(CodigoEfectivoInsuficiente,
        $"La caja sólo tiene {disponible:N0} en efectivo esperado.", new { available = disponible });

    public static Error MovimientoInvalido(string motivo) => new(CodigoMovimientoInvalido, motivo);

    public static Error SoporteRequerido() => new(CodigoSoporteRequerido,
        "Reclasificar desde efectivo exige el soporte cotejado (la transferencia o el comprobante) adjunto.");

    public static Error SaldoDeBovedaInsuficiente(decimal valor) => new ErrorConDatos(CodigoSaldoDeBovedaInsuficiente,
        $"La bóveda no tiene esas denominaciones: falta {valor:N0}.", new { missing = valor });

    public static Error ActaRequerida() => new(CodigoActaRequerida, "El arqueo sorpresivo y el billete retenido llevan su acta adjunta.");

    public static Error BovedaInactiva() => new(CodigoBovedaInactiva, "La bóveda de la oficina está inactiva.");

    public static Error AutorizacionDeDescuento() => new(CodigoAutorizacionDeDescuento,
        "Descontar un faltante por Nómina exige la autorización escrita del cajero para este caso, adjunta.");

    public static Error SobranteNoVencido(DateOnly? desde) => new ErrorConDatos(CodigoSobranteNoVencido,
        desde is { } d
            ? $"El sobrante pasa a ingreso desde el {d:dd/MM/yyyy}."
            : "La cooperativa no ha fijado los días que un sobrante espera antes de pasar a ingreso (Caja.Sobrantes.DiasAntesDeIngreso): sigue como pasivo.",
        new { from = desde });

    public static Error SobranteNoEsPasivo() => new(CodigoSobranteNoEsPasivo, "El sobrante ya se llevó a ingreso o se devolvió.");
}
