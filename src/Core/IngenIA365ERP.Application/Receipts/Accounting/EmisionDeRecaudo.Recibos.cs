using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Accounting;

// Feature 015, US3 (T0116; FR-141; contracts/contabilidad.md §3, §7): el hecho ReciboEmitido, una línea por porción de medio.
public sealed partial class EmisionDeRecaudo
{
    public const string ClaseRecibo = "Receipt";

    /// <summary>El tipo raíz de la <c>ScheduleKey</c> de los recibos de caja: <c>REC.RCE|CierreDeTurno||PorDia</c>.</summary>
    public const string RaizDeLoteDeRecibos = "REC.RCE";

    /// <summary>La naturaleza del puente por clase de aplicación (<c>TargetNature</c>).</summary>
    public static string Naturaleza(ApplicationTarget destino, string? naturalezaDeCuenta) => destino switch
    {
        ApplicationTarget.Loan => "Loan",
        ApplicationTarget.Account => naturalezaDeCuenta ?? "Deposit",
        ApplicationTarget.CommercialReceivable => "Commercial",
        _ => string.Empty,
    };

    /// <summary>Feature 015, R1 (T0357): la persona del adquirente de cada medio de tarjeta del recibo (las demás clases no llevan tercero en el débito).</summary>
    private async Task<IReadOnlyDictionary<int, Guid?>> AdquirentesDeLosMediosAsync(IReadOnlyList<ReceiptMeans> medios, CancellationToken ct)
    {
        var tarjetas = medios.Where(m => m.MeansClass is PaymentMeansClass.CreditCard or PaymentMeansClass.DebitCard).Select(m => m.PaymentMeansId).Distinct().ToList();
        if (tarjetas.Count == 0) return new Dictionary<int, Guid?>();
        return await (from m in db.PaymentMeans.AsNoTracking().IgnoreQueryFilters()
                      join a in db.CardAcquirers.AsNoTracking().IgnoreQueryFilters() on m.CardAcquirerId equals a.Id
                      join p in db.People.AsNoTracking().IgnoreQueryFilters() on a.PersonId equals p.Id
                      where tarjetas.Contains(m.Id)
                      select new { m.Id, p.PublicId }).ToDictionaryAsync(x => x.Id, x => (Guid?)x.PublicId, ct);
    }

    /// <summary>
    /// <c>ReciboEmitido</c>: por cada porción, débito al rol del medio y crédito al puente del módulo o al concepto vario. Modo por defecto <c>PorLotes</c>
    /// (lotes resumidos por día, punto y medio, D25) con alcance la sesión; sin contabilidad iniciada, <c>NotPosted</c>. Sin guardar.
    /// </summary>
    /// <param name="naturalezas">La naturaleza de cada cuenta de las aplicaciones (<c>Deposit</c> o <c>Contribution</c>), por su <c>PublicId</c>.</param>
    public async Task<IntegrationMessage> ReciboEmitidoAsync(Receipt recibo, IReadOnlyList<ReceiptApplication> aplicaciones, IReadOnlyList<ReceiptMeans> medios,
        IReadOnlyList<ReceiptApplicationPortion> porciones, IReadOnlyDictionary<Guid, string> naturalezas, Guid? sesionPublicId, string codigoDelTipo,
        CancellationToken ct)
    {
        var personas = aplicaciones.Select(a => a.BeneficiaryPersonId).Append(recibo.PayerPersonId).Distinct().ToList();
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var conceptos = aplicaciones.Where(a => a.MiscConceptId is not null).Select(a => a.MiscConceptId!.Value).Distinct().ToList();
        var codigos = await db.MiscConcepts.AsNoTracking().Where(c => conceptos.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var lineas = new List<FactLineV1>();
        // Feature 015, R1 (T0357; contabilidad.md §5): el medio por cobrar de una tarjeta lleva como tercero al adquirente (la persona que liquida el lote).
        var adquirentes = await AdquirentesDeLosMediosAsync(medios, ct);
        foreach (var p in porciones)
        {
            var aplicacion = aplicaciones.First(a => a.Id == p.ReceiptApplicationId);
            var medio = medios.First(m => m.Id == p.ReceiptMeansId);
            var dimMedio = recibo.CashBoxId is { } caja
                ? await DimensionesDeCajaAsync(caja, medio.PaymentMeansId, ct)
                : await DimensionesDeOficinaAsync(recibo.BranchId, ct);
            lineas.Add(Linea(RolDelMedio(medio.MeansClass), RolesDeRecaudo.Debito, p.Amount, dimMedio, adquirentes.GetValueOrDefault(medio.PaymentMeansId),
                cruce: recibo.Number));
            var tercero = publicos.GetValueOrDefault(aplicacion.BeneficiaryPersonId);
            if (aplicacion.Target == ApplicationTarget.MiscConcept)
                lineas.Add(Linea(RolesDeRecaudo.ConceptoVario, RolesDeRecaudo.Credito, p.Amount,
                    dimMedio with { MiscConceptCode = codigos.GetValueOrDefault(aplicacion.MiscConceptId!.Value) }, tercero, aplicacion.CrossDocumentReference ?? recibo.Number));
            else if (aplicacion.Target == ApplicationTarget.CommercialReceivable)
                // Feature 015, R1 (T0397; contabilidad.md §5): la cuenta por cobrar comercial, con el tercero y la factura de cruce.
                lineas.Add(Linea(RolesDeRecaudo.CuentasPorCobrarComerciales, RolesDeRecaudo.Credito, p.Amount, dimMedio, tercero,
                    aplicacion.CrossDocumentReference ?? aplicacion.ProductNumber ?? recibo.Number));
            else
                lineas.Add(Linea(RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.Credito, p.Amount, dimMedio with
                {
                    TargetModule = ModulosDeOrigen.Cartera,
                    TargetNature = Naturaleza(aplicacion.Target, aplicacion.ProductPublicId is { } prod ? naturalezas.GetValueOrDefault(prod) : null),
                }));
        }
        var contenido = new ReciboEmitidoV1
        {
            ReceiptPublicId = recibo.PublicId,
            Number = recibo.Number,
            Channel = recibo.Channel.ToString(),
            ValueDate = recibo.ValueDate,
            RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(recibo.RegisteredAt, DateTimeKind.Utc)),
            PayerPersonPublicId = publicos[recibo.PayerPersonId],
            PayerIsThirdParty = recibo.PayerIsThirdParty,
            SessionPublicId = sesionPublicId,
            TotalApplied = recibo.TotalApplied,
            ChangeGiven = recibo.ChangeGiven,
            RegisteredBy = await UsuarioAsync(ct),
            Applications = aplicaciones.OrderBy(a => a.Ordinal).Select(a => new AplicacionDeReciboV1
            {
                Ordinal = a.Ordinal,
                Target = a.Target.ToString(),
                ProductPublicId = a.ProductPublicId,
                ProductNumber = a.ProductNumber,
                MiscConceptCode = a.MiscConceptId is { } c ? codigos.GetValueOrDefault(c) : null,
                BeneficiaryPersonPublicId = publicos.GetValueOrDefault(a.BeneficiaryPersonId),
                Amount = a.Amount,
                ModuleVoucherNumber = a.ModuleVoucherNumber,
            }).ToList(),
            Lines = lineas,
        };

        var (fecha, modoEnLinea) = await FechaYModoAsync(recibo.ValueDate, ct);
        ModoDeEntrega modo = modoEnLinea;
        if (modoEnLinea is ModoDeEntrega.Sellado { Modo: DeliveryMode.Online })
        {
            var leido = await lector.LeerAsync(ParametrosDeRecaudoYTesoreria.ModuloRecaudo, ParametrosDeRecaudoYTesoreria.ContabilidadModoDePaso, fecha, ct: ct);
            var texto = leido.IsSuccess ? leido.Value.Texto : "PorLotes";
            if (texto != "EnLinea")
                modo = new ModoDeEntrega.Sellado(DeliveryMode.Batch,
                    ClavesDeLote.Horario(RaizDeLoteDeRecibos, ClavesDeLote.CierreDeTurno, null, ClavesDeLote.GranularidadPorDia),
                    sesionPublicId is { } s ? ClavesDeLote.SesionDeCaja(s) : string.Create(CultureInfo.InvariantCulture, $"ReceiptsDay:{fecha:yyyy-MM-dd}"));
        }
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == recibo.BranchId).Select(b => b.PublicId).FirstAsync(ct);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, recibo.PublicId, ClaseRecibo, codigoDelTipo, recibo.Number, fecha, oficina,
                PersonPublicId: publicos[recibo.PayerPersonId]),
            ClavesDeEvento.Confirmacion,
            [contenido],
            modo), ct);
        return mensajes[0];
    }
}
