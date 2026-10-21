using IngenIA365ERP.Application.Common.Integration.Lending;

namespace IngenIA365ERP.Application.Tests.Common.Integration.Lending;

/// <summary>
/// Doble de <see cref="IConsultaDeObligaciones"/> (feature 013, T0154, R118): responde lo que la prueba configure por persona y
/// registra cada consulta. Sin configurar, se comporta como la implementación provisional: Cartera sin activar y sin obligaciones.
/// <see cref="Caida"/> hace lanzar las dos consultas (Cartera no responde). Sólo vive en <c>tests/</c>.
/// </summary>
public sealed class ConsultaDeObligacionesDePrueba : IConsultaDeObligaciones
{
    private readonly Dictionary<Guid, List<ObligacionDto>> _obligaciones = new();
    private readonly List<CuotaDeNominaDto> _cuotas = [];
    private readonly HashSet<Guid> _migradas = [];

    public EstadoDeCartera Estado { get; set; } = EstadoDeCartera.NotActivated;
    public Exception? Caida { get; set; }
    public List<ConsultaDeObligaciones> Consultas { get; } = [];
    public List<ConsultaDeCuotasDeNomina> ConsultasDeCuotas { get; } = [];

    /// <summary>Agrega una obligación de la persona (activa a Cartera).</summary>
    public ObligacionDto Agregar(Guid persona, ObligacionDto obligacion)
    {
        Estado = EstadoDeCartera.Active;
        if (!_obligaciones.TryGetValue(persona, out var lista)) _obligaciones[persona] = lista = [];
        lista.Add(obligacion);
        return obligacion;
    }

    /// <summary>Quita todas las obligaciones de la persona (p. ej. «la canceló por caja»).</summary>
    public void Quitar(Guid persona) => _obligaciones.Remove(persona);

    public void AgregarCuota(CuotaDeNominaDto cuota)
    {
        Estado = EstadoDeCartera.Active;
        _cuotas.Add(cuota);
    }

    public void LimpiarCuotas() => _cuotas.Clear();

    public void MarcarMigrada(Guid persona) => _migradas.Add(persona);

    /// <summary>Una obligación con valores por defecto razonables.</summary>
    public static ObligacionDto Obligacion(
        string numero, decimal saldoTotal, decimal exigible = 0m, RolEnLaObligacion rol = RolEnLaObligacion.Debtor,
        EstadoDeObligacion estado = EstadoDeObligacion.Active, Guid? id = null, decimal cuota = 0m) =>
        new(id ?? Guid.NewGuid(), numero, "CONSUMO", "Consumo", ClaseDeObligacion.Standard, null, rol, estado,
            IsLive: estado is EstadoDeObligacion.Active or EstadoDeObligacion.WrittenOff,
            ClosedOn: null, TotalBalance: saldoTotal, DueAmount: exigible, AccruedInstallments: exigible > 0m ? 1 : 0, DaysPastDue: 0,
            Principal: saldoTotal * 0.9m, CurrentInterest: saldoTotal * 0.1m, DefaultInterest: 0m, PendingInstallments: 12, InstallmentAmount: cuota);

    public Task<ObligacionesDePersonasDto> ObligacionesAsync(ConsultaDeObligaciones consulta, CancellationToken ct)
    {
        Consultas.Add(consulta);
        if (Caida is not null) throw Caida;
        var personas = consulta.PersonPublicIds.Distinct().Select(p => new ObligacionesDePersonaDto(
            p, _migradas.Contains(p),
            (_obligaciones.GetValueOrDefault(p) ?? [])
                .Where(o => consulta.IncludeAsCoDebtor || o.Role == RolEnLaObligacion.Debtor)
                .Where(o => consulta.IncludeClosed || o.IsLive)
                .ToList())).ToList();
        return Task.FromResult(new ObligacionesDePersonasDto(Estado, personas));
    }

    public Task<CuotasDeNominaDto> CuotasDeNominaAsync(ConsultaDeCuotasDeNomina consulta, CancellationToken ct)
    {
        ConsultasDeCuotas.Add(consulta);
        if (Caida is not null) throw Caida;
        var cuotas = _cuotas.Where(c => consulta.PersonPublicIds.Contains(c.PersonPublicId)
                                        && c.DueDate >= consulta.PeriodStart && c.DueDate <= consulta.PeriodEnd).ToList();
        var migradas = consulta.PersonPublicIds.Where(_migradas.Contains).ToList();
        return Task.FromResult(new CuotasDeNominaDto(Estado, cuotas, migradas));
    }
}
