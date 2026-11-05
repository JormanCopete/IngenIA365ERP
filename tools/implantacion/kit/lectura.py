# -*- coding: utf-8 -*-
"""
La lectura de un plano con su perfil (contracts/plantillas.md §1.2 de la 016), en este orden:

1. decodificar con la codificación del perfil (un BOM UTF-8 manda y se avisa);
2. saltar las primeras N líneas, las vacías y las que cumplen un patrón;
3. partir por separador (respetando el calificador) o por posiciones; en ancho fijo, una línea de otro largo es error;
4. por mapeo: tomar, recortar, quitar relleno, vacío → ``siVacio``, convertir y traducir con la equivalencia;
5. entregar la fila con su **número de línea original**.

Una línea con un error no se entrega; sus errores sí. Los avisos no detienen la línea.
"""
import csv
import re
from dataclasses import dataclass, field

from . import conversion, errores
from .errores import ErrorDeLinea, PerfilInvalido
from .perfil import normalizar_encabezado

BOM = b"\xef\xbb\xbf"
_CONTROLES = re.compile(r"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]")
_MOJIBAKE = re.compile(r"[ÃÂ](.)")


def _parece_utf8_leido_como_1252(linea):
    """«Ã±», «Ã‘», «Â°»: una letra UTF-8 de dos bytes leída como windows-1252 deja Ã o Â y un byte de continuación."""
    for m in _MOJIBAKE.finditer(linea):
        try:
            if 0x80 <= m.group(1).encode("cp1252")[0] <= 0xBF:
                return True
        except UnicodeEncodeError:
            continue
    return False


@dataclass
class FilaLeida:
    linea: int
    valores: dict  # columna destino → texto canónico


@dataclass
class Lectura:
    filas: list = field(default_factory=list)
    errores: list = field(default_factory=list)
    lineas_leidas: int = 0
    lineas_saltadas: int = 0
    lineas_con_error: int = 0

    @property
    def solo_errores(self):
        return [e for e in self.errores if not e.aviso]


def decodificar(contenido, codificacion):
    """El texto y los avisos de codificación (US2, escenario 3)."""
    avisos = []
    if contenido.startswith(BOM):
        contenido = contenido[len(BOM):]
        if codificacion != "utf-8":
            avisos.append(ErrorDeLinea(1, "", "archivo", errores.ENCODING_SUSPECT,
                                       f"El archivo trae marca UTF-8 (BOM) y el perfil dice {codificacion}: se leyó como UTF-8.", aviso=True))
        codificacion = "utf-8"
    nombre = {"windows-1252": "cp1252", "iso-8859-1": "latin-1"}.get(codificacion, "utf-8")
    texto = contenido.decode(nombre, errors="replace")
    return texto, avisos


def avisos_de_caracteres(numero, linea):
    """Caracteres que no se reconocieron: reemplazo, controles o UTF-8 leído como windows-1252 («Ã±»)."""
    if "�" in linea or _CONTROLES.search(linea) or _parece_utf8_leido_como_1252(linea):
        return ErrorDeLinea(numero, "", "archivo", errores.ENCODING_SUSPECT,
                            f"Caracteres no reconocidos; así quedaría: «{linea.strip()[:80]}». ¿La codificación del perfil es la del archivo?",
                            aviso=True)
    return None


def _cumple(patron, linea):
    if "empiezaCon" in patron:
        return linea.lstrip().upper().startswith(str(patron["empiezaCon"]).upper())
    if "contiene" in patron:
        return str(patron["contiene"]).upper() in linea.upper()
    if "regex" in patron:
        return re.search(patron["regex"], linea) is not None
    raise PerfilInvalido(f"Patrón de salto desconocido: {patron} (empiezaCon, contiene o regex).")


def leer(contenido, perfil, tablas=None):
    """Lee ``contenido`` (bytes) con ``perfil``. ``tablas``: las equivalencias que cita el perfil, por nombre."""
    tablas = tablas or {}
    lectura = Lectura()
    texto, avisos = decodificar(contenido, perfil.codificacion)
    lectura.errores.extend(avisos)
    lineas = texto.splitlines()

    indices = None
    avisos_de_codificacion = 0
    for numero, linea in enumerate(lineas, start=1):
        if numero <= perfil.saltar_primeras:
            lectura.lineas_saltadas += 1
            continue
        if perfil.saltar_vacias and linea.strip() == "":
            lectura.lineas_saltadas += 1
            continue
        if any(_cumple(p, linea) for p in perfil.patrones):
            lectura.lineas_saltadas += 1
            continue

        if perfil.tipo_de_archivo == "delimitado" and perfil.encabezado and indices is None:
            indices = _indices_por_encabezado(_partir_delimitado(linea, perfil), perfil)
            lectura.lineas_saltadas += 1
            continue

        lectura.lineas_leidas += 1
        aviso = avisos_de_caracteres(numero, linea)
        if aviso and avisos_de_codificacion < 20:
            lectura.errores.append(aviso)
            avisos_de_codificacion += 1

        errores_de_linea = []
        valores = _leer_linea(numero, linea, perfil, indices, tablas, errores_de_linea)
        lectura.errores.extend(errores_de_linea)
        if any(not e.aviso for e in errores_de_linea):
            lectura.lineas_con_error += 1
        else:
            lectura.filas.append(FilaLeida(numero, valores))

    if perfil.tipo_de_archivo == "delimitado" and perfil.encabezado and indices is None:
        raise PerfilInvalido("El perfil dice que el archivo trae encabezado y no se encontró ninguna línea.")
    return lectura


def _partir_delimitado(linea, perfil):
    return next(csv.reader([linea], delimiter=perfil.separador, quotechar=perfil.calificador or None,
                           quoting=csv.QUOTE_MINIMAL if perfil.calificador else csv.QUOTE_NONE))


def _indices_por_encabezado(encabezados, perfil):
    por_nombre = {normalizar_encabezado(e): i for i, e in enumerate(encabezados)}
    indices = {}
    for c in perfil.columnas:
        if c.origen is None or isinstance(c.origen, int):
            continue
        i = por_nombre.get(normalizar_encabezado(c.origen))
        if i is None:
            raise PerfilInvalido(f"«{c.destino}»: el encabezado «{c.origen}» no está en el archivo ({', '.join(encabezados)}).")
        indices[c.destino] = i
    return indices


def _leer_linea(numero, linea, perfil, indices, tablas, errores_de_linea):
    def error(columna, origen, codigo, mensaje):
        errores_de_linea.append(ErrorDeLinea(numero, columna.destino if columna else "", origen, codigo, mensaje))

    campos = None
    if perfil.tipo_de_archivo == "anchoFijo":
        largo = len(linea)
        if perfil.largo_de_linea and largo != perfil.largo_de_linea:
            if largo < perfil.largo_de_linea and perfil.rellenar_lineas_cortas:
                linea = linea.ljust(perfil.largo_de_linea)
            else:
                error(None, "línea", errores.LINE_LENGTH, f"Largo {largo}, se esperaban {perfil.largo_de_linea}.")
                return {}
    else:
        campos = _partir_delimitado(linea, perfil)

    valores = {}
    for c in perfil.columnas:
        origen = c.descripcion_origen()
        if c.constante is not None:
            crudo = str(c.constante)
        elif c.posicion is not None:
            fin = c.posicion + c.largo - 1
            if fin > len(linea):
                error(c, origen, errores.FIELD_OUT_OF_LINE, f"La posición {c.posicion}-{fin} se sale de la línea (mide {len(linea)}).")
                continue
            crudo = linea[c.posicion - 1:fin]
        else:
            i = c.origen - 1 if isinstance(c.origen, int) else (indices or {}).get(c.destino)
            if i is None:
                raise PerfilInvalido(f"«{c.destino}»: con «origen» por nombre el archivo tiene que traer encabezado.")
            if i >= len(campos):
                error(c, origen, errores.MISSING_COLUMN, f"La línea trae {len(campos)} columnas y no tiene la {i + 1}.")
                continue
            crudo = campos[i]

        valor = conversion.recortar(crudo, c.recorte)
        if c.quitarCerosIzquierda:
            valor = valor.lstrip("0") or ("0" if valor else "")
        if c.mayusculas:
            valor = valor.upper()
        if valor.strip() == "" and c.siVacio is not None:
            valor = str(c.siVacio)

        try:
            if c.tipo == "fecha":
                valor = conversion.fecha(valor, c.formato, set(c.vaciosDeFecha) if c.vaciosDeFecha is not None else None)
            elif c.tipo == "numero":
                valor = conversion.numero(valor, c.separadorDecimal, c.separadorMiles, c.decimalesImplicitos, c.signo,
                                          c.sufijosNegativos, c.sufijosPositivos)
            elif c.tipo == "sino":
                valor = conversion.si_no(valor, _mayus(c.valoresSi), _mayus(c.valoresNo))
        except conversion.ValorInvalido as ex:
            codigo = {"fecha": errores.INVALID_DATE, "numero": errores.INVALID_NUMBER}.get(c.tipo, errores.INVALID_BOOLEAN)
            error(c, origen, codigo, str(ex))
            continue

        if c.equivalencia and valor != "":
            tabla = tablas.get(c.equivalencia.upper())
            if tabla is None:
                raise PerfilInvalido(f"«{c.destino}»: falta la tabla de equivalencias {c.equivalencia}.")
            nuevo, falla = tabla.traducir(valor)
            if falla:
                if falla[0] == errores.EQUIVALENCE_MISSING and c.sinEquivalencia == "igual":
                    nuevo = valor
                else:
                    error(c, origen, falla[0], falla[1])
                    continue
            valor = nuevo
        valores[c.destino] = valor
    return valores


def _mayus(lista):
    return {str(v).strip().upper() for v in lista} if lista else None
