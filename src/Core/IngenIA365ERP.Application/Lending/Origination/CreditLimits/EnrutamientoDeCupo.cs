using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination.CreditLimits;

/// <summary>Un cupo enrutado (T63): la matriz vigente, el resultado del motor, el nivel efectivo, la huella, el consolidado y el vinculado. (nuevo)</summary>
public sealed record CupoEnrutado(MatrizVigente Matriz, EnrutamientoDeAtribucion Enrutamiento, AttributionLevel Nivel, string Huella, decimal Consolidado,
    bool Vinculado)
{
    /// <summary>¿La matriz pide un nivel por algo más que el techo de monto (una regla, un vinculado, una excepción)?</summary>
    public bool PideOtroNivel => Enrutamiento.Razones.Any(r => r.Clase != RazonDeEnrutamiento.AmountCeiling);

    public RutaDelCupo Ruta() => new(Matriz.Matriz.Id, Matriz.Matriz.VersionNumber, Nivel.Id, Nivel.Code, Huella, Consolidado, Vinculado,
        Enrutamiento.Razones.Select(r => new RoutingReasonDto(r.Clase, r.Nivel.Codigo, r.Detalle)).ToList());
}

/// <summary>
/// El enrutamiento de un cupo (feature 013, US2 bloque 6B; T0405; T63, FR-040, FR-049): sujeto <c>CreditLimit</c> con el monto consolidado (el
/// cupo más la exposición de la persona como deudora y codeudora), la línea y su modalidad, y si la persona es vinculada (al consejo, FR-040). La
/// huella cubre la persona, la clase, la línea, el monto, la matriz, el nivel, las razones, el consolidado y el vinculado: si cambia entre que se
/// asignó y se decide, <c>Lending.Decision.ContentChanged</c>. (nuevo)
/// </summary>
public sealed class EnrutamientoDeCupo(
    IApplicationDbContext db,
    MatrizVigenteDeAtribuciones matrices,
    ExposicionConsolidadaDelDeudor exposicion,
    VinculadosVigentes vinculados,
    Configuration.VersionVigenteDeLinea versiones,
    IDateTimeService reloj)
{
    public async Task<Result<CupoEnrutado>> CalcularAsync(int personId, CreditLimitKind clase, int? lineaId, decimal monto, DateOnly fecha, CancellationToken ct)
    {
        var matriz = await matrices.LeerAsync(fecha, reloj.UtcNow, ct);
        if (matriz is null) return Result.Failure<CupoEnrutado>(ErroresDeGobierno.SinMatrizVigente(fecha));

        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == personId).Select(p => p.PublicId).FirstAsync(ct);
        var expo = await exposicion.DeAsync(personId, fecha, null, ct);
        var consolidado = monto + expo.Total;
        var vinculado = (await vinculados.DeAsync([personId], fecha, ct)).Count > 0;
        var (codigoDeLinea, modalidad) = lineaId is { } l ? await CodigoYModalidadAsync(l, fecha, ct) : (null, null);

        var sujeto = new SujetoAEnrutar(LendingDecisionSubjects.CreditLimit, consolidado)
        {
            Linea = codigoDeLinea,
            Modalidad = modalidad,
            HayVinculado = vinculado,
        };
        EnrutamientoDeAtribucion enrutado;
        try
        {
            enrutado = EnrutadorDeAtribuciones.Enrutar(sujeto, matriz.ParaElMotor);
        }
        catch (InvalidOperationException e)
        {
            return Result.Failure<CupoEnrutado>(new Error(AttributionMatrix.CodigoSinNivelFinal, e.Message));
        }

        var nivel = matriz.Niveles.Single(n => n.Code == enrutado.NivelEfectivo.Codigo);
        var huella = new HuellaDeCartera()
            .Texto("subject", LendingDecisionSubjects.CreditLimit)
            .Identificador("person", persona)
            .Enumeracion("kind", (CreditLimitKind?)clase)
            .Texto("creditLine", codigoDeLinea)
            .Decimal("amount", monto)
            .Identificador("matrix", matriz.Matriz.PublicId)
            .Entero("matrixVersion", matriz.Matriz.VersionNumber)
            .Texto("level", enrutado.NivelEfectivo.Codigo)
            .Lista("reasons", enrutado.Razones, (h, r) => h.Enumeracion("kind", (RazonDeEnrutamiento?)r.Clase).Texto("level", r.Nivel.Codigo))
            .Decimal("consolidated", consolidado)
            .Logico("relatedParty", vinculado)
            .Calcular();
        return Result.Success(new CupoEnrutado(matriz, enrutado, nivel, huella, consolidado, vinculado));
    }

    /// <summary>El código de la línea y la modalidad de su versión vigente (lo que miran las reglas de la matriz).</summary>
    private async Task<(string? Codigo, string? Modalidad)> CodigoYModalidadAsync(int lineaId, DateOnly fecha, CancellationToken ct)
    {
        var codigo = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == lineaId).Select(l => l.Code).FirstOrDefaultAsync(ct);
        var version = await versiones.LeerAsync(lineaId, fecha, ct: ct);
        return (codigo, version?.Version.ModalityCode);
    }
}
