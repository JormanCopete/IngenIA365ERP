namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// La consulta de la carga de Cartera aplicada sin activar (feature 015, R1, E-01 b; contratos-v2.md §2.1; FR-046, FR-164 b; D14): Cartera la ofrece y Recaudo
/// la usa para decidir si un valor que el contrato rechazó por «Cartera no activada» puede quedar como <b>recaudo pendiente de aplicar</b> (la persona está en
/// una carga aplicada que todavía no se activa) o se rechaza. No escribe. Después de activar Cartera responde siempre <c>InAppliedLoad = false</c>: nadie lee la
/// preparación una vez activada. (nuevo)
/// </summary>
public interface IConsultaDeCargaSinActivar
{
    /// <summary>Si la persona está en la carga aplicada sin activar y las obligaciones que la carga le trae (como deudora o codeudora).</summary>
    Task<RespuestaDelContratoDto<CargaSinActivarDto>> PersonaAsync(Guid personPublicId, CancellationToken ct);
}

/// <summary>La respuesta de <see cref="IConsultaDeCargaSinActivar.PersonaAsync"/> (contratos-v2.md §2.1). (nuevo)</summary>
public sealed record CargaSinActivarDto(bool InAppliedLoad, IReadOnlyList<ObligacionCargadaDto> Obligations);

/// <summary>
/// Una obligación de la carga aplicada: su identificador en la carga (<c>LND_MigrationLoanRows.PublicId</c>; el crédito todavía no existe), su número de
/// origen, el nombre de la línea y la fecha de corte de la carga. (nuevo)
/// </summary>
public sealed record ObligacionCargadaDto(Guid ObligationPublicId, string Number, string LineName, DateOnly CutoffDate);
