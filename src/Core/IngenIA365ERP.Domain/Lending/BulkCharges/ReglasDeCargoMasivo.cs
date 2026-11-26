using IngenIA365ERP.Domain.Entities.Lending.BulkCharges;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.AccrualProcesses;

namespace IngenIA365ERP.Domain.Lending.BulkCharges;

/// <summary>
/// Las reglas puras de los cargos masivos (2026-11-25, pedido del dueño; feature 026): a quién se le carga (D3), con qué forma de pago (R8 de la
/// investigación), si el primer pago cae en el ciclo pedido y el período de una fecha. Sin base de datos: quien llama trae los datos. (nuevo)
/// </summary>
public static class ReglasDeCargoMasivo
{
    public const string CodigoFueraDelCiclo = "Lending.BulkCharge.FirstDueDateOutOfCycle";
    public const string CodigoFueraDeRango = "Lending.BulkCharge.OutOfRange";
    public const string CodigoSinTercero = "Lending.BulkCharge.CounterpartRequired";
    public const string CodigoPrimerPagoAnterior = "Lending.BulkCharge.FirstDueDateBeforeCharge";

    /// <summary>Lo que se sabe del asociado para decidir si se le carga.</summary>
    public sealed record Asociado(
        bool EsAsociado,
        string? Estado,
        DateOnly? FechaDeRetiro,
        DateOnly? FechaDeReingreso,
        bool Fallecido,
        bool CasoDeFallecimientoVivo,
        bool RetiroVivo);

    /// <summary>
    /// El motivo por el que el asociado queda fuera a la fecha del cargo, o nulo si se le carga (D3): no asociado, retirado (estado <c>R</c> o fecha de
    /// retiro cumplida sin reingreso posterior), fallecido (marca o caso vivo) o con un retiro de asociado vivo. El cobro jurídico no excluye.
    /// </summary>
    public static string? Exclusion(Asociado a, DateOnly fecha)
    {
        if (!a.EsAsociado) return MotivosDeCargoMasivo.NoAsociado;
        if (a.Fallecido || a.CasoDeFallecimientoVivo) return MotivosDeCargoMasivo.Fallecido;
        if (Retirado(a.Estado, a.FechaDeRetiro, a.FechaDeReingreso, fecha)) return MotivosDeCargoMasivo.Retirado;
        if (a.RetiroVivo) return MotivosDeCargoMasivo.RetiroEnTramite;
        return null;
    }

    /// <summary>Retirado a la fecha: estado <c>R</c> o fecha de retiro cumplida, salvo un reingreso posterior ya cumplido (la regla de originación).</summary>
    public static bool Retirado(string? estado, DateOnly? retiro, DateOnly? reingreso, DateOnly fecha)
    {
        var reingresado = reingreso is { } r && r <= fecha && (retiro is null || r >= retiro);
        if (reingresado) return false;
        return string.Equals(estado?.Trim(), "R", StringComparison.OrdinalIgnoreCase) || retiro is { } w && w <= fecha;
    }

    /// <summary>La forma de pago resuelta de un asociado: la forma, el aviso (si se sustituyó) o el motivo de omisión (sin convenio).</summary>
    public sealed record FormaResuelta(RepaymentChannel? Forma, string? Aviso, string? Omision);

    /// <summary>
    /// La forma de pago del asociado. Con <see cref="BulkChargeChannelMode.Fixed"/>, la fija. Con <see cref="BulkChargeChannelMode.FromAssociate"/>: la
    /// ficha con descuento por nómina (<c>N</c>) va a la pagaduría si la empresa empleadora tiene convenio, si no a la nómina propia; cualquier otra, a
    /// caja. Si la línea no admite la resultante, la de defecto de la línea con el aviso <see cref="MotivosDeCargoMasivo.FormaNoAdmitida"/>. Nómina propia
    /// sin su convenio o pagaduría sin el suyo, omitido.
    /// </summary>
    public static FormaResuelta Forma(BulkChargeChannelMode modo, RepaymentChannel? fija, string? tipoDeDescuentoDeLaFicha, bool hayConvenioDePagaduria,
        bool hayConvenioDeNominaPropia, IReadOnlyCollection<RepaymentChannel> admitidas, RepaymentChannel defecto)
    {
        string? aviso = null;
        RepaymentChannel forma;
        if (modo == BulkChargeChannelMode.Fixed)
            forma = fija ?? defecto;
        else
        {
            var porNomina = string.Equals(tipoDeDescuentoDeLaFicha?.Trim(), "N", StringComparison.OrdinalIgnoreCase);
            forma = !porNomina ? RepaymentChannel.Cashier
                : hayConvenioDePagaduria ? RepaymentChannel.PaymasterDeduction
                : RepaymentChannel.OwnPayroll;
            if (!admitidas.Contains(forma))
            {
                aviso = MotivosDeCargoMasivo.FormaNoAdmitida;
                forma = defecto;
            }
        }

        return forma switch
        {
            RepaymentChannel.PaymasterDeduction when !hayConvenioDePagaduria => new FormaResuelta(null, aviso, MotivosDeCargoMasivo.SinPagaduria),
            RepaymentChannel.OwnPayroll when !hayConvenioDeNominaPropia => new FormaResuelta(null, aviso, MotivosDeCargoMasivo.SinNominaPropia),
            _ => new FormaResuelta(forma, aviso, null),
        };
    }

    /// <summary>¿El primer pago cae en el ciclo pedido? Sin ciclo, siempre.</summary>
    public static bool PrimerPagoEnCiclo(DateOnly primerPago, AccrualDiscountCycle? ciclo) =>
        ciclo is not { } c || PeriodoDeCausacion.EnCicloDeDescuento(primerPago, c);

    /// <summary>El período <c>aaaamm</c> de una fecha.</summary>
    public static int Periodo(DateOnly fecha) => fecha.Year * 100 + fecha.Month;

    /// <summary>¿Es un período <c>aaaamm</c> válido?</summary>
    public static bool PeriodoValido(int periodo) => periodo / 100 is >= 2000 and <= 2999 && periodo % 100 is >= 1 and <= 12;

    /// <summary>
    /// Los datos del cargo que no dependen de la línea: valor positivo, cuotas ≥ 1, primer pago posterior a la fecha del cargo y dentro del ciclo, y la
    /// persona de la contrapartida cuando se pidió una. Nulo si todo está bien.
    /// </summary>
    public static ProblemaDeCartera? ValidarDatos(decimal valor, int cuotas, DateOnly fechaDelCargo, DateOnly primerPago, AccrualDiscountCycle? ciclo,
        BulkChargeCounterpart contrapartida, bool hayPersonaDeContrapartida)
    {
        if (valor <= 0) return ProblemaDeCartera.Con(CodigoFueraDeRango, "El valor del cargo debe ser mayor que cero.", "amount", valor);
        if (cuotas < 1) return ProblemaDeCartera.Con(CodigoFueraDeRango, "El plazo debe ser de al menos una cuota.", "installments", cuotas);
        if (primerPago <= fechaDelCargo)
            return ProblemaDeCartera.Con(CodigoPrimerPagoAnterior, "La fecha del primer pago debe ser posterior a la fecha del cargo.", "firstDueDate",
                primerPago.ToString("yyyy-MM-dd"));
        if (!PrimerPagoEnCiclo(primerPago, ciclo))
            return ProblemaDeCartera.Con(CodigoFueraDelCiclo, "La fecha del primer pago no cae en el ciclo de pago elegido.", "paymentCycle", ciclo?.ToString());
        if (contrapartida == BulkChargeCounterpart.Person && !hayPersonaDeContrapartida)
            return ProblemaDeCartera.Con(CodigoSinTercero, "Elija la persona (proveedor) de la contrapartida.", "counterpartPersonPublicId", null);
        return null;
    }
}
