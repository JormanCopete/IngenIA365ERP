# Cargue de archivos planos para la implantación de COOFLOPAL — informe previo (2026-10-24)

Base: `origin/develop` 511e4cc9 (trae 012, 013 y 014). Número de feature: **016** (015 está en la rama `015-recaudo-tesoreria`).

## 1. Inventario de lo que ya existe

Mecánica común (`Application/Common/Imports`, feature 012): `DefinicionDePlantilla`, `EjecutorDeImportacion`,
`ContextoDeImportacion`, `FilaDeImportacion`, `ImportErrors`; `.xlsx` o `.csv` (una hoja `Datos`), **encabezados en la fila 1**,
modos `Review` (no guarda, 200 con `valid=false`, libro de revisión con `resultado`/`errores`) y `Apply` (**todo o nada**, 422
`Import.Invalid`), `ErrorDeFila { Sheet, Row, Column, Code, Message }`, llave = código (crea/actualiza/sin cambio, nunca borra),
motivo cuando crea vigencias, idempotencia, auditoría `Import.Reviewed/Applied` con SHA-256; un único componente
`ImportarPlantilla.razor`. **No lee TXT de ancho fijo ni CSV con otros nombres/orden de columnas: exige las columnas exactas.**

| Pieza | Entidad | Formato | ¿Sirve a COOFLOPAL? |
|---|---|---|---|
| `ImportAccountsCommand` (009) | auxiliares del plan | xlsx, fila 1, todo-o-nada, `data.errors[]{row,column}` | sí, con códigos nuevos (hoy C07 del plan) |
| `Accounting/Opening` (009) | saldos de apertura `AP` (tercero = persona, documento cruce) | ídem; fecha elegible hasta fin del 1.er ejercicio | sí; exige personas y códigos nuevos |
| Plantillas 012 (16: impuestos … matriz) + `GoLive` (saldo inicial, cifras de referencia) | inventario comercial | mecánica común | sí (M03/M09 del plan) |
| `lending.*` (013): líneas, vinculados, saldos de aportes, 17 partes de migración, cobros provisionales, pagadurías, historial de mora | cartera de créditos | mecánica común + cargas por partes y tandas, cuadre contra Contabilidad, aceptación de diferencias con acta | sí; **ninguna crea personas** |
| Migración de captaciones (014) `contribution-accounts` y demás | aportes (COOFLOPAL sólo aportes) | ídem; cuadre y cifras de referencia (`CifrasDeReferencia.cs`) | sí; la cuenta de aportes exige **asociado vigente** |
| Saldos iniciales de prestaciones (`UpsertBenefitBalanceCommand`) | nómina | **uno a uno**, sin plantilla | empleados ya están; fuera de alcance por decisión |
| `ImportNoveltiesCommand` | novedades de nómina | plantilla | ya en uso |
| Listas restrictivas, valores nacionales, medios de pago, impuestos | plataforma/core | mecánica común | sí |
| `database/migration/` | SQL directo desde la base del sistema anterior | **congelado** (README-CONGELADO) | no |

## 2. Brechas

1. **Personas y terceros**: no hay plantilla (`CreatePersonCommand` uno a uno). Todo depende de esto: apertura (tercero),
   cartera, aportes, vinculados. Volumen COOFLOPAL: por confirmar (asociados + proveedores + entidades).
2. **Asociados**: no hay plantilla (`RegisterAssociateWithPersonCommand` uno a uno).
3. **Archivos tal como salen**: ninguna ruta acepta TXT de ancho fijo ni CSV con columnas propias → falta un **perfil de lectura**
   configurable (archivo → columnas de la plantilla destino).
4. **Códigos del sistema anterior**: ningún cargue traduce cuentas, líneas, oficinas, conceptos → faltan **tablas de equivalencias**
   por cooperativa (la contadora ya las diligencia en el formato de Contabilidad, C06/C45–C47).
5. **Orquestación y cuadre global**: cada módulo tiene su pantalla; no hay vista única del orden, estado y totales de control vs.
   cifras de referencia y vs. balance de apertura.
6. Fuera de alcance por decisión: fichas de empleados (ya creadas), saldos de prestaciones y acumulados 2026.

## 3. Orden de cargue propuesto

0. Equivalencias (cuentas, oficinas, líneas, ciudades, tipos de documento…) y perfiles de lectura.
1. Personas y terceros (naturales, jurídicas; proveedor/cliente; cuenta bancaria).
2. Asociados (sobre personas vivas).
3. Auxiliares del plan (códigos traducidos).
4. Apertura contable `AP` al corte (terceros = personas) → contabilizada.
5. Vinculados y saldos de aportes para cupo (013).
6. Carga de migración de cartera (créditos, cuotas, saldos, mora… al corte) → cuadre → acta → activación.
7. Carga de aportes (014) → cuadre → activación (después de créditos, `LoansFirst`).
8. Inventario: plantillas 1–16 de la 012 (si aplica a la salida).
9. Cuadre global: totales de control vs. cifras de referencia y vs. balance de apertura.

## 4. Decisiones del dueño (formulario, 2026-10-24)

- Origen: **archivos planos tal como salen** del sistema anterior (CSV/TXT) → perfiles de lectura configurables.
- Quién carga: **ambos, misma API** (pantalla para COOFLOPAL; soporte por las mismas rutas).
- Ensayo: **QA con copia de producción** de COOFLOPAL, **tal cual, acceso restringido** al equipo de implantación y borrada al terminar con constancia.
- Validación: **totales de control vs. cifras de referencia + cuadre contra contabilidad**; diferencias se informan y se aceptan con motivo, no bloquean.
- Alcance: **personas y terceros, asociados, archivos planos para lo existente** (apertura, partes de cartera, aportes). Empleados ya están; prestaciones/acumulados fuera.
- Equivalencias: **sí, traducir al cargar**, con tablas por cooperativa cargables desde el formato de la contadora.
- **Tablero de implantación** con orden, estado, totales y aviso informativo de pasos previos faltantes.
- Muestras: el dueño deja muestras de cada archivo en una carpeta fuera del repo; los perfiles concretos se cargan como dato.
