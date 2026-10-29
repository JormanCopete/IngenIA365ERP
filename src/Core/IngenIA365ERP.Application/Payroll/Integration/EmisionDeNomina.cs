using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Payroll;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Integration;

/// <summary>
/// Lo que Nómina emite por la bandeja (feature 015, T2, fase 41, T0448; E-05; FR-168; contratos-v2.md §5): <c>DispersionDeNominaGenerada</c> v1 a Tesorería al generar un
/// archivo de dispersión, con origen el archivo, clave <c>Confirmation:{archivo:N}</c>, modo <c>Always</c> y a nombre de quien genera. Nómina sigue pagando con su archivo
/// (D27): el mensaje es informativo para la posición de liquidez y la conciliación de Tesorería. Sin guardar: nace en el mismo guardado que el archivo. (nuevo)
/// </summary>
public sealed class EmisionDeNomina(IApplicationDbContext db, EmisorDeMensajes emisor, IActorActual actorActual)
{
    public const string ClaseArchivoDeDispersion = "PayrollDisbursementFile";
    public const string TipoDeDocumento = "DSP";

    public async Task<IntegrationMessage> DispersionGeneradaAsync(BankDisbursementFile archivo, Guid runPublicId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(archivo);
        var actor = await actorActual.ObtenerAsync(ct);
        var banco = archivo.BankId is { } b
            ? await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == b).Select(x => x.TransferCode).FirstOrDefaultAsync(ct)
            : null;
        var oficina = await db.Branches.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Id).Select(x => x.PublicId).FirstAsync(ct);
        var numero = archivo.FileName ?? archivo.PublicId.ToString("N")[..12];
        var contenido = new DispersionDeNominaGeneradaV1
        {
            FilePublicId = archivo.PublicId, BankAccount = new CuentaDeOrigenDeLaDispersionV1 { BankCode = banco, AccountNumber = archivo.SourceAccountNumber },
            Total = archivo.TotalAmount, LineCount = archivo.LineCount, PayDate = archivo.PaymentDate, RunPublicId = runPublicId,
            GeneratedBy = new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name },
        };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, archivo.PublicId, ClaseArchivoDeDispersion, TipoDeDocumento, numero.Length <= 40 ? numero : numero[..40],
                archivo.PaymentDate, oficina),
            ClavesDeEvento.ConfirmacionPor(archivo.PublicId), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always)), ct);
        return mensajes[0];
    }
}
