using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>El tercero fijo de un concepto vario, por su <c>PublicId</c> y su nombre. (nuevo)</summary>
public sealed record TerceroDeConcepto(Guid PublicId, string Nombre);

/// <summary>
/// El tercero de un concepto vario (2026-11-16, decisión del dueño: «se debe poder parametrizar cómo funciona cada concepto: si tiene cuenta contable y tercero por
/// defecto, o si el tercero es el mismo cliente que paga»). <see cref="MiscConceptThirdPartyMode.Payer"/>: quien paga, como siempre;
/// <see cref="MiscConceptThirdPartyMode.FixedThirdParty"/>: el tercero fijo del concepto (la aseguradora del SOAT), que se fuerza en el ítem aunque venga otro;
/// <see cref="MiscConceptThirdPartyMode.ChosenByCashier"/>: el que elige el cajero, obligatorio. El tercero contable del concepto es el beneficiario del ítem
/// (<c>EmisionDeRecaudo.ReciboEmitidoAsync</c>), así que estos dos modos no hacen del tercero el titular de lo que se paga. (nuevo)
/// </summary>
public static class TercerosDeConceptos
{
    /// <summary>¿El beneficiario del ítem es el tercero contable del concepto y no el titular de lo que se paga?</summary>
    public static bool EsTerceroContable(MiscConceptThirdPartyMode modo) =>
        modo is MiscConceptThirdPartyMode.FixedThirdParty or MiscConceptThirdPartyMode.ChosenByCashier;

    /// <summary>El tercero fijo de cada concepto que lo tiene, por el <c>Id</c> del concepto.</summary>
    public static async Task<IReadOnlyDictionary<int, TerceroDeConcepto>> FijosAsync(IApplicationDbContext db, IReadOnlyCollection<MiscConcept> conceptos,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conceptos);
        var ids = conceptos.Where(c => c.DefaultThirdPartyPersonId is not null).Select(c => c.DefaultThirdPartyPersonId!.Value).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, TerceroDeConcepto>();
        var personas = await db.People.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        return conceptos.Where(c => c.DefaultThirdPartyPersonId is { } p && personas.ContainsKey(p))
            .ToDictionary(c => c.Id, c => new TerceroDeConcepto(personas[c.DefaultThirdPartyPersonId!.Value].PublicId,
                CashDesk.OperacionesDeCaja.Nombre(personas[c.DefaultThirdPartyPersonId!.Value])));
    }

    /// <summary>
    /// El beneficiario de un ítem del concepto según su modo: <paramref name="propuesto"/> (el del ítem o el titular de la operación) con quien paga; el tercero fijo
    /// con el fijo; el que trae el ítem con el que elige el cajero (<c>Receipts.Receipt.ThirdPartyRequired</c> si no trae ninguno o no existe).
    /// </summary>
    public static async Task<Result<Guid>> BeneficiarioAsync(IApplicationDbContext db, MiscConcept concepto, Guid? delItem, Guid propuesto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(concepto);
        switch (concepto.ThirdPartyMode)
        {
            case MiscConceptThirdPartyMode.FixedThirdParty:
            {
                var fijo = concepto.DefaultThirdPartyPersonId is { } id
                    ? await db.People.AsNoTracking().Where(p => p.Id == id).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct)
                    : null;
                return fijo is { } publico
                    ? Result.Success(publico)
                    : Result.Failure<Guid>(new ErrorConDatos(ErroresDeRecaudo.CodigoTerceroRequerido,
                        $"El concepto {concepto.Code} va a nombre de un tercero fijo que no está configurado: complételo en los conceptos varios.", new { concept = concepto.Code }));
            }
            case MiscConceptThirdPartyMode.ChosenByCashier:
            {
                if (delItem is not { } elegido)
                    return Result.Failure<Guid>(new ErrorConDatos(ErroresDeRecaudo.CodigoTerceroRequerido,
                        $"El concepto {concepto.Code} va a nombre de un tercero: elíjalo antes de cobrar.", new { concept = concepto.Code }));
                return await db.People.AsNoTracking().AnyAsync(p => p.PublicId == elegido, ct)
                    ? Result.Success(elegido)
                    : Result.Failure<Guid>(ErroresDeRecaudo.PersonaNoEncontrada());
            }
            default:
                return Result.Success(propuesto);
        }
    }
}
