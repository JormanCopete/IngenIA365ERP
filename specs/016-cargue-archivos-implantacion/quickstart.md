# Quickstart — 016

Cómo comprobar la feature de punta a punta en local, con **muestras sintéticas** (no hay muestras reales todavía). Requisitos:
015 y 017 ya en `develop` y esta rama al día; API y Web corriendo contra el PostgreSQL local; una cooperativa de prueba con la
contabilidad iniciada; un usuario `CompanyAdmin` con segundo factor.

Las muestras viven en `tests/IngenIA365ERP.Application.Tests/Implantacion/Muestras/`:

| Archivo | Qué es |
|---|---|
| `terceros-ancho-fijo.txt` | 5 personas, windows-1252, 2 líneas de título y una de totales, una con fecha `31022020` |
| `asociados.csv` | 10 asociados, «;», fechas `dd/MM/yyyy`, uno con documento inexistente |
| `equivalencias-contadora.xlsx` | la hoja «Equivalencias» con una «una en varias» y una «no se migra» |
| `apertura.csv` | saldos por cuenta anterior, tercero y documento cruce |
| `creditos.txt`, `cuotas.txt` | dos partes de la carga de Cartera |
| `aportes.csv` | la carga de aportes |
| `usuarios.xlsx` | el formato de usuarios v2 con 5 filas, una con la confirmación distinta y un cargo sólo en «Cargos» |

## 1. Equivalencias y perfiles (US2)

1. `/implantacion/equivalencias` → nueva tabla `CIUDADES` (tipo ciudad) y `TIPODOC` (tipo de documento, con `PPT → PT`).
2. Nueva tabla `CUENTAS` (tipo cuenta) → «Cargar del formato de la contadora» con `equivalencias-contadora.xlsx` → revisar: la
   «una en varias» queda como clase, la cuenta que no existe en el plan como aviso. Aplicar.
3. `/implantacion/perfiles` → nuevo perfil `TERCEROS`, destino «Personas y terceros», ancho fijo, windows-1252, saltar 2 líneas y las
   que empiezan por `TOTAL`. Quitar el mapeo de `primerApellido` y guardar: se rechaza nombrando la columna. Volverlo a poner.
4. Vista previa con `terceros-ancho-fijo.txt`: la línea de la fecha imposible sale con línea y posición; las tildes se ven bien.
   Cambiar la codificación a `utf-8` y repetir: aparece el aviso de caracteres no reconocidos.

## 2. Personas y asociados (US1, US3)

1. `/implantacion` (tablero) → paso «Personas» → «Cargar con perfil» → `TERCEROS` → revisar. Errores con su línea original, nada
   guardado; el avance se ve mientras corre.
2. Corregir la muestra, revisar (válido, 5 a crear) y aplicar. Las 5 personas en Maestros › Personas, como las del alta unitaria.
   La versión del perfil queda sellada: editarlo crea la versión 2.
3. Paso «Asociados» con `asociados.csv` y un perfil CSV: la fila del documento inexistente dice «esta plantilla no crea personas» y
   `apply` no guarda nada. Quitarla y aplicar: 9 asociados con su marca.

## 3. Cargas que ya existen (US4)

1. Perfil `APERTURA` sobre `accounting.opening`, cuenta por la tabla `CUENTAS`. La fila de la cuenta «una en varias» pide decidir.
2. Perfiles de las partes `loans` e `installments` y de aportes. Comparar el resultado con el de subir el mismo contenido en la
   plantilla Excel de siempre: mismos errores, conteos y cuadre (SC-004).

## 4. Tablero (US5)

1. `/implantacion/cortes` → corte «27/11» vigente → cifras: personas 5, asociados vigentes 10 (distinta a propósito).
2. El tablero muestra «Asociados» con la diferencia en rojo → aceptarla con motivo → «con diferencias aceptadas», evento en la
   auditoría. Un total sin cifra dice «sin cifra».

## 5. Configuración (US6)

Exportar la configuración, crear otra cooperativa de prueba, importarla: perfiles con las mismas versiones, tablas y cifras; la misma
muestra da la misma revisión.

## 6. Invitaciones masivas (US7)

1. Seguridad › Usuarios → «Invitar desde archivo» → `usuarios.xlsx` → revisar: la confirmación distinta da `Identity.Document.Mismatch`
   en «Confirmar número de documento»; nada sale.
2. Corregir y aplicar: 5 invitaciones pendientes con el mismo mensaje en las 5 filas, el cargo nuevo en el catálogo.
3. Activar una invitación digitando mal el documento tres veces: la membresía queda «en revisión», como en la 017.

## 7. Pruebas

```bash
dotnet test tests/IngenIA365ERP.Domain.Tests --filter "FullyQualifiedName~Implantacion"
dotnet test tests/IngenIA365ERP.Application.Tests --filter "FullyQualifiedName~Implantacion"
dotnet test tests/IngenIA365ERP.Architecture.Tests
# al final, una sola vez, en los dos motores:
DB_PROVIDER=PostgreSql dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Implantacion"
DB_PROVIDER=SqlServer  dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Implantacion"
```
