using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination.ConnectedGroups;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>Si la concentración se midió sobre la persona o sobre su grupo conectado (C112). (nuevo)</summary>
public enum AlcanceDeConcentracion
{
    Debtor = 1,
    Group = 2,
}

/// <summary>Una persona que suma en la exposición del grupo, con lo que suma. (nuevo)</summary>
public sealed record MiembroQueSuma(Guid PersonPublicId, decimal Exposicion);

/// <summary>
/// Una medición de concentración (FR-041): el límite en pesos (la fracción por la base), la exposición actual, lo pedido, la base —el
/// patrimonio técnico vigente— con su corte, si rige el límite con garantía admisible y, en el grupo, los miembros que suman. (nuevo)
/// </summary>
public sealed record MedicionDeConcentracion(
    AlcanceDeConcentracion Alcance,
    Guid? GrupoPublicId,
    decimal Limite,
    decimal Actual,
    decimal Pedido,
    decimal Base,
    decimal Fraccion,
    DateOnly? Corte,
    bool ConGarantiaAdmisible,
    IReadOnlyList<MiembroQueSuma> Miembros)
{
    public decimal Exceso => Math.Max(0m, Actual + Pedido - Limite);

    public bool Excede => Actual + Pedido > Limite;
}

/// <summary>La concentración por deudor y por grupo; <see cref="Excedida"/> es la primera que pasa el límite (la del deudor primero). (nuevo)</summary>
public sealed record ResultadoDeConcentracion(MedicionDeConcentracion PorDeudor, MedicionDeConcentracion? PorGrupo)
{
    public MedicionDeConcentracion? Excedida => PorDeudor.Excede ? PorDeudor : PorGrupo is { Excede: true } g ? g : null;

    public Error? Error => Excedida is { } m ? ErroresDeOriginacion.ConcentracionExcedida(m) : null;
}

/// <summary>
/// El límite de concentración (feature 013, T0392; FR-041, SC-022, T59, C33, C112): ningún desembolso, cupo ni novedad lleva el endeudamiento
/// de una persona —ni el de su grupo conectado— por encima del límite individual, con o sin garantía admisible.
/// <list type="bullet">
/// <item>La base es el patrimonio técnico de <see cref="IContabilidadParaCartera.IndicadoresDeLaEntidadAsync"/> (el último con corte ≤ la fecha);
/// sin indicador, o con uno más viejo que <c>Contabilidad.IndicadoresVigenciaMeses</c> (C33), no se puede validar:
/// <c>Lending.Concentration.TechnicalCapitalUnavailable</c> y la alerta <c>Cartera.PatrimonioTecnicoNoDisponible</c> al contador (T59), que
/// sobrevive al fallo de la operación (<see cref="AlertasDiferidas"/>).</item>
/// <item>La fracción es <c>Concentracion.LimiteIndividual</c> (o <c>.LimiteIndividualConGarantia</c>) acotada por su límite legal
/// (<see cref="ReglasDeParametrosDeCartera"/>: rige lo más estricto).</item>
/// <item>Por deudor suma su exposición consolidada (<see cref="ExposicionDelDeudor.Total"/>, como deudor y codeudor); por grupo, la directa de
/// cada miembro de <see cref="GrupoConectadoDelDeudor"/> (<see cref="ExposicionDelDeudor.Directa"/>), sin contar dos veces el crédito que un
/// miembro debe y otro respalda. Lo pedido suma en los dos.</item>
/// </list>
/// Excedido: <c>Lending.Concentration.LimitExceeded</c> con <c>scope</c>, límite, actual, pedido, exceso, base y los miembros. La usan la
/// radicación y la decisión, el cupo y, en US3 y K3, el desembolso y la novedad. (nuevo)
/// </summary>
public sealed class ValidacionDeConcentracion(
    IApplicationDbContext db,
    IContabilidadParaCartera contabilidad,
    ReglasDeParametrosDeCartera parametros,
    ExposicionConsolidadaDelDeudor exposicion,
    GrupoConectadoDelDeudor grupos,
    IAlertas alertas,
    AlertasDiferidas? diferidas = null,
    Risk.LectorDeIndicadoresDeLaEntidad? indicadores = null)
{
    /// <summary>US9 (T0703): el lector único de los indicadores con su vigencia (C33); la validación ya no lee el puerto por su cuenta.</summary>
    private readonly Risk.LectorDeIndicadoresDeLaEntidad _indicadores = indicadores ?? new Risk.LectorDeIndicadoresDeLaEntidad(contabilidad, parametros);

    /// <summary>
    /// Mide la concentración. Falla sólo si no se puede medir (sin patrimonio técnico vigente, sin el límite legal); excedida, lo dice el
    /// resultado (<see cref="ResultadoDeConcentracion.Error"/>).
    /// </summary>
    public async Task<Result<ResultadoDeConcentracion>> MedirAsync(int personId, decimal pedido, bool conGarantiaAdmisible, DateOnly fecha,
        int? sinLaSolicitud = null, CancellationToken ct = default)
    {
        var baseDeCalculo = await PatrimonioTecnicoAsync(fecha, ct);
        if (baseDeCalculo.IsFailure) return Result.Failure<ResultadoDeConcentracion>(baseDeCalculo.Error);
        var (patrimonio, corte) = baseDeCalculo.Value;

        var clave = conGarantiaAdmisible ? ParametrosDeCartera.ConcentracionLimiteIndividualConGarantia : ParametrosDeCartera.ConcentracionLimiteIndividual;
        var parametro = await parametros.LeerAsync(clave, fecha, ct: ct);
        if (parametro.IsFailure) return Result.Failure<ResultadoDeConcentracion>(parametro.Error);
        if (parametro.Value.Valor is not decimal fraccion)
            return Result.Failure<ResultadoDeConcentracion>(ErroresDeOriginacion.SinPatrimonioTecnico(fecha));
        var limite = decimal.Round(patrimonio * fraccion, 2, MidpointRounding.AwayFromZero);

        var grupo = await grupos.DeAsync(personId, fecha, ct);
        var exposiciones = await exposicion.DeVariasAsync(grupo.Miembros, fecha, sinLaSolicitud, ct);
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => grupo.Miembros.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);

        var propia = exposiciones[personId];
        var porDeudor = new MedicionDeConcentracion(AlcanceDeConcentracion.Debtor, null, limite, propia.Total, pedido, patrimonio, fraccion, corte,
            conGarantiaAdmisible, [new MiembroQueSuma(publicos[personId], propia.Total)]);

        MedicionDeConcentracion? porGrupo = null;
        if (!grupo.EsDeUno)
        {
            var miembros = grupo.Miembros.Select(m => new MiembroQueSuma(publicos[m], exposiciones[m].Directa)).ToList();
            porGrupo = new MedicionDeConcentracion(AlcanceDeConcentracion.Group, grupo.GrupoPublicId, limite, miembros.Sum(m => m.Exposicion), pedido,
                patrimonio, fraccion, corte, conGarantiaAdmisible, miembros);
        }
        return Result.Success(new ResultadoDeConcentracion(porDeudor, porGrupo));
    }

    /// <summary>Mide y falla también si se excede (<c>Lending.Concentration.LimitExceeded</c>).</summary>
    public async Task<Result<ResultadoDeConcentracion>> ValidarAsync(int personId, decimal pedido, bool conGarantiaAdmisible, DateOnly fecha,
        int? sinLaSolicitud = null, CancellationToken ct = default)
    {
        var medida = await MedirAsync(personId, pedido, conGarantiaAdmisible, fecha, sinLaSolicitud, ct);
        if (medida.IsFailure) return medida;
        return medida.Value.Error is { } error ? Result.Failure<ResultadoDeConcentracion>(error) : medida;
    }

    /// <summary>El patrimonio técnico vigente a la fecha y su corte, o el error con la alerta al contador.</summary>
    private async Task<Result<(decimal Patrimonio, DateOnly Corte)>> PatrimonioTecnicoAsync(DateOnly fecha, CancellationToken ct)
    {
        var leidos = await _indicadores.LeerAsync(fecha, ct);
        var tecnico = leidos.PatrimonioTecnico;
        if (leidos.PatrimonioTecnicoVigente) return Result.Success((tecnico!.Value, tecnico.CutoffDate));

        await AlertarAsync(new AlertaALevantar(TiposDeAlerta.PatrimonioTecnicoNoDisponible, "Patrimonio técnico no disponible",
            $"Al {ErroresDeOriginacion.Fecha(fecha)} no hay un patrimonio técnico vigente registrado en Contabilidad" +
            (tecnico is { } viejo ? $" (el último es del {ErroresDeOriginacion.Fecha(viejo.CutoffDate)})" : string.Empty) +
            ": Cartera no puede validar el límite de concentración y detiene la operación. Registre el indicador en Contabilidad.",
            DedupKey: $"{TiposDeAlerta.PatrimonioTecnicoNoDisponible}:{ErroresDeOriginacion.Fecha(fecha)}"), ct);
        return Result.Failure<(decimal, DateOnly)>(ErroresDeOriginacion.SinPatrimonioTecnico(fecha));
    }

    private async Task AlertarAsync(AlertaALevantar alerta, CancellationToken ct)
    {
        if (diferidas is { Activa: true })
        {
            diferidas.Encolar(alerta);
            return;
        }
        await alertas.LevantarAsync(alerta, ct);
    }
}
