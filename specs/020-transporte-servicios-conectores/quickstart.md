# Quickstart — validar la 020 de punta a punta

Este recorrido se hace en local (o en DEV) **después** de construir todas las fases. Las pruebas
corren una sola vez al final, como pidió el dueño. Ningún dato de esta guía es de una cooperativa
real.

## 0. Preparación

1. Compilar y probar:

   ```bash
   dotnet build IngenIA365ERP.slnx -m:1
   ```

   ```bash
   dotnet test tests/IngenIA365ERP.Domain.Tests
   ```

   ```bash
   dotnet test tests/IngenIA365ERP.Application.Tests
   ```

   ```bash
   dotnet test tests/IngenIA365ERP.Architecture.Tests
   ```

2. Integración, con Docker Desktop encendido:

   ```bash
   dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "Collection=Transporte e2e"
   ```

   La fixture levanta un **proveedor falso** en `https://127.0.0.1:{puerto}` y pone
   `Conectores:PermitirRedesPrivadas=true`, sólo en el host de prueba.
3. A mano, en local: API y Web arriba, maestro con su segundo factor, una cooperativa de prueba con
   Comercial y Cartera activos, y una línea de Cartera de servicio (marcada `IsService`, sin
   interés).

## 1. Habilitar el módulo (US1)

1. Con la cuenta del maestro, ir a `/saas/modulos`, elegir la cooperativa de prueba y habilitar
   **Transporte** con un motivo.
2. Con un usuario administrador de la cooperativa: el menú muestra **Transporte**.
3. Con un administrador de **otra** cooperativa: no hay menú, y
   `GET /api/transport/vehicles` responde **403 `Module.NotEnabled`**.
4. Deshabilitar y volver a habilitar: los datos siguen ahí.

## 2. Vehículos (US2, US3)

1. En `/transporte/catalogos`, crear la marca `GENERICA` y el grupo de rodamiento `G1`.
2. En `/transporte/vehiculos`, «Nuevo»:
   - placa `AAA111`, número interno `0001`, radio-móvil `101`;
   - propietario: una persona **asociada** (la ficha dice «asociado»).
3. Registrar otro con la placa `aaa-111`: rechaza **`Vehicle.PlateDuplicate`**, nombrando el primero.
4. Inactivar con motivo, reactivar, cambiar de propietario a una persona **no asociada**:
   - la pantalla muestra sus deudas si las tiene;
   - la ficha pasa a «externo»;
   - la historia tiene tres eventos.
5. Plantilla `transport.vehicles` con 3 filas válidas y 1 con un documento de propietario
   inexistente:
   - **Revisar**: el error sale en su fila y columna, y nada se guarda.
   - Se corrige y se **aplica**: se crean 3 vehículos.

## 3. Documentos y conductores (US4, US5)

1. Registrar un SOAT que vence en 10 días y una RTM vencida. El tablero `/transporte/documentos`
   muestra uno «por vencer», uno «vencido» y los obligatorios faltantes. El vehículo sigue Activo.
2. Registrar como conductor a una persona y asignarla como titular de `AAA111`. La ficha la muestra.
   Al terminar la asignación, queda en la historia.

## 4. Servicios y facturación (US6)

1. En Comercial, crear dos productos de servicio, `RADIO` y `APP`.
2. En `/transporte/servicios`, configurar cada uno con su prioridad (RADIO 1, APP 2), la línea de
   Cartera de servicio y el medio a crédito.
3. Suscribir `AAA111` a los dos y ponerle a APP un precio pactado menor al de lista.
4. `/transporte/facturacion`:
   - **Vista previa** del período.
   - **Facturar a crédito**: una factura por vehículo con dos líneas, y la de APP con el precio
     pactado, **sin pedir aprobación de descuento**.
   - En Cartera nacen **dos obligaciones** (una por servicio), en la línea de servicio.
5. Facturar otra vez el mismo período: el resumen dice que ya estaban. No hay factura nueva.

## 5. Conector y notificación (US7, US8, US9)

1. Como maestro, en `/saas/conectores`, crear «Proveedor de prueba» con la definición del ejemplo
   de [contracts/api.md](contracts/api.md):
   - una URL `http://` se rechaza (`Connector.InsecureUrl`);
   - una IP privada se rechaza fuera de la prueba;
   - **Simular** muestra el cuerpo sin enviar nada.
2. Habilitarlo para la cooperativa.
3. Como administrador de la cooperativa, en `/admin/conectores`:
   - poner usuario y clave, y **probar conexión** (correcta e incorrecta);
   - recargar: la clave no se ve.
4. Ligar `RADIO` y `APP` a ese conector.
5. En **caja**, pagar sólo la obligación de RADIO. En segundos, `/transporte/envios` muestra un
   envío **Enviado** de RADIO con su identificador. APP no se envió.
6. Proveedor falso en modo «500 dos veces y luego 200»: el envío pasa por **Reintentando** y queda
   **Enviado** con el **mismo identificador** (el proveedor falso registra que lo recibió tres veces
   con el mismo `externalID`).
7. Proveedor falso en modo «400 ya tiene vigencia»: **Enviado con aviso**, sin reintentos.
8. Anular el recibo de RADIO:
   - el envío queda **Pago anulado: revisar en el sistema externo**;
   - llega la alerta al administrador;
   - volver a pagar **no** genera un segundo envío.
9. Deshabilitar el conector para la cooperativa y pagar APP: el envío queda **En espera**. Al
   habilitarlo, sale.

## 6. Ruta sin Cartera (FR-059)

En una cooperativa con Comercial activo y **Cartera inactiva**:
1. Facturar a crédito: la pantalla advierte «se cobra sólo en caja».
2. Pagar en caja la factura por el valor de RADIO: se notifica RADIO (prioridad 1), no APP.

## 7. Seguridad

1. Buscar el texto de la clave de prueba en todos estos sitios: el log de la API, la auditoría
   Mongo, las exportaciones de `envios` y la respuesta de `GET /api/core/connectors`. **Cero
   apariciones** (SC-006).
2. `GET /api/transport/**` con una cooperativa sin módulo, incluso con un rol `*`: 403.

## Lo que se verifica a mano

Cada uno de estos puntos va en el recorrido manual que hace el dueño:
- La factura electrónica real depende del canal DIAN de la cooperativa: verificarlo en su ambiente.
- No hay pruebas de navegador; las pantallas se recorren a mano.
