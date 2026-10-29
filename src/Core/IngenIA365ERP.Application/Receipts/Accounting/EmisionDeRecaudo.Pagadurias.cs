using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Paymasters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Accounting;

// Feature 015, US7 (T0209; FR-078 a FR-080; contracts/contabilidad.md §3): los hechos de la pagaduría, en línea y sin guardar: el giro recibido y el recibo de canal pagaduría de cada
// línea aplicada. Lo que la pagaduría giró y no se aplicó (excedente o línea rechazada) sigue en SaldoDePagadurias: no emite hecho propio.
public sealed partial class EmisionDeRecaudo
{
    public const string ClaseGiroDePagaduria = "PaymasterRemittance";
    public const string ClaseReciboDePagaduria = "PaymasterReceipt";
    public const string TipoGiroDePagaduria = "GP";

    /// <summary><c>GiroDePagaduriaRecibido</c>: <c>BancoRecaudo</c> (giro identificado en el extracto) o <c>Caja</c> (recibido en caja) → <c>SaldoDePagadurias</c>.</summary>
    public async Task<IntegrationMessage> GiroDePagaduriaRecibidoAsync(PaymasterRemittance giro, CancellationToken ct)
    {
        var dim = await DimensionesDeBancoAsync(giro.BranchId, giro.OwnBankAccountPublicId, null, "Paymaster", ct);
        var pagaduria = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == giro.PaymasterPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var rolDelDinero = giro.Source == PaymasterRemittanceSource.BankItem ? RolesDeRecaudo.BancoRecaudo : RolesDeRecaudo.Caja;
        var contenido = new GiroDePagaduriaRecibidoV1
        {
            RemittancePublicId = giro.PublicId, Number = giro.Number, PaymasterPersonPublicId = pagaduria, Source = giro.Source.ToString(),
            BankStatementLinePublicId = giro.BankStatementLinePublicId, Amount = giro.Amount, RemittedOn = giro.RemittedOn, RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(rolDelDinero, RolesDeRecaudo.Debito, giro.Amount, dim, cruce: giro.Number),
                Linea(RolesDeRecaudo.SaldoDePagadurias, RolesDeRecaudo.Credito, giro.Amount, dim, pagaduria, giro.Number),
            ],
        };
        return await EmitirAsync(contenido, giro.PublicId, ClaseGiroDePagaduria, TipoGiroDePagaduria, giro.Number, giro.RemittedOn, giro.BranchId, pagaduria, ct);
    }

    /// <summary><c>ReciboDePagaduriaEmitido</c>: por porción, <c>SaldoDePagadurias</c> → el puente del módulo. Sin guardar.</summary>
    public async Task<IntegrationMessage> ReciboDePagaduriaEmitidoAsync(Receipt recibo, IReadOnlyList<ReceiptApplication> aplicaciones, ReceiptMeans medio,
        IReadOnlyList<ReceiptApplicationPortion> porciones, IReadOnlyDictionary<Guid, string> naturalezas, string codigoDelTipo, CancellationToken ct)
    {
        var personas = aplicaciones.Select(a => a.BeneficiaryPersonId).Append(recibo.PayerPersonId).Distinct().ToList();
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var codigos = await CodigosDeConceptosAsync(aplicaciones, ct);
        var dim = await DimensionesDeBancoAsync(recibo.BranchId, recibo.OwnBankAccountPublicId, medio.PaymentMeansId, "Paymaster", ct);
        var pagaduria = publicos[recibo.PayerPersonId];
        var lineas = new List<FactLineV1>();
        foreach (var p in porciones)
        {
            var a = aplicaciones.First(x => x.Id == p.ReceiptApplicationId);
            lineas.Add(Linea(RolesDeRecaudo.SaldoDePagadurias, RolesDeRecaudo.Debito, p.Amount, dim, pagaduria, recibo.Number));
            lineas.Add(Linea(RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.Credito, p.Amount, dim with
            {
                TargetModule = ModulosDeOrigen.Cartera,
                TargetNature = Naturaleza(a.Target, a.ProductPublicId is { } prod ? naturalezas.GetValueOrDefault(prod) : null),
            }));
        }
        var contenido = new ReciboDePagaduriaEmitidoV1
        {
            ReceiptPublicId = recibo.PublicId, Number = recibo.Number, PaymasterPersonPublicId = pagaduria, ValueDate = recibo.ValueDate, TotalApplied = recibo.TotalApplied,
            RegisteredBy = await UsuarioAsync(ct), Applications = Aplicaciones(aplicaciones, codigos, publicos), Lines = lineas,
        };
        return await EmitirAsync(contenido, recibo.PublicId, ClaseReciboDePagaduria, codigoDelTipo, recibo.Number, recibo.ValueDate, recibo.BranchId, pagaduria, ct);
    }

    /// <summary>
    /// Un mensaje de Recaudo a Cartera sobre un archivo de descuentos (<c>EnvioDeDescuentosConfirmado</c>, <c>ResultadoDeDescuentos</c>; mensajes.md §4): origen el documento
    /// de Recaudo que lo motiva (el envío o la respuesta), clave <c>Confirmation:{archivo:N}</c>, <c>related</c> = el archivo de Cartera, tercero la pagaduría y siempre
    /// <c>Always</c>: no es un hecho contable. A nombre de la persona. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage> MensajeDeLibranzaAsync(PayrollDeductionFile archivo, Guid origen, string clase, string numero, object contenido, DateOnly fecha,
        CancellationToken ct)
    {
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == archivo.BranchId).Select(b => b.PublicId).FirstAsync(ct);
        var pagaduria = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == archivo.PaymasterPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, origen, clase, ClavesDeRecaudo.Codigo, numero.Length > 40 ? numero[..40] : numero, fecha, oficina,
                PersonPublicId: pagaduria),
            ClavesDeEvento.ConfirmacionPor(archivo.FilePublicId), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always),
            Relacionado: new DocumentoRelacionado(archivo.FilePublicId, ClaseArchivoDeDescuentosDeCartera, archivo.FileNumber),
            UsuarioDeOrigen: await UsuarioAsync(ct)), ct);
        return mensajes[0];
    }

    /// <summary>La clase del archivo de descuentos en Cartera (el <c>related</c> de los mensajes de libranza).</summary>
    public const string ClaseArchivoDeDescuentosDeCartera = "PayrollDeductionFile";
}
