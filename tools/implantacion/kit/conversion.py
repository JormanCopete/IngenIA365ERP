# -*- coding: utf-8 -*-
"""
Conversión de un valor del plano al texto canónico que leen las plantillas del ERP (contracts/plantillas.md §1.3):
fecha ``yyyy-MM-dd``; número con punto decimal, sin miles y con el signo adelante; sí/no como ``si``/``no``; el resto,
el texto recortado. Un valor que no encaja es un error con su motivo, **nunca un cero** (research R6).
"""
import re
from datetime import datetime
from decimal import Decimal, InvalidOperation

# Patrones de fecha al estilo .NET (los mismos que el plan de la 016 describe para el perfil) traducidos a strptime.
_TOKENS = [("yyyy", "%Y"), ("yy", "%y"), ("MM", "%m"), ("dd", "%d"), ("HH", "%H"), ("mm", "%M"), ("ss", "%S")]

VACIOS_DE_FECHA = {"", "0", "00000000", "000000", "00/00/0000", "0000-00-00", "00-00-0000", "0000/00/00"}

SI_POR_DEFECTO = {"S", "SI", "SÍ", "Y", "YES", "1", "X", "V", "VERDADERO", "TRUE"}
NO_POR_DEFECTO = {"N", "NO", "0", "F", "FALSO", "FALSE"}


class ValorInvalido(Exception):
    """El valor no encaja con el formato del mapeo; el mensaje dice por qué."""


def patron_strptime(formato):
    """``dd/MM/yyyy`` → ``%d/%m/%Y``. Lo que no es un token se queda literal."""
    salida, i = [], 0
    while i < len(formato):
        for token, directiva in _TOKENS:
            if formato.startswith(token, i):
                salida.append(directiva)
                i += len(token)
                break
        else:
            caracter = formato[i]
            salida.append("%%" if caracter == "%" else caracter)
            i += 1
    return "".join(salida)


def fecha(valor, formato, vacios=None):
    """La fecha en ``yyyy-MM-dd``; vacía si el valor es uno de los que significan «sin fecha»."""
    texto = valor.strip()
    if texto in (vacios if vacios is not None else VACIOS_DE_FECHA) or set(texto) <= {"0", "/", "-", " "}:
        return ""
    try:
        return datetime.strptime(texto, patron_strptime(formato)).strftime("%Y-%m-%d")
    except ValueError:
        raise ValorInvalido(f"«{texto}» no es una fecha {formato}.")


def numero(valor, separador_decimal=".", separador_miles="", decimales_implicitos=0, signo="adelante",
           sufijos_negativos=(), sufijos_positivos=()):
    """El número canónico (``-1234.5``). Vacío si el valor viene vacío; error si no encaja."""
    texto = valor.strip().replace(" ", "")
    if texto == "":
        return ""
    original = texto
    negativo = False

    for sufijo in sufijos_negativos:
        if texto.upper().endswith(sufijo.upper()):
            texto, negativo = texto[: -len(sufijo)], True
            break
    else:
        for sufijo in sufijos_positivos:
            if texto.upper().endswith(sufijo.upper()):
                texto = texto[: -len(sufijo)]
                break

    if signo == "parentesis" and texto.startswith("(") and texto.endswith(")"):
        texto, negativo = texto[1:-1], not negativo
    elif signo == "atras" and texto[-1:] in "+-":
        negativo, texto = (not negativo) if texto[-1] == "-" else negativo, texto[:-1]
    if texto[:1] in "+-":
        negativo, texto = (not negativo) if texto[0] == "-" else negativo, texto[1:]

    if separador_miles:
        texto = texto.replace(separador_miles, "")
    if separador_decimal != ".":
        if "." in texto:
            raise ValorInvalido(f"«{original}» trae un punto y el decimal del perfil es «{separador_decimal}».")
        texto = texto.replace(separador_decimal, ".")
    if not re.fullmatch(r"\d+(\.\d+)?|\.\d+", texto):
        raise ValorInvalido(f"«{original}» no es un número.")
    if decimales_implicitos and "." in texto:
        raise ValorInvalido(f"«{original}» trae decimales y el perfil dice que vienen implícitos.")
    try:
        cifra = Decimal(texto)
    except InvalidOperation:
        raise ValorInvalido(f"«{original}» no es un número.")
    if decimales_implicitos:
        cifra = cifra.scaleb(-int(decimales_implicitos))
    if negativo and cifra != 0:
        cifra = -cifra
    return canonico(cifra)


def canonico(cifra):
    """``Decimal`` → texto sin exponente ni ceros a la derecha (``100``, ``12.5``, ``-0.03``)."""
    texto = format(cifra.normalize(), "f")
    return "0" if texto in ("-0", "") else texto


def si_no(valor, valores_si=None, valores_no=None):
    texto = valor.strip().upper()
    if texto == "":
        return ""
    if texto in (valores_si or SI_POR_DEFECTO):
        return "si"
    if texto in (valores_no or NO_POR_DEFECTO):
        return "no"
    raise ValorInvalido(f"«{valor.strip()}» no es sí ni no.")


def recortar(valor, modo="ambos"):
    if modo == "no":
        return valor
    if modo == "izquierda":
        return valor.lstrip()
    if modo == "derecha":
        return valor.rstrip()
    return valor.strip()
