// Feature 013, K5 (T0975; research R149): la huella SHA-256 de un PDF que la persona tiene en su equipo, para compararla con la del
// documento de Cartera emitido. El archivo NO sale del navegador: a .NET sólo le llega su nombre, su tamaño y la huella en hexadecimal,
// y la API compara esa huella con la guardada (GET /api/lending/documents/verify/{code}?sha256=). Por eso no pasa por InputFile ni por
// la subida de adjuntos (LosAdjuntosNoPasanPorElServidor no se toca).
//
// Es un global (window.huellaDeArchivo), no un módulo ES: se carga UNA vez, con huella, desde App.razor (@Assets) y desde index.html del
// cliente MAUI, igual que descargas.js y adjuntos.js.
(function () {
    function elemento(inputOId) {
        return typeof inputOId === 'string' ? document.getElementById(inputOId) : inputOId;
    }

    function hexadecimal(buffer) {
        return Array.from(new Uint8Array(buffer), function (b) { return b.toString(16).padStart(2, '0'); }).join('');
    }

    window.huellaDeArchivo = {
        // La huella del archivo elegido en un <input type=file>: { name, size, sha256 } con sha256 en hexadecimal minúscula; null si no se
        // eligió nada. WebCrypto no calcula por partes: el archivo se lee entero en la memoria del navegador.
        async calcular(inputOId) {
            const input = elemento(inputOId);
            const file = input && input.files && input.files[0];
            if (!file) return null;
            const huella = await crypto.subtle.digest('SHA-256', await file.arrayBuffer());
            input.value = '';
            return { name: file.name, size: file.size, sha256: hexadecimal(huella) };
        }
    };
})();
