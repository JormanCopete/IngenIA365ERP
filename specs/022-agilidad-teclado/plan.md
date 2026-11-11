# Implementation Plan: Agilidad por teclado — Caja, POS, Factura y toda la aplicación

**Branch**: `claude/agilidad-teclado` | **Date**: 2026-11-08 | **Spec**: [spec.md](spec.md)

## Summary

Una capa de teclado común para toda la aplicación (Enter avanza, ayuda F1 por campo, selección al entrar, Esc seguro), dos
buscadores reescritos sobre su mismo contrato (personas y productos), un panel único de medios de pago con los mismos datos en
ventas y en Recaudo, y las tres pantallas de más uso rehechas encima: Caja de Recaudo a pantalla completa, POS sin ratón y
factura de oficina con borrador automático. Un despliegue, una migración aditiva por proveedor.

## Technical Context

**Language/Version**: .NET 10.0.5, C# 14; JavaScript sin módulos ES (globales cargados con huella, como `pos.js`).
**Primary Dependencies**: Blazor (WebAssembly en la web, Hybrid en MAUI), Syncfusion 33.2.8 por paquetes, MediatR, EF Core.
**Storage**: PostgreSQL y SQL Server (una base por cooperativa) — una migración par aditiva; `localStorage` sólo para
comodidades por equipo (recientes, datáfono, últimas elecciones) y siempre con try/catch.
**Testing**: Shared.Tests (lógica del cobro, normalización de valores, ayudas), Application.Tests (búsqueda de personas,
pagos de venta, teclas de medios), Architecture.Tests (marcas de teclado, ayudas, un solo panel), e2e al final.
**Target Platform**: navegador (web) y MAUI Hybrid (Android/Windows) — mismo Shared y mismo JavaScript.
**Performance Goals**: buscar persona < 1 s con 100.000 personas; teclear nunca espera al servidor (búsquedas con espera de
200 ms y cancelación; borrador de factura encolado).
**Constraints**: constitución (spec-first, Clean Architecture, un solo buscador de personas, PublicId hacia afuera, validación
dual, errores visibles, inmutabilidad de lo contabilizado, migraciones aditivas), `IndicadorDeCarga` en toda pantalla tocada,
tokens de color (ninguna pantalla con colores literales ni `<style>` propio), interop probado a mano.
**Scale/Scope**: 357 pantallas reciben el Enter y la ayuda genérica; 60 usan el buscador de personas, 20 el de productos, 5 el
panel de cobro; 3 pantallas se rehacen.

## Constitution Check

| Principio | Cómo se cumple |
|---|---|
| I Spec-first | spec, plan y tareas antes del código; decisiones del dueño en Clarifications. |
| II Clean Architecture | la búsqueda y los pagos cambian en Application/Domain; Presentation sólo reenvía; el JS no decide reglas. |
| III CQRS | `SearchPeopleQuery` se reescribe; los comandos de venta y cobro ganan campos con su validador. |
| IV Multi-tenancy | todo dato por cooperativa; las claves de `localStorage` llevan la cooperativa y el usuario. |
| V Person maestra | el texto de búsqueda vive en `COR_People` y lo mantiene un solo punto al guardar. |
| VI PublicId | recientes, cuentas propias, bancos y datáfonos viajan por PublicId; `localStorage` no guarda datos personales, sólo PublicId. |
| VII Soft-delete y auditoría | sin cambios; los pagos nuevos se auditan con el documento. |
| VIII Validación dual | el panel avisa lo que falta; el servidor valida cuenta propia, cheque y teclas reservadas. |
| IX Errores visibles | el borrador automático que falla lo dice; ninguna excepción se traga. |
| X SARLAFT | la declaración de origen de fondos se adjunta como archivo real. |
| XI Inmutabilidad | lo confirmado no se edita; el cheque de una venta anulada sigue el flujo de Tesorería. |
| XII Migraciones | una migración par aditiva (columnas nulas e índice), sin datos destruidos; el texto de búsqueda se llena con una tarea idempotente. |

## Diseño

### 1. Capa de teclado — `wwwroot/js/teclado.js` (`window.ingeniaTeclado`)

Un global cargado una vez con huella desde `App.razor` (`@Assets`) y desde el `index.html` de MAUI, como `pos.js`.

- **Enter avanza** (escucha en `window`, fase de burbuja, para correr después de Blazor y de Syncfusion): si la preferencia está
  encendida, el destino no tiene `data-enter="propio"`, el evento no fue prevenido, no es `textarea`, editor enriquecido,
  `contenteditable`, grilla (`.e-grid`) ni un campo con lista abierta (`aria-expanded="true"`), y el elemento no lleva un
  manejador de teclado de Blazor sin marca (defensa), mueve el foco al siguiente campo de su **zona**: el ancestro más cercano
  con `data-zona-teclado`, el diálogo abierto (`.e-dialog`) o la página (`.pagina`, `body`). Campos: `input` visibles y
  habilitados (los de sólo lectura sólo si son listas de Syncfusion), `select`, casillas. Al final de la zona: el elemento de
  `data-siguiente` de la zona si lo tiene; si no, el botón principal (`[data-principal]`, o el primer `.e-primary` visible y
  habilitado de la zona). Shift+Enter retrocede.
- **Selección al entrar**: `focusin` por teclado (la última interacción fue una tecla) en `input` de texto o valor →
  `select()`.
- **Ayuda F1**: muestra junto al campo enfocado el texto de `data-ayuda` (del campo o de su `.campo`), o una ayuda genérica
  armada con la etiqueta, el tipo de campo y sus teclas. Un único `div` reutilizable con `role="tooltip"` y clases de
  `componentes.css`. Esc (que no se propaga a la pantalla), F1 o salir del campo la cierran.
- **Teclas que se previenen**: `data-prevenir="ArrowUp ArrowDown Enter"` en un campo hace `preventDefault` de esas teclas sin
  detener el evento (Blazor lo sigue recibiendo). Reemplaza el truco de `@onkeydown:preventDefault` con bandera.
- **Atajos de pantalla**: `registrar(ref, teclas)` devuelve un identificador; pila: el último registrado decide primero (el
  panel de medios sobre la pantalla). Escucha en `document`, fase de captura, para las F reservadas; previene y avisa
  `Tecla(nombre)` al .NET. `soltar(id)`. El POS conserva `pos.js` para su lectura, ráfaga e impresión; sus teclas de medios
  pasan a la pila común.
- **Preferencia**: `configurar({ enterAvanza })`, llamada al iniciar y al cambiar Preferencias.
- **Ayudantes**: `enfocar(selectorOElemento)`, `mostrarMarcado(lista)` (lleva la opción marcada a la vista).

Marcas en el marcado (contrato entre Razor y el JS): `data-enter="propio"`, `data-zona-teclado`, `data-siguiente`,
`data-principal`, `data-ayuda`, `data-prevenir`.

### 2. Preferencia «Enter pasa al siguiente campo»

Se suma a las preferencias del usuario (las mismas de tema, densidad y escala: `preferencias.js` y su sincronización con el
servidor). Encendida por defecto (Q3).

### 3. Campo de valor — `Components/Comunes/CampoDeValor.razor`

`<input inputmode="decimal">` de texto con formato es-CO al salir (miles con punto, sin decimales en pesos), que al escribir
admite «50000», «50.000» o «50 000» y nunca toma el punto como decimal en pesos; selecciona todo al entrar; Enter confirma el
valor (y avanza por la capa común). Lógica de lectura y formato en una clase pura (`LecturaDeValor`) con pruebas.

### 4. Buscador de personas (mismo componente `PersonSearchPicker`, mismo contrato)

- `input` nativo con `@oninput` (espera 200 ms, cancela la anterior), `data-enter="propio"`,
  `data-prevenir="ArrowUp ArrowDown Enter"`; lista `role="listbox"` con opción marcada, ↑ ↓ Enter Esc; Enter con documento
  exacto elige sin lista.
- **Recientes**: ↓ con el campo vacío. `localStorage` guarda sólo PublicId (clave por cooperativa y usuario); los datos se
  piden al servidor (`/api/core/people/search?ids=`).
- **Crear**: opción «Crear persona nueva con "…"» en la lista y Alt+N.
- **Número de producto** (parámetro opcional, lo usa la Caja): con un término numérico la lista suma «Crédito o cuenta N.º …»;
  elegirla llama al callback de la pantalla.
- **Servidor**: `COR_People.SearchText` (normalizado con `NormalizadorDeBusqueda`: documento, nombres, apellidos, razón
  social, código), mantenido al guardar por un único punto en Persistence para toda entrada `Person` agregada o modificada;
  índice de trigramas en PostgreSQL. La consulta exige cada término (`LIKE` por término, escapado) y ordena: documento exacto,
  documento por prefijo, nombre por prefijo, resto; 20 filas. Las filas existentes se llenan con una tarea idempotente al
  arrancar, por lotes.

### 5. Buscador de productos (`SelectorDeProducto`)

- Parámetros nuevos: `TextoInicial`, `Contexto` de precio (ver §8) y `AutoEnfocar`.
- Cada resultado: código, nombre, precio que aplica, disponible en la bodega, unidad base y empaques.
- `data-enter="propio"`, `data-prevenir`, lista que lleva la marcada a la vista.

### 6. Panel único de medios de pago — `Components/Pagos/PanelDeMediosDePago.razor`

- Modelo puro `Services/Pagos/CobroMultiple.cs` (reemplaza a `CobroDelPos`): pagos con el superconjunto de datos (valor,
  recibido, referencia, aprobación, datáfono, lote, últimos 4, número de bono, cuenta propia, cheque, crédito); falta, sobra,
  vueltas, `Problema()`; adaptadores `ParaVentas(sesión)` → `PagoRequest` y `ParaRecaudo()` → `MedioDeCajaRequest`.
- `MedioDisponible`: el medio con su clase, tecla, si da vueltas, si pide referencia, datáfonos, cuentas propias elegibles,
  valores del crédito; se arma desde lo que trae cada módulo.
- Barra de medios: un botón por medio con su tecla; con la barra enfocada, la tecla (letra, número o F permitida) agrega el
  medio; ← → recorren; Enter agrega el marcado.
- Al agregar: foco en el valor seleccionado (para partir el pago). Cada fila es una zona (`data-zona-teclado`) con
  `data-siguiente` = barra de medios si falta dinero, o el botón de confirmar si no.
- Orden fijo por clase: efectivo (valor, recibido); tarjeta (valor, datáfono, aprobación, lote, últimos 4);
  transferencia/consignación (valor, cuenta propia, referencia); cheque (valor, banco, número, cuenta, fecha, misma plaza,
  girador); bono (valor, número); crédito (valor, cuotas, plazo, periodicidad, primer vencimiento, línea).
- Efectivo: «Recibido» con Enter vacío = exacto, «Exacto» (Alt+0) y los billetes activos del catálogo de denominaciones
  (Alt+1…Alt+9, de menor a mayor); vueltas al instante.
- ↑ ↓ entre filas, Supr quita la fila elegida; datáfono recordado por equipo.
- Lo reemplaza en `PuntoDeVenta`, `NuevaFactura`, `NuevaNotaCredito`, `EditorDelCiclo` y `Caja`.

### 7. Pantallas

- **Caja** (`/recaudo/caja`, `@layout PosLayout`): barra de contexto; columna izquierda buscador único y lista de lo que
  puede pagar (↑ ↓, Espacio/Enter agrega, F4 todo lo exigible); centro, lo que paga (simulación automática con espera y
  cancelación); derecha, el panel y los totales. F2 buscador, F10 medios y luego confirmar (imputa y confirma), Supr quita,
  Ctrl+Alt+X cancela con confirmación, Esc nunca cancela. Tras el recibo: Enter nueva, Ctrl+P tirilla, Ctrl+Shift+P carta.
  Declaración de origen con el componente de adjuntos.
- **POS**: base común; diálogos con zona y botón principal; F2 con lo escrito; suspendidas con ↑ ↓; panel único; teclas de
  medios por la pila común.
- **Factura**: cabecera compacta con lo último elegido en el equipo; líneas en tabla digitable (producto → cantidad → precio →
  descuento → producto); borrador automático (cola de una escritura en vuelo más la siguiente); total y panel siempre a la
  vista; F2, F4, F10, Supr; tras confirmar se queda: Enter nueva, Ctrl+P imprime.

### 8. Servidor

**8.1 Personas.** `Person.SearchText` (normalizado con `NormalizadorDeBusqueda`: documento, nombres, otros nombres,
apellidos, razón social, código heredado), mantenido por un único punto en Persistence para toda entrada `Person` agregada o
modificada; índice como el de productos (trigramas en PostgreSQL). `SearchPeopleQuery(q, rol, ids)`: cada término por `LIKE`
escapado; orden documento exacto → documento por prefijo → primer término al inicio de una palabra → resto, luego nombre; 20
filas. `ids` (máximo 20) devuelve esas personas en ese orden (recientes). Llenado de lo existente: tarea idempotente por
cooperativa al arrancar, por lotes, sólo filas con texto vacío.

**8.2 Productos.** `SearchProductsQuery` gana un contexto de precio opcional: `priceDraft` (borrador de POS o de oficina: el
servidor toma su cliente, canal, sucursal y fecha, como la venta), `priceCashSession` (POS sin borrador: consumidor final,
canal y sucursal del punto, fecha de la sesión) o explícito `priceCustomer`, `priceDocumentType` (canal), `priceWarehouse`
(sucursal) y `priceDate`. Con `Inventory.Prices.View` cada resultado trae `Price`, `PriceIncludesTaxes`, `PriceListCode` y
`Units` (unidad, factor, base, precio de esa unidad si la lista lo tiene); sin el permiso no trae precio (como el
disponible). Usa `ResolucionDePrecios.CandidatasAsync` y `ResolutorDeListaDePrecios` (una consulta para las listas y otra para
sus precios), nunca `PrecificacionDeVenta`.

**8.3 Billetes.** `GET /api/core/cash-denominations` admite `AlgunPermiso` (`Core.PaymentMeans.View`, `Inventory.Pos.Sell`,
`Inventory.Sales.Create`, `Receipts.Receipt.Register`).

**8.4 Preferencia.** Clave `ui.enter-avanza` (`si`/`no`) en `UserSettingKeys.Globales`, DTO, consulta, cliente, JS y
pantalla; sin migración (filas de `ADM_UserSettings`).

**8.5 Medios de la sesión de Recaudo.** `MedioDeSesionDto` gana `QuickKey`, `AllowsChange`, `RequiresReference`,
`ReferenceKind`, `DisplayOrder` y sus datáfonos: la cajera no tiene `Core.PaymentMeans.View` y hoy la lista de datáfonos le
llega vacía.

**8.6 Recaudo, últimos 4.** `RCD_ReceiptMeans.Last4` (char 4, nulo) y `MedioDeCajaRequest.Last4`, validado como en ventas y
cubierto por `LosPagosNoGuardanElNumeroDeTarjeta`.

**8.7 Soporte de la declaración.** `AdjuntosDeModulo` admite el dueño «operación de caja de Recaudo»: sube con
`Receipts.Receipt.Register` mientras la operación no esté confirmada; lee con el mismo permiso; no se borra una vez
confirmada.

**8.8 Transferencias en ventas (Q16).**
- `INV_TransferAccounts` (punto, caja opcional, medio, `OwnBankAccountPublicId`, rótulo copiado, activa) como
  `RCD_TransferAccounts`; habilitar y retirar validan con `ICuentasBancariasPropias` (cuenta activa de recaudo) y que el medio
  sea de clase 6 o 7 y se ofrezca en el punto. Rutas bajo el punto de venta, con el permiso que administra puntos; sección
  «Cuentas para transferencias» en `/ventas/puntos-de-venta`.
- `DocumentPayment` gana `OwnBankAccountPublicId` y `OwnBankAccountLabel`; `DocumentPaymentInput` y `PagoRequest` la cuenta.
  `RegistroDePagos.RegistrarAsync` y `BorradorDeVenta.GuardarPagosAsync` la resuelven por la sesión del pago (punto y caja):
  con cuentas habilitadas para el medio es obligatoria (la única se propone; una no habilitada se rechaza:
  `Payments.OwnBankAccountRequired`, `Payments.OwnBankAccountNotEnabled`); sin habilitadas, como hoy.
  `ReglasDeConfirmacionDeVenta.ValidarVentaAsync` la vuelve a comprobar.
- Los medios del POS (`MedioDelPosDto`) y los de oficina traen las cuentas elegibles de la sesión (ruta nueva para la
  oficina, que hoy arma los medios en el cliente).
- `VentaFacturadaV2` con el pago que lleva la cuenta propia (precedente `VentaACreditoRegistradaV2`; el golden de V1 no
  cambia); el destino Contabilidad acepta V1 y V2. Las notas siguen en V1 (sus pagos de transferencia contabilizan por el
  medio, como hoy).
- `ConstructorDeLineasDeInventario`: un pago con cuenta propia contabiliza contra la cuenta de su vínculo
  (`ACC_OwnBankAccountLinks`, el mismo resolutor de Recaudo); sin cuenta, por la regla del medio. La validación previa de la
  venta detecta la cuenta sin vínculo y no deja confirmar.

**8.9 Cheques en ventas (Q17).** `DocumentPayment` gana banco librado (Id y nombre copiado), número (20), cuenta (30), fecha,
misma plaza y girador (200); `DocumentPaymentInput.Check`. `ValidadorDePagos`: clase 9 exige banco, número, cuenta y fecha y
cliente identificado (`Payments.CheckDataRequired`, `Payments.CheckRequiresIdentifiedCustomer`); la cuenta del cheque queda
fuera de la búsqueda de números de tarjeta; si el medio pide referencia, la referencia es el número del cheque. Sin custodia
ni mensaje nuevo (fase 2).

**8.10 Catálogo de medios.** La pantalla pide un solo carácter para la tecla rápida (el servidor ya sólo admite letra o
número).

**8.11 Migración.** Una sola migración par aditiva al final, cuando los dos frentes del servidor estén juntos:
`COR_People.SearchText` e índice, `INV_TransferAccounts`, columnas nuevas de `INV_DocumentPayments` y `RCD_ReceiptMeans.Last4`.
Paridad con `check-migration-parity.ps1`.

## Project Structure

```text
specs/022-agilidad-teclado/        spec.md, plan.md, tasks.md, checklist de pruebas a mano
src/Core/IngenIA365ERP.Domain       Person.SearchText; pagos de venta con cuenta propia y cheque
src/Core/IngenIA365ERP.Application  SearchPeopleQuery; búsqueda de productos con precio; comandos de venta y cobro
src/Infrastructure/...Persistence   punto único del texto de búsqueda; configuraciones; tarea de llenado
src/Infrastructure/...Migrations.*  una migración par aditiva
src/Presentation/IngenIA365ERP.Shared
  wwwroot/js/teclado.js             capa de teclado
  wwwroot/css/componentes.css       ayuda, listas de buscadores, panel de medios, caja a pantalla completa
  Components/Comunes                CampoDeValor, BarraDeAtajos
  Components/Pagos                  PanelDeMediosDePago
  Components/Shared                 PersonSearchPicker
  Components/Inventario             SelectorDeProducto
  Services/Pagos                    CobroMultiple, MedioDisponible, LecturaDeValor
  Pages/Recaudo/Caja.razor, Pages/Pos/PuntoDeVenta.razor, Pages/Ventas/NuevaFactura.razor
tests/…                             Shared, Application, Architecture
```

## Complexity Tracking

| Decisión | Por qué | Alternativa descartada |
|---|---|---|
| Comprobar el manejador de Blazor del elemento en el JS | defensa ante un campo con Enter propio que se olvide marcar | sólo la marca: un olvido rompería una búsqueda con Enter |
| Llenado del texto de búsqueda por tarea al arrancar y no en la migración | la normalización es C# y debe ser idéntica en los dos motores | SQL por motor: tildes y espacios no saldrían iguales |
