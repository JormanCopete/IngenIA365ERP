using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Lending.Housing;

/// <summary>
/// La reconstrucción de un crédito <b>en UVR</b> (feature 013, K6b, T1054; FR-024, D18): la misma reconstrucción de siempre corrida en UVR y valorada
/// en pesos con la UVR del día. La llama <see cref="ReconstruccionDeCredito.Reconstruir"/> cuando la entrada trae su serie
/// (<see cref="EntradaDeReconstruccion.Uvr"/>), así todo el que reconstruye por la fachada recibe pesos sin saber de la UVR.
///
/// <list type="number">
/// <item><b>A UVR</b>: el plan pactado y cada desembolso a la UVR publicada de su fecha (la del plan y la del primer desembolso son la misma, así el
/// plan cuadra); el saldo inicial migrado y su plan, a la del corte; la tolerancia de saldo vencido, a la de su vigencia; lo descontado pendiente de
/// giro, a la del descuento. Los pagos y las resoluciones se convierten <b>al aplicarse</b>, a la UVR de su fecha valor, con el estado de ese día
/// (<see cref="ConversionUvr.ValorDelPagoEnUvr"/>): la misma función que usan la simulación y la aplicación.</item>
/// <item><b>En UVR</b>: plan, causación (interés vencido sin capitalizar), pagos y saldos a la diezmilésima (<see cref="RedondeoDeCartera.EnUvr"/>).</item>
/// <item><b>A pesos</b>: saldos y plan a la UVR del día D (publicada o, si D no la tiene, proyectada y marcada); cada pago a la de su fecha valor por
/// acumulado contra lo recibido; las filas de las operaciones a la de su día (el desembolso y el saldo inicial, por lo que fueron en pesos); la
/// causación de cada día a la UVR de ese día; y la <b>corrección monetaria</b> (<see cref="LoanTransactionKind.MonetaryCorrection"/>) de cada día:
/// lo que cambió el valor en pesos de lo que se debe en UVR sin contar lo causado ni lo movido —el capital por su total (sin cuota) y los demás
/// conceptos por cuota—, así Σ libro en pesos = saldos en pesos del estado.</item>
/// </list>
/// Pura. (nuevo)
/// </summary>
public static class ReconstruccionEnUvr
{
    /// <summary>Lo que no es de una operación: la causación y lo que la acompaña.</summary>
    private static bool DeCausacion(LoanTransactionKind clase) => LibroTeorico.EsDeCausacion(clase);

    public static EstadoDelCredito Reconstruir(EntradaDeReconstruccion entrada, DateOnly fecha, DateOnly? libroDesde,
        IReadOnlyDictionary<LoanOperationKind, IInterpreteDeOperacion>? interpretes)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var serie = entrada.Uvr ?? throw new ArgumentException("La entrada no es de un crédito en UVR.", nameof(entrada));
        var dePesos = entrada.Condiciones.Redondeo;
        var modo = dePesos.Modo;

        var enUvr = AUvr(entrada, serie, fecha);
        var propios = new Dictionary<LoanOperationKind, IInterpreteDeOperacion>();
        if (interpretes is not null) foreach (var (clase, interprete) in interpretes) propios[clase] = interprete;
        foreach (var clase in new[] { LoanOperationKind.Payment, LoanOperationKind.ProvisionalCollection, LoanOperationKind.CreditBalanceResolution })
        {
            var interno = propios.GetValueOrDefault(clase) ?? ReconstruccionDeCredito.InterpretePropio(clase);
            if (interno is not null) propios[clase] = new InterpreteEnUvr(interno, serie, modo);
        }

        var uvr = ReconstruccionDeCredito.EnSuUnidad(enUvr, fecha, libroDesde, propios);
        if (uvr.Origen is not { } origen) return uvr;

        var delDia = serie.Del(fecha);
        var saldos = ConversionUvr.SaldosEnPesos(uvr.Saldos, delDia.Valor, modo);
        var capitalPorCuota = saldos.Where(s => s.Concepto == LoanConcept.Principal && s.Cuota is not null)
            .GroupBy(s => s.Cuota!.Value).ToDictionary(g => g.Key, g => g.Sum(s => s.Valor));
        var cuotas = uvr.Cuotas.Select(c => c with { CapitalPendiente = capitalPorCuota.GetValueOrDefault(c.Numero) }).ToList();
        var plan = uvr.Plan is { } p ? ConversionUvr.PlanEnPesos(p, delDia.Valor, dePesos) : null;

        var originales = entrada.Operaciones.ToDictionary(o => o.Id, StringComparer.Ordinal);
        var pagos = uvr.Pagos.ToDictionary(kv => kv.Key, kv => ConversionUvr.DistribucionEnPesos(kv.Value,
            ValorRecibido(originales.GetValueOrDefault(kv.Key), kv.Value), serie.Requerida(kv.Value.FechaValor), dePesos), StringComparer.Ordinal);
        var anticipos = uvr.AnticiposAplicados.Select(a => ConversionUvr.AnticipoEnPesos(a, serie.Del(a.Fecha).Valor, modo)).ToList();

        var libro = LibroEnPesos(uvr, entrada, serie, modo, uvr.Pagos, pagos, originales, fecha, libroDesde, origen);
        var valoracion = new ValoracionEnUvr(delDia, uvr, serie, dePesos);
        return new EstadoDelCredito(fecha, uvr.InformacionHasta, uvr.RulesVersion, origen, plan, cuotas, saldos, uvr.AlturaDeMora, uvr.MotivoDeSuspension,
            uvr.TasaDelDia, uvr.PrimerDiaSinUsura, uvr.Transiciones, libro, () => Explicar(uvr, delDia))
        {
            Pagos = pagos,
            AnticiposAplicados = anticipos,
            Uvr = valoracion,
        };
    }

    /// <summary>Lo que se recibió en pesos por un pago o una resolución (lo convertido vuelve a su valor en pesos, no a su conversión).</summary>
    private static decimal ValorRecibido(EntradaDeOperacion? original, DistribucionDePago enUvr) => original?.Contenido switch
    {
        ContenidoDePago pago => pago.Valor,
        ContenidoDeResolucion resolucion => resolucion.Monto,
        _ => original?.Monto ?? throw new InvalidOperationException("El pago en UVR no trae su valor en pesos."),
    };

    // ----------------------------------------------------------------------------------------------------------- a UVR

    /// <summary>La entrada en UVR: lo que no depende del estado, convertido a la UVR publicada de su fecha (ver la clase).</summary>
    private static EntradaDeReconstruccion AUvr(EntradaDeReconstruccion e, SerieUvr serie, DateOnly fecha)
    {
        var c = e.Condiciones;
        var modo = c.Redondeo.Modo;
        decimal U(decimal pesos, DateOnly dia) => ConversionUvr.AUvr(pesos, serie.Requerida(dia), modo);

        var vigentes = ReconstruccionDeCredito.OperacionesVigentes(e, fecha).Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        var origen = e.Operaciones.Where(o => vigentes.Contains(o.Id)).Select(o => (DateOnly?)o.FechaValor).Min();
        var operaciones = e.Operaciones.Select(o => vigentes.Contains(o.Id) ? OperacionEnUvr(o, U) : o).ToList();

        return e with
        {
            Condiciones = c with { Redondeo = RedondeoDeCartera.EnUvr(c.Redondeo) },
            Plan = e.Plan is { } p
                ? p with
                {
                    Monto = U(p.Monto, p.FechaDesembolso),
                    Redondeo = RedondeoDeCartera.EnUvr(p.Redondeo),
                    Extraordinarias = p.Extraordinarias.Select(x => x with { Valor = U(x.Valor, p.FechaDesembolso) }).ToList(),
                }
                : null,
            Operaciones = operaciones,
            Tolerancias = e.Tolerancias.Select(t => t with
            {
                Valor = ConversionUvr.AUvr(t.Valor, serie.Del(origen is { } o && o > t.Desde ? o : t.Desde).Valor, modo),
            }).ToList(),
            PendientesDeGiro = e.PendientesDeGiro.Select(g => g with { Monto = ConversionUvr.AUvr(g.Monto, serie.Del(g.FechaDelDescuento).Valor, modo) }).ToList(),
            Uvr = null,
        };
    }

    /// <summary>Una operación en UVR: los montos que no dependen del estado. Los pagos y las resoluciones los convierte <see cref="InterpreteEnUvr"/>.</summary>
    private static EntradaDeOperacion OperacionEnUvr(EntradaDeOperacion o, Func<decimal, DateOnly, decimal> u)
    {
        var v = o.FechaValor;
        var esPago = o.Clase is LoanOperationKind.Payment or LoanOperationKind.ProvisionalCollection or LoanOperationKind.CreditBalanceResolution;
        return o with
        {
            Monto = o.Monto is { } m && !esPago ? u(m, v) : o.Monto,
            Saldos = o.Saldos.Select(s => s with { Monto = u(s.Monto, v) }).ToList(),
            CuotasMigradas = o.CuotasMigradas.Select(q => q with { Capital = u(q.Capital, v) }).ToList(),
            Contenido = o.Contenido switch
            {
                ContenidoDeCargo cargo => cargo with { Monto = u(cargo.Monto, v) },
                ContenidoDeCondonacion condonacion => condonacion with
                {
                    Lineas = condonacion.Lineas.Select(l => l with { Monto = u(l.Monto, v) }).ToList(),
                },
                ContenidoDeNovedad novedad => novedad with
                {
                    Capitalizaciones = novedad.Capitalizaciones.Select(l => l with { Monto = u(l.Monto, v) }).ToList(),
                    Traslados = novedad.Traslados.Select(l => l with { Monto = u(l.Monto, v) }).ToList(),
                },
                ContenidoDeAjusteComercial ajuste => ajuste with { Monto = u(ajuste.Monto, v) },
                var otro => otro,
            },
        };
    }

    /// <summary>
    /// El intérprete de pagos y resoluciones de un crédito en UVR: convierte lo recibido en pesos a UVR a la UVR publicada de la fecha valor, con el
    /// estado de ese día, y deja el resto al intérprete de siempre.
    /// </summary>
    private sealed class InterpreteEnUvr(IInterpreteDeOperacion interno, SerieUvr serie, RoundingMode modo) : IInterpreteDeOperacion
    {
        public void Aplicar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion)
        {
            var v = operacion.FechaValor;
            var u = serie.Requerida(v);
            object? contenido = operacion.Contenido switch
            {
                ContenidoDePago pago when pago.Valor > 0m => pago with { Valor = ConversionUvr.ValorDelPagoEnUvr(contexto.Instantanea(v), pago.Valor, u, modo) },
                ContenidoDeResolucion r => r with { Monto = ResolucionEnUvr(contexto, r, u) },
                var otro => otro,
            };
            var monto = operacion.Contenido is null && operacion.Monto is { } m && m > 0m
                ? ConversionUvr.ValorDelPagoEnUvr(contexto.Instantanea(v), m, u, modo)
                : operacion.Monto;
            interno.Aplicar(contexto, operacion with { Contenido = contenido, Monto = monto });
        }

        /// <summary>Lo que se resuelve en UVR: si alcanza lo disponible en pesos, todo lo disponible en UVR; si no, la conversión.</summary>
        private decimal ResolucionEnUvr(ContextoDeReconstruccion contexto, ContenidoDeResolucion r, decimal u)
        {
            var disponible = contexto.SaldosFueraDeCausacion.Where(s => s.Key.Item1 == r.Concepto && s.Value > 0m).Sum(s => s.Value);
            return disponible > 0m && r.Monto >= ConversionUvr.APesos(disponible, u, modo) ? disponible : ConversionUvr.AUvr(r.Monto, u, modo);
        }
    }

    // --------------------------------------------------------------------------------------------------------- a pesos

    /// <summary>
    /// El libro en pesos: las filas de las operaciones (los pagos según su distribución en pesos; el desembolso y el saldo inicial por lo que fueron en
    /// pesos; lo demás a la UVR de su día), la causación a la UVR de su día y, con <paramref name="libroDesde"/>, la corrección monetaria de cada día.
    /// </summary>
    private static LibroTeorico LibroEnPesos(EstadoDelCredito uvr, EntradaDeReconstruccion original, SerieUvr serie, RoundingMode modo,
        IReadOnlyDictionary<string, DistribucionDePago> pagosEnUvr, IReadOnlyDictionary<string, DistribucionDePago> pagosEnPesos,
        IReadOnlyDictionary<string, EntradaDeOperacion> originales, DateOnly fecha, DateOnly? libroDesde, DateOnly origen)
    {
        var filasEnUvr = uvr.Libro.Movimientos;
        var enPesos = new List<MovimientoDeLibro>(filasEnUvr.Count);

        // Las filas de cada operación, agrupadas por operación y día.
        var porOperacion = filasEnUvr.Where(f => !DeCausacion(f.Clase)).GroupBy(f => (f.OperacionId, f.Dia));
        foreach (var grupo in porOperacion)
        {
            var u = serie.Del(grupo.Key.Dia).Valor;
            // Las filas del libro ya vienen ordenadas por concepto: cada una busca su componente de la distribución por concepto, cuota y valor.
            var esperadas = grupo.Key.OperacionId is { } id && pagosEnUvr.TryGetValue(id, out var dUvr) && pagosEnPesos.TryGetValue(id, out var dPesos)
                ? Componentes(dUvr).Zip(Componentes(dPesos), (a, b) => (a.Concepto, a.Cuota, Uvr: a.Monto, Pesos: b.Monto)).ToList()
                : [];
            var operacion = grupo.Key.OperacionId is { } oid ? originales.GetValueOrDefault(oid) : null;
            var iniciales = operacion?.Saldos.ToList() ?? [];
            foreach (var f in grupo)
            {
                decimal pesos;
                var i = esperadas.FindIndex(x => x.Concepto == f.Concepto && x.Cuota == f.Cuota && x.Uvr == f.Monto);
                if (i >= 0)
                {
                    pesos = esperadas[i].Pesos;
                    esperadas.RemoveAt(i);
                }
                else if (f.Clase == LoanTransactionKind.Disbursement && operacion?.Monto is { } desembolso)
                    pesos = desembolso;
                else if (f.Clase == LoanTransactionKind.OpeningBalance && iniciales.FirstOrDefault(s => s.Concepto == f.Concepto && s.Cuota == f.Cuota) is { } inicial)
                {
                    pesos = inicial.Monto;
                    iniciales.Remove(inicial);
                }
                else
                    pesos = ConversionUvr.APesos(f.Monto, u, modo);
                if (pesos != 0m) enPesos.Add(f with { Monto = pesos });
            }
        }

        // La causación de cada día, a la UVR de ese día.
        foreach (var f in filasEnUvr.Where(f => DeCausacion(f.Clase)))
        {
            var pesos = ConversionUvr.APesos(f.Monto, serie.Del(f.Dia).Valor, modo);
            if (pesos != 0m) enPesos.Add(f with { Monto = pesos });
        }

        if (libroDesde is { } desde) enPesos.AddRange(Correcciones(uvr, filasEnUvr, enPesos, serie, modo, fecha, desde > origen ? desde : origen));
        return new LibroTeorico(enPesos);
    }

    /// <summary>
    /// Los componentes de una distribución en el orden en que <c>ContextoDeReconstruccion.AsentarPago</c> asienta sus filas, con su signo: lo usado del
    /// anticipo (−), lo cubierto (−), el anticipo recibido (+) y el saldo a favor (+).
    /// </summary>
    private static IEnumerable<LineaDeDistribucion> Componentes(DistribucionDePago d)
    {
        foreach (var u in d.AnticipoUsado) if (u.Monto != 0m) yield return new LineaDeDistribucion(u.Cuota, LoanConcept.Advance, -u.Monto);
        foreach (var l in d.Lineas) if (l.Monto != 0m || (l.Concepto == LoanConcept.Principal && l.Cuota is null)) yield return l with { Monto = -l.Monto };
        if (d.Anticipo > 0m) yield return new LineaDeDistribucion(d.CuotaDelAnticipo, LoanConcept.Advance, d.Anticipo);
        if (d.SaldoAFavor > 0m) yield return new LineaDeDistribucion(null, LoanConcept.CreditBalance, d.SaldoAFavor);
    }

    /// <summary>La clave de la corrección monetaria: el capital por su total (sin cuota); los demás conceptos, por cuota.</summary>
    private static (LoanConcept Concepto, int? Cuota) Clave(LoanConcept concepto, int? cuota) =>
        concepto == LoanConcept.Principal ? (concepto, null) : (concepto, cuota);

    /// <summary>
    /// La corrección monetaria día por día desde <paramref name="desde"/> hasta <paramref name="fecha"/>: para cada clave, el valor en pesos de su saldo
    /// en UVR ese día menos el de la víspera menos lo que el libro en pesos ya movió ese día. El saldo en UVR de cada día sale del de D menos las filas
    /// posteriores.
    /// </summary>
    private static IEnumerable<MovimientoDeLibro> Correcciones(EstadoDelCredito uvr, IReadOnlyList<MovimientoDeLibro> filasEnUvr,
        IReadOnlyList<MovimientoDeLibro> enPesos, SerieUvr serie, RoundingMode modo, DateOnly fecha, DateOnly desde)
    {
        var alFinal = new Dictionary<(LoanConcept, int?), decimal>();
        foreach (var s in uvr.Saldos)
            alFinal[Clave(s.Concepto, s.Cuota)] = alFinal.GetValueOrDefault(Clave(s.Concepto, s.Cuota)) + s.Valor;
        foreach (var f in filasEnUvr) alFinal.TryAdd(Clave(f.Concepto, f.Cuota), 0m);

        var uvrPorDia = filasEnUvr.GroupBy(f => (f.Dia, Clave(f.Concepto, f.Cuota))).ToDictionary(g => g.Key, g => g.Sum(f => f.Monto));
        var pesosPorDia = enPesos.GroupBy(f => (f.Dia, Clave(f.Concepto, f.Cuota))).ToDictionary(g => g.Key, g => g.Sum(f => f.Monto));

        var salida = new List<MovimientoDeLibro>();
        foreach (var (clave, final) in alFinal.OrderBy(k => (int)k.Key.Item1).ThenBy(k => k.Key.Item2 ?? 0))
        {
            // El saldo en UVR al cierre de cada día desde la víspera de «desde», hacia atrás desde D.
            var saldo = final;
            var saldos = new Dictionary<DateOnly, decimal>();
            for (var d = fecha; d >= desde.AddDays(-1); d = d.AddDays(-1))
            {
                saldos[d] = saldo;
                saldo -= uvrPorDia.GetValueOrDefault((d, clave));
            }
            decimal Pesos(DateOnly d) => saldos[d] == 0m ? 0m : ConversionUvr.APesos(saldos[d], serie.Del(d).Valor, modo);
            for (var d = desde; d <= fecha; d = d.AddDays(1))
            {
                var correccion = Pesos(d) - Pesos(d.AddDays(-1)) - pesosPorDia.GetValueOrDefault((d, clave));
                if (correccion != 0m)
                    salida.Add(new MovimientoDeLibro(d, LoanTransactionKind.MonetaryCorrection, clave.Item1, clave.Item2, correccion));
            }
        }
        return salida;
    }

    private static ExplicacionDeCartera Explicar(EstadoDelCredito uvr, UvrDelDia delDia)
    {
        var e = new ExplicacionDeCartera()
            .Linea($"Crédito en UVR (reglas {uvr.RulesVersion}): plan, causación y saldos en UVR a la diezmilésima, valorados en pesos con la UVR del "
                + $"{ExplicacionDeCartera.Fecha(delDia.Fecha)}: {ExplicacionDeCartera.Numero(delDia.Valor)}"
                + (delDia.Proyectada
                    ? $" (proyectada desde la última publicada, del {ExplicacionDeCartera.Fecha(delDia.UltimaPublicada)}; sólo para mostrar)."
                    : " (publicada)."))
            .Linea($"Capital en UVR: {ExplicacionDeCartera.Numero(uvr.Saldo(LoanConcept.Principal))}; valor para cancelar en UVR: "
                + $"{ExplicacionDeCartera.Numero(uvr.ValorParaCancelar)}.");
        foreach (var linea in uvr.Explicacion.Lineas) e.Linea(linea);
        return e;
    }
}
