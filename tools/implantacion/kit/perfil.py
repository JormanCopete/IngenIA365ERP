# -*- coding: utf-8 -*-
"""
El perfil de lectura de un archivo (research R1, R4 de la 016): un JSON por archivo y cooperativa, versionado en git.
Dice qué plantilla destino alimenta, cómo es el archivo y cómo cada columna o posición llena una columna de la plantilla.
Ver ``tools/implantacion/README.md`` para el formato completo.
"""
import json
import unicodedata
from dataclasses import dataclass, field
from pathlib import Path

from .errores import PerfilInvalido

CODIFICACIONES = ("utf-8", "windows-1252", "iso-8859-1")
TIPOS_DE_VALOR = ("texto", "fecha", "numero", "sino")
SIGNOS = ("adelante", "atras", "parentesis")
RECORTES = ("ambos", "izquierda", "derecha", "no")
SIN_EQUIVALENCIA = ("error", "igual")


@dataclass
class Columna:
    destino: str
    origen: object = None          # delimitado: nombre del encabezado o posición 1-based
    posicion: int = None           # ancho fijo: 1-based
    largo: int = None
    constante: str = None
    tipo: str = "texto"
    formato: str = None            # fecha: patrón .NET (dd/MM/yyyy)
    vaciosDeFecha: list = None
    separadorDecimal: str = "."
    separadorMiles: str = ""
    decimalesImplicitos: int = 0
    signo: str = "adelante"
    sufijosNegativos: list = field(default_factory=list)
    sufijosPositivos: list = field(default_factory=list)
    valoresSi: list = None
    valoresNo: list = None
    recorte: str = "ambos"
    quitarCerosIzquierda: bool = False
    mayusculas: bool = False
    siVacio: str = None
    equivalencia: str = None
    sinEquivalencia: str = "error"

    def descripcion_origen(self):
        if self.constante is not None:
            return f"constante «{self.constante}»"
        if self.posicion is not None:
            return f"posición {self.posicion}-{self.posicion + self.largo - 1}"
        return f"columna {self.origen}"


@dataclass
class Perfil:
    nombre: str
    version: int
    destino: str
    hoja: str
    tipo_de_archivo: str           # delimitado | anchoFijo
    codificacion: str
    columnas: list
    separador: str = ","
    calificador: str = '"'
    encabezado: bool = False
    largo_de_linea: int = None
    rellenar_lineas_cortas: bool = False
    saltar_primeras: int = 0
    saltar_vacias: bool = True
    patrones: list = field(default_factory=list)
    agrupar: list = field(default_factory=list)
    sumar: list = field(default_factory=list)
    ruta: Path = None

    @property
    def tablas_de_equivalencia(self):
        return sorted({c.equivalencia.upper() for c in self.columnas if c.equivalencia})

    def codigo(self):
        return f"{self.ruta.stem if self.ruta else self.nombre} v{self.version}"


def cargar(ruta):
    ruta = Path(ruta)
    try:
        datos = json.loads(ruta.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as ex:
        raise PerfilInvalido(f"{ruta.name}: no es un JSON válido ({ex}).")
    perfil = desde_dict(datos)
    perfil.ruta = ruta
    return perfil


def desde_dict(datos):
    def exigir(clave, donde=datos, que="el perfil"):
        if clave not in donde or donde[clave] in (None, ""):
            raise PerfilInvalido(f"Falta «{clave}» en {que}.")
        return donde[clave]

    archivo = exigir("archivo")
    tipo = exigir("tipo", archivo, "«archivo»")
    if tipo not in ("delimitado", "anchoFijo"):
        raise PerfilInvalido(f"«archivo.tipo» es «delimitado» o «anchoFijo», no «{tipo}».")
    codificacion = datos.get("codificacion", "utf-8").lower()
    if codificacion not in CODIFICACIONES:
        raise PerfilInvalido(f"La codificación «{codificacion}» no está entre {', '.join(CODIFICACIONES)}.")

    columnas = []
    conocidas = set(Columna.__dataclass_fields__)
    for i, c in enumerate(exigir("columnas"), start=1):
        desconocidas = set(c) - conocidas
        if desconocidas:
            raise PerfilInvalido(f"Columna {i}: no se conoce {', '.join(sorted(desconocidas))}.")
        columnas.append(Columna(**c))

    saltar = datos.get("saltar", {})
    totales = datos.get("totales", {})
    perfil = Perfil(
        nombre=datos.get("nombre", ""),
        version=int(datos.get("version", 1)),
        destino=exigir("destino"),
        hoja=datos.get("hoja", "Datos"),
        tipo_de_archivo=tipo,
        codificacion=codificacion,
        columnas=columnas,
        separador=archivo.get("separador", ","),
        calificador=archivo.get("calificador", '"'),
        encabezado=bool(archivo.get("encabezado", False)),
        largo_de_linea=archivo.get("largoDeLinea"),
        rellenar_lineas_cortas=bool(archivo.get("rellenarLineasCortas", False)),
        saltar_primeras=int(saltar.get("primeras", 0)),
        saltar_vacias=bool(saltar.get("vacias", True)),
        patrones=list(saltar.get("patrones", [])),
        agrupar=list(totales.get("agrupar", [])),
        sumar=list(totales.get("sumar", [])),
    )
    validar(perfil)
    return perfil


def validar(perfil, encabezados_de_plantilla=None, obligatorias=None):
    """Reglas del perfil (US2, escenario 2). Con la plantilla del ERP, además encabezados y obligatorias."""
    vistos = set()
    for c in perfil.columnas:
        if c.destino in vistos:
            raise PerfilInvalido(f"La columna destino «{c.destino}» está dos veces.")
        vistos.add(c.destino)
        fuentes = sum(x is not None for x in (c.origen, c.posicion, c.constante))
        if fuentes != 1:
            raise PerfilInvalido(f"«{c.destino}»: lleva exactamente una fuente (origen, posicion o constante).")
        if perfil.tipo_de_archivo == "anchoFijo" and c.origen is not None:
            raise PerfilInvalido(f"«{c.destino}»: en ancho fijo se usa posicion y largo, no origen.")
        if perfil.tipo_de_archivo == "delimitado" and c.posicion is not None:
            raise PerfilInvalido(f"«{c.destino}»: en delimitado se usa origen, no posicion.")
        if c.posicion is not None and (not isinstance(c.largo, int) or c.largo <= 0 or c.posicion <= 0):
            raise PerfilInvalido(f"«{c.destino}»: posicion y largo son enteros positivos.")
        if c.tipo not in TIPOS_DE_VALOR:
            raise PerfilInvalido(f"«{c.destino}»: el tipo es uno de {', '.join(TIPOS_DE_VALOR)}.")
        if c.tipo == "fecha" and not c.formato:
            raise PerfilInvalido(f"«{c.destino}»: una fecha necesita su formato (dd/MM/yyyy, yyyyMMdd…).")
        if c.signo not in SIGNOS:
            raise PerfilInvalido(f"«{c.destino}»: el signo es uno de {', '.join(SIGNOS)}.")
        if c.recorte not in RECORTES:
            raise PerfilInvalido(f"«{c.destino}»: el recorte es uno de {', '.join(RECORTES)}.")
        if c.sinEquivalencia not in SIN_EQUIVALENCIA:
            raise PerfilInvalido(f"«{c.destino}»: sinEquivalencia es «error» o «igual».")

    fijas = sorted((c.posicion, c.posicion + c.largo - 1, c.destino) for c in perfil.columnas if c.posicion is not None)
    for (_, fin_a, a), (ini_b, _, b) in zip(fijas, fijas[1:]):
        if ini_b <= fin_a:
            raise PerfilInvalido(f"Las posiciones de «{a}» y «{b}» se cruzan.")
    if perfil.largo_de_linea:
        for inicio, fin, destino in fijas:
            if fin > perfil.largo_de_linea:
                raise PerfilInvalido(f"«{destino}» termina en la posición {fin} y la línea mide {perfil.largo_de_linea}.")

    if encabezados_de_plantilla is not None:
        normalizados = {_n(e): e for e in encabezados_de_plantilla}
        for c in perfil.columnas:
            if _n(c.destino) not in normalizados:
                raise PerfilInvalido(f"«{c.destino}» no es una columna de la plantilla {perfil.destino} (hoja {perfil.hoja}).")
        for o in obligatorias or []:
            if _n(o) not in {_n(c.destino) for c in perfil.columnas}:
                raise PerfilInvalido(f"El perfil no llena la columna obligatoria «{o}» de la plantilla (ni con el archivo ni con una constante).")


def _n(texto):
    """Como ``TablaLeida.Normalizar`` del ERP: sin mayúsculas, tildes, espacios ni signos."""
    plano = unicodedata.normalize("NFD", str(texto).lower())
    return "".join(ch for ch in plano if unicodedata.category(ch) != "Mn" and ch.isalnum())


normalizar_encabezado = _n
