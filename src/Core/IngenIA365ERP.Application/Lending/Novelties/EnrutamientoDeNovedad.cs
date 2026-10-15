using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>Una novedad enrutada (T63): la matriz, el resultado del motor, el nivel efectivo, la huella, el monto que se mira y el vinculado. (nuevo)</summary>
public sealed record NovedadEnrutada(MatrizVigente Matriz, EnrutamientoDeAtribucion Enrutamiento, AttributionLevel Nivel, string Huella, decimal Monto,
    bool Vinculado)
{
    public SubjectRoutingDto Dto(IReadOnlyDictionary<int, string> organos) => new(Nivel.Code, Nivel.Name, Nivel.Kind,
        Nivel.GoverningBodyId is { } g ? organos.GetValueOrDefault(g) : null, Monto, Vinculado,
        Enrutamiento.Razones.Select(r => new RoutingReasonDto(r.Clase, r.Nivel.Codigo, r.Detalle)).ToList(), Huella);
}

/// <summary>
/// El enrutamiento de una novedad (feature 013, K3, T0879; T63, FR-038, FR-040): sujeto <c>Novelty</c> por la matriz de atribuciones vigente, con el
/// monto consolidado del deudor (su exposición más lo que la novedad capitaliza), la línea y la modalidad del crédito principal, el plazo del plan nuevo
/// en meses, los días de mora y si el deudor o alguna parte de los créditos es vinculado (al consejo con acta, FR-040). La matriz no distingue
/// mecanismo ni causa: <c>LND_AttributionRules</c> no tiene esas columnas (pendiente de esquema, ver tasks.md T0879); el nivel sale del monto, la
/// mora, la línea, la modalidad y el vinculado. La huella cubre la novedad, su simulación, la matriz, el nivel, las razones, el monto y el vinculado: si
/// cambia entre que se radicó y se decide, <c>Lending.Decision.ContentChanged</c>. (nuevo)
/// </summary>
public sealed class EnrutamientoDeNovedad(
    IApplicationDbContext db,
    MatrizVigenteDeAtribuciones matrices,
    ExposicionConsolidadaDelDeudor exposicion,
    VinculadosVigentes vinculados,
    IDateTimeService reloj)
{
    public async Task<Result<NovedadEnrutada>> CalcularAsync(LoanNovelty n, DatosDeEnrutamientoDeNovedad datos, DateOnly fecha, CancellationToken ct)
    {
        var matriz = await matrices.LeerAsync(fecha, reloj.UtcNow, ct);
        if (matriz is null) return Result.Failure<NovedadEnrutada>(ErroresDeGobierno.SinMatrizVigente(fecha));

        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == n.LoanId, ct);
        var expo = await exposicion.DeAsync(credito.DebtorPersonId, fecha, null, ct);
        var monto = expo.Total + datos.Capitalized;
        var partes = await PersonasDeLaNovedadAsync(db, n, ct);
        var vinculado = await vinculados.HayAlgunoAsync(partes, fecha, ct);
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == credito.CreditLineId).Select(l => l.Code).FirstAsync(ct);
        var modalidad = await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == credito.Id).OrderByDescending(t => t.VersionNumber)
            .Select(t => t.ModalityCode).FirstAsync(ct);
        var periodicidad = Enum.TryParse<RepaymentPeriodicity>(datos.Periodicity, out var p) ? p : RepaymentPeriodicity.Monthly;

        var sujeto = new SujetoAEnrutar(LendingDecisionSubjects.Novelty, monto)
        {
            Linea = linea,
            Modalidad = modalidad,
            PlazoMeses = CalculoDeNovedad.Meses(datos.Installments, periodicidad),
            DiasDeMora = datos.DaysPastDue,
            HayVinculado = vinculado,
        };
        EnrutamientoDeAtribucion enrutado;
        try
        {
            enrutado = EnrutadorDeAtribuciones.Enrutar(sujeto, matriz.ParaElMotor);
        }
        catch (InvalidOperationException e)
        {
            return Result.Failure<NovedadEnrutada>(new Error(AttributionMatrix.CodigoSinNivelFinal, e.Message));
        }
        var nivel = matriz.Niveles.Single(x => x.Code == enrutado.NivelEfectivo.Codigo);
        var huella = new HuellaDeCartera()
            .Texto("subject", LendingDecisionSubjects.Novelty)
            .Identificador("novelty", n.PublicId)
            .Enumeracion<NoveltyMechanism>("mechanism", n.Mechanism)
            .Enumeracion<NoveltyCause>("cause", n.Cause)
            .Texto("simulation", n.SimulationSha256)
            .Identificador("matrix", matriz.Matriz.PublicId)
            .Entero("matrixVersion", matriz.Matriz.VersionNumber)
            .Texto("level", enrutado.NivelEfectivo.Codigo)
            .Lista("reasons", enrutado.Razones, (h, r) => h.Enumeracion("kind", (RazonDeEnrutamiento?)r.Clase).Texto("level", r.Nivel.Codigo))
            .Decimal("amount", monto)
            .Logico("relatedParty", vinculado)
            .Calcular();
        return Result.Success(new NovedadEnrutada(matriz, enrutado, nivel, huella, monto, vinculado));
    }

    /// <summary>El deudor y las partes vigentes (codeudores, avalistas) de los créditos de la novedad: los que pueden hacerla vinculada.</summary>
    public static async Task<IReadOnlyList<int>> PersonasDeLaNovedadAsync(IApplicationDbContext db, LoanNovelty n, CancellationToken ct)
    {
        var creditos = await db.LoanNoveltyLoans.AsNoTracking().Where(x => x.LoanNoveltyId == n.Id && !x.IsDeleted).Select(x => x.LoanId).ToListAsync(ct);
        creditos.Add(n.LoanId);
        var deudores = await db.Loans.AsNoTracking().Where(l => creditos.Contains(l.Id)).Select(l => l.DebtorPersonId).ToListAsync(ct);
        var partes = await db.LoanParties.AsNoTracking().Where(p => creditos.Contains(p.LoanId) && !p.IsDeleted && p.ReleasedOn == null && p.ExtinguishedOn == null)
            .Select(p => p.PersonId).ToListAsync(ct);
        return deudores.Concat(partes).Distinct().ToList();
    }

    /// <summary>Los nombres de los órganos de decisión, por Id.</summary>
    public static async Task<IReadOnlyDictionary<int, string>> OrganosAsync(IApplicationDbContext db, CancellationToken ct) =>
        await db.GoverningBodies.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
}
