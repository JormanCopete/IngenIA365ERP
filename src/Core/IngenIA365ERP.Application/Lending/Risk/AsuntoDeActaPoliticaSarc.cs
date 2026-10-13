using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>
/// El asunto de acta <c>RiskPolicyVersion</c> (feature 013, US9, T0700; C52, T45): una versión de la política SARC que el consejo aprueba.
/// Pendiente mientras no esté validada; el expediente es la versión (la segregación compara contra quien la registró: no vota su punto ni la
/// valida); la huella cubre el contenido canónico, su vigencia y la norma contra la que se comparó, así que una versión distinta de la que se llevó
/// a la sesión no se cierra con esa acta. Decidir no la valida: la valida <see cref="ValidateRiskPolicyVersionCommand"/> con el punto cerrado. (nuevo)
/// </summary>
public sealed class AsuntoDeActaPoliticaSarc(IApplicationDbContext db) : IAsuntoDeActa
{
    public string SubjectType => LendingDecisionSubjects.RiskPolicyVersion;

    public async Task<Result<AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct)
    {
        var version = await db.RiskPolicyVersions.AsNoTracking().FirstOrDefaultAsync(v => v.PublicId == subjectPublicId && !v.IsDeleted, ct);
        if (version is null) return Result.Failure<AsuntoDeActa>(ErroresDeRiesgo.PoliticaInexistente());

        var registro = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == version.RegisteredByUserId)
            .Select(u => u.PersonId).FirstOrDefaultAsync(ct);
        var huella = new HuellaDeCartera()
            .Identificador("version", version.PublicId)
            .Entero("versionNumber", version.VersionNumber)
            .Fecha("validFrom", version.ValidFrom)
            .Texto("overridesSha256", version.OverridesSha256)
            .Texto("baseDocumentSha256", version.BaseDocumentSha256)
            .Calcular();
        return Result.Success(new AsuntoDeActa(
            string.Create(CultureInfo.InvariantCulture, $"Política SARC, versión {version.VersionNumber} (rige desde {version.ValidFrom:yyyy-MM-dd})"),
            version.Reason,
            version.ValidatedAt is null,
            version.Status.ToString(),
            new ExpedienteDeCartera(AddRiskPolicyVersionCommandHandler.ClaseDeExpediente, version.PublicId),
            huella)
        {
            Personas = registro is { } persona ? [new PersonaDelAsunto(persona, MotivosDeExclusion.Radicador)] : [],
        });
    }

    public Task<Result> AlDecidirAsync(DecisionDeActa decision, CancellationToken ct) => Task.FromResult(Result.Success());
}
