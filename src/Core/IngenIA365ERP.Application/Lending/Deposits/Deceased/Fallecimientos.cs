using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Deceased;

/// <summary>Los errores del fallecimiento de un titular (api.md §13). (nuevo)</summary>
public static class ErroresDeFallecimiento
{
    public const string CodigoInexistente = "Lending.DeceasedCase.NotFound";
    public const string CodigoYaRegistrado = "Lending.DeceasedCase.AlreadyRegistered";
    public const string CodigoSinBeneficiarios = "Lending.DeceasedCase.NoBeneficiaries";
    public const string CodigoTopeSuperado = "Lending.DeceasedCase.CapExceeded";
    public const string CodigoSinSoportes = "Lending.DeceasedCase.SupportsRequired";
    public const string CodigoInvalido = "Lending.DeceasedCase.Invalid";
    public const string CodigoNadaQueEntregar = "Lending.DeceasedCase.NothingToDeliver";
    public const string CodigoEstado = "Lending.DeceasedCase.InvalidTransition";
    public const string CodigoCargosPendientes = "Lending.DeceasedCase.PendingCharges";

    public static Error Inexistente() => new(CodigoInexistente, "El caso de fallecimiento no existe.");

    public static Error YaRegistrado(string numero) => new ErrorConDatos(CodigoYaRegistrado,
        $"El fallecimiento del titular ya está registrado en el caso {numero}.", new { number = numero });

    public static Error SinBeneficiarios(IReadOnlyList<string> cuentas) => new ErrorConDatos(CodigoSinBeneficiarios,
        $"Las cuentas {string.Join(", ", cuentas)} no tenían beneficiarios registrados en la fecha de la defunción: se entregan sin juicio de sucesión o por la sucesión.",
        new { accounts = cuentas });

    public static Error TopeSuperado(decimal total, decimal tope, string fuente) => new ErrorConDatos(CodigoTopeSuperado,
        $"El total a entregar ({total:N2}) supera el tope de la entrega sin juicio de sucesión ({tope:N2}): se entrega por la sucesión.",
        new { total, cap = tope, source = fuente });

    public static Error SinSoportes(IReadOnlyList<string> requeridos) => new ErrorConDatos(CodigoSinSoportes,
        requeridos.Count == 0 ? "La entrega exige sus soportes." : $"La entrega exige los soportes: {string.Join(", ", requeridos)}.", new { supportsRequired = requeridos });

    public static Error Invalido(string campo, string mensaje) => new ErrorConDatos(CodigoInvalido, mensaje, new { field = campo });

    public static Error NadaQueEntregar() => new(CodigoNadaQueEntregar, "El titular no tiene saldos de captación que entregar.");

    public static Error Estado(DeceasedCaseStatus estado, string accion) => new ErrorConDatos(CodigoEstado,
        $"El caso está «{estado}»: {accion}.", new { status = estado.ToString() });

    public static Error CargosPendientes(string cuenta) => new ErrorConDatos(CodigoCargosPendientes,
        $"La cuenta {cuenta} tiene cargos por cobrar: se cobran o se condonan antes de la entrega.", new { account = cuenta });
}

/// <summary>A quién se entrega y cómo: la persona, su porcentaje (fracción) y el medio (giro a su banco o traslado a una cuenta suya). (nuevo)</summary>
public sealed record DestinatarioDeEntrega(
    int PersonaId,
    Guid PersonaPublicId,
    decimal? Porcentaje,
    FormaDeEntrega Forma,
    int? CuentaDestinoId,
    Guid? BancoPublicId,
    byte? TipoDeCuenta,
    string? NumeroDeCuenta);

/// <summary>El medio de una entrega por fallecimiento. (nuevo)</summary>
public enum FormaDeEntrega
{
    DisbursementOrder = 1,
    TransferToAccount = 2,
}

/// <summary>El plan sellado de una entrega (<c>DeliveryPlanJson</c>). (nuevo)</summary>
public sealed record PlanDeEntrega(DeliveryRoute Ruta, IReadOnlyList<DestinatarioDeEntrega> Destinatarios);

/// <summary>Una cuenta del titular fallecido con lo que tiene y si se entrega. (nuevo)</summary>
public sealed record CuentaDelFallecido(MemberAccount Cuenta, decimal Saldo, bool Entregable, string? Nota);

/// <summary>Lo que recibe una persona de una cuenta. (nuevo)</summary>
public sealed record PorcionDeEntrega(int CuentaId, int PersonaId, decimal Porcentaje, decimal Valor);

/// <summary>La vista previa de una entrega: las cuentas, lo de cada persona, el tope, los soportes y la huella. (nuevo)</summary>
public sealed record VistaPreviaDeEntrega(
    DeliveryRoute Ruta,
    DateOnly Fecha,
    IReadOnlyList<CuentaDelFallecido> Cuentas,
    IReadOnlyList<PorcionDeEntrega> Porciones,
    IReadOnlyDictionary<int, IReadOnlyList<(int PersonaId, decimal Porcentaje)>> Beneficiarios,
    decimal Total,
    ValorNacionalVigente? Tope,
    IReadOnlyList<string> SoportesRequeridos,
    IReadOnlyList<string> Avisos,
    string Sha256);

/// <summary>
/// Lo común del fallecimiento de un titular (feature 014, US14, T210; data-model §6.5; FR-034, FR-065, FR-072, FR-082, FR-083; research §23): las cuentas
/// que se entregan, los beneficiarios vigentes en la fecha de la defunción, el reparto, el tope de la entrega sin juicio de sucesión y la entrega misma.
/// <list type="bullet">
/// <item><b>Qué se entrega</b>: las cuentas de captación vivas del titular con saldo —a la vista, permanente, contractual y el CDAT ya vencido—. Los aportes
/// no: se devuelven por el retiro del asociado con causa fallecimiento (FR-076). El CDAT que no ha vencido sigue hasta su vencimiento y queda a disposición
/// (FR-072). Una cuenta con embargo vigente no se entrega mientras el embargo retenga (la orden del juez manda).</item>
/// <item><b>A quién</b>: por <c>Beneficiaries</c>, a los beneficiarios del conjunto vigente en la fecha de la defunción de cada cuenta, por su porcentaje; por
/// <c>WithoutProbate</c> (sin juicio de sucesión, hasta <c>TOPE_ENTREGA_SIN_SUCESION</c> con los soportes de <c>Fallecimiento.SoportesDeEntregaSinSucesion</c>)
/// o <c>Probate</c> (sentencia o escritura de la sucesión), a las personas y porcentajes que diga el soporte, iguales en todas las cuentas.</item>
/// <item><b>Cómo</b>: aprobada por otra persona, en una operación <c>DeceasedDelivery</c> (prefijo <c>FAL-</c>) por destinatario y cuenta —por orden de giro
/// (<c>DevolucionOrdenada</c> + <c>OrdenDeGiroEmitida</c> v2, motivo <c>DeceasedDelivery</c>) o por traslado a una cuenta de ahorro viva del destinatario
/// (<c>TrasladoEntreCuentasAplicado</c>)—, después de liquidar los intereses a la fecha. Sin giros pendientes la cuenta se cancela en el acto; con giros queda en
/// cierre y se cancela cuando Tesorería informa el último (<see cref="AlGirarAsync"/>), y el caso queda entregado con todas sus cuentas canceladas.</item>
/// </list>
/// El reparto es por el mayor residuo al centavo: la suma de las porciones es exactamente el saldo. Respuesta 97 del dueño (2026-10-21, E253): cada porción
/// cobra el GMF como cualquier retiro (<c>DeceasedDelivery</c> en <c>Gmf.HechosGeneradores</c>; la bandera <c>GmfOnDeceasedAndUnclaimed</c> de la línea lo
/// apaga): se retiene <b>de la porción</b> —el destinatario recibe el neto, valor − GMF— con su hecho <c>GmfRetenido</c> en la misma unidad. (nuevo)
/// </summary>
public sealed class Fallecimientos(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    LiquidacionALaFecha liquidacion,
    OrdenesDeGiroDeCuentas ordenes,
    ReglasDeParametrosDeCartera parametros,
    IValoresNacionales valoresNacionales,
    IActorActual actorActual,
    IDateTimeService reloj,
    ReactivacionDeCuenta? reactivacion = null,
    ReglasDeMovimientos? movimientos = null)
{
    public const string Prefijo = "FAL-";
    public const string PrefijoEntrega = "FAL-";

    private const decimal Centavo = 1m / 100m;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Lo que se entrega de una cuenta: el capital, el interés por pagar y la bonificación, en ese orden.</summary>
    public static readonly MemberAccountConcept[] ConceptosQueSeEntregan =
        [MemberAccountConcept.Principal, MemberAccountConcept.InterestPayable, MemberAccountConcept.Bonus];

    // --------------------------------------------------------------------------------------------------------- cuentas --

    /// <summary>Las cuentas de captación vivas del titular con lo que tienen y si se entregan hoy (ver la clase).</summary>
    public async Task<IReadOnlyList<CuentaDelFallecido>> CuentasAsync(int personaId, DateOnly hoy, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.MemberAccounts : db.MemberAccounts.AsNoTracking();
        var cuentas = await consulta.Where(a => a.HolderPersonId == personaId && a.Nature == LineNature.Deposit && a.Status != MemberAccountStatus.Closed
                                                && a.Status != MemberAccountStatus.Opening && !a.IsDeleted)
            .OrderBy(a => a.Id).ToListAsync(ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var saldos = await LectorDeCuentas.SaldosAsync(db, ids, ct);
        var embargadas = (await db.AccountHolds.AsNoTracking().Where(h => ids.Contains(h.MemberAccountId) && h.Reason == HoldReason.Garnishment
                                                                            && h.ReleasedAt == null && !h.IsDeleted)
            .Select(h => h.MemberAccountId).ToListAsync(ct)).ToHashSet();
        var conGiro = (await db.AccountPayoutOrders.AsNoTracking().Where(o => ids.Contains(o.MemberAccountId) && !o.IsDeleted
                                                                              && o.Status == DisbursementOrderStatus.Issued)
            .Select(o => o.MemberAccountId).ToListAsync(ct)).ToHashSet();
        var r = new List<CuentaDelFallecido>();
        foreach (var c in cuentas)
        {
            var s = saldos.GetValueOrDefault(c.Id) ?? [];
            var saldo = ConceptosQueSeEntregan.Sum(k => s.GetValueOrDefault(k));
            string? nota = null;
            if (c.DepositClass == DepositClass.TermDeposit && c.Status != MemberAccountStatus.Matured)
                nota = "El CDAT sigue hasta su vencimiento y queda a disposición: se entrega cuando venza (FR-072).";
            else if (embargadas.Contains(c.Id)) nota = "Tiene un embargo vigente: no se entrega mientras el embargo retenga.";
            else if (conGiro.Contains(c.Id)) nota = "Tiene una orden de giro en curso.";
            else if (s.GetValueOrDefault(MemberAccountConcept.ChargeReceivable) > 0m) nota = "Tiene cargos por cobrar: se cobran o se condonan antes.";
            else if (saldo <= 0m) nota = "Sin saldo.";
            r.Add(new CuentaDelFallecido(c, saldo, nota is null, nota));
        }
        return r;
    }

    /// <summary>
    /// Los beneficiarios de cada cuenta vigentes en la fecha de la defunción (FR-034): el conjunto más reciente que regía ese día, con su porcentaje
    /// (fracción, suma 1).
    /// </summary>
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<(int PersonaId, decimal Porcentaje)>>> BeneficiariosAsync(IReadOnlyList<int> cuentas,
        DateOnly defuncion, CancellationToken ct)
    {
        var ids = cuentas.ToArray();
        var filas = await db.MemberAccountParties.AsNoTracking()
            .Where(p => ids.Contains(p.MemberAccountId) && p.Role == MemberAccountPartyRole.Beneficiary && !p.IsDeleted && p.ValidFrom <= defuncion
                        && (p.ValidTo == null || p.ValidTo >= defuncion))
            .Select(p => new { p.MemberAccountId, p.PersonId, p.Share, p.BeneficiarySetNumber }).ToListAsync(ct);
        var r = new Dictionary<int, IReadOnlyList<(int, decimal)>>();
        foreach (var g in filas.GroupBy(f => f.MemberAccountId))
        {
            var conjunto = g.Max(f => f.BeneficiarySetNumber ?? 0);
            r[g.Key] = g.Where(f => (f.BeneficiarySetNumber ?? 0) == conjunto).OrderBy(f => f.PersonId).Select(f => (f.PersonId, f.Share ?? 0m)).ToList();
        }
        return r;
    }

    /// <summary>Los soportes que pide la entrega sin juicio de sucesión (<c>Fallecimiento.SoportesDeEntregaSinSucesion</c>, JSON); vacío sin valor.</summary>
    public async Task<IReadOnlyList<string>> SoportesSinSucesionAsync(DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.FallecimientoSoportesDeEntregaSinSucesion, fecha, ct: ct);
        var texto = leido.IsSuccess ? leido.Value.Texto : string.Empty;
        if (string.IsNullOrWhiteSpace(texto)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(texto)?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // --------------------------------------------------------------------------------------------------- vista previa --

    /// <summary>
    /// La vista previa de la entrega por <paramref name="ruta"/> a <paramref name="hoy"/> (sin escribir): las cuentas, el reparto por cuenta y persona, el tope
    /// (sin juicio de sucesión), los soportes que pide y la huella. Las personas y porcentajes de <paramref name="destinatarios"/> rigen fuera de
    /// <c>Beneficiaries</c>; en <c>Beneficiaries</c> sólo dicen el medio de cada beneficiario (sin él, giro a su cuenta de depósito registrada).
    /// </summary>
    public async Task<Result<VistaPreviaDeEntrega>> VistaPreviaAsync(DeceasedHolderCase caso, DeliveryRoute ruta, IReadOnlyList<DestinatarioDeEntrega> destinatarios,
        DateOnly hoy, CancellationToken ct, IReadOnlyList<CuentaDelFallecido>? cuentas = null)
    {
        cuentas ??= await CuentasAsync(caso.PersonId, hoy, seguir: false, ct);
        var entregables = cuentas.Where(c => c.Entregable).ToList();
        var avisos = cuentas.Where(c => !c.Entregable && c.Nota is not null).Select(c => $"{c.Cuenta.Number}: {c.Nota}").ToList();
        if (await db.MemberAccounts.AsNoTracking().AnyAsync(a => a.HolderPersonId == caso.PersonId && a.Nature == LineNature.Contribution
                                                                 && a.Status != MemberAccountStatus.Closed && !a.IsDeleted, ct))
            avisos.Add("Los aportes se devuelven por el retiro del asociado con causa fallecimiento (no por esta entrega).");
        if (entregables.Count == 0) return Result.Failure<VistaPreviaDeEntrega>(ErroresDeFallecimiento.NadaQueEntregar());
        var beneficiarios = await BeneficiariosAsync(entregables.Select(c => c.Cuenta.Id).ToList(), caso.DeathDate, ct);
        var porciones = new List<PorcionDeEntrega>();
        if (ruta == DeliveryRoute.Beneficiaries)
        {
            var sin = entregables.Where(c => !beneficiarios.TryGetValue(c.Cuenta.Id, out var b) || b.Count == 0).Select(c => c.Cuenta.Number).ToList();
            if (sin.Count > 0) return Result.Failure<VistaPreviaDeEntrega>(ErroresDeFallecimiento.SinBeneficiarios(sin));
            foreach (var c in entregables) porciones.AddRange(Repartir(c.Cuenta.Id, c.Saldo, beneficiarios[c.Cuenta.Id]));
        }
        else
        {
            var conPorcentaje = destinatarios.Where(d => d.Porcentaje is > 0m).ToList();
            if (conPorcentaje.Count == 0 || conPorcentaje.Sum(d => d.Porcentaje!.Value) != 1m)
                return Result.Failure<VistaPreviaDeEntrega>(ErroresDeFallecimiento.Invalido("recipients",
                    "Sin beneficiarios, la entrega nombra a las personas que reciben con su porcentaje (fracción): suman exactamente 100 %."));
            var partes = conPorcentaje.Select(d => (d.PersonaId, d.Porcentaje!.Value)).ToList();
            foreach (var c in entregables) porciones.AddRange(Repartir(c.Cuenta.Id, c.Saldo, partes));
        }
        var total = entregables.Sum(c => c.Saldo);
        ValorNacionalVigente? tope = null;
        IReadOnlyList<string> soportes = [];
        if (ruta == DeliveryRoute.WithoutProbate)
        {
            var leido = await valoresNacionales.LeerAsync(CodigosDeValorNacional.TopeEntregaSinSucesion, string.Empty, hoy, null, ct);
            if (leido.IsFailure) return Result.Failure<VistaPreviaDeEntrega>(leido.Error);
            tope = leido.Value;
            if (tope.Valor is { } t && total > t) return Result.Failure<VistaPreviaDeEntrega>(ErroresDeFallecimiento.TopeSuperado(total, t, tope.Fuente));
            soportes = await SoportesSinSucesionAsync(hoy, ct);
            if (soportes.Count == 0) avisos.Add("La cooperativa no ha fijado los soportes de la entrega sin juicio de sucesión (Fallecimiento.SoportesDeEntregaSinSucesion).");
        }
        else if (ruta == DeliveryRoute.Probate) soportes = ["Sentencia o escritura de la sucesión"];

        var huella = new HuellaDeCartera().Identificador("case", caso.PublicId).Texto("route", ruta.ToString()).Fecha("date", hoy)
            .Textos("accounts", entregables.Select(c => $"{c.Cuenta.PublicId:N}|{c.Saldo.ToString(CultureInfo.InvariantCulture)}"))
            .Textos("portions", porciones.Select(p => $"{p.CuentaId}|{p.PersonaId}|{p.Valor.ToString(CultureInfo.InvariantCulture)}"))
            .Textos("recipients", destinatarios.OrderBy(d => d.PersonaId).Select(d =>
                $"{d.PersonaPublicId:N}|{d.Porcentaje?.ToString(CultureInfo.InvariantCulture)}|{d.Forma}|{d.CuentaDestinoId}|{d.BancoPublicId:N}|{d.NumeroDeCuenta}"))
            .Calcular();
        return Result.Success(new VistaPreviaDeEntrega(ruta, hoy, cuentas, porciones, beneficiarios, total, tope, soportes, avisos, huella));
    }

    /// <summary>
    /// El reparto de un saldo por porcentajes al centavo por el mayor residuo (la suma es exactamente el saldo; a igual residuo, el porcentaje mayor y luego
    /// la persona).
    /// </summary>
    public static IReadOnlyList<PorcionDeEntrega> Repartir(int cuentaId, decimal saldo, IReadOnlyList<(int PersonaId, decimal Porcentaje)> partes)
    {
        var suma = partes.Sum(p => p.Porcentaje);
        if (suma <= 0m || saldo <= 0m) return [];
        var brutos = partes.Select(p => (p.PersonaId, p.Porcentaje, Exacto: saldo * p.Porcentaje / suma)).ToList();
        var piso = brutos.Select(b => (b.PersonaId, b.Porcentaje, Valor: Math.Floor(b.Exacto * 100m) / 100m, Residuo: b.Exacto * 100m - Math.Floor(b.Exacto * 100m)))
            .ToList();
        var faltan = (int)Math.Round((saldo - piso.Sum(p => p.Valor)) * 100m);
        var orden = piso.Select((p, i) => (p, i)).OrderByDescending(x => x.p.Residuo).ThenByDescending(x => x.p.Porcentaje).ThenBy(x => x.p.PersonaId)
            .Select(x => x.i).Take(Math.Max(0, faltan)).ToHashSet();
        return piso.Select((p, i) => new PorcionDeEntrega(cuentaId, p.PersonaId, p.Porcentaje / suma, p.Valor + (orden.Contains(i) ? Centavo : 0m)))
            .Where(p => p.Valor > 0m).ToList();
    }

    // ------------------------------------------------------------------------------------------------------- entregar --

    /// <summary>
    /// Entrega lo aprobado (ver la clase): por cada cuenta entregable, la liquidación a la fecha, el reparto con el saldo del día y una operación por
    /// destinatario; después, la cuenta cancelada (sin giros) o en cierre (con giros) y el caso aprobado (o entregado si no quedó ningún giro). Las cuentas, la
    /// persona y los destinos ya están bloqueados; guarda. Devuelve lo entregado.
    /// </summary>
    public async Task<Result<decimal>> EntregarAsync(DeceasedHolderCase caso, PlanDeEntrega plan, DateOnly hoy, string motivo, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var texto = DeceasedHolderCase.Recortar(string.IsNullOrWhiteSpace(motivo) ? $"Entrega por fallecimiento del caso {caso.Number}" : motivo);
        var cuentas = await CuentasAsync(caso.PersonId, hoy, seguir: true, ct);
        foreach (var c in cuentas.Where(c => c.Entregable))
        {
            var liquidada = await liquidacion.LiquidarAsync(c.Cuenta, hoy, texto, ct);
            if (liquidada.IsFailure) return Result.Failure<decimal>(liquidada.Error);
        }
        // El saldo del día (con los intereses que la liquidación acaba de abonar).
        cuentas = await CuentasAsync(caso.PersonId, hoy, seguir: true, ct);
        var entregables = cuentas.Where(c => c.Entregable).ToList();
        if (entregables.Count == 0) return Result.Failure<decimal>(ErroresDeFallecimiento.NadaQueEntregar());
        var beneficiarios = await BeneficiariosAsync(entregables.Select(c => c.Cuenta.Id).ToList(), caso.DeathDate, ct);
        var porDestinatario = plan.Destinatarios.ToDictionary(d => d.PersonaId);
        var entregado = 0m;
        var conGiro = new HashSet<int>();
        foreach (var c in entregables)
        {
            var cuenta = c.Cuenta;
            var partes = plan.Ruta == DeliveryRoute.Beneficiaries
                ? beneficiarios.GetValueOrDefault(cuenta.Id) ?? []
                : plan.Destinatarios.Where(d => d.Porcentaje is > 0m).Select(d => (d.PersonaId, d.Porcentaje!.Value)).ToList();
            if (partes.Count == 0) return Result.Failure<decimal>(ErroresDeFallecimiento.SinBeneficiarios([cuenta.Number]));
            var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
            if (saldos.GetValueOrDefault(MemberAccountConcept.ChargeReceivable) > 0m) return Result.Failure<decimal>(ErroresDeFallecimiento.CargosPendientes(cuenta.Number));
            var bolsas = ConceptosQueSeEntregan.Select(k => (Concepto: k, Valor: saldos.GetValueOrDefault(k))).Where(b => b.Valor != 0m).ToList();
            if (bolsas.Any(b => RegistroDeCuentas.Decimales(b.Valor) > 2 || b.Valor < 0m))
                return Result.Failure<decimal>(ErroresDeFallecimiento.Invalido("accounts", $"La cuenta {cuenta.Number} tiene causación sin liquidar: se liquida antes."));
            var saldo = bolsas.Sum(b => b.Valor);
            var hecho = await emision.HechoDeCuentaAsync(cuenta, hoy, ct);
            if (hecho.IsFailure) return Result.Failure<decimal>(hecho.Error);
            var control = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == cuenta.Id, ct);
            foreach (var porcion in Repartir(cuenta.Id, saldo, partes))
            {
                // Lo de cada persona sale de las bolsas en orden (capital, interés por pagar, bonificación).
                var valores = new List<(MemberAccountConcept Concepto, decimal Valor)>();
                var falta = porcion.Valor;
                for (var i = 0; i < bolsas.Count && falta > 0m; i++)
                {
                    var toma = Math.Min(falta, bolsas[i].Valor);
                    if (toma <= 0m) continue;
                    valores.Add((bolsas[i].Concepto, toma));
                    bolsas[i] = (bolsas[i].Concepto, bolsas[i].Valor - toma);
                    falta -= toma;
                }
                var destinatario = porDestinatario.GetValueOrDefault(porcion.PersonaId)
                                   ?? new DestinatarioDeEntrega(porcion.PersonaId, Guid.Empty, null, FormaDeEntrega.DisbursementOrder, null, null, null, null);
                var hecha = await EntregarPorcionAsync(caso, cuenta, control, hecho.Value, porcion, valores, destinatario, hoy, texto, actor.Kind, actor.UserId,
                    actor.CentralUserId, ahora, ct);
                if (hecha.IsFailure) return Result.Failure<decimal>(hecha.Error);
                if (hecha.Value) conGiro.Add(cuenta.Id);
                entregado += porcion.Valor;
            }
            if (conGiro.Contains(cuenta.Id)) cuenta.PedirCierre(ahora);
            else
            {
                cuenta.Cerrar(hoy, actor.UserId, $"Entregada por el fallecimiento del titular (caso {caso.Number})", ahora);
                await LevantarBloqueosAsync(cuenta.Id, actor.UserId, ahora, $"Cuenta entregada (caso {caso.Number})", ct);
            }
            await db.SaveChangesAsync(ct);
        }
        caso.Aprobar(actor.UserId ?? 0, ahora, entregado);
        if (conGiro.Count == 0 && !await QuedanCuentasAsync(caso, ct)) caso.Entregar(ahora);
        await db.SaveChangesAsync(ct);
        return Result.Success(entregado);
    }

    /// <summary>Una porción: la operación <c>DeceasedDelivery</c> con su giro o su traslado. Verdadero si quedó un giro en curso.</summary>
    private async Task<Result<bool>> EntregarPorcionAsync(DeceasedHolderCase caso, MemberAccount cuenta, Domain.Entities.Lending.Deposits.Projections.MemberAccountControl control,
        HechoDeCuenta hecho, PorcionDeEntrega porcion, IReadOnlyList<(MemberAccountConcept Concepto, decimal Valor)> valores, DestinatarioDeEntrega destinatario,
        DateOnly hoy, string texto, Domain.Enums.Integration.ActorKind actorKind, int? usuario, Guid? central, DateTime ahora, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == porcion.PersonaId, ct);
        MemberAccount? destino = null;
        if (destinatario.Forma == FormaDeEntrega.TransferToAccount)
        {
            destino = destinatario.CuentaDestinoId is { } d ? await db.MemberAccounts.FirstOrDefaultAsync(a => a.Id == d, ct) : null;
            if (destino is null) return Result.Failure<bool>(ErroresDeFallecimiento.Invalido("targetAccountPublicId",
                $"La entrega a {ReferenciasDeGobierno.Nombre(persona)} por traslado nombra una cuenta suya."));
        }
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.DeceasedDelivery, PrefijoEntrega, ct);
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            CounterpartMemberAccountId = destino?.Id,
            Kind = MemberAccountOperationKind.DeceasedDelivery,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = hoy,
            AccountingDate = hecho.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = porcion.Valor,
            ActorKind = actorKind,
            ActorUserId = usuario,
            OriginUserCentralId = central,
            DeceasedHolderCaseId = caso.Id,
            Reason = texto,
            Payload = $"{{\"casePublicId\":\"{caso.PublicId:N}\",\"route\":\"{caso.Route}\",\"recipient\":\"{persona.PublicId:N}\",\"form\":\"{destinatario.Forma}\"}}",
            InputsSha256 = new HuellaDeCartera().Identificador("case", caso.PublicId).Identificador("account", cuenta.PublicId).Identificador("recipient", persona.PublicId)
                .Decimal("amount", porcion.Valor, 2).Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
        });
        await db.SaveChangesAsync(ct);

        // Respuesta 97 (E253): el GMF de la porción, retenido de ella; el destinatario recibe el neto.
        GmfCalculado? gmf = null;
        Guid? exenta = null;
        if (movimientos is not null)
        {
            var leida = await movimientos.LeerAsync(cuenta, hoy, ct);
            var calculado = await movimientos.GmfAsync(leida, HechosGeneradoresDeGmf.EntregaPorFallecimiento, porcion.Valor, hoy, ct);
            if (calculado.IsFailure) return Result.Failure<bool>(calculado.Error);
            gmf = calculado.Value;
            exenta = leida.ExentaDeGmf ? cuenta.PublicId : null;
        }
        var valorGmf = GravamenMovimientosFinancieros.SeCobra(gmf) ? gmf.Valor : 0m;
        var repartido = DescontarGmf(valores, valorGmf);
        if (repartido is null)
            return Result.Failure<bool>(ErroresDeFallecimiento.Invalido("gmf", $"El GMF ({valorGmf:N2}) supera lo que se entrega a la persona en la cuenta {cuenta.Number}."));
        var (netos, deGmf) = repartido.Value;
        var neto = porcion.Valor - valorGmf;
        var gmfAEmitir = valorGmf > 0m ? new GmfAEmitir(gmf!, exenta, cuenta.DepositClass?.ToString()) : null;

        if (destino is null)
        {
            var banco = await ordenes.BeneficiarioAsync(persona.Id, destinatario.BancoPublicId, destinatario.TipoDeCuenta, destinatario.NumeroDeCuenta, ct);
            if (banco.IsFailure) return Result.Failure<bool>(banco.Error);
            var b = banco.Value;
            var orden = AccountPayoutOrder.Emitir(cuenta.Id, operacion.Id, PayoutPurpose.DeceasedDelivery, persona.Id, persona.IdType ?? string.Empty,
                persona.TaxId ?? string.Empty, ReferenciasDeGobierno.Nombre(persona), b.BancoId, b.TipoDeCuenta, b.NumeroDeCuenta, neto, hoy.AddDays(1), ahora,
                usuario);
            orden.DeceasedHolderCaseId = caso.Id;
            await numerador.NumerarOrdenDeCuentaAsync(orden, Disbursements.TramoDeDesembolso.PrefijoDeOrden, ct);
            db.AccountPayoutOrders.Add(orden);
            await db.SaveChangesAsync(ct);
            var payee = new PayeeV1
            {
                PersonPublicId = persona.PublicId, TaxIdType = persona.IdType ?? string.Empty, TaxId = persona.TaxId ?? string.Empty,
                Name = ReferenciasDeGobierno.Nombre(persona), BankCode = b.CodigoDeTransferencia, BankAccountNumber = b.NumeroDeCuenta,
                BankAccountType = b.TipoDeCuenta?.ToString(CultureInfo.InvariantCulture),
            };
            var montos = netos.Select(v => new DepositConceptAmountV1
            {
                Concept = v.Concepto.ToString(), Amount = -v.Valor, ReasonCode = nameof(PayoutPurpose.DeceasedDelivery),
            }).ToList();
            var unidad = await emision.DevolucionOrdenadaAsync(new DevolucionAEmitir(hecho, operacion, montos, nameof(PayoutPurpose.DeceasedDelivery), orden, payee,
                persona.PublicId, nameof(PayoutPurpose.DeceasedDelivery), $"Entrega por fallecimiento, caso {caso.Number}, cuenta {cuenta.Number}")
            {
                Clase = EmisionDeCartera.ClaseEntregaPorFallecimiento,
                Gmf = gmfAEmitir,
            }, ct);
            if (unidad.IsFailure) return Result.Failure<bool>(unidad.Error);
            var mensaje = unidad.Value.De(DevolucionOrdenadaV1.Type);
            var filasDelGiro = netos.Select(v => Fila(cuenta, operacion, MemberAccountTransactionKind.Refund, v.Concepto, -v.Valor, hoy,
                hecho.FechaContable, mensaje)).ToList();
            filasDelGiro.AddRange(FilasDelGmf(cuenta, operacion, deGmf, hoy, hecho.FechaContable, unidad.Value.De(GmfRetenidoV1.Type)));
            registro.RegistrarTransacciones(filasDelGiro);
            RegistrarUsoDelGmf(cuenta, operacion, gmf, hoy);
            await db.SaveChangesAsync(ct);
            return Result.Success(true);
        }

        var hechoDestino = await emision.HechoDeCuentaAsync(destino, hoy, ct);
        if (hechoDestino.IsFailure) return Result.Failure<bool>(hechoDestino.Error);
        var deOrigen = netos.Select(v => new DepositConceptAmountV1 { Concept = v.Concepto.ToString(), Amount = -v.Valor }).ToList();
        var aDestino = new List<DepositConceptAmountV1> { new() { Concept = nameof(MemberAccountConcept.Principal), Amount = neto } };
        var traslado = await emision.TrasladoConGmfAsync(new TrasladoAEmitir(hecho, hechoDestino.Value, operacion, deOrigen, aDestino), gmfAEmitir,
            Domain.Enums.Integration.PrevalidationOutcome.NotApplicable, ct);
        if (traslado.IsFailure) return Result.Failure<bool>(traslado.Error);
        var msg = traslado.Value.De(TrasladoEntreCuentasAplicadoV1.Type);
        registro.RegistrarTransacciones([
            .. netos.Select(v => Fila(cuenta, operacion, MemberAccountTransactionKind.TransferOut, v.Concepto, -v.Valor, hoy, hecho.FechaContable, msg)),
            Fila(destino, operacion, MemberAccountTransactionKind.TransferIn, MemberAccountConcept.Principal, neto, hoy, hecho.FechaContable, msg),
            .. FilasDelGmf(cuenta, operacion, deGmf, hoy, hecho.FechaContable, traslado.Value.De(GmfRetenidoV1.Type)),
        ]);
        RegistrarUsoDelGmf(cuenta, operacion, gmf, hoy);
        var controlDestino = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == destino.Id, ct);
        RegistroDeCuentas.MarcarActividadDelTitular(controlDestino, hoy);
        await db.SaveChangesAsync(ct);
        if (await ReactivacionDeCuenta.ReactivarAsync(reactivacion, destino, hoy, ahora, ct) is { IsFailure: true } noReactivada)
            return Result.Failure<bool>(noReactivada.Error);
        return Result.Success(false);
    }

    /// <summary>
    /// Respuesta 97 (E253): lo que sale de cada concepto de la porción una vez retenido el GMF. El GMF sale del capital y, si éste no alcanza, del interés por
    /// pagar (la pareja <c>GmfCharged</c> sólo admite esos dos); devuelve los conceptos con su valor neto (sin los que quedan en cero) y lo que salió como GMF
    /// de cada uno; nulo si la porción no alcanza a cubrir el GMF.
    /// </summary>
    public static (IReadOnlyList<(MemberAccountConcept Concepto, decimal Valor)> Netos, IReadOnlyList<(MemberAccountConcept Concepto, decimal Valor)> DeGmf)? DescontarGmf(
        IReadOnlyList<(MemberAccountConcept Concepto, decimal Valor)> valores, decimal gmf)
    {
        if (gmf <= 0m) return (valores, []);
        var netos = valores.ToList();
        var deGmf = new List<(MemberAccountConcept Concepto, decimal Valor)>();
        var falta = gmf;
        foreach (var concepto in new[] { MemberAccountConcept.Principal, MemberAccountConcept.InterestPayable })
        {
            var i = netos.FindIndex(v => v.Concepto == concepto);
            if (i < 0 || falta <= 0m) continue;
            var toma = Math.Min(falta, netos[i].Valor);
            if (toma <= 0m) continue;
            netos[i] = (concepto, netos[i].Valor - toma);
            deGmf.Add((concepto, toma));
            falta -= toma;
        }
        return falta > 0m ? null : (netos.Where(v => v.Valor > 0m).ToList(), deGmf);
    }

    /// <summary>La pareja <c>GmfCharged</c> de la porción (−capital o interés, +GMF). Sin GMF, nada.</summary>
    private static IEnumerable<MemberAccountTransaction> FilasDelGmf(MemberAccount cuenta, MemberAccountOperation op, IReadOnlyList<(MemberAccountConcept Concepto, decimal Valor)> deGmf,
        DateOnly fecha, DateOnly contable, Guid? mensaje)
    {
        foreach (var (concepto, valor) in deGmf)
            yield return Fila(cuenta, op, MemberAccountTransactionKind.GmfCharged, concepto, -valor, fecha, contable, mensaje);
        if (deGmf.Count > 0)
            yield return Fila(cuenta, op, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Gmf, deGmf.Sum(x => x.Valor), fecha, contable, mensaje);
    }

    private void RegistrarUsoDelGmf(MemberAccount cuenta, MemberAccountOperation op, GmfCalculado? gmf, DateOnly fecha)
    {
        if (gmf is not null)
            registro.RegistrarUsoDeGmf(cuenta.HolderPersonId, op, cuenta.Id, fecha, gmf.HechoGenerador, gmf.Base, gmf.ExentoAplicado, gmf.BaseGravada, gmf.Valor,
                gmf.Politica, gmf.Tarifa);
    }

    /// <summary>¿Le quedan al caso cuentas de captación vivas del titular por cancelar (en cierre o con saldo)?</summary>
    public async Task<bool> QuedanCuentasAsync(DeceasedHolderCase caso, CancellationToken ct) =>
        await db.AccountHolds.AsNoTracking().Where(h => h.DeceasedHolderCaseId == caso.Id && h.ReleasedAt == null && !h.IsDeleted)
            .Join(db.MemberAccounts.AsNoTracking(), h => h.MemberAccountId, a => a.Id, (h, a) => a)
            .AnyAsync(a => a.Nature == LineNature.Deposit && a.Status != MemberAccountStatus.Closed, ct);

    /// <summary>Levanta los bloqueos del fallecimiento de una cuenta (entregada o caso anulado). No guarda.</summary>
    public async Task LevantarBloqueosAsync(int cuentaId, int? usuario, DateTime ahora, string motivo, CancellationToken ct)
    {
        foreach (var h in await db.AccountHolds.Where(h => h.MemberAccountId == cuentaId && h.Reason == HoldReason.Deceased && h.ReleasedAt == null && !h.IsDeleted)
                     .ToListAsync(ct))
            h.Levantar(ahora, usuario, motivo);
    }

    /// <summary>
    /// El giro de una entrega que Tesorería informó como girado (feature 014, US14, T210): la cuenta en cierre sin saldo y sin otros giros se cancela y suelta su
    /// bloqueo; con todas sus cuentas canceladas y sin giros en curso, el caso queda entregado. Bajo el cerrojo de quien consume el resultado; no guarda.
    /// </summary>
    public static async Task AlGirarAsync(IApplicationDbContext db, AccountPayoutOrder orden, DateOnly hoy, DateTime ahora, int? usuario, CancellationToken ct)
    {
        if (orden.Purpose != PayoutPurpose.DeceasedDelivery || orden.DeceasedHolderCaseId is not { } casoId) return;
        var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == orden.MemberAccountId, ct);
        var otras = await db.AccountPayoutOrders.AsNoTracking().AnyAsync(o => o.MemberAccountId == cuenta.Id && o.Id != orden.Id && !o.IsDeleted
                                                                              && o.Status == DisbursementOrderStatus.Issued, ct);
        var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
        var caso = await db.DeceasedHolderCases.FirstAsync(c => c.Id == casoId, ct);
        if (!otras && cuenta.Status == MemberAccountStatus.Closing && LectorDeCuentas.SaldoDelTitular(cuenta.Nature, saldos) == 0m)
        {
            cuenta.Cerrar(hoy, usuario, $"Entregada por el fallecimiento del titular (caso {caso.Number})", ahora);
            foreach (var h in await db.AccountHolds.Where(h => h.MemberAccountId == cuenta.Id && h.Reason == HoldReason.Deceased && h.ReleasedAt == null && !h.IsDeleted)
                         .ToListAsync(ct))
                h.Levantar(ahora, usuario, $"Cuenta entregada (caso {caso.Number})");
        }
        var vivas = await db.AccountPayoutOrders.AsNoTracking().AnyAsync(o => o.DeceasedHolderCaseId == casoId && o.Id != orden.Id && !o.IsDeleted
                                                                              && o.Status == DisbursementOrderStatus.Issued, ct);
        if (vivas || caso.Status != DeceasedCaseStatus.Approved) return;
        var abiertas = await db.MemberAccounts.Where(a => a.HolderPersonId == caso.PersonId && a.Nature == LineNature.Deposit && a.Status == MemberAccountStatus.Closing
                                                         && !a.IsDeleted && a.Id != cuenta.Id).AnyAsync(ct);
        if (!abiertas && (cuenta.Status == MemberAccountStatus.Closed)) caso.Entregar(ahora);
    }

    private static MemberAccountTransaction Fila(MemberAccount cuenta, MemberAccountOperation op, MemberAccountTransactionKind clase, MemberAccountConcept concepto,
        decimal valor, DateOnly fecha, DateOnly contable, Guid? mensaje) => new()
    {
        MemberAccountId = cuenta.Id, Operation = op, OperationId = op.Id, Kind = clase, Concept = concepto, Amount = valor, ValueDate = fecha,
        AccountingDate = contable, RegisteredAt = op.RegisteredAt, MessagePublicId = mensaje,
    };

    /// <summary>El plan sellado (<c>DeliveryPlanJson</c>).</summary>
    public static string Serializar(PlanDeEntrega plan) => JsonSerializer.Serialize(plan, Json);

    public static PlanDeEntrega? Leer(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PlanDeEntrega>(json, Json);
}
