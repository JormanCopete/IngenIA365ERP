/* =============================================================================
   Pestañas que comparten una sesión (2026-11-14).

   «Duplicar pestaña» copia el sessionStorage: las dos quedan con el mismo
   refresh token, y si las dos lo canjean el servidor ve un reuso y cierra la
   sesión de ambas. Aquí viven las dos piezas que lo evitan (ver
   ICoordinacionEntrePestanas en Shared):

   - el TURNO de canje, común a todas las pestañas del navegador (Web Locks);
   - el BUZÓN (localStorage) donde la pestaña que canjeó deja, bajo el refresh
     que gastó, los tokens que recibió, para que su gemela los adopte.

   Las entradas del buzón se podan pasadas trece horas (más que la duración
   máxima de una sesión).
   ========================================================================== */

window.erpPestanas = (function () {
    const PREFIJO = 'ingenia365:sucesor:';
    const VIDA_MS = 13 * 60 * 60 * 1000;
    const turnos = new Map();
    let consecutivo = 0;

    /* Resuelve con un id cuando la pestaña tiene el turno; lo retiene hasta soltar(id).
       Sin Web Locks resuelve de una con '' (sin coordinación, como antes). Pasado
       msMaximo sin turno, rechaza: el C# lo trata como transitorio. */
    function tomar(nombre, msMaximo) {
        if (!navigator.locks || !navigator.locks.request) return Promise.resolve('');
        const corte = new AbortController();
        const reloj = setTimeout(() => corte.abort(), msMaximo);
        return new Promise((resolver, rechazar) => {
            navigator.locks.request(nombre, { signal: corte.signal }, () => {
                clearTimeout(reloj);
                return new Promise(liberar => {
                    const id = 't' + (++consecutivo);
                    turnos.set(id, liberar);
                    resolver(id);
                });
            }).catch(e => { clearTimeout(reloj); rechazar(e); });
        });
    }

    function soltar(id) {
        const liberar = turnos.get(id);
        if (liberar) {
            turnos.delete(id);
            liberar();
        }
    }

    function leer(refresh) {
        try {
            const crudo = localStorage.getItem(PREFIJO + refresh);
            if (!crudo) return null;
            const o = JSON.parse(crudo);
            return o && typeof o.v === 'string' ? o.v : null;
        } catch (e) {
            return null;
        }
    }

    function publicar(refresh, valor) {
        try {
            podar();
            localStorage.setItem(PREFIJO + refresh, JSON.stringify({ t: Date.now(), v: valor }));
        } catch (e) {
            /* Modo privado o almacenamiento lleno: sin buzón, una pestaña duplicada
               puede volver a perder la sesión, como antes. */
        }
    }

    function podar() {
        const ahora = Date.now();
        for (let i = localStorage.length - 1; i >= 0; i--) {
            const k = localStorage.key(i);
            if (!k || !k.startsWith(PREFIJO)) continue;
            try {
                const o = JSON.parse(localStorage.getItem(k));
                if (!o || typeof o.t !== 'number' || ahora - o.t > VIDA_MS) localStorage.removeItem(k);
            } catch (e) {
                localStorage.removeItem(k);
            }
        }
    }

    /* ---------- El grupo de ventanas de una sesión (2026-11-17) ----------
       Las ventanas que comparten una sesión (duplicadas, o abiertas con «Abrir en
       ventana nueva») anotan aquí su última actividad y, si la persona cierra
       sesión en una, la marca de cerrada. Cada ventana lo lee en su latido. */
    const GRUPO = 'ingenia365:grupo:';

    function leerGrupo(grupo) {
        try {
            const o = JSON.parse(localStorage.getItem(GRUPO + grupo) || 'null');
            if (!o) return null;
            return JSON.stringify({ ultimaActividad: o.a ? new Date(o.a).toISOString() : null, cerrada: !!o.c });
        } catch (e) {
            return null;
        }
    }

    function escribirGrupo(grupo, cambio) {
        try {
            podarGrupos();
            const o = JSON.parse(localStorage.getItem(GRUPO + grupo) || 'null') || {};
            Object.assign(o, cambio, { t: Date.now() });
            localStorage.setItem(GRUPO + grupo, JSON.stringify(o));
        } catch (e) {
            /* Sin almacenamiento cada ventana cuenta lo suyo, como antes. */
        }
    }

    function anunciarActividad(grupo, cuandoIso) {
        const cuando = Date.parse(cuandoIso);
        if (isNaN(cuando)) return;
        let previa = 0;
        try { previa = (JSON.parse(localStorage.getItem(GRUPO + grupo) || 'null') || {}).a || 0; } catch (e) { previa = 0; }
        if (cuando > previa) escribirGrupo(grupo, { a: cuando });
    }

    function anunciarCierre(grupo) {
        escribirGrupo(grupo, { c: true });
    }

    function podarGrupos() {
        const ahora = Date.now();
        for (let i = localStorage.length - 1; i >= 0; i--) {
            const k = localStorage.key(i);
            if (!k || !k.startsWith(GRUPO)) continue;
            try {
                const o = JSON.parse(localStorage.getItem(k));
                if (!o || typeof o.t !== 'number' || ahora - o.t > VIDA_MS) localStorage.removeItem(k);
            } catch (e) {
                localStorage.removeItem(k);
            }
        }
    }

    return { tomar, soltar, leer, publicar, leerGrupo, anunciarActividad, anunciarCierre };
})();
