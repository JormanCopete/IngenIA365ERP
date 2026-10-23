using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.PersonBankAccounts;

/// <summary>Una cuenta activa de la persona (interfaces.md §5). (nuevo)</summary>
public sealed record CuentaBancariaDePersonaDto(
    Guid PublicId,
    string BankName,
    string? BankTransferCode,
    string AccountType,
    string AccountNumber,
    string HolderName,
    DateOnly? PayableFrom,
    bool Imported);

/// <summary>
/// Lo que dice la verificación de una cuenta destino (interfaces.md §5; FR-210; SC-026): si está registrada y activa para la persona, desde
/// cuándo se le puede girar, si cambió hace pocos días, si la comparte con otro beneficiario o con un usuario interno, y si es importada sin
/// certificación. Tesorería decide qué hace con cada marca (rechazar la orden, avisar o pedir revisión). (nuevo)
/// </summary>
public sealed record CoincidenciaDeCuentaDto(
    bool Registered,
    Guid? AccountPublicId,
    DateOnly? PayableFrom,
    bool Waiting,
    bool ChangedRecently,
    int? DaysSinceChange,
    bool SharedWithOtherBeneficiary,
    bool SharedWithInternalUser,
    bool ImportedWithoutCertificate);

/// <summary>
/// Las cuentas bancarias de la persona vistas por quien gira (feature 015, E-06, T0030; contracts/interfaces.md §5): las activas y la
/// verificación de una cuenta destino. Las escriben sólo los comandos de esta carpeta, con doble control. (nuevo)
/// </summary>
public interface ICuentasBancariasDePersona
{
    Task<IReadOnlyList<CuentaBancariaDePersonaDto>> ActivasAsync(Guid personPublicId, CancellationToken ct);

    Task<CoincidenciaDeCuentaDto> VerificarAsync(Guid personPublicId, string bankCode, string accountNumber, CancellationToken ct);
}

/// <summary>
/// La implementación de <see cref="ICuentasBancariasDePersona"/> (T0030). <c>bankCode</c> es el código ACH del banco (<c>COR_Banks.TransferCode</c>)
/// o su código heredado. «Cambiada hace N días» se mide desde la aprobación de la fila vigente cuando reemplaza a otra, con
/// <c>CuentasDestino.DiasDeMarca</c> (sin valor, sin marca). «Igual a la de un usuario interno»: la misma cuenta está registrada para una
/// persona que es usuario de la cooperativa (<c>SEC_Users.PersonId</c>). (nuevo)
/// </summary>
public sealed class CuentasBancariasDePersona(IApplicationDbContext db, ILectorDeParametros parametros, IDateTimeService reloj) : ICuentasBancariasDePersona
{
    public async Task<IReadOnlyList<CuentaBancariaDePersonaDto>> ActivasAsync(Guid personPublicId, CancellationToken ct)
    {
        var persona = await PersonaAsync(personPublicId, ct);
        if (persona is null) return [];
        return await db.PersonBankAccounts.AsNoTracking()
            .Where(c => c.PersonId == persona && c.Status == PersonBankAccountState.Active)
            .OrderByDescending(c => c.Id)
            .Select(c => new CuentaBancariaDePersonaDto(c.PublicId, c.Bank!.Name, c.Bank.TransferCode, c.AccountType, c.AccountNumber, c.HolderName,
                c.PayableFrom, c.Origin == PersonBankAccountOrigin.Imported))
            .ToListAsync(ct);
    }

    public async Task<CoincidenciaDeCuentaDto> VerificarAsync(Guid personPublicId, string bankCode, string accountNumber, CancellationToken ct)
    {
        var numero = CuentasBancariasDePersonaComun.Normalizar(accountNumber ?? string.Empty);
        var codigo = bankCode?.Trim() ?? string.Empty;
        var bancos = await db.Banks.AsNoTracking().Where(b => b.TransferCode == codigo || b.LegacyCode == codigo).Select(b => b.Id).ToListAsync(ct);
        var persona = await PersonaAsync(personPublicId, ct);
        if (persona is null || bancos.Count == 0 || numero.Length == 0)
            return new CoincidenciaDeCuentaDto(false, null, null, false, false, null, false, false, false);

        var vigentes = await db.PersonBankAccounts.AsNoTracking()
            .Where(c => bancos.Contains(c.BankId) && c.AccountNumber == numero
                        && (c.Status == PersonBankAccountState.Active || c.Status == PersonBankAccountState.PendingApproval))
            .Select(c => new { c.PublicId, c.PersonId, c.Status, c.PayableFrom, c.DecidedAt, c.ReplacesId, c.Origin, c.CertificateAttachmentPublicId })
            .ToListAsync(ct);

        var propia = vigentes.Where(c => c.PersonId == persona && c.Status == PersonBankAccountState.Active).OrderByDescending(c => c.DecidedAt).FirstOrDefault();
        var otras = vigentes.Where(c => c.PersonId != persona).Select(c => c.PersonId).Distinct().ToList();
        var usuarioInterno = otras.Count > 0 && await db.Users.AsNoTracking().AnyAsync(u => u.PersonId != null && otras.Contains(u.PersonId.Value), ct);

        var hoy = reloj.HoyLocal;
        int? dias = null;
        var cambiada = false;
        if (propia is { ReplacesId: not null, DecidedAt: { } aprobada })
        {
            dias = hoy.DayNumber - DateOnly.FromDateTime(aprobada).DayNumber;
            var marca = await parametros.LeerComoAsync<int?>(ParametrosDeRecaudoYTesoreria.ModuloTesoreria, ParametrosDeRecaudoYTesoreria.CuentasDestinoDiasDeMarca, hoy, ct: ct);
            cambiada = marca.IsSuccess && marca.Value is { } diasDeMarca && dias < diasDeMarca;
        }

        return new CoincidenciaDeCuentaDto(
            Registered: propia is not null,
            AccountPublicId: propia?.PublicId,
            PayableFrom: propia?.PayableFrom,
            Waiting: propia is not null && (propia.PayableFrom is null || propia.PayableFrom > hoy),
            ChangedRecently: cambiada,
            DaysSinceChange: dias,
            SharedWithOtherBeneficiary: otras.Count > 0,
            SharedWithInternalUser: usuarioInterno,
            ImportedWithoutCertificate: propia is { Origin: PersonBankAccountOrigin.Imported, CertificateAttachmentPublicId: null });
    }

    private Task<int?> PersonaAsync(Guid publicId, CancellationToken ct) =>
        db.People.AsNoTracking().Where(p => p.PublicId == publicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
}
