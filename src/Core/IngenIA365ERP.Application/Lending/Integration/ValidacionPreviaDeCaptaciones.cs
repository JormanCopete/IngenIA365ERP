using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.Application.Lending.Integration.HechosRepresentativosDeCaptaciones;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Feature 014, US8 (T142, T145; contabilidad.md §7 de la 014; D23 «igual que los créditos», A8 de la 013; research E108): la validación previa contable de
/// las captaciones antes de operar. Arma los hechos representativos (<see cref="HechosRepresentativosDeCaptaciones"/>) con las dimensiones <b>vigentes</b> de
/// cada cuenta —una pregunta por combinación distinta, no por cuenta— y decide con la misma <see cref="ValidacionPreviaContableDeCartera"/> de los créditos:
/// <list type="bullet">
/// <item><b>Pantalla</b> (<see cref="EnPantallaAsync"/>): con <c>Contabilidad.FaltaDeRegla = Bloquear</c> responde el 422
/// <c>Lending.Prevalidation.NotPostable</c> con la regla que falta; con <c>Advertir</c>, confirma, sella y alerta.</item>
/// <item><b>Proceso</b> (<see cref="EnProcesoAsync"/> y <see cref="TandaAsync"/>, los subpasos del paso de captaciones): con <c>Bloquear</c> la tanda no
/// avanza —sus cuentas quedan en <c>LND_ProcessRunErrors</c> con <c>Lending.Prevalidation.NotPostable</c> y sale la alerta <c>Cartera.HechoNoContabilizable</c>—;
/// corregida la matriz, la pasada siguiente las retoma desde su marca de agua. Con <c>Advertir</c>, sin contabilidad iniciada o con el hecho en
/// <c>NoPasa</c> no pregunta nada.</item>
/// </list>
/// El contrato de cuentas sigue con su propia pregunta por lote (<c>ContratoDeCuentasDeCartera</c>), que ahora arma los mismos representativos. (nuevo)
/// </summary>
public sealed class ValidacionPreviaDeCaptaciones(IApplicationDbContext db, ValidacionPreviaContableDeCartera validacion)
{
    /// <summary>
    /// Las cuentas tal como las ve la pregunta a la fecha (research C20): línea, naturaleza, clase, marca —embargada, exenta del GMF a la fecha o estándar—,
    /// inactividad, la sucursal contable de su oficina, el titular y el número. Las cuentas cuya oficina no tiene sucursal no salen (no emiten, C9 de la 013).
    /// </summary>
    public async Task<IReadOnlyDictionary<int, CuentaRepresentativa>> DimensionesAsync(IReadOnlyCollection<int> cuentas, DateOnly fecha, CancellationToken ct)
    {
        if (cuentas.Count == 0) return new Dictionary<int, CuentaRepresentativa>();
        var ids = cuentas.Distinct().ToArray();
        var filas = await db.MemberAccounts.AsNoTracking().Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.Number, a.HolderPersonId, a.CreditLineId, a.Nature, a.DepositClass, a.Status, a.IsGarnished, a.BranchId }).ToListAsync(ct);
        var lineas = filas.Select(f => f.CreditLineId).Distinct().ToArray();
        var oficinas = filas.Select(f => f.BranchId).Distinct().ToArray();
        var personas = filas.Select(f => f.HolderPersonId).Distinct().ToArray();
        var codigos = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => lineas.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Code, ct);
        var sucursales = await db.Branches.AsNoTracking().Where(b => oficinas.Contains(b.Id) && !b.IsDeleted).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);
        var titulares = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var exentas = (await db.GmfExemptAccounts.AsNoTracking()
                .Where(g => ids.Contains(g.MemberAccountId) && !g.IsDeleted && g.ValidFrom <= fecha && (g.ValidTo == null || g.ValidTo >= fecha))
                .Select(g => g.MemberAccountId).ToListAsync(ct))
            .ToHashSet();
        var resultado = new Dictionary<int, CuentaRepresentativa>();
        foreach (var f in filas)
        {
            if (!sucursales.TryGetValue(f.BranchId, out var sucursal)) continue;
            var d = Dimensiones(codigos.GetValueOrDefault(f.CreditLineId) ?? string.Empty, f.Nature, f.DepositClass, sucursal, f.IsGarnished,
                f.Nature != LineNature.Contribution && exentas.Contains(f.Id), f.Status == MemberAccountStatus.Inactive);
            resultado[f.Id] = Cuenta(d, titulares.GetValueOrDefault(f.HolderPersonId), f.Number);
        }
        return resultado;
    }

    /// <summary>
    /// La pregunta de un subpaso del proceso por las cuentas de su tanda (una por combinación de dimensiones, con el titular y el número de la primera
    /// cuenta de la combinación): nulo si puede seguir; si la política bloquea, alerta y devuelve el fallo de cada cuenta (la tanda no se opera).
    /// <paramref name="contenidos"/> da los hechos que el subpaso emitiría para una cuenta.
    /// </summary>
    public async Task<IReadOnlyList<FalloDeCuenta>?> EnProcesoAsync(IReadOnlyList<int> cuentas, DateOnly d,
        Func<CuentaRepresentativa, IReadOnlyList<object>> contenidos, string operacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contenidos);
        if (cuentas.Count == 0) return null;
        var tipos = contenidos(Cuenta(new(), Guid.Empty)).Select(TipoDe).ToList();
        if (!await validacion.FaltaDeReglaPuedeBloquearAsync(tipos, d, ct)) return null;

        var representativas = (await DimensionesAsync(cuentas, d, ct)).Values.GroupBy(c => c.D).Select(g => g.First());
        var hechos = representativas.SelectMany(c => Unidad(d, c, [.. contenidos(c)])).ToList();
        if (await DecidirYAlertarAsync(hechos, operacion, ct) is not { } bloqueo) return null;
        return cuentas.Select(id => new FalloDeCuenta(id, bloqueo.Code, $"La tanda de {operacion} no avanzó: {bloqueo.Message}")).ToList();
    }

    /// <summary>
    /// Para los subpasos que operan cuenta por cuenta y sólo algunos días (la liquidación periódica, los cargos periódicos, el débito automático): las
    /// cuentas de la tanda leídas una vez y una pregunta por combinación, sólo para las cuentas que de verdad operan ese día.
    /// </summary>
    public async Task<PreguntaDeTanda> TandaAsync(IReadOnlyCollection<int> cuentas, DateOnly d, string operacion, CancellationToken ct) =>
        new(this, await DimensionesAsync(cuentas, d, ct), d, operacion);

    /// <summary>La decisión de un proceso para unos hechos (nula si puede seguir); si bloquea, alerta.</summary>
    internal async Task<Error?> DecidirYAlertarAsync(IReadOnlyList<MensajeContableDto> hechos, string operacion, CancellationToken ct)
    {
        var decision = await validacion.DecidirEnProcesoAsync(hechos, ct);
        if (decision?.Bloqueo is not { } bloqueo) return null;
        await validacion.AlertarBloqueoAsync(decision.Resultado, operacion, ct);
        return bloqueo;
    }

    /// <summary>La pregunta de una pantalla (D23): bloquea con la política en <c>Bloquear</c>; si no, sella y alerta.</summary>
    public Task<DecisionDeValidacionPrevia> EnPantallaAsync(IReadOnlyList<MensajeContableDto> hechos, string operacion, CancellationToken ct) =>
        validacion.DecidirAsync(hechos, OrigenDeValidacionPrevia.Pantalla, operacion, ct);
}

/// <summary>
/// La validación previa de una tanda del proceso cuenta por cuenta (feature 014, US8, T142): una pregunta por combinación de dimensiones y hechos, guardada
/// para las demás cuentas de la tanda. (nuevo)
/// </summary>
public sealed class PreguntaDeTanda
{
    private readonly ValidacionPreviaDeCaptaciones _validacion;
    private readonly IReadOnlyDictionary<int, CuentaRepresentativa> _cuentas;
    private readonly DateOnly _dia;
    private readonly string _operacion;
    private readonly Dictionary<string, Error?> _respuestas = new(StringComparer.Ordinal);

    internal PreguntaDeTanda(ValidacionPreviaDeCaptaciones validacion, IReadOnlyDictionary<int, CuentaRepresentativa> cuentas, DateOnly dia,
        string operacion)
    {
        _validacion = validacion;
        _cuentas = cuentas;
        _dia = dia;
        _operacion = operacion;
    }

    /// <summary>El fallo de la cuenta si sus hechos no se pueden contabilizar y la política bloquea; nulo si puede operar.</summary>
    public async Task<FalloDeCuenta?> CuentaAsync(int cuenta, Func<CuentaRepresentativa, IReadOnlyList<object>> contenidos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contenidos);
        if (!_cuentas.TryGetValue(cuenta, out var c)) return null;
        // La combinación sin titular ni número (que no cambian la regla): la misma respuesta para todas las cuentas de la combinación.
        var anonima = c with { Titular = Guid.Empty, Numero = NumeroDeValidacion };
        var clave = string.Join("||", Unidad(_dia, anonima, [.. contenidos(anonima)]).Select(ValidacionPreviaContableDeCartera.ClaveDeCombinacion));
        if (!_respuestas.TryGetValue(clave, out var bloqueo))
        {
            bloqueo = await _validacion.DecidirYAlertarAsync(Unidad(_dia, c, [.. contenidos(c)]), _operacion, ct);
            _respuestas[clave] = bloqueo;
        }
        return bloqueo is null ? null : new FalloDeCuenta(cuenta, bloqueo.Code, $"La tanda de {_operacion} no avanzó en esta cuenta: {bloqueo.Message}");
    }
}
