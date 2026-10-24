using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Accounting;

// Feature 015, US6 (T0196; FR-068, FR-070 a FR-073, FR-199; contracts/contabilidad.md §3): los hechos de los abonos del extracto, en línea y sin guardar: el recibo de
// canal banco (o el no interactivo de otro canal), la partida reconocida, su clasificación y su devolución al consignante.
public sealed partial class EmisionDeRecaudo
{
    public const string ClaseReciboDeBanco = "BankReceipt";
    public const string ClasePartida = "UnidentifiedItem";
    public const string ClaseIdentificacion = "BankItemIdentification";

    /// <summary>Los tipos de documento del origen de los hechos de partidas (el emisor los exige en un origen <c>Document</c>) (nuevo).</summary>
    public const string TipoPartida = "PI";
    public const string TipoClasificacion = "CLA";

    /// <summary>El tipo del hecho del recibo de un canal no interactivo: banco y otros canales <c>ReciboDeBancoEmitido</c>; la contingencia, <c>ReciboEmitido</c>.</summary>
    public static string TipoDelHechoDelCanal(ReceiptChannel canal) => canal switch
    {
        ReceiptChannel.Contingency or ReceiptChannel.CashDesk => ReciboEmitidoV1.Type,
        _ => ReciboDeBancoEmitidoV1.Type,
    };

    /// <summary>Las dimensiones de una cuenta bancaria propia en una oficina (el rol <c>BancoRecaudo</c> se resuelve por el vínculo de la cuenta, no admite regla).</summary>
    public async Task<ReceiptsFactDimensionsV1> DimensionesDeBancoAsync(int branchId, Guid? cuentaPropia, int? paymentMeansId, string canal, CancellationToken ct)
    {
        var oficina = await DimensionesDeOficinaAsync(branchId, ct);
        var medio = paymentMeansId is { } m
            ? await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == m).Select(x => new { x.Code, x.Class }).FirstOrDefaultAsync(ct)
            : null;
        return oficina with
        {
            OwnBankAccountPublicId = cuentaPropia, MeansCode = medio?.Code, MeansClass = medio is null ? null : (int)medio.Class, Channel = canal,
        };
    }

    /// <summary>
    /// El hecho del recibo no interactivo (FR-068): banco → <c>ReciboDeBancoEmitido</c> (débito <c>BancoRecaudo</c>, o <c>PartidasPorIdentificar</c> si identifica una
    /// partida; crédito el puente del módulo o el concepto vario; el excedente a <c>SaldoAFavorDePagadores</c>); la pagaduría, su propio hecho (fase 12); la
    /// contingencia, <c>ReciboEmitido</c> en línea. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage> ReciboNoInteractivoAsync(Receipt recibo, IReadOnlyList<ReceiptApplication> aplicaciones, ReceiptMeans medio,
        IReadOnlyList<ReceiptApplicationPortion> porciones, IReadOnlyDictionary<Guid, string> naturalezas, Guid? partidaPublicId, decimal aFavor, bool saldoDePagaduria,
        string codigoDelTipo, CancellationToken ct)
    {
        _ = saldoDePagaduria;
        if (recibo.Channel is ReceiptChannel.Contingency or ReceiptChannel.CashDesk)
            return await ReciboEmitidoAsync(recibo, aplicaciones, [medio], porciones, naturalezas, null, codigoDelTipo, ct);

        var personas = aplicaciones.Select(a => a.BeneficiaryPersonId).Append(recibo.PayerPersonId).Distinct().ToList();
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var codigos = await CodigosDeConceptosAsync(aplicaciones, ct);
        var dim = await DimensionesDeBancoAsync(recibo.BranchId, recibo.OwnBankAccountPublicId, medio.PaymentMeansId, recibo.Channel.ToString(), ct);
        var rolDelDinero = partidaPublicId is null ? RolesDeRecaudo.BancoRecaudo : RolesDeRecaudo.PartidasPorIdentificar;
        var lineas = new List<FactLineV1>();
        foreach (var p in porciones)
        {
            var a = aplicaciones.First(x => x.Id == p.ReceiptApplicationId);
            lineas.Add(Linea(rolDelDinero, RolesDeRecaudo.Debito, p.Amount, dim, cruce: recibo.Number));
            lineas.Add(a.Target == ApplicationTarget.MiscConcept
                ? Linea(RolesDeRecaudo.ConceptoVario, RolesDeRecaudo.Credito, p.Amount, dim with { MiscConceptCode = codigos.GetValueOrDefault(a.MiscConceptId!.Value) },
                    publicos.GetValueOrDefault(a.BeneficiaryPersonId), a.CrossDocumentReference ?? recibo.Number)
                : Linea(RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.Credito, p.Amount, dim with
                {
                    TargetModule = ModulosDeOrigen.Cartera,
                    TargetNature = Naturaleza(a.Target, a.ProductPublicId is { } prod ? naturalezas.GetValueOrDefault(prod) : null),
                }));
        }
        if (aFavor > 0m)
        {
            lineas.Add(Linea(rolDelDinero, RolesDeRecaudo.Debito, aFavor, dim, cruce: recibo.Number));
            lineas.Add(Linea(RolesDeRecaudo.SaldoAFavorDePagadores, RolesDeRecaudo.Credito, aFavor, dim, publicos[recibo.PayerPersonId], recibo.Number));
        }
        var contenido = new ReciboDeBancoEmitidoV1
        {
            ReceiptPublicId = recibo.PublicId, Number = recibo.Number, Channel = recibo.Channel.ToString(), ValueDate = recibo.ValueDate, MovementDate = recibo.OriginalValueDate,
            BankStatementLinePublicId = recibo.BankStatementLinePublicId, UnidentifiedItemPublicId = partidaPublicId, PayerPersonPublicId = publicos[recibo.PayerPersonId],
            TotalApplied = recibo.TotalApplied, Surplus = aFavor, RegisteredBy = await UsuarioAsync(ct),
            Applications = Aplicaciones(aplicaciones, codigos, publicos), Lines = lineas,
        };
        return await EmitirAsync(contenido, recibo.PublicId, ClaseReciboDeBanco, codigoDelTipo, recibo.Number, recibo.ValueDate, recibo.BranchId,
            publicos[recibo.PayerPersonId], ct);
    }

    /// <summary><c>PartidaPorIdentificarReconocida</c>: <c>BancoRecaudo</c> → <c>PartidasPorIdentificar</c>. Sin guardar.</summary>
    public async Task<IntegrationMessage> PartidaPorIdentificarReconocidaAsync(UnidentifiedItem partida, CancellationToken ct)
    {
        var dim = await DimensionesDeBancoAsync(partida.BranchId, partida.OwnBankAccountPublicId, null, "Bank", ct);
        var contenido = new PartidaPorIdentificarReconocidaV1
        {
            UnidentifiedItemPublicId = partida.PublicId, Number = partida.Number, BankStatementLinePublicId = partida.BankStatementLinePublicId,
            MovementDate = partida.MovementDate, Amount = partida.Amount, Reference = partida.Reference, DepositorName = partida.DepositorName,
            RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.Debito, partida.Amount, dim, cruce: partida.Number),
                Linea(RolesDeRecaudo.PartidasPorIdentificar, RolesDeRecaudo.Credito, partida.Amount, dim, cruce: partida.Number),
            ],
        };
        return await EmitirAsync(contenido, partida.PublicId, ClasePartida, TipoPartida, partida.Number, partida.MovementDate, partida.BranchId, null, ct);
    }

    /// <summary>
    /// <c>PartidaClasificada</c>: desde el abono (<c>BancoRecaudo</c>) o desde una partida (<c>PartidasPorIdentificar</c>) al concepto vario —rendimiento, reintegro, giro
    /// devuelto o la partida vencida llevada a ingreso con su decisión registrada—. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage> PartidaClasificadaAsync(Guid documento, string numero, bool desdePartida, string clase, string codigoDelConcepto, decimal valor,
        DateOnly fecha, int branchId, Guid? cuentaPropia, Guid? ordenDePago, Guid? aprobacion, CancellationToken ct)
    {
        var dim = await DimensionesDeBancoAsync(branchId, cuentaPropia, null, "Bank", ct);
        var contenido = new PartidaClasificadaV1
        {
            DocumentPublicId = documento, Number = numero, Source = desdePartida ? ClasePartida : "BankItem", Kind = clase, MiscConceptCode = codigoDelConcepto, Amount = valor,
            OperationDate = fecha, PaymentOrderPublicId = ordenDePago, ApprovalRequestPublicId = aprobacion, RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(desdePartida ? RolesDeRecaudo.PartidasPorIdentificar : RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.Debito, valor, dim, cruce: numero),
                Linea(RolesDeRecaudo.ConceptoVario, RolesDeRecaudo.Credito, valor, dim with { MiscConceptCode = codigoDelConcepto }, cruce: numero),
            ],
        };
        return await EmitirAsync(contenido, documento, ClaseIdentificacion, TipoClasificacion, numero, fecha, branchId, null, ct);
    }

    /// <summary><c>PartidaDevueltaAlConsignante</c>: <c>PartidasPorIdentificar</c> → <c>PuenteDeDevolucion</c> (el giro lo contabiliza Tesorería). Sin guardar.</summary>
    public async Task<IntegrationMessage> PartidaDevueltaAlConsignanteAsync(UnidentifiedItem partida, UnidentifiedItemResolution resolucion, Guid beneficiario,
        DateOnly fecha, CancellationToken ct)
    {
        var dim = await DimensionesDeBancoAsync(partida.BranchId, partida.OwnBankAccountPublicId, null, "Bank", ct);
        var contenido = new PartidaDevueltaAlConsignanteV1
        {
            UnidentifiedItemPublicId = partida.PublicId, Number = partida.Number, PaymentOrderPublicId = resolucion.DocumentPublicId,
            PaymentOrderNumber = resolucion.DocumentNumber ?? string.Empty, BeneficiaryPersonPublicId = beneficiario, Amount = resolucion.Amount, RefundedOn = fecha,
            RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeRecaudo.PartidasPorIdentificar, RolesDeRecaudo.Debito, resolucion.Amount, dim, cruce: partida.Number),
                Linea(RolesDeRecaudo.PuenteDeDevolucion, RolesDeRecaudo.Credito, resolucion.Amount, dim with { MiscConceptCode = null }, beneficiario, resolucion.DocumentNumber),
            ],
        };
        return await EmitirAsync(contenido, resolucion.PublicId, ClasePartida, TipoPartida, partida.Number, fecha, partida.BranchId, beneficiario, ct);
    }

    private async Task<IReadOnlyDictionary<int, string>> CodigosDeConceptosAsync(IReadOnlyList<ReceiptApplication> aplicaciones, CancellationToken ct)
    {
        var conceptos = aplicaciones.Where(a => a.MiscConceptId is not null).Select(a => a.MiscConceptId!.Value).Distinct().ToList();
        return conceptos.Count == 0
            ? new Dictionary<int, string>()
            : await db.MiscConcepts.AsNoTracking().IgnoreQueryFilters().Where(c => conceptos.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct);
    }

    private static IReadOnlyList<AplicacionDeReciboV1> Aplicaciones(IReadOnlyList<ReceiptApplication> aplicaciones, IReadOnlyDictionary<int, string> codigos,
        IReadOnlyDictionary<int, Guid> publicos) =>
        aplicaciones.OrderBy(a => a.Ordinal).Select(a => new AplicacionDeReciboV1
        {
            Ordinal = a.Ordinal, Target = a.Target.ToString(), ProductPublicId = a.ProductPublicId, ProductNumber = a.ProductNumber,
            MiscConceptCode = a.MiscConceptId is { } c ? codigos.GetValueOrDefault(c) : null, BeneficiaryPersonPublicId = publicos.GetValueOrDefault(a.BeneficiaryPersonId),
            Amount = a.Amount, ModuleVoucherNumber = a.ModuleVoucherNumber,
        }).ToList();
}
