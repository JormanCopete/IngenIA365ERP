using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.BulkCharges;

/// <summary>
/// <c>LND_BulkChargeLines</c> (feature 026): un asociado de la corrida de cargos masivos, con lo que se le carga (valor, cuotas, primer pago, forma de pago y
/// convenio), su resultado y su motivo, y la obligación creada. <see cref="HoldsPeriod"/> es verdadero desde que la corrida se confirma mientras la línea
/// está pendiente o creada (un borrador no reserva nada): un índice único filtrado por línea de crédito, persona y período impide un segundo cargo del
/// mismo período. Sin diferencias de auditoría: son muchas y el evento es el de la corrida. (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class BulkChargeLine : AuditableEntity
{
    public int RunId { get; init; }

    /// <summary>El deudor (<c>COR_People</c>).</summary>
    public int PersonId { get; init; }

    /// <summary>Copiados de la corrida para la unicidad.</summary>
    public int CreditLineId { get; init; }

    public int Period { get; init; }

    /// <summary>La agencia (oficina) del asociado: la de la obligación.</summary>
    public int BranchId { get; set; }

    public decimal Amount { get; set; }

    public int Installments { get; set; }

    public DateOnly FirstDueDate { get; set; }

    public RepaymentChannel RepaymentChannel { get; set; }

    /// <summary>El convenio de nómina propia o de la pagaduría con que se descuenta (<c>LND_PayrollAgreements</c>).</summary>
    public int? PayrollAgreementId { get; set; }

    public BulkChargeLineStatus Status { get; private set; } = BulkChargeLineStatus.Pending;

    /// <summary>El código del motivo (<see cref="MotivosDeCargoMasivo"/>) de una omisión, falla o anulación.</summary>
    public string? Reason { get; private set; }

    public string? ReasonDetail { get; private set; }

    /// <summary>Un aviso que no impide el cargo (p. ej. la forma de pago de la ficha que la línea no admite).</summary>
    public string? Warning { get; set; }

    /// <summary>La obligación creada (<c>LND_Loans</c>).</summary>
    public int? LoanId { get; private set; }

    /// <summary>La fila del plano de la que salió.</summary>
    public int? SourceRow { get; init; }

    /// <summary>Verdadero en <c>Pending</c> (de una corrida confirmada) y <c>Created</c>: ocupa el período del asociado en la línea.</summary>
    public bool HoldsPeriod { get; private set; }

    public DateTime? ProcessedAt { get; private set; }

    /// <summary>Una línea de la vista previa que queda fuera, con su motivo.</summary>
    public static BulkChargeLine Omitida(int corrida, int persona, int linea, int periodo, int oficina, string motivo, string? detalle, int? fila = null)
    {
        var l = new BulkChargeLine { RunId = corrida, PersonId = persona, CreditLineId = linea, Period = periodo, BranchId = oficina, SourceRow = fila };
        l.Omitir(motivo, detalle, null);
        return l;
    }

    /// <summary>Al confirmar la corrida, la línea pendiente reserva el período del asociado.</summary>
    public void Reservar()
    {
        if (Status == BulkChargeLineStatus.Pending) HoldsPeriod = true;
    }

    public void Crear(int credito, DateTime ahora)
    {
        Status = BulkChargeLineStatus.Created;
        LoanId = credito;
        HoldsPeriod = true;
        Reason = null;
        ReasonDetail = null;
        ProcessedAt = ahora;
    }

    public void Omitir(string motivo, string? detalle, DateTime? ahora)
    {
        Status = BulkChargeLineStatus.Skipped;
        Reason = motivo;
        ReasonDetail = Recortar(detalle);
        HoldsPeriod = false;
        ProcessedAt = ahora;
    }

    public void Fallar(string motivo, string? detalle, DateTime ahora)
    {
        Status = BulkChargeLineStatus.Failed;
        Reason = motivo;
        ReasonDetail = Recortar(detalle);
        HoldsPeriod = false;
        ProcessedAt = ahora;
    }

    public void Anular(string motivo, DateTime ahora)
    {
        Status = BulkChargeLineStatus.Voided;
        Reason = MotivosDeCargoMasivo.Anulado;
        ReasonDetail = Recortar(motivo);
        HoldsPeriod = false;
        ProcessedAt = ahora;
    }

    /// <summary>Anulación rechazada: la obligación sigue viva y la línea creada; queda el motivo.</summary>
    public void NoSePudoAnular(string motivo, string? detalle)
    {
        Reason = motivo;
        ReasonDetail = Recortar(detalle);
    }

    private static string? Recortar(string? s) => s is null ? null : s.Length <= 500 ? s : s[..500];
}

/// <summary>Los motivos de una línea de cargo masivo (códigos estables; la pantalla los traduce). (nuevo)</summary>
public static class MotivosDeCargoMasivo
{
    public const string Retirado = "Withdrawn";
    public const string Fallecido = "Deceased";
    public const string RetiroEnTramite = "WithdrawalInProgress";
    public const string YaCargado = "AlreadyCharged";
    public const string NoAsociado = "NotAssociate";
    public const string SinPagaduria = "NoPayrollAgreement";
    public const string SinNominaPropia = "NoOwnPayrollAgreement";
    public const string FormaNoAdmitida = "ChannelNotAllowed";
    public const string ReglaDeLinea = "LineRule";
    public const string TieneMovimientos = "HasMovements";
    public const string Anulado = "Voided";

    public static IReadOnlyList<string> Todos { get; } =
        [Retirado, Fallecido, RetiroEnTramite, YaCargado, NoAsociado, SinPagaduria, SinNominaPropia, FormaNoAdmitida, ReglaDeLinea, TieneMovimientos, Anulado];
}
