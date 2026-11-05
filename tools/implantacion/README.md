# Kit de implantación (feature 016)

Convierte los archivos planos tal como salen del sistema anterior en las **plantillas exactas del ERP**, para subirlos en las
pantallas de siempre (Revisar → Aplicar). Lo corre la sesión de Claude de la implantación (decisión D10 del dueño, 2026-10-30).

Las reglas de negocio no viven aquí: las aplica el ERP al revisar (alta unitaria, todo o nada, errores por fila y columna, nunca
borra). El kit hace cuatro cosas:

- lee el archivo con su **perfil**;
- traduce los códigos con las **equivalencias**;
- entrega el libro, el **mapa** de cada fila a su línea del plano y un **informe** con los totales de control;
- después de la revisión del ERP, **traduce los errores** a la línea del plano.

Requisitos: Python 3.10+ y `openpyxl`.

## Reglas de la sesión

1. **Los planos y los libros llevan datos personales.** Van a una carpeta de trabajo **fuera del repositorio** (`--trabajo`); el kit
   se niega a escribir dentro. Nunca se pegan en el chat ni se suben a ningún servicio.
2. **Claude no inicia sesión en el ERP ni maneja tokens.** El equipo de la cooperativa o el dueño descarga la plantilla, sube el
   libro, revisa, y aplica sólo cuando la revisión salga limpia.
3. **Nada en producción sin la autorización expresa del dueño.** El ensayo es en QA (runbook
   `docs/operaciones/implantacion-con-el-kit.md`).
4. **Ningún texto del producto nombra al sistema anterior.** El kit habla del «sistema anterior».

## Pasos por archivo

```bash
# 1. (una vez) Equivalencias de cuentas desde el formato de la contadora
python equivalencias.py formato-contadora.xlsx --salida cooperativas/<coop>/equivalencias

# 2. Adaptar: plano + perfil → libro(s) + mapa + informe (código 1 si hubo errores de lectura)
python adaptar.py C:/implantacion/<coop>/planos/terceros.txt \
    --perfil cooperativas/<coop>/perfiles/terceros.json \
    --equivalencias cooperativas/<coop>/equivalencias \
    --plantilla C:/implantacion/<coop>/plantilla-core-people.xlsx \
    --cifras C:/implantacion/<coop>/cifras/terceros.csv \
    --trabajo C:/implantacion/<coop>/salida

# 3. El equipo sube terceros-01.xlsx en la pantalla, «Revisar» con «Descargar revisión en Excel».
# 4. Si la revisión trae errores, traducirlos a la línea del plano:
python traducir-errores.py C:/implantacion/<coop>/salida/terceros-01-revision.xlsx \
    --mapa C:/implantacion/<coop>/salida/terceros-mapa.csv
```

`--plantilla` es el `template.xlsx` que descarga el ERP. Con él, el kit comprueba que cada columna del perfil existe en la
plantilla y que todas las obligatorias se llenan, y ordena las columnas como la plantilla. `--max-filas` parte el libro (60.000
filas por defecto, el tope de una hoja del ERP).

Salidas en `--trabajo`:

| archivo | qué es |
|---|---|
| `{base}-01.xlsx`, `-02`… | lo que se sube al ERP (hoja con el nombre de la plantilla, todo como texto) |
| `{base}-mapa.csv` | `libro, fila, linea`: cada fila del libro con su línea del plano |
| `{base}-errores.csv` | errores y avisos de lectura por línea |
| `{base}-informe.md` | resumen, totales de control contra cifras, errores |

Una línea con un error de lectura **no pasa al libro**: se corrige el plano, el perfil o la equivalencia y se vuelve a adaptar.

## El perfil

Un JSON por archivo y cooperativa en `cooperativas/<coop>/perfiles/`. Ver los ejemplos en `perfiles/ejemplo/`.

```json
{
  "nombre": "Terceros", "version": 1,
  "destino": "core.people", "hoja": "Datos",
  "archivo": { "tipo": "anchoFijo", "largoDeLinea": 133, "rellenarLineasCortas": false },
  "codificacion": "windows-1252",
  "saltar": { "primeras": 1, "vacias": true, "patrones": [ { "empiezaCon": "TOTAL" }, { "contiene": "PAGINA" }, { "regex": "^-+$" } ] },
  "columnas": [ { "destino": "documento", "posicion": 3, "largo": 15, "quitarCerosIzquierda": true } ],
  "totales": { "agrupar": [ "tipoDocumento" ], "sumar": [ "saldo" ] }
}
```

- `archivo.tipo`: `delimitado` (`separador`, `calificador`, `encabezado`) o `anchoFijo` (`largoDeLinea` opcional; una línea de otro
  largo es `Import.Profile.LineLength`).
- `codificacion`: `utf-8`, `windows-1252` o `iso-8859-1`. Un BOM UTF-8 manda y se avisa. Los caracteres no reconocidos, incluido el
  UTF-8 leído como windows-1252 («MUÃ‘OZ»), se avisan con `Import.Profile.EncodingSuspect`.
- Cada columna lleva **una** fuente:
  - `origen`: en delimitado, el nombre del encabezado o la posición 1-based;
  - `posicion` y `largo`: en ancho fijo, 1-based;
  - `constante`.
- Atributos de la columna:
  - `tipo`: `texto` (por defecto), `fecha` (`formato` al estilo .NET: `dd/MM/yyyy`, `yyyyMMdd`; `vaciosDeFecha`), `numero` o
    `sino` (`valoresSi`, `valoresNo`);
  - del número: `separadorDecimal`, `separadorMiles`, `decimalesImplicitos`, `signo` (`adelante`, `atras`, `parentesis`),
    `sufijosNegativos` (`["CR"]`) y `sufijosPositivos`;
  - del texto: `recorte` (`ambos`, `izquierda`, `derecha`, `no`), `quitarCerosIzquierda`, `mayusculas`, `siVacio`;
  - `equivalencia` (nombre de la tabla) y `sinEquivalencia` (`error` por defecto, o `igual` para dejar pasar el código).
- Texto canónico entregado: fecha `yyyy-MM-dd`; número con punto decimal, sin miles y con el signo adelante; `si`/`no`. **Un número
  que no encaja nunca se vuelve cero.**
- Cuando un perfil ya se usó en una carga aplicada, cambiarlo es **subir `version`** en un commit nuevo. Así el informe dice con qué
  versión se leyó cada carga.

## Equivalencias

Un CSV por tabla en `cooperativas/<coop>/equivalencias/`: `codigoAnterior,codigoNuevo,clase,nota`. El nombre del archivo, en
mayúsculas, es el que cita el perfil.

| clase | al cargar |
|---|---|
| `una-a-una`, `varias-en-una` | traduce |
| `una-en-varias` | error `Import.Equivalence.OneToMany` con los destinos: hay que decidir |
| `no-se-migra` | error `Import.Equivalence.NotMigrated`: nunca se omite en silencio |
| (sin fila) | error `Import.Equivalence.Missing`, salvo `sinEquivalencia: "igual"` |

Un mismo código anterior con destinos distintos (salvo todas `una-en-varias`) rechaza la tabla con las filas de cada uno.

## Totales de control

Los nombres de los totales:

- `lineas.leidas`, `lineas.saltadas`, `lineas.conError`, `filas.entregadas`;
- `cuenta:<columna>=<valor>` por cada columna de `totales.agrupar`;
- `suma:<columna>` por cada columna de `totales.sumar`.

Las cifras de referencia son un CSV `total,valor`; el informe marca cada diferencia con **≠**. El cuadre contra la contabilidad se
hace en el ERP (conciliación de Cartera, cuadre de captaciones, informes contables); las diferencias aceptadas, con su motivo, van al
informe de implantación del runbook.

## Pruebas

```bash
python -m unittest discover -s tools/implantacion/pruebas -v
```

Las muestras de `pruebas/muestras/` son sintéticas.
