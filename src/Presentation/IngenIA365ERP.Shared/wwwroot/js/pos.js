// Feature 012, I3 (T634; FR-019, FR-059, SC-008). El teclado, el lector y la impresión del punto de venta.
//
// Es un global (window.ingeniaPos), no un módulo ES: se carga UNA vez, con huella, desde App.razor (@Assets) y desde index.html del
// cliente MAUI, igual que descargas.js. Lo usan PuntoDeVenta.razor y los componentes de Components/Pos.
//
// - Teclas reservadas: mientras el POS está montado, F2, F3, F4, F6, F7, F8, F9, F10, Supr, Esc y Ctrl+Alt+M/C/R/D/O no hacen lo del
//   navegador (F5 no se reserva: recargar sigue siendo recargar). Se avisan al C# por Tecla(nombre). Ctrl+Alt+O (2026-11-12) carga una
//   cotización; la Q no, porque en los teclados en español AltGr+Q (que llega como Ctrl+Alt) escribe la arroba de un correo.
// - Lector: un lector de códigos «teclea» muy rápido. Una ráfaga de caracteres con menos de 30 ms entre uno y otro, terminada en
//   Enter, es una lectura aunque el foco esté en otro campo; se avisa por Lector(texto) y no se deja caer en ese campo.
// - Impresión: en un iframe oculto, para que salga sólo la tirilla y no la pantalla. En el WebView de Android window.print no existe:
//   ahí imprime la implementación nativa (IImpresionDeDocumentos de la app).
// - La caja del equipo en localStorage con try/catch: puede no haber almacenamiento (ventana privada, datos bloqueados) y decide el
//   servidor igual.
// - I6 (T941): la serie de un producto que la controla llega por el mismo camino que cualquier lectura (el campo o la ráfaga): quien
//   decide que esa lectura es una serie es PuntoDeVenta.razor, que la espera tras Inventory.Serial.Required. Aquí no cambia nada: el
//   lector no distingue un código de producto de una serie, y no debe hacerlo.
// - 2026-11-12 (pedido del dueño): con la tirilla en pantalla (entregada(true)), Enter en cualquier parte que no sea un control ni un
//   diálogo se avisa como Tecla('Enter'): es «Nueva venta», y el foco ya no tiene que estar en el campo de lectura. La ráfaga del lector
//   sigue siendo una lectura (la pantalla empieza con ella la venta siguiente). Supr dentro de un diálogo (el cobro, un motivo) borra lo
//   escrito, no quita la línea elegida. Al cerrar el diálogo de impresión el foco vuelve a esta ventana, no se queda en el iframe.
// El interop se prueba a mano: no hay pruebas de navegador en el repositorio.
(function () {
    const RAFAGA_MS = 30;
    const MINIMO_DE_RAFAGA = 4;
    const CLAVE_CAJA = 'ingenia.pos.caja';
    const RESERVADAS = new Set(['F2', 'F3', 'F4', 'F6', 'F7', 'F8', 'F9', 'F10', 'Delete', 'Escape']);
    const CON_CTRL_ALT = new Set(['m', 'c', 'r', 'd', 'o']);
    const CAPAS = '.e-dialog, [role="dialog"], .e-sidebar';
    const CONTROLES = 'input, select, textarea, button, a[href], [contenteditable="true"], [role="button"], [role="option"], [role="combobox"], [role="listbox"], [tabindex]:not([tabindex="-1"])';

    let dotnet = null;
    let extras = new Set();
    let idDelCampo = null;
    let rafaga = '';
    let ultima = 0;
    let entregada = false;

    function enUnaCapa(el) {
        return el instanceof Element && !!el.closest(CAPAS);
    }

    /** Tras la venta, ¿este Enter es «Nueva venta»? Sí, salvo sobre un control (un botón hace lo suyo) o dentro de un diálogo o del menú. */
    function enterLibre(el) {
        if (!(el instanceof Element) || el === document.body || el === document.documentElement) return true;
        return !enUnaCapa(el) && !el.closest(CONTROLES);
    }

    function nombreDeTecla(e) {
        if (e.ctrlKey && e.altKey && CON_CTRL_ALT.has((e.key || '').toLowerCase())) return 'Ctrl+Alt+' + e.key.toUpperCase();
        if (e.key === 'Delete' && enUnaCapa(e.target)) return null;
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
        // Por id y no por el elemento guardado al montar: si Blazor rehace el campo, el guardado ya no es el de la pantalla.
        const enElCampo = !!idDelCampo && !!document.activeElement && document.activeElement.id === idDelCampo;

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
            if (entregada && !enElCampo && !e.defaultPrevented && enterLibre(e.target)) {
                e.preventDefault();
                e.stopPropagation();
                dotnet.invokeMethodAsync('Tecla', 'Enter');
            }
            return;
        }

        if (e.key && e.key.length === 1 && !e.ctrlKey && !e.altKey && !e.metaKey) {
            rafaga = (ahora - ultima < RAFAGA_MS) ? rafaga + e.key : e.key;
            ultima = ahora;
        }
    }

    window.ingeniaPos = {
        /** Monta el teclado del POS; `idDelCampo` es el campo de lectura, que siempre recupera el foco. */
        montar(referencia, idCampo) {
            dotnet = referencia;
            idDelCampo = idCampo || null;
            document.addEventListener('keydown', alPulsar, true);
            const campo = idDelCampo ? document.getElementById(idDelCampo) : null;
            if (campo) campo.focus();
        },

        desmontar() {
            document.removeEventListener('keydown', alPulsar, true);
            dotnet = null;
            idDelCampo = null;
            rafaga = '';
            extras = new Set();
            entregada = false;
        },

        /** 2026-11-12: true mientras la tirilla de la venta cerrada está en pantalla; Enter (fuera de controles y diálogos) es «Nueva venta». */
        entregada(valor) {
            entregada = !!valor;
        },

        /** Reserva además las teclas rápidas de los medios de pago (F1, F11…) mientras el panel de cobro está abierto; [] las suelta. */
        reservar(teclas) {
            extras = new Set((teclas || []).filter(t => t && t !== 'F5'));
        },

        enfocar(id) {
            const el = document.getElementById(id || idDelCampo || '');
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
                let terminado = false;
                const terminar = () => {
                    if (terminado) return;
                    terminado = true;
                    // El foco quedó en el iframe que se va a retirar: vuelve a esta ventana para que la pantalla lo ponga donde va.
                    try { window.focus(); } catch (err) { /* sin foco que devolver */ }
                    setTimeout(() => marco.remove(), 500);
                    resolve(true);
                };
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
