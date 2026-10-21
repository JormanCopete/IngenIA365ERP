namespace IngenIA365ERP.Domain.Enums.Compliance;

/// <summary>
/// Resultado de una consulta de listas restrictivas (feature 013, T70, F-18; contracts/adaptadores-y-archivos.md §3.2; data-model §9.1, que
/// lo fija con <c>Unavailable</c>). El puerto <c>IListasRestrictivas</c> sólo responde <c>NoMatch</c>, <c>PossibleMatch</c> o <c>Match</c>
/// —un canal caído es <c>ChannelUnavailable</c> en su <c>ResultadoExterno</c>—; <c>Unavailable</c> lo usa quien guarda el resultado de la
/// consulta (<c>LND_ScreeningResults.Outcome</c>, int) cuando no hubo respuesta. Vive en Domain, no en Application como dice T0087, porque
/// una entidad lo guarda. Un valor nunca se renumera. (nuevo)
/// </summary>
public enum ScreeningOutcome
{
    NoMatch = 1,
    PossibleMatch = 2,
    Match = 3,
    Unavailable = 4,
}
