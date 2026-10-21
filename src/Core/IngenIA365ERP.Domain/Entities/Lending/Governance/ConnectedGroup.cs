using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_ConnectedGroups</c> (feature 013, T0213; DT §7.10 C112; FR-041, SC-022): un grupo conectado registrado por el jefe de
/// cartera con su fundamento (vinculación económica o jurídica, codeudor común, vinculado). La concentración se mide por deudor
/// <b>y</b> por grupo con el mismo límite. Hecho (<see cref="IHechoInmutable"/>): sólo su terminación se escribe después.
/// </summary>
public class ConnectedGroup : AuditableEntity, IHechoInmutable
{
    public string Name { get; init; } = string.Empty;

    public ConnectedGroupBasis Basis { get; init; }

    /// <summary>Fundamento escrito del grupo; obligatorio.</summary>
    public string Grounds { get; init; } = string.Empty;

    public Guid? SupportAttachmentPublicId { get; init; }

    public int RegisteredByUserId { get; init; }

    public DateOnly ValidFrom { get; init; }

    [EscrituraUnica]
    public DateTime? EndedAt { get; set; }

    [EscrituraUnica]
    public string? EndReason { get; set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0393) --

    public const string CodigoInexistente = "Lending.ConnectedGroup.NotFound";
    public const string CodigoTerminado = "Lending.ConnectedGroup.Ended";

    /// <summary>Vigente el <paramref name="fecha"/>: empezó y no terminó antes de que acabe ese día.</summary>
    public bool VigenteEn(DateOnly fecha) => !IsDeleted && ValidFrom <= fecha && (EndedAt is not { } fin || DateOnly.FromDateTime(fin) > fecha);

    /// <summary>
    /// Un grupo registrado por el jefe de cartera (C112): nombre, fundamento (la clase y el texto, obligatorio), soporte opcional y desde cuándo
    /// rige. Los miembros los agrega el comando.
    /// </summary>
    public static ConnectedGroup Registrar(string nombre, ConnectedGroupBasis fundamento, string texto, Guid? soporte, DateOnly desde, int registradoPor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(texto);
        if (!Enum.IsDefined(fundamento)) throw new ArgumentException("Fundamento de grupo desconocido.", nameof(fundamento));
        return new ConnectedGroup
        {
            Name = nombre.Trim(),
            Basis = fundamento,
            Grounds = texto.Trim(),
            SupportAttachmentPublicId = soporte,
            ValidFrom = desde,
            RegisteredByUserId = registradoPor,
        };
    }

    /// <summary>Termina el grupo con motivo, una sola vez (<see cref="EscrituraUnicaAttribute"/>).</summary>
    public void Terminar(DateTime instante, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (EndedAt is not null)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoTerminado, "El grupo ya está terminado.", "endedAt", EndedAt));
        EndedAt = instante;
        EndReason = motivo.Trim();
    }
}
