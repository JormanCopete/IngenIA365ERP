/* =============================================================================
   Ventanas independientes, como en una aplicación de escritorio (2026-11-17).

   «Abrir en ventana nueva» abre una opción en una ventana propia —sin pestañas ni
   barra de direcciones; en la app instalada, una ventana de la app— que puede
   moverse a otro monitor. Tres formas de pedirlo:

   - el botón que aparece al pasar el ratón por una opción del menú;
   - Ctrl+clic, Mayús+clic o clic con la rueda sobre cualquier enlace de la app;
   - el botón de la barra superior, que abre la pantalla actual.

   La sesión: si la cooperativa lo permite (Security.Session.NewWindowInheritsSession),
   la ventana nueva la hereda de la que la abrió —sin volver a entrar— y las dos
   quedan en el mismo grupo (inactividad común; cerrar sesión cierra todas). Lo
   copia un guion en la cabeza de App.razor ANTES de que arranque Blazor, mirando el
   nombre de la ventana:

     erp-ventana:con-sesion:N   copia la sesión de window.opener (mismo origen);
     erp-ventana:sin-sesion:N   la borra, por si el navegador la copió solo.

   Ese guion cambia el nombre a erp-ventana:abierta para que un F5 posterior no
   vuelva a copiar nada. MAUI no carga este archivo: allí no hay ventanas.
   ========================================================================== */

window.erpVentanas = (function () {
    let heredar = true;
    let consecutivo = 0;
    let boton = null;
    let enlaceActual = null;
    let ocultarEn = 0;

    function configurar(valor) {
        heredar = !!valor;
    }

    /* Abre la ruta (relativa al sitio) en una ventana nueva. false si el navegador
       la bloqueó. La ventana sale escalonada para que no tape exacto a la anterior. */
    function abrir(ruta) {
        let url;
        try {
            url = new URL(ruta, location.href);
        } catch (e) {
            return false;
        }
        if (url.origin !== location.origin) return false;

        const ancho = Math.max(800, Math.round((window.outerWidth || screen.availWidth) * 0.85));
        const alto = Math.max(600, Math.round((window.outerHeight || screen.availHeight) * 0.85));
        const paso = 32 * ((consecutivo % 6) + 1);
        const izquierda = Math.max(0, (window.screenX || 0) + paso);
        const arriba = Math.max(0, (window.screenY || 0) + paso);
        consecutivo++;

        const nombre = 'erp-ventana:' + (heredar ? 'con-sesion:' : 'sin-sesion:') + Date.now() + '-' + consecutivo;
        const rasgos = 'popup=yes,width=' + ancho + ',height=' + alto + ',left=' + izquierda + ',top=' + arriba
            + (heredar ? '' : ',noopener');
        const w = window.open(url.pathname + url.search + url.hash, nombre, rasgos);
        // Con noopener el navegador devuelve null aunque la abra: no se sabe si la bloqueó.
        if (!heredar) return true;
        if (!w) return false;
        try { w.focus(); } catch (e) { /* otra ventana: el foco lo decide el sistema */ }
        return true;
    }

    /* ---------- Enlaces de la app ---------- */

    function enlaceInterno(el) {
        const a = el && el.closest ? el.closest('a[href]') : null;
        if (!a) return null;
        if (a.hasAttribute('download') || (a.target && a.target !== '_self')) return null;
        const href = a.getAttribute('href');
        if (!href || href.startsWith('#') || href.startsWith('javascript:') || href.startsWith('mailto:')
            || href.startsWith('tel:')) return null;
        let url;
        try { url = new URL(a.href, location.href); } catch (e) { return null; }
        if (url.origin !== location.origin || url.pathname.startsWith('/api/') || url.pathname.startsWith('/_')) return null;
        return a;
    }

    // Ctrl/Mayús/Cmd+clic: lo que en el navegador abriría una pestaña sin sesión abre una ventana de la app con ella.
    document.addEventListener('click', function (e) {
        if (e.button !== 0 || !(e.ctrlKey || e.shiftKey || e.metaKey) || e.altKey) return;
        const a = enlaceInterno(e.target);
        if (!a) return;
        e.preventDefault();
        e.stopPropagation();
        abrir(a.href);
    }, true);

    // Clic con la rueda.
    document.addEventListener('auxclick', function (e) {
        if (e.button !== 1) return;
        const a = enlaceInterno(e.target);
        if (!a) return;
        e.preventDefault();
        e.stopPropagation();
        abrir(a.href);
    }, true);

    /* ---------- El botón sobre las opciones del menú ----------
       Uno solo, flotante y fuera del árbol de Blazor (meterlo dentro de cada enlace
       le cambiaría el DOM al menú que Blazor administra). */

    function crearBoton() {
        boton = document.createElement('button');
        boton.type = 'button';
        boton.className = 'erp-abrir-ventana';
        boton.title = 'Abrir en ventana nueva (también Ctrl+clic)';
        boton.setAttribute('aria-label', 'Abrir en ventana nueva');
        boton.innerHTML = '<span class="e-icons e-open-link" aria-hidden="true"></span>';
        boton.addEventListener('mousedown', function (e) { e.preventDefault(); });
        boton.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            if (enlaceActual) abrir(enlaceActual.href);
            ocultar();
        });
        boton.addEventListener('mouseenter', function () { clearTimeout(ocultarEn); });
        boton.addEventListener('mouseleave', programarOcultar);
        document.body.appendChild(boton);
    }

    function mostrar(a) {
        if (!boton) crearBoton();
        clearTimeout(ocultarEn);
        enlaceActual = a;
        const r = a.getBoundingClientRect();
        const lado = Math.min(28, Math.max(20, r.height - 6));
        boton.style.width = lado + 'px';
        boton.style.height = lado + 'px';
        boton.style.top = Math.round(r.top + (r.height - lado) / 2) + 'px';
        boton.style.left = Math.round(r.right - lado - 4) + 'px';
        boton.classList.add('visible');
    }

    function ocultar() {
        if (boton) boton.classList.remove('visible');
        enlaceActual = null;
    }

    function programarOcultar() {
        clearTimeout(ocultarEn);
        ocultarEn = setTimeout(ocultar, 150);
    }

    document.addEventListener('mouseover', function (e) {
        const a = e.target && e.target.closest ? e.target.closest('.nav-menu a[href]') : null;
        if (a && enlaceInterno(a)) mostrar(a);
    });
    document.addEventListener('mouseout', function (e) {
        const a = e.target && e.target.closest ? e.target.closest('.nav-menu a[href]') : null;
        if (a && (!e.relatedTarget || (e.relatedTarget !== boton && !(boton && boton.contains(e.relatedTarget))))) programarOcultar();
    });
    // El menú se desplaza: el botón no puede quedarse flotando en otra opción.
    document.addEventListener('scroll', ocultar, true);

    return { configurar, abrir, disponible: function () { return true; } };
})();
