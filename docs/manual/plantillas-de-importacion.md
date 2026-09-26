# Plantillas de importación: la mecánica común y cómo se agrega una

> Feature 012 (inventario comercial), fase 3 y entrega I1 (T49, FR-030, FR-095). **El orden de las
> plantillas, sus hojas, sus columnas y la forma de la respuesta los fija
> [contracts/plantillas.md](../../specs/012-inventario-comercial/contracts/plantillas.md), que es la
> única fuente**: aquí no se repiten. Esto es la receta para escribir una plantilla nueva o corregir una.

Una cooperativa arranca con miles de productos, decenas de bodegas, sus impuestos y sus tipos de
documento. Nadie los digita uno a uno. La feature 009 ya había resuelto la carga masiva de auxiliares y
de saldos de apertura con una mecánica propia (encabezados en la fila 1, todo o nada, errores por fila y
columna); la 012 la volvió **una infraestructura común** en `Application/Common/Imports`, con revisión
previa, y dieciséis plantillas encima.

## 1. La regla en una frase

**La plantilla aplica las mismas reglas que el alta unitaria, y el ejecutor decide si eso se guarda.**
Una fila importada no puede pasar lo que el formulario rechaza, ni al revés: la plantilla llama a la
regla del alta (por ejemplo, `CreateWarehouseCommandHandler.AplicarReglasAsync`) en vez de copiarla, y
los errores de negocio salen con el **mismo código** que en pantalla.

## 2. Lo que es igual para todas

**El archivo.** `.xlsx`, o `.csv` si la plantilla tiene una sola hoja (que se llama `Datos`). Cada hoja
de datos lleva **sólo los encabezados en la fila 1** —en `camelCase` español, como los escribe la
definición— y las filas debajo; el título, las reglas y un ejemplo por columna van en otra hoja,
«Instrucciones», que el lector no mira. Hasta 16 MB y 60.000 filas por hoja.

**Dos modos** (`ModoDeImportacion`, viaja por nombre):

- **`Review`** corre exactamente las mismas reglas que `Apply` y **no guarda nada**: el ejecutor deshace
  lo que la plantilla dejó en el contexto. Responde 200 aunque haya errores (`valid = false`). Con
  `format=xlsx` devuelve el mismo libro con dos columnas al final, `resultado` (Crear, Actualizar, Sin
  cambio) y `errores`, para corregir sobre el mismo archivo.
- **`Apply`** es **todo o nada**: `TransaccionExplicita`, un solo `SaveChanges`. Con un error no se guarda
  nada y responde 422 `Import.Invalid` con el mismo cuerpo en `data`.

Sin `mode`, 400 `Import.ModeRequired`. Revisar y aplicar son **dos operaciones con dos claves** de
idempotencia: la misma clave con otro modo es otro contenido (`Operation.KeyReused`).

**Errores por hoja, fila y columna** (`ErrorDeFila { Sheet, Row, Column, Code, Message }`): la fila es la
de Excel (el encabezado es la 1); `Row = 0` es un error que no es de una fila (el motivo, una hoja
entera). Se juntan **todos** —la plantilla sigue leyendo después del primero— y se devuelven hasta 1.000,
ordenados; el resto va en el libro de la revisión. `ErrorDeFila` nació en la 009 y se mudó aquí; en las
plantillas de una sola hoja `Sheet` es nulo y no viaja, así que lo que responde la 009 no cambió.

**La respuesta** es `ImportResultDto` (§0.5 del contrato): plantilla, modo, válido, aplicado, nombre y
SHA-256 del archivo, si pide motivo, conteos por hoja (creadas, actualizadas, sin cambio), hasta 5.000
cambios campo a campo con su antes y después, avisos, errores y `extra` con lo propio de cada plantilla.

**Llave y actualización.** El código (o la llave compuesta de la plantilla) identifica la fila: si
existe, actualiza; si queda igual, «sin cambio». El archivo es idempotente: se corrige y se vuelve a
subir sin duplicar. **La plantilla nunca borra** lo que no viene, y los códigos no se renombran.

**Motivo.** Si el archivo crea o cierra una vigencia (un parámetro, una política, una tarifa) o cambia
algo que exige decir por qué, la revisión responde `requiresReason = true` y `Apply` exige `reason`, que
se copia a cada vigencia y al evento de auditoría. Sin él, `Import.Cell.Required` sin fila en la columna
`reason`.

**Permisos por hoja o columna.** Una hoja o columna que exige un permiso que la persona no tiene y trae
datos: `Import.Cell.PermissionRequired` (§0.7 del contrato).

**Auditoría.** El ejecutor registra `Import.Reviewed` o `Import.Applied` (plantilla, modo, nombre y SHA-256
del archivo, conteos) en el módulo de la plantilla; en `Apply`, dentro de la misma transacción, y el
interceptor deja el antes y el después de cada entidad. **El archivo no se guarda.**

**Descarga.** `GET {base}/template.xlsx` da el libro vacío (con el permiso de consulta del área); con
`?withData=true`, el mismo libro lleno con lo que hoy tiene la cooperativa —exige además el permiso de
exportar y, si trae datos personales, `Inventory.Reports.ExportPersonalData`— y se audita como
exportación. Así se exporta, se corrige y se vuelve a importar con el mismo libro.

## 3. Las piezas

| Pieza | Qué hace |
|---|---|
| `DefinicionDePlantilla` / `HojaDePlantilla` / `ColumnaDePlantilla` | la forma: hojas, columnas con su `TipoDeValor`, obligatoria, largo, permiso, reglas y ejemplo. La **misma** definición arma el libro vacío, su hoja «Instrucciones» y lo que el lector exige |
| `EjecutorDeImportacion` | el motor común: lee y comprueba hojas y encabezados, corre la plantilla, deshace o guarda, audita, arma la respuesta |
| `ContextoDeImportacion` / `HojaDeImportacion` | lo que ve la plantilla mientras corre: las hojas leídas, el modo, el motivo, los permisos, y dónde dejar errores, avisos y el resultado de cada fila (`Registrar`) |
| `FilaDeImportacion` | la conversión de cada tipo (`Codigo`, `Texto`, `Entero`, `Monto` 2 decimales, `Cantidad` 4, `Costo` 6, `Porcentaje` en puntos devuelto como fracción, `Fecha`, `Hora` de Colombia, `SiNo`, `SiNoIndiferente`, `Referencia` a un catálogo citado) con su error si la celda no cumple |
| `IComandoDeImportacion` | lo común de todo comando: `Mode`, `File` (`ArchivoDeImportacion`, cuyo contenido no viaja al JSON: va su SHA-256), `Reason`, `OperationKey` |
| `ImportErrors` | los códigos `Import.*`; no hay códigos de importación propios de cada área |
| `CatalogoDePlantillas` (`Application/Inventory/Imports`) | la **única lista** de las dieciséis, en el orden de carga, con su ruta, su comando, desde qué entrega importa y descarga, y sus permisos. La leen `GET /api/inventory/templates` y cada `MapPlantilla` |
| `RutasDePlantilla.MapPlantilla` (API) | publica las dos rutas sobre la base del catálogo |
| `PlantillaDeImportacion` (`API/Reports/Exportadores`) | arma el libro: encabezados en la fila 1, formato de celda por tipo (`@` en códigos, `0.00` en montos, `0.0000` en cantidades, `0.000000` en costos), la hoja «Instrucciones», la descarga con datos y el libro de la revisión |
| `ImportarPlantilla.razor` (`Shared/Components/Inventario`) | el **único** componente de pantalla: descargar, subir, revisar, pedir motivo, aplicar. Ninguna historia crea otro |

## 4. Cómo se agrega una plantilla

1. **El contrato primero.** Agregar o corregir su sección en `contracts/plantillas.md`: número en el orden
   de §0.1 (cada plantilla sólo cita lo que cargaron las anteriores), hojas, columnas, llave, reglas,
   `extra`, permisos. El código sigue al contrato, no al revés.
2. **La definición**, junto a las demás del área (`PlantillasDelCatalogo.cs`, `PlantillasDePuestaEnMarcha.cs`…):
   una clase estática con las constantes de hoja y columna y la `DefinicionDePlantilla` con su módulo de
   auditoría (`ModuloDeAuditoria`).
3. **La entrada** en `CatalogoDePlantillas.Todas`: número, definición, ruta base, nombre del comando,
   entrega desde la que importa y desde la que descarga, permisos de descarga e importación y los
   adicionales como texto.
4. **El comando**, en el molde de `ImportWarehousesCommand`:

   ```csharp
   public sealed record ImportMiCatalogoCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
       : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
   {
       public Guid OperationKey { get; init; }
   }

   public sealed class ImportMiCatalogoCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor)
       : IRequestHandler<ImportMiCatalogoCommand, Result<ImportResultDto>>
   {
       public Task<Result<ImportResultDto>> Handle(ImportMiCatalogoCommand request, CancellationToken ct) =>
           ejecutor.EjecutarAsync(PlantillaDeMiCatalogo.Definicion, request, ProcesarAsync, ct);

       private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
       {
           // 1. Cargar EN BLOQUE los catálogos que el archivo cita (nunca una consulta por fila).
           // 2. Recorrer ctx.Datos.Filas (o ctx.Hoja("…").Filas): leer cada celda con FilaDeImportacion;
           //    hoja.LlaveUnica(...) para los repetidos del archivo.
           // 3. Aplicar LA REGLA DEL ALTA UNITARIA; su error va a la fila con el mismo código.
           // 4. Agregar o modificar entidades en el contexto, SIN guardar, y ctx.Registrar(fila, clave, acción, campos).
           // 5. ctx.PedirMotivo() si la fila crea o cierra una vigencia.
       }
   }
   ```

   Si la plantilla necesita los Id recién creados (una vigencia con el Id de la bodega nueva), pasa un
   `despuesDeGuardar`: el ejecutor guarda, lo corre y vuelve a guardar **en la misma transacción**.
5. **Las rutas**, sobre el grupo del catálogo:

   ```csharp
   g.MapPlantilla(permisoDeVer, permisoDeAdministrar, PlantillaDeMiCatalogo.Clave, "Inventory_MiCatalogo",
       importar: (modo, archivo, motivo, clave) => new ImportMiCatalogoCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave },
       datos: () => new GetMiCatalogoTemplateDataQuery(),       // opcional: la descarga con datos
       permisoDeExportacion: CatalogEndpoints.Exportar);
   ```

   Sin `importar`, sólo se publica la descarga (así están las plantillas 10 a 13 en I1: se descargan desde
   ya y se importan con I3). Sin `datos`, `?withData=true` es el 404 de lo inexistente.
6. **La pantalla** pone `<ImportarPlantilla>` con la ruta base y los permisos; no escribe otro componente.
7. **Pruebas**: una de Application por regla (revisión sin guardar, todo o nada, llave repetida, celda con
   formato, permiso por columna) y, si la plantilla es de puesta en marcha, una e2e que suba el libro.

## 5. Las dieciséis, en I1

Todas publican su descarga en I1. Importan en I1 la 1 a la 9 (impuestos, grupos contables, unidades,
marcas, categorías, productos, bodegas, tipos de documento, vendedores), la 14 (saldo inicial) y la 15
(cifras de SOLIDO); la 10 a la 13 (puntos de venta, medios de pago, listas de precios, topes) importan con
I3; la 16 (matriz contable) descarga e importa con I2. El detalle y la receta de uso, en
[inventario-puesta-en-marcha.md](../operaciones/inventario-puesta-en-marcha.md) y en la pantalla
`/inventario/plantillas`.

## 6. Diferencias con lo que el plan decía

- **La 009 no se pasó a esta mecánica.** La carga de auxiliares (`ImportAccountsCommand`), la apertura
  contable (`OpeningCommands`) y los catálogos contables siguen con su lector propio y comparten sólo
  `ErrorDeFila`. No tienen modo `Review` ni `ImportResultDto`. Pasarlas es trabajo aparte.
- **El catálogo de plantillas vive en Inventario.** `CatalogoDePlantillas` está en
  `Application/Inventory/Imports` y `MapPlantilla` lo consulta por clave; las dos plantillas de Core
  (impuestos y medios de pago) están registradas allí. Un módulo que no sea del comercio y quiera
  plantillas tiene que registrarlas en ese catálogo o sacar el catálogo a `Application/Common/Imports`.

## 7. Qué NO hacer

- No reescribir en la plantilla una regla del alta: llamarla.
- No consultar la base por cada fila: cargar los catálogos citados en bloque.
- No guardar desde la plantilla; no abrir transacción.
- No crear códigos de error `…Import…` propios del área.
- No leer el archivo con otro lector ni poner títulos encima de los encabezados.
- No escribir las columnas de una plantilla en otro sitio que su definición: el libro que se descarga y
  lo que el importador exige salen de la misma.
