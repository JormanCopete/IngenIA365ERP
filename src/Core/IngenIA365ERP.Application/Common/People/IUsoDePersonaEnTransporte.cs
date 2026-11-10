namespace IngenIA365ERP.Application.Common.People;

/// <summary>
/// ¿Es la persona propietaria o conductora vigente de un vehículo vivo? (feature 020, R5; FR-017). Lo consulta <c>DeletePersonCommand</c>
/// antes de borrar, sin conocer Transporte: devuelve la placa que lo impide, o nulo. Sin Transporte instalado no hay implementación.
/// </summary>
public interface IUsoDePersonaEnTransporte
{
    /// <summary>La placa del vehículo vivo del que la persona es propietaria o conductora vigente; nulo si ninguno.</summary>
    Task<string?> PlacaQueLoImpideAsync(int personId, DateOnly fecha, CancellationToken ct);
}
