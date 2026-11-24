using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Application.Compliance.HabeasData.Authorizations;

/// <summary>Una finalidad que cubre la autorización general y, en el canal de contacto, cuál canal. (nuevo)</summary>
public sealed record FinalidadDeLaGeneral(DataAuthorizationPurpose Purpose, DataAuthorizationChannel? ContactChannel = null);

/// <summary>Una finalidad que la autorización general dejó registrada, con el <c>PublicId</c> de su decisión. (nuevo)</summary>
public sealed record AuthorizationPurposeRecordedDto(DataAuthorizationPurpose Purpose, DataAuthorizationChannel? ContactChannel, Guid PublicId);

/// <summary>Una finalidad que la autorización general no registró: el código del motivo y el motivo en español. (nuevo)</summary>
public sealed record AuthorizationPurposeOmittedDto(DataAuthorizationPurpose Purpose, DataAuthorizationChannel? ContactChannel, string Code, string Reason);

/// <summary>Lo que dejó la autorización general: las finalidades registradas y las que quedaron por fuera, con su motivo. (nuevo)</summary>
public sealed record GeneralAuthorizationResultDto(
    IReadOnlyList<AuthorizationPurposeRecordedDto> Recorded,
    IReadOnlyList<AuthorizationPurposeOmittedDto> Omitted);

/// <summary>
/// Lo que se pide al registrar la autorización general de un titular, sin guardar (2026-11-24, pedido del dueño). (nuevo)
/// </summary>
public sealed record AutorizacionGeneralAAgregar(
    Guid PersonPublicId,
    DecisionDeAutorizacion Decision,
    Guid? EvidenceAttachmentPublicId,
    DataAuthorizationOrigin Origin,
    DateTime DecidedAt,
    string? Notes = null);

/// <summary>
/// La <b>autorización general</b> del titular (2026-11-24, pedido del dueño: «una autorización general que permita que el asociado tenga acceso a
/// todo»). No es una finalidad nueva ni hace que el tratamiento general implique las demás: registra <b>una decisión por finalidad</b> —tratamiento
/// general, trámite de crédito, consulta y reporte a centrales y cada uno de los cinco canales de contacto—, cada una contra la versión vigente de la
/// política de su finalidad, así cada consumidor sigue preguntando por la suya y la trazabilidad no cambia. Las reglas de evidencia son las de una
/// sola (<see cref="RecordDataAuthorizationCommandHandler"/>):
/// <list type="bullet">
/// <item>con el soporte firmado (o migrada) cubre todas, también centrales y canales de contacto, que piden consentimiento expreso y probado
/// (FR-006);</item>
/// <item>sin soporte quedan sólo las que no lo exigen —tratamiento general y trámite de crédito, salvo que la cooperativa encienda
/// <see cref="ParametrosDeCartera.AutorizacionesExigenEvidencia"/>— y las demás salen en <see cref="GeneralAuthorizationResultDto.Omitted"/> con su
/// motivo;</item>
/// <item>una finalidad sin política vigente también queda por fuera, con su motivo.</item>
/// </list>
/// Agrega sin guardar: guarda quien llama, en su transacción (el registro desde Autorizaciones del titular o el alta de la persona). (nuevo)
/// </summary>
public sealed class AutorizacionGeneral(IAutorizacionDeDatos autorizaciones, ILectorDeParametros parametros, IDateTimeService reloj)
{
    /// <summary>Todas las finalidades que cubre, en el orden en que se registran.</summary>
    public static IReadOnlyList<FinalidadDeLaGeneral> Finalidades { get; } =
    [
        new(DataAuthorizationPurpose.GeneralTreatment),
        new(DataAuthorizationPurpose.CreditProcessing),
        new(DataAuthorizationPurpose.CreditBureauQuery),
        new(DataAuthorizationPurpose.CreditBureauReport),
        .. Enum.GetValues<DataAuthorizationChannel>().Select(c => new FinalidadDeLaGeneral(DataAuthorizationPurpose.ContactChannel, c)),
    ];

    /// <summary>
    /// Las finalidades que exigen siempre el soporte (salvo la migrada): la consulta y el reporte a centrales y el canal de contacto. Sin él
    /// <c>HasEvidence</c> se derivaría del origen y una no migrada contaría como probada (FR-006).
    /// </summary>
    public static bool ExigeEvidenciaSiempre(DataAuthorizationPurpose finalidad) => finalidad is DataAuthorizationPurpose.CreditBureauQuery
        or DataAuthorizationPurpose.CreditBureauReport or DataAuthorizationPurpose.ContactChannel;

    /// <summary>
    /// Agrega (sin guardar) una decisión por cada finalidad de <see cref="Finalidades"/> salvo las de <paramref name="omitir"/> (el alta ya escribió
    /// el tratamiento general). Falla sólo por un error que no sea de evidencia ni de política, o si no quedó ninguna: sin soporte y con la
    /// cooperativa exigiéndolo, <c>Compliance.HabeasData.EvidenceRequired</c>; sin ninguna política vigente, <c>Compliance.HabeasData.NoCurrentPolicy</c>.
    /// </summary>
    public async Task<Result<GeneralAuthorizationResultDto>> AgregarAsync(AutorizacionGeneralAAgregar pedido, IReadOnlyCollection<FinalidadDeLaGeneral>? omitir,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        var conSoporte = pedido.EvidenceAttachmentPublicId is not null || pedido.Origin == DataAuthorizationOrigin.Migration;
        var exigeParametro = !conSoporte && await ExigeEvidenciaAsync(ct);
        var registradas = new List<AuthorizationPurposeRecordedDto>();
        var omitidas = new List<AuthorizationPurposeOmittedDto>();

        foreach (var f in Finalidades.Where(f => omitir is null || !omitir.Contains(f)))
        {
            if (!conSoporte && ExigeEvidenciaSiempre(f.Purpose))
            {
                omitidas.Add(new(f.Purpose, f.ContactChannel, HabeasDataErrorCodes.EvidenceRequired,
                    "Exige el soporte firmado: la consulta y el reporte a centrales y los canales de contacto piden autorización expresa con su soporte."));
                continue;
            }
            if (!conSoporte && exigeParametro)
            {
                omitidas.Add(new(f.Purpose, f.ContactChannel, HabeasDataErrorCodes.EvidenceRequired,
                    "Exige el soporte firmado: la cooperativa lo pide en Parámetros de Cartera (Autorizaciones.ExigenEvidencia)."));
                continue;
            }

            var agregada = await autorizaciones.AgregarAsync(new AutorizacionAAgregar(pedido.PersonPublicId, f.Purpose, pedido.Decision, null, f.ContactChannel,
                pedido.EvidenceAttachmentPublicId, pedido.Origin, pedido.DecidedAt, pedido.Notes), ct);
            if (agregada.IsSuccess)
            {
                registradas.Add(new(f.Purpose, f.ContactChannel, agregada.Value));
                continue;
            }
            if (agregada.Error.Code != HabeasDataErrorCodes.NoCurrentPolicy) return Result.Failure<GeneralAuthorizationResultDto>(agregada.Error);
            omitidas.Add(new(f.Purpose, f.ContactChannel, HabeasDataErrorCodes.NoCurrentPolicy,
                "La cooperativa no tiene publicada la política de tratamiento de datos de esta finalidad (Cumplimiento › Políticas de datos)."));
        }

        if (registradas.Count == 0 && (omitir is null || omitir.Count == 0))
            return Result.Failure<GeneralAuthorizationResultDto>(omitidas.Any(o => o.Code == HabeasDataErrorCodes.NoCurrentPolicy)
                ? new Error(HabeasDataErrorCodes.NoCurrentPolicy,
                    "No quedó registrada ninguna finalidad: la cooperativa no tiene publicada la política de tratamiento de datos de las que se pueden "
                    + "registrar así. Publíquela en Cumplimiento › Políticas de datos (con «Todas las finalidades» una misma política las cubre todas).")
                : new Error(HabeasDataErrorCodes.EvidenceRequired,
                    "Adjunte el soporte firmado de la autorización (el formato o la grabación): sin él no queda registrada ninguna finalidad."));
        return Result.Success(new GeneralAuthorizationResultDto(registradas, omitidas));
    }

    /// <summary>El parámetro de la cooperativa; si no se puede leer, se exige (lo seguro).</summary>
    private async Task<bool> ExigeEvidenciaAsync(CancellationToken ct)
    {
        var leido = await parametros.LeerComoAsync<bool>(ParametrosDeCartera.Modulo, ParametrosDeCartera.AutorizacionesExigenEvidencia, reloj.HoyLocal, ct: ct);
        return leido.IsFailure || leido.Value;
    }
}
