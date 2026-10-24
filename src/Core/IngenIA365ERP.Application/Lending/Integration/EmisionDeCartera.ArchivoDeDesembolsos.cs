using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Feature 015, US10 (T0267; E-01 c, enmienda C10 de la 013; contratos-v2.md §2.3): el archivo de desembolsos de Cartera viaja a Tesorería —generado y anulado—
/// para que Tesorería registre resultados y concilie (D16). Origen el archivo, clave <c>Confirmation:{archivo:N}</c> (el tipo distingue los dos), modo
/// <c>Always</c> (no pasa a Contabilidad), a nombre de quien genera o anula. Sin guardar: nace en el mismo guardado que el archivo. (nuevo)
/// </summary>
public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen del archivo de desembolsos.</summary>
    public const string ClaseArchivoDeDesembolsos = "DisbursementFile";

    public async Task<IntegrationMessage> ArchivoDeDesembolsosAsync(DisbursementFile archivo, bool anulado, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(archivo);
        var lineas = await (from l in db.DisbursementFileLines.AsNoTracking()
                            join o in db.DisbursementOrders.AsNoTracking() on l.OrderId equals o.Id
                            join p in db.People.AsNoTracking().IgnoreQueryFilters() on o.BeneficiaryPersonId equals p.Id
                            where l.FileId == archivo.Id
                            orderby l.LineNumber
                            select new { o.PublicId, o.Number, o.LoanId, Neto = l.Amount, Persona = p.PublicId }).ToListAsync(ct);
        var oficina = lineas.Count == 0 ? (int?)null
            : await db.Loans.AsNoTracking().Where(c => c.Id == lineas[0].LoanId).Select(c => (int?)c.BranchId).FirstOrDefaultAsync(ct);
        var sucursal = oficina is { } of ? await SucursalContableAsync(of, ct) : null;
        var sucursalPublicId = sucursal is { IsSuccess: true } s ? s.Value
            : await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).OrderBy(b => b.Id).Select(b => b.PublicId).FirstAsync(ct);
        var tamano = await db.Attachments.AsNoTracking().IgnoreQueryFilters().Where(a => a.PublicId == archivo.FileAttachmentPublicId).Select(a => (long?)a.SizeBytes)
            .FirstOrDefaultAsync(ct) ?? 0L;
        var banco = await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == archivo.BankId).Select(b => b.TransferCode).FirstOrDefaultAsync(ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var usuario = new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name };
        var adjunto = new ArchivoDeDesembolsosAdjuntoV1
        {
            AttachmentPublicId = archivo.FileAttachmentPublicId, FileName = archivo.FileName, Sha256 = archivo.FileSha256, SizeBytes = tamano, FormatCode = archivo.FormatCode,
        };
        var cuenta = new CuentaDeOrigenDelArchivoV1 { BankCode = banco, AccountNumber = archivo.SourceAccountNumber };
        var ordenes = lineas.Select(l => new OrdenDelArchivoV1 { OrderPublicId = l.PublicId, OrderNumber = l.Number, NetAmount = l.Neto, PayeePersonPublicId = l.Persona })
            .ToList();
        object contenido = anulado
            ? new ArchivoDeDesembolsosAnuladoV1
            {
                FilePublicId = archivo.PublicId, FileNumber = archivo.Number, File = adjunto, SourceBankAccount = cuenta, Orders = ordenes, Total = archivo.TotalAmount,
                LineCount = archivo.LineCount, GeneratedBy = usuario, Reason = archivo.VoidReason ?? string.Empty,
            }
            : new ArchivoDeDesembolsosGeneradoV1
            {
                FilePublicId = archivo.PublicId, FileNumber = archivo.Number, File = adjunto, SourceBankAccount = cuenta, Orders = ordenes, Total = archivo.TotalAmount,
                LineCount = archivo.LineCount, GeneratedBy = usuario,
            };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, archivo.PublicId, ClaseArchivoDeDesembolsos, Prefijo(archivo.Number), archivo.Number, archivo.PaymentDate,
                sucursalPublicId),
            ClavesDeEvento.ConfirmacionPor(archivo.PublicId), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always),
            Relacionado: lineas.Count == 0 ? null : new DocumentoRelacionado(lineas[0].PublicId, ClaseOrdenDeGiro, lineas[0].Number)), ct);
        return mensajes[0];
    }
}
