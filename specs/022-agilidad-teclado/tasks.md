# Tasks: Agilidad por teclado — Caja, POS, Factura y toda la aplicación

**Input**: [spec.md](spec.md), [plan.md](plan.md) · **Rama**: `claude/agilidad-teclado`

Rutas: `APP` = `src/Core/IngenIA365ERP.Application`, `DOM` = `src/Core/IngenIA365ERP.Domain`,
`PER` = `src/Infrastructure/IngenIA365ERP.Persistence`, `API` = `src/Presentation/IngenIA365ERP.API`,
`SH` = `src/Presentation/IngenIA365ERP.Shared`. Frentes: **S1** servidor general, **S2** ventas (transferencias y cheques),
**C** cliente. S1, S2 y C avanzan en paralelo; la migración (fase 7) espera a S1 y S2.

## Fase 1 — Preparación

- [X] T001 Copia de trabajo `D:/PSNL/wt-teclado`, rama `claude/agilidad-teclado` desde `develop` `772195c4`.
- [X] T002 spec.md, plan.md y tasks.md en `specs/022-agilidad-teclado/`.

## Fase 2 — Servidor general (S1)

- [X] T010 [S1] `DOM/Entities/Core/Person.cs`: `SearchText` y su largo; el texto se arma con `NormalizadorDeBusqueda`.
- [X] T011 [S1] Punto único que recalcula `SearchText` para toda `Person` agregada o modificada al guardar (`PER`).
- [X] T012 [S1] `PER/Configurations/Core/PersonConfiguration.cs`: propiedad e índice por motor (como `IndiceDeBusquedaDeProductos`).
- [X] T013 [S1] Tarea idempotente por cooperativa que llena `SearchText` vacío por lotes al arrancar.
- [X] T014 [S1] `APP/Core/People/Queries/SearchPeopleQuery.cs`: términos, orden, 20 filas, `ids`; validador; ruta `?ids=`.
- [X] T015 [S1] Pruebas de `SearchPeopleQuery` (mayúsculas, tildes, varias palabras, orden, `ids`) y del armado del texto.
- [X] T020 [S1] `APP/Inventory/Catalog/Products/ProductQueries.cs`: contexto de precio (`priceDraft`, `priceCashSession`,
  explícito), `Price`, `PriceIncludesTaxes`, `PriceListCode`, `Units`; con `Inventory.Prices.View`; ruta y DTO de Shared.
- [X] T021 [S1] Pruebas del precio en la búsqueda (con y sin permiso, empaque con y sin precio propio).
- [X] T030 [S1] `API/Endpoints/Core/PaymentMeansEndpoints.cs`: billetes con `AlgunPermiso` de quien cobra.
- [X] T031 [S1] Preferencia `ui.enter-avanza`: `UserSettingKeys`, DTO, consulta, `PreferenciasClient`, pruebas del validador.
- [X] T032 [S1] `MedioDeSesionDto` de Recaudo con tecla, vueltas, referencia, orden y datáfonos (consulta de la sesión y Shared).
- [X] T033 [S1] `RCD_ReceiptMeans.Last4` y `MedioDeCajaRequest.Last4`, validados; prueba.
- [X] T034 [S1] `AdjuntosDeModulo`: dueño «operación de caja» para el soporte de la declaración; pruebas.
- [X] T035 [S1] `SH/Pages/Maestros/MediosDePago.razor`: tecla rápida de un carácter.

## Fase 3 — Ventas: transferencias y cheques (S2)

- [ ] T040 [S2] Entidad `INV_TransferAccounts`, configuración y comandos habilitar/retirar con validador
  (`ICuentasBancariasPropias`, clase 6/7, medio ofrecido en el punto); consulta de cuentas por punto y por sesión.
- [ ] T041 [S2] Rutas bajo el punto de venta y de la sesión; DTO y cliente de Shared.
- [ ] T042 [S2] Sección «Cuentas para transferencias» en `SH/Pages/Ventas/PuntosDeVenta.razor` (con `IndicadorDeCarga`).
- [ ] T043 [S2] `DocumentPayment`: cuenta propia (PublicId y rótulo) y datos del cheque; configuración.
- [ ] T044 [S2] `DocumentPaymentInput`/`PagoRequest`: cuenta propia y cheque; `RegistroDePagos`, `BorradorDeVenta.GuardarPagosAsync`
  y `ReglasDeConfirmacionDeVenta.ValidarVentaAsync` resuelven y validan.
- [ ] T045 [S2] `ValidadorDePagos`: reglas del cheque y del cliente identificado; la cuenta del cheque fuera de la búsqueda de tarjetas.
- [ ] T046 [S2] Medios del POS y de oficina con cuentas elegibles; datáfonos y cheque en el DTO.
- [ ] T047 [S2] `VentaFacturadaV2` con la cuenta propia; emisor y destino Contabilidad (V1 y V2); golden de V2.
- [ ] T048 [S2] `ConstructorDeLineasDeInventario`: pago con cuenta propia contra su vínculo; validación previa sin vínculo.
- [ ] T049 [S2] Pruebas: resolución y validación de la cuenta, cheque, contabilización por cuenta propia, contratos.

## Fase 4 — Base de teclado del cliente (C)

- [ ] T050 [C] `SH/wwwroot/js/teclado.js`: Enter/Shift+Enter por zonas, selección al entrar, ayuda F1, `data-prevenir`,
  pila de atajos, preferencia; cargado en `App.razor` (`@Assets`) y en el `index.html` de MAUI.
- [ ] T051 [C] Estilos de la ayuda, listas de buscadores, panel de medios y caja a pantalla completa en `componentes.css`
  (sólo tokens).
- [ ] T052 [C] Interruptor «Enter pasa al siguiente campo» en `SH/Pages/Security/Preferencias.razor` y su aplicación al iniciar.
- [ ] T053 [C] `SH/Services/Pagos/LecturaDeValor.cs` y `SH/Components/Comunes/CampoDeValor.razor`; pruebas.
- [ ] T054 [C] `SH/Components/Comunes/BarraDeAtajos.razor` (generaliza `AtajosDelPos`).
- [ ] T055 [C] `data-enter="propio"` en los campos con manejador de teclado propio; prueba de arquitectura
  `LosCamposConTeclasPropiasLoDicen`.

## Fase 5 — Componentes (C)

- [ ] T060 [C] `PersonSearchPicker`: entrada nativa, búsqueda al escribir, lista con ↑ ↓ Enter Esc, documento exacto,
  recientes por PublicId, crear con Alt+N, opción de número de producto; mismo contrato.
- [ ] T061 [C] `SelectorDeProducto`: `TextoInicial`, `AutoEnfocar`, contexto de precio, columnas de precio, disponible,
  unidad y empaques.
- [ ] T062 [C] `SH/Services/Pagos/CobroMultiple.cs` y `MedioDisponible.cs` (reemplazan `CobroDelPos`), adaptadores a ventas y
  a Recaudo; pruebas (falta, sobra, vueltas, recibido, adaptadores, problemas).
- [ ] T063 [C] `SH/Components/Pagos/PanelDeMediosDePago.razor`: barra de medios, filas por clase, efectivo con billetes,
  cuenta propia, cheque, crédito con su evaluación, ↑ ↓ Supr, datáfono recordado, ayudas.
- [ ] T064 [C] Reemplazar `PanelDeCobro` en `NuevaNotaCredito.razor` y `EditorDelCiclo.razor`; retirar `PanelDeCobro`.

## Fase 6 — Pantallas (C)

- [ ] T070 [C] Caja de Recaudo (`SH/Pages/Recaudo/Caja.razor`): `PosLayout`, barra de contexto y atajos, buscador único,
  lista de lo pagable, simulación automática, panel único, F10 imputar y confirmar, Ctrl+Alt+X cancelar, tras el recibo Enter y
  Ctrl+P / Ctrl+Shift+P, declaración con adjunto, ayudas de campo.
- [ ] T071 [C] POS (`SH/Pages/Pos/PuntoDeVenta.razor`): base común, diálogos por zonas, F2 con lo escrito y precio, suspendidas
  con flechas, panel único, ayudas de campo; `pos.js` deja las teclas de medios a la pila común.
- [ ] T072 [C] Factura (`SH/Pages/Ventas/NuevaFactura.razor`): cabecera recordada, líneas digitables, borrador automático
  encolado, panel siempre visible, F2/F4/F10/Supr, resultado tras confirmar con Enter y Ctrl+P, ayudas de campo.
- [ ] T073 [C] Prueba de arquitectura: los campos de las tres pantallas llevan `data-ayuda`; sólo `PanelDeMediosDePago` arma
  pagos.

## Fase 7 — Migración, cierre y despliegue

- [ ] T080 Migración par aditiva (`tools/scripts/add-migration.ps1`) y paridad.
- [ ] T081 Suites sin contenedores (Domain, Application, Architecture, Shared) en verde.
- [ ] T082 E2e de una vez al final (personas, venta con transferencia y cheque, Caja) en PostgreSQL y SQL Server.
- [ ] T083 Lista de comprobación a mano (teclado, F1, MAUI) en `specs/022-agilidad-teclado/comprobacion-a-mano.md`.
- [ ] T084 Manual: `docs/manual/teclado-y-atajos.md` y ajustes en los manuales de caja, POS y factura.
- [ ] T085 PR a `develop`; despliegue a DEV, QA y producción sólo con autorización del dueño.
