using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Holds;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Accrual;

/// <summary>
/// La reactivación de una cuenta inactiva por un movimiento del titular (feature 014, US14, T208; FR-058, FR-084; research C20, E159, §23): la cuenta vuelve a
/// <c>Active</c> y sus saldos pasan del capital de captación <b>inactiva</b> al de la activa en una operación <c>Reclassification</c> con su
/// <c>CaptacionReclasificada</c> (causa <c>Reactivation</c>). La llaman la consignación, el retiro, el traslado (las dos puntas) y la entrega del contractual
/// <b>después</b> de guardar las filas de su operación: el hecho de la operación se contabilizó con la cuenta todavía inactiva, así que lo que se mueve es todo
/// el saldo de ese momento. Sin saldo la cuenta se reactiva sin operación. En la transacción y bajo el cerrojo de quien llama. (nuevo)
/// </summary>
public sealed class ReactivacionDeCuenta(ReclasificacionDeCuenta reclasificacion, IDateTimeService reloj)
{
    /// <summary>Reactiva la cuenta si está inactiva (y reclasifica); falso si no lo estaba.</summary>
    public async Task<Result<bool>> ReactivarAsync(MemberAccount cuenta, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        if (cuenta.Status != MemberAccountStatus.Inactive) return Result.Success(false);
        var r = await reclasificacion.ReclasificarAsync(cuenta, fecha, EmisionDeCartera.CausaReactivacion, () => cuenta.Reactivar(reloj.UtcNow), null, ct);
        return r.IsFailure ? Result.Failure<bool>(r.Error) : Result.Success(true);
    }

    /// <summary>
    /// Sin el servicio (pruebas que arman la operación a mano), la reactivación de siempre, sin reclasificar; con él, la reactivación con su
    /// reclasificación. Guarda lo que la reclasificación necesita.
    /// </summary>
    public static async Task<Result<bool>> ReactivarAsync(ReactivacionDeCuenta? servicio, MemberAccount cuenta, DateOnly fecha, DateTime ahora, CancellationToken ct)
    {
        if (cuenta.Status != MemberAccountStatus.Inactive) return Result.Success(false);
        if (servicio is not null) return await servicio.ReactivarAsync(cuenta, fecha, ct);
        cuenta.Reactivar(ahora);
        return Result.Success(true);
    }
}

/// <summary>
/// El subpaso «inactividad» del paso de captaciones (feature 014, US14, T208; FR-058, FR-084, D21; research §23): en cada tanda, las cuentas de captación
/// <b>activas</b> —no el CDAT, que no tiene movimientos del titular— cuya versión vigente de la línea trae <c>InactivityDays</c>. Se mide desde el último
/// movimiento del titular (<c>LND_MemberAccountControls.LastHolderActivityDate</c>; sin él, la activación o la apertura); en una cuenta migrada nunca antes del
/// corte (research E119, decisión A: el plazo corre desde la activación). Cumplido el plazo, en una transacción con el cerrojo del proceso (salta la cuenta
/// ocupada): la cuenta pasa a <c>Inactive</c> desde el día que lo detecta el proceso, sus saldos se reclasifican al capital de captación inactiva
/// (<c>CaptacionReclasificada</c>, causa <c>Inactivity</c>) y se avisa al titular (<c>INACTIVA</c>). <c>Captaciones.DiasDeAvisoPrevioDeInactividad</c> días
/// antes se le avisa que pasará (<c>PREINACTIV</c>), una vez por plazo. El abono de intereses, los cargos (también el de inactividad, que causa el subpaso de
/// cargos periódicos sobre la cuenta inactiva), la reclasificación y lo de la asamblea no son movimientos del titular (FR-058). Corre desde K10. (nuevo)
/// </summary>
public sealed class Inactividad(
    IApplicationDbContext db,
    ReclasificacionDeCuenta reclasificacion,
    ReglasDeParametrosDeCartera parametros,
    ICerrojoDeCartera cerrojo,
    IContextoDeLineasDeCaptacion contexto,
    IDateTimeService reloj,
    ColaDeMensajesAlDeudor? cola = null,
    ILogger<Inactividad>? logger = null) : ISubpasoDeCaptaciones
{
    public const string PlantillaDeAvisoPrevio = "PREINACTIV";
    public const string PlantillaDeInactividad = "INACTIVA";

    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public string Nombre => "Inactividad";

    /// <summary>Una cuenta que se mide: su plazo de la línea y desde cuándo cuenta.</summary>
    public sealed record CuentaQueSeMide(int MemberAccountId, int Dias, DateOnly Desde)
    {
        /// <summary>El día en que cumple el plazo.</summary>
        public DateOnly Limite => Desde.AddDays(Dias);
    }

    /// <summary>
    /// Desde cuándo se mide la inactividad de una cuenta (FR-084; research E119): el último movimiento del titular, la activación o la apertura; en una
    /// migrada, nunca antes del corte (la víspera de la activación).
    /// </summary>
    public static DateOnly DesdeCuando(MemberAccount cuenta, DateOnly? ultimoMovimiento)
    {
        var desde = ultimoMovimiento ?? cuenta.ActivatedOn ?? cuenta.OpenedOn;
        if (cuenta.Origin == MemberAccountOrigin.Migration && cuenta.ActivatedOn is { } activada && desde < activada.AddDays(-1))
            desde = activada.AddDays(-1);
        return desde;
    }

    /// <summary>Las cuentas activas de la tanda que se miden (con plazo en su línea), con su límite.</summary>
    public async Task<IReadOnlyList<CuentaQueSeMide>> CandidatasAsync(IReadOnlyList<int> cuentas, DateOnly d, CancellationToken ct)
    {
        var ids = cuentas.ToArray();
        var filas = await (from a in db.MemberAccounts.AsNoTracking()
                           join c in db.MemberAccountControls.AsNoTracking() on a.Id equals c.MemberAccountId
                           where ids.Contains(a.Id) && a.Nature == LineNature.Deposit && a.Status == MemberAccountStatus.Active && !a.IsDeleted
                                 && a.DepositClass != DepositClass.TermDeposit
                           select new { Cuenta = a, c.LastHolderActivityDate })
            .ToListAsync(ct);
        var dias = new Dictionary<int, int?>();
        var r = new List<CuentaQueSeMide>();
        foreach (var f in filas.OrderBy(f => f.Cuenta.Id))
        {
            if (!dias.TryGetValue(f.Cuenta.CreditLineId, out var plazo))
            {
                plazo = (await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, f.Cuenta.CreditLineId, d, null, ct))?.InactivityDays;
                dias[f.Cuenta.CreditLineId] = plazo;
            }
            if (plazo is not > 0) continue;
            r.Add(new CuentaQueSeMide(f.Cuenta.Id, plazo.Value, DesdeCuando(f.Cuenta, f.LastHolderActivityDate)));
        }
        return r;
    }

    public async Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (cuentas.Count == 0 || contexto.Entrega < EntregaDeCartera.K10) return [];
        var aviso = await AvisoPrevioAsync(parametros, d, ct);
        var fallos = new List<FalloDeCuenta>();
        foreach (var c in await CandidatasAsync(cuentas, d, ct))
        {
            try
            {
                if (d >= c.Limite) await InactivarAsync(c, d, ct);
                else if (aviso > 0 && d >= c.Limite.AddDays(-aviso)) await AvisarAsync(c.MemberAccountId, PlantillaDeAvisoPrevio, "preinactiva", c, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.DescartarCambiosSalvo(corrida);
                _log.LogWarning(ex, "[Cartera.InactividadFallida] La cuenta {Cuenta} no pasó a inactiva el {Dia}.", c.MemberAccountId, d);
                fallos.Add(new FalloDeCuenta(c.MemberAccountId, ex is ReglaDeCarteraException r ? r.Codigo : "Lending.Savings.InactivityFailed", ex.Message));
            }
        }
        return fallos;
    }

    /// <summary>Los días del aviso previo (<c>Captaciones.DiasDeAvisoPrevioDeInactividad</c>); sin lectura (antes de K10), el defecto del catálogo.</summary>
    public static async Task<int> AvisoPrevioAsync(ReglasDeParametrosDeCartera parametros, DateOnly d, CancellationToken ct) =>
        await EnteroAsync(parametros, ParametrosDeCartera.CaptacionesDiasDeAvisoPrevioDeInactividad, d, ct) ?? 0;

    /// <summary>Un parámetro entero a la fecha; sin lectura, el defecto del catálogo; vacío, nulo.</summary>
    public static async Task<int?> EnteroAsync(ReglasDeParametrosDeCartera parametros, string clave, DateOnly d, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(clave, d, ct: ct);
        var texto = leido.IsSuccess
            ? leido.Value.Valor switch { int i => i.ToString(CultureInfo.InvariantCulture), decimal m => ((int)m).ToString(CultureInfo.InvariantCulture), _ => leido.Value.Texto }
            : ParametrosDeCartera.Definiciones.FirstOrDefault(x => x.Clave == clave)?.DefectoSeguro;
        return int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private async Task InactivarAsync(CuentaQueSeMide c, DateOnly d, CancellationToken ct)
    {
        var hecha = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            if ((await cerrojo.BloquearCuentasAsync([c.MemberAccountId], ModoDeEspera.Saltar, ct)).Count == 0) return Result.Success(false);
            var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == c.MemberAccountId, ct);
            // Releída bajo el cerrojo: un movimiento del titular del mismo día la dejó activa con otro último movimiento.
            var ultimo = await db.MemberAccountControls.AsNoTracking().Where(x => x.MemberAccountId == cuenta.Id).Select(x => x.LastHolderActivityDate)
                .FirstAsync(ct);
            if (cuenta.Status != MemberAccountStatus.Active || DesdeCuando(cuenta, ultimo).AddDays(c.Dias) > d) return Result.Success(false);
            var r = await reclasificacion.ReclasificarAsync(cuenta, d, EmisionDeCartera.CausaInactividad, () => cuenta.Inactivar(d, reloj.UtcNow), null, ct);
            if (r.IsFailure) return Result.Failure<bool>(r.Error);
            await AvisarAsync(cuenta.Id, PlantillaDeInactividad, "inactiva", c, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success(true);
        }, ct);
        if (hecha.IsFailure) throw new ReglaDeCarteraException(new ProblemaDeCartera(hecha.Error.Code, hecha.Error.Message));
    }

    /// <summary>Encola el aviso al titular, sin guardar; lo que no se encola queda en el log (nunca tumba el paso). La clave lo deja en uno por plazo.</summary>
    private async Task AvisarAsync(int cuentaId, string plantilla, string clase, CuentaQueSeMide c, CancellationToken ct)
    {
        if (cola is null) return;
        var cuenta = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == cuentaId, ct);
        try
        {
            var linea = await db.CreditLines.AsNoTracking().Where(l => l.Id == cuenta.CreditLineId).Select(l => l.Name).FirstOrDefaultAsync(ct) ?? string.Empty;
            var r = await cola.EncolarAsync(new PedidoDeMensajeAlDeudor(cuenta.HolderPersonId, null, DebtorMessagePurpose.Informative, MessageChannel.Email,
                plantilla, DebtorMessage.OrigenInactividadDeCuenta, $"{cuenta.PublicId:N}:{clase}:{c.Limite:yyyyMMdd}", ActorKind.Process, null)
            {
                OriginPublicId = cuenta.PublicId,
                Valores = new Dictionary<string, string?>
                {
                    ["NumeroDeCuenta"] = cuenta.Number,
                    ["NombreDeLaLinea"] = linea,
                    ["FechaDeInactividad"] = (clase == "inactiva" ? cuenta.InactiveSince ?? c.Limite : c.Limite).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    ["DiasDeInactividad"] = c.Dias.ToString(CultureInfo.InvariantCulture),
                },
            }, ct);
            if (r.IsFailure)
                _log.LogInformation("[Cartera.AvisoDeInactividad] No se encoló el aviso de la cuenta {Cuenta}: {Codigo} {Mensaje}", cuenta.Number, r.Error.Code,
                    r.Error.Message);
            else if (clase == "preinactiva") await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Cartera.AvisoDeInactividad] El aviso de la cuenta {Cuenta} falló.", cuenta.Number);
        }
    }
}

/// <summary>
/// El subpaso «marcas que rigen» del paso de captaciones (feature 014, US14, T208; research E159, §23): la exención del GMF que se marcó o se terminó para
/// <b>una fecha futura</b> no reclasificó al registrarse (sólo la que rige ese mismo día lo hace en el comando). El día <c>d</c> en que la marca empieza
/// (<c>ValidFrom = d</c>) o deja de regir (<c>ValidTo = d − 1</c>), reclasifica los saldos de la cuenta de la marca de antes a la de ahora
/// (<c>CaptacionReclasificada</c>, causa <c>GmfExemption</c> o <c>GmfExemptionEnd</c>). Mueve sólo lo contabilizado <b>antes</b> de <c>d</c>: lo de ese
/// día ya llevó la marca nueva. Una cuenta que ya tiene la reclasificación de esa causa el mismo día (la hizo el comando) no se repite. Una cuenta embargada
/// no cambia de marca (la del embargo manda). (nuevo)
/// </summary>
public sealed class MarcasQueRigen(
    IApplicationDbContext db,
    ReclasificacionDeCuenta reclasificacion,
    ICerrojoDeCartera cerrojo,
    ILogger<MarcasQueRigen>? logger = null) : ISubpasoDeCaptaciones
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public string Nombre => "MarcasQueRigen";

    public async Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (cuentas.Count == 0) return [];
        var ids = cuentas.ToArray();
        var ayer = d.AddDays(-1);
        var cambian = await db.GmfExemptAccounts.AsNoTracking()
            .Where(g => ids.Contains(g.MemberAccountId) && !g.IsDeleted && (g.ValidFrom == d || g.ValidTo == ayer))
            .Select(g => g.MemberAccountId).Distinct().OrderBy(x => x).ToListAsync(ct);
        var fallos = new List<FalloDeCuenta>();
        foreach (var id in cambian)
        {
            try
            {
                await ReclasificarAsync(id, d, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.DescartarCambiosSalvo(corrida);
                _log.LogWarning(ex, "[Cartera.MarcaQueRigeFallida] La cuenta {Cuenta} no reclasificó su exención del {Dia}.", id, d);
                fallos.Add(new FalloDeCuenta(id, ex is ReglaDeCarteraException r ? r.Codigo : "Lending.Savings.ReclassificationFailed", ex.Message));
            }
        }
        return fallos;
    }

    /// <summary>¿Está la cuenta marcada exenta el <paramref name="dia"/>? (con las marcas de la cuenta).</summary>
    public static bool ExentaEl(IEnumerable<(DateOnly Desde, DateOnly? Hasta)> marcas, DateOnly dia) =>
        marcas.Any(m => m.Desde <= dia && (m.Hasta is null || m.Hasta >= dia));

    private async Task ReclasificarAsync(int cuentaId, DateOnly d, CancellationToken ct)
    {
        var r = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            if ((await cerrojo.BloquearCuentasAsync([cuentaId], ModoDeEspera.Saltar, ct)).Count == 0) return Result.Success();
            var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == cuentaId, ct);
            if (!cuenta.Viva) return Result.Success();
            var marcas = (await db.GmfExemptAccounts.AsNoTracking().Where(g => g.MemberAccountId == cuentaId && !g.IsDeleted)
                .Select(g => new { g.ValidFrom, g.ValidTo }).ToListAsync(ct)).Select(g => (g.ValidFrom, g.ValidTo)).ToList();
            var antes = ExentaEl(marcas, d.AddDays(-1));
            var despues = ExentaEl(marcas, d);
            if (antes == despues) return Result.Success();
            var causa = despues ? EmisionDeCartera.CausaExencionDeGmf : EmisionDeCartera.CausaFinDeExencionDeGmf;
            var marca = $"\"cause\":\"{causa}\"";
            if (await db.MemberAccountOperations.AsNoTracking().AnyAsync(o => o.MemberAccountId == cuentaId && o.Kind == MemberAccountOperationKind.Reclassification
                                                                               && o.ValueDate == d && o.Payload.Contains(marca), ct))
                return Result.Success();
            // Lo contabilizado antes de d: lo de ese día ya llevó la marca nueva.
            var saldos = (await db.MemberAccountTransactions.AsNoTracking()
                    .Where(t => t.MemberAccountId == cuentaId && !t.IsDeleted && t.AccountingDate < d
                                && (t.Concept == MemberAccountConcept.Principal || t.Concept == MemberAccountConcept.InterestPayable))
                    .GroupBy(t => t.Concept).Select(g => new { g.Key, Valor = g.Sum(t => t.Amount) }).ToListAsync(ct))
                .ToDictionary(x => x.Key, x => x.Valor);
            var hecha = await reclasificacion.ReclasificarAsync(cuenta, d, causa, () => { }, null, ct, exentaDespues: despues, exentaAntes: antes, saldos: saldos);
            if (hecha.IsFailure) return Result.Failure(hecha.Error);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
        if (r.IsFailure) throw new ReglaDeCarteraException(new ProblemaDeCartera(r.Error.Code, r.Error.Message));
    }
}
