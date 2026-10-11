#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Hoja de calculo de los casos dorados de Cartera (feature 013, T0330, T26; quickstart 1.2).

Calcula A MANO, con decimal de 40 digitos y sin tocar el codigo C# que se prueba, lo que esperan los casos de
Casos/*.json: el plan cuota a cuota (capital, interes, seguro, saldo), las tasas (diaria, nominal del periodo,
efectiva con y sin cargos) y el veredicto de usura. Nunca en Excel: el double y los empates de redondeo dan otras
cifras (T26).

Uso (desde cualquier carpeta):
    python calcular.py                 # imprime lo esperado de todos los casos
    python calcular.py 01-cuota-fija   # solo los archivos que empiezan asi
    python calcular.py --verificar     # compara con el bloque "esperado" de cada archivo (sale 1 si difiere)
    python calcular.py --escribir      # reescribe el bloque "esperado" (el ultimo del archivo) con lo calculado

Reglas que implementa (las de decisiones-transversales T25-T29, research R33-R37, D1, D3, D9):
- Dias de a excluida a b incluida: 30/360 (el 31 vale 30 y el fin de febrero vale 30), real/365 y real/360.
- Tasa diaria compuesta: raiz B-esima de (1+EA) a 24 decimales (al par), menos 1, a 18 decimales (al par).
  Lineal (D1): NA_m = m*((1+EA)^(1/m) - 1) con la raiz a 24 decimales; r_d = NA_m/B a 18 decimales.
- Tasa de n dias: (1+r_d)^n - 1 compuesta, r_d*n lineal. El interes de la cuota: saldo x tasa del periodo.
- Cuota fija por valor presente con los factores reales: A = (saldo - sum E_k v_k) / sum v_k.
- Gracia total: interes sin capitalizar = sum monto x i_j, a la primera cuota o distribuido.
- Seguros: base x tarifa x factor (1 si cobra con la periodicidad del plan).
- Redondeo por concepto, por cuota o por total; la ultima cuota paga el saldo.
- Usura: TIR diaria de los flujos con fechas reales, (1+TIR)^365 - 1.
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

    def a(self, v, unidad):
        return (v / unidad).quantize(D(1), rounding=self.modo) * unidad

    def unidad_(self, v):
        return self.a(v, self.unidad)

    def centavo(self, v):
        return self.a(v, D("0.01"))

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

    rd = tasa_diaria(ea, conv, eq, periodicidad)
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
        seg_cuota = D(0)
        for s, seg in enumerate(seguros):
            base_s = saldo_ini if seg["base"] == "OutstandingBalance" else monto
            exacto = base_s * D(seg["tarifa"]) * factor_de_seguro(seg, periodicidad, ds[k], numero == 1)
            if total_g and k == g:
                exacto += sg_exacto[s]
            seg_cuota += r.concepto(exacto)
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
        cuotas.append({"numero": numero, "vencimiento": fechas[k].isoformat(), "dias": ds[k], "capital": capital,
                       "interes": interes_total, "seguro": seg_cuota, "saldo": saldo, "_computable": computable})
    assert sum(x["capital"] for x in cuotas) == monto, "el capital no suma el monto"
    return {"rd": rd, "nominal": nominal_del_periodo(ea, periodicidad), "cuotaNivelada": cuota_nivelada,
            "cuotas": cuotas, "interesDeGracia": interes_gracia, "residuo": residuo, "desembolso": desembolso, "monto": monto}


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
                 "totales": {"capital": sum(x["capital"] for x in cuotas), "interes": sum(x["interes"] for x in cuotas),
                             "seguro": sum(x["seguro"] for x in cuotas)},
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


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    escribir, verificar = "--escribir" in sys.argv, "--verificar" in sys.argv
    archivos = sorted(f for f in os.listdir(AQUI) if f.endswith(".json") and (not args or any(f.startswith(a) for a in args)))
    malos = 0
    for nombre in archivos:
        ruta = os.path.join(AQUI, nombre)
        caso = cargar(ruta)
        if caso.get("operaciones"):
            print(f"{nombre}: trae operaciones (US4); se omite"); continue
        esperado = esperado_completo(caso)
        texto = a_json(esperado, 2, 2)
        if escribir:
            with open(ruta, encoding="utf-8") as f:
                crudo = f.read()
            i = crudo.rindex('\n  "esperado":')
            with open(ruta, "w", encoding="utf-8", newline="\n") as f:
                f.write(crudo[:i] + '\n  "esperado": ' + texto + "\n}\n")
            print(f"{nombre}: escrito")
        elif verificar:
            ok = iguales(json.loads(a_json(esperado), parse_float=D, parse_int=D), caso.get("esperado"))
            print(f"{nombre}: {'coincide' if ok else 'DIFIERE'}")
            malos += 0 if ok else 1
        else:
            print(f"== {nombre}\n{texto}")
    sys.exit(1 if malos else 0)


if __name__ == "__main__":
    main()
