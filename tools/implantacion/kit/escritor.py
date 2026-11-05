# -*- coding: utf-8 -*-
"""
Los libros que se suben al ERP y el mapa «libro, fila → línea original». El libro tiene una hoja con el nombre de la hoja
de la plantilla y sus encabezados exactos en la fila 1; todo va como texto (el ERP convierte cada celda con su tipo).
Los planos y los libros llevan datos personales: **nunca se escriben dentro del repositorio**.
"""
import csv
from pathlib import Path

from openpyxl import Workbook, load_workbook
from openpyxl.styles import Font

from .errores import PerfilInvalido
from .perfil import normalizar_encabezado

MAXIMO_DE_FILAS = 60000  # el tope por hoja del ejecutor común del ERP (EjecutorDeImportacion.MaximoDeFilasPorHoja)
HOJA_DE_INSTRUCCIONES = "Instrucciones"
KIT = Path(__file__).resolve().parent


def repositorios_que_contienen_el_kit():
    """Cada carpeta antecesora del kit que es la raíz de un repositorio o de un worktree (tiene ``.git``)."""
    return [p for p in KIT.parents if (p / ".git").exists()]


def exigir_fuera_del_repositorio(carpeta):
    destino = Path(carpeta).resolve()
    for raiz in repositorios_que_contienen_el_kit():
        if destino == raiz or raiz in destino.parents:
            raise PerfilInvalido(
                f"La carpeta de trabajo {destino} está dentro del repositorio {raiz}. Los planos y los libros llevan datos "
                "personales: use una carpeta fuera del repositorio.")


def leer_plantilla(ruta, hoja=None):
    """Encabezados (en orden) y obligatorias de la plantilla que descarga el ERP (``…/template.xlsx``)."""
    libro = load_workbook(ruta, read_only=True, data_only=True)
    try:
        hojas = [h for h in libro.sheetnames if h != HOJA_DE_INSTRUCCIONES]
        nombre = next((h for h in hojas if hoja and normalizar_encabezado(h) == normalizar_encabezado(hoja)), None) or (hojas[0] if hojas else None)
        if nombre is None:
            raise PerfilInvalido(f"{Path(ruta).name} no tiene hojas de datos.")
        primera = next(libro[nombre].iter_rows(min_row=1, max_row=1, values_only=True), ())
        encabezados = [str(v).strip() for v in primera if v not in (None, "")]

        obligatorias = []
        if HOJA_DE_INSTRUCCIONES in libro.sheetnames:
            en_la_hoja = len(hojas) == 1
            for fila in libro[HOJA_DE_INSTRUCCIONES].iter_rows(values_only=True):
                primera_celda = str(fila[0] or "") if fila else ""
                if primera_celda.startswith("Hoja «"):
                    en_la_hoja = normalizar_encabezado(primera_celda.split("«")[1].split("»")[0]) == normalizar_encabezado(nombre)
                    continue
                if en_la_hoja and len(fila) >= 3 and str(fila[2] or "").strip().lower() == "sí" and fila[0] in encabezados:
                    obligatorias.append(fila[0])
        return nombre, encabezados, obligatorias
    finally:
        libro.close()


def escribir(filas, encabezados, hoja, carpeta, base, maximo=MAXIMO_DE_FILAS):
    """
    Escribe ``filas`` (``FilaLeida``) en uno o varios libros ``{base}-01.xlsx``… y el mapa ``{base}-mapa.csv``.
    Devuelve las rutas de los libros y la del mapa.
    """
    carpeta = Path(carpeta)
    exigir_fuera_del_repositorio(carpeta)
    carpeta.mkdir(parents=True, exist_ok=True)
    maximo = max(1, int(maximo))
    partes = [filas[i:i + maximo] for i in range(0, len(filas), maximo)] or [[]]

    libros = []
    ruta_mapa = carpeta / f"{base}-mapa.csv"
    with open(ruta_mapa, "w", encoding="utf-8", newline="") as archivo_mapa:
        mapa = csv.writer(archivo_mapa)
        mapa.writerow(["libro", "fila", "linea"])
        for n, parte in enumerate(partes, start=1):
            nombre = f"{base}-{n:02d}.xlsx"
            libro = Workbook()
            ws = libro.active
            ws.title = hoja
            for j, encabezado in enumerate(encabezados, start=1):
                celda = ws.cell(row=1, column=j, value=encabezado)
                celda.font = Font(bold=True)
            for i, fila in enumerate(parte, start=2):
                for j, encabezado in enumerate(encabezados, start=1):
                    valor = fila.valores.get(encabezado, "")
                    if valor != "":
                        celda = ws.cell(row=i, column=j, value=valor)
                        celda.number_format = "@"
                mapa.writerow([nombre, i, fila.linea])
            libro.save(carpeta / nombre)
            libros.append(carpeta / nombre)
    return libros, ruta_mapa
