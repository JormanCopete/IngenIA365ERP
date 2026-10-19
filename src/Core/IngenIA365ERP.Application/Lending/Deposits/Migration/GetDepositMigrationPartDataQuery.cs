using FluentValidation;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Columnas;
using P = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes;

namespace IngenIA365ERP.Application.Lending.Deposits.Migration;

/// <summary>
/// La descarga con datos de una parte de la carga (feature 014, US9, T152; plantillas-y-archivos.md §1.1 y §1.3; api.md §16,
/// <c>GET …/parts/{part}/template.xlsx?withData=true</c>, <c>Lending.Deposits.Migration</c> y <c>Lending.Reports.ExportPersonalData</c>): las filas de las
/// tandas vivas de la parte en el orden de la plantilla, para corregirlas y volver a importarlas con «reemplazar la parte». Con
/// <see cref="DesdeSaldosDeAportes"/> en la parte <c>contribution-accounts</c> es «Tomar de los saldos cargados» (FR-113): convierte los saldos de aportes
/// de la plantilla <c>lending.contribution-balances</c> con fecha = el corte de la carga en filas de cuentas de aportes —el aporte va a aportes ordinarios,
/// la oficina y la vinculación salen de Asociados y la línea es la de aportes activa—, que luego se revisan y corrigen como cualquier parte. Las personas van
/// por su documento (nadie se crea). Las filas salen dentro del alcance de oficina y línea de quien descarga. (nuevo)
/// </summary>
public sealed record GetDepositMigrationPartDataQuery(Guid MigrationLoadPublicId, string Part, bool DesdeSaldosDeAportes = false)
    : IRequest<Result<DatosDePlantilla>>;

public sealed class GetDepositMigrationPartDataQueryValidator : AbstractValidator<GetDepositMigrationPartDataQuery>
{
    public GetDepositMigrationPartDataQueryValidator()
    {
        RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
        RuleFor(x => x.Part).NotEmpty().MaximumLength(40);
    }
}

public sealed class GetDepositMigrationPartDataQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<GetDepositMigrationPartDataQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetDepositMigrationPartDataQuery request, CancellationToken ct)
    {
        if (DefinicionesDeMigracionDeCaptaciones.Buscar(request.Part) is not { Definicion: { } definicion } parte)
            return Result.Failure<DatosDePlantilla>(ErroresDeGobierno.PlantillaInexistente());
        var carga = await ConsultasDeCargasDeCaptaciones.BuscarAsync(db, request.MigrationLoadPublicId, seguimiento: false, ct);
        if (carga is null) return Result.Failure<DatosDePlantilla>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));
        if (!parte.Admite(carga.Nature)) return Result.Failure<DatosDePlantilla>(ErroresDeMigracionDeCaptaciones.ParteDeOtraNaturaleza(parte.Parte, carga.Nature));
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        var filas = request.DesdeSaldosDeAportes && parte.Parte == P.ContributionAccounts
            ? await DesdeSaldosAsync(carga.CutoffDate, alcance, ct)
            : await DeLaCargaAsync(carga, parte.Parte, alcance, ct);
        var columnas = definicion.Hojas[0].Columnas.Select(c => c.Nombre).ToList();
        IReadOnlyList<IReadOnlyList<object?>> ordenadas = filas.Select(f => (IReadOnlyList<object?>)columnas.Select(c => f.GetValueOrDefault(c)).ToList()).ToList();
        return Result.Success(new DatosDePlantilla(new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>> { [definicion.Hojas[0].Nombre] = ordenadas }));
    }

    private sealed record Catalogos(IReadOnlyDictionary<int, string?> Documentos, IReadOnlyDictionary<Guid, (int Id, string Codigo)> Oficinas);

    private async Task<Catalogos> CatalogosAsync(IEnumerable<int> personas, IEnumerable<Guid> oficinas, CancellationToken ct)
    {
        var ids = personas.Distinct().ToList();
        var documentos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.TaxId, ct);
        var publicas = oficinas.Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => publicas.Contains(b.PublicId))
            .ToDictionaryAsync(b => b.PublicId, b => (b.Id, b.LegacyCode ?? b.Name), ct);
        return new Catalogos(documentos, sucursales);
    }

    private static decimal? Puntos(decimal? fraccion) => fraccion is null ? null : fraccion.Value * 100m;

    private static decimal? SinCero(decimal valor) => valor == 0m ? null : valor;

    private async Task<IReadOnlyList<Dictionary<string, object?>>> DeLaCargaAsync(Domain.Entities.Lending.Migration.MigrationLoad carga, string parte,
        AlcanceDeCartera alcance, CancellationToken ct)
    {
        var tandas = ConsultasDeCargasDeCaptaciones.PartesVivas(db, carga.Id, parte);
        switch (parte)
        {
            case P.LineEquivalences:
                return (await db.MigrationLineEquivalenceRows.AsNoTracking().Where(r => tandas.Contains(r.MigrationLoadPartId)).OrderBy(r => r.Id).ToListAsync(ct))
                    .Where(r => alcance.IncluyeLinea(r.CreditLineId))
                    .Select(r => new Dictionary<string, object?>
                    {
                        [C.CodigoHeredado] = r.SourceProductCode, [C.DescripcionHeredada] = r.SourceDescription, [C.Linea] = r.CreditLineCode,
                        [C.Clase] = r.DepositClass?.ToString(),
                    }).ToList();
            case P.MemberAccounts:
            {
                var filas = await db.MigrationAccountRows.AsNoTracking().Where(r => tandas.Contains(r.MigrationLoadPartId)).OrderBy(r => r.Id).ToListAsync(ct);
                var cat = await CatalogosAsync(filas.Select(f => f.PersonId), filas.Select(f => f.OfficePublicId), ct);
                return filas.Where(f => alcance.IncluyeLinea(f.CreditLineId) && cat.Oficinas.TryGetValue(f.OfficePublicId, out var o) && alcance.IncluyeOficina(o.Id))
                    .Select(f => new Dictionary<string, object?>
                    {
                        [C.Numero] = f.SourceAccountNumber, [C.Titular] = cat.Documentos.GetValueOrDefault(f.PersonId), [C.Oficina] = cat.Oficinas[f.OfficePublicId].Codigo,
                        [C.Linea] = f.SourceProductCode is null ? f.CreditLineCode : null, [C.CodigoHeredado] = f.SourceProductCode,
                        [C.Clase] = f.DepositClass.ToString(), [C.FechaDeApertura] = f.OpenedOn, [C.Estado] = f.Status.ToString(),
                        [C.FechaUltimoMovimientoDelTitular] = f.LastHolderActivityDate, [C.Manejo] = f.HandlingMode.ToString(), [C.SaldoCapital] = f.Principal,
                        [C.InteresCausadoNoLiquidado] = SinCero(f.InterestPayable), [C.UltimaLiquidacionHasta] = f.LastSettledThrough,
                        [C.CargosPendientes] = SinCero(f.ChargeReceivable), [C.CanjePendiente] = SinCero(f.ClearingAmount),
                        [C.SoporteDeOrigenDeFondos] = f.SourceOfFundsReference,
                    }).ToList();
            }
            case P.ContributionAccounts:
            {
                var filas = await db.MigrationContributionAccountRows.AsNoTracking().Where(r => tandas.Contains(r.MigrationLoadPartId)).OrderBy(r => r.Id)
                    .ToListAsync(ct);
                var cat = await CatalogosAsync(filas.Select(f => f.PersonId), filas.Select(f => f.OfficePublicId), ct);
                return filas.Where(f => alcance.IncluyeLinea(f.CreditLineId) && cat.Oficinas.TryGetValue(f.OfficePublicId, out var o) && alcance.IncluyeOficina(o.Id))
                    .Select(f => new Dictionary<string, object?>
                    {
                        [C.Asociado] = cat.Documentos.GetValueOrDefault(f.PersonId), [C.Numero] = f.SourceAccountNumber,
                        [C.Oficina] = cat.Oficinas[f.OfficePublicId].Codigo, [C.Linea] = f.SourceProductCode is null ? f.CreditLineCode : null,
                        [C.CodigoHeredado] = f.SourceProductCode, [C.FechaDeVinculacion] = f.OpenedOn, [C.AportesMinimos] = f.MinimumBalance,
                        [C.AportesOrdinarios] = f.OrdinaryBalance, [C.AportesExtraordinarios] = SinCero(f.ExtraordinaryBalance),
                        [C.AportesRevalorizados] = SinCero(f.RevaluedBalance), [C.MinimoNoReducible] = f.MinimumNonReducible, [C.IngresoRegistrado] = f.RegisteredIncome,
                        [C.CuotaPactada] = f.InstallmentAmount,
                    }).ToList();
            }
            case P.MemberAccountParties:
            {
                var filas = await db.MigrationAccountPartyRows.AsNoTracking().Where(r => tandas.Contains(r.MigrationLoadPartId)).OrderBy(r => r.Id).ToListAsync(ct);
                var cat = await CatalogosAsync(filas.Select(f => f.PersonId), [], ct);
                return filas.Select(f => new Dictionary<string, object?>
                {
                    [C.Cuenta] = f.SourceAccountNumber, [C.Persona] = cat.Documentos.GetValueOrDefault(f.PersonId), [C.Rol] = f.Role.ToString(),
                    [C.Proporcion] = f.Role == MemberAccountPartyRole.CoHolder ? Puntos(f.Share) : null, [C.LimiteDeFirma] = f.SignatureLimit,
                    [C.PorcentajeBeneficiario] = f.Role == MemberAccountPartyRole.Beneficiary ? Puntos(f.Share) : null, [C.Fundamento] = f.RepresentationBasis,
                    [C.VigenteDesde] = f.ValidFrom, [C.VigenteHasta] = f.ValidTo, [C.Soporte] = f.SupportReference,
                }).ToList();
            }
            case P.AccountInstallments:
                return (await db.MigrationAccountInstallmentRows.AsNoTracking().Where(r => tandas.Contains(r.MigrationLoadPartId)).OrderBy(r => r.Id).ToListAsync(ct))
                    .Select(f => new Dictionary<string, object?>
                    {
                        [C.Cuenta] = f.SourceAccountNumber,
                        [C.Periodo] = f.SubPeriodNumber > 1 ? $"{f.PeriodYear:0000}-{f.PeriodMonth:00}-{f.SubPeriodNumber}" : $"{f.PeriodYear:0000}-{f.PeriodMonth:00}",
                        [C.FechaDeVencimiento] = f.DueDate, [C.Valor] = f.Amount, [C.Pagado] = SinCero(f.PaidAmount), [C.MotivoNoDescontado] = f.NonDeductionReason?.ToString(),
                    }).ToList();
            case P.GmfExemptions:
            {
                var filas = await db.MigrationGmfExemptionRows.AsNoTracking().Where(r => tandas.Contains(r.MigrationLoadPartId)).OrderBy(r => r.Id).ToListAsync(ct);
                var cat = await CatalogosAsync(filas.Select(f => f.PersonId), [], ct);
                return filas.Select(f => new Dictionary<string, object?>
                {
                    [C.Persona] = cat.Documentos.GetValueOrDefault(f.PersonId), [C.CuentaExenta] = f.SourceAccountNumber, [C.VigenteDesde] = f.MarkedSince,
                    [C.ExentoUsadoDelMes] = f.MonthExemptUsed,
                }).ToList();
            }
            case P.TaxYearToDate:
            {
                var filas = await db.MigrationTaxYearRows.AsNoTracking().Where(r => tandas.Contains(r.MigrationLoadPartId)).OrderBy(r => r.Id).ToListAsync(ct);
                var cat = await CatalogosAsync(filas.Select(f => f.PersonId), [], ct);
                return filas.Select(f => new Dictionary<string, object?>
                {
                    [C.Titular] = cat.Documentos.GetValueOrDefault(f.PersonId), [C.Cuenta] = f.SourceAccountNumber, [C.Anio] = (int)f.TaxYear,
                    [C.Concepto] = f.Concept.ToString(), [C.Valor] = f.Amount, [C.BaseDeRetencion] = f.WithholdingBase,
                }).ToList();
            }
            case P.TermDeposits:
            {
                // US6 (pendiente de la fase 10): los CDAT de la carga como se importaron (las tasas en puntos).
                var filas = await db.MigrationTermDepositRows.AsNoTracking().Where(r => tandas.Contains(r.MigrationLoadPartId)).OrderBy(r => r.Id).ToListAsync(ct);
                var cat = await CatalogosAsync(filas.Select(f => f.PersonId), filas.Select(f => f.OfficePublicId), ct);
                return filas.Where(f => alcance.IncluyeLinea(f.CreditLineId) && cat.Oficinas.TryGetValue(f.OfficePublicId, out var o) && alcance.IncluyeOficina(o.Id))
                    .Select(f => new Dictionary<string, object?>
                    {
                        [C.Numero] = f.SourceAccountNumber, [C.Titular] = cat.Documentos.GetValueOrDefault(f.PersonId), [C.Oficina] = cat.Oficinas[f.OfficePublicId].Codigo,
                        [C.Linea] = f.SourceProductCode is null ? f.CreditLineCode : null, [C.CodigoHeredado] = f.SourceProductCode,
                        [C.NumeroDeTitulo] = f.SourceTitleNumber, [C.Capital] = f.PrincipalAmount, [C.FechaDeConstitucion] = f.ConstitutedOn,
                        [C.FechaDeInicioVigente] = f.StartDate, [C.PlazoDias] = f.TermDays, [C.FechaDeVencimiento] = f.MaturityDate,
                        [C.TasaPactadaEA] = Puntos(f.AppliedAnnualRate), [C.TasaDeTablaEA] = Puntos(f.TableAnnualRate), [C.MargenNegociadoEA] = Puntos(f.NegotiatedSpread),
                        [C.PeriodicidadDeIntereses] = f.InterestPeriodicity.ToString(), [C.Modalidad] = f.InterestTiming.ToString(), [C.PeriodosPagados] = f.PaidPeriods,
                        [C.InteresCausadoNoPagado] = f.InterestPayable,
                        [C.DestinoDeIntereses] = f.InterestDestination == InterestDestination.ToAccount ? "Account" : "DisbursementOrder",
                        [C.CuentaDestino] = f.DestinationAccountNumber, [C.InstruccionAlVencimiento] = f.MaturityInstruction.ToString(),
                        [C.ProrrogasHechas] = f.RolloverCount == 0 ? null : f.RolloverCount, [C.PlazoDeEmision] = f.IssueTermBand.ToString(),
                        [C.Embargado] = f.IsGarnished ? "Sí" : null, [C.TitularFallecido] = f.HolderDeceased ? "Sí" : null,
                    }).ToList();
            }
            default:
                return [];
        }
    }

    /// <summary>«Tomar de los saldos cargados»: la última carga de saldos de aportes de cada persona con fecha = el corte.</summary>
    private async Task<IReadOnlyList<Dictionary<string, object?>>> DesdeSaldosAsync(DateOnly corte, AlcanceDeCartera alcance, CancellationToken ct)
    {
        var saldos = (await db.ContributionBalanceLoads.AsNoTracking().Where(s => s.AsOfDate == corte && !s.IsDeleted).ToListAsync(ct))
            .GroupBy(s => s.PersonId).Select(g => g.OrderByDescending(s => s.RegisteredAt).First()).OrderBy(s => s.PersonId).ToList();
        if (saldos.Count == 0) return [];
        var ids = saldos.Select(s => s.PersonId).ToList();
        var documentos = await db.People.AsNoTracking().Where(p => ids.Contains(p.Id) && !p.IsDeleted).ToDictionaryAsync(p => p.Id, p => p.TaxId, ct);
        var asociados = (await db.Associates.AsNoTracking().Where(a => ids.Contains(a.PersonId) && !a.IsDeleted)
                .Select(a => new { a.PersonId, a.BranchId, a.JoinDate }).ToListAsync(ct))
            .GroupBy(a => a.PersonId).ToDictionary(g => g.Key, g => g.First());
        var oficinas = asociados.Values.Where(a => a.BranchId is not null).Select(a => a.BranchId!.Value).Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().Where(b => oficinas.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.LegacyCode ?? b.Name, ct);
        var linea = await db.CreditLines.AsNoTracking()
            .Where(l => l.Nature == LineNature.Contribution && l.Status == CreditLineStatus.Active && !l.IsDeleted).Select(l => new { l.Id, l.Code })
            .FirstOrDefaultAsync(ct);
        return saldos.Where(s => documentos.ContainsKey(s.PersonId))
            .Where(s => asociados.GetValueOrDefault(s.PersonId)?.BranchId is not { } o || alcance.IncluyeOficina(o))
            .Select(s =>
            {
                var a = asociados.GetValueOrDefault(s.PersonId);
                return new Dictionary<string, object?>
                {
                    [C.Asociado] = documentos[s.PersonId],
                    [C.Oficina] = a?.BranchId is { } o ? sucursales.GetValueOrDefault(o) : null,
                    [C.Linea] = linea is not null && alcance.IncluyeLinea(linea.Id) ? linea.Code : null,
                    [C.FechaDeVinculacion] = a?.JoinDate,
                    [C.AportesMinimos] = 0m,
                    [C.AportesOrdinarios] = s.ContributionsBalance,
                };
            }).ToList();
    }
}
