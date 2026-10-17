#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Hoja de calculo de los casos dorados de perdida esperada de Cartera (feature 013, K6a, T1013; quickstart 1.3 y 12).

Repasa, con decimal de 40 digitos y SIN tocar el codigo C# que se prueba, lo que esperan los casos pe01-pe10 de esta carpeta. Las reglas
se escribieron desde el Anexo 2 (texto de la Circular Externa 68 de 2024) y las decisiones de la fase 22, no desde el C#:
- historial: dado mes a mes, o construido con sus filas (por mes manda el cierre sobre la carga complementaria sobre la migracion; la fila
  posterior del caso manda sobre la anterior; entre creditos de la cadena, la mayor mora), los meses posteriores al corte con las
  transacciones y los que faltan con la regla (MayorObservada, DelCorte, Fija);
- incumplido: mora mayor que el umbral del segmento (o pago unico con mora mayor que el suyo); no se puntua;
- puntaje = 1/(1+e^-Z) redondeado a scoreDecimals (mitad hacia arriba) y calificacion por el primer corte que lo alcanza (inclusive);
- calificacion final = la peor entre la mora del Anexo 1 sembrado, el modelo y el analisis, con arrastre desde B por deudor y segmento SARC;
- PI de la calificacion final (o la del incumplido), PDI del tipo de garantia (la del segmento sin incumplimiento; el tramo de los dias en
  incumplimiento si lo esta), deterioro = PI x PDI x saldo por concepto al centavo, lo causado de un suspendido (categoria por mora >= C) al
  100 %, y la homologacion (solo si la maxima es la del modelo; el incumplido siempre).

Uso:
    python riesgo-pe.py              # imprime lo calculado
    python riesgo-pe.py --verificar  # compara con "esperado" (sale 1 si algo difiere)
"""
import datetime as dt
import os
import sys
from decimal import Decimal as D, getcontext, ROUND_HALF_UP

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from riesgo import leer, CATS  # noqa: E402  (mismo lector con comentarios)

getcontext().prec = 40
AQUI = os.path.dirname(os.path.abspath(__file__))
NORMAS = os.path.join(AQUI, "..", "Normas")
SEMILLAS = os.path.join(AQUI, "..", "..", "..", "..", "..", "src", "Infrastructure", "IngenIA365ERP.Persistence", "Seeding", "Parametric",
                        "Data", "NationalValues")
PRIORIDAD = {"MonthlyClose": 3, "ComplementaryLoad": 2, "Migration": 1}


def mes(t):
    a, m = t.split("-")
    return int(a) * 12 + int(m) - 1


def rango(desde, hasta):
    return range(mes(desde), mes(hasta or desde) + 1)


def anexo2(nombre):
    return leer(os.path.join(SEMILLAS, "sarc-anexo-2.json") if nombre == "sembrado" else os.path.join(NORMAS, nombre + ".json"))


def historial(c):
    if "moraMensual" in c:
        m = c["moraMensual"]; fin = mes(m["hasta"]); dias = m.get("dias", {})
        return [int(dias.get("%04d-%02d" % (i // 12, i % 12 + 1), 0)) for i in range(fin - int(m["meses"]) + 1, fin + 1)], 0, []
    h = c["historial"]
    cadena = {int(c["id"])} | {int(a) for a in h.get("antecesores", [])}
    filas = {}
    for sec, f in enumerate(h["filas"]):
        for i in rango(f["desde"], f.get("hasta")):
            filas.setdefault(i, {}).setdefault(int(f["credito"]), []).append((PRIORIDAD[f["origen"]], sec, int(f["dias"])))
    trans = {}
    for t in h.get("transacciones", []):
        for i in rango(t["desde"], t.get("hasta")):
            trans[i] = int(t["dias"])
    ref = dt.date.fromisoformat(h["fechaDeReferencia"]); ini = dt.date.fromisoformat(h["inicioDeLaCadena"])
    r = ref.year * 12 + ref.month - 1
    inicio = max(r - int(h["mesesExigidos"]) + 1, ini.year * 12 + ini.month - 1)
    corte = h.get("corteDeMigracion")
    corte = None if corte is None else (lambda d: d.year * 12 + d.month - 1)(dt.date.fromisoformat(corte))
    valores, faltan = [], []
    for i in range(inicio, r + 1):
        porCredito = [max(v)[2] for k, v in filas.get(i, {}).items() if k in cadena]
        if porCredito:
            valores.append(max(porCredito))
        elif corte is None or i > corte:
            valores.append(trans[i])
        else:
            valores.append(None); faltan.append(i)
    vistos = [v for v in valores if v is not None]
    regla = h.get("regla", "MayorObservada")
    relleno = {"MayorObservada": max(vistos) if vistos else int(h.get("diasDelCorte") or 0),
               "DelCorte": int(h.get("diasDelCorte") or 0), "Fija": int(h.get("diasFijos") or 0)}[regla]
    return [relleno if v is None else v for v in valores], len(faltan), [relleno] * len(faltan)


def variable(v, c, hist, ctx):
    k = v["kind"]
    if k == "ActiveAssociate":
        return 1 if c.get("asociadoActivo") else 0
    if k == "EmployeeFund":
        return 1 if ctx["fondoDeEmpleados"] else 0
    if k == "InstallmentAtMostSmmlvFraction":
        if v.get("onlyEmployeeFund") and not ctx["fondoDeEmpleados"]:
            return 0
        return 1 if D(c.get("cuota", 0)) <= D(ctx["smmlv"]) * D(v["fraction"]) else 0
    if k == "TermAtMostMonths":
        if v.get("onlyEmployeeFund") and not ctx["fondoDeEmpleados"]:
            return 0
        return 1 if int(c.get("plazoEnMeses", 0)) <= int(v["months"]) else 0
    for cond in v["conditions"]:
        ventana = hist[-int(cond["months"]):]
        lo, hi = int(cond["fromDays"]), cond.get("toDays")
        dentro = lambda d: d >= lo and (hi is None or d <= int(hi))
        if cond["measure"] == "Max" and not dentro(max(ventana)):
            return 0
        if cond["measure"] == "AnyMonth" and not any(dentro(d) for d in ventana):
            return 0
    return 1


def por_mora(a1, sarc, dias):
    seg = [s for s in a1["segments"] if s["code"] == sarc][0]
    return [c["category"] for c in seg["categories"] if int(c["minDaysPastDue"]) <= dias][-1], seg["accrualSuspensionCategory"]


def correr(verificar):
    fallas = 0
    a1 = leer(os.path.join(SEMILLAS, "sarc-anexo-1.json"))
    for nombre in sorted(f for f in os.listdir(AQUI) if f.startswith("pe") and f.endswith(".json")):
        caso = leer(os.path.join(AQUI, nombre)); pe = caso["perdidaEsperada"]; a2 = anexo2(pe.get("anexo2", "sembrado")); ctx = pe["contexto"]
        print("== %s" % caso["nombre"])
        segs = {s["code"]: s for s in a2["segments"]}
        variables = {v["code"]: v for v in a2["variables"]}
        res = {}
        for c in [x for x in pe["creditos"] if x.get("segmento")]:
            s = segs[c["segmento"]]; hist, faltan, completados = historial(c); dias = int(c["diasDeMora"])
            r = {"faltantes": faltan, "completados": completados, "sarc": s["sarcSegment"], "dias": dias}
            en = -1
            if dias > int(s["defaultDaysPastDue"]):
                en = dias - int(s["defaultDaysPastDue"])
            if c.get("pagoUnico") and a2.get("singleInstallmentDefaultDaysPastDue") is not None and dias > int(a2["singleInstallmentDefaultDaysPastDue"]):
                en = max(en, dias - int(a2["singleInstallmentDefaultDaysPastDue"]))
            r["incumplido"] = en >= 0; r["diasEnIncumplimiento"] = max(en, 0)
            if not r["incumplido"]:
                vals = [(t["variable"], variable(variables[t["variable"]], c, hist, ctx), D(t["coefficient"])) for t in s["terms"]]
                z = D(s["intercept"]) + sum(co * v for _, v, co in vals)
                q = D(1).scaleb(-int(a2["scoreDecimals"]))
                puntaje = (1 / (1 + (-z).exp())).quantize(q, rounding=ROUND_HALF_UP)
                modelo = [x["rating"] for x in s["cutoffs"] if puntaje <= D(x["upTo"])][0]
                r.update(z=z, puntaje=puntaje, calificacionDelModelo=modelo, variables=["%s=%d" % (n, v) for n, v, _ in vals])
            mora, susp = por_mora(a1, s["sarcSegment"], dias)
            cand = [mora] + ([r["calificacionDelModelo"]] if not r["incumplido"] else []) + ([c["analisis"]["categoria"]] if c.get("analisis") else [])
            r["propia"] = max(cand, key=CATS.index); r["porMora"] = mora; r["suspension"] = susp
            r["causa"] = "Arrears" if r["propia"] == mora else ("Model" if not r["incumplido"] and r["propia"] == r["calificacionDelModelo"] else "Analysis")
            res[int(c["id"])] = (c, r)
        for cid, (c, r) in res.items():
            grupo = [x for x in res.values() if x[0]["deudor"] == c["deudor"] and x[1]["sarc"] == r["sarc"]]
            peor = max((x[1]["propia"] for x in grupo), key=CATS.index)
            r["calificacionFinal"] = r["propia"]
            if CATS.index(peor) >= CATS.index("B") and CATS.index(peor) > CATS.index(r["propia"]):
                r["calificacionFinal"] = peor; r["causa"] = "Drag"
            arrastre = not r["incumplido"] and any(x[1]["incumplido"] for x in grupo)
            moraActual = max(x[1]["dias"] for x in grupo)
            inc = r["incumplido"] or arrastre
            clave = "VOLUNTARIA" if ctx["voluntaria"] else ctx["tipoDeOrganizacion"]
            tabla = [t for t in a2["probabilityOfDefault"] if t["segment"] == c["segmento"] and t["organizationType"] == clave][0]
            pi = D(tabla["defaultRate"]) if inc else D([x["rate"] for x in tabla["ratings"] if x["rating"] == r["calificacionFinal"]][0])
            gar = [g for g in a2["lossGivenDefault"] if g["collateralType"] == c["garantia"]][0]
            propia = [p for p in segs[c["segmento"]].get("performingLossGivenDefault", []) if p["collateralType"] == c["garantia"]]
            if inc:
                pdi = D([b["rate"] for b in gar["brackets"] if int(b["fromDaysAfterDefault"]) <= r["diasEnIncumplimiento"]][-1])
            else:
                pdi = D(propia[0]["rate"]) if propia else D(gar["brackets"][0]["rate"])
            sal = c["saldos"]; k = D(sal["capital"]); it = D(sal["interesCorriente"]) + D(sal["interesDeMora"]); ot = D(sal["otros"])
            suspendido = c.get("suspendido", CATS.index(r["porMora"]) >= CATS.index(r["suspension"]))
            cent = lambda x: x.quantize(D("0.01"), rounding=ROUND_HALF_UP)
            dc = cent(k * pi * pdi)
            di, do = (cent(it), cent(ot)) if suspendido and a2.get("suspendedConceptsFullyImpaired") else (cent(it * pi * pdi), cent(ot * pi * pdi))
            hom = [h for h in a2["homologation"] if c["segmento"] in h["segments"]][0]["rows"]
            dentro = lambda f: moraActual >= int(f["fromCurrentDaysPastDue"]) and (f.get("toCurrentDaysPastDue") is None or moraActual <= int(f["toCurrentDaysPastDue"]))
            if inc:
                filas = [f for f in hom if f["modelRating"] == "Default"]
                contable, homologada = ([f for f in filas if dentro(f)] or filas)[0]["accountingRating"], True
            elif r.get("calificacionDelModelo") == r["calificacionFinal"]:
                contable, homologada = [f for f in hom if f["modelRating"] == r["calificacionFinal"] and dentro(f)][0]["accountingRating"], True
            else:
                contable, homologada = r["calificacionFinal"], False
            r.update(incumplido=inc, claveDePi=clave, pi=pi, pdi=pdi, exposicion=k + it + ot, suspendido=suspendido, deterioroCapital=dc,
                     deterioroIntereses=di, deterioroOtros=do, total=dc + di + do, contable=contable, homologada=homologada)
            if r["completados"]:
                r["completadosCon"] = r["completados"][0]
            print("  %5d %-21s Z %-7s puntaje %-6s modelo %-4s final %s (%s) PI %s PDI %s → %s; contable %s%s" % (
                cid, c["segmento"], r.get("z", "—"), r.get("puntaje", "—"), r.get("calificacionDelModelo", "inc."), r["calificacionFinal"],
                r["causa"], pi, pdi, r["total"], contable, "" if homologada else " (sin homologar)"))
        for esp in pe["esperado"]:
            if int(esp["id"]) not in res:
                continue
            _, r = res[int(esp["id"])]
            for k, v in esp.items():
                if k in ("id", "explicacionContiene", "modelo"):
                    continue
                calc = r.get(k)
                igual = (D(calc) == D(v)) if isinstance(v, D) and calc is not None else calc == v
                if not igual:
                    fallas += 1
                    print("  !! %s credito %s: %s esperado %s, calculado %s" % (caso["nombre"], esp["id"], k, v, calc))
    if verificar:
        print("fallas: %d" % fallas)
        sys.exit(1 if fallas else 0)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    correr("--verificar" in sys.argv)
