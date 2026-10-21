using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Documents;

/// <summary>
/// El título del CDAT, «no negociable» (feature 014, US13, T196; contracts/documentos.md §2.5; FR-068; research R11, E173): nace <b>solo</b>, en la
/// transacción de la operación que sella una versión de condiciones —la constitución al fondearse, la prórroga (del proceso diario, sin persona) y la
/// renovación—, con el número del título y el sufijo de su versión (<c>TIT-000123-1</c>, <c>-2</c>…). Al nacer, el título vigente anterior recibe el
/// evento <c>Superseded</c> con la operación; al redimirse o cancelarse el CDAT, el vigente recibe <c>Settled</c>. El reverso de la operación que lo
/// selló lo anula (<see cref="AnulacionDeDocumentosDeCuentas"/>). Si no se puede emitir —sin firmante configurado, el almacén caído— la operación sigue
/// y queda en el registro (<c>[Cartera.TituloDeCdatSinEmitir]</c>): el título se emite después desde la ficha (<see cref="ReemitirVigenteAsync"/>), como
/// la liquidación del retiro (E73). (nuevo)
/// </summary>
public sealed class TitulosDeCdat(
    IApplicationDbContext db,
    EmisionDeDocumentosDeCartera emision,
    ConstructorDeDocumentosDeCartera constructor,
    IDateTimeService reloj,
    ILogger<TitulosDeCdat>? logger = null)
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    /// <summary>
    /// Emite el título de <paramref name="version"/> del CDAT y reemplaza el anterior. Corre dentro de la transacción de quien llama (que ya tiene la
    /// cuenta bloqueada) y guarda. Devuelve el documento, o nulo si no se pudo emitir (queda en el registro).
    /// </summary>
    public async Task<IssuedLendingDocument?> EmitirAsync(MemberAccount cuenta, MemberAccountTermsVersion version, MemberAccountOperation operacion, int? usuario,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(operacion);
        var hoy = reloj.HoyLocal;
        var firmante = await constructor.FirmanteAsync(hoy, ct);
        if (firmante.IsFailure)
        {
            _log.LogWarning("[Cartera.TituloDeCdatSinEmitir] El título de la versión {Version} del CDAT {Cuenta} no se emitió: {Motivo}", version.VersionNumber,
                cuenta.Number, firmante.Error.Message);
            return null;
        }
        var titular = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == cuenta.HolderPersonId, ct);
        var anterior = await VigenteAsync(cuenta.Id, ct);
        var emitido = await emision.EmitirAsync(new SolicitudDeEmision(IssuedDocumentKind.TermDepositTitle, titular, firmante.Value, usuario,
            DocumentDeliveryMode.Download, hoy)
        {
            Cuenta = cuenta,
            VersionDeCondicionesId = version.Id,
        }, ct);
        if (emitido.IsFailure)
        {
            _log.LogWarning("[Cartera.TituloDeCdatSinEmitir] El título de la versión {Version} del CDAT {Cuenta} no se emitió: {Codigo} {Motivo}",
                version.VersionNumber, cuenta.Number, emitido.Error.Code, emitido.Error.Message);
            return null;
        }
        var documento = emitido.Value.Documento;
        if (anterior is not null && anterior.Id != documento.Id)
        {
            var (actor, quien) = usuario is { } u ? (ActorKind.Person, (int?)u) : (ActorKind.Process, (int?)null);
            db.IssuedDocumentEvents.Add(IssuedDocumentEvent.Reemplazado(anterior.Id,
                $"Reemplazado por el título {documento.Number} ({Causa(version.Cause)} {operacion.Number}).", documento.IssuedAt, actor, quien,
                operacion.Id == 0 ? null : operacion.Id));
            await db.SaveChangesAsync(ct);
        }
        return documento;
    }

    /// <summary>
    /// El CDAT se redimió o se canceló con <paramref name="operacion"/>: el título vigente queda liquidado (evento <c>Settled</c>). No guarda: lo guarda la
    /// operación que redime.
    /// </summary>
    public async Task LiquidarAsync(MemberAccount cuenta, MemberAccountOperation operacion, int? usuario, DateTime en, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(operacion);
        if (await VigenteAsync(cuenta.Id, ct) is not { } vigente) return;
        var (actor, quien) = usuario is { } u ? (ActorKind.Person, (int?)u) : (ActorKind.Process, (int?)null);
        db.IssuedDocumentEvents.Add(IssuedDocumentEvent.Liquidado(vigente.Id, $"El CDAT {cuenta.Number} se liquidó con la operación {operacion.Number}.", en, actor,
            quien, operacion.Id == 0 ? null : operacion.Id));
    }

    /// <summary>
    /// El título de la versión vigente del CDAT que no se emitió al sellarla (sin firmante, el almacén caído): lo emite ahora, a nombre de quien lo pide.
    /// Si ya tiene título vigente, lo devuelve sin emitir otro.
    /// </summary>
    public async Task<IssuedLendingDocument?> ReemitirVigenteAsync(MemberAccount cuenta, int usuario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        var version = await db.MemberAccountTermsVersions.AsNoTracking().Where(v => v.MemberAccountId == cuenta.Id).OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
        if (version is null) return null;
        var vigente = await VigenteAsync(cuenta.Id, ct);
        if (vigente is not null)
        {
            var titula = await db.IssuedDocumentAccounts.AsNoTracking().AnyAsync(x => x.IssuedDocumentId == vigente.Id && x.TermsVersionId == version.Id, ct);
            if (titula) return vigente;
        }
        var operacion = await db.MemberAccountOperations.AsNoTracking().FirstAsync(o => o.Id == version.OperationId, ct);
        return await EmitirAsync(cuenta, version, operacion, usuario, ct);
    }

    /// <summary>El título vigente del CDAT (ni anulado, ni reemplazado, ni liquidado), guardado o de esta unidad de trabajo.</summary>
    public async Task<IssuedLendingDocument?> VigenteAsync(int cuentaId, CancellationToken ct)
    {
        var cubren = db.IssuedDocumentAccounts.AsNoTracking().Where(x => x.MemberAccountId == cuentaId).Select(x => x.IssuedDocumentId);
        var titulos = await db.IssuedLendingDocuments.AsNoTracking()
            .Where(d => d.Kind == IssuedDocumentKind.TermDepositTitle && cubren.Contains(d.Id) && !d.IsDeleted).OrderByDescending(d => d.Id).ToListAsync(ct);
        if (titulos.Count == 0) return null;
        var ids = titulos.Select(t => t.Id).ToList();
        var cerrados = (await db.IssuedDocumentEvents.AsNoTracking()
                .Where(e => ids.Contains(e.IssuedDocumentId) && (e.Kind == IssuedDocumentEventKind.Voided || e.Kind == IssuedDocumentEventKind.Superseded
                                                                 || e.Kind == IssuedDocumentEventKind.Settled))
                .Select(e => e.IssuedDocumentId).ToListAsync(ct))
            .Concat(db.IssuedDocumentEvents.Local.Where(e => e.Kind is IssuedDocumentEventKind.Voided or IssuedDocumentEventKind.Superseded
                or IssuedDocumentEventKind.Settled).Select(e => e.IssuedDocumentId))
            .ToHashSet();
        return titulos.FirstOrDefault(t => !cerrados.Contains(t.Id));
    }

    private static string Causa(AccountTermsCause c) => c switch
    {
        AccountTermsCause.Rollover => "prórroga",
        AccountTermsCause.Renewal => "renovación",
        _ => "operación",
    };
}
