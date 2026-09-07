# Contrato de pantallas — Nómina (Feature 005)

Blazor en `src/Presentation/IngenIA365ERP.Shared/Pages/Nomina/`. Sistema de diseño:
`tokens.css` + `componentes.css`; **ningún color literal ni `<style>` propio**.
Grillas y diálogos con SyncFusion como los maestros (`SfGrid`, `SfDialog`,
`ConfirmDialog`). Empleado se elige con `PersonSearchPicker` filtrado a
`IsEmployee`. Toda llamada a la API pasa por el cliente tipado `NominaClient`
(`Shared/Services/Nomina/NominaClient.cs`, token desde `CentralAuthClient`);
errores con `Notification.ShowErrorAsync(res, fallback)`; anti doble clic con
`_saving`. Validación de formularios con `DataAnnotations` (Principio VIII, lado
cliente). Cada pantalla se registra en `ManualCatalogo` con su guía.

Todas exigen cooperativa activa; el menú las muestra sólo con `_tieneCooperativa`.

| Ruta | Pantalla | Permiso mínimo para ver |
|---|---|---|
| `/nomina/planes` | Planes de nómina | `Payroll.Plans.View` |
| `/nomina/periodos-pago` (existente, ampliada) | Períodos de pago | `Payroll.PayrollPeriods.Read` |
| `/nomina/novedades` | Novedades del período | `Payroll.Novelties.View` |
| `/nomina/liquidacion` | Liquidación del período | `Payroll.Runs.View` |
| `/nomina/conceptos` (existente, reescrita) | Conceptos de nómina | `Payroll.Concepts.View` |
| `/nomina/parametros-legales` | Parámetros legales | `Payroll.LegalParameters.View` |
| `/nomina/empleados/{id}` (existente, ampliada) | Detalle del empleado: pestaña «Retención y plan» | `Payroll.Employees.Read` |

---

## 1. `/nomina/novedades`

**Cabecera**: `SelectorDePeriodo` (plan → período; si hay un solo plan no se muestra
el plan; por defecto el período abierto más reciente). Pastilla con el estado del
período. Si el período está aprobado, la pantalla es de sólo consulta y el botón
«Nuevo» se convierte en «Registrar ajuste en el siguiente período».

**Barra**: «Buscar» (empleado o concepto), filtros por tipo, estado y origen,
«Nuevo», «Cambio de salario», «Importar», «Recurrentes».

**Grilla**: empleado, concepto, cantidad/valor, fechas, días en período, valor
estimado, origen, estado; acciones lápiz (corregir) y papelera (anular), ambas piden
motivo. Fila `Superseded` o `Cancelled` en texto tenue con enlace «Historial».

**Diálogo «Nueva novedad»**: empleado (picker), concepto (lista de vigentes
aplicables a la clase del empleado, agrupada por naturaleza), y según el concepto:
cantidad, valor, fechas; observación. Muestra el **valor estimado** al vuelo
(llamada a `dry-run` del concepto con la novedad). Mensajes del servidor tal cual.

**Diálogo «Cambio de salario»**: empleado, salario nuevo, fecha de efecto, motivo;
muestra el historial de salarios.

**Diálogo «Importar»**: enlace «Descargar plantilla», selector de archivo, resultado
en tabla (fila, columna, error) o «18 novedades aplicadas».

**Pestaña «Recurrentes»**: lista y alta (empleado, concepto, valor o cantidad, desde,
hasta o número de cuotas), desactivar con motivo.

## 2. `/nomina/liquidacion`

**Cabecera**: `SelectorDePeriodo` + tarjeta de estado: `Abierto sin cálculo` /
`Borrador v2 (calculado por … a las …)` / `Desactualizado: hay cambios desde el
cálculo` / `Aprobado por … el …` / `Reversado`. Botones según estado y permiso:
**Calcular**, **Recalcular**, **Aprobar**, **Reversar**, **Exportar**.

**Pestañas**:
1. **Resumen**: totales (devengado, deducido, aportes, provisiones, neto, ajuste de
   redondeo) y tabla por concepto. Lista de **bloqueos** con empleado y causa.
2. **Empleados**: grilla con días, devengado, deducido, neto, banderas y «cambió»
   respecto del borrador anterior; filtros por bandera. Clic abre el **detalle**.
3. **Comparativo**: neto anterior, actual, variación; resaltado sobre el umbral;
   filtro «sólo variaciones».
4. **Cuadre**: las tres verificaciones con ✓/✗ y el detalle.
5. **Relación de pago** (sólo aprobado): empleado, neto, medio, banco, cuenta,
   estado de pago; «Marcar pagados» (todos o seleccionados: fecha, medio,
   referencia), «Retirar marca» (motivo); «Comprobantes»: descargar uno, todos, o
   «Enviar por correo» (seleccionados o todos) con resultado (enviados, fallidos,
   sin correo) y pestaña de envíos.

**Detalle del empleado** (`SfDialog` ancho o panel lateral): tramos de salario, y
cada línea con `ExplicacionDeLinea`: forma, pasos (etiqueta → valor), parámetro con
vigencia, novedad de origen (enlace a Novedades). Botón «Comprobante PDF».

**`PanelDeAprobacion`** (diálogo): resumen de totales, lista de bloqueos; para cada
bloqueo, casilla «Autorizar excepción» + motivo (visible sólo con
`Payroll.Runs.AuthorizeException`); texto de confirmación explícito («Aprobar el
período septiembre 2026 con 47 empleados y neto $…»); segunda confirmación si la
cooperativa permite aprobar sin segregación (el diálogo lo dice).

**Reversar**: diálogo con motivo obligatorio y la advertencia de lo que ocurre;
bloqueado si hay pagos marcados, mostrando cuáles.

## 3. `/nomina/conceptos`

Pestañas **Definiciones** (vigentes; filtro por naturaleza y origen; badge «Semilla»,
«Propio», «Traducido»), **Versiones** (por código), **Catálogo heredado**
(sólo lectura, con botón «Traducir a definición» que abre el formulario prellenado con
lo traducible y deja el resto vacío, sin inventar).

**Formulario de definición**: código (inmutable al revisar), nombre, naturaleza, forma
de cálculo —el formulario cambia los campos según la forma—, bases que afecta,
prestacional, admite repetirse, topes, clases aplicables, requiere fechas/cantidad/
valor, vigencia desde. Sección **Cuentas contables** (`AccountSearchDialog`) por
centro de costo. Botón **«Probar en seco»**: elige empleado y período y muestra el
valor y la explicación sin guardar. Desactivar cierra la vigencia.

Al guardar una revisión de un concepto con borrador vivo, aviso: «El borrador del
período X quedará desactualizado».

## 4. `/nomina/parametros-legales`

Grilla por código con la vigencia actual y las anteriores; «Nueva vigencia» (desde,
valor o tabla de rangos con editor de filas, fuente normativa). Alerta arriba si algún
código requerido no tiene vigencia para el año en curso o el siguiente (el mismo
chequeo que hace el cálculo).

## 5. `/nomina/planes`

Maestro simple: código, nombre, periodicidad, por defecto, activo, empleados. Con un
solo plan, aviso «Todos los empleados están en el plan por defecto; creá otro sólo si
un grupo se paga con otra periodicidad».

## 6. Detalle del empleado — pestaña «Retención y plan»

Plan de nómina y fecha de efecto del cambio; procedimiento de retención (1 ó 2);
tabla de porcentajes con vigencia (si 2); deducciones y rentas exentas declaradas con
vigencias. Guardar exige `Payroll.Employees.Update`.

## 7. Menú

Grupo **Nómina**: Empleados, Novedades, Liquidación, Períodos de Pago, Planes de
nómina, Conceptos, Parámetros legales, …maestros existentes…, subtítulo «Reportes»
(Comprobante de nómina). El botón «¿Cómo se hace?» resuelve cada ruta a su guía.

## 8. Componentes nuevos (Shared/Components/Nomina/)

- `SelectorDePeriodo.razor`: plan (si hay más de uno) → período; emite `PeriodoCambiado`.
- `ExplicacionDeLinea.razor`: renderiza `ExplanationJson` como lista de pasos.
- `PanelDeAprobacion.razor`: bloqueos, excepciones, confirmaciones.
- `TablaDeRangos.razor`: editor de filas `desde / hasta / tarifa / fijo`.
