using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Integration;

/// <summary>
/// Los dos puertos que Recaudo ofrece a la conciliación mínima de Tesorería (feature 015, US25, T0277; R-05; FR-209, FR-216; contracts/interfaces.md §2):
/// <list type="bullet">
/// <item><see cref="IRecaudoParaConciliacion"/> (lectura): los recibos válidos de una cuenta propia (canal banco, pagaduría o el que haya entrado por esa cuenta) y las
/// partidas por identificar que nacieron de sus abonos, con la línea del extracto de la que nacieron para que el cruce las reconozca por su vínculo;</item>
/// <item><see cref="IPartidasDesdeConciliacion"/> (escritura): un abono sin registro que el conciliador encuentra en el extracto pasa a Recaudo como partida por identificar
/// (<c>OriginKind = Reconciliation</c>) con su hecho <c>PartidaPorIdentificarReconocida</c>. Valida y audita (<c>LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras</c>),
/// toma <c>RCD_Setup</c> antes que Tesorería sus cerrojos (CH16) y escribe en la unidad de trabajo de quien llama.</item>
/// </list>
/// No lee <c>TES_</c>: la línea la pide al puerto de Tesorería (<see cref="IMovimientosBancariosParaRecaudo"/>). (nuevo)
/// </summary>
public sealed class RecaudoParaConciliacion(
    IApplicationDbContext db,
    ICerrojoDeRecaudo cerrojo,
    IMovimientosBancariosParaRecaudo movimientos,
    PartidasPorIdentificar partidas,
    IActorActual actorActual,
    AuditoriaDeRecaudo auditoria) : IRecaudoParaConciliacion, IPartidasDesdeConciliacion
{
    public const string ClaseRecibo = "Receipt";
    public const string ClasePartida = "UnidentifiedItem";
    /// <summary>R2 (fases 37 y 38): el abono que pagó cheques de un lote de canje y el que liquidó un archivo de convenio o de pasarela. (nuevo)</summary>
    public const string ClaseCanje = "ClearingBatch";
    public const string ClaseConvenio = "AgreementSettlement";

    public async Task<IReadOnlyList<RegistroConciliableDto>> RegistrosAsync(Guid ownBankAccountPublicId, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        ValidarPeriodo(ownBankAccountPublicId, desde, hasta);
        var recibos = await db.IssuedReceipts.AsNoTracking()
            .Where(r => r.OwnBankAccountPublicId == ownBankAccountPublicId && r.Status != ReceiptState.Voided && r.ValueDate >= desde && r.ValueDate <= hasta)
            .Select(r => new RegistroConciliableDto(r.PublicId, ClaseRecibo, r.Number, r.ValueDate, r.TotalMeans - r.ChangeGiven, r.Number, r.BankStatementLinePublicId))
            .ToListAsync(ct);
        var deLasPartidas = await db.UnidentifiedItems.AsNoTracking()
            .Where(p => p.OwnBankAccountPublicId == ownBankAccountPublicId && p.OriginKind != UnidentifiedItemOrigin.OpeningLoad && p.MovementDate >= desde
                        && p.MovementDate <= hasta)
            .Select(p => new RegistroConciliableDto(p.PublicId, ClasePartida, p.Number, p.MovementDate, p.Amount, p.Reference, p.BankStatementLinePublicId))
            .ToListAsync(ct);
        // R2 (fases 37 y 38; FR-084, FR-093): el abono del extracto que pagó cheques de un lote de canje o que liquidó un archivo de convenio queda en los libros por su
        // identificación, con su línea: la conciliación de Tesorería lo cruza con ella.
        var liquidaciones = await db.BankItemIdentifications.AsNoTracking()
            .Where(i => i.OwnBankAccountPublicId == ownBankAccountPublicId && (i.Outcome == BankItemOutcome.ClearingBatch || i.Outcome == BankItemOutcome.AgreementSettlement)
                        && i.MovementDate >= desde && i.MovementDate <= hasta)
            .Select(i => new RegistroConciliableDto(i.PublicId, i.Outcome == BankItemOutcome.ClearingBatch ? ClaseCanje : ClaseConvenio, i.Note ?? string.Empty, i.MovementDate,
                i.Amount, i.Note, i.BankStatementLinePublicId))
            .ToListAsync(ct);
        return [.. recibos, .. deLasPartidas, .. liquidaciones];
    }

    public async Task TomarCerrojosAsync(Guid ownBankAccountPublicId, CancellationToken ct)
    {
        if (ownBankAccountPublicId == Guid.Empty) throw new ArgumentException("La cuenta propia es obligatoria.", nameof(ownBankAccountPublicId));
        await cerrojo.TomarSetupAsync(ct: ct);
    }

    public async Task<PartidaCreadaDto> CrearAsync(Guid statementLinePublicId, string motivo, CancellationToken ct)
    {
        ValidarPedido(statementLinePublicId, motivo);
        var linea = await movimientos.LineaAsync(statementLinePublicId, ct)
                    ?? throw new InvalidOperationException("La línea del extracto no existe o no es un abono: sólo un abono sin registro pasa a Recaudo como partida.");
        var pendiente = linea.Amount - linea.Identified;
        if (pendiente <= 0m) throw new InvalidOperationException("El abono ya está identificado en Recaudo: no hay nada que llevar a partidas.");
        var oficina = linea.BranchPublicId is { } b
            ? await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(x => x.PublicId == b).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
            : null;
        if (oficina is null) throw new InvalidOperationException("La cuenta propia del abono no tiene oficina: no se puede reconocer la partida.");
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) throw new InvalidOperationException("El usuario no existe.");

        var partida = await partidas.ReconocerAsync(linea, oficina.Value, pendiente, usuario, motivo, ct);
        partida.OriginKind = UnidentifiedItemOrigin.Reconciliation;
        await movimientos.MarcarIdentificacionAsync(linea.PublicId, linea.Amount, ct);
        await AuditarAsync(partida.PublicId, partida.Number, linea.PublicId, pendiente, motivo, ct);
        return new PartidaCreadaDto(partida.PublicId, partida.Number);
    }

    /// <summary>El validador del puerto de lectura (desviación III).</summary>
    public static void ValidarPeriodo(Guid cuenta, DateOnly desde, DateOnly hasta)
    {
        if (cuenta == Guid.Empty) throw new ArgumentException("La cuenta propia es obligatoria.", nameof(cuenta));
        if (hasta < desde) throw new ArgumentException("El período termina antes de empezar.", nameof(hasta));
    }

    /// <summary>El validador del puerto de escritura (desviación III).</summary>
    public static void ValidarPedido(Guid linea, string motivo)
    {
        if (linea == Guid.Empty) throw new ArgumentException("La línea del extracto es obligatoria.", nameof(linea));
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length > 500)
            throw new ArgumentException("El motivo es obligatorio y tiene a lo sumo 500 caracteres.", nameof(motivo));
    }

    private Task AuditarAsync(Guid partida, string numero, Guid linea, decimal valor, string motivo, CancellationToken ct) =>
        auditoria.AgregarAsync("Receipts.UnidentifiedItem.FromReconciliation", "UnidentifiedItem", partida, null,
            new { number = numero, line = linea, amount = valor, reason = motivo.Trim() }, ct);
}
