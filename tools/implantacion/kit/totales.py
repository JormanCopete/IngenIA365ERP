# -*- coding: utf-8 -*-
"""
Totales de control de un archivo y su comparación con las cifras de referencia (spec US5, research D10).

Nombres de los totales:

- ``lineas.leidas``, ``lineas.saltadas``, ``lineas.conError``, ``filas.entregadas``;
- ``cuenta:<columna>=<valor>`` por cada valor de cada columna de ``totales.agrupar`` del perfil;
- ``suma:<columna>`` por cada columna de ``totales.sumar`` (sobre las filas entregadas).

Las cifras de referencia son un CSV ``total,valor``; una cifra sin total calculado, o al revés, se informa también.
"""
import csv
from decimal import Decimal, InvalidOperation

from .conversion import canonico


def calcular(lectura, perfil):
    totales = {
        "lineas.leidas": Decimal(lectura.lineas_leidas),
        "lineas.saltadas": Decimal(lectura.lineas_saltadas),
        "lineas.conError": Decimal(lectura.lineas_con_error),
        "filas.entregadas": Decimal(len(lectura.filas)),
    }
    for columna in perfil.agrupar:
        for fila in lectura.filas:
            nombre = f"cuenta:{columna}={fila.valores.get(columna, '')}"
            totales[nombre] = totales.get(nombre, Decimal(0)) + 1
    for columna in perfil.sumar:
        suma = Decimal(0)
        for fila in lectura.filas:
            texto = fila.valores.get(columna, "")
            if texto:
                suma += Decimal(texto)
        totales[f"suma:{columna}"] = suma
    return totales


def leer_cifras(ruta):
    cifras = {}
    with open(ruta, encoding="utf-8-sig", newline="") as archivo:
        for numero, fila in enumerate(csv.DictReader(archivo), start=2):
            nombre = (fila.get("total") or "").strip()
            if not nombre:
                continue
            texto = (fila.get("valor") or "").strip().replace(",", ".")
            try:
                cifras[nombre] = Decimal(texto)
            except InvalidOperation:
                raise ValueError(f"{ruta}, fila {numero}: «{texto}» no es un número.")
    return cifras


def comparar(totales, cifras):
    """``[(total, calculado, referencia, diferencia)]``; ``None`` donde falta uno de los dos."""
    salida = []
    for nombre in sorted(set(totales) | set(cifras)):
        calculado, referencia = totales.get(nombre), cifras.get(nombre)
        diferencia = calculado - referencia if calculado is not None and referencia is not None else None
        salida.append((nombre, calculado, referencia, diferencia))
    return salida


def texto(cifra):
    return "" if cifra is None else canonico(cifra)
