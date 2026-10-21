using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_CreditScores</c> (feature 013; data-model §2.5; FR-037): el puntaje de una persona en una solicitud con el modelo de la
/// versión de la línea, explicado variable por variable. Hecho de sólo inserción (<see cref="IHechoInmutable"/>).
/// </summary>
public class CreditScore : AuditableEntity, IHechoInmutable
{
    public int ApplicationId { get; init; }

    public int PersonId { get; init; }

    /// <summary>La versión de la línea cuyo modelo de puntaje se usó (§1.3).</summary>
    public int CreditLineVersionId { get; init; }

    public decimal Score { get; init; }

    public decimal? MinimumScore { get; init; }

    /// <summary>Por debajo del mínimo: sube la solicitud al nivel de excepción (FR-037).</summary>
    public bool BelowMinimum { get; init; }

    /// <summary>JSON por variable: valor, rango, puntos, peso y aporte.</summary>
    public string Explanation { get; init; } = "[]";

    public string InputsSha256 { get; init; } = string.Empty;

    public DateTime CalculatedAt { get; init; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0388) --

    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>
    /// El puntaje calculado por <see cref="Domain.Lending.Scoring.MotorDePuntaje"/>, sellado con la versión de la línea, el mínimo, la
    /// huella de entradas y la explicación por variable (valor, rango, puntos, peso y aporte) como JSON (data-model §2.5).
    /// </summary>
    public static CreditScore Registrar(int applicationId, int personId, int creditLineVersionId, Domain.Lending.Scoring.ResultadoDePuntaje r,
        DateTime calculadoEl)
    {
        ArgumentNullException.ThrowIfNull(r);
        var explicacion = System.Text.Json.JsonSerializer.Serialize(r.Variables.Select(v => new
        {
            code = v.Codigo,
            name = v.Nombre,
            source = v.Fuente,
            value = v.Valor,
            range = v.Rango is null ? null : new { from = v.Rango.From, to = v.Rango.To, noData = v.Rango.NoData },
            noData = v.SinDato,
            outOfRange = v.FueraDeRango,
            points = v.Puntos,
            weight = v.Peso,
            contribution = v.Aporte,
            explanation = v.Explicacion,
        }), Json);
        return new CreditScore
        {
            ApplicationId = applicationId,
            PersonId = personId,
            CreditLineVersionId = creditLineVersionId,
            Score = r.Puntaje,
            MinimumScore = r.Minimo,
            BelowMinimum = r.BajoElMinimo,
            Explanation = explicacion,
            InputsSha256 = r.HuellaDeEntradas,
            CalculatedAt = calculadoEl,
        };
    }
}
