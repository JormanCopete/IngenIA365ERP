namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// El contrato «Proveedor de Obligaciones» (feature 013, T0149; FR-083, D15; contracts/interfaces.md §3): todo dinero que reduce
/// una obligación entra por aquí. Corre síncrono en la unidad de trabajo de quien recauda (T91): las escrituras exigen
/// transacción en curso (<c>TransaccionExplicita.HayEnCurso</c>), nunca abren ni confirman la suya y se llaman directo, nunca por
/// <c>ISender</c>. Un rechazo de negocio nunca es excepción: responde <see cref="ResultadoDelContrato.Rejected"/> con código y
/// mensaje. Antes de activar Cartera la implementación registrada es <see cref="ContratoDeObligacionesSinActivar"/> (§3.17).
/// Los métodos exactos y <see cref="VersionDelContrato"/> los fija <c>LaCarteraSoloSeTocaPorSusInterfaces</c>. (nuevo)
/// </summary>
public interface IContratoDeObligaciones
{
    /// <summary>
    /// La versión vigente que adopta cada recaudador en el registro de participantes («ProveedorDeObligaciones»). Feature 015, R1 (E-01 a): la 2 suma la
    /// partida bancaria citada (<see cref="PartidaBancariaCitada"/>); el despliegue implementa las dos
    /// (<c>ContratosDeIntegracion.VersionesDelContratoDeObligaciones = [1, 2]</c>) y la activación de Cartera sigue aceptando un recaudador con sólo la 1.
    /// </summary>
    const int VersionDelContrato = 2;

    /// <summary>FR-084: obligaciones de un tercero, como deudor y opcionalmente como codeudor. No escribe.</summary>
    Task<RespuestaDelContratoDto<ListadoDeObligacionesDto>> ListarAsync(SolicitudDeListado solicitud, CancellationToken ct);

    /// <summary>FR-073, FR-085: distribución, saldos, plan nuevo e identificador de simulación. No escribe.</summary>
    Task<RespuestaDelContratoDto<SimulacionDePagoDto>> SimularAsync(SolicitudDeSimulacion solicitud, CancellationToken ct);

    /// <summary>FR-085, FR-086, FR-112: un lote de pagos (y los no descontados de la nómina propia); una respuesta por ítem.</summary>
    Task<RespuestaDeAplicacionDto> AplicarAsync(SolicitudDeAplicacion solicitud, CancellationToken ct);

    /// <summary>FR-080, FR-081, FR-085: reversa un pago por su clave.</summary>
    Task<RespuestaDelContratoDto<ComprobanteDeReversoDto>> ReversarAsync(SolicitudDeReverso solicitud, CancellationToken ct);

    /// <summary>FR-076, FR-083: devuelve o aplica un saldo a favor o un anticipo.</summary>
    Task<RespuestaDelContratoDto<ComprobanteDeResolucionDto>> ResolverSaldoAFavorAsync(SolicitudDeResolucion solicitud, CancellationToken ct);
}
