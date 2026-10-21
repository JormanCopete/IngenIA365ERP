using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Accrual;

/// <summary>
/// El paso (5) del ciclo diario y de la puesta al día: los <b>anticipos al vencer</b> (feature 013, US5, bloque 9B; FR-074, D6, D8; data-model
/// §7.9; mensajes.md §10.10). El día que vence una cuota cubierta por anticipo, la reconstrucción ya lo aplicó (<c>AplicadorDePagos.AplicarAnticipo</c>,
/// antes de las operaciones de ese día) y su libro teórico trae las filas <c>AdvanceApplied</c> (y la liberación del anticipo de la cuota, que
/// pasa al general como <c>AdvanceReceived</c>) <b>sin operación</b>: aquí se escriben con la corrida o con la operación que puso al día, y se
/// emite un <c>AnticipoAplicado</c> por crédito y día (origen la parte, clave <c>Confirmation:{crédito:N}</c>, tercero el deudor; lo origina el
/// proceso, que el tipo admite). No es una operación (M10). (nuevo)
/// </summary>
public static class AnticiposAlVencer
{
    /// <summary>Las filas de los anticipos aplicados ese día (las del libro teórico sin operación).</summary>
    public static IReadOnlyList<MovimientoDeLibro> Filas(DiaCausado dia)
    {
        ArgumentNullException.ThrowIfNull(dia);
        return dia.Estado.Libro.DelDia(dia.Dia)
            .Where(f => f.OperacionId is null && f.Clase is LoanTransactionKind.AdvanceApplied or LoanTransactionKind.AdvanceReceived && f.Monto != 0m)
            .ToList();
    }

    /// <summary>
    /// El <c>AnticipoAplicado</c> de un crédito y un día, o nulo si ese día no venció una cuota cubierta. <c>amounts</c> = lo que el anticipo cubrió
    /// por concepto y cuota (positivo); <c>advanceSources</c> = los <c>AnticipoRecibido</c> de los pagos del crédito.
    /// </summary>
    public static HechoPorCredito? Hecho(DatosDeHecho datos, DiaCausado dia, DateTime registradoEn, IReadOnlyList<OriginalFactRefV1> fuentes,
        CategoriaContableVigente? categoria = null)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var aplicaciones = dia.Estado.AnticiposAplicados.Where(a => a.Fecha == dia.Dia && a.Lineas.Count > 0).ToList();
        if (aplicaciones.Count == 0) return null;
        var montos = aplicaciones.SelectMany(a => a.Lineas)
            .GroupBy(l => (l.Cuota, l.Concepto)).OrderBy(g => g.Key.Cuota ?? 0).ThenBy(g => (int)g.Key.Concepto)
            .Select(g => new ConceptAmountV1 { Concept = g.Key.Concepto.ToString(), InstallmentNumber = g.Key.Cuota, Amount = g.Sum(l => l.Monto) })
            .ToList();
        var contenido = new AnticipoAplicadoV1
        {
            Loan = datos.Referencia,
            DebtorPersonPublicId = datos.DeudorPublicId,
            ValueDate = dia.Dia,
            RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(registradoEn, DateTimeKind.Utc)),
            Dimensions = EmisionDeCartera.Dimensiones(datos.Hecho(dia.Dia), categoria ?? CategoriaContableVigente.DeCreditoNuevo),
            Amounts = montos,
            RulesVersion = datos.Condiciones.RulesVersion,
            InstallmentNumber = aplicaciones.Min(a => a.Cuota),
            AdvanceSources = fuentes,
        };
        return new HechoPorCredito(AnticipoAplicadoV1.Type, datos, dia.Dia, contenido);
    }

    /// <summary>Los <c>AnticipoRecibido</c> ya emitidos de los pagos de los créditos, por crédito (las fuentes de <c>advanceSources</c>).</summary>
    public static async Task<IReadOnlyDictionary<int, IReadOnlyList<OriginalFactRefV1>>> FuentesAsync(IApplicationDbContext db, IReadOnlyCollection<int> loanIds,
        CancellationToken ct)
    {
        var ids = loanIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, IReadOnlyList<OriginalFactRefV1>>();
        var pagos = await db.LoanOperations.AsNoTracking()
            .Where(o => ids.Contains(o.LoanId) && o.Kind == LoanOperationKind.Payment && o.ReversedByOperationId == null)
            .Select(o => new { o.LoanId, o.PublicId, o.Number }).ToListAsync(ct);
        var publicos = pagos.Select(p => p.PublicId).ToList();
        var mensajes = publicos.Count == 0
            ? []
            : await db.IntegrationMessages.AsNoTracking().Where(m => publicos.Contains(m.OriginPublicId) && m.Type == AnticipoRecibidoV1.Type)
                .Select(m => new { m.PublicId, m.OriginPublicId }).ToListAsync(ct);
        return ids.ToDictionary(id => id, id => (IReadOnlyList<OriginalFactRefV1>)pagos.Where(p => p.LoanId == id)
            .SelectMany(p => mensajes.Where(m => m.OriginPublicId == p.PublicId)
                .Select(m => new OriginalFactRefV1 { MessageId = m.PublicId, Type = AnticipoRecibidoV1.Type, OriginPublicId = p.PublicId, Number = p.Number }))
            .ToList());
    }

    /// <summary>El mensaje <c>AnticipoAplicado</c> emitido para el crédito y el día (por su parte), para las filas del libro.</summary>
    public static Guid? Mensaje(PartesEmitidas emitidas, Guid loanPublicId, DateOnly dia)
    {
        ArgumentNullException.ThrowIfNull(emitidas);
        var clave = ClavesDeEvento.ConfirmacionPor(loanPublicId);
        var partes = emitidas.Partes.Where(p => p.FactType == AnticipoAplicadoV1.Type && p.ValueDate == dia).Select(p => p.PublicId).ToHashSet();
        return emitidas.Mensajes.FirstOrDefault(m => m.Type == AnticipoAplicadoV1.Type && m.OriginEventKey == clave && partes.Contains(m.OriginPublicId))?.PublicId;
    }

    /// <summary>Las filas del libro de los anticipos de un crédito y un día, de la corrida o de la operación, con su mensaje.</summary>
    public static IReadOnlyList<LoanTransaction> FilasDelLibro(int loanId, DiaCausado dia, int? processRunId, LoanOperation? operacion, DateTime registradoEn,
        Guid? mensaje) =>
        Filas(dia).Select(f => RegistroDeCartera.FilaDeHecho(loanId, f, operacion, processRunId, dia.Dia, registradoEn, mensaje)).ToList();
}
