#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Hoja de calculo de los casos dorados de riesgo de Cartera (feature 013, US9, T0672; quickstart 1.3).

Repasa, con decimal de 40 digitos y SIN tocar el codigo C# que se prueba, lo que esperan los casos r01-r16 de esta carpeta: la
altura de mora con el calendario del art. 829 (domingos y festivos nacionales no son habiles; el sabado si), la calificacion (mora,
pago unico, credito nuevo, reestructurado, insolvencia, analisis, arrastre por deudor y segmento), la subcategoria, la contable, el
deterioro individual del Anexo 1 (garantias por meses de mora, D19, suspension al 100 %, redondeo al centavo o al peso), la politica
efectiva (lo mas estricto por celda), el deterioro general y el selector de modelo. Las reglas se escribieron desde la spec
(FR-093, FR-098-FR-105), las decisiones T45-T50 y D19/D28, no desde el C#.

Uso (desde cualquier carpeta):
    python riesgo.py              # imprime lo calculado de cada caso y variante
    python riesgo.py --verificar  # compara con el bloque "esperado" (sale 1 si algo difiere)
"""
import datetime as dt
import json
import os
import sys
from decimal import Decimal as D, getcontext, ROUND_HALF_UP

getcontext().prec = 40

AQUI = os.path.dirname(os.path.abspath(__file__))
NORMAS = os.path.join(AQUI, "..", "Normas")
CATS = ["A", "B", "C", "D", "E"]
PE_SEGMENTOS = ["CONSUMO_LIBRANZA", "CONSUMO_SIN_LIBRANZA", "COMERCIAL_PN"]
ENTREGAS = ["K1", "K2", "K3", "K4", "K5", "K6a", "K6b"]
# Desempate de causas (la primera gana) y orden en que se listan las candidatas.
DESEMPATE = ["Insolvency", "SingleInstallment", "Restructured", "NewLoan", "Arrears", "Model", "Homologation", "Evaluation",
             "Analysis", "Drag"]


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
        salida.append(c); i += 1
    return "".join(salida)


def leer(ruta):
    with open(ruta, encoding="utf-8") as f:
        return json.loads(sin_comentarios(f.read()), parse_float=D, parse_int=D)


def idx(cat):
    return CATS.index(cat)


# ------------------------------------------------------------------------------------------------ politica efectiva
def efectiva(norma, politica):
    """Lo mas estricto por celda: inicio de categoria menor, porcentaje mayor, fraccion de garantia menor, minimo general mayor."""
    import copy
    n = copy.deepcopy(norma)
    celdas = []
    politica = politica or {}
    for ps in politica.get("segments", []):
        seg = next(s for s in n["segments"] if s["code"] == ps["code"])
        mins = [int(c["minDaysPastDue"]) for c in seg["categories"]]
        propias = []
        for pc in ps.get("categories", []):
            i = idx(pc["category"])
            ef = min(mins[i], int(pc["minDaysPastDue"]))
            propias.append(("segments.%s.categories.%s.minDaysPastDue" % (seg["code"], pc["category"]), D(mins[i]), D(int(pc["minDaysPastDue"])), D(ef)))
            mins[i] = ef
        if propias:
            if all(a < b for a, b in zip(mins, mins[1:])):
                celdas.extend(propias)
                for i, c in enumerate(seg["categories"]):
                    c["minDaysPastDue"] = D(mins[i])
                    c["maxDaysPastDue"] = D(mins[i + 1] - 1) if i + 1 < len(mins) else None
        for pr in ps.get("impairmentRates", []):
            sub = pr.get("subcategory")
            cand = [r for r in seg["impairmentRates"] if r["category"] == pr["category"] and r.get("subcategory") == sub
                    and (pr.get("conditionFrom") is None or (r.get("condition") or {}).get("from") == pr["conditionFrom"])]
            assert len(cand) == 1
            r = cand[0]
            ruta = "segments.%s.impairmentRates.%s" % (seg["code"], r["category"])
            if sub: ruta += "." + sub
            if r.get("condition"): ruta += ".from%d" % int(r["condition"]["from"])
            ef = max(r["rate"], pr["rate"])
            celdas.append((ruta, r["rate"], pr["rate"], ef))
            r["rate"] = ef
    for pg in politica.get("collateral", []):
        g = next(x for x in n["collateral"] if x["nature"] == pg["nature"])
        for pb in pg["brackets"]:
            b = next(x for x in g["brackets"] if x["fromMonths"] == pb["fromMonths"])
            ef = min(b["admissibleFraction"], pb["admissibleFraction"])
            celdas.append(("collateral.%s.brackets.%d.admissibleFraction" % (pg["nature"], int(pb["fromMonths"])), b["admissibleFraction"], pb["admissibleFraction"], ef))
            b["admissibleFraction"] = ef
    gi = politica.get("generalImpairment", {})
    if "minimumRate" in gi:
        for p in n["generalImpairment"]:
            ef = max(p["minimumRate"], gi["minimumRate"])
            celdas.append(("generalImpairment.minimumRate", p["minimumRate"], gi["minimumRate"], ef))
            p["minimumRate"] = ef
    adicional = gi.get("additionalRate", D(0))
    if adicional > 0:
        celdas.append(("generalImpairment.additionalRate", D(0), adicional, adicional))
    return n, celdas, adicional, bool(gi.get("additionalCompensatesIndividual", False))


# ------------------------------------------------------------------------------------------------ mora
def dias_de_mora(caso, cr):
    if not cr.get("cuotas"):
        return int(cr.get("diasDeMora") or 0)
    corte = dt.date.fromisoformat(caso["corte"])
    festivos = {dt.date.fromisoformat(f) for f in caso.get("festivos", [])}
    tol = cr.get("tolerancia", D(0))
    peor = None
    for q in cr["cuotas"]:
        f = dt.date.fromisoformat(q["vencimiento"])
        while f.weekday() == 6 or f in festivos:   # domingo o festivo: el habil siguiente
            f += dt.timedelta(days=1)
        if corte > f and q["saldoVencido"] > tol:
            if peor is None or int(q["numero"]) < peor[0]:
                peor = (int(q["numero"]), f)
    return 0 if peor is None else (corte - peor[1]).days


def segmento(n, codigo):
    return next(s for s in n["segments"] if s["code"] == codigo)


def cat_por_mora(n, codigo, dias):
    r = None
    for c in segmento(n, codigo)["categories"]:
        if dias >= c["minDaysPastDue"]:
            r = c["category"]
    return r


def porcentaje(n, codigo, cat, sub, dias, meses_en_e):
    lista = [p for p in segmento(n, codigo)["impairmentRates"] if p["category"] == cat and (sub is None or (p.get("subcategory") or "None") == sub)]
    for p in lista:
        c = p.get("condition")
        if c is None:
            return p
        v = dias if c["kind"] == "DaysPastDue" else (meses_en_e or 0)
        if v >= c["from"] and (c.get("to") is None or v <= c["to"]):
            return p
    return sorted(lista, key=lambda p: (p.get("condition") or {}).get("from", 0))[0]


# ------------------------------------------------------------------------------------------------ calificacion
def candidatas(n, cr, dias):
    c = []
    mora = cat_por_mora(n, cr["segmento"], dias)
    ins = cr.get("insolvencia")
    if ins and ins["estado"] == "Negotiating":
        c.append(("Insolvency", ins["categoriaAlIniciar"]))
    else:
        c.append(("Arrears", mora))
    if ins and ins["estado"] == "Failed" and n.get("insolvency"):
        c.append(("Insolvency", n["insolvency"]["failedCategory"]))
    if cr.get("modelo"):
        c.append(("Model", cr["modelo"]))
    if cr.get("pagoUnico"):
        ok = [r["category"] for r in n["singleInstallment"] if r["minDaysPastDue"] <= dias]
        if ok:
            c.append(("SingleInstallment", max(ok, key=idx)))
    if cr.get("nuevo") and n.get("newLoan"):
        c.append(("NewLoan", n["newLoan"]["category"]))
    re = cr.get("reestructuracion")
    if re:
        regla = n["restructured"]
        ini = max(re["categoriaAlReestructurar"], regla["minimumCategory"], key=idx)
        if dias > 0:
            c.append(("Restructured", ini))
        else:
            k = int(regla["consecutivePaidInstallmentsToImprove"])
            tramos = int(re["cuotasPagadasSeguidas"]) // k if k > 0 else 0
            c.append(("Restructured", CATS[max(0, idx(ini) - tramos)]))
    if cr.get("evaluacion"):
        c.append(("Evaluation", cr["evaluacion"]["categoria"]))
    if cr.get("analisis"):
        c.append(("Analysis", cr["analisis"]["categoria"]))
    return c, mora


def peor(cands):
    return sorted(cands, key=lambda x: (-idx(x[1]), DESEMPATE.index(x[0])))[0]


def calificar(n, caso, creditos, con_arrastre):
    dias = {cr["id"]: dias_de_mora(caso, cr) for cr in creditos}
    fase1 = {}
    for cr in creditos:
        cs, mora = candidatas(n, cr, dias[cr["id"]])
        fase1[cr["id"]] = (cs, mora)
    propia = {k: peor(v[0]) for k, v in fase1.items()}
    regla = n["drag"]
    res = {}
    for cr in creditos:
        cs = list(fase1[cr["id"]][0])
        if con_arrastre:
            personas = {cr["deudor"]} | (set(cr.get("codeudores", [])) if regla["includesCoDebts"] else set())
            grupo = [x for x in creditos if x["deudor"] in personas and (not regla["withinSegment"] or x["segmento"] == cr["segmento"])]
            p = sorted(((propia[x["id"]][1], x["id"]) for x in grupo), key=lambda t: (-idx(t[0]), t[1]))[0]
            if idx(p[0]) >= idx(regla["fromCategory"]) and idx(p[0]) > idx(propia[cr["id"]][1]):
                cs.append(("Drag", p[0]))
        causa, cat = peor(cs)
        sub = "None"
        if cat == "E":
            imp = cr.get("analisis") if causa == "Analysis" else (cr.get("evaluacion") if causa == "Evaluation" else None)
            if imp and imp.get("subcategoria", "None") != "None":
                sub = imp["subcategoria"]
            elif any(p.get("subcategory") for p in segmento(n, cr["segmento"])["impairmentRates"] if p["category"] == "E"):
                sub = porcentaje(n, cr["segmento"], "E", None, dias[cr["id"]], cr.get("mesesEnE")).get("subcategory") or "None"
        res[cr["id"]] = {"diasDeMora": dias[cr["id"]], "categoriaPorMora": fase1[cr["id"]][1], "categoria": cat, "subcategoria": sub,
                         "causa": causa, "contable": cat, "candidatas": ["%s:%s" % t for t in cs]}
    return res


# ------------------------------------------------------------------------------------------------ deterioro
def redondear(x, modo):
    return x.quantize(D("0.01") if modo == "Centavo" else D("1"), rounding=ROUND_HALF_UP)


def fraccion_admisible(n, naturaleza, meses):
    g = next((x for x in n["collateral"] if x["nature"] == naturaleza), None)
    if g is None:
        return D(0)
    for b in g["brackets"]:
        if meses >= b["fromMonths"] and (b.get("toMonths") is None or meses < b["toMonths"]):
            return b["admissibleFraction"]
    return D(0)


def deteriorar(n, cr, cal, modo):
    dias = cal["diasDeMora"]
    meses = dias // 30
    adm = sum((g["valor"] * fraccion_admisible(n, g["naturaleza"], meses) for g in cr.get("garantias", [])), D(0))
    s = cr.get("saldos", {})
    cap = s.get("capital", D(0))
    p = porcentaje(n, cr["segmento"], cal["categoria"], cal["subcategoria"], dias, cr.get("mesesEnE"))
    aportes = D(0)   # D19 (o sin indicadores): nunca se descuentan
    base = cap if p["base"] == "TotalBalance" else max(D(0), cap - adm - aportes)
    dcap = redondear(base * p["rate"], modo)
    susp = cr.get("suspendido")
    if susp is None:
        susp = idx(cal["categoria"]) >= idx(segmento(n, cr["segmento"])["accrualSuspensionCategory"])
    di = redondear(s.get("interesCorriente", D(0)) + s.get("interesDeMora", D(0)), modo) if susp else D(0)
    do = redondear(s.get("otros", D(0)), modo) if susp else D(0)
    return {"garantiaAdmisible": adm, "aportes": aportes, "base": base, "porcentaje": p["rate"], "deterioroCapital": dcap,
            "deterioroIntereses": di, "deterioroOtros": do, "deterioroTotal": dcap + di + do}


def general(n, gen, creditos, det, adicional, compensa, modo):
    perfil = next((p for p in n["generalImpairment"] if p["profile"] == gen.get("perfil")), None) or \
        next(p for p in n["generalImpairment"] if p["profile"] == "GENERAL")
    minimo = max(perfil["minimumRate"], gen["limiteLegalMinimo"])
    modelos = gen.get("modelosConGeneral", ["ArrearsAnnex1"])
    clave = lambda x: (int(x["sucursal"]), x["modalidad"], x.get("subsegmento"), bool(x.get("empleado", False)), x.get("modelo", "ArrearsAnnex1"))
    grupos = {}
    for l in gen["lineas"]:
        if l.get("modelo", "ArrearsAnnex1") not in modelos:
            continue
        cr = next(c for c in creditos if c["id"] == l["credito"])
        b, i = grupos.get(clave(l), (D(0), D(0)))
        grupos[clave(l)] = (b + cr["saldos"]["capital"], i + det[l["credito"]]["deterioroTotal"])
    anteriores = {clave(a): a["monto"] for a in gen.get("anteriores", [])}
    salida = {}
    for k, (b, i) in grupos.items():
        mm = redondear(b * minimo, modo); ma = redondear(b * adicional, modo)
        comp = min(ma, i) if compensa else D(0)
        ant = anteriores.get(k, D(0))
        salida[k] = (b, minimo, adicional, mm, ma, comp, mm + ma - comp, ant, mm + ma - comp - ant)
    for k, ant in anteriores.items():
        if k not in salida:
            salida[k] = (D(0), minimo, adicional, D(0), D(0), D(0), D(0), ant, -ant)
    return salida


def selector(sel):
    regla = leer(os.path.join(NORMAS, sel.get("regla", "regla-prueba") + ".json"))
    tipo, cat = sel.get("tipo"), sel.get("categoriaDeFondo")
    obligada = tipo in regla["obligatedOrganizationTypes"] and (not cat or not regla["obligatedEmployeeFundCategories"]
                                                                 or cat in regla["obligatedEmployeeFundCategories"])
    k6a = ENTREGAS.index(sel.get("entrega", "K1")) >= ENTREGAS.index("K6a")
    out = []
    for s in PE_SEGMENTOS:
        if obligada and s in regla["coveredSegments"]:
            out.append((s, "ExpectedLossAnnex2" if k6a else "ArrearsAnnex1", "Obligated", not k6a))
        elif k6a and sel.get("eleccion") == "VoluntaryAdoption" and s in sel.get("segmentosDeLaEleccion", []):
            out.append((s, "ExpectedLossAnnex2", "Voluntary", False))
        else:
            out.append((s, "ArrearsAnnex1", "NotObligated", False))
    return out


# ------------------------------------------------------------------------------------------------ casos
def escenarios(caso):
    base = {"nombre": caso["nombre"], "norma": caso.get("norma", "anexo1-prueba"), "politica": caso.get("politica"),
            "opciones": caso.get("opciones", {}), "general": caso.get("general"), "selector": caso.get("selector"),
            "esperado": caso.get("esperado", {})}
    yield base
    for v in caso.get("variantes", []):
        e = dict(base)
        e["nombre"] = "%s · %s" % (caso["nombre"], v["nombre"])
        for k in ("norma", "politica", "opciones", "general", "selector"):
            if v.get(k) is not None:
                e[k] = v[k]
        e["esperado"] = v.get("esperado", {})
        yield e


def igual(a, b):
    if isinstance(a, D) or isinstance(b, D):
        return D(a) == D(b)
    return a == b


def correr(verificar):
    fallas = 0
    for nombre in sorted(f for f in os.listdir(AQUI) if f.endswith(".json")):
        caso = leer(os.path.join(AQUI, nombre))
        for e in escenarios(caso):
            norma = leer(os.path.join(NORMAS, e["norma"] + ".json"))
            n, celdas, adicional, compensa = efectiva(norma, e["politica"])
            op = e["opciones"]
            cal = calificar(n, caso, caso["creditos"], op.get("conArrastre", True))
            modo = op.get("redondeo", "Centavo")
            det = {cr["id"]: deteriorar(n, cr, cal[cr["id"]], modo) for cr in caso["creditos"]}
            print("== %s" % e["nombre"])
            for cr in caso["creditos"]:
                c = cal[cr["id"]]; d = det[cr["id"]]
                print("  %3d %4d d %s → %s%s %-17s base %s × %s = %s (+%s +%s)" % (cr["id"], c["diasDeMora"], c["categoriaPorMora"], c["categoria"],
                      "" if c["subcategoria"] == "None" else "/" + c["subcategoria"], c["causa"], d["base"], d["porcentaje"], d["deterioroCapital"],
                      d["deterioroIntereses"], d["deterioroOtros"]))
            esp = e["esperado"]
            for x in esp.get("creditos", []) or []:
                calc = dict(cal[x["id"]]); calc.update(det[x["id"]])
                for k, v in x.items():
                    if k in ("id", "explicacionContiene"):
                        continue
                    if k == "subcategoria" and v is None:
                        v = "None"
                    if not igual(calc[k], v):
                        fallas += 1
                        print("  !! %s crédito %s: %s esperado %s, calculado %s" % (e["nombre"], x["id"], k, v, calc[k]))
            if esp.get("celdas") is not None:
                a = sorted((r, D(no), D(p), D(ef)) for r, no, p, ef in celdas)
                b = sorted((c["ruta"], D(c["norma"]), D(c["propuesta"]), D(c["efectiva"])) for c in esp["celdas"])
                if a != b:
                    fallas += 1; print("  !! celdas: esperado %s, calculado %s" % (b, a))
            if esp.get("menosEstrictas") is not None:
                a = sorted(r for r, no, p, ef in celdas if ef != p)
                if a != sorted(esp["menosEstrictas"]):
                    fallas += 1; print("  !! menos estrictas: esperado %s, calculado %s" % (sorted(esp["menosEstrictas"]), a))
            if esp.get("general") is not None:
                g = general(n, e["general"], caso["creditos"], det, adicional, compensa, modo)
                clave = lambda x: (int(x["sucursal"]), x["modalidad"], x.get("subsegmento"), bool(x.get("empleado", False)), x.get("modelo", "ArrearsAnnex1"))
                b = {clave(x): tuple(D(x[k]) for k in ("carteraBruta", "porcentajeMinimo", "porcentajeAdicional", "montoMinimo", "montoAdicional",
                                                         "compensacion", "total", "anterior", "delta")) for x in esp["general"]}
                for k, v in g.items():
                    print("  general %s: %s" % (k, ", ".join(str(t) for t in v)))
                if {k: tuple(D(t) for t in v) for k, v in g.items()} != b:
                    fallas += 1; print("  !! general difiere")
            if esp.get("selector") is not None:
                s = selector(e["selector"])
                b = [(x["segmento"], x["modelo"], x["fundamento"], x["marca"]) for x in esp["selector"]]
                print("  selector %s" % s)
                if s != b:
                    fallas += 1; print("  !! selector: esperado %s" % b)
                if esp.get("marca") is not None and any(t[3] for t in s) != esp["marca"]:
                    fallas += 1; print("  !! marca")
    if verificar:
        print("\n%s" % ("todo coincide" if fallas == 0 else "%d diferencias" % fallas))
        sys.exit(1 if fallas else 0)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    correr("--verificar" in sys.argv)
