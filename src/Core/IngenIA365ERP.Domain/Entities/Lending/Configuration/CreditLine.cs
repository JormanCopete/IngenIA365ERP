using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_CreditLines</c> (feature 013, T0210; data-model §1.2; FR-019, FR-021, FR-032): la identidad estable de una línea de
/// crédito. Todo lo que cambia con el tiempo va en su versión (<see cref="CreditLineVersion"/>). El código es la nomenclatura de la
/// cooperativa (<c>CodigoDeCatalogo</c>) y <b>no cambia</b> (<c>init</c>): es la dimensión <c>CreditLineCode</c> de la matriz contable
/// (T52). Con créditos vivos no se borra: se cierra a créditos nuevos.
///
/// <para>
/// US1 (T0351) le da comportamiento: nace en <see cref="CreditLineStatus.Draft"/> (<see cref="Nueva"/>), pasa a
/// <see cref="CreditLineStatus.Active"/> cuando su versión vigente pasa la usura y, si es de origen comercial con interés, tiene su
/// concepto tributario (lo decide el comando; aquí <see cref="Activar"/>), se cierra con motivo (<see cref="Cerrar"/>) y se reabre
/// (<see cref="Reabrir"/>, el comando revalida la usura del día). El bloqueo por usura (<see cref="BloquearPorUsura"/>) no es un
/// cierre: la línea sigue activa pero no admite créditos nuevos hasta que una versión bajo la usura lo limpie
/// (<see cref="DesbloquearPorUsura"/>). Las transiciones se preguntan antes (<c>Validar…</c>) y lanzan si no.
/// </para>
///
/// <para>
/// Feature 014 (T011; research R01; data-model §1.1; FR-027): la línea es la identidad común del catálogo de la Cartera y tiene
/// <see cref="Nature"/> —colocación, captación o aportes—, fija desde la creación. Su versión de condiciones depende de la naturaleza
/// (<see cref="CreditLineVersion"/> sólo para colocación; <c>DepositLineVersion</c> y <c>ContributionLineVersion</c> para las otras dos,
/// <see cref="ValidarNaturaleza"/>). La usura es sólo de la colocación: una línea de captación o de aportes nunca admite créditos
/// (<see cref="AdmiteCreditosNuevos"/>) y el bloqueo y desbloqueo por usura la ignoran. <c>Code</c> sigue único entre las tres naturalezas y
/// sigue siendo la dimensión de línea de la matriz.
/// </para>
/// </summary>
public class CreditLine : AuditableEntity
{
    public const string CodigoCerrada = "Lending.CreditLine.Closed";
    public const string CodigoYaCerrada = "Lending.CreditLine.AlreadyClosed";
    public const string CodigoNoCerrada = "Lending.CreditLine.NotClosed";

    /// <summary>Feature 014 (T011): la operación pide otra naturaleza de línea (una versión de crédito sobre una línea de captación, o al revés).</summary>
    public const string CodigoNaturalezaDistinta = "Lending.CreditLine.NatureMismatch";

    public string Code { get; init; } = string.Empty;

    /// <summary>Feature 014 (T011): colocación (todas las de la 013), captación o aportes. Inmutable desde la creación.</summary>
    public LineNature Nature { get; init; } = LineNature.Placement;

    public string Name { get; set; } = string.Empty;

    /// <summary>Destino del crédito que la línea financia (FR-019).</summary>
    public string? Purpose { get; set; }

    public CreditLineStatus Status { get; private set; } = CreditLineStatus.Draft;

    public CreditLineClosedReason? ClosedReason { get; private set; }

    public DateTime? ClosedAt { get; private set; }

    public int? ClosedByUserId { get; private set; }

    public string? CloseNotes { get; private set; }

    /// <summary>Día desde el que la versión vigente supera la usura de su certificación (FR-021, US1-5); nulo = no bloqueada.</summary>
    public DateOnly? UsuryBlockedSince { get; private set; }

    /// <summary>El valor nacional <c>USURA</c> que la bloqueó (copia de <c>COR_NationalValues</c>, sin FK).</summary>
    public Guid? UsuryBlockedByValuePublicId { get; private set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    /// <summary>
    /// Una línea nueva, en borrador: el código va en mayúsculas y sin espacios alrededor (lo valida <c>CodigoDeCatalogo</c>). Sin
    /// naturaleza, es de colocación (la 013 no cambia).
    /// </summary>
    public static CreditLine Nueva(string code, string name, string? purpose, LineNature nature = LineNature.Placement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(nature)) throw new ArgumentOutOfRangeException(nameof(nature), nature, "Naturaleza de línea desconocida.");
        return new CreditLine
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Purpose = string.IsNullOrWhiteSpace(purpose) ? null : purpose.Trim(),
            Nature = nature,
        };
    }

    /// <summary>¿Es de colocación (crédito)? Sólo ésta tiene usura, modalidad y versión de crédito.</summary>
    public bool EsDeColocacion => Nature == LineNature.Placement;

    /// <summary>
    /// ¿Admite simular, radicar, aprobar, desembolsar o ventas nuevas? De colocación, activa y sin bloqueo por usura. Una línea de captación o
    /// de aportes nunca admite créditos (feature 014, T011).
    /// </summary>
    public bool AdmiteCreditosNuevos => EsDeColocacion && Status == CreditLineStatus.Active && UsuryBlockedSince is null;

    /// <summary>
    /// Lo que impide usarla con la naturaleza <paramref name="esperada"/>, o nulo (feature 014, T011): la versión de crédito y todo lo que sigue
    /// del crédito sólo van sobre colocación; las versiones de captación y de aportes, sobre su naturaleza.
    /// </summary>
    public ProblemaDeCartera? ValidarNaturaleza(LineNature esperada) => Nature == esperada
        ? null
        : new ProblemaDeCartera(CodigoNaturalezaDistinta,
            $"La línea {Code} es de {NombreDe(Nature)}: no admite una operación de {NombreDe(esperada)}.",
            new Dictionary<string, object?> { ["code"] = Code, ["nature"] = Nature.ToString(), ["expected"] = esperada.ToString() });

    private static string NombreDe(LineNature n) => n switch
    {
        LineNature.Placement => "crédito",
        LineNature.Deposit => "captación",
        LineNature.Contribution => "aportes",
        _ => n.ToString(),
    };

    public bool EstaBloqueadaPorUsura => UsuryBlockedSince is not null;

    /// <summary>Lo que impide activarla, o nulo: una línea cerrada no se activa (se reabre).</summary>
    public ProblemaDeCartera? ValidarActivacion() => Status == CreditLineStatus.Closed
        ? ProblemaDeCartera.Con(CodigoCerrada, $"La línea {Code} está cerrada a créditos nuevos; reábrala.", "code", Code)
        : null;

    /// <summary>
    /// <c>Draft → Active</c>; activa, no cambia. En colocación quien llama ya comprobó la usura y el concepto tributario (T44); en captación y
    /// aportes (feature 014, T011) no hay usura: la activa el registro de su primera versión de su naturaleza.
    /// </summary>
    public void Activar()
    {
        ReglaDeCarteraException.SiHay(ValidarActivacion());
        Status = CreditLineStatus.Active;
    }

    /// <summary>Lo que impide cerrarla, o nulo.</summary>
    public ProblemaDeCartera? ValidarCierre() => Status == CreditLineStatus.Closed
        ? ProblemaDeCartera.Con(CodigoYaCerrada, $"La línea {Code} ya está cerrada.", "code", Code)
        : null;

    /// <summary>
    /// La cierra a créditos nuevos (FR-032); los vivos no cambian. El cierre manual lleva motivo; el de una modalidad retirada del
    /// catálogo vigente (T44) lo hace la réplica, sin usuario.
    /// </summary>
    public void Cerrar(CreditLineClosedReason motivo, string? notas, int? usuarioId, DateTime cerradaEn)
    {
        ReglaDeCarteraException.SiHay(ValidarCierre());
        if (!Enum.IsDefined(motivo)) throw new ArgumentOutOfRangeException(nameof(motivo), motivo, "Motivo de cierre desconocido.");
        if (motivo == CreditLineClosedReason.Manual && string.IsNullOrWhiteSpace(notas))
            throw new ArgumentException("El cierre manual de una línea lleva su motivo.", nameof(notas));
        Status = CreditLineStatus.Closed;
        ClosedReason = motivo;
        CloseNotes = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim();
        ClosedByUserId = usuarioId;
        ClosedAt = cerradaEn;
    }

    /// <summary>Lo que impide reabrirla, o nulo.</summary>
    public ProblemaDeCartera? ValidarReapertura() => Status != CreditLineStatus.Closed
        ? ProblemaDeCartera.Con(CodigoNoCerrada, $"La línea {Code} no está cerrada.", "code", Code)
        : null;

    /// <summary><c>Closed → Active</c>: el comando revalidó la usura del día antes. El historial del cierre queda en la auditoría.</summary>
    public void Reabrir()
    {
        ReglaDeCarteraException.SiHay(ValidarReapertura());
        Status = CreditLineStatus.Active;
        ClosedReason = null;
        CloseNotes = null;
        ClosedByUserId = null;
        ClosedAt = null;
    }

    /// <summary>
    /// La bloquea por una usura nueva que su versión vigente supera (FR-021, US1-5). Devuelve <c>true</c> si la bloqueó ahora y
    /// <c>false</c> si ya estaba bloqueada (no cambia nada: una línea ya marcada no se vuelve a alertar).
    /// </summary>
    public bool BloquearPorUsura(DateOnly desde, Guid usuraPublicId)
    {
        // Feature 014 (T011; LasCaptacionesNoUsanLaUsura): la usura es sólo de la colocación; una línea de captación o de aportes la ignora.
        if (!EsDeColocacion || UsuryBlockedSince is not null) return false;
        UsuryBlockedSince = desde;
        UsuryBlockedByValuePublicId = usuraPublicId;
        return true;
    }

    /// <summary>Limpia el bloqueo cuando una versión nueva pasa la usura. Devuelve <c>true</c> si estaba bloqueada.</summary>
    public bool DesbloquearPorUsura()
    {
        if (!EsDeColocacion || UsuryBlockedSince is null) return false;
        UsuryBlockedSince = null;
        UsuryBlockedByValuePublicId = null;
        return true;
    }
}
