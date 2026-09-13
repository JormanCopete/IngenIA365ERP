# Indicador de carga por zona (`IndicadorDeCarga` + `EstadoDeCarga`)

**Qué es**: la forma única de decir «esto se está cargando» o «esto se está guardando» en
una pantalla. Envuelve la zona que espera (una grilla, el formulario de un diálogo, un
panel) y, mientras dura la espera, la atenúa, bloquea sus clics y pinta un spinner con un
texto encima. La persona ve **que** algo pasa y **dónde**; no puede hacer doble clic en
«Guardar» ni leer una tabla vacía creyendo que no hay registros.

**Desde el 2026-09-13 está en todo Core (Maestros) y toda Nómina**. Los demás módulos
(Contabilidad, Cartera, Inventario, Tesorería, CDT, Débito, Asociados, Compliance,
Administración, Seguridad, Reportes) se migran con este mismo documento; no hay que
inventar nada.

## Las dos piezas

| Pieza | Dónde | Qué hace |
|---|---|---|
| `IndicadorDeCarga` | `Shared/Components/Shared/IndicadorDeCarga.razor` | Componente visual. Parámetros: `Cargando` (bool), `Texto` («Cargando…» por defecto), `Compacto` (spinner chico sin texto, para barras y selectores), `ChildContent`. |
| `EstadoDeCarga` | `Shared/Services/EstadoDeCarga.cs` | La bandera. `Iniciar()` la enciende y la apaga sola al salir del `using`, con o sin excepción. Cuenta anidamientos: dos cargas solapadas mantienen el indicador hasta que termina la última. |

Los estilos viven en `wwwroot/css/componentes.css`, sección «Indicador de carga por zona»
(`.zona-cargable`, `.esta-cargando`, `.zona-cargable-velo`). **Ninguna pantalla declara
estilos ni colores propios** para esto.

## Cómo se aplica a una pantalla (receta)

1. **Declarar las banderas**, una por zona independiente:

   ```csharp
   // Indicadores de carga por zona (ver docs/manual/indicador-de-carga.md).
   private readonly EstadoDeCarga _carga = new(), _guardado = new();
   ```

2. **Envolver la zona que espera**:

   ```razor
   <IndicadorDeCarga Cargando="@_carga.Activa">
       <SfGrid TValue="Item" DataSource="@_items" ...> ... </SfGrid>
   </IndicadorDeCarga>
   ```

3. **Encender la bandera en el método que carga**, con `using` como primera línea:

   ```csharp
   private async Task LoadData()
   {
       using var carga = _carga.Iniciar();          // se apaga al salir, pase lo que pase
       try { _items = await Http.GetListAsync<Item>("/api/..."); }
       catch (Exception ex) { _items = []; await Notification.ErrorAsync($"No se pudo cargar la lista: {ex.Message}"); }
   }
   ```

4. **El guardado, igual**: el contenido del diálogo envuelto con `Texto="Guardando…"`, el
   botón deshabilitado y el `using` en `Save()`:

   ```razor
   <DialogTemplates>
       <Content>
           <IndicadorDeCarga Cargando="@_guardado.Activa" Texto="Guardando…">
               ... el formulario ...
           </IndicadorDeCarga>
       </Content>
   </DialogTemplates>
   <DialogButtons>
       <DialogButton Content="Guardar" IsPrimary="true" OnClick="@Save" Disabled="@_guardado.Activa" />
   </DialogButtons>
   ```

   ```csharp
   private async Task Save()
   {
       using var guardado = _guardado.Iniciar();
       ...
   }
   ```

5. **Si la pantalla ya tiene una bandera booleana** (`_ocupado`, `_guardando`, `_saving`)
   que se enciende y apaga en `try/finally`, no hace falta duplicarla: se enlaza tal cual
   (`Cargando="@_guardando"`). Sólo se reemplaza por `EstadoDeCarga` cuando se vaya a
   reescribir el método.

## Regla para pantallas nuevas o modificadas

**Toda pantalla que se cree o se toque lleva el indicador donde espere datos**: la
grilla o lista al abrir, el formulario mientras guarda, cualquier panel que pida algo al
servidor. Es una compuerta más del *Constitution Check* del plan de cada feature
(`.specify/templates/plan-template.md`, fila «UI») y la vigila la prueba de arquitectura
`tests/IngenIA365ERP.Architecture.Tests/Principles/LasPantallasDicenQueEstanCargando.cs`:
en los módulos ya migrados (`ModulosMigrados`), una pantalla con `<SfGrid` o
`<DialogTemplates>` sin `<IndicadorDeCarga` rompe el CI. Al migrar otro módulo se agrega
su carpeta a esa lista y la regla pasa a cubrirlo.

## Reglas

- **Una zona por cosa que se carga aparte.** Una lista y su diálogo son dos zonas; dos
  listas en dos pestañas son dos zonas. Un solo velo sobre toda la página esconde qué
  espera y bloquea lo que sí se podría usar.
- **El texto dice lo que pasa**: «Cargando…», «Guardando…», «Calculando…», «Anulando…»,
  «Importando…». Nunca «Espere».
- **`Compacto` para barras y selectores** (el selector de período de nómina, filtros de
  una toolbar): spinner de 22 px, sin texto, `min-height` de 36 px.
- **No reemplaza al velo global** de `MainLayout` (`ILoadingService`, clase
  `.loading-overlay`), que tapa toda la pantalla. Ese se reserva para lo que no admite
  seguir trabajando mientras corre (aprobar una nómina, un cierre). Hoy no lo usa nadie.
- **Errores visibles** (Principio IX): al tocar la carga de una pantalla, el
  `catch { _items = []; }` silencioso pasa a avisar con `Notification.ErrorAsync`. Un
  indicador que desaparece sin datos y sin mensaje es peor que ninguno.
- **Cargas en paralelo** (`Task.WhenAll`): cada rama abre su propio `using` sobre la misma
  bandera; el contador la mantiene encendida hasta la última. No hace falta coordinar nada.

## Cómo se aplicó en Core y Nómina (para repetirlo en otro módulo)

Las 19 pantallas de Maestros y los 13 catálogos/parámetros de Nómina tienen la misma
forma (grilla + diálogo con `LoadData()`/`Save()`), así que se transformaron con un guion
que hace exactamente los pasos 1–4 y avisa lo que no reconoce; las cinco pantallas
grandes de Nómina (Conceptos, Liquidación, Novedades, Empleados, Empleado detalle) y el
`SelectorDePeriodo` se hicieron a mano con una bandera por lista y el velo de cada diálogo
enlazado a su `_ocupado`/`_guardando` existente. El guion sirve de plantilla para el
siguiente módulo: busca la primera `<SfGrid>`, el `@code {`, `LoadData`/`CargarAsync`,
`Save`/`Guardar`, el `DialogButton Content="Guardar"` y el primer `<Content>` de
`<DialogTemplates>`; lo que no encaje se hace a mano.

## Cómo comprobar

- En una conexión lenta (DevTools → Network → Slow 3G) la grilla aparece atenuada con el
  spinner y «Cargando…» hasta que llegan los datos; al guardar, el formulario se atenúa,
  el botón queda inactivo y dice «Guardando…».
- Con la API apagada: la zona se destapa **y** aparece el aviso de error; nunca queda un
  spinner infinito.
- Cambio de tema (claro/oscuro/contraste): el velo no declara colores; hereda.
