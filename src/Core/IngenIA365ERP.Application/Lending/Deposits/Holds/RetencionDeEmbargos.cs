using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Holds;

/// <summary>
/// La configuración de embargos de la cooperativa a una fecha (feature 014, US11, T188; research §20, E156): el orden de las clases, las protegidas por el
/// límite, las inembargables, cómo se aplica el límite por autoridad, si sólo protege a personas naturales y los días para responder. Cada valor es un
/// parámetro de la cooperativa con vigencia; si no responde (la entrega K8 todavía no está desplegada) rige el defecto del catálogo. (nuevo)
/// </summary>
public sealed record ConfiguracionDeEmbargos(
    IReadOnlyList<DepositClass> OrdenDeClases,
    IReadOnlySet<DepositClass> Protegidas,
    IReadOnlySet<DepositClass> Inembargables,
    IReadOnlyDictionary<GarnishmentAuthority, AplicacionDelLimiteInembargable> Aplicacion,
    bool SoloPersonasNaturales,
    int DiasHabilesParaResponder,
    IReadOnlyDictionary<GarnishmentResponseKind, string> Plantillas);

/// <summary>Lo que dejó el recálculo de un embargo: el embargo y el reparto del motor. (nuevo)</summary>
public sealed record EmbargoRecalculado(Garnishment Embargo, EmbargoCalculado Calculo);

/// <summary>
/// El único escritor de lo que retienen los embargos (feature 014, US11, T189; data-model §5.1, §5.2; FR-055, FR-057, FR-065, D20, R23; research §20):
/// recalcula, bajo el cerrojo de la persona (<c>LND_DebtorControls</c>) y de sus cuentas, todos los embargos activos de la persona en el orden en que se
/// recibieron —el límite es de la persona: lo que retiene uno no lo vuelve a retener el siguiente— y deja los bloqueos <c>Garnishment</c> de cada cuenta en
/// el valor del motor (<see cref="LimiteInembargable"/>): si cambia, levanta el anterior y pone otro, así queda la historia. Después la marca de la cuenta
/// (<c>IsGarnished</c>, que reclasifica los saldos con <see cref="ReclasificacionDeCuenta"/>) y la suspensión de la causación por embargo total si la línea
/// lo dice (FR-065). Lo llaman el registro, el levantamiento, la consignación y el paso diario (los depósitos posteriores). (nuevo)
/// </summary>
public sealed class RetencionDeEmbargos(
    IApplicationDbContext db,
    ReglasDeParametrosDeCartera parametros,
    IValoresNacionales valoresNacionales,
    ReclasificacionDeCuenta reclasificacion,
    IDateTimeService reloj)
{
    public const string CausaSuspension = "TotalGarnishment";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // --------------------------------------------------------------------------------------------------- configuración --

    public async Task<ConfiguracionDeEmbargos> ConfiguracionAsync(DateOnly fecha, CancellationToken ct)
    {
        var orden = Clases(await TextoAsync(ParametrosDeCartera.EmbargosOrdenDeCuentas, fecha, ct));
        var protegidas = Clases(await TextoAsync(ParametrosDeCartera.EmbargosClasesProtegidas, fecha, ct)).ToHashSet();
        var inembargables = Clases(await TextoAsync(ParametrosDeCartera.EmbargosClasesInembargables, fecha, ct)).ToHashSet();
        var aplicacion = new Dictionary<GarnishmentAuthority, AplicacionDelLimiteInembargable>
        {
            [GarnishmentAuthority.Court] = AplicacionDelLimiteInembargable.AllProtectedAccounts,
            [GarnishmentAuthority.TaxAuthority] = AplicacionDelLimiteInembargable.OldestAccount,
            [GarnishmentAuthority.OtherPublicEntity] = AplicacionDelLimiteInembargable.OldestAccount,
        };
        foreach (var (k, v) in Mapa(await TextoAsync(ParametrosDeCartera.EmbargosAplicacionDelLimite, fecha, ct)))
            if (Enum.TryParse<GarnishmentAuthority>(k, true, out var a) && Enum.TryParse<AplicacionDelLimiteInembargable>(v, true, out var ap)) aplicacion[a] = ap;
        var solo = !string.Equals(await TextoAsync(ParametrosDeCartera.EmbargosLimiteSoloPersonasNaturales, fecha, ct), "false", StringComparison.OrdinalIgnoreCase);
        var dias = int.TryParse(await TextoAsync(ParametrosDeCartera.EmbargosDiasHabilesParaResponder, fecha, ct), out var n) ? Math.Max(0, n) : 0;
        var plantillas = new Dictionary<GarnishmentResponseKind, string>();
        foreach (var (k, v) in Mapa(await TextoAsync(ParametrosDeCartera.EmbargosPlantillasDeRespuesta, fecha, ct)))
            if (Enum.TryParse<GarnishmentResponseKind>(k, true, out var clase) && !string.IsNullOrWhiteSpace(v)) plantillas[clase] = v;
        return new ConfiguracionDeEmbargos(orden, protegidas, inembargables, aplicacion, solo, dias, plantillas);
    }

    private async Task<string> TextoAsync(string clave, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(clave, fecha, ct: ct);
        return leido.IsSuccess ? leido.Value.Texto : ParametrosDeCartera.Definiciones.First(d => d.Clave == clave).DefectoSeguro;
    }

    private static List<DepositClass> Clases(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return (JsonSerializer.Deserialize<List<string>>(json, Json) ?? [])
                .Select(t => Enum.TryParse<DepositClass>(t, true, out var c) ? c : (DepositClass?)null).OfType<DepositClass>().Distinct().ToList();
        }
        catch (JsonException) { return []; }
    }

    private static Dictionary<string, string> Mapa(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json, Json) ?? []; }
        catch (JsonException) { return []; }
    }

    // ---------------------------------------------------------------------------------------------------------- límite --

    /// <summary>
    /// El límite inembargable que rige para el embargo (nulo = ninguno): por alimentos ninguno; persona jurídica ninguno si la cooperativa así lo fija; el
    /// judicial, <c>LIMITE_INEMBARGABLE</c>; el coactivo, el <c>LIMITE</c> <c>EMBARGO_COACTIVO_SMMLV</c> por el SMMLV. Sin el valor nacional,
    /// <c>NationalValues.Missing</c>: sin el límite no se sabe qué retener.
    /// </summary>
    public async Task<Result<decimal?>> LimiteAsync(Garnishment g, Person persona, ConfiguracionDeEmbargos cfg, DateOnly fecha, CancellationToken ct)
    {
        if (g.IsChildSupport) return Result.Success<decimal?>(null);
        if (cfg.SoloPersonasNaturales && LectorDeCuentas.EsJuridica(persona)) return Result.Success<decimal?>(null);
        if (g.Authority == GarnishmentAuthority.Court)
        {
            var v = await valoresNacionales.LeerAsync(CodigosDeValorNacional.LimiteInembargable, string.Empty, fecha, null, ct);
            if (v.IsFailure) return Result.Failure<decimal?>(v.Error);
            return Result.Success(v.Value.Valor);
        }
        var veces = await valoresNacionales.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.EmbargoCoactivoSmmlv, fecha, null, ct);
        if (veces.IsFailure) return Result.Failure<decimal?>(veces.Error);
        var smmlv = await valoresNacionales.LeerAsync(CodigosDeValorNacional.Smmlv, string.Empty, fecha, null, ct);
        if (smmlv.IsFailure) return Result.Failure<decimal?>(smmlv.Error);
        return Result.Success<decimal?>(veces.Value.Valor is { } x && smmlv.Value.Valor is { } s ? x * s : null);
    }

    // --------------------------------------------------------------------------------------------------------- cuentas --

    /// <summary>Las cuentas de captación vivas de la persona como titular (las de aportes nunca se embargan).</summary>
    public async Task<List<MemberAccount>> CuentasDeLaPersonaAsync(int persona, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.MemberAccounts : db.MemberAccounts.AsNoTracking();
        return await consulta.Where(a => a.HolderPersonId == persona && a.Nature == LineNature.Deposit && a.Status != MemberAccountStatus.Closed && !a.IsDeleted)
            .OrderBy(a => a.Id).ToListAsync(ct);
    }

    /// <summary>Lo consignado de cada embargo: Σ de sus consignaciones no reversadas.</summary>
    public async Task<Dictionary<int, decimal>> ConsignadoAsync(IReadOnlyCollection<int> embargos, CancellationToken ct)
    {
        if (embargos.Count == 0) return [];
        var ids = embargos.ToArray();
        return await db.MemberAccountOperations.AsNoTracking()
            .Where(o => o.GarnishmentId != null && ids.Contains(o.GarnishmentId.Value) && o.Kind == MemberAccountOperationKind.GarnishmentConsignment
                        && o.ReversedByOperationId == null && !o.IsDeleted)
            .GroupBy(o => o.GarnishmentId!.Value).Select(g => new { g.Key, Total = g.Sum(o => o.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
    }

    /// <summary>Las cuentas que el oficio nombra (<c>Specific</c>); nulo = todas las de ahorro.</summary>
    public static IReadOnlySet<int>? CuentasDelOficio(Garnishment g) =>
        g.AccountsScope == GarnishmentAccountsScope.Specific && !string.IsNullOrWhiteSpace(g.SpecificAccountIdsJson)
            ? (JsonSerializer.Deserialize<List<int>>(g.SpecificAccountIdsJson) ?? []).ToHashSet()
            : null;

    /// <summary>El motor sobre las cuentas de la persona para un embargo, con lo que ya retienen los anteriores.</summary>
    public async Task<Result<EmbargoCalculado>> CalcularAsync(Garnishment g, Person persona, IReadOnlyList<MemberAccount> cuentas,
        IReadOnlyDictionary<int, decimal> saldos, IReadOnlyDictionary<int, decimal> retenidoPorOtros, decimal consignado, ConfiguracionDeEmbargos cfg,
        DateOnly fecha, CancellationToken ct)
    {
        var limite = await LimiteAsync(g, persona, cfg, fecha, ct);
        if (limite.IsFailure) return Result.Failure<EmbargoCalculado>(limite.Error);
        var delOficio = CuentasDelOficio(g);
        var ante = cuentas.Select(c => new CuentaAnteElEmbargo(c.Id, c.DepositClass, c.OpenedOn, saldos.GetValueOrDefault(c.Id),
            retenidoPorOtros.GetValueOrDefault(c.Id), c.DepositClass is { } k && cfg.Protegidas.Contains(k),
            c.DepositClass is { } clase && !cfg.Inembargables.Contains(clase) && (delOficio is null || delOficio.Contains(c.Id)))).ToList();
        var tope = g.Amount is { } valor ? Math.Max(0m, valor - consignado) : (decimal?)null;
        var aplicacion = cfg.Aplicacion.GetValueOrDefault(g.Authority, AplicacionDelLimiteInembargable.AllProtectedAccounts);
        return Result.Success(LimiteInembargable.Calcular(new PedidoDeEmbargo(tope, g.IsChildSupport, limite.Value, aplicacion, cfg.OrdenDeClases, ante)));
    }

    // -------------------------------------------------------------------------------------------------------- recálculo --

    /// <summary>
    /// Recalcula todos los embargos activos de la persona y deja los bloqueos, las marcas y la suspensión. La persona y sus cuentas ya están bloqueadas; corre
    /// en la transacción de quien llama; guarda.
    /// </summary>
    public async Task<Result<IReadOnlyList<EmbargoRecalculado>>> RecalcularAsync(int personaId, DateOnly fecha, int? usuario, string motivo, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == personaId, ct);
        var cfg = await ConfiguracionAsync(fecha, ct);
        var cuentas = await CuentasDeLaPersonaAsync(personaId, seguir: true, ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var porConcepto = await LectorDeCuentas.SaldosAsync(db, ids, ct);
        var saldos = cuentas.ToDictionary(c => c.Id, c => LectorDeCuentas.SaldoDelTitular(c.Nature, porConcepto.GetValueOrDefault(c.Id) ?? []));
        var embargos = await db.Garnishments.Where(g => g.PersonId == personaId && !g.IsDeleted && g.Status == GarnishmentStatus.Active)
            // Respuesta 83 C del dueño (E250): los embargos por alimentos toman primero (prelación legal) y después, por fecha de recepción del oficio.
            .OrderByDescending(g => g.IsChildSupport).ThenBy(g => g.ReceivedOn).ThenBy(g => g.Id).ToListAsync(ct);
        var consignado = await ConsignadoAsync(embargos.Select(e => e.Id).ToList(), ct);

        var acumulado = new Dictionary<int, decimal>();
        var deseado = new Dictionary<(int Embargo, int Cuenta), decimal>();
        var resultado = new List<EmbargoRecalculado>();
        foreach (var g in embargos)
        {
            var calculo = await CalcularAsync(g, persona, cuentas, saldos, acumulado, consignado.GetValueOrDefault(g.Id), cfg, fecha, ct);
            if (calculo.IsFailure) return Result.Failure<IReadOnlyList<EmbargoRecalculado>>(calculo.Error);
            foreach (var c in calculo.Value.Cuentas.Where(c => c.Retenido > 0m))
            {
                deseado[(g.Id, c.CuentaId)] = c.Retenido;
                acumulado[c.CuentaId] = acumulado.GetValueOrDefault(c.CuentaId) + c.Retenido;
            }
            resultado.Add(new EmbargoRecalculado(g, calculo.Value));
        }

        // Los bloqueos: los de embargos levantados o que ya no retienen se levantan; los que cambian, se levantan y se ponen de nuevo.
        var ahora = reloj.UtcNow;
        var vigentes = await db.AccountHolds.Where(h => ids.Contains(h.MemberAccountId) && h.Reason == HoldReason.Garnishment && h.ReleasedAt == null && !h.IsDeleted)
            .ToListAsync(ct);
        foreach (var h in vigentes)
        {
            var quiere = deseado.GetValueOrDefault((h.GarnishmentId ?? 0, h.MemberAccountId));
            if (quiere == h.Amount) { deseado.Remove((h.GarnishmentId!.Value, h.MemberAccountId)); continue; }
            h.Levantar(ahora, usuario, quiere == 0m ? $"{motivo}: ya no retiene" : $"{motivo}: retiene {quiere:N2}");
        }
        foreach (var ((embargo, cuenta), valor) in deseado)
            db.AccountHolds.Add(AccountHold.Poner(cuenta, HoldReason.Garnishment, valor, fecha, null, motivo, null, usuario, garnishmentId: embargo));
        await db.SaveChangesAsync(ct);

        // La marca de cada cuenta (reclasifica) y la suspensión de la causación por embargo total (FR-065).
        foreach (var cuenta in cuentas)
        {
            var embargado = acumulado.GetValueOrDefault(cuenta.Id);
            var marca = embargado > 0m;
            if (marca != cuenta.IsGarnished)
            {
                var embargoId = resultado.FirstOrDefault(r => r.Calculo.Cuentas.Any(c => c.CuentaId == cuenta.Id && c.Retenido > 0m))?.Embargo.Id
                                ?? vigentes.FirstOrDefault(h => h.MemberAccountId == cuenta.Id)?.GarnishmentId;
                var r = await reclasificacion.ReclasificarAsync(cuenta, fecha,
                    marca ? Integration.EmisionDeCartera.CausaEmbargo : Integration.EmisionDeCartera.CausaDesembargo, () => cuenta.IsGarnished = marca, embargoId, ct);
                if (r.IsFailure) return Result.Failure<IReadOnlyList<EmbargoRecalculado>>(r.Error);
            }
            await SuspensionAsync(cuenta, embargado, saldos.GetValueOrDefault(cuenta.Id), fecha, ct);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success<IReadOnlyList<EmbargoRecalculado>>(resultado);
    }

    /// <summary>
    /// FR-065: la cuenta embargada <b>totalmente</b> (lo embargado cubre todo su saldo) deja de causar desde <paramref name="fecha"/> si su línea lo dice; si
    /// deja de estarlo, reanuda desde esa fecha (<c>AccrualResumedOn</c>). El CDAT sigue su plan (no la suspende).
    /// </summary>
    private async Task SuspensionAsync(MemberAccount cuenta, decimal embargado, decimal saldo, DateOnly fecha, CancellationToken ct)
    {
        if (cuenta.DepositClass == DepositClass.TermDeposit) return;
        var total = saldo > 0m && embargado >= saldo;
        var suspendida = cuenta.AccrualSuspendedSince is not null && cuenta.AccrualResumedOn is null;
        if (total && !suspendida)
        {
            var version = await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, cuenta.CreditLineId, fecha, null, ct);
            if (version is not { SuspendsAccrualOnTotalGarnishment: true }) return;
            cuenta.AccrualSuspendedSince = fecha;
            cuenta.AccrualResumedOn = null;
            cuenta.AccrualSuspensionReason = CausaSuspension;
        }
        else if (!total && suspendida && cuenta.AccrualSuspensionReason == CausaSuspension)
        {
            cuenta.AccrualResumedOn = fecha < cuenta.AccrualSuspendedSince!.Value ? cuenta.AccrualSuspendedSince : fecha;
        }
    }

    /// <summary>
    /// El giro de una consignación rechazado por Tesorería (FR-091): el saldo vuelve a la cuenta y queda retenido para el mismo embargo hasta el recálculo
    /// siguiente (el paso diario o cualquier operación del embargo). Sin guardar.
    /// </summary>
    public static void Reintegrar(IApplicationDbContext db, int embargoId, int cuentaId, decimal monto, DateOnly fecha, string motivo)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (monto <= 0m) return;
        db.AccountHolds.Add(AccountHold.Poner(cuentaId, HoldReason.Garnishment, monto, fecha, null, motivo, null, null, garnishmentId: embargoId));
    }
}
