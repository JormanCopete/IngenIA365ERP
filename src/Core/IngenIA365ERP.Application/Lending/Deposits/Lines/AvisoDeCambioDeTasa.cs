using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Lines;

/// <summary>Un titular o cotitular de una cuenta viva de la línea, a quien se avisa el cambio de tasa. (nuevo)</summary>
public sealed record TitularAvisado(int PersonId, int MemberAccountId, string AccountNumber);

/// <summary>
/// El aviso de cambio de tasa de una línea a la vista a sus titulares (feature 014, US1, T067; FR-038): cuando una versión nueva de una línea a la
/// vista que no declara tasa pactada cambia la tasa, cada titular y cotitular de una cuenta viva de la línea recibe el informativo <c>CAMBIOTASA</c>
/// por la mensajería de la Cartera (<see cref="ColaDeMensajesAlDeudor"/>, la ventana y el canal autorizado del titular), y la versión no puede regir
/// antes de hoy más la anticipación del reglamento (<c>Captaciones.AnticipacionAvisoCambioDeTasaDias</c>) o la de la línea, la mayor
/// (<c>Lending.Savings.RateChangeNoticeTooShort</c>). El aviso nunca tumba la versión: sin plantilla, fuera de ventana o con cualquier falla, queda
/// en el log. Las cuentas que sellaron su tasa (CDAT, contractual, tasa pactada) no cambian y no se avisan. (nuevo)
/// </summary>
public sealed class AvisoDeCambioDeTasa(
    IApplicationDbContext db,
    IContextoDeLineasDeCaptacion contexto,
    ColaDeMensajesAlDeudor? cola = null,
    ILogger<AvisoDeCambioDeTasa>? logger = null)
{
    public const string CodigoDePlantilla = "CAMBIOTASA";

    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    /// <summary>¿El paso de <paramref name="anterior"/> a <paramref name="nueva"/> se avisa? Sólo en la vista, sin tasa pactada y si la tasa cambia.</summary>
    public static bool SeAvisa(BorradorDeVersionDeCaptacion? anterior, BorradorDeVersionDeCaptacion nueva) =>
        anterior is not null && nueva.DepositClass == DepositClass.Demand && !nueva.DeclaresAgreedRate && nueva.CambiaLaTasaRespectoDe(anterior);

    /// <summary>Los titulares y cotitulares vigentes hoy de las cuentas vivas (no canceladas) de la línea.</summary>
    public async Task<IReadOnlyList<TitularAvisado>> TitularesAsync(int lineaId, DateOnly hoy, CancellationToken ct) =>
        await (from p in db.MemberAccountParties.AsNoTracking()
               join a in db.MemberAccounts.AsNoTracking() on p.MemberAccountId equals a.Id
               where a.CreditLineId == lineaId && a.Nature == LineNature.Deposit && a.Status != MemberAccountStatus.Closed && !a.IsDeleted
                     && !p.IsDeleted && (p.Role == MemberAccountPartyRole.Holder || p.Role == MemberAccountPartyRole.CoHolder)
                     && p.ValidFrom <= hoy && (p.ValidTo == null || p.ValidTo >= hoy)
               orderby a.Id, p.PersonId
               select new TitularAvisado(p.PersonId, a.Id, a.Number)).Distinct().ToListAsync(ct);

    /// <summary>
    /// La anticipación que rige: la mayor entre la del reglamento (parámetro) y la de la línea (<c>RateChangeNoticeDays</c>), o nula si el cambio no
    /// se avisa (no cambia la tasa, no es a la vista, declara tasa pactada o no hay titulares que avisar).
    /// </summary>
    public async Task<int?> AnticipacionAsync(int lineaId, BorradorDeVersionDeCaptacion? anterior, BorradorDeVersionDeCaptacion nueva, DateOnly hoy,
        CancellationToken ct)
    {
        if (!SeAvisa(anterior, nueva)) return null;
        if ((await TitularesAsync(lineaId, hoy, ct)).Count == 0) return null;
        return Math.Max(await contexto.AnticipacionDelAvisoAsync(hoy, ct), nueva.RateChangeNoticeDays);
    }

    /// <summary>
    /// Encola el aviso a cada titular, <b>sin guardar</b> (en la unidad de trabajo de la versión). Devuelve cuántos quedaron encolados.
    /// </summary>
    public async Task<int> AvisarAsync(CreditLine linea, DepositLineVersion version, BorradorDeVersionDeCaptacion anterior, BorradorDeVersionDeCaptacion nueva,
        DateOnly hoy, int? actorUserId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(linea);
        ArgumentNullException.ThrowIfNull(version);
        if (cola is null || !SeAvisa(anterior, nueva)) return 0;
        var encolados = 0;
        foreach (var titular in await TitularesAsync(linea.Id, hoy, ct))
        {
            try
            {
                var r = await cola.EncolarAsync(new PedidoDeMensajeAlDeudor(titular.PersonId, null, DebtorMessagePurpose.Informative, MessageChannel.Email,
                    CodigoDePlantilla, DebtorMessage.OrigenCambioDeTasa, $"{version.PublicId:N}:{titular.MemberAccountId}:{titular.PersonId}",
                    actorUserId is null ? ActorKind.Process : ActorKind.Person, actorUserId)
                {
                    OriginPublicId = version.PublicId,
                    Valores = new Dictionary<string, string?>
                    {
                        ["NumeroDeCuenta"] = titular.AccountNumber,
                        ["NombreDeLaLinea"] = linea.Name,
                        ["TasaAnterior"] = Tasa(anterior),
                        ["TasaNueva"] = Tasa(nueva),
                        ["FechaDelCambio"] = nueva.ValidFrom.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    },
                }, ct);
                if (r.IsSuccess) encolados++;
                else
                    _log.LogInformation("[Cartera.AvisoDeCambioDeTasa] No se encoló el aviso de la cuenta {Cuenta}: {Codigo} {Mensaje}",
                        titular.AccountNumber, r.Error.Code, r.Error.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "[Cartera.AvisoDeCambioDeTasa] El aviso de la cuenta {Cuenta} falló.", titular.AccountNumber);
            }
        }
        return encolados;
    }

    /// <summary>La tasa de una versión para el mensaje: la fija, la variable o el rango de sus tramos.</summary>
    public static string Tasa(BorradorDeVersionDeCaptacion b)
    {
        var es = CultureInfo.GetCultureInfo("es-CO");
        string Pct(decimal f) => (f * 100m).ToString("0.####", es) + " % EA";
        if (b.RateKind == RateKind.Variable) return $"{b.IndexCode} + {((b.Margin ?? 0m) * 100m).ToString("0.####", es)} puntos";
        if (b.RateTiers.Count == 0) return Pct(b.AnnualRate ?? 0m);
        var tasas = b.RateTiers.Select(t => t.AnnualRate ?? 0m).ToList();
        return tasas.Min() == tasas.Max() ? Pct(tasas.Min()) : $"{Pct(tasas.Min())} a {Pct(tasas.Max())} según el saldo";
    }
}
