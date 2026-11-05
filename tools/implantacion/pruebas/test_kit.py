# -*- coding: utf-8 -*-
"""
Pruebas del kit de implantación (feature 016, D10). Sin datos reales: las muestras de ``muestras/`` son sintéticas.

    python -m unittest discover -s tools/implantacion/pruebas -v
"""
import csv
import json
import sys
import tempfile
import unittest
from pathlib import Path

from openpyxl import Workbook, load_workbook

KIT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(KIT))

from kit import comandos, conversion, equivalencias, errores, escritor, lectura, perfil as perfiles  # noqa: E402

MUESTRAS = KIT / "pruebas" / "muestras"
EJEMPLO = KIT / "perfiles" / "ejemplo"
EQUIVALENCIAS = EJEMPLO / "equivalencias"


def perfil_de(**cambios):
    base = {"destino": "core.people", "archivo": {"tipo": "delimitado", "separador": ";"}, "columnas": [{"destino": "documento", "origen": 1}]}
    base.update(cambios)
    return perfiles.desde_dict(base)


class Trabajo(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.trabajo = Path(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()


class ConversionTests(unittest.TestCase):
    def test_fecha_con_formato_net_y_vacios(self):
        self.assertEqual(conversion.fecha("15/01/1980", "dd/MM/yyyy"), "1980-01-15")
        self.assertEqual(conversion.fecha("19800115", "yyyyMMdd"), "1980-01-15")
        self.assertEqual(conversion.fecha("00000000", "yyyyMMdd"), "")
        self.assertEqual(conversion.fecha("00/00/0000", "dd/MM/yyyy"), "")

    def test_fecha_imposible_es_error(self):
        with self.assertRaises(conversion.ValorInvalido):
            conversion.fecha("30/02/1990", "dd/MM/yyyy")

    def test_numeros_con_miles_decimal_y_signos(self):
        self.assertEqual(conversion.numero("1.250.000,50", ",", "."), "1250000.5")
        self.assertEqual(conversion.numero("-300,00", ","), "-300")
        self.assertEqual(conversion.numero("(45,10)", ",", signo="parentesis"), "-45.1")
        self.assertEqual(conversion.numero("1500-", signo="atras"), "-1500")
        self.assertEqual(conversion.numero("0012345", decimales_implicitos=2), "123.45")
        self.assertEqual(conversion.numero("200CR", sufijos_negativos=["CR"]), "-200")
        self.assertEqual(conversion.numero("200DB", sufijos_negativos=["CR"], sufijos_positivos=["DB"]), "200")

    def test_un_numero_que_no_encaja_nunca_es_cero(self):
        for malo in ("12a", "1.2.3", "--5", "1.5"):
            with self.assertRaises(conversion.ValorInvalido, msg=malo):
                conversion.numero(malo, ",", "")

    def test_si_no(self):
        self.assertEqual(conversion.si_no("S"), "si")
        self.assertEqual(conversion.si_no("n"), "no")
        self.assertEqual(conversion.si_no(" "), "")
        with self.assertRaises(conversion.ValorInvalido):
            conversion.si_no("quizá")


class PerfilTests(unittest.TestCase):
    def test_destino_repetido_se_rechaza(self):
        with self.assertRaisesRegex(errores.PerfilInvalido, "dos veces"):
            perfil_de(columnas=[{"destino": "documento", "origen": 1}, {"destino": "documento", "origen": 2}])

    def test_posiciones_cruzadas_se_rechazan(self):
        with self.assertRaisesRegex(errores.PerfilInvalido, "se cruzan"):
            perfil_de(archivo={"tipo": "anchoFijo"}, columnas=[
                {"destino": "a", "posicion": 1, "largo": 5}, {"destino": "b", "posicion": 4, "largo": 2}])

    def test_fecha_sin_formato_se_rechaza(self):
        with self.assertRaisesRegex(errores.PerfilInvalido, "formato"):
            perfil_de(columnas=[{"destino": "f", "origen": 1, "tipo": "fecha"}])

    def test_con_la_plantilla_falta_una_obligatoria(self):
        p = perfil_de()
        with self.assertRaisesRegex(errores.PerfilInvalido, "obligatoria «tipoDocumento»"):
            perfiles.validar(p, ["tipoDocumento", "documento"], ["tipoDocumento", "documento"])

    def test_con_la_plantilla_una_columna_que_no_existe(self):
        p = perfil_de(columnas=[{"destino": "cedula", "origen": 1}])
        with self.assertRaisesRegex(errores.PerfilInvalido, "no es una columna"):
            perfiles.validar(p, ["documento"], [])


class LecturaTests(unittest.TestCase):
    def leer_muestra(self, perfil, muestra):
        p = perfiles.cargar(EJEMPLO / perfil)
        tablas = equivalencias.leer_carpeta(EQUIVALENCIAS, p.tablas_de_equivalencia)
        return lectura.leer((MUESTRAS / muestra).read_bytes(), p, tablas)

    def test_ancho_fijo_tres_buenas_y_dos_malas_con_su_linea(self):
        leida = self.leer_muestra("terceros-ancho-fijo.json", "terceros-ancho-fijo.txt")
        self.assertEqual([f.linea for f in leida.filas], [2, 3, 4])
        self.assertEqual(leida.lineas_saltadas, 2)  # el título y el pie de totales
        ana = leida.filas[0].valores
        self.assertEqual(ana["tipoDocumento"], "CC")
        self.assertEqual(ana["documento"], "1010101010")
        self.assertEqual(ana["primerApellido"], "MUÑOZ")  # windows-1252 bien leído
        self.assertEqual(ana["ciudad"], "76001")
        self.assertEqual(ana["fechaNacimiento"], "1980-01-15")
        self.assertEqual(ana["esProveedor"], "no")
        juridica = leida.filas[2].valores
        self.assertEqual((juridica["naturaleza"], juridica["digitoVerificacion"], juridica["fechaNacimiento"]), ("juridica", "7", ""))
        codigos = {(e.linea, e.columna, e.codigo) for e in leida.solo_errores}
        self.assertEqual(codigos, {(5, "fechaNacimiento", errores.INVALID_DATE), (6, "ciudad", errores.EQUIVALENCE_MISSING)})
        self.assertIn("CIUDADES", next(e.mensaje for e in leida.solo_errores if e.linea == 6))

    def test_csv_con_encabezado_y_numeros(self):
        p = perfiles.desde_dict({
            "destino": "x", "archivo": {"tipo": "delimitado", "separador": ";", "encabezado": True},
            "columnas": [{"destino": "documento", "origen": "numero"},
                         {"destino": "saldo", "origen": "saldo_aportes", "tipo": "numero", "separadorDecimal": ",",
                          "separadorMiles": ".", "signo": "parentesis"}]})
        leida = lectura.leer((MUESTRAS / "terceros.csv").read_bytes(), p)
        self.assertEqual([f.valores["saldo"] for f in leida.filas], ["1250000.5", "-300", "-45.1"])
        self.assertEqual([f.linea for f in leida.filas], [2, 3, 4])

    def test_campo_fuera_de_la_linea(self):
        p = perfil_de(archivo={"tipo": "anchoFijo"}, columnas=[{"destino": "a", "posicion": 1, "largo": 3},
                                                              {"destino": "b", "posicion": 4, "largo": 10}])
        leida = lectura.leer(b"abcdef\n", p)
        self.assertEqual(leida.solo_errores[0].codigo, errores.FIELD_OUT_OF_LINE)
        self.assertIn("4-13", leida.solo_errores[0].mensaje)

    def test_linea_de_otro_largo(self):
        p = perfil_de(archivo={"tipo": "anchoFijo", "largoDeLinea": 5}, columnas=[{"destino": "a", "posicion": 1, "largo": 5}])
        leida = lectura.leer(b"12345\n1234\n", p)
        self.assertEqual(len(leida.filas), 1)
        self.assertEqual((leida.solo_errores[0].linea, leida.solo_errores[0].codigo), (2, errores.LINE_LENGTH))
        self.assertIn("Largo 4, se esperaban 5", leida.solo_errores[0].mensaje)

    def test_columna_faltante_en_delimitado(self):
        p = perfil_de(columnas=[{"destino": "a", "origen": 1}, {"destino": "b", "origen": 3}])
        leida = lectura.leer(b"x;y\n", p)
        self.assertEqual(leida.solo_errores[0].codigo, errores.MISSING_COLUMN)

    def test_codificacion_equivocada_se_avisa(self):
        p = perfil_de(codificacion="windows-1252", columnas=[{"destino": "a", "origen": 1}])
        leida = lectura.leer("MUÑOZ;1\n".encode("utf-8"), p)
        avisos = [e for e in leida.errores if e.aviso]
        self.assertEqual(avisos[0].codigo, errores.ENCODING_SUSPECT)
        self.assertEqual(len(leida.filas), 1)  # el aviso no detiene la línea

    def test_no_se_migra_es_error_y_sin_equivalencia_igual_pasa(self):
        tabla = equivalencias.TablaDeEquivalencias("CUENTAS")
        tabla.agregar("1105", "110505", equivalencias.UNA_A_UNA, 2)
        tabla.agregar("9999", "", equivalencias.NO_SE_MIGRA, 3)
        p = perfil_de(columnas=[{"destino": "cuenta", "origen": 1, "equivalencia": "CUENTAS"},
                                {"destino": "ciudad", "origen": 2, "equivalencia": "CUENTAS", "sinEquivalencia": "igual"}])
        leida = lectura.leer(b"1105;76001\n9999;76001\n", p, {"CUENTAS": tabla})
        self.assertEqual(leida.filas[0].valores, {"cuenta": "110505", "ciudad": "76001"})
        self.assertEqual(leida.solo_errores[0].codigo, errores.EQUIVALENCE_NOT_MIGRATED)


class EquivalenciasTests(Trabajo):
    def test_codigo_con_destinos_distintos_se_rechaza_con_las_dos_filas(self):
        ruta = self.trabajo / "CUENTAS.csv"
        equivalencias.escribir_tabla(ruta, [("1105", "110505", "una-a-una", ""), ("1105", "110510", "una-a-una", "")])
        with self.assertRaisesRegex(errores.PerfilInvalido, "filas 2, 3"):
            equivalencias.leer_tabla(ruta)

    def test_una_en_varias_pide_decidir(self):
        ruta = self.trabajo / "CUENTAS.csv"
        equivalencias.escribir_tabla(ruta, [("1105", "110505", "una-en-varias", ""), ("1105", "110510", "una-en-varias", "")])
        _, falla = equivalencias.leer_tabla(ruta).traducir("1105")
        self.assertEqual(falla[0], errores.EQUIVALENCE_ONE_TO_MANY)
        self.assertIn("110505, 110510", falla[1])

    def test_formato_de_la_contadora(self):
        libro = Workbook()
        ws = libro.active
        ws.title = "Equivalencias"
        ws.append(["Formato de equivalencias de cuentas"])
        ws.append(["Código actual", "Nombre", "Cuenta CUIF (6 dígitos)", "Auxiliar nuevo (código)", "Tipo de equivalencia"])
        ws.append(["110505", "Caja", "110505", "11050501", "Una a una"])
        ws.append(["110510", "Caja menor", "110505", None, "Varias en una"])
        ws.append(["259595", "Otros", None, None, "Sin equivalente (no se migra)"])
        ws.append(["131010", "Cartera", "141105", None, "Una en varias"])
        ws.append(["131010", "Cartera", "141110", None, "Una en varias"])
        ruta = self.trabajo / "contadora.xlsx"
        libro.save(ruta)
        salida = self.trabajo / "eq"
        self.assertEqual(comandos.equivalencias_desde_contadora([str(ruta), "--salida", str(salida)]), 0)
        tabla = equivalencias.leer_tabla(salida / "CUENTAS.csv")
        self.assertEqual(tabla.traducir("110505")[0], "11050501")
        self.assertEqual(tabla.traducir("110510")[0], "110505")
        self.assertEqual(tabla.traducir("259595")[1][0], errores.EQUIVALENCE_NOT_MIGRATED)
        self.assertEqual(tabla.traducir("131010")[1][0], errores.EQUIVALENCE_ONE_TO_MANY)

    def test_formato_de_la_contadora_con_conflicto_no_escribe(self):
        libro = Workbook()
        ws = libro.active
        ws.title = "Equivalencias"
        ws.append(["Código actual", "Cuenta CUIF (6 dígitos)", "Auxiliar nuevo (código)", "Tipo de equivalencia"])
        ws.append(["110505", "110505", None, "Una a una"])
        ws.append(["110505", "110510", None, "Una a una"])
        ruta = self.trabajo / "contadora.xlsx"
        libro.save(ruta)
        filas, problemas = comandos.leer_formato_de_la_contadora(ruta)
        self.assertTrue(any("filas 2, 3" in p for p in problemas))


class AdaptacionTests(Trabajo):
    def plantilla_del_erp(self):
        """Un libro con la forma del que descarga el ERP: hoja de datos y hoja «Instrucciones»."""
        libro = Workbook()
        ws = libro.active
        ws.title = "Datos"
        encabezados = ["tipoDocumento", "documento", "digitoVerificacion", "naturaleza", "primerNombre", "otrosNombres",
                       "primerApellido", "segundoApellido", "razonSocial", "ciudad", "fechaNacimiento", "esProveedor", "esCliente"]
        ws.append(encabezados)
        ins = libro.create_sheet("Instrucciones")
        ins.append(["Personas y terceros"])
        ins.append([])
        ins.append(["Hoja «Datos»"])
        ins.append(["columna", "tipo", "obligatoria", "reglas", "ejemplo"])
        for e in encabezados:
            ins.append([e, "Texto", "sí" if e in ("tipoDocumento", "documento", "naturaleza") else "no", "", ""])
        ruta = self.trabajo / "plantilla-core-people.xlsx"
        libro.save(ruta)
        return ruta

    def test_adaptar_con_plantilla_cifras_y_particion(self):
        cifras = self.trabajo / "cifras.csv"
        cifras.write_text("total,valor\nfilas.entregadas,4\ncuenta:tipoDocumento=CC,2\n", encoding="utf-8")
        r = comandos.ejecutar_adaptacion(MUESTRAS / "terceros-ancho-fijo.txt", EJEMPLO / "terceros-ancho-fijo.json",
                                         self.trabajo / "salida", EQUIVALENCIAS, self.plantilla_del_erp(), cifras, max_filas=2)
        self.assertEqual([l.name for l in r["libros"]], ["terceros-ancho-fijo-01.xlsx", "terceros-ancho-fijo-02.xlsx"])
        ws = load_workbook(r["libros"][0]).active
        encabezados = [c.value for c in ws[1]]
        # el orden es el de la plantilla y sólo con las columnas que el perfil llena
        self.assertEqual(encabezados[:4], ["tipoDocumento", "documento", "digitoVerificacion", "naturaleza"])
        self.assertNotIn("otrosNombres", encabezados)
        self.assertEqual(ws.cell(row=2, column=2).value, "1010101010")
        with open(r["mapa"], encoding="utf-8") as f:
            mapa = list(csv.DictReader(f))
        self.assertEqual([(m["libro"][-7:], m["fila"], m["linea"]) for m in mapa],
                         [("01.xlsx", "2", "2"), ("01.xlsx", "3", "3"), ("02.xlsx", "2", "4")])
        diferencias = {n: d for n, _, _, d in r["comparacion"]}
        self.assertEqual(diferencias["filas.entregadas"], -1)
        self.assertEqual(diferencias["cuenta:tipoDocumento=CC"], 0)
        self.assertIn("**≠**", r["informe"].read_text(encoding="utf-8"))

    def test_no_escribe_dentro_del_repositorio(self):
        with self.assertRaisesRegex(errores.PerfilInvalido, "dentro del repositorio"):
            comandos.ejecutar_adaptacion(MUESTRAS / "terceros.csv", EJEMPLO / "terceros-csv.json", KIT / "salida-prohibida", EQUIVALENCIAS)
        self.assertFalse((KIT / "salida-prohibida").exists())

    def test_traducir_la_revision_del_erp_a_lineas(self):
        r = comandos.ejecutar_adaptacion(MUESTRAS / "terceros-ancho-fijo.txt", EJEMPLO / "terceros-ancho-fijo.json",
                                         self.trabajo, EQUIVALENCIAS)
        libro = load_workbook(r["libros"][0])
        ws = libro.active
        col = ws.max_column
        ws.cell(row=1, column=col + 1, value="resultado")
        ws.cell(row=1, column=col + 2, value="errores")
        ws.cell(row=3, column=col + 1, value="Con errores")
        ws.cell(row=3, column=col + 2, value="documento: Ya existe LUIS GÓMEZ con ese documento.")
        revision = self.trabajo / "terceros-ancho-fijo-01-revision.xlsx"
        libro.save(revision)
        filas = comandos.ejecutar_traduccion(revision, r["mapa"])
        self.assertEqual(filas, [[3, 3, "Con errores", "documento: Ya existe LUIS GÓMEZ con ese documento."]])


class EjemplosTests(unittest.TestCase):
    def test_los_perfiles_de_ejemplo_cargan(self):
        for ruta in EJEMPLO.glob("*.json"):
            p = perfiles.cargar(ruta)
            equivalencias.leer_carpeta(EQUIVALENCIAS, p.tablas_de_equivalencia)
            json.loads(ruta.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
