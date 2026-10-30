using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Receipts.Parameters;

namespace IngenIA365ERP.Application.Receipts.Setup;

/// <summary>
/// Las reglas del alta de una vigencia de las claves <c>RCD</c> (feature 015, T0042; FR-185; TR43; decisiones-transversales §2.5): la plataforma
/// corre éstas en lugar de las de Inventario (<see cref="ReglasDeParametrosPorModulo"/>). La hora de corte (<c>Recaudo.HoraDeCorte</c>) tiene que
/// caer antes del ciclo diario de Cartera de la línea del día de operación: un inicio de madrugada (antes de <see cref="LimiteDeLaMadrugada"/>)
/// se cuenta en el día siguiente y cualquier corte lo cumple; un inicio del mismo día exige un corte anterior. Es una restricción de configuración
/// comprobada, no una dependencia entre tareas. El piso de <c>Conservacion.Anios</c> lo aplica la definición (su mínimo). (nuevo)
/// </summary>
public sealed class ReglasDeParametrosDeRecaudo(TimeOnly inicioDelCicloDeCartera) : IReglasDeParametrosDeModulo
{
    /// <summary>Hasta esta hora, el inicio del ciclo de Cartera es de madrugada y se cuenta en el día siguiente.</summary>
    public static readonly TimeOnly LimiteDeLaMadrugada = new(12, 0);

    /// <summary>La hora de inicio por defecto del ciclo diario de Cartera (<c>Lending:Process:StartTimeLocal</c>).</summary>
    public static readonly TimeOnly InicioPorDefecto = new(0, 30);

    public string Modulo => ParametrosDeRecaudoYTesoreria.ModuloRecaudo;

    /// <summary>La hora de inicio del ciclo diario de Cartera con que se comprueba el corte (la activación la verifica también, fase 19, T0303).</summary>
    public TimeOnly InicioDelCicloDeCartera => inicioDelCicloDeCartera;

    public Task<Result<DecisionDeReglasDeParametro>> EvaluarAsync(AltaDeParametro alta, CancellationToken ct)
    {
        if (alta.Definicion.Clave == ParametrosDeRecaudoYTesoreria.RecaudoHoraDeCorte
            && TimeOnly.TryParseExact(alta.Valor, "HH:mm", out var corte)
            && !CorteValido(corte, inicioDelCicloDeCartera))
            return Task.FromResult(Result.Failure<DecisionDeReglasDeParametro>(ErroresDeRecaudo.CorteDespuesDelCicloDeCartera(corte, inicioDelCicloDeCartera)));
        return Task.FromResult(Result.Success(DecisionDeReglasDeParametro.Adelante));
    }

    /// <summary>¿El corte cae antes del ciclo de Cartera en la línea del día de operación?</summary>
    public static bool CorteValido(TimeOnly corte, TimeOnly inicioDelCiclo) =>
        inicioDelCiclo < LimiteDeLaMadrugada || corte < inicioDelCiclo;
}
