#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Hoja de calculo de los casos dorados de las novedades de libranza (feature 013, K2, T0803; T78, FR-111, FR-113).

Calcula A MANO, con decimal y sin tocar el codigo C# que se prueba, el "esperado" de cada caso de esta carpeta: por obligacion una
linea con su novedad frente al archivo base (el ultimo enviado del convenio), en el orden de la prelacion, y los totales por novedad.

Reglas (adaptadores-y-archivos.md 6.3, data-model 24.6 y 24.7):
- total = exigible del periodo + vencido si el convenio lo admite ("incluyeVencido"); si no, el vencido no viaja (0).
- sin archivo base: todas "New".
- en el base (y alli no como "Cancellation") y ahora: mismo total -> "Unchanged"; otro -> "Change"; "anterior" = total del base.
- en el base y ya no entre las obligaciones del convenio (cancelada o trasladada): "Cancellation" con total 0 y "anterior".
- ahora y no en el base, o en el base como "Cancellation": "New".
- orden: fecha de la libranza (prelacion cronologica, FR-111) y, a igual fecha, el credito; las lineas se numeran desde 1.

Uso:
    python novedades.py              # imprime lo esperado
    python novedades.py --verificar  # compara con el "esperado" de cada caso (sale 1 si difiere)
    python novedades.py --escribir   # reescribe el "esperado" de cada caso
"""
import json
import os
import re
import sys
from decimal import Decimal as D

AQUI = os.path.dirname(os.path.abspath(__file__))


def sin_comentarios(texto):
    return re.sub(r"(?m)^\s*//.*$", "", texto)


def cargar(ruta):
    with open(ruta, encoding="utf-8") as f:
        return json.loads(sin_comentarios(f.read()), parse_float=D, parse_int=D)


def esperado(caso):
    incluye = bool(caso.get("incluyeVencido", False))
    base = {int(b["credito"]): b for b in (caso.get("base") or [])}
    actuales = {int(o["credito"]): o for o in caso["obligaciones"]}
    lineas = []
    for credito, o in actuales.items():
        vencido = D(o.get("vencido", 0)) if incluye else D(0)
        total = D(o["exigible"]) + vencido
        b = base.get(credito)
        if b is not None and b["novedad"] != "Cancellation":
            anterior = D(b["total"])
            novedad = "Unchanged" if anterior == total else "Change"
        else:
            anterior, novedad = None, "New"
        lineas.append({"credito": credito, "fecha": o["fechaLibranza"], "novedad": novedad, "corriente": D(o["exigible"]),
                       "vencido": vencido, "total": total, "anterior": anterior})
    for credito, b in base.items():
        if b["novedad"] != "Cancellation" and credito not in actuales:
            lineas.append({"credito": credito, "fecha": b["fechaLibranza"], "novedad": "Cancellation", "corriente": D(0),
                           "vencido": D(0), "total": D(0), "anterior": D(b["total"])})
    lineas.sort(key=lambda l: (l["fecha"], l["credito"]))
    salida = []
    for i, l in enumerate(lineas, start=1):
        salida.append({"linea": i, "credito": l["credito"], "novedad": l["novedad"], "corriente": l["corriente"],
                       "vencido": l["vencido"], "total": l["total"], "anterior": l["anterior"]})

    def contar(k):
        return sum(1 for l in salida if l["novedad"] == k)

    def sumar(k):
        return sum((l["total"] for l in salida if l["novedad"] == k), D(0))

    totales = {"nuevas": contar("New"), "cambios": contar("Change"), "cancelaciones": contar("Cancellation"),
               "sinCambio": contar("Unchanged"), "montoNuevas": sumar("New"), "montoCambios": sumar("Change"),
               "montoSinCambio": sumar("Unchanged"), "lineas": len(salida), "total": sum((l["total"] for l in salida), D(0))}
    return {"lineas": salida, "totales": totales}


class _Decimal(float):
    """Escribe un decimal tal cual (sin pasar por binario)."""

    def __init__(self, d):
        self.d = d

    def __repr__(self):
        return format(self.d, "f")


def a_json(v):
    def conv(x):
        if isinstance(x, dict):
            return {k: conv(y) for k, y in x.items()}
        if isinstance(x, list):
            return [conv(y) for y in x]
        if isinstance(x, D):
            return int(x) if x == x.to_integral_value() else _Decimal(x)
        return x
    return json.dumps(conv(v), ensure_ascii=False)


def iguales(a, b):
    return json.loads(a_json(a), parse_float=D) == json.loads(a_json(b), parse_float=D)


def main():
    escribir, verificar = "--escribir" in sys.argv, "--verificar" in sys.argv
    malos = 0
    for nombre in sorted(f for f in os.listdir(AQUI) if f.endswith(".json")):
        ruta = os.path.join(AQUI, nombre)
        caso = cargar(ruta)
        e = esperado(caso)
        if escribir:
            with open(ruta, encoding="utf-8") as f:
                crudo = f.read()
            i = crudo.rindex('\n  "esperado":')
            lineas = ",\n".join("      " + a_json(l) for l in e["lineas"])
            crudo = crudo[:i] + '\n  "esperado": {\n    "lineas": [\n' + lineas + '\n    ],\n    "totales": ' + a_json(e["totales"]) + "\n  }\n}\n"
            with open(ruta, "w", encoding="utf-8", newline="\n") as f:
                f.write(crudo)
            print(f"{nombre}: escrito")
        elif verificar:
            ok = iguales(e, caso.get("esperado"))
            print(f"{nombre}: {'coincide' if ok else 'DIFIERE'}")
            malos += 0 if ok else 1
        else:
            print(f"== {nombre}")
            print(a_json(e))
    sys.exit(1 if malos else 0)


if __name__ == "__main__":
    main()
