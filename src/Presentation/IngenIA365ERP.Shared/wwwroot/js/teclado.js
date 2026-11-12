// Feature 022 (T050; FR-001 a FR-006, FR-010 a FR-012). La capa de teclado de toda la aplicación.
//
// Es un global (window.ingeniaTeclado), no un módulo ES: se carga UNA vez, con huella, desde App.razor (@Assets) y desde el
// index.html de la app MAUI, como pos.js. No decide reglas de negocio: mueve el foco, muestra ayudas y avisa teclas al .NET.
//
// - Enter avanza: Enter pasa al siguiente campo de la ZONA (el ancestro con data-zona-teclado, el diálogo abierto o la página) y
//   Shift+Enter al anterior. En el último campo lleva al elemento de data-siguiente de la zona o, si no hay, a su botón principal
//   (data-principal o el primer .e-primary); Enter sobre un botón lo ejecuta el navegador (Q4: nadie confirma por accidente).
//   No actúa si la preferencia está apagada (Q3), si el evento ya fue prevenido, en áreas de texto, editores, grillas, listas
//   abiertas (aria-expanded="true"), con Alt/Ctrl/Meta, ni en un campo con data-enter="propio" o con manejador de teclado de
//   Blazor (defensa ante un campo que se olvidó marcar). Escucha en window, en burbuja: corre después de Blazor y Syncfusion.
// - Selección al entrar: al llegar a un campo de texto o valor por teclado, su contenido queda seleccionado (FR-005).
// - Ayuda F1: muestra junto al campo enfocado el texto de data-ayuda (del campo o de su envoltorio) o una ayuda genérica con la
//   etiqueta, el tipo de campo y sus teclas. Esc, F1 o salir del campo la cierran; ese Esc no llega a la pantalla.
// - data-prevenir="ArrowUp ArrowDown Enter": previene esas teclas en el campo sin detener el evento (Blazor lo sigue recibiendo).
// - data-lector="#campo-de-producto" (2026-11-12): en un campo de cantidad, una ráfaga del lector de códigos (8 dígitos o más, menos de
//   30 ms entre uno y otro, como en pos.js) terminada en Enter no es una cantidad: el campo vuelve a lo que tenía y la lectura se escribe
//   en el campo de producto con su Enter. Así la segunda lectura no queda como una cantidad enorme si el foco se quedó en la línea.
// - Atajos de pantalla: registrar(ref, teclas) apila; el último registro decide primero. Las F y las combinaciones con Ctrl se
//   avisan siempre; Enter y Supr sólo fuera de un campo editable; Esc sólo si no hay una lista, un diálogo o la ayuda abiertos.
// El interop se prueba a mano: no hay pruebas de navegador en el repositorio.
(function () {
    const CAMPOS = 'input, select, textarea, [contenteditable="true"], [role="combobox"], [role="spinbutton"], [role="textbox"]';
    const TIPOS_SIN_CAMPO = new Set(['hidden', 'button', 'submit', 'reset', 'image', 'file', 'range', 'color']);
    const TIPOS_DE_TEXTO = new Set(['text', 'search', 'number', 'tel', 'email', 'url', '']);
    const NO_ENTER = '.e-grid, .e-richtexteditor, .e-rte-content, [data-enter="propio"]';
    const EDITABLES = 'input, select, textarea, [contenteditable="true"], [role="combobox"], [role="textbox"], [role="spinbutton"]';
    const INTERACTIVOS = 'input, select, textarea, button, a[href], [contenteditable="true"], [role="combobox"], [role="option"], [role="button"]';

    const estado = { enterAvanza: true };
    const pila = [];
    let siguienteId = 1;
    let porTeclado = false;
    let globo = null;
    let globoDe = null;

    // ------------------------------------------------------------------------------------- utilidades --

    function visible(el) {
        if (!el || !el.isConnected) return false;
        if (!(el.offsetWidth || el.offsetHeight || el.getClientRects().length)) return false;
        const estilo = window.getComputedStyle(el);
        return estilo.visibility !== 'hidden' && estilo.display !== 'none';
    }

    function esCampo(el) {
        if (!el || !el.matches || !el.matches(CAMPOS)) return false;
        if (el.disabled || el.getAttribute('aria-disabled') === 'true') return false;
        if (el.tabIndex < 0) return false;
        if (el.tagName === 'INPUT' && TIPOS_SIN_CAMPO.has((el.type || '').toLowerCase())) return false;
        // Sólo lectura no es un campo para digitar, salvo las listas de Syncfusion, que se abren y eligen con el teclado.
        if (el.readOnly && el.getAttribute('role') !== 'combobox' && !el.classList.contains('e-dropdownlist')) return false;
        return visible(el);
    }

    function zonaDe(el) {
        return el.closest('[data-zona-teclado]')
            || el.closest('.e-dialog, [role="dialog"]')
            || el.closest('.pagina, form')
            || document.body;
    }

    function camposDe(zona) {
        return Array.from(zona.querySelectorAll(CAMPOS)).filter(esCampo);
    }

    /** El manejador de teclado de Blazor del elemento, si lo tiene (Blazor guarda sus manejadores en una propiedad _blazorEvents_N). */
    function tieneTeclasDeBlazor(el) {
        for (const clave of Object.keys(el)) {
            if (!clave.startsWith('_blazorEvents_')) continue;
            const info = el[clave];
            const h = info && info.handlers;
            if (h && (h.keydown || h.keyup || h.keypress)) return true;
        }
        return false;
    }

    function hayListaAbierta(el) {
        return el.getAttribute('aria-expanded') === 'true';
    }

    function hayCapaAbierta() {
        if (globo && globo.hidden === false) return true;
        const activo = document.activeElement;
        if (activo && activo.getAttribute && activo.getAttribute('aria-expanded') === 'true') return true;
        return Array.from(document.querySelectorAll('.e-popup-open, .e-dialog')).some(visible);
    }

    function enfocarElemento(el, seleccionar) {
        if (!el) return false;
        el.focus();
        if (seleccionar !== false && typeof el.select === 'function' && el.tagName === 'INPUT' && TIPOS_DE_TEXTO.has((el.type || '').toLowerCase())) {
            try { el.select(); } catch (err) { /* algunos tipos no admiten select() */ }
        }
        return document.activeElement === el;
    }

    function botonPrincipal(zona) {
        const marcado = Array.from(zona.querySelectorAll('[data-principal]')).find(b => visible(b) && !b.disabled);
        if (marcado) return marcado;
        return Array.from(zona.querySelectorAll('button.e-primary, a.e-btn.e-primary')).find(b => visible(b) && !b.disabled) || null;
    }

    function destinoAlTerminar(zona) {
        const selector = zona.getAttribute && zona.getAttribute('data-siguiente');
        if (selector) {
            const destino = document.querySelector(selector);
            if (destino && visible(destino)) {
                if (esCampo(destino) || destino.tabIndex >= 0) return destino;
                const primero = camposDe(destino)[0] || destino.querySelector('button, [tabindex="0"]');
                if (primero) return primero;
            }
        }
        return botonPrincipal(zona);
    }

    // ------------------------------------------------------------------------------------ Enter avanza --

    function avanzar(desde, atras) {
        const zona = zonaDe(desde);
        const campos = camposDe(zona);
        let i = campos.indexOf(desde);
        if (i < 0) return false;
        // Un grupo de radios es un solo campo.
        const grupo = desde.type === 'radio' && desde.name ? desde.name : null;
        do { i += atras ? -1 : 1; } while (grupo && i >= 0 && i < campos.length && campos[i].type === 'radio' && campos[i].name === grupo);
        if (i >= 0 && i < campos.length) return enfocarElemento(campos[i]);
        if (atras) return false;
        const destino = destinoAlTerminar(zona);
        return destino ? enfocarElemento(destino) : false;
    }

    function alPulsarEnter(e) {
        if (e.key !== 'Enter' || e.defaultPrevented || e.isComposing) return;
        if (e.altKey || e.ctrlKey || e.metaKey) return;
        if (!estado.enterAvanza) return;
        const el = e.target;
        if (!(el instanceof Element) || !esCampo(el)) return;
        if (el.tagName === 'TEXTAREA' || el.isContentEditable) return;
        if (el.closest(NO_ENTER)) return;
        if (hayListaAbierta(el)) return;
        // data-enter="avanza": el campo tiene teclas propias pero no usa Enter. Sin marca y con manejador de Blazor, no se toca.
        if (el.getAttribute('data-enter') !== 'avanza' && tieneTeclasDeBlazor(el)) return;
        if (avanzar(el, e.shiftKey)) e.preventDefault();
    }

    // --------------------------------------------------------------------------------- ayuda de campo --

    function textoPropio(nodo) {
        let texto = '';
        for (const hijo of nodo.childNodes) {
            if (hijo.nodeType === Node.TEXT_NODE) texto += hijo.textContent;
            else if (hijo.nodeType === Node.ELEMENT_NODE && !hijo.matches(EDITABLES + ', button, .e-input-group, ul, table') && !hijo.querySelector(EDITABLES)) texto += hijo.textContent;
        }
        return texto.replace(/\s+/g, ' ').replace(/\s*\*\s*$/, ' *').trim();
    }

    function etiquetaDe(el) {
        const propia = el.getAttribute('aria-label');
        if (propia) return propia;
        const etiqueta = (el.labels && el.labels[0]) || el.closest('label, .campo');
        if (etiqueta) {
            const t = textoPropio(etiqueta);
            if (t) return t;
        }
        return el.getAttribute('placeholder') || el.getAttribute('title') || '';
    }

    function clase(el) {
        if (el.getAttribute('data-valor') !== null || el.getAttribute('inputmode') === 'decimal') return 'valor';
        if (el.tagName === 'SELECT' || el.getAttribute('role') === 'combobox') return 'lista';
        if (el.tagName === 'TEXTAREA') return 'texto-largo';
        const tipo = (el.type || '').toLowerCase();
        if (tipo === 'checkbox' || tipo === 'radio') return 'casilla';
        if (tipo === 'date' || el.classList.contains('e-datepicker')) return 'fecha';
        if (tipo === 'number') return 'numero';
        return 'texto';
    }

    const GENERICAS = {
        'valor': 'Escriba el valor en pesos, con o sin puntos de miles (50000 o 50.000). Al salir se muestra con su formato.',
        'numero': 'Escriba el número; ↑ ↓ lo suben o bajan de a uno.',
        'fecha': 'Escriba la fecha (día, mes y año) o use ↑ ↓ sobre cada parte.',
        'lista': 'Escriba la primera letra de la opción o use ↑ ↓ para recorrerlas.',
        'casilla': 'Espacio la marca o la desmarca.',
        'texto-largo': 'Puede escribir varias líneas: aquí Enter hace un salto de línea; Tab pasa al siguiente campo.',
        'texto': 'Escriba el dato.',
    };

    function teclasDe(el, tipo) {
        const teclas = [];
        if (tipo !== 'texto-largo' && estado.enterAvanza && !el.closest('[data-enter="propio"]')) teclas.push('Enter: siguiente campo', 'Shift+Enter: anterior');
        teclas.push('Tab: siguiente', 'Esc: cerrar esta ayuda');
        return teclas;
    }

    function crearGlobo() {
        globo = document.createElement('div');
        globo.className = 'ayuda-de-campo';
        globo.setAttribute('role', 'tooltip');
        globo.id = 'ayuda-de-campo';
        globo.hidden = true;
        document.body.appendChild(globo);
        return globo;
    }

    function mostrarAyuda(el) {
        const g = globo || crearGlobo();
        const envoltorio = el.closest('[data-ayuda]');
        const propia = envoltorio && envoltorio.getAttribute('data-ayuda');
        const tipo = clase(el);
        const titulo = etiquetaDe(el);
        g.replaceChildren();
        if (titulo) {
            const t = document.createElement('div');
            t.className = 'ayuda-de-campo-titulo';
            t.textContent = titulo;
            g.appendChild(t);
        }
        const p = document.createElement('div');
        p.className = 'ayuda-de-campo-texto';
        p.textContent = propia || GENERICAS[tipo] || GENERICAS.texto;
        g.appendChild(p);
        const k = document.createElement('div');
        k.className = 'ayuda-de-campo-teclas';
        k.textContent = teclasDe(el, tipo).join(' · ');
        g.appendChild(k);

        g.hidden = false;
        const r = el.getBoundingClientRect();
        const ancho = Math.min(360, window.innerWidth - 16);
        g.style.maxWidth = ancho + 'px';
        let izquierda = Math.max(8, Math.min(r.left, window.innerWidth - ancho - 8));
        let arriba = r.bottom + 6;
        const alto = g.offsetHeight;
        if (arriba + alto > window.innerHeight - 8) arriba = Math.max(8, r.top - alto - 6);
        g.style.left = (izquierda + window.scrollX) + 'px';
        g.style.top = (arriba + window.scrollY) + 'px';
        el.setAttribute('aria-describedby', 'ayuda-de-campo');
        globoDe = el;
    }

    function cerrarAyuda() {
        if (!globo || globo.hidden) return false;
        globo.hidden = true;
        if (globoDe && globoDe.getAttribute('aria-describedby') === 'ayuda-de-campo') globoDe.removeAttribute('aria-describedby');
        globoDe = null;
        return true;
    }

    // ---------------------------------------------------------------------------------- atajos de pantalla --

    function nombreDeTecla(e) {
        let nombre = e.key;
        if (nombre === 'Del') nombre = 'Delete';
        if (nombre === 'Esc') nombre = 'Escape';
        if (nombre && nombre.length === 1) nombre = nombre.toUpperCase();
        const partes = [];
        if (e.ctrlKey) partes.push('Ctrl');
        if (e.altKey) partes.push('Alt');
        if (e.shiftKey && (e.ctrlKey || e.altKey || /^F\d{1,2}$/.test(nombre))) partes.push('Shift');
        partes.push(nombre);
        return partes.join('+');
    }

    function puedeAvisar(nombre, e) {
        const el = e.target instanceof Element ? e.target : null;
        if (nombre === 'Enter' || nombre === 'Delete') return !el || !el.closest(INTERACTIVOS);
        if (nombre === 'Escape') return !hayCapaAbierta();
        return true;
    }

    function alPulsarAtajo(e) {
        if (!pila.length || e.defaultPrevented) return;
        const nombre = nombreDeTecla(e);
        for (let i = pila.length - 1; i >= 0; i--) {
            const registro = pila[i];
            if (!registro.teclas.has(nombre)) continue;
            if (!puedeAvisar(nombre, e)) return;
            e.preventDefault();
            e.stopPropagation();
            registro.dotnet.invokeMethodAsync('Tecla', nombre).catch(err => console.warn('No se pudo avisar la tecla ' + nombre + '.', err));
            return;
        }
    }

    // ------------------------------------------------------------------------ el lector en una cantidad --

    const LECTOR_RAFAGA_MS = 30;
    const LECTOR_MINIMO = 8;
    const lector = { campo: null, texto: '', ultima: 0, valorAntes: null };

    /** Atiende la tecla si el campo tiene data-lector; true si era el Enter de una lectura y ya la llevó al campo de producto. */
    function alPulsarEnCampoConLector(e) {
        const el = e.target instanceof HTMLInputElement ? e.target : null;
        if (!el || !el.hasAttribute('data-lector')) return false;
        const ahora = performance.now();
        if (e.key === 'Enter') {
            const esLectura = lector.campo === el && /^\d+$/.test(lector.texto) && lector.texto.length >= LECTOR_MINIMO
                && ahora - lector.ultima < LECTOR_RAFAGA_MS * 3;
            const texto = lector.texto;
            const antes = lector.valorAntes;
            lector.campo = null;
            lector.texto = '';
            if (!esLectura) return false;
            const destino = document.querySelector(el.getAttribute('data-lector'));
            if (!(destino instanceof HTMLInputElement) || destino.disabled) return false;
            e.preventDefault();
            e.stopImmediatePropagation();
            el.value = antes === null ? '' : antes;
            destino.focus();
            destino.value = texto;
            destino.dispatchEvent(new Event('input', { bubbles: true }));
            destino.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', bubbles: true, cancelable: true }));
            return true;
        }
        if (e.key && e.key.length === 1 && !e.ctrlKey && !e.altKey && !e.metaKey) {
            if (lector.campo === el && ahora - lector.ultima < LECTOR_RAFAGA_MS) {
                lector.texto += e.key;
            } else {
                lector.campo = el;
                lector.texto = e.key;
                lector.valorAntes = el.value;
            }
            lector.ultima = ahora;
        }
        return false;
    }

    // ------------------------------------------------------------------------------------- escuchas --

    window.addEventListener('keydown', function (e) {
        porTeclado = true;
        if (alPulsarEnCampoConLector(e)) return;
        // La ayuda abierta se cierra con Esc antes que nada, y ese Esc no llega a la pantalla.
        if (e.key === 'Escape' && cerrarAyuda()) { e.preventDefault(); e.stopImmediatePropagation(); return; }
        if (e.key === 'F1' && !e.ctrlKey && !e.altKey && !e.metaKey) {
            e.preventDefault();
            e.stopImmediatePropagation();
            const activo = document.activeElement;
            if (globoDe && globoDe === activo) { cerrarAyuda(); return; }
            if (activo && activo !== document.body && (esCampo(activo) || activo.closest('[data-ayuda]'))) mostrarAyuda(activo);
            return;
        }
        const el = e.target instanceof Element ? e.target : null;
        const prevenir = el && el.closest('[data-prevenir]');
        if (prevenir && prevenir.getAttribute('data-prevenir').split(/\s+/).includes(e.key)) e.preventDefault();
        alPulsarAtajo(e);
    }, true);

    window.addEventListener('keydown', alPulsarEnter, false);

    window.addEventListener('pointerdown', function () { porTeclado = false; }, true);

    window.addEventListener('focusin', function (e) {
        const el = e.target;
        if (globoDe && el !== globoDe) cerrarAyuda();
        if (!porTeclado || !(el instanceof HTMLInputElement)) return;
        if (!TIPOS_DE_TEXTO.has((el.type || '').toLowerCase()) || el.readOnly || el.hasAttribute('data-sin-seleccion')) return;
        setTimeout(function () { if (document.activeElement === el) { try { el.select(); } catch (err) { /* nada */ } } }, 0);
    }, true);

    window.addEventListener('scroll', function () { cerrarAyuda(); }, true);

    // ---------------------------------------------------------------------------------- API para el .NET --

    function leerPreferenciaGuardada() {
        try {
            const p = JSON.parse(window.localStorage.getItem('erp-preferencias') || '{}');
            const v = p.enterAvanza ?? p['ui.enter-avanza'];
            if (v === false || v === 'no') estado.enterAvanza = false;
        } catch (err) { /* sin almacenamiento: queda encendido */ }
    }
    leerPreferenciaGuardada();

    window.ingeniaTeclado = {
        /** Aplica la preferencia del usuario (Q3): { enterAvanza: true|false }. */
        configurar(opciones) {
            if (opciones && typeof opciones.enterAvanza === 'boolean') estado.enterAvanza = opciones.enterAvanza;
        },

        /** Registra las teclas de una pantalla o componente; el último registro decide primero. Devuelve el identificador. */
        registrar(dotnet, teclas) {
            const id = siguienteId++;
            pila.push({ id, dotnet, teclas: new Set((teclas || []).filter(Boolean)) });
            return id;
        },

        /** Suelta un registro. */
        soltar(id) {
            const i = pila.findIndex(r => r.id === id);
            if (i >= 0) pila.splice(i, 1);
        },

        /** Enfoca un elemento (selector CSS o referencia) y selecciona su texto. */
        enfocar(objetivo) {
            const el = typeof objetivo === 'string' ? document.querySelector(objetivo) : objetivo;
            return enfocarElemento(el);
        },

        /** Enfoca el primer campo de una zona (selector CSS o referencia). */
        enfocarPrimero(objetivo) {
            const zona = typeof objetivo === 'string' ? document.querySelector(objetivo) : objetivo;
            if (!zona) return false;
            return enfocarElemento(camposDe(zona)[0]);
        },

        /** Lleva a la vista la opción marcada (aria-selected="true") de una lista. */
        mostrarMarcado(lista) {
            const contenedor = typeof lista === 'string' ? document.querySelector(lista) : lista;
            const marcado = contenedor && contenedor.querySelector('[aria-selected="true"]');
            if (marcado) marcado.scrollIntoView({ block: 'nearest' });
        },

        /** Cierra la ayuda de campo si está abierta. */
        cerrarAyuda,

        /** Una comodidad del equipo en localStorage (datáfono, últimas elecciones); nunca datos personales. Falla en silencio. */
        recordar(clave, valor) {
            try {
                if (valor === null || valor === undefined || valor === '') window.localStorage.removeItem(clave);
                else window.localStorage.setItem(clave, String(valor));
                return true;
            } catch (err) { return false; }
        },

        recordado(clave) {
            try { return window.localStorage.getItem(clave); } catch (err) { return null; }
        },
    };
})();
