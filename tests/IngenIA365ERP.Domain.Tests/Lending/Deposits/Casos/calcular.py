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
- Tasa diaria lineal: NA_m / B con NA_m = m x ((1+EA)^(1/m) - 1) y m los periodos del anio de la liquidacion (EquivalenciaDeTasas).
- GMF (US4, T109; R07, D32): por disposicion, con la politica vigente a su fecha (tarifa, tope en UVT, UVT, politica de exencion y
  hechos gravados y exentos). Exencion mensual por persona: en la cuenta marcada (SingleMarkedAccount) o en todas (PerHolderLimit),
  hasta tope - usado en el mes; valor = base gravada x tarifa al peso (mitad hacia arriba). Un reverso devuelve al tope lo que la
  original uso y espeja su valor. Casos gmf-*.json (bloque "entrada").
- Interes de captacion (US5, T127, T129; R05): saldo diario, promedio y minimo, tramos, cambio de tasa (bloque "cambiosDeTasa" de las
  condiciones), minimo para interes y la convencion; ver la seccion "interes diario". Casos interes-*.json (consultas).
- Retencion en la fuente sobre rendimientos (US5, T128; R06): perfil, umbral diario, componente inflacionario y tarifa. Casos
  retencion-*.json (bloque "entrada").
- CDAT (US6, T166, T167; FR-023): plan vencido o anticipado, causacion cuadrada al plan, gracia en dias habiles, prorroga con o sin
  capitalizacion, redencion con penalidad y plazo de emision. Casos cdat-*.json (bloque "entrada").
- Tasa negociada (US6, T168; FR-127): la regla mas especifica y el margen. Casos tasa-negociada-*.json (bloque "entrada").
- Ahorro contractual (US7, T181; FR-024, FR-074): plan de cuotas por periodicidad desde el inicio, interes de los aportes
  A x ((1 + r_d)^N - 1) al corte (saldo diario con equivalencia compuesta: capitalizar no cambia el valor final), bonificacion (bonificada - base),
  reliquidacion a la tasa de castigo (base - castigo), cumplimiento con tolerancia y dias de gracia, plazo de emision. Casos contractual-*.json
  (bloque "entrada").
- Limite inembargable (US11, T188; FR-055, D20; CGP art. 594 num. 2, ET art. 837-1): las cuentas embargables en el orden (clases del parametro, luego
  antiguedad, luego id); lo protegido sobre la suma de las protegidas (asignado de la ultima a la primera en el orden) o sobre la mas antigua; nada
  protegido por alimentos o sin limite; lo que retienen otros embargos no se repite; se retiene en el orden hasta el tope. Casos embargo-*.json
  (bloque "entrada").
- Asamblea (US12, T201; FR-045 a FR-047, D13): base por saldo promedio diario del ejercicio o al cierre, tasa decidida o valor sobre la base,
  limite legal (IPC) en la revalorizacion, redondeo a la unidad de la entidad, reparto del valor y prorrateo al fondo por el mayor residuo, tope de lo
  amortizable. Casos asamblea-*.json (bloque "entrada").
- Fondo de liquidez (US15, T214; FR-025, FR-109; Decreto 1068 de 2015 con el Decreto 976 de 2026): regimen general (porcentaje de los depositos y del
  permanente solo retirable al desvincularse) o por madurez residual (categoria plena: capital + intereses causados y por causar, CDAT y contractual por
  banda, vencidos o con redencion anticipada pactada en la primera), transicion (rige el general y el nuevo se informa), pisos legales (rige el mayor) y
  el requerido de cada renglon al centavo (mitad hacia arriba). Riesgo de liquidez: brecha por banda y acumulada, IRL acumulado = (ALN + entradas
  acumuladas) / salidas acumuladas a 4 decimales. Depositos asegurados: por ahorrador hasta el tope. Casos liquidez-*.json (bloque "entrada", "tipo").
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


def tasa_diaria(ea, conv):
    return q(raiz(1 + ea, base(conv)) - 1, 18)


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
# US5 (T127, T129): el interes de la ventana por diferencia de acumulados. Cada dia lleva la tasa que rige (la de la condicion o la del
# ultimo cambio de tasa con desde <= dia); n = dias de la convencion entre el dia anterior y el dia. Metodos:
#   saldo diario:   A(d) = suma_k interes(saldo_k, tasa_k, n_k)  (compuesto (1+r)^n - 1 por porcion de tramo)
#   saldo promedio: A(d) = promedio_d x suma_k r_k(promedio_d) x n_k  (interes simple)
#   saldo minimo:   A(d) = minimo_d x (prod_k (1 + r_k(minimo_d))^n_k - 1)
# con r_k(B) = interes(B, tasa_k, 1) / B. Una base bajo el minimo para interes (o <= 0) causa cero. Lo causado es Seis(A(fin)).
def periodos_por_anio(periodicidad):
    dias_ = {"Weekly": 7, "TenDay": 10, "Biweekly": 15, "Monthly": 30, "Bimonthly": 60, "Quarterly": 90, "SemiAnnual": 180,
             "Annual": 360}.get(periodicidad or "Monthly", 30)
    return 360 // dias_ if 360 % dias_ == 0 else 365 // dias_


def tasa_diaria_c(c, ea):
    conv, eq = c.get("convencion", "Actual365"), c.get("equivalencia", "Compound")
    if eq == "Linear":
        m = periodos_por_anio(c.get("periodicidadDeLiquidacion"))
        return q(D(m) * (raiz(1 + ea, m) - 1) / base(conv), 18)
    return tasa_diaria(ea, conv)


def tasa_del_dia(c, d):
    """La EA y los tramos que rigen el dia d: la condicion base o el ultimo cambio de tasa con desde <= d."""
    vigente = {"tasaEA": c.get("tasaEA"), "tramos": c.get("tramos") or [], "modoDeTramos": c.get("modoDeTramos")}
    for cambio in sorted(c.get("cambiosDeTasa") or [], key=lambda x: fecha(x["desde"])):
        if fecha(cambio["desde"]) <= d:
            vigente = {"tasaEA": cambio.get("tasaEA"),
                       "tramos": cambio.get("tramos") if cambio.get("tramos") is not None else vigente["tramos"],
                       "modoDeTramos": cambio.get("modoDeTramos") or vigente["modoDeTramos"]}
    return vigente


def factor(c, r, n):
    if n <= 0:
        return D(0)
    if c.get("equivalencia", "Compound") == "Linear":
        return r * n
    return (1 + r) ** n - 1


def interes_base(c, t, b, n):
    """El interes de la base b durante n dias con la tasa t (sin el minimo para interes)."""
    if b <= 0 or n <= 0:
        return D(0)
    tramos = sorted(t["tramos"], key=lambda x: D(x["desde"]))
    if not tramos:
        return b * factor(c, tasa_diaria_c(c, D(t["tasaEA"])), n)
    if t.get("modoDeTramos") == "Stepped":
        total, abajo = D(0), None
        for i, tr in enumerate(tramos):
            desde = D(tr["desde"]) if i == 0 else abajo
            if b <= desde:
                break
            hasta = D(tr["hasta"]) if tr.get("hasta") is not None else None
            tope = hasta if hasta is not None and hasta < b else b
            if tope - desde > 0:
                total += (tope - desde) * factor(c, tasa_diaria_c(c, D(tr["ea"])), n)
            if hasta is None:
                break
            abajo = hasta
        return total
    elegido = None
    for tr in tramos:
        if b >= D(tr["desde"]) and (tr.get("hasta") is None or b <= D(tr["hasta"])):
            elegido = tr
    if elegido is None:
        return D(0)
    return b * factor(c, tasa_diaria_c(c, D(elegido["ea"])), n)


def causado_por_dia(caso, desde, hasta, r):
    """[(dia, causado a 6 decimales)] de desde (excluida) a hasta (incluida), por diferencia de acumulados."""
    c, ops = caso["condiciones"], caso.get("operaciones", [])
    metodo = c.get("metodoDeSaldo", "DailyBalance")
    minimo_interes = D(c.get("saldoMinimoParaInteres", 0))
    conv = c.get("convencion", "Actual365")
    exacto, anterior_seis, salida = D(0), D(0), []
    suma, dias_prom, minimo, corridos = D(0), 0, None, []
    anterior, d = desde, desde
    while d < hasta:
        d = d + dt.timedelta(days=1)
        n = dias(conv, anterior, d)
        anterior = d
        saldo = saldo_a(ops, d, r)
        t = tasa_del_dia(c, d)
        corridos.append((t, n))
        if n > 0:
            suma += saldo * n
            dias_prom += n
        minimo = saldo if minimo is None else min(minimo, saldo)
        if metodo == "AverageBalance":
            b = suma / dias_prom if dias_prom else saldo
        elif metodo == "MinimumBalance":
            b = minimo
        else:
            b = saldo
        bajo = b <= 0 or b < minimo_interes
        if metodo == "AverageBalance":
            exacto = D(0) if bajo else b * sum(((interes_base(c, tk, b, 1) / b) * nk for tk, nk in corridos if nk > 0), D(0))
        elif metodo == "MinimumBalance":
            if bajo:
                exacto = D(0)
            elif c.get("equivalencia", "Compound") == "Linear":
                exacto = b * sum(((interes_base(c, tk, b, 1) / b) * nk for tk, nk in corridos if nk > 0), D(0))
            else:
                prod = D(1)
                for tk, nk in corridos:
                    if nk > 0:
                        prod *= (1 + interes_base(c, tk, b, 1) / b) ** nk
                exacto = b * (prod - 1)
        else:
            exacto += D(0) if bajo else interes_base(c, t, b, n)
        seis = q(exacto, 6)
        salida.append((d, seis - anterior_seis))
        anterior_seis = seis
    return salida


def causado(caso, desde, hasta, r):
    """Interes causado de desde (excluida) a hasta (incluida), a 6 decimales (MontoCausado)."""
    return sum((x for _, x in causado_por_dia(caso, desde, hasta, r)), D(0))


def liquidado(caso, total_causado):
    unidad = D(caso["condiciones"].get("unidadDeRedondeo", 1))
    return q(total_causado / unidad, 0, ROUND_HALF_UP) * unidad


# ------------------------------------------------------------------------------------------------- retencion (R06)
# US5 (T128, T129): perfil (no contribuyente o autorretenedor: cero); umbral diario = umbralDiarioUvt x UVT (a 2 decimales) si alcanza a
# la clase (AllDepositClasses: todas; ExceptTermDeposit: menos el CDAT; None: ninguna): si ningun dia lo alcanza, cero; si alguno, la base
# es el interes total. Componente inflacionario (si la politica lo dice y el titular es persona natural no obligada): bruto x fraccion
# a la unidad (mitad hacia arriba); retencion = (bruto - componente) x tarifa a la unidad.
def a_unidad(x, unidad):
    return q(x / unidad, 0, ROUND_HALF_UP) * unidad


def esperado_retencion(caso):
    e = caso["entrada"]
    p, t = e["politica"], e["titular"]
    bruto = D(e["interesBruto"])
    unidad = D(p.get("unidadDeRedondeo", 1))
    alcance = p.get("alcance", "ExceptTermDeposit")
    aplica = alcance == "AllDepositClasses" or (alcance == "ExceptTermDeposit" and e["clase"] != "TermDeposit")
    umbral = q(D(p["umbralDiarioUvt"]) * D(p["uvt"]), 2, ROUND_HALF_UP)
    alcanzan = [x["dia"] for x in e.get("interesesDiarios", []) if aplica and D(x["interes"]) >= umbral]

    def cero(motivo):
        return {"umbralAlcanzado": len(alcanzan) > 0, "componente": D(0), "baseGravable": D(0), "valor": D(0), "motivo": motivo}
    if bruto <= 0:
        return cero("NoInterest")
    if t.get("perfil") == "NonTaxpayer":
        return cero("NonTaxpayer")
    if t.get("perfil") == "SelfWithholder":
        return cero("SelfWithholder")
    if aplica and not alcanzan:
        return cero("BelowThreshold")
    if D(p["tarifa"]) <= 0:
        return cero("ZeroRate")
    componente = D(0)
    if p.get("componenteInflacionarioAplica") and t.get("personaNaturalNoObligada") and p.get("componenteInflacionario") is not None:
        componente = min(bruto, a_unidad(bruto * D(p["componenteInflacionario"]), unidad))
    gravable = bruto - componente
    valor = min(bruto, a_unidad(gravable * D(p["tarifa"]), unidad))
    return {"umbralAlcanzado": len(alcanzan) > 0, "componente": componente, "baseGravable": gravable, "valor": valor, "motivo": None}


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


# ------------------------------------------------------------------------------------------------------------- CDAT (R11)
# US6 (T166, T167; FR-023; investigacion 3.2): el plazo en dias calendario desde el inicio, partido en periodos de los dias de la
# periodicidad (Monthly 30, Quarterly 90...; el ultimo absorbe lo que sobre; SinglePayment = un periodo). Interes de un periodo de n dias:
# C x ((1 + r_d)^n - 1) con r_d = raiz B-esima de (1+EA) - 1 (tasa_diaria); anticipado: C x i_v / (1 + i_v). Redondeo con la unidad y el
# modo de la condicion (AwayFromZero = mitad hacia arriba). Fecha de pago: vencido, el fin del periodo; anticipado, el inicio; si no es
# habil (sabado, domingo o festivo), el siguiente habil. Causacion: Seis(C x ((1 + r_d)^k - 1) x f), el ultimo dia el interes del plan.
# Plazo de emision (CUIF 2110, meses de 30): < 180 UnderSixMonths; <= 360 SixToTwelve; < 540 TwelveToEighteen; si no EighteenOrMore.
PERIODICIDADES = {"SinglePayment": 0, "Weekly": 7, "TenDay": 10, "Biweekly": 15, "Monthly": 30, "Bimonthly": 60, "Quarterly": 90,
                  "SemiAnnual": 180, "Annual": 360}


def redondear(x, unidad, modo):
    from decimal import ROUND_CEILING, ROUND_FLOOR
    m = {"AwayFromZero": ROUND_HALF_UP, "ToEven": ROUND_HALF_EVEN, "Up": ROUND_CEILING, "Down": ROUND_FLOOR}.get(modo, ROUND_HALF_UP)
    return q(x / unidad, 0, m) * unidad


def es_habil(d, festivos):
    return d.weekday() < 5 and d not in festivos


def siguiente_habil(d, festivos):
    while not es_habil(d, festivos):
        d = d + dt.timedelta(days=1)
    return d


def sumar_habiles(d, n, festivos):
    contados = 0
    while contados < n:
        d = d + dt.timedelta(days=1)
        if es_habil(d, festivos):
            contados += 1
    return d


def plazo_de_emision(dias_):
    if dias_ < 180: return "UnderSixMonths"
    if dias_ <= 360: return "SixToTwelve"
    if dias_ < 540: return "TwelveToEighteen"
    return "EighteenOrMore"


def plan_cdat(c, festivos):
    capital, ea, conv = D(c["capital"]), D(c["tasaEA"]), c["convencion"]
    inicio, plazo = fecha(c["inicio"]), int(c["plazoDias"])
    unidad, modo = D(c.get("unidadDeRedondeo", 1)), c.get("modoDeRedondeo", "AwayFromZero")
    anticipado = c.get("modalidad", "Arrears") == "Advance"
    venc = inicio + dt.timedelta(days=plazo)
    r = tasa_diaria(ea, conv)
    paso = plazo if c["periodicidad"] == "SinglePayment" else PERIODICIDADES[c["periodicidad"]]
    periodos, desde, n = [], inicio, 0
    while desde < venc:
        n += 1
        hasta = min(desde + dt.timedelta(days=paso), venc)
        k = dias(conv, desde, hasta)
        iv = (1 + r) ** k - 1
        tasa = iv / (1 + iv) if anticipado else iv
        interes = redondear(capital * tasa, unidad, modo)
        pago = siguiente_habil(desde if anticipado else hasta, festivos)
        periodos.append({"numero": n, "desde": desde, "hasta": hasta, "fechaDePago": pago, "dias": k, "interes": interes, "iv": iv})
        desde = hasta
    return {"r": r, "venc": venc, "periodos": periodos, "capital": capital, "conv": conv, "anticipado": anticipado, "unidad": unidad, "modo": modo}


def acumulado_cdat(plan, d):
    for p in plan["periodos"]:
        if p["desde"] < d <= p["hasta"]:
            if d == p["hasta"]:
                return p["interes"]
            k = dias(plan["conv"], p["desde"], d)
            f = 1 / (1 + p["iv"]) if plan["anticipado"] else D(1)
            a = q(plan["capital"] * ((1 + plan["r"]) ** k - 1) * f, 6)
            return min(a, p["interes"])
    return D(0)


def esperado_cdat(caso):
    e = caso["entrada"]
    festivos = {fecha(x) for x in e.get("festivos", [])}
    c = e["condiciones"]
    plan = plan_cdat(c, festivos)
    salida = {
        "vencimiento": plan["venc"].isoformat(),
        "fechaDePagoAlVencimiento": siguiente_habil(plan["venc"], festivos).isoformat(),
        "plazoDeEmision": plazo_de_emision(int(c["plazoDias"])),
        "periodos": [{"numero": p["numero"], "desde": p["desde"].isoformat(), "hasta": p["hasta"].isoformat(),
                      "fechaDePago": p["fechaDePago"].isoformat(), "dias": p["dias"], "interes": p["interes"]} for p in plan["periodos"]],
        "interesTotal": sum((p["interes"] for p in plan["periodos"]), D(0)),
    }
    if e.get("gracia") is not None:
        g = int(e["gracia"])
        salida["finDeLaGracia"] = (sumar_habiles(plan["venc"], g, festivos) if g > 0 else plan["venc"]).isoformat()
    if e.get("causacionEn"):
        salida["acumulados"] = [{"dia": x, "acumulado": acumulado_cdat(plan, fecha(x))} for x in e["causacionEn"]]
    if e.get("redencion") is not None:
        rd = e["redencion"]
        f = fecha(rd["fecha"])
        # Lo causado y no pagado: el acumulado del periodo en curso (los anteriores ya se pagaron, interesPagado).
        causado = acumulado_cdat(plan, f)
        pagado = D(rd.get("interesPagado", 0))
        corridos = dias(plan["conv"], fecha(c["inicio"]), f)
        pen = rd.get("penalidad")
        reliq, ajuste, s_int, s_cap = None, D(0), D(0), D(0)
        if pen is not None and pen["modo"] == "PenaltyRate":
            rp = tasa_diaria(D(pen["tasa"]), plan["conv"])
            reliq = redondear(plan["capital"] * ((1 + rp) ** corridos - 1), plan["unidad"], plan["modo"])
            queda = reliq - pagado
            if queda >= causado:
                pass
            elif queda >= 0:
                ajuste = queda - causado
            else:
                ajuste, s_cap = -causado, -queda
        elif pen is not None:
            s_int = redondear(causado * D(pen["fraccion"]), plan["unidad"], plan["modo"])
            if s_int > causado:
                s_int = redondear(causado, plan["unidad"], plan["modo"])
        neto = causado + ajuste - s_int
        a_pagar = D(0) if neto <= 0 else redondear(neto, plan["unidad"], plan["modo"])
        salida["redencion"] = {"diasCorridos": corridos, "interesCausado": causado, "interesReliquidado": reliq, "ajusteDeCausacion": ajuste,
                               "penalidadSobreInteres": s_int, "penalidadSobreCapital": s_cap, "interesAPagar": a_pagar}
    if e.get("prorroga") is not None:
        pr = e["prorroga"]
        bruto = salida["interesTotal"]
        retencion = a_unidad(bruto * D(pr.get("tarifaDeRetencion", 0)), D(1))
        capitalizado = bruto - retencion if pr.get("capitaliza") else D(0)
        nuevas = dict(c)
        nuevas["capital"] = D(c["capital"]) + capitalizado
        nuevas["inicio"] = plan["venc"].isoformat()
        nuevas["tasaEA"] = D(pr.get("tasaEA", c["tasaEA"]))
        nuevo = plan_cdat(nuevas, festivos)
        salida["prorroga"] = {"retencion": retencion, "capitalizado": capitalizado, "nuevoCapital": nuevas["capital"],
                              "nuevoVencimiento": nuevo["venc"].isoformat(),
                              "nuevoInteresTotal": sum((p["interes"] for p in nuevo["periodos"]), D(0))}
    return salida


# --------------------------------------------------------------------------------------------- tasa negociada (R13)
# US6 (T168; FR-127): la regla mas especifica (todas sus condiciones no nulas casan; gana la que fija mas; a igual, la de menor prioridad y
# despues el codigo); margen <= 0: MarginInvalid; sin regla: NoApplicableRule; sobre el maximo: MarginAboveRule; si no, cabe en la regla y
# en la atribucion si la atribucion lo alcanza.
CRITERIOS = ["antiguedadMinimaMeses", "montoMinimo", "montoMaximo", "plazoMinimoDias", "plazoMaximoDias", "tasaBaseMinima", "tasaBaseMaxima",
             "saldoDelTitularMinimo", "saldoDelTitularMaximo", "oficina", "canal"]


def casa(r, p):
    def g(k):
        return r.get(k)
    if g("antiguedadMinimaMeses") is not None and D(p["antiguedadMeses"]) < D(g("antiguedadMinimaMeses")): return False
    if g("montoMinimo") is not None and D(p["monto"]) < D(g("montoMinimo")): return False
    if g("montoMaximo") is not None and D(p["monto"]) > D(g("montoMaximo")): return False
    if g("plazoMinimoDias") is not None and (p.get("plazoDias") is None or D(p["plazoDias"]) < D(g("plazoMinimoDias"))): return False
    if g("plazoMaximoDias") is not None and (p.get("plazoDias") is None or D(p["plazoDias"]) > D(g("plazoMaximoDias"))): return False
    if g("tasaBaseMinima") is not None and D(p["tasaDeTabla"]) < D(g("tasaBaseMinima")): return False
    if g("tasaBaseMaxima") is not None and D(p["tasaDeTabla"]) > D(g("tasaBaseMaxima")): return False
    if g("saldoDelTitularMinimo") is not None and D(p["saldoDelTitular"]) < D(g("saldoDelTitularMinimo")): return False
    if g("saldoDelTitularMaximo") is not None and D(p["saldoDelTitular"]) > D(g("saldoDelTitularMaximo")): return False
    if g("oficina") is not None and D(p["oficina"]) != D(g("oficina")): return False
    if g("canal") and str(g("canal")).lower() != str(p.get("canal") or "").lower(): return False
    return True


def esperado_tasa_negociada(caso):
    e = caso["entrada"]
    salida = []
    for p in e["pedidos"]:
        margen = D(p["margenPedido"])
        if margen <= 0:
            salida.append({"regla": None, "codigo": "Lending.NegotiatedRate.MarginInvalid", "seAplicaSola": False, "esperaAprobador": False})
            continue
        casan = [r for r in e["reglas"] if casa(r, p)]
        casan.sort(key=lambda r: (-sum(1 for k in CRITERIOS if r.get(k) not in (None, "")), int(r["prioridad"]), r["codigo"]))
        if not casan:
            salida.append({"regla": None, "codigo": "Lending.NegotiatedRate.NoApplicableRule", "seAplicaSola": False, "esperaAprobador": False})
            continue
        r = casan[0]
        if margen > D(r["margenMaximo"]):
            salida.append({"regla": r["codigo"], "codigo": "Lending.NegotiatedRate.MarginAboveRule", "seAplicaSola": False, "esperaAprobador": False})
            continue
        atr = p.get("atribucion")
        cabe = atr is not None and margen <= D(atr)
        salida.append({"regla": r["codigo"], "codigo": None, "seAplicaSola": cabe, "esperaAprobador": not cabe})
    return {"evaluaciones": salida}


# ------------------------------------------------------------------------------------------- ahorro contractual (US7)
# T181 (FR-024, FR-074; investigacion 3.4): las cuotas vencen cada periodo desde el inicio (meses calendario en las de meses enteros, dias en las
# menores) hasta el vencimiento = inicio + plazo en meses. El interes de un aporte A del dia f al corte es A x ((1 + r_d)^N - 1), N dias de la
# convencion; cada interes se redondea con la unidad de la linea y la diferencia nunca es negativa. Una cuota esta atrasada si vencio hace mas de
# los dias de gracia y le queda saldo; mas atrasadas que la tolerancia, incumplido.
def sumar_meses(f, n):
    y, m = f.year + (f.month - 1 + n) // 12, (f.month - 1 + n) % 12 + 1
    import calendar
    return dt.date(y, m, min(f.day, calendar.monthrange(y, m)[1]))


def cuotas_contractual(inicio, periodicidad, venc):
    dias_ = PERIODICIDADES[periodicidad]
    meses = dias_ // 30 if dias_ >= 30 else 0
    lista, k = [], 1
    while True:
        v = sumar_meses(inicio, k * meses) if meses else inicio + dt.timedelta(days=k * dias_)
        if v > venc:
            break
        lista.append({"numero": k, "vencimiento": v})
        k += 1
    return lista


def interes_aportes(aportes, ea, conv, corte):
    r = tasa_diaria(ea, conv)
    total = D(0)
    for f, m in aportes:
        if m == 0 or f >= corte:
            continue
        total += m * ((1 + r) ** dias(conv, f, corte) - 1)
    return total


def esperado_contractual(caso):
    e = caso["entrada"]
    c = e["condiciones"]
    cuota, conv, inicio = D(c["cuota"]), c["convencion"], fecha(c["inicio"])
    unidad, modo = D(c.get("unidadDeRedondeo", 1)), c.get("modoDeRedondeo", "AwayFromZero")
    venc = sumar_meses(inicio, int(c["plazoMeses"]))
    cuotas = cuotas_contractual(inicio, c["periodicidad"], venc)
    plan_aportes = [(q["vencimiento"], cuota) for q in cuotas]
    base_ = D(c["tasaBase"])
    ib = redondear(interes_aportes(plan_aportes, base_, conv, venc), unidad, modo)
    salida = {
        "vencimiento": venc.isoformat(),
        "plazoDeEmision": plazo_de_emision(int(c["plazoMeses"]) * 30),
        "cuotas": len(cuotas),
        "primeraCuota": cuotas[0]["vencimiento"].isoformat(),
        "ultimaCuota": cuotas[-1]["vencimiento"].isoformat(),
        "totalPactado": cuota * len(cuotas),
        "interesProyectadoBase": ib,
        "valorFinalBase": cuota * len(cuotas) + ib,
    }
    if c.get("tasaBonificada") is not None:
        ibo = redondear(interes_aportes(plan_aportes, D(c["tasaBonificada"]), conv, venc), unidad, modo)
        salida["interesProyectadoBonificado"] = ibo
        salida["bonificacionProyectada"] = max(D(0), ibo - ib)
        salida["valorFinalCumpliendo"] = cuota * len(cuotas) + ibo
    aportes = [(fecha(a["fecha"]), D(a["monto"])) for a in e["aportes"]] if e.get("aportes") is not None else plan_aportes
    corte = fecha(e["corte"]) if e.get("corte") else venc
    total = sum((m for f, m in aportes if f <= corte), D(0))
    def diferencia(otra, bonifica):
        i0 = redondear(interes_aportes(aportes, base_, conv, corte), unidad, modo)
        i1 = redondear(interes_aportes(aportes, D(otra), conv, corte), unidad, modo)
        dif = max(D(0), (i1 - i0) if bonifica else (i0 - i1))
        return {"aportes": total, "interesBase": i0, "interesOtraTasa": i1, "diferencia": dif, "valorFinalOtraTasa": total + i1}
    if e.get("bonificacion") and c.get("tasaBonificada") is not None:
        salida["bonificacion"] = diferencia(c["tasaBonificada"], True)
    if e.get("reliquidacion") and c.get("tasaCastigo") is not None:
        salida["reliquidacion"] = diferencia(c["tasaCastigo"], False)
    if e.get("cumplimiento") is not None:
        cu = e["cumplimiento"]
        dia, tol, gracia = fecha(cu["dia"]), int(cu.get("tolerancia", 0)), int(cu.get("diasDeGracia", 0))
        por_numero = {q["numero"]: q for q in cuotas}
        filas = []
        for x in cu["cuotas"]:
            n = int(x["numero"])
            valor = D(x.get("valor", cuota))
            pend = D(0) if x.get("anulada") else max(D(0), valor - D(x.get("pagado", 0)))
            filas.append((n, por_numero[n]["vencimiento"], pend))
        atras = [n for n, v, p in filas if p > 0 and v + dt.timedelta(days=gracia) < dia]
        pagadas = sum(1 for n, v, p in filas if p == 0)
        salida["cumplimiento"] = {"generadas": len(filas), "pagadas": pagadas, "atrasadas": len(atras), "numerosAtrasados": atras,
                                  "superaLaTolerancia": len(atras) > tol,
                                  "completo": pagadas >= len(cuotas) and all(p == 0 for n, v, p in filas)}
    return salida


# --------------------------------------------------------------------------------------------- limite inembargable (US11)
# T188 (FR-055, D20; research E155): el limite es de la persona. Protegido: con "AllProtectedAccounts", L se reparte entre las cuentas protegidas
# embargables recorriendolas de la ultima a la primera del orden del embargo; con "OldestAccount", solo la protegida mas antigua guarda hasta L.
# Embargable de una cuenta = saldo - protegido - lo que ya retienen otros embargos (nunca negativo). Se retiene en el orden hasta el tope.
def esperado_embargo(caso):
    e = caso["entrada"]
    orden_clases = e.get("ordenDeClases") or []
    def pos(c):
        if c.get("clase") is None:
            return 10**9
        return orden_clases.index(c["clase"]) if c["clase"] in orden_clases else len(orden_clases)
    cuentas = sorted(e["cuentas"], key=lambda c: (pos(c), c["abiertaEl"], c["id"]))
    limite = D(0) if e.get("porAlimentos") or e.get("limite") is None else D(str(e["limite"]))
    protegido = {c["id"]: D(0) for c in cuentas if c["embargable"]}
    saldo = lambda c: max(D(0), D(str(c["saldo"])))
    if limite > 0:
        prot = [c for c in cuentas if c["embargable"] and c["protegida"]]
        if e.get("aplicacion", "AllProtectedAccounts") == "OldestAccount":
            if prot:
                mas = sorted(prot, key=lambda c: (c["abiertaEl"], c["id"]))[0]
                protegido[mas["id"]] = min(limite, saldo(mas))
        else:
            resto = limite
            for c in reversed(prot):
                if resto <= 0:
                    break
                p = min(resto, saldo(c))
                protegido[c["id"]] = p
                resto -= p
    tope = None if e.get("tope") is None else D(str(e["tope"]))
    salida, total_emb, retenido_total = [], D(0), D(0)
    for i, c in enumerate(cuentas, start=1):
        if not c["embargable"]:
            salida.append({"id": c["id"], "orden": i, "protegido": D(0), "embargable": D(0), "retenido": D(0)})
            continue
        emb = max(D(0), saldo(c) - protegido[c["id"]] - max(D(0), D(str(c.get("retenidoPorOtros", 0)))))
        total_emb += emb
        ret = emb if tope is None else min(emb, tope)
        if tope is not None:
            tope -= ret
        retenido_total += ret
        salida.append({"id": c["id"], "orden": i, "protegido": protegido[c["id"]], "embargable": emb, "retenido": ret})
    return {"limiteAplicado": limite, "protegido": sum(protegido.values(), D(0)), "totalEmbargable": total_emb, "retenido": retenido_total,
            "cuentas": salida}


# --------------------------------------------------------------------------------------------- asamblea (US12)
# T201 (FR-045 a FR-047, D13; research §22): base = saldo promedio diario del ejercicio (saldo de cada dia del 1-ene al 31-dic por fecha valor, recorrido
# dia por dia, / dias del anio, a 2 decimales al par) o saldo al 31-dic. Tasa = porcentaje, o valor / base total; en la revalorizacion, nunca mas que el
# limite legal (IPC). Calculado = base x tasa a la unidad (mitad hacia arriba), o el valor (a la unidad, hacia abajo) repartido por la base con el mayor
# residuo si se decidio un valor y el limite no lo corto. Amortizacion: nunca mas que lo amortizable (hacia abajo a la unidad). Si lo calculado supera el
# fondo (hacia abajo a la unidad), el fondo se reparte por lo calculado con el mayor residuo (desempate: el primero en orden de persona y cuenta).
def mayor_resto(total, pesos, unidad):
    suma = sum(pesos, D(0))
    if not pesos or suma <= 0 or total <= 0:
        return [D(0) for _ in pesos]
    exactas = [total * p / suma for p in pesos]
    partes = [(x / unidad).to_integral_value(rounding="ROUND_FLOOR") * unidad for x in exactas]
    faltan = int(((total - sum(partes, D(0))) / unidad).to_integral_value())
    orden = sorted(range(len(pesos)), key=lambda i: (-(exactas[i] - partes[i]), i))
    for i in orden[:max(0, faltan)]:
        partes[i] += unidad
    return partes


def esperado_asamblea(caso):
    e = caso["entrada"]
    anio = int(e["ejercicio"])
    unidad = D(str(e.get("unidad", 1)))
    abajo = lambda x: (x / unidad).to_integral_value(rounding="ROUND_FLOOR") * unidad
    arriba_mitad = lambda x: (x / unidad).quantize(D(1), rounding=ROUND_HALF_UP) * unidad
    inicio, cierre = dt.date(anio, 1, 1), dt.date(anio, 12, 31)
    lineas = []
    for a in sorted(e["asociados"], key=lambda a: (a["persona"], a["cuenta"])):
        movs = [(fecha(m["fecha"]), D(str(m["monto"]))) for m in a.get("movimientos", [])]
        if e["base"] == "YearEndBalance":
            b = sum((m for f, m in movs if f <= cierre), D(0))
        else:
            total, d = D(0), inicio
            while d <= cierre:
                total += sum((m for f, m in movs if f <= d), D(0))
                d += dt.timedelta(days=1)
            b = q(total / D((cierre - inicio).days + 1), 2)
        if b > 0:
            lineas.append({"persona": a["persona"], "cuenta": a["cuenta"], "base": b,
                           "maximo": None if a.get("maximoAmortizable") is None else D(str(a["maximoAmortizable"]))})
    base_total = sum((l["base"] for l in lineas), D(0))
    fondo_decidido = D(str(e["fondo"]))
    porcentaje = None if e.get("porcentaje") is None else D(str(e["porcentaje"]))
    valor = None if e.get("valor") is None else D(str(e["valor"]))
    limite = None if e.get("limiteLegal") is None else D(str(e["limiteLegal"]))
    decidida = porcentaje if porcentaje is not None else valor / base_total
    limitada = e["clase"] == "Revaluation" and limite is not None and decidida > limite
    tasa = limite if limitada else decidida
    if valor is not None and not limitada:
        calc = mayor_resto(abajo(valor), [l["base"] for l in lineas], unidad)
    else:
        calc = [arriba_mitad(l["base"] * tasa) for l in lineas]
    acotada = [False] * len(lineas)
    if e["clase"] == "Amortization":
        for i, l in enumerate(lineas):
            if l["maximo"] is not None and calc[i] > max(D(0), abajo(l["maximo"])):
                calc[i] = max(D(0), abajo(l["maximo"]))
                acotada[i] = True
    total_calc = sum(calc, D(0))
    fondo = abajo(fondo_decidido)
    prorrateada = total_calc > fondo
    aplicado = mayor_resto(fondo, calc, unidad) if prorrateada else calc
    factor = q(fondo / total_calc, 6) if prorrateada else None
    salida = [{"persona": l["persona"], "cuenta": l["cuenta"], "base": l["base"], "calculado": calc[i], "aplicado": aplicado[i],
               "limitadaPorMinimo": acotada[i]} for i, l in enumerate(lineas) if calc[i] != 0 or aplicado[i] != 0 or acotada[i]]
    total_aplicado = sum(aplicado, D(0))
    return {"baseTotal": base_total, "tasaAplicada": q(tasa, 10), "limitadaPorElLimiteLegal": limitada, "totalCalculado": total_calc,
            "prorrateada": prorrateada, "factor": factor, "totalAplicado": total_aplicado, "remanente": fondo_decidido - total_aplicado,
            "lineas": salida}


# ------------------------------------------------------------------------------------------------ fondo de liquidez (US15)

def _c(x):
    return q(D(str(x)), 2, ROUND_HALF_UP)


def _regla(r):
    regimen = r.get("regimen", "General")
    plena = regimen == "PorMadurez"
    p = D(str(r["porcentajeDepositos"]))
    perm = D(str(r["porcentajePermanenteSoloAlRetirarse"])) if r.get("porcentajePermanenteSoloAlRetirarse") is not None else p
    bandas = sorted([(b.get("hastaDias"), D(str(b["porcentaje"]))) for b in r.get("bandas", [])],
                    key=lambda b: b[0] if b[0] is not None else 10 ** 9)
    if not bandas or bandas[-1][0] is not None:
        bandas.append((None, D(0)))
    return {"regimen": regimen, "plena": plena, "p": p, "perm": perm, "bandas": bandas,
            "causados": r.get("incluyeInteresesCausados", plena), "porCausar": r.get("incluyeInteresesPorCausar", plena),
            "anticipada": r.get("redencionAnticipadaEnLaPrimeraBanda", True), "fin": r.get("finDeLaTransicion")}


def _mayor(x, piso):
    return x if piso is None else max(x, D(str(piso)))


def _nombre_banda(bandas, i):
    desde = 0 if i == 0 else (bandas[i - 1][0] or 0) + 1
    return f"{desde} a {bandas[i][0]} días" if bandas[i][0] is not None else f"más de {desde - 1} días"


def _indice(bandas, dias):
    for i, (hasta, _) in enumerate(bandas):
        if hasta is None or dias <= hasta:
            return i
    return len(bandas) - 1


CLASES = ["ALaVista", "Cdat", "Contractual", "PermanenteConRetiroParcial", "PermanenteSoloAlRetirarse"]


def _regimen(r, pisos, depositos):
    plena = r["plena"]
    pdep = _mayor(r["p"], pisos.get("depositos"))
    if plena and pisos.get("permanenteSoloAlRetirarsePlena") is not None:
        piso_perm = pisos.get("permanenteSoloAlRetirarsePlena")
    else:
        piso_perm = pisos.get("permanenteSoloAlRetirarse")
    pperm = _mayor(r["perm"], piso_perm)
    bandas = r["bandas"] if plena else []
    grupos = {}
    for d in depositos:
        cap, cau = D(str(d["capital"])), D(str(d.get("interesCausado", 0)))
        if cap == 0 and cau == 0:
            continue
        if plena and d["clase"] in ("Cdat", "Contractual"):
            dias = 0 if d.get("redencionAnticipadaPactada") and r["anticipada"] else (d.get("diasAlVencimiento") or 0)
            b = _indice(bandas, dias)
        else:
            b = -1
        grupos.setdefault((CLASES.index(d["clase"]), b), []).append(d)
    renglones = []
    for (ci, b) in sorted(grupos):
        lista = grupos[(ci, b)]
        capital = sum((D(str(d["capital"])) for d in lista), D(0))
        intereses = D(0)
        if r["causados"]:
            intereses += sum((D(str(d.get("interesCausado", 0))) for d in lista), D(0))
        if plena and r["porCausar"]:
            intereses += sum((D(str(d.get("interesPorCausar", 0))) for d in lista), D(0))
        if b >= 0:
            propio = bandas[b][1]
            if b == 0:
                pct = max(propio, pdep)
            else:
                pct = _mayor(propio, pisos.get("bandaMediaPlena")) if propio > 0 else propio
        else:
            pct = pperm if CLASES[ci] == "PermanenteSoloAlRetirarse" else pdep
        base_ = capital + intereses
        renglones.append({"clase": CLASES[ci], "banda": _nombre_banda(bandas, b) if b >= 0 else None, "cuentas": len(lista),
                          "capital": capital, "intereses": intereses, "base": base_, "porcentaje": pct,
                          "requerido": _c(max(D(0), base_) * pct)})
    return {"regimen": r["regimen"], "requerido": sum((x["requerido"] for x in renglones), D(0)), "renglones": renglones}


def esperado_fondo(e):
    regla, general, pisos = _regla(e["regla"]), _regla(e["general"]), e.get("pisos", {})
    fecha_ = fecha(e["fecha"])
    if regla["plena"] and regla["fin"] is not None and fecha_ < fecha(regla["fin"]):
        g = dict(general, regimen="General", plena=False)
        vigente = _regimen(g, pisos, e["depositos"])
        nuevo = _regimen(regla, pisos, e["depositos"])
        vigente["nuevo"] = {"regimen": nuevo["regimen"], "requerido": nuevo["requerido"]}
        vigente["finDeLaTransicion"] = regla["fin"]
        return vigente
    r = _regimen(regla, pisos, e["depositos"])
    r["nuevo"] = None
    r["finDeLaTransicion"] = None
    return r


def esperado_riesgo(e):
    topes = sorted({b for b in e["bandas"] if b is not None})
    if any(b is None for b in e["bandas"]):
        topes.append(None)
    aln = sum((_c(D(str(a["valor"])) * (1 - D(str(a["descuento"])))) for a in e["activos"]), D(0))
    acc = [[D(0), D(0), D(0)] for _ in topes]
    fuera = D(0)
    for f in e["flujos"]:
        m = D(str(f["monto"]))
        i = next((k for k, h in enumerate(topes) if h is None or f["dias"] <= h), -1)
        if i < 0:
            fuera += m
            continue
        acc[i][["Entrada", "SalidaContractual", "SalidaNoContractual"].index(f["tipo"])] += m
    bandas, acumulada, ent, sal = [], D(0), D(0), D(0)
    for i, h in enumerate(topes):
        en, c, n = (_c(x) for x in acc[i])
        brecha = en - c - n
        acumulada += brecha
        ent += en
        sal += c + n
        irl = q((aln + ent) / sal, 4, ROUND_HALF_UP) if sal > 0 else None
        desde = 1 if i == 0 else (topes[i - 1] or 0) + 1
        nombre = f"{desde} a {h} días" if h is not None else f"más de {desde - 1} días"
        bandas.append({"banda": nombre, "entradas": en, "salidasContractuales": c, "salidasNoContractuales": n, "brecha": brecha,
                       "brechaAcumulada": acumulada, "irlAcumulado": irl})
    con = [b for b in bandas if b["irlAcumulado"] is not None]
    minimo = min(con, key=lambda b: b["irlAcumulado"]) if con else None
    return {"activosLiquidosNetos": aln, "bandas": bandas, "irlMinimo": minimo["irlAcumulado"] if minimo else None,
            "bandaDelIrlMinimo": minimo["banda"] if minimo else None, "fueraDelHorizonte": _c(fuera)}


def esperado_asegurados(e):
    tope = D(str(e["tope"]))
    salida = []
    for a in e["ahorradores"]:
        dep = D(str(a["depositos"]))
        if dep <= 0:
            continue
        aseg = min(dep, tope)
        salida.append({"persona": a["persona"], "cuentas": a["cuentas"], "depositos": dep, "asegurado": aseg, "noAsegurado": dep - aseg})
    return {"ahorradores": salida, "totalAsegurado": sum((x["asegurado"] for x in salida), D(0)),
            "totalNoAsegurado": sum((x["noAsegurado"] for x in salida), D(0))}


def esperado_liquidez(caso):
    e = caso["entrada"]
    tipo = e.get("tipo", "fondo")
    if tipo == "fondo":
        return esperado_fondo(e)
    if tipo == "riesgo":
        return esperado_riesgo(e)
    if tipo == "asegurados":
        return esperado_asegurados(e)
    raise ValueError(f"tipo de liquidez desconocido: {tipo}")

def esperado_de_entrada(caso):
    nombre = caso["nombre"]
    if nombre.startswith("asamblea-"): return esperado_asamblea(caso)
    if nombre.startswith("liquidez-"): return esperado_liquidez(caso)
    if nombre.startswith("gmf-"): return esperado_gmf(caso)
    if nombre.startswith("cargo-"): return esperado_cargo(caso)
    if nombre.startswith("retencion-"): return esperado_retencion(caso)
    if nombre.startswith("cdat-"): return esperado_cdat(caso)
    if nombre.startswith("tasa-negociada-"): return esperado_tasa_negociada(caso)
    if nombre.startswith("contractual-"): return esperado_contractual(caso)
    if nombre.startswith("embargo-"): return esperado_embargo(caso)
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
