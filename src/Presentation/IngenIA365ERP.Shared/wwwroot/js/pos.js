// Feature 012, I3 (T634; FR-019, FR-059, SC-008). El teclado, el lector y la impresión del punto de venta.
//
// Es un global (window.ingeniaPos), no un módulo ES: se carga UNA vez, con huella, desde App.razor (@Assets) y desde index.html del
// cliente MAUI, igual que descargas.js. Lo usan PuntoDeVenta.razor y los componentes de Components/Pos.
//
// - Teclas reservadas: mientras el POS está montado, F2, F3, F4, F6, F7, F8, F9, F10, Supr, Esc y Ctrl+Alt+M/C/R/D no hacen lo del
//   navegador (F5 no se reserva: recargar sigue siendo recargar). Se avisan al C# por Tecla(nombre).
// - Lector: un lector de códigos «teclea» muy rápido. Una ráfaga de caracteres con menos de 30 ms entre uno y otro, terminada en
//   Enter, es una lectura aunque el foco esté en otro campo; se avisa por Lector(texto) y no se deja caer en ese campo.
// - Impresión: en un iframe oculto, para que salga sólo la tirilla y no la pantalla. En el WebView de Android window.print no existe:
//   ahí imprime la implementación nativa (IImpresionDeDocumentos de la app).
// - La caja del equipo en localStorage con try/catch: puede no haber almacenamiento (ventana privada, datos bloqueados) y decide el
//   servidor igual.
// - I6 (T941): la serie de un producto que la controla llega por el mismo camino que cualquier lectura (el campo o la ráfaga): quien
//   decide que esa lectura es una serie es PuntoDeVenta.razor, que la espera tras Inventory.Serial.Required. Aquí no cambia nada: el
//   lector no distingue un código de producto de una serie, y no debe hacerlo.
// El interop se prueba a mano: no hay pruebas de navegador en el repositorio.
(function () {
    const RAFAGA_MS = 30;
    const MINIMO_DE_RAFAGA = 4;
    const CLAVE_CAJA = 'ingenia.pos.caja';
    const RESERVADAS = new Set(['F2', 'F3', 'F4', 'F6', 'F7', 'F8', 'F9', 'F10', 'Delete', 'Escape']);
    const CON_CTRL_ALT = new Set(['m', 'c', 'r', 'd']);

    let dotnet = null;
    let extras = new Set();
    let campo = null;
    let rafaga = '';
    let ultima = 0;

    function nombreDeTecla(e) {
        if (e.ctrlKey && e.altKey && CON_CTRL_ALT.has((e.key || '').toLowerCase())) return 'Ctrl+Alt+' + e.key.toUpperCase();
        if (RESERVADAS.has(e.key) || extras.has(e.key)) return e.key;
        return null;
    }

    function alPulsar(e) {
        if (!dotnet) return;

        const tecla = nombreDeTecla(e);
        if (tecla) {
            e.preventDefault();
            e.stopPropagation();
            rafaga = '';
            dotnet.invokeMethodAsync('Tecla', tecla);
            return;
        }

        const ahora = performance.now();
        const enElCampo = campo && document.activeElement === campo;

        if (e.key === 'Enter') {
            const esRafaga = rafaga.length >= MINIMO_DE_RAFAGA && ahora - ultima < RAFAGA_MS * 3;
            if (esRafaga && !enElCampo) {
                e.preventDefault();
                e.stopPropagation();
                const texto = rafaga;
                rafaga = '';
                dotnet.invokeMethodAsync('Lector', texto);
                return;
            }
            rafaga = '';
            return;
        }

        if (e.key && e.key.length === 1 && !e.ctrlKey && !e.altKey && !e.metaKey) {
            rafaga = (ahora - ultima < RAFAGA_MS) ? rafaga + e.key : e.key;
            ultima = ahora;
        }
    }

    window.ingeniaPos = {
        /** Monta el teclado del POS; `idDelCampo` es el campo de lectura, que siempre recupera el foco. */
        montar(referencia, idDelCampo) {
            dotnet = referencia;
            campo = idDelCampo ? document.getElementById(idDelCampo) : null;
            document.addEventListener('keydown', alPulsar, true);
            if (campo) campo.focus();
        },

        desmontar() {
            document.removeEventListener('keydown', alPulsar, true);
            dotnet = null;
            campo = null;
            rafaga = '';
            extras = new Set();
        },

        /** Reserva además las teclas rápidas de los medios de pago (F1, F11…) mientras el panel de cobro está abierto; [] las suelta. */
        reservar(teclas) {
            extras = new Set((teclas || []).filter(t => t && t !== 'F5'));
        },

        enfocar(id) {
            const el = id ? document.getElementById(id) : campo;
            if (el) { el.focus(); if (el.select) el.select(); }
        },

        /** Imprime `html` (un documento completo) en un iframe oculto y lo retira al terminar. */
        imprimir(html) {
            return new Promise((resolve) => {
                if (typeof window.print !== 'function') { resolve(false); return; }
                const marco = document.createElement('iframe');
                marco.setAttribute('aria-hidden', 'true');
                marco.style.position = 'fixed';
                marco.style.width = '0';
                marco.style.height = '0';
                marco.style.border = '0';
                marco.style.right = '0';
                marco.style.bottom = '0';
                document.body.appendChild(marco);
                const doc = marco.contentWindow.document;
                doc.open();
                doc.write(html);
                doc.close();
                const terminar = () => { setTimeout(() => marco.remove(), 500); resolve(true); };
                marco.contentWindow.onafterprint = terminar;
                setTimeout(() => {
                    try { marco.contentWindow.focus(); marco.contentWindow.print(); }
                    catch (err) { console.warn('No se pudo imprimir la tirilla.', err); marco.remove(); resolve(false); return; }
                    setTimeout(terminar, 60000);
                }, 50);
            });
        },

        leerCaja() {
            try { return window.localStorage.getItem(CLAVE_CAJA); }
            catch (err) { return null; }
        },

        guardarCaja(valor) {
            try {
                if (valor) window.localStorage.setItem(CLAVE_CAJA, valor);
                else window.localStorage.removeItem(CLAVE_CAJA);
                return true;
            } catch (err) { return false; }
        }
    };
})();
