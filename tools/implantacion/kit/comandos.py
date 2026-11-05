# -*- coding: utf-8 -*-
"""Los tres comandos del kit. Los guiones ``adaptar.py``, ``equivalencias.py`` y ``traducir-errores.py`` sólo los llaman."""
import argparse
import csv
import sys
from datetime import datetime
from pathlib import Path

from openpyxl import load_workbook

from . import equivalencias, escritor, lectura, perfil as perfiles, totales
from .errores import ENCABEZADO_CSV, PerfilInvalido
from .perfil import normalizar_encabezado

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")


# ----------------------------------------------------------------------------------------------------- adaptar --

def adaptar(argv=None):
    p = argparse.ArgumentParser(description="Convierte un plano del sistema anterior en la plantilla del ERP con su perfil.")
    p.add_argument("archivo", help="el plano tal como sale (CSV, TXT de ancho fijo…)")
    p.add_argument("--perfil", required=True, help="el JSON del perfil")
    p.add_argument("--trabajo", required=True, help="carpeta de salida, FUERA del repositorio")
    p.add_argument("--equivalencias", help="carpeta con las tablas CSV que cita el perfil")
    p.add_argument("--plantilla", help="el template.xlsx que descarga el ERP: comprueba encabezados y obligatorias")
    p.add_argument("--cifras", help="CSV total,valor con las cifras de referencia")
    p.add_argument("--max-filas", type=int, default=escritor.MAXIMO_DE_FILAS, help="filas por libro (60.000)")
    p.add_argument("--nombre", help="base del nombre de los libros (por defecto, el del plano)")
    a = p.parse_args(argv)
    try:
        resultado = ejecutar_adaptacion(a.archivo, a.perfil, a.trabajo, a.equivalencias, a.plantilla, a.cifras, a.max_filas, a.nombre)
    except (PerfilInvalido, ValueError, OSError) as ex:
        print(f"No se pudo adaptar: {ex}", file=sys.stderr)
        return 2
    print(resultado["resumen"])
    return 1 if resultado["errores"] else 0


def ejecutar_adaptacion(archivo, ruta_perfil, trabajo, carpeta_equivalencias=None, plantilla=None, cifras=None,
                        max_filas=escritor.MAXIMO_DE_FILAS, nombre=None):
    escritor.exigir_fuera_del_repositorio(trabajo)
    perfil = perfiles.cargar(ruta_perfil)

    encabezados = [c.destino for c in perfil.columnas]
    if plantilla:
        hoja, encabezados_erp, obligatorias = escritor.leer_plantilla(plantilla, perfil.hoja)
        perfiles.validar(perfil, encabezados_erp, obligatorias)
        perfil.hoja = hoja
        # El orden del libro es el de la plantilla; sólo las columnas que el perfil llena.
        llenas = {normalizar_encabezado(c.destino): c.destino for c in perfil.columnas}
        encabezados = [llenas[normalizar_encabezado(e)] for e in encabezados_erp if normalizar_encabezado(e) in llenas]

    tablas = equivalencias.leer_carpeta(carpeta_equivalencias, perfil.tablas_de_equivalencia) if perfil.tablas_de_equivalencia else {}
    contenido = Path(archivo).read_bytes()
    leida = lectura.leer(contenido, perfil, tablas)

    base = nombre or Path(archivo).stem
    libros, mapa = escritor.escribir(leida.filas, encabezados, perfil.hoja, trabajo, base, max_filas)

    ruta_errores = Path(trabajo) / f"{base}-errores.csv"
    with open(ruta_errores, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f)
        w.writerow(ENCABEZADO_CSV)
        for e in leida.errores:
            w.writerow(e.como_fila())

    calculados = totales.calcular(leida, perfil)
    comparacion = totales.comparar(calculados, totales.leer_cifras(cifras)) if cifras else None
    informe = _informe(archivo, perfil, leida, libros, mapa, ruta_errores, calculados, comparacion)
    ruta_informe = Path(trabajo) / f"{base}-informe.md"
    ruta_informe.write_text(informe, encoding="utf-8")

    resumen = (f"{perfil.codigo()} → {perfil.destino}: {leida.lineas_leidas} líneas leídas, {len(leida.filas)} entregadas, "
               f"{leida.lineas_con_error} con error, {len(leida.errores) - len(leida.solo_errores)} avisos. "
               f"Libros: {', '.join(l.name for l in libros)}. Informe: {ruta_informe}")
    return {"lectura": leida, "libros": libros, "mapa": mapa, "informe": ruta_informe, "errores": leida.solo_errores,
            "totales": calculados, "comparacion": comparacion, "resumen": resumen}


def _informe(archivo, perfil, leida, libros, mapa, ruta_errores, calculados, comparacion):
    r = [f"# Adaptación de {Path(archivo).name}", "",
         f"- Fecha: {datetime.now():%Y-%m-%d %H:%M}",
         f"- Perfil: {perfil.codigo()} ({perfil.nombre}) → plantilla `{perfil.destino}`, hoja «{perfil.hoja}»",
         f"- Libros para subir: {', '.join(l.name for l in libros)}",
         f"- Mapa fila → línea: {mapa.name}; errores y avisos: {ruta_errores.name}", "",
         "## Totales de control", "", "| total | calculado | referencia | diferencia |", "|---|---:|---:|---:|"]
    filas = comparacion if comparacion is not None else [(n, v, None, None) for n, v in sorted(calculados.items())]
    for nombre, calculado, referencia, diferencia in filas:
        marca = " **≠**" if diferencia not in (None, 0) else ""
        r.append(f"| {nombre} | {totales.texto(calculado)} | {totales.texto(referencia)} | {totales.texto(diferencia)}{marca} |")
    solo = leida.solo_errores
    r += ["", f"## Errores de lectura ({len(solo)})", ""]
    if not solo:
        r.append("Ninguno: todas las líneas leídas pasaron al libro.")
    for e in solo[:200]:
        r.append(f"- Línea {e.linea}, {e.columna or '(línea)'} ({e.origen}): `{e.codigo}` {e.mensaje}")
    if len(solo) > 200:
        r.append(f"- … y {len(solo) - 200} más en {ruta_errores.name}.")
    avisos = [e for e in leida.errores if e.aviso]
    if avisos:
        r += ["", f"## Avisos ({len(avisos)})", ""]
        r += [f"- Línea {e.linea}: `{e.codigo}` {e.mensaje}" for e in avisos[:50]]
    return "\n".join(r) + "\n"


# ----------------------------------------------------------------------------------------------- equivalencias --

COLUMNAS_DE_LA_CONTADORA = {
    "anterior": "Código actual",
    "cuif": "Cuenta CUIF (6 dígitos)",
    "auxiliar": "Auxiliar nuevo (código)",
    "tipo": "Tipo de equivalencia",
}


def equivalencias_desde_contadora(argv=None):
    p = argparse.ArgumentParser(description="Hoja «Equivalencias» del formato de la contadora → tabla CUENTAS.csv del kit.")
    p.add_argument("libro", help="el formato de la contadora (.xlsx)")
    p.add_argument("--salida", required=True, help="carpeta de equivalencias de la cooperativa")
    p.add_argument("--tabla", default="CUENTAS", help="nombre de la tabla (CUENTAS)")
    p.add_argument("--hoja", default="Equivalencias")
    a = p.parse_args(argv)
    try:
        filas, problemas = leer_formato_de_la_contadora(a.libro, a.hoja)
    except (PerfilInvalido, OSError) as ex:
        print(f"No se pudo leer: {ex}", file=sys.stderr)
        return 2
    if problemas:
        print("No se escribió la tabla:", file=sys.stderr)
        for x in problemas:
            print(f"- {x}", file=sys.stderr)
        return 1
    Path(a.salida).mkdir(parents=True, exist_ok=True)
    ruta = Path(a.salida) / f"{a.tabla.upper()}.csv"
    equivalencias.escribir_tabla(ruta, filas)
    print(f"{len(filas)} equivalencias en {ruta}")
    return 0


def leer_formato_de_la_contadora(ruta, hoja="Equivalencias"):
    """``[(anterior, nuevo, clase, nota)]`` y la lista de problemas (clase desconocida, sin destino, conflictos)."""
    libro = load_workbook(ruta, read_only=True, data_only=True)
    try:
        nombre = next((h for h in libro.sheetnames if normalizar_encabezado(h) == normalizar_encabezado(hoja)), None)
        if nombre is None:
            raise PerfilInvalido(f"{Path(ruta).name} no tiene la hoja «{hoja}».")
        filas = list(libro[nombre].iter_rows(values_only=True))
    finally:
        libro.close()

    buscadas = {k: normalizar_encabezado(v) for k, v in COLUMNAS_DE_LA_CONTADORA.items()}
    inicio, indices = None, None
    for i, fila in enumerate(filas):
        normal = [normalizar_encabezado(v) if v is not None else "" for v in fila]
        if buscadas["anterior"] in normal:
            indices = {k: (normal.index(v) if v in normal else None) for k, v in buscadas.items()}
            inicio = i
            break
    if indices is None or indices["tipo"] is None or (indices["cuif"] is None and indices["auxiliar"] is None):
        raise PerfilInvalido(f"La hoja «{hoja}» no trae los encabezados {', '.join(COLUMNAS_DE_LA_CONTADORA.values())}.")

    def celda(fila, k):
        j = indices[k]
        return "" if j is None or j >= len(fila) or fila[j] is None else str(fila[j]).strip()

    salida, problemas = [], []
    tabla = equivalencias.TablaDeEquivalencias("CUENTAS")
    for n, fila in enumerate(filas[inicio + 1:], start=inicio + 2):
        anterior = celda(fila, "anterior")
        if not anterior:
            continue
        clase = equivalencias.normalizar_clase(celda(fila, "tipo"))
        if clase is None:
            problemas.append(f"Fila {n}: el tipo de equivalencia «{celda(fila, 'tipo')}» no se reconoce.")
            continue
        nuevo = celda(fila, "auxiliar") or celda(fila, "cuif")
        if not nuevo and clase != equivalencias.NO_SE_MIGRA:
            problemas.append(f"Fila {n}: el código {anterior} no trae cuenta CUIF ni auxiliar nuevo.")
            continue
        tabla.agregar(anterior, nuevo, clase, n)
        salida.append((anterior, nuevo, clase, ""))
    problemas += tabla.conflictos()
    return salida, problemas


# ---------------------------------------------------------------------------------------------- traducir errores --

def traducir_errores(argv=None):
    p = argparse.ArgumentParser(description="Lleva los errores del libro de revisión del ERP a las líneas del plano.")
    p.add_argument("revision", help="el libro de revisión que devolvió el ERP (…-revision.xlsx)")
    p.add_argument("--mapa", required=True, help="el …-mapa.csv que dejó adaptar.py")
    p.add_argument("--libro", help="el nombre del libro que se subió (por defecto, el de la revisión sin «-revision»)")
    p.add_argument("--salida", help="CSV de salida (por defecto, junto a la revisión)")
    a = p.parse_args(argv)
    try:
        filas = ejecutar_traduccion(a.revision, a.mapa, a.libro)
    except (PerfilInvalido, OSError) as ex:
        print(f"No se pudo traducir: {ex}", file=sys.stderr)
        return 2
    salida = Path(a.salida) if a.salida else Path(a.revision).with_name(Path(a.revision).stem + "-por-linea.csv")
    with open(salida, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f)
        w.writerow(["linea", "filaDelLibro", "resultado", "errores"])
        w.writerows(filas)
    print(f"{len(filas)} filas con errores → {salida}")
    return 1 if filas else 0


def ejecutar_traduccion(revision, ruta_mapa, nombre_libro=None):
    nombre_libro = nombre_libro or Path(revision).name.replace("-revision", "")
    mapa = {}
    with open(ruta_mapa, encoding="utf-8", newline="") as f:
        for fila in csv.DictReader(f):
            if fila["libro"] == nombre_libro:
                mapa[int(fila["fila"])] = int(fila["linea"])
    if not mapa:
        raise PerfilInvalido(f"El mapa no tiene filas del libro {nombre_libro}; indíquelo con --libro.")

    libro = load_workbook(revision, read_only=True, data_only=True)
    try:
        hoja = next(h for h in libro.sheetnames if h != escritor.HOJA_DE_INSTRUCCIONES)
        filas = list(libro[hoja].iter_rows(values_only=True))
    finally:
        libro.close()
    encabezados = [normalizar_encabezado(v or "") for v in filas[0]] if filas else []
    if "resultado" not in encabezados or "errores" not in encabezados:
        raise PerfilInvalido("El libro no trae las columnas «resultado» y «errores»: ¿es la revisión en Excel del ERP?")
    i_resultado, i_errores = encabezados.index("resultado"), encabezados.index("errores")

    salida = []
    for n, fila in enumerate(filas[1:], start=2):
        texto = fila[i_errores] if i_errores < len(fila) else None
        if texto:
            salida.append([mapa.get(n, ""), n, fila[i_resultado] or "", texto])
    return salida
