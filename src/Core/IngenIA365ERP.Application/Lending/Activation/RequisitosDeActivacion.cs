using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>
/// Los requisitos de la activación de Cartera (feature 013, T0233, C107; FR-142, T84; data-model §30.1; api.md §19.3), en su <b>forma
/// mínima</b>, la que basta para activar una cooperativa sin cartera que migrar:
/// <list type="bullet">
/// <item><c>LoadApplied</c>: la carga está <c>Applied</c>;</item>
/// <item><c>DateInTerm</c>: hoy no pasa del plazo de la carga (<c>ActivationDeadline</c>);</item>
/// <item><c>AccountingMonthOpen</c>: el mes de la fecha de activación está abierto en Contabilidad (<see cref="IContabilidadParaCartera"/>);</item>
/// <item><c>ReceiptsCollectorRegistered</c>: un recaudador que cuenta en el registro de participantes, de clase <c>Receipts</c> (y fuera de
/// producción también un doble <c>Test</c>, el <c>TST</c>);</item>
/// <item><c>TreasuryDestinationRegistered</c>: un destino <c>Treasury</c> que cuenta (fuera de producción, el doble <c>TES</c> con
/// <c>TesoreriaDePrueba</c>; en producción el registro nunca cuenta un doble).</item>
/// </list>
/// El carácter de producción es el del despliegue (<see cref="IAmbienteDeDespliegue"/>, que la API toma de <c>IHostEnvironment.IsProduction()</c>).
/// US8 suma el resto de api.md §19.3 (diferencias aceptadas, ventas conciliadas, K6a, Anexo 1 y usura, roles, oficinas vinculadas, regla
/// del puente de Nómina) y los avisos que no bloquean. (nuevo)
/// </summary>
public sealed class RequisitosDeActivacion(
    IRegistroDeIntegracion registro,
    IContabilidadParaCartera contabilidad,
    IAmbienteDeDespliegue ambiente)
{
    public const string CargaAplicada = "LoadApplied";
    public const string FechaEnPlazo = "DateInTerm";
    public const string MesContableAbierto = "AccountingMonthOpen";
    public const string RecaudadorRegistrado = "ReceiptsCollectorRegistered";
    public const string TesoreriaRegistrada = "TreasuryDestinationRegistered";

    public bool EsProduccion => ambiente.EsProduccion;

    /// <summary>
    /// Cada requisito con su estado para activar la carga <paramref name="carga"/> en <paramref name="fechaDeActivacion"/>, mirado
    /// <paramref name="hoy"/>. Sólo lee.
    /// </summary>
    public async Task<IReadOnlyList<RequisitoDeActivacionDto>> EvaluarAsync(
        MigrationLoad? carga, DateOnly fechaDeActivacion, DateOnly hoy, CancellationToken ct)
    {
        var requisitos = new List<RequisitoDeActivacionDto>(5)
        {
            carga is { Status: MigrationLoadStatus.Applied }
                ? Cumple(CargaAplicada, "Hay una carga aplicada.")
                : Falta(CargaAplicada, "aplicar una carga de migración (aunque no traiga créditos)", "Cartera", "/cartera/migracion", "Lending.Migration.Load"),

            carga?.ActivationDeadline is { } plazo && hoy <= plazo
                ? Cumple(FechaEnPlazo, string.Create(CultureInfo.InvariantCulture, $"Se registra a más tardar el {plazo:yyyy-MM-dd}."))
                : Falta(FechaEnPlazo, carga?.ActivationDeadline is { } vencido
                    ? string.Create(CultureInfo.InvariantCulture, $"activar dentro del plazo de la carga (venció el {vencido:yyyy-MM-dd}): aplique una carga con un corte nuevo")
                    : "una carga aplicada con plazo de activación", "Cartera", "/cartera/migracion", "Lending.Migration.Load"),
        };

        var periodo = await contabilidad.EstadoDelPeriodoAsync(fechaDeActivacion, ct);
        requisitos.Add(periodo.IsSuccess && periodo.Value is { AccountingInitialized: true, State: EstadoDePeriodoContable.Open }
            ? Cumple(MesContableAbierto, string.Create(CultureInfo.InvariantCulture, $"El mes de {fechaDeActivacion:yyyy-MM} está abierto en Contabilidad."))
            : Falta(MesContableAbierto, periodo.IsSuccess && !periodo.Value.AccountingInitialized
                ? "iniciar la contabilidad"
                : string.Create(CultureInfo.InvariantCulture, $"el mes de {fechaDeActivacion:yyyy-MM} abierto en Contabilidad"),
                "Contabilidad", "/contabilidad/periodos", "Accounting.Periods.Reopen"));

        var participantes = await registro.QueCuentanAsync(fechaDeActivacion, ct);
        var recaudador = participantes.Any(p => p.Role == IntegrationParticipantRole.ObligationsCollector
            && (p.Class == IntegrationParticipantClass.Receipts || (!ambiente.EsProduccion && p.Class == IntegrationParticipantClass.Test)));
        requisitos.Add(recaudador
            ? Cumple(RecaudadorRegistrado, "Hay un recaudador de Recaudo en el registro.")
            : Falta(RecaudadorRegistrado, "registrar el recaudador de Recaudo en el registro de integración", "Core",
                "/admin/participantes-de-integracion", "Core.IntegrationParticipants.Manage"));

        var tesoreria = await registro.DestinoAsync(IntegrationDestinations.Treasury, fechaDeActivacion, ct);
        requisitos.Add(tesoreria is not null && (!ambiente.EsProduccion || !tesoreria.IsTestDouble)
            ? Cumple(TesoreriaRegistrada, "Hay un destino de Tesorería en el registro.")
            : Falta(TesoreriaRegistrada, "registrar el destino de Tesorería en el registro de integración", "Core",
                "/admin/participantes-de-integracion", "Core.IntegrationParticipants.Manage"));

        return requisitos;
    }

    private static RequisitoDeActivacionDto Cumple(string codigo, string detalle) => new(codigo, true, detalle, null);

    private static RequisitoDeActivacionDto Falta(string codigo, string detalle, string modulo, string? pagina, string? permiso) =>
        new(codigo, false, detalle, new QuienCorrigeDto(modulo, pagina, permiso));
}

/// <summary>Un requisito de la activación con su estado y, si falta, quién lo corrige (api.md §19.3). (nuevo)</summary>
public sealed record RequisitoDeActivacionDto(string Code, bool Met, string Detail, QuienCorrigeDto? WhoFixes);
