#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Hoja de calculo de los casos dorados de Cartera (feature 013, T0330, T26; quickstart 1.2).

Calcula A MANO, con decimal de 40 digitos y sin tocar el codigo C# que se prueba, lo que esperan los casos de
Casos/*.json: el plan cuota a cuota (capital, interes, seguro, saldo), las tasas (diaria, nominal del periodo,
efectiva con y sin cargos) y el veredicto de usura. Nunca en Excel: el double y los empates de redondeo dan otras
cifras (T26).

US5 (fase 9, T0513) suma los pagos (bloque «US5»): prelacion, las siete modalidades, anticipo al vencer, abono con su eleccion,
cancelacion y resolucion de saldos; el "esperado" de cada operacion de pago (su distribucion con la informacion hasta su registro) y,
en las consultas con "conPagos", la distribucion de cada pago a (D, R), que muestra la redistribucion.

K3 (fase 16, T0858) suma las NOVEDADES (bloque «K3»): el plan regenerado desde la fecha de la decision por una prorroga, un cambio
de fecha o de tasa, una novacion o una refinanciacion, con su usura; el "esperado" de cada novedad del arreglo "novedades".

US4 (fase 8, T0464) la amplia con la causacion, la mora, la suspension, la regeneracion del plan (tramos, reajustes), el plan
migrado y la reconstruccion a (D, R), calculadas DIA POR DIA (el C# avanza por segmentos entre eventos), para los "esperado" de
las "consultas" de cada caso y de cada variante: modo "Reconstruccion" (defecto), "PagosATiempo", "Causacion" y "Diferencias".
Las reglas de US4 estan en el bloque «US4» mas abajo.

Uso (desde cualquier carpeta):
    python calcular.py                 # imprime lo esperado de todos los casos
    python calcular.py 01-cuota-fija   # solo los archivos que empiezan asi
    python calcular.py --verificar     # compara con el bloque "esperado" y el de cada consulta (sale 1 si difiere)
    python calcular.py --escribir      # reescribe el "esperado" del plan (el ultimo del archivo), el arreglo "consultas" y, si
                                       # sus variantes traen consultas, el arreglo "variantes", con lo calculado

Reglas que implementa (las de decisiones-transversales T25-T29, research R33-R37, D1, D3, D9):
- Dias de a excluida a b incluida: 30/360 (el 31 vale 30 y el fin de febrero vale 30), real/365 y real/360.
- Tasa diaria compuesta: raiz B-esima de (1+EA) a 24 decimales (al par), menos 1, a 18 decimales (al par).
  Lineal (D1): NA_m = m*((1+EA)^(1/m) - 1) con la raiz a 24 decimales; r_d = NA_m/B a 18 decimales.
- Tasa de n dias: (1+r_d)^n - 1 compuesta, r_d*n lineal. El interes de la cuota: saldo x tasa del periodo.
- Cuota fija por valor presente con los factores reales: A = (saldo - sum E_k v_k) / sum v_k.
- Gracia total: interes sin capitalizar = sum monto x i_j, a la primera cuota o distribuido.
- Seguros: base x tarifa x factor (1 si cobra con la periodicidad del plan). 2026-11-16 (feature 024, fase 2): un seguro con
  "clase": "Administration" es la cuota de ADMINISTRACION: se calcula igual, pero la cuota la lleva aparte ("administracion") y el libro en
  su propio concepto Administration, causado con su propio momento ("causacionDeAdministracion"); la cuota nivelada por cuota usa la suma.
- Redondeo por concepto, por cuota o por total; la ultima cuota paga el saldo.
- Usura: TIR diaria de los flujos con fechas reales, (1+TIR)^365 - 1.
- 2026-11-24 (feature 025, HU2): con "tasaMV" (mensual vencida pactada) la tasa diaria es MV/30 a 18 decimales, lineal, en 30/360 (si no,
  Lending.Rate.MonthlyRequiresThirty360); la nominal del periodo es MV x dias del periodo / 30; la E.A. derivada (1+MV)^12 - 1 (a 18) es la
  que se compara con la usura del dia y, si la pasa, la tasa del dia es la mensual equivalente de la usura (menos la sobretasa) entre 30.
  "tasaEA" queda como la E.A. derivada a seis decimales, sólo informativa. La mora sigue en E.A.
- 2026-11-24 (feature 025, fase 3, HU1 + HU3), escrito desde la regla (FR-003, FR-004, FR-013; D1, D2) y no desde el C#:
  * "ajustesDelCorte" (fecha, cuota, valor, registradoEn): el interes del periodo al corte que se cobra en una cuota sin vencer; la reconstruccion
    lo suma al interes corriente de esa cuota el dia de su fecha, fuera de la causacion (fila CutoverInterestAdjustment), despues de las
    operaciones de ese dia. El valor de una cuota al vencer (valor_de_cuota) suma el interes que la cuota lleva fuera del plan: el saldo inicial
    de corriente (o su suspenso) que trajo en la carga y sus ajustes del corte.
  * "propuestasDelCorte" (corte, hoy, informacionHasta, desembolso): lo que propone el recalculo. La MV redonda (4 decimales en fraccion) mas
    cercana a la E.A. sellada si su E.A. derivada difiere menos de 0,00005; con ella las condiciones pasan a MV, 30/360 y lineal. La primera
    cuota con vencimiento posterior al corte; el inicio de su periodo (el vencimiento de la anterior del plan, su "desde" cargado si es anterior
    al corte, el desembolso si es la cuota 1, o su vencimiento menos un periodo); el capital no vencido al corte; el interes del periodo completo
    (capital x tasa del periodo, redondeado como un concepto); lo causado de esa cuota del dia siguiente al corte a su vencimiento (la
    reconstruccion proyectada); lo que trajo en la carga; el ajuste (la diferencia redondeada, si pasa de un peso) y la cuota destino (la
    primera que vence despues de hoy).
- 2026-11-24 (feature 025, fase 4, HU4; FR-014, FR-015), escrito desde la regla y no desde el C#:
  * "periodosCompletos" (corte, desembolso, registradoEn): la carga con la opcion "el periodo en curso al corte se causa completo". La primera
    cuota cargada con vencimiento posterior al corte toma como inicio de su periodo el vencimiento de la cuota cargada anterior; si no la hay y
    es la cuota 1, el desembolso; si no, su vencimiento menos un periodo. Sobre el plan migrado con ese inicio (el interes sigue desde el
    corte): el interes del periodo completo (saldo inicial de la cuota x tasa pactada del periodo, redondeado como un concepto), lo que el plan
    causa desde el corte (su interes), lo que trajo en la carga y el ajuste (la diferencia redondeada si pasa de un peso), que se cobra en esa
    misma cuota. Comprueba que da lo mismo que la propuesta del recalculo del corte (fase 3) con hoy = corte, y deja el ajuste como
    "ajustesDelCorte" del dia de la activacion (corte + 1) registrado en "registradoEn", que las consultas leen.
"""
import calendar
import datetime as dt
import json
import os
import re
import sys
from decimal import Decimal as D, getcontext, ROUND_HALF_UP, ROUND_HALF_EVEN, ROUND_CEILING, ROUND_FLOOR

getcontext().prec = 40

AQUI = os.path.dirname(os.path.abspath(__file__))
MODOS = {"AwayFromZero": ROUND_HALF_UP, "ToEven": ROUND_HALF_EVEN, "Up": ROUND_CEILING, "Down": ROUND_FLOOR}
DIAS_DE = {"SinglePayment": 0, "Weekly": 7, "TenDay": 10, "Biweekly": 15, "Monthly": 30, "Bimonthly": 60,
           "Quarterly": 90, "SemiAnnual": 180, "Annual": 360}


# ------------------------------------------------------------------------------------------------ JSON con comentarios
def sin_comentarios(texto):
    salida, i, en_cadena = [], 0, False
    while i < len(texto):
        c = texto[i]
        if en_cadena:
            salida.append(c)
            if c == "\\":
                salida.append(texto[i + 1]); i += 2; continue
            if c == '"':
                en_cadena = False
            i += 1; continue
        if c == '"':
            en_cadena = True; salida.append(c); i += 1; continue
        if texto.startswith("//", i):
            while i < len(texto) and texto[i] != "\n":
                i += 1
            continue
        if texto.startswith("/*", i):
            i = texto.index("*/", i) + 2; continue
        salida.append(c); i += 1
    return re.sub(r",(\s*[}\]])", r"\1", "".join(salida))


def cargar(ruta):
    with open(ruta, encoding="utf-8") as f:
        return json.loads(sin_comentarios(f.read()), parse_float=D, parse_int=D)


def fecha(s):
    return dt.date.fromisoformat(s)


# --------------------------------------------------------------------------------------------------------------- dias
def es_fin_de_febrero(f):
    return f.month == 2 and f.day == calendar.monthrange(f.year, 2)[1]


def dias(conv, a, b):
    assert b >= a
    if conv == "Thirty360":
        da = 30 if (a.day == 31 or es_fin_de_febrero(a)) else a.day
        db = 30 if (b.day == 31 or es_fin_de_febrero(b)) else b.day
        return 360 * (b.year - a.year) + 30 * (b.month - a.month) + (db - da)
    return (b - a).days


def base(conv):
    return 365 if conv == "Actual365" else 360


# -------------------------------------------------------------------------------------------------------------- tasas
def q(x, decimales, modo=ROUND_HALF_EVEN):
    return x.quantize(D(1).scaleb(-decimales), rounding=modo)


def raiz(x, n):
    """Raiz n-esima a 40 digitos, redondeada a 24 decimales al par (lo que entrega AritmeticaDecimal.RaizEntera)."""
    if n == 1:
        return q(x, 24)
    return q((x.ln() / D(n)).exp(), 24)


def periodos_por_anio(periodicidad):
    d = DIAS_DE[periodicidad]
    if d == 0:
        return 1
    return 360 // d if 360 % d == 0 else 365 // d


def tasa_diaria(ea, conv, equivalencia, periodicidad):
    b = base(conv)
    if equivalencia == "Linear":
        m = periodos_por_anio(periodicidad)
        na = m * (raiz(1 + ea, m) - 1)
        return q(na / b, 18)
    return q(raiz(1 + ea, b) - 1, 18)


def nominal_del_periodo(ea, periodicidad):
    return q(raiz(1 + ea, periodos_por_anio(periodicidad)) - 1, 18)


# 2026-11-24 (feature 025, HU2): la tasa pactada en mensual vencida.
def mv_de(c):
    return D(c["tasaMV"]) if c.get("tasaMV") is not None else None


def efectiva_de_mensual(mv):
    return q((1 + mv) ** 12 - 1, 18)


def tasa_diaria_pactada(ea, mv, conv, equivalencia, periodicidad):
    if mv is None:
        return tasa_diaria(ea, conv, equivalencia, periodicidad)
    assert conv == "Thirty360" and equivalencia == "Linear", "Lending.Rate.MonthlyRequiresThirty360"
    return q(mv / 30, 18)


def nominal_pactada(ea, mv, periodicidad):
    if mv is None:
        return nominal_del_periodo(ea, periodicidad)
    if DIAS_DE[periodicidad] == 0:
        return nominal_del_periodo(efectiva_de_mensual(mv), periodicidad)
    return q(mv * DIAS_DE[periodicidad] / 30, 18)


def tasa_del_periodo(rd, n, equivalencia):
    return rd * n if equivalencia == "Linear" else (1 + rd) ** n - 1


# ------------------------------------------------------------------------------------------------------------- fechas
def en_el_dia(anio, mes, dia):
    return dt.date(anio, mes, min(dia, calendar.monthrange(anio, mes)[1]))


def sumar_meses(f, meses):
    total = f.year * 12 + (f.month - 1) + meses
    return total // 12, total % 12 + 1


def vencimientos(desembolso, periodicidad, periodos, dia_de_pago, primer):
    p = DIAS_DE[periodicidad]
    if p == 0:
        assert periodos == 1 and primer is not None
        return [primer]
    if p % 30 == 0:                                     # por meses
        meses = p // 30
        if primer is not None:
            uno, dia = primer, (dia_de_pago or primer.day)
        elif dia_de_pago is not None:
            dia = dia_de_pago
            ly, lm = sumar_meses(desembolso, meses)
            limite = en_el_dia(ly, lm, desembolso.day)
            uno = en_el_dia(ly, lm, dia)
            if uno < limite:
                ny, nm = sumar_meses(dt.date(ly, lm, 1), 1)
                uno = en_el_dia(ny, nm, dia)
        else:
            dia = desembolso.day
            ly, lm = sumar_meses(desembolso, meses)
            uno = en_el_dia(ly, lm, dia)
        salida = [uno]
        for k in range(1, periodos):
            y, m = sumar_meses(dt.date(uno.year, uno.month, 1), meses * k)
            salida.append(en_el_dia(y, m, dia))
        return salida
    if 30 % p == 0:                                     # partes del mes (quincenal, decadal)
        dia = dia_de_pago or (primer.day if primer else desembolso.day)
        desfase = (dia - 1) % p + 1
        def ranuras(desde):
            y, m = desde.year, desde.month
            while True:
                for j in range(30 // p):
                    yield en_el_dia(y, m, desfase + j * p)
                y, m = sumar_meses(dt.date(y, m, 1), 1)
        if primer is not None:
            uno = primer
        else:
            limite = desembolso + dt.timedelta(days=p)
            uno = next(f for f in ranuras(limite) if f >= limite)
        salida, anterior = [uno], uno
        for f in ranuras(anterior):
            if len(salida) == periodos:
                break
            if f > anterior:
                salida.append(f); anterior = f
        return salida
    uno = primer or desembolso + dt.timedelta(days=p)     # semanal
    return [uno + dt.timedelta(days=p * k) for k in range(periodos)]


# ------------------------------------------------------------------------------------------------------------ redondeo
class Redondeo:
    def __init__(self, r):
        self.unidad, self.modo, self.momento = D(r["unidad"]), MODOS[r["modo"]], r["momento"]
        # K6b (T1042): la unidad minima de un monto; el centavo en pesos, la diezmilesima en UVR (la pone vivienda.py).
        self.minima = D(r.get("minima", "0.01"))

    def a(self, v, unidad):
        return (v / unidad).quantize(D(1), rounding=self.modo) * unidad

    def unidad_(self, v):
        return self.a(v, self.unidad)

    def centavo(self, v):
        return self.a(v, self.minima)

    def concepto(self, v):
        return self.unidad_(v) if self.momento == "PerConcept" else self.centavo(v)

    def cuota_nivelada(self, v):
        return self.centavo(v) if self.momento == "PerTotal" else self.unidad_(v)


# ---------------------------------------------------------------------------------------------------------------- plan
def factor_de_seguro(seg, periodicidad, dias_periodo, es_primero):
    cobro = seg["periodicidad"]
    if cobro == "SinglePayment":
        return D(1) if es_primero else D(0)
    if cobro == periodicidad:
        return D(1)
    if periodicidad == "SinglePayment":
        return D(dias_periodo) / D(DIAS_DE[cobro])
    return D(DIAS_DE[periodicidad]) / D(DIAS_DE[cobro])


def es_administracion(seg):
    """2026-11-16 (feature 024, fase 2): el cargo de la linea de clase administracion."""
    return seg.get("clase") == "Administration"


def tiene_administracion(seguros):
    return any(es_administracion(s) for s in seguros)


def cargos_de_cuota(seguros, r, base_saldo, monto, periodicidad, dias_periodo, es_primero, extras=None):
    """Lo que cobran los seguros y la administracion en una cuota, cada cargo redondeado por su lado: (seguro, administracion)."""
    seg, adm = D(0), D(0)
    for s, x in enumerate(seguros):
        base_s = base_saldo if x["base"] == "OutstandingBalance" else monto
        exacto = base_s * D(x["tarifa"]) * factor_de_seguro(x, periodicidad, dias_periodo, es_primero)
        if extras is not None:
            exacto += extras[s]
        if es_administracion(x):
            adm += r.concepto(exacto)
        else:
            seg += r.concepto(exacto)
    return seg, adm


def nivelada(saldo, desde, tasas, extra):
    v, suma, pve = D(1), D(0), D(0)
    for j in range(desde, len(tasas)):
        v = v / (1 + tasas[j]); suma += v; pve += extra[j] * v
    return (saldo - pve) / suma


def generar_plan(c):
    monto = D(c["monto"]); n = int(c["plazoCuotas"]); periodicidad = c["periodicidad"]
    conv, eq, sistema = c["convencion"], c["equivalencia"], c["sistema"]
    ea = D(c["tasaEA"]); r = Redondeo(c["redondeo"])
    desembolso = fecha(c["fechaDesembolso"])
    primer = fecha(c["primerVencimiento"]) if c.get("primerVencimiento") else None
    dia_de_pago = int(c["diaDePago"]) if c.get("diaDePago") else None
    gracia = c.get("gracia") or {"clase": "None", "periodos": 0}
    clase = gracia.get("clase", "None"); g = int(gracia.get("periodos", 0)) if clase != "None" else 0
    total_g = clase == "Total"; tratamiento = gracia.get("tratamiento") or "FirstInstallment"
    nd = int(gracia["cuotasDeDistribucion"]) if gracia.get("cuotasDeDistribucion") else None
    seguros = c.get("seguros") or []

    mv = mv_de(c)
    rd = tasa_diaria_pactada(ea, mv, conv, eq, periodicidad)
    fechas = vencimientos(desembolso, periodicidad, n, dia_de_pago, primer)
    desde = [desembolso] + fechas[:-1]
    ds = [dias(conv, desde[k], fechas[k]) for k in range(n)]
    tasas = [tasa_del_periodo(rd, ds[k], eq) for k in range(n)]
    amortizan = n - g
    extra = [D(0)] * n
    for x in c.get("extraordinarias") or []:
        idx = int(x["cuota"]) - 1 + (g if total_g else 0)
        extra[idx] += r.concepto(D(x["valor"]))

    a = nivelada(monto, g, tasas, extra) if sistema == "LevelPayment" else D(0)
    cuota_nivelada = r.cuota_nivelada(a) if sistema == "LevelPayment" else None
    abono = (monto - sum(extra)) / amortizan if sistema == "ConstantPrincipal" else D(0)

    saldo = monto; ig_exacto = D(0); sg_exacto = [D(0)] * len(seguros)
    interes_gracia = D(0); porcion_g = D(0); repartida = D(0); residuo = D(0); cuotas = []
    for k in range(n):
        if total_g and k < g:
            ig_exacto += saldo * tasas[k]
            for s, seg in enumerate(seguros):
                base_s = saldo if seg["base"] == "OutstandingBalance" else monto
                sg_exacto[s] += base_s * D(seg["tarifa"]) * factor_de_seguro(seg, periodicidad, ds[k], False)
            continue
        numero = k - g + 1 if total_g else k + 1
        ultimo = k == n - 1
        saldo_ini = saldo
        interes = r.concepto(saldo * tasas[k])
        porcion = D(0)
        if total_g:
            if k == g:
                interes_gracia = r.concepto(ig_exacto)
                porcion_g = r.concepto(interes_gracia / nd) if tratamiento == "Distributed" else interes_gracia
            if tratamiento == "Distributed":
                pendiente = interes_gracia - repartida
                porcion = min(porcion_g, pendiente) if numero < nd else (pendiente if numero == nd else D(0))
                repartida += porcion
            elif k == g:
                porcion = interes_gracia
        interes_total = interes + porcion
        solo_seg, adm_cuota = cargos_de_cuota(seguros, r, saldo_ini, monto, periodicidad, ds[k], numero == 1,
                                              sg_exacto if total_g and k == g else None)
        seg_cuota = solo_seg + adm_cuota  # la cuota nivelada por cuota usa la suma de los cargos
        computable = D(0)
        for s, seg in enumerate(seguros):
            if seg.get("computa"):
                base_s = saldo_ini if seg["base"] == "OutstandingBalance" else monto
                exacto = base_s * D(seg["tarifa"]) * factor_de_seguro(seg, periodicidad, ds[k], numero == 1)
                if total_g and k == g:
                    exacto += sg_exacto[s]
                computable += r.concepto(exacto)
        solo_interes = (not total_g) and k < g
        if solo_interes:
            capital = D(0)
        else:
            if sistema == "LevelPayment":
                teorico = (r.unidad_(a + extra[k] + porcion + seg_cuota) - interes_total - seg_cuota
                           if r.momento == "PerInstallment" else r.cuota_nivelada(a) + extra[k] - interes)
            elif sistema == "ConstantPrincipal":
                teorico = (r.unidad_(abono + extra[k] + interes_total + seg_cuota) - interes_total - seg_cuota
                           if r.momento == "PerInstallment" else r.concepto(abono) + extra[k])
            else:
                teorico = monto
            if ultimo:
                capital = saldo; residuo = capital - teorico
            else:
                capital = teorico
                if capital < 0:
                    capital = D(0)
                    if sistema == "LevelPayment":
                        a = nivelada(saldo, k + 1, tasas, extra)
                if capital > saldo:
                    capital = saldo
        saldo -= capital
        cuota = {"numero": numero, "vencimiento": fechas[k].isoformat(), "dias": ds[k], "capital": capital,
                 "interes": interes_total, "seguro": solo_seg, "saldo": saldo, "_computable": computable,
                 "_desde": desde[k], "_saldoIni": saldo_ini, "_porcion": porcion, "_solo": solo_interes,
                 "_extra": extra[k], "_tramos": None, "_adm": adm_cuota}
        if tiene_administracion(seguros):
            cuota["administracion"] = adm_cuota
        cuotas.append(cuota)
    assert sum(x["capital"] for x in cuotas) == monto, "el capital no suma el monto"
    return {"rd": rd, "nominal": nominal_pactada(ea, mv, periodicidad), "cuotaNivelada": cuota_nivelada,
            "cuotas": cuotas, "interesDeGracia": interes_gracia, "residuo": residuo, "desembolso": desembolso, "monto": monto,
            "ea": ea, "periodosGracia": [(desde[k], fechas[k]) for k in range(g)] if total_g else []}


# --------------------------------------------------------------------------------------------------------------- usura
def efectiva(desembolso, flujos):
    """TIR diaria por biseccion a 40 digitos sobre los dias reales; (1+r)^365 - 1."""
    porDia = {}
    for f, v in flujos:
        t = (f - desembolso).days
        porDia[t] = porDia.get(t, D(0)) + v
    def vp(r):
        v = 1 / (1 + r)
        return sum(F * v ** t for t, F in porDia.items())
    bajo, alto = D("-0.01"), D("0.01")
    assert vp(bajo) * vp(alto) < 0
    for _ in range(140):
        medio = (bajo + alto) / 2
        if vp(medio) * vp(bajo) > 0:
            bajo = medio
        else:
            alto = medio
    r = (bajo + alto) / 2
    return (1 + r) ** 365 - 1


def usura_vigente(caso):
    cond, series = caso["condiciones"], caso.get("series") or {}
    clave = cond.get("modalidad")
    if clave is None:
        return None
    fd = fecha(cond["fechaDesembolso"])
    vigentes = [u for u in series.get("usura") or [] if u["clave"] == clave and fecha(u["desde"]) <= fd]
    return max(vigentes, key=lambda u: u["desde"])["ea"] if vigentes else None


def totales_de(cuotas):
    """Los totales del plan; 2026-11-16 (feature 024, fase 2): con la administracion si las cuotas la llevan."""
    totales = {"capital": sum(x["capital"] for x in cuotas), "interes": sum(x["interes"] for x in cuotas),
               "seguro": sum(x["seguro"] for x in cuotas)}
    if any("administracion" in x for x in cuotas):
        totales["administracion"] = sum(x.get("administracion", D(0)) for x in cuotas)
    return totales


def esperado_de(caso):
    cond = caso["condiciones"]
    plan = generar_plan(cond)
    d0 = plan["desembolso"]
    sin = [(d0, -plan["monto"])] + [(fecha(x["vencimiento"]), x["capital"] + x["interes"]) for x in plan["cuotas"]]
    con = [(d0, -plan["monto"])] + [(fecha(x["vencimiento"]), x["capital"] + x["interes"] + x["_computable"]) for x in plan["cuotas"]]
    e_sin = efectiva(d0, sin)
    e_con = e_sin if all(x["_computable"] == 0 for x in plan["cuotas"]) else efectiva(d0, con)
    u = usura_vigente(caso)
    usura = {"veredicto": "SinUsura"} if u is None else {
        "veredicto": "Excede" if e_con > u else "Pasa", "usura": u, "holgura": q(u - e_con, 6)}
    cuotas = [{k: v for k, v in x.items() if not k.startswith("_")} for x in plan["cuotas"]]
    return {
        "tasas": {"diaria": plan["rd"], "nominalDelPeriodo": plan["nominal"], "cuotaNivelada": plan["cuotaNivelada"],
                  "efectiva": q(e_con, 6), "efectivaSinCargos": q(e_sin, 6)},
        "plan": {"interesDeGracia": plan["interesDeGracia"], "residuo": plan["residuo"],
                 "totales": totales_de(cuotas),
                 "cuotas": cuotas},
        "usura": usura,
    }


def esperado_completo(caso):
    salida = esperado_de(caso)
    variantes = caso.get("variantes") or []
    if variantes:
        salida["variantes"] = {}
        for v in variantes:
            sub = dict(caso); sub["condiciones"] = v["condiciones"]; sub.pop("variantes", None)
            salida["variantes"][v["nombre"]] = esperado_de(sub)
    return salida


# ===================================================================================================== K3 (fase 16, T0858)
# El plan de una NOVEDAD (FR-090, FR-091, T29), escrito desde la spec y no desde el C#:
# - Se regenera DESDE LA FECHA DE LA DECISION como un plan nuevo: capital = saldo de capital del credito a esa fecha (lo vencido y lo
#   por vencer) + lo que recoge una refinanciacion + el interes que se capitaliza. Los COSTOS de la novedad nunca entran (FR-091).
# - Parte de las condiciones del credito (sistema, convencion, equivalencia, redondeo, seguros) y cambia lo que pide la novedad:
#   cuotas (prorroga), dia de pago o primer vencimiento (cambio de fecha), tasa (cambio de tasa), periodicidad. Las extraordinarias
#   pactadas no pasan. [decision] Si el dia de pago no cambia, es el del credito (el pactado, el del primer vencimiento o el del
#   desembolso): las fechas no se corren.
# - Prorroga, cambio de fecha o de tasa conservan el numero del credito: las cuotas nuevas siguen la numeracion desde la siguiente a
#   la ultima que vencio hasta la fecha de la decision (con el plan original). Novacion y refinanciacion: credito enlazado, desde 1.
# - La usura es la vigente de la modalidad a la fecha de la decision; la efectiva, la TIR de los flujos del plan nuevo desde esa fecha.

def novedad_condiciones(c, n):
    base_dia = int(c["diaDePago"]) if c.get("diaDePago") else (fecha(c["primerVencimiento"]) if c.get("primerVencimiento") else fecha(c["fechaDesembolso"])).day
    capital = D(n["saldoDeCapital"]) + sum((D(x) for x in n.get("saldosRecogidos") or []), D(0)) + D(n.get("interesCapitalizado") or 0)
    nc = dict(c)
    nc["monto"] = capital
    nc["fechaDesembolso"] = n["fecha"]
    nc["plazoCuotas"] = n["cuotas"]
    nc["periodicidad"] = n.get("periodicidad") or c["periodicidad"]
    nc["tasaEA"] = n["tasaEA"] if n.get("tasaEA") is not None else c["tasaEA"]
    if n.get("tasaEA") is not None:
        nc.pop("tasaMV", None)              # 2026-11-24 (feature 025): una tasa nueva en E.A. deja la base en E.A.
    nc["diaDePago"] = int(n["diaDePago"]) if n.get("diaDePago") else base_dia
    nc["primerVencimiento"] = n.get("primerVencimiento")
    nc["extraordinarias"] = []
    nc["gracia"] = None
    return nc


def primera_cuota(c, n):
    if n.get("creditoEnlazado"):
        return 1
    original = generar_plan(c)
    f = fecha(n["fecha"])
    return sum(1 for x in original["cuotas"] if fecha(x["vencimiento"]) <= f) + 1


def usura_a(caso, clave, f):
    vigentes = [u for u in (caso.get("series") or {}).get("usura") or [] if u["clave"] == clave and fecha(u["desde"]) <= f]
    return max(vigentes, key=lambda u: u["desde"])["ea"] if vigentes else None


def esperado_de_novedad(caso, n):
    c = caso["condiciones"]
    nc = novedad_condiciones(c, n)
    plan = generar_plan(nc)
    p1 = primera_cuota(c, n)
    d0 = plan["desembolso"]
    sin = [(d0, -plan["monto"])] + [(fecha(x["vencimiento"]), x["capital"] + x["interes"]) for x in plan["cuotas"]]
    e_sin = efectiva(d0, sin)
    u = usura_a(caso, c.get("modalidad"), d0)
    usura = {"veredicto": "SinUsura"} if u is None else {
        "veredicto": "Excede" if e_sin > u else "Pasa", "usura": u, "holgura": q(u - e_sin, 6)}
    cuotas = []
    for x in plan["cuotas"]:
        y = {k: v for k, v in x.items() if not k.startswith("_")}
        y["numero"] = int(x["numero"]) + p1 - 1
        cuotas.append(y)
    return {
        "primeraCuota": p1,
        "capital": plan["monto"],
        "tasas": {"diaria": plan["rd"], "nominalDelPeriodo": plan["nominal"], "cuotaNivelada": plan["cuotaNivelada"],
                  "efectiva": q(e_sin, 6), "efectivaSinCargos": q(e_sin, 6)},
        "plan": {"interesDeGracia": plan["interesDeGracia"], "residuo": plan["residuo"],
                 "totales": totales_de(cuotas),
                 "cuotas": cuotas},
        "usura": usura,
    }


def novedades_calculadas(caso):
    salida = []
    for n in caso.get("novedades") or []:
        m = dict(n)
        m["esperado"] = esperado_de_novedad(caso, n)
        salida.append(m)
    return salida


# ===================================================================================================== US4 (fase 8, T0464)
# Causacion por diferencia de acumulados, mora, suspension, regeneracion del plan y reconstruccion a (D, R).
# Se calcula DIA POR DIA (el C# avanza por segmentos entre eventos): si al motor le falta un punto de cambio, los casos lo
# delatan. Reglas (research R32-R38, DT T24-T30; las decisiones que no estaban escritas van marcadas con [decision]):
# - Un evento con fecha valor v rige para los intervalos (d-1, d] con d > v. Lo causado el dia d por (concepto, cuota) es
#   U(A(d)) - U(A(d-1)); A es la suma exacta de sus ventanas, U el redondeo sellado (a la unidad por concepto; si no, al centavo).
# - Ventana compuesta B*((1+r_d)^n - 1) o lineal B*r_d*n, n los dias de la convencion desde su inicio. Se reinicia SOLO si
#   cambia la base, la tasa diaria o el periodo; [decision] el periodo cambia en cada vencimiento del plan (y de los periodos de
#   la gracia total) y, despues del ultimo, cada mes en el mismo dia: ni la corriente ni la mora capitalizan entre periodos.
# - Exigible = vencimiento o el siguiente habil (solo el domingo y los festivos nacionales no son habiles, D2). El capital de la
#   cuota vence el dia siguiente a su exigible; hasta ese dia causa corriente, que va a la cuota del periodo del dia (la
#   siguiente) [decision: despues del ultimo vencimiento, a la ultima cuota].
# - Corriente: base = capital pendiente de las cuotas no vencidas; mora: por cuota vencida, sobre su capital (y su corriente si la
#   regla sellada lo permite).
# - Tasas del dia: TasaDelDia con la usura del dia de la modalidad (bitemporal a R). Sin usura certificada para el dia se proyecta
#   con la ultima conocida y se informa ValidaHasta (D11). 2026-11-16 (feature 024): si ese dia rige la tasa general de mora de la empresa
#   (serie "moraGeneral"), la mora se causa con ella en vez de la del credito, topada igual (ver mora_general_del_dia).
# - Altura: dia - exigible de la cuota vencida mas antigua cuyo saldo vencido supere la tolerancia; [decision] el saldo vencido de
#   la cuota es su capital pendiente mas su corriente (con suspenso) y su seguro al dia anterior: la mora no cuenta.
# - Suspension el dia d: castigo (desde el dia siguiente a su fecha, K3), insolvencia, mas de una reestructuracion o altura >= umbral vigente [decision: el umbral es la
#   primera altura que suspende]; lo nuevo va a *InSuspense, lo causado antes no se reclasifica.
# - Seguros: al vencer la cuota (AtDueDate) o a prorrata de los dias del periodo (Daily), D9.
# - K2 (fase 15, T0804; T30, FR-114): la serie "pendientesDeGiro" trae lo que la pagaduria desconto de una cuota y no ha girado
#   (cuota, monto, vencimiento de la ventana de giro, umbral sellado, registradoEn). Conocido a R, mientras la mora de la pagaduria
#   (dias desde el vencimiento de la ventana, nunca negativos) no supere el umbral (sin umbral: nunca), lo descontado se resta del
#   saldo vencido de la cuota para la altura, y cubre la cuota en el orden seguro -> corriente -> capital: lo cubierto no causa mora
#   [decision: FR-114 dice que al deudor no se le imputa]. Superado el umbral, la cuota cuenta entera desde su exigible y causa mora
#   desde ese dia en adelante (lo de antes no se recalcula).

CONCEPTOS = ["Principal", "CurrentInterest", "DefaultInterest", "CurrentInterestInSuspense", "DefaultInterestInSuspense",
             "Insurance", "CollectionFee", "OtherCharge", "Advance", "CreditBalance", "Administration",
             # 2026-11-16 (feature 024, fase 4): lo reconocido al inicio del ciclo y todavia no causado, por rubro y modo (no es saldo del deudor).
             "RecognizedInterest", "RecognizedInsurance", "RecognizedAdministration",
             "RecognizedInterestDirect", "RecognizedInsuranceDirect", "RecognizedAdministrationDirect"]
# 2026-11-16 (feature 024, fase 4): el concepto reconocido de cada rubro por modo de reconocimiento.
RECONOCIDO = {("CurrentInterest", "IngresoDiferido"): "RecognizedInterest", ("Insurance", "IngresoDiferido"): "RecognizedInsurance",
              ("Administration", "IngresoDiferido"): "RecognizedAdministration", ("CurrentInterest", "IngresoDirecto"): "RecognizedInterestDirect",
              ("Insurance", "IngresoDirecto"): "RecognizedInsuranceDirect", ("Administration", "IngresoDirecto"): "RecognizedAdministrationDirect"}
RUBROS_RECONOCIBLES = ["CurrentInterest", "Insurance", "Administration"]
ORDEN = {c: i + 1 for i, c in enumerate(CONCEPTOS)}
# 2026-11-16 (feature 024, fase 2): la administracion es cobrable, al final de la lista (asi no mueve el orden de los que la linea no nombra).
COBRABLES = ["Principal", "CurrentInterest", "DefaultInterest", "CurrentInterestInSuspense", "DefaultInterestInSuspense",
             "Insurance", "CollectionFee", "OtherCharge", "Administration"]
UN_DIA = dt.timedelta(days=1)


def instante(s):
    return dt.datetime.fromisoformat(s.replace("Z", "+00:00"))


def exigible(v, festivos):
    d = v
    while d.weekday() == 6 or d in festivos:
        d += UN_DIA
    return d


def tope_pactable(u, conv):
    anio = 360 if conv == "Thirty360" else 365
    if anio == base(conv):
        return u
    return q(raiz(1 + u, 365) ** base(conv) - 1, 18)


_tasas = {}


def tasa_del_dia(cond, ea, usura, general=None, mv=None):
    """TasaDelDia.Calcular: (diaria corriente, diaria mora, tope corriente, tope mora, topada corriente, topada mora).
    2026-11-16 (feature 024): con la tasa general de mora del dia (general), la mora es ella y no la del credito; se topa igual.
    2026-11-24 (feature 025, HU2): con la corriente en MV (mv), su E.A. es la derivada y el tope es la mensual de la usura."""
    clave = (ea, usura, cond["conv"], cond["eq"], cond["per"], cond["claseMora"], cond["moraFija"], cond["factor"], cond["sobretasa"], general, mv)
    if clave in _tasas:
        return _tasas[clave]
    conv, eq, per = cond["conv"], cond["eq"], cond["per"]
    if mv is not None:
        ea = efectiva_de_mensual(mv)
    if general is not None:
        mora_ea = general
    else:
        mora_ea = cond["moraFija"] if cond["claseMora"] == "Fixed" else q(cond["factor"] * ea, 18)
    dc = tasa_diaria_pactada(ea, mv, conv, eq, per)
    dm = tasa_diaria(mora_ea, conv, eq, per)
    if usura is None:
        r = (dc, dm, None, None, False, False, ea, mora_ea)
    else:
        uc = max(usura - cond["sobretasa"], D(0))
        tc = tasa_diaria(tope_pactable(uc, conv), conv, eq, "Monthly" if mv is not None else per)
        tm = tasa_diaria(tope_pactable(usura, conv), conv, eq, per)
        topc = ea > tope_pactable(uc, conv) if mv is not None else dc > tc
        r = (tc if topc else dc, min(dm, tm), tc, tm, topc, dm > tm, ea, mora_ea)
    _tasas[clave] = r
    return r


def tasa_variable(indice, periodos_nominales, margen, piso, techo):
    x = indice + margen
    ea = q((1 + x / periodos_nominales) ** periodos_nominales - 1, 18) if periodos_nominales else q(x, 18)
    if piso is not None and ea < piso:
        ea = piso
    if techo is not None and ea > techo:
        ea = techo
    return ea


def leer_condiciones(c):
    r = c.get("tasaVariable") or {}
    return {
        "conv": c["convencion"], "eq": c.get("equivalencia", "Compound"), "per": c.get("periodicidad", "Monthly"),
        "r": Redondeo(c["redondeo"]), "ea": D(c["tasaEA"]), "mv": mv_de(c),
        "claseMora": c.get("claseDeMora", "RelativeToCurrent"),
        "factor": D(c["factorDeMora"]) if c.get("factorDeMora") is not None else None,
        "moraFija": D(c["moraFijaEA"]) if c.get("moraFijaEA") is not None else None,
        "sobretasa": D(c.get("sobretasaDeCargos", 0)), "moraSobreIntereses": bool(c.get("moraSobreIntereses", False)),
        "seguros": c.get("causacionDeSeguros", "AtDueDate"), "reestructuraciones": int(c.get("reestructuraciones", 0)),
        "administracion": c.get("causacionDeAdministracion", "AtDueDate"),
        "insolvencia": fecha(c["insolvenciaDesde"]) if c.get("insolvenciaDesde") else None,
        "castigo": fecha(c["castigoDesde"]) if c.get("castigoDesde") else None,
        "usuraRequerida": bool(c.get("usuraRequerida", True)), "modalidad": c.get("modalidad"),
        "margen": D(r["margen"]) if r.get("margen") is not None else D(0),
        "piso": D(r["piso"]) if r.get("piso") is not None else None,
        "techo": D(r["techo"]) if r.get("techo") is not None else None,
        "periodosNominales": int(r["periodosNominales"]) if r.get("periodosNominales") else None,
    }


# ------------------------------------------------------------------------------------------- usura bitemporal (T38, R48)
def usuras(series, clave):
    filas = []
    for i, u in enumerate(series.get("usura") or []):
        if u["clave"] != clave:
            continue
        filas.append({"desde": fecha(u["desde"]), "hasta": fecha(u["hasta"]) if u.get("hasta") else None, "ea": D(u["ea"]),
                      "conocida": instante(u["registradoEn"]), "seq": i + 1, "id": u.get("publicId"), "corrige": u.get("corrige")})
    return filas


def usura_del_dia(filas, d, R):
    """(valor, certificada): VigenciaBitemporal.Vigente; si no cubre d, la ultima conocida que empezo antes (proyeccion)."""
    conocidas = [u for u in filas if u["conocida"] <= R]
    reemplazadas = {u["corrige"] for u in conocidas if u.get("corrige")}
    vigentes = [u for u in conocidas if u["id"] not in reemplazadas]

    def fin(u):
        if u["hasta"] is not None:
            return u["hasta"]
        sig = [w["desde"] for w in vigentes if w["desde"] > u["desde"]]
        return min(sig) - UN_DIA if sig else dt.date.max
    cubren = [u for u in vigentes if u["desde"] <= d <= fin(u)]
    if cubren:
        return max(cubren, key=lambda u: (u["desde"], u["seq"]))["ea"], True
    antes = [u for u in vigentes if u["desde"] <= d]
    if antes:
        return max(antes, key=lambda u: (u["desde"], u["seq"]))["ea"], False
    return None, False


# ------------------------------------------------------------------------- tasa general de mora (feature 024, fase 1)
# 2026-11-16 (pedido del dueno): la empresa digita una TASA GENERAL DE MORA con vigencia (Mora.TasaGeneral; serie "moraGeneral" del caso:
# desde, ea -null = vacia- y registradoEn). El dia d rige, entre las conocidas en R (registradoEn <= R), la de "desde" mas reciente que no
# pase de d (a igual "desde", la que va despues en el archivo); hasta la vispera de la siguiente. Con ea, la mora de ESE dia se causa con ella
# en vez de la del credito (fija o factor x corriente) y se topa igual en la usura del dia; vacia o sin vigencia, la del credito. Aplica a todos
# los creditos, tambien a los ya desembolsados; nunca hacia atras (el escritor no admite vigencias con fecha pasada).
def moras_generales(series):
    return [{"desde": fecha(x["desde"]), "ea": D(x["ea"]) if x.get("ea") is not None else None,
             "conocida": instante(x["registradoEn"]), "orden": i + 1} for i, x in enumerate(series.get("moraGeneral") or [])]


def mora_general_del_dia(filas, d, R):
    candidatas = [f for f in filas if f["conocida"] <= R and f["desde"] <= d]
    return max(candidatas, key=lambda f: (f["desde"], f["orden"]))["ea"] if candidatas else None


def serie_vigente(filas, d):
    validas = [f for f in filas if f[0] <= d]
    return max(validas, key=lambda f: f[0])[1] if validas else None


# -------------------------------------------------------------------------------------------- regeneracion (T0486, D12)
def tramos_de(x, rd):
    return x["_tramos"] if x["_tramos"] is not None else [(x["_desde"], fecha(x["vencimiento"]), x["_saldoIni"], rd)]


def exacto_de_tramos(tramos, conv, eq, hasta=None):
    total = D(0)
    for a, b, B, rd in tramos:
        fin_ = b if hasta is None else min(b, hasta)
        if fin_ > a:
            total += B * tasa_del_periodo(rd, dias(conv, a, fin_), eq)
    return total


def regenerar(plan, c, v, nueva_ea=None, cambio=D(0), eleccion=None, ajustes=None):
    """Regenera desde la primera cuota que vence despues de v conservando numero y fechas; la primera lleva sus tramos.
    US5: los ajustes de capital (pagos) de las cuotas que vencen despues de v se consolidan en el cambio; con ReduceTerm se conserva la
    cuota nivelada (o el capital de cada cuota) y salen cuotas del final."""
    if ajustes:
        cambio = cambio + sum((ajustes.get(x["numero"], D(0)) for x in plan["cuotas"] if fecha(x["vencimiento"]) > v), D(0))
    conv, eq, per, sistema = c["convencion"], c.get("equivalencia", "Compound"), c.get("periodicidad", "Monthly"), c["sistema"]
    r = Redondeo(c["redondeo"])
    seguros = c.get("seguros") or []
    rd_old = plan["rd"]
    ea_new = plan["ea"] if nueva_ea is None else nueva_ea
    rd_new = rd_old if nueva_ea is None else tasa_diaria(ea_new, conv, eq, per)
    cuotas = [dict(x) for x in plan["cuotas"]]
    k = next((i for i, x in enumerate(cuotas) if fecha(x["vencimiento"]) > v), None)
    nuevo = dict(plan); nuevo["rd"] = rd_new; nuevo["ea"] = ea_new
    if k is None:
        assert cambio == 0, "no quedan cuotas por regenerar"
        return nuevo
    if plan["periodosGracia"] and v < cuotas[0]["_desde"]:
        raise ValueError("regenerar dentro de la gracia total no esta soportado")
    x = cuotas[k]
    tr = [(a, min(b, v), B, rd) for (a, b, B, rd) in tramos_de(x, rd_old) if a < v]
    i0 = exacto_de_tramos(tr, conv, eq)
    c_after = x["_saldoIni"] + cambio
    venc_k = fecha(x["vencimiento"])
    tr.append((v, venc_k, c_after, rd_new))
    i1 = i0 + c_after * tasa_del_periodo(rd_new, dias(conv, v, venc_k), eq)
    n = len(cuotas)
    tasas = [tasa_del_periodo(rd_new, cuotas[j]["dias"], eq) for j in range(n)]
    monto_total = sum(cuotas[j]["capital"] for j in range(k)) + c_after
    if eleccion == "ReduceTerm" and c_after < x["_saldoIni"]:
        return reducir_plazo(plan, c, cuotas, k, tr, i1, c_after, tasas, monto_total, nuevo)

    def nivelada_desde(saldo, j0, i_primero=None):
        # Saldo tras cada cuota = alfa + beta*A; la cuota de solo interes no lo mueve; A anula el ultimo.
        alfa, beta = saldo, D(0)
        for j in range(j0, n):
            if cuotas[j]["_solo"]:
                continue
            if j == j0 and i_primero is not None:
                alfa = alfa + i_primero
            else:
                alfa = alfa * (1 + tasas[j]); beta = beta * (1 + tasas[j])
            alfa -= cuotas[j]["_extra"]; beta -= 1
        return -alfa / beta

    a = nivelada_desde(c_after, k, i1) if sistema == "LevelPayment" else D(0)
    amortizan = sum(1 for j in range(k, n) if not cuotas[j]["_solo"])
    abono = ((c_after - sum(cuotas[j]["_extra"] for j in range(k, n))) / amortizan
             if sistema == "ConstantPrincipal" else D(0))
    saldo = c_after; residuo = D(0)
    for j in range(k, n):
        y = cuotas[j]
        ultimo = j == n - 1
        saldo_ini = saldo
        interes = r.concepto(i1 if j == k else saldo * tasas[j])
        porcion = y["_porcion"]
        interes_total = interes + porcion
        if j == k:
            solo_seg, adm_cuota = y["seguro"], y.get("_adm", D(0))
        else:
            solo_seg, adm_cuota = cargos_de_cuota(seguros, r, saldo_ini, monto_total, per, y["dias"], y["numero"] == 1)
        seg_cuota = solo_seg + adm_cuota
        if y["_solo"]:
            capital = D(0)
        else:
            if sistema == "LevelPayment":
                teorico = (r.unidad_(a + y["_extra"] + porcion + seg_cuota) - interes_total - seg_cuota
                           if r.momento == "PerInstallment" else r.cuota_nivelada(a) + y["_extra"] - interes)
            elif sistema == "ConstantPrincipal":
                teorico = (r.unidad_(abono + y["_extra"] + interes_total + seg_cuota) - interes_total - seg_cuota
                           if r.momento == "PerInstallment" else r.concepto(abono) + y["_extra"])
            else:
                teorico = saldo
            if ultimo:
                capital = saldo; residuo = capital - teorico
            else:
                capital = teorico
                if capital < 0:
                    capital = D(0)
                    if sistema == "LevelPayment":
                        a = nivelada_desde(saldo, j + 1)
                if capital > saldo:
                    capital = saldo
        saldo -= capital
        y.update({"capital": capital, "interes": interes_total, "seguro": solo_seg, "saldo": saldo, "_saldoIni": saldo_ini,
                  "_tramos": tr if j == k else None, "_adm": adm_cuota})
        if "administracion" in y:
            y["administracion"] = adm_cuota
        if j == k:
            y["_saldoIni"] = c_after
    assert sum(y["capital"] for y in cuotas) == monto_total, "Lending.Schedule.PrincipalMismatch"
    nuevo["cuotas"] = cuotas; nuevo["monto"] = monto_total; nuevo["residuo"] = residuo
    nuevo["cuotaNivelada"] = r.cuota_nivelada(a) if sistema == "LevelPayment" else None
    return nuevo


def reducir_plazo(plan, c, cuotas, k, tr, i1, c_after, tasas, monto_total, nuevo):
    """US5: reducir el plazo. Cuota fija: la nivelada sellada hasta acabar el saldo (la que lo termina paga el saldo y absorbe el residuo).
    Sin nivelada: el capital de cada cuota se conserva y el abono sale desde la ultima. Siempre queda la primera regenerada."""
    per = c.get("periodicidad", "Monthly")
    r = Redondeo(c["redondeo"])
    seguros = c.get("seguros") or []
    n = len(cuotas)
    x = cuotas[k]
    nivelada = plan["cuotaNivelada"] if c["sistema"] == "LevelPayment" else None
    caps = [y["capital"] for y in cuotas]
    ultima = n - 1
    if nivelada is None:
        quitar = x["_saldoIni"] - c_after
        j = n - 1
        while j >= k and quitar > 0:
            t = min(caps[j], quitar)
            caps[j] -= t
            quitar -= t
            j -= 1
        while ultima > k and caps[ultima] == 0:
            ultima -= 1
    nuevas = [dict(y) for y in cuotas[:k]]
    saldo, residuo = c_after, D(0)
    for j in range(k, ultima + 1):
        y = dict(cuotas[j])
        saldo_ini = saldo
        interes = r.concepto(i1 if j == k else saldo * tasas[j])
        porcion = y["_porcion"]
        interes_total = interes + porcion
        if j == k:
            solo_seg, adm_cuota = y["seguro"], y.get("_adm", D(0))
        else:
            solo_seg, adm_cuota = cargos_de_cuota(seguros, r, saldo_ini, monto_total, per, y["dias"], y["numero"] == 1)
        seg_cuota = solo_seg + adm_cuota
        termina = False
        if y["_solo"]:
            capital = D(0)
        elif nivelada is not None:
            teorico = (r.unidad_(nivelada + y["_extra"] + porcion + seg_cuota) - interes_total - seg_cuota
                       if r.momento == "PerInstallment" else r.cuota_nivelada(nivelada) + y["_extra"] - interes)
            if j == n - 1 or teorico >= saldo:
                capital = saldo
                residuo = capital - teorico
                termina = True
            else:
                capital = teorico if teorico >= 0 else D(0)
        else:
            capital = min(caps[j], saldo)
            if j == ultima:
                capital = saldo
        saldo -= capital
        y.update({"capital": capital, "interes": interes_total, "seguro": solo_seg, "saldo": saldo,
                  "_saldoIni": c_after if j == k else saldo_ini, "_tramos": tr if j == k else None, "_adm": adm_cuota})
        if "administracion" in y:
            y["administracion"] = adm_cuota
        nuevas.append(y)
        if termina:
            break
    assert sum(y["capital"] for y in nuevas) == monto_total, "Lending.Schedule.PrincipalMismatch"
    nuevo["cuotas"] = nuevas
    nuevo["monto"] = monto_total
    nuevo["residuo"] = residuo
    nuevo["cuotaNivelada"] = plan["cuotaNivelada"]
    return nuevo


def plan_migrado(c, corte, cuotas_cargadas, principal):
    """D5: el capital por cuota se conserva; el interes de cada periodo lo pone el motor desde el corte."""
    conv, eq, per = c["convencion"], c.get("equivalencia", "Compound"), c.get("periodicidad", "Monthly")
    r = Redondeo(c["redondeo"])
    ea = D(c["tasaEA"]); rd = tasa_diaria_pactada(ea, mv_de(c), conv, eq, per)
    filas = sorted(cuotas_cargadas, key=lambda x: int(x["numero"]))
    caps = [D(x["capital"]) for x in filas]
    residuo = principal - sum(caps)
    caps[-1] += residuo
    assert caps[-1] >= 0, "Lending.Schedule.PrincipalMismatch"
    cuotas = []; anterior = None
    for i, x in enumerate(filas):
        venc = fecha(x["vencimiento"])
        desde_ = fecha(x["desde"]) if x.get("desde") else (anterior if anterior is not None else corte)
        saldo_ini = sum(caps[i:])
        ini = max(desde_, corte)
        interes = r.concepto(saldo_ini * tasa_del_periodo(rd, dias(conv, ini, venc), eq)) if venc > corte else D(0)
        cuotas.append({"numero": int(x["numero"]), "vencimiento": venc.isoformat(), "dias": dias(conv, desde_, venc) if venc > desde_ else 0,
                       "capital": caps[i], "interes": interes, "seguro": D(0), "saldo": saldo_ini - caps[i],
                       "_desde": desde_, "_saldoIni": saldo_ini, "_porcion": D(0), "_solo": False, "_extra": D(0),
                       "_tramos": [(ini, venc, saldo_ini, rd)] if venc > corte else []})
        anterior = venc
    return {"rd": rd, "ea": ea, "cuotas": cuotas, "monto": principal, "residuo": residuo, "periodosGracia": [],
            "cuotaNivelada": None, "interesDeGracia": D(0)}


def proyectar_causacion(plan, c, D_):
    """Lo causado hasta D por cuota y concepto suponiendo pagos a tiempo (T0486)."""
    conv, eq = c["convencion"], c.get("equivalencia", "Compound")
    r = Redondeo(c["redondeo"])
    out = []
    for x in plan["cuotas"]:
        v = r.concepto(exacto_de_tramos(tramos_de(x, plan["rd"]), conv, eq, D_))
        if x["numero"] == 1 and plan["periodosGracia"]:
            v += r.concepto(sum(plan["monto"] * tasa_del_periodo(plan["rd"], dias(conv, a, min(b, D_)), eq)
                                for a, b in plan["periodosGracia"] if a < D_))
        if v:
            out.append({"cuota": x["numero"], "concepto": "CurrentInterest", "valor": v})
        if x["seguro"] and D_ >= fecha(x["vencimiento"]):
            out.append({"cuota": x["numero"], "concepto": "Insurance", "valor": x["seguro"]})
        if x.get("_adm") and D_ >= fecha(x["vencimiento"]):
            out.append({"cuota": x["numero"], "concepto": "Administration", "valor": x["_adm"]})
    return out


# ===================================================================================================== US5 (fase 9, T0513)
# Pagos: prelacion, las siete modalidades, anticipo al vencer, abono extraordinario con su eleccion (plan regenerado) o saldo a favor,
# cancelacion con el anticipo del mismo credito, resolucion de saldos. Reglas (FR-071-FR-081, D4-D8, D24; las que no estaban escritas
# van marcadas con [decision]):
# - Exigible a la fecha valor v: las cuotas con vencimiento <= v (incluida la que vence ese dia) con todos sus conceptos [decision].
#   Todo pago cubre primero lo exigible (FR-072).
# - Prelacion OldestInstallmentFirst: por cuota (sin cuota = la mas antigua) y dentro por concepto; ConceptFirst: por concepto y dentro
#   por cuota. Orden de conceptos: gastos (solo si se informaron; si no, al final [decision]) -> seguros -> mora con su suspenso -> corriente
#   con su suspenso -> capital; lo que la lista no nombra va despues.
# - Gracia total repartida: el interes de la gracia que el libro lleva en la cuota 1 se atribuye, desde la ultima cuota, a lo sumo su porcion
#   a cada cuota j >= 2 y el resto a la 1; se cobra (y cuenta para la mora) con la cuota a la que se atribuye [decision].
# - Cuota del ciclo: lo exigible y la primera cuota con vencimiento >= v; si aun no vence, lo que le toca (capital pendiente + interes y
#   seguro del plan - anticipo de esa cuota - anticipo general) queda como anticipo de esa cuota (D8).
# - Anticipo y valor libre: lo exigible y el resto como anticipo general, hasta lo que falta por vencer segun el plan [decision para el
#   valor libre]; lo que sobra es excedente (D5).
# - Abono extraordinario: lo exigible y el resto a capital con la eleccion (plan regenerado desde la primera cuota que vence despues de v);
#   sin eleccion, saldo a favor (D24); si el resto alcanza el capital por vencer, cancelacion (D5).
# - Cancelacion: todo lo cobrable, primero con el anticipo del credito (el general y luego el de cada cuota), nunca el saldo a favor (D7).
# - El dia que vence una cuota, antes de las operaciones de ese dia, el anticipo de esa cuota y luego el general la cubren con la prelacion,
#   parcial si no alcanza (D6); lo que sobra del de la cuota, ya cubierta, pasa al general.
# - Cada pago se redistribuye desde su decision en orden (fecha valor, secuencia): un pago fechado atras o un reverso redistribuyen los
#   posteriores (D4, FR-081). Reducir el plazo: cuota fija con la nivelada sellada hasta acabar el saldo; si no, se conserva el capital de
#   cada cuota y el abono sale desde la ultima.

def prelacion_de(c):
    lista = c.get("conceptosDePrelacion") or ["CollectionFee", "Insurance", "DefaultInterest", "CurrentInterest", "Principal"]
    susp = bool(c.get("suspensoConSuConcepto", True))
    gastos = bool(c.get("gastosInformados", False))
    salida = []
    for x in lista:
        if x == "CollectionFee" and not gastos:
            continue
        if x in ("CurrentInterestInSuspense", "DefaultInterestInSuspense") and susp:
            continue
        salida.append(x)
        # 2026-11-16 (feature 024, fase 2): la administracion que la lista no nombra va con el seguro.
        if x == "Insurance" and "Administration" not in lista:
            salida.append("Administration")
        if susp and x == "DefaultInterest":
            salida.append("DefaultInterestInSuspense")
        if susp and x == "CurrentInterest":
            salida.append("CurrentInterestInSuspense")
    for x in COBRABLES:
        if x not in salida and x not in ("CurrentInterestInSuspense", "DefaultInterestInSuspense", "CollectionFee"):
            salida.append(x)
            if x == "Insurance" and "Administration" not in salida:
                salida.append("Administration")
    for x in ("DefaultInterestInSuspense", "CurrentInterestInSuspense", "CollectionFee"):
        if x not in salida:
            salida.append(x)
    return {"orden": c.get("prelacion", "OldestInstallmentFirst"), "pos": {x: i for i, x in enumerate(salida)},
            "fueraDeOrden": bool(c.get("cuotaEspecificaFueraDeOrden", False))}


# 2026-11-16 (pedido del dueno; Pagos.PrelacionDeRubros), escrito desde la regla y no desde el C#: el orden de los RUBROS de la empresa en que un
# pago cubre lo que debe el credito. Rubros: Seguro (Insurance) y Administracion (2026-11-16, feature 024, fase 2: su propio concepto
# Administration; si la lista nombra solo uno de los dos, el otro va a su lado), Interes (mora y luego corriente, cada una con su suspenso si va con su concepto), GastosDeCobranza (CollectionFee, solo informados; si no,
# al final de todo), OtrosCargos (OtherCharge), CuotasExtras (TODO el capital de las cuotas con prima, "_extra" != 0) y Capital (el de las demas
# cuotas y el sin cuota). Los que la lista no nombra van ANTES del capital (antes del primero de CuotasExtras y Capital), en el orden de RUBROS; el
# capital que falta va al final y las cuotas extras que faltan justo antes del capital. Despues, como siempre, los suspensos que no van con su
# concepto y al final los gastos no informados. El orden de las cuotas (OldestInstallmentFirst / ConceptFirst) sigue siendo el de la linea.
# [decision] el pago sella el orden en su decision ("rubros" de la operacion) y la reconstruccion lo repite; el anticipo que cubre una cuota al
# vencer usa el ultimo orden sellado por un pago ya aplicado (sin ninguno, la prelacion de la linea).
RUBROS = ["Seguro", "Administracion", "Interes", "GastosDeCobranza", "OtrosCargos", "CuotasExtras", "Capital"]


def rubros_completos(rubros):
    lista = [r.strip() for r in rubros]
    if "Capital" not in lista:
        lista.append("Capital")
    if "CuotasExtras" not in lista:
        lista.insert(lista.index("Capital"), "CuotasExtras")
    if "Seguro" in lista and "Administracion" not in lista:
        lista.insert(lista.index("Seguro") + 1, "Administracion")
    elif "Administracion" in lista and "Seguro" not in lista:
        lista.insert(lista.index("Administracion"), "Seguro")
    faltan = [r for r in RUBROS if r not in lista]
    i = min(lista.index("CuotasExtras"), lista.index("Capital"))
    return lista[:i] + faltan + lista[i:]


def prelacion_con_rubros(c, rubros):
    """La prelacion de la linea (orden de cuotas, suspenso, gastos, fuera de orden) con los conceptos ordenados por los rubros (texto con comas);
    "Principal*" es el puesto del capital de una cuota con prima. Sin rubros, la de la linea."""
    base = prelacion_de(c)
    if not rubros:
        return base
    susp = bool(c.get("suspensoConSuConcepto", True))
    gastos = bool(c.get("gastosInformados", False))
    salida = []

    def poner(x):
        if x not in salida:
            salida.append(x)
    for r in rubros_completos(rubros.split(",")):
        if r == "Seguro":
            poner("Insurance")
        elif r == "Administracion":
            poner("Administration")
        elif r == "Interes":
            poner("DefaultInterest")
            if susp:
                poner("DefaultInterestInSuspense")
            poner("CurrentInterest")
            if susp:
                poner("CurrentInterestInSuspense")
        elif r == "GastosDeCobranza":
            if gastos:
                poner("CollectionFee")
        elif r == "OtrosCargos":
            poner("OtherCharge")
        elif r == "CuotasExtras":
            poner("Principal*")
        elif r == "Capital":
            poner("Principal")
    for x in ("DefaultInterestInSuspense", "CurrentInterestInSuspense", "CollectionFee"):
        poner(x)
    return dict(base, pos={x: i for i, x in enumerate(salida)}, rubros=rubros)


def puesto(prel, plan, cuota, concepto):
    """El puesto de un concepto en la prelacion: el capital de una cuota con prima va en el de las cuotas extras si hay rubros."""
    if concepto == "Principal" and "Principal*" in prel["pos"] and cuota is not None \
            and any(x["numero"] == cuota and x["_extra"] != 0 for x in plan["cuotas"]):
        return prel["pos"]["Principal*"]
    return prel["pos"][concepto]


def reparte_gracia(plan):
    return bool(plan["periodosGracia"]) and any(x["_porcion"] != 0 for x in plan["cuotas"][1:])


def atribuir_gracia(plan, saldo):
    """La porcion de la gracia de cada cuota j >= 2 (desde la ultima) y el resto a la cuota 1."""
    salida, queda = {}, saldo
    if reparte_gracia(plan) and queda > 0:
        for x in reversed(plan["cuotas"][1:]):
            porcion = min(x["_porcion"], queda)
            if porcion <= 0:
                continue
            salida[x["numero"]] = porcion
            queda -= porcion
    salida[plan["cuotas"][0]["numero"]] = queda
    return salida


def cubetas_de(snap, prel):
    plan = snap["plan"]
    primera = plan["cuotas"][0]["numero"]
    out = []
    for (cuota, concepto), v in sorted(snap["por"].items(), key=lambda kv: (kv[0][0] if kv[0][0] is not None else 0, ORDEN[kv[0][1]])):
        if v <= 0 or concepto not in COBRABLES:
            continue
        if concepto == "CurrentInterest" and cuota == primera and reparte_gracia(plan):
            for j, parte in atribuir_gracia(plan, v).items():
                if parte > 0:
                    out.append((j, cuota, concepto, parte))
            continue
        out.append((cuota, cuota, concepto, v))
    if prel["orden"] == "ConceptFirst":
        out.sort(key=lambda c: (puesto(prel, plan, c[0], c[2]), c[0] or 0))
    else:
        out.sort(key=lambda c: (c[0] or 0, puesto(prel, plan, c[0], c[2])))
    return out


def repartir(cubetas, fondos):
    lineas, resto = [], fondos
    for _, libro, concepto, monto in cubetas:
        if resto <= 0:
            break
        t = min(resto, monto)
        if t <= 0:
            continue
        resto -= t
        for l in lineas:
            if l["cuota"] == libro and l["concepto"] == concepto:
                l["valor"] += t
                break
        else:
            lineas.append({"cuota": libro, "concepto": concepto, "valor": t})
    return lineas, resto


def exigible_(plan, orden, v):
    if orden is None:
        return True
    x = next((y for y in plan["cuotas"] if y["numero"] == orden), None)
    return x is None or fecha(x["vencimiento"]) <= v


def pend_(snap, n):
    return snap["por"].get((n, "Principal"), D(0))


def anticipo_(snap, cuota):
    return snap["por"].get((cuota, "Advance"), D(0))


def anticipo_total(snap):
    return sum((v for (c, k), v in snap["por"].items() if k == "Advance"), D(0))


def valor_de_cuota(snap, x):
    # 2026-11-24 (feature 025, fase 3): mas el interes que la cuota lleva fuera del plan (el cargado y el ajuste del corte).
    return pend_(snap, x["numero"]) + x["interes"] + x["seguro"] + x.get("_adm", D(0)) + snap.get("fuera", {}).get(x["numero"], D(0))


def tramo_que_contiene(f, periodicidad):
    """2026-11-24 (feature 025, fase 5, HU5; FR-016), escrito desde la regla: el tramo del calendario que contiene f. Mensual del 1 al ultimo dia;
    quincenal 1-15 y 16-fin; decadal 1-10, 11-20 y 21-fin; semanal de lunes a domingo; las de mas de un mes y el pago unico, el mes."""
    primero = dt.date(f.year, f.month, 1)
    ultimo = dt.date(f.year, f.month, calendar.monthrange(f.year, f.month)[1])
    if periodicidad == "Weekly":
        lunes = f - dt.timedelta(days=f.weekday())
        return lunes, lunes + dt.timedelta(days=6)
    if periodicidad in ("Biweekly", "TenDay"):
        largo = DIAS_DE[periodicidad]
        cuantos = 30 // largo
        n = min(cuantos, (f.day - 1) // largo + 1)
        desde = primero + dt.timedelta(days=(n - 1) * largo)
        return desde, (ultimo if n == cuantos else desde + dt.timedelta(days=largo - 1))
    return primero, ultimo


def tramo_empezado(venc, periodicidad, f):
    return tramo_que_contiene(venc, periodicidad)[0] <= f


def del_tramo_en_curso(plan, v, periodicidad):
    """Las cuotas que vencen despues de v dentro del tramo que contiene v (la cuota del periodo por periodo calendario)."""
    return {x["numero"] for x in plan["cuotas"] if fecha(x["vencimiento"]) > v and tramo_empezado(fecha(x["vencimiento"]), periodicidad, v)}


def interes_sin_tramo(snap, v, periodicidad):
    """El interes corriente causado de las cuotas cuyo tramo no ha empezado a v: una cancelacion por periodo calendario no lo cobra (D3)."""
    sin = {x["numero"] for x in snap["plan"]["cuotas"] if fecha(x["vencimiento"]) > v and not tramo_empezado(fecha(x["vencimiento"]), periodicidad, v)}
    return [{"cuota": c, "concepto": k, "valor": x} for (c, k), x in sorted(snap["por"].items(), key=lambda kv: (kv[0][0] or 0))
            if k == "CurrentInterest" and x > 0 and c in sin]


def nueva_distribucion(sol):
    return {"modalidad": sol["modalidad"], "valor": sol["valor"], "fechaValor": sol["fechaValor"], "eleccion": sol.get("eleccion"),
            "cuota": sol.get("cuota"), "lineas": [], "anticipoUsado": [], "anticipo": D(0), "cuotaDelAnticipo": None, "saldoAFavor": D(0),
            "excedente": D(0), "planNuevo": None, "cancela": False, "avisos": [], "interesPorCausar": [], "interesNoCobrado": []}


def cerrar(cubetas, d):
    d["cancela"] = d["planNuevo"] is None and len(cubetas) > 0 and sum(c[3] for c in cubetas) == sum((l["valor"] for l in d["lineas"]), D(0))
    return d


def cancelacion(snap, cubetas, d, sol=None):
    """2026-11-16 (Pagos.InteresesAlPagarAntesDelVencimiento = CuotaCompleta): con "cuotaCompleta" en la solicitud, el interes de la cuota en curso
    que falta por causar (snap["interesPorCausar"], la causacion proyectada) va en cubetas AL FINAL, fuera de la prelacion, y solo se cobra si los
    fondos (anticipo usado + pago) cubren todo lo demas; cobrado, va entero en "interesPorCausar" aunque no alcance (lo que falte queda en la cuota)
    [decision del dueno: con el capital en cero la causacion no lo causaria nunca]. Si no cubren, la cancelacion de siempre."""
    # 2026-11-24 (feature 025, fase 5, D3): por periodo calendario, el interes causado de las cuotas cuyo tramo no ha empezado no se cobra (sale de lo que
    # se cubre y se devuelve: interesNoCobrado); si asi no cancela, la cancelacion de siempre. La cuota completa, solo de un tramo ya empezado.
    if sol is not None and sol.get("periodoCalendario"):
        per, v = sol["periodicidad"], sol["fechaValor"]
        no_cobrado = interes_sin_tramo(snap, v, per)
        if no_cobrado:
            sin = {x["cuota"] for x in no_cobrado}
            cobrables = [c for c in cubetas if not (c[2] == "CurrentInterest" and c[1] in sin)]
            cal = cancelacion(snap, cobrables, dict(d), dict(sol, periodoCalendario=False, _soloTramo=True))
            if cal["cancela"]:
                cal["interesNoCobrado"] = no_cobrado
                return cal
        return cancelacion(snap, cubetas, d, dict(sol, periodoCalendario=False, _soloTramo=True))
    corrida = cancelacion_sobre(snap, cubetas, dict(d))
    extra = snap["interesPorCausar"]() if sol is not None and sol.get("cuotaCompleta") and snap.get("interesPorCausar") else []
    if extra and sol.get("_soloTramo"):
        plan = snap["plan"]
        extra = [x for x in extra if any(y["numero"] == x["cuota"] and tramo_empezado(fecha(y["vencimiento"]), sol["periodicidad"], sol["fechaValor"])
                                         for y in plan["cuotas"])]
    if not extra:
        return corrida
    regulares = sum((c[3] for c in cubetas), D(0))
    con = cancelacion_sobre(snap, cubetas + [(x["cuota"], x["cuota"], x["concepto"], x["valor"]) for x in extra], dict(d))
    fondos = sum((u["valor"] for u in con["anticipoUsado"]), D(0)) + con["valor"] - con["excedente"]
    if fondos < regulares:
        return corrida
    con["interesPorCausar"] = [dict(x) for x in extra]
    con["avisos"] = ["Lending.Payment.FullInstallmentInterestCharged"] + list(con["avisos"])
    return con


def cancelacion_sobre(snap, cubetas, d):
    total = sum((c[3] for c in cubetas), D(0))
    usado, por_usar = [], total
    fuentes = sorted(((c, v) for (c, k), v in snap["por"].items() if k == "Advance" and v > 0), key=lambda cv: (cv[0] is not None, cv[0] or 0))
    for c, v in fuentes:
        t = min(v, por_usar)
        if t <= 0:
            break
        usado.append({"cuota": c, "concepto": "Advance", "valor": t})
        por_usar -= t
    pago = min(d["valor"], por_usar)
    lineas, _ = repartir(cubetas, sum((u["valor"] for u in usado), D(0)) + pago)
    d.update({"lineas": lineas, "anticipoUsado": usado, "excedente": d["valor"] - pago, "cancela": pago == por_usar})
    return d


def problema_de_pago(snap, sol, prel):
    if sol["modalidad"] != "SpecificInstallment":
        return None
    n = sol.get("cuota")
    if n is None or all(x["numero"] != n for x in snap["plan"]["cuotas"]):
        return "Lending.Payment.ModalityNotAllowed"
    if prel["fueraDeOrden"]:
        return None
    pend = [c for c in cubetas_de(snap, prel) if (c[0] or 0) < n and exigible_(snap["plan"], c[0], sol["fechaValor"]) and c[3] > 0]
    return "Lending.Payment.SpecificInstallmentBlocked" if pend else None


def distribuir(snap, sol, prel, cond_json):
    plan, v = snap["plan"], sol["fechaValor"]
    assert problema_de_pago(snap, sol, prel) is None
    cubetas = cubetas_de(snap, prel)
    # 2026-11-24 (feature 025, fase 5, D3): por periodo calendario lo causado (no el capital) de la cuota del tramo en curso es exigible para todo abono.
    del_tramo = del_tramo_en_curso(plan, v, sol["periodicidad"]) if sol.get("periodoCalendario") else set()
    exig = [c for c in cubetas if exigible_(plan, c[0], v) or (c[2] != "Principal" and c[0] in del_tramo)]
    d = nueva_distribucion(sol)
    m = sol["modalidad"]
    if m == "FullPayoff":
        return cancelacion(snap, cubetas, d, sol)
    if m == "PastDue":
        d["lineas"], d["excedente"] = repartir(exig, sol["valor"])
        return cerrar(cubetas, d)
    if m == "CycleInstallment":
        d["lineas"], resto = repartir(exig, sol["valor"])
        if sol.get("periodoCalendario"):
            ciclo = next((x for x in plan["cuotas"] if x["numero"] in del_tramo), None)
        else:
            ciclo = next((x for x in plan["cuotas"] if fecha(x["vencimiento"]) >= v), None)
        if ciclo is None or fecha(ciclo["vencimiento"]) == v or resto == 0:
            d["excedente"] = resto
            return cerrar(cubetas, d)
        ya = sum((l["valor"] for l in d["lineas"] if l["cuota"] == ciclo["numero"]), D(0))
        falta = max(D(0), valor_de_cuota(snap, ciclo) - anticipo_(snap, ciclo["numero"]) - anticipo_(snap, None) - ya)
        a = min(resto, falta)
        # 2026-11-16 (feature 024, fase 4): la cuota reconocida al inicio del ciclo se paga completa: sin el aviso del anticipo.
        d.update({"anticipo": a, "cuotaDelAnticipo": ciclo["numero"] if a > 0 else None, "excedente": resto - a,
                  "avisos": ["Lending.Payment.AdvanceDoesNotReduceInterest"] if a > 0 and ciclo["numero"] not in snap.get("reconocidas", ()) else []})
        return cerrar(cubetas, d)
    if m in ("Advance", "FreeAmount"):
        d["lineas"], resto = repartir(exig, sol["valor"])
        por_vencer = sum((valor_de_cuota(snap, x) for x in plan["cuotas"] if fecha(x["vencimiento"]) > v), D(0))
        ya = sum((l["valor"] for l in d["lineas"] if l["cuota"] in del_tramo), D(0))
        a = min(resto, max(D(0), por_vencer - anticipo_total(snap) - ya))
        d.update({"anticipo": a, "excedente": resto - a,
                  "avisos": ["Lending.Payment.AdvanceDoesNotReduceInterest"] if (a > 0 or m == "Advance") else []})
        return cerrar(cubetas, d)
    if m == "ExtraordinaryPrincipal":
        lineas, resto = repartir(exig, sol["valor"])
        d["lineas"] = lineas
        if resto == 0:
            return cerrar(cubetas, d)
        por_vencer = sum((pend_(snap, x["numero"]) for x in plan["cuotas"] if fecha(x["vencimiento"]) > v), D(0))
        if resto >= por_vencer:
            d = cancelacion(snap, cubetas, nueva_distribucion(sol), sol)
            d["avisos"] = d["avisos"] + ["Lending.Payment.TreatedAsPayoff"]
            return d
        if sol.get("eleccion") is None:
            d.update({"saldoAFavor": resto, "avisos": ["Lending.Payment.PreferenceAssumed"]})
            return cerrar(cubetas, d)
        ajustes = {x["numero"]: pend_(snap, x["numero"]) - x["capital"] for x in plan["cuotas"] if pend_(snap, x["numero"]) != x["capital"]}
        d["planNuevo"] = regenerar(plan, cond_json, v, None, -resto, sol["eleccion"], ajustes)
        d["lineas"] = lineas + [{"cuota": None, "concepto": "Principal", "valor": resto}]
        return cerrar(cubetas, d)
    if m == "SpecificInstallment":
        n = sol["cuota"]
        x = next(y for y in plan["cuotas"] if y["numero"] == n)
        de_la = [c for c in cubetas if c[0] == n]
        if fecha(x["vencimiento"]) <= v:
            d["lineas"], d["excedente"] = repartir(de_la, sol["valor"])
            return cerrar(cubetas, d)
        falta = max(D(0), valor_de_cuota(snap, x) - anticipo_(snap, n))
        a = min(sol["valor"], falta)
        d.update({"anticipo": a, "cuotaDelAnticipo": n if a > 0 else None, "excedente": sol["valor"] - a,
                  "avisos": ["Lending.Payment.AdvanceDoesNotReduceInterest"] if a > 0 and n not in snap.get("reconocidas", ()) else []})
        return cerrar(cubetas, d)
    raise ValueError(m)


def aplicar_anticipo(snap, n, prel):
    propio, general = anticipo_(snap, n), anticipo_(snap, None)
    if propio <= 0 and general <= 0:
        return None
    de_la = [c for c in cubetas_de(snap, prel) if c[0] == n]
    debe = sum((c[3] for c in de_la), D(0))
    up = min(max(propio, D(0)), debe)
    ug = min(max(general, D(0)), debe - up)
    lineas, _ = repartir(de_la, up + ug)
    cubierta = up + ug == debe
    liberado = max(propio, D(0)) - up if cubierta else D(0)
    if not lineas and liberado == 0:
        return None
    usado = ([{"cuota": n, "concepto": "Advance", "valor": up}] if up > 0 else []) + \
            ([{"cuota": None, "concepto": "Advance", "valor": ug}] if ug > 0 else [])
    return {"fecha": snap["fecha"], "cuota": n, "lineas": lineas, "usado": usado, "liberado": liberado, "cubierta": cubierta}


def tope_legal(monto, t):
    nacional = D(t["vecesNacional"])
    propias = D(t["vecesCooperativa"]) if t.get("vecesCooperativa") is not None else None
    veces = propias if propias is not None and propias > nacional else nacional
    tope = veces * D(t["smmlv"])
    return {"dentroDelTope": monto <= tope, "topeEnPesos": tope, "veces": veces}


def distribucion_json(d):
    salida = {"distribucion": [dict(l) for l in d["lineas"]], "anticipoUsado": [dict(u) for u in d["anticipoUsado"]],
              "anticipo": d["anticipo"], "cuotaDelAnticipo": d["cuotaDelAnticipo"], "saldoAFavor": d["saldoAFavor"],
              "excedente": d["excedente"], "aplicado": d["valor"] - d["excedente"], "cancela": d["cancela"], "avisos": list(d["avisos"])}
    if d.get("interesPorCausar"):
        salida["interesPorCausar"] = [dict(x) for x in d["interesPorCausar"]]
    if d.get("interesNoCobrado"):
        salida["interesNoCobrado"] = [dict(x) for x in d["interesNoCobrado"]]
    if d["planNuevo"] is not None:
        salida["planNuevo"] = plan_de_consulta(d["planNuevo"])
    return salida


# ----------------------------------------------------------------------------------------- reconstruccion a (D, R), T24
class Ventanas:
    def __init__(self, cond):
        self.cond = cond; self.abiertas = {}; self.todas = {}

    def fijar(self, corte, deseadas):
        for k in list(self.abiertas):
            w = self.abiertas[k]
            if deseadas.get(k) != w["param"]:
                w["fin"] = corte; del self.abiertas[k]
        for k, p in deseadas.items():
            if k not in self.abiertas:
                w = {"inicio": corte, "fin": None, "param": p}
                self.abiertas[k] = w; self.todas.setdefault(k, []).append(w)

    def exacto(self, k, d):
        total = D(0)
        for w in self.todas.get(k, []):
            if w["inicio"] >= d:
                continue
            fin_ = d if w["fin"] is None else min(d, w["fin"])
            B, rd, _ = w["param"]
            total += B * tasa_del_periodo(rd, dias(self.cond["conv"], w["inicio"], fin_), self.cond["eq"])
        return total


def reconstruir(caso, cond_json, ops_json, D_, R, libro_desde=None, plan_json=None):
    cond = leer_condiciones(cond_json)
    series = caso.get("series") or {}
    festivos = {fecha(f) for f in series.get("festivos") or []}
    us = usuras(series, cond["modalidad"])
    mg = moras_generales(series)
    umbrales = [(fecha(x["desde"]), int(x["dias"])) for x in series.get("umbralesDeSuspension") or []]
    tolerancias = [(fecha(x["desde"]), D(x["valor"])) for x in series.get("tolerancias") or []]
    giros = {}
    for g in series.get("pendientesDeGiro") or []:
        if instante(g["registradoEn"]) <= R and D(g["monto"]) > 0:
            giros.setdefault(int(g["cuota"]), []).append(
                (D(g["monto"]), fecha(g["vencimientoDelGiro"]), int(g["umbral"]) if g.get("umbral") is not None else None))
    r = cond["r"]

    def cubierto_por_giro(n_, d):
        total = D(0)
        for monto, vence, umbral in giros.get(n_, []):
            if umbral is None or max((d - vence).days, 0) <= umbral:
                total += monto
        return total

    ops = [dict(o, _seq=o.get("secuencia", i + 1)) for i, o in enumerate(ops_json) if instante(o["registradoEn"]) <= R]
    reversadas = {o["reversa"] for o in ops if o.get("reversa")}
    ops = [o for o in ops if not o.get("reversa") and o["id"] not in reversadas and fecha(o["fechaValor"]) <= D_]
    ops.sort(key=lambda o: (fecha(o["fechaValor"]), o["_seq"]))
    if not ops:
        return None
    origen = fecha(ops[0]["fechaValor"])
    # "apertura": lo que no sale de la causacion por (concepto, cuota): saldos iniciales y, desde US5, lo que mueven los pagos (en
    # ("Principal", n) el capital pendiente de la cuota n fuera del plan).
    estado = {"plan": None, "ea": cond["ea"], "mv": cond["mv"], "apertura": {}, "libro": [], "pagos": {}, "anticipos": [], "antHasta": origen,
              "reestructuraciones": cond["reestructuraciones"], "extinguido": None, "rubros": None, "cierres": []}

    def mover(dia, clase, concepto, cuota, monto):
        if monto == 0:
            return
        key = (concepto, cuota)
        estado["apertura"][key] = estado["apertura"].get(key, D(0)) + monto
        estado["libro"].append((dia, clase, concepto, cuota, monto))

    def pend(x):
        return x["capital"] + estado["apertura"].get(("Principal", x["numero"]), D(0))

    def consolidar(p, v):
        for x in p["cuotas"]:
            if fecha(x["vencimiento"]) > v:
                estado["apertura"].pop(("Principal", x["numero"]), None)

    def regenerar_ctx(v, ea, cambio):
        p = estado["plan"]
        ajustes = {x["numero"]: estado["apertura"][("Principal", x["numero"])] for x in p["cuotas"]
                   if estado["apertura"].get(("Principal", x["numero"]), D(0)) != 0}
        nuevo = regenerar(p, cond_json, v, ea, cambio, None, ajustes)
        consolidar(p, v)
        estado["plan"] = nuevo

    def asentar_pago(o, v, d):
        clase = "Recovery" if cond["castigo"] is not None and cond["castigo"] <= v else "PaymentAllocation"
        # 2026-11-16: la cancelacion con la cuota completa causa primero, en su fecha valor, el interes de la cuota en curso que faltaba.
        for x in d.get("interesPorCausar") or []:
            mover(v, "UnaccruedInstallmentInterest", x["concepto"], x["cuota"], x["valor"])
        # 2026-11-24 (feature 025, fase 5, D3): lo causado de un tramo sin empezar se devuelve (RecognitionRelease negativo en la corriente).
        for x in d.get("interesNoCobrado") or []:
            mover(v, "RecognitionRelease", x["concepto"], x["cuota"], -x["valor"])
        for u in d["anticipoUsado"]:
            mover(v, "AdvanceApplied", "Advance", u["cuota"], -u["valor"])
        for l in d["lineas"]:
            if l["concepto"] == "Principal" and l["cuota"] is None:
                estado["libro"].append((v, clase, "Principal", None, -l["valor"]))
            else:
                mover(v, clase, l["concepto"], l["cuota"], -l["valor"])
        if d["anticipo"] > 0:
            mover(v, "AdvanceReceived", "Advance", d["cuotaDelAnticipo"], d["anticipo"])
        if d["saldoAFavor"] > 0:
            mover(v, "CreditBalanceRecognized", "CreditBalance", None, d["saldoAFavor"])
        if d["planNuevo"] is not None:
            consolidar(estado["plan"], v)
            estado["plan"] = d["planNuevo"]
        estado["pagos"][o["id"]] = d

    def solicitud_de(o, v):
        return {"modalidad": o.get("modalidad") or "FreeAmount", "valor": D(o["valor"] if o.get("valor") is not None else o["monto"]),
                "fechaValor": v, "eleccion": o.get("eleccion"), "cuota": int(o["cuota"]) if o.get("cuota") is not None else None,
                "cuotaCompleta": o.get("interesesAntesDelVencimiento") == "CuotaCompleta",
                # 2026-11-24 (feature 025, fase 5): el periodo calendario sellado en la decision del pago.
                "periodoCalendario": o.get("periodoCalendario") is True, "periodicidad": cond_json.get("periodicidad", "Monthly")}

    def reconocidas_en(v):
        """2026-11-16 (feature 024, fase 4): las cuotas con algo reconocido todavia por causar a v (abiertas: sin vencer ni cancelado el credito)."""
        salida = set()
        for rt, rk, rconcepto, rrubro, rmonto in lista_de_reconocimientos(caso, cond_json, R, v):
            x = next((y for y in plan()["cuotas"] if y["numero"] == rk), None)
            if x is None or fecha(x["vencimiento"]) <= v or any(c[0] <= v for c in estado["cierres"]):
                continue
            ks = set(ven.todas) | {("Insurance", y["numero"], False) for y in plan()["cuotas"] if y["seguro"]} \
                | {("Administration", y["numero"], False) for y in plan()["cuotas"] if y.get("_adm")}
            causado = sum((U(kk, v) - U(kk, rt - UN_DIA) for kk in ks if kk[0] == rrubro and kk[1] == rk), D(0))
            cobrado = sum((f[4] for f in estado["libro"] if f[1] == "UnaccruedInstallmentInterest" and f[2] == rrubro and f[3] == rk and rt <= f[0] <= v), D(0))
            if min(rmonto, max(D(0), causado + cobrado)) < rmonto:
                salida.add(rk)
        return salida

    def capital_por_pagar():
        return sum((pend(x) for x in estado["plan"]["cuotas"]), D(0)) if estado["plan"] is not None else D(0)

    def aplicar(o):
        # 2026-11-16 (feature 024, fase 4), escrito desde la regla: la operacion que deja el credito sin capital por pagar (lo cancela) o lo extingue
        # cierra, ese dia, los reconocimientos de la cuota en curso.
        antes, ext = capital_por_pagar(), estado["extinguido"]
        aplicar_op(o)
        if estado["extinguido"] is not None and estado["extinguido"] != ext:
            estado["cierres"].append((estado["extinguido"], o["id"]))
        elif antes > 0 and capital_por_pagar() <= 0:
            estado["cierres"].append((fecha(o["fechaValor"]), o["id"]))

    def aplicar_op(o):
        v = fecha(o["fechaValor"]); t = o["tipo"]
        if t == "Disbursement":
            monto = D(o["monto"])
            if estado["plan"] is None:
                assert monto == D(cond_json["monto"]), "el primer tramo es el monto del plan"
                estado["plan"] = generar_plan(cond_json)
            else:
                regenerar_ctx(v, None, monto)
            estado["libro"].append((v, "Disbursement", "Principal", None, monto))
        elif t == "OpeningBalance":
            saldos = o["saldos"]
            principal = sum(D(s["valor"]) for s in saldos if s["concepto"] == "Principal")
            estado["plan"] = plan_migrado(cond_json, v, o["cuotas"], principal)
            for s in saldos:
                cu = int(s["cuota"]) if s.get("cuota") is not None else None
                estado["libro"].append((v, "OpeningBalance", s["concepto"], cu, D(s["valor"])))
                if s["concepto"] != "Principal":
                    key = (s["concepto"], cu)
                    estado["apertura"][key] = estado["apertura"].get(key, D(0)) + D(s["valor"])
        elif t == "RateReset":
            ix = o["_indice"]
            ea = tasa_variable(ix["valor"], ix["periodosNominales"], cond["margen"], cond["piso"], cond["techo"])
            estado["ea"] = ea
            estado["mv"] = None
            regenerar_ctx(v, ea, D(0))
        elif t in ("Payment", "ProvisionalCollection"):
            snap = instantanea(v)
            snap["reconocidas"] = reconocidas_en(v)
            sol = solicitud_de(o, v)
            if sol["cuotaCompleta"]:
                # 2026-11-16: la proyeccion con las operaciones ya aplicadas (las anteriores en el orden), una sola vez.
                previas = ops[:next(i for i, x in enumerate(ops) if x is o)]
                memo = {}

                def proyeccion(snap=snap, previas=previas, memo=memo):
                    if "v" not in memo:
                        memo["v"] = interes_por_causar(caso, cond_json, previas, snap, R)
                    return memo["v"]
                snap["interesPorCausar"] = proyeccion
            # K6b (T1042): un credito en UVR convierte lo recibido en pesos a UVR con el estado del dia (lo pone vivienda.py).
            if cond_json.get("_pagoEnUvr") is not None:
                sol["valor"] = cond_json["_pagoEnUvr"](snap, sol["valor"], v)
            # 2026-11-16: con el orden de rubros sellado en el pago; sin el, la prelacion de la linea.
            pr = prelacion_con_rubros(cond_json, o.get("rubros"))
            problema = problema_de_pago(snap, sol, pr)
            if problema == "Lending.Payment.SpecificInstallmentBlocked":
                pr = dict(pr, fueraDeOrden=True)
            elif problema == "Lending.Payment.ModalityNotAllowed":
                sol = dict(sol, modalidad="FreeAmount", cuota=None)
            asentar_pago(o, v, distribuir(snap, sol, pr, cond_json))
            if o.get("rubros"):
                estado["rubros"] = o["rubros"]
        elif t == "CreditBalanceResolution":
            concepto, monto = o["concepto"], D(o["valor"])
            fuentes = sorted(((c_, x) for (k_, c_), x in estado["apertura"].items() if k_ == concepto and x > 0),
                             key=lambda cv: (cv[0] is not None, cv[0] or 0))
            disponible = sum((x for _, x in fuentes), D(0))
            if monto > disponible:
                raise ValueError("Lending.CreditBalance.Insufficient")
            if o.get("comoAbonoACapital"):
                # 2026-11-16 [decision del dueno]: el saldo a favor aplicado como abono reparte con el orden de rubros sellado y, si termina
                # cancelando con la cuota completa sellada, cobra la cuota en curso como cualquier cancelacion.
                snap = instantanea(v)
                sol = {"modalidad": "ExtraordinaryPrincipal", "valor": monto, "fechaValor": v, "eleccion": o["eleccion"], "cuota": None,
                       "cuotaCompleta": o.get("interesesAntesDelVencimiento") == "CuotaCompleta"}
                if sol["cuotaCompleta"]:
                    previas = ops[:next(i for i, x in enumerate(ops) if x is o)]
                    memo = {}

                    def proyeccion(snap=snap, previas=previas, memo=memo):
                        if "v" not in memo:
                            memo["v"] = interes_por_causar(caso, cond_json, previas, snap, R)
                        return memo["v"]
                    snap["interesPorCausar"] = proyeccion
                d = distribuir(snap, sol, prelacion_con_rubros(cond_json, o.get("rubros")), cond_json)
                mover(v, "CreditBalanceSettled", "CreditBalance", None, -(d["valor"] - d["excedente"]))
                asentar_pago(o, v, d)
                if o.get("rubros"):
                    estado["rubros"] = o["rubros"]
            else:
                queda = monto
                for c_, x in fuentes:
                    t_ = min(x, queda)
                    if t_ <= 0:
                        break
                    mover(v, "CreditBalanceSettled", concepto, c_, -t_)
                    queda -= t_
        elif t == "CommercialAdjustment":
            ajuste_comercial(o, v)
        elif t == "TermsChange":
            novedad_op(o, v)
        elif t == "Waiver":
            condonacion(o, v)
        else:
            raise ValueError(f"operacion {t} no soportada en K1")

    # US6 (fase 10, T0565): el ajuste de una venta a credito de Comercial (FR-054), con el signo que manda Comercial.
    def ajuste_comercial(o, v):
        clase, monto = o["clase"], D(o["valor"])
        snap = instantanea(v)
        if clase in ("Voiding", "VoidingByDianRejection"):
            for (cuota, concepto), x in sorted(snap["por"].items(), key=lambda kv: (kv[0][0] or 0, ORDEN[kv[0][1]])):
                if concepto in COBRABLES and x != 0:
                    mover(v, "CommercialAdjustment", concepto, cuota, -x)
            aplicado = sum((d_["valor"] - d_["excedente"] for d_ in estado["pagos"].values()), D(0))
            resuelto = -sum((f[4] for f in estado["libro"] if f[1] == "CreditBalanceSettled"), D(0))
            saldo_a_favor = estado["apertura"].get(("CreditBalance", None), D(0))
            for (concepto, cuota), x in sorted(((k, x) for k, x in estado["apertura"].items() if k[0] == "Advance" and x != 0),
                                               key=lambda kv: kv[0][1] or 0):
                mover(v, "CommercialAdjustment", concepto, cuota, -x)
            reconocer = aplicado - resuelto - saldo_a_favor
            if reconocer > 0:
                mover(v, "CreditBalanceRecognized", "CreditBalance", None, reconocer)
            return
        p = estado["plan"]
        if clase == "DebitNote":
            assert monto > 0, "una nota debito sube el valor"
            regenerar_con(v, monto, None)
            estado["libro"].append((v, "CommercialAdjustment", "Principal", None, monto))
            return
        assert clase in ("CreditNote", "Return") and monto < 0, "una nota credito o una devolucion bajan el valor"
        devuelto = -monto
        pendiente = sum((pend(x) for x in p["cuotas"]), D(0))
        bajado = min(devuelto, pendiente)
        if bajado > 0 and pendiente > 0:
            for (cuota, concepto), x in sorted(snap["por"].items(), key=lambda kv: (kv[0][0] or 0, ORDEN[kv[0][1]])):
                if cuota is None or x <= 0 or concepto not in ("CurrentInterest", "CurrentInterestInSuspense"):
                    continue
                reverso = r.concepto(x * bajado / pendiente)
                mover(v, "CommercialAdjustment", concepto, cuota, -min(reverso, x))
        por_vencer = [x for x in p["cuotas"] if fecha(x["vencimiento"]) > v]
        capital_por_vencer = sum((pend(x) for x in por_vencer), D(0))
        a = min(bajado, capital_por_vencer)
        if 0 < a < capital_por_vencer:
            regenerar_con(v, -a, o.get("recalculo") or "ReduceInstallment")
            estado["libro"].append((v, "CommercialAdjustment", "Principal", None, -a))
        elif a > 0:
            for x in por_vencer:
                mover(v, "CommercialAdjustment", "Principal", x["numero"], -pend(x))
        queda = bajado - a
        for x in sorted((y for y in estado["plan"]["cuotas"] if fecha(y["vencimiento"]) <= v), key=lambda y: y["numero"]):
            if queda <= 0:
                break
            t_ = min(queda, pend(x))
            if t_ <= 0:
                continue
            mover(v, "CommercialAdjustment", "Principal", x["numero"], -t_)
            queda -= t_
        exceso = devuelto - bajado
        if exceso > 0:
            mover(v, "CreditBalanceRecognized", "CreditBalance", None, exceso)

    # K3 (fase 16, T0874): la NOVEDAD en la reconstruccion, escrita desde la spec (FR-090, FR-091) y no desde el C#:
    # - lo que se capitaliza sale de su concepto y su cuota (fila Capitalization, -) sin pasar de su saldo a la fecha valor;
    # - prorroga, cambio de fecha o de tasa: el capital capitalizado entra sin cuota (Capitalization, +Principal) y el plan se regenera
    #   desde la fecha valor sobre TODO el capital pendiente mas lo capitalizado, con las condiciones del credito (las del bloque «K3» de
    #   arriba) y la numeracion desde la siguiente a la ultima vencida; las vencidas se conservan y su capital se consolida en el plan nuevo;
    # - novacion o refinanciacion, en el credito anterior: todo lo que queda a la fecha valor pasa al enlazado (TransferToLinkedLoan, -) y
    #   el credito deja de causar desde el dia siguiente;
    # - novacion o refinanciacion, en el enlazado: nace con su plan pactado (monto = capital trasladado + capitalizado, fecha = la de la
    #   novedad) y recibe el capital sin cuota (TransferFromLinkedLoan) y lo capitalizado (Capitalization) como capital.
    # El conteo de reestructuraciones y la tasa nueva rigen desde el dia siguiente.
    def partidas(lista):
        return [(x["concepto"], int(x["cuota"]) if x.get("cuota") is not None else None, D(x["valor"])) for x in lista or []]

    def novedad_op(o, v):
        mec = o["mecanismo"]
        crea = mec in ("Novation", "Refinancing")
        caps = partidas(o.get("capitalizaciones"))
        if o.get("lado") == "Enlazado":
            assert crea and estado["plan"] is None, "el enlazado nace con la novacion o la refinanciacion"
            tras = partidas(o.get("traslados"))
            capital = sum((x for c_, _, x in tras if c_ == "Principal"), D(0))
            cap = sum((x for _, _, x in caps), D(0))
            assert D(cond_json["monto"]) == capital + cap and fecha(cond_json["fechaDesembolso"]) == v, "plan pactado del enlazado"
            estado["plan"] = generar_plan(cond_json)
            if capital:
                estado["libro"].append((v, "TransferFromLinkedLoan", "Principal", None, capital))
            if cap:
                estado["libro"].append((v, "Capitalization", "Principal", None, cap))
            for c_, cu, x in tras:
                if c_ != "Principal":
                    mover(v, "TransferFromLinkedLoan", c_, cu, x)
            if o.get("reestructuracionesDespues") is not None:
                estado["reestructuraciones"] = int(o["reestructuracionesDespues"])
            return
        snap = instantanea(v)
        total = D(0)
        for c_, cu, x in caps:
            assert x <= snap["por"].get((cu, c_), D(0)), "no se capitaliza mas que el saldo"
            mover(v, "Capitalization", c_, cu, -x)
            total += x
        if crea:
            snap = instantanea(v)
            for (cu, c_), x in sorted(snap["por"].items(), key=lambda kv: (kv[0][0] or 0, ORDEN[kv[0][1]])):
                if x:
                    mover(v, "TransferToLinkedLoan", c_, cu, -x)
            estado["extinguido"] = v
            if o.get("reestructuracionesDespues") is not None:
                estado["reestructuraciones"] = int(o["reestructuracionesDespues"])
            return
        if total:
            estado["libro"].append((v, "Capitalization", "Principal", None, total))
        p = estado["plan"]
        saldo = sum((pend(x) for x in p["cuotas"]), D(0))
        vencidas = [x for x in p["cuotas"] if fecha(x["vencimiento"]) <= v]
        primera = vencidas[-1]["numero"] + 1 if vencidas else p["cuotas"][0]["numero"]
        n = {"saldoDeCapital": saldo, "saldosRecogidos": [], "interesCapitalizado": total, "fecha": v.isoformat(),
             "cuotas": int(o["cuotasNuevas"]), "periodicidad": o.get("periodicidad"),
             "tasaEA": o["tasaNueva"] if o.get("tasaNueva") is not None else estado["ea"],
             "diaDePago": o.get("diaDePago"), "primerVencimiento": o.get("primerVencimiento")}
        nuevo = generar_plan(novedad_condiciones(cond_json, n))
        for x in nuevo["cuotas"]:
            x["numero"] = int(x["numero"]) + primera - 1
        completo = dict(nuevo, cuotas=vencidas + nuevo["cuotas"],
                        periodosGracia=[pg for pg in p["periodosGracia"] if pg[1] <= v] + nuevo["periodosGracia"])
        for x in p["cuotas"]:
            objetivo = -x["capital"] if fecha(x["vencimiento"]) <= v else D(0)
            if objetivo:
                estado["apertura"][("Principal", x["numero"])] = objetivo
            else:
                estado["apertura"].pop(("Principal", x["numero"]), None)
        if o.get("tasaNueva") is not None:
            estado["ea"] = D(o["tasaNueva"])
            estado["mv"] = None
        estado["plan"] = completo
        if o.get("reestructuracionesDespues") is not None:
            estado["reestructuraciones"] = int(o["reestructuracionesDespues"])

    # K3 (T0884): la CONDONACION baja cada concepto de su cuota (fila Waiver, -) sin pasar de su saldo a la fecha valor.
    def condonacion(o, v):
        snap = instantanea(v)
        for c_, cu, x in partidas(o.get("condonaciones")):
            assert x <= snap["por"].get((cu, c_), D(0)), "no se condona mas que el saldo"
            mover(v, "Waiver", c_, cu, -x)

    def regenerar_con(v, cambio, eleccion):
        p = estado["plan"]
        ajustes = {x["numero"]: estado["apertura"][("Principal", x["numero"])] for x in p["cuotas"]
                   if estado["apertura"].get(("Principal", x["numero"]), D(0)) != 0}
        nuevo = regenerar(p, cond_json, v, None, cambio, eleccion, ajustes)
        consolidar(p, v)
        estado["plan"] = nuevo

    pendientes = list(ops)
    while pendientes and fecha(pendientes[0]["fechaValor"]) <= origen:
        aplicar(pendientes.pop(0))

    # 2026-11-24 (feature 025, fase 3): los ajustes del corte conocidos en R hasta D, en orden.
    ajustes_corte = sorted(((fecha(a["fecha"]), int(a["cuota"]), D(a["valor"])) for a in caso.get("ajustesDelCorte") or []
                            if instante(a["registradoEn"]) <= R and fecha(a["fecha"]) <= D_ and D(a["valor"]) != 0), key=lambda a: (a[0], a[1]))

    def aplicar_ajustes_corte(hasta):
        while ajustes_corte and ajustes_corte[0][0] <= hasta:
            f_, k_, v_ = ajustes_corte.pop(0)
            mover(f_, "CutoverInterestAdjustment", "CurrentInterest", k_, v_)

    def fuera_del_plan(dia):
        salida = {}
        for f in estado["libro"]:
            if f[3] is None or f[0] > dia or f[1] not in ("OpeningBalance", "CutoverInterestAdjustment") \
                    or f[2] not in ("CurrentInterest", "CurrentInterestInSuspense"):
                continue
            salida[f[3]] = salida.get(f[3], D(0)) + f[4]
        return salida

    ven = Ventanas(cond)
    seguros_por = {}  # numero -> (premio, desde, venc)

    def U(k, dia):
        if k[0] == "Insurance":
            return r.concepto(seguro_exacto(k[1], dia))
        if k[0] == "Administration":
            return r.concepto(administracion_exacta(k[1], dia))
        return r.concepto(ven.exacto(k, dia))
    transiciones = []
    primer_sin_usura = None

    def plan():
        return estado["plan"]

    def cortes_de_periodo():
        p = plan()
        cs = sorted({b for _, b in p["periodosGracia"]} | {fecha(x["vencimiento"]) for x in p["cuotas"]})
        ultimo = cs[-1]
        if ultimo < D_:
            meses = (D_.year - ultimo.year) * 12 + D_.month - ultimo.month + 2
            cs += vencimientos(ultimo, "Monthly", meses, ultimo.day, None)
        return cs

    def saldo(numero, concepto, d):
        total = estado["apertura"].get((concepto, numero), D(0)) if concepto != "Principal" else D(0)
        for k in ven.todas:
            if k[0] == concepto and k[1] == numero:
                total += r.concepto(ven.exacto(k, d))
        if concepto == "Insurance":
            total += r.concepto(seguro_exacto(numero, d))
        if concepto == "Administration":
            total += r.concepto(administracion_exacta(numero, d))
        return total

    def cargo_exacto(numero, d, campo, momento):
        x = next((y for y in plan()["cuotas"] if y["numero"] == numero), None)
        if estado["extinguido"] is not None and d > estado["extinguido"]:
            d = estado["extinguido"]
        prima = x.get(campo, D(0)) if x is not None else D(0)
        if x is None or not prima or d <= origen:
            return D(0)
        venc = fecha(x["vencimiento"])
        if momento == "Daily":
            if d >= venc:
                return prima
            total_dias = dias(cond["conv"], x["_desde"], venc) if venc > x["_desde"] else 0
            if total_dias == 0 or d <= x["_desde"]:
                return D(0)
            return prima * D(dias(cond["conv"], x["_desde"], d)) / D(total_dias)
        return prima if d >= venc else D(0)

    def seguro_exacto(numero, d):
        return cargo_exacto(numero, d, "seguro", cond["seguros"])

    def administracion_exacta(numero, d):
        # 2026-11-16 (feature 024, fase 2): la administracion, con su propio momento.
        return cargo_exacto(numero, d, "_adm", cond["administracion"])

    def evaluar(d):
        p = plan()
        cuotas = p["cuotas"]
        ex = {x["numero"]: exigible(fecha(x["vencimiento"]), festivos) for x in cuotas}
        vencida = {x["numero"]: d > ex[x["numero"]] for x in cuotas}
        tol = serie_vigente(tolerancias, d) or D(0)
        umb = serie_vigente(umbrales, d)
        cands = []
        cub = {}
        for x in cuotas:
            n_ = x["numero"]
            if not vencida[n_]:
                continue
            ic = interes_corriente(n_, d - UN_DIA)
            # 2026-11-16 (feature 024, fase 2): la administracion cuenta en el saldo vencido y se cubre con el seguro, antes de la corriente.
            seg = saldo(n_, "Insurance", d - UN_DIA) + saldo(n_, "Administration", d - UN_DIA)
            sv = pend(x) + ic + seg
            c_ = cubierto_por_giro(n_, d)
            if c_ > 0:
                tras = max(c_ - seg, D(0))
                cub[n_] = (min(ic, tras), min(pend(x), max(tras - ic, D(0))))
            if sv - c_ > tol:
                cands.append(n_)
        altura = (d - ex[min(cands)]).days if cands else 0
        motivo = None
        # K3 (T0908): el castigo suspende desde el dia siguiente a su fecha valor.
        if cond["castigo"] is not None and cond["castigo"] < d:
            motivo = "WriteOff"
        elif cond["insolvencia"] is not None and cond["insolvencia"] <= d:
            motivo = "Insolvency"
        elif estado["reestructuraciones"] > 1:
            motivo = "Restructuring"
        elif umb is not None and altura >= umb:
            motivo = "Delinquency"
        return {"ex": ex, "vencida": vencida, "altura": altura, "cands": cands, "motivo": motivo, "cub": cub}

    def interes_corriente(numero, d):
        """La corriente (con su suspenso) que se cobra a una cuota, con la gracia repartida atribuida a su cuota (US5)."""
        p = plan()
        susp = saldo(numero, "CurrentInterestInSuspense", d)
        if not reparte_gracia(p):
            return saldo(numero, "CurrentInterest", d) + susp
        primera = p["cuotas"][0]["numero"]
        partes = atribuir_gracia(p, saldo(primera, "CurrentInterest", d))
        propio = D(0) if numero == primera else saldo(numero, "CurrentInterest", d)
        return propio + partes.get(numero, D(0)) + susp

    def deseadas(d, ev):
        nonlocal primer_sin_usura
        p = plan()
        if estado["extinguido"] is not None and d > estado["extinguido"]:
            return {}
        u, cert = usura_del_dia(us, d, R)
        if cond["usuraRequerida"] and not cert and primer_sin_usura is None:
            primer_sin_usura = d
        dc, dm, *_ = tasa_del_dia(cond, estado["ea"], u, mora_general_del_dia(mg, d, R), estado["mv"])
        cs = cortes_de_periodo()
        periodo = next(i for i, x in enumerate(cs) if x >= d)
        susp = ev["motivo"] is not None
        cur = "CurrentInterestInSuspense" if susp else "CurrentInterest"
        mor = "DefaultInterestInSuspense" if susp else "DefaultInterest"
        out = {}
        cuotas = p["cuotas"]
        if p["periodosGracia"] and d <= cuotas[0]["_desde"]:
            out[(cur, 1, True)] = (sum(pend(x) for x in cuotas), dc, periodo)
        else:
            k = next((x["numero"] for x in cuotas if fecha(x["vencimiento"]) >= d), cuotas[-1]["numero"])
            b = sum(pend(x) for x in cuotas if not ev["vencida"][x["numero"]])
            if b > 0:
                out[(cur, k, False)] = (b, dc, periodo)
        for x in cuotas:
            n_ = x["numero"]
            if ev["vencida"][n_]:
                ic_cub, k_cub = ev["cub"].get(n_, (D(0), D(0)))
                b = pend(x) - k_cub
                if cond["moraSobreIntereses"]:
                    b += interes_corriente(n_, d - UN_DIA) - ic_cub
                if b > 0:
                    out[(mor, n_, False)] = (b, dm, periodo)
        return out

    def saldos_a(dia):
        p = plan()
        claves_ = set(ven.todas) | {("Insurance", x["numero"], False) for x in p["cuotas"] if x["seguro"]} \
            | {("Administration", x["numero"], False) for x in p["cuotas"] if x.get("_adm")}
        por_ = {}
        for x in p["cuotas"]:
            if pend(x):
                por_[(x["numero"], "Principal")] = pend(x)
        for k in claves_:
            v_ = U(k, dia)
            if v_:
                por_[(k[1], k[0])] = por_.get((k[1], k[0]), D(0)) + v_
        for (concepto, cuota), v_ in estado["apertura"].items():
            if concepto == "Principal" and cuota is not None:
                continue
            por_[(cuota, concepto)] = por_.get((cuota, concepto), D(0)) + v_
        return {k: v_ for k, v_ in por_.items() if v_}

    def instantanea(dia):
        return {"fecha": dia, "plan": plan(), "por": saldos_a(dia), "fuera": fuera_del_plan(dia)}

    def aplicar_anticipos(hasta):
        if hasta <= estado["antHasta"]:
            return
        desde = estado["antHasta"]
        estado["antHasta"] = hasta
        for x in list(plan()["cuotas"]):
            venc = fecha(x["vencimiento"])
            if not (desde < venc <= hasta):
                continue
            if not any(k[0] == "Advance" and v_ > 0 for k, v_ in estado["apertura"].items()):
                break
            # 2026-11-16: con el ultimo orden de rubros sellado por un pago ya aplicado; sin ninguno, la prelacion de la linea.
            a = aplicar_anticipo(instantanea(venc), x["numero"], prelacion_con_rubros(cond_json, estado.get("rubros")))
            if a is None:
                continue
            for u in a["usado"]:
                mover(venc, "AdvanceApplied", "Advance", u["cuota"], -u["valor"])
            for l in a["lineas"]:
                mover(venc, "AdvanceApplied", l["concepto"], l["cuota"], -l["valor"])
            if a["liberado"] > 0:
                mover(venc, "AdvanceApplied", "Advance", a["cuota"], -a["liberado"])
                mover(venc, "AdvanceReceived", "Advance", None, a["liberado"])
            estado["anticipos"].append(a)

    previo = evaluar(origen)["motivo"] is not None
    d = origen + UN_DIA
    while d <= D_:
        aplicar_anticipos(d - UN_DIA)
        while pendientes and fecha(pendientes[0]["fechaValor"]) < d:
            aplicar(pendientes.pop(0))
        aplicar_ajustes_corte(d - UN_DIA)
        ev = evaluar(d)
        ven.fijar(d - UN_DIA, deseadas(d, ev))
        ahora = ev["motivo"] is not None
        if ahora != previo:
            transiciones.append({"fecha": d.isoformat(), "suspendida": ahora})
        previo = ahora
        d += UN_DIA
    aplicar_anticipos(D_)
    while pendientes:
        aplicar(pendientes.pop(0))
    aplicar_ajustes_corte(D_)

    p = plan()
    claves = set(ven.todas) | {("Insurance", x["numero"], False) for x in p["cuotas"] if x["seguro"]} \
        | {("Administration", x["numero"], False) for x in p["cuotas"] if x.get("_adm")}

    libro = list(estado["libro"])
    if libro_desde is not None:
        d = max(libro_desde, origen + UN_DIA)
        while d <= D_:
            for k in sorted(claves, key=lambda k: (ORDEN[k[0]], k[1], k[2])):
                delta = U(k, d) - U(k, d - UN_DIA)
                if delta:
                    libro.append((d, "Accrual", k[0], k[1], delta))
            d += UN_DIA
    # 2026-11-16 (feature 024, fase 4), escrito desde la regla y no desde el C#: cada reconocimiento (cuota k, rubro, dia t, monto R) cubre la causacion
    # de la cuota desde t hasta su cierre z (el vencimiento de la cuota o, si antes, la operacion que cancela o extingue; si fue antes de t, t). C(d) es lo
    # causado del rubro en la cuota de t a d (la suma de las filas de causacion de esos dias) y K(d) lo cobrado completo al cancelar (filas
    # UnaccruedInstallmentInterest); lo consumido es min(R, C(d) + K(d)). Filas en el concepto reconocido: +R el dia t (PeriodRecognition), el consumo
    # de la causacion de cada dia (Accrual, negativo), el del cobro completo (UnaccruedInstallmentInterest) y, el dia z, la devolucion de lo que falto
    # (RecognitionRelease).
    reconocidos = []
    for rt, rk, rconcepto, rrubro, rmonto in lista_de_reconocimientos(caso, cond_json, R, D_):
        claves_k = [kk for kk in claves if kk[0] == rrubro and kk[1] == rk]

        def C(d, rt=rt, claves_k=claves_k):
            return sum((U(kk, d) - U(kk, rt - UN_DIA) for kk in claves_k), D(0))
        cobros = [(f[0], f[4]) for f in estado["libro"] if f[1] == "UnaccruedInstallmentInterest" and f[2] == rrubro and f[3] == rk and f[0] >= rt]

        def consumido(d, hasta, rt=rt, rmonto=rmonto, C=C, cobros=cobros):
            if d < rt:
                return D(0)
            return min(rmonto, max(D(0), C(d) + sum((x for dd, x in cobros if dd <= hasta), D(0))))
        x = next((y for y in p["cuotas"] if y["numero"] == rk), None)
        cierre = rt if x is None or fecha(x["vencimiento"]) < rt else fecha(x["vencimiento"])
        despues = [c for c in estado["cierres"] if c[0] >= rt]
        ev = min(despues, key=lambda c: c[0]) if despues else (max(estado["cierres"], key=lambda c: c[0]) if estado["cierres"] else None)
        if ev is not None and ev[0] < cierre:
            cierre = max(ev[0], rt)
        if D_ < cierre:
            reconocidos.append({"cuota": rk, "concepto": rconcepto, "desde": rt.isoformat(), "reconocido": rmonto,
                                "pendiente": rmonto - consumido(D_, D_)})
        if libro_desde is None or rt >= libro_desde:
            libro.append((rt, "PeriodRecognition", rconcepto, rk, rmonto))
        if libro_desde is not None:
            d = max(libro_desde, rt)
            while d <= D_ and d <= cierre:
                antes, tras_causar, tras_cobrar = consumido(d - UN_DIA, d - UN_DIA), consumido(d, d - UN_DIA), consumido(d, d)
                if tras_causar != antes:
                    libro.append((d, "Accrual", rconcepto, rk, antes - tras_causar))
                if tras_cobrar != tras_causar:
                    libro.append((d, "UnaccruedInstallmentInterest", rconcepto, rk, tras_causar - tras_cobrar))
                if d == cierre and tras_cobrar != rmonto:
                    libro.append((d, "RecognitionRelease", rconcepto, rk, tras_cobrar - rmonto))
                d += UN_DIA

    filas = {}
    for dia_, clase, concepto, cuota, valor in libro:
        if libro_desde is None or dia_ >= libro_desde:
            key = (dia_, clase, concepto, cuota)
            filas[key] = filas.get(key, D(0)) + valor
    filas_libro = [{"dia": k[0].isoformat(), "clase": k[1], "concepto": k[2], "cuota": k[3], "valor": v}
                   for k, v in sorted(filas.items(), key=lambda kv: (kv[0][0], kv[0][1] == "Accrual", ORDEN[kv[0][2]], kv[0][3] or 0)) if v]

    # saldos a D
    ev = evaluar(D_) if D_ > origen else {"vencida": {x["numero"]: False for x in p["cuotas"]}, "altura": 0, "cands": [], "motivo": None,
                                           "ex": {x["numero"]: exigible(fecha(x["vencimiento"]), festivos) for x in p["cuotas"]}}
    por = saldos_a(D_)
    saldos = {}
    for (cuota, concepto), v in por.items():
        saldos[concepto] = saldos.get(concepto, D(0)) + v
    u, _ = usura_del_dia(us, D_, R)
    dc, dm, tc, tm, topc, topm, _, _ = tasa_del_dia(cond, estado["ea"], u, mora_general_del_dia(mg, D_, R), estado["mv"])
    return {
        "saldos": {c_: saldos[c_] for c_ in CONCEPTOS if c_ in saldos},
        "porCuota": [{"cuota": k[0], "concepto": k[1], "valor": v}
                     for k, v in sorted(por.items(), key=lambda kv: (kv[0][0] if kv[0][0] is not None else 0, ORDEN[kv[0][1]]))],
        "capitalVencido": sum((pend(x) for x in p["cuotas"] if ev["vencida"][x["numero"]]), D(0)),
        "diasDeMora": ev["altura"],
        "cuotaMasAntigua": min(ev["cands"]) if ev["cands"] else None,
        "cuotasVencidas": len(ev["cands"]),
        "suspendida": ev["motivo"] is not None,
        "valorParaCancelar": sum((saldos.get(c_, D(0)) for c_ in COBRABLES), D(0)) - saldos.get("Advance", D(0)),
        "tasaDiariaCorriente": dc, "tasaDiariaMora": dm, "usuraTopada": topc or topm,
        "validaHasta": (primer_sin_usura - UN_DIA).isoformat() if primer_sin_usura is not None else None,
        "transiciones": transiciones,
        "_libro": filas_libro, "_plan": p, "_pagos": estado["pagos"], "_anticipos": estado["anticipos"],
        "_vencidas": [x["numero"] for x in p["cuotas"] if ev["vencida"][x["numero"]]],
        "_por": por, "_origen": origen, "_reconocidos": reconocidos,
    }


def reconocimientos_de(caso, cond_json):
    """2026-11-16 (feature 024, fase 4): los reconocimientos con que corren unas condiciones: los de su variante (_reconocimientos) o los del caso."""
    if "_reconocimientos" in cond_json:
        return cond_json["_reconocimientos"] or []
    return caso.get("reconocimientos") or []


def lista_de_reconocimientos(caso, cond_json, R, D_):
    """(t, cuota, concepto reconocido, rubro, monto) de los reconocimientos conocidos en R con t <= D, con los montos de su "esperado", en orden."""
    salida = []
    for rc in reconocimientos_de(caso, cond_json):
        t = fecha(rc["fecha"])
        if instante(rc["registradoEn"]) > R or t > D_:
            continue
        for m in (rc.get("esperado") or {}).get("montos") or []:
            if D(m["valor"]) != 0:
                salida.append((t, int(rc["cuota"]), RECONOCIDO[(m["concepto"], rc.get("modo") or "IngresoDiferido")], m["concepto"], D(m["valor"])))
    return sorted(salida, key=lambda x: (x[0], x[1], ORDEN[x[2]]))


def cuota_por_reconocer(caso, cond_json, ops_json, rc):
    """2026-11-16 (feature 024, fase 4), escrito desde la regla: lo que reconoce el reconocimiento de la cuota k desde el dia t: lo que la causacion de
    esa cuota va a causar de t (incluido) a su vencimiento, por rubro, proyectado con las operaciones con fecha valor hasta t (y los reversos), sin pagos
    despues y con la usura y la mora general congeladas en las vigentes a t. El suspenso no se reconoce. Vacio si la cuota ya vencio."""
    t, R, k = fecha(rc["fecha"]), instante(rc["registradoEn"]), int(rc["cuota"])
    series = dict(caso.get("series") or {})
    series["usura"] = [u for u in series.get("usura") or [] if fecha(u["desde"]) <= t]
    series["moraGeneral"] = [x for x in series.get("moraGeneral") or [] if fecha(x["desde"]) <= t]
    otro = dict(caso, series=series)
    sin = dict(cond_json, _reconocimientos=[])
    ops = [dict(o, _indice=indice_para(caso, o)) if o["tipo"] == "RateReset" else o for o in ops_json
           if fecha(o["fechaValor"]) <= t or o.get("reversa")]
    hoy = reconstruir(otro, sin, ops, t, R)
    x = next((y for y in hoy["_plan"]["cuotas"] if y["numero"] == k), None) if hoy else None
    if x is None or fecha(x["vencimiento"]) < t:
        return []
    venc = fecha(x["vencimiento"])
    e = reconstruir(otro, sin, ops, venc, R, libro_desde=t)
    tot = {}
    for f in e["_libro"]:
        if f["clase"] == "Accrual" and f["cuota"] == k and f["concepto"] in rc["rubros"] and t <= fecha(f["dia"]) <= venc:
            tot[f["concepto"]] = tot.get(f["concepto"], D(0)) + f["valor"]
    return [{"concepto": c, "valor": tot[c]} for c in RUBROS_RECONOCIBLES if c in rc["rubros"] and tot.get(c, D(0)) > 0]


def reconocimientos_calculados(caso):
    """Los reconocimientos del caso y de cada variante con su "esperado" (los montos) recalculado; los deja en el caso para las consultas."""
    for rc in caso.get("reconocimientos") or []:
        rc["esperado"] = {"montos": cuota_por_reconocer(caso, caso["condiciones"], caso.get("operaciones") or [], rc)}
    for var in caso.get("variantes") or []:
        for rc in var.get("reconocimientos") or []:
            rc["esperado"] = {"montos": cuota_por_reconocer(caso, var["condiciones"], var.get("operaciones") or caso.get("operaciones") or [], rc)}


def interes_por_causar(caso, cond_json, previas, snap, R):
    """2026-11-16 (Pagos.InteresesAlPagarAntesDelVencimiento = CuotaCompleta), escrito desde la regla y no desde el C#: lo que la causacion
    todavia no ha causado de la CUOTA EN CURSO a la fecha v del snap -la primera cuota que vence despues de v y cuyo periodo (desde, vencimiento]
    contiene a v-: se reconstruye con las operaciones anteriores (sin el pago ni nada despues) hasta su vencimiento y se suman sus filas Accrual de
    la corriente y su suspenso desde v + 1. La usura se congela en la vigente a v [decision: las que empiezan despues no cuentan, asi simular,
    aplicar y reconstruir mas tarde dan lo mismo]."""
    v, plan = snap["fecha"], snap["plan"]
    x = next((y for y in plan["cuotas"] if fecha(y["vencimiento"]) > v), None)
    if x is None or x["_desde"] >= v or not previas:
        return []
    venc = fecha(x["vencimiento"])
    series = dict(caso.get("series") or {})
    series["usura"] = [u for u in series.get("usura") or [] if fecha(u["desde"]) <= v]
    series["moraGeneral"] = [x for x in series.get("moraGeneral") or [] if fecha(x["desde"]) <= v]
    e = reconstruir(dict(caso, series=series), cond_json, previas, venc, R, libro_desde=v + UN_DIA)
    tot = {}
    for f in e["_libro"]:
        if f["clase"] == "Accrual" and f["cuota"] == x["numero"] and f["concepto"] in ("CurrentInterest", "CurrentInterestInSuspense") \
                and v < fecha(f["dia"]) <= venc:
            tot[f["concepto"]] = tot.get(f["concepto"], D(0)) + f["valor"]
    return [{"cuota": x["numero"], "concepto": c, "valor": tot[c]} for c in ("CurrentInterest", "CurrentInterestInSuspense") if tot.get(c, D(0)) > 0]


def indice_para(caso, op):
    """El ultimo indice publicado a la fecha del reajuste y conocido al registrarlo (FR-067)."""
    v, R = fecha(op["fechaValor"]), instante(op["registradoEn"])
    filas = [x for x in (caso.get("series") or {}).get("indices") or []
             if x["codigo"] == op["indice"] and fecha(x["desde"]) <= v and instante(x["registradoEn"]) <= R]
    x = max(filas, key=lambda x: (x["desde"], x["registradoEn"]))
    return {"valor": D(x["valor"]), "periodosNominales": int(x["periodosNominales"]) if x.get("periodosNominales") else None}


def plan_de_consulta(p):
    return {"cuotas": [dict({"numero": x["numero"], "vencimiento": x["vencimiento"], "dias": x["dias"], "capital": x["capital"],
                             "interes": x["interes"], "seguro": x["seguro"], "saldo": x["saldo"]},
                            **({"administracion": x["administracion"]} if "administracion" in x else {})) for x in p["cuotas"]],
            "totales": {"capital": sum(x["capital"] for x in p["cuotas"]), "interes": sum(x["interes"] for x in p["cuotas"])}}


def esperado_de_consulta(caso, cond_json, ops_json, con):
    modo = con.get("modo", "Reconstruccion")
    D_, R = fecha(con["fecha"]), instante(con["informacionHasta"])
    ops = [dict(o, _indice=indice_para(caso, o)) if o["tipo"] == "RateReset" else o for o in ops_json]
    if modo == "PagosATiempo":
        return {"causado": proyectar_causacion(generar_plan(cond_json), cond_json, D_)}
    if modo == "Causacion":
        marca = fecha(con["marca"])
        filas = []
        d = marca + UN_DIA
        while d <= D_:
            e = reconstruir(caso, cond_json, ops, d, R, libro_desde=d)
            # 2026-11-16 (feature 024, fase 4): y lo reconocido que se devuelve al vencer la cuota, con su clase.
            filas += [dict({"dia": f["dia"], "concepto": f["concepto"], "cuota": f["cuota"], "valor": f["valor"]},
                           **({"clase": f["clase"]} if f["clase"] != "Accrual" else {}))
                      for f in e["_libro"] if f["clase"] in ("Accrual", "RecognitionRelease")]
            d += UN_DIA
        return {"causacion": filas}
    if modo == "Diferencias":
        origen = min(fecha(o["fechaValor"]) for o in ops_json)
        viejo = reconstruir(caso, cond_json, ops, D_, instante(con["comparaConR"]), libro_desde=origen)["_libro"]
        nuevo = reconstruir(caso, cond_json, ops, D_, R, libro_desde=origen)["_libro"]
        tot = {}
        for f, s in [(f, 1) for f in nuevo] + [(f, -1) for f in viejo]:
            k = (f["dia"], f["concepto"], f["cuota"])
            tot[k] = tot.get(k, D(0)) + s * f["valor"]
        return {"diferencias": [{"dia": k[0], "concepto": k[1], "cuota": k[2], "valor": v}
                                for k, v in sorted(tot.items(), key=lambda kv: (kv[0][0], ORDEN[kv[0][1]], kv[0][2] or 0)) if v]}
    e = reconstruir(caso, cond_json, ops, D_, R, libro_desde=fecha(con["libroDesde"]) if con.get("libroDesde") else None)
    salida = {k: v for k, v in e.items() if not k.startswith("_")}
    if con.get("libroDesde"):
        salida["libro"] = e["_libro"]
    # 2026-11-16 (feature 024, fase 4): lo reconocido abierto a la fecha, solo en los casos que reconocen.
    if reconocimientos_de(caso, cond_json):
        salida["reconocidos"] = e["_reconocidos"]
    if con.get("conPlan"):
        salida["plan"] = plan_de_consulta(e["_plan"])
    if con.get("conPagos"):
        salida["pagos"] = [dict(distribucion_json(d), id=i) for i, d in e["_pagos"].items()]
    return salida


def vencimiento_anterior(venc, periodicidad):
    """2026-11-24 (feature 025, fase 3): un periodo antes: los meses enteros con el calendario (el dia se topa al fin de mes), lo demas en dias."""
    d_ = DIAS_DE[periodicidad]
    if d_ == 0:
        return venc
    if d_ % 30 == 0:
        a, m = sumar_meses(venc, -(d_ // 30))
        return en_el_dia(a, m, venc.day)
    return venc - dt.timedelta(days=d_)


def propuesta_del_corte(caso, p):
    """2026-11-24 (feature 025, fase 3): lo que propone el recalculo de la carga desde el corte (ver la cabecera)."""
    cond_json = caso["condiciones"]
    corte, hoy, R = fecha(p["corte"]), fecha(p["hoy"]), instante(p["informacionHasta"])
    ops = caso.get("operaciones") or []
    sin_ajustes = dict(caso, ajustesDelCorte=[])
    mv = None
    if cond_json.get("tasaMV") is None:
        ea = D(cond_json["tasaEA"])
        redonda = nominal_del_periodo(ea, "Monthly").quantize(D("0.0001"), rounding=ROUND_HALF_UP)
        if redonda > 0 and abs(efectiva_de_mensual(redonda) - ea) < D("0.00005"):
            mv = redonda
            cond_json = dict(cond_json, tasaMV=mv, tasaEA=q(efectiva_de_mensual(mv), 6), convencion="Thirty360", equivalencia="Linear")
    c = leer_condiciones(cond_json)
    al_corte = reconstruir(sin_ajustes, cond_json, ops, corte, R)
    plan = al_corte["_plan"]
    x = next((y for y in plan["cuotas"] if fecha(y["vencimiento"]) > corte), None)
    if x is None:
        return None
    numero, venc = x["numero"], fecha(x["vencimiento"])
    anterior = next((y for y in plan["cuotas"] if y["numero"] == numero - 1), None)
    if anterior is not None:
        inicio = fecha(anterior["vencimiento"])
    elif x["_desde"] < corte:
        inicio = x["_desde"]
    elif numero == 1 and p.get("desembolso") and fecha(p["desembolso"]) < venc:
        inicio = fecha(p["desembolso"])
    else:
        inicio = vencimiento_anterior(venc, c["per"])
    capital = sum((v for (k, con), v in al_corte["_por"].items() if con == "Principal" and k is not None
                   and fecha(next(y for y in plan["cuotas"] if y["numero"] == k)["vencimiento"]) > corte), D(0))
    rd = tasa_diaria_pactada(c["ea"], c["mv"], c["conv"], c["eq"], c["per"])
    periodo = c["r"].concepto(capital * tasa_del_periodo(rd, dias(c["conv"], inicio, venc), c["eq"]))
    proyectado = reconstruir(sin_ajustes, cond_json, ops, venc, R, libro_desde=corte + UN_DIA)
    causado = sum((f["valor"] for f in proyectado["_libro"] if f["clase"] == "Accrual" and f["cuota"] == numero
                   and f["concepto"] in ("CurrentInterest", "CurrentInterestInSuspense") and corte < fecha(f["dia"]) <= venc), D(0))
    cargado = sum((D(s["valor"]) for o in ops if o["tipo"] == "OpeningBalance" for s in o.get("saldos") or []
                   if s.get("cuota") is not None and int(s["cuota"]) == numero and s["concepto"] in ("CurrentInterest", "CurrentInterestInSuspense")), D(0))
    falta = c["r"].concepto(periodo - causado - cargado)
    en_hoy = reconstruir(sin_ajustes, cond_json, ops, hoy, R)
    destino = next((y["numero"] for y in en_hoy["_plan"]["cuotas"] if fecha(y["vencimiento"]) > hoy), None)
    return {"mvPropuesta": mv, "cuotaDelCorte": numero, "inicioDelPeriodo": inicio.isoformat(), "vencimiento": venc.isoformat(),
            "diasFaltantes": dias(c["conv"], inicio, corte) if inicio < corte else 0, "capitalAlCorte": capital, "interesDelPeriodo": periodo,
            "yaCausado": causado, "cargado": cargado, "ajuste": falta if falta > 1 else D(0), "cuotaDestino": destino}


def propuestas_calculadas(caso):
    """2026-11-24 (feature 025, fase 3): cada propuesta del corte con su "esperado"."""
    for p in caso.get("propuestasDelCorte") or []:
        p["esperado"] = propuesta_del_corte(caso, p)
    return caso.get("propuestasDelCorte") or []


def periodo_completo(caso, p):
    """2026-11-24 (feature 025, fase 4): lo que suma la opcion del periodo completo al activar (ver la cabecera)."""
    cond_json = caso["condiciones"]
    c = leer_condiciones(cond_json)
    corte = fecha(p["corte"])
    desembolso = fecha(p["desembolso"]) if p.get("desembolso") else None
    ops = caso.get("operaciones") or []
    apertura = next(o for o in ops if o["tipo"] == "OpeningBalance")
    cuotas = sorted(apertura["cuotas"], key=lambda y: int(y["numero"]))
    x = next((y for y in cuotas if fecha(y["vencimiento"]) > corte), None)
    if x is None:
        return None
    numero_, venc = int(x["numero"]), fecha(x["vencimiento"])
    anteriores = [y for y in cuotas if int(y["numero"]) < numero_]
    if anteriores:
        inicio = fecha(anteriores[-1]["vencimiento"])
    elif numero_ == 1 and desembolso is not None and desembolso < venc:
        inicio = desembolso
    else:
        inicio = vencimiento_anterior(venc, c["per"])
    con_inicio = [dict(y, desde=inicio.isoformat()) if y is x and inicio < corte else {k: v for k, v in y.items() if k != "desde"} for y in cuotas]
    principal = sum((D(s_["valor"]) for s_ in apertura.get("saldos") or [] if s_["concepto"] == "Principal"), D(0))
    plan = plan_migrado(cond_json, corte, con_inicio, principal)
    q_ = next(y for y in plan["cuotas"] if y["numero"] == numero_)
    efectivo = q_["_desde"] if q_["_desde"] < corte else corte
    periodo = c["r"].concepto(q_["_saldoIni"] * tasa_del_periodo(plan["rd"], dias(c["conv"], efectivo, venc), c["eq"]))
    causado = q_["interes"]
    cargado = sum((D(s_["valor"]) for s_ in apertura.get("saldos") or [] if s_.get("cuota") is not None and int(s_["cuota"]) == numero_
                   and s_["concepto"] in ("CurrentInterest", "CurrentInterestInSuspense")), D(0))
    falta = c["r"].concepto(periodo - causado - cargado)
    esperado = {"cuotaDelCorte": numero_, "inicioDelPeriodo": efectivo.isoformat(), "vencimiento": venc.isoformat(),
                "diasFaltantes": dias(c["conv"], efectivo, corte) if efectivo < corte else 0, "capitalAlCorte": q_["_saldoIni"],
                "interesDelPeriodo": periodo, "yaCausado": causado, "cargado": cargado, "ajuste": falta if falta > 1 else D(0), "cuotaDestino": numero_}
    # La misma regla que el recalculo del corte (fase 3) con hoy = corte, sobre la reconstruccion: la activacion calcula asi.
    caso_con_inicio = dict(caso, operaciones=[dict(o, cuotas=con_inicio) if o is apertura else o for o in ops])
    prop = propuesta_del_corte(caso_con_inicio, {"corte": p["corte"], "hoy": p["corte"], "informacionHasta": p["registradoEn"],
                                                  "desembolso": p.get("desembolso")})
    assert prop is not None and prop["mvPropuesta"] is None, "el periodo completo no cambia la tasa"
    for k in ("cuotaDelCorte", "inicioDelPeriodo", "vencimiento", "diasFaltantes", "capitalAlCorte", "interesDelPeriodo", "yaCausado", "cargado",
              "ajuste", "cuotaDestino"):
        assert prop[k] == esperado[k], f"periodo completo: {k} {prop[k]} != {esperado[k]}"
    return esperado


def periodos_calculados(caso):
    """2026-11-24 (feature 025, fase 4): cada periodo completo con su "esperado" y, si suma, su ajuste del corte el dia de la activacion."""
    periodos = caso.get("periodosCompletos") or []
    if not periodos:
        return []
    ajustes = []
    for p in periodos:
        p["esperado"] = periodo_completo(caso, p)
        e = p["esperado"]
        if e is not None and e["ajuste"] > 0:
            ajustes.append({"fecha": (fecha(p["corte"]) + UN_DIA).isoformat(), "cuota": e["cuotaDestino"], "valor": e["ajuste"],
                            "registradoEn": p["registradoEn"]})
    caso["ajustesDelCorte"] = ajustes
    return periodos


def esperado_de_operacion(caso, cond_json, ops_json, op):
    """US5: la distribucion de un pago con la informacion hasta su registro (simular = aplicar) y, si trae topeLegal, el tope."""
    ops = [dict(o, _indice=indice_para(caso, o)) if o["tipo"] == "RateReset" else o for o in ops_json]
    v, R = fecha(op["fechaValor"]), instante(op["registradoEn"])
    e = reconstruir(caso, cond_json, ops, v, R)
    salida = distribucion_json(e["_pagos"][op["id"]])
    if op.get("topeLegal"):
        salida["topeLegal"] = tope_legal(D(cond_json["monto"]), op["topeLegal"])
    return salida


def operaciones_calculadas(caso, cond_json, ops_json):
    """Las operaciones con su "esperado" recalculado (solo las que lo traen)."""
    salida = []
    for op in ops_json:
        nueva = dict(op)
        if "esperado" in op:
            nueva["esperado"] = esperado_de_operacion(caso, cond_json, ops_json, op)
        salida.append(nueva)
    return salida


def consultas_calculadas(caso):
    salida = []
    for con in caso.get("consultas") or []:
        nueva = {k: v for k, v in con.items() if k != "esperado"}
        nueva["esperado"] = esperado_de_consulta(caso, caso["condiciones"], caso.get("operaciones") or [], con)
        salida.append(nueva)
    for var in caso.get("variantes") or []:
        # 2026-11-16 (feature 024, fase 4): una variante con sus reconocimientos (vacia: ninguno).
        cond = var["condiciones"] if var.get("reconocimientos") is None else dict(var["condiciones"], _reconocimientos=var["reconocimientos"])
        for con in var.get("consultas") or []:
            con["esperado"] = esperado_de_consulta(caso, cond, var.get("operaciones") or caso.get("operaciones") or [], con)
    return salida


# ------------------------------------------------------------------------------------------------------------- salida
def numero(d):
    s = format(d.normalize(), "f") if d == d.to_integral_value() else format(d, "f")
    return s if "." in s or "E" in s else s


def a_json(v, sangria=2, nivel=1):
    pad, pad_in = " " * (sangria * (nivel - 1)), " " * (sangria * nivel)
    if isinstance(v, dict):
        if v and all(not isinstance(x, (dict, list)) for x in v.values()):
            return "{ " + ", ".join(f'"{k}": {a_json(x)}' for k, x in v.items()) + " }"
        return "{\n" + ",\n".join(f'{pad_in}"{k}": {a_json(x, sangria, nivel + 1)}' for k, x in v.items()) + "\n" + pad + "}"
    if isinstance(v, list):
        if not v:
            return "[]"
        return "[\n" + ",\n".join(pad_in + a_json(x, sangria, nivel + 1) for x in v) + "\n" + pad + "]"
    if isinstance(v, bool):
        return "true" if v else "false"
    if v is None:
        return "null"
    if isinstance(v, D):
        return numero(v)
    if isinstance(v, int):
        return str(v)
    return json.dumps(v, ensure_ascii=False)


def iguales(a, b):
    if isinstance(a, dict) and isinstance(b, dict):
        return a.keys() == b.keys() and all(iguales(a[k], b[k]) for k in a)
    if isinstance(a, list) and isinstance(b, list):
        return len(a) == len(b) and all(iguales(x, y) for x, y in zip(a, b))
    if isinstance(a, D) or isinstance(b, D):
        return a is not None and b is not None and D(a) == D(b)
    return a == b


def bloque(crudo, clave):
    """(inicio, fin) del arreglo de primer nivel "clave": [...] en el texto crudo (respeta cadenas y comentarios)."""
    marca = '\n  "' + clave + '": '
    i = crudo.index(marca + '[') + len(marca)
    nivel, j, en_cadena = 0, i, False
    while True:
        c = crudo[j]
        if en_cadena:
            if c == "\\":
                j += 2; continue
            if c == '"':
                en_cadena = False
        elif c == '"':
            en_cadena = True
        elif c in "[{":
            nivel += 1
        elif c in "]}":
            nivel -= 1
            if nivel == 0:
                return i, j + 1
        j += 1


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    escribir, verificar = "--escribir" in sys.argv, "--verificar" in sys.argv
    archivos = sorted(f for f in os.listdir(AQUI) if f.endswith(".json") and (not args or any(f.startswith(a) for a in args)))
    malos = 0
    for nombre in archivos:
        ruta = os.path.join(AQUI, nombre)
        caso = cargar(ruta)
        # K6b (T1042): los casos de vivienda los calcula vivienda.py (la misma hoja, en UVR y valorada en pesos).
        if (caso.get("condiciones") or {}).get("reglas") == "Vivienda":
            continue
        esperado = esperado_completo(caso) if "esperado" in caso else None
        # 2026-11-16 (feature 024, fase 4): los montos de los reconocimientos van antes: las consultas los usan.
        reconocimientos_calculados(caso)
        # 2026-11-24 (feature 025, fase 3): las propuestas del recalculo del corte (las calcula antes de que las consultas las lean).
        propuestas = propuestas_calculadas(caso)
        # 2026-11-24 (feature 025, fase 4): el periodo completo deja los ajustes del corte que las consultas leen.
        periodos = periodos_calculados(caso)
        consultas = consultas_calculadas(caso)
        novedades = novedades_calculadas(caso)
        operaciones = operaciones_calculadas(caso, caso["condiciones"], caso.get("operaciones") or [])
        for var in caso.get("variantes") or []:
            if var.get("operaciones"):
                var["operaciones"] = operaciones_calculadas(caso, var["condiciones"], var["operaciones"])
        if escribir:
            with open(ruta, encoding="utf-8") as f:
                crudo = f.read()
            if any("esperado" in o for o in caso.get("operaciones") or []):
                a, b = bloque(crudo, "operaciones")
                crudo = crudo[:a] + a_json(operaciones, 2, 2) + crudo[b:]
            if esperado is not None:
                i = crudo.rindex('\n  "esperado":')
                crudo = crudo[:i] + '\n  "esperado": ' + a_json(esperado, 2, 2) + "\n}\n"
            if caso.get("reconocimientos"):
                a, b = bloque(crudo, "reconocimientos")
                crudo = crudo[:a] + a_json(caso["reconocimientos"], 2, 2) + crudo[b:]
            if propuestas:
                a, b = bloque(crudo, "propuestasDelCorte")
                crudo = crudo[:a] + a_json(propuestas, 2, 2) + crudo[b:]
            if periodos:
                a, b = bloque(crudo, "periodosCompletos")
                crudo = crudo[:a] + a_json(periodos, 2, 2) + crudo[b:]
                a, b = bloque(crudo, "ajustesDelCorte")
                crudo = crudo[:a] + a_json(caso["ajustesDelCorte"], 2, 2) + crudo[b:]
            if consultas:
                a, b = bloque(crudo, "consultas")
                crudo = crudo[:a] + a_json(consultas, 2, 2) + crudo[b:]
            if novedades:
                a, b = bloque(crudo, "novedades")
                crudo = crudo[:a] + a_json(novedades, 2, 2) + crudo[b:]
            if any(v.get("consultas") or v.get("operaciones") for v in caso.get("variantes") or []):
                a, b = bloque(crudo, "variantes")
                crudo = crudo[:a] + a_json(caso["variantes"], 2, 2) + crudo[b:]
            with open(ruta, "w", encoding="utf-8", newline="\n") as f:
                f.write(crudo)
            print(f"{nombre}: escrito")
        elif verificar:
            ok = esperado is None or iguales(json.loads(a_json(esperado), parse_float=D, parse_int=D), caso.get("esperado"))
            original = cargar(ruta)
            for calc, orig in zip(consultas, original.get("consultas") or []):
                ok = ok and iguales(json.loads(a_json(calc["esperado"]), parse_float=D, parse_int=D), orig.get("esperado"))
            for vc, vo in zip(caso.get("variantes") or [], original.get("variantes") or []):
                for calc, orig in zip(vc.get("consultas") or [], vo.get("consultas") or []):
                    ok = ok and iguales(json.loads(a_json(calc["esperado"]), parse_float=D, parse_int=D), orig.get("esperado"))
                for calc, orig in zip(vc.get("operaciones") or [], vo.get("operaciones") or []):
                    if "esperado" in orig:
                        ok = ok and iguales(json.loads(a_json(calc["esperado"]), parse_float=D, parse_int=D), orig.get("esperado"))
            for calc, orig in zip(operaciones, original.get("operaciones") or []):
                if "esperado" in orig:
                    ok = ok and iguales(json.loads(a_json(calc["esperado"]), parse_float=D, parse_int=D), orig.get("esperado"))
            for calc, orig in zip(novedades, original.get("novedades") or []):
                ok = ok and iguales(json.loads(a_json(calc["esperado"]), parse_float=D, parse_int=D), orig.get("esperado"))
            # 2026-11-16 (feature 024, fase 4): los montos de cada reconocimiento.
            for calc, orig in zip(caso.get("reconocimientos") or [], original.get("reconocimientos") or []):
                ok = ok and iguales(json.loads(a_json(calc["esperado"]), parse_float=D, parse_int=D), orig.get("esperado"))
            for vc, vo in zip(caso.get("variantes") or [], original.get("variantes") or []):
                for calc, orig in zip(vc.get("reconocimientos") or [], vo.get("reconocimientos") or []):
                    ok = ok and iguales(json.loads(a_json(calc["esperado"]), parse_float=D, parse_int=D), orig.get("esperado"))
            # 2026-11-24 (feature 025, fase 3): lo que propone cada recalculo del corte.
            for calc, orig in zip(propuestas, original.get("propuestasDelCorte") or []):
                ok = ok and iguales(json.loads(a_json(calc["esperado"]), parse_float=D, parse_int=D), orig.get("esperado"))
            # 2026-11-24 (feature 025, fase 4): el periodo completo y el ajuste del corte que deja.
            for calc, orig in zip(periodos, original.get("periodosCompletos") or []):
                ok = ok and iguales(json.loads(a_json(calc["esperado"]), parse_float=D, parse_int=D), orig.get("esperado"))
            if periodos:
                ok = ok and iguales(json.loads(a_json(caso["ajustesDelCorte"]), parse_float=D, parse_int=D), original.get("ajustesDelCorte"))
            print(f"{nombre}: {'coincide' if ok else 'DIFIERE'}")
            malos += 0 if ok else 1
        else:
            print(f"== {nombre}")
            if esperado is not None:
                print(a_json(esperado, 2, 2))
            if consultas:
                print(a_json(consultas, 2, 2))
            if any("esperado" in o for o in operaciones):
                print(a_json(operaciones, 2, 2))
            if novedades:
                print(a_json(novedades, 2, 2))
            if propuestas:
                print(a_json(propuestas, 2, 2))
            if periodos:
                print(a_json(periodos, 2, 2))
    sys.exit(1 if malos else 0)


if __name__ == "__main__":
    main()
