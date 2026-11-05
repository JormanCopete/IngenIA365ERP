# -*- coding: utf-8 -*-
"""
Tablas de equivalencias «código anterior → código nuevo» (research R7 de la 016). Cada tabla es un CSV
``codigoAnterior,codigoNuevo,clase,nota`` en UTF-8; el nombre del archivo (sin extensión, en mayúsculas) es el nombre de
la tabla que cita el perfil.

Clases: ``una-a-una`` y ``varias-en-una`` traducen; ``una-en-varias`` obliga a decidir (error de la fila con los destinos
posibles); ``no-se-migra`` es error de la fila (D5), nunca se omite en silencio.
"""
import csv
import unicodedata
from pathlib import Path

from . import errores

UNA_A_UNA = "una-a-una"
VARIAS_EN_UNA = "varias-en-una"
UNA_EN_VARIAS = "una-en-varias"
NO_SE_MIGRA = "no-se-migra"
CLASES = (UNA_A_UNA, VARIAS_EN_UNA, UNA_EN_VARIAS, NO_SE_MIGRA)

ENCABEZADO = ["codigoAnterior", "codigoNuevo", "clase", "nota"]


def normalizar_clase(texto):
    """«Sin equivalente (no se migra)» → ``no-se-migra``; «Una a una» → ``una-a-una``. Nulo si no se reconoce."""
    plano = "".join(c for c in unicodedata.normalize("NFD", (texto or "").lower()) if unicodedata.category(c) != "Mn")
    plano = plano.replace("_", " ").replace("-", " ")
    if "no se migra" in plano or "sin equivalente" in plano:
        return NO_SE_MIGRA
    if "una en varias" in plano or "uno a varios" in plano or "una a varias" in plano:
        return UNA_EN_VARIAS
    if "varias en una" in plano or "varios a uno" in plano or "varias a una" in plano:
        return VARIAS_EN_UNA
    if "una a una" in plano or "uno a uno" in plano:
        return UNA_A_UNA
    return None


def clave(codigo):
    return (codigo or "").strip().upper()


class TablaDeEquivalencias:
    def __init__(self, nombre):
        self.nombre = nombre
        self._filas = {}  # código anterior → [(nuevo, clase, número de fila)]

    def agregar(self, anterior, nuevo, clase, fila):
        self._filas.setdefault(clave(anterior), []).append((nuevo.strip(), clase, fila))

    def conflictos(self):
        """Mismo código anterior con destinos distintos sin ser todas ``una-en-varias`` (US2, escenario 4)."""
        salida = []
        for anterior, filas in self._filas.items():
            destinos = {nuevo for nuevo, clase, _ in filas if clase != NO_SE_MIGRA}
            clases = {clase for _, clase, _ in filas}
            cruzadas = (len(destinos) > 1 and clases != {UNA_EN_VARIAS}) or (NO_SE_MIGRA in clases and len(clases) > 1)
            if len(filas) > 1 and cruzadas:
                numeros = ", ".join(str(f) for _, _, f in filas)
                salida.append(f"Tabla {self.nombre}: el código anterior «{anterior}» tiene destinos distintos en las filas {numeros}.")
        return salida

    def traducir(self, codigo):
        """``(nuevo, None)`` o ``(None, (código de error, mensaje))``."""
        filas = self._filas.get(clave(codigo))
        if not filas:
            return None, (errores.EQUIVALENCE_MISSING, f"Tabla {self.nombre}, código anterior {codigo.strip()}: no tiene equivalencia.")
        clases = {clase for _, clase, _ in filas}
        if NO_SE_MIGRA in clases:
            return None, (errores.EQUIVALENCE_NOT_MIGRATED, f"Tabla {self.nombre}, código anterior {codigo.strip()}: no se migra.")
        if UNA_EN_VARIAS in clases:
            destinos = ", ".join(sorted({nuevo for nuevo, _, _ in filas}))
            return None, (errores.EQUIVALENCE_ONE_TO_MANY,
                          f"Tabla {self.nombre}, código anterior {codigo.strip()}: va a varios destinos ({destinos}); hay que decidir cuál.")
        return filas[0][0], None

    def __len__(self):
        return len(self._filas)


def leer_tabla(ruta):
    """Lee un CSV de equivalencias. Lanza ``PerfilInvalido`` con la fila si una clase no se reconoce o hay conflictos."""
    ruta = Path(ruta)
    tabla = TablaDeEquivalencias(ruta.stem.upper())
    with open(ruta, encoding="utf-8-sig", newline="") as archivo:
        lector = csv.DictReader(archivo)
        faltan = [c for c in ENCABEZADO[:3] if c not in (lector.fieldnames or [])]
        if faltan:
            raise errores.PerfilInvalido(f"{ruta.name}: faltan las columnas {', '.join(faltan)}.")
        for numero, fila in enumerate(lector, start=2):
            anterior = (fila.get("codigoAnterior") or "").strip()
            if not anterior:
                continue
            clase = normalizar_clase(fila.get("clase")) or (UNA_A_UNA if not (fila.get("clase") or "").strip() else None)
            if clase is None:
                raise errores.PerfilInvalido(f"{ruta.name}, fila {numero}: la clase «{fila.get('clase')}» no se reconoce ({', '.join(CLASES)}).")
            nuevo = (fila.get("codigoNuevo") or "").strip()
            if not nuevo and clase != NO_SE_MIGRA:
                raise errores.PerfilInvalido(f"{ruta.name}, fila {numero}: falta el código nuevo.")
            tabla.agregar(anterior, nuevo, clase, numero)
    conflictos = tabla.conflictos()
    if conflictos:
        raise errores.PerfilInvalido(" ".join(conflictos))
    return tabla


def leer_carpeta(carpeta, nombres):
    """Las tablas ``nombres`` (mayúsculas) de ``carpeta``. Una tabla citada que no existe es un perfil inválido."""
    tablas = {}
    for nombre in sorted(set(nombres)):
        ruta = Path(carpeta) / f"{nombre}.csv" if carpeta else None
        if ruta is None or not ruta.exists():
            raise errores.PerfilInvalido(f"El perfil usa la tabla de equivalencias {nombre} y no está en {carpeta or '(sin --equivalencias)'}.")
        tablas[nombre] = leer_tabla(ruta)
    return tablas


def escribir_tabla(ruta, filas):
    """Escribe ``[(anterior, nuevo, clase, nota)]`` como CSV UTF-8."""
    with open(ruta, "w", encoding="utf-8", newline="") as archivo:
        escritor = csv.writer(archivo)
        escritor.writerow(ENCABEZADO)
        escritor.writerows(filas)
