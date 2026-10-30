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

    // -------------------------------------------------------------------------------------------------------- órdenes manuales --
    public const string CodigoOrdenManualConceptoInactivo = "Treasury.ManualOrder.ConceptInactive";
    public const string CodigoOrdenManualSobreElTope = "Treasury.ManualOrder.OverConceptCap";
    public const string CodigoOrdenManualBeneficiarioPrepara = "Treasury.ManualOrder.BeneficiaryIsPreparer";
    public const string CodigoOrdenManualBeneficiarioAprueba = "Treasury.ManualOrder.BeneficiaryIsApprover";
    public const string CodigoOrdenManualDerivarACausacion = "Treasury.ManualOrder.DeriveToAccountingPayable";
    public const string CodigoOrdenManualRetencionInvalida = "Treasury.ManualOrder.InvalidWithholding";
    public const string CodigoOrdenManualAutoaprobada = "Treasury.ManualOrder.SelfApproval";
    public const string CodigoOrdenManualMismoAprobador = "Treasury.ManualOrder.SameApprover";
    public const string CodigoOrdenManualNoPendiente = "Treasury.ManualOrder.NotPendingApproval";
    public const string CodigoOrdenManualSinAprobar = "Treasury.ManualOrder.PendingApproval";

    public static Error OrdenManualConceptoInactivo() => new(CodigoOrdenManualConceptoInactivo,
        "El concepto no está activo en el catálogo de órdenes manuales: elija uno activo o pida que lo habiliten.");

    public static Error OrdenManualSobreElTope(decimal tope) => new ErrorConDatos(CodigoOrdenManualSobreElTope,
        $"La orden pasa del tope del concepto ({Pesos(tope)}): divídala o tramítela como causación en Contabilidad.", new { maxAmount = tope });

    public static Error OrdenManualBeneficiarioPrepara() => new(CodigoOrdenManualBeneficiarioPrepara,
        "Usted es el beneficiario de la orden: no puede prepararla. La prepara otra persona.");

    public static Error OrdenManualBeneficiarioAprueba() => new(CodigoOrdenManualBeneficiarioAprueba,
        "Usted es el beneficiario de la orden: no puede aprobarla. La aprueban otras dos personas.");

    public static Error OrdenManualDerivarACausacion(string? nota) => new ErrorConDatos(CodigoOrdenManualDerivarACausacion,
        "El concepto exige la retención en la fuente que corresponda: regístrela en la orden o cause primero la cuenta por pagar en Contabilidad y pague desde allá.",
        new { withholdingNote = nota });

    public static Error OrdenManualRetencionInvalida() => new(CodigoOrdenManualRetencionInvalida,
        "La retención tiene que ser menor que el valor bruto de la orden.");

    public static Error OrdenManualAutoaprobada() => new(CodigoOrdenManualAutoaprobada,
        "Usted preparó esta orden manual: la aprueban otras dos personas con el permiso de aprobar órdenes manuales.");

    public static Error OrdenManualMismoAprobador() => new(CodigoOrdenManualMismoAprobador,
        "Usted ya dio la primera aprobación de esta orden: la segunda la da otra persona.");

    public static Error OrdenManualNoPendiente(string numero) => new(CodigoOrdenManualNoPendiente,
        $"La orden manual {numero} no está esperando aprobación.");

    // ------------------------------------------------------------------------------------------------------ firmantes y tokens --
    public const string CodigoCambioDeFirmantesAutoaprobado = "Treasury.Signer.SelfApproval";
    public const string CodigoFirmanteEstado = "Treasury.Signer.InvalidState";
    public const string CodigoFirmanteRepetido = "Treasury.Signer.AlreadyActive";
    public const string CodigoTokenRepetido = "Treasury.Token.SerialInUse";

    public static Error CambioDeFirmantesAutoaprobado() => new(CodigoCambioDeFirmantesAutoaprobado,
        "Usted pidió este cambio de firmantes o tokens: lo aprueba otra persona con el permiso de aprobar firmantes.");

    public static Error FirmanteEstado(string estado, string esperado) => new(CodigoFirmanteEstado,
        $"El firmante o token está {estado}: sólo se puede hacer esto si está {esperado}.");

    public static Error FirmanteRepetido() => new(CodigoFirmanteRepetido,
        "Esa persona ya es firmante vigente de la cuenta con esa clase de firma: retire la vigencia anterior antes de registrar otra.");

    public static Error TokenRepetido(string serial) => new(CodigoTokenRepetido,
        $"El token {serial} ya está vigente en una cuenta: retírelo antes de asignarlo otra vez.");
}
