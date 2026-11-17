using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.PersonBankAccounts;

/// <summary>
/// Una cuenta bancaria pedida que espera aprobación, como la ve la lista de pendientes: la persona, el banco, el tipo y sólo los cuatro
/// últimos dígitos del número (el número completo se ve en las cuentas de la persona, donde se aprueba).
/// </summary>
public sealed record CuentaBancariaPorAprobarDto(
    Guid PublicId,
    Guid PersonPublicId,
    string PersonName,
    string PersonTaxId,
    string BankName,
    string AccountType,
    string AccountNumberLast4,
    bool HasCertificate,
    string RequestedByName,
    DateTime CreatedAt);

/// <summary>
/// <c>GET /api/core/person-bank-accounts/pending</c> (<c>Core.PersonBankAccounts.Approve</c>) (2026-11-17, pedido del dueño): las cuentas
/// bancarias de personas que esperan aprobación, de la más antigua a la más nueva. Hasta ese día no había una lista: había que ir persona
/// por persona. La lectura queda en la auditoría como la de las cuentas de una persona.
/// </summary>
public sealed record ListPendingPersonBankAccountsQuery : IRequest<Result<IReadOnlyList<CuentaBancariaPorAprobarDto>>>;

public sealed class ListPendingPersonBankAccountsQueryHandler(IApplicationDbContext db, IServiceProvider servicios)
    : IRequestHandler<ListPendingPersonBankAccountsQuery, Result<IReadOnlyList<CuentaBancariaPorAprobarDto>>>
{
    public const int Maximo = 500;

    public async Task<Result<IReadOnlyList<CuentaBancariaPorAprobarDto>>> Handle(ListPendingPersonBankAccountsQuery request, CancellationToken ct)
    {
        var filas = await (from c in db.PersonBankAccounts.AsNoTracking()
                           where !c.IsDeleted && c.Status == PersonBankAccountState.PendingApproval
                           join p in db.People.AsNoTracking().IgnoreQueryFilters() on c.PersonId equals p.Id
                           join b in db.Banks.AsNoTracking().IgnoreQueryFilters() on c.BankId equals b.Id
                           orderby c.CreatedAt, c.Id
                           select new { Cuenta = c, Persona = p, Banco = b.Name })
            .Take(Maximo)
            .ToListAsync(ct);

        var lista = filas.Select(f => new CuentaBancariaPorAprobarDto(
                f.Cuenta.PublicId, f.Persona.PublicId,
                !string.IsNullOrWhiteSpace(f.Persona.BusinessName) ? f.Persona.BusinessName! : NombreDePersona.Completo(f.Persona),
                f.Persona.TaxId, f.Banco, f.Cuenta.AccountType, UltimosCuatro(f.Cuenta.AccountNumber), f.Cuenta.CertificateAttachmentPublicId is not null,
                f.Cuenta.RequestedByName, f.Cuenta.CreatedAt))
            .ToList();

        await AuditoriaEncadenada.RegistrarAsync(servicios, new AuditLogCommand
        {
            Action = CuentasBancariasDePersonaComun.EventoLeida,
            EntityType = nameof(PersonBankAccount),
            EntityId = "pending",
            Module = ModuloDeAuditoria.Treasury,
            NewValues = new { pending = lista.Count },
        }, ct);
        return Result.Success<IReadOnlyList<CuentaBancariaPorAprobarDto>>(lista);
    }

    public static string UltimosCuatro(string numero) => numero.Length <= 4 ? numero : "…" + numero[^4..];
}
