#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Hoja de calculo de los casos dorados de VIVIENDA (feature 013, K6b, T1042; FR-024, D18): 61-vivienda-pesos-tasa-fija,
62-vivienda-uvr-cuota-constante-en-uvr, 63-vivienda-uvr-pago-y-reverso, 64-vivienda-sin-mora-sobre-intereses y 65-vivienda-prepago-sin-penalidad.

Usa la reconstruccion de calcular.py (decimal de 40 digitos, sin tocar el C#) con las reglas de la Ley 546 de 1999 de la version de reglas de
vivienda y, en UVR, la corre EN UVR y la valora en pesos. Las reglas que implementa (las de Domain/Lending/Housing, escritas desde la spec):

- Vivienda (pesos o UVR): sin mora sobre intereses aunque la condicion lo diga; el tope del dia es el menor entre el tope de vivienda
  (series.topeDeVivienda) y la usura (en estos casos no hay usura de VIVIENDA: rige el tope).
- UVR: AUvr(p) = signo x redondeo(|p| / UVR, 4 decimales); APesos(u) = signo x redondeo(|u| x UVR, 2 decimales); con el modo de la linea.
  * El plan y el primer desembolso a la UVR de la fecha de desembolso; la causacion, los pagos y los saldos en UVR a la diezmilesima.
  * Un pago se convierte a la UVR de su fecha valor con el estado de ese dia: si alcanza el valor para cancelar en pesos, cancela exactamente lo
    que se debe en UVR y el resto se convierte aparte.
  * Saldos en pesos a la UVR de D: el capital de las cuotas por ACUMULADO contra la valoracion del capital total; los demas uno a uno.
  * Un pago en pesos: lo usado del anticipo uno a uno; lineas, anticipo, saldo a favor y excedente por acumulado contra lo recibido.
  * El plan en pesos a la UVR de D, cada valor uno a uno.
  * Libro: el desembolso por lo que fue en pesos; los pagos segun su distribucion en pesos; la causacion a la UVR de su dia; y la CORRECCION
    MONETARIA (MonetaryCorrection) de cada dia: APesos(saldo UVR del dia, UVR del dia) - APesos(saldo UVR de la vispera, UVR de la vispera) - lo
    que el libro en pesos ya movio ese dia; el capital por su total (sin cuota), los demas conceptos por cuota.
  * La UVR de un dia sin publicar (liquidacion futura): la ultima publicada por el factor de sus dos ultimos dias publicados elevado a los dias
    que faltan, a 4 decimales; marcada proyectada, solo para mostrar.

La serie UVR de los casos es DE PRUEBA (no oficial): la arma serie_de_prueba() con la formula del Banco de la Republica (UVR del 15 por
(1 + variacion)^(t/d)) y variaciones inventadas. Pendiente de que la contadora firme los valores (quickstart §12).

Uso:
    python vivienda.py                 # imprime lo esperado
    python vivienda.py --verificar     # compara con los "esperado" de los archivos (sale 1 si difiere)
    python vivienda.py --escribir      # reescribe las "consultas" y las "operaciones" con "esperado"
    python vivienda.py --serie         # imprime la serie UVR de prueba (para pegarla en series.uvr)
"""
import copy
import datetime as dt
import json
import os
import sys
from decimal import Decimal as D

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import calcular as C  # noqa: E402

DIEZMILESIMA, CENTAVO = D("0.0001"), D("0.01")


# --------------------------------------------------------------------------------------------------------- serie UVR de prueba
def serie_de_prueba():
    """UVR diaria de prueba del 2026-10-16 al 2027-02-15: UVR_t = UVR_15 x (1 + i)^(t/d), t los dias desde el 15, d los del periodo."""
    inicial = D("405.1234")
    variaciones = [D("0.0042"), D("0.0038"), D("0.0051"), D("0.0047")]   # inventadas: NO son el IPC
    salida = []
    quince = dt.date(2026, 10, 15)
    for i in variaciones:
        siguiente = dt.date(quince.year + (quince.month // 12), quince.month % 12 + 1, 15)
        d = (siguiente - quince).days
        for t in range(1, d + 1):
            f = quince + dt.timedelta(days=t)
            v = C.q(inicial * (1 + i) ** (D(t) / D(d)), 4, C.MODOS["AwayFromZero"])
            salida.append({"fecha": f.isoformat(), "valor": v})
        inicial = salida[-1]["valor"]
        quince = siguiente
    return salida


# --------------------------------------------------------------------------------------------------------------- conversiones
def signo(x):
    return D(-1) if x < 0 else D(1)


def a_uvr(p, u, modo):
    return signo(p) * C.q(abs(p) / u, 4, modo)


def a_pesos(x, u, modo):
    return signo(x) * C.q(abs(x) * u, 2, modo)


def repartir(valores, u, modo, total=None):
    salida, acumulado, anterior = [], D(0), D(0)
    for x in valores:
        acumulado += x
        ahora = a_pesos(acumulado, u, modo)
        salida.append(ahora - anterior)
        anterior = ahora
    if total is not None:
        ultimos = [i for i, x in enumerate(valores) if x != 0]
        if ultimos:
            salida[ultimos[-1]] += total - sum(salida, D(0))
    return salida


class Serie:
    def __init__(self, filas):
        self.v = {C.fecha(x["fecha"]): D(x["valor"]) for x in filas}

    def publicada(self, f):
        return self.v.get(f)

    def requerida(self, f):
        assert f in self.v, f"no hay UVR publicada el {f}"
        return self.v[f]

    def del_dia(self, f):
        """(valor, proyectada)."""
        if f in self.v:
            return self.v[f], False
        antes = sorted(x for x in self.v if x < f)
        ultima = antes[-1]
        factor = self.v[ultima] / self.v[antes[-2]] if len(antes) > 1 and antes[-2] == ultima - C.UN_DIA else D(1)
        return C.q(factor ** (f - ultima).days * self.v[ultima], 4, C.MODOS["AwayFromZero"]), True


def saldos_en_pesos(por_cuota, u, modo):
    """por_cuota: lista [(cuota, concepto, valor UVR)] en el orden del estado -> la misma lista en pesos (sin ceros)."""
    capital = [x for x in por_cuota if x[1] == "Principal"]
    repartido = repartir([x[2] for x in capital], u, modo)
    capital_pesos = {id(x): p for x, p in zip(capital, repartido)}
    salida = []
    for x in por_cuota:
        p = capital_pesos[id(x)] if x[1] == "Principal" else a_pesos(x[2], u, modo)
        if p:
            salida.append((x[0], x[1], p))
    return salida


def valor_para_cancelar(lista):
    return sum((x[2] for x in lista if x[1] in C.COBRABLES), D(0)) - sum((x[2] for x in lista if x[1] == "Advance"), D(0))


def ordenados(por):
    return [(k[0], k[1], v) for k, v in sorted(por.items(), key=lambda kv: (kv[0][0] or 0, C.ORDEN[kv[0][1]]))]


def plan_en_pesos(p, u, modo):
    q = copy.copy(p)
    q["cuotas"] = [dict(x, capital=a_pesos(x["capital"], u, modo), interes=a_pesos(x["interes"], u, modo),
                        seguro=a_pesos(x["seguro"], u, modo), saldo=a_pesos(x["saldo"], u, modo)) for x in p["cuotas"]]
    return q


def distribucion_en_pesos(d, valor, u, modo):
    usado = [dict(x, valor=a_pesos(x["valor"], u, modo)) for x in d["anticipoUsado"]]
    comps = [l["valor"] for l in d["lineas"]] + [d["anticipo"], d["saldoAFavor"], d["excedente"]]
    rep = repartir(comps, u, modo, valor + sum((x["valor"] for x in usado), D(0)))
    n = len(d["lineas"])
    salida = dict(d, valor=valor, lineas=[dict(l, valor=rep[i]) for i, l in enumerate(d["lineas"])], anticipoUsado=usado,
                  anticipo=rep[n], saldoAFavor=rep[n + 1], excedente=rep[n + 2])
    if d.get("planNuevo") is not None:
        salida["planNuevo"] = plan_en_pesos(d["planNuevo"], u, modo)
    return salida


# --------------------------------------------------------------------------------------------------------------- reconstruccion
def es_uvr(caso, cond):
    return cond.get("unidad") == "Uvr"


def caso_de_vivienda(caso, cond):
    """El caso y las condiciones con las reglas de vivienda: sin mora sobre intereses y con el tope de vivienda como el tope del dia."""
    c = copy.deepcopy(cond)
    c["moraSobreIntereses"] = False
    k = copy.deepcopy(caso)
    series = k.setdefault("series", {})
    assert not [u for u in series.get("usura") or [] if u["clave"] == c["modalidad"]], "estos casos no traen usura de VIVIENDA"
    series["usura"] = [dict(t, clave=c["modalidad"]) for t in series.get("topeDeVivienda") or []]
    return k, c


def reconstruir(caso, cond, ops, D_, R, libro_desde=None):
    """La reconstruccion de calcular.py con las reglas de vivienda y, en UVR, valorada en pesos. Devuelve el «esperado» de la consulta."""
    k, c = caso_de_vivienda(caso, cond)
    ops = [o for o in ops]
    if not es_uvr(caso, cond):
        e = C.reconstruir(k, c, ops, D_, R, libro_desde=libro_desde)
        salida = {x: v for x, v in e.items() if not x.startswith("_")}
        salida["_e"] = e
        return salida

    serie = Serie(caso["series"]["uvr"])
    modo = C.MODOS[c["redondeo"]["modo"]]
    desembolso = C.fecha(c["fechaDesembolso"])
    u0 = serie.requerida(desembolso)
    cu = dict(c, monto=a_uvr(D(c["monto"]), u0, modo),
              redondeo=dict(c["redondeo"], unidad=DIEZMILESIMA, minima=DIEZMILESIMA),
              extraordinarias=[dict(x, valor=a_uvr(D(x["valor"]), u0, modo)) for x in c.get("extraordinarias") or []])

    def pago_en_uvr(snap, valor, v):
        u = serie.requerida(v)
        lista = ordenados(snap["por"])
        vpc_uvr = valor_para_cancelar(lista)
        if vpc_uvr > 0:
            vpc_pesos = valor_para_cancelar(saldos_en_pesos(lista, u, modo))
            if valor >= vpc_pesos:
                return vpc_uvr + a_uvr(valor - vpc_pesos, u, modo)
        return a_uvr(valor, u, modo)

    cu["_pagoEnUvr"] = pago_en_uvr
    ops_u = []
    vigentes = {o["id"] for o in ops if C.instante(o["registradoEn"]) <= R}
    for o in ops:
        o2 = dict(o)
        if o["tipo"] == "Disbursement" and o["id"] in vigentes and C.fecha(o["fechaValor"]) <= D_:
            o2["monto"] = a_uvr(D(o["monto"]), serie.requerida(C.fecha(o["fechaValor"])), modo)
        ops_u.append(o2)
    e = C.reconstruir(k, cu, ops_u, D_, R, libro_desde=libro_desde)
    if e is None:
        return None

    u_d, proyectada = serie.del_dia(D_)
    por_uvr = ordenados(e["_por"])
    por = saldos_en_pesos(por_uvr, u_d, modo)
    saldos = {}
    for _, concepto, v in por:
        saldos[concepto] = saldos.get(concepto, D(0)) + v
    capital_por_cuota = {x[0]: x[2] for x in por if x[1] == "Principal" and x[0] is not None}

    originales = {o["id"]: o for o in ops}
    pagos = {}
    for i, d in e["_pagos"].items():
        o = originales[i]
        recibido = D(o["valor"] if o.get("valor") is not None else o["monto"])
        pagos[i] = distribucion_en_pesos(d, recibido, serie.requerida(d["fechaValor"] if isinstance(d["fechaValor"], dt.date)
                                                                       else C.fecha(d["fechaValor"])), modo)

    salida = {
        "saldos": {x: saldos[x] for x in C.CONCEPTOS if x in saldos},
        "porCuota": [{"cuota": x[0], "concepto": x[1], "valor": x[2]} for x in por],
        "capitalVencido": sum((capital_por_cuota.get(n, D(0)) for n in e["_vencidas"]), D(0)),
        "diasDeMora": e["diasDeMora"], "cuotaMasAntigua": e["cuotaMasAntigua"], "cuotasVencidas": e["cuotasVencidas"],
        "suspendida": e["suspendida"],
        "valorParaCancelar": valor_para_cancelar(por),
        "tasaDiariaCorriente": e["tasaDiariaCorriente"], "tasaDiariaMora": e["tasaDiariaMora"], "usuraTopada": e["usuraTopada"],
        "validaHasta": e["validaHasta"], "transiciones": e["transiciones"],
        "uvrDelDia": u_d, "uvrProyectada": proyectada,
        "saldosEnUvr": dict(e["saldos"]), "valorParaCancelarEnUvr": e["valorParaCancelar"],
    }
    salida["_e"] = e
    salida["_plan"] = plan_en_pesos(e["_plan"], u_d, modo)
    salida["_pagos"] = pagos
    if libro_desde is not None:
        salida["_libro"] = libro_en_pesos(e, pagos, ops, serie, modo, D_, max(libro_desde, e["_origen"]), R)
    return salida


def libro_en_pesos(e, pagos, ops, serie, modo, D_, desde, R):
    """El libro en pesos (ver el encabezado): las filas de calcular.py convertidas y la correccion monetaria de cada dia."""
    filas = []
    for f in e["_libro"]:
        dia = C.fecha(f["dia"])
        if f["clase"] in ("Accrual", "AccrualAdjustment", "SuspenseReclassification"):
            p = a_pesos(f["valor"], serie.del_dia(dia)[0], modo)
            if p:
                filas.append((dia, f["clase"], f["concepto"], f["cuota"], p))
    vigentes = [o for o in ops if C.instante(o["registradoEn"]) <= R]
    reversadas = {o["reversa"] for o in vigentes if o.get("reversa")}
    for o in vigentes:
        v = C.fecha(o["fechaValor"])
        if o.get("reversa") or o["id"] in reversadas or v > D_ or v < desde:
            continue
        if o["tipo"] == "Disbursement":
            filas.append((v, "Disbursement", "Principal", None, D(o["monto"])))
        elif o["tipo"] in ("Payment", "ProvisionalCollection"):
            d = pagos[o["id"]]
            for u in d["anticipoUsado"]:
                if u["valor"]:
                    filas.append((v, "AdvanceApplied", "Advance", u["cuota"], -u["valor"]))
            for l in d["lineas"]:
                if l["valor"] or (l["concepto"] == "Principal" and l["cuota"] is None):
                    filas.append((v, "PaymentAllocation", l["concepto"], l["cuota"], -l["valor"]))
            if d["anticipo"] > 0:
                filas.append((v, "AdvanceReceived", "Advance", d["cuotaDelAnticipo"], d["anticipo"]))
            if d["saldoAFavor"] > 0:
                filas.append((v, "CreditBalanceRecognized", "CreditBalance", None, d["saldoAFavor"]))
        else:
            raise ValueError(f"vivienda.py no convierte operaciones {o['tipo']}")

    # La correccion monetaria: el saldo en UVR de cada dia sale del de D menos las filas posteriores.
    def clave(concepto, cuota):
        return (concepto, None) if concepto == "Principal" else (concepto, cuota)
    final = {}
    for (cuota, concepto), v in e["_por"].items():
        final[clave(concepto, cuota)] = final.get(clave(concepto, cuota), D(0)) + v
    uvr_dia = {}
    for f in e["_libro"]:
        k = (C.fecha(f["dia"]), clave(f["concepto"], f["cuota"]))
        uvr_dia[k] = uvr_dia.get(k, D(0)) + f["valor"]
        final.setdefault(clave(f["concepto"], f["cuota"]), D(0))
    pesos_dia = {}
    for f in filas:
        k = (f[0], clave(f[2], f[3]))
        pesos_dia[k] = pesos_dia.get(k, D(0)) + f[4]
    correcciones = []
    for k in sorted(final, key=lambda k: (C.ORDEN[k[0]], k[1] or 0)):
        saldo, saldos, d = final[k], {}, D_
        while d >= desde - C.UN_DIA:
            saldos[d] = saldo
            saldo -= uvr_dia.get((d, k), D(0))
            d -= C.UN_DIA

        def pesos(dia):
            return D(0) if saldos[dia] == 0 else a_pesos(saldos[dia], serie.del_dia(dia)[0], modo)
        d = desde
        while d <= D_:
            corr = pesos(d) - pesos(d - C.UN_DIA) - pesos_dia.get((d, k), D(0))
            if corr:
                correcciones.append((d, "MonetaryCorrection", k[0], k[1], corr))
            d += C.UN_DIA
    todas = filas + correcciones
    # El orden de LibroTeorico: dia, las que no son causacion antes, concepto, cuota.
    todas.sort(key=lambda f: (f[0], 1 if f[1] == "Accrual" else 0, C.ORDEN[f[2]], f[3] or 0))
    return [{"dia": f[0].isoformat(), "clase": f[1], "concepto": f[2], "cuota": f[3], "valor": f[4]} for f in todas]


# --------------------------------------------------------------------------------------------------------------- esperado
def esperado_de_consulta(caso, cond, ops, con):
    modo_ = con.get("modo", "Reconstruccion")
    D_, R = C.fecha(con["fecha"]), C.instante(con["informacionHasta"])
    if modo_ == "Causacion":
        marca = C.fecha(con["marca"])
        filas = []
        d = marca + C.UN_DIA
        while d <= D_:
            x = reconstruir(caso, cond, ops, d, R, libro_desde=d)
            libro = x["_libro"] if es_uvr(caso, cond) else x["_e"]["_libro"]
            filas += [{"dia": f["dia"], "clase": f["clase"], "concepto": f["concepto"], "cuota": f["cuota"], "valor": f["valor"]}
                      for f in libro if f["dia"] == d.isoformat() and f["clase"] in ("Accrual", "MonetaryCorrection")]
            d += C.UN_DIA
        return {"causacion": filas}
    libro_desde = C.fecha(con["libroDesde"]) if con.get("libroDesde") else None
    x = reconstruir(caso, cond, ops, D_, R, libro_desde=libro_desde)
    salida = {k: v for k, v in x.items() if not k.startswith("_")}
    uvr = es_uvr(caso, cond)
    if libro_desde is not None:
        salida["libro"] = x["_libro"] if uvr else x["_e"]["_libro"]
    if con.get("conPlan"):
        salida["plan"] = C.plan_de_consulta(x["_plan"] if uvr else x["_e"]["_plan"])
    if con.get("conPagos"):
        pagos = x["_pagos"] if uvr else x["_e"]["_pagos"]
        salida["pagos"] = [dict(C.distribucion_json(d), id=i) for i, d in pagos.items()]
    return salida


def esperado_de_operacion(caso, cond, ops, op):
    """La distribucion de un pago con la informacion hasta su registro (simular = aplicar)."""
    v, R = C.fecha(op["fechaValor"]), C.instante(op["registradoEn"])
    x = reconstruir(caso, cond, ops, v, R)
    pagos = x["_pagos"] if es_uvr(caso, cond) else x["_e"]["_pagos"]
    return C.distribucion_json(pagos[op["id"]])


def calcular_caso(caso):
    cond, ops = caso["condiciones"], caso.get("operaciones") or []
    consultas = []
    for con in caso.get("consultas") or []:
        nueva = {k: v for k, v in con.items() if k != "esperado"}
        nueva["esperado"] = esperado_de_consulta(caso, cond, ops, con)
        consultas.append(nueva)
    operaciones = []
    for op in ops:
        nueva = dict(op)
        if "esperado" in op:
            nueva["esperado"] = esperado_de_operacion(caso, cond, ops, op)
        operaciones.append(nueva)
    return consultas, operaciones


def main():
    if "--serie" in sys.argv:
        print(C.a_json(serie_de_prueba(), 2, 2))
        return
    escribir, verificar = "--escribir" in sys.argv, "--verificar" in sys.argv
    archivos = sorted(f for f in os.listdir(AQUI) if f.endswith(".json"))
    malos = 0
    for nombre in archivos:
        ruta = os.path.join(AQUI, nombre)
        caso = C.cargar(ruta)
        if (caso.get("condiciones") or {}).get("reglas") != "Vivienda":
            continue
        consultas, operaciones = calcular_caso(caso)
        if escribir:
            with open(ruta, encoding="utf-8") as f:
                crudo = f.read()
            if any("esperado" in o for o in caso.get("operaciones") or []):
                a, b = C.bloque(crudo, "operaciones")
                crudo = crudo[:a] + C.a_json(operaciones, 2, 2) + crudo[b:]
            if consultas:
                a, b = C.bloque(crudo, "consultas")
                crudo = crudo[:a] + C.a_json(consultas, 2, 2) + crudo[b:]
            with open(ruta, "w", encoding="utf-8", newline="\n") as f:
                f.write(crudo)
            print(f"{nombre}: escrito")
        elif verificar:
            original = C.cargar(ruta)
            ok = True
            for calc, orig in zip(consultas, original.get("consultas") or []):
                ok = ok and C.iguales(json.loads(C.a_json(calc["esperado"]), parse_float=D, parse_int=D), orig.get("esperado"))
            for calc, orig in zip(operaciones, original.get("operaciones") or []):
                if "esperado" in orig:
                    ok = ok and C.iguales(json.loads(C.a_json(calc["esperado"]), parse_float=D, parse_int=D), orig.get("esperado"))
            print(f"{nombre}: {'coincide' if ok else 'DIFIERE'}")
            malos += 0 if ok else 1
        else:
            print(f"== {nombre}")
            print(C.a_json(consultas, 2, 2))
    sys.exit(1 if malos else 0)


if __name__ == "__main__":
    main()
