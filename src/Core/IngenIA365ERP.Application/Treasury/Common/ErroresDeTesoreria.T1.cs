using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Treasury.Common;

// Feature 015, T1 (fases 33 a 35; data-model §21): los errores de los traslados entre cuentas propias, del cierre del mes de los cheques, de las órdenes manuales, de los
// firmantes y tokens y del comprobante de pago. Todos (nuevo), en DT §2.7.
public static partial class ErroresDeTesoreria
{
    // ------------------------------------------------------------------------------------------------------------ traslados --
    public const string CodigoTrasladoMismaCuenta = "Treasury.Transfer.SameAccount";
    public const string CodigoTrasladoCuentaInactiva = "Treasury.Transfer.AccountInactive";
    public const string CodigoTrasladoEstado = "Treasury.Transfer.InvalidState";
    public const string CodigoTrasladoAutoaprobado = "Treasury.Transfer.SelfApproval";
    public const string CodigoTrasladoSinSoporte = "Treasury.Transfer.SupportRequired";
    public const string CodigoTrasladoLineaInvalida = "Treasury.Transfer.StatementLineMismatch";

    public static Error TrasladoMismaCuenta() => new(CodigoTrasladoMismaCuenta,
        "El traslado sale de una cuenta y llega a otra: elija dos cuentas propias distintas.");

    public static Error TrasladoCuentaInactiva(string cuenta) => new(CodigoTrasladoCuentaInactiva,
        $"La cuenta {cuenta} no está activa: no recibe ni envía traslados.");

    public static Error TrasladoEstado(string numero, string estado, string esperado) => new(CodigoTrasladoEstado,
        $"El traslado {numero} está {estado}: sólo se puede hacer esto con un traslado {esperado}.");

    public static Error TrasladoAutoaprobado() => new(CodigoTrasladoAutoaprobado,
        "Usted pidió este traslado: lo aprueba otra persona con el permiso de aprobar traslados.");

    public static Error TrasladoSinSoporte() => new(CodigoTrasladoSinSoporte,
        "Confirme el traslado con la constancia del banco o con la línea del extracto de la cuenta de destino.");

    public static Error TrasladoLineaInvalida() => new(CodigoTrasladoLineaInvalida,
        "La línea del extracto no es un abono de la cuenta de destino por el valor del traslado: elija la que corresponde.");

    // ------------------------------------------------------------------------------------------------ cierre del mes de cheques --
    public const string CodigoCierreDeChequesRepetido = "Treasury.MonthEndChecks.AlreadyRun";
    public const string CodigoCierreDeChequesMesAbierto = "Treasury.MonthEndChecks.MonthNotEnded";

    public static Error CierreDeChequesRepetido(string cuenta, int anio, int mes) => new(CodigoCierreDeChequesRepetido,
        $"El cierre del mes de los cheques de {cuenta} para {mes:00}/{anio} ya se hizo: corre una sola vez por cuenta y mes.");

    public static Error CierreDeChequesMesAbierto(int anio, int mes) => new(CodigoCierreDeChequesMesAbierto,
        $"El mes {mes:00}/{anio} todavía no termina: el cierre de los cheques se corre desde el primer día del mes siguiente.");
}
