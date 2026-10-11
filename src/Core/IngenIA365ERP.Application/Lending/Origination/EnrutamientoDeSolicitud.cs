using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>
/// Lo que pesa en el enrutamiento de una solicitud (T63, FR-037, FR-038, FR-040): el monto consolidado (lo pedido más la exposición del
/// solicitante como deudor y codeudor, reservas y aprobadas), la línea y su modalidad, el plazo, las garantías, si hay un vinculado, el puntaje
/// bajo el mínimo, las alertas (validaciones en advertencia), la excepción a la política y los hallazgos no exceptuables. (nuevo)
/// </summary>
public sealed record DatosDeEnrutamiento(
    decimal MontoConsolidado,
    bool HayVinculado,
    bool PuntajeBajoElMinimo,
    bool ConAlertas,
    IReadOnlyList<MotivoNoExceptuable> Hallazgos);

/// <summary>
/// Una solicitud enrutada (T0395): la matriz vigente, el resultado del motor, el nivel efectivo como fila, la huella <c>RoutingSha256</c> y lo
/// que se enrutó. (nuevo)
/// </summary>
public sealed record SolicitudEnrutada(MatrizVigente Matriz, EnrutamientoDeAtribucion Enrutamiento, AttributionLevel Nivel, string Huella,
    DatosDeEnrutamiento Datos);

/// <summary>
/// El enrutamiento de una solicitud (feature 013, T0395; T63, FR-038, FR-040; data-model §2.1): arma <see cref="SujetoAEnrutar"/> con el monto
/// consolidado, la línea y su modalidad, el plazo, las garantías (los codeudores), el vinculado, el puntaje, las alertas, la excepción pedida y
/// los hallazgos no exceptuables; llama a <see cref="EnrutadorDeAtribuciones"/> con la matriz vigente (<see cref="MatrizVigenteDeAtribuciones"/>)
/// y sella en la solicitud la matriz, el nivel efectivo, el consolidado, el vinculado y la huella <c>RoutingSha256</c>. Si la huella cambia antes
/// de decidir (cambió el consolidado, una validación o el puntaje), se reenruta (<see cref="ReenrutarSiCambioAsync"/>). La huella es la que la
/// decisión compara con <c>expectedContentSha256</c>. (nuevo)
/// </summary>
public sealed class EnrutamientoDeSolicitud(
    IApplicationDbContext db,
    MatrizVigenteDeAtribuciones matrices,
    ExposicionConsolidadaDelDeudor exposicion,
    VinculadosVigentes vinculados,
    IDateTimeService reloj)
{
    /// <summary>Enruta con los datos dados (sin escribir).</summary>
    public async Task<Result<SolicitudEnrutada>> CalcularAsync(CreditApplication s, CreditLine linea, CreditLineVersion version,
        DatosDeEnrutamiento datos, DateOnly fecha, CancellationToken ct)
    {
        var matriz = await matrices.LeerAsync(fecha, reloj.UtcNow, ct);
        if (matriz is null) return Result.Failure<SolicitudEnrutada>(ErroresDeGobierno.SinMatrizVigente(fecha));

        var codeudores = await db.CreditApplicationParties.AsNoTracking()
            .AnyAsync(p => p.ApplicationId == s.Id && p.Role == CreditPartyRole.CoDebtor && !p.IsDeleted, ct);
        var sujeto = new SujetoAEnrutar(LendingDecisionSubjects.CreditApplication, datos.MontoConsolidado)
        {
            Linea = linea.Code,
            Modalidad = version.ModalityCode,
            PlazoMeses = s.TermMonths,
            ClasesDeGarantia = codeudores ? [CollateralKind.CoDebtor] : [],
            PuntajeBajoElMinimo = datos.PuntajeBajoElMinimo,
            ConAlertas = datos.ConAlertas,
            ExcepcionALaPolitica = s.PolicyExceptionRequested,
            HayVinculado = datos.HayVinculado,
            Hallazgos = datos.Hallazgos,
        };

        EnrutamientoDeAtribucion enrutado;
        try
        {
            enrutado = EnrutadorDeAtribuciones.Enrutar(sujeto, matriz.ParaElMotor);
        }
        catch (InvalidOperationException e)
        {
            return Result.Failure<SolicitudEnrutada>(new Error(AttributionMatrix.CodigoSinNivelFinal, e.Message));
        }

        var nivel = matriz.Niveles.Single(n => n.Code == enrutado.NivelEfectivo.Codigo);
        return Result.Success(new SolicitudEnrutada(matriz, enrutado, nivel, Huella(s, linea, version, matriz, enrutado, datos), datos));
    }

    /// <summary>Sella el enrutamiento en la solicitud (<c>Submitted</c>/<c>PendingDecision</c>/<c>Approved</c> → <c>PendingDecision</c>).</summary>
    public void Aplicar(CreditApplication s, SolicitudEnrutada e) =>
        s.Enrutar(e.Matriz.Matriz.Id, e.Nivel.Id, e.Huella, e.Datos.MontoConsolidado, e.Datos.HayVinculado, reloj.UtcNow);

    /// <summary>
    /// Los datos del enrutamiento con lo que la solicitud tiene guardado (las validaciones vigentes, el último puntaje) y la exposición de hoy:
    /// lo que ve quien decide en <c>GET /applications/{id}/routing</c> y lo que la decisión vuelve a calcular.
    /// </summary>
    public async Task<DatosDeEnrutamiento> DatosVigentesAsync(CreditApplication s, DateOnly fecha, CancellationToken ct)
    {
        var validaciones = CreditApplicationCheck.Vigentes(await db.CreditApplicationChecks.AsNoTracking()
            .Where(c => c.ApplicationId == s.Id && !c.IsDeleted).ToListAsync(ct));
        // De cada (clase, parte) cuenta la etapa más reciente.
        var ultimas = validaciones.GroupBy(c => (c.Kind, c.PartyId)).Select(g => g.OrderByDescending(c => c.EvaluatedAt).First()).ToList();
        var puntaje = await db.CreditScores.AsNoTracking().Where(x => x.ApplicationId == s.Id && !x.IsDeleted)
            .OrderByDescending(x => x.CalculatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var personas = await db.CreditApplicationParties.AsNoTracking().Where(p => p.ApplicationId == s.Id && !p.IsDeleted)
            .Select(p => p.PersonId).ToListAsync(ct);
        var expo = await exposicion.DeAsync(s.ApplicantPersonId, fecha, s.Id, ct);
        var vinculo = (await vinculados.DeAsync(personas, fecha, ct)).Count > 0;
        return new DatosDeEnrutamiento(s.RequestedAmount + expo.Total, vinculo, puntaje?.BelowMinimum == true,
            ultimas.Any(c => c.Outcome == CheckOutcome.Warning && c.Kind != ApplicationCheckKind.Scoring),
            NoExceptuables.Evaluar(ultimas.Where(c => c.Outcome == CheckOutcome.Blocking).Select(Hallazgo).OfType<MotivoNoExceptuable>()));
    }

    /// <summary>
    /// Vuelve a calcular con lo vigente y, si la huella cambió, reenruta la solicitud (sin guardar). Devuelve el enrutamiento vigente y si
    /// cambió.
    /// </summary>
    public async Task<Result<(SolicitudEnrutada Enrutada, bool Cambio)>> ReenrutarSiCambioAsync(CreditApplication s, CreditLine linea,
        CreditLineVersion version, DateOnly fecha, CancellationToken ct)
    {
        var datos = await DatosVigentesAsync(s, fecha, ct);
        var calculado = await CalcularAsync(s, linea, version, datos, fecha, ct);
        if (calculado.IsFailure) return Result.Failure<(SolicitudEnrutada, bool)>(calculado.Error);
        var cambio = !string.Equals(s.RoutingSha256, calculado.Value.Huella, StringComparison.Ordinal);
        if (cambio && s.Status is CreditApplicationStatus.PendingDecision or CreditApplicationStatus.Approved) Aplicar(s, calculado.Value);
        return Result.Success((calculado.Value, cambio));
    }

    /// <summary>El motivo no exceptuable que aporta una validación bloqueante, si lo aporta (R132).</summary>
    public static MotivoNoExceptuable? Hallazgo(CreditApplicationCheck c) => c.Kind switch
    {
        ApplicationCheckKind.PaymentCapacity when c.ErrorCode == ErroresDeOriginacion.CodigoCapacidadDePago && c.Outcome == CheckOutcome.Blocking =>
            MotivoNoExceptuable.PaymentCapacity,
        ApplicationCheckKind.DataAuthorization => MotivoNoExceptuable.HolderAuthorization,
        ApplicationCheckKind.RestrictiveLists when c.ErrorCode == ErroresDeOriginacion.CodigoCoincidenciaEnListas => MotivoNoExceptuable.RestrictiveListMatch,
        ApplicationCheckKind.Usury => MotivoNoExceptuable.Usury,
        _ => null,
    };

    /// <summary>
    /// La huella del enrutamiento (<c>RoutingSha256</c>, T63): la solicitud, lo pedido, la línea y su versión, la matriz y su versión, el nivel
    /// efectivo, las razones en su orden, el consolidado, el vinculado, el puntaje bajo, las alertas, la excepción pedida y los no exceptuables.
    /// </summary>
    public static string Huella(CreditApplication s, CreditLine linea, CreditLineVersion version, MatrizVigente matriz, EnrutamientoDeAtribucion e,
        DatosDeEnrutamiento d) =>
        new HuellaDeCartera()
            .Identificador("application", s.PublicId)
            .Decimal("requested", s.RequestedAmount)
            .Entero("termMonths", s.TermMonths)
            .Texto("creditLine", linea.Code)
            .Identificador("creditLineVersion", version.PublicId)
            .Identificador("matrix", matriz.Matriz.PublicId)
            .Entero("matrixVersion", matriz.Matriz.VersionNumber)
            .Texto("level", e.NivelEfectivo.Codigo)
            .Lista("reasons", e.Razones, (h, r) => h.Enumeracion("kind", (RazonDeEnrutamiento?)r.Clase).Texto("level", r.Nivel.Codigo))
            .Decimal("consolidated", d.MontoConsolidado)
            .Logico("relatedParty", d.HayVinculado)
            .Logico("scoreBelowMinimum", d.PuntajeBajoElMinimo)
            .Logico("warnings", d.ConAlertas)
            .Logico("policyException", s.PolicyExceptionRequested)
            .Lista("nonExceptionable", e.NoExceptuables, (h, m) => h.Enumeracion("reason", (MotivoNoExceptuable?)m))
            .Calcular();
}
