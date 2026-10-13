using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// El emisor de las cuentas contingentes de Cartera (feature 013, US7, T0659; contabilidad.md §19, mensajes.md §10.24; A3, C15, C91, C92, C94):
/// <c>CreditoAprobadoSinDesembolsar</c> (9115) y <c>CupoRotativoOtorgado</c> (913005) sobre <see cref="EmisionDeCartera"/>; <c>GarantiaConstituida</c>
/// (9110) ya la emiten las operaciones de garantías (<c>GarantiasDeCartera</c>, <see cref="EmisionDeCartera.GarantiaConstituidaAsync"/>).
///
/// <para>
/// <b>Por saldo, no por evento.</b> Cada comando que puede mover el contingente (la decisión individual o de acta sobre una solicitud o un cupo,
/// el desistimiento o la anulación, la asignación y el fin de un cupo) llama a <see cref="SolicitudAsync"/> o <see cref="CupoAsync"/> antes de su
/// guardado; el ciclo diario llama a <see cref="VencimientosAsync"/> para lo que vence con el tiempo (la aprobación que pasó su vigencia, FR-042;
/// el cupo cuya versión en cero empezó). Cada llamada compara el saldo contingente que debería tener el sujeto a la fecha con el
/// <c>balanceAfter</c> del último hecho emitido para él y emite <b>sólo la diferencia</b>, con signo y su movimiento. Es idempotente: repetir la
/// corrida o el comando no emite nada (T5).
/// </para>
/// <list type="bullet">
/// <item>Aprobación: el aprobado vigente menos lo desembolsado; sube al aprobar (<c>Approved</c>) y baja al desembolsar cada tramo (lo emite el
/// desembolso, <see cref="EmisionDeCartera.BajaDeAprobadoSinDesembolsarAsync"/>), al vencer (<c>Expired</c>), al desistir (<c>Withdrawn</c>) o al anular
/// (<c>Voided</c>).</item>
/// <item>Cupo rotativo: el monto de la versión firme vigente; usarlo o pagarlo no lo mueve (C92). <c>Granted</c>, <c>Increased</c>, <c>Reduced</c> o
/// <c>Expired</c> (versión en cero).</item>
/// </list>
/// Origen: la solicitud (clase <c>CreditApplication</c>) o el cupo (clase <c>CreditLimit</c>, con su primera versión como identidad); la primera
/// emisión de un origen lleva <c>Confirmation</c> y las siguientes <c>Confirmation:{n}</c> con un identificador propio (la bandeja admite un solo
/// mensaje por origen, tipo y clave). Sin la fila del tipo en <c>LND_FactTypes</c> (A3 rechazado o sin sembrar) no emite. (nuevo)
/// </summary>
public sealed class MovimientoContingenteDeCartera(IApplicationDbContext db, EmisionDeCartera emision, FechaContable fechaContable, IDateTimeService reloj)
{
    public const string ClaseSolicitud = "CreditApplication";
    public const string ClaseCupo = "CreditLimit";

    /// <summary>Los movimientos de §10.24.</summary>
    public static class Movimientos
    {
        public const string Aprobada = "Approved";
        public const string Desembolsada = "Disbursed";
        public const string Vencida = "Expired";
        public const string Desistida = "Withdrawn";
        public const string Anulada = "Voided";
        public const string Otorgado = "Granted";
        public const string Ampliado = "Increased";
        public const string Reducido = "Reduced";
    }

    /// <summary>Los tres tipos contingentes, que admiten proceso (C15) junto con los de T11.</summary>
    public static IReadOnlyList<string> Tipos { get; } = [CreditoAprobadoSinDesembolsarV1.Type, CupoRotativoOtorgadoV1.Type, GarantiaConstituidaV1.Type];

    /// <summary>
    /// Pone al día el contingente de la solicitud <paramref name="solicitud"/> (su estado en la unidad de trabajo) a <paramref name="fecha"/> (hoy si
    /// es nula). Sin guardar; nulo si no hay nada que emitir.
    /// </summary>
    public async Task<Result<IntegrationMessage?>> SolicitudAsync(CreditApplication solicitud, DateOnly? fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (!await HayTipoAsync(CreditoAprobadoSinDesembolsarV1.Type, ct)) return Result.Success<IntegrationMessage?>(null);
        var dia = fecha ?? reloj.HoyLocal;

        var desembolsado = await (from o in db.LoanOperations.AsNoTracking()
                                  join l in db.Loans.AsNoTracking() on o.LoanId equals l.Id
                                  where l.ApplicationId == solicitud.Id && o.Kind == LoanOperationKind.Disbursement && o.ReversedByOperationId == null
                                  select o.Amount).SumAsync(ct);
        var aprobado = solicitud.ApprovedAmount ?? 0m;
        var vigente = solicitud.Status == CreditApplicationStatus.Approved && !solicitud.AprobacionVencida(dia)
            || solicitud.Status == CreditApplicationStatus.Disbursed;
        var esperado = vigente ? Math.Max(0m, aprobado - desembolsado) : 0m;
        var anterior = await UltimoSaldoAsync(CreditoAprobadoSinDesembolsarV1.Type, solicitud.PublicId, ct);
        var variacion = esperado - anterior;
        if (variacion == 0m) return Result.Success<IntegrationMessage?>(null);

        var movimiento = variacion > 0m ? Movimientos.Aprobada
            : solicitud.Status == CreditApplicationStatus.Cancelled
                ? solicitud.CancelReason?.StartsWith("Desistimiento", StringComparison.Ordinal) == true ? Movimientos.Desistida : Movimientos.Anulada
            : Movimientos.Vencida;
        var sucursal = await emision.SucursalContableAsync(solicitud.BranchId, ct);
        if (sucursal.IsFailure) return Result.Failure<IntegrationMessage?>(sucursal.Error);
        var deudor = await PersonaAsync(solicitud.ApplicantPersonId, ct);
        var linea = await LineaAsync(solicitud.CreditLineId, ct);
        var numero = solicitud.Number ?? "SOL-" + solicitud.PublicId.ToString("N")[..12].ToUpperInvariant();
        var contenido = new CreditoAprobadoSinDesembolsarV1
        {
            DebtorPersonPublicId = deudor,
            Subject = new ContingentSubjectV1 { Kind = ClaseSolicitud, PublicId = solicitud.PublicId, Number = numero },
            ValueDate = dia,
            RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(reloj.UtcNow, DateTimeKind.Utc)),
            CreditLineCode = linea,
            Movement = movimiento,
            Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = variacion, ReasonCode = "ApprovedNotDisbursed" }],
            BalanceAfter = esperado,
        };
        return await EmitirAsync(contenido, CreditoAprobadoSinDesembolsarV1.Type, ClaseSolicitud, solicitud.PublicId, numero, dia, sucursal.Value, deudor, ct);
    }

    /// <summary>
    /// Pone al día el contingente del cupo rotativo de <paramref name="version"/> (cualquier versión del cupo) a <paramref name="fecha"/> (hoy si es
    /// nula). Los cupos consolidado y por línea no son contingentes. Sin guardar; nulo si no hay nada que emitir.
    /// </summary>
    public async Task<Result<IntegrationMessage?>> CupoAsync(CreditLimit version, DateOnly? fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Kind != CreditLimitKind.Revolving || !await HayTipoAsync(CupoRotativoOtorgadoV1.Type, ct)) return Result.Success<IntegrationMessage?>(null);
        var dia = fecha ?? reloj.HoyLocal;

        var delCupo = (await VersionesDeCupo.DeAsync(db, [version.PersonId], ct)).Where(v => VersionesDeCupo.MismoCupo(v, version))
            .OrderBy(v => v.VersionNumber).ToList();
        var identidad = delCupo.First();
        var esperado = VersionesDeCupo.Vigente(delCupo, dia)?.Amount ?? 0m;
        var anterior = await UltimoSaldoAsync(CupoRotativoOtorgadoV1.Type, identidad.PublicId, ct);
        var variacion = esperado - anterior;
        if (variacion == 0m) return Result.Success<IntegrationMessage?>(null);

        var movimiento = variacion > 0m ? anterior == 0m ? Movimientos.Otorgado : Movimientos.Ampliado
            : esperado == 0m ? Movimientos.Vencida : Movimientos.Reducido;
        var sucursal = await emision.SucursalPrincipalAsync(ct);
        var deudor = await PersonaAsync(version.PersonId, ct);
        var linea = version.CreditLineId is { } l ? await LineaAsync(l, ct) : string.Empty;
        var numero = "CUP-" + identidad.PublicId.ToString("N")[..12].ToUpperInvariant();
        var contenido = new CupoRotativoOtorgadoV1
        {
            DebtorPersonPublicId = deudor,
            Subject = new ContingentSubjectV1 { Kind = ClaseCupo, PublicId = identidad.PublicId, Number = numero },
            ValueDate = dia,
            RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(reloj.UtcNow, DateTimeKind.Utc)),
            CreditLineCode = linea,
            Movement = movimiento,
            Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = variacion, ReasonCode = "RevolvingLimit" }],
            BalanceAfter = esperado,
        };
        return await EmitirAsync(contenido, CupoRotativoOtorgadoV1.Type, ClaseCupo, identidad.PublicId, numero, dia, sucursal, deudor, ct);
    }

    /// <summary>
    /// Lo que vence con el tiempo, para el ciclo diario del día <paramref name="dia"/> (actúa el proceso): las aprobaciones vigentes que pasaron
    /// su vigencia y los cupos rotativos cuyo monto vigente cambió (una versión que empezó, incluida la versión en cero del fin). Devuelve cuántos
    /// hechos emitió. Sin guardar.
    /// </summary>
    public async Task<Result<int>> VencimientosAsync(DateOnly dia, CancellationToken ct)
    {
        var emitidos = 0;
        if (await HayTipoAsync(CreditoAprobadoSinDesembolsarV1.Type, ct))
        {
            var vencidas = await db.CreditApplications
                .Where(s => s.Status == CreditApplicationStatus.Approved && s.ApprovalValidUntil != null && s.ApprovalValidUntil < dia && !s.IsDeleted)
                .ToListAsync(ct);
            foreach (var s in vencidas)
            {
                var r = await SolicitudAsync(s, dia, ct);
                if (r.IsFailure) return Result.Failure<int>(r.Error);
                if (r.Value is not null) emitidos++;
            }
        }

        if (await HayTipoAsync(CupoRotativoOtorgadoV1.Type, ct))
        {
            var rotativos = await db.CreditLimits.AsNoTracking().Where(c => c.Kind == CreditLimitKind.Revolving && !c.IsDeleted && c.ValidFrom <= dia)
                .ToListAsync(ct);
            foreach (var cupo in rotativos.GroupBy(c => (c.PersonId, c.CreditLineId)).Select(g => g.OrderBy(c => c.VersionNumber).First()))
            {
                var r = await CupoAsync(cupo, dia, ct);
                if (r.IsFailure) return Result.Failure<int>(r.Error);
                if (r.Value is not null) emitidos++;
            }
        }
        return Result.Success(emitidos);
    }

    // ------------------------------------------------------------------------------------------------------------- piezas --

    private async Task<Result<IntegrationMessage?>> EmitirAsync(object contenido, string tipo, string clase, Guid origen, string numero, DateOnly dia,
        Guid sucursal, Guid deudor, CancellationToken ct)
    {
        var fecha = await fechaContable.CalcularAsync(dia, ct);
        if (fecha.IsFailure) return Result.Failure<IntegrationMessage?>(fecha.Error);
        var yaHay = db.IntegrationMessages.Local.Any(m => m.OriginPublicId == origen && m.Type == tipo)
            || await db.IntegrationMessages.AsNoTracking().AnyAsync(m => m.OriginPublicId == origen && m.Type == tipo, ct);
        var clave = yaHay ? ClavesDeEvento.ConfirmacionPor(Guid.NewGuid()) : ClavesDeEvento.Confirmacion;
        var emitido = await emision.ContingenteAsync(new OrigenDeEmision(MessageOriginKind.Document, origen, clase, EmisionDeCartera.Prefijo(numero) ?? clase,
            numero, fecha.Value, sucursal, PersonPublicId: deudor), tipo, clave, contenido, ct);
        return emitido.IsFailure ? Result.Failure<IntegrationMessage?>(emitido.Error) : Result.Success<IntegrationMessage?>(emitido.Value);
    }

    /// <summary>
    /// El <c>balanceAfter</c> del último hecho de <paramref name="tipo"/> del sujeto (por su <c>PublicId</c> en el contenido), contando los de la
    /// unidad de trabajo sin guardar; cero si no hay ninguno.
    /// </summary>
    public async Task<decimal> UltimoSaldoAsync(string tipo, Guid sujeto, CancellationToken ct)
    {
        var texto = sujeto.ToString("D");
        var local = db.IntegrationMessages.Local.Where(m => m.Type == tipo && m.PayloadJson.Contains(texto, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.EmittedAt).Select(m => m.PayloadJson).FirstOrDefault();
        var contenido = local ?? await db.IntegrationMessages.AsNoTracking().Where(m => m.Type == tipo && m.PayloadJson.Contains(texto))
            .OrderByDescending(m => m.Id).Select(m => m.PayloadJson).FirstOrDefaultAsync(ct);
        if (contenido is null) return 0m;
        using var doc = JsonDocument.Parse(contenido);
        return doc.RootElement.TryGetProperty("balanceAfter", out var saldo) && saldo.TryGetDecimal(out var valor) ? valor : 0m;
    }

    private Task<bool> HayTipoAsync(string tipo, CancellationToken ct) =>
        db.LendingFactTypes.AsNoTracking().AnyAsync(f => f.Code == tipo && !f.IsDeleted, ct);

    private async Task<Guid> PersonaAsync(int personaId, CancellationToken ct) =>
        await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == personaId).Select(p => p.PublicId).FirstAsync(ct);

    private async Task<string> LineaAsync(int lineaId, CancellationToken ct) =>
        await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == lineaId).Select(l => l.Code).FirstOrDefaultAsync(ct) ?? string.Empty;

    /// <summary>El texto de un saldo contingente (para la auditoría y los mensajes al usuario).</summary>
    public static string Texto(decimal valor) => valor.ToString("N2", CultureInfo.GetCultureInfo("es-CO"));
}
