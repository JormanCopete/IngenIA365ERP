using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Replay;

/// <summary>
/// Lo que deja causar un día un crédito (US4, T0489): las filas <c>Accrual</c> de ese día valor por concepto y cuota, la transición de
/// la causación si la hubo (para <c>CausacionSuspendida</c>/<c>CausacionReanudada</c>), el estado del crédito a ese día y, si no se pudo
/// causar, el problema (sin filas).
/// </summary>
public sealed record ResultadoDeCausacion(
    DateOnly Fecha,
    IReadOnlyList<MovimientoDeLibro> Filas,
    TransicionDeEstado? Transicion,
    EstadoDelCredito Estado,
    ProblemaDeCartera? Problema)
{
    public bool Causo => Problema is null;
}

/// <summary>
/// Causar un crédito de d − 1 a d (feature 013, US4, T0489; T6, T25, R33; FR-062–FR-066, SC-020): exige que la marca de agua sea la
/// víspera (<see cref="CodigoFueraDeOrden"/>), reconstruye a d y devuelve sólo lo de ese día valor. Repetir el mismo día con la misma
/// entrada da lo mismo (es una función); que no se escriba dos veces lo garantiza la marca, que tras causar d ya no es d − 1. Sin
/// usura certificada para d (y la modalidad la exige) no causa: devuelve <see cref="CodigoValorNacionalFaltante"/> y la marca no
/// avanza (D11). Puro. (nuevo)
/// </summary>
public static class CausacionDeCredito
{
    /// <summary>Se pidió causar un día que no es el siguiente a la marca de agua.</summary>
    public const string CodigoFueraDeOrden = "Lending.Accrual.OutOfOrder";

    /// <summary>El día no tiene usura certificada para la modalidad.</summary>
    public const string CodigoValorNacionalFaltante = "NationalValues.Missing";

    public static ResultadoDeCausacion Causar(EntradaDeReconstruccion entrada, DateOnly ultimaCausada, DateOnly dia,
        IReadOnlyDictionary<LoanOperationKind, IInterpreteDeOperacion>? interpretes = null)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        if (dia != ultimaCausada.AddDays(1))
            throw new ReglaDeCarteraException(new ProblemaDeCartera(CodigoFueraDeOrden,
                $"Se causa un día a la vez y en orden: la marca está en {ExplicacionDeCartera.Fecha(ultimaCausada)} y se pidió {ExplicacionDeCartera.Fecha(dia)}.",
                new Dictionary<string, object?> { ["lastAccruedDate"] = ultimaCausada, ["date"] = dia }));

        var estado = ReconstruccionDeCredito.Reconstruir(entrada, dia, dia, interpretes);
        if (estado.Origen is not { } origen || dia <= origen)
            return new ResultadoDeCausacion(dia, [], null, estado, null);
        if (entrada.Condiciones.UsuraRequerida && estado.PrimerDiaSinUsura is { } sinUsura && sinUsura <= dia)
            return new ResultadoDeCausacion(dia, [], null, estado, ProblemaDeCartera.Con(CodigoValorNacionalFaltante,
                $"No hay usura certificada para el {ExplicacionDeCartera.Fecha(sinUsura)}: el crédito no se causa hasta que se replique.", "date", sinUsura));

        var filas = estado.Libro.DelDia(dia).Where(f => f.Clase == LoanTransactionKind.Accrual).ToList();
        var transicion = estado.Transiciones.LastOrDefault(t => t.Fecha == dia);
        return new ResultadoDeCausacion(dia, filas, transicion, estado, null);
    }
}
