using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Operations;

/// <summary>
/// Una cuenta lista para un movimiento (feature 014, US4, T111): la cuenta, la versión de su línea que rige (nula en aportes), sus saldos por concepto, su
/// disponible desglosado, si es la marcada exenta del GMF y lo que hace falta saber del titular (menor, persona jurídica, nombre). (nuevo)
/// </summary>
public sealed record CuentaParaMovimiento(
    MemberAccount Cuenta,
    DepositLineVersion? Version,
    IReadOnlyDictionary<MemberAccountConcept, decimal> Saldos,
    DisponibleCalculado Disponible,
    bool ExentaDeGmf,
    bool TitularMenor,
    bool TitularJuridico,
    Person Titular)
{
    /// <summary>El saldo del titular (data-model §0.2).</summary>
    public decimal Saldo => Disponible.Saldo;
}

/// <summary>Lo que dejó la atribución: quién aprobó (si hizo falta) y la solicitud de aprobación que se usó. (nuevo)</summary>
public sealed record AtribucionEvaluada(int? AprobadoPor, Guid? AprobacionPublicId)
{
    public static AtribucionEvaluada SinAprobacion { get; } = new(null, null);
}

/// <summary>
/// Las reglas comunes de los movimientos de una cuenta (feature 014, US4, T111; contrato-de-cuentas §3.7–§3.10; FR-050 a FR-059): el disponible (el mismo
/// para consultar, simular y retirar, SC-007), las firmas, los topes de la línea, la atribución con monto y su aprobador presente, el GMF por su motor, el
/// canje por clase de medio y la regla de «sólo asociados». No escribe nada salvo, al pedir una aprobación, la solicitud de la plataforma (la guarda
/// quien llama, en su transacción). Bloqueos y embargos llegan con US11 (K8): hasta entonces suman cero. (nuevo)
/// </summary>
public sealed class ReglasDeMovimientos(
    IApplicationDbContext db,
    ReglasDeParametrosDeCartera parametros,
    IValoresNacionales valoresNacionales,
    ICalendarioHabil calendario,
    IActorActual actorActual,
    IDateTimeService reloj,
    IMotorDeAprobaciones? aprobaciones = null)
{
    /// <summary>La atribución con monto del retiro y del traslado (api.md §1.6).</summary>
    public const string PermisoDeRetiro = "Lending.Accounts.Withdraw";

    /// <summary>La atribución con monto de la consignación en efectivo (api.md §1.6; FR-059).</summary>
    public const string PermisoDeConsignacion = "Lending.Accounts.Deposit";

    /// <summary>US6 (T173; api.md §1.6): la atribución con monto de la redención anticipada de un CDAT (el capital redimido antes del vencimiento).</summary>
    public const string PermisoDeRedencion = "Lending.TermDeposits.Redeem";

    /// <summary>El JSON de canje que rige si el parámetro no responde (el defecto del catálogo, C14).</summary>
    private static string CanjePorDefecto =>
        ParametrosDeCartera.Definiciones.First(d => d.Clave == ParametrosDeCartera.CaptacionesDiasDeCanjePorClaseDeMedio).DefectoSeguro;

    // ------------------------------------------------------------------------------------------------------- la cuenta --

    /// <summary>La cuenta con su versión, saldos, disponible y titular a <paramref name="fecha"/>.</summary>
    public async Task<CuentaParaMovimiento> LeerAsync(MemberAccount cuenta, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        var version = cuenta.Nature == LineNature.Deposit
            ? await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, cuenta.CreditLineId, fecha, null, ct)
            : null;
        var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
        var enCanje = (await EnCanjeAsync([cuenta.Id], fecha, ct)).GetValueOrDefault(cuenta.Id);
        var minimo = cuenta.Status == MemberAccountStatus.Opening ? 0m : version?.MinBalance ?? 0m;
        var disponible = DisponibleDeCuenta.Calcular(cuenta.Nature, LectorDeCuentas.SaldoDelTitular(cuenta.Nature, saldos), 0m, 0m, enCanje, minimo);
        var exenta = await db.GmfExemptAccounts.AsNoTracking()
            .AnyAsync(g => g.MemberAccountId == cuenta.Id && !g.IsDeleted && g.ValidFrom <= fecha && (g.ValidTo == null || g.ValidTo >= fecha), ct);
        var titular = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == cuenta.HolderPersonId, ct);
        var juridico = LectorDeCuentas.EsJuridica(titular);
        var menor = !juridico && ReglasDeTitularidad.EsMenor(titular.DateOfBirth, fecha, await LectorDeCuentas.MayoriaDeEdadAsync(parametros, fecha, ct));
        return new CuentaParaMovimiento(cuenta, version, saldos, disponible, exenta, menor, juridico, titular);
    }

    /// <summary>
    /// Lo que sigue en canje de cada cuenta a <paramref name="fecha"/> (FR-050, C14): las consignaciones vivas con <c>ClearingUntil</c> posterior a la fecha
    /// que nadie liberó todavía. Suma al saldo, no al disponible.
    /// </summary>
    public async Task<Dictionary<int, decimal>> EnCanjeAsync(IReadOnlyCollection<int> cuentas, DateOnly fecha, CancellationToken ct)
    {
        if (cuentas.Count == 0) return [];
        var ids = cuentas.ToArray();
        return await db.MemberAccountOperations.AsNoTracking()
            .Where(o => ids.Contains(o.MemberAccountId) && o.ClearingUntil != null && o.ClearingUntil > fecha && o.ClearedAt == null
                        && o.ReversedByOperationId == null && !o.IsDeleted)
            .GroupBy(o => o.MemberAccountId)
            .Select(g => new { g.Key, Total = g.Sum(o => o.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
    }

    /// <summary>El disponible del contrato (el mismo número que valida el retiro, SC-007).</summary>
    public static DisponibleDto Dto(DisponibleCalculado d) => new(d.Saldo, d.Retenido, d.Embargado, d.EnCanje, d.Minimo, d.Disponible);

    /// <summary>El disponible de unas cuentas en bloque (la consulta del contrato y las listas).</summary>
    public async Task<Dictionary<int, DisponibleCalculado>> DisponiblesAsync(IReadOnlyList<MemberAccount> cuentas, DateOnly fecha, CancellationToken ct)
    {
        var ids = cuentas.Select(c => c.Id).ToList();
        var saldos = await LectorDeCuentas.SaldosAsync(db, ids, ct);
        var canje = await EnCanjeAsync(ids, fecha, ct);
        var resultado = new Dictionary<int, DisponibleCalculado>();
        foreach (var cuenta in cuentas)
        {
            var minimo = 0m;
            if (cuenta.Nature == LineNature.Deposit && cuenta.Status != MemberAccountStatus.Opening)
                minimo = (await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, cuenta.CreditLineId, fecha, null, ct))?.MinBalance ?? 0m;
            resultado[cuenta.Id] = DisponibleDeCuenta.Calcular(cuenta.Nature,
                LectorDeCuentas.SaldoDelTitular(cuenta.Nature, saldos.GetValueOrDefault(cuenta.Id) ?? []), 0m, 0m, canje.GetValueOrDefault(cuenta.Id), minimo);
        }
        return resultado;
    }

    // --------------------------------------------------------------------------------------------------------- firmas --

    /// <summary>Las firmas de un retiro o un traslado (FR-051, US4-1) y el mapa de Id interno a <c>PublicId</c> de las personas de la cuenta.</summary>
    public async Task<(FirmasRevisadas Firmas, IReadOnlyDictionary<int, Guid> Personas)> FirmasAsync(CuentaParaMovimiento c, IReadOnlyCollection<Guid> firmantes,
        decimal monto, DateOnly fecha, CancellationToken ct)
    {
        var partes = await db.MemberAccountParties.AsNoTracking()
            .Where(p => p.MemberAccountId == c.Cuenta.Id && !p.IsDeleted && p.Role != MemberAccountPartyRole.Beneficiary && p.ValidFrom <= fecha
                        && (p.ValidTo == null || p.ValidTo >= fecha))
            .Select(p => new { p.PersonId, p.Role, p.SignatureLimit }).ToListAsync(ct);
        var idsDePersona = partes.Select(p => p.PersonId).Append(c.Cuenta.HolderPersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => idsDePersona.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var publicos = firmantes.Distinct().ToList();
        var firmaron = personas.Where(kv => publicos.Contains(kv.Value)).Select(kv => kv.Key).ToList();
        var revisadas = FirmasDeCuenta.Revisar(c.Cuenta.HandlingMode, c.Cuenta.HolderPersonId, c.TitularMenor, c.TitularJuridico,
            partes.Select(p => new ParteFirmante(p.PersonId, p.Role, p.SignatureLimit)).ToList(), firmaron, monto);
        return (revisadas, personas);
    }

    // ----------------------------------------------------------------------------------------------------------- topes --

    /// <summary>
    /// Los topes de la línea (FR-051, FR-059): el retiro máximo por operación y por día (<c>Lending.Savings.WithdrawalLimitExceeded</c>) y el efectivo máximo
    /// por operación y por día (<c>Lending.Savings.CashLimitExceeded</c>), contando lo ya hecho el mismo día valor en la cuenta y sin lo reversado.
    /// </summary>
    public async Task<Error?> TopesAsync(CuentaParaMovimiento c, decimal monto, ClaseDeMedioDeRecaudo medio, bool esRetiro, DateOnly fecha, CancellationToken ct)
    {
        var v = c.Version;
        if (v is null) return null;
        var delDia = db.MemberAccountOperations.AsNoTracking()
            .Where(o => o.MemberAccountId == c.Cuenta.Id && o.ValueDate == fecha && o.ReversedByOperationId == null && !o.IsDeleted);
        if (esRetiro && (v.MaxWithdrawalPerOperation is not null || v.MaxWithdrawalPerDay is not null))
        {
            var hoy = await delDia.Where(o => o.Kind == MemberAccountOperationKind.Withdrawal || o.Kind == MemberAccountOperationKind.Transfer)
                .SumAsync(o => (decimal?)o.Amount, ct) ?? 0m;
            if (monto > (v.MaxWithdrawalPerOperation ?? decimal.MaxValue) || hoy + monto > (v.MaxWithdrawalPerDay ?? decimal.MaxValue))
                return ErroresDeMovimientos.TopeDeRetiro(v.MaxWithdrawalPerOperation, v.MaxWithdrawalPerDay, hoy, monto);
        }
        if (medio == ClaseDeMedioDeRecaudo.Cash && (v.MaxCashPerOperation is not null || v.MaxCashPerDay is not null))
        {
            var clase = esRetiro ? MemberAccountOperationKind.Withdrawal : MemberAccountOperationKind.Deposit;
            var efectivo = nameof(ClaseDeMedioDeRecaudo.Cash);
            var hoy = await delDia.Where(o => o.Kind == clase && o.PaymentMeansClass == efectivo).SumAsync(o => (decimal?)o.Amount, ct) ?? 0m;
            var maximo = Math.Min(v.MaxCashPerOperation ?? decimal.MaxValue, Math.Max(0m, (v.MaxCashPerDay ?? decimal.MaxValue) - hoy));
            if (monto > maximo) return ErroresDeMovimientos.TopeDeEfectivo(v.MaxCashPerOperation ?? v.MaxCashPerDay ?? 0m, monto, hoy);
        }
        return null;
    }

    // --------------------------------------------------------------------------------------------------- sólo asociados --

    /// <summary>
    /// FR-030 (research E75): una cuenta de una línea que exige asociado —y toda cuenta de aportes— no recibe consignaciones si el titular dejó de ser
    /// asociado vigente; y ninguna cuenta del titular las recibe mientras su retiro esté aprobado, liquidado o por rehacer (sólo las del proceso de retiro,
    /// que son internas).
    /// </summary>
    public async Task<Error?> SoloAsociadosAsync(CuentaParaMovimiento c, DateOnly fecha, CancellationToken ct)
    {
        var retiro = await db.MembershipWithdrawals.AsNoTracking()
            .Where(w => w.PersonId == c.Cuenta.HolderPersonId && !w.IsDeleted
                        && (w.Status == MembershipWithdrawalStatus.Approved || w.Status == MembershipWithdrawalStatus.Settled
                            || w.Status == MembershipWithdrawalStatus.ToRedo))
            .Select(w => w.Number).FirstOrDefaultAsync(ct);
        if (retiro is not null) return ErroresDeMovimientos.RetiroDeAsociadoEnCurso(c.Titular.PublicId, retiro);
        var exige = c.Cuenta.Nature == LineNature.Contribution || c.Version is { RequiresAssociate: true };
        if (exige && !(await PersonaDeOriginacion.AfiliacionAsync(db, c.Titular, fecha, ct)).Activo)
            return ErroresDeCuentas.SoloAsociados(c.Titular.PublicId, ReferenciasDeGobierno.Nombre(c.Titular), c.Titular.IsAssociate ? "Withdrawn" : "NotAssociate");
        return null;
    }

    // ------------------------------------------------------------------------------------------------------------- GMF --

    /// <summary>
    /// La política del GMF que rige en <paramref name="fecha"/> (research R07; parámetros <c>Gmf.*</c> con vigencia y la UVT): sin UVT,
    /// <c>NationalValues.Missing</c>; con <c>Gmf.HechosGeneradores</c> ilegible, <c>Lending.Gmf.ParametersInvalid</c> (falla cerrado).
    /// </summary>
    public async Task<Result<PoliticaDeGmf>> PoliticaDeGmfAsync(DateOnly fecha, CancellationToken ct)
    {
        var tarifa = await DecimalAsync(ParametrosDeCartera.GmfTarifa, fecha, ct);
        var tope = await DecimalAsync(ParametrosDeCartera.GmfTopeExencionUvt, fecha, ct);
        var politica = await LectorDeCuentas.PoliticaDeGmfAsync(parametros, fecha, ct);
        var hechos = await parametros.LeerAsync(ParametrosDeCartera.GmfHechosGeneradores, fecha, ct: ct);
        var texto = hechos.IsSuccess ? hechos.Value.Texto
            : ParametrosDeCartera.Definiciones.First(d => d.Clave == ParametrosDeCartera.GmfHechosGeneradores).DefectoSeguro;
        var (gravados, exentos, problema) = PoliticaDeGmf.LeerHechos(texto);
        if (problema is not null) return Result.Failure<PoliticaDeGmf>(ErroresDeMovimientos.GmfSinParametros(problema));
        if (tarifa is not { } t || tope is not { } u)
            return Result.Failure<PoliticaDeGmf>(ErroresDeMovimientos.GmfSinParametros("la tarifa o el tope no se pudieron leer."));
        // D54 (2026-10-18): el redondeo es parámetro de la cooperativa (defecto al peso).
        var redondeo = await parametros.LeerAsync(ParametrosDeCartera.GmfRedondeo, fecha, ct: ct);
        var alPeso = !(redondeo.IsSuccess && string.Equals(redondeo.Value.Texto, "Centavo", StringComparison.Ordinal));
        if (t == 0m || politica == GmfExemptionPolicy.NotApplicable)
            return Result.Success(new PoliticaDeGmf(t, u, 0m, politica, gravados, exentos) { AlPeso = alPeso });
        var uvt = await valoresNacionales.LeerAsync(CodigosDeValorNacional.Uvt, string.Empty, fecha, ct: ct);
        if (uvt.IsFailure) return Result.Failure<PoliticaDeGmf>(uvt.Error);
        if (uvt.Value.Valor is not { } valor)
            return Result.Failure<PoliticaDeGmf>(new ErrorConDatos("NationalValues.Missing", $"No hay UVT vigente el {ErroresDeCuentas.Fecha(fecha)}: el GMF no se calcula.",
                new { code = CodigosDeValorNacional.Uvt, date = ErroresDeCuentas.Fecha(fecha) }));
        return Result.Success(new PoliticaDeGmf(t, u, valor, politica, gravados, exentos) { AlPeso = alPeso });
    }

    /// <summary>
    /// El GMF de una disposición de la cuenta (FR-053) por <see cref="GravamenMovimientosFinancieros"/>: con lo exento ya usado en el mes por el titular
    /// (Σ de <c>LND_GmfMonthlyUsage</c>, leído bajo el cerrojo de la persona). Nulo si la cuenta no es de captación.
    /// </summary>
    public async Task<Result<GmfCalculado?>> GmfAsync(CuentaParaMovimiento c, string hecho, decimal monto, DateOnly fecha, CancellationToken ct)
    {
        if (c.Cuenta.Nature != LineNature.Deposit || monto <= 0m) return Result.Success<GmfCalculado?>(null);
        var politica = await PoliticaDeGmfAsync(fecha, ct);
        if (politica.IsFailure) return Result.Failure<GmfCalculado?>(politica.Error);
        var usado = await ExentoUsadoAsync(c.Cuenta.HolderPersonId, fecha, ct);
        return Result.Success<GmfCalculado?>(GravamenMovimientosFinancieros.Calcular(politica.Value,
            new DisposicionDeGmf(hecho, monto, c.Version?.IsGmfSubject ?? true, c.ExentaDeGmf, usado)));
    }

    /// <summary>Lo exento usado en el mes de <paramref name="fecha"/> por la persona (Σ de sus filas, con los reversos en negativo).</summary>
    public async Task<decimal> ExentoUsadoAsync(int persona, DateOnly fecha, CancellationToken ct)
    {
        var anio = (short)fecha.Year;
        var mes = (byte)fecha.Month;
        return Math.Max(0m, await db.GmfMonthlyUsages.AsNoTracking().Where(u => u.PersonId == persona && u.Year == anio && u.Month == mes && !u.IsDeleted)
            .SumAsync(u => (decimal?)u.ExemptAmount, ct) ?? 0m);
    }

    public static GmfCalculadoDto? Dto(GmfCalculado? g) => g is null
        ? null
        : new GmfCalculadoDto(g.Base, g.ExentoUsadoAntes, g.ExentoAplicado, g.BaseGravada, g.Tarifa, g.Valor, g.Politica.ToString(), g.Explicacion);

    // ----------------------------------------------------------------------------------------------------------- canje --

    /// <summary>
    /// Hasta cuándo queda en canje una consignación (FR-050, C14): los días hábiles de su clase de medio (<c>Captaciones.DiasDeCanjePorClaseDeMedio</c>,
    /// JSON <c>{ clase: días, "*": días }</c>) contados desde la fecha valor, sin sábados, domingos ni festivos. Nulo = sin canje.
    /// </summary>
    public async Task<DateOnly?> CanjeHastaAsync(ClaseDeMedioDeRecaudo medio, DateOnly fechaValor, CancellationToken ct)
    {
        var dias = await DiasDeCanjeAsync(medio, fechaValor, ct);
        if (dias <= 0) return null;
        var cal = await calendario.ObtenerAsync(fechaValor, fechaValor.AddMonths(2), DiasDeDescanso.SabadoYDomingo, ct: ct);
        return cal.SumarHabiles(fechaValor, dias);
    }

    /// <summary>Los días hábiles de canje de una clase de medio; un JSON ilegible rige como el defecto del catálogo.</summary>
    public async Task<int> DiasDeCanjeAsync(ClaseDeMedioDeRecaudo medio, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.CaptacionesDiasDeCanjePorClaseDeMedio, fecha, ct: ct);
        return DiasDeCanje(leido.IsSuccess ? leido.Value.Texto : CanjePorDefecto, medio) ?? DiasDeCanje(CanjePorDefecto, medio) ?? 0;
    }

    /// <summary>Los días de una clase de medio en el JSON del parámetro (la clase, si no, <c>*</c>); nulo si el JSON no se lee.</summary>
    public static int? DiasDeCanje(string? json, ClaseDeMedioDeRecaudo medio)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            int? comodin = null;
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.Number || !p.Value.TryGetInt32(out var n)) continue;
                if (string.Equals(p.Name, medio.ToString(), StringComparison.OrdinalIgnoreCase)) return Math.Max(0, n);
                if (p.Name == "*") comodin = Math.Max(0, n);
            }
            return comodin ?? 0;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------------------------------------------- atribución --

    /// <summary>
    /// La atribución con monto del usuario que actúa (api.md §1.6; FR-051, FR-059; US4-5): sin usuario (el proceso) o dentro de su límite
    /// (<c>SEC_PermissionAmountLimits</c>, el mayor de sus roles), pasa. Por encima, con una aprobación de la plataforma <b>aprobada</b> de esta misma operación
    /// (misma cuenta, monto y huella) pasa con su aprobador; si no, queda en espera: se pide (o se reusa la pendiente) una aprobación de un nivel con
    /// aprobador presente con segundo factor y responde <c>Lending.Savings.PendingApproval</c> con su <c>approvalPublicId</c>. La solicitud la guarda quien
    /// llama. No mueve saldo.
    /// </summary>
    public async Task<Result<AtribucionEvaluada>> AtribucionAsync(string permiso, MemberAccount cuenta, decimal monto, string huella, string rotulo,
        Guid? aprobacionPublicId, DateOnly fecha, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario || actor.EsProceso) return Result.Success(AtribucionEvaluada.SinAprobacion);
        var maximo = await LimitesDePermisoPorUsuario.DeAsync(db, usuario, permiso, fecha, ct);
        if (maximo is not { } tope || monto <= tope) return Result.Success(AtribucionEvaluada.SinAprobacion);

        // US6 (T173; api.md §1.6): la redención anticipada de un CDAT sobre su atribución se aprueba como un retiro (el mismo sujeto, con su permiso).
        var sujeto = permiso is PermisoDeRetiro or PermisoDeRedencion ? ApprovalSubjects.AccountWithdrawal : ApprovalSubjects.AccountCashDeposit;
        if (aprobacionPublicId is { } pedida)
        {
            var aprobada = await db.ApprovalRequests.AsNoTracking().Include(r => r.Decisions)
                .FirstOrDefaultAsync(r => r.PublicId == pedida && r.SourceType == ApprovalSourceTypes.MemberAccountOperation && r.Subject == sujeto
                                          && r.SourcePublicId == cuenta.PublicId && r.ContentSha256 == huella, ct);
            if (aprobada is { Status: ApprovalRequestStatus.Approved })
            {
                var aprobador = aprobada.Decisions.Where(d => d.Decision == ApprovalDecisionKind.Approve).OrderByDescending(d => d.Level).Select(d => (int?)d.DecidedByUserId)
                    .FirstOrDefault();
                return Result.Success(new AtribucionEvaluada(aprobador, aprobada.PublicId));
            }
            if (aprobada is { Status: ApprovalRequestStatus.Pending })
                return Result.Failure<AtribucionEvaluada>(ErroresDeMovimientos.PendienteDeAprobacion(aprobada.PublicId, permiso, monto, tope));
        }

        var pendiente = await db.ApprovalRequests.AsNoTracking()
            .Where(r => r.SourceType == ApprovalSourceTypes.MemberAccountOperation && r.SourcePublicId == cuenta.PublicId && r.ContentSha256 == huella
                        && r.Status == ApprovalRequestStatus.Pending)
            .Select(r => (Guid?)r.PublicId).FirstOrDefaultAsync(ct);
        if (pendiente is { } yaPedida) return Result.Failure<AtribucionEvaluada>(ErroresDeMovimientos.PendienteDeAprobacion(yaPedida, permiso, monto, tope));
        if (aprobaciones is null) return Result.Failure<AtribucionEvaluada>(ErroresDeMovimientos.PendienteDeAprobacion(Guid.Empty, permiso, monto, tope));

        var solicitud = await aprobaciones.SolicitarAsync(new SolicitudDeAprobacion(sujeto, ApprovalSourceTypes.MemberAccountOperation, cuenta.PublicId,
            rotulo.Length <= 200 ? rotulo : rotulo[..200], null, null, null, monto, fecha, usuario, [], huella, permiso), ct);
        if (solicitud.IsFailure) return Result.Failure<AtribucionEvaluada>(solicitud.Error);
        // D53 (2026-10-18): la política de la cooperativa puede no exigir aprobación para este monto (sus umbrales no lo alcanzan): procede.
        if (solicitud.Value is null) return Result.Success(AtribucionEvaluada.SinAprobacion);
        return Result.Failure<AtribucionEvaluada>(ErroresDeMovimientos.PendienteDeAprobacion(solicitud.Value.PublicId, permiso, monto, tope));
    }

    /// <summary>
    /// Lo que exigiría la atribución sin pedir nada (la simulación, §3.7): nulo si el usuario que actúa no la supera; si no, el permiso, el monto y su
    /// máximo.
    /// </summary>
    public async Task<AtribucionRequeridaDto?> AtribucionRequeridaAsync(string permiso, decimal monto, DateOnly fecha, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario || actor.EsProceso) return null;
        return await LimitesDePermisoPorUsuario.DeAsync(db, usuario, permiso, fecha, ct) is { } tope && monto > tope
            ? new AtribucionRequeridaDto(permiso, monto, tope)
            : null;
    }

    /// <summary>
    /// La huella de lo que se aprueba (la solicitud de aprobación la sella y el reintento la compara): la clase de la operación, el recaudador y su clave, la
    /// cuenta, el monto y, en un traslado, la cuenta destino.
    /// </summary>
    public static string HuellaDeOperacion(string clase, string? recaudador, string? clave, Guid cuenta, decimal monto, Guid? destino = null) =>
        new IngenIA365ERP.Domain.Lending.HuellaDeCartera()
            .Texto("kind", clase).Texto("collector", recaudador).Texto("key", clave).Identificador("account", cuenta).Decimal("amount", monto, 2)
            .Identificador("target", destino).Calcular();

    private async Task<decimal?> DecimalAsync(string clave, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(clave, fecha, ct: ct);
        var texto = leido.IsSuccess ? leido.Value.Texto : ParametrosDeCartera.Definiciones.First(d => d.Clave == clave).DefectoSeguro;
        if (leido.IsSuccess && leido.Value.Valor is decimal d) return d;
        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
    }
}
