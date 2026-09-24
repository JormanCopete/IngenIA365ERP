// Feature 011 (research R14): el archivo va del disco del usuario al almacén SIN pasar por .NET.
//
// Pasar el archivo por InputFile lo copiaría a la memoria de .NET (en el servidor con Blazor Server,
// en la pestaña con WebAssembly) y después habría que devolverlo a JavaScript para subirlo. Aquí el
// File se queda en el navegador: a .NET sólo le llegan nombre, tipo, tamaño y huella, y la respuesta
// del almacén.
//
// Es un global (window.adjuntos), no un módulo ES: se carga UNA vez, con huella, desde App.razor
// (@Assets) y desde index.html del cliente MAUI, igual que descargas.js.
(function () {
    // Tipo por extensión: Windows informa un .csv como application/vnd.ms-excel, y el servidor lo
    // rechazaría al confirmar porque un CSV no tiene la firma de un .xls. Si la extensión no está en la
    // lista, se deja el tipo que diga el navegador y el servidor decide.
    const tiposPorExtension = {
        pdf: 'application/pdf',
        png: 'image/png',
        jpg: 'image/jpeg',
        jpeg: 'image/jpeg',
        gif: 'image/gif',
        webp: 'image/webp',
        doc: 'application/msword',
        docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        xls: 'application/vnd.ms-excel',
        xlsx: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        txt: 'text/plain',
        csv: 'text/csv'
    };

    // Archivos elegidos, por un id que .NET usa para pedir la subida. No se retienen más de lo necesario.
    const elegidos = new Map();

    function tipoDe(file) {
        const punto = file.name.lastIndexOf('.');
        const extension = punto >= 0 ? file.name.slice(punto + 1).toLowerCase() : '';
        return tiposPorExtension[extension] || file.type || '';
    }

    function base64(buffer) {
        const bytes = new Uint8Array(buffer);
        let binario = '';
        for (let i = 0; i < bytes.length; i += 0x8000) {
            binario += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
        }
        return btoa(binario);
    }

    function elemento(inputOId) {
        return typeof inputOId === 'string' ? document.getElementById(inputOId) : inputOId;
    }

    window.adjuntos = {
        // Lee el archivo elegido en un <input type=file> y devuelve sólo su descripción y su huella
        // SHA-256 en base64 (la que exige el almacén). null si no se eligió nada.
        async preparar(inputOId) {
            const input = elemento(inputOId);
            const file = input && input.files && input.files[0];
            if (!file) return null;
            // WebCrypto no calcula por partes: el archivo se lee entero, en la memoria del navegador
            // (no en la del servidor). Con el tope de 25 MB no es un problema.
            const huella = await crypto.subtle.digest('SHA-256', await file.arrayBuffer());
            const id = (crypto.randomUUID && crypto.randomUUID()) || String(Date.now()) + Math.random();
            elegidos.set(id, file);
            input.value = '';
            return { id: id, name: file.name, type: tipoDe(file), size: file.size, sha256: base64(huella) };
        },

        // Envía el archivo al almacén con la autorización firmada: todos los campos, en el orden
        // recibido, y el archivo al final. Nunca lanza: devuelve { ok, status, code, error }.
        async subir(id, url, campos, campoDelArchivo) {
            const file = elegidos.get(id);
            if (!file) return { ok: false, status: 0, code: null, error: 'El archivo ya no está disponible; vuelva a elegirlo.' };
            const formulario = new FormData();
            for (const [nombre, valor] of Object.entries(campos || {})) formulario.append(nombre, valor);
            formulario.append(campoDelArchivo || 'file', file, file.name);
            try {
                // Sin cookies: la autorización es la firma, y el almacén no es de este origen.
                const respuesta = await fetch(url, { method: 'POST', body: formulario, credentials: 'omit' });
                if (respuesta.ok) {
                    elegidos.delete(id);
                    return { ok: true, status: respuesta.status, code: null, error: null };
                }
                let texto = '';
                try { texto = await respuesta.text(); } catch (e) { texto = ''; }
                // S3 responde XML (<Code>EntityTooLarge</Code>); el almacén local, el sobre JSON.
                const codigo = (texto.match(/<Code>([^<]+)<\/Code>/) || texto.match(/"code"\s*:\s*"([^"]+)"/) || [])[1] || null;
                return { ok: false, status: respuesta.status, code: codigo, error: null };
            } catch (e) {
                // Red caída, o el almacén sin CORS para este origen: el navegador no deja leer la respuesta.
                return { ok: false, status: 0, code: null, error: String(e && e.message ? e.message : e) };
            }
        },

        // Suelta un archivo elegido que no se va a subir.
        olvidar(id) {
            elegidos.delete(id);
        },

        // Descarga por navegación al enlace firmado (research R2): el almacén responde con
        // Content-Disposition: attachment, así que la página no se va. No hace falta CORS.
        descargar(url, nombre) {
            const a = document.createElement('a');
            a.href = url;
            if (nombre) a.download = nombre;
            a.rel = 'noopener';
            document.body.appendChild(a);
            a.click();
            a.remove();
        }
    };
})();
