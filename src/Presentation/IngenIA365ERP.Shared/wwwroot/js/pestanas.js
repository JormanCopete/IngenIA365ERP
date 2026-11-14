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

    return { tomar, soltar, leer, publicar };
})();
