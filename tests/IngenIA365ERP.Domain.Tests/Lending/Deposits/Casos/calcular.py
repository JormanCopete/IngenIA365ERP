#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Hoja de calculo de los casos dorados de captaciones (feature 014, T004; research R05, R12; FR-026, SC-002).

Calcula A MANO, con decimal de 40 digitos y sin tocar el codigo C# que se prueba, lo que esperan los casos de Casos/*.json, con el
formato de los casos de credito (tests/IngenIA365ERP.Domain.Tests/Lending/Casos/calcular.py): JSON con comentarios, claves en
espanol y enums por nombre. Nunca en Excel: el double y los empates de redondeo dan otras cifras (T26 de la 013).

Lo que calcula hoy (fase fundacional y US4 de captaciones; las historias siguientes amplian esta hoja como lo hizo la 013):
- Dias de a excluida a b incluida: 30/360 (el 31 vale 30 y el fin de febrero vale 30), real/365 y real/360.
- Tasa diaria compuesta (R05): raiz B-esima de (1+EA) a 24 decimales (al par), menos 1, a 18 decimales (al par), como
  EquivalenciaDeTasas.TasaDiaria. Lineal: EA/B a 18 decimales.
- Saldos por concepto a (D, R) (ReconstruccionDeCuenta, R04): suma por signo de las operaciones con fecha valor <= D y registradas
  hasta R; un reverso resta su original.
- Interes del dia por saldo diario (D31): saldo con fecha valor al cierre del dia x tasa diaria del tramo; tramos sobre el saldo
  entero (WholeBalance) o escalonados (Stepped); un dia bajo el minimo para interes causa cero. El acumulado se guarda a 6 decimales
  (MontoCausado) y la liquidacion redondea a la unidad de la version; la diferencia la absorbe la misma operacion.
- GMF (US4, T109; R07, D32): por disposicion, con la politica vigente a su fecha (tarifa, tope en UVT, UVT, politica de exencion y
  hechos gravados y exentos). Exencion mensual por persona: en la cuenta marcada (SingleMarkedAccount) o en todas (PerHolderLimit),
  hasta tope - usado en el mes; valor = base gravada x tarifa al peso (mitad hacia arriba). Un reverso devuelve al tope lo que la
  original uso y espeja su valor. Casos gmf-*.json (bloque "entrada").
- Cargos de cuenta (US4, T115; FR-128): gratuitos del periodo, exoneraciones (la primera que aplica), valor fijo o porcentaje al peso
  acotado por minimo y maximo, cobrado si el saldo lo deja sin bajar del minimo; si no, pendiente o condonado segun la linea. Casos
  cargo-*.json (bloque "entrada").

Uso (desde cualquier carpeta):
    python calcular.py                 # imprime lo esperado de todos los casos
    python calcular.py 01-a-la-vista   # solo los archivos que empiezan asi
    python calcular.py --verificar     # compara con el "esperado" de cada consulta (sale 1 si difiere)
"""
import datetime as dt
import json
import os
import re
import sys
from decimal import Decimal as D, getcontext, ROUND_HALF_EVEN, ROUND_HALF_UP

getcontext().prec = 40

AQUI = os.path.dirname(os.path.abspath(__file__))

# Signo del libro (data-model 0.2): + = la Cartera le debe al titular. Los tipos que restan del capital.
RESTAN = {"Withdrawal", "TransferOut", "WithholdingApplied", "GmfCharged", "ChargeApplied", "PenaltyApplied", "Refund",
          "ToUnclaimed", "GarnishmentConsigned", "InternalOffset"}


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
    return dt.date.fromisoformat(str(s))


def instante(s):
    return dt.datetime.fromisoformat(str(s).replace("Z", "+00:00"))


# ------------------------------------------------------------------------------------------------------- dias y tasas
def es_fin_de_febrero(f):
    return f.month == 2 and (f + dt.timedelta(days=1)).month == 3


def dias(conv, a, b):
    assert b >= a
    if conv == "Thirty360":
        da = 30 if (a.day == 31 or es_fin_de_febrero(a)) else a.day
        db = 30 if (b.day == 31 or es_fin_de_febrero(b)) else b.day
        return 360 * (b.year - a.year) + 30 * (b.month - a.month) + (db - da)
    return (b - a).days


def base(conv):
    return 365 if conv == "Actual365" else 360


def q(x, decimales, modo=ROUND_HALF_EVEN):
    return x.quantize(D(1).scaleb(-decimales), rounding=modo)


def raiz(x, n):
    """Raiz n-esima a 40 digitos, redondeada a 24 decimales al par (lo que entrega AritmeticaDecimal.RaizEntera)."""
    if n == 1:
        return q(x, 24)
    return q((x.ln() / D(n)).exp(), 24)


def tasa_diaria(ea, conv, equivalencia="Compound"):
    b = base(conv)
    if equivalencia == "Linear":
        return q(ea / b, 18)
    return q(raiz(1 + ea, b) - 1, 18)


# ------------------------------------------------------------------------------------------------------ saldos (R04)
def saldo_a(operaciones, d, r):
    """Capital a la fecha d con la informacion registrada hasta r (un reverso anula su original)."""
    vistas = [o for o in operaciones if fecha(o["fechaValor"]) <= d and instante(o["registradoEn"]) <= r]
    reversadas = {o["reversa"] for o in vistas if o.get("reversa")}
    total = D(0)
    for o in vistas:
        if o.get("reversa") or o["id"] in reversadas:
            continue
        monto = D(o["monto"])
        total += -monto if o["tipo"] in RESTAN else monto
    return total


# ---------------------------------------------------------------------------------------------- interes diario (R05)
def tasa_del_saldo(c, saldo):
    """La EA que rige un saldo: la de la version o la del tramo (WholeBalance); escalonado se resuelve en interes_del_dia."""
    if not c.get("tramos"):
        return D(c["tasaEA"])
    for t in c["tramos"]:
        if saldo >= D(t["desde"]) and (t.get("hasta") is None or saldo <= D(t["hasta"])):
            return D(t["ea"])
    return D(0)


def interes_del_dia(c, saldo):
    conv, eq = c.get("convencion", "Actual365"), c.get("equivalencia", "Compound")
    if saldo <= 0 or saldo < D(c.get("saldoMinimoParaInteres", 0)):
        return D(0)
    if c.get("tramos") and c.get("modoDeTramos") == "Stepped":
        total = D(0)
        for t in c["tramos"]:
            desde, hasta = D(t["desde"]), (D(t["hasta"]) if t.get("hasta") is not None else None)
            if saldo <= desde:
                continue
            porcion = (min(saldo, hasta) if hasta is not None else saldo) - desde
            total += porcion * tasa_diaria(D(t["ea"]), conv, eq)
        return total
    return saldo * tasa_diaria(tasa_del_saldo(c, saldo), conv, eq)


def causado(caso, desde, hasta, r):
    """Interes causado dia por dia de desde (excluida) a hasta (incluida), acumulado a 6 decimales (MontoCausado)."""
    c, ops = caso["condiciones"], caso.get("operaciones", [])
    acumulado, d = D(0), desde
    while d < hasta:
        d = d + dt.timedelta(days=1)
        acumulado += interes_del_dia(c, saldo_a(ops, d, r))
    return q(acumulado, 6)


def liquidado(caso, total_causado):
    unidad = D(caso["condiciones"].get("unidadDeRedondeo", 1))
    return q(total_causado / unidad, 0, ROUND_HALF_UP) * unidad


# -------------------------------------------------------------------------------------------------------- consultas
def esperado_de(caso, consulta):
    d, r = fecha(consulta["fecha"]), instante(consulta["informacionHasta"])
    modo = consulta.get("modo", "Saldos")
    if modo == "Saldos":
        return {"principal": saldo_a(caso.get("operaciones", []), d, r)}
    desde = fecha(consulta.get("desde") or caso["condiciones"]["fechaDeApertura"])
    total = causado(caso, desde, d, r)
    if modo == "Causacion":
        return {"interesCausado": total}
    if modo == "Liquidacion":
        liq = liquidado(caso, total)
        return {"interesCausado": total, "interesLiquidado": liq, "diferenciaDeRedondeo": liq - total}
    raise ValueError(f"modo desconocido: {modo}")


# ------------------------------------------------------------------------------------------------------------- GMF
def politica_vigente(politicas, f):
    vigentes = [p for p in politicas if fecha(p["desde"]) <= f]
    return max(vigentes, key=lambda p: fecha(p["desde"])) if vigentes else None


def gmf_de(p, mov, usado):
    tope = q(D(p["topeExencionUvt"]) * D(p["uvt"]), 2, ROUND_HALF_UP)
    nada = {"exentoAplicado": D(0), "baseGravada": D(0), "valor": D(0), "exentoUsadoDespues": usado}
    if D(p["tarifa"]) <= 0 or p["politica"] == "NotApplicable":
        return nada
    hecho = mov["hecho"]
    if hecho not in p["gravados"] or hecho in p.get("exentos", []) or not mov.get("cuentaSujeta", True):
        return nada
    con_exencion = p["politica"] == "PerHolderLimit" or (p["politica"] == "SingleMarkedAccount" and mov.get("esLaCuentaMarcada", False))
    disponible = max(D(0), tope - usado) if con_exencion else D(0)
    monto = D(mov["monto"])
    exento = min(monto, disponible)
    gravada = monto - exento
    valor = q(gravada * D(p["tarifa"]), 0, ROUND_HALF_UP)
    return {"exentoAplicado": exento, "baseGravada": gravada, "valor": valor, "exentoUsadoDespues": usado + exento}


def esperado_gmf(caso):
    entrada = caso["entrada"]
    usado_por_mes, salida, por_id = {}, [], {}
    for mov in entrada["movimientos"]:
        f = fecha(mov["fecha"])
        mes = (f.year, f.month)
        usado = usado_por_mes.get(mes, D(0))
        if mov.get("reversa"):
            original = por_id[mov["reversa"]]
            usado = max(D(0), usado - original["exentoAplicado"])
            r = {"exentoAplicado": -original["exentoAplicado"], "baseGravada": -original["baseGravada"], "valor": -original["valor"],
                 "exentoUsadoDespues": usado}
        else:
            r = gmf_de(politica_vigente(entrada["politicas"], f), mov, usado)
            usado = r["exentoUsadoDespues"]
        usado_por_mes[mes] = usado
        por_id[mov["id"]] = r
        salida.append(dict({"id": mov["id"]}, **r))
    return {"movimientos": salida, "totalGmf": sum((m["valor"] for m in salida), D(0))}


# ------------------------------------------------------------------------------------------------------------ cargos
def exoneracion(cargo, c):
    if c.get("exoneracionIndividual"): return "IndividualExemption"
    if cargo.get("exentoSiSaldoPromedio") is not None and D(c.get("saldoPromedio", 0)) >= D(cargo["exentoSiSaldoPromedio"]): return "AverageBalanceAtLeast"
    if cargo.get("exentoSiSaldoMinimo") is not None and D(c.get("saldoMinimo", 0)) >= D(cargo["exentoSiSaldoMinimo"]): return "MinimumBalanceAtLeast"
    if cargo.get("exentoSiAntiguedadMeses") is not None and c.get("mesesDeAntiguedad") is not None             and D(c["mesesDeAntiguedad"]) >= D(cargo["exentoSiAntiguedadMeses"]): return "SeniorityAtLeast"
    if cargo.get("exentoEmpleados") and c.get("titularEsEmpleado"): return "Employee"
    if cargo.get("exentoCuentaExentaGmf") and c.get("cuentaExentaDeGmf"): return "GmfExemptAccount"
    if cargo.get("exentoMenores") and c.get("titularMenor"): return "MinorHolder"
    if c.get("oficina") and c["oficina"] in cargo.get("oficinasExentas", []): return "Branch"
    return None


def cargo_de(cargo, c):
    no = lambda motivo: {"resultado": "NoSeCausa", "valor": D(0), "exoneracion": motivo}
    if cargo["disparador"] in ("WithdrawalOverFree", "DepositOverFree") and cargo.get("gratuitos") is not None             and D(c["operacionesEnElPeriodo"]) <= D(cargo["gratuitos"]):
        return no("FreeOperation")
    exo = exoneracion(cargo, c)
    if exo:
        return no(exo)
    if cargo["modo"] == "FixedAmount":
        valor = D(cargo["valorFijo"])
    else:
        base_ = D(c.get("saldoPromedio", 0)) if cargo.get("baseDelPorcentaje") == "AverageBalance" else D(c.get("montoDeLaOperacion", 0))
        valor = q(max(D(0), base_) * D(cargo["porcentaje"]), 0, ROUND_HALF_UP)
    if cargo.get("minimo") is not None and valor < D(cargo["minimo"]): valor = D(cargo["minimo"])
    if cargo.get("maximo") is not None and valor > D(cargo["maximo"]): valor = D(cargo["maximo"])
    if valor <= 0:
        return no(None)
    if D(c["cobrableSinBajarDelMinimo"]) >= valor:
        return {"resultado": "Cobrado", "valor": valor, "exoneracion": None}
    if cargo.get("politicaSiNoAlcanza", "KeepPending") == "Waive":
        return no("ShortfallWaived")
    return {"resultado": "Pendiente", "valor": valor, "exoneracion": None}


def esperado_cargo(caso):
    entrada = caso["entrada"]
    return {"casos": [cargo_de(entrada["cargo"], c) for c in entrada["casos"]]}


def esperado_de_entrada(caso):
    nombre = caso["nombre"]
    if nombre.startswith("gmf-"): return esperado_gmf(caso)
    if nombre.startswith("cargo-"): return esperado_cargo(caso)
    raise ValueError(f"no se sabe calcular {nombre}")


def iguales(a, b):
    if isinstance(a, dict) and isinstance(b, dict):
        return a.keys() == b.keys() and all(iguales(a[k], b[k]) for k in a)
    if isinstance(a, list) and isinstance(b, list):
        return len(a) == len(b) and all(iguales(x, y) for x, y in zip(a, b))
    if a is None or b is None:
        return a is None and b is None
    try:
        return D(str(a)) == D(str(b))
    except Exception:
        return a == b


def main(argv):
    verificar = "--verificar" in argv
    prefijos = [a for a in argv if not a.startswith("--")]
    archivos = sorted(f for f in os.listdir(AQUI) if f.endswith(".json") and (not prefijos or any(f.startswith(p) for p in prefijos)))
    difiere = False
    for nombre in archivos:
        caso = cargar(os.path.join(AQUI, nombre))
        if caso.get("entrada") is not None:
            calculado = esperado_de_entrada(caso)
            if verificar:
                if not iguales(calculado, caso.get("esperado")):
                    difiere = True
                    print(f"DIFIERE {nombre}: calculado {calculado} / esperado {caso.get('esperado')}")
            else:
                print(f"{nombre}: {json.dumps(calculado, default=str, ensure_ascii=False)}")
        for i, consulta in enumerate(caso.get("consultas", [])):
            calculado = esperado_de(caso, consulta)
            if verificar:
                if not iguales(calculado, consulta.get("esperado")):
                    difiere = True
                    print(f"DIFIERE {nombre} consulta {i}: calculado {calculado} / esperado {consulta.get('esperado')}")
            else:
                print(f"{nombre} consulta {i} ({consulta.get('modo', 'Saldos')} a {consulta['fecha']}): {calculado}")
    if verificar and not difiere:
        print(f"OK: {len(archivos)} caso(s) coinciden.")
    return 1 if difiere else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
