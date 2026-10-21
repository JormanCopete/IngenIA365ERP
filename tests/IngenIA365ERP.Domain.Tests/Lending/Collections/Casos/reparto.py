#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Hoja de calculo de los casos dorados del reparto de gestores (feature 013, K4, T0920; FR-118, T77; data-model 26.6).

Calcula A MANO, con Decimal y sin tocar el codigo C# que se prueba (RepartoDeGestores), lo que esperan los casos
reparto-*.json: a que gestor va cada credito (o cada deudor completo) y que creditos quedan sin regla.

Reglas, escritas desde la especificacion y no desde el motor:
- Un credito casa con una regla si cada criterio no nulo coincide (oficina, etapa, linea, pagaduria) y su saldo esta
  entre el minimo y el maximo, ambos incluidos.
- Cada credito va a la primera regla que casa, en orden de prioridad ascendente y, si empatan, del PublicId de la
  regla como texto.
- En una regla con "deudorCompleto", la unidad es el deudor con sus creditos que casaron con esa regla; si no, el
  credito. Tamano de la unidad: cantidad de creditos (ByCount) o suma de saldos (ByBalance).
- Primero, las unidades cuyo gestor vigente (el mismo en todos sus creditos) esta en la regla lo conservan y suman su
  tamano a la carga de ese gestor.
- Despues, el resto en orden del PublicId de la unidad (credito o deudor) como texto: cada una va al gestor con menor
  (carga + tamano) / peso; si empatan, el de menor PublicId como texto.
- Las asignaciones se listan regla por regla (en el orden de las reglas): primero las conservadas, despues las
  repartidas, cada grupo en orden de la unidad. Los sin regla, en orden del PublicId del credito.

Uso:
    python reparto.py              # imprime lo esperado de todos los casos
    python reparto.py --verificar  # compara con el bloque "esperado" (sale 1 si difiere)
    python reparto.py --escribir   # reescribe el bloque "esperado" con lo calculado
"""
import json
import pathlib
import sys
from decimal import Decimal

AQUI = pathlib.Path(__file__).resolve().parent


def dec(x):
    return None if x is None else Decimal(str(x))


def casa(regla, credito):
    for clave in ("oficina", "etapa", "linea", "pagaduria"):
        if regla.get(clave) is not None and credito.get(clave) != regla[clave]:
            return False
    saldo = dec(credito["saldo"])
    if regla.get("saldoMinimo") is not None and saldo < dec(regla["saldoMinimo"]):
        return False
    if regla.get("saldoMaximo") is not None and saldo > dec(regla["saldoMaximo"]):
        return False
    return True


def calcular(caso):
    reglas = sorted(caso["reglas"], key=lambda r: (r["prioridad"], r["publicId"]))
    tomados = {r["publicId"]: [] for r in reglas}
    sin_regla = []
    for c in sorted(caso["creditos"], key=lambda c: c["credito"]):
        destino = next((r for r in reglas if casa(r, c)), None)
        if destino is None:
            sin_regla.append(c["credito"])
        else:
            tomados[destino["publicId"]].append(c)

    asignaciones = []
    for r in reglas:
        creditos = tomados[r["publicId"]]
        if r.get("deudorCompleto"):
            deudores = sorted({c["deudor"] for c in creditos})
            unidades = [(d, None, d, sorted([c for c in creditos if c["deudor"] == d], key=lambda c: c["credito"])) for d in deudores]
        else:
            unidades = [(c["credito"], c["credito"], c["deudor"], [c]) for c in sorted(creditos, key=lambda c: c["credito"])]

        por_saldo = r["distribucion"] == "ByBalance"
        pesos = {g["usuario"]: dec(g["peso"]) for g in r["gestores"]}
        carga = {u: Decimal(0) for u in pesos}

        def tamano(cs):
            return sum((dec(c["saldo"]) for c in cs), Decimal(0)) if por_saldo else Decimal(len(cs))

        def vigente(cs):
            gestores = {c.get("gestorActual") for c in cs}
            return gestores.pop() if len(gestores) == 1 else None

        conservadas, resto = [], []
        for u in unidades:
            g = vigente(u[3])
            (conservadas if g in carga else resto).append(u)

        for clave, credito, deudor, cs in conservadas:
            g = vigente(cs)
            carga[g] += tamano(cs)
            asignaciones.append(fila(credito, deudor, cs, g, r["publicId"], True))

        for clave, credito, deudor, cs in resto:
            t = tamano(cs)
            g = min(sorted(carga), key=lambda u: (carga[u] + t) / pesos[u])
            carga[g] += t
            asignaciones.append(fila(credito, deudor, cs, g, r["publicId"], False))

    return {"asignaciones": asignaciones, "sinRegla": sin_regla}


def fila(credito, deudor, cs, gestor, regla, se_mantiene):
    return {"credito": credito, "deudor": deudor, "creditos": [c["credito"] for c in cs], "gestor": gestor,
            "regla": regla, "seMantiene": se_mantiene}


def main():
    modo = sys.argv[1] if len(sys.argv) > 1 else ""
    distinto = False
    for ruta in sorted(AQUI.glob("reparto-*.json")):
        caso = json.loads(ruta.read_text(encoding="utf-8"))
        esperado = calcular(caso)
        if modo == "--verificar":
            if caso.get("esperado") != esperado:
                distinto = True
                print(f"DIFIERE {ruta.name}")
        elif modo == "--escribir":
            caso["esperado"] = esperado
            ruta.write_text(json.dumps(caso, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            print(f"escrito {ruta.name}")
        else:
            print(ruta.name)
            print(json.dumps(esperado, ensure_ascii=False, indent=2))
    sys.exit(1 if distinto else 0)


if __name__ == "__main__":
    main()
