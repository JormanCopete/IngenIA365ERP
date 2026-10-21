using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;

/// <summary>
/// <c>LND_GmfExemptAccounts</c> (feature 014, US2, T072; data-model §3.8; FR-022, FR-036, D32, R07): la cuenta de ahorro que su titular marcó exenta
/// del GMF, con vigencia. Sólo aplica con la política <c>SingleMarkedAccount</c> (<c>Gmf.PoliticaDeExencion</c>): <b>una sola marca vigente por
/// persona</b> en la cooperativa (<c>UK_LND_GmfExemptAccounts_LivePerson</c>); marcar otra se rechaza con <see cref="CodigoYaMarcada"/> nombrando la
/// vigente. Cambiar la marca es cerrar la vigente (<see cref="Terminar"/>) y abrir otra en el mismo comando; nunca se borra. Estado con fechas, no
/// hecho. Escribe sólo quien tiene <c>Lending.GmfExemptions.Manage</c> (o la apertura que la pide con la cuenta). (nuevo)
/// </summary>
public class GmfExemptAccount : AuditableEntity
{
    /// <summary>La persona ya tiene una cuenta exenta vigente (api.md §1.8).</summary>
    public const string CodigoYaMarcada = "Lending.Gmf.AlreadyMarked";

    /// <summary>Sólo una cuenta de ahorro (no CDAT, no aportes) del titular se marca (api.md §10).</summary>
    public const string CodigoNoEsAhorro = "Lending.Gmf.AccountNotSavings";

    /// <summary>Largo de los motivos (<c>nvarchar(300)</c>).</summary>
    public const int LargoDelMotivo = 300;

    /// <summary>El titular de la cuenta.</summary>
    public int PersonId { get; set; }

    /// <summary>Cuenta de ahorro (a la vista, contractual o permanente) del titular.</summary>
    public int MemberAccountId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>Nulo en la migración (lo hace el proceso).</summary>
    public int? MarkedByUserId { get; set; }

    public string MarkReason { get; set; } = string.Empty;

    public int? EndedByUserId { get; set; }

    public string? EndReason { get; set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    /// <summary>¿Rige el <paramref name="dia"/>? (desde incluido, hasta incluido).</summary>
    public bool VigenteEn(DateOnly dia) => !IsDeleted && ValidFrom <= dia && (ValidTo is null || dia <= ValidTo);

    /// <summary>Nulo si la cuenta se puede marcar: una cuenta de ahorro (no CDAT ni aportes) que no esté cancelada.</summary>
    public static ProblemaDeCartera? ValidarCuenta(MemberAccount cuenta)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        if (cuenta.Nature != LineNature.Deposit || cuenta.DepositClass == DepositClass.TermDeposit)
            return ProblemaDeCartera.Con(CodigoNoEsAhorro,
                "Sólo una cuenta de ahorro (a la vista, contractual o permanente) se marca exenta del GMF: un CDAT o una cuenta de aportes no.",
                "accountPublicId", cuenta.PublicId);
        return cuenta.AdmiteOperaciones();
    }

    /// <summary>La marca nueva de <paramref name="cuenta"/> desde <paramref name="desde"/>, con su motivo.</summary>
    public static GmfExemptAccount Marcar(MemberAccount cuenta, DateOnly desde, string motivo, int? usuario)
    {
        ReglaDeCarteraException.SiHay(ValidarCuenta(cuenta));
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        return new GmfExemptAccount
        {
            PersonId = cuenta.HolderPersonId,
            MemberAccountId = cuenta.Id,
            ValidFrom = desde,
            MarkedByUserId = usuario,
            MarkReason = Recortar(motivo),
        };
    }

    /// <summary>
    /// Cierra la vigencia el <paramref name="hasta"/> (incluido). Una marca que se cambia el mismo día en que empezó cierra la víspera: no rigió nunca.
    /// </summary>
    public void Terminar(DateOnly hasta, string motivo, int? usuario)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (ValidTo is not null)
            throw new InvalidOperationException($"La marca de exención ya terminó el {ValidTo:yyyy-MM-dd}.");
        if (hasta < ValidFrom.AddDays(-1))
            throw new ArgumentOutOfRangeException(nameof(hasta), hasta, "La marca no termina antes de empezar.");
        ValidTo = hasta;
        EndedByUserId = usuario;
        EndReason = Recortar(motivo);
    }

    private static string Recortar(string texto)
    {
        var t = texto.Trim();
        return t.Length <= LargoDelMotivo ? t : t[..LargoDelMotivo];
    }
}
