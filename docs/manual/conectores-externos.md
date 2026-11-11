# Conectores externos

Feature 020. Un conector avisa a un sistema externo (por ejemplo, el de un proveedor de radio o de app)
que un servicio de un vehículo quedó pagado. **Se define como dato, no como código**: ninguna cooperativa
queda acoplada a un proveedor, y uno nuevo es una definición nueva.

Hay tres papeles:

| Quién | Dónde | Qué hace |
|---|---|---|
| Administrador de la plataforma | Seguridad › Conectores externos (`/saas/conectores`) | define, versiona, simula y habilita por cooperativa |
| Administrador de la cooperativa | Administración › Conectores externos (`/admin/conectores`) | pone credenciales y prueba la conexión |
| Quien configura Transporte | Transporte › Servicios | liga cada servicio a su conector |

## 1. Definir un conector

Se crea con código y nombre, y luego se **publica una versión**: URL base y definición JSON. Cada cambio
publica una versión nueva e inmutable (con su huella SHA-256); los envíos guardan la versión con que
salieron.

La **URL base** es `https`, sin usuario ni clave, sin consulta, y apunta a una dirección pública. Al
conectar se vuelve a revisar cada IP resuelta: una red interna o reservada nunca se alcanza.

```json
{
  "auth": { "kind": "LoginToken", "method": "POST", "path": "/v1/login",
    "body": "{\"username\":\"{{cred.username}}\",\"password\":\"{{cred.password}}\"}",
    "tokenPath": "$.token", "expiresInSeconds": 86400, "header": "Authorization", "prefix": "Bearer " },
  "operation": { "method": "POST", "path": "/v1/servicios/activar",
    "body": "{\"movil\":{{vehiculo.radioMovil:int}},\"placa\":\"{{vehiculo.placa}}\",\"valor\":{{pago.valor:int}},\"externalID\":\"{{envio.id}}\"}" },
  "credentialFields": [ { "name": "username", "label": "Usuario", "secret": false },
                        { "name": "password", "label": "Clave", "secret": true } ],
  "responses": [ { "status": 200, "outcome": "Done" },
                 { "status": 400, "contains": "ya tiene vigencia", "outcome": "DoneWithWarning" },
                 { "status": 400, "outcome": "ManualReview" },
                 { "status": 401, "outcome": "ReAuth" },
                 { "status": 500, "outcome": "Retry" },
                 { "status": 0, "outcome": "Retry" } ],
  "maxAttempts": 12,
  "timeoutSeconds": 30
}
```

### Autenticación

- `LoginToken`: un inicio de sesión que devuelve un token (`tokenPath`), que se manda en `header` con
  `prefix`. El token vive en memoria hasta su vigencia menos un minuto; **nunca se guarda**. Ante un 401
  se pide otro una sola vez.
- `HeaderKey`: una llave fija en una cabecera (`value`, normalmente `{{cred.apiKey}}`).
- `Basic`: usuario y clave (`username`, `password`).
- `None`: sin autenticación.

### Plantillas, variables y formatos

`{{variable}}` o `{{variable:formato}}`. Formatos: `int`, `dec2`, `date(aaaa-MM-dd)` o texto (por defecto,
escapado para JSON: las comillas las pone la plantilla). Variables:

- `vehiculo.placa`, `vehiculo.numeroInterno`, `vehiculo.radioMovil`;
- `propietario.documento`, `propietario.nombre` (**sensibles**: no se registran);
- `servicio.codigo`, `servicio.nombre`;
- `periodo.inicio`, `periodo.fin`, `periodo.texto`;
- `pago.valor`, `pago.recibo`, `pago.fecha`;
- `factura.numero`;
- `envio.id`: el identificador del envío, **el mismo en todos los reintentos** (para que el sistema
  externo no aplique dos veces);
- `cred.<campo>`: sólo en la autenticación.

Una variable desconocida, un formato inventado o una credencial fuera de la autenticación impiden
publicar.

### Mapa de respuestas

Reglas en orden: código HTTP y, si se quiere, un texto que la respuesta contiene (sin distinguir
mayúsculas ni tildes). `status: 0` es un error de red o tiempo agotado. Resultados: `Done`,
`DoneWithWarning`, `Retry`, `ReAuth`, `ManualReview`. **Lo no previsto es «revisar a mano».**

## 2. Simular

En la ficha del conector, «Simular» arma la petición con datos de ejemplo (o los que se escriban,
`variable=valor` por línea) **sin salir a la red**: método, URL, cabeceras (sin secretos) y cuerpo.

## 3. Habilitar por cooperativa

En la ficha, cada cooperativa se habilita o deshabilita con motivo. La copia en la base de la cooperativa
la deja al día una tarea en segundos. Deshabilitar deja **en espera** los envíos pendientes; habilitar de
nuevo los despierta.

## 4. Credenciales

En **Administración › Conectores externos** la cooperativa pone sus credenciales: se guardan **cifradas** y
**no se vuelven a mostrar**. Un campo que se deja vacío conserva lo que tenía. «Probar conexión» se
autentica y dice si conectó, sin mostrar el token. Poner credenciales despierta los envíos que esperaban.

## 5. Tablero de envíos

**Transporte › Envíos a sistemas externos**. Los envíos salen en segundo plano por la bandeja de
integración, con reintentos crecientes hasta `maxAttempts` llamadas reales (la espera con el conector
deshabilitado no cuenta). Agotados o con una respuesta para revisar, llega la alerta
`Conector.ReintentosAgotados`. Desde el detalle se reintenta (devuelve el cupo de intentos, mismo
identificador) o se cierra a mano con motivo.
