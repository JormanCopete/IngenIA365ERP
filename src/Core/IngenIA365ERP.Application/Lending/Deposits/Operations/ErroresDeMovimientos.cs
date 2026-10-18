using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Application.Lending.Deposits.Operations;

/// <summary>
/// Los errores de los movimientos de las cuentas —consignar, retirar, trasladar, reversar, los cargos y el débito automático— (feature 014, US4, T111,
/// T116, T119; contrato-de-cuentas §3.14; api.md §1.8, §4, §9). Un rechazo del contrato es <c>Rejected</c> con este código; en las rutas, 422 con
/// <c>data</c> (404 los inexistentes; 202 la espera de aprobación). Mensajes en español que dicen qué hacer (FR-013). (nuevo)
/// </summary>
public static class ErroresDeMovimientos
{
    public const string CodigoDisponibleInsuficiente = "Lending.Savings.InsufficientAvailable";
    public const string CodigoFaltanFirmas = "Lending.Savings.SignaturesMissing";
    public const string CodigoTopeDeRetiro = "Lending.Savings.WithdrawalLimitExceeded";
    public const string CodigoTopeDeEfectivo = "Lending.Savings.CashLimitExceeded";
    public const string CodigoPendienteDeAprobacion = "Lending.Savings.PendingApproval";
    public const string CodigoRetiroNoAdmitido = "Lending.Savings.WithdrawalNotAllowed";
    public const string CodigoCdatVigente = "Lending.TermDeposit.NotMatured";
    public const string CodigoDestinoNoOperable = "Lending.Savings.TargetNotOperable";
    public const string CodigoMismaCuenta = "Lending.Savings.SameAccount";
    public const string CodigoReversoSobregira = "Lending.Savings.ReversalWouldOverdraw";
    public const string CodigoSobregirada = "Lending.Savings.Overdrawn";
    public const string CodigoCierreBloqueado = "Lending.Savings.CloseBlocked";
    public const string CodigoGmfSinParametros = "Lending.Gmf.ParametersInvalid";
    public const string CodigoCargoNoEnLaLinea = "Lending.Savings.ChargeNotInLine";
    public const string CodigoCargoExonerado = "Lending.Savings.ChargeExempt";
    public const string CodigoCargoInexistente = "Lending.Savings.ChargeNotFound";
    public const string CodigoNoEsDelModulo = "Lending.Savings.ReversalOwnedByProcess";
    public const string CodigoRetiroDeAsociadoEnCurso = "Lending.Savings.MembershipWithdrawalInProgress";

    public static Error DisponibleInsuficiente(DisponibleCalculado d, decimal pedido) => new ErrorConDatos(CodigoDisponibleInsuficiente,
        string.Create(CultureInfo.InvariantCulture,
            $"La operación de {pedido:N2} supera el disponible ({d.Disponible:N2}): saldo {d.Saldo:N2}, bloqueado {d.Retenido:N2}, embargado {d.Embargado:N2}, en canje {d.EnCanje:N2}, mínimo {d.Minimo:N2}."),
        new
        {
            balance = d.Saldo, holds = d.Retenido, garnishments = d.Embargado, pendingClearance = d.EnCanje, minimumBalance = d.Minimo,
            available = d.Disponible, requested = pedido,
        });

    public static Error FaltanFirmas(FirmasRevisadas f, IReadOnlyDictionary<int, Guid> personas) => new ErrorConDatos(CodigoFaltanFirmas,
        $"{f.Explicacion} Faltan {f.Faltantes.Count} firma(s).",
        new
        {
            handlingMode = f.Manejo.ToString(),
            required = f.Requeridas.Select(p => personas.GetValueOrDefault(p)).ToList(),
            provided = f.Presentadas.Select(p => personas.GetValueOrDefault(p)).ToList(),
            missing = f.Faltantes.Select(p => personas.GetValueOrDefault(p)).ToList(),
        });

    public static Error TopeDeRetiro(decimal? porOperacion, decimal? porDia, decimal usadoHoy, decimal pedido) => new ErrorConDatos(CodigoTopeDeRetiro,
        string.Create(CultureInfo.InvariantCulture,
            $"El retiro de {pedido:N2} supera el máximo de la línea (por operación {Tope(porOperacion)}, por día {Tope(porDia)}; usado hoy {usadoHoy:N2})."),
        new { perOperation = porOperacion, perDay = porDia, usedToday = usadoHoy, requested = pedido });

    public static Error TopeDeEfectivo(decimal maximo, decimal pedido, decimal usadoHoy) => new ErrorConDatos(CodigoTopeDeEfectivo,
        string.Create(CultureInfo.InvariantCulture, $"El efectivo de {pedido:N2} supera el máximo de la línea ({maximo:N2}; usado hoy {usadoHoy:N2})."),
        new { maxCash = maximo, requested = pedido, usedToday = usadoHoy });

    public static Error PendienteDeAprobacion(Guid aprobacion, string permiso, decimal monto, decimal maximo) => new ErrorConDatos(CodigoPendienteDeAprobacion,
        string.Create(CultureInfo.InvariantCulture,
            $"La operación de {monto:N2} supera su atribución ({maximo:N2}): queda en espera de un aprobador presente con segundo factor y no mueve saldo hasta aprobarse.")
        + " Repita con la misma clave y la aprobación.",
        new { status = "PendingApproval", approvalPublicId = aprobacion, required = new { permissionCode = permiso, amount = monto, maxAmount = maximo } });

    public static Error RetiroNoAdmitido(string numero) => new ErrorConDatos(CodigoRetiroNoAdmitido,
        $"La línea de la cuenta {numero} no admite retiros parciales: su saldo se entrega con el retiro del asociado o al cancelarla.", new { account = numero });

    public static Error CdatVigente(string numero) => new ErrorConDatos(CodigoCdatVigente,
        $"El CDAT {numero} no ha vencido: no se retira por el contrato (se redime por su ruta).", new { account = numero });

    public static Error DestinoNoOperable(string numero, MemberAccountStatus estado) => new ErrorConDatos(CodigoDestinoNoOperable,
        $"La cuenta destino {numero} está «{estado}»: no recibe el traslado.", new { account = numero, status = estado.ToString() });

    public static Error MismaCuenta() => new(CodigoMismaCuenta, "El traslado es entre dos cuentas distintas.");

    public static Error ReversoSobregira(Guid operacion, string numero) => new ErrorConDatos(CodigoReversoSobregira,
        $"Reversar la consignación dejaría la cuenta en negativo: la operación {numero} ya usó ese dinero. Reverse primero esa operación.",
        new { blockingOperation = new { publicId = operacion, number = numero } });

    public static Error GmfSinParametros(string detalle) => new ErrorConDatos(CodigoGmfSinParametros,
        $"El GMF no se puede calcular con los parámetros vigentes: {detalle}", new { detail = detalle });

    public static Error CierreBloqueado(IReadOnlyList<object> bloqueos) => new ErrorConDatos(CodigoCierreBloqueado,
        "La cuenta no se puede cancelar mientras tenga bloqueos, embargos o valores en canje.", new { blockers = bloqueos });

    public static Error CargoNoEnLaLinea(string codigo) => new ErrorConDatos(CodigoCargoNoEnLaLinea,
        $"La línea de la cuenta no tiene habilitado el cargo {codigo}.", new { chargeCode = codigo });

    public static Error CargoExonerado(string codigo, string? exoneracion, string explicacion) => new ErrorConDatos(CodigoCargoExonerado,
        explicacion, new { chargeCode = codigo, exemption = exoneracion });

    public static Error CargoInexistente() => new(CodigoCargoInexistente, "El cargo no existe en esta cuenta.");

    public static Error DelProceso(string numero) => new ErrorConDatos(CodigoNoEsDelModulo,
        $"La operación {numero} la hizo un proceso de la Cartera (cruce del retiro, débito automático, cargo): se reversa desde su origen.",
        new { operation = numero });

    public static Error RetiroDeAsociadoEnCurso(Guid persona, string numero) => new ErrorConDatos(CodigoRetiroDeAsociadoEnCurso,
        $"El titular tiene el retiro {numero} aprobado o liquidado: la cuenta sólo recibe las operaciones del proceso de retiro (FR-030).",
        new { personPublicId = persona, withdrawalNumber = numero });

    private static string Tope(decimal? valor) => valor is { } v ? v.ToString("N2", CultureInfo.InvariantCulture) : "sin tope";
}
