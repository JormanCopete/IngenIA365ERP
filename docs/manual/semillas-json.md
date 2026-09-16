# Semillas JSON de contabilidad (feature 009)

Los catálogos contables son **datos, no código** (research R9). Viven en
`src/Infrastructure/IngenIA365ERP.Persistence/Seeding/Parametric/Data/*.json`, van incrustados en
el ensamblado de Persistence (`EmbeddedResource`) y los siembran seeders idempotentes por clave
natural al crear una cooperativa y al arrancar (`SeedOrchestrator`). Quien los lee es
`RecursoJson.Leer<T>(nombre)`; quien los revisa es el contador, leyendo el archivo.

| Archivo | Seeder | Orden | Qué siembra |
|---|---|---|---|
| `puc-comercial.json` | `AccountCatalogsSeeder` | 58 | Catálogo PUC comercial (Decreto 2650 de 1993) hasta nivel 4 |
| `puc-solidario.json` | `AccountCatalogsSeeder` | 58 | Catálogo Único de Información Financiera de la Supersolidaria (Resolución 2015110009615) hasta nivel 4 |
| `rubros-niif.json` | `FinancialStatementItemsSeeder` | 59 | Rubros de ESF, ERI, cambios en el patrimonio y flujo de efectivo por grupo NIIF |
| `voucher-types.json` | `VoucherTypesSeeder` | 60 | Tipos de comprobante: manual, reservados por módulo, apertura, cierre, activos |
| `cross-document-types.json` | `CrossDocumentTypesSeeder` | 61 | Tipos de documento cruce |
| `exogena-2026.json` | `ExogenousSeeder` (E4) | 62 | Formatos y conceptos de la exógena de la resolución vigente |

Los archivos admiten comentarios `//` y coma final: se leen con `ReadCommentHandling.Skip` y
`AllowTrailingCommas`. Los enumerados van como texto (`"usage": "Module"`).

## Formato de un catálogo de cuentas

```jsonc
{
  "code": "PUC-COMERCIAL",                 // código del catálogo (ACC_AccountCatalogs.Code)
  "name": "Plan Único de Cuentas para comerciantes",
  "version": "Decreto 2650 de 1993",
  "natures": {
    "default": { "1": "D", "2": "C", "3": "C", "4": "C", "5": "D", "6": "D", "7": "D", "8": "D", "9": "C" },
    "exceptions": { "1199": "C", "1592": "C", "4175": "D" }   // prefijos con naturaleza contraria a su clase
  },
  "niif": [ { "prefix": "11", "item": "ESF-A-EFECTIVO" }, ... ],   // rubro NIIF por prefijo (el más largo gana)
  "accounts": [
    ["1", "ACTIVO"],
    ["11", "DISPONIBLE"],
    ["1105", "CAJA"],
    ["110505", "Caja general"],
    ...
  ]
}
```

- **Nivel** por longitud del código: 1 → clase, 2 → grupo, 4 → cuenta, 6 → subcuenta. No hay
  otras longitudes en un catálogo oficial; las auxiliares (5 y 6) las crea la empresa.
- **Padre**: el código sin sus últimos dígitos (`110505` → `1105` → `11` → `1`) y tiene que existir.
- **Naturaleza**: la de la clase, salvo los prefijos de `exceptions` (deterioros, depreciaciones,
  devoluciones y descuentos, que van contra la naturaleza de su clase).
- **Rubro NIIF**: el `item` del prefijo más largo que coincida; toda cuenta tiene que caer en uno y
  el rubro tiene que existir en `rubros-niif.json`.

La prueba `LasSemillasJsonSonCoherentes` (Application.Tests) comprueba todo esto en cada archivo:
padre existente, longitudes válidas, naturaleza D/C, rubro existente, códigos únicos, conteo mayor
que cero, usos válidos en los tipos de comprobante y un código por módulo.

## Cómo revisa el contador

1. Abre el archivo (cualquier editor de texto; GitHub lo muestra con números de línea).
2. Contrasta contra la norma (`research.md` §fuentes normativas trae la referencia de cada catálogo).
3. Cada corrección es una línea del arreglo `accounts`: código y nombre. Nada más.
4. Deja constancia en el PR: quién revisó, contra qué versión de la norma y qué cambió.

La validación formal dentro del sistema queda en `ACC_AccountCatalogs.ValidatedAt/ValidatedBy`
(FR-006: «catálogo validado por» con fecha) y se hace desde Contabilidad › Catálogos con el
permiso `Accounting.Setup.Manage`.

## Cómo se versiona un catálogo

- Un catálogo oficial que cambia (nueva resolución) es un **archivo nuevo** con otro `version`
  y otro `code` (`PUC-SOLIDARIO-2027`), no una edición del existente: las cooperativas ya
  iniciadas conservan su plan (las cuentas se copian al iniciar, FR-003) y las nuevas eligen.
- Corregir una errata sí edita el archivo: el seeder sólo inserta lo que falta, así que una
  cooperativa ya sembrada recibe la entrada nueva pero **no** cambia una existente (D-10, FR-016
  de la feature 004). Si la corrección es de nombre en una entrada ya sembrada, va por migración
  de datos con la referencia de la resolución.
- El catálogo propio de una cooperativa (importado desde Excel o CSV, U2) no vive aquí: queda en
  `ACC_AccountCatalogs` con `Source = Imported`.
