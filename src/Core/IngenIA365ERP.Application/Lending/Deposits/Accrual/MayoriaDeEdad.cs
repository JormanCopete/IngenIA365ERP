using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Accrual;

/// <summary>
/// El subpaso «mayoría de edad» del paso de captaciones (feature 014, US2, T076; research R22; data-model §3.1, §3.2; FR-033): en cada tanda, las cuentas
/// de captación con un representante legal vigente cuyo titular —persona natural— cumplió el día d o antes la mayoría de edad
/// (<c>Captaciones.MayoriaDeEdadAnios</c>, 18 con fuente). Por cada una, en una transacción:
/// <list type="number">
/// <item>avisa al titular y al representante con el informativo <c>MAYOREDAD</c> por la mensajería de la Cartera (antes de cerrar la vigencia, que es lo
/// que lo hace destinatario); un aviso que no se encola queda en el log, nunca tumba el paso;</item>
/// <item>cierra la vigencia del representante la víspera del cumpleaños (<c>EndReason</c> «mayoría de edad», sin usuario: lo hace el proceso), así que
/// desde ese día sólo firma el titular;</item>
/// <item>marca la cuenta «actualizar datos» desde el cumpleaños, hasta que el titular actualice su conocimiento y su autorización de datos
/// (<c>POST /{id}/data-update</c>).</item>
/// </list>
/// No cambia el número, el saldo ni las condiciones. Es idempotente: una cuenta ya sin representante vigente no vuelve a entrar. Desde K8 (US4, T120;
/// R22, FR-033) anula el retiro en espera de aprobación de la cuenta (la solicitud de la plataforma queda <c>Cancelled</c>): lo pidió el representante,
/// que desde el cumpleaños ya no firma; el aviso de la mayoría de edad les llega a los dos. Una cuenta que falla queda en
/// <c>LND_ProcessRunErrors</c> y las demás siguen. (nuevo)
/// </summary>
public sealed class MayoriaDeEdad(
    IApplicationDbContext db,
    ReglasDeParametrosDeCartera parametros,
    ColaDeMensajesAlDeudor? cola = null,
    ILogger<MayoriaDeEdad>? logger = null,
    IngenIA365ERP.Application.Common.Approvals.IMotorDeAprobaciones? aprobaciones = null) : ISubpasoDeCaptaciones
{
    public const string CodigoDePlantilla = "MAYOREDAD";

    public const string MotivoDeFin = "Mayoría de edad del titular: desde ese día maneja su cuenta.";

    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public string Nombre => "MayoriaDeEdad";

    /// <summary>Una cuenta que pasa al titular: la cuenta, su representante vigente y el día en que el titular cumplió la mayoría de edad. (nuevo)</summary>
    public sealed record CuentaQuePasa(int MemberAccountId, int RepresentanteId, DateOnly Cumple);

    /// <summary>Las cuentas de la tanda que pasan al titular el día <paramref name="d"/> o antes.</summary>
    public async Task<IReadOnlyList<CuentaQuePasa>> CandidatasAsync(IReadOnlyList<int> cuentas, DateOnly d, int mayoria, CancellationToken ct)
    {
        var ids = cuentas.ToArray();
        var filas = await (from p in db.MemberAccountParties.AsNoTracking()
                           join a in db.MemberAccounts.AsNoTracking() on p.MemberAccountId equals a.Id
                           join t in db.People.AsNoTracking() on a.HolderPersonId equals t.Id
                           where ids.Contains(a.Id) && a.Nature == LineNature.Deposit && a.Status != MemberAccountStatus.Closed && !a.IsDeleted
                                 && p.Role == MemberAccountPartyRole.LegalRepresentative && p.ValidTo == null && !p.IsDeleted
                                 && t.DateOfBirth != null && (t.PersonType == null || t.PersonType != LectorDeCuentas.PersonaJuridica)
                           select new { a.Id, Representante = p.Id, Nacimiento = t.DateOfBirth!.Value })
            .ToListAsync(ct);
        return filas
            .Select(f => new CuentaQuePasa(f.Id, f.Representante, ReglasDeTitularidad.CumpleMayoriaEl(f.Nacimiento, mayoria)))
            .Where(c => c.Cumple <= d)
            .OrderBy(c => c.MemberAccountId)
            .ToList();
    }

    public async Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuentas);
        if (cuentas.Count == 0) return [];
        var mayoria = await LectorDeCuentas.MayoriaDeEdadAsync(parametros, d, ct);
        var fallos = new List<FalloDeCuenta>();
        foreach (var candidata in await CandidatasAsync(cuentas, d, mayoria, ct))
        {
            try
            {
                await PasarAlTitularAsync(candidata, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.DescartarCambiosSalvo(corrida);
                _log.LogWarning(ex, "[Cartera.MayoriaDeEdadFallida] La cuenta {Cuenta} no pasó a su titular.", candidata.MemberAccountId);
                fallos.Add(new FalloDeCuenta(candidata.MemberAccountId, "Lending.Savings.MajorityFailed", ex.Message));
            }
        }
        return fallos;
    }

    private async Task PasarAlTitularAsync(CuentaQuePasa candidata, CancellationToken ct) =>
        await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == candidata.MemberAccountId, ct);
            var representante = await db.MemberAccountParties.FirstAsync(p => p.Id == candidata.RepresentanteId, ct);
            if (representante.ValidTo is not null) return 0;

            // Primero el aviso (el representante todavía es parte vigente de la cuenta para la cola), después el cierre.
            await AvisarAsync(cuenta, [cuenta.HolderPersonId, representante.PersonId], candidata.Cumple, ct);
            var hasta = candidata.Cumple.AddDays(-1);
            representante.Terminar(hasta < representante.ValidFrom.AddDays(-1) ? representante.ValidFrom.AddDays(-1) : hasta, MotivoDeFin, null);
            // US4 (T120): el retiro que esperaba un aprobador ya no vale (lo pidió quien desde hoy no firma).
            if (aprobaciones is not null
                && await aprobaciones.InvalidarAsync(Domain.Approvals.ApprovalSourceTypes.MemberAccountOperation, cuenta.PublicId,
                    Domain.Approvals.ApprovalSubjects.AccountWithdrawal, ct) is { } anulada)
                _log.LogInformation("[Cartera.RetiroAnuladoPorMayoriaDeEdad] Se anuló la solicitud {Solicitud} del retiro de la cuenta {Cuenta}.",
                    anulada.PublicId, cuenta.Number);
            if (!cuenta.RequiresDataUpdate)
            {
                cuenta.RequiresDataUpdate = true;
                cuenta.DataUpdateRequiredSince = candidata.Cumple;
            }
            await db.SaveChangesAsync(ct);
            return 1;
        }, ct);

    /// <summary>Encola el aviso a cada persona, sin guardar; lo que no se encola queda en el log.</summary>
    private async Task AvisarAsync(MemberAccount cuenta, IEnumerable<int> personas, DateOnly cumple, CancellationToken ct)
    {
        if (cola is null) return;
        var linea = await db.CreditLines.AsNoTracking().Where(l => l.Id == cuenta.CreditLineId).Select(l => l.Name).FirstOrDefaultAsync(ct) ?? string.Empty;
        foreach (var persona in personas.Distinct())
        {
            try
            {
                var r = await cola.EncolarAsync(new PedidoDeMensajeAlDeudor(persona, null, DebtorMessagePurpose.Informative, MessageChannel.Email,
                    CodigoDePlantilla, DebtorMessage.OrigenMayoriaDeEdad, $"{cuenta.PublicId:N}:mayoria:{persona}", ActorKind.Process, null)
                {
                    OriginPublicId = cuenta.PublicId,
                    Valores = new Dictionary<string, string?>
                    {
                        ["NumeroDeCuenta"] = cuenta.Number,
                        ["NombreDeLaLinea"] = linea,
                        ["FechaDeMayoriaDeEdad"] = cumple.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    },
                }, ct);
                if (r.IsFailure)
                    _log.LogInformation("[Cartera.AvisoDeMayoriaDeEdad] No se encoló el aviso de la cuenta {Cuenta}: {Codigo} {Mensaje}", cuenta.Number,
                        r.Error.Code, r.Error.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "[Cartera.AvisoDeMayoriaDeEdad] El aviso de la cuenta {Cuenta} falló.", cuenta.Number);
            }
        }
    }
}
