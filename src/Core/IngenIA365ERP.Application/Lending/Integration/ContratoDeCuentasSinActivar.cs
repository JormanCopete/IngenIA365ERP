using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La implementación de <see cref="IContratoDeCuentas"/> mientras captaciones y aportes no estén activados (feature 014, T040; contrato-de-cuentas
/// §3.13, FR-093, R21): responde «no habilitado» <b>sin efectos</b> y con el código de la naturaleza de la cuenta —<c>Lending.Savings.NotActivated</c>
/// para captación, <c>Lending.Contributions.NotActivated</c> para aportes—. Consultar y listar exigibles responden
/// <see cref="ResultadoDelContrato.Answered"/> con las dos naturalezas <see cref="EstadoDeCartera.NotActivated"/> y listas vacías; simular,
/// <c>Answered</c> con <c>Allowed = false</c>; consignar, retirar, trasladar y reversar, <see cref="ResultadoDelContrato.Rejected"/> en cada ítem. No
/// escribe nada ni exige transacción: quien recauda conserva lo rechazado y lo aplica al activar con su fecha valor. Para saber la naturaleza sólo lee
/// la cuenta (por <c>PublicId</c>, sin seguimiento); una cuenta que no existe responde como captación. Se registra por defecto en
/// <c>DependencyInjection</c>; la activación de cada naturaleza (T055) cambia a <c>ContratoDeCuentasDeCartera</c>. (nuevo)
/// </summary>
public sealed class ContratoDeCuentasSinActivar(IApplicationDbContext db) : IContratoDeCuentas
{
    /// <summary>Captaciones sin activar (data-model §1.2).</summary>
    public const string CodigoCaptacionesNoActivadas = "Lending.Savings.NotActivated";

    /// <summary>Aportes sin activar (data-model §1.2).</summary>
    public const string CodigoAportesNoActivados = "Lending.Contributions.NotActivated";

    public const string MensajeCaptacionesNoActivadas =
        "Las captaciones todavía no están activadas en esta cooperativa. Esta operación estará disponible cuando se registre su activación.";

    public const string MensajeAportesNoActivados =
        "Los aportes todavía no están activados en esta cooperativa. Esta operación estará disponible cuando se registre su activación.";

    private static readonly DisponibleDto SinDisponible = new(0m, 0m, 0m, 0m, 0m, 0m);

    public Task<RespuestaDelContratoDto<CuentasDePersonaDto>> ConsultarAsync(SolicitudDeConsultaDeCuentas solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var vacio = new CuentasDePersonaDto(EstadoDeCartera.NotActivated, EstadoDeCartera.NotActivated, solicitud.PersonPublicId, solicitud.Date, []);
        return Task.FromResult(new RespuestaDelContratoDto<CuentasDePersonaDto>(
            ResultadoDelContrato.Answered, CodigoCaptacionesNoActivadas, MensajeCaptacionesNoActivadas, vacio));
    }

    public Task<RespuestaDelContratoDto<ExigiblesDeCuentasDto>> ListarExigiblesAsync(SolicitudDeExigibles solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var vacio = new ExigiblesDeCuentasDto(EstadoDeCartera.NotActivated, EstadoDeCartera.NotActivated, [], []);
        return Task.FromResult(new RespuestaDelContratoDto<ExigiblesDeCuentasDto>(
            ResultadoDelContrato.Answered, CodigoCaptacionesNoActivadas, MensajeCaptacionesNoActivadas, vacio));
    }

    public async Task<RespuestaDelContratoDto<SimulacionDeCuentaDto>> SimularAsync(SolicitudDeSimulacionDeCuenta solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var (codigo, mensaje) = NoActivada((await NaturalezasAsync([solicitud.AccountPublicId], ct)).GetValueOrDefault(solicitud.AccountPublicId));
        var simulacion = new SimulacionDeCuentaDto(string.Empty, false, SinDisponible, SinDisponible, null, [], [], null, false, [], [mensaje]);
        return new RespuestaDelContratoDto<SimulacionDeCuentaDto>(ResultadoDelContrato.Answered, codigo, mensaje, simulacion);
    }

    public async Task<RespuestaDeConsignacionDto> ConsignarAsync(SolicitudDeConsignacion solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var consignaciones = solicitud.Deposits ?? [];
        var noDescontados = solicitud.NotDeducted ?? [];
        var naturalezas = await NaturalezasAsync(consignaciones.Select(c => c.AccountPublicId).Concat(noDescontados.Select(n => n.AccountPublicId)), ct);
        return new RespuestaDeConsignacionDto(
            consignaciones.Select(c => Rechazo<ComprobanteDeCuentaDto>(naturalezas.GetValueOrDefault(c.AccountPublicId))).ToList(),
            noDescontados.Select(n => Rechazo<NoDescontadoDeCuentaRegistradoDto>(naturalezas.GetValueOrDefault(n.AccountPublicId))).ToList());
    }

    public async Task<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> RetirarAsync(SolicitudDeRetiro solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        return Rechazo<ComprobanteDeCuentaDto>((await NaturalezasAsync([solicitud.AccountPublicId], ct)).GetValueOrDefault(solicitud.AccountPublicId));
    }

    public async Task<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> TrasladarAsync(SolicitudDeTraslado solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var naturalezas = await NaturalezasAsync([solicitud.SourceAccountPublicId, solicitud.TargetAccountPublicId], ct);
        // Si alguna de las dos es de aportes, responde la de aportes; si no, la de captaciones.
        var naturaleza = naturalezas.Values.Contains(LineNature.Contribution) ? LineNature.Contribution : LineNature.Deposit;
        return Rechazo<ComprobanteDeCuentaDto>(naturaleza);
    }

    public Task<RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>> ReversarAsync(SolicitudDeReversoDeCuenta solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        // Sin activar no hay operación que reversar: la clave no nombra cuenta y responde la de captaciones.
        return Task.FromResult(Rechazo<ComprobanteDeReversoDeCuentaDto>(LineNature.Deposit));
    }

    /// <summary>El código y el mensaje de «no habilitado» de la naturaleza; sin naturaleza conocida, el de captaciones.</summary>
    public static (string Codigo, string Mensaje) NoActivada(LineNature naturaleza) => naturaleza == LineNature.Contribution
        ? (CodigoAportesNoActivados, MensajeAportesNoActivados)
        : (CodigoCaptacionesNoActivadas, MensajeCaptacionesNoActivadas);

    private static RespuestaDelContratoDto<T> Rechazo<T>(LineNature naturaleza)
    {
        var (codigo, mensaje) = NoActivada(naturaleza);
        return new(ResultadoDelContrato.Rejected, codigo, mensaje, default,
            new Dictionary<string, object?> { ["nature"] = naturaleza == LineNature.Contribution ? nameof(LineNature.Contribution) : nameof(LineNature.Deposit) });
    }

    /// <summary>La naturaleza de cada cuenta nombrada que existe, en una sola lectura.</summary>
    private async Task<Dictionary<Guid, LineNature>> NaturalezasAsync(IEnumerable<Guid> cuentas, CancellationToken ct)
    {
        var ids = cuentas.Where(g => g != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return [];
        return await db.MemberAccounts.AsNoTracking().Where(a => ids.Contains(a.PublicId))
            .Select(a => new { a.PublicId, a.Nature }).ToDictionaryAsync(a => a.PublicId, a => a.Nature, ct);
    }
}
