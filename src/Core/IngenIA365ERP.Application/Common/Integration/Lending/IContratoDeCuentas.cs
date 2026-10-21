namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// El contrato «Proveedor de Cuentas» (feature 014, T039; research R09; FR-086–FR-093; contracts/contrato-de-cuentas.md §3): todo dinero que entra o
/// sale de una cuenta de captación o de aportes desde otro módulo entra por aquí. Un puerto propio, no más métodos en
/// <see cref="IContratoDeObligaciones"/>: depositar no es pagar una deuda (no hay prelación, ni anticipos, ni saldo a favor). Las mismas reglas que el
/// de obligaciones: corre síncrono en la unidad de trabajo de quien recauda —las escrituras exigen transacción en curso
/// (<c>TransaccionExplicita.HayEnCurso</c>), nunca abren ni confirman la suya y se llaman directo, nunca por <c>ISender</c>—; un rechazo de negocio
/// nunca es excepción: responde <see cref="ResultadoDelContrato.Rejected"/> con código y mensaje en español; cada escritura ejecuta su validador y
/// audita su intento y su resultado (desviación T101 de la 013). Nómina, Recaudo y Tesorería tocan las cuentas sólo por aquí
/// (<c>LasCuentasSeTocanSoloPorElContrato</c>). Antes de activar, la implementación registrada es <see cref="ContratoDeCuentasSinActivar"/> (§3.13).
/// Los métodos exactos y <see cref="VersionDelContrato"/> los fija <c>LaCarteraSoloSeTocaPorSusInterfaces</c>. (nuevo)
/// </summary>
public interface IContratoDeCuentas
{
    /// <summary>Versión que adopta cada participante en el registro («ProveedorDeCuentas»).</summary>
    const int VersionDelContrato = 1;

    /// <summary>FR-086: cuentas de una persona con estado, saldo y disponible desglosado. No escribe.</summary>
    Task<RespuestaDelContratoDto<CuentasDePersonaDto>> ConsultarAsync(SolicitudDeConsultaDeCuentas solicitud, CancellationToken ct);

    /// <summary>FR-087: cuotas exigibles de aportes, ahorro permanente y contractual de unas personas a una fecha. No escribe.</summary>
    Task<RespuestaDelContratoDto<ExigiblesDeCuentasDto>> ListarExigiblesAsync(SolicitudDeExigibles solicitud, CancellationToken ct);

    /// <summary>US4: valida firmas, disponible, bloqueos, embargos, montos y atribución, y calcula el GMF. No escribe.</summary>
    Task<RespuestaDelContratoDto<SimulacionDeCuentaDto>> SimularAsync(SolicitudDeSimulacionDeCuenta solicitud, CancellationToken ct);

    /// <summary>FR-050, FR-087, FR-089: un lote de consignaciones (y los no descontados de Nómina); una respuesta por ítem.</summary>
    Task<RespuestaDeConsignacionDto> ConsignarAsync(SolicitudDeConsignacion solicitud, CancellationToken ct);

    /// <summary>FR-051, FR-088: retiro autorizado por la Cartera y entregado por el recaudador.</summary>
    Task<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> RetirarAsync(SolicitudDeRetiro solicitud, CancellationToken ct);

    /// <summary>FR-052: traslado entre cuentas, una sola operación.</summary>
    Task<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> TrasladarAsync(SolicitudDeTraslado solicitud, CancellationToken ct);

    /// <summary>FR-092: reversa una operación propia por su clave.</summary>
    Task<RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>> ReversarAsync(SolicitudDeReversoDeCuenta solicitud, CancellationToken ct);
}
