/* =============================================================================
   Passkeys (WebAuthn) — el único puente con `navigator.credentials`.

   Módulo ES, no script global: se importa desde C# con
   `IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/…")`, y así
   sólo se descarga en las dos pantallas que lo usan. El `./` es obligatorio:
   Blazor lo resuelve contra `document.baseURI`, y sin él la ruta se resolvería
   contra `_framework/` y el import fallaría con un 404.

   -----------------------------------------------------------------------------
   POR QUÉ ESTE ARCHIVO ES CASI TODO CONVERSIÓN DE BASE64URL

   El navegador habla `ArrayBuffer` y el servidor habla JSON. WebAuthn no define
   ninguna serialización para el intercambio, así que cada implementación elige
   la suya, y Fido2NetLib eligió base64url para todo `byte[]` (su
   `Base64UrlConverter`). Los campos son estos y no hay más:

     entran (servidor → navegador)   challenge, user.id,
                                     excludeCredentials[].id, allowCredentials[].id
     salen  (navegador → servidor)   rawId, clientDataJSON, attestationObject,
                                     authenticatorData, signature, userHandle

   Olvidar UNO no da error de sintaxis ni excepción clara: da un `TypeError` del
   navegador o una firma que no verifica en el servidor, que es lo mismo que
   «no funciona» sin decir por qué. Por eso están enumerados arriba.

   -----------------------------------------------------------------------------
   NO SE LANZA NADA HACIA C#

   Todas las funciones devuelven `{ ok, motivo, mensaje, respuestaJson }`. Una
   excepción que cruza el interop llega al C# como `JSException` con el mensaje
   ya aplanado, y distinguir «la persona cerró el diálogo» de «el dominio está
   mal configurado» pasaría a ser comparar cadenas. Son dos situaciones
   opuestas: una no merece ni un aviso rojo, la otra es una avería que hay que
   registrar.
   ========================================================================== */

/* Motivos que entiende el C#. Cambiar uno obliga a cambiar WebAuthnInterop.cs. */
const CANCELADO = 'cancelado';
const YA_INSCRITA = 'yaInscrita';
const CONFIGURACION = 'configuracion';
const NO_SOPORTADO = 'noSoportado';
const ERROR = 'error';

/* ---------- base64url ---------- */

function aBytes(base64url) {
    const base64 = String(base64url).replace(/-/g, '+').replace(/_/g, '/');
    const relleno = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    const binario = atob(relleno);
    const bytes = new Uint8Array(binario.length);
    for (let i = 0; i < binario.length; i++) {
        bytes[i] = binario.charCodeAt(i);
    }
    return bytes;
}

function aTexto(buffer) {
    const bytes = new Uint8Array(buffer);
    let binario = '';
    /* De a trozos: `String.fromCharCode.apply` con un array de decenas de miles
       de elementos desborda la pila de argumentos. Un attestationObject grande
       llega ahí. */
    const TROZO = 0x8000;
    for (let i = 0; i < bytes.length; i += TROZO) {
        binario += String.fromCharCode.apply(null, bytes.subarray(i, i + TROZO));
    }
    return btoa(binario)
        .replace(/\+/g, '-')
        .replace(/\//g, '_')
        .replace(/=+$/, '');
}

/** Los descriptores de credencial llevan el id en base64url; el resto se copia. */
function convertirDescriptores(lista) {
    if (!Array.isArray(lista)) return [];
    return lista.map(d => Object.assign({}, d, { id: aBytes(d.id) }));
}

/* ---------- Disponibilidad ---------- */

/**
 * Si esto da false, el botón no se muestra. Ofrecer un botón que sólo puede
 * fallar es peor que no ofrecerlo.
 *
 * `isSecureContext` es la comprobación que más veces sorprende: sobre http://
 * en una IP de red local la API existe pero rechaza todo, y el error que sale
 * es un `NotAllowedError` idéntico al de cancelar.
 */
export function estaDisponible() {
    return typeof window !== 'undefined'
        && window.isSecureContext === true
        && typeof window.PublicKeyCredential === 'function'
        && !!(navigator.credentials && navigator.credentials.create);
}

/* ---------- Alta ---------- */

export async function inscribir(opcionesJson) {
    if (!estaDisponible()) {
        return fallo(NO_SOPORTADO, 'Este navegador no admite llaves de seguridad.');
    }

    let opciones;
    try {
        opciones = JSON.parse(opcionesJson);
        opciones.challenge = aBytes(opciones.challenge);
        opciones.user.id = aBytes(opciones.user.id);
        opciones.excludeCredentials = convertirDescriptores(opciones.excludeCredentials);
    } catch (e) {
        return fallo(ERROR, 'Las opciones que envió el servidor no se pudieron leer.');
    }

    let credencial;
    try {
        credencial = await navigator.credentials.create({ publicKey: opciones });
    } catch (e) {
        return clasificar(e, true);
    }

    if (!credencial) {
        return fallo(CANCELADO, 'No se creó ninguna llave.');
    }

    const extensiones = credencial.getClientExtensionResults
        ? credencial.getClientExtensionResults()
        : {};

    const respuesta = {
        id: credencial.id,
        rawId: aTexto(credencial.rawId),
        type: credencial.type,
        response: {
            attestationObject: aTexto(credencial.response.attestationObject),
            clientDataJSON: aTexto(credencial.response.clientDataJSON),
            /* Con qué se conectó la llave —usb, nfc, ble, internal—. Sólo sirve
               para que la próxima vez el navegador sugiera el medio correcto en
               vez de ofrecerlos todos. Si el navegador no lo expone, se omite. */
            transports: credencial.response.getTransports
                ? credencial.response.getTransports()
                : [],
        },
        /* El mismo valor bajo los dos nombres a propósito: el intercambio no
           tiene un nombre canónico para esto —la especificación dice
           `clientExtensionResults` y las librerías de servidor suelen leer
           `extensions`— y el deserializador descarta el que no conozca. Mandar
           los dos cuesta unos bytes; mandar el que no era cuesta un campo vacío
           que nadie relaciona con esta línea. */
        extensions: extensiones,
        clientExtensionResults: extensiones,
    };

    return { ok: true, motivo: null, mensaje: null, respuestaJson: JSON.stringify(respuesta) };
}

/* ---------- Ingreso ---------- */

export async function firmar(opcionesJson) {
    if (!estaDisponible()) {
        return fallo(NO_SOPORTADO, 'Este navegador no admite llaves de seguridad.');
    }

    let opciones;
    try {
        opciones = JSON.parse(opcionesJson);
        opciones.challenge = aBytes(opciones.challenge);
        opciones.allowCredentials = convertirDescriptores(opciones.allowCredentials);
    } catch (e) {
        return fallo(ERROR, 'Las opciones que envió el servidor no se pudieron leer.');
    }

    let credencial;
    try {
        credencial = await navigator.credentials.get({ publicKey: opciones });
    } catch (e) {
        return clasificar(e, false);
    }

    if (!credencial) {
        return fallo(CANCELADO, 'No se usó ninguna llave.');
    }

    const extensiones = credencial.getClientExtensionResults
        ? credencial.getClientExtensionResults()
        : {};

    const respuesta = {
        id: credencial.id,
        rawId: aTexto(credencial.rawId),
        type: credencial.type,
        response: {
            authenticatorData: aTexto(credencial.response.authenticatorData),
            clientDataJSON: aTexto(credencial.response.clientDataJSON),
            signature: aTexto(credencial.response.signature),
            /* Va null y no cadena vacía cuando la llave no lo devuelve: una
               cadena vacía se decodifica a un array de cero bytes, que no es lo
               mismo que «no vino». */
            userHandle: credencial.response.userHandle
                ? aTexto(credencial.response.userHandle)
                : null,
        },
        extensions: extensiones,
        clientExtensionResults: extensiones,
    };

    return { ok: true, motivo: null, mensaje: null, respuestaJson: JSON.stringify(respuesta) };
}

/* ---------- Errores ---------- */

function fallo(motivo, mensaje) {
    return { ok: false, motivo: motivo, mensaje: mensaje, respuestaJson: null };
}

/**
 * El navegador distingue muy poco, así que hay que sacarle todo lo que da.
 *
 * `NotAllowedError` es el caso incómodo: lo devuelve tanto quien cierra el
 * diálogo como quien deja pasar el tiempo, y la especificación lo hace a
 * propósito para no revelar si la llave existía. No se puede separar, así que
 * se trata como cancelación — que es lo más frecuente con diferencia y lo único
 * que no merece un aviso de error.
 */
function clasificar(e, esAlta) {
    const nombre = e && e.name ? e.name : '';

    if (nombre === 'NotAllowedError' || nombre === 'AbortError') {
        return fallo(CANCELADO, 'Se canceló la operación con la llave.');
    }

    if (nombre === 'InvalidStateError') {
        return esAlta
            ? fallo(YA_INSCRITA, 'Esa llave ya está inscrita en tu cuenta.')
            : fallo(ERROR, 'La llave no se pudo usar.');
    }

    if (nombre === 'SecurityError') {
        /* No es culpa de quien usa la aplicación: el dominio configurado no
           coincide con el que sirve la página. Se distingue porque el arreglo
           está en el servidor, y el C# lo registra en vez de tratarlo como un
           tropiezo del usuario. */
        return fallo(CONFIGURACION,
            'La configuración del dominio no permite usar llaves aquí. Avisá a soporte.');
    }

    if (nombre === 'NotSupportedError' || nombre === 'ConstraintError') {
        return fallo(NO_SOPORTADO, 'Esta llave no es compatible con lo que pide el sistema.');
    }

    return fallo(ERROR, e && e.message ? e.message : 'No se pudo completar la operación.');
}
