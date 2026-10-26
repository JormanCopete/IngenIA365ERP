# Comercial: inventario, compras, ventas, punto de venta y facturación electrónica

> Índice del módulo Comercial (feature 012, entregas I1 a I6, en producción desde el 2026-10-21). Es la puerta de entrada
> para quien opera o parametriza el módulo en la cooperativa: qué cubre, quién hace qué, en qué orden se pone en marcha y
> dónde está cada manual. El detalle de cada pantalla también está en el manual en línea de la aplicación (**SISTEMA → Manual del sistema**,
> o «¿Cómo se hace?» en la barra superior), tema por tema.

## Qué hace

Comercial lleva todo lo que la cooperativa compra, guarda y vende cuando maneja mercancía o servicios: un almacén, un
supermercado, una droguería, un punto de insumos. Reemplaza al módulo de inventario del sistema anterior; las cifras de
ese sistema sólo entran como **cifras de referencia** para comparar, nunca como movimientos.

Está dividido en cinco grupos del menú:

| Grupo del menú | Qué se hace ahí | Manual |
|---|---|---|
| **Inventario** | Catálogo de productos, bodegas, existencias, kardex, ajustes, traslados, conteos, períodos, puesta en marcha, costeo, aprobaciones, alertas, parámetros, la relación con Contabilidad, el tablero y los informes. | [comercial-inventario-y-bodegas.md](comercial-inventario-y-bodegas.md) |
| **Compras** | Solicitudes, órdenes, recepciones, compra directa, facturas y notas del proveedor, devoluciones, cruce a tres vías, costos adicionales (fletes, seguros) y documento soporte. | [comercial-compras.md](comercial-compras.md) |
| **Ventas** | Ventas de oficina, cotizaciones, pedidos, remisiones, notas crédito y débito, promociones, listas de precios y topes de descuento, puntos de venta y cajas, vendedores, canales e informes de ventas. | [comercial-ventas-y-pos.md](comercial-ventas-y-pos.md) |
| **Punto de venta** | Vender en el POS con lector y teclado, sesiones de caja, movimientos de caja y cierre del día. | [comercial-ventas-y-pos.md](comercial-ventas-y-pos.md) |
| **Facturación electrónica** (Administración, Maestros y Ventas) | Configuración de la emisión, resoluciones DIAN, bandeja de documentos electrónicos y contingencias. | [comercial-facturacion-electronica.md](comercial-facturacion-electronica.md) |

Los medios de pago (efectivo, tarjetas, bonos, créditos…) se administran en **Maestros → Medios de pago** y el catálogo
de impuestos en **Maestros → Impuestos y retenciones**, porque los comparte toda la cooperativa.

## Quién lo hace

El módulo trae permisos separados para cada tarea, de modo que cada rol vea y haga sólo lo suyo. Los perfiles sugeridos
(jefe de inventario, bodeguero, comprador, cajero, supervisor de caja…) se crean desde **Administración → Roles y
Permisos** con «Crear desde perfil sugerido» y se ajustan a la cooperativa; el alcance por bodega y los montos máximos
se asignan aparte.

| Quién | Qué hace habitualmente |
|---|---|
| **Jefe de inventario** | Catálogo, bodegas, parámetros, cierres de período, conteos, aprobación de ajustes y traslados, puesta en marcha. |
| **Bodeguero** | Recepciones, traslados (despachar y recibir), captura de conteos, consultas de existencias y kardex. |
| **Comprador** | Solicitudes, órdenes, facturas y notas del proveedor, devoluciones, costos adicionales, documento soporte. |
| **Vendedor / asesor de oficina** | Cotizaciones, pedidos, remisiones, facturas de oficina, notas. |
| **Cajero** | Abrir su caja, vender en el POS, movimientos de caja, cerrar su caja. |
| **Supervisor de caja** | Aprobar descuentos sobre el tope (en persona con su llave o su autenticador), aprobar movimientos de caja, cerrar y reabrir el día. |
| **Contadora** | Matriz contable de Inventario, tipos de comprobante, completitud, lotes, conciliación, validación de impuestos y retenciones. |
| **Administrador** | Facturación electrónica, resoluciones DIAN, medios de pago, alcance comercial por bodega de cada usuario, políticas de aprobación y montos máximos. |

Dos reglas valen para todo el módulo:

- **Alcance por bodega**: cada usuario ve y opera sólo las bodegas que le asignaron (Inventario → Alcance comercial).
  Una bodega fuera de su alcance no existe para él.
- **Quien crea no aprueba**: cuando un documento pide aprobación, la da otra persona, y quien aprobó un nivel no aprueba
  el siguiente.

## Orden de puesta en marcha

La puesta en marcha se hace **bodega por bodega**: el sistema anterior sigue siendo el de registro de cada bodega hasta
el día en que se activa en el módulo nuevo. No hay sincronización automática entre los dos.

1. **Lo que no tiene plantilla**: sucursales con su municipio (Maestros → Agencias), centros de costo, personas del
   maestro (proveedores y clientes), bancos, canales de venta y roles.
2. **Las dieciséis plantillas de parametrización**, en su orden (Inventario → Plantillas): impuestos, grupos contables,
   unidades, marcas, categorías, productos, bodegas, tipos de documento, vendedores, puntos de venta y cajas, medios de
   pago, listas de precios, topes de descuento, saldo inicial, cifras de referencia y matriz contable. Cada una se
   revisa sin guardar nada y se aplica todo o nada.
3. **Contabilidad**: la contadora diligencia la matriz contable de Inventario y revisa que la **completitud** quede
   vacía. Mientras la contabilidad no esté iniciada, lo que confirma Inventario queda marcado «no pasa» a Contabilidad y
   se envía después.
4. **Por cada bodega**: conteo físico, cifras de referencia al corte, saldo inicial (plantilla 14) aprobado por otra
   persona y **activación** con el cuadre contra Contabilidad.
5. **Ventas y caja**: puntos de venta, cajas, datáfonos, denominaciones, listas de precios y topes.
6. **Facturación electrónica**: configuración de la emisión y resoluciones DIAN asociadas al canal. Hoy el único canal
   instalado es el **simulado** (ver [Lo que todavía no está](#lo-que-todavía-no-está)).
7. **Usuarios**: roles, alcance por bodega, políticas de aprobación y montos máximos.

La receta detallada para soporte y para quien dirige la implantación está en
[../operaciones/inventario-puesta-en-marcha.md](../operaciones/inventario-puesta-en-marcha.md).

## Manuales relacionados

Manuales funcionales del módulo:

- [comercial-inventario-y-bodegas.md](comercial-inventario-y-bodegas.md): catálogo, bodegas, existencias, movimientos,
  puesta en marcha, costeo, aprobaciones, alertas, parámetros, Contabilidad, tablero e informes.
- [comercial-compras.md](comercial-compras.md): todo el ciclo de compras.
- [comercial-ventas-y-pos.md](comercial-ventas-y-pos.md): ventas de oficina, punto de venta y caja.
- [comercial-facturacion-electronica.md](comercial-facturacion-electronica.md): la emisión ante la DIAN y el canal
  simulado.

Manuales de fondo (más técnicos, para soporte y para quien quiere entender las reglas por dentro):

- [inventario-documentos-y-kardex.md](inventario-documentos-y-kardex.md): el documento de inventario, su ciclo, la
  anulación y el kardex.
- [plataforma-de-integracion.md](plataforma-de-integracion.md): mensajes entre módulos, procesos de fondo, parámetros
  con vigencia, aprobaciones y alertas.
- [plantillas-de-importacion.md](plantillas-de-importacion.md): la mecánica común de las plantillas.
- [cartera-ventas-a-credito.md](cartera-ventas-a-credito.md): cómo una venta a crédito se convierte en una obligación de
  Cartera.
- [contabilidad-contrato-de-contabilizacion.md](contabilidad-contrato-de-contabilizacion.md): cómo llega al libro lo que
  manda Inventario.

Guías de operación (soporte):

- [../operaciones/inventario-puesta-en-marcha.md](../operaciones/inventario-puesta-en-marcha.md): puesta en marcha
  bodega por bodega.
- [../operaciones/inventario-retiro-heredado.md](../operaciones/inventario-retiro-heredado.md): retiro de las tablas del
  inventario anterior (lo hace soporte una sola vez por cooperativa).

## Lo que todavía no está

- **Facturación electrónica real ante la DIAN.** No hay proveedor tecnológico contratado ni software propio en
  funcionamiento: el único canal instalado es el **simulado**. Los documentos salen con la leyenda **«SIN VALIDEZ
  FISCAL»** y **no llegan a la DIAN**. Elegir y contratar el proveedor, registrar las resoluciones reales y probar en su
  ambiente de pruebas es del dueño. Ver [comercial-facturacion-electronica.md](comercial-facturacion-electronica.md).
- **Crédito con Cartera (entrega IC).** Del lado de Cartera ya está construido (feature 013: consulta de cupo, reserva
  del número y conversión de la venta en obligación), pero **Cartera no está activada** en la cooperativa. Mientras no
  se active, una venta con un medio de crédito va como **crédito provisional**: queda esperando la aprobación de alguien
  con el permiso de vender a crédito y un monto suficiente, y la venta se registra como pendiente de validar por Cartera.
  En la factura de oficina la pantalla no espera esa aprobación: avisa que quedó en aprobación y lleva al documento,
  cuya pestaña «Crédito» muestra el estado.
- **Generación automática semanal del documento soporte.** El botón «Generar los de la semana» prepara los borradores,
  pero no hay un proceso que lo dispare solo el último día hábil; la regla de días hábiles y la numeración de la nota de
  ajuste están por cotejar con la contadora y el proveedor.
- **Nota crédito sobre una factura expedida fuera del ERP** (ventas anteriores a la salida en vivo): no está decidido si
  se admite. Hoy la nota crédito sólo se hace sobre ventas confirmadas en el módulo.
- **Ingenia365 como proveedor tecnológico**, multimoneda operativa (importaciones en moneda extranjera) y los eventos
  RADIAN distintos del acuse de recibo y el recibo del bien (aceptación expresa o tácita, endoso): fuera del alcance.
- **Movimientos históricos del sistema anterior**: no se migran; se entra con saldos.

Pendientes del dueño o de la contadora antes de operar de lleno:

- validar la semilla tributaria (tarifas, bases en UVT, conceptos de retención) y los impuestos que vende la
  cooperativa (INC, bolsas, bebidas y ultraprocesados);
- validar los tipos de comprobante de Inventario y diligenciar la matriz contable;
- definir roles, políticas de aprobación por tipo de documento y montos máximos por rol;
- confirmar las tolerancias de reconteo y la fecha del ajuste de conteo, las tolerancias del cruce a tres vías y los
  umbrales de los informes (clase ABC, días sin movimiento, tope de faltantes);
- definir la impresión de las cajas (impresora de 58 u 80 mm y navegador en modo quiosco);
- entregar las resoluciones y prefijos de facturación que usa hoy la cooperativa, el software asociado y el buzón del
  correo al comprador;
- fijar las fechas del conteo, del saldo inicial y de la activación de cada bodega.
